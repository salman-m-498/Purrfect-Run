using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generic object pooling for multiple enemy types (WebGL optimized)
/// Now supports Bat, Skull, and any future IEnemy implementations
/// </summary>
public class EnemyPoolManager : MonoBehaviour
{
    public static EnemyPoolManager Instance { get; private set; }

    [System.Serializable]
    public class EnemyPool
    {
        public string poolName;
        public GameObject prefab;
        public EnemyStats defaultStats;
        public int initialSize = 30;
        public int maxSize = 100;
        
        [HideInInspector] public Queue<IEnemy> available = new Queue<IEnemy>();
        [HideInInspector] public HashSet<IEnemy> active = new HashSet<IEnemy>();
        [HideInInspector] public Transform poolParent;
    }

    [Header("Pool Settings")]
    public List<EnemyPool> enemyPools = new List<EnemyPool>();
    public Transform poolRootParent;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        if (poolRootParent == null)
        {
            poolRootParent = new GameObject("Enemy Pools").transform;
            poolRootParent.SetParent(transform);
        }
        
        InitializeAllPools();
    }

    private void InitializeAllPools()
    {
        foreach (var pool in enemyPools)
        {
            // Create parent for this enemy type
            pool.poolParent = new GameObject($"{pool.poolName} Pool").transform;
            pool.poolParent.SetParent(poolRootParent);
            
            // Pre-instantiate enemies
            for (int i = 0; i < pool.initialSize; i++)
            {
                CreateNewEnemy(pool);
            }
            
            Debug.Log($"Pool '{pool.poolName}' initialized with {pool.initialSize} enemies");
        }
    }

    private IEnemy CreateNewEnemy(EnemyPool pool)
    {
        GameObject obj = Instantiate(pool.prefab, pool.poolParent);
        obj.SetActive(false);
        
        IEnemy enemy = obj.GetComponent<IEnemy>();
        if (enemy == null)
        {
            Debug.LogError($"Prefab {pool.prefab.name} does not have IEnemy component!");
            Destroy(obj);
            return null;
        }
        
        pool.available.Enqueue(enemy);
        return enemy;
    }

    // ============================================================
    // SPAWN BY TYPE
    // ============================================================

    /// <summary>
    /// Spawn enemy by pool name (e.g. "Bat", "Skull")
    /// </summary>
    public IEnemy SpawnEnemy(string poolName, Vector3 position, EnemyStats statsOverride = null)
    {
        EnemyPool pool = enemyPools.Find(p => p.poolName == poolName);
        if (pool == null)
        {
            Debug.LogError($"No pool found with name '{poolName}'!");
            return null;
        }
        
        return SpawnFromPool(pool, position, statsOverride);
    }

    /// <summary>
    /// Spawn specific enemy type
    /// </summary>
    public T SpawnEnemy<T>(Vector3 position, EnemyStats statsOverride = null) where T : MonoBehaviour, IEnemy
    {
        // Find pool by component type
        EnemyPool pool = enemyPools.Find(p => p.prefab.GetComponent<T>() != null);
        if (pool == null)
        {
            Debug.LogError($"No pool found for enemy type {typeof(T).Name}!");
            return null;
        }
        
        return SpawnFromPool(pool, position, statsOverride) as T;
    }

    /// <summary>
    /// Spawn bat enemy (convenience method for legacy code)
    /// </summary>
    public BatEnemy SpawnBat(Vector3 position, EnemyStats statsOverride = null)
    {
        return SpawnEnemy<BatEnemy>(position, statsOverride);
    }

    /// <summary>
    /// Spawn skull enemy (convenience method)
    /// </summary>
    public SkullEnemy SpawnSkull(Vector3 position, EnemyStats statsOverride = null)
    {
        return SpawnEnemy<SkullEnemy>(position, statsOverride);
    }

    // ============================================================
    // CORE POOLING LOGIC
    // ============================================================

    private IEnemy SpawnFromPool(EnemyPool pool, Vector3 position, EnemyStats statsOverride)
    {
        IEnemy enemy;
        
        // Get from pool or create new
        if (pool.available.Count > 0)
        {
            enemy = pool.available.Dequeue();
        }
        else if (pool.active.Count < pool.maxSize)
        {
            enemy = CreateNewEnemy(pool);
            if (enemy == null) return null;
            enemy = pool.available.Dequeue();
        }
        else
        {
            Debug.LogWarning($"Pool '{pool.poolName}' at max capacity ({pool.maxSize})!");
            return null;
        }
        
        // Setup enemy
        enemy.transform.position = position;
        enemy.transform.rotation = Quaternion.identity;
        enemy.gameObject.SetActive(true);
        enemy.Initialize(statsOverride ?? pool.defaultStats);
        enemy.ResetForPooling();
        
        pool.active.Add(enemy);
        
        Debug.Log($"Spawned {pool.poolName} at {position}. Active: {pool.active.Count}, Available: {pool.available.Count}");
        
        return enemy;
    }

    public void ReturnToPool(IEnemy enemy)
    {
        if (enemy == null || enemy.gameObject == null) return;
        
        // Find which pool this enemy belongs to
        EnemyPool pool = FindPoolForEnemy(enemy);
        if (pool == null)
        {
            Debug.LogWarning($"Could not find pool for enemy {enemy.gameObject.name}");
            return;
        }
        
        if (!pool.active.Contains(enemy))
            return;
        
        pool.active.Remove(enemy);
        enemy.gameObject.SetActive(false);
        enemy.transform.SetParent(pool.poolParent);
        pool.available.Enqueue(enemy);
    }

    // Legacy method for BatEnemy - calls generic version
    public void ReturnToPool(BatEnemy bat)
    {
        ReturnToPool(bat as IEnemy);
    }

    // Method for SkullEnemy - calls generic version
    public void ReturnToPool(SkullEnemy skull)
    {
        ReturnToPool(skull as IEnemy);
    }

    private EnemyPool FindPoolForEnemy(IEnemy enemy)
    {
        foreach (var pool in enemyPools)
        {
            if (pool.active.Contains(enemy) || pool.available.Contains(enemy))
                return pool;
        }
        return null;
    }

    // ============================================================
    // MANAGEMENT
    // ============================================================

    public void ClearAllEnemies()
    {
        foreach (var pool in enemyPools)
        {
            List<IEnemy> toReturn = new List<IEnemy>(pool.active);
            foreach (var enemy in toReturn)
            {
                ReturnToPool(enemy);
            }
        }
    }

    public void ClearEnemiesByType(string poolName)
    {
        EnemyPool pool = enemyPools.Find(p => p.poolName == poolName);
        if (pool != null)
        {
            List<IEnemy> toReturn = new List<IEnemy>(pool.active);
            foreach (var enemy in toReturn)
            {
                ReturnToPool(enemy);
            }
        }
    }

    // ============================================================
    // STATS & QUERIES
    // ============================================================

    public int GetTotalActiveCount()
    {
        int total = 0;
        foreach (var pool in enemyPools)
            total += pool.active.Count;
        return total;
    }

    public int GetActiveCount(string poolName)
    {
        EnemyPool pool = enemyPools.Find(p => p.poolName == poolName);
        return pool?.active.Count ?? 0;
    }

    public int GetAvailableCount(string poolName)
    {
        EnemyPool pool = enemyPools.Find(p => p.poolName == poolName);
        return pool?.available.Count ?? 0;
    }

    public Dictionary<string, int> GetAllPoolStats()
    {
        Dictionary<string, int> stats = new Dictionary<string, int>();
        foreach (var pool in enemyPools)
        {
            stats[pool.poolName] = pool.active.Count;
        }
        return stats;
    }

    void OnDestroy()
    {
        ClearAllEnemies();
    }

#if UNITY_EDITOR
    // Editor debug info
    [ContextMenu("Print Pool Status")]
    private void PrintPoolStatus()
    {
        foreach (var pool in enemyPools)
        {
            Debug.Log($"Pool '{pool.poolName}': Active={pool.active.Count}, Available={pool.available.Count}, Max={pool.maxSize}");
        }
    }
#endif
}