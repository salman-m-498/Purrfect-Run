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
    public float levelCheckDistance = 50f; // How far ahead to check for new sections
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
        Debug.Log("🎮 EndlessGameManager.Start() called");
        FindReferences();
        
        if (levelGenerator == null)
            Debug.LogError("❌ levelGenerator is null after FindReferences()");
        if (playerController == null)
            Debug.LogError("❌ playerController is null after FindReferences()");
    }

    void Update()
    {
        // Debug state changes
        if (currentState == EndlessGameState.Playing && !gameActive)
        {
            Debug.LogWarning("⚠️ State is Playing but gameActive is false!");
        }
        
        if (currentState != EndlessGameState.Playing) 
        {
            return;
        }

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
        Debug.Log("🎮 StartEndlessGame() called!");
        
        gameActive = true;
        currentState = EndlessGameState.Playing;
        progressionDistance = 0f;
        currentDifficultyMultiplier = 1f;
        waveNumber = 0;

        // Make sure the very first level chunk exists
        if (levelGenerator != null && !GameObject.Find("GeneratedLevel"))
            levelGenerator.GenerateEndlessLevel();   // or GenerateAndCreate()

        // Get the already-generated level from the scene
        // The EndlessLevelGenerator should have already created "GeneratedLevel" parent
        currentLevelParent = GameObject.Find("GeneratedLevel");
        Debug.Log($"🔍 Looking for GeneratedLevel... Found: {(currentLevelParent != null ? "✅ YES" : "❌ NO")}");
        
        if (currentLevelParent == null)
        {
            Debug.LogError("❌ GeneratedLevel not found in scene! Make sure EndlessLevelGenerator has been run and level is in the scene.");
            return;
        }

        // guarantee cursor is in the right place before we ever ask for more
        levelGenerator.SyncCursorToRightmostPoint();

        // Collect all existing level segments (LevelSegment_0, LevelSegment_1, etc.)
        // These were created by EndlessLevelGenerator
        activeLevelSections.Clear();
        LevelBuilder[] levelSegments = currentLevelParent.GetComponentsInChildren<LevelBuilder>();
        foreach (var builder in levelSegments)
        {
            activeLevelSections.Add(builder.gameObject);
        }
        
        Debug.Log($"🔍 Collected {activeLevelSections.Count} existing level segments from scene.");

        // Initialize player
        if (playerController != null)
        {
            playerController.Initialize(gameManager ?? FindObjectOfType<GameManager>());
            playerController.ResetForNewLevel();
            Transform spawnT = GameObject.FindWithTag("PlayerSpawn")?.transform;
            if (spawnT != null)
            {
                PlayerSpawnSystem.SetStartPoint(spawnT);
                PlayerSpawnSystem.SpawnPlayer();
            }
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
        if (playerController == null || levelGenerator == null || currentLevelParent == null) 
        {
            if (playerController == null) Debug.LogWarning("❌ playerController is null");
            if (levelGenerator == null) Debug.LogWarning("❌ levelGenerator is null");
            if (currentLevelParent == null) Debug.LogWarning("❌ currentLevelParent is null");
            return;
        }

        float playerX = playerController.transform.position.x;
        
        // Get the current generation progress from the generator
        float generationProgressX = levelGenerator.GetCurrentGenerationX();

        // Generate new sections if player is getting close to the end of generated content
        if (playerX + levelCheckDistance > generationProgressX)
        {
            Debug.Log($"🏗️ Player at X={playerX:F1}, generation at X={generationProgressX:F1}. Generating ahead...");
            GenerateNewLevelSectionsUsingGenerator();
        }

        // Cleanup old sections that are far behind the player
        CleanupOldSections(playerX);
    }

    private void GenerateNewLevelSectionsUsingGenerator()
    {
        if (levelGenerator == null || currentLevelParent == null) return;
        
        float beforeProgress = levelGenerator.GetCurrentGenerationX();
        
        // Use incremental generation to add new sections without clearing
        levelGenerator.GenerateAdditionalSections(sectionsToPregenerate);
        
        // Refresh the list of active sections
        int previousCount = activeLevelSections.Count;
        activeLevelSections.Clear();
        
        LevelBuilder[] levelSegments = currentLevelParent.GetComponentsInChildren<LevelBuilder>();
        foreach (var builder in levelSegments)
        {
            activeLevelSections.Add(builder.gameObject);
        }
        
        float afterProgress = levelGenerator.GetCurrentGenerationX();
        
        Debug.Log($"✅ Generated {sectionsToPregenerate} new sections. Total segments: {activeLevelSections.Count} (was {previousCount}). Progress: X={afterProgress:F1} (was {beforeProgress:F1})");
    }

    private void CleanupOldSections(float playerX)
    {
        // Cleanup old sections that are far behind the player
        for (int i = activeLevelSections.Count - 1; i >= 0; i--)
        {
            if (activeLevelSections[i] == null)
            {
                activeLevelSections.RemoveAt(i);
                continue;
            }

            // Get the leftmost point of this section to determine its position
            SplineComponent spline = activeLevelSections[i].GetComponent<SplineComponent>();
            if (spline != null && spline.controlPoints.Count > 0)
            {
                float sectionStartX = spline.controlPoints[0].x;
                float sectionEndX = spline.controlPoints[spline.controlPoints.Count - 1].x;
                
                // Only destroy if the END of the section is behind the cleanup distance
                if (sectionEndX < playerX - levelCleanupDistance)
                {
                    Debug.Log($"🧹 Destroying level section (X={sectionStartX:F1} to {sectionEndX:F1}), player at X={playerX:F1}");
                    Destroy(activeLevelSections[i]);
                    activeLevelSections.RemoveAt(i);
                }
            }
        }
    }

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
        Gizmos.DrawLine(
            new Vector3(playerPos.x - 50, fallDeathHeight, 0), 
            new Vector3(playerPos.x + 50, fallDeathHeight, 0)
        );

        // Draw Z bounds
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            playerPos + Vector3.forward * outOfBoundsZDistance + Vector3.left * 20,
            playerPos + Vector3.forward * outOfBoundsZDistance + Vector3.right * 20
        );
        Gizmos.DrawLine(
            playerPos - Vector3.forward * outOfBoundsZDistance + Vector3.left * 20,
            playerPos - Vector3.forward * outOfBoundsZDistance + Vector3.right * 20
        );
        
        // Draw generation trigger distance
        if (levelGenerator != null)
        {
            float generationX = levelGenerator.GetCurrentGenerationX();
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(
                new Vector3(generationX, playerPos.y - 10, 0),
                new Vector3(generationX, playerPos.y + 10, 0)
            );
            
            // Draw check distance
            Gizmos.color = Color.green;
            float checkX = playerPos.x + levelCheckDistance;
            Gizmos.DrawLine(
                new Vector3(checkX, playerPos.y - 5, 0),
                new Vector3(checkX, playerPos.y + 5, 0)
            );
        }
        
        // Draw cleanup distance
        Gizmos.color = Color.red;
        float cleanupX = playerPos.x - levelCleanupDistance;
        Gizmos.DrawLine(
            new Vector3(cleanupX, playerPos.y - 5, 0),
            new Vector3(cleanupX, playerPos.y + 5, 0)
        );
    }
#endif
}