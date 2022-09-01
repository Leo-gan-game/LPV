
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PropagationPass : ScriptableRenderPass
{
    private const string profilerTag = "Propagation Pass";
    private static readonly int gridRTexID = Shader.PropertyToID("gridRTex");
    private static readonly int gridGTexID = Shader.PropertyToID("gridGTex");
    private static readonly int gridBTexID = Shader.PropertyToID("gridBTex");
    private static readonly int gridLuminanceTexID = Shader.PropertyToID("gridLuminanceTex");

    private static readonly int outputGridRTexID = Shader.PropertyToID("outputGridRTex");
    private static readonly int outputGridGTexID = Shader.PropertyToID("outputGridGTex");
    private static readonly int outputGridBTexID = Shader.PropertyToID("outputGridBTex");


    private static readonly int lpvRedSHInputID = Shader.PropertyToID("lpvRedSHInput");
    private static readonly int lpvGreenSHInputID = Shader.PropertyToID("lpvGreenSHInput");
    private static readonly int lpvBlueSHInputID = Shader.PropertyToID("lpvBlueSHInput");

    private static readonly int lpvRedSHOutputID = Shader.PropertyToID("lpvRedSHOutput");
    private static readonly int lpvGreenSHOutputID = Shader.PropertyToID("lpvGreenSHOutput");
    private static readonly int lpvBlueSHOutputID = Shader.PropertyToID("lpvBlueSHOutput");


    private static readonly int cellCountID = Shader.PropertyToID("CellCount");
    private static readonly int rsmResolutionID = Shader.PropertyToID("rsmResolution");

    private static readonly int worldToLightLocalMatrixID = Shader.PropertyToID("WorldToLightLocalMatrix");
    private static readonly int minID = Shader.PropertyToID("minAABB");
    private static readonly int maxID = Shader.PropertyToID("maxAABB");
    private static readonly int cellSizeID = Shader.PropertyToID("cellSize");
    private ComputeShader propagationCS;
    private RenderTexture gridRT;
    private int propagationKernel;
    private int propagationCompositionKernel;
    private int lightInjectKernel;
    private int lpvClearKernel;
    private Bounds bounds;
    private int lpvGridResolution;
    private int rsmResolution;
    private LPVData lPVData;
    private int passIndex;
    private struct LPVData
    {
        public RenderTexture lpvRedSH;
        public RenderTexture lpvGreenSH;
        public RenderTexture lpvBlueSH;
        public RenderTexture lpvLuminance;
        public int dimXYZ;
        public RenderTexture lpvRedPropagationBuffer;
        public RenderTexture lpvGreenPropagationBuffer;
        public RenderTexture lpvBluePropagationBuffer;
    }
    public PropagationPass(ComputeShader cs,int gridResolution,int rsmResolution, Bounds aabb)
    {
        this.propagationCS = cs;
        bounds = aabb;
        propagationKernel = propagationCS.FindKernel("propagation");
        propagationCompositionKernel = propagationCS.FindKernel("propagationComposition");
        lightInjectKernel = propagationCS.FindKernel("lightInject");
        lpvClearKernel = propagationCS.FindKernel("lpvClear");
        profilingSampler = new ProfilingSampler(profilerTag);
        lpvGridResolution = gridResolution;
        this.rsmResolution = rsmResolution;
        InitTexture();


    }

    private void InitTexture()
    {
        var desc = new RenderTextureDescriptor(lpvGridResolution, lpvGridResolution, RenderTextureFormat.ARGBFloat);
        desc.dimension = TextureDimension.Tex3D;
        desc.bindMS = false;
        desc.volumeDepth = lpvGridResolution;
        desc.depthBufferBits = 0;
        desc.width = lpvGridResolution;
        desc.height = lpvGridResolution;
        desc.enableRandomWrite = true;
        desc.msaaSamples = 1;
        desc.sRGB = true;
        if(lPVData.lpvRedSH == null)
        {
            lPVData.lpvRedSH = new RenderTexture(desc);
            lPVData.lpvGreenSH = new RenderTexture(desc);
            lPVData.lpvBlueSH = new RenderTexture(desc);
            lPVData.lpvLuminance = new RenderTexture(desc);
            lPVData.lpvRedPropagationBuffer = new RenderTexture(desc);
            lPVData.lpvGreenPropagationBuffer = new RenderTexture(desc);
            lPVData.lpvBluePropagationBuffer = new RenderTexture(desc);

            lPVData.lpvRedSH.filterMode = FilterMode.Trilinear;
            lPVData.lpvGreenSH.filterMode = FilterMode.Trilinear;
            lPVData.lpvBlueSH.filterMode = FilterMode.Trilinear;

            lPVData.lpvRedPropagationBuffer.filterMode = FilterMode.Trilinear;
            lPVData.lpvGreenPropagationBuffer.filterMode = FilterMode.Trilinear;
            lPVData.lpvBluePropagationBuffer.filterMode = FilterMode.Trilinear;

            lPVData.lpvRedSH.Create();
            lPVData.lpvGreenSH.Create();
            lPVData.lpvBlueSH.Create();
            lPVData.lpvLuminance.Create();

            lPVData.lpvRedPropagationBuffer.Create();
            lPVData.lpvGreenPropagationBuffer.Create();
            lPVData.lpvBluePropagationBuffer.Create();
        }

        //lPVData.dimXYZ = 32 * 32 * 32;
        //int stride = Marshal.SizeOf(typeof(float));
        //lPVData.lpvLuminance = new ComputeBuffer(lPVData.dimXYZ, stride, ComputeBufferType.Structured);
       
    }

    private void ClearTexture()
    {
        if (lPVData.lpvRedSH)
        {
            lPVData.lpvRedSH.Release();
            lPVData.lpvRedSH = null;
        }
        if (lPVData.lpvGreenSH)
        {
            lPVData.lpvGreenSH.Release();
            lPVData.lpvGreenSH = null;
        }
        if (lPVData.lpvBlueSH)
        {
            lPVData.lpvBlueSH.Release();
            lPVData.lpvBlueSH = null;
        }
        if (lPVData.lpvLuminance)
        {
            lPVData.lpvLuminance.Release();
            lPVData.lpvLuminance = null;
        }
        if (lPVData.lpvRedPropagationBuffer)
        {
            lPVData.lpvRedPropagationBuffer.Release();
            lPVData.lpvRedPropagationBuffer = null;
        }
        if (lPVData.lpvGreenPropagationBuffer)
        {
            lPVData.lpvGreenPropagationBuffer.Release();
            lPVData.lpvGreenPropagationBuffer = null;
        }
        if (lPVData.lpvBluePropagationBuffer)
        {
            lPVData.lpvBluePropagationBuffer.Release();
            lPVData.lpvBluePropagationBuffer = null;
        }
        
        

    }


    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        InitTexture();
        base.Configure(cmd, cameraTextureDescriptor);
        ConfigureClear(ClearFlag.All, Color.black);
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
        int thread_groups = (lpvGridResolution + 3) / 4;
        // read Main Light index;
        int shadowLightIndex = renderingData.lightData.mainLightIndex;

        //read light through light index;
        VisibleLight shadowLight = renderingData.lightData.visibleLights[shadowLightIndex];

        Light light = shadowLight.light;

        using (new ProfilingScope(cmd, profilingSampler))
        {
            context.ExecuteCommandBuffer(cmd);//set cmd need execute the command.
            cmd.Clear();
            cmd.SetComputeMatrixParam(propagationCS, worldToLightLocalMatrixID, light.transform.worldToLocalMatrix);
            if (passIndex % 8 == 0)
            {
                LpvClear(cmd);
                LightInject(cmd);
                passIndex = 0;
                for (int i = 0; i < 8; i++)
                {
                    LPVPropagation(context, cmd);
                }
            }
            passIndex++;
        }

        cmd.SetGlobalTexture(gridRTexID, lPVData.lpvRedSH);
        cmd.SetGlobalTexture(gridGTexID, lPVData.lpvGreenSH);
        cmd.SetGlobalTexture(gridBTexID, lPVData.lpvBlueSH);
        cmd.SetGlobalVector(minID, bounds.min);
        cmd.SetGlobalVector(maxID, bounds.max);
        cmd.SetGlobalMatrix(worldToLightLocalMatrixID, light.transform.worldToLocalMatrix);
        var cellSize = (bounds.max - bounds.min) / lpvGridResolution;
        cmd.SetGlobalVector(cellSizeID, cellSize);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        CommandBufferPool.Release(cmd);
    }

    private void LPVPropagation(ScriptableRenderContext context, CommandBuffer cmd)
    {
        cmd.SetComputeTextureParam(propagationCS, propagationKernel, outputGridRTexID, lPVData.lpvRedPropagationBuffer);
        cmd.SetComputeTextureParam(propagationCS, propagationKernel, outputGridGTexID, lPVData.lpvGreenPropagationBuffer);
        cmd.SetComputeTextureParam(propagationCS, propagationKernel, outputGridBTexID, lPVData.lpvBluePropagationBuffer);

        cmd.SetComputeTextureParam(propagationCS, propagationKernel, gridRTexID, lPVData.lpvRedSH);
        cmd.SetComputeTextureParam(propagationCS, propagationKernel, gridGTexID, lPVData.lpvGreenSH);
        cmd.SetComputeTextureParam(propagationCS, propagationKernel, gridBTexID, lPVData.lpvBlueSH);

        cmd.DispatchCompute(propagationCS, propagationKernel, lpvGridResolution, lpvGridResolution, lpvGridResolution);

        cmd.SetComputeTextureParam(propagationCS, propagationCompositionKernel, lpvRedSHInputID, lPVData.lpvRedPropagationBuffer);
        cmd.SetComputeTextureParam(propagationCS, propagationCompositionKernel, lpvGreenSHInputID, lPVData.lpvGreenPropagationBuffer);
        cmd.SetComputeTextureParam(propagationCS, propagationCompositionKernel, lpvBlueSHInputID, lPVData.lpvBluePropagationBuffer);

        cmd.SetComputeTextureParam(propagationCS, propagationCompositionKernel, lpvRedSHOutputID, lPVData.lpvRedSH);
        cmd.SetComputeTextureParam(propagationCS, propagationCompositionKernel, lpvGreenSHOutputID, lPVData.lpvGreenSH);
        cmd.SetComputeTextureParam(propagationCS, propagationCompositionKernel, lpvBlueSHOutputID, lPVData.lpvBlueSH);


        cmd.DispatchCompute(propagationCS, propagationCompositionKernel, lpvGridResolution/8, lpvGridResolution/8, lpvGridResolution);
    }

    public void LpvClear(CommandBuffer cmd)
    {
        int thread_groups = (lpvGridResolution + 3) / 4;
        cmd.SetComputeTextureParam(propagationCS, lpvClearKernel, gridRTexID, lPVData.lpvRedSH);
        cmd.SetComputeTextureParam(propagationCS, lpvClearKernel, gridGTexID, lPVData.lpvGreenSH);
        cmd.SetComputeTextureParam(propagationCS, lpvClearKernel, gridBTexID, lPVData.lpvBlueSH);
        cmd.SetComputeTextureParam(propagationCS, lpvClearKernel, gridLuminanceTexID, lPVData.lpvLuminance);
        var cellSize = (bounds.max - bounds.min) / lpvGridResolution;
        cmd.SetComputeVectorParam(propagationCS, cellSizeID, cellSize);
        cmd.DispatchCompute(propagationCS, lpvClearKernel, thread_groups, thread_groups, thread_groups);
    }

    public void LightInject(CommandBuffer cmd)
    {
        int thread_groups = (lpvGridResolution + 3) / 4;
        cmd.SetComputeTextureParam(propagationCS, lightInjectKernel, gridRTexID, lPVData.lpvRedSH);
        cmd.SetComputeTextureParam(propagationCS, lightInjectKernel, gridGTexID, lPVData.lpvGreenSH);
        cmd.SetComputeTextureParam(propagationCS, lightInjectKernel, gridBTexID, lPVData.lpvBlueSH);
        cmd.SetComputeTextureParam(propagationCS, lightInjectKernel, gridLuminanceTexID, lPVData.lpvLuminance);

        cmd.SetComputeVectorParam(propagationCS, minID, bounds.min);
        cmd.SetComputeVectorParam(propagationCS, maxID, bounds.max);
        cmd.SetComputeIntParam(propagationCS, cellCountID, lpvGridResolution);
        cmd.SetComputeIntParam(propagationCS, rsmResolutionID, rsmResolution);
        var cellSize = (bounds.max - bounds.min) / lpvGridResolution;
        cmd.SetComputeVectorParam(propagationCS, cellSizeID, cellSize);
        cmd.DispatchCompute(propagationCS, lightInjectKernel, lpvGridResolution, lpvGridResolution, lpvGridResolution);

    }
    // Cleanup any allocated resources that were created during the execution of this render pass.
    public override void OnCameraCleanup(CommandBuffer cmd)
    {
    }

    public void Clear()
    {
        ClearTexture();

    }
    
}
