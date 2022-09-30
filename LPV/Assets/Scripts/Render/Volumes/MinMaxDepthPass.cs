
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class MinMaxDepthPass : ScriptableRenderPass
{
    private int minMaxKernel;
    private ComputeShader minMaxComputeShader;
    private RenderTexture minMaxHalfDepth;
    private RenderTargetIdentifier depthTarget;
    private const string profilerTag = "MinMaxDepth Pass";
    private int sizeAndInvSizeID = Shader.PropertyToID("SizeAndInvSize");
    private int sceneDepthTextureID = Shader.PropertyToID("SceneDepthTexture");
    private int resultID = Shader.PropertyToID("Result");
    private Vector4 sizeAndInvSizeVector;

    public RenderTexture MinMaxHalfDepth { get => minMaxHalfDepth; set => minMaxHalfDepth = value; }

    public MinMaxDepthPass(ComputeShader cs)
    {
        profilingSampler = new ProfilingSampler(profilerTag);
        minMaxComputeShader = cs;
        minMaxKernel = minMaxComputeShader.FindKernel("MinMaxDepth");
    }
    private void InitTexture(RenderTextureDescriptor descriptor)
    {
        RenderTextureDescriptor des = descriptor;
        if (MinMaxHalfDepth == null)
        {
            des.colorFormat = RenderTextureFormat.RFloat;
            des.enableRandomWrite = true;
            des.width = descriptor.width / 2;
            des.height = descriptor.height / 2;
            sizeAndInvSizeVector = new Vector4(descriptor.width, descriptor.height, 1.0f / descriptor.width, 1.0f / descriptor.height);
            MinMaxHalfDepth = new RenderTexture(des);
            MinMaxHalfDepth.Create();
        }
    }

    private void ClearTexture()
    {
        if (MinMaxHalfDepth)
        {
            MinMaxHalfDepth.Release();
            MinMaxHalfDepth = null;
        }
    }
    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        InitTexture(cameraTextureDescriptor);
        base.Configure(cmd, cameraTextureDescriptor);
    }
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get(profilerTag);
        int threadGroupX = (int)(sizeAndInvSizeVector.x + 7) / 8;
        int threadGroupY = (int)(sizeAndInvSizeVector.y + 7) / 8;
        using (new ProfilingScope(cmd, profilingSampler))
        {
            cmd.SetComputeVectorParam(minMaxComputeShader, sizeAndInvSizeID, sizeAndInvSizeVector);
            cmd.SetComputeTextureParam(minMaxComputeShader, minMaxKernel, sceneDepthTextureID, depthTarget);
            cmd.SetComputeTextureParam(minMaxComputeShader, minMaxKernel, resultID, MinMaxHalfDepth);
            cmd.DispatchCompute(minMaxComputeShader, minMaxKernel, threadGroupX, threadGroupY, 1);
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

