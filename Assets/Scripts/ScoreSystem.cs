using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enhanced ScoreSystem with addictive combo mechanics, multipliers, and juice.
/// Tracks and manages scoring for the current level and run.
/// </summary>
public class ScoreSystem : MonoBehaviour
{
    // NOTE: Update to static or Singleton pattern later

    [Header("Score Tracking")]
    private int currentLevelScore = 0;
    private int totalRunScore = 0;
    private int comboScore = 0; // Score accumulated during current combo
    
    [Header("Combo System")]
    private int comboCount = 0; // Number of tricks in combo
    private float comboMultiplier = 1f;
    private float comboTimer = 0f;
    public float comboTimeWindow = 2f; // Time window to continue combo
    private List<string> comboTricks = new List<string>(); // Track trick names
    
    [Header("Multiplier System")]
    public float baseMultiplier = 1f;
    public float multiplierPerTrick = 0.5f; // +0.5x per trick
    public float maxMultiplier = 10f;
    private float speedMultiplier = 1f; // Bonus for speed
    private float airTimeMultiplier = 1f; // Bonus for air time
    
    [Header("Streak System")]
    private int perfectLandingStreak = 0;
    private int tricksWithoutFalling = 0; // Tracks tricks landed between falls/drops
    public int streakThreshold = 3; // Perfect landings needed for streak bonus
    
    [Header("Statistics")]
    private int totalCombos = 0;
    private int perfectLandings = 0;
    private int tricksPerformed = 0;
    private int biggestCombo = 0;
    private float highestMultiplier = 0f; // Changed to float to match comboMultiplier
    
    [Header("Special Bonuses")]
    private bool isInMegaCombo = false; // 5+ tricks
    private bool isOnFire = false; // 10+ tricks without falling/dropping a combo
    
    [Header("References")]
    // NOTE: Assuming UIManager and GameManager are singletons or you will ensure they exist.
    private GameManager gameManager;
    
    // Public properties for read access
    public int CurrentScore => currentLevelScore;

    // Events for UI animations/juice
    public System.Action<int, float> OnScoreAdded; // (points, multiplier)
    public System.Action<int, float> OnComboStarted; // (comboCount, multiplier)
    public System.Action<int, int, float> OnComboIncreased; // (comboCount, totalComboScore, multiplier)
    public System.Action<int, int, List<string>> OnComboFinalized; // (comboCount, totalScore, trickNames)
    public System.Action<int> OnStreakAchieved; // (streakCount)
    public System.Action OnMegaCombo; // 5+ tricks
    public System.Action OnOnFire; // 10+ tricks
    
    private void Awake()
    {
        gameManager = FindObjectOfType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogError("ScoreSystem: GameManager not found!");
        }
    }

    private void Start()
    {
        UpdateScoreDisplay();
        Debug.Log("ScoreSystem: Initialized and ready");
    }

    private void Update()
    {
        // Update combo timer
        if (comboCount > 0)
        {
            comboTimer -= Time.deltaTime;
            
            // Update UI with timer (Assuming UIManager has this method)
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateComboTimer(comboTimer, comboTimeWindow);
            }
            
            if (comboTimer <= 0f)
            {
                // Combo expired without landing - lose it all!
                DropCombo();
            }
        }
    }

    // --- Core Scoring Logic ---

    /// <summary>
    /// Add a trick to the combo system. This should be called *while* the player is in the air.
    /// </summary>
    public void AddTrickScore(string trickName, int basePoints, float airTime = 0f, float speed = 0f)
    {
        // Calculate dynamic multipliers (can be 1x if airTime/speed are 0)
        airTimeMultiplier = 1f + (airTime * 0.1f); // +10% per second in air
        speedMultiplier = 1f + (speed * 0.05f); // +5% per unit of speed (adjust 0.05f based on game scale)
        
        // Increment combo
        if (comboCount == 0)
        {
            StartCombo(trickName, basePoints);
        }
        else
        {
            ContinueCombo(trickName, basePoints);
        }
        
        // Reset combo timer
        comboTimer = comboTimeWindow;
        
        // Check for special states (Mega Combo, On Fire)
        CheckSpecialStates();
    }
    
    private void StartCombo(string trickName, int basePoints)
    {
        comboCount = 1;
        comboMultiplier = baseMultiplier;
        comboTricks.Clear();
        comboTricks.Add(trickName);
        
        // Calculate score with multipliers
        int points = CalculateTrickPoints(basePoints);
        comboScore = points;
        
        tricksPerformed++;
        tricksWithoutFalling++;
        
        OnComboStarted?.Invoke(comboCount, comboMultiplier);
        OnScoreAdded?.Invoke(points, comboMultiplier);
        
        Debug.Log($"🎯 COMBO STARTED! {trickName} = {points} pts (x{comboMultiplier:F1})");
    }
    
    private void ContinueCombo(string trickName, int basePoints)
    {
        comboCount++;
        comboTricks.Add(trickName);
        
        // Increase multiplier (Capped at maxMultiplier)
        comboMultiplier = Mathf.Min(
            baseMultiplier + (comboCount * multiplierPerTrick),
            maxMultiplier
        );
        
        // Calculate score
        int points = CalculateTrickPoints(basePoints);
        comboScore += points;
        
        tricksPerformed++;
        tricksWithoutFalling++;
        
        // Track highest multiplier
        if (comboMultiplier > highestMultiplier)
        {
            highestMultiplier = comboMultiplier;
        }
        
        OnComboIncreased?.Invoke(comboCount, comboScore, comboMultiplier);
        OnScoreAdded?.Invoke(points, comboMultiplier);
        
        Debug.Log($"🔥 COMBO x{comboCount}! {trickName} = {points} pts (x{comboMultiplier:F1}) | Total: {comboScore}");
    }
    
    private int CalculateTrickPoints(int basePoints)
    {
        float totalMultiplier = comboMultiplier * airTimeMultiplier * speedMultiplier;
        
        // Streak bonus for maintaining a perfect landing streak
        if (perfectLandingStreak >= streakThreshold)
        {
            totalMultiplier *= 1.5f; // 50% bonus for perfect streak
            // Invoke streak achieved event only when the trick is performed to provide feedback.
            if (tricksWithoutFalling > 0 && tricksWithoutFalling % streakThreshold == 0) 
            {
                 OnStreakAchieved?.Invoke(perfectLandingStreak);
            }
        }
        
        return Mathf.RoundToInt(basePoints * totalMultiplier);
    }
    
    /// <summary>
    /// Finalize combo when landing (adds accumulated combo score to level score)
    /// </summary>
    /// <param name="perfectLanding">True if the player executed a perfect landing.</param>
    public void FinalizeCombo(bool perfectLanding = false)
    {
        if (comboCount == 0) return;
        
        // Perfect landing bonus and streak tracking
        if (perfectLanding)
        {
            perfectLandings++;
            perfectLandingStreak++;
            float landingBonus = 1.2f; // 20% bonus to the total combo score
            comboScore = Mathf.RoundToInt(comboScore * landingBonus);
            
            Debug.Log($"✨ PERFECT LANDING! +20% Bonus | Streak: {perfectLandingStreak}");
        }
        else
        {
            perfectLandingStreak = 0; // Reset streak on a non-perfect landing
        }
        
        // Add combo score to level score
        currentLevelScore += comboScore;
        totalCombos++;
        
        // Track biggest combo
        if (comboCount > biggestCombo)
        {
            biggestCombo = comboCount;
        }
        
        // Notify UI
        OnComboFinalized?.Invoke(comboCount, comboScore, new List<string>(comboTricks));
        
        Debug.Log($"💰 COMBO BANKED! {comboCount} tricks = {comboScore} pts | Total Score: {currentLevelScore}");
        
        // Reset combo state for the next combo
        ResetComboState();
        
        // Update display
        UpdateScoreDisplay();
    }
    
    /// <summary>
    /// Drop combo if failed (e.g., ran out of time, or fell/crashed). No points awarded.
    /// </summary>
    public void DropCombo()
    {
        if (comboCount == 0) return;
        
        Debug.Log($"❌ COMBO DROPPED! Lost {comboScore} potential points");
        
        // Reset combo/streak/fall state
        ResetComboState();
        perfectLandingStreak = 0;
        tricksWithoutFalling = 0; // Combo drop counts as a "fall" in terms of streak
        
        // Notify UI (Assuming UIManager has this method)
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowComboDropped(comboScore);
        }
    }
    
    private void ResetComboState()
    {
        comboCount = 0;
        comboScore = 0;
        comboMultiplier = 1f;
        comboTimer = 0f;
        comboTricks.Clear();
        isInMegaCombo = false;
        isOnFire = false;
    }
    
    private void CheckSpecialStates()
    {
        // Mega Combo (5+ tricks in a single combo)
        if (comboCount >= 5 && !isInMegaCombo)
        {
            isInMegaCombo = true;
            OnMegaCombo?.Invoke();
            Debug.Log("🌟 MEGA COMBO ACTIVATED!");
        }
        
        // On Fire (10+ tricks without landing/dropping a combo)
        if (tricksWithoutFalling >= 10 && !isOnFire)
        {
            isOnFire = true;
            OnOnFire?.Invoke();
            Debug.Log("🔥🔥🔥 YOU'RE ON FIRE! 🔥🔥🔥");
        }
    }
    
    // --- Standard Score Management Methods ---

    /// <summary>
    /// Add points outside of the main combo system (e.g., collecting items).
    /// </summary>
    public void AddScore(int points)
    {
        if (points <= 0) return;

        currentLevelScore += points;
        UpdateScoreDisplay();
        Debug.Log($"ScoreSystem: Added {points} passive points. Current Level Score: {currentLevelScore}");
    }
    
    /// <summary>
    /// Resets the LEVEL score and adds it to run total (call this AFTER level ends).
    /// </summary>
    public void FinalizeLevelScore()
    {
        // Ensure any active combo is dropped or finalized before this is called by GameManager
        DropCombo(); // Added safety call

        Debug.Log($"ScoreSystem: Finalizing level score. Adding {currentLevelScore} to run total.");
        totalRunScore += currentLevelScore;
        currentLevelScore = 0;
        
        // Reset per-level stats
        totalCombos = 0;
        perfectLandings = 0;
        tricksPerformed = 0;
        
        ResetComboState();
        UpdateScoreDisplay();
    }
    
    /// <summary>
    /// Reset ONLY the current level score and stats (call this when restarting a level).
    /// </summary>
    public void ResetScore()
    {
        currentLevelScore = 0;
        totalCombos = 0;
        perfectLandings = 0;
        tricksPerformed = 0;
        tricksWithoutFalling = 0;
        perfectLandingStreak = 0;
        biggestCombo = 0;
        highestMultiplier = 0f;
        ResetComboState();
        UpdateScoreDisplay();
        Debug.Log("ScoreSystem: Level score reset to 0");
    }
    
    /// <summary>
    /// Reset all scores and stats at the start of a new run.
    /// </summary>
    public void ResetRunScore()
    {
        totalRunScore = 0;
        ResetScore();
        Debug.Log("ScoreSystem: Run score reset");
    }
    
    // --- Accessor Methods (Getters) ---
    
    public int GetCurrentScore() => currentLevelScore;
    /// <summary>
    /// Get the total score for the entire run, including the current level's score.
    /// </summary>
    public int GetTotalRunScore() => totalRunScore + currentLevelScore;
    public int GetComboCount() => comboCount;
    public float GetComboMultiplier() => comboMultiplier;
    public int GetComboScore() => comboScore;
    public int GetBiggestCombo() => biggestCombo;
    public float GetHighestMultiplier() => highestMultiplier;
    public int GetPerfectLandingStreak() => perfectLandingStreak;
    public int GetTricksPerformed() => tricksPerformed;
    public int GetTotalCombos() => totalCombos;
    public int GetPerfectLandings() => perfectLandings;
    public bool IsInCombo() => comboCount > 0;

    // --- Utility Methods ---
    
    private void UpdateScoreDisplay()
    {
        if (UIManager.Instance != null)
        {
            // Only update the level score display here. Combo display is handled by events.
            UIManager.Instance.UpdateScore(currentLevelScore);
        }
        else
        {
            Debug.LogWarning("ScoreSystem: UIManager.Instance is null! Cannot update score display.");
        }
    }
    
    /// <summary>
    /// Get a formatted summary of run statistics for end-of-run screen.
    /// </summary>
    public string GetRunSummary()
    {
        return $"Total Score: {GetTotalRunScore():N0}\n" +
               $"Biggest Combo: {biggestCombo}x\n" +
               $"Highest Multiplier: x{highestMultiplier:F1}\n" +
               $"Total Tricks: {tricksPerformed:N0}\n" +
               $"Perfect Landings: {perfectLandings}";
    }
    
    /// <summary>
    /// Get a formatted summary of the current combo for UI display.
    /// </summary>
    public string GetComboSummary()
    {
        if (comboCount == 0) return string.Empty;

        return $"{comboCount}x COMBO\n" +
               $"{comboScore:N0} pts\n" +
               $"x{comboMultiplier:F1} Multiplier";
    }
}