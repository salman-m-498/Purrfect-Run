using UnityEngine;

[ExecuteInEditMode]
public class SkyDome : MonoBehaviour
{
    [Header("Dome Settings")]
    [Range(8, 32)]
    public int segments = 16; // Keep low for PSX look
    [Range(4, 16)]
    public int rings = 8; // Vertical segments
    public float radius = 500f;
    [Range(0f, 1f)]
    public float bottomCutoff = 0.3f; // Don't render bottom of sphere

    [Header("Materials")]
    public Material skyMaterial;
    
    [Header("Auto-Update")]
    public bool autoGenerate = false;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    void OnEnable()
    {
        SetupComponents();
        
        // Position at camera first
        if (Camera.main != null)
        {
            transform.position = Camera.main.transform.position;
        }
        
        GenerateSkyDome();
    }

    void Start()
    {
        // Initial position at camera
        if (Camera.main != null)
        {
            transform.position = Camera.main.transform.position;
        }
    }

    void LateUpdate()
    {
        // Follow camera position (not rotation) every frame
        if (Camera.main != null)
        {
            transform.position = Camera.main.transform.position;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (autoGenerate && Application.isPlaying)
        {
            GenerateSkyDome();
        }
    }
#endif

    void SetupComponents()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = gameObject.AddComponent<MeshFilter>();

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        if (skyMaterial != null)
            meshRenderer.sharedMaterial = skyMaterial;
    }

    [ContextMenu("Generate Sky Dome")]
    public void GenerateSkyDome()
    {
        Mesh mesh = new Mesh();
        mesh.name = "PSX Sky Dome";

        // Calculate vertex count
        int vertexCount = (segments + 1) * (rings + 1);
        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        
        int vertIndex = 0;

        // Generate vertices
        for (int ring = 0; ring <= rings; ring++)
        {
            float v = (float)ring / rings;
            // Adjust vertical angle to skip bottom portion
            float verticalAngle = Mathf.Lerp(bottomCutoff * Mathf.PI, Mathf.PI * 0.5f, v);
            float y = Mathf.Sin(verticalAngle);
            float ringRadius = Mathf.Cos(verticalAngle);

            for (int seg = 0; seg <= segments; seg++)
            {
                float u = (float)seg / segments;
                float horizontalAngle = u * Mathf.PI * 2f;

                float x = Mathf.Cos(horizontalAngle) * ringRadius;
                float z = Mathf.Sin(horizontalAngle) * ringRadius;

                vertices[vertIndex] = new Vector3(x, y, z) * radius;
                normals[vertIndex] = -new Vector3(x, y, z).normalized; // Inverted normals
                uvs[vertIndex] = new Vector2(u, v);

                vertIndex++;
            }
        }

        // Generate triangles
        int[] triangles = new int[segments * rings * 6];
        int triIndex = 0;

        for (int ring = 0; ring < rings; ring++)
        {
            for (int seg = 0; seg < segments; seg++)
            {
                int current = ring * (segments + 1) + seg;
                int next = current + segments + 1;

                // First triangle (inverted winding for inside view)
                triangles[triIndex++] = current;
                triangles[triIndex++] = current + 1;
                triangles[triIndex++] = next;

                // Second triangle
                triangles[triIndex++] = next;
                triangles[triIndex++] = current + 1;
                triangles[triIndex++] = next + 1;
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        mesh.RecalculateBounds();
        meshFilter.sharedMesh = mesh;

        Debug.Log($"Sky dome generated: {vertices.Length} vertices, {triangles.Length / 3} triangles");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius * 0.1f);
    }
}