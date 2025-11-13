using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Experimental.Rendering;

public class AutoExposureRendererFeature : ScriptableRendererFeature
{
    Material m_material;
    CustomEffectPass m_ScriptablePass;
    public ComputeShader autoExposure;
    public ComputeShader exposureHistogram;

    /// <inheritdoc/>
    public override void Create()
    {
        m_material = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/_Pulse_AutoExposure"));
        autoExposure = Resources.Load<ComputeShader>("AutoExposure/AutoExposure");
        exposureHistogram = Resources.Load<ComputeShader>("AutoExposure/ExposureHistogram");
        m_ScriptablePass = new CustomEffectPass(m_material);

        // Configures where the render pass should be injected.
        m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if(autoExposure == null || exposureHistogram == null)
        {
            return;
        }

        m_ScriptablePass.Setup(autoExposure, exposureHistogram);
        renderer.EnqueuePass(m_ScriptablePass);
    }

    protected override void Dispose(bool disposing)
    {
        m_ScriptablePass?.Dispose();
        if (m_material != null)
            CoreUtils.Destroy(m_material);
        base.Dispose(disposing);
    }

    class CustomEffectPass : ScriptableRenderPass
    {
        const string m_PassName = "AutoExposureRendererFeature";
        Material m_BlitMaterial;
        ComputeShader m_autoExposure;
        ComputeShader m_exposureHistogram;

        GraphicsBuffer bufferData;

        RTHandle m_CurrentAutoExposure;
        bool isFirstFrame = true;

        public CustomEffectPass(Material mat)
        {
            m_BlitMaterial = mat;
            bufferData = LogHistogram.GetGraphicsBuffer();
        }

        public void Setup(ComputeShader autoExposure, ComputeShader exposureHistogram)
        {
            m_autoExposure = autoExposure;
            m_exposureHistogram = exposureHistogram;
            requiresIntermediateTexture = true;
        }

        class PassData
        {
            internal TextureHandle cameraColorTexture;
        }

        class LogHistogramPassData
        {
            public ComputeShader computeShader;
            public BufferHandle buffer;
            public TextureHandle source;
            public Vector4 scaleOffsetRes;
        }

        class AutoExposurePassData
        {
            public ComputeShader computeShader;
            public AutoExposureVolumeComponent settings;
            public BufferHandle buffer;
            public TextureHandle source;
            public TextureHandle currentAutoExposure;
            public Vector4 scaleOffsetRes;
            public bool firstFrame;
        }

        // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
        // FrameData is a context container through which URP resources can be accessed and managed.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            VolumeStack stack = VolumeManager.instance.stack;
            AutoExposureVolumeComponent customEffect = stack.GetComponent<AutoExposureVolumeComponent>();

            if (!customEffect.IsActive())
            {
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogError("Skipping render pass. AutoExposureRendererFeature requires an intermediate ColorTexture.");
                return;
            }

            TextureHandle source = resourceData.activeColorTexture;

            TextureDesc descriptor = renderGraph.GetTextureDesc(source);
            descriptor.name = $"CameraColor - {m_PassName}";
            descriptor.clearBuffer = false;
            TextureHandle destination = renderGraph.CreateTexture(descriptor);

            RenderTextureDescriptor desc = new RenderTextureDescriptor(1, 1, RenderTextureFormat.RFloat);
            desc.depthBufferBits = 0;
            desc.stencilFormat = GraphicsFormat.None;
            desc.enableRandomWrite = true;

            RenderingUtils.ReAllocateHandleIfNeeded(ref m_CurrentAutoExposure, desc, FilterMode.Point, TextureWrapMode.Clamp, name: " m_CurrentAutoExposure");
            TextureHandle m_CurrentAutoExposure_Handle = renderGraph.ImportTexture(m_CurrentAutoExposure);

            BufferHandle bufferHandleRG = renderGraph.ImportBuffer(bufferData);

            if (isFirstFrame)
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Clear Auto Exposure", out var passData))
                {
                    // Set the texture as the render target
                    builder.SetRenderAttachment(m_CurrentAutoExposure_Handle, 0, AccessFlags.Write);

                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        context.cmd.ClearRenderTarget(true, true, new Color(customEffect.keyValue.value * 0.05f, 0f, 0f, 1f));
                    });
                }

                isFirstFrame = false;
            }


            using (var builder = renderGraph.AddComputePass("LogHistogram", out LogHistogramPassData data))
            {
                data.scaleOffsetRes = LogHistogram.GetHistogramScaleOffsetRes(cameraData);
                data.computeShader = m_exposureHistogram;

                data.buffer = bufferHandleRG;
                builder.UseBuffer(data.buffer, AccessFlags.Write);

                data.source = source;
                builder.UseTexture(data.source, AccessFlags.Read);

                builder.SetRenderFunc((LogHistogramPassData data, ComputeGraphContext context) => ExecuteLogHistogramPass(data, context));
            }

            //renderGraph.AddCopyPass(source, destination);
            //RenderGraphUtils.BlitMaterialParameters parameters = new(destination, source, m_BlitMaterial, 0);
            //renderGraph.AddBlitPass(parameters, m_PassName);

            bool firstFrame = false;

            using (var builder = renderGraph.AddComputePass("AutoExposure", out AutoExposurePassData data))
            {
                data.scaleOffsetRes = LogHistogram.GetHistogramScaleOffsetRes(cameraData);
                data.computeShader = m_autoExposure;
                data.settings = customEffect;
                data.firstFrame = firstFrame;

                data.buffer = bufferHandleRG;
                builder.UseBuffer(data.buffer, AccessFlags.Read);

                data.source = source;
                builder.UseTexture(data.source, AccessFlags.Read);

                data.currentAutoExposure = m_CurrentAutoExposure_Handle;
                builder.UseTexture(data.currentAutoExposure, AccessFlags.ReadWrite);

                builder.SetRenderFunc((AutoExposurePassData data, ComputeGraphContext context) => ExecuteAutoExposurePass(data, context));
            }

            if (firstFrame)
            {
                //renderGraph.AddCopyPass(m_AutoExposurePool_Handle0, m_AutoExposurePool_Handle1);
            }

            m_BlitMaterial.SetFloat("_exposureCompenastion", customEffect.keyValue.value);
            m_BlitMaterial.SetTexture("_Exposure", m_CurrentAutoExposure);
            RenderGraphUtils.BlitMaterialParameters parameters = new(source, destination, m_BlitMaterial, 0);
            renderGraph.AddBlitPass(parameters, m_PassName);

            resourceData.cameraColor = destination;
        }

        void ExecuteLogHistogramPass(LogHistogramPassData data, ComputeGraphContext context)
        {
            var cmd = context.cmd;
            var compute = data.computeShader;

            cmd.BeginSample("LogHistogram");

            int kernel = compute.FindKernel("KEyeHistogramClear");
            cmd.SetComputeBufferParam(compute, kernel, "_HistogramBuffer", data.buffer);
            cmd.DispatchCompute(compute, kernel, Mathf.CeilToInt(LogHistogram.k_Bins / (float)LogHistogram.m_ThreadX), 1, 1);

            kernel = compute.FindKernel("KEyeHistogram");
            cmd.SetComputeBufferParam(compute, kernel, "_HistogramBuffer", data.buffer);
            cmd.SetComputeTextureParam(compute, kernel, "_Source", data.source);
            cmd.SetComputeVectorParam(compute, "_ScaleOffsetRes", data.scaleOffsetRes);
            cmd.DispatchCompute(compute, kernel,
                Mathf.CeilToInt(data.scaleOffsetRes.z / 2f / LogHistogram.m_ThreadX),
                Mathf.CeilToInt(data.scaleOffsetRes.w / 2f / LogHistogram.m_ThreadY),
                1
            );

            cmd.EndSample("LogHistogram");
        }

        void ExecuteAutoExposurePass(AutoExposurePassData data, ComputeGraphContext context)
        {
            var cmd = context.cmd;
            var compute = data.computeShader;
            var settings = data.settings;

            cmd.BeginSample("AutoExposureLookup");

            // Make sure filtering values are correct to avoid apocalyptic consequences
            float lowPercent = settings.filtering.value.x;
            float highPercent = settings.filtering.value.y;
            const float kMinDelta = 1e-2f;
            highPercent = Mathf.Clamp(highPercent, 1f + kMinDelta, 99f);
            lowPercent = Mathf.Clamp(lowPercent, 1f, highPercent - kMinDelta);

            // Clamp min/max adaptation values as well
            float minLum = settings.minLuminance.value;
            float maxLum = settings.maxLuminance.value;
            settings.minLuminance.value = Mathf.Min(minLum, maxLum);
            settings.maxLuminance.value = Mathf.Max(minLum, maxLum);

            string adaptation;

            if (data.firstFrame || settings.eyeAdaptation.value == AutoExposureVolumeComponent.EyeAdaptation.Fixed)
                adaptation = "KAutoExposureAvgLuminance_fixed";
            else
                adaptation = "KAutoExposureAvgLuminance_progressive";

            int kernel = compute.FindKernel(adaptation);
            cmd.SetComputeBufferParam(compute, kernel, "_HistogramBuffer", data.buffer);
            cmd.SetComputeVectorParam(compute, "_Params1", new Vector4(lowPercent * 0.01f, highPercent * 0.01f, Exp2(settings.minLuminance.value), Exp2(settings.maxLuminance.value)));
            cmd.SetComputeVectorParam(compute, "_Params2", new Vector4(settings.speedDown.value, settings.speedUp.value, settings.keyValue.value, Time.deltaTime));
            cmd.SetComputeVectorParam(compute, "_ScaleOffsetRes", data.scaleOffsetRes);

            if (data.firstFrame)
            {
                cmd.SetComputeTextureParam(compute, kernel, "_Destination", data.currentAutoExposure);
                cmd.DispatchCompute(compute, kernel, 1, 1, 1);
            }
            else
            {
                cmd.SetComputeTextureParam(compute, kernel, "_Source", data.currentAutoExposure);
                cmd.SetComputeTextureParam(compute, kernel, "_Destination", data.currentAutoExposure);
                cmd.DispatchCompute(compute, kernel, 1, 1, 1);
            }

            cmd.EndSample("AutoExposureLookup");
        }

        public static float Exp2(float x)
        {
            return Mathf.Exp(x * 0.69314718055994530941723212145818f);
        }

        public void Dispose()
        {
            bufferData?.Release();
            m_CurrentAutoExposure?.Release();
        }
    }
}
