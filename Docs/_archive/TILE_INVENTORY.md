# LIT-ISO Tile & Asset Inventory

**Last Updated:** 2026-05-20  
**Project:** LIT-ISO Isometric Procedural World (Unity 6.3)  
**Grid Type:** IsometricZAsY (cellSize: 1, 0.5, 1)

---

## 📊 Project Structure Overview

```
Assets/
├── Tilemaps/
│   ├── Isometric/
│   │   ├── RuleTiles/          ← Procedural tile generation rules
│   │   ├── Sprites/            ← Visual tile sprites (organized by biome)
│   │   ├── Colliders/          ← Collision shapes for elevation
│   │   └── Palettes/           ← Color palettes (if used)
│   └── Hexagonal/              ← Legacy/unused (121+ hex tiles)
├── World/
│   ├── Biomes/                 ← Biome definition assets
│   ├── Items/                  ← Item definitions (resources)
│   ├── ResourceNodes/          ← Harvestable node definitions
│   ├── Lighting/               ← Lighting profiles
│   └── Characters/             ← Player sprites & animations
└── Scripts/
    ├── IsoBiomeDefinition.cs   ← Biome configuration system
    ├── IsoWorldChunkManager.cs  ← Chunk loading & procedural gen
    └── Gameplay/               ← Resource/inventory systems
```

---

## 🌍 BIOMES (6 Total)

Each biome has a complete tile set with Flat Ground, Raised Ground, and Decoration layers.

### 1. **Plains** ✅ (Main biome - most developed)
- **File:** `BiomeDefinition_Plains.asset`
- **Noise Offset:** 0.0
- **Raised Tile Threshold:** 0.68 (68% probability of height)
- **Height Noise Scale:** 0.08
- **Decoration Chance:** 1.8% per cell
- **Tiles:**
  - **Flat Ground:** `NeighbourTile_Plains_FlatGrass` (contextual neighbor-aware)
  - **Variants:** `RandomTile_Plains_FlatGrass`, `RandomTile_Plains_RaisedGrass`
  - **Raised:** `NeighbourTile_Plains_RaisedGrass`
  - **Decorations:** `RandomTile_Plains_Plants` (18 sprite variants)
- **Cliff Borders:** tile-border-left, tile-border-right, tile-border-top, tile-border-vertical

### 2. **Desert** ✅
- **File:** `BiomeDefinition_Desert.asset`
- **Noise Offset:** 101.0 (distinct from Plains)
- **Raised Tile Threshold:** 0.72
- **Height Noise Scale:** 0.065
- **Decoration Chance:** 0.8% per cell
- **Tiles:**
  - **Flat Ground:** `NeighbourTile_Desert_FlatSand`, `NeighbourTile_Desert_FlatSandWave`
  - **Variants:** `RandomTile_Desert_FlatSand`, `RandomTile_Desert_Planks`, `RandomTile_Desert_Coral`
  - **Raised:** `NeighbourTile_Desert_RaisedSand`, `NeighbourTile_Desert_RaisedSandWave`
  - **Variants Raised:** `RandomTile_Desert_RaisedSand`
- **Cliff Borders:** Same as Plains

### 3. **Frozen Mountain** ✅
- **File:** `BiomeDefinition_FrozenMountain.asset`
- **Noise Offset:** 211.0
- **Raised Tile Threshold:** 0.58 (more elevated terrain)
- **Height Noise Scale:** 0.095
- **Decoration Chance:** 0% (no decorations)
- **Tiles:**
  - **Flat Ground:** `NeighbourTile_FrozenMountain_FlatSnow`
  - **Variants:** `RandomTile_FrozenMountain_FlatSnow`, `RandomTile_FrozenMountain_FlatGround`
  - **Raised:** `NeighbourTile_FrozenMountain_RaisedSnow`
  - **Variants Raised:** `RandomTile_FrozenMountain_RaisedSnow`

### 4. **Frozen Cave** ✅
- **File:** `BiomeDefinition_FrozenCave.asset`
- **Noise Offset:** 307.0
- **Raised Tile Threshold:** 0.62
- **Height Noise Scale:** 0.075
- **Decoration Chance:** 1.2% per cell
- **Tiles:**
  - **Flat Ground:** None (cave has no flat ground)
  - **Raised Wall:** `NeighbourTile_FrozenCave_RaisedWall`
  - **Variants:** `RandomTile_FrozenCave_RaisedFloor`, `RandomTile_FrozenCave_Coral`
  - **Special:** Raised floor tiles (cave interior)
- **No cliff borders** (cave environment)

### 5. **Temple** ✅
- **File:** `BiomeDefinition_Temple.asset`
- **Noise Offset:** 419.0
- **Raised Tile Threshold:** 0.70
- **Height Noise Scale:** 0.07
- **Decoration Chance:** 0% (no decorations)
- **Tiles:**
  - **Flat Ground:** `NeighbourTile_Temple_AnimatedLava`
  - **Variants:** `RandomTile_Temple_FlatStone`
  - **Raised:** `NeighbourTile_Temple_LavaWall`
  - **Variants Raised:** `RandomTile_Temple_RaisedStone`, `RandomTile_Temple_RaisedBlueStone`
- **Special:** Animated lava tile (particle/shader effect)

### 6. **Basic** ✅ (Fallback/test)
- **File:** `BiomeDefinition_Basic.asset`
- **Noise Offset:** 523.0
- **Raised Tile Threshold:** 0.66
- **Height Noise Scale:** 0.08
- **Decoration Chance:** 0% (no decorations)
- **Tiles:**
  - **Flat Ground:** `NeighbourTile_Basic_FlatFloor`
  - **Raised:** `NeighbourTile_Basic_RaisedFloor`
- **Use:** Default fallback when biome selection fails

---

## 🎯 TILE TYPES & RULES

### **NeighbourTiles** (14 total) - Contextual/Rule-Based
Tiles that adapt based on neighboring tiles. Unity's `RuleTile` system.

**Purpose:** Create seamless terrain transitions by checking cardinal neighbors (N, S, E, W, NE, NW, SE, SW).

**List:**
```
NeighbourTile_Basic_FlatFloor          → Basic flat ground (contextual)
NeighbourTile_Basic_RaisedFloor        → Basic raised (contextual)

NeighbourTile_Plains_FlatGrass         → Plains flat (contextual) — MAIN TILE
NeighbourTile_Plains_RaisedGrass       → Plains raised (contextual)

NeighbourTile_Desert_FlatSand          → Desert flat (contextual)
NeighbourTile_Desert_FlatSandWave      → Desert flat alt (wave pattern)
NeighbourTile_Desert_RaisedSand        → Desert raised (contextual)
NeighbourTile_Desert_RaisedSandWave    → Desert raised alt (wave)

NeighbourTile_FrozenMountain_FlatSnow  → Mountain flat (contextual)
NeighbourTile_FrozenMountain_RaisedSnow→ Mountain raised (contextual)

NeighbourTile_FrozenCave_RaisedFloor   → Cave raised floor (contextual)
NeighbourTile_FrozenCave_RaisedWall    → Cave raised wall (contextual)

NeighbourTile_Temple_AnimatedLava      → Temple lava (animated, contextual)
NeighbourTile_Temple_LavaWall          → Temple lava wall (contextual)
```

### **RandomTiles** (15 total) - Variety/Decoration
Tiles that pick randomly from a sprite set to add visual variety.

**Purpose:** Prevent monotonous terrain by random sprite selection within the same tile type.

**List:**
```
RandomTile_Plains_FlatGrass            → Grass flat variants
RandomTile_Plains_RaisedGrass          → Grass raised variants
RandomTile_Plains_Plants               → Decorative plants (most decorated)

RandomTile_Desert_FlatSand             → Sand flat variants
RandomTile_Desert_RaisedSand           → Sand raised variants
RandomTile_Desert_Planks               → Wooden structure decorations
RandomTile_Desert_Coral                → Coral/sea life decorations

RandomTile_FrozenMountain_FlatSnow     → Snow flat variants
RandomTile_FrozenMountain_FlatGround   → Rocky ground variants
RandomTile_FrozenMountain_RaisedSnow   → Snow raised variants

RandomTile_FrozenCave_RaisedFloor      → Cave floor variants
RandomTile_FrozenCave_Coral            → Cave coral/growth

RandomTile_Temple_FlatStone            → Stone flat variants
RandomTile_Temple_RaisedStone          → Stone raised variants
RandomTile_Temple_RaisedBlueStone      → Blue stone raised variants
```

---

## 🧱 COLLIDER TILES (15 total) - Elevation/Cliff Collision

These define physical collision shapes for cliff edges when height > 0.

### **Main Cliff Borders** (5 - used across all biomes)
```
tile-border-left         → Cliff edge on West side
tile-border-right        → Cliff edge on East side
tile-border-top          → Cliff edge on North side
tile-border-vertical     → Cliff edge on East/North corners (fallback)
tile-border-horizontal   → Cliff edge on South side
```

### **Cube/Height Colliders** (3)
```
cube08-a                 → Full cube collision (8-unit height)
cube08-b                 → Partial cube (variant)
cube08-c                 → Partial cube (variant)
```

### **Slope/Stair Colliders** (4)
```
slope-a                  → Ramp up (45-degree slope)
stairs-border            → Stair outline
stairs-collider-a        → Stair collision shape
```

### **Half-Height Colliders** (4)
```
tile-half-1              → 50% height (quarter tile)
tile-half-2              → 50% height (variant)
tile-half-3              → 50% height (variant)
tile-half-4              → 50% height (variant)
```

**How Used:** In `IsoWorldChunkManager.PaintChunk()` - cliff colliders are placed when a raised tile is adjacent to a flat tile, creating walkable elevation transitions.

---

## 🎨 PALETTES

**Location:** `Assets/Tilemaps/Isometric/Palettes/`  
**Status:** ⚠️ Empty/Unused

No color palette assets found in current scan. The project may use:
- Direct sprite colors (no palette swapping)
- Lighting profiles for day/night color shifts (in `Assets/World/Lighting/`)
- Shader-based color manipulation

---

## 🖼️ SPRITES BY BIOME

### **Basic**
- **Flat:** 17 tiles (base00–base16.png) — gray test sprites
- **Raised:** 18 tiles (cube00–cube16.png + slope1, slope2) — gray cubes

### **Plains** 
- **Flat:** Grass variants
- **Raised:** Elevated grass
- **Decoration:** 18 sprites (`plains-sliced_XX.png`) — flowers, grass tufts, rocks

### **Desert**
- **Flat:** Sand variants
- **Raised:** Sand dunes & rock formations
- **Decoration:** ~10 sprites (coral, cactus, palm trees, bones)

### **Frozen Mountain**
- **Flat:** Snow/ice ground
- **Raised:** Snow peaks, rocky outcrops
- **Decoration:** None (bare mountain biome)

### **Frozen Cave**
- **Raised:** Cave floor & walls (no flat)
- **Decoration:** ~8 sprites (ice crystals, fungal growths)

### **Temple**
- **Flat:** Stone/lava ground
- **Raised:** Lava walls, stone platforms
- **Decoration:** None (architectural biome)

---

## 🎁 GAMEPLAY ITEMS (6 total)

**Location:** `Assets/World/Items/`

### Resource Items

| Item ID | Display Name | Category | Max Stack | Use |
|---------|-------------|----------|-----------|-----|
| `wood` | Wood | Resource | 999 | Tree harvesting |
| `pinecone` | Pinecone | Resource | 999 | Tree harvesting (rare) |
| `treesap` | Treesap | Resource | 999 | Tree harvesting (rare) |
| `stone` | Stone | Resource | 999 | Rock harvesting |
| `copper_ore` | Copper Ore | Resource | 999 | Rock harvesting |
| `coin` | Coin | Currency | 9999 | Currency (future trading) |

**Fields per Item:**
- `itemId` — unique string identifier
- `displayName` — UI label
- `icon` — 32×32 sprite for hotbar (NOT YET ASSIGNED)
- `category` — enum: Resource, Tool, Consumable, Currency
- `maxStack` — inventory limit

---

## ⛏️ RESOURCE NODES (2 total)

**Location:** `Assets/World/ResourceNodes/`

### **Node_OakTree**
- **Display Name:** Oak Tree
- **Spawn Chance:** 6% per eligible flat cell
- **Harvest Cooldown:** 45 seconds (respawn time)
- **Harvest Radius:** 1.3 units (distance player must be within)
- **Minimum Spacing:** 2.5 units (trees spawn at least this far apart)
- **Drops:**
  | Item | Min | Max | Chance |
  |------|-----|-----|--------|
  | Wood | 1 | 3 | 100% (guaranteed) |
  | Pinecone | 0 | 2 | 40% |
  | Treesap | 0 | 1 | 20% |
- **Sprites:** None assigned (needs sprite art)
- **Harvest Sound:** None assigned (needs SFX)

### **Node_Rock**
- **Display Name:** Rock
- **Spawn Chance:** 4% per eligible flat cell
- **Harvest Cooldown:** 60 seconds
- **Harvest Radius:** 1.2 units
- **Minimum Spacing:** 3.0 units (rocks space out more)
- **Drops:**
  | Item | Min | Max | Chance |
  |------|-----|-----|--------|
  | Stone | 1 | 3 | 100% (guaranteed) |
  | Copper Ore | 0 | 1 | 30% |
- **Sprites:** None assigned (needs sprite art)
- **Harvest Sound:** None assigned (needs SFX)

**How Spawned:** During `IsoWorldChunkManager.PaintChunk()`, after placing tiles, nodes are instantiated at random cells within each chunk. Deterministic hashing ensures the same node always appears at the same world position (seeded by world seed).

---

## 🔗 TILE GENERATION PIPELINE

```
IsoWorldSetup.CreateInfinitePlainsPrototype()
    ↓
IsoWorldChunkManager.RefreshChunks()
    ↓
IsoWorldChunkManager.PaintChunk()
    ├─ Select biome at (worldX, worldY) via Perlin noise
    ├─ Sample height (Perlin) → 0 (flat) or 1 (raised)
    ├─ Place flat ground tile (using NeighbourTile rule)
    ├─ Place elevation tile if height > 0
    ├─ Place cliff colliders at height edges
    ├─ Place decoration tile if flat + dice roll < chance
    └─ Spawn resource nodes (ResourceNode.cs) on flat cells
```

### Key Functions

| Script | Method | Purpose |
|--------|--------|---------|
| `IsoWorldChunkManager` | `RefreshChunks()` | Load/unload chunks around player |
| `IsoWorldChunkManager` | `PaintChunk()` | Generate all tiles for a 32×32 chunk |
| `IsoBiomeDefinition` | `GetFlatGroundTile()` | Pick tile based on biome & rules |
| `IsoBiomeDefinition` | `GetRaisedGroundTile()` | Pick raised tile |
| `IsoBiomeDefinition` | `GetDecorationTile()` | Pick random decoration |
| `IsoBiomeDefinition` | `ShouldPlaceDecoration()` | Chance check for decorations |

---

## 📌 NAMING CONVENTIONS

### NeighbourTile Naming
```
NeighbourTile_[Biome]_[Height][Type]
```
- `[Biome]` — Plains, Desert, FrozenMountain, FrozenCave, Temple, Basic
- `[Height]` — Flat, Raised, Wall (cave-specific)
- `[Type]` — Ground, Sand, Grass, Floor, Lava, Snow, etc.
- **Optional:** Wave, Sand, Planks (variants for same biome/height)

### RandomTile Naming
```
RandomTile_[Biome]_[Height][Type]
```
- Same structure, but contains **multiple sprite variants** internally
- Used for visual variety (grass has 8+ variants in one tile)

### Collider Naming
```
tile-border-[direction]    → Cliff edge (North, South, East, West)
tile-half-[1-4]            → Quarter-height variants
cube08-[a-c]               → Full-height cube variants
slope-[a]                  → Ramp collision
stairs-[border/collider-a] → Stair shapes
```

### Item Naming
```
Item_[ResourceName].asset
```
- `Item_Wood.asset`, `Item_Stone.asset`, `Item_CopperOre.asset`

### Biome Naming
```
BiomeDefinition_[BiomeName].asset
```
- `BiomeDefinition_Plains.asset`, `BiomeDefinition_Desert.asset`

### Node Naming
```
Node_[NodeName].asset
```
- `Node_OakTree.asset`, `Node_Rock.asset`

---

## ⚠️ MISSING/TODO

| Category | Issue | Impact |
|----------|-------|--------|
| **Item Icons** | No sprites assigned to Item definitions | Hotbar shows placeholder |
| **Node Sprites** | ResourceNode nodeSprites[] empty | Trees/rocks not visible, can't harvest |
| **Node Sounds** | harvestSound fields null | No audio feedback when harvesting |
| **Palettes** | No color palettes created | Can't palette-swap sprites |
| **Hexagonal Tiles** | 121+ hexagonal tiles in project | Unused (isometric is active) |
| **Frozen Cave Deco** | Has decoration chance but may not spawn | Check biome setup |
| **Collision Testing** | Cliff colliders not manually tested | May have gaps or issues |

---

## 🚀 USAGE EXAMPLES

### **To add a new resource item:**
1. Create new `ItemDefinition` asset in `Assets/World/Items/`
2. Set itemId, displayName, icon, category, maxStack
3. Add to `ResourceNodeDefinition.drops[]` array

### **To increase tree spawn rate:**
Edit `Assets/World/Biomes/BiomeDefinition_Plains.asset`:
- Change `spawnChance: 0.06` → e.g., `0.10` (10%)
- Affects the `ResourceNodeDefinition` tied to Plains biome

### **To adjust decoration frequency:**
Edit biome definition:
- Change `decorationChance: 0.018` → e.g., `0.04` (4% per cell)
- Applies to `RandomTile_Plains_Plants` sprites

### **To add a new biome:**
1. Create new `BiomeDefinition_[Name].asset`
2. Point to appropriate NeighbourTiles, RandomTiles, colliders
3. Set unique `biomeNoiseOffset` (e.g., 600+)
4. Add to `IsoWorldChunkManager.biomes[]` array
5. Provide sprites in `Assets/Tilemaps/Isometric/Sprites/[Name]/`

---

## 📐 Technical Reference

### Grid Configuration
- **Layout:** IsometricZAsY (Y axis = height depth)
- **Cell Size:** (1, 0.5, 1) units
- **Chunk Size:** 32×32 cells per chunk
- **Active Radius:** 1 chunk in all directions (3×3 = 9 chunks loaded)
- **Transparency Sort:** Custom axis (0, 1, -0.26) for proper isometric layering

### Procedural Parameters
- **World Seed:** Configurable (default 12345)
- **Biome Blend Scale:** 0.0125 (controls biome region size)
- **Height Noise Multiplier:** Per-biome (0.065 to 0.095)
- **Raised Threshold:** Per-biome (0.58 to 0.72 Perlin value)

---

## ✅ Verification Checklist

When adding/modifying tiles:

- [ ] Sprite is 16×16 pixels (isometric standard)
- [ ] Sprite has correct pivot for isometric (0.5, 0.5)
- [ ] NeighbourTile rule configured with proper neighbor checks
- [ ] RandomTile has at least 2 sprite variants
- [ ] Collider tile referenced if elevated
- [ ] Biome definition points to correct tiles
- [ ] Tested in InfinitePlainsPrototype scene with terrain generation
- [ ] No z-order/layering issues in final render

---

**End of Inventory**
