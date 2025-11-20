using UnityEngine;

/// <summary>
/// PlayerStats: Central source of truth for all player attributes.
/// All systems read from this component to ensure consistency.
/// Permanent items modify these stats at run start.
/// Consumables and systems can read/query these stats.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    [System.Serializable]
    public struct StatModifiers
    {
        public float healthMultiplier;
        public float staminaMultiplier;
        public float speedMultiplier;
        public float jumpHeightMultiplier;
        public float comboWindowMultiplier;
        public float coinMultiplier;
        public int extraLives;
        public float damageReduction; // 0.0 = no reduction, 0.5 = 50% reduction
    }

    [Header("Base Stats")]
    [SerializeField] private float baseMaxHealth = 100f;
    [SerializeField] private float baseMaxStamina = 100f;
    [SerializeField] private float baseSpeed = 10f;
    [SerializeField] private float baseJumpHeight = 5f;
    [SerializeField] private float baseComboWindow = 2f;
    [SerializeField] private float baseCoinMultiplier = 1f;
    [SerializeField] private int baseExtraLives = 0;

    [Header("Current Run")]
    [SerializeField] private float currentHealth;
    [SerializeField] private float currentStamina;

    private StatModifiers currentModifiers;
    private StatModifiers baseModifiers;

    // Events
    public System.Action<float, float> OnHealthChanged; // current, max
    public System.Action<float, float> OnStaminaChanged; // current, max
    public System.Action OnStatsModified;

    private void Awake()
    {
        ResetToBaseStats();
    }

    /// <summary>
    /// Reset stats to base values and reapply all permanent item modifiers.
    /// Call this at the start of each run.
    /// </summary>
    public void ResetToBaseStats()
    {
        currentHealth = GetMaxHealth();
        currentStamina = GetMaxStamina();
        
        // Reset modifiers to neutral (1.0x multipliers, 0 additions)
        baseModifiers = new StatModifiers
        {
            healthMultiplier = 1f,
            staminaMultiplier = 1f,
            speedMultiplier = 1f,
            jumpHeightMultiplier = 1f,
            comboWindowMultiplier = 1f,
            coinMultiplier = 1f,
            extraLives = 0,
            damageReduction = 0f
        };
        currentModifiers = baseModifiers;

        OnStatsModified?.Invoke();
    }

    /// <summary>
    /// Apply permanent item modifiers. Call after ResetToBaseStats() or load from save.
    /// </summary>
    public void ApplyModifier(StatModifiers modifier)
    {
        currentModifiers.healthMultiplier *= modifier.healthMultiplier;
        currentModifiers.staminaMultiplier *= modifier.staminaMultiplier;
        currentModifiers.speedMultiplier *= modifier.speedMultiplier;
        currentModifiers.jumpHeightMultiplier *= modifier.jumpHeightMultiplier;
        currentModifiers.comboWindowMultiplier *= modifier.comboWindowMultiplier;
        currentModifiers.coinMultiplier *= modifier.coinMultiplier;
        currentModifiers.extraLives += modifier.extraLives;
        currentModifiers.damageReduction += modifier.damageReduction;

        // Clamp damage reduction to 0-1
        currentModifiers.damageReduction = Mathf.Clamp01(currentModifiers.damageReduction);

        OnStatsModified?.Invoke();
    }

    /// <summary>
    /// Get final calculated max health (base × multiplier)
    /// </summary>
    public float GetMaxHealth() => baseMaxHealth * currentModifiers.healthMultiplier;

    /// <summary>
    /// Get final calculated max stamina (base × multiplier)
    /// </summary>
    public float GetMaxStamina() => baseMaxStamina * currentModifiers.staminaMultiplier;

    /// <summary>
    /// Get final calculated speed (base × multiplier)
    /// </summary>
    public float GetSpeed() => baseSpeed * currentModifiers.speedMultiplier;

    /// <summary>
    /// Get final calculated jump height (base × multiplier)
    /// </summary>
    public float GetJumpHeight() => baseJumpHeight * currentModifiers.jumpHeightMultiplier;

    /// <summary>
    /// Get final calculated combo window (base × multiplier)
    /// </summary>
    public float GetComboWindow() => baseComboWindow * currentModifiers.comboWindowMultiplier;

    /// <summary>
    /// Get final calculated coin multiplier
    /// </summary>
    public float GetCoinMultiplier() => currentModifiers.coinMultiplier;

    /// <summary>
    /// Get total extra lives from permanent items
    /// </summary>
    public int GetExtraLives() => currentModifiers.extraLives;

    /// <summary>
    /// Get damage reduction amount (0-1 scale)
    /// </summary>
    public float GetDamageReduction() => currentModifiers.damageReduction;

    // Health Management
    public void SetHealth(float value)
    {
        currentHealth = Mathf.Clamp(value, 0f, GetMaxHealth());
        OnHealthChanged?.Invoke(currentHealth, GetMaxHealth());
    }

    public void AddHealth(float amount)
    {
        SetHealth(currentHealth + amount);
    }

    public void TakeDamage(float damage)
    {
        // Apply damage reduction
        float reducedDamage = damage * (1f - currentModifiers.damageReduction);
        SetHealth(currentHealth - reducedDamage);
    }

    public float GetCurrentHealth() => currentHealth;

    // Stamina Management
    public void SetStamina(float value)
    {
        currentStamina = Mathf.Clamp(value, 0f, GetMaxStamina());
        OnStaminaChanged?.Invoke(currentStamina, GetMaxStamina());
    }

    public void AddStamina(float amount)
    {
        SetStamina(currentStamina + amount);
    }

    public void ConsumeStamina(float amount)
    {
        SetStamina(currentStamina - amount);
    }

    public float GetCurrentStamina() => currentStamina;

    /// <summary>
    /// Get all current modifiers as a struct
    /// </summary>
    public StatModifiers GetCurrentModifiers() => currentModifiers;

    /// <summary>
    /// Check if player is at full health
    /// </summary>
    public bool IsFullHealth() => currentHealth >= GetMaxHealth();

    /// <summary>
    /// Check if player is at full stamina
    /// </summary>
    public bool IsFullStamina() => currentStamina >= GetMaxStamina();

    /// <summary>
    /// Check if player is alive
    /// </summary>
    public bool IsAlive() => currentHealth > 0f;

#if UNITY_EDITOR
    [ContextMenu("Log Current Stats")]
    public void LogStats()
    {
        Debug.Log($"=== PLAYER STATS ===\n" +
            $"Health: {GetCurrentHealth()}/{GetMaxHealth()}\n" +
            $"Stamina: {GetCurrentStamina()}/{GetMaxStamina()}\n" +
            $"Speed: {GetSpeed()}\n" +
            $"Jump Height: {GetJumpHeight()}\n" +
            $"Combo Window: {GetComboWindow()}\n" +
            $"Coin Multiplier: {GetCoinMultiplier()}\n" +
            $"Extra Lives: {GetExtraLives()}\n" +
            $"Damage Reduction: {GetDamageReduction() * 100}%");
    }
#endif
}
