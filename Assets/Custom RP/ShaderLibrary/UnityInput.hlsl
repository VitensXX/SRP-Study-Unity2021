//unity 标准输入库
#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNITY_INPUT_INCLUDED

float4x4 unity_ObjectToWorld;
float4x4 unity_WorldToObject;

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 glstate_matrix_projection;

float4x4 unity_prev_MatrixM;
float4x4 unity_prev_MatrixIM;

//这个矩阵包含一些再这里我们不需要的转换信息
real4 unity_WorldTransformParams;

#endif