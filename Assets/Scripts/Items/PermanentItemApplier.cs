using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// PermanentItemApplier: Applies all equipped permanent items to PlayerStats at run start.
/// Integrates ItemInventory with the gameplay systems.
/// Should be called by GameManager at level initialization.
/// </summary>
public class PermanentItemApplier : MonoBehaviour
{
    [SerializeField] private PlayerStats playerStats;

    private void Start()
    {
        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>();
    }

    /// <summary>
    /// Apply all equipped permanent items to PlayerStats
    /// Call this at the start of each run
    /// </summary>
    public void ApplyEquippedItems()
    {
        if (playerStats == null)
        {
            Debug.LogError("PermanentItemApplier: No PlayerStats assigned!");
            return;
        }

        // Reset stats to base
        playerStats.ResetToBaseStats();

        // Get all equipped items
        if (ItemInventory.Instance == null)
        {
            Debug.LogWarning("PermanentItemApplier: ItemInventory.Instance not found!");
            return;
        }

        List<PermanentItemData> equippedItems = ItemInventory.Instance.GetEquippedItems();

        // Apply each item's modifiers
        foreach (var item in equippedItems)
        {
            if (item != null)
            {
                var modifier = item.GetStatModifier();
                playerStats.ApplyModifier(modifier);
                Debug.Log($"Applied permanent item: {item.itemName}");
            }
        }

        Debug.Log($"Applied {equippedItems.Count} permanent items");
    }

    /// <summary>
    /// Apply a single item's modifiers
    /// </summary>
    public void ApplySingleItem(PermanentItemData item)
    {
        if (playerStats == null || item == null) return;

        var modifier = item.GetStatModifier();
        playerStats.ApplyModifier(modifier);
    }

#if UNITY_EDITOR
    [ContextMenu("Test Apply Items")]
    public void TestApplyItems()
    {
        ApplyEquippedItems();
        playerStats.LogStats();
    }
#endif
}
