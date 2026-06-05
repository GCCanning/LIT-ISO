# LIT-ISO Asset Catalog & Visual Reference

**Generated:** 2026-05-20

---

## 📦 ASSET INVENTORY BY CATEGORY

### BASE TILES (Plain & Raised Ground)

**Basic Flat Tiles (17 total) - `plains-sliced_*`**
- Plain geometric diamonds
- Used as test/fallback tiles
- Color variations (purple, pink, cyan, yellow)
- Sizes: base00 through base16

**Basic Raised Tiles (18 total) - Cubes**
- Isometric cubes (3D elevation appearance)
- Colors: purple/cyan, brown/cyan, tan/brown, yellow/brown
- Slopes: slope1, slope2 (ramp variants)
- Used for testing height variations

---

## 🌱 PLAINS BIOME DECORATIONS

**9 Decoration Sprites** (`plains-sliced_63` to `plains-sliced_74`)

**What's Available:**
```
🌸 Flowers/Plants:
   - Red flower cluster (plains-sliced_63)
   - Green bush/shrub (plains-sliced_67)
   - Small grass tufts (plains-sliced_68)
   - Wooden log/trunk (plains-sliced_70)

🪨 Rock/Natural:
   - (Additional variants in range 63-74)

📊 Total: 9 sprite variants
    → Creates natural-looking meadows with flowers & plants
    → 1.8% spawn chance per cell (fairly dense)
```

---

## 🏜️ DESERT BIOME DECORATIONS

**22 Decoration Sprites** (`desert-sliced_22` to `desert-sliced_67`)

**What's Available:**
```
🪨 Rocks & Bones:
   - Bone/skull (desert-sliced_22)
   - Rock formations (desert-sliced_23)
   - Stacked stones (desert-sliced_46)
   - Large boulder (desert-sliced_56)

🌴 Plants & Objects:
   - Coral/sea growth (desert-sliced_48)
   - Various rock sizes & colors
   - Wooden structures (planks)
   - Decorative elements

📊 Total: 22 sprite variants
    → Creates sparse, arid landscape
    → 0.8% spawn chance per cell (less dense than Plains)
    → Variety: bones, rocks, plants, structures
```

---

## ❄️ FROZEN CAVE DECORATIONS

**9 Decoration Sprites** (`cave-sliced_22` to `cave-sliced_32`)

**What's Available:**
```
🧊 Ice & Crystal:
   - Ice shelf/platform (cave-sliced_22)
   - Amber/resin chunk (cave-sliced_25)
   - Crystal formations (cave-sliced_26)
   - Stalactite/growth (cave-sliced_31)
   - Frozen spike (cave-sliced_32)

💎 Underground Features:
   - Mineral deposits
   - Fungal growths
   - Crystal clusters

📊 Total: 9 sprite variants
    → Creates underground cave atmosphere
    → 1.2% spawn chance per cell
    → Mix of ice, crystals, and organic growths
```

---

## 🏔️ FROZEN MOUNTAIN & TEMPLE

**Frozen Mountain:**
- ❌ 0% decoration chance (bare mountain terrain)
- Only ground & raised tiles
- No decorations (intentional - harsh environment)

**Temple:**
- ❌ 0% decoration chance
- Only ground & lava tiles
- 🌋 Animated lava tile (special effect)
- No additional decorations

---

## 📋 SUMMARY BY BIOME

| Biome | Flat Ground | Raised Ground | Decorations | Total Sprites | Density |
|-------|-------------|---------------|-------------|---------------|---------|
| **Plains** | ✓ Grass | ✓ Raised Grass | 9 variants | 9 | 1.8% |
| **Desert** | ✓ Sand | ✓ Sand Dunes | 22 variants | 22 | 0.8% |
| **Frozen Mountain** | ✓ Snow | ✓ Peaks | None | 0 | 0% |
| **Frozen Cave** | None | ✓ Floor/Wall | 9 variants | 9 | 1.2% |
| **Temple** | ✓ Stone | ✓ Lava* | None | 0 | 0% |
| **Basic** | ✓ Test | ✓ Test | None | 0 | 0% |

\* Temple has animated lava (shader-based)

---

## 🎨 TILE VISUAL TYPES

### Flat Ground (Level 0)
```
  ◇
 ◇ ◇
  ◇
```
- Diamond shape (isometric view of flat surface)
- Used for: grass, sand, stone, snow
- Color varies by biome

### Raised Ground (Level 1+)
```
  □
 ■ ■
  ■
```
- 3D cube appearance (isometric view)
- Shows height elevation
- Color indicates biome type

### Decorations
```
Small sprites placed ON TOP of tiles
- Flowers, rocks, plants, structures
- Minimal collision
- Visual variety only
```

---

## 📦 ASSET ORGANIZATION IN UNITY

```
Assets/Tilemaps/Isometric/Sprites/
│
├── Basic/
│   ├── Flat/          (17 gray test tiles)
│   └── Raised/        (18 gray cubes + slopes)
│
├── Plains/
│   ├── Flat/          (grass variants)
│   ├── Raised/        (elevated grass)
│   └── Decoration/    (9 sprites: flowers, plants, rocks)
│
├── Desert/
│   ├── Flat/          (sand variants)
│   ├── Raised/        (sand dunes, rocks)
│   └── Decoration/    (22 sprites: bones, rocks, coral, plants)
│
├── FrozenMountain/
│   ├── Flat/          (snow ground)
│   └── Raised/        (snow peaks, rocks)
│
├── FrozenCave/
│   ├── Raised/        (cave floor, walls)
│   └── Decoration/    (9 sprites: ice, crystals, growths)
│
└── Temple/
    ├── Flat/          (stone, lava)
    └── Raised/        (lava walls, platforms)
```

---

## 🔍 SPRITE RESOLUTION & FORMAT

**Standard Specs:**
- **Resolution:** 16×16 pixels (isometric standard)
- **Format:** PNG with transparency
- **Pivot:** (0.5, 0.5) - center of sprite
- **Color Space:** sRGB
- **Filter Mode:** Point (crisp pixel art)

**Isometric Perspective:**
- Top-left to bottom-right is visual "northeast"
- Top-right to bottom-left is visual "southeast"
- Creates that characteristic diamond shape

---

## 📊 SPRITE DISTRIBUTION

```
Total Decoration Sprites: 40

Plains:        ███░░░░░░░  9 (22%)
Desert:        ████████░░  22 (55%)
Frozen Cave:   ███░░░░░░░  9 (22%)
Mountain:      ░░░░░░░░░░  0 (0%)
Temple:        ░░░░░░░░░░  0 (0%)
                           ────
                           40 total
```

**Distribution Notes:**
- Desert has most variety (most hostile biome)
- Plains and Cave have equal options
- Mountain and Temple are stark/minimalist
- Total of 40 unique decoration sprites

---

## 🎯 USAGE IN WORLD GENERATION

### How Decorations Get Placed:

1. **Generation Phase:**
   ```
   For each cell in chunk:
       if height == 0 (flat):
           if random() < decorationChance:
               place_random_decoration_sprite()
   ```

2. **Plains Example:**
   - Cell is flat
   - Random check: 1.8% chance
   - If passes: pick one of 9 flower/plant sprites
   - Place on top of grass tile

3. **Desert Example:**
   - Cell is flat
   - Random check: 0.8% chance (sparser)
   - If passes: pick one of 22 rock/bone sprites
   - Creates scattered rocky landscape

---

## 🚫 CURRENTLY MISSING

### Player/Character Assets
```
❌ Player Sprite Sheets
   - We have idle & walk 8-directional input system
   - But no actual sprites assigned yet!
   - Need: 2 sprite sheets (idle + walk)
   - Format: 8-wide × 2-tall (16 frames total)
   - Resolution: 32×32 or 64×64 recommended
```

### Resource Node Assets
```
❌ Tree Visual
   - Oak Tree node has no sprite
   - Can't see trees in world

❌ Rock Visual
   - Rock node has no sprite
   - Can't see rocks in world

❌ Harvest Sounds
   - No SFX for gathering
   - No audio feedback
```

### Item UI Assets
```
❌ Item Icons
   - 6 items (wood, stone, coin, etc.)
   - Each needs 32×32 icon
   - For hotbar display
```

---

## 💡 WHAT YOU COULD ADD

### Additional Decorations (Priority: Medium)
- More Plains variants (trees, mushrooms, logs)
- More Desert variants (cacti, palm trees, ruins)
- More Cave variants (stalactites, fungal clusters)

### Environmental Objects (Priority: Medium)
- Ambient NPCs (animals, birds)
- Structures (houses, towers, walls)
- Water features (rivers, ponds, waterfalls)

### Interactive Elements (Priority: High)
- **Slime mob** (from your Slime.zip) - can be a visible mob with animations
- Enemy spawn points
- Treasure chests
- Portals/gates

### Weather Effects (Priority: Low)
- Particle effects (falling snow, rain)
- Animated water
- Fog/mist

---

## 📸 Visual Summary

### Current State:
✅ **Complete:** Base tiles, decoration sprites, biome variety  
✅ **Rich:** 40+ decoration sprites across 3 main biomes  
✅ **Detailed:** Isometric perspective, color variants  

❌ **Missing:** Player sprites, node visuals, UI icons  
❌ **Needed:** Enemy/mob sprites, interactive objects  

### World Feels:
- **Plains:** Lush meadows with scattered flowers ✅
- **Desert:** Sparse, rocky, bones scattered ✅
- **Cave:** Crystalline underground ✅
- **Mountain:** Bare, harsh peaks ✅
- **Temple:** Stark, lava-themed ✅

---

## 🎮 Next Steps

1. **Assign sprites to resource nodes** (Trees & Rocks)
2. **Create/import player sprites** (idle & walk animations)
3. **Create item icons** (6 resources for hotbar)
4. **Add harvest sounds** (chop, pick, mine SFX)
5. **Implement mob system** (e.g., Slime mob with animations)

---

**End of Asset Catalog**

For detailed tile type information, see: `TILE_INVENTORY.md`  
For quick parameter reference, see: `TILE_SYSTEM_REFERENCE.txt`
