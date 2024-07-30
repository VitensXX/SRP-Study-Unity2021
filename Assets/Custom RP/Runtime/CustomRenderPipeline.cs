using UnityEngine;
using UnityEngine.Rendering;

public partial class CustomRenderPipeline : RenderPipeline
{
    bool useDynamicBatching, useGPUInstancing, useLightsPerObject;
    ShadowSettings shadowSettings;
    PostFXSettings postFXSettings;
    bool allowHDR;

    CameraRenderer renderer = new CameraRenderer();
    int colorLUTResolution;

    public CustomRenderPipeline(bool allowHDR, bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher,
        bool useLightsPerObject, ShadowSettings shadowSettings, PostFXSettings postFXSettings, int colorLUTResolution)
    {
        this.allowHDR = allowHDR;
        this.useDynamicBatching = useDynamicBatching;
        this.useGPUInstancing = useGPUInstancing;
        this.shadowSettings = shadowSettings;
        this.useLightsPerObject = useLightsPerObject;
        this.postFXSettings = postFXSettings;
        this.colorLUTResolution = colorLUTResolution;
        GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        //灯光使用线性强度
        GraphicsSettings.lightsUseLinearIntensity = true;

        InitializeForEditor();
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        int length = cameras.Length;
        for (int i = 0; i < length; i++)
        {
            renderer.Render(context, cameras[i], allowHDR, useDynamicBatching, useGPUInstancing,
                useLightsPerObject, shadowSettings, postFXSettings, colorLUTResolution);
        }
    }
}
