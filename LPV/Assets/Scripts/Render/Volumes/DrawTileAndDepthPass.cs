using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DrawTileAndDepthPass : ScriptableRenderPass
{
    private int drawTileIndexAndDistanceKernel;
    private ComputeShader drawTileIndexAndDistanceComputeShader;
    private RenderTexture tileIndexAndSceneDistanceTexture;
    private RenderTargetIdentifier depthTarget;
    private const string profilerTag = "DrawTileIndexAndSceneDis Pass";
    private int sizeAndInvSizeID = Shader.PropertyToID("SizeAndInvSize");
    private int jitterIndexID = Shader.PropertyToID("Jitter");
    private int minMaxDepthTextureID = Shader.PropertyToID("MinMaxDepthTexture");


    private int resultID = Shader.PropertyToID("Result");
    private Vector4 sizeAndInvSizeVector;
    private int width;
    private int height;

    public RenderTexture TileIndexAndSceneDistanceTexture { get => tileIndexAndSceneDistanceTexture; }

    public DrawTileAndDepthPass(ComputeShader cs)
    {
        profilingSampler = new ProfilingSampler(profilerTag);
        drawTileIndexAndDistanceComputeShader = cs;
        drawTileIndexAndDistanceKernel = drawTileIndexAndDistanceComputeShader.FindKernel("DrawTileIndexAndSceneDis");
    }
    private void InitTexture(RenderTextureDescriptor descriptor)
    {
        RenderTextureDescriptor des = descriptor;
        if (tileIndexAndSceneDistanceTexture == null || width != descriptor.width || height != descriptor.height)
        {
            width = descriptor.width;
            height = descriptor.height;
            des.colorFormat = RenderTextureFormat.RGHalf;
            des.sRGB = false;
            des.enableRandomWrite = true;
            des.width = descriptor.width / 4;
            des.height = descriptor.height / 4;
            sizeAndInvSizeVector = new Vector4(descriptor.width, descriptor.height, 1.0f / descriptor.width, 1.0f / descriptor.height);
            tileIndexAndSceneDistanceTexture = new RenderTexture(des);
            tileIndexAndSceneDistanceTexture.Create();
        }
    }

    private void ClearTexture()
    {
        if (tileIndexAndSceneDistanceTexture != null)
        {
            tileIndexAndSceneDistanceTexture.Release();
            tileIndexAndSceneDistanceTexture = null;
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
        int threadGroupX = (int)(sizeAndInvSizeVector.x + 31) / 32;
        int threadGroupY = (int)(sizeAndInvSizeVector.y + 31) / 32;
        using (new ProfilingScope(cmd, profilingSampler))
        {
            cmd.SetComputeVectorParam(drawTileIndexAndDistanceComputeShader, sizeAndInvSizeID, sizeAndInvSizeVector);
            cmd.SetComputeIntParam(drawTileIndexAndDistanceComputeShader, jitterIndexID, VolumeRenderFeature.Instance.JitterIndex);
            
            cmd.SetComputeTextureParam(drawTileIndexAndDistanceComputeShader, drawTileIndexAndDistanceKernel, minMaxDepthTextureID, VolumeRenderFeature.Instance.MinMaxDepth);
            cmd.SetComputeTextureParam(drawTileIndexAndDistanceComputeShader, drawTileIndexAndDistanceKernel, resultID, tileIndexAndSceneDistanceTexture);
            cmd.DispatchCompute(drawTileIndexAndDistanceComputeShader, drawTileIndexAndDistanceKernel, threadGroupX, threadGroupY, 1);
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

