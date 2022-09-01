#ifndef UNIVERSAL_FORWARD_RSM_LIT_PASS_INCLUDED
#define UNIVERSAL_FORWARD_RSM_LIT_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "SamplingLibrary.hlsl"

// GLES2 has limited amount of interpolators
#if defined(_PARALLAXMAP) && !defined(SHADER_API_GLES)
#define REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR
#endif

#if (defined(_NORMALMAP) || (defined(_PARALLAXMAP) && !defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR))) || defined(_DETAIL)
#define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR
#endif

// keep this file in sync with LitGBufferPass.hlsl

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 lightmapUV   : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                       : TEXCOORD0;
    DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 1);

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    float3 positionWS               : TEXCOORD2;
#endif

    float3 normalWS                 : TEXCOORD3;
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    float4 tangentWS                : TEXCOORD4;    // xyz: tangent, w: sign
#endif
    float3 viewDirWS                : TEXCOORD5;

    half4 fogFactorAndVertexLight   : TEXCOORD6; // x: fogFactor, yzw: vertex light

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord              : TEXCOORD7;
#endif

#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    float3 viewDirTS                : TEXCOORD8;
#endif
    float4 RSM_UV                   :TEXCOORD9;
    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    inputData.positionWS = input.positionWS;
#endif

    half3 viewDirWS = SafeNormalize(input.viewDirWS);
#if defined(_NORMALMAP) || defined(_DETAIL)
    float sgn = input.tangentWS.w;      // should be either +1 or -1
    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    inputData.normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz));
#else
    inputData.normalWS = input.normalWS;
#endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = viewDirWS;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif

    inputData.fogCoord = input.fogFactorAndVertexLight.x;
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
    inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);
}

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

#define SH_C0 0.282094792f // 1 / 2sqrt(pi)
#define SH_C1 0.488602512f // sqrt(3/pi) / 2

#define SH_cosLobe_C0 0.886226925f // sqrt(pi)/2 
#define SH_cosLobe_C1 1.02332671f // sqrt(pi/3) 

float4x4 RSM_VP;
float4x4 RSM_V;
StructuredBuffer<float4> _RandomBuffer;
float MaxSampleRadius;
float mweight;
float3 minAABB;
float3 maxAABB;
float3 cellSize;
float4x4 WorldToLightLocalMatrix;

float3 convertPointWSToGridIndex(float3 wPos) {
    return float3((wPos - minAABB) / cellSize);
}

float4 evalSH_direct(float3 dir) {
    return float4(SH_C0, -SH_C1 * dir.y, SH_C1 * dir.z, -SH_C1 * dir.x);
}

float4 dirToCosineLobe(float3 dir)
{
    return float4(SH_cosLobe_C0, -SH_cosLobe_C1 * dir.y, SH_cosLobe_C1 * dir.z, -SH_cosLobe_C1 * dir.x);
}

// Used in Standard (Physically Based) shader
Varyings LitPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

    // normalWS and tangentWS already normalize.
    // this is required to avoid skewing the direction during interpolation
    // also required for per-vertex lighting and SH evaluation
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    half3 viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
    half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);

    // already normalized from normal transform to WS.
    output.normalWS = normalInput.normalWS;
    output.viewDirWS = viewDirWS;
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR) || defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    real sign = input.tangentOS.w * GetOddNegativeScale();
    half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);
#endif
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    output.tangentWS = tangentWS;
#endif

#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS = GetViewDirectionTangentSpace(tangentWS, output.normalWS, viewDirWS);
    output.viewDirTS = viewDirTS;
#endif

    OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
    OUTPUT_SH(output.normalWS.xyz, output.vertexSH);

    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    output.positionWS = vertexInput.positionWS;
#endif
    float4 posV = mul(RSM_V, float4(vertexInput.positionWS, 1));
    output.RSM_UV = mul(RSM_VP, float4(vertexInput.positionWS, 1));
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif

    output.positionCS = vertexInput.positionCS;

    return output;
}

TEXTURE2D(RSM_Postion);      SAMPLER(sampler_RSM_Point_Clamp_Postion);
TEXTURE2D(RSM_Normal);       SAMPLER(sampler_RSM_Point_Clamp_Normal);
TEXTURE2D(RSM_Flux);         SAMPLER(sampler_RSM_Point_Clamp_Flux);

TEXTURE3D(gridRTex);      SAMPLER(sampler_gridRTex);
TEXTURE3D(gridGTex);       SAMPLER(sampler_gridGTex);
TEXTURE3D(gridBTex);         SAMPLER(sampler_gridBTex);
uint _RsmSampleCount;

half4 RSMLitPassFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(_PARALLAXMAP)
#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS = input.viewDirTS;
#else
    half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, input.viewDirWS);
#endif
    ApplyPerPixelDisplacement(viewDirTS, input.uv);
#endif

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    float3 worldPos = input.positionWS;
    float3 indirect = 0;
    
    float4 rec_ndc = input.RSM_UV/ input.RSM_UV.w;
    float2 rec_uv = rec_ndc.xy * 0.5 + 0.5;
    float RSMTexelSize = 1.0 / _RsmSampleCount;

#if defined(RSM_RENDER)
    /*for (uint idx = 0; idx < _RsmSampleCount; idx++)
    {
#if defined(RANDOM_BUF)
        float2 sample_coord = rec_uv + _RandomBuffer[idx].xy * MaxSampleRadius * RSMTexelSize;
        float weight = _RandomBuffer[idx].z;
#else
        float2 Xi = Hammersley(idx, _RsmSampleCount, HaltonSequence(idx));
        float ss1 = Xi.x * sin(Xi.y * 2.0 * 3.1415926);
        float ss2 = Xi.x * cos(Xi.y * 2.0 * 3.1415926);
        float2 sample_coord = rec_uv + float2(ss1, ss2) * MaxSampleRadius * RSMTexelSize;
        float weight = Xi.x * Xi.x;
#endif
        if (sample_coord.x >= 0.0&& sample_coord.y >= 0.0 ) {
            float3 vpl_normal = normalize(SAMPLE_TEXTURE2D(RSM_Normal, sampler_RSM_Point_Clamp_Normal, sample_coord).xyz * 2 - 1);
            float3 vpl_worldPos = SAMPLE_TEXTURE2D(RSM_Postion, sampler_RSM_Point_Clamp_Postion, sample_coord).xyz;
            float3 vpl_flux = SAMPLE_TEXTURE2D(RSM_Flux, sampler_RSM_Point_Clamp_Flux, sample_coord);
            float3 vpDir = (worldPos - vpl_worldPos);
            float3 indirect_result = (vpl_flux * max(0, dot(vpl_normal, vpDir)) * max(0, dot(input.normalWS, -vpDir))) / pow(length(worldPos - vpl_worldPos), 2.0);
            indirect_result *= (weight * mweight);
            indirect += indirect_result;
        }
        
    }*/
#if defined(RSM_DEBUG)
    color.rgb = indirect ;
#else
    color.rgb += indirect ;
#endif
#else
    
#endif
    float3 index = convertPointWSToGridIndex(worldPos);
    float3 uvw = index / 32.0;
    float3 Normal =  normalize(mul((float3x3)WorldToLightLocalMatrix, input.normalWS));
    float4 SHintensity = evalSH_direct(-Normal);
    float3 LpvIntensity = float3(
        dot(SHintensity, SAMPLE_TEXTURE3D(gridRTex, sampler_gridRTex, uvw)),
        dot(SHintensity, SAMPLE_TEXTURE3D(gridGTex, sampler_gridGTex, uvw)),
        dot(SHintensity, SAMPLE_TEXTURE3D(gridBTex, sampler_gridBTex, uvw))
        );
    indirect.rgb = max(0,LpvIntensity) * mweight;
#if defined(RSM_DEBUG)
    color.rgb = indirect;
#else
    color.rgb += indirect;
#endif
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, _Surface);
    return color;
}

#endif