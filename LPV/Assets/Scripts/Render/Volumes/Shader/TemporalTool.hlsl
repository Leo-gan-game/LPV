#ifndef TEMPORAL_TOOL_INCLUDE
#define TEMPORAL_TOOL_INCLUDE

float HistoryClip(float3 History, float3 Filtered, float3 NeighborMin, float3 NeighborMax)
{
	float3 BoxMin = NeighborMin;
	float3 BoxMax = NeighborMax;
	//float3 BoxMin = min( Filtered, NeighborMin );
	//float3 BoxMax = max( Filtered, NeighborMax );

	float3 RayOrigin = History;
	float3 RayDir = Filtered - History;
	RayDir = abs(RayDir) < (1.0 / 65536.0) ? (1.0 / 65536.0) : RayDir;
	float3 InvRayDir = rcp(RayDir);

	float3 MinIntersect = (BoxMin - RayOrigin) * InvRayDir;
	float3 MaxIntersect = (BoxMax - RayOrigin) * InvRayDir;
	float3 EnterIntersect = min(MinIntersect, MaxIntersect);
	return max(max(EnterIntersect.x, EnterIntersect.y), EnterIntersect.z);
}

#endif //TEMPORAL_TOOL_INCLUDE