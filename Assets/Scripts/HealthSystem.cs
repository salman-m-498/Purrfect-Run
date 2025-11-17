using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Player HealthSystem: supports taking damage, healing, regeneration,
/// invulnerability frames and exposes events for UI and death handling.
/// Enemies and pickup objects should call `ApplyDamage(...)` or `RestoreHealth(...)`.
/// </summary>
public class HealthSystem : MonoBehaviour
{
    [Header("Health Settings")]
    [Tooltip("Maximum health value")]
    public float maxHealth = 100f;

    [Tooltip("Starting & current health")]
    [SerializeField]
    private float currentHealth = 100f;

    [Tooltip("Base health regeneration per second")]
    public float healthRegen = 5f;

    [Tooltip("Additional regen per second when grounded")]
    public float groundedRegenBonus = 3f;

    [Header("Damage / Invulnerability")]
    [Tooltip("Seconds of invulnerability after taking damage")]
    public float invulnerabilitySeconds = 0.5f;

    [Tooltip("If true the player is currently invulnerable and ignores damage")]
    public bool isInvulnerable { get; private set; }

    [Header("References")]
    public GameManager gameManager;
    private PlayerController playerController;
    private DollyCam cam;

    [Header("Events")]
    // float current, float max
    public UnityEvent<float, float> OnHealthChanged;
    public UnityEvent OnDeath;

    private bool isGrounded = false;
    private bool isDead = false;

    private Coroutine invulCoroutine;

    private void Awake()
    {
        // Ensure starting health is valid
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        // Attempt to find GameManager automatically
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();
    }

    private void Start()
    {
        // Grab references from GameManager if available
        if (gameManager != null)
        {
            playerController ??= gameManager.playerController;
            cam ??= gameManager.cam;
        }

        // Fire initial health event so UI can register
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Update()
    {
        HandleHealthRegen();
        // Visual/effect hooks are invoked when health changes via events.
    }

    /// <summary>
    /// Apply damage to the player. Enemies should call this.
    /// Returns true if damage was applied (not ignored due to invuln/dead).
    /// </summary>
    public bool ApplyDamage(float amount, GameObject source = null)
    {
        if (isDead) return false;
        if (isInvulnerable) return false;

        if (amount <= 0f) return false;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        // Notify listeners (UI, etc.)
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
        {
            Die(source);
        }
        else
        {
            // Start brief invulnerability to avoid instant follow-up hits
            if (invulCoroutine != null) StopCoroutine(invulCoroutine);
            invulCoroutine = StartCoroutine(InvulnerabilityRoutine(invulnerabilitySeconds));
        }

        return true;
    }

    /// <summary>
    /// Restore health by the given amount. Clamped to maxHealth.
    /// Can be called by healing pickups or abilities.
    /// </summary>
    public void RestoreHealth(float amount)
    {
        if (isDead) return;
        if (amount <= 0f) return;

        float prev = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);

        if (!Mathf.Approximately(prev, currentHealth))
        {
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }

    /// <summary>
    /// Instantly fill health to max.
    /// </summary>
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die(GameObject source)
    {
        isDead = true;
        // Fire death event for other systems to react
        OnDeath?.Invoke();

        // Example: notify GameManager / PlayerController (if needed)
        if (playerController == null && gameManager != null)
            playerController = gameManager.playerController;

        if (playerController != null)
        {
            playerController.enabled = false; // simple default action
        }

        // Additional death handling (animations, respawn) should be handled by listeners.
    }

    private IEnumerator InvulnerabilityRoutine(float seconds)
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(seconds);
        isInvulnerable = false;
        invulCoroutine = null;
    }

    /// <summary>
    /// Health regeneration handled here. If grounded, regen is higher.
    /// </summary>
    private void HandleHealthRegen()
    {
        if (isDead) return;
        if (currentHealth >= maxHealth) return;

        float regenThisFrame = healthRegen * Time.deltaTime;
        if (isGrounded)
            regenThisFrame += groundedRegenBonus * Time.deltaTime;

        if (regenThisFrame > 0f)
        {
            float prev = currentHealth;
            currentHealth = Mathf.Clamp(currentHealth + regenThisFrame, 0f, maxHealth);
            if (!Mathf.Approximately(prev, currentHealth))
                OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }

    /// <summary>
    /// External systems (PlayerController) should call this to indicate grounded state.
    /// </summary>
    public void SetGrounded(bool grounded)
    {
        isGrounded = grounded;
    }

    /// <summary>
    /// Helper: return current health value.
    /// </summary>
    public float GetCurrentHealth() => currentHealth;

    /// <summary>
    /// Helper: return whether player is dead.
    /// </summary>
    public bool IsDead() => isDead;

    // Optional debug helpers or visual/effect hooks could be added below.
}