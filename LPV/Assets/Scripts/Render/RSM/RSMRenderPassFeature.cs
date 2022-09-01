using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Collections;


public class RSMRenderPassFeature : ScriptableRendererFeature
{
    public static Vector3 centorPos;
    public static Vector3 AABBsize;
    public static int RSMResolution;

    class RSMRenderPass : ScriptableRenderPass
    {

        const string profilerTag = "Reflective Shadow Caster Pass";
        private static readonly int TextureOfPosition = Shader.PropertyToID("RSM_Postion");
        private static readonly int TextureOfNormal = Shader.PropertyToID("RSM_Normal");
        private static readonly int TextureOfFlux = Shader.PropertyToID("RSM_Flux");
        private static readonly int RandomBufferID = Shader.PropertyToID("_RandomBuffer");
        private static readonly int MaxSampleRadiusID = Shader.PropertyToID("MaxSampleRadius");
        private static readonly int MweightID = Shader.PropertyToID("mweight");
        private static readonly int centorPosID = Shader.PropertyToID("centorPos");
        private static readonly int sizeID = Shader.PropertyToID("AABBsize");
        private static readonly int diractionLightID = Shader.PropertyToID("_LightDirection");
        private static readonly int RsmSampleCountProperty = Shader.PropertyToID("_RsmSampleCount");
        private static readonly int RSM_VP = Shader.PropertyToID("RSM_VP");
        private static readonly int RSM_V = Shader.PropertyToID("RSM_V");
        private static readonly int TextureSizeID = Shader.PropertyToID("_TextureSize");
        
        private ShaderTagId shaderTagId ;
        private FilteringSettings filteringSettings;
        private RenderStateBlock renderStateBlock;

        private RenderTargetHandle positionRT;
        private RenderTargetHandle normalRT;
        private RenderTargetHandle fluxRT;
        private RenderTargetHandle depthRT;

        private RenderTargetIdentifier[] identifiers;

        private int m_RSMResolution;
        public int m_shadowSize=2;
        public int m_shadowIndex=0;
        public int m_RsmSampleCount;
        public float m_ShadowDistance;
        public float m_ShadowMapRatio;
        public float m_MaxSampleRadius;
        public float m_intensity;
        public UnityEngine.ComputeBuffer randomBuffer;
        public Vector3 m_ShadowOffset;
        

        public RSMRenderPass(int rsmSampleCount)
        {
            m_RsmSampleCount = rsmSampleCount;
            profilingSampler = new ProfilingSampler(profilerTag);
            shaderTagId = new ShaderTagId("ReflictiveShadowCaster");
            GenerateRandomValue();
        }
        public void Clear()
        {
            randomBuffer.Release();
        }
        private void GenerateRandomValue()
        {
            var R = new Unity.Mathematics.Random();
            R.InitState();
            NativeArray<Vector4> randomData = new NativeArray<Vector4>(m_RsmSampleCount, Allocator.Temp);
            for (int i=0;i< m_RsmSampleCount; i++)
            {
                var xi = R.NextFloat2Direction();
                randomData[i] = new Vector4(xi.x * Unity.Mathematics.math.sin(2 * Unity.Mathematics.math.PI * xi.y), xi.x * Unity.Mathematics.math.cos(2 * Unity.Mathematics.math.PI * xi.y), xi.x * xi.x, 0);
            }
            randomBuffer = new UnityEngine.ComputeBuffer(m_RsmSampleCount, sizeof(float) * 4, UnityEngine.ComputeBufferType.Structured);
            randomBuffer.SetData(randomData);
        }
        private Matrix4x4 GetModelMatrix(Vector3 position, Quaternion rotate)
        {
            float x = rotate.x;
            float y = rotate.y;
            float z = rotate.z;
            float w = rotate.w;
            var q00 = 1 - 2 * y * y - 2 * z * z;
            var q01 = 2 * x * y - 2 * z * w;
            var q02 = 2 * x * z + 2 * y * w;
            var q10 = 2 * x * y + 2 * z * w;
            var q11 = 1 - 2 * x * x - 2 * z * z;
            var q12 = 2 * y * z - 2 * x * w;
            var q20 = 2 * x * z - 2 * y * w;
            var q21 = 2 * y * z + 2 * x * w;
            var q22 = 1 - 2 * x * x - 2 * y * y;
            var modelMatrix =
                new Matrix4x4(
                new Vector4(q00, q10, q20, 0),
                new Vector4(q01, q11, q21, 0),
                new Vector4(q02, q12, q22, 0),
                new Vector4(position.x, position.y, position.z, 1)
                );
            return modelMatrix;
        }
        public void SetData(RenderTargetHandle posTarget, RenderTargetHandle norTarget, RenderTargetHandle fluxTarget, int rSMResolution)
        {
            positionRT = posTarget;
            normalRT = norTarget;
            fluxRT = fluxTarget;
            m_RSMResolution = rSMResolution;
            identifiers = new RenderTargetIdentifier[] { positionRT.Identifier(), normalRT.Identifier(), fluxRT.Identifier() };
        }
        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in a performant manner.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(profilerTag);
            Camera camera;
            Light light = renderingData.lightData.visibleLights[renderingData.lightData.mainLightIndex].light;
            Matrix4x4 viewMatrix, project, rsmVP;
            CalculateMatrix2(renderingData, out camera, light, out viewMatrix, out project, out rsmVP);
            CalculateMatrix(renderingData, out camera, out light, out viewMatrix, out project, out rsmVP);
            

            using (new ProfilingScope(cmd, profilingSampler))
            {

                cmd.SetViewport(new Rect(0, 0, m_RSMResolution, m_RSMResolution));
                cmd.SetViewProjectionMatrices(viewMatrix, project);
                cmd.SetGlobalVector(diractionLightID, light.transform.forward);
                context.ExecuteCommandBuffer(cmd);//set cmd need execute the command.
                cmd.Clear();
                var cullResults = renderingData.cullResults;
                LightData lightData = renderingData.lightData;
                ShadowData shadowData = renderingData.shadowData;


                SortingSettings sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.RenderQueue };

                FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.all);
                DrawingSettings drawingSettings = new DrawingSettings() { sortingSettings = sortingSettings };
                RenderStateBlock stateBlock = new RenderStateBlock(RenderStateMask.Nothing);
                drawingSettings.SetShaderPassName(0, shaderTagId);
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings, ref stateBlock);

            }
            cmd.SetGlobalVector(centorPosID, RSMRenderPassFeature.centorPos);
            cmd.SetGlobalVector(sizeID, RSMRenderPassFeature.AABBsize);
            cmd.SetGlobalInt(RsmSampleCountProperty, m_RsmSampleCount);
            cmd.SetGlobalInt(TextureSizeID, m_RSMResolution);
            cmd.SetGlobalFloat(MaxSampleRadiusID, m_MaxSampleRadius);
            cmd.SetGlobalFloat(MweightID, m_intensity);
            cmd.SetGlobalTexture(TextureOfPosition, positionRT.id);
            cmd.SetGlobalTexture(TextureOfNormal, normalRT.id);
            cmd.SetGlobalTexture(TextureOfFlux, fluxRT.id);
            cmd.SetGlobalBuffer(RandomBufferID, randomBuffer);
            cmd.SetGlobalMatrix(RSM_VP, rsmVP);
            cmd.SetGlobalMatrix(RSM_V, viewMatrix);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void CalculateMatrix2(RenderingData renderingData, out Camera camera, Light light, out Matrix4x4 viewMatrix, out Matrix4x4 project, out Matrix4x4 rsmVP)
        {
            camera = renderingData.cameraData.camera;
            //计算相机的包围盒
            Vector3[] nearPt = new Vector3[4];
            Vector3[] farPt = new Vector3[4];
            //获取近裁剪面、远裁剪面的顶点坐标
            //该函数获取到的是相机局部坐标系的顶点坐标
            camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.nearClipPlane, Camera.MonoOrStereoscopicEye.Mono, nearPt);
            camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, farPt);

            float nearSplit = 0;
            float farSplit = m_ShadowMapRatio;
            GetFrustumPortion(ref nearPt, ref farPt, nearSplit, farSplit);

            //计算相机的包围盒 world space
            Vector3[] nearPtWS = new Vector3[4];
            Vector3[] farPtWS = new Vector3[4];

            //计算从相机局部坐标系转到灯光坐标系的矩阵
            Matrix4x4 cameraMatrix = camera.transform.localToWorldMatrix;
            for (int i = 0; i < 4; ++i)
            {
                //将顶点坐标转移到灯光坐标系
                nearPtWS[i] = cameraMatrix.MultiplyPoint(nearPt[i]);
                farPtWS[i] = cameraMatrix.MultiplyPoint(farPt[i]);
            }
            // read Main Light index;
            int shadowLightIndex = renderingData.lightData.mainLightIndex;

            var lightMatrix = light.transform.worldToLocalMatrix;
            Vector4 axisX = light.transform.localToWorldMatrix.GetColumn(0);
            Vector4 axisY = light.transform.localToWorldMatrix.GetColumn(1);
            Vector4 axisZ = light.transform.localToWorldMatrix.GetColumn(2);
            Bounds casterBounds;
            renderingData.cullResults.GetShadowCasterBounds(shadowLightIndex, out casterBounds);
            Vector3 center = casterBounds.center;
            float castersRadius = Vector3.Magnitude(casterBounds.max - casterBounds.min) * 0.5f;
            Vector3 initialLightPos = center - (Vector3)axisZ * castersRadius;
            //initialLightPos += m_ShadowOffset;
            viewMatrix = new Matrix4x4(axisX, axisY, axisZ, new Vector4(initialLightPos.x, initialLightPos.y, initialLightPos.z, 1));

            //计算相机的包围盒 camera space
            Vector3[] nearPtCS = new Vector3[4];
            Vector3[] farPtCS = new Vector3[4];

            for (int i = 0; i < 4; ++i)
            {
                //将顶点坐标转移到灯光坐标系
                nearPtCS[i] = lightMatrix.MultiplyPoint(nearPtWS[i]);
                farPtCS[i] = lightMatrix.MultiplyPoint(farPtWS[i]);
            }

            //在灯光坐标系下计算最大外包AABB盒
            float[] xs = { nearPtCS[0].x, nearPtCS[1].x, nearPtCS[2].x, nearPtCS[3].x, farPtCS[0].x, farPtCS[1].x, farPtCS[2].x, farPtCS[3].x };
            float[] ys = { nearPtCS[0].y, nearPtCS[1].y, nearPtCS[2].y, nearPtCS[3].y, farPtCS[0].y, farPtCS[1].y, farPtCS[2].y, farPtCS[3].y };
            float[] zs = { nearPtCS[0].z, nearPtCS[1].z, nearPtCS[2].z, nearPtCS[3].z, farPtCS[0].z, farPtCS[1].z, farPtCS[2].z, farPtCS[3].z };

            float minX = Mathf.Min(xs);
            float maxX = Mathf.Max(xs);
            float minY = Mathf.Min(ys);
            float maxY = Mathf.Max(ys);
            float minZ = Mathf.Min(zs);
            float maxZ = Mathf.Max(zs);


            Gizmos.color = Color.red;
            Vector3 previousNearCorners0 = new Vector3(minX, minY, minZ);
            Vector3 previousNearCorners1 = new Vector3(maxX, minY, minZ);
            Vector3 previousNearCorners2 = new Vector3(maxX, maxY, minZ);
            Vector3 previousNearCorners3 = new Vector3(minX, maxY, minZ);

            Vector3 pos = previousNearCorners0 + (previousNearCorners2 - previousNearCorners0) * 0.5f;
            
            float w = (maxX - minX) / 2;
            float h = (maxY - minY) / 2;
            pos = light.transform.TransformPoint(pos);



            // Quantize the position to shadow map texel size; gets rid of some "shadow swimming"
            double texelSizeX = (maxX - minX) / RSMResolution;
            double texelSizeY = (maxY - minY) / RSMResolution;
            double projX = axisX.x * (double)pos.x + axisX.y * (double)pos.y + axisX.z * (double)pos.z;
            double projY = axisY.x * (double)pos.x + axisY.y * (double)pos.y + axisY.z * (double)pos.z;
            float modX = (float)(projX % texelSizeX);
            float modY = (float)(projY % texelSizeY);
            pos -= (Vector3)axisX * modX;
            pos -= (Vector3)axisY * modY;

            viewMatrix.SetColumn(3, pos);
            viewMatrix.m33 = 1;
            viewMatrix = viewMatrix.inverse;
            project = Matrix4x4.Ortho(-w, w, -h, h, 0, 1000);
            rsmVP = project * viewMatrix;
        }

        private void CalculateMatrix(RenderingData renderingData, out Camera camera, out Light light, out Matrix4x4 viewMatrix, out Matrix4x4 project, out Matrix4x4 rsmVP)
        {
            camera = renderingData.cameraData.camera;
            //计算相机的包围盒
            Vector3[] nearPt = new Vector3[4];
            Vector3[] farPt = new Vector3[4];
            //获取近裁剪面、远裁剪面的顶点坐标
            //该函数获取到的是相机局部坐标系的顶点坐标
            camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.nearClipPlane, Camera.MonoOrStereoscopicEye.Mono, nearPt);
            camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, farPt);

            // read Main Light index;
            int shadowLightIndex = renderingData.lightData.mainLightIndex;

            //read light through light index;
            VisibleLight shadowLight = renderingData.lightData.visibleLights[shadowLightIndex];

            Bounds casterBounds;
            renderingData.cullResults.GetShadowCasterBounds(shadowLightIndex, out casterBounds);
            Vector3 center = casterBounds.center;
            float castersRadius = Vector3.Magnitude(casterBounds.max - casterBounds.min) * 0.5f;

            Vector3[] frustumSplit = new Vector3[8];
            float nearSplit = 0;
            float farSplit = m_ShadowMapRatio;
            GetFrustumPortion(nearPt, farPt, nearSplit, farSplit, ref frustumSplit);

            light = shadowLight.light;
            Vector4 axisX = light.transform.localToWorldMatrix.GetColumn(0);
            Vector4 axisY = light.transform.localToWorldMatrix.GetColumn(1);
            Vector4 axisZ = light.transform.localToWorldMatrix.GetColumn(2);
            Vector3 axisZ3V = light.transform.localToWorldMatrix.GetColumn(2);

            Vector3 initialLightPos = center - axisZ3V * castersRadius;
            //initialLightPos += m_ShadowOffset;
            viewMatrix = new Matrix4x4(axisX, axisY, axisZ, new Vector4(initialLightPos.x, initialLightPos.y, initialLightPos.z, 1));

            // Calculate the visible frustum bounds in initial light space
            
            Vector3 sphereCenter = Vector3.zero;
            float radius = 0;
            // Sphere is in camera space, so view vector is along negative Z
            CalculateBoundingSphereFromFrustumPoints(frustumSplit, ref sphereCenter, ref radius);
            // Now we transform our sphere center into world coordinates
            sphereCenter = camera.transform.localToWorldMatrix.MultiplyPoint(sphereCenter);


            var v = sphereCenter - (Vector3)viewMatrix.GetColumn(3);

            Vector3 p = viewMatrix.transpose.MultiplyVector(v);

            //p = viewMatrix.MultiplyVector(p);
            Bounds frustumBoundsLocal = new Bounds(Vector3.zero, Vector3.negativeInfinity);
            frustumBoundsLocal.Encapsulate(p);
            Vector3 m_min = p;
            Vector3 m_max = p;
            Vector3 offset = new Vector3(radius, radius, radius);
            m_min -= offset;
            m_max += offset;

            frustumBoundsLocal.Expand(radius * 2);
            Vector3 frustumBoundsSizeLocal;
            frustumBoundsSizeLocal = frustumBoundsLocal.max - frustumBoundsLocal.min;
            Vector3 halfFrustumBoundsSizeLocal = frustumBoundsSizeLocal * 0.5f;

            // Quantize the position to shadow map texel size; gets rid of some "shadow swimming"
            Vector3 frustumBoundsCenterLocal = frustumBoundsLocal.center;
            double texelSizeX = frustumBoundsSizeLocal.x / RSMResolution;
            double texelSizeY = frustumBoundsSizeLocal.y / RSMResolution;

            Vector3 stableLightPosWorld = viewMatrix.MultiplyPoint(frustumBoundsCenterLocal);
            const float kShadowProjectionPlaneOffsetFactor = 0.1f;
            double projX = axisX.x * (double)stableLightPosWorld.x + axisX.y * (double)stableLightPosWorld.y + axisX.z * (double)stableLightPosWorld.z;
            double projY = axisY.x * (double)stableLightPosWorld.x + axisY.y * (double)stableLightPosWorld.y + axisY.z * (double)stableLightPosWorld.z;
            float modX = (float)(projX % texelSizeX);
            float modY = (float)(projY % texelSizeY);
            stableLightPosWorld -= (Vector3)axisX * modX;
            stableLightPosWorld -= (Vector3)axisY * modY;

            stableLightPosWorld -= axisZ3V * halfFrustumBoundsSizeLocal.z * (1.0f + 2.0f * kShadowProjectionPlaneOffsetFactor);
            float nearPlane = halfFrustumBoundsSizeLocal.z * kShadowProjectionPlaneOffsetFactor;
            float farPlane = halfFrustumBoundsSizeLocal.z * (2.0f + 3.0f * kShadowProjectionPlaneOffsetFactor);
            stableLightPosWorld += m_ShadowOffset;
            viewMatrix.SetColumn(3, stableLightPosWorld);
            viewMatrix.m33 = 1;
            project = Matrix4x4.Ortho(-halfFrustumBoundsSizeLocal.x, halfFrustumBoundsSizeLocal.x, -halfFrustumBoundsSizeLocal.y, halfFrustumBoundsSizeLocal.y, 0, 1000);
            //project = GL.GetGPUProjectionMatrix(project, false);
            var t = Matrix4x4.identity;
            t.m22 = -1;
            viewMatrix = t * viewMatrix;
            viewMatrix = viewMatrix.inverse;
            rsmVP = project * viewMatrix;

            ////计算从相机局部坐标系转到灯光坐标系的矩阵
            //Matrix4x4 cameraToLight = camera.transform.localToWorldMatrix;

            //for (int i = 0; i < 4; ++i)
            //{
            //    //将顶点坐标转移到灯光坐标系
            //    nearPt[i] = cameraToLight.MultiplyPoint(nearPt[i]);
            //    farPt[i] = cameraToLight.MultiplyPoint(farPt[i]);
            //}

            ////在灯光坐标系下计算最大外包AABB盒
            //float[] xs = { nearPt[0].x, nearPt[1].x, nearPt[2].x, nearPt[3].x, farPt[0].x, farPt[1].x, farPt[2].x, farPt[3].x };
            //float[] ys = { nearPt[0].y, nearPt[1].y, nearPt[2].y, nearPt[3].y, farPt[0].y, farPt[1].y, farPt[2].y, farPt[3].y };
            //float[] zs = { nearPt[0].z, nearPt[1].z, nearPt[2].z, nearPt[3].z, farPt[0].z, farPt[1].z, farPt[2].z, farPt[3].z };

            //Vector3 minPt = new Vector3(Mathf.Min(xs), Mathf.Min(ys), Mathf.Min(zs));
            //Vector3 maxPt = new Vector3(Mathf.Max(xs), Mathf.Max(ys), Mathf.Max(zs));



            //var rotation = light.transform.rotation;






            //InverseMultiplyPoint3Affine
            //Vector3 pos = viewMatrix.GetColumn(3);
            //Vector3 v =frustumBoundsCenterLocal - pos;
            //Vector3 p = viewMatrix.MultiplyVector(v);
            //Bounds bounds;
            //bounds.Encapsulate(p);
            //bounds.Expand()
            //float kShadowProjectionPlaneOffsetFactor = 0.1f;
            //Vector3 halfFrustumBoundsSizeLocal = frustumBoundsSizeLocal * m_ShadowMapRatio;
            //Vector3 stableLightPosWorld = viewMatrix.MultiplyPoint3x4(frustumBoundsCenterLocal);
            //stableLightPosWorld -= axisZ3V * halfFrustumBoundsSizeLocal.z * (1.0f + 2.0f * kShadowProjectionPlaneOffsetFactor);
            //float nearPlane = 0;
            //float farPlane = halfFrustumBoundsSizeLocal.z * (1/ m_ShadowMapRatio );
            //stableLightPosWorld += m_ShadowOffset;
            //viewMatrix.SetColumn(3,stableLightPosWorld);
            //viewMatrix.m33 = 1;
            //var project = Matrix4x4.Ortho(-halfFrustumBoundsSizeLocal.x , halfFrustumBoundsSizeLocal.x , -halfFrustumBoundsSizeLocal.y, halfFrustumBoundsSizeLocal.y, nearPlane, farPlane);

            //viewMatrix.SetColumn(2, -axisZ);
            //viewMatrix = viewMatrix.inverse;
            //float nearClipPlane = 0;// minPt.z;
            //float farClipPlane = m_ShadowDistance;
            //float aspect = (maxPt.x - minPt.x) / (maxPt.y - minPt.y);
            //float orthographicSize = (maxPt.y - minPt.y) * m_ShadowMapRatio;
            //float aabbLength = (maxPt.y - minPt.y);
            //RSMRenderPassFeature.AABBsize = new Vector3(aabbLength * aspect, aabbLength, maxPt.z - minPt.z);
            //viewMatrix = Matrix4x4.TRS(RSMRenderPassFeature.centorPos, rotation, new Vector3(1, 1, -1)).inverse;


            //var project = Matrix4x4.Ortho(-orthographicSize * aspect, orthographicSize * aspect, -orthographicSize, orthographicSize, nearClipPlane, farClipPlane);

            // project = GL.GetGPUProjectionMatrix(project, false);
            //renderingData.cameraData.maxShadowDistance = Mathf.Min(m_ShadowDistance, camera.farClipPlane);
            //renderingData.cullResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(renderingData.lightData.mainLightIndex, m_shadowIndex, m_shadowSize,
            //    m_spliteRatio, m_RSMResolution, light.shadowNearPlane, out viewMatrix,
            //    out project, out ShadowSplitData shadowSplitData);

            //t.m11 = -1;
            //rsmVP = t * rsmVP;
        }
        private void GetFrustumPortion(ref Vector3[] nearPt, ref Vector3[] farPt, float nearSplit, float farSplit)
        {
            nearPt[0] = Vector3.Lerp(nearPt[0], farPt[0], nearSplit);
            nearPt[1] = Vector3.Lerp(nearPt[1], farPt[1], nearSplit);
            nearPt[2] = Vector3.Lerp(nearPt[2], farPt[2], nearSplit);
            nearPt[3] = Vector3.Lerp(nearPt[3], farPt[3], nearSplit);

            farPt[0] = Vector3.Lerp(nearPt[0], farPt[0], farSplit);
            farPt[1] = Vector3.Lerp(nearPt[1], farPt[1], farSplit);
            farPt[2] = Vector3.Lerp(nearPt[2], farPt[2], farSplit);
            farPt[3] = Vector3.Lerp(nearPt[3], farPt[3], farSplit);
        }
        private void GetFrustumPortion(Vector3[] nearPt, Vector3[] farPt, float nearSplit, float farSplit,  ref Vector3[] frustumSplit)
        {
            frustumSplit[0] = Vector3.Lerp(nearPt[0], farPt[0], nearSplit);
            frustumSplit[1] = Vector3.Lerp(nearPt[1], farPt[1], nearSplit);
            frustumSplit[2] = Vector3.Lerp(nearPt[2], farPt[2], nearSplit);
            frustumSplit[3] = Vector3.Lerp(nearPt[3], farPt[3], nearSplit);

            frustumSplit[4] = Vector3.Lerp(nearPt[0], farPt[0], farSplit);
            frustumSplit[5] = Vector3.Lerp(nearPt[1], farPt[1], farSplit);
            frustumSplit[6] = Vector3.Lerp(nearPt[2], farPt[2], farSplit);
            frustumSplit[7] = Vector3.Lerp(nearPt[3], farPt[3], farSplit);
        }

        //https://lxjk.github.io/2017/04/15/Calculate-Minimal-Bounding-Sphere-of-Frustum.html
        private void CalculateBoundingSphereFromFrustumPoints(Vector3[] points, ref Vector3 outCenter, ref float outRadius)
        {
            Vector3[] spherePoints = new Vector3[4];
            spherePoints[0] = points[0];//n1
            spherePoints[1] = points[3];//n2
            spherePoints[2] = points[5];//f1
            spherePoints[3] = points[7];//f2

            // Is bounding sphere at the far or near plane?
            for (int plane = 1; plane >= 0; --plane)
            {
                Vector3 pointA = spherePoints[plane * 2];
                Vector3 pointB = spherePoints[plane * 2 + 1];
                Vector3 center = (pointA + pointB) * 0.5f;//f0
                float radius2 = Vector3.SqrMagnitude(pointA - center);
                Vector3 pointC = spherePoints[(1 - plane) * 2];
                Vector3 pointD = spherePoints[(1 - plane) * 2 + 1];

                // Check if all points are inside sphere
                if (Vector3.SqrMagnitude(pointC - center) <= radius2 &&
                    Vector3.SqrMagnitude(pointD - center) <= radius2)
                {
                    outCenter = center;
                    outRadius = Mathf.Sqrt(radius2);
                    return;
                }
            }
            // Sphere touches all four frustum points
            CalculateSphereFrom4Points(spherePoints, ref outCenter, ref outRadius);
        }
        private void CalculateSphereFrom4Points(Vector3[] points, ref Vector3 outCenter, ref float outRadius){
            Matrix4x4 mat = new Matrix4x4();

            for (int i = 0; i< 4; ++i)
            {
                Vector4 p = new Vector4(points[i].x, points[i].y, points[i].z, 1);
                mat.SetRow(i, p);
            }
            float m11 = mat.determinant;

            for (int i = 0; i < 4; ++i)
            {
                Vector4 p = new Vector4(points[i].x * points[i].x + points[i].y * points[i].y + points[i].z * points[i].z, points[i].y, points[i].z, 1);
                mat.SetRow(i, p);
            }
            float m12 = mat.determinant;

            for (int i = 0; i < 4; ++i)
            {
                Vector4 p = new Vector4(points[i].x, points[i].x * points[i].x + points[i].y * points[i].y + points[i].z * points[i].z, points[i].z, 1);
                mat.SetRow(i, p);
            }
            float m13 = mat.determinant;

            for (int i = 0; i < 4; ++i)
            {
                Vector4 p = new Vector4(points[i].x, points[i].y, points[i].x * points[i].x + points[i].y * points[i].y + points[i].z * points[i].z, 1);
                mat.SetRow(i, p);
            }
            float m14 = mat.determinant;

            for (int i = 0; i < 4; ++i)
            {
                Vector4 p = new Vector4(points[i].x * points[i].x + points[i].y * points[i].y + points[i].z * points[i].z, points[i].y, points[i].z, 1);

            }
            float m15 = mat.determinant;

            Vector3 c;
            c.x = 0.5f * m12 / m11;
            c.y = 0.5f * m13 / m11;
            c.z = 0.5f * m14 / m11;
            outRadius = Mathf.Sqrt(c.x * c.x + c.y * c.y + c.z * c.z - m15 / m11);
            outCenter = c;
        }

// Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(positionRT.id);
            cmd.ReleaseTemporaryRT(normalRT.id);
            cmd.ReleaseTemporaryRT(fluxRT.id);
            cmd.ReleaseTemporaryRT(depthRT.id);
        }
        
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            base.Configure(cmd, cameraTextureDescriptor);
            var positionDes = cameraTextureDescriptor;
            positionDes.width = m_RSMResolution;
            positionDes.height = m_RSMResolution;
            positionDes.msaaSamples = 1;
            var normalDes = positionDes;
            
            var fluxDes = positionDes;
            positionDes.colorFormat = RenderTextureFormat.ARGBFloat;
            var depthDes = new RenderTextureDescriptor(m_RSMResolution, m_RSMResolution, RenderTextureFormat.Depth, 24, 1);
            
            cmd.GetTemporaryRT(positionRT.id, positionDes,FilterMode.Point);
            cmd.GetTemporaryRT(normalRT.id, normalDes, FilterMode.Bilinear);
            cmd.GetTemporaryRT(fluxRT.id, fluxDes,FilterMode.Bilinear);
            cmd.GetTemporaryRT(depthRT.id, depthDes, FilterMode.Point);
            ConfigureTarget(identifiers, depthRT.Identifier());
            ConfigureClear(ClearFlag.All, Color.clear);
        }
    }

    RSMRenderPass m_ScriptablePass;
    RSMDebugPass m_DebugPass;

    private RenderTargetHandle positionRT;
    private RenderTargetHandle normalRT;
    private RenderTargetHandle fluxRT;
    
    public int m_RSMResolution = 1024;
    [Range(4,4096)]
    public int rsmSampleCount;
    public float shadowDistance;
    [Range(0,512)]
    public float maxSampleRadius;
    [Range(0.01f, 1)]
    public float intensity;
    [Range(0.01f,1)]
    public float shadowMapRatio=0.5f;
    public RenderPassEvent rsmPassEvent = RenderPassEvent.AfterRenderingPrePasses;

    public bool DebugRendering;

    public bool RandomBufferTooltip;

    public bool m_DebugRSM;
    public RSMDebugType debugType;
    public Vector3 shadowOffset;

    public enum RSMDebugType
    {
        RSM_Flux,
        RSM_Normal,
        RSM_Position
    }

    /// <inheritdoc/>
    public override void Create()
    {
        RSMRenderPassFeature.RSMResolution = m_RSMResolution;
        if (m_ScriptablePass != null)
        {
            m_ScriptablePass.Clear();
            m_ScriptablePass = null;
        }
        m_ScriptablePass = new RSMRenderPass(rsmSampleCount);
        m_DebugPass = new RSMDebugPass();
        positionRT = new RenderTargetHandle();
        normalRT = new RenderTargetHandle();
        fluxRT = new RenderTargetHandle();
        positionRT.Init("positionRT");
        normalRT.Init("normalRT");
        fluxRT.Init("fluxRT");
        // Configures where the render pass should be injected.
        if (isActive)
        {
            Shader.EnableKeyword("RSM_RENDER");
        }
        else
        {
            Shader.DisableKeyword("RSM_RENDER");
        }
        if (DebugRendering)
        {
            Shader.EnableKeyword("RSM_DEBUG");
        }
        else
        {
            Shader.DisableKeyword("RSM_DEBUG");
        }
        if (RandomBufferTooltip)
        {
            Shader.EnableKeyword("RANDOM_BUF");
        }
        else
        {
            Shader.DisableKeyword("RANDOM_BUF");
        }

    }

    protected override void Dispose(bool b)
    {
        if (m_ScriptablePass !=null)
        {
            m_ScriptablePass.Clear();
            m_ScriptablePass = null;
        }
    }
    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        m_ScriptablePass.renderPassEvent = rsmPassEvent;
        if (m_DebugRSM)
        {
            switch (debugType)
            {
                case RSMDebugType.RSM_Flux:
                    m_DebugPass.Setup(renderer.cameraColorTarget, fluxRT.Identifier());
                    break;
                case RSMDebugType.RSM_Normal:
                    m_DebugPass.Setup(renderer.cameraColorTarget, normalRT.Identifier());
                    break;
                case RSMDebugType.RSM_Position:
                    m_DebugPass.Setup(renderer.cameraColorTarget, positionRT.Identifier());
                    break;
                default:
                    m_DebugPass.Setup(renderer.cameraColorTarget, fluxRT.Identifier());
                    break;
            }
            m_DebugPass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            renderer.EnqueuePass(m_DebugPass);
        }
        
        
        m_ScriptablePass.m_intensity = intensity;
        m_ScriptablePass.m_ShadowDistance = shadowDistance;
        m_ScriptablePass.m_ShadowMapRatio = shadowMapRatio;
        m_ScriptablePass.m_MaxSampleRadius = maxSampleRadius;
        m_ScriptablePass.m_ShadowOffset = shadowOffset;
        m_ScriptablePass.SetData(positionRT, normalRT, fluxRT, m_RSMResolution);

        renderer.EnqueuePass(m_ScriptablePass);
        
        
    }
}


