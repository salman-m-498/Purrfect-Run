using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public enum GameState
{
    MainMenu,
    Playing,
    Paused,
    LevelComplete,
    LevelFailed,
    Store,
    SkillCheck,
    Endless,
    GameOver
}

public enum LevelType
{
    Normal,
    SkillCheck
}

/// <summary>
/// GameManager: The brain of the game - controls rounds, levels, win/loss states, and global data.
/// Delegates specific responsibilities to specialized systems.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Core System References")]
    public PlayerController playerController;
    public LevelManager levelManager;
    public ScoreSystem scoreSystem;
    public StaminaSystem staminaSystem;
    public HealthSystem healthSystem;
    public StoreManager storeManager;
    public UIManager uiManager;
    public SkillCheckSystem skillCheckSystem;
    public ItemSystem itemSystem;
    public DollyCam cam;

    [Header("Game State")]
    public GameState currentState = GameState.MainMenu;
    private GameState stateBeforePause;

    [Header("Run Progression")]
    public int currentRound = 1;          // Current round (each round = 3 levels)
    public int currentLevelInRound = 1;   // 1, 2, or 3 (3rd is always skill check)
    public int totalLevelsCompleted = 0;  // Total across all rounds
    public int maxRoundsPerRun = 10;      // How many rounds before victory
    
    [Header("Global Player Data")]
    public PlayerData playerData;         // Persistent run data
    
    [Header("Run Statistics")]
    public float runStartTime;
    public float totalRunTime;
    public int highestRoundReached = 0;
    public int totalCoinsEarnedThisRun = 0;

    [Header("Level Configuration")]
    public int baseScoreRequirement = 5000;
    public float scoreMultiplierPerLevel = 1.2f;
    public float baseLevelDuration = 60f;

    [Header("Enemy System")]
    public WaveController waveController;

    // Events
    public System.Action<GameState> OnGameStateChanged;
    public System.Action<int, int> OnRoundChanged;     // (round, level)
    public System.Action<bool> OnLevelEnded;           // (success)
    public System.Action OnRunEnded;
    public System.Action<int> OnCoinsChanged;

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeSystems();
    }

    void InitializeSystems()
    {
        // Initialize PlayerData
        if (playerData == null)
            playerData = new PlayerData();
        
        // Find system references if not assigned
        if (playerController == null)
            playerController = FindObjectOfType<PlayerController>();
        if (levelManager == null)
            levelManager = FindObjectOfType<LevelManager>();
        if (scoreSystem == null)
            scoreSystem = FindObjectOfType<ScoreSystem>();
        if (staminaSystem == null)
            staminaSystem = FindObjectOfType<StaminaSystem>();
            if (healthSystem == null)
            healthSystem = FindObjectOfType<HealthSystem>();
        if (storeManager == null)
            storeManager = FindObjectOfType<StoreManager>();
        if (uiManager == null)
            uiManager = FindObjectOfType<UIManager>();
        if (skillCheckSystem == null)
            skillCheckSystem = FindObjectOfType<SkillCheckSystem>();
        if (itemSystem == null)
            itemSystem = FindObjectOfType<ItemSystem>();
        if (cam == null)
            cam = FindObjectOfType<DollyCam>();

        // Initialize subsystems
        if (playerController != null)
            playerController.Initialize(this);
        
        // Load persistent data
        playerData.LoadFromPlayerPrefs();
        
        Debug.Log("GameManager: All systems initialized");
    }

    void Start()
    {
        ChangeGameState(GameState.MainMenu);
    }

    void Update()
    {
        // Handle pause input
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (currentState == GameState.Playing)
                PauseGame();
            else if (currentState == GameState.Paused)
                ResumeGame();
        }

        // Update run time
        if (currentState == GameState.Playing)
        {
            totalRunTime = Time.time - runStartTime;
        }
    }

    // ==================== GAME STATE MANAGEMENT ====================

    public void ChangeGameState(GameState newState)
    {
        GameState previousState = currentState;
        currentState = newState;

        // Exit previous state
        OnExitState(previousState);

        // Enter new state
        OnEnterState(newState);

        OnGameStateChanged?.Invoke(newState);
        Debug.Log($"GameManager: State changed {previousState} → {newState}");
    }

    private void OnExitState(GameState state)
    {
        switch (state)
        {
            case GameState.Playing:
                Time.timeScale = 1f;
                break;
        }
    }

    private void OnEnterState(GameState state)
    {
        switch (state)
        {
            case GameState.MainMenu:
                ShowMainMenu();
                break;
            case GameState.Playing:
                StartLevel();
                break;
            case GameState.Paused:
                PauseLevel();
                break;
            case GameState.LevelComplete:
                HandleLevelComplete();
                break;
            case GameState.LevelFailed:
                HandleLevelFailed();
                break;
            case GameState.Store:
                OpenStore();
                break;
            case GameState.SkillCheck:
                StartSkillCheck();
                break;
            case GameState.Endless:
                // Endless mode is handled by EndlessGameManager
                Debug.Log("GameManager: Entered Endless Mode state");
                break;
            case GameState.GameOver:
                HandleGameOver();
                break;
        }
    }

    // ==================== MAIN MENU ====================

    private void ShowMainMenu()
    {
        Time.timeScale = 1f;
        if (uiManager != null)
            uiManager.ShowMainMenu();
    }

    public void OnPlayButtonPressed()
    {
        StartNewRun();
    }

    public void OnQuitButtonPressed()
    {
        Application.Quit();
    }

    // ==================== SCORE SYSTEM INTEGRATION ====================

    // Add this helper method to GameManager to get current score
    public int GetCurrentScore()
    {
        return scoreSystem != null ? scoreSystem.GetCurrentScore() : 0;
    }

    // Update the CheckLevelCompletion method to use proper score tracking
    private void CheckLevelCompletion()
    {
        bool passed = false;

        // Check completion based on level type
        if (currentLevelInRound == 3)
        {
            // Skill Check level
            passed = skillCheckSystem != null && skillCheckSystem.IsChallengePassed();
        }
        else
        {
            // Normal level - check score requirement - FIXED: Use GetCurrentScore()
            int currentScore = GetCurrentScore();
            int required = CalculateScoreRequirement();
            passed = currentScore >= required;
            
            Debug.Log($"Level Check - Score: {currentScore}/{required} - Passed: {passed}");
        }

        if (passed)
        {
            ChangeGameState(GameState.LevelComplete);
        }
        else
        {
            ChangeGameState(GameState.LevelFailed);
        }
    }

    // ==================== RUN MANAGEMENT ====================

    public void StartNewRun()
    {
        // Reset run data
        currentRound = 1;
        currentLevelInRound = 1;
        totalLevelsCompleted = 0;
        totalCoinsEarnedThisRun = 0;
        runStartTime = Time.time;
        totalRunTime = 0f;

        // Reset player run data (not persistent data)
        playerData.ResetRunData();

        // Reset systems - FIXED: Reset run score instead of level score
        if (scoreSystem != null)
        {
            scoreSystem.ResetRunScore();  // NEW: Reset entire run score
        }
        if (staminaSystem != null)
            staminaSystem.ResetStamina();
        if (itemSystem != null)
            itemSystem.ClearActiveItems();

        Debug.Log("GameManager: Starting new run");
        
        // Start first level
        ChangeGameState(GameState.Playing);
    }

    /// <summary>
    /// Starts endless mode - infinite levels with auto-scaling difficulty
    /// </summary>
    public void StartEndlessMode()
    {
        // Reset run data
        currentRound = 1;
        currentLevelInRound = 1;
        totalLevelsCompleted = 0;
        totalCoinsEarnedThisRun = 0;
        runStartTime = Time.time;
        totalRunTime = 0f;

        // Reset player run data
        playerData.ResetRunData();

        // Reset systems
        if (scoreSystem != null)
            scoreSystem.ResetRunScore();
        if (staminaSystem != null)
            staminaSystem.ResetStamina();
        if (itemSystem != null)
            itemSystem.ClearActiveItems();

        Debug.Log("GameManager: Starting Endless Mode");

        // Start endless mode
        ChangeGameState(GameState.Endless);

        // Delegate to EndlessGameManager
        EndlessGameManager endlessManager = FindObjectOfType<EndlessGameManager>();
        if (endlessManager != null)
        {
            endlessManager.StartEndlessGame();
        }
        else
        {
            Debug.LogWarning("EndlessGameManager not found in scene!");
        }
    }


    // ==================== LEVEL FLOW ====================

    private void StartLevel()
    {
        Time.timeScale = 1f;
        if (waveController != null) waveController.OnLevelStart(currentRound, currentLevelInRound);

        // Determine level type (use LevelManager's nested enum to match LevelManager.SetupLevel signature)
        LevelManager.LevelType levelType = (currentLevelInRound == 3) ? LevelManager.LevelType.SkillCheck : LevelManager.LevelType.Normal;

        // Calculate requirements for this level
        int requiredScore = CalculateScoreRequirement();
        float timeLimit = baseLevelDuration;

        // Tell LevelManager to setup the level
        if (levelManager != null)
        {
            levelManager.SetupLevel(levelType, requiredScore, timeLimit);
            levelManager.StartLevel();
        }

        // Reset player for new level
        if (playerController != null)
            playerController.ResetForNewLevel();

        // Reset score for THIS LEVEL (not entire run) - FIXED
        if (scoreSystem != null)
        {
            scoreSystem.ResetScore();  // This now properly tracks level vs run score
        }

        // Update UI
        if (uiManager != null)
        {
            uiManager.ShowGameplayUI();
            uiManager.UpdateRoundDisplay(currentRound, currentLevelInRound);
        }

        Debug.Log($"GameManager: Started Level - Round {currentRound}, Level {currentLevelInRound}/{3} ({levelType})");
    }


    private void StartSkillCheck()
    {
        if (skillCheckSystem != null)
        {
            skillCheckSystem.StartRandomChallenge();
        }
        
        // Skill checks still use normal level flow but with special win conditions
        ChangeGameState(GameState.Playing);
    }

    public void OnLevelTimerExpired()
    {
        // Level time ran out - check if player passed
        CheckLevelCompletion();
    }

    public void OnPlayerReachedFinish()
    {
        // Player reached finish line early
        CheckLevelCompletion();
    }

    public void OnLevelCompleted(bool success, int score, float time)
    {
        Debug.Log($"Level Completed: Success={success}, Score={score}, Time={time}");
    }

    private void HandleLevelComplete()
    {
        totalLevelsCompleted++;
        
        // IMPORTANT: Finalize the level score BEFORE getting it
        if (scoreSystem != null)
        {
            scoreSystem.FinalizeLevelScore(); // Add current level score to run total
        }
        
        // NOW get the current level score (which was just finalized)
        int currentScore = GetCurrentScore();
        
        // Award coins based on performance
        int coinsEarned = CalculateCoinsEarned();
        AddCoins(coinsEarned);

        // Update persistent stats
        if (currentRound > highestRoundReached)
            highestRoundReached = currentRound;

        // Save high scores - Use total run score for high score tracking
        int totalScore = scoreSystem != null ? scoreSystem.GetTotalRunScore() : 0;
        if (totalScore > playerData.highScore)
        {
            playerData.highScore = totalScore;
            playerData.SaveToPlayerPrefs();
        }

        OnLevelEnded?.Invoke(true);

        // Show results
        if (uiManager != null)
        {
            LevelResultData results = new LevelResultData
            {
                passed = true,
                score = currentScore,  // Show level score
                coinsEarned = coinsEarned,
                levelTime = levelManager != null ? levelManager.GetLevelTime() : 0f
            };
            uiManager.ShowLevelResults(results);
        }

        Debug.Log($"GameManager: Level Complete! Score: {currentScore}, Total Run Score: {totalScore}, Coins earned: {coinsEarned}");
    }


    private void HandleLevelFailed()
    {
        OnLevelEnded?.Invoke(false);

        // Get current level score - FIXED
        int currentScore = GetCurrentScore();

        // Show results
        if (uiManager != null)
        {
            LevelResultData results = new LevelResultData
            {
                passed = false,
                score = currentScore,
                requiredScore = CalculateScoreRequirement(),
                coinsEarned = 0,
                levelTime = levelManager != null ? levelManager.GetLevelTime() : 0f
            };
            uiManager.ShowLevelResults(results);
        }

        Debug.Log($"GameManager: Level Failed - Score: {currentScore}/{CalculateScoreRequirement()}");
    }

    // ==================== PROGRESSION FLOW ====================

    public void OnContinueAfterLevelComplete()
    {
        // Move to next level
        currentLevelInRound++;

        // Check if round is complete (3 levels done)
        if (currentLevelInRound > 3)
        {
            // Round complete - go to store
            currentRound++;
            currentLevelInRound = 1;
            OnRoundChanged?.Invoke(currentRound, currentLevelInRound);

            // Check if game is won
            if (currentRound > maxRoundsPerRun)
            {
                // Player beat the game!
                ChangeGameState(GameState.GameOver);
            }
            else
            {
                // Go to store between rounds
                ChangeGameState(GameState.Store);
            }
        }
        else
        {
            // Continue to next level in same round
            OnRoundChanged?.Invoke(currentRound, currentLevelInRound);
            ChangeGameState(GameState.Playing);
        }
    }

    public void OnRetryAfterLevelFailed()
    {
        // Retry current level
        ChangeGameState(GameState.Playing);
    }

    public void OnQuitToMenuAfterLevelFailed()
    {
        // End run and return to menu
        EndRun(false);
        ChangeGameState(GameState.MainMenu);
    }

    

    // ==================== STORE FLOW ====================

    private void OpenStore()
    {
        if (storeManager != null)
        {
            storeManager.OpenStore(playerData.coins);
        }

        if (uiManager != null)
        {
            uiManager.ShowStoreUI();
        }

        Debug.Log("GameManager: Store opened");
    }

    public void OnItemPurchased(string itemId, int cost)
    {
        // Deduct coins
        AddCoins(-cost);

        // Apply item
        if (itemSystem != null)
        {
            itemSystem.ApplyItem(itemId);
        }

        // Add to owned items for this run
        playerData.AddItemToRun(itemId);

        Debug.Log($"GameManager: Purchased {itemId} for {cost} coins");
    }

    public void OnStoreExit()
    {
        // Return to next level
        ChangeGameState(GameState.Playing);
    }

    // ==================== PAUSE/RESUME ====================

    public void PauseGame()
    {
        if (currentState == GameState.Playing)
        {
            stateBeforePause = currentState;
            ChangeGameState(GameState.Paused);
        }
    }

    public void ResumeGame()
    {
        if (currentState == GameState.Paused)
        {
            ChangeGameState(stateBeforePause);
        }
    }

    private void PauseLevel()
    {
        Time.timeScale = 0f;
        if (uiManager != null)
            uiManager.ShowPauseMenu();
    }

    public void OnResumeButtonPressed()
    {
        ResumeGame();
    }

    public void OnRestartLevelFromPause()
    {
        ResumeGame();
        ChangeGameState(GameState.Playing);
    }

    public void OnQuitToMenuFromPause()
    {
        ResumeGame();
        EndRun(false);
        ChangeGameState(GameState.MainMenu);
    }

    // ==================== GAME OVER ====================

    private void HandleGameOver()
    {
        bool victory = currentRound > maxRoundsPerRun;
        EndRun(victory);

        if (uiManager != null)
        {
            GameOverData data = new GameOverData
            {
                victory = victory,
                roundsCompleted = currentRound - 1,
                totalScore = scoreSystem != null ? scoreSystem.GetTotalRunScore() : 0,
                coinsEarned = totalCoinsEarnedThisRun,
                runTime = totalRunTime
            };
            uiManager.ShowGameOver(data);
        }

        OnRunEnded?.Invoke();
        Debug.Log($"GameManager: Game Over - Victory: {victory}");
    }

    private void EndRun(bool victory)
    {
        // Update persistent stats
        playerData.totalRuns++;
        if (victory)
            playerData.totalVictories++;
        
        playerData.totalPlayTime += totalRunTime;
        playerData.SaveToPlayerPrefs();

        Debug.Log("GameManager: Run ended");
    }

    // ==================== COIN MANAGEMENT ====================

    public void AddCoins(int amount)
    {
        playerData.coins += amount;
        totalCoinsEarnedThisRun += Mathf.Max(0, amount); // Only count positive additions
        OnCoinsChanged?.Invoke(playerData.coins);

        if (uiManager != null)
            uiManager.UpdateCoinDisplay(playerData.coins);
    }

    private int CalculateCoinsEarned()
    {
        int score = scoreSystem != null ? scoreSystem.GetCurrentScore() : 0;
        
        // Base coins from score
        int baseCoins = score / 100;
        
        // Bonus from performance metrics
        int bonusCoins = 0;
        if (scoreSystem != null)
        {
            bonusCoins += scoreSystem.GetTotalCombos() * 10;
            bonusCoins += scoreSystem.GetPerfectLandings() * 5;
        }

        return baseCoins + bonusCoins;
    }

    // ==================== DIFFICULTY SCALING ====================

    private int CalculateScoreRequirement()
    {
        // Score requirement increases each level
        int totalLevels = (currentRound - 1) * 3 + currentLevelInRound;
        return Mathf.RoundToInt(baseScoreRequirement * Mathf.Pow(scoreMultiplierPerLevel, totalLevels - 1));
    }

    // ==================== PUBLIC GETTERS ====================

    public int GetCurrentRound() => currentRound;
    public int GetCurrentLevelInRound() => currentLevelInRound;
    public int GetTotalCoins() => playerData.coins;
    public PlayerData GetPlayerData() => playerData;
    public bool IsSkillCheckLevel() => currentLevelInRound == 3;

    // ==================== CLEANUP ====================

    void OnApplicationQuit()
    {
        playerData.SaveToPlayerPrefs();
    }
}

// ==================== DATA STRUCTURES ====================

[System.Serializable]
public class LevelResultData
{
    public bool passed;
    public int score;
    public int requiredScore;
    public int coinsEarned;
    public float levelTime;
}

[System.Serializable]
public class GameOverData
{
    public bool victory;
    public int roundsCompleted;
    public int totalScore;
    public int coinsEarned;
    public float runTime;
}