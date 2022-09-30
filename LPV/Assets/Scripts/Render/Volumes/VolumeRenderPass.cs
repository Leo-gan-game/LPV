using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class VolumeRenderPass : ScriptableRenderPass
{
    //private bool downsampling;
    private RenderTexture volumeRT;
    
    //public Bounds cloudBounds;
    //public Material cloudMaterial;
    //public Material blitMat;
    public RenderTargetIdentifier cameraColorTexture;
    private VolumeRenderFeature.VolumeSetting volumeSetting;
    private int frameIndex = 0;
    private const string profilerTag = "Volumes Pass";
    private int VolumeRTID =  Shader.PropertyToID("VolumeRT");
    private int JitterIndexID = Shader.PropertyToID("JitterIndex");
    private int sizeAndInvSizeID = Shader.PropertyToID("SizeAndInvSize");
    private int tileIndexAndSceneDistanceTextureID = Shader.PropertyToID("TileIndexAndSceneDistanceTexture");
    private int width;
    private int height;
    private Vector4 sizeAndInvSizeVector;

    public RenderTexture VolumeRT { get => volumeRT;  }
    public int JitterIndex { get => frameIndex;}

    public VolumeRenderPass(VolumeRenderFeature.VolumeSetting volumeSetting)
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
        if (volumeRT == null || width !=descriptor.width || height != descriptor.height)
        {
            
            var desc = descriptor;
            width = descriptor.width;
            height = descriptor.height;
            desc.enableRandomWrite = true;
            desc.dimension = TextureDimension.Tex2D;
            switch (volumeSetting.SampleSize)
            {
                case PexilSize.Half:
                    desc.width = desc.width / 2;
                    desc.height = desc.height / 2;
                    break;
                case PexilSize.Quarter:
                    desc.width = desc.width / 4;
                    desc.height = desc.height / 4;
                    break;
                case PexilSize.One_Eighth:
                    desc.width = desc.width / 8;
                    desc.height = desc.height / 8;
                    break;
            }
            desc.bindMS = false;
            desc.msaaSamples = 1;
            desc.sRGB = true;
            desc.depthBufferBits = 0;
            volumeRT = new RenderTexture(desc);
            volumeRT.Create();

        }
         

    }

    public void Clear()
    {
        if (volumeRT != null)
        {
            volumeRT.Release();
            volumeRT = null;

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
        using (new ProfilingScope(cmd, profilingSampler))
        {

            DrawCloud(cmd, volumeRT, renderingData.cameraData.camera);
            
            //cmd.SetGlobalTexture("_MainTex", VolumeRenderFeature.Instance.ReconstructionTexture);

            //cmd.SetRenderTarget(cameraColorTexture, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.DontCare);
            //cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, volumeSetting.blitMat, 0, 0);

            ++frameIndex;
            frameIndex = frameIndex % 4;
        }
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
    }


    private void DrawCloud(CommandBuffer cmd,RenderTargetIdentifier target, Camera camera)
    {
        var projectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
        //cmd.SetRenderTarget(target);
        cmd.SetRenderTarget(target, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
        volumeSetting.cloudMaterial.SetVector("boxMin", volumeSetting.bounds.min);
        volumeSetting.cloudMaterial.SetVector("boxMax", volumeSetting.bounds.max);

        volumeSetting.cloudMaterial.SetVector("_moveDir", volumeSetting._moveDir);
        volumeSetting.cloudMaterial.SetFloat("_moveScale", volumeSetting._moveScale);
        volumeSetting.cloudMaterial.SetFloat("_g", volumeSetting._g);
        volumeSetting.cloudMaterial.SetFloat("_MarchLength", volumeSetting._MarchLength);
        volumeSetting.cloudMaterial.SetInt("_MaxMarchCount", volumeSetting._MaxMarchCount);
        volumeSetting.cloudMaterial.SetFloat("_LightMaxMarchNumber", volumeSetting._LightMaxMarchNumber);
        volumeSetting.cloudMaterial.SetFloat("_BlueNoiseEffect", volumeSetting._BlueNoiseEffect);
        volumeSetting.cloudMaterial.SetFloat("_Pos2UVScale", volumeSetting._Pos2UVScale);
        volumeSetting.cloudMaterial.SetInt(JitterIndexID, frameIndex);
        volumeSetting.cloudMaterial.SetVector(sizeAndInvSizeID, sizeAndInvSizeVector);
        volumeSetting.cloudMaterial.SetTexture("_MinMaxDepthTexture", VolumeRenderFeature.Instance.MinMaxDepth);
        volumeSetting.cloudMaterial.SetTexture(tileIndexAndSceneDistanceTextureID, VolumeRenderFeature.Instance.TileIndexAndSceneDistanceTexture);
        cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
        switch (volumeSetting.SampleSize)
        {
            case PexilSize.Full:
                cmd.SetViewport(new Rect(0, 0, camera.pixelWidth, camera.pixelHeight));
                break;
            case PexilSize.Half:
                cmd.SetViewport(new Rect(0, 0, camera.pixelWidth/2, camera.pixelHeight/2));
                break;
            case PexilSize.Quarter:
                cmd.SetViewport(new Rect(0, 0, camera.pixelWidth/4, camera.pixelHeight/4));
                break;
            case PexilSize.One_Eighth:
                cmd.SetViewport(new Rect(0, 0, camera.pixelWidth/8, camera.pixelHeight/8));
                break;

        }
        cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, volumeSetting.cloudMaterial, 0, 0);
    }
    // Cleanup any allocated resources that were created during the execution of this render pass.
    public override void OnCameraCleanup(CommandBuffer cmd)
    {
       
       
    }
}