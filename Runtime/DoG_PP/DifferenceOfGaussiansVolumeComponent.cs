using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
[VolumeComponentMenu("Custom/DifferenceOfGaussians")]
public class DifferenceOfGaussiansVolumeComponent : VolumeComponent, IPostProcessComponent
{
    public ClampedIntParameter gaussianKernelSize = new ClampedIntParameter(5, 1, 10);
    public ClampedFloatParameter stdev = new ClampedFloatParameter(2.0f, 0.1f, 5.0f);
    public ClampedFloatParameter stdevScale = new ClampedFloatParameter(1.6f, 0.1f, 5.0f);
    public ClampedFloatParameter tau = new ClampedFloatParameter(1.0f, 0.01f, 5.0f);

    public BoolParameter thresholding = new BoolParameter(true);
    public BoolParameter tanh = new BoolParameter(false);

    public ClampedFloatParameter phi = new ClampedFloatParameter(1.0f, 0.01f, 100.0f);

    public ClampedFloatParameter threshold = new ClampedFloatParameter(0.005f, -1f, 1.0f);

    public BoolParameter invert = new BoolParameter(false);


    public bool IsActive()
    {
        return gaussianKernelSize.overrideState;
    }

    public void UpdateMaterialVariables(Material dogMat)
    {
        dogMat.SetInt("_GaussianKernelSize", gaussianKernelSize.value);
        dogMat.SetFloat("_Sigma", stdev.value);
        dogMat.SetFloat("_K", stdevScale.value);
        dogMat.SetFloat("_Tau", tau.value);
        dogMat.SetFloat("_Phi", phi.value);
        dogMat.SetFloat("_Threshold", threshold.value);
        dogMat.SetInt("_Thresholding", thresholding.value ? 1 : 0);
        dogMat.SetInt("_Invert", invert.value ? 1 : 0);
        dogMat.SetInt("_Tanh", tanh.value ? 1 : 0);
    }
}
