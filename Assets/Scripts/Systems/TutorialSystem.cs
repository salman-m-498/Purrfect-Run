using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Contextual tutorial system that teaches players controls through gameplay.
/// Automatically triggers hints at the right moments and hides them once completed.
/// </summary>
public class TutorialSystem : MonoBehaviour
{
    public static TutorialSystem Instance { get; private set; }

    [Header("Tutorial State")]
    public bool tutorialActive = false;
    private bool tutorialCompleted = false;
    
    private enum TutorialStep
    {
        Push,
        Jump,
        BasicTrick,
        RightClickTrick,
        Dodge,
        Combo,
        Completed
    }
    
    private TutorialStep currentStep = TutorialStep.Push;
    private HashSet<TutorialStep> completedSteps = new HashSet<TutorialStep>();
    
    [Header("UI References")]
    public GameObject tutorialHintPanel;
    public TMP_Text tutorialText;
    public Image tutorialIcon;
    public CanvasGroup tutorialCanvasGroup;
    
    [Header("Tutorial Sprites (Optional)")]
    public Sprite pushIcon;
    public Sprite jumpIcon;
    public Sprite trickIcon;
    public Sprite comboIcon;
    
    [Header("Timing")]
    public float hintFadeInDuration = 0.5f;
    public float hintFadeOutDuration = 0.3f;
    public float delayBetweenHints = 2f;
    
    [Header("Progress Tracking")]
    private int pushCount = 0;
    private int jumpCount = 0;
    private int trickCount = 0;
    private int rightClickTrickCount = 0;
    private int grindCount = 0;
    private int comboCount = 0;
    private int dodgeCount = 0;
    private const int REQUIRED_DODGES = 1;
    private const int REQUIRED_RIGHT_CLICK_TRICKS = 1;
    
    private const int REQUIRED_PUSHES = 1;
    private const int REQUIRED_JUMPS = 1;
    private const int REQUIRED_TRICKS = 1;
    private const int REQUIRED_COMBOS = 1;
    
    // References
    private PlayerController playerController;
    private ScoreSystem scoreSystem;
    
    // Coroutines
    private Coroutine currentHintCoroutine;
    private Coroutine stepProgressCoroutine;

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
        // Check if this is the player's first time
        tutorialCompleted = PlayerPrefs.GetInt("TutorialCompleted", 0) == 1;
        
        if (tutorialHintPanel != null)
        {
            tutorialHintPanel.SetActive(false);
        }
        
        if (tutorialCanvasGroup != null)
        {
            tutorialCanvasGroup.alpha = 0f;
        }
    }

    /// <summary>
    /// Start the tutorial - called by EndlessGameManager when starting endless mode
    /// </summary>
    public void StartTutorial()
    {
        // Skip if already completed
        if (tutorialCompleted)
        {
            Debug.Log("Tutorial already completed - skipping");
            return;
        }
        
        tutorialActive = true;
        currentStep = TutorialStep.Push;
        completedSteps.Clear();
        
        // Find references
        if (playerController == null)
            playerController = FindObjectOfType<PlayerController>();
        if (scoreSystem == null)
            scoreSystem = FindObjectOfType<ScoreSystem>();
        
        // Subscribe to events
        SubscribeToEvents();
        
        // Show first hint after brief delay
        StartCoroutine(DelayedShowHint(1f, TutorialStep.Push));
        
        Debug.Log("Tutorial Started!");
    }

    /// <summary>
    /// Subscribe to game events to track player actions
    /// </summary>
    private void SubscribeToEvents()
    {
        if (scoreSystem != null)
        {
            scoreSystem.OnComboStarted += OnComboStarted;
        }
    }

    private void OnDestroy()
    {
        if (scoreSystem != null)
        {
            scoreSystem.OnComboStarted -= OnComboStarted;
        }
    }

    // ============================================================
    // PLAYER ACTION TRACKING
    // ============================================================

    /// <summary>
    /// Call this when player pushes
    /// </summary>
    public void OnPlayerPush()
    {
        if (!tutorialActive || completedSteps.Contains(TutorialStep.Push)) return;
        
        pushCount++;
        Debug.Log($"Tutorial: Push detected ({pushCount}/{REQUIRED_PUSHES})");
        
        if (pushCount >= REQUIRED_PUSHES)
        {
            CompleteStep(TutorialStep.Push);
            AdvanceToNextStep(TutorialStep.Jump);
        }
    }

    /// <summary>
    /// Call this when player jumps/ollies
    /// </summary>
    public void OnPlayerJump()
    {
        if (!tutorialActive || completedSteps.Contains(TutorialStep.Jump)) return;
        
        jumpCount++;
        Debug.Log($"Tutorial: Jump detected ({jumpCount}/{REQUIRED_JUMPS})");
        
        if (jumpCount >= REQUIRED_JUMPS)
        {
            CompleteStep(TutorialStep.Jump);
            AdvanceToNextStep(TutorialStep.BasicTrick);
        }
    }

    /// <summary>
    /// Call this when player performs a trick
    /// </summary>
    public void OnPlayerTrick(string trickName)
    {
        if (!tutorialActive || completedSteps.Contains(TutorialStep.BasicTrick)) return;
        
        // Only count actual tricks, not grinds or manuals
        if (trickName == "Grind" || trickName == "Manual") return;
        
        trickCount++;
        Debug.Log($"Tutorial: Trick detected - {trickName} ({trickCount}/{REQUIRED_TRICKS})");
        
        if (trickCount >= REQUIRED_TRICKS)
        {
            CompleteStep(TutorialStep.BasicTrick);
            AdvanceToNextStep(TutorialStep.RightClickTrick);
        }
    }

    /// <summary>
    /// Call this when player performs a right-click trick (PopShoveIt or TreFlip)
    /// </summary>
    public void OnPlayerRightClickTrick(string trickName)
    {
        if (!tutorialActive || completedSteps.Contains(TutorialStep.RightClickTrick)) return;

        rightClickTrickCount++;
        Debug.Log($"Tutorial: Right-click trick detected - {trickName} ({rightClickTrickCount}/{REQUIRED_RIGHT_CLICK_TRICKS})");

        if (rightClickTrickCount >= REQUIRED_RIGHT_CLICK_TRICKS)
        {
            CompleteStep(TutorialStep.RightClickTrick);
            AdvanceToNextStep(TutorialStep.Dodge);
        }
    }

    public void OnPlayerDodge()
    {
        Debug.Log($"OnPlayerDodge() called - tutorialActive: {tutorialActive}, already completed: {completedSteps.Contains(TutorialStep.Dodge)}");
        
        if (!tutorialActive || completedSteps.Contains(TutorialStep.Dodge)) 
        {
            Debug.LogWarning($"Dodge skipped - tutorialActive: {tutorialActive}, completedSteps contains Dodge: {completedSteps.Contains(TutorialStep.Dodge)}");
            return;
        }

        dodgeCount++;
        Debug.Log($"Tutorial: Dodge detected ({dodgeCount}/{REQUIRED_DODGES})");

        if (dodgeCount >= REQUIRED_DODGES)
        {
            CompleteStep(TutorialStep.Dodge);
            AdvanceToNextStep(TutorialStep.Combo);
        }
    }

    /// <summary>
    /// Called when player starts a combo
    /// </summary>
    public void OnComboStarted(int comboCount, float multiplier)
    {
        if (!tutorialActive || completedSteps.Contains(TutorialStep.Combo)) return;
        
        // Only count combos with 2+ tricks
        if (comboCount >= 2)
        {
            this.comboCount++;
            Debug.Log($"Tutorial: Combo detected ({this.comboCount}/{REQUIRED_COMBOS})");
            
            if (this.comboCount >= REQUIRED_COMBOS)
            {
                CompleteStep(TutorialStep.Combo);
                CompleteTutorial();
            }
        }
    }

    // ============================================================
    // TUTORIAL FLOW
    // ============================================================

    private void CompleteStep(TutorialStep step)
    {
        completedSteps.Add(step);
        Debug.Log($"Tutorial step completed: {step}");
        
        // Hide current hint
        if (currentHintCoroutine != null)
        {
            StopCoroutine(currentHintCoroutine);
        }
        currentHintCoroutine = StartCoroutine(HideHint());
    }

    private void AdvanceToNextStep(TutorialStep nextStep)
    {
        currentStep = nextStep;
        
        // Show next hint after delay
        if (stepProgressCoroutine != null)
        {
            StopCoroutine(stepProgressCoroutine);
        }
        stepProgressCoroutine = StartCoroutine(DelayedShowHint(delayBetweenHints, nextStep));
    }

    private void CompleteTutorial()
    {
        tutorialActive = false;
        tutorialCompleted = true;
        
        // Save completion state
        PlayerPrefs.SetInt("TutorialCompleted", 1);
        PlayerPrefs.Save();
        
        Debug.Log("Tutorial Complete! Player has learned all basic controls.");
        
        // Show completion message
        StartCoroutine(ShowCompletionMessage());
    }

    // ============================================================
    // HINT DISPLAY
    // ============================================================

    private IEnumerator DelayedShowHint(float delay, TutorialStep step)
    {
        yield return new WaitForSeconds(delay);
        ShowHint(step);
    }

    private void ShowHint(TutorialStep step)
    {
        if (tutorialHintPanel == null || tutorialText == null) return;
        
        // Set hint text and icon based on step
        string hintText = GetHintText(step);
        Sprite hintIcon = GetHintIcon(step);
        
        tutorialText.text = hintText;
        
        if (tutorialIcon != null && hintIcon != null)
        {
            tutorialIcon.sprite = hintIcon;
            tutorialIcon.gameObject.SetActive(true);
        }
        else if (tutorialIcon != null)
        {
            tutorialIcon.gameObject.SetActive(false);
        }
        
        // Fade in
        tutorialHintPanel.SetActive(true);
        if (currentHintCoroutine != null)
        {
            StopCoroutine(currentHintCoroutine);
        }
        currentHintCoroutine = StartCoroutine(FadeHint(true));
        
        Debug.Log($"Showing tutorial hint: {hintText}");
    }

    private IEnumerator HideHint()
    {
        if (tutorialCanvasGroup == null) yield break;
        
        yield return StartCoroutine(FadeHint(false));
        
        if (tutorialHintPanel != null)
        {
            tutorialHintPanel.SetActive(false);
        }
    }

    private IEnumerator FadeHint(bool fadeIn)
    {
        if (tutorialCanvasGroup == null) yield break;
        
        float duration = fadeIn ? hintFadeInDuration : hintFadeOutDuration;
        float startAlpha = tutorialCanvasGroup.alpha;
        float targetAlpha = fadeIn ? 1f : 0f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            tutorialCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }
        
        tutorialCanvasGroup.alpha = targetAlpha;
    }

    private IEnumerator ShowCompletionMessage()
    {
        if (tutorialText == null) yield break;
        
        tutorialText.text = "🎉 Tutorial Complete! Keep skating!";
        
        if (tutorialIcon != null)
        {
            tutorialIcon.gameObject.SetActive(false);
        }
        
        tutorialHintPanel.SetActive(true);
        yield return StartCoroutine(FadeHint(true));
        
        yield return new WaitForSeconds(3f);
        
        yield return StartCoroutine(HideHint());
    }

    // ============================================================
    // HINT TEXT & ICONS
    // ============================================================

    private string GetHintText(TutorialStep step)
    {
        // Detect if using touch controls
        bool isTouchDevice = Input.touchSupported && Application.isMobilePlatform;
        
        switch (step)
        {
            case TutorialStep.Push:
                return isTouchDevice 
                    ? "Tap to push and gain speed" 
                    : "Click or Tap to push and gain speed";
            
            case TutorialStep.Jump:
                return isTouchDevice 
                    ? "⬆Swipe up to jump" 
                    : "⬆Hold Left-Click and swipe up to jump";
            
            case TutorialStep.BasicTrick:
                return isTouchDevice 
                    ? "Swipe left/right in the air for tricks" 
                    : "Swipe left/right while airborne for tricks";
            
            case TutorialStep.RightClickTrick:
                return isTouchDevice
                    ? "Double-tap to spin (PopShoveIt / TreFlip)"
                    : "Right-click to spin (PopShoveIt / TreFlip)";
            
            case TutorialStep.Dodge:
                return isTouchDevice
                    ? "Tap ← → buttons to dodge left / right"
                    : "Press A / D to dodge left / right";
            
            case TutorialStep.Combo:
                return "Chain tricks without landing to build combos!";
            
            default:
                return "";
        }
    }

    private Sprite GetHintIcon(TutorialStep step)
    {
        switch (step)
        {
            case TutorialStep.Push:
                return pushIcon;
            case TutorialStep.Jump:
                return jumpIcon;
            case TutorialStep.BasicTrick:
                return trickIcon;
            case TutorialStep.RightClickTrick:
                return trickIcon;
            case TutorialStep.Combo:
                return comboIcon;
            default:
                return null;
        }
    }

    // ============================================================
    // PUBLIC UTILITY
    // ============================================================

    /// <summary>
    /// Check if tutorial is currently active
    /// </summary>
    public bool IsTutorialActive()
    {
        return tutorialActive;
    }

    /// <summary>
    /// Check if tutorial has been completed (ever)
    /// </summary>
    public bool IsTutorialCompleted()
    {
        return tutorialCompleted;
    }

    /// <summary>
    /// Reset tutorial progress (for testing or new player)
    /// </summary>
    public void ResetTutorial()
    {
        PlayerPrefs.SetInt("TutorialCompleted", 0);
        PlayerPrefs.Save();
        tutorialCompleted = false;
        completedSteps.Clear();
        pushCount = 0;
        jumpCount = 0;
        trickCount = 0;
        comboCount = 0;
        dodgeCount = 0;
        Debug.Log("Tutorial progress reset");
    }
}