using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
[VolumeComponentMenu("Custom/ToneMappingVolumeComponent")]
public class ToneMappingVolumeComponent : VolumeComponent, IPostProcessComponent
{
    public BoolParameter enabled = new BoolParameter(true);

    public enum Tonemappers
    {
        RGBClamp,
        TumblinRushmeier,
        Schlick,
        Ward,
        Reinhard,
        ReinhardExtended,
        Hable,
        Uchimura,
        NarkowiczACES,
        HillACES,
        Custom
    }

    public EnumParameter<Tonemappers> toneMapper = new EnumParameter<Tonemappers>(Tonemappers.RGBClamp);

    // Tumblin Rushmeier Parameters
    [DisplayInfo(name = "Maximum Display Luminance")]
    public ClampedFloatParameter Ldmax = new ClampedFloatParameter(100f, 0.01f, 200f);
    [DisplayInfo(name = "Maximum Contrast")]
    public ClampedFloatParameter Cmax = new ClampedFloatParameter(50f, 0.01f, 100f);

    // Schlick Parameters
    [DisplayInfo(name = "Point (Max Brightness)")]
    public ClampedFloatParameter p = new ClampedFloatParameter(1f, 1f, 100f);
    [DisplayInfo(name = "HiVal")]
    public ClampedFloatParameter hiVal = new ClampedFloatParameter(1f, 1f, 150f);

    // Reinhard Extended Parameters
    [DisplayInfo(name = "White Point")]
    public ClampedFloatParameter Pwhite = new ClampedFloatParameter(2f, 1f, 50f);

    // Hable Parameters
    public ClampedFloatParameter shoulderStrength = new ClampedFloatParameter(0.15f, 0f, 1f);
    public ClampedFloatParameter linearStrength = new ClampedFloatParameter(0.5f, 0f, 1f);
    public ClampedFloatParameter linearAngle = new ClampedFloatParameter(0.1f, 0f, 1f);
    public ClampedFloatParameter toeStrength = new ClampedFloatParameter(0.2f, 0f, 1f);
    public ClampedFloatParameter toeNumerator = new ClampedFloatParameter(0.02f, 0f, 1f);
    public ClampedFloatParameter toeDenominator = new ClampedFloatParameter(0.3f, 0f, 1f);
    public ClampedFloatParameter linearWhitePoint = new ClampedFloatParameter(12f, 0f, 50f);

    // Uchimura Parameters
    public ClampedFloatParameter maxBrightness = new ClampedFloatParameter(1f, 1f, 100f);
    public ClampedFloatParameter contrast = new ClampedFloatParameter(1f, 0f, 5f);
    public ClampedFloatParameter linearStart = new ClampedFloatParameter(0.22f, 0f, 1f);
    public ClampedFloatParameter linearLength = new ClampedFloatParameter(0.4f, 0.01f, 0.99f);
    public ClampedFloatParameter blackTightnessShape = new ClampedFloatParameter(1.33f, 1f, 3f);
    public ClampedFloatParameter blackTightnessOffset = new ClampedFloatParameter(0f, 0f, 1f);

    // Custom Parameters
    public Texture3DParameter lutTexture = new Texture3DParameter(null);
    public FloatParameter postExposure = new FloatParameter(1f);

    public bool IsActive()
    {
        return enabled.overrideState && enabled.value;
    }
}
