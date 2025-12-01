using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles the audio volume sliders in the pause menu.
/// Attach this to your pause menu panel.
/// </summary>
public class PauseMenuAudioUI : MonoBehaviour
{
    [Header("Volume Sliders")]
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;

    [Header("Volume Labels (Optional)")]
    public TMP_Text masterVolumeLabel;
    public TMP_Text musicVolumeLabel;
    public TMP_Text sfxVolumeLabel;

    [Header("Settings")]
    public bool showPercentages = true; // Show "75%" next to sliders

    private void OnEnable()
    {
        // Wait a frame to ensure AudioManager is ready
        StartCoroutine(InitializeAfterDelay());
    }

    private void Start()
    {
        // Set up slider listeners
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }
    }

    private System.Collections.IEnumerator InitializeAfterDelay()
    {
        // Wait until AudioManager is ready
        int attempts = 0;
        while (AudioManager.Instance == null && attempts < 10)
        {
            yield return null;
            attempts++;
        }

        InitializeSliders();
    }

    private void InitializeSliders()
    {
        if (AudioManager.Instance == null)
        {
            Debug.LogWarning("PauseMenuAudioUI: AudioManager.Instance is still null after waiting!");
            // Try to find it in scene
            AudioManager.Instance = FindObjectOfType<AudioManager>();
            
            if (AudioManager.Instance == null)
            {
                Debug.LogError("PauseMenuAudioUI: No AudioManager found in scene! Please add one.");
                return;
            }
        }

        // Set slider values without triggering events
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.GetMasterVolume());
            UpdateLabel(masterVolumeLabel, masterVolumeSlider.value);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.GetMusicVolume());
            UpdateLabel(musicVolumeLabel, musicVolumeSlider.value);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.GetSFXVolume());
            UpdateLabel(sfxVolumeLabel, sfxVolumeSlider.value);
        }
    }

    // Slider event handlers
    private void OnMasterVolumeChanged(float value)
    {
        AudioManager.Instance?.SetMasterVolume(value);
        UpdateLabel(masterVolumeLabel, value);
    }

    private void OnMusicVolumeChanged(float value)
    {
        AudioManager.Instance?.SetMusicVolume(value);
        UpdateLabel(musicVolumeLabel, value);
    }

    private void OnSFXVolumeChanged(float value)
    {
        AudioManager.Instance?.SetSFXVolume(value);
        UpdateLabel(sfxVolumeLabel, value);
        
        // Optional: Play a test SFX sound when adjusting SFX volume
        // AudioManager.Instance?.PlaySFX(SoundID.UI_Click);
    }

    private void UpdateLabel(TMP_Text label, float volume)
    {
        if (label == null || !showPercentages) return;

        int percentage = Mathf.RoundToInt(volume * 100f);
        label.text = $"{percentage}%";
    }

    private void OnDestroy()
    {
        // Clean up listeners
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);

        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
    }
}