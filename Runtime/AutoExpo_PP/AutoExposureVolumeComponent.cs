using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
[VolumeComponentMenu("Custom/AutoExposureVolumeComponent")]
public class AutoExposureVolumeComponent : VolumeComponent, IPostProcessComponent
{
    /// <summary>
    /// Eye adaptation modes.
    /// </summary>
    public enum EyeAdaptation
    {
        /// <summary>
        /// Progressive (smooth) eye adaptation.
        /// </summary>
        Progressive,

        /// <summary>
        /// Fixed (instant) eye adaptation.
        /// </summary>
        Fixed
    }

    public BoolParameter enabled = new BoolParameter(true);

    [Header("Exposure")]

    [DisplayInfo(name = "Filtering (%)"), Tooltip("Filters the bright and dark parts of the histogram when computing the average luminance. This is to avoid very dark pixels and very bright pixels from contributing to the auto exposure. Unit is in percent.")]
    public FloatRangeParameter filtering = new FloatRangeParameter(new Vector2(30f, 95f), 1f, 99f);

    [DisplayInfo(name = "Minimum (EV)"), Tooltip("Minimum average luminance to consider for auto exposure. Unit is EV.")]
    public ClampedFloatParameter minLuminance = new ClampedFloatParameter(5f, LogHistogram.rangeMin, LogHistogram.rangeMax);

    [DisplayInfo(name = "Maximum (EV)"), Tooltip("Maximum average luminance to consider for auto exposure. Unit is EV.")]
    public ClampedFloatParameter maxLuminance = new ClampedFloatParameter(-5f, LogHistogram.rangeMin, LogHistogram.rangeMax);

    [DisplayInfo(name = "Exposure Compensation"), Tooltip("Use this to scale the global exposure of the scene.")]
    public MinFloatParameter keyValue = new MinFloatParameter(0.4f, 0f);

    [Header("Adaptation")]

    [DisplayInfo(name = "Type"), Tooltip("Use \"Progressive\" if you want auto exposure to be animated. Use \"Fixed\" otherwise.")]
    public EnumParameter<EyeAdaptation> eyeAdaptation = new EnumParameter<EyeAdaptation>(EyeAdaptation.Progressive);

    [DisplayInfo(name = "Speed Up"), Tooltip("Adaptation speed from a dark to a light environment.")]
    public MinFloatParameter speedUp = new MinFloatParameter(2f, 0f);

    [DisplayInfo(name = "Speed Down"), Tooltip("Adaptation speed from a light to a dark environment.")]
    public MinFloatParameter speedDown = new MinFloatParameter(1f, 0f);

    public bool IsActive()
    {
        return enabled.overrideState && enabled.value;
    }
}
