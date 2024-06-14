using UnityEngine;
using UnityEngine.Rendering;

public partial class CameraRenderer
{
    ScriptableRenderContext context;
    Camera camera;

    const string bufferName = "Render Camera";

    CommandBuffer buffer = new CommandBuffer
    {
        name = bufferName
    };

    Lighting lighting = new Lighting();
    PostFXStack postFXStack = new PostFXStack();

    public void Render(ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useGPUInstancing,
        bool useLightsPerObject, ShadowSettings shadowSettings, PostFXSettings postFXSettings)
    {
        this.context = context;
        this.camera = camera;

        //设置命令缓冲区名字
        PrepareBuffer();
        PrepareForSceneWindow();

        if (!Cull(shadowSettings.maxDistance))
        {
            return;
        }

        buffer.BeginSample(SampleName);
        ExecuteBuffer();
        lighting.Setup(context, cullingResults, shadowSettings, useLightsPerObject);
        postFXStack.Setup(context, camera, postFXSettings);
        buffer.EndSample(SampleName);
        Setup();
        DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, useLightsPerObject);
        DrawUnsupportedShaders();
        // DrawGizmos();
        DrawGizmosBeforeFX();
        if (postFXStack.IsActive)
        {
            postFXStack.Render(frameBufferId);
        }
        DrawGizmosAfterFX();
        Cleanup();
        // lighting.Cleanup();
        Submit();
    }

    static int frameBufferId = Shader.PropertyToID("_CameraFrameBuffer");
    void Setup()
    {
        context.SetupCameraProperties(camera);
        //相机的clear flags
        CameraClearFlags flags = camera.clearFlags;

        if (postFXStack.IsActive)
        {
            if (flags > CameraClearFlags.Color)
            {
                flags = CameraClearFlags.Color;
            }
            buffer.GetTemporaryRT(
                frameBufferId, camera.pixelWidth, camera.pixelHeight,
                32, FilterMode.Bilinear, RenderTextureFormat.Default
            );
            buffer.SetRenderTarget(
                frameBufferId,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
        }

        buffer.ClearRenderTarget(flags <= CameraClearFlags.Depth, flags <= CameraClearFlags.Color,
            flags == CameraClearFlags.Color ? camera.backgroundColor.linear : Color.clear);
        buffer.BeginSample(SampleName);
        ExecuteBuffer();
    }

    void Cleanup()
    {
        lighting.Cleanup();
        if (postFXStack.IsActive)
        {
            buffer.ReleaseTemporaryRT(frameBufferId);
        }
    }

    static ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
    static ShaderTagId litShaderTagId = new ShaderTagId("CustomLit");
    void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject)
    {
        PerObjectData lightsPerObjectFlags = useLightsPerObject ?
            PerObjectData.LightData | PerObjectData.LightIndices :
            PerObjectData.None;

        var sortingSettings = new SortingSettings(camera)
        {
            criteria = SortingCriteria.CommonOpaque
        };
        var drawingSettings = new DrawingSettings(unlitShaderTagId, sortingSettings)
        {
            enableDynamicBatching = useDynamicBatching,
            enableInstancing = useGPUInstancing,
            perObjectData = PerObjectData.Lightmaps | PerObjectData.ShadowMask
            | PerObjectData.LightProbe | PerObjectData.OcclusionProbe
            | PerObjectData.LightProbeProxyVolume | PerObjectData.OcclusionProbeProxyVolume | PerObjectData.ReflectionProbes
            | lightsPerObjectFlags
        };
        drawingSettings.SetShaderPassName(1, litShaderTagId);
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


    void Submit()
    {
        buffer.EndSample(SampleName);
        ExecuteBuffer();
        context.Submit();
    }

    void ExecuteBuffer()
    {
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    CullingResults cullingResults;
    bool Cull(float maxShadowDistance)
    {
        ScriptableCullingParameters p;
        if (camera.TryGetCullingParameters(out p))
        {
            //It doesn't make sense to render shadows that are further away than the camera can see, 
            //so take the minimum of the max shadow distance and the camera's far clip plane.
            p.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
            cullingResults = context.Cull(ref p);
            return true;
        }
        return false;
    }

}
