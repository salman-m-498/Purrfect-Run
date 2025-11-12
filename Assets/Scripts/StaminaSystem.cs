using UnityEngine;
using UnityEngine.UI;

public class StaminaSystem : MonoBehaviour
{
    [Header("Balanced Stamina Settings")]
    public float maxStamina = 100f;
    public float currentStamina = 100f;
    public float staminaRegenRate = 3f;      // Base regen per second when grounded
    public float staminaDecayRate = 0.2f;    // Passive decay
    public float lowStaminaThreshold = 20f;  // Tricks disabled if below this
    public float criticalStaminaThreshold = 30f; // Visual effects start

    [Header("Action Costs / Gains")]
    public float pushRestore = 15f;          // Push restores stamina
    public float grindRegenRate = 5f;        // Regen per second while grinding
    public float perfectLandBonus = 10f;     // Bonus for perfect landings
    public float basicTrickCost = 15f;       // Simple trick cost
    public float complexTrickCost = 25f;     // Complex trick cost
    public float grindStartCost = 5f;        // Starting grind cost
    public float jumpCost = 4f;              // Ollie/jump cost
    public float specialTrickCost = 40f;     // Special moves
    public float boostCost = 20f;            // Optional boost mechanic

    [Header("Visual Effects")]
    private StaminaBarUI staminaBarUI;        // Reference to the UI component

    [Header("References")]
    public GameManager gameManager;
    private PlayerController playerController;
    private Camera mainCamera;                // Optional: camera effects for low stamina

    private bool isGrinding = false;
    private bool isGrounded = true;
    private bool isPerformingTrick = false;
    private float lastPushTime = 0f;
    private const float PUSH_COOLDOWN = 0.5f; // Prevent stamina farming

    private void Awake()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();
    }

    private void Start()
    {
        if (playerController == null)
            playerController = gameManager.playerController;

        staminaBarUI ??= gameManager.staminaBarUI;
        mainCamera ??= gameManager.mainCamera;

        if (staminaBarUI == null)
        {
            InitializeUI();
        }
    }

    private void InitializeUI()
    {
        if (staminaBarUI == null)
        {
            Canvas targetCanvas = FindObjectOfType<Canvas>();
            if (targetCanvas != null)
            {
                staminaBarUI = StaminaBarUI.CreateStaminaBar(targetCanvas.transform);
            }
            else
            {
                Debug.LogError("No Canvas found for Stamina Bar UI!");
            }
        }

        currentStamina = maxStamina;
        UpdateVisuals();
    }

    private void Update()
    {
        // Passive decay
        ModifyStamina(-staminaDecayRate * Time.deltaTime);

        // Regeneration when grounded and not performing tricks
        if (isGrounded && !isPerformingTrick)
        {
            float regen = isGrinding ? grindRegenRate : staminaRegenRate;
            ModifyStamina(+regen * Time.deltaTime);
        }

        UpdateVisuals();
        UpdateEffects();
    }

    /// <summary>
    /// Modify stamina by a certain amount. Positive consumes, negative restores.
    /// </summary>
    public void ModifyStamina(float amount)
    {
        float previousStamina = currentStamina;
        currentStamina = Mathf.Clamp(currentStamina - amount, 0f, maxStamina); // Subtract positive cost, add negative regen

        // Trick availability state
        if (previousStamina >= lowStaminaThreshold && currentStamina < lowStaminaThreshold)
        {
            DisableTricks();
        }
        else if (previousStamina < lowStaminaThreshold && currentStamina >= lowStaminaThreshold)
        {
            EnableTricks();
        }
    }

    public bool HasEnoughStamina(float cost)
    {
        return currentStamina >= cost;
    }

    // ===================== Player Actions =====================

    public void OnPush()
    {
        if (Time.time - lastPushTime < PUSH_COOLDOWN) return;

        // Diminishing returns at high stamina
        float actualRestore = pushRestore;
        if (currentStamina > 80f)
        {
            actualRestore *= 1f - ((currentStamina - 80f) / 20f);
        }

        ModifyStamina(-actualRestore); // Negative = restore
        lastPushTime = Time.time;
    }

    public void OnTrickStart(bool isComplex)
    {
        float cost = isComplex ? complexTrickCost : basicTrickCost;
        if (HasEnoughStamina(cost))
        {
            ModifyStamina(cost); // Positive cost = consume
            isPerformingTrick = true;
        }
    }

    public void OnTrickEnd()
    {
        isPerformingTrick = false;
    }

    public void OnGrindStart()
    {
        if (HasEnoughStamina(grindStartCost))
        {
            ModifyStamina(grindStartCost);
            isGrinding = true;
        }
    }

    public void OnGrindEnd()
    {
        isGrinding = false;
    }

    public void OnJump()
    {
        if (HasEnoughStamina(jumpCost))
            ModifyStamina(jumpCost);
    }

    public void OnPerfectLand()
    {
        ModifyStamina(-perfectLandBonus); // Negative = restore
    }

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

    public void SetGrounded(bool grounded)
    {
        isGrounded = grounded;
    }

    // ===================== Visual & Feedback =====================

    private void UpdateVisuals()
    {
        if (staminaBarUI != null)
        {
            staminaBarUI.UpdateStamina(currentStamina, maxStamina);
        }
    }

    private void UpdateEffects()
    {
        if (mainCamera != null)
        {
            // Optional: add subtle effects for low stamina
            if (currentStamina < criticalStaminaThreshold)
            {
                // e.g., camera shake, desaturation, vignette
            }
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
}
