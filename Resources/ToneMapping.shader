Shader "Hidden/_Pulse_ToneMapping"
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

        // Tumblin Rushmeier Properties and Schlick and Ward
        float _Ldmax;
        float _Cmax;
        //float _lumChangeRate;
        float _HiVal;
        float _P;

        // Reinhard Extended Properties
        float _Pwhite;

        // Hable Properties
        float _A, _B, _C, _D, _E, _F, _W;

        //Uchimura Porperties
        float _M, _a, _m, _l, _c, _b;

        //Custom
        float4 _LogLut3D_Params;        // x: 1 / lut_size, y: lut_size - 1, z: postexposure, w: We need lut at all or not

        ////////////////////////////////////////////////////////////////////////

        TEXTURE2D_X(_AvrLumTexture);

        TEXTURE3D(_LogLut3D);
        SAMPLER(sampler_LogLut3D);

        // Luminance based tonemappers

        /*half4 toneMap_TumblinRushmeier_BlurPass(Varyings input) : SV_Target
        {
            return col;
        }*/

        float4 toneMap_RGBClamp(Varyings input) : SV_Target
        {
            float4 col;
            col = SAMPLE_TEXTURE2D_X(_BlitTexture, _sampler_Linear_Clamp, input.texcoord);

            return saturate(col);
        }

        float4 toneMap_TumblinRushmeier(Varyings input) : SV_Target
        {
            float4 col;
            col = SAMPLE_TEXTURE2D_X(_AvrLumTexture, _sampler_Linear_Clamp, input.texcoord);
            float Lin = Luminance(col);

            float Lavg = Luminance(SAMPLE_TEXTURE2D_LOD(_AvrLumTexture, _sampler_Linear_Clamp, input.texcoord, 10));
            //float newLavg = Lavg / 100 + 0.2;
            //Lavg = lerp(newLavg, Lavg, _lumChangeRate);

            float logLrw = log10(Lavg) + 0.84;
            float alphaRw = 0.4 * logLrw + 2.92;
            float betaRw = -0.4 * logLrw * logLrw - 2.584 * logLrw + 2.0208;
            float Lwd = _Ldmax / sqrt(_Cmax);
            float logLd = log10(Lwd) + 0.84;
            float alphaD = 0.4 * logLd + 2.92;
            float betaD = -0.4 * logLd * logLd - 2.584 * logLd + 2.0208;
            float Lout = pow(abs(Lin), alphaRw / alphaD) / _Ldmax * pow(10.0, (betaRw - betaD) / alphaD) - (1.0 / _Cmax);

            float3 Cout = (col / Lin * Lout).xyz;

            return float4(saturate(Cout), col.a);
        }

        float4 toneMap_Schlick(Varyings input) : SV_Target
        {
            float4 col;
            col = SAMPLE_TEXTURE2D_X(_BlitTexture, _sampler_Linear_Clamp, input.texcoord);

            float Lin = Luminance(col);

            float Lout = (_P * Lin) / (_P * Lin - Lin + _HiVal);

            float3 Cout = col.xyz / Lin * Lout;

            return float4(saturate(Cout), col.a);
        }

        float4 toneMap_Ward(Varyings input) : SV_Target
        {
            float4 col;
            col = SAMPLE_TEXTURE2D_X(_BlitTexture, _sampler_Linear_Clamp, input.texcoord);

            float Lin = Luminance(col);

            float m = (1.219f + pow(abs(_Ldmax) / 2.0f, 0.4f)) / (1.219f + pow(abs(Lin), 0.4f));
            m = pow(abs(m), 2.5f); 

            float Lout = m / _Ldmax * Lin;

            float3 Cout = col.xyz / Lin * Lout;

            return float4(saturate(Cout), col.a);
        }

        float4 toneMap_Reinhard(Varyings input) : SV_Target
        {
            float4 col;
            col = SAMPLE_TEXTURE2D_X(_BlitTexture, _sampler_Linear_Clamp, input.texcoord);

            float LumIn = Luminance(col);
            float LumOut = LumIn / (1 + LumIn);

            float3 Cout = (col / LumIn * LumOut).xyz;

            return float4(saturate(Cout), col.a);
        }

        float4 toneMap_ReinhardExtended(Varyings input) : SV_Target
        {
            float4 col;
            col = SAMPLE_TEXTURE2D_X(_BlitTexture, _sampler_Linear_Clamp, input.texcoord);

            float LumIn = Luminance(col);
            float LumOut = (LumIn * (1.0 + LumIn / (_Pwhite * _Pwhite))) / (1.0 + LumIn);

            float3 Cout = (col / LumIn * LumOut).xyz;

            return float4(saturate(Cout), col.a);
        }

        // Hable (Uncharted 2) Tonemap

        float HableTonemap(half x)
        {
            return ((x * (_A * x + _C * _B) + _D * _E) / (x * (_A * x + _B) + _D * _F)) - _E / _F;
        }

        float3 HableTonemap3(half3 x)
        {
            return ((x * (_A * x + _C * _B) + _D * _E) / (x * (_A * x + _B) + _D * _F)) - _E / _F;
        }

        // Color based tonemappers (filmic tonemappers)

        float4 toneMap_Hable(Varyings input) : SV_Target
        {
            float4 col;
            col = SAMPLE_TEXTURE2D_X(_BlitTexture, _sampler_Linear_Clamp, input.texcoord);
            
            float Cexposure = 2;
            
            float whiteScale = 1 / HableTonemap(_W);
            float3 current = Cexposure * HableTonemap3(col.xyz);

            float3 Cout = current * whiteScale;

            return float4(saturate(Cout), col.a);
        }

        float4 toneMap_Uchimura(Varyings input) : SV_Target
        {
            float4 col;
            col = SAMPLE_TEXTURE2D_X(_BlitTexture, _sampler_Linear_Clamp, input.texcoord);
            
            float l0 = ((_M - _m) * _l) / _a;
            float S0 = _m + l0;
            float S1 = _m + _a * l0;
            float C2 = (_a * _M) / (_M - S1);
            float CP = -C2 / _M;

            float3 w0 = 1.0f - smoothstep(float3(0.0f, 0.0f, 0.0f), float3(_m, _m, _m), col.xyz);
            float3 w2 = step(float3(_m + l0, _m + l0, _m + l0), col.xyz);
            float3 w1 = float3(1.0f, 1.0f, 1.0f) - w0 - w2;

            float3 T = _m * pow(abs(col.xyz / _m), _c) + _b;
            float3 S = _M - (_M - S1) * exp(CP * (col.xyz - S0));
            float3 L = _m + _a * (col.xyz - _m);

            float3 Cout = T * w0 + L * w1 + S * w2;

            return float4(saturate(Cout), col.a);
        }

        float4 toneMap_NarkowiczACES(Varyings input) : SV_Target
        {
            float4 col;
            col = SAMPLE_TEXTURE2D_X(_BlitTexture, _sampler_Linear_Clamp, input.texcoord);

            float3 Cout = ((col * (2.51f * col + 0.03f)) / (col * (2.43f * col + 0.59f) + 0.14f)).xyz;

            return float4(saturate(Cout), col.a);
        }

        float4 toneMap_HillACES(Varyings input) : SV_Target
        {
            float4 col;
            col = SAMPLE_TEXTURE2D_X(_BlitTexture, _sampler_Linear_Clamp, input.texcoord);

            float3 Cout = mul(ACESInputMat, col.xyz);
            Cout = RRTAndODTFit(Cout);

            Cout = mul(ACESOutputMat, Cout);

            return float4(saturate(Cout), col.a);
        }

        float4 toneMap_Custom(Varyings input) : SV_Target
        {
            float4 col;
            col = SAMPLE_TEXTURE2D_X(_BlitTexture, _sampler_Linear_Clamp, input.texcoord);

            col *= _LogLut3D_Params.z;
            
            // Move from linear to LogC
            float3 colorLutSpace = saturate(LinearToLogC(col.xyz));

            col.xyz = ApplyLut3D(TEXTURE3D_ARGS(_LogLut3D, sampler_LogLut3D), colorLutSpace, _LogLut3D_Params.xy);

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
            Name "Pulse_RGBClamp_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment toneMap_RGBClamp

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_TumblinRushmeier_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment toneMap_TumblinRushmeier

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_Schlick_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment toneMap_Schlick

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_Ward_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment toneMap_Ward

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_Reinhard_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment toneMap_Reinhard

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_ReinhardExtended_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment toneMap_ReinhardExtended

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_Hable_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment toneMap_Hable

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_Uchimura_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment toneMap_Uchimura

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_NarkowiczACES_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment toneMap_NarkowiczACES

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_HillACES_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment toneMap_HillACES

            ENDHLSL
        }

        Pass
        {
            Name "Pulse_Custom_Pass"
        
            HLSLPROGRAM

            // Pragmas
            //#pragma target 3.0
            #pragma vertex vert
            #pragma fragment toneMap_Custom

            ENDHLSL
        }
    }
}
