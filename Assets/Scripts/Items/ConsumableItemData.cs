using UnityEngine;

/// <summary>
/// ConsumableItemData: ScriptableObject defining a consumable item's properties and effects.
/// Each consumable is a single prefab with a trigger collider that applies this data on pickup.
/// </summary>
[CreateAssetMenu(menuName = "Items/Consumable", fileName = "Consumable_")]
public class ConsumableItemData : ScriptableObject
{
    public enum EffectType
    {
        RestoreHealth,
        RestoreStamina,
        SpeedBoost,
        Invincibility,
        MagnetCoins,
        Shield,
        ComboExtend,
        FlatScore
    }

    [Header("Display")]
    public string itemName = "Consumable";
    public string description = "A one-time use item";
    [TextArea(3, 5)]
    public string longDescription = "";
    public Sprite icon;
    public Color iconColor = Color.white;

    [Header("Physics")]
    public float spawnHeight = 0.5f;
    public float despawnTime = 30f; // Despawn after 30 seconds if not picked up

    [Header("Pickup Behavior")]
    public EffectType effectType = EffectType.RestoreHealth;
    public float effectValue = 20f; // Health restored, stamina restored, speed multiplier, duration, etc.
    public float effectDuration = 0f; // 0 = instant, >0 = temporary buff duration
    public AudioClip pickupSound;
    public float pickupSoundVolume = 1f;

    [Header("Visuals")]
    public GameObject pickupVisualPrefab; // Optional: spawn visual effect on pickup
    public ParticleSystem pickupEffectPrefab; // Optional: particle effect

    [Header("Physics")]
    public float bounceHeight = 2f;
    public float bobSpeed = 2f; // Bobbing animation
    public float rotationSpeed = 90f; // Degrees per second

    public string GetDescription()
    {
        return $"{itemName}: {description}";
    }

    public string GetEffectDescription()
    {
        return effectType switch
        {
            EffectType.RestoreHealth => $"Restore {effectValue} health",
            EffectType.RestoreStamina => $"Restore {effectValue} stamina",
            EffectType.SpeedBoost => $"+{(effectValue - 1f) * 100}% speed for {effectDuration}s",
            EffectType.Invincibility => $"Invincible for {effectDuration} seconds",
            EffectType.MagnetCoins => $"Pull nearby coins for {effectDuration} seconds",
            EffectType.Shield => $"Absorb one hit",
            EffectType.ComboExtend => $"Extend combo window for {effectDuration}s",
            EffectType.FlatScore => $"+{effectValue} score",
            _ => "Unknown effect"
        };
    }
}
