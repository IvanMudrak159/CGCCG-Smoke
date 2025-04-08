using UnityEngine;

public class Shape : MonoBehaviour
{
    public enum ShapeType {Sphere,Cube};
    
    [SerializeField] private ShapeType shapeType = ShapeType.Cube;
    [SerializeField] private Color color = Color.white;
    [SerializeField, Range(0, 2)] private float sigmaA; 
    [SerializeField] private Collider _collider;
    [SerializeField] private bool useLight;

    public ShapeType Type => shapeType;
    public Color Color => color;
    public float SigmaA => sigmaA;
    public Vector3 Position => transform.position;
    public Collider Collider => _collider;
    public bool UseLight => useLight;
}
