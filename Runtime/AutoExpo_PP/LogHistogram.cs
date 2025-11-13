using UnityEngine;

public static class LogHistogram
{
    public const int rangeMin = -9; // ev
    public const int rangeMax = 9; // ev

    public const int k_Bins = 128;
    public const int m_ThreadX = 16;
    public const int m_ThreadY = 16;

    public static GraphicsBuffer GetGraphicsBuffer()
    {
        var buffData = new GraphicsBuffer(GraphicsBuffer.Target.Structured, k_Bins, sizeof(uint));
        /*var ones = new uint[k_Bins];
        for (int i = 0; i < k_Bins; i++)
        {
            ones[i] = uint.MaxValue;
        }
        buffData.SetData(ones);*/
        return buffData;
    }

    public static Vector4 GetHistogramScaleOffsetRes(UnityEngine.Rendering.Universal.UniversalCameraData cameraData)
    {
        float diff = rangeMax - rangeMin;
        float scale = 1f / diff;
        float offset = -rangeMin * scale;
        return new Vector4(scale, offset, cameraData.camera.pixelWidth, cameraData.camera.pixelHeight);
    }
}
