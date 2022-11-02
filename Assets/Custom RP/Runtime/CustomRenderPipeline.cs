using UnityEngine;
using UnityEngine.Rendering;

public class CustomRenderPipeline : RenderPipeline
{
    CameraRenderer renderer = new CameraRenderer();

    public CustomRenderPipeline () {
		GraphicsSettings.useScriptableRenderPipelineBatching = true;
	}
    
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        int length = cameras.Length;
        for (int i = 0; i < length; i++)
        {
            renderer.Render(context, cameras[i]);
        }
    }
}
