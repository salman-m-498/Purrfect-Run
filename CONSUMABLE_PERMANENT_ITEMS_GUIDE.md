# 🎒 Consumable & Permanent Item Systems - Complete Implementation

## System Overview

### Two-Tier Item Architecture

```
Consumable Items (In-Run, Temporary)
├─ Spawn in level as pickups
├─ One-time use
├─ Immediate effects
├─ Examples: Health, Stamina, Speed Boost
└─ Defined in: ConsumableItemData (ScriptableObject)

Permanent Items (Meta Progression, Persistent)
├─ Unlocked from chests
├─ Saved to inventory
├─ Applied every run
├─ Provide persistent upgrades
├─ Examples: +Max Health, +Speed, +Coins
└─ Defined in: PermanentItemData (ScriptableObject)

Central Hub: PlayerStats
├─ Single source of truth for all stats
├─ Modified by permanent items
├─ Queried by all systems
└─ Resets each run
```

---

## 🎒 Component Breakdown

### 1. PlayerStats.cs
**Central stat storage and application**

**Responsibilities**:
- Store base stats (max health, max stamina, speed, etc.)
- Apply item modifiers to base stats
- Provide queried values to all systems
- Reset stats each run
- Handle temporary buffs via external systems

**Key Methods**:
```csharp
ResetToBaseStats()           // Reset to base and clear modifiers
ApplyModifier(modifier)      // Apply permanent item bonus
GetMaxHealth()               // Get calculated max health
GetSpeed()                   // Get calculated speed
GetCoinMultiplier()          // Get coin multiplier
AddHealth(amount)            // Direct health modification
TakeDamage(damage)           // Take damage with reduction
```

**Integration Points**:
- HealthSystem reads GetMaxHealth()
- StaminaSystem reads GetMaxStamina()
- MovementController reads GetSpeed()
- ScoreSystem reads GetCoinMultiplier()

**Example Usage**:
```csharp
// At run start
PlayerStats stats = GetComponent<PlayerStats>();
stats.ResetToBaseStats();
stats.ApplyModifier(item1.GetStatModifier());
stats.ApplyModifier(item2.GetStatModifier());

// During gameplay
float maxHP = stats.GetMaxHealth();
stats.AddHealth(20);
stats.TakeDamage(10); // Respects damage reduction
```

---

### 2. ConsumableItemData.cs
**ScriptableObject defining consumable properties**

**Effect Types**:
```csharp
RestoreHealth       // Instant health restoration
RestoreStamina      // Instant stamina restoration
SpeedBoost          // Temporary speed increase
Invincibility       // Temporary invincibility
MagnetCoins         // Pull nearby coins
Shield              // Absorb one hit
ComboExtend         // Extend combo window
FlatScore           // Add score directly
```

**Key Fields**:
```csharp
effectType          // Type of effect
effectValue         // Amount/multiplier
effectDuration      // How long it lasts (0 = instant)
pickupSound         // Audio clip
pickupEffectPrefab  // Particle effect
bobSpeed            // Animation speed
rotationSpeed       // Rotation animation
```

**Creating a Consumable**:
```
Right-click in Assets → Create → Items → Consumable
Set Effect Type: RestoreHealth
Set Effect Value: 20
Assign pickup sound and visual
```

---

### 3. PermanentItemData.cs
**ScriptableObject defining permanent upgrades**

**Item Types**:
```csharp
MaxHealthBoost      // Increase max HP
MaxStaminaBoost     // Increase max stamina
SpeedBoost          // Increase movement speed
JumpHeightBoost     // Increase jump height
ComboWindowBoost    // Extend combo timing
CoinMultiplierBoost // Increase coin rewards
ExtraLife           // Add extra life
DamageReduction     // Reduce damage taken
GrindingStability   // Improve grinding
LandingComfort      // Smoother landings
```

**Rarity System**:
- Common (Gray) - 100% drop weight
- Uncommon (Green) - 70% drop weight
- Rare (Blue) - 40% drop weight
- Epic (Purple) - 20% drop weight
- Legendary (Gold) - 5% drop weight

**Creating a Permanent Item**:
```
Right-click in Assets → Create → Items → Permanent Item
Set Item Type: MaxHealthBoost
Set Stat Multiplier: 1.3 (30% increase)
Set Rarity: Uncommon
Assign icon and description
```

---

### 4. PickupBase.cs
**Base class for all pickups**

**Features**:
- Bobbing animation
- Rotation animation
- Collision detection
- Despawn timer
- Audio/visual feedback
- Event system

**Inheriting From PickupBase**:
```csharp
public class MyPickup : PickupBase
{
    protected override void ApplyEffect(PlayerController player)
    {
        // Apply your specific effect here
    }
}
```

**Animation Loop**:
```
Start
  ↓
Update every frame
  ├─ Bob up and down
  ├─ Rotate around Y axis
  └─ Check for despawn timer
  ↓
OnTriggerEnter with player
  ├─ Check hasBeenCollected flag
  ├─ Call ApplyEffect()
  ├─ PlayPickupFeedback()
  └─ Destroy gameObject
```

---

### 5. ConsumablePickup.cs
**Implementation of PickupBase for consumables**

**Handles**:
- Reading ConsumableItemData
- Applying the specific effect type
- Temporary buff duration
- Audio and particle feedback

**Effect Application**:
```csharp
switch (consumableData.effectType)
{
    case RestoreHealth:
        healthSystem.RestoreHealth(value);
        break;
    case SpeedBoost:
        StartCoroutine(TemporarySpeedBoost());
        break;
    // ... etc
}
```

**Example: Health Consumable**:
```
Place ConsumablePickup prefab in level
Assign ConsumableItemData (Health Restore, 20 HP)
Player touches it
Health restores instantly
Pickup destroyed
```

---

### 6. ItemInventory.cs
**Stores all unlocked and equipped items**

**Responsibilities**:
- Track unlocked permanent items
- Track equipped items
- Save/load from PlayerPrefs
- Provide item lists to UI/systems

**Key Methods**:
```csharp
UnlockItem(item)           // Add to inventory
IsItemUnlocked(item)       // Check if owned
EquipItem(item)            // Add to active bonuses
UnequipItem(item)          // Remove from bonuses
GetUnlockedItems()         // Get all owned
GetEquippedItems()         // Get active
GetEquippedModifiers()     // Get stat changes
```

**Integration**:
```csharp
// When chest gives item
ItemInventory.Instance.UnlockItem(item);
ItemInventory.Instance.EquipItem(item);

// At run start
List<PermanentItemData> equipped = 
    ItemInventory.Instance.GetEquippedItems();
foreach (var item in equipped)
{
    playerStats.ApplyModifier(item.GetStatModifier());
}
```

---

### 7. ChestSystem.cs
**Manages chest spawning and item selection**

**Features**:
- Configurable drop rate (15% default)
- Slot generation (1-3 items per chest)
- Rarity weighting
- Automatic item selection
- Integration with ItemInventory

**Drop Configuration**:
```csharp
chestDropRate = 0.15f;           // 15% per enemy kill
slotWeights = [0.6f, 0.3f, 0.1f] // 60% 1-slot, 30% 2-slot, 10% 3-slot
```

**How It Works**:
```
Enemy dies
  ↓
15% chance to drop chest
  ↓
Generate 1-3 items based on weights
  ↓
Spawn chest prefab at death location
  ↓
Chest opens with animation
  ↓
Player selects (or auto-select after delay)
  ↓
Item unlocked and equipped
  ↓
Chest disappears
```

**Usage in Enemy Script**:
```csharp
void OnDeath()
{
    ChestSystem.Instance.TrySpawnChest(transform.position);
}
```

---

### 8. Chest.cs
**Individual chest instance**

**Animation Sequence**:
1. **Open Lid** (1 second)
   - Lid rotates open
   - Chest bounces up
   
2. **Spin Slots** (0.5 seconds)
   - Item slots spin and scale up
   - Shows all available items
   
3. **Wait for Selection** (1 second)
   - Player can click items
   - Auto-selects if no input
   
4. **Close** (0.5 seconds)
   - Fade out
   - Destroy chest

**Vampire Survivors Style**:
- Multiple items visible
- Slot machine animation
- Cool visual feedback
- Auto-complete if player doesn't choose

---

### 9. PermanentItemApplier.cs
**Integrates permanent items with gameplay**

**Called at Run Start**:
```csharp
permanentItemApplier.ApplyEquippedItems();
```

**Process**:
1. Get PlayerStats component
2. Reset stats to base values
3. Get all equipped items from ItemInventory
4. Apply each item's modifiers to PlayerStats
5. All systems now read correct calculated stats

**Integration with GameManager**:
```csharp
void StartLevel()
{
    playerStats.ResetToBaseStats();
    permanentItemApplier.ApplyEquippedItems();
    // Now all systems use modified stats
}
```

---

## 📋 Complete Workflow

### Consumable Pickup Flow
```
Designer creates ConsumableItemData
  ├─ Set effect type and value
  ├─ Add sound and visuals
  └─ Save as ScriptableObject

Level designer places ConsumablePickup in scene
  └─ Assign ConsumableItemData

Player touches pickup during level
  ├─ Collision detected
  ├─ ApplyEffect() called
  │  ├─ Health: RestoreHealth(20)
  │  ├─ Stamina: RestoreStamina(50)
  │  ├─ SpeedBoost: TemporarySpeedBoost(4 sec)
  │  └─ etc
  ├─ PlayPickupFeedback()
  │  ├─ Play sound
  │  └─ Spawn particles
  └─ Destroy pickup
```

### Permanent Item Flow
```
Designer creates PermanentItemData
  ├─ Set item type and multiplier
  ├─ Set rarity
  └─ Save as ScriptableObject

ChestSystem added available items
  └─ AddAvailableItem(myItem)

Enemy dies during level
  ├─ 15% chance TrySpawnChest()
  └─ Spawn chest at death location

Chest opens with animation
  ├─ Show 1-3 items
  └─ Wait for selection

Player clicks item (or auto-select)
  ├─ ChestSystem.OnChestRewardSelected()
  ├─ ItemInventory.UnlockItem()
  ├─ ItemInventory.EquipItem()
  └─ Save to PlayerPrefs

Next level starts
  ├─ PlayerStats.ResetToBaseStats()
  ├─ PermanentItemApplier.ApplyEquippedItems()
  │  ├─ Get all equipped items
  │  └─ Apply each item's modifier
  └─ All systems use enhanced stats
```

---

## 🔧 Integration Checklist

- [ ] PlayerStats added to player prefab
- [ ] PermanentItemApplier added to player prefab
- [ ] ItemInventory singleton created in scene
- [ ] ChestSystem singleton created in scene
- [ ] Available items added to ChestSystem
- [ ] Chest prefab configured and assigned
- [ ] Consumable pickups placed in levels
- [ ] Enemy death triggers TrySpawnChest()
- [ ] GameManager calls ApplyEquippedItems() at level start
- [ ] All systems read from PlayerStats

---

## 🎮 Example Item Setup

### Tuna (Consumable)
```
Effect Type: RestoreHealth
Effect Value: 20
Duration: 0 (instant)
Sound: Meow (cat eating)
Particles: Golden sparkles
```

### Sugar Rush (Consumable)
```
Effect Type: SpeedBoost
Effect Value: 1.2 (20% faster)
Duration: 4 seconds
Sound: Whoosh
Particles: Speed lines
```

### Bigger Lungs (Permanent)
```
Item Type: MaxStaminaBoost
Stat Multiplier: 1.3 (30% more)
Rarity: Uncommon
Icon: Lung icon
Description: +30% Max Stamina
```

### Lucky Collar (Permanent)
```
Item Type: CoinMultiplierBoost
Stat Multiplier: 1.15 (15% more coins)
Rarity: Rare
Icon: Collar icon
Description: +15% Coin Multiplier
```

---

## 📊 System Architecture Diagram

```
GameManager
  ├─ OnLevelStart
  │  ├─ PlayerStats.ResetToBaseStats()
  │  └─ PermanentItemApplier.ApplyEquippedItems()
  │     ├─ ItemInventory.GetEquippedItems()
  │     └─ Apply each item's modifier
  │
  └─ During Level
     ├─ ConsumablePickup.OnTriggerEnter()
     │  └─ Apply instant effect to player
     │
     └─ Enemy.OnDeath()
        └─ ChestSystem.TrySpawnChest()
           ├─ Generate random items
           └─ Spawn Chest prefab
              └─ Chest.SelectItem()
                 ├─ ItemInventory.UnlockItem()
                 ├─ ItemInventory.EquipItem()
                 └─ Save to PlayerPrefs

All Systems
  ├─ HealthSystem reads PlayerStats.GetMaxHealth()
  ├─ StaminaSystem reads PlayerStats.GetMaxStamina()
  ├─ MovementController reads PlayerStats.GetSpeed()
  └─ ScoreSystem reads PlayerStats.GetCoinMultiplier()
```

---

## 🛠️ Designer Workflow

### Adding a New Consumable
1. Right-click in Assets → Create → Items → Consumable
2. Set name, description, icon
3. Select effect type (RestoreHealth, SpeedBoost, etc.)
4. Set effect value (20 health, 1.2x speed, etc.)
5. Set effect duration (0 for instant, >0 for temporary)
6. Assign sound and particle effects
7. Save

### Adding a New Permanent Item
1. Right-click in Assets → Create → Items → Permanent Item
2. Set name, description, icon
3. Select item type (MaxHealthBoost, SpeedBoost, etc.)
4. Set stat multiplier (1.1 = +10%, 1.3 = +30%)
5. Set rarity (affects drop chance)
6. Save
7. Add to ChestSystem's available items list

### Placing Consumables in Level
1. Drag ConsumablePickup prefab into scene
2. Assign ConsumableItemData ScriptableObject
3. Position in level
4. Done! (Animation and collection are automatic)

---

## 🎯 Key Design Principles

1. **ScriptableObject-Driven**: All data in SO files, no code changes needed
2. **Modular**: Each system is independent and can be extended
3. **Readable Stats**: PlayerStats is single source of truth
4. **Async Effects**: Consumables handle their own coroutines
5. **Persistent**: ItemInventory saves to PlayerPrefs
6. **Extensible**: Easy to add new item types without modifying core
7. **Visual Feedback**: Animations, sounds, particles all configurable

---

## ✅ Testing Checklist

- [ ] ConsumablePickup applies health correctly
- [ ] ConsumablePickup applies stamina correctly
- [ ] Speed boost duration works correctly
- [ ] Invincibility blocks damage for duration
- [ ] Magnet pulls coins correctly
- [ ] Shield absorbs one hit
- [ ] Chest spawns on 15% of enemy kills
- [ ] Chest animation plays smoothly
- [ ] Item selection unlocks item
- [ ] Item appears in ItemInventory
- [ ] Equipped items apply at run start
- [ ] Stats calculated correctly with modifiers
- [ ] Multiple items stack multipliers correctly
- [ ] Save/load works correctly
- [ ] UI displays items correctly

---

## 🚀 Ready to Use!

All systems are complete, documented, and ready for integration. Designers can create items without touching code, and programmers can easily extend with new item types.
