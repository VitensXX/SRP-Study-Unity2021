using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class Lighting {

	const string bufferName = "Lighting";
    const int maxDirLightCount = 4;
    // static int dirLightColorId = Shader.PropertyToID("_DirectionalLightColor");
	// static int dirLightDirectionId = Shader.PropertyToID("_DirectionalLightDirection");
    static int dirLightCountId = Shader.PropertyToID("_DirectionalLightCount");
    static int dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors");
    static int dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections");
    static Vector4[] dirLightColors = new Vector4[maxDirLightCount];
	static Vector4[] dirLightDirections = new Vector4[maxDirLightCount];

	CommandBuffer buffer = new CommandBuffer {
		name = bufferName
	};
	
    CullingResults cullingResults;

	public void Setup (ScriptableRenderContext context, CullingResults cullingResults) {
        this.cullingResults = cullingResults;
		buffer.BeginSample(bufferName);
		// SetupDirectionalLight();
        SetupLights();
		buffer.EndSample(bufferName);
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}
	
	void SetupDirectionalLight (int index, ref VisibleLight visibleLight) {
		dirLightColors[index] = visibleLight.finalColor;
        //光照方向是通过VisibleLight.LocakToWorldMatrix属性来获取的，该矩阵的第三列即为光源的前向向量，需要取反
		dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
	}

    void SetupLights () {
        NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;

		int dirLightCount = 0;

		for (int i = 0; i < visibleLights.Length; i++) {
			VisibleLight visibleLight = visibleLights[i];
			if (visibleLight.lightType == LightType.Directional){
				//VisibleLight结构体比较大，改用ref 传递引用而不是值，能省下值传递生成副本的开销
				SetupDirectionalLight(i, ref visibleLight);
				//设置限制
				if (dirLightCount >= maxDirLightCount) {
					break;
				}
			}
		}

		buffer.SetGlobalInt(dirLightCountId, visibleLights.Length);
		buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
		buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
    }
}