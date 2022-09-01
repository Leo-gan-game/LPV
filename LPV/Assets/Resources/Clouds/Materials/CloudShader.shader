Shader "Unlit/CloudShader"
{
    Properties
    {
        CloudTex ("Texture", 3D) = "white" {}
        BlueNoiseTex("BlueNoiseTex", 2D) = "white" {}
        _moveDir("Move Dir", Vector) = (1,0,0)
        _moveScale("move Scale",Range(0,1)) = 1
        _g("G",Range(0,1)) = 1
        _loopCount("march Number",Range(0,512)) = 1
        _LightmarchNumber(" Light march Number",Range(0,512)) = 1
        boxMin("box Min", Vector) = (-0.5,-0.5,-0.5)
        boxMax("box Max", Vector) = (0.5,0.5,0.5)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite("Z Write", Float) = 1
    }
        SubShader
    {
        Tags { "RenderType" = "Transparent" }
        LOD 100
        

        Pass
        {
             Tags { "RenderType" = "Transparent" }
             ZWrite [_ZWrite]
             Blend[_SrcBlend][_DstBlend]
             HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            //#pragma enable_d3d11_debug_symbols
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE3D(CloudTex);        SAMPLER(sampler_CloudTex);
            #define SH_C0 0.282094792f // 1 / 2sqrt(pi)
            #define SH_C1 0.488602512f // sqrt(3/pi) / 2

            #define SH_cosLobe_C0 0.886226925f // sqrt(pi)/2 
            #define SH_cosLobe_C1 1.02332671f // sqrt(pi/3) 
            #define E 2.718281828459f
            float3 _moveDir;
            float _moveScale;
            float mweight;
            int _loopCount;
            int _LightmarchNumber;
            float3 minAABB;
            float3 maxAABB;
            float3 cellSize;
            float _g;
            
            float4x4 WorldToLightLocalMatrix;
            TEXTURE2D_X_FLOAT(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);
            TEXTURE2D(BlueNoiseTex);
            SAMPLER(sampler_BlueNoiseTex);
            float4 BlueNoiseTex_ST;
            float4 _CameraDepthTexture_TexelSize;
            float4 CloudTex_ST;
            TEXTURE3D(gridRTex);      SAMPLER(sampler_gridRTex);
            TEXTURE3D(gridGTex);      SAMPLER(sampler_gridGTex);
            TEXTURE3D(gridBTex);      SAMPLER(sampler_gridBTex);
            float3 boxMin;
            float3 boxMax;


            float Beer(float depth, float absorptivity = 1)
            {
                return exp(-depth * absorptivity);
            }

            float BeerPowder(float depth, float absorptivity = 1)
            {
                return 2*exp(depth * absorptivity) * (1 - exp(-2 * depth));
            }

            float HenyeyGreenstein(float LightDotView, float G)
            {
                float G2 = G * G;
                return (1.0f - G2) / pow(1.0f + G2 - 2.0 * G * LightDotView, 3.0f / 2.0f) / (4.0f * PI);
            }

            float4 evalSH_direct(float3 dir) {
                return float4(SH_C0, -SH_C1 * dir.y, SH_C1 * dir.z, -SH_C1 * dir.x);
            }

            float4 dirToCosineLobe(float3 dir)
            {
                return float4(SH_cosLobe_C0, -SH_cosLobe_C1 * dir.y, SH_cosLobe_C1 * dir.z, -SH_cosLobe_C1 * dir.x);
            }

            float3 convertPointWSToGridIndex(float3 wPos) {
                return float3((wPos - minAABB) / cellSize);
            }
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 texcoord     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float2 uv          : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
                float4 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float3 viewDir      : TEXCOORD3;
            };
            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);


                float4 positionCS = TransformWorldToHClip(positionWS);

        #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
        #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
        #endif

                return positionCS;
            }
            float2 RayBoxDst(float3 pos, float3 rayDir)
            {
                float3 t0 = (boxMin - pos) / rayDir;
                float3 t1 = (boxMax - pos) / rayDir;

                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);

                //射线到box两个相交点的距离, dstA最近距离， dstB最远距离
                float dstA = max(max(tmin.x, tmin.y), tmin.z);
                float dstB = min(min(tmax.x, tmax.y), tmax.z);

                float dstToBox = max(0, dstA);
                float dstInBox = max(0, dstB - dstToBox);

                return float2(dstToBox, dstInBox);
            }

            float GetCurrentPositionLum(float3 currentPos,float3 lightDirLS)
            {
                //计算包围盒
                float rayBoxDst = RayBoxDst(currentPos, lightDirLS).y;
                //当前步进的长度
                float marchingLength = 0;
                //总密度
                float totalDensity = 0;
                //[unroll(32)]
                //指定步进次数marchNumber进行步进
                for (int march = 0; march <= _LightmarchNumber; march++)
                {
                    //向前步进l个长度
                    marchingLength += 1/ _LightmarchNumber;
                    //当前步进的位置
                    float3 pos = currentPos + lightDirLS * marchingLength;

                    //如果超出包围盒直接退出
                    //if (marchingLength > rayBoxDst)
                     //   break;

                    //获取当前位置的密度
                    float density = SAMPLE_TEXTURE3D_LOD(CloudTex, sampler_CloudTex, pos,0).r;
                    //计算总密度
                    totalDensity += density / _LightmarchNumber;
                }
                //根据总密度计算当前采样点的光照
                return BeerPowder(totalDensity);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                output.uv = TRANSFORM_TEX(input.texcoord, BlueNoiseTex);
                output.positionCS = GetShadowPositionHClip(input);
                output.positionWS = float4(TransformObjectToWorld(input.positionOS.xyz), 1);
               
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }



            half4 frag(Varyings input) : SV_Target
            {
                half4 col = 0;
                float density = 0;
                float3 dirWS = normalize(input.positionWS - _WorldSpaceCameraPos);
                float3 dirLSnormalized = TransformWorldToObjectDir(dirWS);
                float3 dirWSnormalized = TransformObjectToWorldDir(dirLSnormalized,false);
                float l = 1.0 / _loopCount;
                float3 size = boxMax - boxMin;
                float3 pos = TransformWorldToObject(input.positionWS);
                //float2 uv = (size.zy * 0.5f + (pos.zy - 0)) / max(size.x, size.y);
                float bn = SAMPLE_TEXTURE2D_LOD(BlueNoiseTex, sampler_BlueNoiseTex, input.uv, 0).r;
                
                
               
                float3 indirect = 0;
                float totaldensity=0;
                float3 totalLum=0;
                float g = _g;
                float rayBoxDst = RayBoxDst(pos, dirLSnormalized).y;
                for (int i = 0; i < _loopCount; i++) {
                    float3 positionWS = input.positionWS + dirWSnormalized * l * i;
                    float4 positionCS = TransformWorldToHClip(positionWS);
                    float4 screenPos = ComputeScreenPos(positionCS);
                    float2 screenUV = screenPos.xy/ screenPos.w;
                    int2 posCS = screenUV * _CameraDepthTexture_TexelSize.zw;
                    float d = LOAD_TEXTURE2D_X(_CameraDepthTexture, posCS).x;
                    float eye_z = LinearEyeDepth(d, _ZBufferParams);
                    

                    if (l * i < rayBoxDst&& eye_z>positionCS.w){
                        pos = TransformWorldToObject(positionWS) ;
                        pos += (dirLSnormalized*l * i) ;

                        positionWS = TransformObjectToWorld(pos);
                        pos = pos + 0.5 + (bn - 0.5) * 2 * l+_Time.x* _moveDir* _moveScale;
                        float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                        Light light = GetMainLight(shadowCoord);

                        float3 lightDir = light.direction;
                        float3 lightDirLS = TransformWorldToObjectDir(lightDir);//转换灯光方向为本地坐标系
                        float phase = HenyeyGreenstein(dot(dirWS, lightDir), g);
                        float3 cloudRGB = SAMPLE_TEXTURE3D_LOD(CloudTex, sampler_CloudTex, pos* CloudTex_ST.x,0).r;
                        float density = cloudRGB ;// dot(cloudRGB, float3(0.5, 0.25, 0.125));
                        if (density > 0) {

                            float3 lum = GetCurrentPositionLum(pos, lightDirLS) * light.shadowAttenuation*light.color;
//#if _ADD_LIGHT_ON         
                            lum *= density * l;
                            int addLightsCount = GetAdditionalLightsCount();
                            for (int idx = 0; idx < addLightsCount; idx++)
                            {
                                Light addlight = GetAdditionalLight(idx, positionWS);
                                lightDir = light.direction;
                                lightDirLS = TransformWorldToObjectDir(lightDir);//转换灯光方向为本地坐标系
                                lum += GetCurrentPositionLum(pos, lightDirLS)* addlight.color* addlight.distanceAttenuation.r * light.shadowAttenuation;
                            }
//#endif 
                            totalLum += Beer(totaldensity) * lum * phase;
                            totaldensity += density*l;
                            //float3 index = convertPointWSToGridIndex(positionWS);
                            //float3 uvw = index / 32.0;
                            //float3 Normal = normalize(mul((float3x3)WorldToLightLocalMatrix, input.normalWS));
                            //float4 SHintensity = evalSH_direct(-Normal);
                            //float3 LpvIntensity = float3(
                            //    dot(SHintensity, SAMPLE_TEXTURE3D_LOD(gridRTex, sampler_gridRTex, uvw, 0)),
                            //    dot(SHintensity, SAMPLE_TEXTURE3D_LOD(gridGTex, sampler_gridGTex, uvw, 0)),
                            //    dot(SHintensity, SAMPLE_TEXTURE3D_LOD(gridBTex, sampler_gridBTex, uvw, 0))
                            //    );
                            //indirect.rgb += max(0, LpvIntensity) * density/ _loopCount* lum * phase;
                        }
                        
                    }
                    
                }
                // sample the texture
                col.a+= totalLum;
                col.rgb += totalLum;
                return col;
            }
                ENDHLSL
            }

    }
}
