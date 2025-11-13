Shader "Hidden/_Pulse_FSR"  // Implementation from https://www.shadertoy.com/view/stXSWB
{
    HLSLINCLUDE
        #pragma exclude_renderers gles

        // Include neccessary libraries

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "PulseCommon.hlsl"
        #include "fxFSR.hlsl"
        //#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // Porperties from renderer feature

        half2 _SrcSize;
        half2 _DstSize;
        half _Sharpness;  // 0 - 1

        // Fargment shader definitons
        half4 fxFSRPass(Varyings input) : SV_Target
        {
            half4 col;

            //input.texcoord.y = -input.texcoord.y;
            Main_fxFSR(_BlitTexture, input.texcoord * _BlitScaleBias.xy + _BlitScaleBias.zw, _Sharpness, _SrcSize, _DstSize, col);

            //half4 oldCol = SAMPLE_TEXTURE2D_X(_DefaultTexture, _sampler_Linear_Clamp, input.texcoord);
            //half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, _sampler_Linear_Clamp, input.texcoord) * _Intensity;
            return col;
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
            Name "Pulse_FSR_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment fxFSRPass

            ENDHLSL
        }
    }
}
