using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Player References")]
    public PlayerController playerController;
    public DollyCam dollyCam;
    public Transform boardVisual;

    [Header("UI References")]
    public ComboTextSpawner comboTextSpawner;
    public StaminaBarUI staminaBarUI;
    public TMP_Text scoreText;
    public TMP_Text comboText;

    [Header("Game Settings")]
    public bool isPaused = false;
    public bool gameStarted = false;

    [Header("Scene References")]
    public Camera mainCamera;
    public LayerMask groundLayer;
    public LayerMask grindLayer;

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

        InitializeGame();
    }

    void InitializeGame()
    {

        if(mainCamera == null)
            mainCamera = Camera.main;
        // Find references if not set
        if (playerController == null)
            playerController = FindObjectOfType<PlayerController>();
            
        if (dollyCam == null)
            dollyCam = FindObjectOfType<DollyCam>();
            
        if (comboTextSpawner == null)
            comboTextSpawner = FindObjectOfType<ComboTextSpawner>();

        // Initialize player references
        if (playerController != null)
        {
            playerController.Initialize(this);
        }
        else
        {
            Debug.LogError("PlayerController not found in scene!");
        }

        UpdateUI(0, 0, 1f);
    }

    public void UpdateUI(int score, int comboCount, float multiplier)
    {
        if (scoreText != null)
            scoreText.text = $"Score: {score}";
            
        if (comboText != null && comboCount > 1)
            comboText.text = $"Combo x{multiplier:F1} ({comboCount})";
        else if (comboText != null)
            comboText.text = "";
    }

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
    }

    public void RestartGame()
    {
        // Reset player state
        if (playerController != null)
        {
            playerController.ResetState();
        }

        UpdateUI(0, 0, 1f);
        gameStarted = true;
    }

    void OnEnable()
    {
        // Subscribe to events
    }

    void OnDisable()
    {
        // Unsubscribe from events
    }
}