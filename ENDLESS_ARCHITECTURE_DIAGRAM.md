# Endless Game System - Architecture Diagram

## System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        ENDLESS GAME SYSTEM                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                   EndlessGameManager                    │   │
│  │  ─────────────────────────────────────────────────────  │   │
│  │  Core Loop:                                             │   │
│  │  - startendlessGame() → Initialize                      │   │
│  │  - Update() → Check loss conditions                     │   │
│  │  - UpdateLevelGeneration() → Stream sections            │   │
│  │  - UpdateProgression() → Track distance/difficulty     │   │
│  │  - StartNextWave() → Spawn enemies                      │   │
│  │  - EndGame() → Game over                                │   │
│  │  - ReturnToMenu() → Exit                                │   │
│  └─────────────────────────────────────────────────────────┘   │
│                             │                                    │
│          ┌──────────────────┼──────────────────┐                │
│          ▼                  ▼                  ▼                │
│  ┌─────────────────┐ ┌─────────────┐ ┌──────────────────┐    │
│  │ Level Generator │ │Wave Controller│ │  FallDetector    │    │
│  ├─────────────────┤ ├─────────────┤ ├──────────────────┤    │
│  │ - Terrain types │ │ - Spawn     │ │ - Fall check     │    │
│  │ - Flat/up/down  │ │   waves     │ │ - Death check    │    │
│  │ - Spline create │ │ - Scale     │ │ - Z bound check  │    │
│  │ - Mesh build    │ │   difficulty│ │ - Health monitor │    │
│  │ - Collider gen  │ │ - Enemy     │ │ - Trigger loss   │    │
│  │                 │ │   pooling   │ │                  │    │
│  └─────────────────┘ └─────────────┘ └──────────────────┘    │
│          │                  │                  │                │
│          └──────────────────┼──────────────────┘                │
│                             ▼                                    │
│                   ┌──────────────────┐                          │
│                   │  Score System    │                          │
│                   ├──────────────────┤                          │
│                   │ - +10/meter      │                          │
│                   │ - +100/enemy     │                          │
│                   │ - Total tracking │                          │
│                   └──────────────────┘                          │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
          │                         │                    │
          ▼                         ▼                    ▼
    ┌──────────────┐        ┌──────────────┐    ┌──────────────┐
    │ GameManager  │        │  UIManager   │    │ PlayerHealth │
    ├──────────────┤        ├──────────────┤    ├──────────────┤
    │ - Game state │        │ - Show HUD   │    │ - Health     │
    │ - Round info │        │ - Game over  │    │ - Stamina    │
    │ - Level data │        │ - Score UI   │    │ - System     │
    └──────────────┘        └──────────────┘    └──────────────┘
```

---

## Game Loop (Per Frame)

```
Update()
│
├─ CheckLossConditions()
│  ├─ playerY < -20? → EndGame("Fell off")
│  ├─ |playerZ| > 10? → EndGame("Wandered off")
│  └─ Health <= 0? → EndGame("Died")
│
├─ UpdateLevelGeneration()
│  ├─ playerX > generationProgress - 50? 
│  │  └─ GenerateNewLevelSection()
│  │     ├─ Create terrain (flat/up/down/gap)
│  │     ├─ Build spline
│  │     └─ Create mesh & colliders
│  │
│  └─ Cleanup old sections
│     └─ sectionX < playerX - 100? → Destroy()
│
├─ UpdateProgression()
│  ├─ newDistance = playerX
│  ├─ scoreSystem.AddScore(distance × 10)
│  ├─ difficultyMultiplier = 1.0 + (distance × 0.05)
│  └─ OnProgressionUpdate event
│
└─ Wave Management
   └─ CurrentWave completed? → StartNextWave()
      ├─ enemyCount = 3 × difficultyMultiplier
      ├─ spawnInterval = 2 / difficultyMultiplier
      └─ waveController.StartWave(waveNumber++)
```

---

## Loss Detection Flow

```
FallDetector.Update()
│
├─ Every 0.1 seconds:
│  │
│  ├─ Check Y Position
│  │  └─ playerY < -20?
│  │     └─ TriggerFailure("Fell off the level!")
│  │
│  ├─ Check Z Distance
│  │  └─ |playerZ| > 10?
│  │     └─ TriggerFailure("Wandered off the track!")
│  │
│  └─ Check Health
│     └─ health <= 0?
│        └─ TriggerFailure("Health depleted!")
│
└─ TriggerFailure(reason)
   │
   ├─ In Endless Mode?
   │  └─ EndlessGameManager.EndGame(reason)
   │     ├─ gameActive = false
   │     ├─ Time.timeScale = 0
   │     ├─ Show GameOverUI
   │     └─ Display stats
   │
   └─ In Normal Mode?
      └─ GameManager.ChangeGameState(LevelFailed)
```

---

## Difficulty Progression

```
Distance Traveled → Difficulty Multiplier → Enemy Count

0m   → 1.0x → 3 enemies/wave
10m  → 1.5x → 4-5 enemies/wave
20m  → 2.0x → 6 enemies/wave
50m  → 3.5x → 10 enemies/wave
80m  → 5.0x → 15+ enemies/wave (capped)

Formula:
  diffMultiplier = MIN(1.0 + (distance × 0.05), 5.0)
  enemyCount = baseCount × diffMultiplier
  spawnInterval = baseInterval / diffMultiplier
```

---

## Scene Setup

```
Scene Root
│
├─ Managers (Empty)
│  ├─ GameManager
│  │  └─ Script: GameManager
│  │     └─ Method: StartEndlessMode()
│  │
│  ├─ EndlessGameManager (NEW)
│  │  └─ Script: EndlessGameManager
│  │     ├─ Fields: (see below)
│  │     └─ Methods: (see above)
│  │
│  ├─ FallDetector (NEW)
│  │  └─ Script: FallDetector
│  │     ├─ CheckFallConditions()
│  │     └─ CheckDeathCondition()
│  │
│  ├─ UIManager
│  │  └─ Script: UIManager
│  │
│  └─ SetupValidator (NEW)
│     └─ Script: EndlessGameSetupValidator
│        └─ ValidateSetup()
│
├─ Player
│  └─ PlayerController
│     ├─ Z-Centering (y = 0 constraint)
│     ├─ Tumble Recovery (3x torque)
│     └─ Movement (X-Y plane)
│
├─ UI Canvas
│  ├─ EndlessGameUI (NEW)
│  │  └─ HUD elements
│  │  └─ Game Over panel
│  │
│  └─ MainMenu
│     └─ "Endless Mode" Button
│        └─ OnClick: GameManager.StartEndlessMode()
│
└─ Terrain
   └─ Generated at runtime
      ├─ Initial sections created
      ├─ New sections spawned ahead
      └─ Old sections destroyed behind
```

---

## Data Flow

```
StartEndlessMode() Click
         │
         ▼
  GameManager.StartEndlessMode()
         │
         ├─ Reset run data
         ├─ Reset systems
         │
         ▼
  ChangeGameState(Endless)
         │
         ▼
  EndlessGameManager.StartEndlessGame()
         │
         ├─ Initialize level generator
         ├─ Spawn player
         ├─ Reset score
         ├─ Reset difficulty (1.0x)
         │
         ▼
  WaitForSeconds(3)
         │
         ▼
  waveController.StartWave(1)
         │
         ├─ Spawn 3 enemies (1.0x)
         │
         ▼
  Game Loop Running
         │
         ├─ Check loss conditions
         ├─ Generate/cleanup terrain
         ├─ Update difficulty
         ├─ Update score
         │
         ├─ Wave 1 completes
         │  ├─ All enemies dead
         │  │
         │  ▼
         │  StartNextWave()
         │  ├─ waveNumber = 2
         │  ├─ difficulty = 1.0 + (distance × 0.05)
         │  └─ Spawn 3 × difficulty enemies
         │
         └─ ...continues infinitely...
         │
         ▼
  Loss Condition Triggered
         │
         ├─ Fall, Wander, or Die
         │
         ▼
  FallDetector.TriggerFailure()
         │
         ▼
  EndlessGameManager.EndGame(reason)
         │
         ├─ Stop waves
         ├─ Freeze time
         ├─ Show game over UI
         │
         ▼
  Player Chooses
         │
         ├─ Retry → StartEndlessGame()
         │
         └─ Menu → ReturnToMenu()
```

---

## Component Dependencies

```
EndlessGameManager
├─ depends on → PlayerController
├─ depends on → EndlessLevelGenerator
├─ depends on → WaveController
├─ depends on → ScoreSystem
├─ depends on → UIManager
├─ depends on → GameManager
└─ depends on → DollyCam

FallDetector
├─ depends on → PlayerController
├─ depends on → HealthSystem
├─ depends on → GameManager
└─ depends on → EndlessGameManager

EndlessGameUI
├─ depends on → EndlessGameManager
├─ depends on → PlayerController
├─ depends on → HealthSystem
├─ depends on → UIManager
└─ depends on → ScoreSystem

GameManager
├─ adds → Endless game state
└─ adds → StartEndlessMode() method
```

---

## File Structure

```
Project Root/
│
├─ Assets/
│  └─ Scripts/
│     ├─ Managers/
│     │  ├─ GameManager.cs (MODIFIED)
│     │  ├─ LevelManager.cs (MODIFIED)
│     │  └─ EndlessGameManager.cs (NEW - 500+ lines)
│     │
│     ├─ Systems/
│     │  ├─ FallDetector.cs (NEW - 150+ lines)
│     │  └─ EndlessGameSetupValidator.cs (NEW - 200+ lines)
│     │
│     ├─ UI/
│     │  └─ EndlessGameUI.cs (NEW - 250+ lines)
│     │
│     └─ (existing scripts unchanged)
│
└─ Documentation/
   ├─ ENDLESS_GAME_SETUP.md (500+ lines - Complete Setup)
   ├─ ENDLESS_MODE_QUICKSTART.md (200+ lines - Quick Start)
   ├─ ENDLESS_SYSTEM_SUMMARY.md (400+ lines - This file)
   └─ ENDLESS_ARCHITECTURE_DIAGRAM.md (this file)
```

---

## State Machine

```
                    ┌─────────────┐
                    │  MainMenu   │
                    └──────┬──────┘
                           │ Click Endless
                           ▼
                  ┌────────────────────┐
                  │    GameManager     │
                  │ StartEndlessMode() │
                  └────────┬───────────┘
                           │
                           ▼
                  ┌────────────────────┐
                  │ Endless Game State │
                  └────────┬───────────┘
                           │
                           ▼
          ┌────────────────────────────────┐
          │  EndlessGameManager.Playing    │
          │  - Generate terrain            │
          │  - Spawn waves                 │
          │  - Update difficulty           │
          │  - Check loss conditions       │
          └────────┬───────────────────────┘
                   │
        ┌──────────┼──────────┐
        │          │          │
        ▼          ▼          ▼
    Fall Off   Wander Off   Health=0
        │          │          │
        └──────────┼──────────┘
                   ▼
         ┌──────────────────────┐
         │ EndlessGameManager   │
         │     .EndGame()       │
         └──────────┬───────────┘
                    │
                    ▼
           ┌────────────────────┐
           │  GameOver State    │
           │ Show Final Stats:  │
           │ - Distance traveled│
           │ - Score            │
           │ - Coins            │
           │ - Time             │
           └────────┬───────────┘
                    │
           ┌────────┴─────────┐
           ▼                  ▼
        Retry              Menu
           │                  │
      ┌────▼──────┐      ┌────▼──────┐
      │Playing    │      │MainMenu   │
      │(restart)  │      │(reload)   │
      └───────────┘      └───────────┘
```

---

## Performance Optimization

```
Memory Management:
  Level Sections: ~100 units behind deleted (prevent accumulation)
  Enemy Pool: Recycled via BatEnemyPoolManager
  Terrain Streaming: Only 3 sections ahead generated
  Check Interval: FallDetector checks 10x/sec not every frame

CPU Optimization:
  Update Calls: Consolidated in EndlessGameManager.Update()
  Physics: Handled by PlayerController in FixedUpdate()
  Rendering: Standard Unity culling handles off-screen objects
  Waves: Sequential spawning (not all at once)

GPU Optimization:
  Spline-based terrain (fewer vertices)
  Material batching (single material per section)
  Collider simplification (box segments)
```

---

## Configuration Tuning

```
Easy Mode Adjustments:
  baseWaveEnemyCount → 2 (was 3)
  baseWaveInterval → 4 (was 2)
  waveScalingPerDistance → 0.03 (was 0.05)
  fallDeathHeight → -30 (was -20)

Hard Mode Adjustments:
  baseWaveEnemyCount → 5 (was 3)
  baseWaveInterval → 1 (was 2)
  waveScalingPerDistance → 0.1 (was 0.05)
  fallDeathHeight → -10 (was -20)

Terrain Variety:
  flatChance → 0.3 to 0.6 (less/more flat)
  uphillChance → 0.2 to 0.4 (less/more climbs)
  downhillChance → 0.2 to 0.4 (less/more drops)
  gapChance → 0.05 to 0.25 (less/more jumps)
```

---

## Next Enhancements

```
Gameplay:
  ├─ Power-ups (shield, speed, invincibility)
  ├─ Boss waves (special enemy types)
  ├─ Special events (earthquakes, weather)
  └─ Skill challenges (rails, jumps)

Meta:
  ├─ Leaderboards (top 10 runs)
  ├─ Achievements (distance milestones)
  ├─ Cosmetics (unlockable skins)
  └─ Daily challenges

Systems:
  ├─ Prestige system (reset with multiplier)
  ├─ Difficulty presets (easy/normal/hard)
  ├─ Custom runs (player-configurable)
  └─ Instant replay (record best runs)
```

---

This architecture provides:
✅ Clear separation of concerns
✅ Easy configuration and tuning
✅ Performance optimization
✅ Extensibility for future features
✅ Maintainability and clarity
