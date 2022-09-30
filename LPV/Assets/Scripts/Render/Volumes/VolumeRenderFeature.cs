using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public enum PexilSize
{
    Full,
    Half,
    Quarter,
    One_Eighth,
}
public class VolumeRenderFeature : ScriptableRendererFeature
{
    
    [System.Serializable]
    public struct VolumeSetting
    {
        public PexilSize SampleSize;
        public Material cloudMaterial;
        public Material blitMat;
        public Bounds bounds;

        public Vector3 _moveDir;
        [Range(0, 1)]
        public float _moveScale;
        [Range(-2, 2)]
        public float _g;
        [Range(0.01f, 800)]
        public float _MarchLength;
        [Range(0, 3000)]
        public int _MaxMarchCount;
        [Range(0, 16)]
        public int _LightMaxMarchNumber;
        [Range(0.01f, 1)]
        public float _BlueNoiseEffect;
        [Range(0.0001f, 1)]
        public float _Pos2UVScale;
        public RenderPassEvent renderPassEvent;
        [Space(4)]
        public ComputeShader minMaxComputeShader;

        public ComputeShader tileIndexAndSceneDisComputeShader;

        public ComputeShader resctructionComputeShader;
        
        public ComputeShader raymarchingCloudShader;

        public RenderPassEvent BlitRenderPassEvent;
    }

    public VolumeSetting volumeSetting;

    private VolumeRenderPass scriptablePass;
    private RayMarchingCloudRenderPass rayMarchingCloudRenderPass;
    private MinMaxDepthPass minMaxDepthPass;
    private ReconstructionPass reconstructionPass;
    private DrawTileAndDepthPass tileIndexAndSceneDisPass;
    private BlitVolumeRenderPass blitVolumeRenderPass;
    
    private static VolumeRenderFeature _Instance;
    private Matrix4x4 previewView;
    private Matrix4x4 previewProj;
    public static VolumeRenderFeature Instance { get => _Instance;}


    /// <inheritdoc/>
    public override void Create()
    {
        _Instance = this;
        scriptablePass = new VolumeRenderPass(volumeSetting);
        minMaxDepthPass = new MinMaxDepthPass(volumeSetting.minMaxComputeShader);
        reconstructionPass = new ReconstructionPass(volumeSetting.resctructionComputeShader);
        tileIndexAndSceneDisPass = new DrawTileAndDepthPass(volumeSetting.tileIndexAndSceneDisComputeShader);
        blitVolumeRenderPass = new BlitVolumeRenderPass(volumeSetting);
        rayMarchingCloudRenderPass = new RayMarchingCloudRenderPass(volumeSetting.raymarchingCloudShader);
        // Configures where the render pass should be injected.
        minMaxDepthPass.renderPassEvent = volumeSetting.renderPassEvent;
        reconstructionPass.renderPassEvent = volumeSetting.renderPassEvent;
        scriptablePass.renderPassEvent = volumeSetting.renderPassEvent;
        tileIndexAndSceneDisPass.renderPassEvent = volumeSetting.renderPassEvent;
        blitVolumeRenderPass.renderPassEvent = volumeSetting.BlitRenderPassEvent;
        rayMarchingCloudRenderPass.renderPassEvent = volumeSetting.renderPassEvent;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var camera = renderingData.cameraData.camera;
        minMaxDepthPass.Setup(renderer.cameraDepthTarget);
        blitVolumeRenderPass.Setup(renderer.cameraColorTarget);
        reconstructionPass.PreviewView = previewView;
        reconstructionPass.PreviewProj = previewProj;
        renderer.EnqueuePass(minMaxDepthPass);
        renderer.EnqueuePass(tileIndexAndSceneDisPass);
        //renderer.EnqueuePass(scriptablePass);
        renderer.EnqueuePass(rayMarchingCloudRenderPass);
        renderer.EnqueuePass(reconstructionPass);
        renderer.EnqueuePass(blitVolumeRenderPass);
        previewView = camera.worldToCameraMatrix;
        previewProj = camera.projectionMatrix;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        scriptablePass.Clear();
        minMaxDepthPass.Clear();
        reconstructionPass.Clear();
        tileIndexAndSceneDisPass.Clear();
    }

    public RenderTexture MinMaxDepth { get => minMaxDepthPass.MinMaxHalfDepth; }

    public RenderTexture RaymarchingTextures { get => rayMarchingCloudRenderPass.RayMarchingCloudTexture; }

    public RenderTexture ReconstructionTexture { get => reconstructionPass.CloudRenderTexture; }

    public RenderTexture TileIndexAndSceneDistanceTexture { get => tileIndexAndSceneDisPass.TileIndexAndSceneDistanceTexture; }

    public int JitterIndex { get => rayMarchingCloudRenderPass.JitterIndex; }


}


