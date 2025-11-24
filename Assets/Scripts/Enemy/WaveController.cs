using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Vampire Survivors style wave controller with multiple enemy types
/// Modified for skating game - spawns enemies in front of fast-moving player
/// </summary>
public class WaveController : MonoBehaviour
{
    [System.Serializable]
    public class EnemySpawnInfo
    {
        public string enemyPoolName;  // "Bat", "Skull", etc.
        public int count;
        public EnemyStats statsOverride; // Optional - uses pool default if null
        [Range(0f, 1f)] public float spawnWeight = 1f; // For random distribution
    }

    [System.Serializable]
    public class WaveConfig
    {
        public int waveNumber;
        public List<EnemySpawnInfo> enemyTypes = new List<EnemySpawnInfo>();
        public float spawnInterval = 2f;
        public float spawnRadius = 15f;
        
        // Helper to get total enemy count
        public int GetTotalCount()
        {
            int total = 0;
            foreach (var enemy in enemyTypes)
                total += enemy.count;
            return total;
        }
    }

    [Header("Wave Configuration")]
    public List<WaveConfig> predefinedWaves = new List<WaveConfig>();
    public bool useProceduralWaves = true;
    
    [Header("Procedural Scaling")]
    public int baseEnemyCount = 5;
    public float enemyCountMultiplier = 1.3f;
    public float minSpawnInterval = 0.2f;
    public float baseSpawnInterval = 2f;
    
    [Header("Procedural Enemy Mix")]
    public List<EnemySpawnInfo> proceduralEnemyTypes = new List<EnemySpawnInfo>();
    
    [Header("Spawn Area - Skating Game")]
    public Transform spawnCenter; // Usually the player
    public float spawnAheadDistance = 20f;
    public float spawnAheadDistanceMax = 30f;
    public float spawnLateralSpread = 8f;
    public float spawnHeightAboveGround = 3f;
    public float velocityPredictionMultiplier = 1.5f;
    public LayerMask groundCheck;
    public float maxGroundCheckDistance = 50f;
    
    [Header("Current Wave")]
    public int currentWave = 0;
    public bool waveActive = false;
    public int enemiesSpawned = 0;
    public int enemiesAlive = 0;
    public int enemiesKilled = 0;
    
    private Coroutine activeWaveCoroutine;
    private Rigidbody playerRb;
    
    void Start()
    {
        // Auto-find player if not set
        if (spawnCenter == null && GameManager.Instance?.playerController != null)
        {
            spawnCenter = GameManager.Instance.playerController.transform;
        }

        // Cache player rigidbody for velocity prediction
        if (spawnCenter != null)
        {
            playerRb = spawnCenter.GetComponent<Rigidbody>();
            if (playerRb == null)
                playerRb = spawnCenter.GetComponentInParent<Rigidbody>();
        }
        
        // Subscribe to enemy death events
        EnemyManager.OnEnemyDeath += OnEnemyKilled;
    }

    void OnDestroy()
    {
        EnemyManager.OnEnemyDeath -= OnEnemyKilled;
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
        
        Debug.Log($"Starting Wave {waveNumber}: {config.GetTotalCount()} enemies of {config.enemyTypes.Count} type(s)");
        
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
        return GenerateProceduralWave(waveNumber);
    }

    private WaveConfig GenerateProceduralWave(int waveNumber)
    {
        WaveConfig config = new WaveConfig();
        config.waveNumber = waveNumber;
        config.spawnInterval = Mathf.Max(minSpawnInterval, baseSpawnInterval / Mathf.Sqrt(waveNumber));
        config.spawnRadius = spawnAheadDistance;
        
        // Calculate total enemies for this wave
        int totalEnemies = Mathf.RoundToInt(baseEnemyCount * Mathf.Pow(enemyCountMultiplier, waveNumber - 1));
        
        // Distribute enemies based on weights
        float totalWeight = 0f;
        foreach (var enemyType in proceduralEnemyTypes)
            totalWeight += enemyType.spawnWeight;
        
        if (totalWeight <= 0f)
        {
            Debug.LogError("No procedural enemy types configured!");
            return config;
        }
        
        // Create spawn info for each enemy type
        foreach (var template in proceduralEnemyTypes)
        {
            EnemySpawnInfo spawnInfo = new EnemySpawnInfo();
            spawnInfo.enemyPoolName = template.enemyPoolName;
            spawnInfo.statsOverride = template.statsOverride;
            spawnInfo.spawnWeight = template.spawnWeight;
            
            // Calculate count based on weight
            float ratio = template.spawnWeight / totalWeight;
            spawnInfo.count = Mathf.RoundToInt(totalEnemies * ratio);
            
            // Ensure at least 1 of each type if weight > 0
            if (spawnInfo.count == 0 && template.spawnWeight > 0)
                spawnInfo.count = 1;
            
            config.enemyTypes.Add(spawnInfo);
        }
        
        return config;
    }

    private IEnumerator SpawnWaveCoroutine(WaveConfig config)
    {
        // Create a list of all enemies to spawn with their types
        List<EnemySpawnInfo> spawnQueue = new List<EnemySpawnInfo>();
        
        foreach (var enemyType in config.enemyTypes)
        {
            for (int i = 0; i < enemyType.count; i++)
            {
                spawnQueue.Add(enemyType);
            }
        }
        
        // Shuffle for variety
        ShuffleList(spawnQueue);
        
        // Spawn enemies
        foreach (var spawnInfo in spawnQueue)
        {
            if (!waveActive)
                yield break;
            
            SpawnSingleEnemy(spawnInfo, config);
            enemiesSpawned++;
            
            yield return new WaitForSeconds(config.spawnInterval);
        }
        
        Debug.Log($"Wave {currentWave}: All {config.GetTotalCount()} enemies spawned!");
        
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

    // Fisher-Yates shuffle
    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    // ============================================================
    // SPAWNING - AHEAD OF PLAYER
    // ============================================================

    private void SpawnSingleEnemy(EnemySpawnInfo spawnInfo, WaveConfig config)
    {
        Vector3 spawnPos = GetRandomSpawnPositionAhead();
        
        IEnemy enemy = EnemyPoolManager.Instance?.SpawnEnemy(
            spawnInfo.enemyPoolName, 
            spawnPos, 
            spawnInfo.statsOverride
        );
        
        if (enemy != null)
        {
            enemiesAlive++;
        }
        else
        {
            Debug.LogWarning($"Failed to spawn enemy: {spawnInfo.enemyPoolName}");
        }
    }

    private Vector3 GetRandomSpawnPositionAhead()
    {
        if (spawnCenter == null)
        {
            Debug.LogWarning("No spawn center set!");
            return Vector3.up * spawnHeightAboveGround;
        }

        // Predict where player will be based on velocity
        Vector3 predictedPlayerPos = spawnCenter.position;
        if (playerRb != null && playerRb.velocity.sqrMagnitude > 0.1f)
        {
            predictedPlayerPos += playerRb.velocity * velocityPredictionMultiplier;
        }

        // Player moves on world right (Vector3.right)
        // Spawn enemies AHEAD of predicted position
        float aheadDistance = Random.Range(spawnAheadDistance, spawnAheadDistanceMax);
        Vector3 aheadOffset = Vector3.right * aheadDistance;

        // Add lateral spread (perpendicular to movement direction - world forward)
        float lateralOffset = Random.Range(-spawnLateralSpread, spawnLateralSpread);
        Vector3 lateralVector = Vector3.forward * lateralOffset;

        // Combine for XZ position (will adjust Y based on terrain)
        Vector3 spawnPosXZ = predictedPlayerPos + aheadOffset + lateralVector;
        
        // Raycast from high above to find actual ground height
        Vector3 spawnPos = spawnPosXZ + Vector3.up * 20f; // Start high
        RaycastHit hit;
        
        if (Physics.Raycast(spawnPos, Vector3.down, out hit, maxGroundCheckDistance, groundCheck))
        {
            // Found ground - spawn above it
            spawnPos = hit.point + Vector3.up * spawnHeightAboveGround;
        }
        else
        {
            // No ground found - use predicted player height as fallback
            spawnPos = spawnPosXZ;
            spawnPos.y = predictedPlayerPos.y + spawnHeightAboveGround;
            Debug.LogWarning($"No ground found at spawn position {spawnPosXZ}, using player height");
        }
        
        return spawnPos;
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnCenter == null) return;

        // Predict player position
        Vector3 predictedPos = spawnCenter.position;
        if (Application.isPlaying && playerRb != null && playerRb.velocity.sqrMagnitude > 0.1f)
        {
            predictedPos += playerRb.velocity * velocityPredictionMultiplier;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(predictedPos, 0.5f);

        // Draw spawn zone ahead of player
        Gizmos.color = Color.cyan;
        
        // Sample terrain at multiple points to visualize spawn area
        int samples = 5;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)(samples - 1);
            float ahead = Mathf.Lerp(spawnAheadDistance, spawnAheadDistanceMax, t);
            
            for (int j = -2; j <= 2; j++)
            {
                float lateral = (j / 2f) * spawnLateralSpread;
                Vector3 testPos = predictedPos + Vector3.right * ahead + Vector3.forward * lateral;
                
                // Raycast to find terrain height
                RaycastHit hit;
                if (Physics.Raycast(testPos + Vector3.up * 20f, Vector3.down, out hit, maxGroundCheckDistance, groundCheck))
                {
                    Vector3 spawnPoint = hit.point + Vector3.up * spawnHeightAboveGround;
                    Gizmos.DrawWireSphere(spawnPoint, 0.3f);
                    
                    // Draw line from ground to spawn point
                    Gizmos.color = new Color(0, 1, 0, 0.3f);
                    Gizmos.DrawLine(hit.point, spawnPoint);
                    Gizmos.color = Color.cyan;
                }
                else
                {
                    // No ground found - draw red sphere
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(testPos, 0.2f);
                    Gizmos.color = Color.cyan;
                }
            }
        }
        
        // Draw center line showing player movement direction
        Gizmos.color = Color.red;
        Gizmos.DrawLine(predictedPos, predictedPos + Vector3.right * spawnAheadDistanceMax);
    }

    // ============================================================
    // EVENTS
    // ============================================================

    private void OnEnemyKilled(IEnemy enemy)
    {
        enemiesKilled++;
        enemiesAlive--;
        
        // Award score through GameManager
        if (GameManager.Instance?.scoreSystem != null && enemy.stats != null)
        {
            GameManager.Instance.scoreSystem.AddScore(enemy.stats.killScore);
        }
        
        Debug.Log($"Wave {currentWave}: {enemiesKilled}/{enemiesSpawned} killed, {enemiesAlive} alive");
    }

    private void OnWaveComplete()
    {
        waveActive = false;
        
        Debug.Log($"✅ Wave {currentWave} Complete! Killed {enemiesKilled} enemies");
        
        // Notify GameManager or UI
        if (GameManager.Instance != null)
        {
            // Award bonus coins for wave completion
            GameManager.Instance.AddCoins(currentWave * 10);
        }
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