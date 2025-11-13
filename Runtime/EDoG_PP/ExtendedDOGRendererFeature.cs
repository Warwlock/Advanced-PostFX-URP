using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Experimental.Rendering;

public class ExtendedDOGRendererFeature : ScriptableRendererFeature
{
    Material m_material;
    public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
    CustomEffectPass m_ScriptablePass;

    /// <inheritdoc/>
    public override void Create()
    {
        m_material = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/_ExtendedDOG"));
        m_ScriptablePass = new CustomEffectPass(m_material);

        // Configures where the render pass should be injected.
        m_ScriptablePass.renderPassEvent = injectionPoint;
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
        const string m_PassName = "ExtendedDOGRendererFeature";
        Material m_BlitMaterial;
        RTHandle eigenvectors2;
        RTHandle differenceOfGaussians;

        public CustomEffectPass(Material mat)
        {
            m_BlitMaterial = mat;
        }

        public void Setup()
        {
            requiresIntermediateTexture = true;
        }

        // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
        // FrameData is a context container through which URP resources can be accessed and managed.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            VolumeStack stack = VolumeManager.instance.stack;
            ExtendedDOGVolumeComponent customEffect = stack.GetComponent<ExtendedDOGVolumeComponent>();

            if (!customEffect.IsActive())
            {
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogError("Skipping render pass. ExtendedDOGRendererFeature requires an intermediate ColorTexture.");
                return;
            }

            TextureHandle source = resourceData.activeColorTexture;

            TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.name = $"CameraColor - {m_PassName}";
            destinationDesc.clearBuffer = false;
            TextureDesc superSampleDesc = renderGraph.GetTextureDesc(source);
            superSampleDesc.name = $"SuperSample - {m_PassName}";
            superSampleDesc.clearBuffer = false;
            superSampleDesc.width = destinationDesc.width * customEffect.superSample.value;
            superSampleDesc.height = destinationDesc.height * customEffect.superSample.value;

            Vector4 texelSize = new Vector4(1f / destinationDesc.width, 1f / destinationDesc.height, destinationDesc.width, destinationDesc.height);
            m_BlitMaterial.SetVector("_Blit_TexelSize", texelSize);
            customEffect.UpdateMaterialVariables(m_BlitMaterial);

            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);
            TextureHandle rgbToLab = renderGraph.CreateTexture(superSampleDesc);
            TextureHandle structureTensor = renderGraph.CreateTexture(superSampleDesc);
            TextureHandle eigenvectors1 = renderGraph.CreateTexture(superSampleDesc);
            TextureHandle gaussian1 = renderGraph.CreateTexture(superSampleDesc);
            TextureHandle gaussian2 = renderGraph.CreateTexture(superSampleDesc);

            var desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.stencilFormat = GraphicsFormat.None;
            desc.msaaSamples = 1;
            desc.width = destinationDesc.width * customEffect.superSample.value;
            desc.height = destinationDesc.height * customEffect.superSample.value;
            RenderingUtils.ReAllocateHandleIfNeeded(ref eigenvectors2, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_EigenVectors2");
            TextureHandle eigenvectors2Handle = renderGraph.ImportTexture(eigenvectors2);
            RenderingUtils.ReAllocateHandleIfNeeded(ref differenceOfGaussians, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_DifferenceOfGaussians");
            TextureHandle differenceOfGaussiansHandle = renderGraph.ImportTexture(differenceOfGaussians);

            RenderGraphUtils.BlitMaterialParameters rgbToLabParams = new(source, rgbToLab, m_BlitMaterial, m_BlitMaterial.FindPass("RGBtoLAB_Pass"));
            renderGraph.AddBlitPass(rgbToLabParams, "RGBtoLAB_Pass");

            if (customEffect.useFlow.value || customEffect.smoothEdges.value)
            {
                RenderGraphUtils.BlitMaterialParameters structureTensorParams = new(rgbToLab, structureTensor, m_BlitMaterial, m_BlitMaterial.FindPass("CalcEigenvectors_Pass"));
                renderGraph.AddBlitPass(structureTensorParams, "CalcEigenvectors_Pass");

                RenderGraphUtils.BlitMaterialParameters eigenvectors1Params = new(structureTensor, eigenvectors1, m_BlitMaterial, m_BlitMaterial.FindPass("TFMBlur1_Pass"));
                renderGraph.AddBlitPass(eigenvectors1Params, "TFMBlur1_Pass");
                RenderGraphUtils.BlitMaterialParameters eigenvectors2Params = new(eigenvectors1, eigenvectors2Handle, m_BlitMaterial, m_BlitMaterial.FindPass("TFMBlur2_Pass"));
                renderGraph.AddBlitPass(eigenvectors2Params, "TFMBlur2_Pass");

                m_BlitMaterial.SetTexture("_TFM", eigenvectors2);
            }

            if (customEffect.useFlow.value)
            {
                RenderGraphUtils.BlitMaterialParameters gaussian1Params = new(rgbToLab, gaussian1, m_BlitMaterial, m_BlitMaterial.FindPass("FDoGBlur1_Pass"));
                renderGraph.AddBlitPass(gaussian1Params, "FDoGBlur1_Pass");
                RenderGraphUtils.BlitMaterialParameters gaussian2Params = new(gaussian1, gaussian2, m_BlitMaterial, m_BlitMaterial.FindPass("FDoGBlur2_Pass"));
                renderGraph.AddBlitPass(gaussian2Params, "FDoGBlur2_Pass");
            }
            else
            {
                RenderGraphUtils.BlitMaterialParameters gaussian1Params = new(rgbToLab, gaussian1, m_BlitMaterial, m_BlitMaterial.FindPass("NonFDoGBlur1_Pass"));
                renderGraph.AddBlitPass(gaussian1Params, "NonFDoGBlur1_Pass");
                RenderGraphUtils.BlitMaterialParameters gaussian2Params = new(gaussian1, gaussian2, m_BlitMaterial, m_BlitMaterial.FindPass("NonFDoGBlur2_Pass"));
                renderGraph.AddBlitPass(gaussian2Params, "NonFDoGBlur2_Pass");
            }

            if (customEffect.smoothEdges.value)
            {
                RenderGraphUtils.BlitMaterialParameters dogParams = new(gaussian2, differenceOfGaussiansHandle, m_BlitMaterial, m_BlitMaterial.FindPass("AntiAliasing_Pass"));
                renderGraph.AddBlitPass(dogParams, "AntiAliasing_Pass");
            }
            else
            {
                renderGraph.AddCopyPass(gaussian2, differenceOfGaussiansHandle);
            }

            m_BlitMaterial.SetTexture("_DogTex", differenceOfGaussians);

            RenderGraphUtils.BlitMaterialParameters parameters = new(source, destination, m_BlitMaterial, m_BlitMaterial.FindPass("Blend_Pass"));
            renderGraph.AddBlitPass(parameters, "Blend_Pass");

            resourceData.cameraColor = destination;
        }

        public void Dispose()
        {
            eigenvectors2?.Release();
            differenceOfGaussians?.Release();
        }
    }
}
