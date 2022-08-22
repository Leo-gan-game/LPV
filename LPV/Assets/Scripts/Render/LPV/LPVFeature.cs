using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class LPVFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class LPVFeatureSetting
    {
        public int lpvGridResolution = 32;
        public int rsmResolution=512;
        public Bounds bounds;
        public ComputeShader cs;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
        public int LpvGridResolution { get => lpvGridResolution; set => lpvGridResolution = value; }
        public ComputeShader CS { get => cs; set => cs = value; }
        public Bounds Bounds { get => bounds; set => bounds = value; }
    }

    PropagationPass propagationPass;
    public LPVFeatureSetting featureSetting;
    private static LPVFeature Instance;

    public LPVFeatureSetting FeatureSetting { get => featureSetting; set => featureSetting = value; }
    public static LPVFeature Feature { get => Instance;}


    /// <inheritdoc/>
    public override void Create()
    {
        Instance = this;
        if (propagationPass != null)
        {
            propagationPass.Clear();
        }
        propagationPass = new PropagationPass(featureSetting.CS,featureSetting.lpvGridResolution, featureSetting.rsmResolution, featureSetting.bounds);
        
        // Configures where the render pass should be injected.
        propagationPass.renderPassEvent = featureSetting.renderPassEvent;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(propagationPass);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        propagationPass.Clear();
    }
}


