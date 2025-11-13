Shader "Hidden/_Pulse_Bloom"
{
    HLSLINCLUDE
        #pragma exclude_renderers gles

        // Include neccessary libraries

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "PulseCommon.hlsl"
        #include "PulseBlurTypes.hlsl"
        //#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // Porperties from renderer feature

        half _Threshold;
        half _Intensity;
        half _Scatter;
        half _Quality;

        TEXTURE2D(_DefaultTexture);

        // The fragment shader definition.            
        half4 ClipPass(Varyings input) : SV_Target
        {
            half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, _sampler_Linear_Clamp, input.texcoord);
            half4 sub = Luminance(col) > _Threshold ? col : 0;
            return sub;
        }

        half4 BlurPassH(Varyings input) : SV_Target
        {
            return LinearBlur(input.texcoord, float2(_Scatter, 0), _Quality);
        }

        half4 BlurPassV(Varyings input) : SV_Target
        {
            return LinearBlur(input.texcoord, float2(0, _Scatter), _Quality);
        }

        half4 BilinearPass(Varyings input) : SV_Target
        {
            half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, _sampler_Linear_Clamp, input.texcoord);
            return col;
        }

        half4 BloomPass(Varyings input) : SV_Target
        {
            half4 oldCol = SAMPLE_TEXTURE2D_X(_DefaultTexture, _sampler_Linear_Clamp, input.texcoord);
            half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, _sampler_Linear_Clamp, input.texcoord) * _Intensity;
            return col + oldCol;
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
            Name "Pulse_Clip_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment ClipPass

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_BlurH_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment BlurPassH

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_BlurV_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment BlurPassV

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_Bilinear_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment BilinearPass

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_Bloom_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment BloomPass

            ENDHLSL
        }
    }
}
