using UnityEngine;

/// <summary>
/// Simple enemy spawner - now supports multiple enemy types via pooling
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class SpawnEntry
    {
        public string enemyPoolName = "Bat";
        public EnemyStats statsOverride;
        [Range(0f, 1f)] public float spawnChance = 1f;
    }

    [Header("Spawn Configuration")]
    public SpawnEntry[] enemyTypes = new SpawnEntry[] 
    { 
        new SpawnEntry { enemyPoolName = "Bat", spawnChance = 0.7f },
        new SpawnEntry { enemyPoolName = "Skull", spawnChance = 0.3f }
    };
    
    [Header("Spawn Settings")]
    public float spawnInterval = 5f;
    public bool autoSpawn = true;
    public int maxEnemies = 10;
    
    [Header("Spawn Area")]
    public Vector3 spawnAreaSize = new Vector3(3f, 3f, 3f);
    public Vector3 spawnAreaOffset = Vector3.up * 2f;

    private float timer;

    void Update()
    {
        if (!autoSpawn) return;

        timer -= Time.deltaTime;

        if (timer <= 0)
        {
            TrySpawn();
            timer = spawnInterval;
        }
    }

    public void TrySpawn()
    {
        if (EnemyManager.Count >= maxEnemies)
            return;

        Vector3 pos = transform.position + spawnAreaOffset + new Vector3(
            Random.Range(-spawnAreaSize.x, spawnAreaSize.x),
            Random.Range(-spawnAreaSize.y, spawnAreaSize.y),
            Random.Range(-spawnAreaSize.z, spawnAreaSize.z)
        );

        SpawnRandomEnemy(pos);
    }

    private void SpawnRandomEnemy(Vector3 position)
    {
        if (enemyTypes.Length == 0)
        {
            Debug.LogWarning("No enemy types configured in spawner!");
            return;
        }

        // Calculate total spawn chance
        float totalChance = 0f;
        foreach (var entry in enemyTypes)
            totalChance += entry.spawnChance;

        // Random selection based on spawn chance
        float roll = Random.Range(0f, totalChance);
        float current = 0f;

        foreach (var entry in enemyTypes)
        {
            current += entry.spawnChance;
            if (roll <= current)
            {
                SpawnEnemy(entry, position);
                return;
            }
        }

        // Fallback to first enemy
        SpawnEnemy(enemyTypes[0], position);
    }

    private void SpawnEnemy(SpawnEntry entry, Vector3 position)
    {
        if (EnemyPoolManager.Instance == null)
        {
            Debug.LogError("EnemyPoolManager not found!");
            return;
        }

        IEnemy enemy = EnemyPoolManager.Instance.SpawnEnemy(
            entry.enemyPoolName, 
            position, 
            entry.statsOverride
        );

        if (enemy == null)
        {
            Debug.LogWarning($"Failed to spawn enemy: {entry.enemyPoolName}");
        }
    }

    // Manual spawn call for scripts
    public void SpawnOnce() => TrySpawn();

    // Spawn specific enemy type
    public void SpawnSpecific(string poolName)
    {
        if (EnemyManager.Count >= maxEnemies)
            return;

        Vector3 pos = transform.position + spawnAreaOffset + new Vector3(
            Random.Range(-spawnAreaSize.x, spawnAreaSize.x),
            Random.Range(-spawnAreaSize.y, spawnAreaSize.y),
            Random.Range(-spawnAreaSize.z, spawnAreaSize.z)
        );

        EnemyPoolManager.Instance?.SpawnEnemy(poolName, pos);
    }

    // Legacy support for old scripts
    public void SpawnBat()
    {
        SpawnSpecific("Bat");
    }

    public void SpawnSkull()
    {
        SpawnSpecific("Skull");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(spawnAreaOffset, spawnAreaSize * 2f);
    }
}