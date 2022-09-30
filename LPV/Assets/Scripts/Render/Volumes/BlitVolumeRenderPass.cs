using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BlitVolumeRenderPass : ScriptableRenderPass
{

    //public Bounds cloudBounds;
    //public Material cloudMaterial;
    //public Material blitMat;
    public RenderTargetIdentifier cameraColorTexture;
    private VolumeRenderFeature.VolumeSetting volumeSetting;
    private int frameIndex = 0;
    private const string profilerTag = "Volumes Blit Pass";
    private int sizeAndInvSizeID = Shader.PropertyToID("SizeAndInvSize");
    private int width;
    private int height;
    private Vector4 sizeAndInvSizeVector;

    public int JitterIndex { get => frameIndex;}

    public BlitVolumeRenderPass(VolumeRenderFeature.VolumeSetting volumeSetting)
    {
        profilingSampler = new ProfilingSampler(profilerTag);
        this.volumeSetting = volumeSetting;
        Application.targetFrameRate = 30;
    }


    public void Setup(RenderTargetIdentifier renderTexture)
    {
        cameraColorTexture = renderTexture;
    }

    private void InitTexture(CommandBuffer cmd,RenderTextureDescriptor descriptor)
    {
        sizeAndInvSizeVector = new Vector4(descriptor.width, descriptor.height, 1.0f / descriptor.width, 1.0f / descriptor.height);
    }

    public void Clear()
    {

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
        using (new ProfilingScope(cmd, profilingSampler))
        {
            cmd.SetGlobalTexture("_MainTex", VolumeRenderFeature.Instance.ReconstructionTexture);

            cmd.SetRenderTarget(cameraColorTexture, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.DontCare);
            cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, volumeSetting.blitMat, 0, 0);
        }
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
    }

    public override void OnCameraCleanup(CommandBuffer cmd)
    {
       
       
    }
}