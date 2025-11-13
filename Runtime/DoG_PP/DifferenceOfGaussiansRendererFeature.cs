using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Experimental.Rendering;

public class DifferenceOfGaussiansRendererFeature : ScriptableRendererFeature
{
    Material m_material;
    CustomEffectPass m_ScriptablePass;

    /// <inheritdoc/>
    public override void Create()
    {
        m_material = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/_DifferenceOfGaussians"));
        m_ScriptablePass = new CustomEffectPass(m_material);

        // Configures where the render pass should be injected.
        m_ScriptablePass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
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
        const string m_PassName = "DifferenceOfGaussiansRendererFeature";
        Material m_BlitMaterial;
        RTHandle gaussian2Handle;

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
            DifferenceOfGaussiansVolumeComponent customEffect = stack.GetComponent<DifferenceOfGaussiansVolumeComponent>();

            if (!customEffect.IsActive())
            {
                return;
            }

            if (m_BlitMaterial == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogError("Skipping render pass. DifferenceOfGaussiansRendererFeature requires an intermediate ColorTexture.");
                return;
            }

            TextureHandle source = resourceData.activeColorTexture;

            TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.name = $"CameraColor - {m_PassName}";
            destinationDesc.clearBuffer = false;

            Vector4 texelSize = new Vector4(1f / destinationDesc.width, 1f / destinationDesc.height, destinationDesc.width, destinationDesc.height);
            m_BlitMaterial.SetVector("_Blit_TexelSize", texelSize);
            customEffect.UpdateMaterialVariables(m_BlitMaterial);

            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);
            TextureHandle gaussian1Handle = renderGraph.CreateTexture(destinationDesc);

            var desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.stencilFormat = GraphicsFormat.None;
            desc.msaaSamples = 1;
            RenderingUtils.ReAllocateHandleIfNeeded(ref gaussian2Handle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_SSGIHistoryCameraColorTexture");
            TextureHandle gaussian2TextureHandle = renderGraph.ImportTexture(gaussian2Handle);

            RenderGraphUtils.BlitMaterialParameters gaussian1params = new(source, gaussian1Handle, m_BlitMaterial, 0);
            renderGraph.AddBlitPass(gaussian1params, "DOG - Gaussian1");

            RenderGraphUtils.BlitMaterialParameters gaussian2params = new(gaussian1Handle, gaussian2TextureHandle, m_BlitMaterial, 1);
            renderGraph.AddBlitPass(gaussian2params, "DOG - Gaussian2");

            m_BlitMaterial.SetTexture("_GaussianTex", gaussian2Handle);

            RenderGraphUtils.BlitMaterialParameters parameters = new(source, destination, m_BlitMaterial, 2);
            renderGraph.AddBlitPass(parameters, m_PassName);

            resourceData.cameraColor = destination;
        }

        public void Dispose()
        {
            gaussian2Handle?.Release();
        }
    }
}
