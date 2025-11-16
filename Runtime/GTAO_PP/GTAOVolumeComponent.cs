using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
[VolumeComponentMenu("Custom/GTAO")]
public class GTAOVolumeComponent : VolumeComponent, IPostProcessComponent
{
    public enum OutPass
    {
        Combine = 4,
        AO = 5,
        RO = 6,
        BentNormal = 7
    };

    public BoolParameter enabled = new BoolParameter(true);

    [Header("Render Property")]
    public ClampedIntParameter DirSampler = new ClampedIntParameter(2, 1, 4);
    public ClampedIntParameter SliceSampler = new ClampedIntParameter(2, 1, 8);
    public ClampedFloatParameter Radius = new ClampedFloatParameter(1f, 1.0f, 5.0f);
    public ClampedFloatParameter Intensity = new ClampedFloatParameter(1.0f, 0.0f, 1.0f);
    public ClampedFloatParameter Power = new ClampedFloatParameter(1f, 1.0f, 8.0f);
    public BoolParameter MultiBounce = new BoolParameter(true);

    [Header("Filtter Property")]
    public ClampedFloatParameter Sharpeness = new ClampedFloatParameter(0.25f, 0.0f, 1.0f);
    public ClampedFloatParameter TemporalScale = new ClampedFloatParameter(1.0f, 1.0f, 5.0f);
    public ClampedFloatParameter TemporalResponse = new ClampedFloatParameter(1.0f, 0.0f, 1.0f);

    [Header("Debug")]
    public EnumParameter<OutPass> AODebug = new EnumParameter<OutPass>(OutPass.Combine);
    public BoolParameter HideInSceneView = new BoolParameter(false);

    public bool IsActive()
    {
        return enabled.overrideState && enabled.value;
    }
}
