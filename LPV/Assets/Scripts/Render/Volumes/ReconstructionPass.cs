
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ReconstructionPass : ScriptableRenderPass
{
    private int reconstructionKernel;
    private ComputeShader reconstructionComputeShader;
    private RenderTexture[] cloudRenderTextures;
    private RenderTargetIdentifier depthTarget;
    private const string profilerTag = "Reconstruction Pass";
    private int sizeAndInvSizeID = Shader.PropertyToID("SizeAndInvSize");
    private int raymarchingTextureID = Shader.PropertyToID("RaymarchingTexture");
    private int tileIndexAndSceneDistanceTextureID = Shader.PropertyToID("TileIndexAndSceneDistanceTexture");
    private int previousTextureID = Shader.PropertyToID("PreviousTexture");
    private int minMaxDepthTextureID = Shader.PropertyToID("MinMaxDepthTexture");
    private int prevViewProjID = Shader.PropertyToID("PrevViewProj");
    private int invProjMatrixID = Shader.PropertyToID("invProjMatrix");
    private int resultID = Shader.PropertyToID("Result");
    private Vector4 sizeAndInvSizeVector;
    private int width;
    private int height;
    private Matrix4x4 previewView;
    private Matrix4x4 previewProj;

    public RenderTexture CloudRenderTexture { get => cloudRenderTextures[VolumeRenderFeature.Instance.JitterIndex%2];}
    public Matrix4x4 PreviewView { get => previewView; set => previewView = value; }
    public Matrix4x4 PreviewProj { get => previewProj; set => previewProj = value; }

    public ReconstructionPass(ComputeShader cs)
    {
        profilingSampler = new ProfilingSampler(profilerTag);
        reconstructionComputeShader = cs;
        reconstructionKernel = reconstructionComputeShader.FindKernel("Reconstruction");
    }
    private void InitTexture(RenderTextureDescriptor descriptor)
    {
        RenderTextureDescriptor des = descriptor;
        if (cloudRenderTextures == null || width != descriptor.width || height != descriptor.height)
        {
            width = descriptor.width;
            height = descriptor.height;
            des.colorFormat = RenderTextureFormat.ARGBHalf;
            des.enableRandomWrite = true;
            des.sRGB = false;
            des.width = descriptor.width / 2;
            des.height = descriptor.height / 2;
            sizeAndInvSizeVector = new Vector4(descriptor.width, descriptor.height, 1.0f / descriptor.width, 1.0f / descriptor.height);
            cloudRenderTextures = new RenderTexture[2];
            cloudRenderTextures[0] = new RenderTexture(des);
            cloudRenderTextures[0].Create();
            cloudRenderTextures[1] = new RenderTexture(des);
            cloudRenderTextures[1].Create();
        }
    }

    private void ClearTexture()
    {
        if (cloudRenderTextures != null)
        {
            if (cloudRenderTextures[0])
            {
                cloudRenderTextures[0].Release();
                cloudRenderTextures[0] = null;
            }
            if (cloudRenderTextures[1])
            {
                cloudRenderTextures[1].Release();
                cloudRenderTextures[1] = null;
            }
        }
        
    }
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        InitTexture(renderingData.cameraData.cameraTargetDescriptor);
        base.OnCameraSetup(cmd, ref renderingData);
    }
    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        
        base.Configure(cmd, cameraTextureDescriptor);
    }
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get(profilerTag);
        int threadGroupX = (int)(sizeAndInvSizeVector.x + 7) / 8;
        int threadGroupY = (int)(sizeAndInvSizeVector.y + 7) / 8;
        using (new ProfilingScope(cmd, profilingSampler))
        {
            Matrix4x4 PrevViewProj = previewProj * previewView;
            cmd.SetComputeVectorParam(reconstructionComputeShader, sizeAndInvSizeID, sizeAndInvSizeVector);
           
            cmd.SetComputeMatrixParam(reconstructionComputeShader, prevViewProjID, PrevViewProj);
            cmd.SetComputeMatrixParam(reconstructionComputeShader, invProjMatrixID, renderingData.cameraData.camera.projectionMatrix.inverse);
            cmd.SetComputeTextureParam(reconstructionComputeShader, reconstructionKernel, raymarchingTextureID, VolumeRenderFeature.Instance.RaymarchingTextures);
            cmd.SetComputeTextureParam(reconstructionComputeShader, reconstructionKernel, minMaxDepthTextureID, VolumeRenderFeature.Instance.MinMaxDepth);
            cmd.SetComputeTextureParam(reconstructionComputeShader, reconstructionKernel, tileIndexAndSceneDistanceTextureID, VolumeRenderFeature.Instance.TileIndexAndSceneDistanceTexture);
            cmd.SetComputeTextureParam(reconstructionComputeShader, reconstructionKernel, previousTextureID, cloudRenderTextures[(VolumeRenderFeature.Instance.JitterIndex+1) % 2]);
            cmd.SetComputeTextureParam(reconstructionComputeShader, reconstructionKernel, resultID, cloudRenderTextures[(VolumeRenderFeature.Instance.JitterIndex) % 2]);
            cmd.DispatchCompute(reconstructionComputeShader, reconstructionKernel, threadGroupX, threadGroupY, 1);
        }
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
    }

    internal void Setup(RenderTargetIdentifier cameraDepthTarget)
    {
        depthTarget = cameraDepthTarget;
    }

    internal void Clear()
    {
        ClearTexture();
    }
}

