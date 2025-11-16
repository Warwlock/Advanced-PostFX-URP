using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using System.Diagnostics.Contracts;
using Unity.VisualScripting;
using UnityEngine.Experimental.Rendering;

public class ToneMappingRendererFeature : ScriptableRendererFeature
{
    Material m_material;
    CustomEffectPass m_ScriptablePass;
    ComputeShader customTonemapping;

    /// <inheritdoc/>
    public override void Create()
    {
        m_material = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/_Pulse_ToneMapping"));
        customTonemapping = Resources.Load<ComputeShader>("CustomTonemapping");
        VolumeStack stack = VolumeManager.instance.stack;
        ToneMappingVolumeComponent customEffect = stack.GetComponent<ToneMappingVolumeComponent>();

        m_ScriptablePass = new CustomEffectPass(m_material, customTonemapping, customEffect.lutTexture.value);

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
        m_ScriptablePass?.Dispose();
        if (m_material != null)
            CoreUtils.Destroy(m_material);
        base.Dispose(disposing);
    }

    class CustomEffectPass : ScriptableRenderPass
    {
        const string m_PassName = "ToneMappingRendererFeature";
        Material m_BlitMaterial;
        RTHandle m_CurrentDestination;

        ComputeShader customTonemapping;
        Texture lutTexture;

        public CustomEffectPass(Material mat, ComputeShader customTonemapping, Texture lutTexture)
        {
            m_BlitMaterial = mat;
            this.lutTexture = lutTexture;
            this.customTonemapping = customTonemapping;
        }

        public void Setup()
        {
            requiresIntermediateTexture = true;
        }

        class PassData
        {
            internal TextureHandle lutTextureToRead;
        }

        class ComputePass
        {
            internal ComputeShader computeShader;
            internal TextureHandle lutTextureToRead;
        }

        // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
        // FrameData is a context container through which URP resources can be accessed and managed.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            VolumeStack stack = VolumeManager.instance.stack;
            ToneMappingVolumeComponent customEffect = stack.GetComponent<ToneMappingVolumeComponent>();

            if (!customEffect.IsActive())
            {
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogError("Skipping render pass. ToneMappingRendererFeature requires an intermediate ColorTexture.");
                return;
            }

            SetMaterialParameters(customEffect);

            TextureHandle source = resourceData.activeColorTexture;

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(m_PassName, out var passData))
            {
                //builder.UseTexture(m_CurrentAutoExposure_Handle, AccessFlags.Read);
                //builder.UseTexture(source);
                builder.SetRenderAttachment(source, 0, AccessFlags.ReadWrite);

                if ((int)customEffect.toneMapper.value == 10)
                {
                    if (customEffect.lutTexture.value != null)
                    {
                        // Create a render texture from the input texture
                        RTHandle rtHandle = RTHandles.Alloc(customEffect.lutTexture.value);

                        // Create a texture handle that the render graph system can use
                        TextureHandle textureToRead = renderGraph.ImportTexture(rtHandle);

                        // Add the texture to the pass data
                        passData.lutTextureToRead = textureToRead;

                        // Set the texture as readable
                        builder.UseTexture(passData.lutTextureToRead, AccessFlags.Read);
                    }
                }

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    if ((int)customEffect.toneMapper.value == 10)
                    {
                        if (customEffect.lutTexture.value == null) return;
                        int lutSize = customEffect.lutTexture.value.width;
                        m_BlitMaterial.SetVector("_LogLut3D_Params", new Vector4(1f / lutSize, lutSize - 1f, customEffect.postExposure.value, 1f));
                        m_BlitMaterial.SetTexture("_LogLut3D", data.lutTextureToRead);
                    }
                    Blitter.BlitTexture(context.cmd, source, new Vector4(1, 1, 0, 0), m_BlitMaterial, (int)customEffect.toneMapper.value);

                    if ((int)customEffect.toneMapper.value == 10)
                        RTHandles.Release(data.lutTextureToRead);
                });
            }

            //RenderGraphUtils.BlitMaterialParameters parameters = new(source, destination, m_BlitMaterial, (int)customEffect.toneMapper.value);
            //renderGraph.AddBlitPass(parameters, m_PassName);
        }

        void SetMaterialParameters(ToneMappingVolumeComponent customEffect)
        {
            m_BlitMaterial.SetFloat("_Ldmax", customEffect.Ldmax.value);
            m_BlitMaterial.SetFloat("_Cmax", customEffect.Cmax.value);
            m_BlitMaterial.SetFloat("_P", customEffect.p.value);
            m_BlitMaterial.SetFloat("_HiVal", customEffect.hiVal.value);
            m_BlitMaterial.SetFloat("_Pwhite", customEffect.Pwhite.value);
            m_BlitMaterial.SetFloat("_A", customEffect.shoulderStrength.value);
            m_BlitMaterial.SetFloat("_B", customEffect.linearStrength.value);
            m_BlitMaterial.SetFloat("_C", customEffect.linearAngle.value);
            m_BlitMaterial.SetFloat("_D", customEffect.toeStrength.value);
            m_BlitMaterial.SetFloat("_E", customEffect.toeNumerator.value);
            m_BlitMaterial.SetFloat("_F", customEffect.toeDenominator.value);
            m_BlitMaterial.SetFloat("_W", customEffect.linearWhitePoint.value);
            m_BlitMaterial.SetFloat("_M", customEffect.maxBrightness.value);
            m_BlitMaterial.SetFloat("_a", customEffect.contrast.value);
            m_BlitMaterial.SetFloat("_m", customEffect.linearStart.value);
            m_BlitMaterial.SetFloat("_l", customEffect.linearLength.value);
            m_BlitMaterial.SetFloat("_c", customEffect.blackTightnessShape.value);
            m_BlitMaterial.SetFloat("_b", customEffect.blackTightnessOffset.value);
        }

        public void Dispose()
        {
            m_CurrentDestination?.Release();
        }
    }
}
