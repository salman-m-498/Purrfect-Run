using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

/// <summary>
/// Displays game over screen for endless mode with run statistics
/// </summary>
public class EndlessGameOverUI : MonoBehaviour
{
    public static EndlessGameOverUI Instance { get; private set; }

    [Header("UI Panels")]
    public GameObject gameOverPanel;
    public CanvasGroup panelCanvasGroup;
    
    [Header("Title")]
    public TMP_Text titleText;
    public string[] deathMessages = new string[]
    {
        "WIPEOUT!",
        "BAILED!",
        "SLAMMED!",
        "STACKED!",
        "YARD SALE!"
    };
    
    [Header("Statistics")]
    public TMP_Text distanceText;
    public TMP_Text scoreText;
    public TMP_Text highScoreText;
    public TMP_Text coinsEarnedText;
    public TMP_Text timePlayedText;
    public TMP_Text causeOfDeathText;
    
    [Header("Special Stats (Optional)")]
    public TMP_Text tricksPerformedText;
    public TMP_Text biggestComboText;
    public TMP_Text perfectLandingsText;
    
    [Header("Buttons")]
    public Button playAgainButton;
    public Button mainMenuButton;
    
    [Header("Scene Management")]
    public string playAgainSceneName = "EndlessMode"; // Scene to reload for play again
    public string mainMenuSceneName = "MainMenu"; // Scene to load for main menu
    public bool useTransitionForPlayAgain = true; // Optional: disable transition for instant reload
    
    [Header("Animation")]
    public float statDelay = 0.3f; // Delay between each stat appearing
    public float countUpDuration = 1f; // Duration for number count-up animation
    public AnimationCurve countUpCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Audio (Optional)")]
    public AudioSource sfxSource;
    public AudioClip statPopSound;
    public AudioClip newHighScoreSound;
    
    private EndlessGameManager gameManager;
    private bool isNewHighScore = false;

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
        gameManager = FindObjectOfType<EndlessGameManager>();
        
        // Hide panel initially
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
        
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
        }
        
        // Setup button listeners
        if (playAgainButton != null)
        {
            playAgainButton.onClick.AddListener(OnPlayAgainClicked);
        }
        
        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }
    }

    /// <summary>
    /// Show the game over screen with run statistics
    /// </summary>
    public void ShowGameOver(string causeOfDeath, float distance, int score, int coinsEarned, float timePlayed)
    {
        if (gameOverPanel == null)
        {
            Debug.LogError("Game Over Panel not assigned!");
            return;
        }
        
        // Check if this is a new high score
        PlayerData playerData = GameManager.Instance?.GetPlayerData();
        if (playerData != null)
        {
            isNewHighScore = score > playerData.highScore;
        }
        
        // Activate panel
        gameOverPanel.SetActive(true);
        
        // Start the reveal animation
        StartCoroutine(AnimateGameOverReveal(causeOfDeath, distance, score, coinsEarned, timePlayed));
    }

    private IEnumerator AnimateGameOverReveal(string causeOfDeath, float distance, int score, int coinsEarned, float timePlayed)
    {
        // Fade in panel
        if (panelCanvasGroup != null)
        {
            yield return StartCoroutine(FadeCanvasGroup(panelCanvasGroup, 0f, 1f, 0.5f));
        }
        
        // Show random death message title
        if (titleText != null)
        {
            string randomMessage = deathMessages[Random.Range(0, deathMessages.Length)];
            titleText.text = randomMessage;
            titleText.gameObject.SetActive(true);
            
            // Punch scale animation on title
            StartCoroutine(PunchScale(titleText.transform, 1.3f, 0.3f));
            PlaySound(statPopSound);
        }
        
        yield return new WaitForSecondsRealtime(0.5f);
        
        // Show cause of death
        if (causeOfDeathText != null)
        {
            causeOfDeathText.text = causeOfDeath;
            causeOfDeathText.gameObject.SetActive(true);
            StartCoroutine(PunchScale(causeOfDeathText.transform, 1.2f, 0.2f));
            PlaySound(statPopSound);
        }
        
        yield return new WaitForSecondsRealtime(statDelay);
        
        // Animate each stat appearing with count-up
        if (distanceText != null)
        {
            distanceText.gameObject.SetActive(true);
            yield return StartCoroutine(CountUpFloat(distanceText, 0f, distance, "Distance: {0:F1}m", countUpDuration));
            PlaySound(statPopSound);
            yield return new WaitForSecondsRealtime(statDelay);
        }
        
        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(true);
            yield return StartCoroutine(CountUpInt(scoreText, 0, score, "Score: {0:N0}", countUpDuration));
            PlaySound(statPopSound);
            yield return new WaitForSecondsRealtime(statDelay);
        }
        
        // Show high score with special effect if new record
        if (highScoreText != null)
        {
            PlayerData playerData = GameManager.Instance?.GetPlayerData();
            int highScore = playerData != null ? playerData.highScore : 0;
            
            highScoreText.gameObject.SetActive(true);
            
            if (isNewHighScore)
            {
                highScoreText.text = "🏆 NEW HIGH SCORE! 🏆";
                highScoreText.color = Color.yellow;
                StartCoroutine(PulseScale(highScoreText.transform, 1.2f, 0.5f));
                PlaySound(newHighScoreSound);
            }
            else
            {
                yield return StartCoroutine(CountUpInt(highScoreText, 0, highScore, "High Score: {0:N0}", countUpDuration * 0.5f));
                PlaySound(statPopSound);
            }
            
            yield return new WaitForSecondsRealtime(statDelay);
        }
        
        if (coinsEarnedText != null)
        {
            coinsEarnedText.gameObject.SetActive(true);
            yield return StartCoroutine(CountUpInt(coinsEarnedText, 0, coinsEarned, "Coins Earned: {0}", countUpDuration));
            PlaySound(statPopSound);
            yield return new WaitForSecondsRealtime(statDelay);
        }
        
        if (timePlayedText != null)
        {
            timePlayedText.gameObject.SetActive(true);
            timePlayedText.text = $"Time: {FormatTime(timePlayed)}";
            StartCoroutine(PunchScale(timePlayedText.transform, 1.1f, 0.2f));
            PlaySound(statPopSound);
            yield return new WaitForSecondsRealtime(statDelay);
        }
        
        // Optional: Show special stats if available
        if (gameManager != null && gameManager.scoreSystem != null)
        {
            if (tricksPerformedText != null)
            {
                int tricks = gameManager.scoreSystem.GetTricksPerformed();
                tricksPerformedText.gameObject.SetActive(true);
                yield return StartCoroutine(CountUpInt(tricksPerformedText, 0, tricks, "Tricks: {0}", countUpDuration * 0.5f));
                PlaySound(statPopSound);
                yield return new WaitForSecondsRealtime(statDelay * 0.5f);
            }
            
            if (biggestComboText != null)
            {
                int biggestCombo = gameManager.scoreSystem.GetBiggestCombo();
                biggestComboText.gameObject.SetActive(true);
                yield return StartCoroutine(CountUpInt(biggestComboText, 0, biggestCombo, "Best Combo: {0}x", countUpDuration * 0.5f));
                PlaySound(statPopSound);
                yield return new WaitForSecondsRealtime(statDelay * 0.5f);
            }
            
            if (perfectLandingsText != null)
            {
                int perfectLandings = gameManager.scoreSystem.GetPerfectLandings();
                perfectLandingsText.gameObject.SetActive(true);
                yield return StartCoroutine(CountUpInt(perfectLandingsText, 0, perfectLandings, "Perfect Landings: {0}", countUpDuration * 0.5f));
                PlaySound(statPopSound);
                yield return new WaitForSecondsRealtime(statDelay * 0.5f);
            }
        }
        
        // Show buttons
        if (playAgainButton != null)
        {
            playAgainButton.gameObject.SetActive(true);
            StartCoroutine(PunchScale(playAgainButton.transform, 1.2f, 0.3f));
        }
        
        if (mainMenuButton != null)
        {
            mainMenuButton.gameObject.SetActive(true);
            StartCoroutine(PunchScale(mainMenuButton.transform, 1.2f, 0.3f));
        }
    }

    // ============================================================
    // ANIMATION HELPERS
    // ============================================================

    private IEnumerator CountUpInt(TMP_Text text, int start, int end, string format, float duration)
    {
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = countUpCurve.Evaluate(elapsed / duration);
            int current = Mathf.RoundToInt(Mathf.Lerp(start, end, t));
            text.text = string.Format(format, current);
            yield return null;
        }
        
        text.text = string.Format(format, end);
    }

    private IEnumerator CountUpFloat(TMP_Text text, float start, float end, string format, float duration)
    {
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = countUpCurve.Evaluate(elapsed / duration);
            float current = Mathf.Lerp(start, end, t);
            text.text = string.Format(format, current);
            yield return null;
        }
        
        text.text = string.Format(format, end);
    }

    private IEnumerator PunchScale(Transform target, float punchAmount, float duration)
    {
        Vector3 originalScale = target.localScale;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            
            float scale;
            if (t < 0.5f)
            {
                scale = Mathf.Lerp(1f, punchAmount, t * 2f);
            }
            else
            {
                scale = Mathf.Lerp(punchAmount, 1f, (t - 0.5f) * 2f);
            }
            
            target.localScale = originalScale * scale;
            yield return null;
        }
        
        target.localScale = originalScale;
    }

    private IEnumerator PulseScale(Transform target, float pulseAmount, float duration)
    {
        Vector3 originalScale = target.localScale;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.PingPong(elapsed * 2f, 1f);
            float scale = Mathf.Lerp(1f, pulseAmount, t);
            target.localScale = originalScale * scale;
            yield return null;
        }
        
        target.localScale = originalScale;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            group.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        
        group.alpha = to;
    }

    private void PlaySound(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        return $"{minutes:00}:{seconds:00}";
    }

    // ============================================================
    // BUTTON HANDLERS
    // ============================================================

    private void OnPlayAgainClicked()
    {
        Debug.Log("Play Again clicked - Reloading scene: " + playAgainSceneName);
        
        // Reset time scale BEFORE any scene operations
        Time.timeScale = 1f;
        
        // Hide panel
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
        
        // Optional: Use transition effect
        if (useTransitionForPlayAgain && CircularTransition.Instance != null)
        {
            // Zoom from black then reload
            Vector3 centerPos = Camera.main != null 
                ? Camera.main.transform.position 
                : Vector3.zero;
            
            StartCoroutine(CircularTransition.Instance.ZoomFromBlack(centerPos, () =>
            {
                ReloadScene(playAgainSceneName);
            }));
        }
        else
        {
            // Instant reload
            ReloadScene(playAgainSceneName);
        }
    }

    private void OnMainMenuClicked()
    {
        Debug.Log("Main Menu clicked - Loading scene: " + mainMenuSceneName);
        
        // Reset time scale
        Time.timeScale = 1f;
        
        // Hide panel
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
        
        // Load main menu scene
        LoadScene(mainMenuSceneName);
    }

    private void ReloadScene(string sceneName)
    {
        // If no scene name provided, reload current scene
        if (string.IsNullOrEmpty(sceneName))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }
    }

    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("No scene name provided! Reloading current scene instead.");
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            );
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }
    }

    // ============================================================
    // PUBLIC API
    // ============================================================

    public void Hide()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }
}