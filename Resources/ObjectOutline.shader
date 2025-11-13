Shader "Hidden/_Pulse_ObjectOutline"
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

        TEXTURE2D(_CustomDepthTexture);
        float _OutlineThickness;
        float4 _OutlineColor;
        float _OutlineDepthMultiplier;
        float _OutlineDepthBias;

        float SobelDepth(float ldc, float ldl, float ldr, float ldu, float ldd)
        {
            return abs(ldl - ldc) +
                abs(ldr - ldc) +
                abs(ldu - ldc) +
                abs(ldd - ldc);
        }

        float SobelSampleDepth(Texture2D t, SamplerState s, float2 uv, float3 offset)
        {
            float pixelCenter = LinearEyeDepth(t.Sample(s, uv).r, _ZBufferParams);
            float pixelLeft = LinearEyeDepth(t.Sample(s, uv - offset.xz).r, _ZBufferParams);
            float pixelRight = LinearEyeDepth(t.Sample(s, uv + offset.xz).r, _ZBufferParams);
            float pixelUp = LinearEyeDepth(t.Sample(s, uv + offset.zy).r, _ZBufferParams);
            float pixelDown = LinearEyeDepth(t.Sample(s, uv - offset.zy).r, _ZBufferParams);

            return SobelDepth(pixelCenter, pixelLeft, pixelRight, pixelUp, pixelDown);
        }

        // The fragment shader definition.            
        float4 frag(Varyings input) : SV_Target
        {
            float3 offset = float3((1.0 / _ScreenParams.x), (1.0 / _ScreenParams.y), 0.0) * _OutlineThickness;
            float4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, _sampler_Linear_Clamp, input.texcoord);
            
            float sobelDepth = SobelSampleDepth(_CustomDepthTexture, _sampler_Linear_Clamp, input.texcoord.xy, offset);
            sobelDepth = pow(saturate(sobelDepth) * _OutlineDepthMultiplier, _OutlineDepthBias);

            float3 outlineColor = lerp(col, _OutlineColor.rgb, _OutlineColor.a);
            float3 color = lerp(col, outlineColor, sobelDepth);
            
            return float4(color, 1.0);
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
            Name "Pulse_Vignette_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            ENDHLSL
        }
    }
}
