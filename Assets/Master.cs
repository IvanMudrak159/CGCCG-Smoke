using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class Master : MonoBehaviour
{
    [SerializeField] private Shape shape;
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
        Vector3 col = new Vector3 (shape.Color.r, shape.Color.g, shape.Color.b);
        ShapeData[] shapeDatas = new ShapeData[1];
        ShapeData shapeData = new ShapeData()
        {
            position = shape.Position,
            size = shape.Collider.bounds.size * 0.5f,
            color = col,
            colliderMin = shape.Collider.bounds.min,
            colliderMax = shape.Collider.bounds.max,
            shapeType = (int) shape.Type,
            sigmaA = shape.SigmaA,
        };
        shapeDatas[0] = shapeData;
        
        
        ComputeBuffer shapeBuffer = new ComputeBuffer (1, ShapeData.GetSize ());
        shapeBuffer.SetData (shapeDatas);
        raymarching.SetInt ("numShapes", 1);
        raymarching.SetBuffer (kernelIndex, "shapes", shapeBuffer);

        /*
        raymarching.SetVector("_boxPosition", shape.Position);
        raymarching.SetVector("_boxHalfSize", shape.Collider.bounds.size * 0.5f);
        raymarching.SetVector("_boxMin", shape.Collider.bounds.min);
        raymarching.SetVector("_boxMax", shape.Collider.bounds.max);
        */
        
        raymarching.SetMatrix ("_CameraToWorld", cam.cameraToWorldMatrix);
        raymarching.SetMatrix ("_CameraInverseProjection", cam.projectionMatrix.inverse);
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
        
        InitRenderTexture ();
        SetParameters();
        raymarching.SetTexture (kernelIndex, "Result", target);
        raymarching.SetTexture (kernelIndex, "Source", source);

        int threadGroupsX = Mathf.CeilToInt (cam.pixelWidth / 8.0f);
        int threadGroupsY = Mathf.CeilToInt (cam.pixelHeight / 8.0f);
        raymarching.Dispatch (kernelIndex, threadGroupsX, threadGroupsY, 1);

        Graphics.Blit (target, destination);
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
        public float sigmaA;
        
        public static int GetSize () {
            return sizeof (float) * 16 + sizeof (int);
        }
    }
}
