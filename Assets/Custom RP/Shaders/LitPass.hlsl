#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED

//#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

// CBUFFER_START(UnityPerMaterial)
// float4 _BaseColor;
// CBUFFER_END

/*
//纹理和采样器是着色器资源，必须在全局定义，不能放入缓冲区中
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
	//	float4 _BaseColor;
	UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
	UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
	UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
	UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)
*/

struct Attributes {
	float3 positionOS : POSITION;
	float3 normalOS : NORMAL;
	float2 baseUV : TEXCOORD0;
	GI_ATTRIBUTE_DATA
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
	float4 positionCS : SV_POSITION;
	float3 positionWS : VAR_POSITION;
	float3 normalWS : VAR_NORMAL;
	float2 baseUV : VAR_BASE_UV;
	GI_VARYINGS_DATA
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings LitPassVertex (Attributes input){
	// float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
	// return TransformWorldToHClip(positionWS);
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	TRANSFER_GI_DATA(input, output);
	output.positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS = TransformWorldToHClip(output.positionWS);
	output.normalWS = TransformObjectToWorldNormal(input.normalOS);
	//float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	//output.baseUV = input.baseUV * baseST.xy + baseST.zw;
	output.baseUV = TransformBaseUV(input.baseUV);
	return output;
}

void ClipLOD (float2 positionCS, float fade) {
	#if defined(LOD_FADE_CROSSFADE)
		//float dither = (positionCS.y % 32) / 32; //条状的过渡
		float dither = InterleavedGradientNoise(positionCS.xy, 0);
		clip(fade + (fade < 0.0 ? dither : -dither));
	#endif
}

float4 LitPassFragment (Varyings input) : SV_TARGET {
	UNITY_SETUP_INSTANCE_ID(input);
	ClipLOD(input.positionCS.xy, unity_LODFade.x);
	//float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
	//float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
	//float4 base = baseMap * baseColor;
	float4 base = GetBase(input.baseUV);
	
	//AlphaTest
	#if defined(_CLIPPING)
		clip(base.a - GetCutoff(input.baseUV));
	#endif
	
	// base.rgb = input.normalWS;
	// base.rgb = abs(length(input.normalWS) - 1.0) * 10.0;
	// base.rgb = normalize(input.normalWS);
	// return base;

	Surface surface;
	surface.position = input.positionWS;
	surface.normal = normalize(input.normalWS);
	// surface.normal = NormalTangentToWorld(GetNormalTS(input.baseUV), input.normalWS, input.tangentWS);
	surface.interpolatedNormal = input.normalWS;
	surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
	surface.depth = -TransformWorldToView(input.positionWS).z;
	surface.color = base.rgb;
	surface.alpha = base.a;
	surface.metallic = GetMetallic(input.baseUV);
	surface.smoothness = GetSmoothness(input.baseUV);
	surface.fresnelStrength = GetFresnel(input.baseUV);
	//计算抖动值
	surface.dither = InterleavedGradientNoise(input.positionCS.xy, 0);
	surface.renderingLayerMask = asuint(unity_RenderingLayer.x);
	#if defined(_PREMULTIPLY_ALPHA)
		BRDF brdf = GetBRDF(surface, true);
	#else
		BRDF brdf = GetBRDF(surface);
	#endif
	
	//获取全局光照
	GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);
	float3 color = GetLighting(surface, brdf, gi);
	color += GetEmission(input.baseUV);
	return float4(color, GetFinalAlpha(surface.alpha));
}

#endif