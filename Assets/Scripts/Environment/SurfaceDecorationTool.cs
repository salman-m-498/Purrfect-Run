using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

#if UNITY_EDITOR
[ExecuteInEditMode]
public class SurfaceDecorationTool : MonoBehaviour
{
    [Header("Target Mesh")]
    public MeshFilter targetMesh;
    
    [Header("Decoration Settings")]
    public GameObject decorationPrefab; // Your grass quad
    public float density = 1f; // Decorations per square unit
    public float minScale = 0.8f;
    public float maxScale = 1.2f;
    public float randomRotation = 15f;
    public Vector2 randomOffset = new Vector2(0.1f, 0.1f);
    
    [Header("Surface Filtering")]
    public float maxSlopeAngle = 60f; // Only place on surfaces under this angle
    public LayerMask surfaceLayer;
    
    [Header("Optimization")]
    public bool useGPUInstancing = true;
    public int maxInstancesPerBatch = 1023; // WebGL limit
    public bool combineMeshes = false; // Alternative to instancing
    
    [Header("Preview")]
    public bool showPreview = true;
    public Color gizmoColor = Color.green;
    
    private List<Matrix4x4> decorationMatrices = new List<Matrix4x4>();
    private List<Vector3> previewPositions = new List<Vector3>();

    public void GenerateDecorations()
    {
        if (targetMesh == null || decorationPrefab == null)
        {
            Debug.LogError("Please assign target mesh and decoration prefab!");
            return;
        }

        // Clear existing decorations
        ClearDecorations();
        decorationMatrices.Clear();
        previewPositions.Clear();

        Mesh mesh = targetMesh.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        int[] triangles = mesh.triangles;

        // Calculate surface area and generate points
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int idx0 = triangles[i];
            int idx1 = triangles[i + 1];
            int idx2 = triangles[i + 2];

            Vector3 v0 = targetMesh.transform.TransformPoint(vertices[idx0]);
            Vector3 v1 = targetMesh.transform.TransformPoint(vertices[idx1]);
            Vector3 v2 = targetMesh.transform.TransformPoint(vertices[idx2]);

            Vector3 n0 = targetMesh.transform.TransformDirection(normals[idx0]);
            Vector3 n1 = targetMesh.transform.TransformDirection(normals[idx1]);
            Vector3 n2 = targetMesh.transform.TransformDirection(normals[idx2]);

            // Check if triangle is facing upward enough
            Vector3 avgNormal = (n0 + n1 + n2) / 3f;
            float angle = Vector3.Angle(avgNormal, Vector3.up);
            
            if (angle > maxSlopeAngle)
                continue;

            // Calculate triangle area
            float area = Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
            int pointCount = Mathf.Max(1, Mathf.RoundToInt(area * density));

            // Generate random points on triangle
            for (int p = 0; p < pointCount; p++)
            {
                Vector3 point = GetRandomPointOnTriangle(v0, v1, v2);
                Vector3 normal = GetInterpolatedNormal(v0, v1, v2, n0, n1, n2, point);
                
                // Add random offset
                point += new Vector3(
                    Random.Range(-randomOffset.x, randomOffset.x),
                    0,
                    Random.Range(-randomOffset.y, randomOffset.y)
                );

                // Raycast to ensure it's on the surface
                if (Physics.Raycast(point + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 4f, surfaceLayer))
                {
                    PlaceDecoration(hit.point, hit.normal);
                }
                else
                {
                    PlaceDecoration(point, normal);
                }
            }
        }

        if (useGPUInstancing)
        {
            CreateInstancedBatches();
        }
        else if (combineMeshes)
        {
            CreateCombinedMesh();
        }
        else
        {
            CreateIndividualObjects();
        }

        Debug.Log($"Generated {decorationMatrices.Count} decorations");
    }

    void PlaceDecoration(Vector3 position, Vector3 normal)
    {
        // Create rotation aligned to surface normal
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal);
        
        // Add random rotation around the normal
        rotation *= Quaternion.Euler(0, Random.Range(-randomRotation, randomRotation), 0);
        
        // Random scale
        float scale = Random.Range(minScale, maxScale);
        Vector3 scaleVec = new Vector3(scale, scale, scale);

        Matrix4x4 matrix = Matrix4x4.TRS(position, rotation, scaleVec);
        decorationMatrices.Add(matrix);
        previewPositions.Add(position);
    }

    void CreateInstancedBatches()
    {
        MeshFilter prefabMesh = decorationPrefab.GetComponent<MeshFilter>();
        MeshRenderer prefabRenderer = decorationPrefab.GetComponent<MeshRenderer>();

        if (prefabMesh == null || prefabRenderer == null)
        {
            Debug.LogError("Decoration prefab needs MeshFilter and MeshRenderer!");
            return;
        }

        Material mat = prefabRenderer.sharedMaterial;
        if (mat != null)
        {
            mat.enableInstancing = true;
        }

        // Create batches
        int batchCount = Mathf.CeilToInt((float)decorationMatrices.Count / maxInstancesPerBatch);
        
        for (int b = 0; b < batchCount; b++)
        {
            GameObject batchObj = new GameObject($"DecorationBatch_{b}");
            batchObj.transform.SetParent(transform);
            
            SurfaceDecorationBatch batch = batchObj.AddComponent<SurfaceDecorationBatch>();
            
            int startIdx = b * maxInstancesPerBatch;
            int count = Mathf.Min(maxInstancesPerBatch, decorationMatrices.Count - startIdx);
            
            batch.matrices = decorationMatrices.GetRange(startIdx, count).ToArray();
            batch.mesh = prefabMesh.sharedMesh;
            batch.material = mat;
        }
    }

    void CreateCombinedMesh()
    {
        MeshFilter prefabMesh = decorationPrefab.GetComponent<MeshFilter>();
        MeshRenderer prefabRenderer = decorationPrefab.GetComponent<MeshRenderer>();

        if (prefabMesh == null || prefabRenderer == null) return;

        List<CombineInstance> combines = new List<CombineInstance>();

        foreach (Matrix4x4 matrix in decorationMatrices)
        {
            CombineInstance ci = new CombineInstance();
            ci.mesh = prefabMesh.sharedMesh;
            ci.transform = matrix;
            combines.Add(ci);
        }

        // Split into multiple meshes if too many vertices
        int verticesPerInstance = prefabMesh.sharedMesh.vertexCount;
        int maxVertices = 65000; // Unity mesh limit
        int instancesPerMesh = maxVertices / verticesPerInstance;

        int meshCount = Mathf.CeilToInt((float)combines.Count / instancesPerMesh);

        for (int m = 0; m < meshCount; m++)
        {
            GameObject meshObj = new GameObject($"CombinedDecorations_{m}");
            meshObj.transform.SetParent(transform);

            MeshFilter mf = meshObj.AddComponent<MeshFilter>();
            MeshRenderer mr = meshObj.AddComponent<MeshRenderer>();

            int startIdx = m * instancesPerMesh;
            int count = Mathf.Min(instancesPerMesh, combines.Count - startIdx);

            Mesh combined = new Mesh();
            combined.CombineMeshes(combines.GetRange(startIdx, count).ToArray(), true, true);
            combined.Optimize();
            
            mf.sharedMesh = combined;
            mr.sharedMaterial = prefabRenderer.sharedMaterial;
        }
    }

    void CreateIndividualObjects()
    {
        GameObject container = new GameObject("DecorationObjects");
        container.transform.SetParent(transform);

        foreach (Matrix4x4 matrix in decorationMatrices)
        {
            GameObject instance = Instantiate(decorationPrefab, container.transform);
            instance.transform.position = matrix.GetColumn(3);
            instance.transform.rotation = matrix.rotation;
            instance.transform.localScale = matrix.lossyScale;
        }
    }

    public void ClearDecorations()
    {
        // Remove all child objects
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
    }

    Vector3 GetRandomPointOnTriangle(Vector3 v0, Vector3 v1, Vector3 v2)
    {
        float r1 = Random.value;
        float r2 = Random.value;

        if (r1 + r2 > 1f)
        {
            r1 = 1f - r1;
            r2 = 1f - r2;
        }

        return v0 + r1 * (v1 - v0) + r2 * (v2 - v0);
    }

    Vector3 GetInterpolatedNormal(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 n0, Vector3 n1, Vector3 n2, Vector3 point)
    {
        Vector3 bary = GetBarycentricCoordinates(v0, v1, v2, point);
        return (n0 * bary.x + n1 * bary.y + n2 * bary.z).normalized;
    }

    Vector3 GetBarycentricCoordinates(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 point)
    {
        Vector3 v0v1 = v1 - v0;
        Vector3 v0v2 = v2 - v0;
        Vector3 v0p = point - v0;

        float d00 = Vector3.Dot(v0v1, v0v1);
        float d01 = Vector3.Dot(v0v1, v0v2);
        float d11 = Vector3.Dot(v0v2, v0v2);
        float d20 = Vector3.Dot(v0p, v0v1);
        float d21 = Vector3.Dot(v0p, v0v2);

        float denom = d00 * d11 - d01 * d01;
        float v = (d11 * d20 - d01 * d21) / denom;
        float w = (d00 * d21 - d01 * d20) / denom;
        float u = 1f - v - w;

        return new Vector3(u, v, w);
    }

    void OnDrawGizmos()
    {
        if (!showPreview || previewPositions.Count == 0) return;

        Gizmos.color = gizmoColor;
        foreach (Vector3 pos in previewPositions)
        {
            Gizmos.DrawWireSphere(pos, 0.1f);
        }
    }
}

// Batch renderer for GPU instancing
public class SurfaceDecorationBatch : MonoBehaviour
{
    public Matrix4x4[] matrices;
    public Mesh mesh;
    public Material material;

    void Update()
    {
        if (mesh != null && material != null && matrices != null)
        {
            Graphics.DrawMeshInstanced(mesh, 0, material, matrices);
        }
    }
}

[CustomEditor(typeof(SurfaceDecorationTool))]
public class SurfaceDecorationToolEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SurfaceDecorationTool tool = (SurfaceDecorationTool)target;

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate Decorations", GUILayout.Height(30)))
        {
            tool.GenerateDecorations();
        }

        if (GUILayout.Button("Clear Decorations", GUILayout.Height(30)))
        {
            tool.ClearDecorations();
        }
    }
}
#endif