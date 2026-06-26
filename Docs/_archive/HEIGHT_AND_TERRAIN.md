# Multi-Height Terrain & More Biome Variety

## Summary of Changes

| Improvement | Before | After |
|-------------|--------|-------|
| **Terrain height levels** | 0 or 1 only | **0 to 3** (multi-level hills/mountains) |
| **Biome types in world** | 3 (Plains/Desert/FrozenMountain) | **5** (+ Frozen Cave, Temple) |
| **Climate variety** | Slow gradient | **~2× more frequent** |
| **Active view radius** | 3×3 chunks (96×96 tiles) | **5×5 chunks (160×160 tiles)** |
| **Tile stacking** | Single floating top | **Solid columns** (z=1, z=2, z=3) |
| **Peak decorations** | Only flat ground | **Now on mountain peaks too** |

---

## 🏔️ Multi-Height Terrain

### How it works

The old system was binary: a cell was either flat (height 0) or raised (height 1). Now the noise value translates to a height range:

```
noise < threshold           → height 0 (flat)
threshold ≤ noise < +0.33   → height 1
threshold + 0.33 ≤ noise   → height 2
threshold + 0.66 ≤ noise   → height 3
```

Plus a **falloff exponent** (default 1.4) biases toward lower heights, making tall peaks rare and special.

### New IsoWorldChunkManager fields

```csharp
[Header("Terrain Height")]
public int maxTerrainHeight = 3;       // 1 = old behavior, 3 = up to 3-block hills
public float terrainHeightFalloff = 1.4f;  // 1 = linear, 2 = lots of low ground
```

### Tile stacking

When a cell has height = 3, the system now places tiles at z=1, z=2, AND z=3 (solid column) instead of just z=3 (floating tile).

### Decorations on peaks

Mountain peaks, temple plateaus, and other raised areas can now have decorations (rocks, snow caps, purple blocks). Previously decorations only appeared on flat ground.

---

## 🌍 More Biome Types Actually Appear

### Problem
The previous selection logic only ever picked **3 biomes**:
- Plains, Desert, FrozenMountain

Frozen Cave, Temple, and Basic were defined but NEVER actually appeared in the world.

### Fix
New selection logic adds two more biome conditions:

```
🧊 Frozen Cave  →  cold + moist + low elevation
🔥 Temple       →  warm + dry + mid elevation
```

### Updated biome distribution

| Biome | Climate Selector | Where to find |
|-------|------------------|---------------|
| 🧊 **Frozen Cave** | temp <0.32, moist >0.6, elev <0.45 | Cold wet lowlands |
| ❄️ **Frozen Mountain** | temp <0.28 OR elev >0.74 | Cold peaks |
| 🔥 **Temple** | temp >0.55, moist <0.4, elev 0.45-0.7 | Arid uplands |
| 🏜️ **Desert** | temp >0.58, moist <0.42 | Hot, dry areas |
| 🌿 **Plains** | (default) | Everywhere else |

---

## 🌡️ Faster Climate Variety

To make the player encounter different biomes more often, climate noise scales were ~2× increased:

| Noise Scale | Before | After | Effect |
|-------------|--------|-------|--------|
| Temperature | 0.0045 | **0.008** | Biome transitions every ~125 tiles |
| Moisture | 0.004 | **0.007** | Biome transitions every ~140 tiles |
| Continental | 0.0035 | **0.006** | Mountains/lowlands every ~170 tiles |

This means within a single play session, you'll likely walk through 3+ different biomes.

---

## 📏 Bigger View Radius

Active radius increased from **1 (3×3 chunks)** to **2 (5×5 chunks)**.

| Setting | Tiles Visible | Chunks Loaded |
|---------|---------------|---------------|
| activeRadius=1 (old) | 96 × 96 | 9 |
| **activeRadius=2 (new)** | **160 × 160** | **25** |

This gives you ~2.7× more world visible at once. You can now SEE biome transitions in the distance and plan your route.

---

## 🎮 Gameplay Impact

### Movement
- **Walking**: Still cannot walk up any cliff (maxWalkStepHeight=0)
- **Jumping**: Up to 3 blocks high (configured earlier)
- **Multi-step climbing**: Stairs naturally form on slopes — jump multiple times to climb to peaks
- **Camera shake** scales with jump height (already in place from earlier)

### Visual
- **Peaks visible from afar** — tall mountains are now landmarks
- **More striped terrain** — natural hills and valleys
- **Sword in the Stone** more visible — mountain peaks are now decorated
- **Temple plateaus** have ancient blocks on top
- **Frozen caves** appear as cold misty lowlands

---

## 🎛 Tuning Knobs (in Inspector)

On the **`IsoWorldGrid`** GameObject's IsoWorldChunkManager component:

### Height Settings
- `maxTerrainHeight`: 1 (flat) → 8 (extreme)
  - **3 = recommended** (good variety, jumpable)
  - 5+ = too tall to jump (need a custom climbing system)
- `terrainHeightFalloff`: 0.5 → 3
  - 0.5 = TALL everywhere
  - 1.0 = linear distribution
  - **1.4 = recommended** (natural distribution)
  - 2+ = mostly low with rare peaks

### Climate Variety
- `temperatureNoiseScale`: lower = bigger biome regions, higher = patchwork
- Higher = more biome transitions per area
- **0.008 = recommended** (good variety without feeling chaotic)

### Visibility
- `activeRadius`: 1 (tight) → 3 (huge)
  - 1 = 9 chunks (96×96 tiles) — fastest
  - **2 = 25 chunks (160×160 tiles) — recommended**
  - 3 = 49 chunks (224×224 tiles) — may impact performance

---

## 🚀 How to Apply Changes

The settings change automatically when you regenerate the scene:

```
Tools > Iso World > Build And Validate Full Playtest Scene
```

Press Play and walk around — you should immediately see:

1. ✅ **Multi-level hills/mountains** (jump up to climb them!)
2. ✅ **Solid tile stacking** (no more floating tops)
3. ✅ **Visible biome transitions** (try walking 100+ tiles in any direction)
4. ✅ **Frozen cave biomes** (cold blue areas in cold-wet zones)
5. ✅ **Temple biomes** (warm purple temple in warm-dry uplands)
6. ✅ **Decorated peaks** (rocks/snow on mountain tops, blocks on temple platforms)

---

## 🐛 Troubleshooting

### "Everything is too tall / hard to jump"
- Lower `terrainHeightFalloff` to 1.0 (more low terrain)
- Or lower `maxTerrainHeight` to 2

### "I never see Temple/FrozenCave"
- Walk in different directions — climate noise creates regions
- Increase `temperatureNoiseScale` further (e.g. 0.012) to make transitions even more frequent
- Or change `seed` value to get a different world layout

### "Performance is slow"
- Reduce `activeRadius` from 2 to 1
- Reduce `chunkSize` from 32 to 16 (smaller chunks)
- Disable `showTileBorders`

### "Terrain looks wrong / floating tiles"
- Re-run `Tools > Iso World > Build And Validate Full Playtest Scene`
- The tile stacking only applies to NEW chunks; existing ones use old logic

---

## 📊 What "Terrain Types" Means

There are several ways to interpret "more terrain types":

### 1. ✅ Multi-height terrain (done)
Hills, mountains, plateaus with proper stacking

### 2. ✅ More biome diversity (done)
5 different biomes now appearing instead of 3

### 3. 🔧 Future: Different tile per height
Currently the same raised tile is used at all heights. Could enhance to:
- Height 1: grass dirt edge
- Height 2: rocky middle
- Height 3: snow peak

This would require new `peakTile`, `midElevationTile` fields on IsoBiomeDefinition.

### 4. 🔧 Future: Surface kinds (water, lava, ice)
Could add hazardous tiles that damage the player.

If you want any of these, let me know!

---

## 📁 Files Modified

| File | Change |
|------|--------|
| `IsoWorldChunkManager.cs` | Added maxTerrainHeight, terrainHeightFalloff fields. Tile stacking. Expanded biome selection. |
| `IsoWorldSetup.cs` | Set new height fields. Bumped climate noise. activeRadius=2. |
| `QuickPlayTestSetup.cs` | Same updates for existing scenes |

---

**Status:** Multi-height terrain + more biomes wired ✅  
**Last Updated:** 2026-05-20
