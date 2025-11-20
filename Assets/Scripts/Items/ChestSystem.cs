using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// ChestSystem: Manages item reward chests that drop from enemies.
/// Implements a Vampire Survivors-style slot machine chest with 1, 2, or 3 items.
/// Enemies randomly drop chests with configurable drop rates.
/// </summary>
public class ChestSystem : MonoBehaviour
{
    public static ChestSystem Instance { get; private set; }

    [Header("Chest Spawning")]
    [SerializeField] private float chestDropRate = 0.15f; // 15% chance per enemy kill
    [SerializeField] private GameObject chestPrefab;
    [SerializeField] private float chestSpawnHeight = 1f;

    [Header("Chest Contents")]
    [SerializeField] private List<PermanentItemData> availableItems = new List<PermanentItemData>();
    [SerializeField] private AnimationCurve rariryWeighting = AnimationCurve.EaseInOut(0, 1, 1, 0.2f);

    [Header("Slot Configuration")]
    [SerializeField] private int minSlots = 1;
    [SerializeField] private int maxSlots = 3;
    [SerializeField] private List<float> slotWeights = new List<float> { 0.6f, 0.3f, 0.1f }; // 60% 1-slot, 30% 2-slot, 10% 3-slot

    // Events
    public System.Action<List<PermanentItemData>> OnChestOpened;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    /// <summary>
    /// Try to spawn a chest at the given position
    /// </summary>
    public void TrySpawnChest(Vector3 position)
    {
        if (Random.value < chestDropRate)
        {
            SpawnChest(position);
        }
    }

    /// <summary>
    /// Force spawn a chest at the given position
    /// </summary>
    public void SpawnChest(Vector3 position)
    {
        if (chestPrefab == null)
        {
            Debug.LogWarning("ChestSystem: No chest prefab assigned!");
            return;
        }

        GameObject chestGO = Instantiate(chestPrefab, position + Vector3.up * chestSpawnHeight, Quaternion.identity);
        Chest chest = chestGO.GetComponent<Chest>();
        
        if (chest != null)
        {
            // Generate random items for this chest
            List<PermanentItemData> chestItems = GenerateChestContents();
            chest.Initialize(chestItems, this);
        }
    }

    /// <summary>
    /// Generate random items for a chest
    /// </summary>
    private List<PermanentItemData> GenerateChestContents()
    {
        // Determine number of slots
        int numSlots = DetermineSlotCount();

        // Select random items from available pool
        List<PermanentItemData> chestItems = new List<PermanentItemData>();
        
        for (int i = 0; i < numSlots; i++)
        {
            PermanentItemData selectedItem = SelectRandomItem();
            if (selectedItem != null)
            {
                chestItems.Add(selectedItem);
            }
        }

        return chestItems;
    }

    /// <summary>
    /// Determine how many slots this chest has
    /// </summary>
    private int DetermineSlotCount()
    {
        float roll = Random.value;
        float cumulative = 0f;

        for (int i = 0; i < slotWeights.Count; i++)
        {
            cumulative += slotWeights[i];
            if (roll < cumulative)
            {
                return Mathf.Min(minSlots + i, maxSlots);
            }
        }

        return maxSlots;
    }

    /// <summary>
    /// Select a random item weighted by rarity
    /// </summary>
    private PermanentItemData SelectRandomItem()
    {
        if (availableItems.Count == 0) return null;

        // Weight items by rarity
        List<(PermanentItemData item, float weight)> weightedItems = new List<(PermanentItemData, float)>();

        foreach (var item in availableItems)
        {
            float rarityWeight = GetRarityWeight(item.rarity);
            weightedItems.Add((item, rarityWeight));
        }

        // Select based on weights
        float totalWeight = weightedItems.Sum(x => x.weight);
        float roll = Random.value * totalWeight;
        float accumulated = 0f;

        foreach (var (item, weight) in weightedItems)
        {
            accumulated += weight;
            if (roll < accumulated)
            {
                return item;
            }
        }

        return availableItems[Random.Range(0, availableItems.Count)];
    }

    /// <summary>
    /// Get weight for rarity level (affects drop chance)
    /// </summary>
    private float GetRarityWeight(PermanentItemData.Rarity rarity)
    {
        return rarity switch
        {
            PermanentItemData.Rarity.Common => 1f,
            PermanentItemData.Rarity.Uncommon => 0.7f,
            PermanentItemData.Rarity.Rare => 0.4f,
            PermanentItemData.Rarity.Epic => 0.2f,
            PermanentItemData.Rarity.Legendary => 0.05f,
            _ => 1f
        };
    }

    /// <summary>
    /// Called when a chest is opened
    /// </summary>
    public void OnChestRewardSelected(PermanentItemData selectedItem)
    {
        if (selectedItem == null) return;

        // Unlock the item
        ItemInventory.Instance.UnlockItem(selectedItem);
        ItemInventory.Instance.EquipItem(selectedItem);

        OnChestOpened?.Invoke(new List<PermanentItemData> { selectedItem });
    }

    /// <summary>
    /// Add available items for chests
    /// </summary>
    public void AddAvailableItem(PermanentItemData item)
    {
        if (item != null && !availableItems.Contains(item))
        {
            availableItems.Add(item);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Test Spawn Chest")]
    public void TestSpawnChest()
    {
        SpawnChest(transform.position);
    }
#endif
}
