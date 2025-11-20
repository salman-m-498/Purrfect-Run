using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(SplineComponent))]
public class LevelBuilder : MonoBehaviour
{
    public enum ColliderMode { None, Mesh, BoxSegments }

    [Header("Sampling")]
    public float sampleDistance = 1f;
    public float simplifyTolerance = 0.25f; // set >0 to simplify
    public bool alignToWorldRight = true;

    [Header("Cross-section")]
    public float width = 6f;
    public float bankFactor = 1f; // rotate cross-section on curves
    public float heightOffset = 0f;

    [Header("Mesh")]
    public Material material;
    public float uvScale = 1f;
    public bool flatShaded = true;

    [Header("Colliders")]
    public ColliderMode colliderMode = ColliderMode.BoxSegments;
    public PhysicMaterial physicsMaterial;

    [Header("Optimization")]
    public bool generateLightmapUVs = false;
    public bool combineMesh = false; // future use

    // generated
    [HideInInspector] public Mesh generatedMesh;
    GameObject meshHolder;
    List<GameObject> colliderBoxes = new List<GameObject>();

    SplineComponent spline;

    public void Generate()
    {
        spline = GetComponent<SplineComponent>();
        if (spline == null || spline.controlPoints.Count < 2)
        {
            Debug.LogWarning("Spline needs at least 2 control points.");
            return;
        }

        // sample spline
        List<Vector3> samples = SampleSplinePoints();

        if (simplifyTolerance > 0.0001f)
            samples = DouglasPeuckerSimplifier.Simplify(samples, simplifyTolerance);

        if (samples.Count < 2)
        {
            Debug.LogWarning("Not enough samples after simplify.");
            return;
        }

        // align average tangent to world right if requested
        if (alignToWorldRight)
            AlignAverageTangentToWorldRight(samples);

        // build mesh
        BuildMeshFromSamples(samples);

        // build colliders
        ClearColliders();
        if (colliderMode == ColliderMode.Mesh)
            BuildMeshCollider();
        else if (colliderMode == ColliderMode.BoxSegments)
            BuildBoxColliders(samples);

        Debug.Log("Level generated: " + name);
    }

    List<Vector3> SampleSplinePoints()
    {
        List<Vector3> points = new List<Vector3>();
        float totalLength = EstimateSplineLength();
        int steps = Mathf.Max(2, Mathf.CeilToInt(totalLength / sampleDistance));
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 p = spline.GetPoint(t) + Vector3.up * heightOffset;
            points.Add(p);
        }
        return points;
    }

    public float EstimateSplineLength()
    {
        int steps = Mathf.Max(8, spline.controlPoints.Count * 8);
        float len = 0f;
        Vector3 prev = spline.GetPoint(0);
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector3 p = spline.GetPoint(t);
            len += Vector3.Distance(prev, p);
            prev = p;
        }
        return len;
    }

    void AlignAverageTangentToWorldRight(List<Vector3> samples)
    {
        // compute average tangent
        Vector3 avgT = Vector3.zero;
        for (int i = 0; i < samples.Count - 1; i++)
            avgT += (samples[i + 1] - samples[i]).normalized;
        avgT.Normalize();
        if (avgT == Vector3.zero) return;
        Quaternion rot = Quaternion.FromToRotation(avgT, Vector3.right);
        // rotate all sample points around origin to align
        for (int i = 0; i < samples.Count; i++)
            samples[i] = rot * (samples[i] - transform.position) + transform.position;
        // also rotate the GameObject's transform so result matches visually
        transform.rotation = rot * transform.rotation;
    }

    void BuildMeshFromSamples(List<Vector3> samples)
    {
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // Build cube meshes between consecutive samples
        for (int i = 0; i < samples.Count - 1; i++)
        {
            Vector3 a = samples[i];
            Vector3 b = samples[i + 1];
            Vector3 midpoint = (a + b) * 0.5f;
            Vector3 dir = (b - a);
            float length = dir.magnitude;
            if (length < 0.0001f) continue;
            dir.Normalize();

            // Calculate banking at midpoint (same as colliders)
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(up, dir).normalized;
            if (right == Vector3.zero) right = Vector3.right;

            float bank = 0f;
            if (i > 0 && i < samples.Count - 1)
            {
                Vector3 prevDir = (samples[i] - samples[i - 1]).normalized;
                Vector3 nextDir = (samples[i + 1] - samples[i]).normalized;
                float angle = Vector3.SignedAngle(prevDir, nextDir, up) * Mathf.Deg2Rad;
                bank = Mathf.Clamp(angle * bankFactor, -45f * Mathf.Deg2Rad, 45f * Mathf.Deg2Rad);
            }

            Quaternion roll = Quaternion.AngleAxis(bank * Mathf.Rad2Deg, dir);
            Vector3 rolledRight = roll * right;
            
            // Convert to local space
            Vector3 localMidpoint = transform.InverseTransformPoint(midpoint);
            
            // Build a cube with the width and segment length
            // Cube dimensions: width (x), 2 units tall (y), length along forward (z)
            float halfWidth = width * 0.5f;
            float halfHeight = 1f; // box is 2 units tall, so half is 1
            float halfLength = length * 0.5f;

            int baseVert = verts.Count;
            
            // Create 8 vertices of the cube in local space around the midpoint
            Vector3[] cubeVerts = new Vector3[8];
            cubeVerts[0] = localMidpoint + transform.InverseTransformDirection(-rolledRight * halfWidth - up * halfHeight - dir * halfLength);
            cubeVerts[1] = localMidpoint + transform.InverseTransformDirection(rolledRight * halfWidth - up * halfHeight - dir * halfLength);
            cubeVerts[2] = localMidpoint + transform.InverseTransformDirection(rolledRight * halfWidth + up * halfHeight - dir * halfLength);
            cubeVerts[3] = localMidpoint + transform.InverseTransformDirection(-rolledRight * halfWidth + up * halfHeight - dir * halfLength);
            
            cubeVerts[4] = localMidpoint + transform.InverseTransformDirection(-rolledRight * halfWidth - up * halfHeight + dir * halfLength);
            cubeVerts[5] = localMidpoint + transform.InverseTransformDirection(rolledRight * halfWidth - up * halfHeight + dir * halfLength);
            cubeVerts[6] = localMidpoint + transform.InverseTransformDirection(rolledRight * halfWidth + up * halfHeight + dir * halfLength);
            cubeVerts[7] = localMidpoint + transform.InverseTransformDirection(-rolledRight * halfWidth + up * halfHeight + dir * halfLength);

            for (int v = 0; v < 8; v++)
                verts.Add(cubeVerts[v]);

            // Add UVs
            for (int v = 0; v < 8; v++)
                uvs.Add(new Vector2(v % 2, v / 4));

            // Build 12 triangles (6 faces * 2 triangles each)
            // Front face (z-)
            tris.Add(baseVert + 0); tris.Add(baseVert + 2); tris.Add(baseVert + 1);
            tris.Add(baseVert + 0); tris.Add(baseVert + 3); tris.Add(baseVert + 2);
            // Back face (z+)
            tris.Add(baseVert + 5); tris.Add(baseVert + 6); tris.Add(baseVert + 4);
            tris.Add(baseVert + 4); tris.Add(baseVert + 6); tris.Add(baseVert + 7);
            // Left face (-x)
            tris.Add(baseVert + 4); tris.Add(baseVert + 7); tris.Add(baseVert + 0);
            tris.Add(baseVert + 0); tris.Add(baseVert + 7); tris.Add(baseVert + 3);
            // Right face (+x)
            tris.Add(baseVert + 1); tris.Add(baseVert + 2); tris.Add(baseVert + 5);
            tris.Add(baseVert + 5); tris.Add(baseVert + 2); tris.Add(baseVert + 6);
            // Bottom face (-y)
            tris.Add(baseVert + 4); tris.Add(baseVert + 0); tris.Add(baseVert + 1);
            tris.Add(baseVert + 4); tris.Add(baseVert + 1); tris.Add(baseVert + 5);
            // Top face (+y)
            tris.Add(baseVert + 3); tris.Add(baseVert + 7); tris.Add(baseVert + 6);
            tris.Add(baseVert + 3); tris.Add(baseVert + 6); tris.Add(baseVert + 2);
        }

        // Create mesh
        Mesh mesh = new Mesh();
        mesh.name = name + "_GeneratedMesh";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // assign to GameObject
        if (meshHolder == null)
        {
            meshHolder = new GameObject(name + "_GeneratedMesh");
            meshHolder.transform.SetParent(transform, false);
        }
        
        MeshFilter mf = meshHolder.GetComponent<MeshFilter>();
        if (mf == null) mf = meshHolder.AddComponent<MeshFilter>();
        MeshRenderer mr = meshHolder.GetComponent<MeshRenderer>();
        if (mr == null) mr = meshHolder.AddComponent<MeshRenderer>();

        mf.sharedMesh = mesh;
        generatedMesh = mesh;
        
        // Assign material; if none set, create a default white material for visibility
        if (material != null)
        {
            mr.sharedMaterial = material;
        }
        else
        {
            // Create a default material if none assigned
            Material defaultMat = new Material(Shader.Find("Standard"));
            defaultMat.name = "DefaultLevelMaterial";
            mr.sharedMaterial = defaultMat;
            Debug.LogWarning("No material assigned to LevelBuilder. Using default white material. Assign a material in the Inspector for custom appearance.", this);
        }

        // Ensure renderer is enabled
        mr.enabled = true;

        // remove previous mesh collider component on meshHolder (if any)
        MeshCollider existing = meshHolder.GetComponent<MeshCollider>();
        if (existing != null && colliderMode != ColliderMode.Mesh)
            DestroyImmediate(existing);
    }

    void ApplyFlatShading(Mesh mesh)
    {
        // Duplicate vertices so each triangle has its own verts and a single normal.
        Vector3[] oldVerts = mesh.vertices;
        int[] oldTris = mesh.triangles;
        Vector2[] oldUV = mesh.uv;

        Vector3[] newVerts = new Vector3[oldTris.Length];
        Vector2[] newUVs = new Vector2[oldTris.Length];
        int[] newTris = new int[oldTris.Length];
        Vector3[] newNormals = new Vector3[oldTris.Length];

        for (int i = 0; i < oldTris.Length; i += 3)
        {
            int i0 = oldTris[i];
            int i1 = oldTris[i + 1];
            int i2 = oldTris[i + 2];

            Vector3 v0 = oldVerts[i0];
            Vector3 v1 = oldVerts[i1];
            Vector3 v2 = oldVerts[i2];

            Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;

            int ni = i;
            newVerts[ni] = v0; newVerts[ni + 1] = v1; newVerts[ni + 2] = v2;
            newUVs[ni] = oldUV[i0]; newUVs[ni + 1] = oldUV[i1]; newUVs[ni + 2] = oldUV[i2];
            newNormals[ni] = normal; newNormals[ni + 1] = normal; newNormals[ni + 2] = normal;
            newTris[ni] = ni; newTris[ni + 1] = ni + 1; newTris[ni + 2] = ni + 2;
        }

        mesh.Clear();
        mesh.vertices = newVerts;
        mesh.triangles = newTris;
        mesh.uv = newUVs;
        mesh.normals = newNormals;
    }

    void BuildMeshCollider()
    {
        if (meshHolder == null || generatedMesh == null) return;
        MeshCollider mc = meshHolder.GetComponent<MeshCollider>();
        if (mc == null) mc = meshHolder.AddComponent<MeshCollider>();
        mc.sharedMesh = generatedMesh;
        if (physicsMaterial != null) mc.sharedMaterial = physicsMaterial;
        mc.convex = false; // for long tracks, MeshCollider non-convex is fine for static geometry
    }

    void BuildBoxColliders(List<Vector3> samples)
    {
        // create a set of box colliders each spanning a segment
        // use the same geometry logic as the mesh to ensure they're perfectly aligned
        for (int i = 0; i < samples.Count - 1; i++)
        {
            Vector3 a = samples[i];
            Vector3 b = samples[i + 1];
            Vector3 midpoint = (a + b) * 0.5f;
            Vector3 dir = (b - a);
            float length = dir.magnitude;
            if (length < 0.0001f) continue;
            dir.Normalize();

            // Calculate banking at midpoint (same as mesh does)
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(up, dir).normalized;
            if (right == Vector3.zero) right = Vector3.right;

            float bank = 0f;
            if (i > 0 && i < samples.Count - 1)
            {
                Vector3 prevDir = (samples[i] - samples[i - 1]).normalized;
                Vector3 nextDir = (samples[i + 1] - samples[i]).normalized;
                float angle = Vector3.SignedAngle(prevDir, nextDir, up) * Mathf.Deg2Rad;
                bank = Mathf.Clamp(angle * bankFactor, -45f * Mathf.Deg2Rad, 45f * Mathf.Deg2Rad);
            }

            Quaternion roll = Quaternion.AngleAxis(bank * Mathf.Rad2Deg, dir);
            
            // Convert midpoint to local space (same as mesh does)
            Vector3 localMidpoint = transform.InverseTransformPoint(midpoint);
            
            GameObject go = new GameObject("SegmentCollider_" + i);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localMidpoint;
            go.transform.localRotation = Quaternion.Inverse(transform.rotation) * Quaternion.LookRotation(dir, up);

            BoxCollider bc = go.AddComponent<BoxCollider>();
            // size: x = width, y = thickness (2), z = segment length
            bc.size = new Vector3(width, 2f, length + 0.01f);
            if (physicsMaterial != null) bc.sharedMaterial = physicsMaterial;
            colliderBoxes.Add(go);
        }
    }

    void ClearColliders()
    {
        for (int i = colliderBoxes.Count - 1; i >= 0; i--)
            DestroyImmediate(colliderBoxes[i]);
        colliderBoxes.Clear();
    }

    public void Clear()
    {
        if (meshHolder != null) DestroyImmediate(meshHolder);
        ClearColliders();
        generatedMesh = null;
    }
}
