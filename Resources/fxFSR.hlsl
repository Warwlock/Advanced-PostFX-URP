#ifndef _PULSE_FSR_INCLUDED
#define _PULSE_FSR_INCLUDED

    /*
    * FidelityFX Super Resolution scales up a low resolution
    * image, while adding fine detail.
    *
    * MIT Open License
    *
    * https://gpuopen.com/fsr
    *
    *
    * It works in two passes-
    *   EASU upsamples the image with a clamped Lanczos kernel.
    *   RCAS sharpens the image at the target resolution.
    *
    * I needed to make a few changes to improve readability and
    * WebGL compatibility in an algorithm I don't fully understand.
    * Expect bugs.
    *
    * Shader not currently running for WebGL1 targets (eg. mobile Safari)
    *
    * There is kind of no point to using FSR in Shadertoy, as it renders buffers
    * at full target resolution. But this might be useful for WebGL based demos
    * running smaller-than-target render buffers.
    *
    * For sharpening with a full resolution render buffer,
    * FidelityFX CAS is a better option.
    * https://www.shadertoy.com/view/ftsXzM
    *
    * For readability and compatibility, these optimisations have been removed:
    *   * Fast approximate inverse and inversesqrt
    *   * textureGather fetches (not WebGL compatible)
    *   * Multiplying by reciprocal instead of division
    *
    * Apologies to AMD for the numerous slowdowns and errors I have introduced.
    * Link: https://www.shadertoy.com/view/stXSWB
    */

    SamplerState my_linear_clamp_sampler;

    //Source Image Size:
    static float2 srcSize;
    //Destintion Screen Size:
    static float2 dstSize;

    /* EASU stage
    * This takes a reduced resolution source, and scales it up while preserving detail.
    */

    float3 FsrEasuCF(Texture2D _tex, float2 p)
    {
        return _tex.Sample(my_linear_clamp_sampler, p).xyz;
    }

    /**** EASU ****/

    void FsrEasuCon(
        out float4 con0,
        out float4 con1,
        out float4 con2,
        out float4 con3,
        // This the rendered image resolution being upscaled
        float2 inputViewportInPixels,
        // This is the resolution of the resource containing the input image (useful for dynamic resolution)
        float2 inputSizeInPixels,
        // This is the display resolution which the input image gets upscaled to
        float2 outputSizeInPixels
    )
    {
        // Output integer position to a pixel position in viewport.
        con0 = float4(
            inputViewportInPixels.x/outputSizeInPixels.x,
            inputViewportInPixels.y/outputSizeInPixels.y,
            .5*inputViewportInPixels.x/outputSizeInPixels.x-.5,
            .5*inputViewportInPixels.y/outputSizeInPixels.y-.5
        );

        // Viewport pixel position to normalized image space.
        // This is used to get upper-left of 'F' tap.
        con1 = float4(1,1,1,-1)/inputSizeInPixels.xyxy;
        // Centers of gather4, first offset from upper-left of 'F'.
        //      +---+---+
        //      |   |   |
        //      +--(0)--+
        //      | b | c |
        //  +---F---+---+---+
        //  | e | f | g | h |
        //  +--(1)--+--(2)--+
        //  | i | j | k | l |
        //  +---+---+---+---+
        //      | n | o |
        //      +--(3)--+
        //      |   |   |
        //      +---+---+
        // These are from (0) instead of 'F'.
        con2 = float4(-1,2,1,2)/inputSizeInPixels.xyxy;
        con3 = float4(0,4,0,0)/inputSizeInPixels.xyxy;
    }

    // Filtering for a given tap for the scalar.
    void FsrEasuTapF(
        inout float3 aC, // Accumulated color, with negative lobe.
        inout float aW, // Accumulated weight.
        float2 off, // Pixel offset from resolve position to tap.
        float2 dir, // Gradient direction.
        float2 len, // Length.
        float lob, // Negative lobe strength.
        float clp, // Clipping point.
        float3 c
    )
    {
        // Tap color.
        // Rotate offset by direction.
        float2 v = float2(dot(off, dir), dot(off,float2(-dir.y,dir.x)));
        // Anisotropy.
        v *= len;
        // Compute distance^2.
        float d2 = min(dot(v,v),clp);
        // Limit to the window as at corner, 2 taps can easily be outside.
        // Approximation of lancos2 without sin() or rcp(), or sqrt() to get x.
        //  (25/16 * (2/5 * x^2 - 1)^2 - (25/16 - 1)) * (1/4 * x^2 - 1)^2
        //  |_______________________________________|   |_______________|
        //                   base                             window
        // The general form of the 'base' is,
        //  (a*(b*x^2-1)^2-(a-1))
        // Where 'a=1/(2*b-b^2)' and 'b' moves around the negative lobe.
        float wB = .4 * d2 - 1.;
        float wA = lob * d2 -1.;
        wB *= wB;
        wA *= wA;
        wB = 1.5625*wB-.5625;
        float w=  wB * wA;
        // Do weighted average.
        aC += c*w;
        aW += w;
    }

    //------------------------------------------------------------------------------------------------------------------------------
    // Accumulate direction and length.
    void FsrEasuSetF(
        inout float2 dir,
        inout float len,
        float w,
        float lA,float lB,float lC,float lD,float lE
    )
    {
        // Direction is the '+' diff.
        //    a
        //  b c d
        //    e
        // Then takes magnitude from abs average of both sides of 'c'.
        // Length converts gradient reversal to 0, smoothly to non-reversal at 1, shaped, then adding horz and vert terms.
        float lenX = max(abs(lD - lC), abs(lC - lB));
        float dirX = lD - lB;
        dir.x += dirX * w;
        lenX = clamp(abs(dirX)/lenX,0.,1.);
        lenX *= lenX;
        len += lenX * w;
        // Repeat for the y axis.
        float lenY = max(abs(lE - lC), abs(lC - lA));
        float dirY = lE - lA;
        dir.y += dirY * w;
        lenY = clamp(abs(dirY) / lenY,0.,1.);
        lenY *= lenY;
        len += lenY * w;
    }

    //------------------------------------------------------------------------------------------------------------------------------
    void FsrEasuF(
        out float3 pix,
        Texture2D _tex,
        float2 ip, // Integer pixel position in output.
        // Constants generated by FsrEasuCon().
        float4 con0, // xy = output to input scale, zw = first pixel offset correction
        float4 con1,
        float4 con2,
        float4 con3
    )
    {
        //------------------------------------------------------------------------------------------------------------------------------
        // Get position of 'f'.
        float2 pp = ip * con0.xy + con0.zw; // Corresponding input pixel/subpixel
        float2 fp = floor(pp);// fp = source nearest pixel
        pp -= fp; // pp = source subpixel

        //------------------------------------------------------------------------------------------------------------------------------
        // 12-tap kernel.
        //    b c
        //  e f g h
        //  i j k l
        //    n o
        // Gather 4 ordering.
        //  a b
        //  r g
        float2 p0 = fp * con1.xy + con1.zw;
    
        // These are from p0 to avoid pulling two constants on pre-Navi hardware.
        float2 p1 = p0 + con2.xy;
        float2 p2 = p0 + con2.zw;
        float2 p3 = p0 + con3.xy;

        // TextureGather is not available on WebGL2
        float4 off = float4(-.5,.5,-.5,.5)*con1.xxyy;
        // textureGather to texture offsets
        // x=west y=east z=north w=south
        float3 bC = FsrEasuCF(_tex, p0 + off.xw); float bL = bC.g + 0.5 *(bC.r + bC.b);
        float3 cC = FsrEasuCF(_tex, p0 + off.yw); float cL = cC.g + 0.5 *(cC.r + cC.b);
        float3 iC = FsrEasuCF(_tex, p1 + off.xw); float iL = iC.g + 0.5 *(iC.r + iC.b);
        float3 jC = FsrEasuCF(_tex, p1 + off.yw); float jL = jC.g + 0.5 *(jC.r + jC.b);
        float3 fC = FsrEasuCF(_tex, p1 + off.yz); float fL = fC.g + 0.5 *(fC.r + fC.b);
        float3 eC = FsrEasuCF(_tex, p1 + off.xz); float eL = eC.g + 0.5 *(eC.r + eC.b);
        float3 kC = FsrEasuCF(_tex, p2 + off.xw); float kL = kC.g + 0.5 *(kC.r + kC.b);
        float3 lC = FsrEasuCF(_tex, p2 + off.yw); float lL = lC.g + 0.5 *(lC.r + lC.b);
        float3 hC = FsrEasuCF(_tex, p2 + off.yz); float hL = hC.g + 0.5 *(hC.r + hC.b);
        float3 gC = FsrEasuCF(_tex, p2 + off.xz); float gL = gC.g + 0.5 *(gC.r + gC.b);
        float3 oC = FsrEasuCF(_tex, p3 + off.yz); float oL = oC.g + 0.5 *(oC.r + oC.b);
        float3 nC = FsrEasuCF(_tex, p3 + off.xz); float nL = nC.g + 0.5 *(nC.r + nC.b);
   
        //------------------------------------------------------------------------------------------------------------------------------
        // Simplest multi-channel approximate luma possible (luma times 2, in 2 FMA/MAD).
        // Accumulate for bilinear interpolation.
        float2 dir = float2(0, 0);
        float len = 0.;

        FsrEasuSetF(dir, len, (1.-pp.x)*(1.-pp.y), bL, eL, fL, gL, jL);
        FsrEasuSetF(dir, len,    pp.x  *(1.-pp.y), cL, fL, gL, hL, kL);
        FsrEasuSetF(dir, len, (1.-pp.x)*  pp.y  , fL, iL, jL, kL, nL);
        FsrEasuSetF(dir, len,    pp.x  *  pp.y  , gL, jL, kL, lL, oL);

        //------------------------------------------------------------------------------------------------------------------------------
        // Normalize with approximation, and cleanup close to zero.
        float2 dir2 = dir * dir;
        float dirR = dir2.x + dir2.y;
        bool zro = dirR < (1.0/32768.0);
        dirR = rsqrt(dirR);
        dirR = zro ? 1.0 : dirR;
        dir.x = zro ? 1.0 : dir.x;
        dir *= float2(dirR, dirR);
        // Transform from {0 to 2} to {0 to 1} range, and shape with square.
        len = len * 0.5;
        len *= len;
        // Stretch kernel {1.0 vert|horz, to sqrt(2.0) on diagonal}.
        float stretch = dot(dir,dir) / (max(abs(dir.x), abs(dir.y)));
        // Anisotropic length after rotation,
        //  x := 1.0 lerp to 'stretch' on edges
        //  y := 1.0 lerp to 2x on edges
        float2 len2 = float2(1. +(stretch-1.0)*len, 1. -.5 * len);
        // Based on the amount of 'edge',
        // the window shifts from +/-{sqrt(2.0) to slightly beyond 2.0}.
        float lob = .5 - .29 * len;
        // Set distance^2 clipping point to the end of the adjustable window.
        float clp = 1./lob;

        //------------------------------------------------------------------------------------------------------------------------------
        // Accumulation mixed with min/max of 4 nearest.
        //    b c
        //  e f g h
        //  i j k l
        //    n o
        float3 min4 = min(min(fC,gC),min(jC,kC));
        float3 max4 = max(max(fC,gC),max(jC,kC));
        // Accumulation.
        float3 aC = float3(0, 0, 0);
        float aW = 0.;
        FsrEasuTapF(aC, aW, float2( 0,-1)-pp, dir, len2, lob, clp, bC);
        FsrEasuTapF(aC, aW, float2( 1,-1)-pp, dir, len2, lob, clp, cC);
        FsrEasuTapF(aC, aW, float2(-1, 1)-pp, dir, len2, lob, clp, iC);
        FsrEasuTapF(aC, aW, float2( 0, 1)-pp, dir, len2, lob, clp, jC);
        FsrEasuTapF(aC, aW, float2( 0, 0)-pp, dir, len2, lob, clp, fC);
        FsrEasuTapF(aC, aW, float2(-1, 0)-pp, dir, len2, lob, clp, eC);
        FsrEasuTapF(aC, aW, float2( 1, 1)-pp, dir, len2, lob, clp, kC);
        FsrEasuTapF(aC, aW, float2( 2, 1)-pp, dir, len2, lob, clp, lC);
        FsrEasuTapF(aC, aW, float2( 2, 0)-pp, dir, len2, lob, clp, hC);
        FsrEasuTapF(aC, aW, float2( 1, 0)-pp, dir, len2, lob, clp, gC);
        FsrEasuTapF(aC, aW, float2( 1, 2)-pp, dir, len2, lob, clp, oC);
        FsrEasuTapF(aC, aW, float2( 0, 2)-pp, dir, len2, lob, clp, nC);
        //------------------------------------------------------------------------------------------------------------------------------
        // Normalize and dering.
        pix=min(max4,max(min4,aC/aW));
    }

    float4 EASUmain(Texture2D _tex, float2 uv)
    {
        float3 c;
        float4 con0,con1,con2,con3;
        FsrEasuCon(
            con0, con1, con2, con3, srcSize, srcSize, dstSize
        );
        float2 fragCoord = float2(uv.x * dstSize.x, uv.y * dstSize.y);
        FsrEasuF(c, _tex, fragCoord, con0, con1, con2, con3);
        return float4(c.xyz, 1);
    }

    /***** RCAS *****/
    #define FSR_RCAS_LIMIT (0.25-(1.0/16.0))
    //#define FSR_RCAS_DENOISE

    // Input callback prototypes that need to be implemented by calling shader (?)
    float4 FsrRcasLoadF(Texture2D _tex, float2 p){
        return EASUmain(_tex, p/dstSize.xy);
    }
    //------------------------------------------------------------------------------------------------------------------------------
    void FsrRcasCon(
        out float con,
        // The scale is {0.0 := maximum, to N>0, where N is the number of stops (halving) of the reduction of sharpness}.
        float sharpness
    ){
        // Transform from stops to linear value.
        con = exp2(-sharpness);
    }

    float3 FsrRcasF(
        Texture2D _tex,
        float2 ip, // Integer pixel position in output.
        float con
    )
    {
        // Constant generated by RcasSetup().
        // Algorithm uses minimal 3x3 pixel neighborhood.
        //    b 
        //  d e f
        //    h
        float2 sp = float2(ip);
        float3 b = FsrRcasLoadF(_tex, sp + float2( 0,-1)).rgb;
        float3 d = FsrRcasLoadF(_tex, sp + float2(-1, 0)).rgb;
        float3 e = FsrRcasLoadF(_tex, sp).rgb;
        float3 f = FsrRcasLoadF(_tex, sp+float2( 1, 0)).rgb;
        float3 h = FsrRcasLoadF(_tex, sp+float2( 0, 1)).rgb;
        // Luma times 2.
        float bL = b.g + .5 * (b.b + b.r);
        float dL = d.g + .5 * (d.b + d.r);
        float eL = e.g + .5 * (e.b + e.r);
        float fL = f.g + .5 * (f.b + f.r);
        float hL = h.g + .5 * (h.b + h.r);
        // Noise detection.
        float nz = .25 * (bL + dL + fL + hL) - eL;
        nz=clamp(
            abs(nz)
            /(
                max(max(bL,dL),max(eL,max(fL,hL)))
                -min(min(bL,dL),min(eL,min(fL,hL)))
            ),
            0., 1.
        );
        nz=1.-.5*nz;
        // Min and max of ring.
        float3 mn4 = min(b, min(f, h));
        float3 mx4 = max(b, max(f, h));
        // Immediate constants for peak range.
        float2 peakC = float2(1., -4.);
        // Limiters, these need to be high precision RCPs.
        float3 hitMin = mn4 / (4. * mx4);
        float3 hitMax = (peakC.x - mx4) / (4.* mn4 + peakC.y);
        float3 lobeRGB = max(-hitMin, hitMax);
        float lobe = max(
            -FSR_RCAS_LIMIT,
            min(max(lobeRGB.r, max(lobeRGB.g, lobeRGB.b)), 0.)
        )*con;
        // Apply noise removal.
        #ifdef FSR_RCAS_DENOISE
        lobe *= nz;
        #endif
        // Resolve, which needs the medium precision rcp approximation to avoid visible tonality changes.
        return (lobe * (b + d + h + f) + e) / (4. * lobe + 1.);
    }

    void Main_fxFSR(Texture2D _MainTex, float2 uv, float lerpVal, float2 srcSizeIn, float2 dstSizeIn, out float4 col)
    {
        srcSize = srcSizeIn;
        dstSize = dstSizeIn;

        // Set up constants
        float con;
        float xSharp = lerpVal; // range 0-1;
        float sharpness = 2-2*pow(xSharp,.25); //0.2 (default (?));
        FsrRcasCon(con,sharpness);

        // Perform RCAS pass
        float2 fragCoord = float2(uv.x * dstSize.x, uv.y * dstSize.y);
        float3 col1 = FsrRcasF(_MainTex, fragCoord, con);

        col = float4(col1, 0);
    }

#endif