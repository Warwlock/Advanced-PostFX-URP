Shader "Hidden/_DifferenceOfGaussians"
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

        //#define PI 3.14159265358979323846f

        TEXTURE2D_X(_GaussianTex);

        int _GaussianKernelSize, _Thresholding, _Invert, _Tanh;
        float _Sigma, _Threshold, _K, _Tau, _Phi;

        float gaussian(float sigma, float pos) {
            return (1.0f / sqrt(2.0f * PI * sigma * sigma)) * exp(-(pos * pos) / (2.0f * sigma * sigma));
        }

        float luminance(float3 color) {
            return dot(color, float3(0.299f, 0.587f, 0.114f));
        }

        // The fragment shader definition.            
        float4 Blur1frag(Varyings input) : SV_Target
        {
            float2 col = 0;
            float kernelSum1 = 0.0f;
            float kernelSum2 = 0.0f;

            for (int x = -_GaussianKernelSize; x <= _GaussianKernelSize; ++x) {
                float c = luminance(sampleBlit(input.texcoord + float2(x, 0) * _Blit_TexelSize.xy).xyz);
                float gauss1 = gaussian(_Sigma, x);
                float gauss2 = gaussian(_Sigma * _K, x);

                col.r += c * gauss1;
                kernelSum1 += gauss1;

                col.g += c * gauss2;
                kernelSum2 += gauss2;
            }

            return float4(col.r / kernelSum1, col.g / kernelSum2, 0, 0);
        }

        float4 Blur2frag(Varyings input) : SV_Target
        {
            float2 col = 0;
            float kernelSum1 = 0.0f;
            float kernelSum2 = 0.0f;

            for (int y = -_GaussianKernelSize; y <= _GaussianKernelSize; ++y) {
                float4 c = sampleBlit(input.texcoord + float2(0, y) * _Blit_TexelSize.xy);
                float gauss1 = gaussian(_Sigma, y);
                float gauss2 = gaussian(_Sigma * _K, y);

                col.r += c.r * gauss1;
                kernelSum1 += gauss1;

                col.g += c.g * gauss2;
                kernelSum2 += gauss2;
            }

            return float4(col.r / kernelSum1, col.g / kernelSum2, 0, 0);
        }

        float4 DOGfrag(Varyings input) : SV_Target
        {
            float2 G = SAMPLE_TEXTURE2D_X(_GaussianTex, _sampler_Linear_Clamp, input.texcoord).rg;

            float4 D = (G.r - _Tau * G.g);

            if (_Thresholding) {
                if (_Tanh)
                    D = (D >= _Threshold) ? 1 : 1 + tanh(_Phi * (D - _Threshold));
                else
                    D = (D >= _Threshold) ? 1 : 0;
            }

            if (_Invert)
                D = 1 - D;

            return D;
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
            Name "Pulse_DoG_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment Blur1frag

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_DoG_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment Blur2frag

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_DoG_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment DOGfrag

            ENDHLSL
        }
    }
}
