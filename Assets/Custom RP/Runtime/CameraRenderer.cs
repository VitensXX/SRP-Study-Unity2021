using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    ScriptableRenderContext context;
    Camera camera;

    const string bufferName = "Render Camera";

	CommandBuffer buffer = new CommandBuffer {
		name = bufferName
	};
    
    public void Render(ScriptableRenderContext context, Camera camera){
        this.context = context;
        this.camera = camera;

        //设置命令缓冲区名字
        PrepareBuffer();
        PrepareForSceneWindow();

        if(!Cull()){
            return;
        }

        Setup();
        DrawVisibleGeometry();
        DrawUnsupportedShaders();
        DrawGizmos();
        Submit();
	}

    void Setup () {
		context.SetupCameraProperties(camera);
        //相机的clear flags
        CameraClearFlags flags = camera.clearFlags;
        buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, flags <= CameraClearFlags.Color, 
            flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
	}

    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
    void DrawVisibleGeometry () {
        var sortingSettings = new SortingSettings(camera){
            criteria = SortingCriteria.CommonOpaque
        };
        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings);
		var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        //绘制不透明物体
		context.DrawRenderers(
			cullingResults, ref drawingSettings, ref filteringSettings
		);

        //绘制天空盒
		context.DrawSkybox(camera);

        sortingSettings.criteria = SortingCriteria.CommonTransparent;
		drawingSettings.sortingSettings = sortingSettings;
		filteringSettings.renderQueueRange = RenderQueueRange.transparent;
        //绘制透明物体
		context.DrawRenderers(
			cullingResults, ref drawingSettings, ref filteringSettings
		);
	}


     void Submit () {
        buffer.EndSample(SampleName);
        ExecuteBuffer();
		context.Submit();
	}

    void ExecuteBuffer () {
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}

    CullingResults cullingResults;
    bool Cull () {
		ScriptableCullingParameters p;
		if (camera.TryGetCullingParameters(out p)) {
            cullingResults = context.Cull(ref p);
			return true;
		}
		return false;
	}

}
