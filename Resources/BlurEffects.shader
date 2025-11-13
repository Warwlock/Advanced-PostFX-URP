Shader "Hidden/_Pulse_BlurEffects"
{
    HLSLINCLUDE
        #pragma exclude_renderers gles
        //#pragma multi_compile _SAMPLES_LOW _SAMPLES_MEDIUM _SAMPLES_HIGH _SAMPLES_ULTRA

        // Include neccessary libraries

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "PulseCommon.hlsl"
        #include "PulseBlurTypes.hlsl"
        //#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // Porperties from renderer feature

        half2 _Direction;
        half _Strength;

        half _SAMPLES;
        half _invSAMPLES;
        half _invSqrtSAMPLES;
        half _lodSAMPLES;

        half4 _rotMatrix;

        half _Lod;
        half _powerLOD;

        // Blur shaders from: https://www.shadertoy.com/view/NscGDf

        half4 BoxBlurFrag(Varyings input) : SV_Target
        {
            half dist = _invSqrtSAMPLES;
            dist = dist > 0 ? _invSqrtSAMPLES : 1;

            return BoxBlur(input.texcoord, _Strength, dist); // Expensive
        }

        half4 LinearBlurFrag(Varyings input) : SV_Target
        {
            half dist = _invSAMPLES;
            dist = dist > 0 ? _invSAMPLES : 1;

            return LinearBlur(input.texcoord, _Direction, dist);
        }

        half4 RadialBlurFrag(Varyings input) : SV_Target
        {
            half dist = _invSAMPLES;
            dist = dist > 0 ? _invSAMPLES : 1;

            return RadialBlur(input.texcoord, _Strength, dist);
        }

        half4 AngularBlurFrag(Varyings input) : SV_Target
        {
            half dist = _invSAMPLES;
            dist = dist > 0 ? _invSAMPLES : 1;

            half2x2 rotmat = half2x2(_rotMatrix);

            return AngularBlur(input.texcoord, _Strength, dist, rotmat);
        }

        half4 GaussianBlurFrag(Varyings input) : SV_Target
        {
            //half dist = _lodSAMPLES;
            //dist = dist > 0 ? _lodSAMPLES : 1;

            return GaussianBlur(input.texcoord, _Lod, _powerLOD, _lodSAMPLES, _SAMPLES);
        }

        half4 MipMapBlurFrag(Varyings input) : SV_Target
        {
            //half dist = _lodSAMPLES;
            //dist = dist > 0 ? _lodSAMPLES : 1;

            return sampleBlitLod(input.texcoord, _Lod);
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
            Name "Pulse_BoxBlur_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment BoxBlurFrag

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_LinearBlur_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment LinearBlurFrag

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_RadialBlur_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment RadialBlurFrag

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_AngularBlur_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment AngularBlurFrag

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_GaussianBlur_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment GaussianBlurFrag

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_MipMapBlur_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment MipMapBlurFrag

            ENDHLSL
        }
    }
}
