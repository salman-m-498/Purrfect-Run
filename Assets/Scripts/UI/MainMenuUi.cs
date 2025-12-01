using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Main Menu Manager - Handles the main menu UI and navigation
/// </summary>
public class MainMenuUi : MonoBehaviour
{
    [Header("Menu Panels")]
    public GameObject mainMenuPanel;
    public GameObject settingsPanel;
    public GameObject creditsPanel;
    public GameObject statsPanel;

    [Header("Stats Display")]
    public TMP_Text highScoreText;
    public TMP_Text totalRunsText;
    public TMP_Text totalCoinsText;

    [Header("Scene Names")]
    public string gameSceneName = "EndlessMode"; // Name of your gameplay scene

    [Header("Audio")]
    public AudioSource buttonClickSound;

    private void Start()
    {
        // Show main menu by default
        ShowMainMenu();

        // Load and display player stats
        UpdateStatsDisplay();

        // Play menu music
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMusic(SoundID.Music_Menu);
        }
    }

    // ============================================================
    // NAVIGATION
    // ============================================================

    public void ShowMainMenu()
    {
        HideAllPanels();
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        PlayButtonSound();
    }

    public void ShowSettings()
    {
        HideAllPanels();
        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        PlayButtonSound();
    }

    public void ShowCredits()
    {
        HideAllPanels();
        if (creditsPanel != null)
            creditsPanel.SetActive(true);

        PlayButtonSound();
    }

    public void ShowStats()
    {
        HideAllPanels();
        if (statsPanel != null)
            statsPanel.SetActive(true);

        UpdateStatsDisplay();
        PlayButtonSound();
    }

    private void HideAllPanels()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
        if (statsPanel != null) statsPanel.SetActive(false);
    }

    // ============================================================
    // BUTTON ACTIONS
    // ============================================================

    public void PlayGame()
    {
        PlayButtonSound();
        
        // Stop menu music
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopMusic();
        }

        // Load game scene
        SceneManager.LoadScene(gameSceneName);
    }

    public void QuitGame()
    {
        PlayButtonSound();
        
        Debug.Log("Quitting game...");
        
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    // ============================================================
    // STATS
    // ============================================================

    private void UpdateStatsDisplay()
    {
        if (GameManager.Instance == null) return;

        PlayerData playerData = GameManager.Instance.GetPlayerData();
        if (playerData == null) return;

        if (highScoreText != null)
            highScoreText.text = $"High Score: {playerData.highScore:N0}";

        if (totalRunsText != null)
            totalRunsText.text = $"Total Runs: {playerData.totalRuns}";

        if (totalCoinsText != null)
            totalCoinsText.text = $"Total Coins: {playerData.lifetimeCoins:N0}";
    }

    // ============================================================
    // AUDIO
    // ============================================================

    private void PlayButtonSound()
    {
        if (buttonClickSound != null)
        {
            buttonClickSound.Play();
        }
        else if (AudioManager.Instance != null)
        {
            // Fallback to AudioManager if available
            // AudioManager.Instance.PlaySFX(SoundID.UI_Click);
        }
    }
}