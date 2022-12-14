using UnityEngine;
using UnityEngine.Rendering;

public class CustomRenderPipeline : RenderPipeline
{
    bool useDynamicBatching, useGPUInstancing;
    ShadowSettings shadowSettings;

    CameraRenderer renderer = new CameraRenderer();

    public CustomRenderPipeline (bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher, ShadowSettings shadowSettings) {
        this.useDynamicBatching = useDynamicBatching;
		this.useGPUInstancing = useGPUInstancing;
        this.shadowSettings = shadowSettings;
		GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        //灯光使用线性强度
        GraphicsSettings.lightsUseLinearIntensity = true;
	}
    
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        int length = cameras.Length;
        for (int i = 0; i < length; i++)
        {
            renderer.Render(context, cameras[i], useDynamicBatching, useGPUInstancing, shadowSettings);
        }
    }
}
