using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class Master : MonoBehaviour
{
    [SerializeField] private List<Shape> shapes;
    [SerializeField] private Light light;
    [SerializeField] private float globalDensity;
    private RenderTexture target;
    private Camera cam;
    private int kernelIndex;
    private ComputeShader raymarching;

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
                continue; // Skip this shape if it's destroyed or null
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
                featheringStrength = shape.FeatheringStrength,
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

    }

    
    void OnRenderImage (RenderTexture source, RenderTexture destination) {
        
        // Get the current camera that's rendering (works for both Game and Scene view)
        cam = Camera.current;
        if (cam == null)
        {
            cam = Camera.main;
        }
        
        if (cam == null || raymarching == null)
        {
            Graphics.Blit(source, destination); // не робимо нічого, якщо помилка
            return;
        }
        buffersToDispose = new List<ComputeBuffer> ();
        InitRenderTexture ();
        SetParameters();
        raymarching.SetTexture (kernelIndex, "Result", target);
        raymarching.SetTexture (kernelIndex, "Source", source);

        int threadGroupsX = Mathf.CeilToInt (cam.pixelWidth / 8.0f);
        int threadGroupsY = Mathf.CeilToInt (cam.pixelHeight / 8.0f);
        raymarching.Dispatch (kernelIndex, threadGroupsX, threadGroupsY, 1);

        Graphics.Blit (target, destination);

        foreach (var buffer in buffersToDispose) {
            buffer.Dispose ();
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
        public float featheringStrength;
        public float g;
        public int useLight;
        public int useForwardRaymarching;
        
        public static int GetSize () {
            return sizeof (float) * 19 + sizeof (int) * 4;
        }
    }
}
