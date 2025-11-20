using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// EndlessGameUI - Manages endless mode UI elements
/// Displays distance, difficulty, score, and health during gameplay
/// Shows game over screen with final stats
/// </summary>
public class EndlessGameUI : MonoBehaviour
{
    [Header("Gameplay HUD")]
    public Text distanceText;
    public Text scoreText;
    public Text difficultyText;
    public Text healthText;
    public Text waveText;
    public Image healthBar;

    [Header("Game Over Screen")]
    public GameObject gameOverPanel;
    public Text gameOverReasonText;
    public Text finalDistanceText;
    public Text finalScoreText;
    public Text finalCoinsText;
    public Text finalTimeText;
    public Button retryButton;
    public Button menuButton;

    [Header("References")]
    public EndlessGameManager endlessGameManager;
    public UIManager uiManager;
    public PlayerController playerController;
    public HealthSystem healthSystem;

    private float gameStartTime = 0f;
    private bool gameOverShown = false;

    void Start()
    {
        FindReferences();
        RegisterListeners();
        HideGameOverPanel();
    }

    void Update()
    {
        if (endlessGameManager.currentState == EndlessGameManager.EndlessGameState.Playing)
        {
            UpdateHUD();
        }
    }

    // ============================================================
    // HUD UPDATES
    // ============================================================

    private void UpdateHUD()
    {
        // Distance
        if (distanceText != null)
        {
            distanceText.text = $"Distance: {endlessGameManager.progressionDistance:F0}m";
        }

        // Score
        if (scoreText != null)
        {
            int currentScore = endlessGameManager.scoreSystem != null ? 
                endlessGameManager.scoreSystem.GetTotalRunScore() : 0;
            scoreText.text = $"Score: {currentScore}";
        }

        // Difficulty Multiplier
        if (difficultyText != null)
        {
            difficultyText.text = $"Difficulty: {endlessGameManager.currentDifficultyMultiplier:F2}x";
        }

        // Health
        if (healthSystem != null)
        {
            if (healthText != null)
            {
                float currentHealth = healthSystem.GetCurrentHealth();
                healthText.text = $"Health: {currentHealth:F0}/{healthSystem.maxHealth:F0}";
            }

            if (healthBar != null)
            {
                float healthPercent = healthSystem.GetCurrentHealth() / healthSystem.maxHealth;
                healthBar.fillAmount = Mathf.Clamp01(healthPercent);
            }
        }
    }

    // ============================================================
    // GAME OVER SCREEN
    // ============================================================

    public void ShowGameOver(string reason, float distance, int score, int coins, float runTime)
    {
        if (gameOverShown) return;
        gameOverShown = true;

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (gameOverReasonText != null)
            gameOverReasonText.text = reason;

        if (finalDistanceText != null)
            finalDistanceText.text = $"Distance: {distance:F1}m";

        if (finalScoreText != null)
            finalScoreText.text = $"Score: {score}";

        if (finalCoinsText != null)
            finalCoinsText.text = $"Coins: {coins}";

        if (finalTimeText != null)
            finalTimeText.text = $"Time: {runTime:F0}s";

        // Setup button listeners
        if (retryButton != null)
        {
            retryButton.onClick.AddListener(OnRetryPressed);
        }

        if (menuButton != null)
        {
            menuButton.onClick.AddListener(OnMenuPressed);
        }
    }

    private void HideGameOverPanel()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
        gameOverShown = false;
    }

    // ============================================================
    // BUTTON HANDLERS
    // ============================================================

    private void OnRetryPressed()
    {
        Time.timeScale = 1f;
        endlessGameManager.StartEndlessGame();
        HideGameOverPanel();
    }

    private void OnMenuPressed()
    {
        Time.timeScale = 1f;
        endlessGameManager.ReturnToMenu();
    }

    // ============================================================
    // EVENT LISTENERS
    // ============================================================

    private void RegisterListeners()
    {
        if (endlessGameManager != null)
        {
            endlessGameManager.OnProgressionUpdate += OnProgressionUpdate;
            endlessGameManager.OnGameStateChanged += OnGameStateChanged;
        }
    }

    private void OnProgressionUpdate(float distance)
    {
        // Called when distance changes
        // Already handled in UpdateHUD
    }

    private void OnGameStateChanged(EndlessGameManager.EndlessGameState newState)
    {
        if (newState == EndlessGameManager.EndlessGameState.GameOver)
        {
            // Game over should already be called by EndlessGameManager.EndGame()
        }
    }

    // ============================================================
    // UTILITIES
    // ============================================================

    private void FindReferences()
    {
        if (endlessGameManager == null)
            endlessGameManager = FindObjectOfType<EndlessGameManager>();
        if (playerController == null)
            playerController = FindObjectOfType<PlayerController>();
        if (healthSystem == null)
            healthSystem = FindObjectOfType<HealthSystem>();
        if (uiManager == null)
            uiManager = UIManager.Instance;
    }

    void OnDestroy()
    {
        if (endlessGameManager != null)
        {
            endlessGameManager.OnProgressionUpdate -= OnProgressionUpdate;
            endlessGameManager.OnGameStateChanged -= OnGameStateChanged;
        }
    }
}
