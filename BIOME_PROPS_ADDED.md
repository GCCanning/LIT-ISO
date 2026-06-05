# Biome Props Added — More Variety In The World

## What Changed

Added more decoration props to **every biome** plus increased decoration density across the board. The world should now feel significantly more populated and visually interesting.

---

## 🌿 Plains Biome

### Before
- 1 decoration tile (`RandomTile_Plains_Plants`) — a single random tile of plants
- Decoration chance: **3%** (sparse)

### After
- Same decoration tile (it already contains multiple plant variants internally)
- Decoration chance: **7%** (over 2× denser!)

**Result:** Grass plains now have noticeably more flowers, mushrooms, and tall grass scattered around.

---

## 🏜️ Desert Biome

### Before
- 1 decoration tile (`RandomTile_Desert_Coral`)
- Decoration chance: **1%** (very sparse)
- Planks were a flat ground variant (mixed with sand)

### After
- **2 decoration tiles**:
  - `RandomTile_Desert_Coral` — Coral/cactus formations
  - `RandomTile_Desert_Planks` — Scattered wooden plank piles (treasure remnants?)
- Decoration chance: **4%** (4× denser)
- Planks moved from terrain variants to decorations (where they belong)

**Result:** Desert has scattered cacti AND mysterious wooden planks, hinting at old shipwrecks or settlements.

---

## ❄️ Frozen Mountain Biome

### Before
- 4 decoration tiles (rocks + snow-capped rocks)
- Decoration chance: **1.8%**

### After
- **6 decoration tiles**:
  - `decoration_rock_1`, `decoration_rock_2` — Plain rocks
  - `decoration_rock_NW` — Directional rock variant
  - `decoration_snowcap_rock_1`, `decoration_snowcap_rock_2` — Snowy rocks
  - 🗡️ **`decoration_sword_in_the_stone`** — Legendary landmark!
- Decoration chance: **3.5%** (nearly 2× denser)

**Result:** Mountains are more textured AND you can now stumble upon the legendary Sword in the Stone! It's a rare encounter due to the 3.5% chance × 1/6 odds of picking that specific decoration.

---

## 🧊 Frozen Cave Biome

### Before
- 1 decoration tile (`RandomTile_FrozenCave_Coral`)
- Decoration chance: **1.2%**

### After
- Same decoration tile (crystal coral formations)
- Decoration chance: **3%** (2.5× denser)

**Result:** Caves now feel like ancient crystalline grottos rather than empty corridors.

---

## 🔥 Temple Biome

### Before
- ❌ **NO decorations** (`null` array)
- Decoration chance: **0%**

### After
- **2 decoration tiles**:
  - 💎 `temple-purpleblock` — Mysterious purple block (puzzle element?)
  - 🪜 `temple-stairs-blue` — Ancient blue stairs
- Decoration chance: **2.5%**

**Result:** Temples are no longer bare stone — they now have mysterious purple blocks and blue staircases scattered through them, suggesting ancient civilizations and puzzles.

---

## 📊 Summary Table

| Biome | Old Props | New Props | Density | Special |
|-------|-----------|-----------|---------|---------|
| Plains | 1 | 1 | 3% → **7%** | More flowers/plants |
| Desert | 1 | **2** | 1% → **4%** | Coral + Planks |
| Frozen Mountain | 4 | **6** | 1.8% → **3.5%** | ⚔️ **Sword in the Stone**! |
| Frozen Cave | 1 | 1 | 1.2% → **3%** | More crystals |
| Temple | 0 | **2** | 0% → **2.5%** | 💎 Purple block + 🪜 Blue stairs |
| Basic | 0 | 0 | 0% | (intentional — fallback only) |

---

## 🎮 How to Apply Changes

The biome ScriptableObjects need to be regenerated, then the world re-rendered:

### Option A: Rebuild the entire scene (recommended)
```
Tools > Iso World > Build And Validate Full Playtest Scene
```
This regenerates biomes + scene in one click.

### Option B: Update biome assets only
```
Tools > Iso World > Create Or Update Biome Definitions
```
Then press Play in your current scene — new chunks loaded after this will use the updated decorations.

### Option C: Existing scene + force chunk regeneration
1. Press Play
2. Walk far enough that new chunks load (they'll use new decorations)
3. Old chunks still have old decorations until reloaded

---

## 🗺️ Where to Find Each Biome

Based on climate parameters in the biome definitions:

| Biome | Climate Range | Where to Look |
|-------|---------------|---------------|
| Plains | Temp 0.28-0.72, Moist 0.38-1, Elev 0-0.72 | Most common (default) |
| Desert | Temp 0.58-1, Moist 0-0.42, Elev 0-0.7 | Hot, dry areas |
| Frozen Mountain | Temp 0-0.34, Moist 0-1, Elev 0.58-1 | Cold, high elevations |
| Frozen Cave | Temp 0-0.4, Moist 0.3-1, Elev 0-1 | Cold, moist areas |
| Temple | Temp 0.45-1, Moist 0-0.65, Elev 0-1 | Warm/dry mid-areas |

The procedural world generates these based on Perlin noise climate maps, so explore!

---

## 🐛 Troubleshooting

### "I don't see any new props"
1. Re-run `Tools > Iso World > Create Or Update Biome Definitions`
2. Press Play
3. Walk around to load new chunks
4. Or: stop and restart Play mode to force regeneration

### "I see SOME new props but not all"
- Mountain decorations are spread across 6 tiles, so each appearance is only 1/6 chance of being any specific one
- Sword in the Stone is therefore rare: 3.5% × (1/6) = **0.58% per tile** (intentional rarity!)
- Walk for a while to see all variants

### "Temple still has no decorations"
- Make sure you ran the menu item to update biome assets
- Temples are uncommon (need warm AND dry climate), so explore until you find one

### "Decorations look weird or misaligned"
- Open the biome asset in `Assets/World/Biomes/`
- Inspect the `decorationTiles[]` array in Inspector
- Verify tile sprites are pointing to actual sprite assets (not "missing")

---

## 🎨 Want To Tune Further?

Each biome's `decorationChance` is a 0-1 value:
- **0** = no decorations
- **0.01** = ~1 per 100 tiles
- **0.05** = ~5 per 100 tiles (moderate)
- **0.10** = ~10 per 100 tiles (busy)
- **0.20** = ~20 per 100 tiles (overgrown)

Adjust directly in:
- `IsoWorldSetup.cs` (lines 230-410, the `CreateOrUpdateBiomes()` method)
- Or open the biome ScriptableObject in `Assets/World/Biomes/` and adjust in Inspector

---

## 🚀 Future Prop Ideas (Not Yet Added)

These are available in the asset folders but not yet wired:

| Biome | Available Tiles | Could Use For |
|-------|----------------|---------------|
| Temple | `molten_NE_0-5` animated lava | Hazardous lava pools |
| Plains | Various sliced tiles | Flowers in red/blue/yellow |
| Desert | Various sliced tiles | Cacti, bones, ruins |
| FrozenCave | cave-sliced_01-43 | Crystal formations, stalactites |

Let me know which props you want to add next!

---

**Status:** Props added to all biomes ✅  
**Last Updated:** 2026-05-20
