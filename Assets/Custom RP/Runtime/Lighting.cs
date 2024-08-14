using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using System.Collections.Generic;

public class Lighting
{
	const string bufferName = "Lighting";
	static string lightsPerObjectKeyword = "_LIGHTS_PER_OBJECT";
	const int maxDirLightCount = 4;
	// static int dirLightColorId = Shader.PropertyToID("_DirectionalLightColor");
	// static int dirLightDirectionId = Shader.PropertyToID("_DirectionalLightDirection");
	static int dirLightCountId = Shader.PropertyToID("_DirectionalLightCount");
	static int dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors");
	static int dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirectionsAndMasks");
	static int dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");
	static Vector4[] dirLightColors = new Vector4[maxDirLightCount];
	static Vector4[] dirLightDirectionsAndMasks = new Vector4[maxDirLightCount];
	static Vector4[] dirLightShadowData = new Vector4[maxDirLightCount];

	//其他类型光源
	const int maxOtherLightCount = 64;
	static int otherLightCountId = Shader.PropertyToID("_OtherLightCount");
	static int otherLightColorsId = Shader.PropertyToID("_OtherLightColors");
	static int otherLightPositionsId = Shader.PropertyToID("_OtherLightPositions");
	static int otherLightDirectionsId = Shader.PropertyToID("_OtherLightDirectionsAndMasks");
	static int otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles");
	static int otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");
	static Vector4[] otherLightColors = new Vector4[maxOtherLightCount];
	static Vector4[] otherLightPositions = new Vector4[maxOtherLightCount];
	static Vector4[] otherLightDirectionsAndMasks = new Vector4[maxOtherLightCount];
	static Vector4[] otherLightSpotAngles = new Vector4[maxOtherLightCount];
	static Vector4[] otherLightShadowData = new Vector4[maxOtherLightCount];

	CommandBuffer buffer = new CommandBuffer
	{
		name = bufferName
	};

	CullingResults cullingResults;
	Shadows shadows = new Shadows();

	public void Setup(ScriptableRenderContext context, CullingResults cullingResults,
		ShadowSettings shadowSettings, bool useLightsPerObject, int renderingLayerMask)
	{
		this.cullingResults = cullingResults;
		buffer.BeginSample(bufferName);
		shadows.Setup(context, cullingResults, shadowSettings);
		SetupLights(useLightsPerObject, renderingLayerMask);
		shadows.Render();
		buffer.EndSample(bufferName);
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}

	void SetupDirectionalLight(int index, int visibleIndex, ref VisibleLight visibleLight, Light light)
	{
		dirLightColors[index] = visibleLight.finalColor;
		//光照方向是通过VisibleLight.LocakToWorldMatrix属性来获取的，该矩阵的第三列即为光源的前向向量，需要取反
		Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
		dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
		dirLightDirectionsAndMasks[index] = dirAndMask;
		dirLightShadowData[index] = shadows.ReserveDirectionalShadows(light, visibleIndex);
	}

	//设置点光源 将点光源的颜色和位置信息存储到数组
	void SetupPointLight(int index, int visibleIndex, ref VisibleLight visibleLight, Light light)
	{
		otherLightColors[index] = visibleLight.finalColor;
		//位置信息在本地到世界的转换矩阵的最后一列
		Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
		//将光照范围的平方的倒数存储在光源位置的w分量中
		position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
		otherLightPositions[index] = position;
		otherLightSpotAngles[index] = new Vector4(0f, 1f);

		Vector4 dirAndmask = Vector4.zero;
		dirAndmask.w = light.renderingLayerMask.ReinterpretAsFloat();
		otherLightDirectionsAndMasks[index] = dirAndmask;
		// Light light = visibleLight.light;
		otherLightShadowData[index] = shadows.ReserveOtherShadows(light, visibleIndex);
	}

	//将聚光灯的光源颜色，位置，方向存储到数组
	void SetupSpotLight(int index, int visibleIndex, ref VisibleLight visibleLight, Light light)
	{
		otherLightColors[index] = visibleLight.finalColor;
		Vector4 position = visibleLight.localToWorldMatrix.GetColumn(3);
		position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
		otherLightPositions[index] = position;
		Vector4 dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
		dirAndMask.w = light.renderingLayerMask.ReinterpretAsFloat();
		otherLightDirectionsAndMasks[index] = dirAndMask;

		// Light light = visibleLight.light;
		float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
		float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * visibleLight.spotAngle);
		float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
		otherLightSpotAngles[index] = new Vector4(angleRangeInv, -outerCos * angleRangeInv);
		otherLightShadowData[index] = shadows.ReserveOtherShadows(light, visibleIndex);
	}

	void SetupLights(bool useLightsPerObject, int renderingLayerMask)
	{
		NativeArray<int> indexMap = useLightsPerObject ? cullingResults.GetLightIndexMap(Allocator.Temp) : default;

		NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;

		int dirLightCount = 0;
		int otherLightCount = 0;

		int i;
		for (i = 0; i < visibleLights.Length; i++)
		{
			int newIndex = -1;
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
			Light light = visibleLight.light;
			if ((light.renderingLayerMask & renderingLayerMask) != 0)
			{
				switch (visibleLight.lightType)
				{
					case LightType.Directional:
						if (dirLightCount < maxDirLightCount)
						{
							SetupDirectionalLight(dirLightCount++, i, ref visibleLight, light);
						}
						break;
					case LightType.Point:
						if (otherLightCount < maxOtherLightCount)
						{
							newIndex = otherLightCount;
							SetupPointLight(otherLightCount++, i, ref visibleLight, light);
						}
						break;
					case LightType.Spot:
						if (otherLightCount < maxOtherLightCount)
						{
							newIndex = otherLightCount;
							SetupSpotLight(otherLightCount++, i, ref visibleLight, light);
						}
						break;
				}
			}
			if (useLightsPerObject)
			{
				indexMap[i] = newIndex;
			}
		}

		if (useLightsPerObject)
		{
			for (; i < indexMap.Length; i++)
			{
				indexMap[i] = -1;
			}
			cullingResults.SetLightIndexMap(indexMap);
			indexMap.Dispose();
			Shader.EnableKeyword(lightsPerObjectKeyword);
		}
		else
		{
			Shader.DisableKeyword(lightsPerObjectKeyword);
		}

		buffer.SetGlobalInt(dirLightCountId, dirLightCount);
		if (dirLightCount > 0)
		{
			buffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
			buffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightDirectionsAndMasks);
			buffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
		}

		buffer.SetGlobalInt(otherLightCountId, otherLightCount);
		if (otherLightCount > 0)
		{
			buffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
			buffer.SetGlobalVectorArray(otherLightPositionsId, otherLightPositions);
			buffer.SetGlobalVectorArray(otherLightDirectionsId, otherLightDirectionsAndMasks);
			buffer.SetGlobalVectorArray(otherLightSpotAnglesId, otherLightSpotAngles);
			buffer.SetGlobalVectorArray(otherLightShadowDataId, otherLightShadowData);
		}
	}

	public void Cleanup()
	{
		shadows.Cleanup();
	}
}