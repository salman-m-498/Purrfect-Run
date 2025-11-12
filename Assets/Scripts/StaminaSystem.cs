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
    [Tooltip("Base stamina regeneration per second when grounded")]
    public float staminaRegenRate = 3f;
    [Tooltip("Passive stamina decay per second")]
    public float staminaDecayRate = 0.2f;
    [Tooltip("Stamina level below which tricks are disabled")]
    public float lowStaminaThreshold = 20f;
    [Tooltip("Stamina level below which visual effects start")]
    public float criticalStaminaThreshold = 30f;

    [Header("Action Costs / Gains")]
    public float pushRestore = 15f;
    public float grindRegenRate = 5f;
    public float perfectLandBonus = 10f;
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
        // Passive stamina decay over time
        ModifyStamina(staminaDecayRate * Time.deltaTime);
    }

    private void HandleStaminaRegen()
    {
        if (isGrounded && !isPerformingTrick)
        {
            float regenAmount = isGrinding ? grindRegenRate : staminaRegenRate;
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
    }

    public void OnTrickStart(bool isComplex)
    {
        float cost = isComplex ? complexTrickCost : basicTrickCost;
        if (HasEnoughStamina(cost))
        {
            ModifyStamina(cost); // Positive = consume
            isPerformingTrick = true;
        }
    }

    public void OnTrickEnd() => isPerformingTrick = false;

    public void OnGrindStart()
    {
        if (HasEnoughStamina(grindStartCost))
        {
            ModifyStamina(grindStartCost);
            isGrinding = true;
        }
    }

    public void OnGrindEnd() => isGrinding = false;

    public void OnJump()
    {
        if (HasEnoughStamina(jumpCost))
            ModifyStamina(jumpCost);
    }

    public void OnPerfectLand() => ModifyStamina(-perfectLandBonus);

    public void OnSpecialTrick()
    {
        if (HasEnoughStamina(specialTrickCost))
            ModifyStamina(specialTrickCost);
    }

    public void OnBoost()
    {
        if (HasEnoughStamina(boostCost))
            ModifyStamina(boostCost);
    }

    public void SetGrounded(bool grounded) => isGrounded = grounded;

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
            playerController.canOllie = false;
    }

    private void EnableTricks()
    {
        if (playerController != null)
            playerController.canOllie = true;
    }

    public void ResetStamina() { }
}
