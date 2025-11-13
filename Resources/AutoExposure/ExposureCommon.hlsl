#ifndef EXPOSURE_COMMON
#define EXPOSURE_COMMON

#define EPSILON         1.0e-4

half Luminance(half3 linearRgb)
{
    return dot(linearRgb, float3(0.2126729, 0.7151522, 0.0721750));
}

half Luminance(half4 linearRgba)
{
    return Luminance(linearRgba.rgb);
}

#endif // EXPOSURE_COMMON