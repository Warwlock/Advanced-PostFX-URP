using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
[VolumeComponentMenu("Custom/EdgeDetectionVolumeComponent")]
public class EdgeDetectionVolumeComponent : VolumeComponent, IPostProcessComponent
{
    public BoolParameter enabled = new BoolParameter(true);

    public FloatParameter depthEpsilon = new FloatParameter(0.1f);
    public FloatParameter normalEpsilon = new FloatParameter(0.1f);
    public FloatParameter version = new FloatParameter(0f);
    public ClampedFloatParameter depthFadeDistance = new ClampedFloatParameter(0.3f, 0f, 1f);

    public bool IsActive()
    {
        return enabled.overrideState && enabled.value;
    }

    public void UpdateMaterialProperties(Material mat)
    {
        if (mat == null)
            return;

        mat.SetFloat("_depthEps", depthEpsilon.value);
        mat.SetFloat("_normalEps", normalEpsilon.value);
        mat.SetFloat("_oldVersion", version.value);
        mat.SetFloat("_depthFadeDistance", depthFadeDistance.value);
    }
}
