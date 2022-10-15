//公共方法库
#ifndef CUSTOM_COMMON_INCLUDED
#define CUSTOM_COMMON_INCLUDED

#include "UnityInput.hlsl"

// #define UNITY_MATRIX_M unity_ObjectToWorld
//这里面包含了下面注释的两个方法 TransformObjectToWorld 和 TransformWorldToHClip
// Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl
// #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
float3 TransformObjectToWorld (float3 positionOS) {
	return mul(unity_ObjectToWorld, float4(positionOS, 1.0)).xyz;
}

float4 TransformWorldToHClip (float3 positionWS) {
	return mul(unity_MatrixVP, float4(positionWS, 1.0));
}
	
#endif