using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

public class EdgeDetectionRendererFeature : ScriptableRendererFeature
{
    Material m_material;
    CustomEffectPass m_ScriptablePass;

    /// <inheritdoc/>
    public override void Create()
    {
        m_material = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/_Pulse_EdgeDetection"));
        m_ScriptablePass = new CustomEffectPass(m_material);

        // Configures where the render pass should be injected.
        m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if(m_material == null)
        {
            return;
        }
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
        const string m_PassName = "EdgeDetectionRendererFeature";
        Material m_BlitMaterial;

        public CustomEffectPass(Material mat)
        {
            m_BlitMaterial = mat;
        }

        public void Setup(Material mat)
        {
            m_BlitMaterial = mat;
            requiresIntermediateTexture = true;
        }

        // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
        // FrameData is a context container through which URP resources can be accessed and managed.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            VolumeStack stack = VolumeManager.instance.stack;
            EdgeDetectionVolumeComponent customEffect = stack.GetComponent<EdgeDetectionVolumeComponent>();

            if (!customEffect.IsActive())
            {
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogError("Skipping render pass. EdgeDetectionRendererFeature requires an intermediate ColorTexture.");
                return;
            }

            TextureHandle source = resourceData.activeColorTexture;

            TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.name = $"CameraColor - {m_PassName}";
            destinationDesc.clearBuffer = false;

            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

            Vector4 texelSize = new Vector4(1f / destinationDesc.width, 1f / destinationDesc.height, destinationDesc.width, destinationDesc.height);
            m_BlitMaterial.SetVector("_Blit_TexelSize", texelSize);
            m_BlitMaterial.SetVector("_CameraClipPlane", new Vector4(cameraData.camera.nearClipPlane, cameraData.camera.farClipPlane, 0, 0));
            //m_BlitMaterial.SetTexture("_CameraNormalsTexture", resourceData.cameraNormalsTexture);
            customEffect.UpdateMaterialProperties(m_BlitMaterial);

            RenderGraphUtils.BlitMaterialParameters parameters = new(source, destination, m_BlitMaterial, 0);
            renderGraph.AddBlitPass(parameters, m_PassName);

            resourceData.cameraColor = destination;
        }

        public void Dispose()
        {

        }
    }
}
