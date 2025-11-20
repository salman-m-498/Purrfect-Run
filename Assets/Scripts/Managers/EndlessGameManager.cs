using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// EndlessGameManager - Manages truly endless gameplay with:
/// - Infinite level generation with dynamic section culling
/// - Auto-spawning enemy waves that scale with progression
/// - Loss conditions: fall off level OR die to enemies
/// - Continuous difficulty scaling
/// </summary>
public class EndlessGameManager : MonoBehaviour
{
    public static EndlessGameManager Instance { get; private set; }

    public enum EndlessGameState { Menu, Playing, GameOver }
    
    [SerializeField]
    public EndlessGameState currentState = EndlessGameState.Menu;

    [Header("Endless Level Generation")]
    public EndlessLevelGenerator levelGenerator;
    public float levelCheckDistance = 50f; // How far ahead to spawn new sections
    public float levelCleanupDistance = 100f; // How far behind to destroy sections
    public int sectionsToPregenerate = 3; // Keep this many sections ahead generated

    [Header("Enemy Waves")]
    public WaveController waveController;
    public float waveStartDelay = 3f; // Delay before first wave starts
    public int baseWaveEnemyCount = 3; // Enemies in wave 1
    public float baseWaveInterval = 2f; // Time between waves
    public float waveScalingPerDistance = 0.05f; // How much difficulty scales per unit traveled

    [Header("Difficulty Scaling")]
    public float progressionDistance = 0f; // Total distance traveled
    public float currentDifficultyMultiplier = 1f;
    public float maxDifficultyMultiplier = 5f;

    [Header("Scoring")]
    public ScoreSystem scoreSystem;
    public int scorePerMeterTraveled = 10;
    public int scorePerEnemyKilled = 100;

    [Header("References")]
    public PlayerController playerController;
    public UIManager uiManager;
    public GameManager gameManager;
    public DollyCam cam;

    [Header("Loss Conditions")]
    public float fallDeathHeight = -20f; // Y position below which player dies
    public float outOfBoundsZDistance = 10f; // How far from Z=0 before falling off

    private GameObject currentLevelParent;
    private List<GameObject> activeLevelSections = new List<GameObject>();
    private float levelGenerationProgress = 0f;
    private int waveNumber = 0;
    private Coroutine waveCoroutine;
    private bool gameActive = false;

    // Events
    public System.Action<float> OnProgressionUpdate; // distance
    public System.Action<float> OnDifficultyUpdate; // multiplier
    public System.Action<EndlessGameState> OnGameStateChanged;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        FindReferences();
    }

    void Update()
    {
        if (currentState != EndlessGameState.Playing) return;

        // Check loss conditions
        CheckLossConditions();

        // Update level generation
        UpdateLevelGeneration();

        // Update progression metrics
        UpdateProgression();
    }

    // ============================================================
    // GAME FLOW
    // ============================================================

    public void StartEndlessGame()
    {
        Debug.Log("🎮 Starting Endless Mode!");
        
        gameActive = true;
        currentState = EndlessGameState.Playing;
        progressionDistance = 0f;
        currentDifficultyMultiplier = 1f;
        waveNumber = 0;

        // Initialize level generator
        if (levelGenerator != null)
        {
            levelGenerator.randomSeed = Random.Range(0, 100000); // Random seed each run
            levelGenerator.GenerateEndlessLevel();
            levelGenerator.CreateLevelFromControlPoints();
            // Get generated level parent from the scene
            currentLevelParent = GameObject.Find("GeneratedLevel");
            if (currentLevelParent == null)
                currentLevelParent = new GameObject("GeneratedLevel");
        }

        // Initialize player
        if (playerController != null)
        {
            playerController.Initialize(gameManager ?? FindObjectOfType<GameManager>());
            playerController.ResetForNewLevel();
            playerController.transform.position = new Vector3(-204.723953f, 22.3436966f, 0); // Start of level
        }

        // Initialize score system
        if (scoreSystem != null)
        {
            scoreSystem.ResetRunScore();
        }

        // Start UI
        if (uiManager != null)
        {
            uiManager.ShowGameplayUI();
        }

        // Start waves after delay
        if (waveCoroutine != null)
            StopCoroutine(waveCoroutine);
        waveCoroutine = StartCoroutine(StartWavesAfterDelay(waveStartDelay));

        OnGameStateChanged?.Invoke(currentState);
    }

    public void EndGame(string reason)
    {
        gameActive = false;
        currentState = EndlessGameState.GameOver;

        // Stop waves
        if (waveController != null)
            waveController.StopWave();

        // Freeze gameplay
        Time.timeScale = 0f;

        // Show game over UI
        if (uiManager != null)
        {
            GameOverData data = new GameOverData
            {
                victory = false,
                roundsCompleted = 0,
                totalScore = scoreSystem != null ? scoreSystem.GetTotalRunScore() : 0,
                coinsEarned = (int)(progressionDistance * 10), // Coins based on distance
                runTime = Time.timeSinceLevelLoad
            };
            uiManager.ShowGameOver(data);
        }

        Debug.Log($"💀 GAME OVER - Reason: {reason}\nDistance: {progressionDistance:F1}m | Score: {scoreSystem?.GetTotalRunScore() ?? 0}");

        OnGameStateChanged?.Invoke(currentState);
    }

    // ============================================================
    // LOSS CONDITION CHECKS
    // ============================================================

    private void CheckLossConditions()
    {
        if (playerController == null) return;

        Vector3 playerPos = playerController.transform.position;

        // Loss 1: Fall off the level (too far below)
        if (playerPos.y < fallDeathHeight)
        {
            EndGame("Fell off the level!");
            return;
        }

        // Loss 2: Wander off the Z=0 plane
        if (Mathf.Abs(playerPos.z) > outOfBoundsZDistance)
        {
            EndGame("Wandered off the track!");
            return;
        }

        // Loss 3: Health depleted (if using health system)
        if (gameManager?.healthSystem != null && gameManager.healthSystem.GetCurrentHealth() <= 0)
        {
            EndGame("Health depleted!");
            return;
        }
    }

    // ============================================================
    // LEVEL GENERATION & STREAMING
    // ============================================================

    private void UpdateLevelGeneration()
    {
        if (playerController == null || levelGenerator == null) return;

        float playerX = playerController.transform.position.x;

        // Generate new sections if needed
        if (levelGenerationProgress < playerX + (sectionSpacing * sectionsToPregenerate))
        {
            GenerateNewLevelSection();
        }

        // Cleanup old sections
        for (int i = activeLevelSections.Count - 1; i >= 0; i--)
        {
            if (activeLevelSections[i] == null)
            {
                activeLevelSections.RemoveAt(i);
                continue;
            }

            float sectionX = activeLevelSections[i].transform.position.x;
            if (sectionX < playerX - levelCleanupDistance)
            {
                Debug.Log($"🧹 Destroying level section at X={sectionX:F1}");
                Destroy(activeLevelSections[i]);
                activeLevelSections.RemoveAt(i);
            }
        }
    }

    private void GenerateNewLevelSection()
    {
        Debug.Log($"📍 Generating new level section (Current: {levelGenerationProgress:F1})");
        
        // Create a new spline section
        int sectionIndex = activeLevelSections.Count;
        float currentX = levelGenerationProgress;
        float currentY = GetHeightAtX(currentX);

        // Randomly determine section type
        float rand = Random.value;
        float endX = currentX + sectionSpacing;
        float endY = currentY;
        string sectionType = "";

        if (rand < 0.4f)
        {
            sectionType = "FLAT";
            endY = currentY;
        }
        else if (rand < 0.7f)
        {
            sectionType = "UP";
            endY = currentY + Random.Range(5f, 15f);
        }
        else
        {
            sectionType = "DOWN";
            endY = currentY - Random.Range(5f, 15f);
        }

        // Create spline section
        GameObject sectionGO = new GameObject($"LevelSection_{sectionIndex}");
        sectionGO.transform.SetParent(currentLevelParent.transform);

        SplineComponent spline = sectionGO.AddComponent<SplineComponent>();
        List<Vector3> points = new List<Vector3>
        {
            new Vector3(currentX, currentY, 0),
            new Vector3((currentX + endX) * 0.5f, (currentY + endY) * 0.5f, 0),
            new Vector3(endX, endY, 0)
        };
        spline.controlPoints = points;

        // Build level mesh/colliders
        LevelBuilder builder = sectionGO.AddComponent<LevelBuilder>();
        builder.sampleDistance = 1f;
        builder.simplifyTolerance = 0.1f;
        builder.alignToWorldRight = false;
        builder.width = levelGenerator.width;
        builder.bankFactor = levelGenerator.bankFactor;
        builder.colliderMode = LevelBuilder.ColliderMode.BoxSegments;
        builder.material = levelGenerator.levelMaterial;
        builder.physicsMaterial = levelGenerator.physicsMaterial;

        builder.Generate();

        activeLevelSections.Add(sectionGO);
        levelGenerationProgress = endX;

        Debug.Log($"✅ Section {sectionType}: X [{currentX:F1} → {endX:F1}] Y [{currentY:F1} → {endY:F1}]");
    }

    private float GetHeightAtX(float x)
    {
        // Get height from existing level or interpolate
        if (activeLevelSections.Count > 0)
        {
            // Use last section's endpoint as reference
            GameObject lastSection = activeLevelSections[activeLevelSections.Count - 1];
            SplineComponent spline = lastSection.GetComponent<SplineComponent>();
            if (spline != null && spline.controlPoints.Count > 0)
            {
                return spline.controlPoints[spline.controlPoints.Count - 1].y;
            }
        }

        return 20f; // Default starting height
    }

    private float sectionSpacing => levelGenerator.sectionSpacing;

    // ============================================================
    // PROGRESSION & DIFFICULTY
    // ============================================================

    private void UpdateProgression()
    {
        if (playerController == null) return;

        float newDistance = playerController.transform.position.x;
        
        if (newDistance > progressionDistance)
        {
            float distanceTraveled = newDistance - progressionDistance;
            progressionDistance = newDistance;

            // Add distance-based score
            if (scoreSystem != null)
            {
                scoreSystem.AddScore((int)(distanceTraveled * scorePerMeterTraveled));
            }
        }

        // Update difficulty
        float newDifficulty = 1f + (progressionDistance * waveScalingPerDistance);
        currentDifficultyMultiplier = Mathf.Min(newDifficulty, maxDifficultyMultiplier);

        OnProgressionUpdate?.Invoke(progressionDistance);
        OnDifficultyUpdate?.Invoke(currentDifficultyMultiplier);
    }

    // ============================================================
    // WAVE MANAGEMENT
    // ============================================================

    private IEnumerator StartWavesAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartNextWave();
    }

    private void StartNextWave()
    {
        waveNumber++;

        if (waveController == null)
        {
            Debug.LogWarning("No WaveController found!");
            return;
        }

        // Scale enemies based on difficulty
        int enemyCount = (int)(baseWaveEnemyCount * currentDifficultyMultiplier);
        float spawnInterval = baseWaveInterval / currentDifficultyMultiplier;

        // Modify wave controller settings
        if (waveController.predefinedWaves.Count == 0)
        {
            // Create wave config on the fly
            WaveController.WaveConfig config = new WaveController.WaveConfig
            {
                waveNumber = waveNumber,
                enemyCount = enemyCount,
                spawnInterval = Mathf.Max(0.2f, spawnInterval),
                spawnRadius = 20f,
                enemyStats = null // Use default
            };
            waveController.predefinedWaves.Add(config);
        }

        // Start wave
        waveController.StartWave(waveNumber);

        Debug.Log($"🌊 Wave {waveNumber} started: {enemyCount} enemies (difficulty: {currentDifficultyMultiplier:F1}x)");

        // Schedule next wave after current one ends
        StartCoroutine(ScheduleNextWave());
    }

    private IEnumerator ScheduleNextWave()
    {
        // Wait for current wave to complete
        while (waveController.waveActive)
        {
            yield return new WaitForSeconds(0.5f);
        }

        // Wait before starting next wave
        yield return new WaitForSeconds(2f);

        if (gameActive)
        {
            StartNextWave();
        }
    }

    // ============================================================
    // UTILITIES
    // ============================================================

    private void FindReferences()
    {
        if (playerController == null)
            playerController = FindObjectOfType<PlayerController>();
        if (scoreSystem == null)
            scoreSystem = FindObjectOfType<ScoreSystem>();
        if (uiManager == null)
            uiManager = UIManager.Instance;
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();
        if (waveController == null)
            waveController = FindObjectOfType<WaveController>();
        if (levelGenerator == null)
            levelGenerator = FindObjectOfType<EndlessLevelGenerator>();
        if (cam == null)
            cam = FindObjectOfType<DollyCam>();
    }

    public float GetCurrentScore() => scoreSystem?.GetTotalRunScore() ?? 0;

    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        currentState = EndlessGameState.Menu;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ============================================================
    // DEBUG
    // ============================================================

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (playerController == null) return;

        Vector3 playerPos = playerController.transform.position;

        // Draw fall threshold
        Gizmos.color = Color.red;
        Gizmos.DrawLine(playerPos - Vector3.right * 50, playerPos + Vector3.right * 50);
        for (int i = 0; i < 10; i++)
        {
            Gizmos.DrawLine(
                playerPos + Vector3.right * (i * 10 - 45) - Vector3.up * 2,
                playerPos + Vector3.right * (i * 10 - 45)
            );
        }

        // Draw Z bounds
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(playerPos - Vector3.forward * outOfBoundsZDistance, playerPos + Vector3.forward * outOfBoundsZDistance);
    }
#endif
}
