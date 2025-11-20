using UnityEngine;

/// <summary>
/// Handles player stamina for tricks, jumps, grinding, and boosts.
/// Integrates with UIManager for visual updates.
/// </summary>
public class StaminaSystem : MonoBehaviour
{
    [Header("Stamina Settings")]
    public float maxStamina = 100f;
    public float currentStamina = 100f;
    [Tooltip("Base stamina regeneration per second")]
    public float staminaRegenRate = 5f;
    [Tooltip("Bonus regen when grounded")]
    public float groundedRegenBonus = 2f;
    [Tooltip("Passive stamina decay per second")]
    public float staminaDecayRate = 0.2f;
    [Tooltip("Stamina level below which tricks are disabled")]
    public float lowStaminaThreshold = 20f;
    [Tooltip("Stamina level below which visual effects start")]
    public float criticalStaminaThreshold = 30f;

    [Header("Action Costs / Gains")]
    public float pushRestore = 15f;
    public float grindRegenRate = 5f;
    public float perfectLandBonus = 15f;
    public float basicTrickCost = 15f;
    public float complexTrickCost = 25f;
    public float grindStartCost = 5f;
    public float jumpCost = 4f;
    public float specialTrickCost = 40f;
    public float boostCost = 20f;

    [Header("References")]
    public GameManager gameManager;
    private PlayerController playerController;
    private DollyCam cam;

    private bool isGrinding = false;
    private bool isGrounded = true;
    private bool isPerformingTrick = false;
    private float lastPushTime = 0f;
    private const float PUSH_COOLDOWN = 0.5f;

    private void Awake()
    {
        // Automatically find GameManager if not assigned
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();
    }

    private void Start()
    {
        // Grab references from GameManager
        playerController ??= gameManager.playerController;
        cam ??= gameManager.cam;

        // Ensure UIManager exists
        if (UIManager.Instance == null)
        {
            Debug.LogError("UIManager not found! Add a UIManager to the scene.");
        }
    }

    private void Update()
    {
        HandleStaminaDecay();
        HandleStaminaRegen();
        UpdateVisuals();
        UpdateEffects();
    }

    // ===================== STAMINA MODIFIERS =====================

    /// <summary>
    /// Modify stamina by a certain amount.
    /// Positive values consume stamina, negative values restore stamina.
    /// </summary>
    public void ModifyStamina(float amount)
    {
        float previous = currentStamina;

        // Subtract positive cost, add negative (restoration)
        currentStamina = Mathf.Clamp(currentStamina - amount, 0f, maxStamina);

        // Handle trick availability when crossing thresholds
        if (previous >= lowStaminaThreshold && currentStamina < lowStaminaThreshold)
            DisableTricks();
        else if (previous < lowStaminaThreshold && currentStamina >= lowStaminaThreshold)
            EnableTricks();
    }

    public bool HasEnoughStamina(float cost) => currentStamina >= cost;

    private void HandleStaminaDecay()
    {
        // Only decay when performing tricks
        if (isPerformingTrick)
        {
            ModifyStamina(staminaDecayRate * Time.deltaTime);
        }
    }

    private void HandleStaminaRegen()
    {
        // Always regenerate when not performing tricks
        if (!isPerformingTrick)
        {
            float regenAmount = staminaRegenRate;
            
            // Bonus regen when grinding
            if (isGrinding)
            {
                regenAmount += grindRegenRate;
            }
            // Bonus regen when grounded
            else if (isGrounded)
            {
                regenAmount += groundedRegenBonus;
            }
            
            ModifyStamina(-regenAmount * Time.deltaTime); // Negative = restore
        }
    }

    // ===================== PLAYER ACTIONS =====================

    public void OnPush()
    {
        if (Time.time - lastPushTime < PUSH_COOLDOWN) return;

        // Reduce restoration if stamina is already high
        float actualRestore = pushRestore;
        if (currentStamina > 80f)
            actualRestore *= 1f - ((currentStamina - 80f) / 20f);

        ModifyStamina(-actualRestore); // Negative = restore
        lastPushTime = Time.time;
        
        Debug.Log($"Push restore: +{actualRestore} stamina (Now: {currentStamina:F1})");
    }

    public void OnTrickStart(bool isComplex)
    {
        float cost = isComplex ? complexTrickCost : basicTrickCost;
        if (HasEnoughStamina(cost))
        {
            ModifyStamina(cost); // Positive = consume
            isPerformingTrick = true;
            Debug.Log($"Trick started: -{cost} stamina (Now: {currentStamina:F1})");
        }
    }

    public void OnTrickEnd()
    {
        isPerformingTrick = false;
        Debug.Log($"Trick ended. Stamina will now regenerate.");
    }

    public void OnGrindStart()
    {
        if (HasEnoughStamina(grindStartCost))
        {
            ModifyStamina(grindStartCost);
            isGrinding = true;
            Debug.Log($"Grind started: -{grindStartCost} stamina (Now: {currentStamina:F1})");
        }
    }

    public void OnGrindEnd()
    {
        isGrinding = false;
        Debug.Log($"Grind ended. Stamina: {currentStamina:F1}");
    }

    public void OnJump()
    {
        if (HasEnoughStamina(jumpCost))
        {
            ModifyStamina(jumpCost);
            Debug.Log($"Jump: -{jumpCost} stamina (Now: {currentStamina:F1})");
        }
    }

    public void OnPerfectLand()
    {
        ModifyStamina(-perfectLandBonus); // Negative = restore
        Debug.Log($"⭐ PERFECT LANDING! +{perfectLandBonus} stamina bonus (Now: {currentStamina:F1})");
    }

    public void OnSpecialTrick()
    {
        if (HasEnoughStamina(specialTrickCost))
        {
            ModifyStamina(specialTrickCost);
            Debug.Log($"Special trick: -{specialTrickCost} stamina (Now: {currentStamina:F1})");
        }
    }

    public void OnBoost()
    {
        if (HasEnoughStamina(boostCost))
        {
            ModifyStamina(boostCost);
            Debug.Log($"Boost: -{boostCost} stamina (Now: {currentStamina:F1})");
        }
    }

    public void SetGrounded(bool grounded)
    {
        isGrounded = grounded;
    }

    // ===================== VISUALS & FEEDBACK =====================

    private void UpdateVisuals()
    {
        // Update the stamina bar through UIManager
        if (UIManager.Instance != null)
            UIManager.Instance.UpdateStamina(currentStamina, maxStamina);
    }

    private void UpdateEffects()
    {
        if (cam != null && currentStamina < criticalStaminaThreshold)
        {
            // Optional: low stamina effects (camera shake, vignette, desaturation)
        }
    }

    private void DisableTricks()
    {
        if (playerController != null)
        {
            playerController.canOllie = false;
            Debug.LogWarning("⚠️ Low stamina! Tricks disabled.");
        }
    }

    private void EnableTricks()
    {
        if (playerController != null)
        {
            playerController.canOllie = true;
            Debug.Log("✓ Stamina recovered! Tricks enabled.");
        }
    }

    public void ResetStamina()
    {
        currentStamina = maxStamina;
        isGrinding = false;
        isPerformingTrick = false;
        Debug.Log("Stamina reset to full.");
    }
}