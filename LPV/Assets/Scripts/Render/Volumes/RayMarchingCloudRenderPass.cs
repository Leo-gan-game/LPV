using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RayMarchingCloudRenderPass : ScriptableRenderPass
{
    private int rayMarchingKernel;
    private ComputeShader volumeCloudComputeShader;
    private RenderTexture rayMarchingCloudTexture;
    private RenderTargetIdentifier depthTarget;
    private const string profilerTag = "Ray Marching Cloud Pass";
    private int sizeAndInvSizeID = Shader.PropertyToID("SizeAndInvSize");
    private int tileIndexAndSceneDistanceTextureID = Shader.PropertyToID("TileIndexAndSceneDistanceTexture");
    private int minMaxDepthTextureID = Shader.PropertyToID("MinMaxDepthTexture");
    private int invProjMatrixID = Shader.PropertyToID("invProjMatrix");
    private int invViewMatrixID = Shader.PropertyToID("invViewMatrix");
    private int resultID = Shader.PropertyToID("Result");
    private Vector4 sizeAndInvSizeVector;
    private int width;
    private int height;
    private int frameIndex;
    private Matrix4x4 previewView;
    private Matrix4x4 previewProj;

    private const float PlanetRadius = 6371000.0f;
    private const float AtmosphereRadius = PlanetRadius+80000.0f;
    private readonly Vector4 DensityScale = new Vector4(7994.0f, 1200.0f, 0, 0);
    private readonly Vector4 RayleighSct = new Vector4(5.8f, 13.5f, 33.1f, 0.0f) * 0.000001f;
    private readonly Vector4 MieSct = new Vector4(2.0f, 2.0f, 2.0f, 0.0f) * 0.00001f;
    public float MieG = 0.76f;
    public float DistanceScale = 1;

    public int JitterIndex { get => frameIndex; }
    public RenderTexture RayMarchingCloudTexture { get => rayMarchingCloudTexture; }
    public Matrix4x4 PreviewView { get => previewView; set => previewView = value; }
    public Matrix4x4 PreviewProj { get => previewProj; set => previewProj = value; }

    public RayMarchingCloudRenderPass(ComputeShader cs)
    {
        profilingSampler = new ProfilingSampler(profilerTag);
        volumeCloudComputeShader = cs;
        rayMarchingKernel = volumeCloudComputeShader.FindKernel("RaymarchingCloud");

        frameIndex = 0;
    }




    private void InitTexture(CommandBuffer cmd,RenderTextureDescriptor descriptor)
    {
        sizeAndInvSizeVector = new Vector4(descriptor.width, descriptor.height, 1.0f / descriptor.width, 1.0f / descriptor.height);
        if (rayMarchingCloudTexture == null || width !=descriptor.width || height != descriptor.height)
        {
            
            var desc = descriptor;
            width = descriptor.width;
            height = descriptor.height;
            desc.enableRandomWrite = true;
            desc.dimension = TextureDimension.Tex2D;
            desc.colorFormat = RenderTextureFormat.ARGBHalf;
            desc.width = desc.width / 4;
            desc.height = desc.height / 4;
            desc.bindMS = false;
            desc.msaaSamples = 1;
            desc.sRGB = false;
            desc.depthBufferBits = 0;
            rayMarchingCloudTexture = new RenderTexture(desc);
            rayMarchingCloudTexture.Create();
        }
    }

    public void Clear()
    {
        if (rayMarchingCloudTexture != null)
        {
            rayMarchingCloudTexture.Release();
            rayMarchingCloudTexture = null;

        }
    }

    // This method is called before executing the render pass.
    // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
    // When empty this render pass will render to the active camera render target.
    // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
    // The render pipeline will ensure target setup and clearing happens in a performant manner.
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        InitTexture(cmd,renderingData.cameraData.cameraTargetDescriptor);
    } 

    // Here you can implement the rendering logic.
    // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
    // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
    // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get(profilerTag);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        //Ray marching compute handle 1/4 size of screen render target size.
        //the compute shader thread group is [8,8,1]
        int threadGroupX = (int)(sizeAndInvSizeVector.x + 31) / 32;
        int threadGroupY = (int)(sizeAndInvSizeVector.y + 31) / 32;
        using (new ProfilingScope(cmd, profilingSampler))
        {
            cmd.SetComputeVectorParam(volumeCloudComputeShader, sizeAndInvSizeID, sizeAndInvSizeVector);
            //cmd.SetComputeMatrixParam(volumeCloudComputeShader, invViewMatrixID, renderingData.cameraData.camera.worldToCameraMatrix.inverse);
            //cmd.SetComputeMatrixParam(volumeCloudComputeShader, invProjMatrixID, renderingData.cameraData.camera.projectionMatrix.inverse);
            cmd.SetComputeMatrixParam(volumeCloudComputeShader, invProjMatrixID, renderingData.cameraData.camera.projectionMatrix.inverse);
            cmd.SetComputeFloatParam(volumeCloudComputeShader, "AtmosphereRadius", AtmosphereRadius);
            cmd.SetComputeFloatParam(volumeCloudComputeShader, "PlanetRadius", PlanetRadius);


            cmd.SetComputeFloatParam(volumeCloudComputeShader, "_MieG", MieG);
            cmd.SetComputeFloatParam(volumeCloudComputeShader, "_DistanceScale", DistanceScale);

            cmd.SetComputeVectorParam(volumeCloudComputeShader, "_DensityScaleHeight", DensityScale);
            Vector4 scatteringR = new Vector4(5.8f, 13.5f, 33.1f, 0.0f) * 0.000001f;
            Vector4 scatteringM = new Vector4(2.0f, 2.0f, 2.0f, 0.0f) * 0.00001f;

            cmd.SetComputeVectorParam(volumeCloudComputeShader, "_ScatteringR", RayleighSct);
            cmd.SetComputeVectorParam(volumeCloudComputeShader, "_ScatteringM", MieSct );
            cmd.SetComputeVectorParam(volumeCloudComputeShader, "_ExtinctionR", RayleighSct );
            cmd.SetComputeVectorParam(volumeCloudComputeShader, "_ExtinctionM", MieSct );
            cmd.SetComputeTextureParam(volumeCloudComputeShader, rayMarchingKernel, minMaxDepthTextureID, VolumeRenderFeature.Instance.MinMaxDepth);
            cmd.SetComputeTextureParam(volumeCloudComputeShader, rayMarchingKernel, resultID, rayMarchingCloudTexture);
            cmd.SetComputeTextureParam(volumeCloudComputeShader, rayMarchingKernel, tileIndexAndSceneDistanceTextureID, VolumeRenderFeature.Instance.TileIndexAndSceneDistanceTexture);
            cmd.DispatchCompute(volumeCloudComputeShader, rayMarchingKernel, threadGroupX, threadGroupY, 1);
        }
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        //cmd.SetGlobalTexture("_MainTex", VolumeRenderFeature.Instance.ReconstructionTexture);

        //cmd.SetRenderTarget(cameraColorTexture, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.DontCare);
        //cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, volumeSetting.blitMat, 0, 0);

        ++frameIndex;
        frameIndex = frameIndex % 4;
        CommandBufferPool.Release(cmd);
    }


    
    // Cleanup any allocated resources that were created during the execution of this render pass.
    public override void OnCameraCleanup(CommandBuffer cmd)
    {
       
       
    }
}