#ifndef CUSTOM_SHADOWS_INCLUDED
#define CUSTOM_SHADOWS_INCLUDED

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4


// As the atlas isn't a regular texture let's define it via the TEXTURE2D_SHADOW macro instead to be clear,
// even though it doesn't make a difference for the platforms that we support. 
// And we'll use a special SAMPLER_CMP macro to define the sampler state, 
// as this does define a different way to sample shadow maps, because regular bilinear filtering doesn't make sense for depth data.
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
	int _CascadeCount;
	float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
	float4 _CascadeData[MAX_CASCADE_COUNT];
	float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
	// float _ShadowDistance;
	float4 _ShadowDistanceFade;
CBUFFER_END

//阴影数据
struct ShadowData {
	int cascadeIndex;
	float strength;
};

float FadedShadowStrength (float distance, float scale, float fade) {
	return saturate((1.0 - distance * scale) * fade);
}

//得到世界空间的表面阴影数据
ShadowData GetShadowData (Surface surfaceWS) {
	ShadowData data;
	data.strength = FadedShadowStrength(surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y);
	int i;
	for (i = 0; i < _CascadeCount; i++) {
		float4 sphere = _CascadeCullingSpheres[i];
		float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz);
		if (distanceSqr < sphere.w) {
			//如果绘制在最后一个级联的范围中
			if (i == _CascadeCount - 1) {
				data.strength *= FadedShadowStrength(distanceSqr,  _CascadeData[i].x, _ShadowDistanceFade.z);
			}
			break;
		}
	}
	//超过了最后一个最大的级联范围，视为没有阴影了，不需要采样阴影
	if (i == _CascadeCount) {
		data.strength = 0.0;
	}
	data.cascadeIndex = i;
	return data;
}

struct DirectionalShadowData {
	float strength;
	int tileIndex;
	float normalBias;
};

float SampleDirectionalShadowAtlas (float3 positionSTS) {
	return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionSTS);
}

float GetDirectionalShadowAttenuation (DirectionalShadowData directional, ShadowData global, Surface surfaceWS) {
	if (directional.strength <= 0.0) {
		return 1.0;
	}
	float3 normalBias = surfaceWS.normal * (directional.normalBias * _CascadeData[global.cascadeIndex].y);
	float3 positionSTS = mul(_DirectionalShadowMatrices[directional.tileIndex], float4(surfaceWS.position + normalBias, 1.0)).xyz;
	float shadow = SampleDirectionalShadowAtlas(positionSTS);
	return lerp(1.0, shadow, directional.strength);
}

#endif