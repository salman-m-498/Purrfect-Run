# Endless Level Generator - Usage Guide

## Overview
The **EndlessLevelGenerator** creates procedurally generated skateable terrain with varied slopes, flat sections, and gaps. It uses the existing spline-based level building system to create smooth, flowing levels.

## Quick Start

### 1. Open the Generator Window
- Go to menu: **Window → Level Tools → Endless Level Generator**
- Or create a new GameObject and add the `EndlessLevelGenerator` component

### 2. Configure Settings
Adjust the parameters in the Inspector or Editor Window:

- **Segments Per Section**: How smooth/detailed each terrain section is (5-10 recommended)
- **Section Spacing**: Distance between major terrain changes (15-25 units)
- **Total Sections**: How many sections to generate (5-20)

### 3. Set Terrain Variation
- **Flat Section Chance**: 0.4 = 40% flat sections
- **Upslope Chance**: 0.3 = 30% uphill sections
- **Downslope Chance**: 0.3 = 30% downhill sections
  - ⚠️ These should sum to approximately 1.0

### 4. Configure Details
- **Max Slope Height**: Tallest slope allowed (10-20 units)
- **Gap Chance**: 0.15 = 15% chance of obstacle gaps (0-0.5)
- **Gap Length**: How long obstacle gaps are (5-15 units)

### 5. Set Appearance
- **Level Material**: Material for the generated mesh
- **Width**: Track width (6 units default)
- **Bank Factor**: How much curves bank (0.5-2.0)
- **Physics Material**: Friction settings

### 6. Generate!
Click **"Generate and Create Level"** to:
1. Generate random control points
2. Create a spline from those points
3. Build mesh and colliders

---

## Terrain Types

### Flat Section 🏁
Horizontal straight path. Good for:
- Recovery sections
- Gaining speed
- Placing obstacles

### Uphill Slope 🏔️
Climbing section. Good for:
- Building momentum
- Jump setups
- Challenging landings

### Downhill Slope ⬇️
Descending section. Good for:
- Speed maintenance
- Gravity-assist tricks
- Testing board control

### Gaps ➡️
Breaks in terrain where obstacles go. Good for:
- Jump obstacles
- Challenge sections
- Pacing variation

---

## Advanced Features

### Random Seed
For reproducible levels:
1. Set **Random Seed** to a fixed number (0, 42, 123, etc.)
2. Same seed = same level every time
3. Set to -1 for random generation each time

### Preview in Editor
- Gizmos show green control points
- Dark green lines show connections
- Helps verify terrain flow before building

### Customization
Extend the script to add:
- Custom terrain features (ramps, loops)
- Obstacle placement
- Difficulty scaling
- Theme variations

---

## Tips & Tricks

### For Difficulty Progression
- Start with more flat sections, fewer slopes
- Gradually increase slope height and gap chance
- Use random seed to ensure variety

### For Performance
- Keep segments reasonable (5-10 per section)
- Use simpler materials/colliders
- Limit total sections if needed

### For Visual Interest
- Mix all terrain types
- Vary section spacing
- Use banking and curves

### For Testing
1. Generate with fixed seed
2. Test the level
3. Note problem areas
4. Adjust settings
5. Regenerate with same seed
6. Compare changes

---

## Example Configurations

### Easy/Beginner
```
Segments Per Section: 5
Section Spacing: 20
Total Sections: 10
Flat Chance: 0.5
Up Chance: 0.3
Down Chance: 0.2
Max Slope: 10
Gap Chance: 0.05
```

### Medium/Intermediate
```
Segments Per Section: 7
Section Spacing: 18
Total Sections: 15
Flat Chance: 0.4
Up Chance: 0.35
Down Chance: 0.25
Max Slope: 15
Gap Chance: 0.15
```

### Hard/Expert
```
Segments Per Section: 10
Section Spacing: 15
Total Sections: 20
Flat Chance: 0.3
Up Chance: 0.4
Down Chance: 0.3
Max Slope: 20
Gap Chance: 0.25
```

---

## Troubleshooting

### "Level looks jumpy"
- Increase **Segments Per Section**
- Decrease **Section Spacing**
- Adjust **Bank Factor**

### "Too many slopes/flats"
- Adjust the chance percentages
- Make sure they sum to ~1.0

### "Gaps not appearing"
- Increase **Gap Chance** (try 0.2-0.3)
- Ensure **Gap Length** is reasonable (>5)

### "Level too hard/easy"
- Adjust **Max Slope Height**
- Change section spacing
- Modify terrain distribution

---

## Integration with Your Game

The generator creates:
1. **SplineComponent** - Control point spline
2. **LevelBuilder** - Generates mesh and colliders
3. **Mesh** - Visual track
4. **BoxColliders** - Per-segment colliders

Everything integrates with your existing player controller!

---

## Future Enhancements

Potential additions:
- Runtime infinite level streaming
- Obstacle placement system
- Difficulty scaling curves
- Level theme variations
- Combo/trick scoring zones
- Audio reactive generation
