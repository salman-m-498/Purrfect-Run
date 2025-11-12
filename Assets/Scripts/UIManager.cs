using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// UIManager handles all player-facing UI in the game:
/// - Stamina bar
/// - Trick/Combo text spawning
/// - Score display (optional)
/// - Run stats (optional)
/// This class centralizes UI updates for easy access from gameplay scripts.
/// </summary>
public class UIManager : MonoBehaviour
{
    #region Singleton
    public static UIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    #endregion

    [Header("Stamina Bar")]
    public StaminaBarUI staminaBarPrefab;
    private StaminaBarUI staminaBarInstance;

    [Header("Combo Text")]
    public ComboTextSpawner comboTextSpawnerPrefab;
    private ComboTextSpawner comboTextSpawnerInstance;

    [Header("Optional UI Elements")]
    public TMP_Text scoreText;
    public TMP_Text roundText;

    [Header("Canvas Reference")]
    public Canvas uiCanvas;

    private void Start()
    {
        if (uiCanvas == null)
        {
            uiCanvas = FindObjectOfType<Canvas>();
            if (uiCanvas == null)
            {
                Debug.LogError("UIManager: No Canvas found in the scene. Please assign one.");
            }
        }

        // Instantiate stamina bar
        if (staminaBarPrefab != null)
        {
            staminaBarInstance = Instantiate(staminaBarPrefab, uiCanvas.transform);
            staminaBarInstance.name = "StaminaBar";
        }
        else
        {
            Debug.LogError("UIManager: StaminaBarPrefab not assigned!");
        }

        // Instantiate combo text spawner
        if (comboTextSpawnerPrefab != null)
        {
            GameObject spawnerObj = Instantiate(comboTextSpawnerPrefab.gameObject, uiCanvas.transform);
            comboTextSpawnerInstance = spawnerObj.GetComponent<ComboTextSpawner>();
            spawnerObj.name = "ComboTextSpawner";
        }
        else
        {
            Debug.LogError("UIManager: ComboTextSpawnerPrefab not assigned!");
        }
    }

    #region Stamina Bar API
    /// <summary>
    /// Updates the player's stamina bar.
    /// </summary>
    /// <param name="current">Current stamina value</param>
    /// <param name="max">Maximum stamina value</param>
    public void UpdateStamina(float current, float max)
    {
        if (staminaBarInstance != null)
        {
            staminaBarInstance.UpdateStamina(current, max);
        }
    }
    #endregion

    #region Combo Text API
    /// <summary>
    /// Spawns a combo or trick text at a world position.
    /// </summary>
    /// <param name="trickName">Name of the trick performed</param>
    /// <param name="worldPosition">World position where trick occurred</param>
    /// <param name="multiplier">Number of flips</param>
    /// <param name="comboMultiplier">Combo multiplier</param>
    public void SpawnTrickText(string trickName, Vector3 worldPosition, int multiplier = 1, float comboMultiplier = 1f)
    {
        if (comboTextSpawnerInstance != null)
        {
            comboTextSpawnerInstance.SpawnTrickText(trickName, worldPosition, multiplier, comboMultiplier);
        }
    }
    #endregion

    #region Score / Round UI
    /// <summary>
    /// Updates the score display text.
    /// </summary>
    public void UpdateScore(int score)
    {
        if (scoreText != null)
        {
            scoreText.text = $"Score: {score}";
        }
    }

    /// <summary>
    /// Updates the current round display text.
    /// </summary>
    public void UpdateRound(int roundNumber)
    {
        if (roundText != null)
        {
            roundText.text = $"Round {roundNumber}";
        }
    }
    #endregion

    #region Optional Run Stats (Post-Run UI)
    /// <summary>
    /// Display stats after a run (success or fail)
    /// </summary>
    public void DisplayRunStats(int roundsCompleted, int levelsCompleted, int highestScore, float timeElapsed)
    {
        // You can implement a panel showing these stats
        Debug.Log($"Run Stats: Rounds {roundsCompleted}, Levels {levelsCompleted}, High Score {highestScore}, Time {timeElapsed:F2}s");
    }

    internal void ShowLevelUI(LevelManager levelManager)
    {
        throw new NotImplementedException();
    }
    #endregion

    public void ShowSkillChallenge(object challenge, float target) { }
    public void ShowMainMenu() { }
    public void ShowGameplayUI() { }
    public void UpdateRoundDisplay(int round)
    {
        Debug.Log($"Updating Round Display: Round {round}");
    }

    // Optional overload if called 2 args
    public void UpdateRoundDisplay(int round, int totalRounds)
    {
        Debug.Log($"Updating Round Display: Round {round} / {totalRounds}");
    }

    public void ShowLevelResults(LevelResultData results)
    {
        Debug.Log($"Showing Level Results - Passed: {results.passed}, Score: {results.score}, Required: {results.requiredScore}, Coins: {results.coinsEarned}, Time: {results.levelTime}");
    }

    public void ShowStoreUI() { }
    public void ShowPauseMenu() { }
    public void ShowGameOver(GameOverData data) { }
    public void UpdateCoinDisplay(int coins) { }
    public void ShowLevelUI(LevelResultData level) { }
}
