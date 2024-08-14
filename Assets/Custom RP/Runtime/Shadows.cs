using UnityEngine;
using UnityEngine.Rendering;

public class Shadows
{
	const int maxShadowedDirectionalLightCount = 4;
	const int maxShadowedOtherLightCount = 16;
	const int maxCascades = 4;

	int shadowedDirLightCount;
	int shadowedOtherLightCount;

	const string bufferName = "Shadows";
	static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
	static int dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices");
	static int otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas");
	static int otherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices");
	static int otherShadowTilesId = Shader.PropertyToID("_OtherShadowTiles");
	static int cascadeCountId = Shader.PropertyToID("_CascadeCount");
	static int cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres");
	static int cascadeDataId = Shader.PropertyToID("_CascadeData");
	// static int shadowDistanceId = Shader.PropertyToID("_ShadowDistance");
	static int shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");
	static int shadowPancakingId = Shader.PropertyToID("_ShadowPancaking");
	static int shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");
	static Vector4[] cascadeCullingSpheres = new Vector4[maxCascades];//xyz:pos  w:半径
	static Vector4[] cascadeData = new Vector4[maxCascades];
	static Vector4[] otherShadowTiles = new Vector4[maxShadowedOtherLightCount];
	static Matrix4x4[] dirShadowMatrices = new Matrix4x4[maxShadowedDirectionalLightCount * maxCascades];
	static Matrix4x4[] otherShadowMatrices = new Matrix4x4[maxShadowedOtherLightCount];

	//PCF滤波模式
	static string[] directionalFilterKeywords = {
		"_DIRECTIONAL_PCF3",
		"_DIRECTIONAL_PCF5",
		"_DIRECTIONAL_PCF7",
	};

	static string[] cascadeBlendKeywords = {
		"_CASCADE_BLEND_SOFT",
		"_CASCADE_BLEND_DITHER"
	};

	static string[] shadowMaskKeywords = {
		"_SHADOW_MASK_ALWAYS",
		"_SHADOW_MASK_DISTANCE"
	};

	//非定向光的滤波模式
	static string[] otherFilterKeywords = {
		"_OTHER_PCF3",
		"_OTHER_PCF5",
		"_OTHER_PCF7",
	};

	CommandBuffer buffer = new CommandBuffer
	{
		name = bufferName
	};

	struct ShadowedDirectionalLight
	{
		public int visibleLightIndex;
		public float slopeScaleBias;
		//阴影视椎体近裁剪平面偏移
		public float nearPlaneOffset;
	}

	ShadowedDirectionalLight[] ShadowedDirectionalLights = new ShadowedDirectionalLight[maxShadowedDirectionalLightCount];
	int ShadowedDirectionalLightCount;

	struct ShadowedOtherLight
	{
		public int visibleLightIndex;
		public float slopeScaleBias;
		public float normalBias;
		public bool isPoint;
	}

	ShadowedOtherLight[] shadowedOtherLights = new ShadowedOtherLight[maxShadowedOtherLightCount];

	ScriptableRenderContext context;

	CullingResults cullingResults;

	ShadowSettings settings;
	bool useShadowMask;

	public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings settings)
	{
		this.context = context;
		this.cullingResults = cullingResults;
		this.settings = settings;
		ShadowedDirectionalLightCount = 0;
		shadowedOtherLightCount = 0;
		useShadowMask = false;
	}

	//存储可见光阴影数据 （阴影强度，阴影序号）
	public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex)
	{
		if (ShadowedDirectionalLightCount < maxShadowedDirectionalLightCount
			&& light.shadows != LightShadows.None
			&& light.shadowStrength > 0f
			//是否在阴影最大投射范围内有被改光源影响且需要投射阴影的的物体存在
			// && cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)
			)
		{
			float maskChannel = -1;
			//如果使用了shadowMask
			LightBakingOutput lightBaking = light.bakingOutput;
			if (
				lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
				lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
			)
			{
				useShadowMask = true;
				//得到光源shadowMask的通道索引
				maskChannel = lightBaking.occlusionMaskChannel;
			}

			if (!cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
			{
				return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
			}

			ShadowedDirectionalLights[ShadowedDirectionalLightCount] = new ShadowedDirectionalLight
			{
				visibleLightIndex = visibleLightIndex,
				slopeScaleBias = light.shadowBias,
				nearPlaneOffset = light.shadowNearPlane
			};

			return new Vector4(light.shadowStrength, settings.directional.cascadeCount * ShadowedDirectionalLightCount++, light.shadowNormalBias, maskChannel);
		}

		return new Vector4(0f, 0f, 0f, -1f);
	}

	//存储其他类型光源阴影
	public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex)
	{
		if (light.shadows == LightShadows.None || light.shadowStrength <= 0f)
		{
			return new Vector4(0f, 0f, 0f, -1f);
		}

		float maskChannel = -1f;

		// if (light.shadows != LightShadows.None && light.shadowStrength > 0f)
		// {
		LightBakingOutput lightBaking = light.bakingOutput;

		bool isPoint = light.type == LightType.Point;
		int newLightCount = shadowedOtherLightCount + (isPoint ? 6 : 1);

		if (
			newLightCount >= maxShadowedOtherLightCount ||
			!cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)
		)
		{
			return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
		}

		shadowedOtherLights[shadowedOtherLightCount] = new ShadowedOtherLight
		{
			visibleLightIndex = visibleLightIndex,
			slopeScaleBias = light.shadowBias,
			normalBias = light.shadowNormalBias,
			isPoint = isPoint
		};

		Vector4 data = new Vector4(
			light.shadowStrength, shadowedOtherLightCount,
			isPoint ? 1f : 0f, maskChannel
		);
		shadowedOtherLightCount = newLightCount;
		return data;

		// return new Vector4(light.shadowStrength, shadowedOtherLightCount++, 0f, maskChannel);
		// // }
		// // }
		// // return new Vector4(0f, 0f, 0f, -1f);
	}
	Vector4 atlasSizes;

	public void Render()
	{
		if (ShadowedDirectionalLightCount > 0)
		{
			RenderDirectionalShadows();
		}
		else
		{
			buffer.GetTemporaryRT(
				dirShadowAtlasId, 1, 1,
				32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap
			);
		}

		if (shadowedOtherLightCount > 0)
		{
			RenderOtherShadows();
		}
		else
		{
			buffer.SetGlobalTexture(otherShadowAtlasId, dirShadowAtlasId);
		}

		buffer.BeginSample(bufferName);
		SetKeywords(shadowMaskKeywords, useShadowMask ?
			(QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1) : -1);

		buffer.SetGlobalInt(
			cascadeCountId,
			shadowedDirLightCount > 0 ? settings.directional.cascadeCount : 0
		);
		float f = 1f - settings.directional.cascadeFade;
		buffer.SetGlobalVector(
			shadowDistanceFadeId, new Vector4(
				1f / settings.maxDistance, 1f / settings.distanceFade,
				1f / (1f - f * f)
			)
		);
		buffer.SetGlobalVector(shadowAtlasSizeId, atlasSizes);
		buffer.EndSample(bufferName);
		ExecuteBuffer();
	}

	void RenderDirectionalShadows()
	{
		int atlasSize = (int)settings.directional.atlasSize;
		atlasSizes.x = atlasSize;
		atlasSizes.y = 1f / atlasSize;
		buffer.GetTemporaryRT(dirShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
		buffer.SetRenderTarget(dirShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		buffer.ClearRenderTarget(true, false, Color.clear);
		buffer.SetGlobalFloat(shadowPancakingId, 1f);
		buffer.BeginSample(bufferName);
		ExecuteBuffer();

		int tiles = ShadowedDirectionalLightCount * settings.directional.cascadeCount;
		int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
		// int split = ShadowedDirectionalLightCount <= 1 ? 1 : 2;
		int tileSize = atlasSize / split;

		for (int i = 0; i < ShadowedDirectionalLightCount; i++)
		{
			RenderDirectionalShadows(i, split, tileSize);
		}

		// buffer.SetGlobalInt(cascadeCountId, settings.directional.cascadeCount);
		buffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
		buffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
		buffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);
		// buffer.SetGlobalFloat(shadowDistanceId, settings.maxDistance);
		// float f = 1f - settings.directional.cascadeFade;
		// buffer.SetGlobalVector(shadowDistanceFadeId, new Vector4(1f / settings.maxDistance, 1f / settings.distanceFade, 1f / (1f - f * f)));
		SetKeywords(directionalFilterKeywords, (int)settings.directional.filter - 1);
		SetKeywords(cascadeBlendKeywords, (int)settings.directional.cascadeBlend - 1);
		//传递图集大小和纹素大小
		// buffer.SetGlobalVector(shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize));
		buffer.EndSample(bufferName);
		ExecuteBuffer();
	}

	void RenderDirectionalShadows(int index, int split, int tileSize)
	{
		ShadowedDirectionalLight light = ShadowedDirectionalLights[index];
		var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex)
		{
			useRenderingLayerMaskTest = true
		};
		int cascadeCount = settings.directional.cascadeCount;
		int tileOffset = index * cascadeCount;
		Vector3 ratios = settings.directional.CascadeRatios;
		float cullingFactor = Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);
		float tileScale = 1f / split;
		for (int i = 0; i < cascadeCount; i++)
		{
			//找到与光的方向匹配的视图和投影矩阵，并给一个裁剪空间的立方体，该立方体与包含光源阴影的摄像机的可见区域重叠
			cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
				light.visibleLightIndex, i, cascadeCount, ratios, tileSize, light.nearPlaneOffset,
				out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
				out ShadowSplitData splitData
			);
			//得到第一个光源的包围球数据
			if (index == 0)
			{
				SetCascadeData(i, splitData.cullingSphere, tileSize);
				// Vector4 cullingSphere = splitData.cullingSphere;
				// //shader中通过距离的平方来判断是否在包围球中，这里提前将半径的平方存起来
				// cullingSphere.w *= cullingSphere.w;
				// cascadeCullingSpheres[i] = cullingSphere;
			}
			//剔除偏差
			splitData.shadowCascadeBlendCullingFactor = cullingFactor;
			shadowSettings.splitData = splitData;
			int tileIndex = tileOffset + i;
			//得到世界空间到灯光空间的转换矩阵
			// dirShadowMatrices[index] = projectionMatrix * viewMatrix;
			dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
				projectionMatrix * viewMatrix,
				SetTileViewport(tileIndex, split, tileSize), tileScale
			);
			// SetTileViewport(index, split, tileSize);
			buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
			// buffer.SetGlobalDepthBias(500000f, 0f);
			// buffer.SetGlobalDepthBias(0, 3f);
			buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
			ExecuteBuffer();
			context.DrawShadows(ref shadowSettings);
			buffer.SetGlobalDepthBias(0f, 0f);
		}
	}

	void RenderOtherShadows()
	{
		int atlasSize = (int)settings.other.atlasSize;
		atlasSizes.z = atlasSize;
		atlasSizes.w = 1f / atlasSize;
		buffer.GetTemporaryRT(
			otherShadowAtlasId, atlasSize, atlasSize,
			32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap
		);
		buffer.SetRenderTarget(
			otherShadowAtlasId,
			RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
		);
		buffer.ClearRenderTarget(true, false, Color.clear);
		buffer.SetGlobalFloat(shadowPancakingId, 1f);
		buffer.BeginSample(bufferName);
		ExecuteBuffer();

		int tiles = shadowedOtherLightCount;
		int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
		int tileSize = atlasSize / split;

		for (int i = 0; i < shadowedOtherLightCount;)
		{
			if (shadowedOtherLights[i].isPoint)
			{
				RenderPointShadows(i, split, tileSize);
				i += 6;
			}
			else
			{
				RenderSpotShadows(i, split, tileSize);
				i += 1;
			}
		}

		buffer.SetGlobalMatrixArray(otherShadowMatricesId, otherShadowMatrices);
		buffer.SetGlobalVectorArray(otherShadowTilesId, otherShadowTiles);
		SetKeywords(otherFilterKeywords, (int)settings.other.filter - 1);
		buffer.EndSample(bufferName);
		ExecuteBuffer();
	}

	void RenderSpotShadows(int index, int split, int tileSize)
	{
		ShadowedOtherLight light = shadowedOtherLights[index];
		var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex)
		{
			useRenderingLayerMaskTest = true
		};
		cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(
			light.visibleLightIndex, out Matrix4x4 viewMatrix,
			out Matrix4x4 projectionMatrix, out ShadowSplitData splitData
		);
		shadowSettings.splitData = splitData;

		float texelSize = 2f / (tileSize * projectionMatrix.m00);
		float filterSize = texelSize * ((float)settings.other.filter + 1f);
		float bias = light.normalBias * filterSize * 1.4142136f;
		Vector2 offset = SetTileViewport(index, split, tileSize);
		float tileScale = 1f / split;
		SetOtherTileData(index, offset, tileScale, bias);
		otherShadowMatrices[index] = ConvertToAtlasMatrix(
			projectionMatrix * viewMatrix, offset, tileScale
		);
		buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
		buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
		ExecuteBuffer();
		context.DrawShadows(ref shadowSettings);
		buffer.SetGlobalDepthBias(0f, 0f);
	}

	void RenderPointShadows(int index, int split, int tileSize)
	{
		ShadowedOtherLight light = shadowedOtherLights[index];
		var shadowSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex)
		{
			useRenderingLayerMaskTest = true
		};

		float texelSize = 2f / tileSize;
		float filterSize = texelSize * ((float)settings.other.filter + 1f);
		float bias = light.normalBias * filterSize * 1.4142136f;
		float tileScale = 1f / split;
		float fovBias = Mathf.Atan(1f + bias + filterSize) * Mathf.Rad2Deg * 2f - 90f;
		for (int i = 0; i < 6; i++)
		{
			cullingResults.ComputePointShadowMatricesAndCullingPrimitives(
				light.visibleLightIndex, (CubemapFace)i, fovBias,
				out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
				out ShadowSplitData splitData
			);
			viewMatrix.m11 = -viewMatrix.m11;
			viewMatrix.m12 = -viewMatrix.m12;
			viewMatrix.m13 = -viewMatrix.m13;
			shadowSettings.splitData = splitData;
			int tileIndex = index + i;
			// float texelSize = 2f / (tileSize * projectionMatrix.m00);
			// float filterSize = texelSize * ((float)settings.other.filter + 1f);
			// float bias = light.normalBias * filterSize * 1.4142136f;
			Vector2 offset = SetTileViewport(tileIndex, split, tileSize);
			// float tileScale = 1f / split;
			SetOtherTileData(tileIndex, offset, tileScale, bias);
			otherShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
				projectionMatrix * viewMatrix, offset, tileScale
			);

			buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
			buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
			ExecuteBuffer();
			context.DrawShadows(ref shadowSettings);
			buffer.SetGlobalDepthBias(0f, 0f);
		}
	}

	void SetOtherTileData(int index, Vector2 offset, float scale, float bias)
	{
		float border = atlasSizes.w * 0.5f;
		Vector4 data = Vector4.zero;
		data.x = offset.x * scale + border;
		data.y = offset.y * scale + border;
		data.z = scale - border - border;
		data.w = bias;
		otherShadowTiles[index] = data;
	}

	//设置关键字开启哪种PCF滤波模式
	void SetKeywords(string[] keywords, int enabledIndex)
	{
		// int enabledIndex = (int)settings.directional.filter - 1;
		for (int i = 0; i < keywords.Length; i++)
		{
			if (i == enabledIndex)
			{
				buffer.EnableShaderKeyword(keywords[i]);
			}
			else
			{
				buffer.DisableShaderKeyword(keywords[i]);
			}
		}
	}

	void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
	{
		//包围球直径除以阴影土块尺寸=纹素大小
		float texelSize = 2f * cullingSphere.w / tileSize;
		float filterSize = texelSize * ((float)settings.directional.filter + 1f);
		cullingSphere.w -= filterSize;
		//shader中通过距离的平方来判断是否在包围球中，这里提前将半径的平方存起来
		cullingSphere.w *= cullingSphere.w;
		cascadeCullingSpheres[index] = cullingSphere;
		// cascadeData[index].x = 1f / cullingSphere.w;
		//最坏的情况是正方形的对角线，所以放大根号2倍
		cascadeData[index] = new Vector4(1f / cullingSphere.w, filterSize * 1.4142136f);
	}

	Vector2 SetTileViewport(int index, int split, float tileSize)
	{
		Vector2 offset = new Vector2(index % split, index / split);
		buffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
		return offset;
	}

	Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, float scale)
	{
		if (SystemInfo.usesReversedZBuffer)
		{
			m.m20 = -m.m20;
			m.m21 = -m.m21;
			m.m22 = -m.m22;
			m.m23 = -m.m23;
		}

		// float scale = 1f / split;
		//-1~1 => 0~1
		m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
		m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
		m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
		m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
		m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
		m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
		m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
		m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;

		m.m20 = 0.5f * (m.m20 + m.m30);
		m.m21 = 0.5f * (m.m21 + m.m31);
		m.m22 = 0.5f * (m.m22 + m.m32);
		m.m23 = 0.5f * (m.m23 + m.m33);

		return m;
	}

	void ExecuteBuffer()
	{
		context.ExecuteCommandBuffer(buffer);
		buffer.Clear();
	}

	public void Cleanup()
	{
		buffer.ReleaseTemporaryRT(dirShadowAtlasId);
		if (shadowedOtherLightCount > 0)
		{
			buffer.ReleaseTemporaryRT(otherShadowAtlasId);
		}
		ExecuteBuffer();
	}
}