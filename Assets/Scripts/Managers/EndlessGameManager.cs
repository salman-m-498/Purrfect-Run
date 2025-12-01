using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;


/// <summary>
/// EndlessGameManager - Manages truly endless gameplay with pause support
/// </summary>
public class EndlessGameManager : MonoBehaviour
{
    public static EndlessGameManager Instance { get; private set; }

    public enum EndlessGameState { Menu, Playing, GameOver }
    
    [SerializeField]
    public EndlessGameState currentState = EndlessGameState.Menu;

    [Header("Endless Level Generation")]
    public EndlessLevelGenerator levelGenerator;
    public float levelCheckDistance = 50f;
    public int sectionsToPregenerate = 3;

    [Header("Enemy Waves")]
    public WaveController waveController;
    public float waveStartDelay = 3f;
    public int baseWaveEnemyCount = 3;
    public float baseWaveInterval = 2f;
    public float waveScalingPerDistance = 0.05f;

    [Header("Difficulty Scaling")]
    public float progressionDistance = 0f;
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
    public float fallDeathHeight = -20f;
    public float outOfBoundsZDistance = 10f;

    private GameObject currentLevelParent;
    private List<GameObject> activeLevelSections = new List<GameObject>();
    private int waveNumber = 0;
    private Coroutine waveCoroutine;
    private bool gameActive = false;

    public System.Action<float> OnProgressionUpdate;
    public System.Action<float> OnDifficultyUpdate;
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
        // Don't update gameplay if paused
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
        {
            return;
        }

        if (currentState == EndlessGameState.Playing && !gameActive)
        {
            Debug.LogWarning("⚠️ State is Playing but gameActive is false!");
        }
        
        if (currentState != EndlessGameState.Playing) 
        {
            return;
        }

        // Handle ESC key for pause menu
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }

        if (waveController != null && uiManager != null)
        {
            int wave = waveController.GetCurrentWave();
            int left = waveController.GetEnemiesRemaining();
            uiManager.UpdateWaveHUD(wave, left);
        }

        CheckLossConditions();
        UpdateLevelGeneration();
        UpdateProgression();
    }

    // ============================================================
    // PAUSE FUNCTIONALITY
    // ============================================================

    public void TogglePause()
    {
        if (currentState != EndlessGameState.Playing)
        {
            return; // Can't pause if not playing
        }

        if (PauseManager.Instance == null)
        {
            Debug.LogError("PauseManager.Instance is null!");
            return;
        }

        if (PauseManager.Instance.IsPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        if (currentState != EndlessGameState.Playing) return;

        PauseManager.Instance?.Pause();
        UnlockCursor();
        
        if (uiManager != null)
        {
            uiManager.ShowPauseMenu();
        }

        Debug.Log("🎮 Game Paused");
    }

    public void ResumeGame()
    {
        PauseManager.Instance?.Resume();
        LockCursor();
        
        if (uiManager != null)
        {
            uiManager.HidePauseMenu();
        }

        Debug.Log("🎮 Game Resumed");
    }

    // ============================================================
    // GAME FLOW
    // ============================================================

    public void StartEndlessGame()
    {
        Debug.Log("🎮 StartEndlessGame() called!");
        ClearUIFocus();
        LockCursor();
        
        // CRITICAL: Reset Time.timeScale and clear any pause state
        Time.timeScale = 1f;
        if (PauseManager.Instance != null)
        {
            PauseManager.Instance.ForceResumeAll();
        }
        
        // Stop any previous music before starting new music
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopMusic();
        }
        
        gameActive = true;
        currentState = EndlessGameState.Playing;
        progressionDistance = 0f;
        currentDifficultyMultiplier = 1f;
        waveNumber = 0;

        if (EnemyPoolManager.Instance != null)
        {
            EnemyPoolManager.Instance.ClearAllEnemies();
        }
        
        EnemyManager.KillAll();

        if (levelGenerator != null && !GameObject.Find("GeneratedLevel"))
            levelGenerator.GenerateEndlessLevel();

        currentLevelParent = GameObject.Find("GeneratedLevel");
        Debug.Log($"🔍 Looking for GeneratedLevel... Found: {(currentLevelParent != null ? "✅ YES" : "❌ NO")}");
        
        if (currentLevelParent == null)
        {
            Debug.LogError("❌ GeneratedLevel not found in scene!");
            return;
        }

        levelGenerator.SyncCursorToRightmostPoint();

        activeLevelSections.Clear();
        LevelBuilder[] levelSegments = currentLevelParent.GetComponentsInChildren<LevelBuilder>();
        foreach (var builder in levelSegments)
        {
            activeLevelSections.Add(builder.gameObject);
        }
        
        Debug.Log($"🔍 Collected {activeLevelSections.Count} existing level segments from scene.");

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

        if (scoreSystem != null)
        {
            scoreSystem.ResetRunScore();
        }

        if (uiManager != null)
        {
            uiManager.ShowGameplayUI();
            uiManager.HidePauseMenu(); // Make sure pause menu is hidden at start
        }

        TutorialSystem tutorialSystem = FindObjectOfType<TutorialSystem>();
        if (tutorialSystem != null)
        {
            tutorialSystem.StartTutorial();
        }

        if (waveController != null)
        {
            waveController.StopWave();
            waveController.currentWave = 0;
            waveController.waveActive = false;
            waveController.enemiesSpawned = 0;
            waveController.enemiesAlive = 0;
            waveController.enemiesKilled = 0;
            
            Debug.Log("✅ WaveController reset for new run");
        }

        if (waveCoroutine != null)
            StopCoroutine(waveCoroutine);
        waveCoroutine = StartCoroutine(StartWavesAfterDelay(waveStartDelay));

        OnGameStateChanged?.Invoke(currentState);

        // Play gameplay music AFTER stopping any previous music
        if (AudioManager.Instance != null)
        {
            Debug.Log("🎵 StartEndlessGame: About to play Music_Gameplay");
            AudioManager.Instance.PlayMusic(SoundID.Music_Gameplay);
            Debug.Log("🎵 StartEndlessGame: Music_Gameplay play command sent");
        }
        else
        {
            Debug.LogError("🎵 StartEndlessGame: AudioManager.Instance is NULL!");
        }
        
        Debug.Log("🎮 StartEndlessGame() complete - waves will start soon");
    }

    public void EndGame(string reason)
    {
        Debug.Log($"🎮 EndGame() called! Reason: {reason}\nStack Trace: {System.Environment.StackTrace}");
        
        gameActive = false;
        currentState = EndlessGameState.GameOver;

        // Make sure pause menu is hidden
        if (uiManager != null)
        {
            uiManager.HidePauseMenu();
        }

        if (waveController != null)
            waveController.StopWave();

        int finalScore = scoreSystem != null ? scoreSystem.GetTotalRunScore() : 0;
        int coinsEarned = (int)(progressionDistance * 10);
        float timePlayed = Time.timeSinceLevelLoad;

        if (GameManager.Instance != null)
        {
            PlayerData playerData = GameManager.Instance.GetPlayerData();
            if (playerData != null)
            {
                playerData.UpdateHighScore(finalScore);
                playerData.AddLifetimeCoins(coinsEarned);
                playerData.totalRuns++;
                playerData.SaveToPlayerPrefs();
            }
        }

        // Stop current music before playing game over music
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopMusic();
        }

        Debug.Log($"GAME OVER - Reason: {reason}\nDistance: {progressionDistance:F1}m | Score: {finalScore}");
        
        bool defeat = reason.Contains("Fell") || reason.Contains("Health") || reason.Contains("track");
        
        if (AudioManager.Instance != null)
        {
            if (defeat)
                AudioManager.Instance.PlayMusic(SoundID.Music_GameOver);
            else
                AudioManager.Instance.PlayMusic(SoundID.Music_Gameplay);
        }

        Vector3 playerPos = playerController != null 
            ? playerController.transform.position 
            : Vector3.zero;
        
        if (CircularTransition.Instance != null)
        {
            StartCoroutine(CircularTransition.Instance.ZoomToBlack(playerPos, () =>
            {
                Time.timeScale = 0f;
                
                if (EndlessGameOverUI.Instance != null)
                {
                    EndlessGameOverUI.Instance.ShowGameOver(
                        reason,
                        progressionDistance,
                        finalScore,
                        coinsEarned,
                        timePlayed
                    );
                }
            }));
        }
        else
        {
            Time.timeScale = 0f;
            
            if (EndlessGameOverUI.Instance != null)
            {
                EndlessGameOverUI.Instance.ShowGameOver(
                    reason,
                    progressionDistance,
                    finalScore,
                    coinsEarned,
                    timePlayed
                );
            }
        }

        OnGameStateChanged?.Invoke(currentState);
    }

    // ============================================================
    // LOSS CONDITION CHECKS
    // ============================================================

    private void CheckLossConditions()
    {
        if (playerController == null) return;

        Vector3 playerPos = playerController.transform.position;

        if (playerPos.y < fallDeathHeight)
        {
            EndGame("Fell off the level!");
            return;
        }

        if (Mathf.Abs(playerPos.z) > outOfBoundsZDistance)
        {
            EndGame("Wandered off the track!");
            return;
        }

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
            return;
        }

        float playerX = playerController.transform.position.x;
        float generationProgressX = levelGenerator.GetCurrentGenerationX();

        if (playerX + levelCheckDistance > generationProgressX)
        {
            GenerateNewLevelSectionsUsingGenerator();
        }

        CleanupPassedSegments(playerX);
    }

    private void GenerateNewLevelSectionsUsingGenerator()
    {
        if (levelGenerator == null || currentLevelParent == null) return;
        
        float beforeProgress = levelGenerator.GetCurrentGenerationX();
        
        levelGenerator.GenerateAdditionalSections(sectionsToPregenerate);
        
        int previousCount = activeLevelSections.Count;
        activeLevelSections.Clear();
        
        LevelBuilder[] levelSegments = currentLevelParent.GetComponentsInChildren<LevelBuilder>();
        foreach (var builder in levelSegments)
        {
            activeLevelSections.Add(builder.gameObject);
        }
        
        float afterProgress = levelGenerator.GetCurrentGenerationX();
        float lowestLevelY = GetLowestLevelY();
        fallDeathHeight = lowestLevelY - 20f;
        
        Debug.Log($"Generated {sectionsToPregenerate} new sections. Total segments: {activeLevelSections.Count} (was {previousCount}). Progress: X={afterProgress:F1} (was {beforeProgress:F1})");
    }

    private void CleanupPassedSegments(float playerX)
    {
        if (levelGenerator == null) return;
        
        List<GameObject> destroyedSegments = levelGenerator.CleanupPassedSegments(playerX);
        
        if (destroyedSegments.Count > 0)
        {
            activeLevelSections.Clear();
            if (currentLevelParent != null)
            {
                LevelBuilder[] levelSegments = currentLevelParent.GetComponentsInChildren<LevelBuilder>();
                foreach (var builder in levelSegments)
                {
                    activeLevelSections.Add(builder.gameObject);
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

            if (scoreSystem != null)
            {
                scoreSystem.AddScore((int)(distanceTraveled * scorePerMeterTraveled));
            }
        }

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

        int enemyCount = (int)(baseWaveEnemyCount * currentDifficultyMultiplier);
        float spawnInterval = baseWaveInterval / currentDifficultyMultiplier;

        if (waveController.predefinedWaves.Count == 0)
        {
            WaveController.WaveConfig config = new WaveController.WaveConfig
            {
                waveNumber = waveNumber,
                spawnInterval = Mathf.Max(0.2f, spawnInterval),
                spawnRadius = 20f,
            };
            waveController.predefinedWaves.Add(config);
        }

        waveController.StartWave(waveNumber);

        Debug.Log($"Wave {waveNumber} started: {enemyCount} enemies (difficulty: {currentDifficultyMultiplier:F1}x)");

        StartCoroutine(ScheduleNextWave());
    }

    private IEnumerator ScheduleNextWave()
    {
        while (waveController.waveActive)
        {
            yield return new WaitForSeconds(0.5f);
        }

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
        if (PauseManager.Instance != null)
        {
            PauseManager.Instance.ForceResumeAll();
        }
        
        // Stop music before returning to menu
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopMusic();
        }
        
        currentState = EndlessGameState.Menu;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ============================================================
    // Cursor Functions (WebGL-safe)
    // ============================================================

    void LockCursor()
    {
    #if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL: do NOT hide or lock cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Force browser focus so keyboard works without holding mouse
        Application.ExternalEval("window.focus();");
    #else
        // Desktop: Confined is perfect for drag-based gameplay
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
    #endif
    }

    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    void ClearUIFocus()
    {
        EventSystem.current?.SetSelectedGameObject(null);
    }




    // ============================================================
    // DYNAMIC FALL-HEIGHT
    // ============================================================

    private float GetLowestLevelY()
    {
        float lowest = float.MaxValue;
        bool found = false;

        foreach (GameObject segGo in activeLevelSections)
        {
            if (segGo == null) continue;

            lowest = Mathf.Min(lowest, GetLowestYInGameObject(segGo));

            foreach (Transform child in segGo.transform)
            {
                lowest = Mathf.Min(lowest, GetLowestYInGameObject(child.gameObject));
            }
            found = true;
        }

        return found ? lowest : -100f;
    }

    private float GetLowestYInGameObject(GameObject go)
    {
        float minY = float.MaxValue;

        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Vector3[] verts = mf.sharedMesh.vertices;
            foreach (Vector3 v in verts)
                minY = Mathf.Min(minY, go.transform.TransformPoint(v).y);
        }

        MeshCollider mc = go.GetComponent<MeshCollider>();
        if (mc != null && mc.sharedMesh != null)
        {
            Vector3[] verts = mc.sharedMesh.vertices;
            foreach (Vector3 v in verts)
                minY = Mathf.Min(minY, go.transform.TransformPoint(v).y);
        }

        return minY;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (playerController == null) return;

        Vector3 playerPos = playerController.transform.position;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(
            new Vector3(playerPos.x - 50, fallDeathHeight, 0), 
            new Vector3(playerPos.x + 50, fallDeathHeight, 0)
        );

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(
            playerPos + Vector3.forward * outOfBoundsZDistance + Vector3.left * 20,
            playerPos + Vector3.forward * outOfBoundsZDistance + Vector3.right * 20
        );
        Gizmos.DrawLine(
            playerPos - Vector3.forward * outOfBoundsZDistance + Vector3.left * 20,
            playerPos - Vector3.forward * outOfBoundsZDistance + Vector3.right * 20
        );
        
        if (levelGenerator != null)
        {
            float generationX = levelGenerator.GetCurrentGenerationX();
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(
                new Vector3(generationX, playerPos.y - 10, 0),
                new Vector3(generationX, playerPos.y + 10, 0)
            );
            
            Gizmos.color = Color.green;
            float checkX = playerPos.x + levelCheckDistance;
            Gizmos.DrawLine(
                new Vector3(checkX, playerPos.y - 5, 0),
                new Vector3(checkX, playerPos.y + 5, 0)
            );
        }
    }
#endif
}