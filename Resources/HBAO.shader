Shader "Hidden/_Pulse_HBAO"
{
    HLSLINCLUDE
        #pragma exclude_renderers gles

        // Include neccessary libraries

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "PulseCommon.hlsl"
        //#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // Porperties from renderer feature

        float UNITY_PI = 3.14159265358979;
        float _AOStrength;
        float _AORadius;
        float _AOBias;
        float _EnableBlur;

        float2 _InvTextureSize;

        TEXTURE2D_X(_AOTexture);

        float getRawDepth(float2 uv) { return sampleDepthLod(uv, 0); }

        float3 FetchViewPos(float2 uv)
        {
            float3 viewSpaceRay = mul(unity_CameraInvProjection, float4(uv * 2.0 - 1.0, 1.0, 1.0) * _ProjectionParams.z).xyz;
            float rawDepth = getRawDepth(uv);
            return viewSpaceRay * Linear01Depth(rawDepth, _ZBufferParams);
        }

        float FallOff(float dist)
        {
            return saturate(1 - dist * dist / (_AORadius * _AORadius));
        }

        float TanToSin(float x)
        {
            return x / sqrt(x * x + 1.0);
        }

        float ViewPosTan(float3 V)
        {
            return V.z;
        }

        float BiasedViewPosTan(float3 V, float bias)
        {
            //bias [0,1]
            float tangentBias = tan(bias * 0.5 * UNITY_PI);
            return ViewPosTan(V) + tangentBias;
        }

        float fullAO(float3 pos, float3 stepPos, float3 tangentVec, inout float top)
        {
            float3 h = stepPos - pos;
            float3 h_dir = normalize(h);
            float tanH = ViewPosTan(h_dir);
            float sinH = TanToSin(tanH);
            float tanT = BiasedViewPosTan(tangentVec, _AOBias);
            float sinT = TanToSin(tanT);
            float dist = length(h);
            float sinBlock = sinH - sinT;
            float diff = max(sinBlock - top, 0);
            top = max(sinBlock, top);
            return diff * FallOff(dist);
        }

        // very bad noise
        // from here https://forum.unity.com/threads/generate-random-float-between-0-and-1-in-shader.610810/
        float random(float2 uv)
        {
            return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453123);
        }

        // The fragment shader definition.            
        float4 HBAOFrag(Varyings input) : SV_Target
        {
            float ao = 0;

            float3 viewPosition = FetchViewPos(input.texcoord);

            float rnd = random(input.texcoord);

            const int NumDirs = 4;
            float delta = 2.0 * UNITY_PI / (NumDirs + 1);
            const int NumSteps = 6;

            float stepSize = _AORadius / abs(viewPosition.z);
            //The maximum step radius is divided by ViewPos. If it is less than 1, it means that this area is too far and the AO influence is very small.
            if (stepSize < 1.0) return 1.0;
            stepSize /= NumSteps;


            float InitialAngle = delta * rnd;
            for (int i = 0; i < NumDirs; i++)
            {
                float angle = InitialAngle + delta * i;
                float cos, sin;
                sincos(angle, sin, cos);
                float2 dir = float2(cos, sin);
                float rayPixel = 1;
                float top = 0;
                float3 tangentVec = FetchViewPos(input.texcoord + dir * _InvTextureSize) - FetchViewPos(input.texcoord - dir * _InvTextureSize);
                tangentVec = normalize(tangentVec);

                for (int j = 0; j < NumSteps; ++j)
                {
                    float2 stepUV = rayPixel * dir * _InvTextureSize + input.texcoord;
                    float3 stepViewPos = FetchViewPos(stepUV);
                    ao += fullAO(viewPosition, stepViewPos, tangentVec, top);
                    rayPixel += stepSize;
                }
            }

            ao /= float(NumDirs);
            ao = ao * _AOStrength;
            return saturate(1 - ao);
        }

        float4 HBAOMergeFrag(Varyings input) : SV_Target
        {
            float ao = SAMPLE_TEXTURE2D_X(_AOTexture, _sampler_Linear_Clamp, input.texcoord).x;
            float4 color = sampleBlit(input.texcoord);
            return float4(color.rgb * ao, color.a);
        }


        //9x9 gaussian blur
        //https://rastergrid.com/blog/2010/09/efficient-gaussian-blur-with-linear-sampling/

        float4 HorizontalBlurFrag(Varyings input) : SV_Target
        {
            const float3 offset = float3(0.0f, 1.3846153846f, 3.2307692308f);
            const float3 weight = float3(0.2270270270f, 0.3162162162f, 0.0702702703f);
            float4 centerColor = sampleBlit(input.texcoord) * weight.x;

            centerColor += sampleBlit(input.texcoord + float2(0.0, offset.y) * _Blit_TexelSize.xy) * weight.y;
            centerColor += sampleBlit(input.texcoord - float2(0.0, offset.y) * _Blit_TexelSize.xy) * weight.y;

            centerColor += sampleBlit(input.texcoord + float2(0.0, offset.z) * _Blit_TexelSize.xy) * weight.z;
            centerColor += sampleBlit(input.texcoord - float2(0.0, offset.z) * _Blit_TexelSize.xy) * weight.z;

            return centerColor;
        }

        float4 VerticalBlurFrag(Varyings input) : SV_Target
        {
            const float3 offset = float3(0.0f, 1.3846153846f, 3.2307692308f);
            const float3 weight = float3(0.2270270270f, 0.3162162162f, 0.0702702703f);
            float4 centerColor = sampleBlit(input.texcoord) * weight.x;

            centerColor += sampleBlit(input.texcoord + float2(offset.y, 0.0) * _Blit_TexelSize.xy) * weight.y;
            centerColor += sampleBlit(input.texcoord - float2(offset.y, 0.0) * _Blit_TexelSize.xy) * weight.y;

            centerColor += sampleBlit(input.texcoord + float2(offset.z, 0.0) * _Blit_TexelSize.xy) * weight.z;
            centerColor += sampleBlit(input.texcoord - float2(offset.z, 0.0) * _Blit_TexelSize.xy) * weight.z;
            return centerColor;
        }

        //3x3 gaussian blur
        float4 Tap4BlurFrag(Varyings input) : SV_Target
        {
            const float4 duv = _Blit_TexelSize.xyxy * float4(0.5, 0.5, -0.5, 0);
            half4 acc;
            acc = sampleBlit(input.texcoord - duv.xy);
            acc += sampleBlit(input.texcoord - duv.zy);
            acc += sampleBlit(input.texcoord + duv.zy);
            acc += sampleBlit(input.texcoord + duv.xy);
            return acc * 0.25f;
        }

    ENDHLSL

    SubShader
    {
        Tags
        { 
            "RenderType" = "Opaque" "RenderPipeline"="UniversalPipeline"
        }
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "Pulse_HBAO_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment HBAOFrag

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_HBAOMerge_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment HBAOMergeFrag

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_HorizontalBlur_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment HorizontalBlurFrag

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_VerticalBlur_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment VerticalBlurFrag

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_TapBlur_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment Tap4BlurFrag

            ENDHLSL
        }
    }
}
