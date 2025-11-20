using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// ItemInventory: Stores all unlocked permanent items for the player.
/// Handles adding, removing, and querying items.
/// Persists to PlayerPrefs.
/// </summary>
public class ItemInventory : MonoBehaviour
{
    public static ItemInventory Instance { get; private set; }

    [SerializeField] private List<PermanentItemData> unlockedItems = new List<PermanentItemData>();
    [SerializeField] private List<PermanentItemData> equippedItems = new List<PermanentItemData>();

    // Events
    public System.Action<PermanentItemData> OnItemUnlocked;
    public System.Action<PermanentItemData> OnItemEquipped;
    public System.Action<PermanentItemData> OnItemUnequipped;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        LoadInventoryFromSave();
    }

    /// <summary>
    /// Unlock a permanent item
    /// </summary>
    public void UnlockItem(PermanentItemData item)
    {
        if (item == null) return;

        if (!unlockedItems.Contains(item))
        {
            unlockedItems.Add(item);
            OnItemUnlocked?.Invoke(item);
            SaveInventoryToPrefs();
            Debug.Log($"Unlocked item: {item.itemName}");
        }
    }

    /// <summary>
    /// Check if an item is unlocked
    /// </summary>
    public bool IsItemUnlocked(PermanentItemData item)
    {
        return item != null && unlockedItems.Contains(item);
    }

    /// <summary>
    /// Equip an item (add it to active bonuses)
    /// </summary>
    public void EquipItem(PermanentItemData item)
    {
        if (item == null || !IsItemUnlocked(item)) return;

        if (!equippedItems.Contains(item))
        {
            equippedItems.Add(item);
            OnItemEquipped?.Invoke(item);
            SaveInventoryToPrefs();
            Debug.Log($"Equipped item: {item.itemName}");
        }
    }

    /// <summary>
    /// Unequip an item
    /// </summary>
    public void UnequipItem(PermanentItemData item)
    {
        if (item == null) return;

        if (equippedItems.Remove(item))
        {
            OnItemUnequipped?.Invoke(item);
            SaveInventoryToPrefs();
            Debug.Log($"Unequipped item: {item.itemName}");
        }
    }

    /// <summary>
    /// Check if an item is currently equipped
    /// </summary>
    public bool IsItemEquipped(PermanentItemData item)
    {
        return item != null && equippedItems.Contains(item);
    }

    /// <summary>
    /// Get all unlocked items
    /// </summary>
    public List<PermanentItemData> GetUnlockedItems() => new List<PermanentItemData>(unlockedItems);

    /// <summary>
    /// Get all equipped items
    /// </summary>
    public List<PermanentItemData> GetEquippedItems() => new List<PermanentItemData>(equippedItems);

    /// <summary>
    /// Get all stat modifiers from equipped items
    /// </summary>
    public List<PlayerStats.StatModifiers> GetEquippedModifiers()
    {
        return equippedItems
            .Select(item => item.GetStatModifier())
            .ToList();
    }

    /// <summary>
    /// Remove an unlocked item completely
    /// </summary>
    public void RemoveItem(PermanentItemData item)
    {
        if (item == null) return;

        UnequipItem(item);
        unlockedItems.Remove(item);
        SaveInventoryToPrefs();
    }

    /// <summary>
    /// Save inventory to PlayerPrefs
    /// </summary>
    private void SaveInventoryToPrefs()
    {
        // Store IDs of unlocked items
        string unlockedIds = string.Join(",", unlockedItems.Select(i => i.itemId));
        string equippedIds = string.Join(",", equippedItems.Select(i => i.itemId));
        
        PlayerPrefs.SetString("UnlockedItems", unlockedIds);
        PlayerPrefs.SetString("EquippedItems", equippedIds);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Load inventory from PlayerPrefs
    /// </summary>
    private void LoadInventoryFromSave()
    {
        // This requires having a database of all items by ID
        // For now, we'll implement a basic version
        string unlockedIds = PlayerPrefs.GetString("UnlockedItems", "");
        string equippedIds = PlayerPrefs.GetString("EquippedItems", "");

        // TODO: Load actual item data from ID lookup
        Debug.Log($"Loaded unlocked items: {unlockedIds}");
        Debug.Log($"Loaded equipped items: {equippedIds}");
    }

    /// <summary>
    /// Clear all items (for testing)
    /// </summary>
    public void ClearInventory()
    {
        unlockedItems.Clear();
        equippedItems.Clear();
        PlayerPrefs.DeleteKey("UnlockedItems");
        PlayerPrefs.DeleteKey("EquippedItems");
        PlayerPrefs.Save();
    }

    public int GetUnlockedItemCount() => unlockedItems.Count;
    public int GetEquippedItemCount() => equippedItems.Count;

#if UNITY_EDITOR
    [ContextMenu("Log Inventory")]
    public void LogInventory()
    {
        Debug.Log($"=== INVENTORY ===\nUnlocked: {unlockedItems.Count}\nEquipped: {equippedItems.Count}");
        foreach (var item in unlockedItems)
        {
            Debug.Log($"  - {item.itemName} {(equippedItems.Contains(item) ? "[EQUIPPED]" : "")}");
        }
    }
#endif
}
