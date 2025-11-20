using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Object pooling for 100+ enemies without lag (WebGL optimized)
/// </summary>
public class EnemyPoolManager : MonoBehaviour
{
    public static EnemyPoolManager Instance { get; private set; }

    [Header("Pool Settings")]
    public GameObject batPrefab;
    public EnemyStats defaultBatStats;
    public int initialPoolSize = 50;
    public int maxPoolSize = 200;
    public Transform poolParent;
    
    private Queue<BatEnemy> availableEnemies = new Queue<BatEnemy>();
    private HashSet<BatEnemy> activeEnemies = new HashSet<BatEnemy>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            
            // Don't persist between scenes to avoid conflicts with GameManager
            // Remove if you want persistence across scenes
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        if (poolParent == null)
        {
            poolParent = new GameObject("Enemy Pool").transform;
            poolParent.SetParent(transform);
        }
        
        InitializePool();
    }

    private void InitializePool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewEnemy();
        }
        
        Debug.Log($"Enemy pool initialized with {initialPoolSize} bats");
    }

    private BatEnemy CreateNewEnemy()
    {
        GameObject obj = Instantiate(batPrefab, poolParent);
        obj.SetActive(false);
        
        BatEnemy bat = obj.GetComponent<BatEnemy>();
        if (bat == null)
        {
            bat = obj.AddComponent<BatEnemy>();
        }
        
        availableEnemies.Enqueue(bat);
        return bat;
    }

    public BatEnemy SpawnEnemy(Vector3 position, EnemyStats stats = null)
    {
        // Get from pool or create new
        BatEnemy bat;
        
        if (availableEnemies.Count > 0)
        {
            bat = availableEnemies.Dequeue();
        }
        else if (activeEnemies.Count < maxPoolSize)
        {
            bat = CreateNewEnemy();
            bat = availableEnemies.Dequeue(); // Dequeue the newly created enemy from the queue
        }
        else
        {
            Debug.LogWarning("Enemy pool at max capacity!");
            return null;
        }
        
        // Setup enemy
        bat.transform.position = position;
        bat.transform.rotation = Quaternion.identity;
        bat.gameObject.SetActive(true);
        bat.Initialize(stats ?? defaultBatStats);
        bat.ResetForPooling();
        
        activeEnemies.Add(bat);
        
        Debug.Log($"Spawned bat at {position}. Active: {activeEnemies.Count}, Available: {availableEnemies.Count}");
        
        return bat;
    }

    public void ReturnToPool(BatEnemy bat)
    {
        if (!activeEnemies.Contains(bat))
            return;
        
        activeEnemies.Remove(bat);
        bat.gameObject.SetActive(false);
        bat.transform.SetParent(poolParent);
        availableEnemies.Enqueue(bat);
    }

    public void ClearAllEnemies()
    {
        List<BatEnemy> toReturn = new List<BatEnemy>(activeEnemies);
        foreach (var bat in toReturn)
        {
            ReturnToPool(bat);
        }
    }

    public int GetActiveCount() => activeEnemies.Count;
    public int GetAvailableCount() => availableEnemies.Count;
    
    // Integration with your GameManager
    void OnDestroy()
    {
        ClearAllEnemies();
    }
}