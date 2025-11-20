using UnityEngine;

/// <summary>
/// PermanentItemData: ScriptableObject defining a permanent upgrade item.
/// These items are unlocked, stored in inventory, and apply their bonuses every run.
/// </summary>
[CreateAssetMenu(menuName = "Items/Permanent Item", fileName = "PermanentItem_")]
public class PermanentItemData : ScriptableObject
{
    [Header("Identity")]
    public string itemName = "Item Name";
    [TextArea(2, 4)]
    public string description = "Item description";
    public Sprite icon;
    public Color iconColor = Color.white;
    public int itemId; // Unique ID for save system

    public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }
    [Header("Rarity")]
    public Rarity rarity = Rarity.Common;

    public enum ItemType
    {
        MaxHealthBoost,
        MaxStaminaBoost,
        SpeedBoost,
        JumpHeightBoost,
        ComboWindowBoost,
        CoinMultiplierBoost,
        ExtraLife,
        DamageReduction,
        GrindingStability,
        LandingComfort
    }
    [Header("Item Type")]
    public ItemType itemType = ItemType.MaxHealthBoost;

    [Header("Effect Values")]
    [Tooltip("Multiplier for stat increases (e.g., 1.1 = +10%)")]
    public float statMultiplier = 1.1f;
    [Tooltip("Flat increase to stats (additive)")]
    public float statFlatBonus = 0f;

    [Header("UI")]
    public string rarityColor;

    /// <summary>
    /// Convert this item data to PlayerStats modifier values
    /// </summary>
    public PlayerStats.StatModifiers GetStatModifier()
    {
        PlayerStats.StatModifiers modifier = new PlayerStats.StatModifiers();

        switch (itemType)
        {
            case ItemType.MaxHealthBoost:
                modifier.healthMultiplier = statMultiplier;
                break;
            case ItemType.MaxStaminaBoost:
                modifier.staminaMultiplier = statMultiplier;
                break;
            case ItemType.SpeedBoost:
                modifier.speedMultiplier = statMultiplier;
                break;
            case ItemType.JumpHeightBoost:
                modifier.jumpHeightMultiplier = statMultiplier;
                break;
            case ItemType.ComboWindowBoost:
                modifier.comboWindowMultiplier = statMultiplier;
                break;
            case ItemType.CoinMultiplierBoost:
                modifier.coinMultiplier = statMultiplier;
                break;
            case ItemType.ExtraLife:
                modifier.extraLives = (int)statFlatBonus;
                break;
            case ItemType.DamageReduction:
                modifier.damageReduction = statFlatBonus; // Use flat bonus for percentage
                break;
        }

        return modifier;
    }

    public string GetDescription()
    {
        return itemType switch
        {
            ItemType.MaxHealthBoost => $"Max Health +{(statMultiplier - 1f) * 100:F0}%",
            ItemType.MaxStaminaBoost => $"Max Stamina +{(statMultiplier - 1f) * 100:F0}%",
            ItemType.SpeedBoost => $"Movement Speed +{(statMultiplier - 1f) * 100:F0}%",
            ItemType.JumpHeightBoost => $"Jump Height +{(statMultiplier - 1f) * 100:F0}%",
            ItemType.ComboWindowBoost => $"Combo Window +{(statMultiplier - 1f) * 100:F0}%",
            ItemType.CoinMultiplierBoost => $"Coins +{(statMultiplier - 1f) * 100:F0}%",
            ItemType.ExtraLife => $"+{(int)statFlatBonus} Extra Life",
            ItemType.DamageReduction => $"-{statFlatBonus * 100:F0}% Damage Taken",
            ItemType.GrindingStability => "Improved grinding stability",
            ItemType.LandingComfort => "Smoother landings",
            _ => "Unknown item type"
        };
    }

    public Color GetRarityColor()
    {
        return rarity switch
        {
            Rarity.Common => new Color(0.8f, 0.8f, 0.8f), // White/Gray
            Rarity.Uncommon => new Color(0.2f, 0.8f, 0.2f), // Green
            Rarity.Rare => new Color(0.2f, 0.5f, 1f), // Blue
            Rarity.Epic => new Color(0.8f, 0.2f, 1f), // Purple
            Rarity.Legendary => new Color(1f, 0.8f, 0.2f), // Gold
            _ => Color.white
        };
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(itemName))
            itemName = name;
    }
#endif
}
