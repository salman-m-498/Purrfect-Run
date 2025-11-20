using UnityEngine;
using System;

public class LevelManager : MonoBehaviour
{
    public enum LevelType { Normal, SkillCheck }
    public enum SkillChallenge { None, AirTime, Speed, PerfectLanding }

    [Header("Level Settings")]
    public LevelType levelType = LevelType.Normal;
    public int requiredScore = 1000;         // Score needed to complete level
    public float timeLimit = 0f;             // 0 = no limit
    public SkillChallenge skillChallenge = SkillChallenge.None;
    public float skillTarget = 0f;           // e.g., AirTime seconds, Speed units, etc.

    [Header("References")]
    public Transform startPoint;
    public Transform finishPoint;
    private PlayerController playerController;
    private ScoreSystem scoreSystem;
    private UIManager uiManager;
    private GameManager gameManager;

    private bool levelActive = false;
    private float levelStartTime;

    // Event for Level Completion or Failure
    public event Action<bool, LevelManager> OnLevelComplete; // bool = success/fail

    private void Awake()
    {
        playerController = FindObjectOfType<PlayerController>();
        scoreSystem = FindObjectOfType<ScoreSystem>();
        uiManager = UIManager.Instance;
        gameManager = FindObjectOfType<GameManager>();

        if (!startPoint || !finishPoint)
            Debug.LogWarning("Start or Finish point not assigned in LevelManager.");
    }

    private void Start()
    {
        StartLevel();
    }

    private void Update()
    {
        if (!levelActive) return;

        CheckFinish();
        CheckTimeLimit();
        CheckSkillChallenge();
    }

    // ===================== LEVEL CONTROL =====================

    /// <summary>
    /// Initializes the level and starts tracking.
    /// </summary>
    public void StartLevel()
    {
        levelActive = true;
        levelStartTime = Time.time;

        // Move player to start
        if (playerController != null && startPoint != null)
        {
            playerController.transform.position = startPoint.position;
            playerController.ResetState();
        }

        // Initialize UI
        if (uiManager != null)
        {
            uiManager.ShowLevelUI(this);
        }

        Debug.Log($"Level started. Type: {levelType}, Required Score: {requiredScore}");
    }

    public float GetLevelTime()
    {
        return 60f; // Example: 60 seconds for testing
    }

    /// <summary>
    /// Ends the level and reports to GameManager.
    /// </summary>
    /// <param name="success">Did the player succeed?</param>
    public void EndLevel(bool success)
    {
        levelActive = false;

        float elapsedTime = Time.time - levelStartTime;
        int finalScore = scoreSystem != null ? scoreSystem.CurrentScore : 0;

        Debug.Log($"Level ended. Success: {success}, Score: {finalScore}, Time: {elapsedTime:F2}s");

        // Report to GameManager
        gameManager?.OnLevelCompleted(success, finalScore, elapsedTime);

        // Trigger completion event
        OnLevelComplete?.Invoke(success, this);
    }

    // ===================== LEVEL CHECKS =====================

    private void CheckFinish()
    {
        if (finishPoint == null || playerController == null) return;

        float distanceToFinish = Vector3.Distance(playerController.transform.position, finishPoint.position);
        if (distanceToFinish < 1.5f) // adjustable tolerance
        {
            bool success = scoreSystem != null && scoreSystem.CurrentScore >= requiredScore;

            // For skill check levels, validate challenge
            if (levelType == LevelType.SkillCheck)
            {
                success = success && CheckSkillCompletion();
            }

            EndLevel(success);
        }
    }

    private void CheckTimeLimit()
    {
        if (timeLimit <= 0f) return;

        if (Time.time - levelStartTime >= timeLimit)
        {
            Debug.Log("Level failed: Time limit reached.");
            EndLevel(false);
        }
    }

    private void CheckSkillChallenge()
    {
        if (levelType != LevelType.SkillCheck) return;

        // For visual or gameplay hints (optional)
        uiManager?.ShowSkillChallenge(skillChallenge, skillTarget);
    }

    private bool CheckSkillCompletion()
    {
        switch (skillChallenge)
        {
            case SkillChallenge.AirTime:
                return playerController != null && playerController.CurrentAirTime >= skillTarget;

            case SkillChallenge.Speed:
                return playerController != null && playerController.CurrentSpeed >= skillTarget;

            case SkillChallenge.PerfectLanding:
                return playerController != null && playerController.LastLandingPerfect;

            default:
                return true;
        }
    }

    // ===================== DEBUG / EDITOR HELPERS =====================

    private void OnDrawGizmosSelected()
    {
        if (startPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(startPoint.position, 0.5f);
        }

        if (finishPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(finishPoint.position, 0.5f);
        }
    }

    public void SetupLevel(LevelType levelType, int requiredScore, float timeLimit)
    {
        this.levelType = levelType;
        this.requiredScore = requiredScore;
        this.timeLimit = timeLimit;
        Debug.Log($"LevelManager: SetupLevel called - Type: {levelType}, Score: {requiredScore}, Time: {timeLimit}");
    }
}
