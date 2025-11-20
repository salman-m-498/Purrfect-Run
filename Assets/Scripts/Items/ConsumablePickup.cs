using UnityEngine;
using System.Collections;

/// <summary>
/// ConsumablePickup: Pickup implementation for consumable items.
/// Applies the effect defined in ConsumableItemData.
/// Handles temporary buffs with coroutines.
/// </summary>
public class ConsumablePickup : PickupBase
{
    [Header("Consumable Data")]
    [SerializeField] private ConsumableItemData consumableData;

    private PlayerStats playerStats;
    private StaminaSystem staminaSystem;
    private HealthSystem healthSystem;
    private ScoreSystem scoreSystem;
    private PlayerController playerController;

    protected override void Start()
    {
        base.Start();

        if (consumableData == null)
        {
            Debug.LogError($"ConsumablePickup on {gameObject.name} has no ConsumableItemData assigned!");
            Destroy(gameObject);
        }
    }

    protected override void ApplyEffect(PlayerController player)
    {
        playerController = player;
        playerStats = player.GetComponent<PlayerStats>();
        staminaSystem = player.GetComponent<StaminaSystem>();
        healthSystem = player.GetComponent<HealthSystem>();
        scoreSystem = FindObjectOfType<ScoreSystem>();

        switch (consumableData.effectType)
        {
            case ConsumableItemData.EffectType.RestoreHealth:
                ApplyHealthRestoration();
                break;

            case ConsumableItemData.EffectType.RestoreStamina:
                ApplyStaminaRestoration();
                break;

            case ConsumableItemData.EffectType.SpeedBoost:
                ApplySpeedBoost();
                break;

            case ConsumableItemData.EffectType.Invincibility:
                ApplyInvincibility();
                break;

            case ConsumableItemData.EffectType.MagnetCoins:
                ApplyMagnet();
                break;

            case ConsumableItemData.EffectType.Shield:
                ApplyShield();
                break;

            case ConsumableItemData.EffectType.ComboExtend:
                ApplyComboExtend();
                break;

            case ConsumableItemData.EffectType.FlatScore:
                ApplyFlatScore();
                break;
        }

        Debug.Log($"Picked up: {consumableData.itemName}");
    }

    private void ApplyHealthRestoration()
    {
        if (healthSystem != null)
        {
            healthSystem.RestoreHealth(consumableData.effectValue);
        }
    }

    private void ApplyStaminaRestoration()
    {
        if (staminaSystem != null)
        {
            staminaSystem.ModifyStamina(consumableData.effectValue);
        }
    }

    private void ApplySpeedBoost()
    {
        if (playerController != null)
        {
            StartCoroutine(TemporarySpeedBoost());
        }
    }

    private IEnumerator TemporarySpeedBoost()
    {
        // This would need integration with PlayerController's speed system
        // For now, log the effect
        Debug.Log($"Speed boosted by {(consumableData.effectValue - 1f) * 100}% for {consumableData.effectDuration}s");
        yield return new WaitForSeconds(consumableData.effectDuration);
        Debug.Log("Speed boost expired");
    }

    private void ApplyInvincibility()
    {
        if (healthSystem != null)
        {
            StartCoroutine(TemporaryInvincibility());
        }
    }

    private IEnumerator TemporaryInvincibility()
    {
        if (healthSystem != null)
        {
            healthSystem.SetInvulnerable(true);
            yield return new WaitForSeconds(consumableData.effectDuration);
            healthSystem.SetInvulnerable(false);
        }
    }

    private void ApplyMagnet()
    {
        // Attract coins in area
        StartCoroutine(MagnetEffect());
    }

    private IEnumerator MagnetEffect()
    {
        Debug.Log($"Magnet active for {consumableData.effectDuration}s");
        // TODO: Find all coins in area and pull them to player
        yield return new WaitForSeconds(consumableData.effectDuration);
        Debug.Log("Magnet deactivated");
    }

    private void ApplyShield()
    {
        if (healthSystem != null)
        {
            healthSystem.ApplyShield();
        }
    }

    private void ApplyComboExtend()
    {
        // Extend combo window - integration with ComboSystem
        Debug.Log($"Combo window extended for {consumableData.effectDuration}s");
    }

    private void ApplyFlatScore()
    {
        if (scoreSystem != null)
        {
            scoreSystem.AddScore((int)consumableData.effectValue);
        }
    }

    protected override void PlayPickupFeedback()
    {
        base.PlayPickupFeedback();

        // Play pickup sound
        if (consumableData.pickupSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(consumableData.pickupSound, consumableData.pickupSoundVolume);
        }

        // Spawn visual effect if available
        if (consumableData.pickupEffectPrefab != null)
        {
            Instantiate(consumableData.pickupEffectPrefab, transform.position, Quaternion.identity);
        }
    }
}
