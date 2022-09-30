#ifndef SCREEN_SPACE_UTILITY_INCLUDE
#define SCREEN_SPACE_UTILITY_INCLUDE

float4x4 invViewMatrix;
float4x4 invProjMatrix;

float4 SizeAndInvSize;

TEXTURE2D_X_FLOAT(_MinMaxDepthTexture);
SAMPLER(sampler_MinMaxDepthTexture);
TEXTURE2D(TileIndexAndSceneDistanceTexture);
SAMPLER(samplerTileIndexAndSceneDistanceTexture);

float3 GetWorldSpacePositionForUV(float2 uv,float size=4.0) {
	float deviceDepth = SAMPLE_TEXTURE2D(TileIndexAndSceneDistanceTexture, samplerTileIndexAndSceneDistanceTexture, uv).y;
	float2 screenUV = uv * size;
#if UNITY_REVERSED_Z
	deviceDepth = 1 - deviceDepth;
#endif
	deviceDepth = 2.0 * deviceDepth - 1.0;

#if UNITY_UV_STARTS_AT_TOP
	screenUV.y = 1.0f - screenUV.y;
#endif
	float3 positionVS = ComputeViewSpacePosition(screenUV, deviceDepth, invProjMatrix);
	float4 positionWS = float4(mul(unity_CameraToWorld, float4(positionVS, 1.0)).xyz, 1.0);
	return positionWS.xyz;
}


float3 GetWorldSpacePositionForIndex(int2 index, float size = 4.0) {
	float deviceDepth = TileIndexAndSceneDistanceTexture.Load(int3(index,0)).y;
	float2 screenUV = index * SizeAndInvSize.zw * size;
#if UNITY_REVERSED_Z
	deviceDepth = 1 - deviceDepth;
#endif
	deviceDepth = 2.0 * deviceDepth - 1.0;

#if UNITY_UV_STARTS_AT_TOP
	screenUV.y = 1.0f - screenUV.y;
#endif
	float3 positionVS = ComputeViewSpacePosition(screenUV, deviceDepth, invProjMatrix);
	float4 positionWS = float4(mul(unity_CameraToWorld, float4(positionVS, 1.0)).xyz, 1.0);
	return positionWS.xyz;
}


//-----------------------------------------------------------------------------------------
// Helper Funcs : RaySphereIntersection
//-----------------------------------------------------------------------------------------
float2 RaySphereIntersection(float3 rayOrigin, float3 rayDir, float3 sphereCenter, float sphereRadius)
{
	rayOrigin -= sphereCenter;
	float a = dot(rayDir, rayDir);
	float b = 2.0 * dot(rayOrigin, rayDir);
	float c = dot(rayOrigin, rayOrigin) - (sphereRadius * sphereRadius);
	float d = b * b - 4 * a * c;
	if (d < 0)
	{
		return -1;
	}
	else
	{
		d = sqrt(d);
		return float2(-b - d, -b + d) / (2 * a);
	}
}
#endif //SCREEN_SPACE_UTILITY_INCLUDE