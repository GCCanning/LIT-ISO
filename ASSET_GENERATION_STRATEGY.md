# Asset Generation Strategy - LIT-ISO

**Goal:** Maximize asset variety while minimizing credit costs using scaling, color tinting, and smart batching.

---

## 1️⃣ TILES (Ground & Elevated)

**Current Status:** ✅ You have base tiles already
- Plains (grass)
- Desert (sand)
- Frozen Mountain (snow)
- Frozen Cave (stone/ice)
- Temple (stone/lava)

**Strategy: MINIMAL NEW TILES**
- Keep what you have
- Optional: Generate 1-2 variants per biome if desired
- Use **Shader-based color variations** instead (cheaper than new sprites)

**Cost:** 0-10 credits (optional enhancement only)

---

## 2️⃣ BIOME DECORATIONS (Trees, Shrubs, Rocks)

**Current Status:** ❌ Missing trees, shrubs, large rocks

**Optimization Strategy:**
- **1 tree per biome** → Scale variations (0.8x - 1.5x) → Creates 5+ visual variants
- **1 shrub per biome** → Same approach
- **1 rock per biome** → Same approach
- **Color tinting in Unity** → Adjust hue/saturation for seasonal/wear variations

**Per-Biome Breakdown:**
```
Plains:
  • Oak tree (1 base)     → scale 0.8x, 1.0x, 1.2x
  • Grass bush (1 base)   → scale variations
  • Boulder (1 base)      → scale variations
  
Desert:
  • Palm/Cactus (1 base)  → scale 0.9x - 1.3x
  • Desert shrub (1 base)
  • Rock outcrop (1 base)

Frozen Cave:
  • Stalagmite/spike (1 base)
  • Ice formation (1 base)
  • Icy boulder (1 base)

Frozen Mountain:
  • Pine tree (1 base)
  • Frozen shrub (1 base)
  • Snow rock (1 base)

Temple:
  • (none needed - stark aesthetic)
```

**Generation Method:**
- Batch by biome: "Create 3 isometric sprites for plains: oak tree, bush, boulder"
- OR separate prompts for each

**Cost Estimate:** 14-22 credits (3-5 per biome)

**In-Game Implementation:**
```csharp
// Reuse 1 sprite with variations
Sprite treeBase = Resources.Load("Plains/tree");
float scale = Random.Range(0.8f, 1.5f);
Color tint = Color.Lerp(greenBase, greenDark, Random.value);

spriteRenderer.sprite = treeBase;
transform.localScale = Vector3.one * scale;
spriteRenderer.color = tint;
```

---

## 3️⃣ MOBS & ENEMIES

**Current Status:** ❌ Missing all enemy sprites

**Optimization Strategy:**

### **Option A: Minimal (Budget-Friendly)**
Generate **4 core mob types**, create variants via scaling + color:
1. **Slime** (basic melee)
   - 1 idle + 1 walk animation sheet
   - Scale: 0.5x (small), 1.0x (normal), 1.5x (elite)
   - Color: Green (basic), Red (fire), Blue (ice), Purple (poison)

2. **Goblin** (ranged)
   - 1 idle + 1 walk sheet
   - Same scale/color variations

3. **Skeleton** (medium)
   - 1 idle + 1 walk sheet
   - Variations by tint

4. **Boss Variant** (large elite)
   - 1 unique sprite (or scaled up + glow effect)
   - Larger, glowing colors

**Generation Format (each mob):**
```
Create 8-directional pixel art sprite sheet for [MOB TYPE] idle animation.
- Layout: 8 columns × 2 rows = 512×128 pixels
- Each frame: 64×64 pixels
- Directions: N, NE, E, SE, S, SW, W, NW
- Style: Isometric RPG, matches knight character quality
- Transparent background
```

**Cost Estimate:** 20-30 credits (5-8 per mob type, idle + walk)

### **Option B: Rich (More Variety)**
Generate 6-8 mob types with full animations

**Cost Estimate:** 40-60 credits

---

## 4️⃣ ITEMS & INVENTORY

**Current Status:** ❌ Missing item icons and visual pickups

**Optimization Strategy:**

### **Type A: Inventory Icons (32×32 grid)**
Generate **1 icon per resource type**, batch them:

```
Create a pixel art sprite sheet with 6 item icons for an RPG inventory.
Layout: 6 icons × 32×32 pixels = 192×32 total
Icons needed:
1. Wood/Log (brown wooden material)
2. Stone/Rock (gray stone)
3. Gold Coin (yellow/gold)
4. Health Potion (red liquid bottle)
5. Mana Potion (blue glowing orb)
6. Key/Treasure (ornate golden item)

Style: Simple, clear, isometric-friendly
Background: Transparent
```

**Cost:** 3-5 credits (batched single generation)

### **Type B: Pickup Visuals (World Icons)**
Small sprites that appear when items drop in the world (floating coins, wood bundles, etc.)

**Strategy:** Reuse inventory icons OR generate 1-2 unique "pickup" sprites and scale them

**Cost:** 0-5 credits (reuse or minimal new)

### **Type C: Equipment/Weapons** (Optional)
- Sword, shield, armor pieces
- **Strategy:** 1 sword model → scale + color tint for different rarities
- Gold (rare), Silver (common), Iron (basic)

**Cost:** 5-10 credits

---

## 5️⃣ BUILDING ASSETS

**Current Status:** ❌ None

**Optimization Strategy:**

### **Minimal Set (Budget-Friendly)**
Generate **1 archetype per building type**, scale for variety:

```
1. House (wood/stone variant)
   - Small, medium, large via scaling
   - Color: Brown (wood) or Gray (stone)
   - Variations: Thatched roof, tile roof

2. Tower/Watchtower
   - 1 base design
   - Scale: 1x, 1.3x, 1.6x for height variation
   - Color tint for different materials

3. Wall segment
   - 1 horizontal segment
   - Rotate/flip for corners
   - Scale for different heights

4. Gate/Door
   - 1 gate design
   - Color variants (wood, metal, stone)

5. Chest/Container
   - 1 chest sprite
   - Scale + color for rarity (common, rare, epic)

6. Sign/Post
   - 1 post design
   - Generic signpost for waypoints
```

**Generation Method:**
Batch by structure type: "Create 3 isometric building sprites: wooden house, stone tower, wooden fence"

**Cost Estimate:** 15-25 credits (3-5 per building type)

**In-Game Implementation:**
```csharp
// Reuse base building sprite
BuildingType type = BuildingType.House;
Sprite baseSprite = Resources.Load($"Buildings/{type}");

// Vary visually
float scale = Random.Range(0.8f, 1.2f);
Color material = (Random.value > 0.5f) ? woodColor : stoneColor;

spriteRenderer.sprite = baseSprite;
transform.localScale = Vector3.one * scale;
spriteRenderer.color = material;
```

---

## 📊 COMPLETE BUDGET BREAKDOWN

| Category | Count | Strategy | Credits | Total |
|----------|-------|----------|---------|-------|
| **Tiles** | 5 biomes | Keep existing (optional +2 per biome) | 0-10 | 0-10 |
| **Biome Decorations** | 15 assets | 1 per biome (3 per) | 14-22 | 14-22 |
| **Mobs** | 4 types | Idle + Walk (5-8 per) | 20-32 | 20-32 |
| **Inventory Items** | 6 icons | Batched grid | 3-5 | 3-5 |
| **Buildings** | 6 types | 1 base (3-5 per) | 18-30 | 18-30 |
| **Player Character** | 2 animations | Idle + Walk (already budgeted) | 30-40 | 30-40 |
| | | | **TOTAL:** | **85-139** |

**You have 200 credits → Can do EVERYTHING with 85-139, leaving 61-115 for extras!**

---

## 🎯 PRIORITY ORDER

**Phase 1 (Essential - ~45 credits):**
1. Player Idle animation
2. Player Walk animation
3. Basic Slime mob (idle + walk)

**Phase 2 (Core Gameplay - ~40 credits):**
4. Biome decorations (trees, shrubs, rocks)
5. Inventory icons (6 items)

**Phase 3 (Polish - ~30-40 credits):**
6. Additional mobs (Goblin, Skeleton, etc.)
7. Building assets
8. Equipment/weapons

**Phase 4 (Stretch - remaining credits):**
9. Tile variants
10. Boss mob variations
11. Particle effect assets
12. Environmental objects (torches, barrels, etc.)

---

## 💾 ORGANIZATION IN UNITY

```
Assets/
├── Characters/
│   └── Player/
│       ├── idle.png (512×128)
│       ├── walk.png (512×128)
│       └── Animations/ (auto-generated)
│
├── Enemies/
│   ├── Slime/
│   │   ├── idle.png (512×128)
│   │   ├── walk.png (512×128)
│   │   └── (scale/color variations in code)
│   ├── Goblin/
│   │   ├── idle.png
│   │   └── walk.png
│   └── ...
│
├── Items/
│   ├── Icons/
│   │   └── inventory_icons.png (192×32)
│   └── Pickups/
│       └── item_drops.png
│
├── Buildings/
│   ├── house.png
│   ├── tower.png
│   ├── wall.png
│   ├── gate.png
│   ├── chest.png
│   └── sign.png
│
└── Tilemaps/Isometric/Sprites/
    └── (existing tiles stay)
```

---

## 🔧 SCALING + COLOR STRATEGY

Instead of generating 20 variants, store **1 base sprite** and apply:

```csharp
// Scaling
transform.localScale = new Vector3(scale, scale, 1f);

// Color tinting
Color baseColor = new Color(0.5f, 0.8f, 0.3f);  // Base green
Color tinted = baseColor * Random.Range(0.7f, 1.2f);  // Darker/lighter
spriteRenderer.color = tinted;

// Rotation (flipping)
spriteRenderer.flipX = (Random.value > 0.5f);  // Mirror for variety

// Layering
spriteRenderer.sortingOrder = Mathf.RoundToInt(transform.position.y * 10f);
```

**Result:** 1 sprite → 5-10 visual variations with near-zero additional cost!

---

## 📝 NEXT STEPS

1. **Confirm** this strategy works for your game
2. **Prioritize** which phase to start (suggest Phase 1 + Phase 2)
3. **Generate** prompts for the first batch
4. **Implement** scaling/color system in Unity
5. **Test** visual variety with variations

---

**Ready to proceed?**
