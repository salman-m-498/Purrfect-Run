using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;

/// <summary>
/// Enhanced UIManager handles all player-facing UI and visual feedback:
/// - Stamina bar
/// - Trick/Combo text spawning (Juice)
/// - Score display (Animated)
/// - Combo HUD (Timer, Multiplier, Score)
/// - Special State effects (Mega Combo, On Fire)
/// This class subscribes to ScoreSystem events for responsive UI updates.
/// </summary>
public class UIManager : MonoBehaviour
{
    #region Singleton
    // Using UIManager as the name again for direct replacement
    public static UIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Debug.Log("UIManager: Instance set successfully");
    }
    #endregion

    [Header("=== UI REFERENCES ===")]
    public StaminaBarUI staminaBarPrefab;
    private StaminaBarUI staminaBarInstance;
    public ComboTextSpawner comboTextSpawnerPrefab;
    private ComboTextSpawner comboTextSpawnerInstance;
    public TrickText trickTextPrefab;
    public Canvas uiCanvas;

    [Header("=== SCORE & ROUND DISPLAY ===")]
    public TMP_Text scoreText; // Main score text
    public TMP_Text roundText;
    private int displayedScore = 0; // The score currently shown in the UI
    private Coroutine scoreAnimCoroutine;
    
    [Header("=== COMBO HUD ===")]
    public GameObject comboPanel;
    public TMP_Text comboCountText;
    public TMP_Text comboMultiplierText;
    public TMP_Text comboScoreText;
    public Image comboTimerBar;
    public GameObject comboGlowEffect;
    
    [Header("=== MULTIPLIER DISPLAY ===")]
    public TMP_Text multiplierText; // Used for static overall multiplier if needed, or redundancy
    public Image multiplierFillBar;
    
    [Header("=== STATS HUD ===")]
    public TMP_Text tricksCountText;
    public TMP_Text perfectLandingsText;
    public TMP_Text biggestComboText;
    
    [Header("=== SPECIAL EFFECTS ===")]
    public GameObject megaComboEffect;
    public GameObject onFireEffect;
    public AudioSource comboSound;
    public AudioSource megaComboSound;

    // References
    private ScoreSystem scoreSystem;

    private void Start()
    {
        InitializeUI();
        FindAndSubscribeToScoreSystem();
    }

    private void InitializeUI()
    {
        // Find canvas if not assigned
        if (uiCanvas == null)
        {
            uiCanvas = FindObjectOfType<Canvas>();
            if (uiCanvas == null)
            {
                Debug.LogError("UIManager: No Canvas found!");
                return;
            }
        }

        // Verify score text
        if (scoreText == null)
        {
            Debug.LogError("UIManager: scoreText NOT assigned! Please assign in Inspector.");
        }
        else
        {
            scoreText.text = "0";
            scoreText.ForceMeshUpdate();
        }

        // Initialize stamina bar (existing system)
        if (staminaBarPrefab != null)
        {
            staminaBarInstance = Instantiate(staminaBarPrefab, uiCanvas.transform);
            staminaBarInstance.name = "StaminaBar";
        }

        // Initialize combo text spawner (existing system)
        if (comboTextSpawnerPrefab != null)
        {
            GameObject spawnerObj = Instantiate(comboTextSpawnerPrefab.gameObject, uiCanvas.transform);
            comboTextSpawnerInstance = spawnerObj.GetComponent<ComboTextSpawner>();
            spawnerObj.name = "ComboTextSpawner";
        }

        // Initialize new combo panel
        if (comboPanel != null)
        {
            comboPanel.SetActive(false);
        }

        // Initialize special effects
        if (megaComboEffect != null) megaComboEffect.SetActive(false);
        if (onFireEffect != null) onFireEffect.SetActive(false);
        if (comboGlowEffect != null) comboGlowEffect.SetActive(false);

        Debug.Log("UIManager: Initialized successfully");
    }

    /// <summary>
    /// Finds the ScoreSystem and subscribes to its events.
    /// </summary>
    private void FindAndSubscribeToScoreSystem()
    {
        scoreSystem = FindObjectOfType<ScoreSystem>();
        if (scoreSystem != null)
        {
            scoreSystem.OnComboStarted += ShowComboStarted;
            scoreSystem.OnComboIncreased += UpdateComboDisplay;
            scoreSystem.OnComboFinalized += ShowComboFinalized;
            scoreSystem.OnMegaCombo += ShowMegaComboEffect;
            scoreSystem.OnOnFire += ShowOnFire;
            // scoreSystem.OnScoreAdded; // Not subscribed as UpdateScore() is called by ScoreSystem
            scoreSystem.OnStreakAchieved += ShowStreakAchieved;
            
            Debug.Log("UIManager: Subscribed to ScoreSystem events.");
        }
        else
        {
            Debug.LogError("UIManager: ScoreSystem not found! Combo features will not work.");
        }
    }
    
    private void OnDestroy()
    {
        if (scoreSystem != null)
        {
            scoreSystem.OnComboStarted -= ShowComboStarted;
            scoreSystem.OnComboIncreased -= UpdateComboDisplay;
            scoreSystem.OnComboFinalized -= ShowComboFinalized;
            scoreSystem.OnMegaCombo -= ShowMegaComboEffect;
            scoreSystem.OnOnFire -= ShowOnFire;
            scoreSystem.OnStreakAchieved -= ShowStreakAchieved;
        }
    }


    // ==================== SCORE / STAMINA / TRICK TEXT (API) ====================

    /// <summary>
    /// Updates the main score display text with animation. Called by ScoreSystem.
    /// </summary>
    public void UpdateScore(int newScore)
    {
        if (scoreText == null) return;

        // Animate score counting up smoothly
        if (scoreAnimCoroutine != null)
            StopCoroutine(scoreAnimCoroutine);
        
        scoreAnimCoroutine = StartCoroutine(AnimateScoreCount(displayedScore, newScore));
    }

    /// <summary>
    /// Updates the player's stamina bar.
    /// </summary>
    public void UpdateStamina(float current, float max)
    {
        if (staminaBarInstance != null)
        {
            staminaBarInstance.UpdateStamina(current, max);
        }
    }

    // ==================== TRICK TEXT SPAWNER ====================

    /// <summary>
    /// Spawns an animated text pop-up at the player's position, converting it to screen space.
    /// </summary>
    /// <param name="trickName">Name of the trick (e.g., "Kickflip")</param>
    /// <param name="worldPosition">The player's world position where the trick was performed.</param>
    /// <param name="score">The score received for the trick/grind.</param>
    /// <param name="comboMultiplier">The current combo multiplier.</param>
    public void SpawnTrickText(string trickName, Vector3 worldPosition, int score, float comboMultiplier)
    {
        if (trickTextPrefab == null)
        {
            Debug.LogError("TrickText Prefab is not assigned in UIManager!");
            return;
        }

        // 1. Convert World Position to Screen Position
        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);

        // Check if the object is visible and in front of the camera
        if (screenPos.z < 0) return;

        // 2. Instantiate the prefab as a child of the UI Canvas
        TrickText trickTextInstance = Instantiate(trickTextPrefab, uiCanvas.transform);
        
        // 3. Set the position of the instantiated object.
        // Screen position is already relative to the bottom-left of the screen, which is what the Canvas uses.
        trickTextInstance.transform.position = screenPos;

        // 4. Initialize and start the animation
        trickTextInstance.Setup(trickName, score, comboMultiplier);
        
        // Give it a slight random horizontal offset for visual separation when spamming tricks
        float randomOffset = UnityEngine.Random.Range(-50f, 50f);
        trickTextInstance.transform.position += new Vector3(randomOffset, 0, 0);
    }

    // ==================== ANIMATED SCORE COUNT ====================

    private IEnumerator AnimateScoreCount(int from, int to)
    {
        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Ease out cubic
            t = 1f - Mathf.Pow(1f - t, 3f);

            int current = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
            scoreText.text = current.ToString("N0");
            scoreText.ForceMeshUpdate();
            displayedScore = current;

            yield return null;
        }

        scoreText.text = to.ToString("N0");
        scoreText.ForceMeshUpdate();
        displayedScore = to;
    }
    
    

    // ==================== COMBO SYSTEM HANDLERS (Subscribed to ScoreSystem) ====================

    /// <summary>
    /// Show combo panel when combo starts
    /// </summary>
    public void ShowComboStarted(int comboCount, float multiplier)
    {
        if (comboPanel != null)
        {
            comboPanel.SetActive(true);
            StartCoroutine(PunchScale(comboPanel.transform, 1.2f, 0.2f));
        }

        UpdateComboDisplay(comboCount, 0, multiplier);

        if (comboSound != null)
        {
            comboSound.pitch = 1f; 
            comboSound.Play();
        }
    }

    /// <summary>
    /// Update combo display during combo. Called by ScoreSystem.
    /// </summary>
    public void UpdateComboDisplay(int comboCount, int comboScore, float multiplier)
    {
        // Update texts
        if (comboCountText != null)
        {
            comboCountText.text = $"{comboCount}x";
            StartCoroutine(PunchScale(comboCountText.transform, 1.3f, 0.15f));
        }

        if (comboMultiplierText != null)
        {
            comboMultiplierText.text = $"x{multiplier:F1}";
            comboMultiplierText.color = GetMultiplierColor(multiplier);
        }

        if (comboScoreText != null)
        {
            comboScoreText.text = $"{comboScore:N0} pts";
        }
        
        // Update multiplier display bar
        UpdateMultiplierDisplay(multiplier);

        // Play incremental sound
        if (comboSound != null)
        {
            comboSound.pitch = Mathf.Min(1f + (comboCount * 0.1f), 2f); // Pitch increases with combo, capped at 2x
            comboSound.Play();
        }
    }

    /// <summary>
    /// Update the combo timer bar. Called by ScoreSystem.Update().
    /// </summary>
    public void UpdateComboTimer(float currentTime, float maxTime)
    {
        if (comboTimerBar != null)
        {
            float fill = Mathf.Clamp01(currentTime / maxTime);
            comboTimerBar.fillAmount = fill;

            // Change color based on urgency
            if (fill < 0.2f)
                comboTimerBar.color = Color.red;
            else if (fill < 0.5f)
                comboTimerBar.color = Color.yellow;
            else
                comboTimerBar.color = Color.green;
        }
    }

    /// <summary>
    /// Show combo finalized (banked). Called by ScoreSystem.
    /// </summary>
    public void ShowComboFinalized(int comboCount, int totalScore, List<string> tricks)
    {
        StartCoroutine(ComboFinalizedAnimation(comboCount, totalScore));

        // Play special sound for big combos
        if (comboCount >= 5 && megaComboSound != null)
        {
            megaComboSound.Play();
        }
        
        // Optional: Spawn a huge banked score text popup here
        // SpawnTrickText($"BANKED! +{totalScore:N0}", Vector3.zero, comboCount, 1f);

        // Hide panel after delay
        StartCoroutine(HideComboPanel(1.5f));
        
        // Reset special effects
        if (megaComboEffect != null) megaComboEffect.SetActive(false);
        if (onFireEffect != null) onFireEffect.SetActive(false);
    }

    private IEnumerator ComboFinalizedAnimation(int comboCount, int score)
    {
        // Flash the combo score
        if (comboScoreText != null)
        {
            Color originalColor = comboScoreText.color;
            for (int i = 0; i < 3; i++)
            {
                comboScoreText.color = Color.yellow;
                yield return new WaitForSeconds(0.1f);
                comboScoreText.color = Color.white;
                yield return new WaitForSeconds(0.1f);
            }
            comboScoreText.color = originalColor;
        }
    }

    /// <summary>
    /// Show combo dropped (failed). Called by ScoreSystem.
    /// </summary>
    public void ShowComboDropped(int lostScore)
    {
        if (comboPanel != null)
        {
            StartCoroutine(ShakeTransform(comboPanel.transform, 0.3f, 5f));
        }

        if (comboScoreText != null)
        {
            StartCoroutine(ShowDroppedText());
        }

        StartCoroutine(HideComboPanel(0.8f));
        
        // Reset special effects
        if (megaComboEffect != null) megaComboEffect.SetActive(false);
        if (onFireEffect != null) onFireEffect.SetActive(false);
    }
    
    private IEnumerator ShowDroppedText()
    {
        if (comboScoreText == null) yield break;

        string originalText = comboScoreText.text;
        Color originalColor = comboScoreText.color;

        comboScoreText.text = "DROPPED!";
        comboScoreText.color = Color.red;
        comboScoreText.ForceMeshUpdate();

        yield return new WaitForSeconds(0.6f);

        comboScoreText.text = originalText;
        comboScoreText.color = originalColor;
        comboScoreText.ForceMeshUpdate();
    }

    private IEnumerator HideComboPanel(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (comboPanel != null)
        {
            comboPanel.SetActive(false);
        }

        if (comboGlowEffect != null)
        {
            comboGlowEffect.SetActive(false);
        }
    }

    // ==================== MULTIPLIER & STREAK EFFECTS ====================

    private void UpdateMultiplierDisplay(float multiplier)
    {
        if (multiplierText != null)
        {
            multiplierText.text = $"x{multiplier:F1}";
            multiplierText.color = GetMultiplierColor(multiplier);
        }

        if (multiplierFillBar != null)
        {
            float maxMultiplier = scoreSystem != null ? scoreSystem.maxMultiplier : 10f;
            multiplierFillBar.fillAmount = Mathf.Clamp01(multiplier / maxMultiplier);
            multiplierFillBar.color = GetMultiplierColor(multiplier);
        }
    }

    private Color GetMultiplierColor(float multiplier)
    {
        if (multiplier < 2.5f) return Color.white;
        if (multiplier < 5f) return Color.yellow;
        if (multiplier < 7.5f) return new Color(1f, 0.65f, 0f); // Orange
        if (multiplier < 9.5f) return new Color(1f, 0.27f, 0f); // Red-Orange
        return Color.red; // Max
    }
    
    /// <summary>
    /// Show a bonus message when a perfect streak threshold is hit.
    /// </summary>
    public void ShowStreakAchieved(int streakCount)
    {
        StartCoroutine(ShowBigMessage($"✨ STREAK x{streakCount} BONUS! ✨", Color.cyan, 1.0f));
    }

    public void ShowMegaComboEffect()
    {
        if (megaComboEffect != null && !megaComboEffect.activeSelf)
        {
            megaComboEffect.SetActive(true);
            StartCoroutine(ShowBigMessage("⭐ MEGA COMBO! ⭐", Color.yellow, 1.5f));
        }

        if (comboGlowEffect != null)
        {
            comboGlowEffect.SetActive(true);
        }
    }

    public void ShowOnFire()
    {
        if (onFireEffect != null && !onFireEffect.activeSelf)
        {
            onFireEffect.SetActive(true);
            StartCoroutine(ShowBigMessage("🔥 ON FIRE! 🔥", Color.red, 2f));
        }
    }

    private IEnumerator ShowBigMessage(string message, Color color, float duration)
    {
        // Create temporary big message
        GameObject msgObj = new GameObject("BigMessage");
        msgObj.transform.SetParent(uiCanvas.transform, false);

        TMP_Text msgText = msgObj.AddComponent<TextMeshProUGUI>();
        msgText.text = message;
        msgText.fontSize = 72;
        msgText.color = color;
        msgText.alignment = TextAlignmentOptions.Center;
        msgText.fontStyle = FontStyles.Bold;

        RectTransform rect = msgObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(800, 200);

        // Animate (Scale up/down and Fade out)
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Scale animation: Pop in, hold, fade out
            float scale = 1f;
            if (t < 0.2f) scale = Mathf.Lerp(0f, 1.3f, t / 0.2f);
            else if (t < 0.3f) scale = Mathf.Lerp(1.3f, 1f, (t - 0.2f) / 0.1f);
            else if (t > 0.7f) scale = Mathf.Lerp(1f, 0f, (t - 0.7f) / 0.3f);

            msgObj.transform.localScale = Vector3.one * scale;

            // Fade out
            Color c = msgText.color;
            if (t > 0.7f) c.a = 1f - ((t - 0.7f) / 0.3f);
            msgText.color = c;

            yield return null;
        }

        Destroy(msgObj);
    }

    // ==================== STATS HUD (Must be manually updated by GameManager/ScoreSystem) ====================

    /// <summary>
    /// Updates the statistical texts on the HUD.
    /// </summary>
    public void UpdateStatsHUD(int tricks, int perfectLandings, int biggestCombo)
    {
        if (tricksCountText != null) tricksCountText.text = $"Tricks: {tricks}";
        if (perfectLandingsText != null) perfectLandingsText.text = $"Perfect: {perfectLandings}";
        if (biggestComboText != null) biggestComboText.text = $"Best: {biggestCombo}x";
        
        // Force mesh updates for immediate display
        if (tricksCountText != null) tricksCountText.ForceMeshUpdate();
        if (perfectLandingsText != null) perfectLandingsText.ForceMeshUpdate();
        if (biggestComboText != null) biggestComboText.ForceMeshUpdate();
    }

    // ==================== GENERIC PANEL MANAGEMENT (Preserved) ====================
    
    public void ShowMainMenu() { Debug.Log("ShowMainMenu called"); HideAllPanels(); }
    public void ShowGameplayUI() { Debug.Log("ShowGameplayUI called"); HideAllPanels(); }
    
    public void UpdateRoundDisplay(int round)
    {
        if (roundText != null) roundText.text = $"Round {round}";
    }

    public void UpdateRoundDisplay(int round, int totalRounds)
    {
        if (roundText != null) roundText.text = $"Round {round}/{totalRounds}";
    }

    public void ShowLevelResults(LevelResultData results)
    {
        Debug.Log($"Level Results - Passed: {results.passed}, Score: {results.score}");
        HideAllPanels();
    }

    public void ShowStoreUI() { Debug.Log("ShowStoreUI called"); HideAllPanels(); }
    public void ShowPauseMenu() { Debug.Log("ShowPauseMenu called"); HideAllPanels(); }
    public void ShowGameOver(GameOverData data) { Debug.Log($"Game Over - Victory: {data.victory}"); HideAllPanels(); }
    public void UpdateCoinDisplay(int coins) { Debug.Log($"Coins: {coins}"); }
    
    // Placeholder definitions for internal methods used in the original UIManager
    public void ShowSkillChallenge(object challenge, float target) { }
    public void ShowLevelUI(LevelManager levelManager) { }
    public void ShowLevelUI(LevelResultData level) { }


    private void HideAllPanels()
    {
        if (comboPanel != null) comboPanel.SetActive(false);
        if (megaComboEffect != null) megaComboEffect.SetActive(false);
        if (onFireEffect != null) onFireEffect.SetActive(false);
        // Add other main UI panels here
    }

    // ==================== ANIMATION HELPERS ====================

    private IEnumerator PunchScale(Transform target, float punchScale, float duration)
    {
        if (target == null) yield break;

        Vector3 originalScale = target.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float scale = 1f;
            if (t < 0.5f) scale = Mathf.Lerp(1f, punchScale, t / 0.5f);
            else scale = Mathf.Lerp(punchScale, 1f, (t - 0.5f) / 0.5f);

            target.localScale = originalScale * scale;
            yield return null;
        }

        target.localScale = originalScale;
    }

    private IEnumerator ShakeTransform(Transform target, float duration, float magnitude)
    {
        if (target == null) yield break;

        Vector3 originalPos = target.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float x = UnityEngine.Random.Range(-1f, 1f) * magnitude;
            float y = UnityEngine.Random.Range(-1f, 1f) * magnitude;

            target.localPosition = originalPos + new Vector3(x, y, 0);
            yield return null;
        }

        target.localPosition = originalPos;
    }
}