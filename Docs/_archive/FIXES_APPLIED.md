# Fixes Applied — Shadow Visibility, Jump Height, Asset Inventory

## Summary

| Issue | Status | Files Touched |
|-------|--------|---------------|
| Shadows not visible | ✅ FIXED | DropShadowCaster.cs, IsoLightingController.cs, IsoWorldSetup.cs, QuickPlayTestSetup.cs |
| Walking up 1-block heights | ✅ FIXED | IsoWorldSetup.cs, QuickPlayTestSetup.cs |
| Need asset inventory | ✅ DONE | BIOME_ASSETS_INVENTORY.md (new) |

---

## 🌑 Shadow Visibility Root Cause

The biggest issue was a **conflict between IsoLightingController and DropShadowCaster**:

- `IsoLightingController.CaptureSceneRenderers()` was capturing ALL SpriteRenderers in the scene, including the dynamically-created shadow sprite
- Each frame, `ApplyRendererTints()` would multiply the captured color by the lighting profile's sprite tint
- This **overwrote** the DropShadowCaster's per-frame color updates
- Result: shadow alpha was constantly being reset to whatever value was captured initially

**Fix:** Added a `DropShadowSpriteMarker` component on shadow GameObjects. `IsoLightingController` now skips any sprite with this marker, leaving shadow color management entirely to `DropShadowCaster`.

### Other Shadow Improvements

1. **Bigger by default** — 1.0×0.45 (was 0.7×0.3)
2. **More opaque** — 0.7 (was 0.55)
3. **Better positioning** — `groundYOffset = -0.25` (was -0.05) — clearly below feet
4. **Stronger sorting** — `sortingOrderOffset = -10` (was -1) — guarantees below player sprite
5. **Minimum opacity at night** — 0.15 (was 0) — still visible by moonlight
6. **Faster texture gen** — Uses `SetPixels()` batch instead of `SetPixel()` loop
7. **Debug log on creation** — Console shows when shadow is created

---

## 🏔️ Jump Height Change

Changed in both `IsoWorldSetup.cs` and `QuickPlayTestSetup.cs`:

```csharp
// OLD
playerController.maxJumpHeight = 1;  // Only jump 1 block

// NEW
playerController.maxJumpHeight = 3;  // Can jump up to 3 blocks
```

`maxWalkStepHeight` stays at 0, meaning the player still **cannot walk up cliffs** — they must press **Space** to jump.

---

## 🗺️ Asset Inventory Highlights

The full inventory is in `BIOME_ASSETS_INVENTORY.md`. Quick reference:

| Biome | # Tiles | Notable Features |
|-------|---------|------------------|
| **Plains** | 72 | Grass, flowers, plants |
| **Desert** | 66 | Sand, dunes, planks, coral |
| **Frozen Mountain** | 53 | Snow, rocks, sword-in-stone! |
| **Frozen Cave** | 43 | Ice/crystal cave tiles |
| **Temple** | 61 | Stone, ANIMATED LAVA (6 frames), purple blocks, stairs |
| **Basic** | 36 | Generic cubes and bases (fallback) |
| **Colliders** | 15 | Shared cliff/slope colliders |

**Total: 331 tile assets + 44 rule tiles = 375 assets across 6 biomes**

**Special discoveries:**
- 🗡️ Frozen Mountain has a `decoration_sword_in_the_stone` tile — could be a quest landmark
- 🔥 Temple has 6-frame animated lava tiles (NE_0 through NE_5)
- 💎 Temple has a unique `temple-purpleblock` for puzzles
- 🏗️ Basic biome cubes can be used for prototyping any building

---

## How to Verify

### Test 1: Build a fresh scene
```
Tools > Iso World > Build And Validate Full Playtest Scene
```
Press Play. Console should show:
```
[DropShadowCaster] Created shadow for 'Player' (size=1x0.45, opacity=0.7, sortingOrder=-10, layer=Default)
✅ Sun Controller found: Sun
✅ Player has DropShadowCaster (dynamic shadows enabled)
```

### Test 2: Visual confirmation
- Look at player's feet → dark ellipse visible
- Click "Day Night Music" → drag `normalizedCycleTime` slider
- Watch shadow shift west↔east as day progresses

### Test 3: Jump height
- Walk into a cliff (height ≥ 1) → BLOCKED
- Press **Space** while moving into cliff → JUMP UP
- Try cliffs of 1, 2, 3 blocks → ALL CLEAR
- Try a 4+ block cliff → BLOCKED (intended)

---

## Files Modified

1. **`Assets/Scripts/DropShadowCaster.cs`**
   - Better defaults (size, opacity, position, sorting)
   - Added `minOpacity` for moonlight shadows
   - Added `DropShadowSpriteMarker` component
   - Faster texture generation via SetPixels
   - Debug log on creation

2. **`Assets/Scripts/IsoLightingController.cs`**
   - `CaptureSceneRenderers()` now skips DropShadowSpriteMarker

3. **`Assets/Scripts/Editor/IsoWorldSetup.cs`**
   - `maxJumpHeight = 3` (was 1)
   - Updated DropShadowCaster defaults to match new visibility settings

4. **`Assets/Scripts/Editor/QuickPlayTestSetup.cs`**
   - `maxJumpHeight = 3` (was 1)
   - Updated DropShadowCaster defaults

## Files Created

1. **`BIOME_ASSETS_INVENTORY.md`** — Full asset catalog
2. **`FIXES_APPLIED.md`** — This document

---

## If Shadow STILL Not Visible

Try these debug steps:

1. **Confirm shadow GameObject exists**
   - In Play mode, click Player in Hierarchy
   - Expand → look for "Shadow" child
   - If missing, DropShadowCaster failed to run (check Console for errors)

2. **Force visibility (Inspector tweaks)**
   - Click the Shadow GameObject
   - In SpriteRenderer: set Color alpha to 1.0 (full opacity)
   - In DropShadowCaster (on Player): set `maxOpacity` = 1.0, `shadowWidth` = 2.0

3. **Check sorting**
   - Shadow sortingLayer should match Player's layer
   - sortingOrder should be NEGATIVE (e.g. -10)
   - If shadow is on a separate "UI" or wrong layer, fix in Project Settings → Tags and Layers

4. **Check the lighting profile**
   - If using a very dark night profile, `minOpacity = 0.15` may still be visible only faintly
   - Set the Day/Night Music's normalizedCycleTime to 0.25 (noon) for max visibility

5. **Last resort: Manual creation**
   - Drag a simple black ellipse PNG into the project
   - Manually create a child sprite under Player named "TestShadow"
   - Confirm it appears — this isolates whether the issue is positioning or DropShadowCaster

---

**Status:** All three issues addressed ✅  
**Last Updated:** 2026-05-20
