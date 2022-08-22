#ifndef _Sampling_Library_
#define _Sampling_Library_

uint ReverseBits32_(uint bits)
{
	bits = (bits << 16) | (bits >> 16);
	bits = ((bits & 0x00ff00ff) << 8) | ((bits & 0xff00ff00) >> 8);
	bits = ((bits & 0x0f0f0f0f) << 4) | ((bits & 0xf0f0f0f0) >> 4);
	bits = ((bits & 0x33333333) << 2) | ((bits & 0xcccccccc) >> 2);
	bits = ((bits & 0x55555555) << 1) | ((bits & 0xaaaaaaaa) >> 1);
	return bits;
}

float RadicalInverseSpecialized(uint base, uint a)
{
	const float invBase = (float)1 / (float)base;
	uint reversedDigits = 0;
	float invBaseN = 1;
	while (a > 0) {
		uint next = a / base;
		uint digit = a - next * base;
		reversedDigits = reversedDigits * base + digit;
		invBaseN *= invBase;
		a = next;
	}
	return reversedDigits * invBaseN;
}

float RadicalInverseSpecialized2(uint a)
{
	return (float)ReverseBits32_(a) / (float)0xffffffffu;
}

float RadicalInverseSpecialized3(uint a)
{
	const uint BaseN[] = { 3,9,27,81,243,729,2187, 6561 ,19683 ,59049 ,177147 , 531441 ,1594323 , 4782969 ,14348907 ,43046721 ,129140163 ,387420489 ,1162261467 ,3486784401 };
	const float invBaseN[] = { 1.f / BaseN[0],1.f / BaseN[1],1.f / BaseN[2],1.f / BaseN[3],1.f / BaseN[4],1.f / BaseN[5],1.f / BaseN[6],1.f / BaseN[7],1.f / BaseN[8],1.f / BaseN[9],
	1.0 / BaseN[10],1.f / BaseN[11],1.f / BaseN[12], 1.f / BaseN[13], 1.f / BaseN[14], 1.f / BaseN[15], 1.f / BaseN[16], 1.f / BaseN[17], 1.f / BaseN[18], 1.f / BaseN[19] };
	const float4 inv1 = float4(invBaseN[0], invBaseN[1], invBaseN[2], invBaseN[3]);
	const float4 inv2 = float4(invBaseN[4], invBaseN[5], invBaseN[6], invBaseN[7]);
	const float4 inv3 = float4(invBaseN[8], invBaseN[9], invBaseN[10], invBaseN[11]);
	const float4 inv4 = float4(invBaseN[12], invBaseN[13], invBaseN[14], invBaseN[15]);
	const float4 inv5 = float4(invBaseN[16], invBaseN[17], invBaseN[18], invBaseN[19]);

	const uint4 A = a.xxxx;
	const uint4 digit1 = uint4 (A * (inv1 * 3)) % 3;
	const uint4 digit2 = uint4 (A * (inv2 * 3)) % 3;
	const uint4 digit3 = uint4 (A * (inv3 * 3)) % 3;
	const uint4 digit4 = uint4 (A * (inv4 * 3)) % 3;
	const uint4 digit5 = uint4 (A * (inv5 * 3)) % 3;

	const float4 reverse1 = inv1 * (float4) digit1;
	const float4 reverse2 = inv2 * (float4) digit2;
	const float4 reverse3 = inv3 * (float4) digit3;
	const float4 reverse4 = inv4 * (float4) digit4;
	const float4 reverse5 = inv5 * (float4) digit5;

	return (dot(1, reverse1) + dot(1, reverse2) + dot(1, reverse3) + dot(1, reverse4) + dot(1, reverse5));
}


uint HaltonSequence(uint Index, uint base = 3)
{
	uint result = 0;
	uint f = 1;
	uint i = Index;

	[unroll(255)]
	while (i > 0) {
		result += (f / base) * (i % base);
		i = floor(i / base);
	}
	return result;
}

float2 Hammersley(uint a) {
	return float2(RadicalInverseSpecialized2(a), RadicalInverseSpecialized3(a));
}

float2 Hammersley(uint Index, uint NumSamples)
{
	return float2((float)Index / (float)NumSamples, ReverseBits32_(Index));
}

float2 Hammersley(uint Index, uint NumSamples, uint2 Random)
{
	float E1 = frac((float)Index / NumSamples + float(Random.x & 0xffff) / (1 << 16));
	float E2 = float(ReverseBits32_(Index) ^ Random.y) * 2.3283064365386963e-10;
	return float2(E1, E2);
}

float2 Hammersley16(uint Index, uint NumSamples, uint2 Random)
{
	float E1 = frac((float)Index / NumSamples + float(Random.x) * (1.0 / 65536.0));
	float E2 = float((ReverseBits32_(Index) >> 16) ^ Random.y) * (1.0 / 65536.0);
	return float2(E1, E2);
}

#endif