using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class Master : MonoBehaviour
{
    [SerializeField] private BoxCollider boxCollider;
    private RenderTexture target;
    private Camera cam;
    private int kernelIndex;
    private ComputeShader raymarching;

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

    void SetParameters () {
        raymarching.SetVector("_boxPosition", boxCollider.transform.position);
        raymarching.SetVector("_boxHalfSize", boxCollider.size * 0.5f);
        raymarching.SetVector("_boxMin", boxCollider.bounds.min);
        raymarching.SetVector("_boxMax", boxCollider.bounds.max);
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
}
