Shader "Cloud/ScreenSpaceCloudShader"
{
    Properties
    {
        ShapeNoiseTex("ShapeNoiseTex", 3D) = "white" {}
        DetailNoiseTex("DetailNoiseTex", 3D) = "white" {}
        BlueNoiseTex("BlueNoiseTex", 2D) = "white" {}
        //_moveDir("Move Dir", Vector) = (1,0,0)
        //_moveScale("move Scale",Range(0,1)) = 1
        //_g("G",Range(0,1)) = 1
        //_MarchLength("March Length",Range(0.01,800)) = 300
        //_loopCount("march Number",Range(0,512)) = 1
        //_LightmarchNumber(" Light march Number",Range(0,512)) = 1
        //boxMin("box Min", Vector) = (-0.5,-0.5,-0.5)
        //boxMax("box Max", Vector) = (0.5,0.5,0.5)
        //_BlueNoiseEffect("Blue Noise Effect",Range(0.01,1)) = 1
        //_Pos2UVScale("_Pos2UVScale",Range(0.0001,1)) = 1
        _DensetyOffset("Densety Offset",Range(-20,20)) = 1
        _detailNoiseScale("_detailNoiseScale",Range(0.0001,100)) = 0.01
        _ShapeTexChanleWight("Shape Noise Wight", Vector) = (1,0,0,0)
        shapeOffset("shape Offset",Vector) = (1,0,0)
        _DetailTexChanleWight("Detail Wight", Vector) = (1,0,0,0)
        detailOffset("detail Offset",Vector) = (1,0,0)
        detailNoiseWight("detailNoiseWight",Range(0,4)) = 1
        offsetSpeed("offset Speed",Range(0,2)) = 1
        boundUVWScale("boundUVWScale",float) = 1
        _absorptivity("_absorptivity",float) = 1
        densityMultiplier("densityMultiplier",float) =1 
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite("Z Write", float) = 1
    }
        SubShader
        {
            Tags { "RenderType" = "Transparent" }
            LOD 100


            Pass
            {
                 Tags { "RenderType" = "Transparent" }
            ZTest Off
            ZWrite[_ZWrite]
            Blend Off
            //Blend[_SrcBlend][_DstBlend]
                 HLSLPROGRAM
            //#pragma exclude_renderers gles gles3 glcore
            //#pragma only_renderers gles gles3 glcore d3d11
            #pragma target 4.5
            
            #pragma vertex vert
            #pragma fragment frag
            #pragma enable_d3d11_debug_symbols
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE3D(ShapeNoiseTex);        SAMPLER(sampler_ShapeNoiseTex);
            TEXTURE3D(DetailNoiseTex);        SAMPLER(sampler_DetailNoiseTex);
            half4 ShapeNoiseTex_ST;
            half4 DetailNoiseTex_ST;
            #define SH_C0 0.282094792f // 1 / 2sqrt(pi)
            #define SH_C1 0.488602512f // sqrt(3/pi) / 2

            #define SH_cosLobe_C0 0.886226925f // sqrt(pi)/2 
            #define SH_cosLobe_C1 1.02332671f // sqrt(pi/3) 
            #define E 2.718281828459f
            half3 _moveDir;
            half _moveScale;
            half mweight;
            int _MaxMarchCount;
            int _LightMaxMarchNumber;
            half3 minAABB;
            half3 maxAABB;
            half3 cellSize;
            half _g;
            float _MarchLength;
            float _BlueNoiseEffect;
            float _DensetyOffset;
            half4x4 WorldToLightLocalMatrix;
            float boundUVWScale;
            TEXTURE2D_X_FLOAT(_MinMaxDepthTexture);
            SAMPLER(sampler_MinMaxDepthTexture);
            TEXTURE2D(TileIndexAndSceneDistanceTexture);
            SAMPLER(samplerTileIndexAndSceneDistanceTexture);
            
            TEXTURE2D(BlueNoiseTex);
            SAMPLER(sampler_BlueNoiseTex);

            half4 BlueNoiseTex_ST;

            half3 boxMin;
            half3 boxMax;
            half _Pos2UVScale;

            float4 _ShapeTexChanleWight;
            float3 _DetailTexChanleWight;

            float3 shapeOffset;
            float3 detailOffset;
            float offsetSpeed;
            float _detailNoiseScale;
            float detailNoiseWight;
            float _absorptivity;
            float densityMultiplier;

            float4 SizeAndInvSize;
            int JitterIndex;
            //映射原始值到新空间中的值。
            float Remap(float original_value, float original_min, float original_max, float new_min, float new_max)
            {
                return new_min + ((original_value - original_min) / (original_max - original_min)) * (new_max - new_min);
            }

            half Beer(half depth, half absorptivity = 1)
            { 
                return exp(-depth * absorptivity);
            }

            half BeerPowder(half depth, half absorptivity = 1)
            {
                return 2 * exp(-depth * absorptivity) * (1 - exp(-2 * depth));
            }

            half HenyeyGreenstein(half LightDotView, half G)
            {
                half G2 = G * G;
                return (1.0f - G2) / pow(1.0f + G2 - 2.0 * G * LightDotView, 3.0f / 2.0f) / (4.0f * PI);
            }



            half3 convertPointWSToGridIndex(half3 wPos) {
                return half3((wPos - minAABB) / cellSize);
            }
            struct Attributes
            {
                half4 positionOS   : POSITION;
                half3 normalOS     : NORMAL;
                half2 texcoord     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                half2 uv          : TEXCOORD0;
                half4 positionCS   : SV_POSITION;
                half4 positionWS   : TEXCOORD1;
                half3 normalWS     : TEXCOORD2;
                half3 viewDir      : TEXCOORD3;
            };
            half4 GetShadowPositionHClip(Attributes input)
            {
                half3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                half3 normalWS = TransformObjectToWorldNormal(input.normalOS);


                half4 positionCS = TransformWorldToHClip(positionWS);

        #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
        #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
        #endif

                return positionCS;
            }
            half2 RayBoxDst(half3 pos, half3 rayDir)
            {
                half3 t0 = (boxMin - pos) / rayDir;
                half3 t1 = (boxMax - pos) / rayDir;

                half3 tmin = min(t0, t1);
                half3 tmax = max(t0, t1);

                //射线到box两个相交点的距离, dstA最近距离， dstB最远距离
                half dstA = max(max(tmin.x, tmin.y), tmin.z);
                half dstB = min(min(tmax.x, tmax.y), tmax.z);

                half dstToBox = max(0, dstA);
                half dstInBox = max(0, dstB - dstToBox);

                return half2(dstToBox, dstInBox);
            }
            float SampleDensity(float3 pos) {
                float3 size = boxMax - boxMin;
                float3 uvw = (size * 0.5 + pos) * boundUVWScale;
                float3 shapeSamplePos = uvw + shapeOffset * offsetSpeed * _Time.y * _moveScale * _moveDir;
                //计算高度（0-1）
                float hightPercent = (pos.y - boxMin.y) / size.y;
                float hightGradient = saturate(Remap(hightPercent, 0, 0.5, 0, 1)) * saturate(Remap(hightPercent, 0, 0.7, 0, 1));
                float4 shapeNoise = SAMPLE_TEXTURE3D_LOD(ShapeNoiseTex, sampler_ShapeNoiseTex, shapeSamplePos, 0);
                float4 shapeWeightNormalized = _ShapeTexChanleWight / dot(_ShapeTexChanleWight, 1);
                float shapeFBM = dot(shapeNoise, shapeWeightNormalized) * hightGradient;
                float baseShapeDensity = shapeFBM + _DensetyOffset * 0.1;

                if (baseShapeDensity > 0) {
                    float3 detailSamplePos = uvw * _detailNoiseScale + detailOffset * offsetSpeed*_Time.x;
                    float3 detailNoise = SAMPLE_TEXTURE3D_LOD(DetailNoiseTex, sampler_DetailNoiseTex, detailSamplePos, 0);
                    float3 detailWeightNormalized = _DetailTexChanleWight / dot(_DetailTexChanleWight, 1);
                    float detailFBM = dot(detailNoise, detailWeightNormalized);

                    float detailErodeWeight = (1 - shapeFBM) * (1 - shapeFBM) * (1 - shapeFBM);
                    float cloudDensity = baseShapeDensity - (1 - detailFBM) * detailErodeWeight * detailNoiseWight;
                    return cloudDensity*0.1* densityMultiplier;
                }
                return 0;
            }

            half GetCurrentPositionLum(half3 currentPos,half3 lightDir)
            {
                //计算包围盒
                half rayBoxDst = RayBoxDst(currentPos, lightDir).y;
                
                float setpSize = rayBoxDst / _LightMaxMarchNumber;

                //当前步进的长度
                half marchingLength = 0;
                //总密度
                half totalDensity = 0;
                //[unroll(32)]
                //指定步进次数marchNumber进行步进
                for (int march = 0; march <= _LightMaxMarchNumber; march++)
                {
                    //向前步进l个长度
                    marchingLength += setpSize;
                    //当前步进的位置
                    half3 pos = currentPos + lightDir * marchingLength;
                    //获取当前位置的密度
                    half density = SampleDensity(pos);

                    //计算总密度
                    totalDensity += max(0, density*setpSize);
                }
                //根据总密度计算当前采样点的光照
                //
                return 0.1 + Beer(totalDensity, _absorptivity) * (1 - 0.1);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                output.uv = TRANSFORM_TEX(input.texcoord, BlueNoiseTex);
                output.positionCS = GetShadowPositionHClip(input);
                output.positionWS = half4(TransformObjectToWorld(input.positionOS.xyz), 1);
                float3 viewDir = mul(unity_CameraInvProjection, float4(input.texcoord * 2.0 - 1.0, 0, -1)).xyz;
                output.viewDir = mul(unity_CameraToWorld, float4(viewDir, 0)).xyz;
                
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

           

            

            half4 frag(Varyings input) : SV_Target
            {
                half4 col = 0;
                half density = 0;
                float3 cameraPos = GetCameraPositionWS();
                float3 viewDir = normalize(input.viewDir);
                float2 uvPixelSize = SizeAndInvSize.zw * 2.0f;
                float2 uv = input.uv + uvPixelSize * int2(JitterIndex % 2, JitterIndex / 2);
                float depth =  SAMPLE_TEXTURE2D(TileIndexAndSceneDistanceTexture, samplerTileIndexAndSceneDistanceTexture, uv).y; depthZ
                float dstToObj = LinearEyeDepth(depth, _ZBufferParams);
                half2 rayBoxDst = RayBoxDst(cameraPos, viewDir);

                float dstToCloud = rayBoxDst.x;
                float dstInCloud = rayBoxDst.y;

                //穿出云覆盖范围的位置(结束位置)
                float endPos = dstToCloud + dstInCloud;
                
                //不在包围盒内或被物体遮挡 直接显示背景
                if (dstInCloud <= 0 || dstToObj <= dstToCloud)
                {
                    return half4(0, 0, 0, 1);
                }
                
                half blueNoise = SAMPLE_TEXTURE2D_LOD(BlueNoiseTex, sampler_BlueNoiseTex, input.uv, 0).r;
                float currentMarchLength = dstToCloud + _MarchLength * blueNoise* _BlueNoiseEffect;
                half3 pos = cameraPos + currentMarchLength * viewDir;
                float marchLength = _MarchLength;

                half transmittance = 1;
                float3 lightEnergy = 0;

                half g = _g;
                int count = 0;
                for (int i = 0; i < _MaxMarchCount; i++) {
                    pos = cameraPos + currentMarchLength * viewDir;
                    
                    //pos += _Time.y * _moveScale * _moveDir;//
                    count++;
                    if (dstToObj <= currentMarchLength || endPos <= currentMarchLength)
                    {
                        break;
                    }

                    float density = SampleDensity(pos) ;
                    half4 shadowCoord = TransformWorldToShadowCoord(pos);
                    Light light = GetMainLight(shadowCoord);

                    half3 lightDir = light.direction;
                    half phase = HenyeyGreenstein(dot(viewDir, lightDir), g);
                    
                    if (density > 0) {
                       
                        half lightTransmittance = GetCurrentPositionLum(pos, lightDir);
                        lightEnergy += density * _MarchLength * transmittance * lightTransmittance * phase *light.shadowAttenuation * light.color;
                        transmittance *= Beer(density * _MarchLength, _absorptivity);

                        //exit early if t is close zero
                        if (transmittance < 0.01) {
                            break;
                        }
                    }
                    currentMarchLength += _MarchLength;
                }
                // sample the texture
                col.a += transmittance;
                col.rgb += lightEnergy;
                return col;
            }
                ENDHLSL
            }

        }
}
