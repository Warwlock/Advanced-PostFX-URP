using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
[VolumeComponentMenu("Custom/ExtendedDifferenceOfGaussians")]
public class ExtendedDOGVolumeComponent : VolumeComponent, IPostProcessComponent
{
    public BoolParameter enabled = new BoolParameter(true);

    public ClampedIntParameter superSample = new ClampedIntParameter(1, 1, 4);

    [Header("Edge Tangent Flow Settings")]
    public BoolParameter useFlow = new BoolParameter(true);

    public ClampedFloatParameter structureTensorDeviation = new ClampedFloatParameter(2.0f, 0.0f, 5.0f);
    public ClampedFloatParameter lineIntegralDeviation = new ClampedFloatParameter(2.0f, 0.0f, 20.0f);

    public Vector2Parameter lineConvolutionStepSizes = new Vector2Parameter(new Vector2(1, 1));

    public BoolParameter calcDiffBeforeConvolution = new BoolParameter(true);

    [Header("Difference Of Gaussians Settings")]
    public ClampedFloatParameter differenceOfGaussiansDeviation = new ClampedFloatParameter(2.0f, 0.0f, 10.0f);

    public ClampedFloatParameter stdevScale = new ClampedFloatParameter(1.6f, 0.1f, 5.0f);
    public ClampedFloatParameter Sharpness = new ClampedFloatParameter(1.0f, 0.0f, 100.0f);


    public enum ThresholdMode
    {
        NoThreshold = 0,
        Tanh,
        Quantization,
        SmoothQuantization
    }

    [Header("Threshold Settings")]
    public EnumParameter<ThresholdMode> thresholdMode = new EnumParameter<ThresholdMode>(ThresholdMode.NoThreshold);

    public ClampedIntParameter quantizerStep = new ClampedIntParameter(2, 1, 16);
    public ClampedFloatParameter whitePoint = new ClampedFloatParameter(50.0f, 0.0f, 100.0f);
    public ClampedFloatParameter softThreshold = new ClampedFloatParameter(1.0f, 0.0f, 10.0f);

    public BoolParameter invert = new BoolParameter(false);


    [Header("Anti Aliasing Settings")]
    public BoolParameter smoothEdges = new BoolParameter(true);

    public ClampedFloatParameter edgeSmoothDeviation = new ClampedFloatParameter(1.0f, 0.0f, 10.0f);
    public Vector2Parameter edgeSmoothStepSizes = new Vector2Parameter(new Vector2(1, 1));


    [Header("Cross Hatch Settings")]
    public BoolParameter enableHatching = new BoolParameter(false);
    public TextureParameter hatchTexture = new TextureParameter(null);

    [Space(10)]

    public ClampedFloatParameter hatchResolution = new ClampedFloatParameter(1.0f, 0.0f, 8.0f);
    public ClampedFloatParameter hatchRotation = new ClampedFloatParameter(90.0f, -180.0f, 180.0f);

    [Space(10)]
    public BoolParameter enableSecondLayer = new BoolParameter(true);
    public ClampedFloatParameter secondWhitePoint = new ClampedFloatParameter(20.0f, 0.0f, 100.0f);
    public ClampedFloatParameter hatchResolution2 = new ClampedFloatParameter(1.0f, 0.0f, 8.0f);
    public ClampedFloatParameter secondHatchRotation = new ClampedFloatParameter(60.0f, -180.0f, 180.0f);

    [Space(10)]
    public BoolParameter enableThirdLayer = new BoolParameter(true);
    public ClampedFloatParameter thirdWhitePoint = new ClampedFloatParameter(30.0f, 0.0f, 100.0f);
    public ClampedFloatParameter hatchResolution3 = new ClampedFloatParameter(1.0f, 0.0f, 8.0f);
    public ClampedFloatParameter thirdHatchRotation = new ClampedFloatParameter(120.0f, -180.0f, 180.0f);

    [Space(10)]
    public BoolParameter enableFourthLayer = new BoolParameter(true);
    public ClampedFloatParameter fourthWhitePoint = new ClampedFloatParameter(30.0f, 0.0f, 100.0f);
    public ClampedFloatParameter hatchResolution4 = new ClampedFloatParameter(1.0f, 0.0f, 8.0f);
    public ClampedFloatParameter fourthHatchRotation = new ClampedFloatParameter(120.0f, -180.0f, 180.0f);

    [Space(10)]
    public BoolParameter enableColoredPencil = new BoolParameter(false);
    public ClampedFloatParameter brightnessOffset = new ClampedFloatParameter(0.0f, -1.0f, 1.0f);
    public ClampedFloatParameter saturation = new ClampedFloatParameter(1.0f, 0.0f, 5.0f);


    [Header("Blend Settings")]
    public ClampedFloatParameter termStrength = new ClampedFloatParameter(1.0f, 0.0f, 5.0f);

    public enum BlendMode
    {
        NoBlend = 0,
        Interpolate,
        TwoPointInterpolate
    }

    public EnumParameter<BlendMode> blendMode = new EnumParameter<BlendMode>(BlendMode.NoBlend);

    public ColorParameter minColor = new ColorParameter(new Color(0.0f, 0.0f, 0.0f));
    public ColorParameter maxColor = new ColorParameter(new Color(1.0f, 1.0f, 1.0f));

    public ClampedFloatParameter blendStrength = new ClampedFloatParameter(1.0f, 0.0f, 2.0f);

    public bool IsActive()
    {
        return enabled.overrideState && enabled.value;
    }

    public void UpdateMaterialVariables(Material dogMat)
    {
        dogMat.SetFloat("_SigmaC", structureTensorDeviation.value);
        dogMat.SetFloat("_SigmaE", differenceOfGaussiansDeviation.value);
        dogMat.SetFloat("_SigmaM", lineIntegralDeviation.value);
        dogMat.SetFloat("_SigmaA", edgeSmoothDeviation.value);
        dogMat.SetFloat("_K", stdevScale.value);
        dogMat.SetFloat("_Tau", Sharpness.value);
        dogMat.SetFloat("_Phi", softThreshold.value);
        dogMat.SetFloat("_Threshold", whitePoint.value);
        dogMat.SetFloat("_Threshold2", secondWhitePoint.value);
        dogMat.SetFloat("_Threshold3", thirdWhitePoint.value);
        dogMat.SetFloat("_Threshold4", fourthWhitePoint.value);
        dogMat.SetFloat("_Thresholds", quantizerStep.value);
        dogMat.SetFloat("_BlendStrength", blendStrength.value);
        dogMat.SetFloat("_DoGStrength", termStrength.value);
        dogMat.SetFloat("_HatchTexRotation", hatchRotation.value);
        dogMat.SetFloat("_HatchTexRotation1", secondHatchRotation.value);
        dogMat.SetFloat("_HatchTexRotation2", thirdHatchRotation.value);
        dogMat.SetFloat("_HatchTexRotation3", fourthHatchRotation.value);
        dogMat.SetFloat("_HatchRes1", hatchResolution.value);
        dogMat.SetFloat("_HatchRes2", hatchResolution2.value);
        dogMat.SetFloat("_HatchRes3", hatchResolution3.value);
        dogMat.SetFloat("_HatchRes4", hatchResolution4.value);
        dogMat.SetInt("_EnableSecondLayer", enableSecondLayer.value ? 1 : 0);
        dogMat.SetInt("_EnableThirdLayer", enableThirdLayer.value ? 1 : 0);
        dogMat.SetInt("_EnableFourthLayer", enableFourthLayer.value ? 1 : 0);
        dogMat.SetInt("_EnableColoredPencil", enableColoredPencil.value ? 1 : 0);
        dogMat.SetFloat("_BrightnessOffset", brightnessOffset.value);
        dogMat.SetFloat("_Saturation", saturation.value);
        dogMat.SetVector("_IntegralConvolutionStepSizes", new Vector4(lineConvolutionStepSizes.value.x, lineConvolutionStepSizes.value.y, 
            edgeSmoothStepSizes.value.x, edgeSmoothStepSizes.value.y));
        dogMat.SetVector("_MinColor", minColor.value);
        dogMat.SetVector("_MaxColor", maxColor.value);
        dogMat.SetInt("_Thresholding", (int)thresholdMode.value);
        dogMat.SetInt("_BlendMode", (int)blendMode.value);
        dogMat.SetInt("_Invert", invert.value ? 1 : 0);
        dogMat.SetInt("_CalcDiffBeforeConvolution", calcDiffBeforeConvolution.value ? 1 : 0);
        dogMat.SetInt("_HatchingEnabled", enableHatching.value ? 1 : 0);
        dogMat.SetTexture("_HatchTex", hatchTexture.value);
    }
}
