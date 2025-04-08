using UnityEngine;

public class Shape : MonoBehaviour
{
    public enum ShapeType {Sphere,Cube};
    public enum PhaseType {Isotropic, HenyeyGreenstein, None};
    
    [SerializeField] private ShapeType shapeType = ShapeType.Cube;
    [SerializeField] private PhaseType phase = PhaseType.Isotropic;
    [SerializeField] private Color color = Color.white;
    [SerializeField, Range(0, 2)] private float sigmaA; 
    [SerializeField, Range(0, 2)] private float sigmaS; 
    [SerializeField, Range(-0.5f, 0.5f)] private float g; 
    [SerializeField] private Collider _collider;
    [SerializeField] private bool useLight;
    [SerializeField] private bool useForwardReymarching;

    public ShapeType Type => shapeType;
    public PhaseType TypePhase => phase;
    public Color Color => color;
    public float SigmaA => sigmaA;
    public float SigmaS => sigmaS;
    public float G => g;
    public Vector3 Position => transform.position;
    public Collider Collider => _collider;
    public bool UseLight => useLight;
    public bool UseForwardReymarching => useForwardReymarching;
}
