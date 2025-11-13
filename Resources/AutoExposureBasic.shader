Shader "Hidden/_Pulse_AutoExposure"
{
    HLSLINCLUDE
        #pragma exclude_renderers gles

        // Include neccessary libraries

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "PulseCommon.hlsl"
        #include "AutoExposure/ExposureCommon.hlsl"
        //#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // Porperties from renderer feature

        float _exposureCompenastion;
        TEXTURE2D_X(_Exposure);

        float GetExposureMultiplier(float avgLuminance)
        {
            avgLuminance = max(EPSILON, avgLuminance);
            //float keyValue = 1.03 - (2.0 / (2.0 + log2(avgLuminance + 1.0)));
            float keyValue = _exposureCompenastion;// _Params2.z;
            float exposure = keyValue / avgLuminance;
            return exposure;
        }

        // from: https://bruop.github.io/exposure/
        //float _s; // Sensor sensitivity (S = 100)
        //float _k; // reflected-light meter calibration constant (K = 12.5)
        //float _q; // lens and vignetting attentuation (q = 0.65)
        //float _h; // exposure (H)
        // This simplifies to: Lmax = 9.6 * Lavg;
        // L' = H * L = L/(9.6 * Lavg)

        // The fragment shader definition.
        float4 lodAutoExposure(Varyings input) : SV_Target
        {
            float4 col = sampleBlit(input.texcoord);
            float avgLum =  SAMPLE_TEXTURE2D_X(_Exposure, _sampler_Linear_Clamp, input.texcoord).x;

            return col * GetExposureMultiplier(avgLum);
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
            Name "Pulse_GammaCorrection_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment lodAutoExposure

            ENDHLSL
        }
    }
}
