using UnityEngine;
using System.Collections.Generic;

public class WebGLFoliageSpawner : MonoBehaviour
{
    [System.Serializable]
    public class FoliageType
    {
        public string name = "Foliage";
        public Mesh mesh;
        public Material material;
        [Range(0f, 1f)] public float density = 0.5f;
        public Vector2 scaleRange = new Vector2(0.8f, 1.2f);
        public bool randomRotation = true;
    }

    [Header("Spawn Area")]
    public Vector2 spawnAreaSize = new Vector2(100, 100);
    public float chunkSize = 20f;
    public LayerMask groundMask;
    public float maxGroundDistance = 5f;

    [Header("Foliage Types")]
    public FoliageType[] foliageTypes;

    [Header("WebGL Performance")]
    public int maxVerticesPerMesh = 65000; // Unity mesh limit
    public bool useMeshCombining = true; // Better for WebGL
    public bool useInstancing = false; // Fallback option
    public float cullingDistance = 50f;

    [Header("Debug")]
    public bool showGizmos = true;
    public bool respawnOnStart = true;

    private List<GameObject> combinedMeshObjects = new List<GameObject>();
    private Dictionary<FoliageType, List<List<Matrix4x4>>> instanceBatches = new Dictionary<FoliageType, List<List<Matrix4x4>>>();
    private Camera mainCam;

    void Start()
    {
        mainCam = Camera.main;
        
        if (respawnOnStart)
        {
            SpawnFoliage();
        }
    }

    public void SpawnFoliage()
    {
        ClearFoliage();

        Vector3 center = transform.position;
        Vector2 halfSize = spawnAreaSize / 2f;

        foreach (var foliageType in foliageTypes)
        {
            if (foliageType.mesh == null || foliageType.material == null) continue;

            List<CombineInstance> combineList = new List<CombineInstance>();
            List<Matrix4x4> instanceList = new List<Matrix4x4>();

            // Calculate spawn points
            int spawnCount = Mathf.RoundToInt(spawnAreaSize.x * spawnAreaSize.y * foliageType.density);
            int currentVertexCount = 0;

            for (int i = 0; i < spawnCount; i++)
            {
                Vector3 randomPos = new Vector3(
                    center.x + Random.Range(-halfSize.x, halfSize.x),
                    center.y + 100f,
                    center.z + Random.Range(-halfSize.y, halfSize.y)
                );

                RaycastHit hit;
                if (Physics.Raycast(randomPos, Vector3.down, out hit, 200f, groundMask))
                {
                    if (Vector3.Distance(hit.point, new Vector3(hit.point.x, center.y, hit.point.z)) < maxGroundDistance)
                    {
                        Vector3 spawnPos = hit.point;
                        float scale = Random.Range(foliageType.scaleRange.x, foliageType.scaleRange.y);
                        Quaternion rotation = foliageType.randomRotation 
                            ? Quaternion.Euler(0, Random.Range(0f, 360f), 0) 
                            : Quaternion.identity;

                        rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * rotation;
                        Matrix4x4 matrix = Matrix4x4.TRS(spawnPos, rotation, Vector3.one * scale);

                        if (useMeshCombining)
                        {
                            // Check if we need to create a new combined mesh
                            currentVertexCount += foliageType.mesh.vertexCount;
                            
                            if (currentVertexCount >= maxVerticesPerMesh && combineList.Count > 0)
                            {
                                CreateCombinedMesh(combineList, foliageType);
                                combineList.Clear();
                                currentVertexCount = foliageType.mesh.vertexCount;
                            }

                            CombineInstance combine = new CombineInstance
                            {
                                mesh = foliageType.mesh,
                                transform = matrix
                            };
                            combineList.Add(combine);
                        }
                        else
                        {
                            instanceList.Add(matrix);
                        }
                    }
                }
            }

            // Create final combined mesh or setup instancing
            if (useMeshCombining && combineList.Count > 0)
            {
                CreateCombinedMesh(combineList, foliageType);
            }
            else if (useInstancing && instanceList.Count > 0)
            {
                // Split into batches for instancing (WebGL safe limit)
                int batchSize = 256;
                instanceBatches[foliageType] = new List<List<Matrix4x4>>();
                
                for (int i = 0; i < instanceList.Count; i += batchSize)
                {
                    int count = Mathf.Min(batchSize, instanceList.Count - i);
                    List<Matrix4x4> batch = instanceList.GetRange(i, count);
                    instanceBatches[foliageType].Add(batch);
                }
            }
        }

        Debug.Log($"Foliage spawned. Combined meshes: {combinedMeshObjects.Count}");
    }

    void CreateCombinedMesh(List<CombineInstance> combines, FoliageType foliageType)
    {
        GameObject meshObj = new GameObject($"Combined_{foliageType.name}");
        meshObj.transform.parent = transform;
        meshObj.transform.localPosition = Vector3.zero;

        MeshFilter mf = meshObj.AddComponent<MeshFilter>();
        MeshRenderer mr = meshObj.AddComponent<MeshRenderer>();

        Mesh combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(combines.ToArray(), true, true);
        
        // Optimize for WebGL
        combinedMesh.Optimize();
        combinedMesh.RecalculateBounds();

        mf.mesh = combinedMesh;
        mr.material = foliageType.material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        combinedMeshObjects.Add(meshObj);
    }

    void Update()
    {
        // Only used if instancing is enabled
        if (!useInstancing || instanceBatches.Count == 0) return;

        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;

        foreach (var kvp in instanceBatches)
        {
            FoliageType foliageType = kvp.Key;
            List<List<Matrix4x4>> batches = kvp.Value;

            foreach (var batch in batches)
            {
                if (batch.Count == 0) continue;

                // Simple distance culling
                if (!IsAnyInstanceVisible(batch, mainCam)) continue;

                Graphics.DrawMeshInstanced(
                    foliageType.mesh,
                    0,
                    foliageType.material,
                    batch,
                    null,
                    UnityEngine.Rendering.ShadowCastingMode.Off,
                    false
                );
            }
        }
    }

    bool IsAnyInstanceVisible(List<Matrix4x4> batch, Camera cam)
    {
        Vector3 camPos = cam.transform.position;
        
        foreach (var matrix in batch)
        {
            Vector3 instancePos = matrix.GetColumn(3);
            if (Vector3.Distance(camPos, instancePos) <= cullingDistance)
            {
                return true;
            }
        }
        
        return false;
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(spawnAreaSize.x, 1, spawnAreaSize.y));
    }

    [ContextMenu("Respawn Foliage")]
    public void RespawnFoliage()
    {
        SpawnFoliage();
    }

    [ContextMenu("Clear Foliage")]
    public void ClearFoliage()
    {
        foreach (var obj in combinedMeshObjects)
        {
            if (obj != null)
            {
                if (Application.isPlaying)
                    Destroy(obj);
                else
                    DestroyImmediate(obj);
            }
        }
        combinedMeshObjects.Clear();
        instanceBatches.Clear();
    }

    void OnDestroy()
    {
        ClearFoliage();
    }
}