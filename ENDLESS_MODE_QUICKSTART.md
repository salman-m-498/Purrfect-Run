# Endless Mode - Quick Start Guide

## 🚀 30-Second Setup

### 1. Add Components to Scene
```
Right-click in Hierarchy:
  → Create Empty → Name "EndlessGameManager" → Add Component → EndlessGameManager
  → Create Empty → Name "FallDetector" → Add Component → FallDetector
  → Create Empty → Name "EndlessGameUI" → Add Component → EndlessGameUI
  → Create Empty → Name "SetupValidator" → Add Component → EndlessGameSetupValidator
```

### 2. Auto-Assign References
```
In Inspector of "SetupValidator":
  → Click "Fix Common Issues" context menu option
  → All references auto-assigned!
```

### 3. Add UI Button
```
In your MainMenu Canvas:
  → Add Button → Name "Endless Mode Button"
  → On Button Click → Select GameManager → GameManager.StartEndlessMode()
```

### 4. Play!
```
Press Play → Click "Endless Mode" button → Skate infinitely → Beat your distance!
```

---

## ✅ Validation

**See setup completion in Inspector**:
1. Select "SetupValidator" GameObject
2. Check SetupStatus in Inspector
3. If not 100%: Right-click → "Fix Common Issues"
4. Right-click → "Validate Setup" to verify

**Or check Console output**:
```
Play game → Console shows:
  ✓ Endless Game Setup Validation
  ✅ EndlessGameManager
  ✅ GameManager
  ✅ FallDetector
  ✅ EndlessLevelGenerator
  ✅ WaveController
  ✅ PlayerController
  ✅ ScoreSystem
  ✅ HealthSystem
  ✅ UIManager
  ✅ EndlessGameUI
  
  Setup Completion: 100%
  ✅ All components configured! Ready to play endless mode.
```

---

## 🎮 Gameplay

**Starting**:
- Click "Endless Mode" button
- Player spawns at start
- Enemies spawn after 3 seconds

**Playing**:
- Skate forward (X axis movement)
- Jump and land for tricks (increases score)
- Avoid falling off edges
- Avoid enemy contact

**Difficulty**:
- Every meter traveled increases difficulty 0.05x
- Enemy count: `3 × difficulty`
- Spawn interval: `2 / difficulty`

**Losing**:
- Fall below Y = -20 → Game Over
- Wander off track (|Z| > 10) → Game Over
- Health reaches 0 → Game Over

**Game Over**:
- Shows distance traveled, score, coins
- "Retry" button → Play again
- "Menu" button → Return to main menu

---

## 📊 Monitor During Play

**HUD Shows**:
- Distance traveled (meters)
- Current score
- Difficulty multiplier
- Health/Max health
- Current wave number

**Console Logs** (if you want to monitor):
- Generating new level section
- Destroying old sections
- Wave spawning
- Loss conditions

---

## 🔧 Customization

### Easy Difficulty
In EndlessGameManager Inspector:
```
Base Wave Enemy Count: 2 (was 3)
Base Wave Interval: 4 (was 2)
Wave Scaling Per Distance: 0.03 (was 0.05)
Fall Death Height: -30 (was -20)
```

### Hard Difficulty
In EndlessGameManager Inspector:
```
Base Wave Enemy Count: 5 (was 3)
Base Wave Interval: 1 (was 2)
Wave Scaling Per Distance: 0.1 (was 0.05)
Fall Death Height: -10 (was -20)
```

### Terrain Generation
In EndlessLevelGenerator Inspector:
```
Flat Chance: 0.5 (was 0.4) - More flat sections
Uphill Chance: 0.25 (was 0.3) - Fewer steep climbs
Downhill Chance: 0.25 (was 0.3) - Fewer steep drops
```

---

## 🐛 Troubleshooting

| Issue | Check | Fix |
|-------|-------|-----|
| "No WaveController" | WaveController exists? | Run "Fix Common Issues" |
| No enemies | WaveController assigned? | Drag WaveController to EndlessGameManager |
| Player doesn't spawn | PlayerController assigned? | Drag PlayerController to EndlessGameManager |
| Levels not generating | LevelGenerator assigned? | Drag EndlessLevelGenerator to EndlessGameManager |
| Too many enemies | Difficulty too high | Reduce Base Wave Enemy Count |
| Game too easy | Difficulty too low | Increase Base Wave Enemy Count |
| Player falls immediately | Start position wrong | Check Y value is ~22.3 |

---

## 📁 Files Created

```
Assets/Scripts/Managers/
  - EndlessGameManager.cs        (Core endless game loop)
  
Assets/Scripts/UI/
  - EndlessGameUI.cs             (HUD and game over screen)
  
Assets/Scripts/Systems/
  - FallDetector.cs              (Loss condition detection)
  - EndlessGameSetupValidator.cs (Setup verification tool)

Documentation/
  - ENDLESS_GAME_SETUP.md        (Complete setup guide - 500+ lines)
  - ENDLESS_MODE_QUICKSTART.md   (This file)
```

---

## 🎯 Key Features

✅ **Infinite Levels**: Procedurally generated terrain  
✅ **Auto Waves**: Enemy spawning scales with difficulty  
✅ **Dynamic Streaming**: Sections generate ahead, cleanup behind  
✅ **Clear Loss States**: Fall or die = game over  
✅ **Score Tracking**: Distance + enemy kills  
✅ **Performance**: Section culling prevents lag  

---

## 📞 Need Help?

1. **Setup not working?** → Open ENDLESS_GAME_SETUP.md (full guide)
2. **Component missing?** → Run "Fix Common Issues"
3. **Not generating levels?** → Check EndlessLevelGenerator assigned
4. **Enemies not spawning?** → Check WaveController assigned
5. **Player falling?** → Check fallDeathHeight value (-20 is correct)

---

## 🚀 Next Play Session

Just hit play, click "Endless Mode", and skate! 

Good luck beating your distance record! 🛹
