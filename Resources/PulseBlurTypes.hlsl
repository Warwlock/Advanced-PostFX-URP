#ifndef _PULSE_BLURTYPE_INCLUDED
#define _PULSE_BLURTYPE_INCLUDED

// Blur functions

half4 BoxBlur(half2 uv, half strg, half dist) // dist = inverseqrt(SAMPLES); (invSqrtSAMPLES)
{
    half2 texel = _Blit_TexelSize.xy;

    half4 total = 0;
    half accum = 0;
  
    for(half i = -0.5; i <= 0.5; i += dist)
    {
        for(half j = -0.5; j <= 0.5; j += dist)
        {
            half2 coord = uv + half2(i, j) * strg * texel;
            total += sampleBlit(coord);
            accum++;
        }
    }

    return total / accum;// * dist * dist;
}

half4 LinearBlur(half2 uv, half2 dir, half dist) // dist = 1 / SAMPLES; (invSAMPLES)
{
    if (dist < 0.01)
        dist = 0.01;
    
    half2 texel = _Blit_TexelSize.xy;

    half4 total = 0;
    half accum = 0;

    for(half i = -0.5; i <= 0.5; i += dist)
    {
        half2 coord = uv + i * dir * texel;
        total += sampleBlit(coord);
        accum++;
    }

    return total / accum;// * dist;
}

half4 RadialBlur(half2 uv, half strg, half dist) // dist = 1 / SAMPLES; (invSAMPLES)
{
    half2 texel = _Blit_TexelSize.xy;

    half4 total = 0;

    half rad = strg * length(texel);
    for(half i = 0.0; i <= 1.0; i += dist)
    {
        half2 coord = (uv - 0.5) / (1.0 + rad * i) + 0.5;
        total += sampleBlit(coord);
    }

    return total * dist;
}

// Rotation matrix:
//
// half2 dir = half2(cos(strg * dist), sin(strg * dist)); strg = angle
// half2x2 rotMatrix = half2x2(dir.xy, -dir.y, dir.x);
//
half4 AngularBlur(half2 uv, half strg, half dist, half2x2 rotMatrix) // dist = 1 / SAMPLES; (invSAMPLES)
{
    half4 total = 0;
    half2 coord = uv - 0.5;

    for(float i = 0.0; i <= 1.0; i += dist)
    {
        total += sampleBlit(coord + 0.5);
        coord = mul(coord, rotMatrix);
    }
    
    return total * dist;
}

half gaussian(half2 i, half Samples)
{
    return exp(-0.5 * dot(i /= Samples * 0.25, i) / (6.28 * Samples * Samples * 0.0625));
}

half4 GaussianBlur(half2 uv, half lod, half sLod, half dist, half Samples) // dist = SAMPLES / sLOD; (lodSAMPLES)
{
    half2 texel = _Blit_TexelSize.xy;

    half4 total = 0;
    half accum = 0;

    for(int i = 0; i < dist * dist; i++)
    {
        half2 d = half2(i % dist, i / dist) * sLod - Samples / 2;
        half weight = gaussian(d, Samples);
        total += weight * sampleBlitLod(uv + texel * d, lod);
        accum += weight;
    }

    return total / accum;
}

#endif