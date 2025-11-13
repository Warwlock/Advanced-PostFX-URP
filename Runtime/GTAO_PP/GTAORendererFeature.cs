using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Experimental.Rendering;

public class GTAORendererFeature : ScriptableRendererFeature
{
    Material m_material;
    CustomEffectPass m_ScriptablePass;

    /// <inheritdoc/>
    public override void Create()
    {
        m_material = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/GroundTruthAmbientOcclusion"));
        m_ScriptablePass = new CustomEffectPass(m_material);

        // Configures where the render pass should be injected.
        m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        m_ScriptablePass.Setup();
        renderer.EnqueuePass(m_ScriptablePass);
    }

    protected override void Dispose(bool disposing)
    {
        if (m_ScriptablePass != null)
            m_ScriptablePass.Dispose();
        if (m_material != null)
            CoreUtils.Destroy(m_material);
        base.Dispose(disposing);
    }

    class CustomEffectPass : ScriptableRenderPass
    {
        const string m_PassName = "GTAORendererFeature";
        Material m_BlitMaterial;
        Material testMat;

        //////Transform property 
        private Matrix4x4 projectionMatrix;
        private Matrix4x4 LastFrameViewProjectionMatrix;
        private Matrix4x4 View_ProjectionMatrix;
        private Matrix4x4 Inverse_View_ProjectionMatrix;
        private Matrix4x4 worldToCameraMatrix;

        ////// private
        private float HalfProjScale;
        private float TemporalOffsets;
        private float TemporalDirections;
        private Vector2 CameraSize;
        private Vector2 RenderResolution;
        private Vector4 UVToView;
        private Vector4 oneOverSize_Size;
        private Vector4 Target_TexelSize;

        private uint m_sampleStep = 0;
        private static readonly float[] m_temporalRotations = { 60, 300, 180, 240, 120, 0 };
        private static readonly float[] m_spatialOffsets = { 0, 0.5f, 0.25f, 0.75f };

        private RTHandle _AO_Scene_Color_Texture;
        private RTHandle AO_BentNormal_RT0;
        private RTHandle AO_BentNormal_RT1;
        private RTHandle GTAO_Spatial_Texture;
        private RTHandle Prev_RT;
        private RTHandle Curr_RT;
        private RTHandle Combined_AO_RT;

        //////Shader Property
        ///Public
        private static int _ProjectionMatrix_ID = Shader.PropertyToID("_ProjectionMatrix");
        private static int _LastFrameViewProjectionMatrix_ID = Shader.PropertyToID("_LastFrameViewProjectionMatrix");
        private static int _View_ProjectionMatrix_ID = Shader.PropertyToID("_View_ProjectionMatrix");
        private static int _Inverse_View_ProjectionMatrix_ID = Shader.PropertyToID("_Inverse_View_ProjectionMatrix");
        private static int _WorldToCameraMatrix_ID = Shader.PropertyToID("_WorldToCameraMatrix");
        private static int _CameraToWorldMatrix_ID = Shader.PropertyToID("_CameraToWorldMatrix");


        private static int _AO_DirSampler_ID = Shader.PropertyToID("_AO_DirSampler");
        private static int _AO_SliceSampler_ID = Shader.PropertyToID("_AO_SliceSampler");
        private static int _AO_Power_ID = Shader.PropertyToID("_AO_Power");
        private static int _AO_Intensity_ID = Shader.PropertyToID("_AO_Intensity");
        private static int _AO_Radius_ID = Shader.PropertyToID("_AO_Radius");
        private static int _AO_Sharpeness_ID = Shader.PropertyToID("_AO_Sharpeness");
        private static int _AO_TemporalScale_ID = Shader.PropertyToID("_AO_TemporalScale");
        private static int _AO_TemporalResponse_ID = Shader.PropertyToID("_AO_TemporalResponse");
        private static int _AO_MultiBounce_ID = Shader.PropertyToID("_AO_MultiBounce");


        ///Private
        private static int _AO_HalfProjScale_ID = Shader.PropertyToID("_AO_HalfProjScale");
        private static int _AO_TemporalOffsets_ID = Shader.PropertyToID("_AO_TemporalOffsets");
        private static int _AO_TemporalDirections_ID = Shader.PropertyToID("_AO_TemporalDirections");
        private static int _AO_UVToView_ID = Shader.PropertyToID("_AO_UVToView");
        private static int _AO_RT_TexelSize_ID = Shader.PropertyToID("_AO_RT_TexelSize");


        private static int _AO_Scene_Color_ID = Shader.PropertyToID("_AO_Scene_Color");
        private static int _BentNormal_Texture_ID = Shader.PropertyToID("_BentNormal_Texture");
        private static int _GTAO_Texture_ID = Shader.PropertyToID("_GTAO_Texture");
        private static int _GTAO_Spatial_Texture_ID = Shader.PropertyToID("_GTAO_Spatial_Texture");
        private static int _PrevRT_ID = Shader.PropertyToID("_PrevRT");
        private static int _CurrRT_ID = Shader.PropertyToID("_CurrRT");
        private static int _Combien_AO_RT_ID = Shader.PropertyToID("_Combien_AO_RT");


        /////Render Code

        private class PassData
        {
            internal Material passMaterial;
        }

        class CopyPassData
        {
            internal bool localGBuffers;
            internal TextureHandle gBuffer0Handle;
            internal TextureHandle gBuffer1Handle;
            internal TextureHandle gBuffer2Handle;
        }

        public CustomEffectPass(Material mat)
        {
            m_BlitMaterial = mat;
        }

        public void Setup()
        {
            testMat = new Material(Shader.Find("Hidden/_Pulse_DirectBlit"));
            requiresIntermediateTexture = true;
        }

        // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
        // FrameData is a context container through which URP resources can be accessed and managed.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            VolumeStack stack = VolumeManager.instance.stack;
            GTAOVolumeComponent customEffect = stack.GetComponent<GTAOVolumeComponent>();

            if (!customEffect.IsActive())
            {
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if(cameraData.camera.cameraType == CameraType.SceneView && customEffect.HideInSceneView.value)
            {
                return;
            }

            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogError("Skipping render pass. GTAORendererFeature requires an intermediate ColorTexture.");
                return;
            }

            UpdateVariables(cameraData, customEffect);

            //////Rendering
            TextureHandle source = resourceData.activeColorTexture;

            TextureDesc descriptor = renderGraph.GetTextureDesc(source);
            descriptor.name = $"CameraColor - {m_PassName}";
            descriptor.clearBuffer = false;

            TextureHandle destination = renderGraph.CreateTexture(descriptor);

            var desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.stencilFormat = GraphicsFormat.None;
            RenderingUtils.ReAllocateHandleIfNeeded(ref _AO_Scene_Color_Texture, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_AO_Scene_Color_Texture");
            TextureHandle _AO_Scene_Color_Handle = renderGraph.ImportTexture(_AO_Scene_Color_Texture);

            RenderingUtils.ReAllocateHandleIfNeeded(ref AO_BentNormal_RT0, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "AO_BentNormal_RT0");
            TextureHandle AO_BentNormal_RT0_Handle = renderGraph.ImportTexture(AO_BentNormal_RT0);
            RenderingUtils.ReAllocateHandleIfNeeded(ref AO_BentNormal_RT1, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "AO_BentNormal_RT1");
            TextureHandle AO_BentNormal_RT1_Handle = renderGraph.ImportTexture(AO_BentNormal_RT1);

            RenderingUtils.ReAllocateHandleIfNeeded(ref GTAO_Spatial_Texture, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "GTAO_Spatial_Texture");
            TextureHandle GTAO_Spatial_Texture_Handle = renderGraph.ImportTexture(GTAO_Spatial_Texture);

            RenderingUtils.ReAllocateHandleIfNeeded(ref Prev_RT, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "History_RT");
            m_BlitMaterial.SetTexture(_PrevRT_ID, Prev_RT);
            TextureHandle Prev_RT_Handle = renderGraph.ImportTexture(Prev_RT);

            RenderingUtils.ReAllocateHandleIfNeeded(ref Curr_RT, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "Current_RT");
            TextureHandle Curr_RT_Handle = renderGraph.ImportTexture(Curr_RT);

            RenderingUtils.ReAllocateHandleIfNeeded(ref Combined_AO_RT, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "Combined_AO_RT");
            TextureHandle Combined_AO_RT_Handle = renderGraph.ImportTexture(Combined_AO_RT);

            m_BlitMaterial.SetTexture(_AO_Scene_Color_ID, _AO_Scene_Color_Texture);
            m_BlitMaterial.SetTexture(_GTAO_Texture_ID, AO_BentNormal_RT0);
            m_BlitMaterial.SetTexture(_BentNormal_Texture_ID, AO_BentNormal_RT1);
            //renderGraph.AddCopyPass(source, _AO_Scene_Color_Handle);

            using (var builder = renderGraph.AddUnsafePass<CopyPassData>("GTAO Copy Pass", out var passData))
            {
                builder.UseTexture(source);
                builder.UseTexture(_AO_Scene_Color_Handle, AccessFlags.Write);

                passData.localGBuffers = resourceData.gBuffer[0].IsValid();

                if (passData.localGBuffers)
                {
                    passData.gBuffer0Handle = resourceData.gBuffer[0];
                    passData.gBuffer1Handle = resourceData.gBuffer[1];
                    passData.gBuffer2Handle = resourceData.gBuffer[2];

                    builder.UseTexture(passData.gBuffer0Handle, AccessFlags.Read);
                    builder.UseTexture(passData.gBuffer1Handle, AccessFlags.Read);
                    builder.UseTexture(passData.gBuffer2Handle, AccessFlags.Read);
                }

                //builder.UseTexture(resourceData.gBuffer[3]);

                //paramsSsr.propertyBlock.SetTexture(paramsSsr.sourceTexturePropertyID, paramsSsr.source);

                builder.SetRenderFunc((CopyPassData data, UnsafeGraphContext context) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                    if (data.localGBuffers)
                    {
                        cmd.SetGlobalTexture("_GBuffer0", data.gBuffer0Handle);
                        cmd.SetGlobalTexture("_GBuffer1", data.gBuffer1Handle);
                        cmd.SetGlobalTexture("_GBuffer2", data.gBuffer2Handle);
                    }
                    else
                    {
                        
                    }

                    cmd.CopyTexture(source, _AO_Scene_Color_Handle);
                });
            }

            //Resolve GTAO
            //m_BlitMaterial.SetTexture(_GTAO_Texture_ID, AO_BentNormal_RT0);
            //m_BlitMaterial.SetTexture(_BentNormal_Texture_ID, AO_BentNormal_RT1);


            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Resolve GTAO Pass", out var passData))
            {
                passData.passMaterial = m_BlitMaterial;

                builder.SetRenderAttachment(AO_BentNormal_RT0_Handle, 0);
                builder.SetRenderAttachment(AO_BentNormal_RT1_Handle, 1);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
            }

            /////Spatial Filter
            m_BlitMaterial.SetTexture(_GTAO_Spatial_Texture_ID, GTAO_Spatial_Texture);
            RenderGraphUtils.BlitMaterialParameters spatialXParams = new(source, GTAO_Spatial_Texture_Handle, m_BlitMaterial, 1);
            renderGraph.AddBlitPass(spatialXParams, "Spatial Filter - XBlur");

            renderGraph.AddCopyPass(GTAO_Spatial_Texture_Handle, AO_BentNormal_RT0_Handle);
            RenderGraphUtils.BlitMaterialParameters spatialYParams = new(source, GTAO_Spatial_Texture_Handle, m_BlitMaterial, 2);
            renderGraph.AddBlitPass(spatialYParams, "Spatial Filter - YBlur");

            //////Temporal Filter
            m_BlitMaterial.SetTexture(_CurrRT_ID, Curr_RT);
            RenderGraphUtils.BlitMaterialParameters temporalParams = new(source, Curr_RT_Handle, m_BlitMaterial, 3);
            renderGraph.AddBlitPass(temporalParams, "Temporal Filter");

            /////Combine Scene Color
            m_BlitMaterial.SetTexture(_Combien_AO_RT_ID, Combined_AO_RT);
            RenderGraphUtils.BlitMaterialParameters combinedParams = new(source, destination, m_BlitMaterial, (int)customEffect.AODebug.value);
            renderGraph.AddBlitPass(combinedParams, "Combine Scene Color");
            renderGraph.AddCopyPass(Curr_RT_Handle, Prev_RT_Handle, passName: "Copy Current To History");

            resourceData.cameraColor = destination;
        }

        static void ExecutePass(PassData passData, RasterGraphContext context)
        {
            RasterCommandBuffer cmd = context.cmd;

            //passData.passMaterial.SetTexture(_GTAO_Texture_ID, passData.AO_BentNormal_RT0);
            //passData.passMaterial.SetTexture(_BentNormal_Texture_ID, passData.AO_BentNormal_RT1);

            Blitter.BlitTexture(cmd, new Vector4(1, 1, 0, 0), passData.passMaterial, 0);
        }

        public void Dispose()
        {
            _AO_Scene_Color_Texture?.Release();
            AO_BentNormal_RT0?.Release();
            AO_BentNormal_RT1?.Release();
            GTAO_Spatial_Texture?.Release();
            Prev_RT?.Release();
            Curr_RT?.Release();
            Combined_AO_RT?.Release();
        }

        void UpdateVariables(UniversalCameraData cameraData, GTAOVolumeComponent customEffect)
        {
            RenderResolution = new Vector2(cameraData.camera.pixelWidth, cameraData.camera.pixelHeight);
            worldToCameraMatrix = cameraData.camera.worldToCameraMatrix;
            m_BlitMaterial.SetMatrix(_WorldToCameraMatrix_ID, worldToCameraMatrix);
            m_BlitMaterial.SetMatrix(_CameraToWorldMatrix_ID, worldToCameraMatrix.inverse);
            projectionMatrix = GL.GetGPUProjectionMatrix(cameraData.camera.projectionMatrix, false);
            m_BlitMaterial.SetMatrix(_ProjectionMatrix_ID, projectionMatrix);
            View_ProjectionMatrix = projectionMatrix * worldToCameraMatrix;
            m_BlitMaterial.SetMatrix(_View_ProjectionMatrix_ID, View_ProjectionMatrix);
            m_BlitMaterial.SetMatrix(_Inverse_View_ProjectionMatrix_ID, View_ProjectionMatrix.inverse);
            m_BlitMaterial.SetMatrix(_LastFrameViewProjectionMatrix_ID, LastFrameViewProjectionMatrix);

            m_BlitMaterial.SetFloat(_AO_DirSampler_ID, customEffect.DirSampler.value);
            m_BlitMaterial.SetFloat(_AO_SliceSampler_ID, customEffect.SliceSampler.value);
            m_BlitMaterial.SetFloat(_AO_Intensity_ID, customEffect.Intensity.value);
            m_BlitMaterial.SetFloat(_AO_Radius_ID, customEffect.Radius.value);
            m_BlitMaterial.SetFloat(_AO_Power_ID, customEffect.Power.value);
            m_BlitMaterial.SetFloat(_AO_Sharpeness_ID, customEffect.Sharpeness.value);
            m_BlitMaterial.SetFloat(_AO_TemporalScale_ID, customEffect.TemporalScale.value);
            m_BlitMaterial.SetFloat(_AO_TemporalResponse_ID, customEffect.TemporalResponse.value);
            m_BlitMaterial.SetInt(_AO_MultiBounce_ID, customEffect.MultiBounce.value ? 1 : 0);

            float fovRad = cameraData.camera.fieldOfView * Mathf.Deg2Rad;
            float invHalfTanFov = 1 / Mathf.Tan(fovRad * 0.5f);
            Vector2 focalLen = new Vector2(invHalfTanFov * ((float)RenderResolution.y / (float)RenderResolution.x), invHalfTanFov);
            Vector2 invFocalLen = new Vector2(1 / focalLen.x, 1 / focalLen.y);
            m_BlitMaterial.SetVector(_AO_UVToView_ID, new Vector4(2 * invFocalLen.x, 2 * invFocalLen.y, -1 * invFocalLen.x, -1 * invFocalLen.y));

            float projScale;
            projScale = (float)RenderResolution.y / (Mathf.Tan(fovRad * 0.5f) * 2) * 0.5f;
            m_BlitMaterial.SetFloat(_AO_HalfProjScale_ID, projScale);

            oneOverSize_Size = new Vector4(1 / (float)RenderResolution.x, 1 / (float)RenderResolution.y, (float)RenderResolution.x, (float)RenderResolution.y);
            m_BlitMaterial.SetVector(_AO_RT_TexelSize_ID, oneOverSize_Size);

            float temporalRotation = m_temporalRotations[m_sampleStep % 6];
            float temporalOffset = m_spatialOffsets[(m_sampleStep / 6) % 4];
            m_BlitMaterial.SetFloat(_AO_TemporalDirections_ID, temporalRotation / 360);
            m_BlitMaterial.SetFloat(_AO_TemporalOffsets_ID, temporalOffset);
            m_sampleStep++;
        }
    }
}
