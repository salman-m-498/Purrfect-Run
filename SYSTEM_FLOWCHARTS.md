# 🔀 System Flowcharts & Architecture

## Complete Consumable Item Flow

```
┌─────────────────────────────────────────────────────────────┐
│ CONSUMABLE ITEM FLOW                                        │
└─────────────────────────────────────────────────────────────┘

1. DESIGNER CREATE ITEM
   └─→ Right-click → Create → Items → Consumable
   └─→ Set Effect Type (e.g., RestoreHealth)
   └─→ Set Effect Value (e.g., 20)
   └─→ Assign sound clip
   └─→ Save ScriptableObject

2. LEVEL DESIGNER PLACE ITEM
   └─→ Drag ConsumablePickup prefab into scene
   └─→ Assign ConsumableItemData reference
   └─→ Position in level

3. GAMEPLAY - PLAYER TOUCHES ITEM
   ┌────────────────────────────────────────┐
   │ ConsumablePickup.OnTriggerEnter()      │
   │  • Check hasBeenCollected              │
   │  • Set hasBeenCollected = true         │
   └────────────────┬───────────────────────┘
                    │
   ┌────────────────▼───────────────────────┐
   │ ConsumablePickup.ApplyEffect()         │
   │  • Switch on effectType                │
   │  • Call appropriate handler            │
   └────────────────┬───────────────────────┘
                    │
                    ├─ RestoreHealth?
                    │  └─→ HealthSystem.RestoreHealth(20)
                    │
                    ├─ RestoreStamina?
                    │  └─→ StaminaSystem.RestoreStamina(50)
                    │
                    ├─ SpeedBoost?
                    │  └─→ StartCoroutine(TemporarySpeedBoost())
                    │      └─→ MovementController.speedMultiplier = 1.2
                    │      └─→ Wait 4 seconds
                    │      └─→ MovementController.speedMultiplier = 1.0
                    │
                    ├─ Invincibility?
                    │  └─→ StartCoroutine(TemporaryInvincibility())
                    │      └─→ PlayerController.isInvincible = true
                    │      └─→ Wait 5 seconds
                    │      └─→ PlayerController.isInvincible = false
                    │
                    ├─ MagnetCoins?
                    │  └─→ CoinSystem.ActivateMagnet()
                    │
                    ├─ Shield?
                    │  └─→ ShieldSystem.CreateShield()
                    │
                    ├─ ComboExtend?
                    │  └─→ ComboSystem.ExtendComboWindow(5 seconds)
                    │
                    └─ FlatScore?
                       └─→ ScoreSystem.AddScore(100)

4. FEEDBACK
   ┌────────────────────────────────────────┐
   │ ConsumablePickup.PlayPickupFeedback()  │
   │  • Play audio clip                     │
   │  • Spawn particle effect               │
   │  • Visual indicator (screen flash?)    │
   └────────────────┬───────────────────────┘
                    │
5. CLEANUP
   └────────────────▼───────────────────────┐
   │ Destroy(gameObject)                    │
   └────────────────────────────────────────┘
```

---

## Complete Permanent Item Flow

```
┌─────────────────────────────────────────────────────────────┐
│ PERMANENT ITEM FLOW                                         │
└─────────────────────────────────────────────────────────────┘

1. DESIGNER CREATE ITEM
   └─→ Right-click → Create → Items → Permanent Item
   └─→ Set Item Type (e.g., MaxHealthBoost)
   └─→ Set Stat Multiplier (e.g., 1.3 = 30%)
   └─→ Set Rarity (Common, Uncommon, Rare, Epic, Legendary)
   └─→ Save ScriptableObject

2. ADD TO GAME
   └─→ Drag PermanentItemData into ChestSystem.availableItems

3. GAMEPLAY - ENEMY DIES
   ┌────────────────────────────────────────┐
   │ BatEnemy.OnDeath()                     │
   │  • Play death animation                │
   │  • Drop coins                          │
   │  • Drop chest?                         │
   └────────────────┬───────────────────────┘
                    │
   ┌────────────────▼───────────────────────┐
   │ ChestSystem.TrySpawnChest()            │
   │  • Random(0, 1) < 0.15?                │
   │  • 85% chance: do nothing              │
   │  • 15% chance: continue                │
   └────────────────┬───────────────────────┘
                    │
   ┌────────────────▼───────────────────────┐
   │ ChestSystem.SpawnChest()               │
   │  • Generate random items               │
   │  • Instantiate Chest prefab            │
   │  • Initialize with items               │
   └────────────────┬───────────────────────┘
                    │
4. GENERATE CHEST CONTENTS
   ┌────────────────────────────────────────┐
   │ ChestSystem.GenerateChestContents()    │
   └────────────────┬───────────────────────┘
                    │
   ┌────────────────▼───────────────────────┐
   │ DetermineSlotCount()                   │
   │  • Random.value < 0.6? → 1 slot        │
   │  • Random.value < 0.3? → 2 slots       │
   │  • Random.value < 0.1? → 3 slots       │
   └────────────────┬───────────────────────┘
                    │
   ┌────────────────▼───────────────────────┐
   │ SelectRandomItem() (per slot)          │
   │  • Get all available items             │
   │  • Calculate rarity weights:           │
   │    - Common: 1.0x weight               │
   │    - Uncommon: 0.7x weight             │
   │    - Rare: 0.4x weight                 │
   │    - Epic: 0.2x weight                 │
   │    - Legendary: 0.05x weight           │
   │  • Weighted random selection           │
   │  • Add to chest contents               │
   └────────────────┬───────────────────────┘
                    │
5. CHEST ANIMATION
   ┌────────────────────────────────────────┐
   │ Chest.Initialize(items)                │
   │  • Set chest items                     │
   │  • Schedule OpenChestSequence()        │
   └────────────────┬───────────────────────┘
                    │
   ┌────────────────▼───────────────────────┐
   │ Chest.OpenChestSequence()              │
   │  └─→ OpenLid() [1 second]              │
   │      • Rotate lid 90°                  │
   │      • Bounce chest up 0.1 units       │
   │      • Easing curve animation          │
   │                                        │
   │  └─→ SpinSlots() [0.5 seconds]         │
   │      • Rotate each item 360°           │
   │      • Scale items 1.0 → 1.2 → 1.0    │
   │      • Show all item icons             │
   │                                        │
   │  └─→ WaitForSelection() [1 second]     │
   │      • Show selection UI               │
   │      • Wait for player click           │
   │      • Or auto-select after delay      │
   │                                        │
   │  └─→ SelectItem(index)                 │
   │      • Record selected item            │
   │      • Notify ChestSystem              │
   │                                        │
   │  └─→ CloseChest() [0.5 seconds]        │
   │      • Fade out chest                  │
   │      • Destroy chest GameObject       │
   └────────────────┬───────────────────────┘
                    │
6. ADD TO INVENTORY
   ┌────────────────────────────────────────┐
   │ ChestSystem.OnChestRewardSelected()    │
   │  • ItemInventory.UnlockItem(item)      │
   │  • ItemInventory.EquipItem(item)       │
   │  • PlayerStats.ApplyModifier()         │
   │    (immediate effect if in-run)        │
   │  • ItemInventory.SaveInventoryToPrefs()│
   └────────────────┬───────────────────────┘
                    │
7. NEXT RUN STARTS
   ┌────────────────────────────────────────┐
   │ GameManager.StartNewRun()              │
   │  • PlayerStats.ResetToBaseStats()      │
   │  • PermanentItemApplier.               │
   │    ApplyEquippedItems()                │
   └────────────────┬───────────────────────┘
                    │
   ┌────────────────▼───────────────────────┐
   │ ItemInventory.GetEquippedItems()       │
   │  • Get list of all equipped items      │
   │  • Return to applier                   │
   └────────────────┬───────────────────────┘
                    │
   ┌────────────────▼───────────────────────┐
   │ For Each Equipped Item:                │
   │  • item.GetStatModifier()              │
   │  • PlayerStats.ApplyModifier()         │
   │                                        │
   │ Example: Bigger Lungs                  │
   │  • Modifier: staminaMultiplier = 1.3   │
   │  • Result: max stamina = 50 × 1.3 = 65│
   │                                        │
   │ Example: Lucky Collar                  │
   │  • Modifier: coinMultiplier = 1.15     │
   │  • Result: coins = 1 × 1.15 = 1.15    │
   │                                        │
   │ Multiple items stack:                  │
   │  • Item 1: 1.3x stamina                │
   │  • Item 2: 1.2x stamina                │
   │  • Together: 1.3 × 1.2 = 1.56x         │
   └────────────────┬───────────────────────┘
                    │
   ┌────────────────▼───────────────────────┐
   │ All Systems Read Enhanced Stats        │
   │  • HealthSystem: GetMaxHealth()        │
   │  • StaminaSystem: GetMaxStamina()      │
   │  • MovementController: GetSpeed()      │
   │  • CoinSystem: GetCoinMultiplier()     │
   │  • Gameplay uses enhanced values       │
   └────────────────────────────────────────┘
```

---

## PlayerStats Stat Authority Flow

```
┌─────────────────────────────────────────────────────────────┐
│ PLAYER STATS CALCULATION                                    │
└─────────────────────────────────────────────────────────────┘

BASE STATS (from PlayerStats inspector)
  ├─ baseMaxHealth = 100
  ├─ baseMaxStamina = 50
  ├─ baseSpeed = 5.0
  ├─ baseJumpHeight = 3.0
  └─ baseCoinMultiplier = 1.0

MODIFIERS (applied from permanent items)
  ├─ healthMultiplier = 1.0
  ├─ staminaMultiplier = 1.0
  ├─ speedMultiplier = 1.0
  ├─ jumpHeightMultiplier = 1.0
  └─ coinMultiplier = 1.0

RUN-TIME MODIFICATIONS
  
  1. Run Starts
     └─→ ResetToBaseStats()
         • Clear all modifiers
         • Set current health = base max
         • Set current stamina = base max
  
  2. Apply Equipped Items
     └─→ For each item in ItemInventory.GetEquippedItems()
         • GetStatModifier() from item
         • ApplyModifier(modifier)
            └─→ healthMultiplier *= modifier.healthMultiplier
            └─→ staminaMultiplier *= modifier.staminaMultiplier
            └─→ speedMultiplier *= modifier.speedMultiplier
            └─→ (and others...)
  
  3. Gameplay - Query Stats
     └─→ GetMaxHealth()
         • return baseMaxHealth × healthMultiplier
     
     └─→ GetMaxStamina()
         • return baseMaxStamina × staminaMultiplier
     
     └─→ GetSpeed()
         • return baseSpeed × speedMultiplier
     
     └─→ GetCoinMultiplier()
         • return baseCoinMultiplier × coinMultiplier

EXAMPLE CALCULATION
  
  Start: baseMaxHealth = 100, modifier = 1.0
  ✓ GetMaxHealth() = 100 × 1.0 = 100
  
  Item 1: Bigger Lungs (maxStaminaBoost = 1.3)
  ✓ GetMaxStamina() = 50 × 1.3 = 65
  
  Item 2: Lucky Collar (coinMultiplier = 1.15)
  ✓ GetCoinMultiplier() = 1.0 × 1.15 = 1.15
  
  All systems now query and use these values
  ✓ HealthSystem.GetMaxHealth() = 100
  ✓ StaminaSystem.GetMaxStamina() = 65
  ✓ CoinSystem.GetMultiplier() = 1.15
```

---

## ItemInventory Persistence Flow

```
┌─────────────────────────────────────────────────────────────┐
│ INVENTORY SAVE / LOAD                                       │
└─────────────────────────────────────────────────────────────┘

UNLOCK ITEM (during chest reward)
  ┌──────────────────────────────────────────┐
  │ ItemInventory.UnlockItem(item)           │
  │  • Add to unlockedItems list             │
  │  • Fire OnItemUnlocked event             │
  │  • Save to PlayerPrefs                   │
  └──────────────────────────────────────────┘

EQUIP ITEM
  ┌──────────────────────────────────────────┐
  │ ItemInventory.EquipItem(item)            │
  │  • Add to equippedItems list             │
  │  • Fire OnItemEquipped event             │
  │  • Save to PlayerPrefs                   │
  └──────────────────────────────────────────┘

SAVE TO PREFERENCES
  ┌──────────────────────────────────────────┐
  │ ItemInventory.SaveInventoryToPrefs()     │
  │                                          │
  │ Serialize:                               │
  │  ├─ unlockedItems.Count → PlayerPrefs    │
  │  ├─ foreach item in unlockedItems        │
  │  │   └─ item.itemId → PlayerPrefs        │
  │  ├─ equippedItems.Count → PlayerPrefs    │
  │  └─ foreach item in equippedItems        │
  │      └─ item.itemId → PlayerPrefs        │
  └──────────────────────────────────────────┘

GAME RESTART / NEW SESSION

LOAD FROM PREFERENCES
  ┌──────────────────────────────────────────┐
  │ ItemInventory.LoadInventoryFromSave()    │
  │                                          │
  │ Deserialize:                             │
  │  ├─ Read unlockedItems.Count             │
  │  ├─ For each saved itemId                │
  │  │  └─ Lookup item via ItemDatabase      │
  │  │     └─ Add to unlockedItems list      │
  │  ├─ Read equippedItems.Count             │
  │  └─ For each saved itemId                │
  │     └─ Lookup item via ItemDatabase      │
  │        └─ Add to equippedItems list      │
  └──────────────────────────────────────────┘

AT LEVEL START
  ┌──────────────────────────────────────────┐
  │ PermanentItemApplier.                    │
  │ ApplyEquippedItems()                     │
  │                                          │
  │  ├─ PlayerStats.ResetToBaseStats()       │
  │  └─ For each item in                     │
  │     ItemInventory.GetEquippedItems()     │
  │     └─ ApplyModifier()                   │
  │        └─ Gameplay uses bonuses          │
  └──────────────────────────────────────────┘

PERSISTENT ACROSS SESSIONS
  Run 1: Unlock "Bigger Lungs"
  └─→ Saved to PlayerPrefs
  
  Close Game
  └─→ PlayerPrefs persist
  
  Restart Game
  └─→ ItemInventory.LoadInventoryFromSave()
  └─→ "Bigger Lungs" loaded and equipped
  
  Run 2: +30% stamina bonus
```

---

## System Dependencies Graph

```
┌─────────────────────────────────────────────────────────────┐
│ SYSTEM DEPENDENCY GRAPH                                     │
└─────────────────────────────────────────────────────────────┘

                         GAME MANAGER
                              │
                    ┌─────────┼─────────┐
                    │         │         │
            ResetStats  ApplyItems  SetupLevel
                    │         │         │
                    ▼         ▼         ▼
              PlayerStats  ItemApplier  (Level init)
                    │         │
                    │    ┌────┴────┐
                    │    │         │
                    ▼    ▼         ▼
              ItemInventory    PlayerStats
                    │
         ┌──────────┼──────────┐
         │          │          │
         ▼          ▼          ▼
     Unlocked   Equipped   Modifiers
      Items      Items     Applied
         │          │          │
         └──────────┼──────────┘
                    │
              ┌─────▼──────┐
              │ PlayerPrefs│ ← Persistence
              └────────────┘

DURING GAMEPLAY

    Enemy Dies
         │
         ▼
    ChestSystem
         │
    ┌────┴────┐
    │          │
    ▼          ▼
  Spawn     Select
  Chest      Item
    │          │
    ▼          ▼
Chest       ItemInventory
Animation   UnlockItem()
    │       EquipItem()
    │          │
    │    Apply if
    │    in-run
    │          │
    │    PlayerStats
    │          │
    └────┬─────┘
         │
    All Systems
    Read Enhanced
    Stats

CONSUMABLE PICKUPS

    Player Touches
         │
         ▼
    ConsumablePickup
         │
    ┌────┴──────────────────────┐
    │                           │
    ▼                           ▼
 Apply Effect            Feedback
    │                      (Sound)
 ┌──┴──┬─────┬─────┬─────┬──┐   
 │     │     │     │     │  │
 ▼     ▼     ▼     ▼     ▼  ▼
Health Stam Speed  Magnet Shield Others
System System (temp) System (temp)
    │    │     │      │      │     │
    └────┴─────┴──────┴──────┴─────┘
         Immediate Effect
```

---

## Create Asset Menu Structure

```
┌─────────────────────────────────────────────────────────────┐
│ SCRIPTABLE OBJECT CREATION                                  │
└─────────────────────────────────────────────────────────────┘

Right-click in Project Folder
    │
    ├─ Create
    │   │
    │   └─ Items
    │       │
    │       ├─ Consumable
    │       │   └─ Creates ConsumableItemData
    │       │       ├─ Effect Type dropdown
    │       │       ├─ Effect Value slider
    │       │       ├─ Effect Duration slider
    │       │       └─ Sound assignment
    │       │
    │       └─ Permanent Item
    │           └─ Creates PermanentItemData
    │               ├─ Item Type dropdown
    │               ├─ Stat Multiplier slider
    │               ├─ Rarity dropdown
    │               └─ Icon assignment
```

---

## Event Flow Diagram

```
┌─────────────────────────────────────────────────────────────┐
│ EVENT SYSTEM FLOW                                           │
└─────────────────────────────────────────────────────────────┘

EVENT SOURCES                   LISTENERS (UI)
│                               │
├─ ItemInventory               ├─ Update Inventory Panel
│  ├─ OnItemUnlocked ──────────┤─ Show new item
│  ├─ OnItemEquipped ──────────┤─ Update equipped list
│  └─ OnItemUnequipped ────────┤─ Remove from equipped
│                              │
├─ ChestSystem                 ├─ Show chest animation
│  └─ OnChestOpened ───────────┤─ Enable selection UI
│                              │
└─ Chest                       └─ Play audio effects
   └─ SelectItem ──────────────┤─ Visual feedback

HOW EVENTS WORK

Unlock Item:
    ItemInventory.UnlockItem(item)
        │
        └─→ unlockedItems.Add(item)
        └─→ OnItemUnlocked?.Invoke(item)
            │
            └─→ UI listener receives event
            └─→ UI_ShowUnlockedItem(item)
            └─→ Display "New item!" animation

Equip Item:
    ItemInventory.EquipItem(item)
        │
        └─→ equippedItems.Add(item)
        └─→ OnItemEquipped?.Invoke(item)
            │
            └─→ UI listener receives event
            └─→ UI_UpdateStats(item)
            └─→ Display stat increases
```

---

## Complete Workflow Timeline

```
DEVELOPMENT TIMELINE

Designer Work
├─ Create ConsumableItemData instances
│  ├─ Tuna (+20 health)
│  ├─ Milk (+50 stamina)
│  ├─ Sugar Rush (+20% speed 4s)
│  └─ ... (8 total)
│
├─ Create PermanentItemData instances
│  ├─ Bigger Lungs (+30% stamina)
│  ├─ Lucky Collar (+15% coins)
│  ├─ Muscle Meow (+10% speed)
│  └─ ... (10+ total)
│
└─ Assign to ChestSystem.availableItems

Level Designer Work
├─ Place ConsumablePickup prefabs
│  ├─ Assign ConsumableItemData
│  ├─ Position strategically
│  └─ Test pickup mechanics
│
└─ Test chest spawning from enemies

Programmer Work
├─ Add to GameManager.StartNewRun()
│  ├─ playerStats.ResetToBaseStats()
│  └─ permanentItemApplier.ApplyEquippedItems()
│
├─ Add to enemy death handler
│  └─ ChestSystem.Instance.TrySpawnChest()
│
├─ Create Chest UI prefab
│  ├─ Lid, 3 item slots
│  ├─ Selection buttons
│  └─ Assign to ChestSystem
│
└─ Verify system method names
   └─ ConsumablePickup calls correct APIs

GAMEPLAY RUNTIME

Session 1:
├─ Level starts
│  └─ PlayerStats.ResetToBaseStats()
│  └─ ApplyEquippedItems() [empty first run]
│
├─ Player explores level
│  └─ Touches consumables for immediate boosts
│
├─ Enemy drops chest (15% chance)
│  └─ Chest animation plays
│  └─ Player selects item
│  └─ Item unlocked and saved
│
└─ Player dies or completes level

Session 2 (New Run):
├─ Level starts
│  └─ PlayerStats.ResetToBaseStats()
│  └─ ApplyEquippedItems() [now has 1 item]
│  └─ +30% stamina active
│
├─ All gameplay uses enhanced stats
│
└─ Drop another chest, unlock more items

Session 3 (After Restart):
├─ Game loads saved inventory
│  └─ ItemInventory.LoadInventoryFromSave()
│  └─ All items still there
│
├─ Level starts
│  └─ PlayerStats.ResetToBaseStats()
│  └─ ApplyEquippedItems() [2 items now]
│  └─ +30% stamina AND +15% coins active
│
└─ Continue with full bonuses
```

---

This comprehensive flowchart shows every system in action! 🎯
