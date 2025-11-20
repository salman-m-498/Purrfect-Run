# 🔌 Integration Guide - Consumable & Permanent Item Systems

## Quick Integration Checklist

This guide shows exactly where to add code to integrate the item systems into your existing gameplay.

---

## 1️⃣ Add PlayerStats to Player

**File**: `Assets/Scripts/PlayerController.cs` or `Assets/Scripts/Player/Player.cs`

**What to add**:
```csharp
public class PlayerController : MonoBehaviour
{
    private PlayerStats playerStats;
    
    void Start()
    {
        // Get or add PlayerStats component
        playerStats = GetComponent<PlayerStats>();
        if (playerStats == null)
        {
            playerStats = gameObject.AddComponent<PlayerStats>();
        }
    }
    
    // All systems now read from PlayerStats instead of local fields
    // Movement: float speed = playerStats.GetSpeed();
    // Health: float maxHP = playerStats.GetMaxHealth();
    // etc.
}
```

---

## 2️⃣ Update GameManager for Run Start

**File**: `Assets/Scripts/GameManager.cs` or `Assets/Scripts/Managers/GameManager.cs`

**Find**: The method that starts a new level/run

**What to add**:
```csharp
public class GameManager : MonoBehaviour
{
    private PermanentItemApplier permanentItemApplier;
    
    void Awake()
    {
        permanentItemApplier = GetComponent<PermanentItemApplier>();
        if (permanentItemApplier == null)
        {
            permanentItemApplier = gameObject.AddComponent<PermanentItemApplier>();
        }
    }
    
    public void StartNewRun()
    {
        // Reset player stats to base
        PlayerStats playerStats = player.GetComponent<PlayerStats>();
        playerStats.ResetToBaseStats();
        
        // Apply all equipped permanent items
        permanentItemApplier.ApplyEquippedItems();
        
        // Continue with your existing level setup
        SetupLevel();
    }
    
    // ... rest of your code
}
```

**What this does**:
- Resets player stats each run
- Applies all items from previous runs
- All systems read the enhanced stats

---

## 3️⃣ Add ItemInventory Singleton to Scene

**File**: Any script that initializes the scene

**What to add**:
```csharp
void InitializeGameSystems()
{
    // Make sure ItemInventory singleton exists
    if (ItemInventory.Instance == null)
    {
        GameObject inventoryObj = new GameObject("ItemInventory");
        inventoryObj.AddComponent<ItemInventory>();
    }
    
    // Similar for ChestSystem
    if (ChestSystem.Instance == null)
    {
        GameObject chestObj = new GameObject("ChestSystem");
        chestObj.AddComponent<ChestSystem>();
    }
}
```

**Alternative**: Create empty GameObjects in scene with ItemInventory and ChestSystem components

---

## 4️⃣ Setup ChestSystem with Available Items

**File**: `Assets/Scripts/Managers/ChestSystem.cs` or setup script

**What to add**:
```csharp
public class ChestSystem : MonoBehaviour
{
    [SerializeField] private List<PermanentItemData> availableItems;
    
    void Start()
    {
        // Load available items from Resources or manually assign
        if (availableItems == null || availableItems.Count == 0)
        {
            availableItems = new List<PermanentItemData>();
            
            // Load all PermanentItemData ScriptableObjects
            PermanentItemData[] allItems = Resources.LoadAll<PermanentItemData>("Items/Permanent");
            foreach (var item in allItems)
            {
                availableItems.Add(item);
            }
        }
    }
}
```

**Or in Inspector**:
1. Create empty gameobject "ChestSystem"
2. Add ChestSystem component
3. Drag/drop PermanentItemData ScriptableObjects into availableItems list

---

## 5️⃣ Wire Enemy Deaths to Chest Spawning

**File**: `Assets/Scripts/Enemies/BatEnemy.cs` (or your enemy death handler)

**Find**: The code that handles enemy death

**What to add**:
```csharp
public class BatEnemy : MonoBehaviour
{
    void OnDeath()
    {
        // Your existing death logic
        PlayDeathAnimation();
        DropCoins();
        
        // Add this line to drop chests
        ChestSystem.Instance.TrySpawnChest(transform.position);
        
        Destroy(gameObject);
    }
}
```

**That's it!** Now enemies have 15% chance to drop chests on death.

---

## 6️⃣ Configure Chest Prefab

**File**: Create in scene or Resources folder

**Steps**:
1. Create an empty GameObject called "Chest"
2. Add Chest.cs component
3. Add a CanvasGroup component
4. Create UI Canvas with:
   - Lid image (rotatable)
   - 3 item slot images (can be hidden)
   - Selection buttons (or just click detection)
5. Assign all references in Chest.cs inspector:
   - lidTransform
   - slotImages (3 of them)
   - canvasGroup
6. Save as prefab
7. Assign to ChestSystem.chestPrefab field

---

## 7️⃣ Verify System Method Calls

**IMPORTANT**: Verify that ConsumablePickup is calling the correct methods on your systems.

### Check HealthSystem

**Find**: Your HealthSystem.cs or wherever health restoration is handled

**Verify these methods exist**:
```csharp
public void AddHealth(float amount)
public void RestoreHealth(float amount)
public float GetMaxHealth()
public float GetCurrentHealth()
```

**If method names differ**, update ConsumablePickup.cs:
```csharp
case EffectType.RestoreHealth:
    healthSystem.RestoreHealth(value);  // ← Update this line
    PlayPickupFeedback();
    break;
```

### Check StaminaSystem

**Verify these methods exist**:
```csharp
public void RestoreStamina(float amount)
public void ConsumeStamina(float amount)
public float GetMaxStamina()
public float GetCurrentStamina()
```

### Check ScoreSystem

**Verify this method exists**:
```csharp
public void AddScore(float amount)
```

---

## 8️⃣ Place Consumables in Levels

**Steps**:
1. Create ConsumableItemData ScriptableObjects (see guide below)
2. Create ConsumablePickup prefab with:
   - Collider (trigger)
   - MeshRenderer or SpriteRenderer
   - Rigidbody (kinematic)
3. Drag prefab into level
4. Assign ConsumableItemData to the ConsumablePickup component
5. Position where you want it

---

## 📝 Creating ScriptableObjects (Designer Task)

### Create a Consumable Item

**In Editor**:
```
Right-click in Project folder
→ Create → Items → Consumable
```

**Configure**:
- Name: "Health Potion"
- Effect Type: RestoreHealth
- Effect Value: 20
- Effect Duration: 0 (instant)
- Pickup Sound: (drag audio clip)
- Bounce Height: 0.1

### Create a Permanent Item

**In Editor**:
```
Right-click in Project folder
→ Create → Items → Permanent Item
```

**Configure**:
- Name: "Bigger Lungs"
- Item Type: MaxStaminaBoost
- Stat Multiplier: 1.3
- Rarity: Uncommon
- Icon: (drag icon image)
- Description: "+30% Max Stamina"

---

## 🔍 Verification Steps

### Test 1: PlayerStats Reset
```csharp
// In console or debug script
PlayerStats stats = player.GetComponent<PlayerStats>();
Debug.Log($"Max Health: {stats.GetMaxHealth()}");  // Should be base value (e.g., 100)
```

### Test 2: Apply Modifier
```csharp
var modifier = new PlayerStats.StatModifiers { healthMultiplier = 1.3f };
stats.ApplyModifier(modifier);
Debug.Log($"Max Health after modifier: {stats.GetMaxHealth()}");  // Should be 130
```

### Test 3: ItemInventory Save/Load
```csharp
// Unlock an item
ItemInventory.Instance.UnlockItem(someItem);
ItemInventory.Instance.EquipItem(someItem);

// Simulate save/load
ItemInventory.Instance.SaveInventoryToPrefs();
ItemInventory.Instance.LoadInventoryFromSave();

// Verify it's still there
Debug.Log($"Equipped items: {ItemInventory.Instance.GetEquippedItems().Count}");
```

### Test 4: Chest Spawn
```csharp
// In an enemy death handler
ChestSystem.Instance.TrySpawnChest(transform.position);
// Should see chest spawn 15% of the time
```

### Test 5: Consumable Pickup
```csharp
// Play level, touch a consumable pickup
// Should see:
// - Pickup effect applied
// - Sound plays
// - Pickup disappears
```

---

## 🐛 Common Issues & Solutions

### Issue: ConsumablePickup effect doesn't apply
**Solution**: Verify method names match your systems
```csharp
// Check what method your health system actually uses
healthSystem.AddHealth(20);      // Try this
healthSystem.RestoreHealth(20);  // Or this
// Update ConsumablePickup.cs line accordingly
```

### Issue: ItemInventory loses data on reload
**Solution**: Make sure SaveInventoryToPrefs() is called
```csharp
// In ItemInventory.cs, add to OnDestroy or game end
void OnDestroy()
{
    SaveInventoryToPrefs();
}
```

### Issue: Chest doesn't spawn on enemy death
**Solution**: Verify ChestSystem.TrySpawnChest() is being called
```csharp
// Add debug line in your enemy death handler
void OnDeath()
{
    Debug.Log("Enemy dying, spawning chest...");
    ChestSystem.Instance.TrySpawnChest(transform.position);
}
```

### Issue: Chest prefab missing or errors
**Solution**: Create chest in scene manually
```
1. Create empty GameObject "Chest"
2. Add Chest.cs component
3. Create child Canvas with UI elements
4. Assign all references in inspector
5. Test by calling Chest.Initialize() manually
```

---

## 📊 Integration Order

**Recommended order to avoid errors**:

1. ✅ Add PlayerStats to player
2. ✅ Create ItemInventory in scene
3. ✅ Create ChestSystem in scene
4. ✅ Create PermanentItemApplier component
5. ✅ Update GameManager to call ApplyEquippedItems()
6. ✅ Verify method names in health/stamina systems
7. ✅ Add chest spawning to enemy death
8. ✅ Create Chest prefab and assign
9. ✅ Create ScriptableObject items
10. ✅ Place consumables in levels
11. ✅ Test consumable pickup
12. ✅ Test chest drop
13. ✅ Test chest selection
14. ✅ Test item persistence (save/load)

---

## 🎮 Testing Flow

**Quick test**:
```
1. Start game
2. Verify PlayerStats base stats logged
3. Kill an enemy
4. Verify chest has 15% chance to spawn
5. Open chest, select item
6. Verify stats increased in next run
7. Close and restart game
8. Verify equipped items still there
```

---

## ✨ You're Ready!

Once you've completed all integration steps, your game has:
- ✅ Permanent items system with meta progression
- ✅ Consumable pickups for moment-to-moment gameplay
- ✅ Chest rewards for defeating enemies
- ✅ Persistent item inventory with save/load
- ✅ All stats managed centrally through PlayerStats

Good luck! 🎉
