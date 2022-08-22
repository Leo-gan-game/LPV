using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RSMDebugPass : ScriptableRenderPass
{
    public Material blitMaterial = null;
    private const string profilerTag = "DrawDebug";
    private RenderTargetIdentifier cameraColorTarget;
    private RenderTargetIdentifier sourceIdentifier;

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer cmd = CommandBufferPool.Get(profilerTag);

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        using (new ProfilingScope(cmd, profilingSampler))
        {
            Camera camera = renderingData.cameraData.camera;
            DrawDebug(cmd, cameraColorTarget, camera);
        }
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }
    public void Setup(RenderTargetIdentifier cameraColorTarget,RenderTargetIdentifier rsmFlux)
    {
        this.cameraColorTarget = cameraColorTarget;
        this.sourceIdentifier = rsmFlux;
    }

    private void DrawDebug(CommandBuffer cmd, RenderTargetIdentifier target, Camera camera)
    {
        //var projectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
        //cmd.SetRenderTarget(target, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, RenderBufferLoadAction.Load, RenderBufferStoreAction.DontCare);
        //cmd.ClearRenderTarget(false, true, Color.clear);
        //cmd.SetGlobalMatrix("unity_CameraProjection", projectionMatrix);
        //cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
        //cmd.SetViewport(camera.pixelRect);
        //cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, blitMaterial, 0, 0);
        cmd.Blit(sourceIdentifier, target);
    }
}
