using System.Collections.Generic;
using UnityEngine;

[ImageEffectAllowedInSceneView]
public class Master : MonoBehaviour
{
    [SerializeField] private List<Shape> shapes;
    [SerializeField] private Light light;
    [SerializeField] private float globalDensity;
    [SerializeField] private Voxelizer smokeVoxelData = null;
    [Range(0.0f, 1.0f), SerializeField] private float densityFalloff = 0.25f;
    [SerializeField, Range(0, 2)] private float sigmaA; 
    [SerializeField, Range(0, 2)] private float sigmaS;
    [SerializeField] private bool useLight;
    [SerializeField] private Shape.PhaseType phase = Shape.PhaseType.Isotropic;
    [SerializeField, Range(-0.5f, 0.5f)] private float g; 
    [Range(1, 2560)] public int stepCount = 150;
    [Range(1, 32)] public int lightStepCount = 16;
    public Color smokeColor;
    public Color extinctionColor;
    [Range(0.01f, 0.1f)] public float stepSize = 0.05f;
    [Range(0.01f, 1.0f)] public float lightStepSize = 0.25f;
    [Range(0.0f, 10.0f)] public float volumeDensity = 1.0f;
    [Range(0.0f, 10.0f)] public float shadowDensity = 1.0f;
    [Range(0.0f, 1.0f)] public float alphaThreshold = 0.1f;
    [Range(0.0f, 3.0f)] public float scatteringCoefficient = 0.5f;
    [Range(-1.0f, 1.0f)] public float sharpness;
    private RenderTexture smokeAlbedoFullTex, smokeAlbedoQuarterTex;
    private RenderTexture smokeMaskFullTex , smokeMaskQuarterTex;

    private RenderTexture target;
    private Camera cam;
    private int kernelIndex;
    private ComputeShader raymarching;
    private ComputeBuffer smokeVoxelBuffer;
    private RenderTexture depthTex;
    private Material compositeMaterial;

    private List<ComputeBuffer> buffersToDispose;

    
    private void Awake()
    {
        raymarching = (ComputeShader)Resources.Load("Shaders/Raymarching");
        if (raymarching == null)
        {
            Debug.LogError("Compute Shader is not assigned!");
        }
        else
        {
            kernelIndex = raymarching.FindKernel("CSMain");
        }
        
        depthTex = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
        depthTex.enableRandomWrite = true;
        depthTex.Create();
        
        smokeAlbedoFullTex = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB64, RenderTextureReadWrite.Linear);
        smokeAlbedoFullTex.enableRandomWrite = true;
        smokeAlbedoFullTex.Create();

        smokeMaskFullTex = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
        smokeMaskFullTex.enableRandomWrite = true;
        smokeMaskFullTex.Create();

        smokeAlbedoQuarterTex = new RenderTexture(Screen.width/4, Screen.height/4, 0, RenderTextureFormat.ARGB64, RenderTextureReadWrite.Linear);
        smokeAlbedoQuarterTex.enableRandomWrite = true;
        smokeAlbedoQuarterTex.Create();

        smokeMaskQuarterTex = new RenderTexture(Screen.width/4, Screen.height/4, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
        smokeMaskQuarterTex.enableRandomWrite = true;
        smokeMaskQuarterTex.Create();
        
        compositeMaterial = new Material(Shader.Find("Hidden/CompositeEffects"));

    }

    void SetParameters ()
    {
        ShapeData[] shapeDatas = new ShapeData[shapes.Count];
        for (int i = 0; i < shapes.Count; i++)
        {
            
            Shape shape = shapes[i];
            
            if (shape == null) 
            {
                Debug.LogWarning($"Shape at index {i} is null, skipping.");
                continue;
            }
            
            Vector3 col = new Vector3 (shape.Color.r, shape.Color.g, shape.Color.b);
            ShapeData shapeData = new ShapeData()
            {
                position = shape.Position,
                size = shape.Collider.bounds.size * 0.5f,
                color = col,
                colliderMin = shape.Collider.bounds.min,
                colliderMax = shape.Collider.bounds.max,
                shapeType = (int) shape.Type,
                phaseType = (int) shape.TypePhase,
                sigmaA = shape.SigmaA,
                sigmaS = shape.SigmaS,
                sparsity = shape.Sparsity,
                transparency = shape.Transparency,
                gravityMultiplier = shape.GravityMultiplier,
                g = shape.G,
                useLight = shape.UseLight ? 1 : 0,
                useForwardRaymarching = shape.UseForwardReymarching ? 1 : 0,
            };
            shapeDatas[i] = shapeData;
        }
        
        ComputeBuffer shapeBuffer = new ComputeBuffer (shapeDatas.Length, ShapeData.GetSize ());
        shapeBuffer.SetData (shapeDatas);
        raymarching.SetInt ("numShapes", shapeDatas.Length);
        raymarching.SetBuffer (kernelIndex, "shapes", shapeBuffer);
        raymarching.SetMatrix ("_CameraToWorld", cam.cameraToWorldMatrix);
        raymarching.SetMatrix ("_CameraInverseProjection", cam.projectionMatrix.inverse);
        buffersToDispose.Add (shapeBuffer);
        
        
        bool lightIsDirectional = light.type == LightType.Directional;
        raymarching.SetVector ("_Light", (!lightIsDirectional) ? light.transform.forward : light.transform.position);
        raymarching.SetVector ("_LightColor", light.color);
        raymarching.SetFloat ("_LightIntensity", light.intensity);
        raymarching.SetFloat ("globalDensity", globalDensity);
        raymarching.SetBool ("positionLight", !lightIsDirectional);
        raymarching.SetFloat("_DensityFalloff", 1 - densityFalloff);

        Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
        Matrix4x4 viewProjMatrix = projMatrix * cam.worldToCameraMatrix;
        raymarching.SetMatrix("_CameraInvViewProjection", viewProjMatrix.inverse);

        raymarching.SetVector("_Radius", smokeVoxelData.GetSmokeRadius());
        raymarching.SetVector("_SmokeOrigin", smokeVoxelData.GetSmokeOrigin());
        raymarching.SetTexture(kernelIndex, "_DepthTex", depthTex);
        
        raymarching.SetFloat("sigmaA",  sigmaA);
        raymarching.SetFloat("sigmaS",  sigmaS);
        int lightUse = useLight ? 1 : 0;
        raymarching.SetInt("useLight", lightUse);
        raymarching.SetInt("phaseType", (int) phase);
        raymarching.SetFloat("g", g);
        
        raymarching.SetInt("_StepCount", stepCount);
        raymarching.SetInt("_LightStepCount", lightStepCount);

        raymarching.SetVector("_SmokeColor", smokeColor);
        raymarching.SetVector("_ExtinctionColor", extinctionColor);

        raymarching.SetFloat("_VolumeDensity", volumeDensity * stepSize);
        raymarching.SetFloat("_ShadowDensity", shadowDensity * lightStepSize);

        raymarching.SetFloat("_StepSize", stepSize);
        raymarching.SetFloat("_LightStepSize", lightStepSize);
        raymarching.SetFloat("_ScatteringCoefficient", scatteringCoefficient);
        raymarching.SetFloat("_AlphaThreshold", alphaThreshold);

        raymarching.SetFloat("_ElapsedTime", Time.time);
        
        raymarching.SetTexture(kernelIndex, "_SmokeMaskTex", smokeMaskFullTex);
        raymarching.SetTexture (kernelIndex, "Result", target);
    }

    void Update()
    {
        if (smokeVoxelData != null) {
            smokeVoxelBuffer = smokeVoxelData.GetSmokeVoxelBuffer(); 
            raymarching.SetBuffer(0, "_SmokeVoxels", smokeVoxelBuffer);
            raymarching.SetVector("_BoundsExtent", smokeVoxelData.GetBoundsExtent());
            raymarching.SetVector("_VoxelResolution", smokeVoxelData.GetVoxelResolution());
        }
    }

    void OnRenderImage (RenderTexture source, RenderTexture destination) {
        Graphics.Blit(source, depthTex, compositeMaterial, 0);
        
        cam = Camera.current;
        if (cam == null)
        {
            cam = Camera.main;
        }
        
        if (cam == null || raymarching == null)
        {
            Graphics.Blit(source, destination);
            return;
        }
        buffersToDispose = new List<ComputeBuffer> ();
        InitRenderTexture ();
        SetParameters();
        // --- Render smoke at quarter resolution ---
        // Set up quarter-res target for raymarching
        RenderTexture quarterTarget = smokeAlbedoQuarterTex;
        raymarching.SetTexture (kernelIndex, "Source", source);
        raymarching.SetTexture (kernelIndex, "Result", quarterTarget);
        raymarching.SetTexture (kernelIndex, "_SmokeMaskTex", smokeMaskQuarterTex);
        int threadGroupsX = Mathf.CeilToInt (quarterTarget.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt (quarterTarget.height / 8.0f);
        raymarching.Dispatch (kernelIndex, threadGroupsX, threadGroupsY, 1);

        // Upscale quarter-res smoke to full-res target
        // (target is a full-res RenderTexture)
        compositeMaterial.SetTexture("_SmokeTex", smokeAlbedoQuarterTex);
        compositeMaterial.SetTexture("_SmokeMaskTex", smokeMaskQuarterTex);
        compositeMaterial.SetTexture("_DepthTex", depthTex);
        compositeMaterial.SetTexture("_MainTex", source); // Pass full-res scene to composite
        compositeMaterial.SetFloat("_Sharpness", sharpness);
        compositeMaterial.SetFloat("_DebugView", 0);

        // Upscale smoke only: write upscaled smoke to 'target' (full-res, only smoke)
        Graphics.Blit(smokeAlbedoQuarterTex, target, compositeMaterial, 1);

        // Composite upscaled smoke over the original full-res scene
        // Output to destination
        compositeMaterial.SetTexture("_SmokeTex", target); // Now _SmokeTex is full-res upscaled smoke
        Graphics.Blit(source, destination, compositeMaterial, 2);

        foreach (var buffer in buffersToDispose)
        {
            buffer.Dispose();
        }
    }

    private void InitRenderTexture () {
        if (target == null || target.width != cam.pixelWidth || target.height != cam.pixelHeight) {
            if (target != null) {
                target.Release ();
            }
            target = new RenderTexture (cam.pixelWidth, cam.pixelHeight, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.enableRandomWrite = true;
            target.Create ();
        }
    }

    struct ShapeData
    {
        public Vector3 position;
        public Vector3 size;
        public Vector3 color;
        public Vector3 colliderMin;
        public Vector3 colliderMax;
        public int shapeType;
        public int phaseType;
        public float sigmaA;
        public float sigmaS;
        public float sparsity;
        public float transparency;
        public float gravityMultiplier;
        public float g;
        public int useLight;
        public int useForwardRaymarching;
        
        public static int GetSize () {
            return sizeof (float) * 21 + sizeof (int) * 4;
        }
    }
}
