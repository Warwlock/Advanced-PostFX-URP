Shader "Hidden/_Pulse_EdgeDetection"
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

        float _depthEps;
        float _normalEps;
        float _depthFadeDistance;
        float _oldVersion;
        float2 _CameraClipPlane;

        sampler2D _GBuffer2;
        TEXTURE2D(_CameraNormalsTexture);

        float3 SampleSceneNormals(float2 uv)
        {
            return tex2D(_GBuffer2, uv).rgb;
            //return SAMPLE_TEXTURE2D_X(_CameraNormalsTexture, _sampler_Linear_Clamp, uv);
        }

        // The fragment shader definition.            
        float4 frag(Varyings input) : SV_Target
        {
            float edgeColor = 1;
            float4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, _sampler_Linear_Clamp, input.texcoord);

            float2 uv = input.texcoord;
            float2 texel_size = _Blit_TexelSize.xy;

            float3 worldNormal = SampleSceneNormals(input.texcoord);
            //float depth = tex2D(_CameraDepthTexture, input.texcoord.xy).r;
            float depth = SampleSceneDepth(input.texcoord);
            float3 worldPos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
            float linearDepth = 1 - Linear01Depth(depth, _ZBufferParams);
            float linearEyeDepth = pow(LinearEyeDepth(depth, _ZBufferParams), .5) - _depthFadeDistance * 35;

            float depthDistance = saturate(linearDepth * (_depthFadeDistance * _CameraClipPlane.y)); //pow(linearDepth, 1) * .5;
            float2 uvs[4];
            uvs[0] = uv + texel_size * float2(0, 1) * depthDistance;
            uvs[1] = uv + texel_size * float2(0, -1) * depthDistance;
            uvs[2] = uv + texel_size * float2(1, 0) * depthDistance;
            uvs[3] = uv + texel_size * float2(-1, 0) * depthDistance;

            /*uint scale = 10;
            uint3 worldIntPos = uint3(abs(worldPos.xyz * scale));
            bool white = (worldIntPos.x & 1) ^ (worldIntPos.y & 1) ^ (worldIntPos.z & 1);
            return white ? float4(1,1,1,1) : float4(0,0,0,1);*/
            
            if(_oldVersion < 0.5)
            {
                for(int i = 0; i < 4; i++)
                {
                    float3 neighbourNormal = SampleSceneNormals(uvs[i]);
                    float neighbourDepth = SampleSceneDepth(uvs[i]);
                    float normalDist = length(neighbourNormal - worldNormal);
                    float depthDist = abs(neighbourDepth - depth);

                    if(normalDist > _normalEps || depthDist > _depthEps)
                    {
                        return 1;
                    }
                }
            }
            else if (_oldVersion < 1.5)
            {
                for(int i = 0; i < 4; i++)
                {
                    float3 neighbourNormal = SampleSceneNormals(uvs[i]);
                    float neighbourDepth = SampleSceneDepth(uvs[i]);
                    float3 neighbourWorldPos = ComputeWorldSpacePosition(uvs[i], neighbourDepth, UNITY_MATRIX_I_VP);

                    float normalDist = dot(neighbourNormal, worldNormal);
                    float planeDistance = abs(dot(worldNormal, neighbourWorldPos - worldPos));

                    if(normalDist < cos(_normalEps) || planeDistance > _depthEps)
                    {
                        //float fadeRange = linearDepth + 
                        edgeColor = lerp(0, 1, saturate(linearEyeDepth * .9 + 1));
                    }
                }
            }
            else
            {
                for(int i = 0; i < 4; i++)
                {
                    float3 neighbourNormal = SampleSceneNormals(uvs[i]);
                    float neighbourDepth = SampleSceneDepth(uvs[i]);
                    float3 neighbourWorldPos = ComputeWorldSpacePosition(uvs[i], neighbourDepth, UNITY_MATRIX_I_VP);

                    float normalDist = dot(neighbourNormal, worldNormal);
                    float planeDistance = abs(dot(worldNormal, neighbourWorldPos - worldPos));

                    if(normalDist < cos(_normalEps) || planeDistance > _depthEps)
                    {
                        //float fadeRange = linearDepth + 
                        edgeColor = 0;// = lerp(1, 0, saturate(depth * _depthFadeDistance * 1.5 - 0.2));
                    }
                }
            }
            return col * edgeColor;
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
