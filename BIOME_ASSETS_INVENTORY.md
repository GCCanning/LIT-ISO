# Biome Assets Inventory & Fixes Applied

## Issues Addressed

| Issue | Status |
|-------|--------|
| Shadows not visible from sun | ✅ FIXED — Improved defaults, sorting, visibility |
| Need biome asset inventory | ✅ DONE — See below |
| Walking up 1-block heights | ✅ FIXED — Jump height now 3 blocks |

---

## 🌑 Shadow Fix Details

### Issues found in original DropShadowCaster

1. **Sorting order too small** — `-1` could be overridden by other UI elements
2. **Position too close to player** — `groundYOffset = -0.05f` was nearly at center
3. **Texture used SetPixel inefficiently** — Slow generation, possible flicker
4. **No minimum opacity at night** — Shadow fully invisible during dark periods
5. **No debug output** — Hard to tell if shadow was even created

### What was changed in `DropShadowCaster.cs`

| Setting | Old | New | Reason |
|---------|-----|-----|--------|
| `shadowWidth` | 0.7 | 1.0 | More visible |
| `shadowHeight` | 0.3 | 0.45 | Better isometric ellipse |
| `maxOpacity` | 0.55 | 0.7 | Clearer shadow |
| `minOpacity` | — | 0.15 | Subtle moonlight shadow at night |
| `groundYOffset` | -0.05 | -0.25 | Clearly below player center |
| `sortingOrderOffset` | -1 | -10 | Reliably below player sprite |
| Texture gradient | smoothstep(0.4,1) | smoothstep(0.2,1) | Wider visible area |
| Texture generation | `SetPixel` loop | `SetPixels` batch | Faster, no flicker |
| Debug logging | None | Logs on creation | Easy to verify |

### How to verify shadow is now visible

1. Open Unity, run `Tools > Iso World > Build And Validate Full Playtest Scene`
2. Press Play
3. **Check Console** — you should see:
   ```
   [DropShadowCaster] Created shadow for 'Player' (size=1.0x0.45, opacity=0.7, sortingOrder=-10, layer=Default)
   ```
4. **Look at the player** — there should be a dark ellipse clearly visible beneath the feet
5. **Open the "Day Night Music" GameObject** in Hierarchy and drag `normalizedCycleTime`:
   - `0.0` (dawn): shadow extends west, long
   - `0.25` (noon): shadow small, directly below
   - `0.5` (dusk): shadow extends east, long
   - `0.75` (midnight): shadow faded but still slightly visible

### If shadow STILL not visible

1. **Check the Hierarchy** — Click on Player, expand. Should see "Shadow" child GameObject
2. **Click the Shadow** — Inspector should show:
   - SpriteRenderer enabled ✓
   - Sprite = "GeneratedShadowSprite"
   - Color = dark (alpha ~0.55)
   - Sorting Order = -10
3. **Possible cause**: Camera "Z" position is wrong (should be -10), or player sprite is opaque and covering everything
4. **Workaround**: Increase `shadowWidth` to 2.0 and `groundYOffset` to -0.6 in inspector to make it impossible to miss

---

## 🏔️ Jump Height Fix Details

### What was changed

In both `IsoWorldSetup.cs` and `QuickPlayTestSetup.cs`:

```csharp
playerController.maxWalkStepHeight = 0;   // CANNOT walk up cliffs - must jump
playerController.maxJumpHeight = 3;       // CAN jump up to 3 blocks high
```

### Behavior

| Height Difference | Can Walk Up? | Can Jump Up? |
|-------------------|--------------|--------------|
| 0 (flat) | ✅ Yes | N/A |
| 1 block | ❌ No (blocked) | ✅ Yes (Space) |
| 2 blocks | ❌ No (blocked) | ✅ Yes (Space) |
| 3 blocks | ❌ No (blocked) | ✅ Yes (Space) |
| 4+ blocks | ❌ No (blocked) | ❌ No (blocked) |

### How to test

1. Run `Tools > Iso World > Build And Validate Full Playtest Scene`
2. Press Play
3. Walk into a cliff edge — you should be **blocked** (cannot walk up)
4. Press **Space** to jump up — should clear up to 3 blocks
5. Try jumping into a 4+ block cliff — should be blocked

### If you want different behavior

In the Player GameObject's `IsoPlayerController` component, adjust:
- **`maxWalkStepHeight`**: Set to 1 to allow walking up 1 block automatically (no jump needed)
- **`maxJumpHeight`**: Set higher (e.g. 5) for taller jumps

---

## 🗺️ COMPLETE BIOME ASSET INVENTORY

### Available Biomes (6)

1. **Plains** — Grass, flowers, plants
2. **Desert** — Sand, dunes, coral, planks
3. **Frozen Mountain** — Snow, rocks, mountain peaks
4. **Frozen Cave** — Ice walls, cave floors
5. **Temple** — Stone, lava, purple blocks
6. **Basic** — Generic cubes and bases (fallback)

---

### 🌿 Plains Biome

**Tile Assets:** `Assets/Tilemaps/Isometric/Tiles/Plains/plains-sliced_01.asset` → `plains-sliced_72.asset`
- **72 total tile variants** (numbered 01-72)
- Sliced from sprite sheet: green grass with variations
- Includes: flat grass, raised grass cubes, slopes, edges, decorations

**Rule Tiles (composed of tiles above):**
- `NeighbourTile_Plains_FlatGrass.asset` — Auto-tiles flat grass with edges/corners
- `NeighbourTile_Plains_RaisedGrass.asset` — Auto-tiles raised grass cliffs
- `RandomTile_Plains_FlatGrass.asset` — Random variants of flat grass
- `RandomTile_Plains_RaisedGrass.asset` — Random variants of raised grass
- `RandomTile_Plains_Plants.asset` — Random plant decorations

---

### 🏜️ Desert Biome

**Tile Assets:** `Assets/Tilemaps/Isometric/Tiles/Desert/desert-sliced_01.asset` → `desert-sliced_67.asset`
- **66 total tile variants** (numbered 01-67, missing 24)
- Sand tiles with dune patterns

**Rule Tiles:**
- `NeighbourTile_Desert_FlatSand.asset` — Auto-tiles flat sand
- `NeighbourTile_Desert_FlatSandWave.asset` — Wavy sand variants
- `NeighbourTile_Desert_RaisedSand.asset` — Raised sand cliffs
- `NeighbourTile_Desert_RaisedSandWave.asset` — Wavy raised sand
- `RandomTile_Desert_FlatSand.asset` — Random flat sand
- `RandomTile_Desert_RaisedSand.asset` — Random raised sand
- `RandomTile_Desert_Coral.asset` — Coral decorations
- `RandomTile_Desert_Planks.asset` — Wooden plank decorations

---

### ❄️ Frozen Mountain Biome

**Tile Assets:** `Assets/Tilemaps/Isometric/Tiles/FrozenMountain/` (53 assets)

**Snow Tiles:**
- `snow_center_0` to `snow_center_4` — 5 variations of center snow
- `snow_N`, `snow_S`, `snow_E`, `snow_W` — Edge tiles
- `snow_NE`, `snow_NW`, `snow_SE`, `snow_SW` — Corner tiles
- `snow_corner_N`, `snow_corner_S`, `snow_corner_E`, `snow_corner_W` — Inner corners
- `snow_N_inner`, `snow_S_inner`, `snow_E_inner`, `snow_W_inner` — Inner edges

**Elevated Snow Tiles (raised cliff variants):**
- `elevation_snow_center_0` to `elevation_snow_center_2`
- `elevation_snow_N/S/E/W` and `_inner` variants
- `elevation_snow_corner_N/S/E/W/SE/SW`

**Mountain Tiles:**
- `mountain_0` to `mountain_4` — 5 mountain peak variants

**Slopes:**
- `slope_NE`, `slope_NW`

**Decorations:**
- `decoration_rock_1`, `decoration_rock_2` — Plain rocks
- `decoration_rock_NW` — Directional rock
- `decoration_snowcap_rock_1`, `decoration_snowcap_rock_2` — Snowy rocks
- `decoration_sword_in_the_stone` — Special landmark!

**Rule Tiles:**
- `NeighbourTile_FrozenMountain_FlatSnow.asset`
- `NeighbourTile_FrozenMountain_RaisedSnow.asset`
- `RandomTile_FrozenMountain_FlatSnow.asset`
- `RandomTile_FrozenMountain_FlatGround.asset`
- `RandomTile_FrozenMountain_RaisedSnow.asset`

---

### 🧊 Frozen Cave Biome

**Tile Assets:** `Assets/Tilemaps/Isometric/Tiles/FrozenCave/cave-sliced_01.asset` → `cave-sliced_43.asset`
- **43 total tile variants** (numbered 01-43)
- Ice/crystal cave tiles with blue/purple tones

**Rule Tiles:**
- `NeighbourTile_FrozenCave_RaisedFloor.asset`
- `NeighbourTile_FrozenCave_RaisedWall.asset`
- `RandomTile_FrozenCave_RaisedFloor.asset`
- `RandomTile_FrozenCave_Coral.asset`

---

### 🔥 Temple Biome

**Tile Assets:** `Assets/Tilemaps/Isometric/Tiles/Temple/` (61 assets)

**Temple Stone Tiles:**
- `temple-sliced_01` to `temple-sliced_42` (with some gaps)
- Stone block patterns for temple floors and walls

**Lava/Molten Tiles (Animated):**
- `molten_NE_0` to `molten_NE_5` — Northeast lava flow (6 frames)
- `molten_NW_0` to `molten_NW_5` — Northwest lava flow (6 frames)
- `molten_center` — Center lava
- `molten_cornerN_0` to `molten_cornerN_5` — North corner lava (6 frames)
- `molten_cornerS` — South corner

**Special Tiles:**
- `temple-purpleblock` — Distinctive purple block
- `temple-stairs-blue` — Blue stair

**Rule Tiles:**
- `NeighbourTile_Temple_AnimatedLava.asset` — Animated lava floor
- `NeighbourTile_Temple_LavaWall.asset` — Lava walls
- `RandomTile_Temple_FlatStone.asset`
- `RandomTile_Temple_RaisedStone.asset`
- `RandomTile_Temple_RaisedBlueStone.asset`

---

### 🟦 Basic Biome (Fallback/Prototype)

**Tile Assets:** `Assets/Tilemaps/Isometric/Tiles/Basic/`

**Base Tiles (17 variants):**
- `base00` to `base16` — Flat ground tiles

**Cube Tiles (17 variants):**
- `cube00` to `cube16` — Raised cube blocks

**Slopes:**
- `slope`, `slope2` — Slope tiles

**Rule Tiles:**
- `NeighbourTile_Basic_FlatFloor.asset`
- `NeighbourTile_Basic_RaisedFloor.asset`

---

### 🧱 Collider Tiles (Shared by all biomes)

**Location:** `Assets/Tilemaps/Isometric/Colliders/ColliderTiles/`

| Asset | Purpose |
|-------|---------|
| `cube08-a.asset` | Full cube collider variant A |
| `cube08-b.asset` | Full cube collider variant B |
| `cube08-c.asset` | Full cube collider variant C |
| `slope-a.asset` | Slope collider |
| `stairs-border.asset` | Stairs border |
| `stairs-collider-a.asset` | Stairs collider |
| `tile-border-horizontal.asset` | Horizontal cliff border |
| `tile-border-left.asset` | Left/west cliff |
| `tile-border-right.asset` | Right/east cliff |
| `tile-border-top.asset` | Top/north cliff |
| `tile-border-vertical.asset` | Vertical cliff |
| `tile-half-1.asset` through `tile-half-4.asset` | Half-tile colliders |

---

## 📊 Asset Counts Summary

| Biome | Tile Assets | Rule Tiles | Total |
|-------|-------------|------------|-------|
| Plains | 72 | 5 | 77 |
| Desert | 66 | 8 | 74 |
| Frozen Mountain | 53 | 5 | 58 |
| Frozen Cave | 43 | 4 | 47 |
| Temple | 61 | 5 | 66 |
| Basic | 36 | 2 | 38 |
| **Colliders** | — | 15 | 15 |
| **TOTAL** | **331** | **44** | **375** |

---

## 🎨 Biome Definitions (ScriptableObjects)

**Location:** `Assets/World/Biomes/`

Each biome has a `.asset` ScriptableObject defining:
- Biome name and kind enum
- Temperature/moisture/elevation thresholds (for procedural placement)
- Flat ground tile + variants
- Raised cliff tile + variants
- Decoration tile array
- Spawn parameters (decoration chance, transition multiplier)
- Collider tiles for each direction (N/S/E/W)
- Height noise scale and raised threshold

**Active Biomes (defined in `IsoWorldSetup.cs`):**
1. `BiomeDefinition_Plains.asset`
2. `BiomeDefinition_Desert.asset`
3. `BiomeDefinition_FrozenMountain.asset`
4. `BiomeDefinition_FrozenCave.asset`
5. `BiomeDefinition_Temple.asset`
6. `BiomeDefinition_Basic.asset`

---

## 🎯 Recommended Asset Usage Per Biome

### For Plains (current default biome)
- Flat: `RandomTile_Plains_FlatGrass.asset` (uses many variants for variety)
- Raised: `NeighbourTile_Plains_RaisedGrass.asset` (auto-tiles cliffs)
- Decoration: `RandomTile_Plains_Plants.asset` (sparse decoration)

### For Future Forest Biome (recommendation)
Would need new assets. Could combine:
- Plains green tiles as base
- New tree/log/mushroom decoration tiles
- Darker color tint via biome data

### For Cave Levels
Use Frozen Cave as base, override colors in lighting profile.

---

## 🚨 Missing Assets (Things You DON'T Have)

| Missing | Type | Suggested Action |
|---------|------|------------------|
| Tree node prefabs | Resource node | Generate with Hyper3D or Sprite generator |
| Rock node prefabs | Resource node | Generate or use existing decoration_rock_1 |
| Wood icon | UI sprite (32x32) | Use sprite generator |
| Stone icon | UI sprite (32x32) | Use sprite generator |
| Witch portrait | UI sprite (64x64) | Hyper3D character portrait |
| Harvest sound (chop) | AudioClip | Search free audio library |
| Harvest sound (mine) | AudioClip | Search free audio library |
| Forest biome tiles | Sprite sheet | Could re-tint plains or generate new |
| Player walk/idle sprites | 8-directional sheet | Already attempted via Hyper3D |

---

## ✅ Verification

Run these to confirm everything works:

1. **`Tools > Iso World > Build And Validate Full Playtest Scene`**
2. Press Play ▶
3. Verify Console shows:
   ```
   [DropShadowCaster] Created shadow for 'Player'...
   ✅ Sun Controller found: Sun
   ✅ Player has DropShadowCaster (dynamic shadows enabled)
   ```
4. **Look at player feet** — visible dark shadow ellipse
5. **Walk into a cliff** — should be blocked from walking up
6. **Press Space** — should jump up to 3 block heights

---

**Status:** All three issues addressed ✅  
**Last Updated:** 2026-05-20
