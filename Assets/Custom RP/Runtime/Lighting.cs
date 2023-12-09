using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;

public class Lighting
{
	const string bufferName = "Lighting";
	const int maxDirLightCount = 4;
	// static int dirLightColorId = Shader.PropertyToID("_DirectionalLightColor");
	// static int dirLightDirectionId = Shader.PropertyToID("_DirectionalLightDirection");
	static int dirLightCountId = Shader.PropertyToID("_DirectionalLightCount");
	static int dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors");
	static int dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections");
	static int dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");
	static Vector4[] dirLightColors = new Vector4[maxDirLightCount];
	static Vector4[] dirLightDirections = new Vector4[maxDirLightCount];
	static Vector4[] dirLightShadowData = new Vector4[maxDirLightCount];

	//其他类型光源
	const int maxOtherLightCount = 64;
	static int otherLightCountId = Shader.PropertyToID("_OtherLightCount");
	static int otherLightColorsId = Shader.PropertyToID("_OtherLightColors");
	static int otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions");
	static int otherLightDirectionsId = Shader.PropertyToID("_OtherLightDirections");
	static int otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles");
	static Vector4[] otherLightColors = new Vector4[maxOtherLightCount];
	static Vector4[] otherLightPositions = new Vector4[maxOtherLightCount];
	static Vector4[] otherLightDirections = new Vector4[maxOtherLightCount];
	static Vector4[] otherLightSpotAngles = new Vector4[maxOtherLightCount];

	CommandBuffer buffer = new CommandBuffer
	{
		name = bufferName
	};

	CullingResults cullingResults;
	Shadows shadows = new Shadows();

	public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings)
	{
		this.cullingResults = cullingResults;
		buffer.BeginSample(bufferName);
		shadows.Setup(context, cullingResults, shadowSettings);
		SetupLights();
		shadows.Render();
		buffer.EndSample(bufferName);
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}

	void SetupDirectionalLight(int index, ref VisibleLight visibleLight)
	{
		dirLightColors[index] = visibleLight.finalColor;
		//光照方向是通过VisibleLight.LocakToWorldMatrix属性来获取的，该矩阵的第三列即为光源的前向向量，需要取反
		dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
		dirLightShadowData[index] = shadows.ReserveDirectionalShadows(visibleLight.light, index);
	}

	//设置点光源 将点光源的颜色和位置信息存储到数组
	void SetupPointLight(int index, ref VisibleLight visibleLight)
	{
		otherLightColors[index] = visibleLight.finalColor;
		//位置信息在本地到世界的转换矩阵的最后一列
		Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
		//将光照范围的平方的倒数存储在光源位置的w分量中
		position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
		otherLightPositions[index] = position;
		otherLightSpotAngles[index] = new Vector4(0f, 1f);
	}

	//将聚光灯的光源颜色，位置，方向存储到数组
	void SetupSpotLight(int index, ref VisibleLight visibleLight)
	{
		otherLightColors[index] = visibleLight.finalColor;
		Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
		position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
		otherLightPositions[index] = position;
		otherLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);

		Light light = visibleLight.light;
		float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
		float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
		float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
		otherLightSpotAngles[index] = new Vector4(angleRangeInv, -outerCos * angleRangeInv);
	}

	void SetupLights()
	{
		NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;

		int dirLightCount = 0;
		int otherLightCount = 0;

		for (int i = 0; i < visibleLights.Length; i++)
		{
			VisibleLight visibleLight = visibleLights[i];
			// if (visibleLight.lightType == LightType.Directional)
			// {
			// 	//VisibleLight结构体比较大，改用ref 传递引用而不是值，能省下值传递生成副本的开销
			// 	SetupDirectionalLight(i, ref visibleLight);
			// 	//设置限制
			// 	if (dirLightCount >= maxDirLightCount)
			// 	{
			// 		break;
			// 	}
			// }
			switch (visibleLight.lightType)
			{
				case LightType.Directional:
					if (dirLightCount < maxDirLightCount)
					{
						SetupDirectionalLight(dirLightCount++, ref visibleLight);
					}
					break;
				case LightType.Point:
					if (otherLightCount < maxOtherLightCount)
					{
						SetupPointLight(otherLightCount++, ref visibleLight);
					}
					break;
				case LightType.Spot:
					if (otherLightCount < maxOtherLightCount)
					{
						SetupSpotLight(otherLightCount++, ref visibleLight);
					}
					break;
			}
		}

		buffer.SetGlobalInt(dirLightCountId, dirLightCount);
		if (dirLightCount > 0)
		{
			buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
			buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirections);
			buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
		}

		buffer.SetGlobalInt(otherLightCountId, otherLightCount);
		if (otherLightCount > 0)
		{
			buffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
			buffer.SetGlobalVectorArray(otherLightPositionsId, otherLightPositions);
			buffer.SetGlobalVectorArray(otherLightDirectionsId, otherLightDirections);
			buffer.SetGlobalVectorArray(otherLightSpotAnglesId, otherLightSpotAngles);
		}
	}

	public void Cleanup()
	{
		shadows.Cleanup();
	}
}