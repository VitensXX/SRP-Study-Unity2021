//unity 标准输入库
#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNITY_INPUT_INCLUDED

CBUFFER_START(UnityPerDraw)
    float4x4 unity_ObjectToWorld;
    float4x4 unity_WorldToObject;
    float4 unity_LODFade;
    //这个矩阵包含一些再这里我们不需要的转换信息
    real4 unity_WorldTransformParams;
    float4 unity_RenderingLayer;
    real4 unity_LightData;
	real4 unity_LightIndices[2];

    //遮蔽探针（Occlusion Probe)，用于动态物体的ShadowMask数据获取
    float4 unity_ProbesOcclusion;
    float4 unity_SpecCube0_HDR;
    
    float4 unity_LightmapST;
	float4 unity_DynamicLightmapST;

    //光照探针使用
    float4 unity_SHAr;
	float4 unity_SHAg;
	float4 unity_SHAb;
	float4 unity_SHBr;
	float4 unity_SHBg;
	float4 unity_SHBb;
	float4 unity_SHC;

    //光照探针代理使用 LPPV
    float4 unity_ProbeVolumeParams;
	float4x4 unity_ProbeVolumeWorldToObject;
	float4 unity_ProbeVolumeSizeInv;
	float4 unity_ProbeVolumeMin;
CBUFFER_END

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 glstate_matrix_projection;

float4x4 unity_prev_MatrixM;
float4x4 unity_prev_MatrixIM;

float3 _WorldSpaceCameraPos;


#endif