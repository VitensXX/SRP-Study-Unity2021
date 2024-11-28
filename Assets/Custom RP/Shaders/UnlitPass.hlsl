#ifndef CUSTOM_UNLIT_PASS_INCLUDED
#define CUSTOM_UNLIT_PASS_INCLUDED

#include "../ShaderLibrary/Common.hlsl"
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
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)
*/

struct Attributes {
	float3 positionOS : POSITION;
	float4 color : COLOR;
	#if defined(_FLIPBOOK_BLENDING)
		float4 baseUV : TEXCOORD0;
		float flipbookBlend : TEXCOORD1;
	#else
		float2 baseUV : TEXCOORD0;
	#endif
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
	float4 positionCS_SS : SV_POSITION;
	#if defined(_VERTEX_COLORS)
		float4 color : VAR_COLOR;
	#endif
	float2 baseUV : VAR_BASE_UV;
	#if defined(_FLIPBOOK_BLENDING)
		float3 flipbookUVB : VAR_FLIPBOOK;
	#endif
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings UnlitPassVertex (Attributes input){
	// float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
	// return TransformWorldToHClip(positionWS);
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	float3 positionWS = TransformObjectToWorld(input.positionOS);
	output.positionCS_SS = TransformWorldToHClip(positionWS);
	//float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
	//output.baseUV = input.baseUV * baseST.xy + baseST.zw;
	#if defined(_VERTEX_COLORS)
		output.color = input.color;
	#endif
	output.baseUV.xy = TransformBaseUV(input.baseUV.xy);
	#if defined(_FLIPBOOK_BLENDING)
		output.flipbookUVB.xy = TransformBaseUV(input.baseUV.zw);
		output.flipbookUVB.z = input.flipbookBlend;
	#endif
	return output;
}

float4 UnlitPassFragment (Varyings input) : SV_TARGET {
	UNITY_SETUP_INSTANCE_ID(input);
	float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
	//float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
	//float4 base = baseMap * baseColor;
	InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);
	// return GetBufferColor(config.fragment, 0.05);
	// return float4(config.fragment.bufferDepth.xxx / 20.0, 1.0);
	// return float4(config.fragment.depth.xxx / 20.0, 1.0);
	#if defined(_VERTEX_COLORS)
		config.color = input.color;
	#endif

	#if defined(_FLIPBOOK_BLENDING)
		config.flipbookUVB = input.flipbookUVB;
		config.flipbookBlending = true;
	#endif

	#if defined(_NEAR_FADE)
		config.nearFade = true;
	#endif

	#if defined(_SOFT_PARTICLES)
		config.softParticles = true;
	#endif
	
	float4 base = GetBase(config);
	//AlphaTest
	#if defined(_CLIPPING)
		clip(base.a - GetCutoff(config));
	#endif

	#if defined(_DISTORTION)
		float2 distortion = GetDistortion(config) * base.a;
		// base.rgb = GetBufferColor(config.fragment, distortion).rgb;
		base.rgb = lerp(
			GetBufferColor(config.fragment, distortion).rgb, base.rgb,
			saturate(base.a - GetDistortionBlend(config))
		);
	#endif
	
	return float4(base.rgb, GetFinalAlpha(base.a));
}

#endif