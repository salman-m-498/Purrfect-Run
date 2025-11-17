using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Vampire Survivors style wave controller with increasing difficulty
/// Integrates with your existing GameManager round system
/// </summary>
public class WaveController : MonoBehaviour
{
    [System.Serializable]
    public class WaveConfig
    {
        public int waveNumber;
        public int enemyCount;
        public float spawnInterval;
        public float spawnRadius = 15f;
        public EnemyStats enemyStats;
    }

    [Header("Wave Configuration")]
    public List<WaveConfig> predefinedWaves = new List<WaveConfig>();
    public bool useProceduralWaves = true;
    
    [Header("Procedural Scaling")]
    public int baseEnemyCount = 5;
    public float enemyCountMultiplier = 1.3f;
    public float minSpawnInterval = 0.2f;
    public float baseSpawnInterval = 2f;
    
    [Header("Spawn Area")]
    public Transform spawnCenter; // Usually the player
    public float spawnDistance = 15f;
    public float spawnHeight = 5f;
    public LayerMask groundCheck;
    [Tooltip("If true, enemies will spawn in an arc in front of the spawn center instead of all around")]
    public bool spawnInFrontOnly = true;
    [Tooltip("Arc angle (degrees) used when spawning in front only; centered on spawnCenter.forward")]
    [Range(10f, 180f)]
    public float spawnArcAngle = 90f;
    [Tooltip("Minimum spawn distance from the spawn center when spawning in front")]
    public float spawnMinDistance = 5f;
    
    [Header("Current Wave")]
    public int currentWave = 0;
    public bool waveActive = false;
    public int enemiesSpawned = 0;
    public int enemiesAlive = 0;
    public int enemiesKilled = 0;
    
    private Coroutine activeWaveCoroutine;
    
    void Start()
    {
        // Auto-find player if not set
        if (spawnCenter == null && GameManager.Instance?.playerController != null)
        {
            spawnCenter = GameManager.Instance.playerController.transform;
        }
        
        // Subscribe to bat death events
        BatEnemy.OnBatDeath += OnEnemyKilled;
        
        // Start first wave automatically or wait for GameManager signal
        // StartWave(1);
    }

    void OnDestroy()
    {
        BatEnemy.OnBatDeath -= OnEnemyKilled;
    }

    // ============================================================
    // WAVE CONTROL
    // ============================================================

    public void StartWave(int waveNumber)
    {
        if (waveActive)
        {
            Debug.LogWarning("Wave already active!");
            return;
        }
        
        currentWave = waveNumber;
        enemiesSpawned = 0;
        enemiesKilled = 0;
        waveActive = true;
        
        WaveConfig config = GetWaveConfig(waveNumber);
        
        Debug.Log($"Starting Wave {waveNumber}: {config.enemyCount} enemies");
        
        if (activeWaveCoroutine != null)
            StopCoroutine(activeWaveCoroutine);
        
        activeWaveCoroutine = StartCoroutine(SpawnWaveCoroutine(config));
    }

    public void StopWave()
    {
        waveActive = false;
        
        if (activeWaveCoroutine != null)
        {
            StopCoroutine(activeWaveCoroutine);
            activeWaveCoroutine = null;
        }
        
        // Clear remaining enemies
        EnemyPoolManager.Instance?.ClearAllEnemies();
    }

    private WaveConfig GetWaveConfig(int waveNumber)
    {
        // Try to find predefined wave
        WaveConfig predefined = predefinedWaves.Find(w => w.waveNumber == waveNumber);
        if (predefined != null && !useProceduralWaves)
            return predefined;
        
        // Generate procedural wave
        WaveConfig config = new WaveConfig();
        config.waveNumber = waveNumber;
        config.enemyCount = Mathf.RoundToInt(baseEnemyCount * Mathf.Pow(enemyCountMultiplier, waveNumber - 1));
        config.spawnInterval = Mathf.Max(minSpawnInterval, baseSpawnInterval / Mathf.Sqrt(waveNumber));
        config.spawnRadius = spawnDistance;
        config.enemyStats = EnemyPoolManager.Instance?.defaultBatStats;
        
        return config;
    }

    private IEnumerator SpawnWaveCoroutine(WaveConfig config)
    {
        for (int i = 0; i < config.enemyCount; i++)
        {
            if (!waveActive)
                yield break;
            
            SpawnSingleEnemy(config);
            enemiesSpawned++;
            
            yield return new WaitForSeconds(config.spawnInterval);
        }
        
        Debug.Log($"Wave {currentWave}: All {config.enemyCount} enemies spawned!");
        
        // Wait for all enemies to be killed
        while (enemiesAlive > 0 && waveActive)
        {
            yield return new WaitForSeconds(0.5f);
        }
        
        if (waveActive)
        {
            OnWaveComplete();
        }
    }

    // ============================================================
    // SPAWNING
    // ============================================================

    private void SpawnSingleEnemy(WaveConfig config)
    {
        Vector3 spawnPos = GetRandomSpawnPosition(config.spawnRadius);
        
        BatEnemy bat = EnemyPoolManager.Instance?.SpawnEnemy(spawnPos, config.enemyStats);
        
        if (bat != null)
        {
            enemiesAlive++;
        }
    }

    private Vector3 GetRandomSpawnPosition(float radius)
    {
        if (spawnCenter == null)
        {
            Debug.LogWarning("No spawn center set!");
            return Vector3.up * spawnHeight;
        }
        
        Vector3 spawnPos = Vector3.up * spawnHeight;

        // If configured to spawn in front only, pick a point inside a forward-facing arc
        if (spawnInFrontOnly)
        {
            // Choose a random angle within the arc (centered on forward)
            float halfArc = spawnArcAngle * 0.5f;
            float angle = Random.Range(-halfArc, halfArc);

            // Choose a random distance between min and radius
            float dist = Random.Range(Mathf.Min(spawnMinDistance, radius), radius);

            // Direction rotated by angle around Y
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * spawnCenter.forward;
            spawnPos = spawnCenter.position + dir.normalized * dist + Vector3.up * spawnHeight;
        }
        else
        {
            // Random position all around player
            Vector2 randomCircle = Random.insideUnitCircle.normalized * radius;
            spawnPos = spawnCenter.position + new Vector3(randomCircle.x, spawnHeight, randomCircle.y);
        }
        
        // Optional: Adjust to terrain height
        RaycastHit hit;
        if (Physics.Raycast(spawnPos + Vector3.up * 10f, Vector3.down, out hit, 50f, groundCheck))
        {
            spawnPos.y = hit.point.y + spawnHeight;
        }
        
        return spawnPos;
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnCenter == null) return;

        Gizmos.color = Color.cyan;

        // Draw a small sphere at the spawn center
        Gizmos.DrawWireSphere(spawnCenter.position, 0.25f);

        if (spawnInFrontOnly)
        {
            // Draw forward arc
            int steps = 32;
            float halfArc = spawnArcAngle * 0.5f;
            Vector3 origin = spawnCenter.position + Vector3.up * spawnHeight;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float angle = Mathf.Lerp(-halfArc, halfArc, t);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * spawnCenter.forward;
                Vector3 point = origin + dir.normalized * spawnDistance;
                Gizmos.DrawSphere(point, 0.15f);
            }

            // Draw min distance arc if applicable
            if (spawnMinDistance > 0f && spawnMinDistance < spawnDistance)
            {
                Gizmos.color = new Color(0f, 1f, 1f, 0.5f);
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    float angle = Mathf.Lerp(-halfArc, halfArc, t);
                    Vector3 dir = Quaternion.Euler(0f, angle, 0f) * spawnCenter.forward;
                    Vector3 point = origin + dir.normalized * spawnMinDistance;
                    Gizmos.DrawSphere(point, 0.08f);
                }
            }
        }
        else
        {
            // Draw full circle where enemies can spawn
            int steps = 36;
            Vector3 origin = spawnCenter.position + Vector3.up * spawnHeight;
            for (int i = 0; i <= steps; i++)
            {
                float angle = (360f / steps) * i;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 point = origin + dir.normalized * spawnDistance;
                Gizmos.DrawSphere(point, 0.12f);
            }
        }
    }

    // ============================================================
    // EVENTS
    // ============================================================

    private void OnEnemyKilled(BatEnemy bat)
    {
        enemiesKilled++;
        enemiesAlive--;
        
        // Award score through GameManager
        if (GameManager.Instance?.scoreSystem != null && bat.stats != null)
        {
            GameManager.Instance.scoreSystem.AddScore(bat.stats.killScore);
        }
        
        Debug.Log($"Wave {currentWave}: {enemiesKilled}/{enemiesSpawned} killed, {enemiesAlive} alive");
    }

    private void OnWaveComplete()
    {
        waveActive = false;
        
        Debug.Log($"âœ… Wave {currentWave} Complete! Killed {enemiesKilled} enemies");
        
        // Notify GameManager or UI
        if (GameManager.Instance != null)
        {
            // Award bonus coins for wave completion
            GameManager.Instance.AddCoins(currentWave * 10);
        }
        
        // Auto-start next wave after delay (optional)
        // StartCoroutine(StartNextWaveAfterDelay(5f));
    }

    private IEnumerator StartNextWaveAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartWave(currentWave + 1);
    }

    // ============================================================
    // PUBLIC API
    // ============================================================

    public void StartNextWave()
    {
        StartWave(currentWave + 1);
    }

    public void RestartWave()
    {
        StopWave();
        StartWave(currentWave);
    }

    public float GetWaveProgress()
    {
        if (enemiesSpawned == 0) return 0f;
        return (float)enemiesKilled / enemiesSpawned;
    }

    // Integration with your GameManager level system
    public void OnLevelStart(int round, int level)
    {
        // Map your round/level to wave number
        int waveNumber = (round - 1) * 3 + level;
        StartWave(waveNumber);
    }

    public void OnLevelEnd()
    {
        StopWave();
    }
}