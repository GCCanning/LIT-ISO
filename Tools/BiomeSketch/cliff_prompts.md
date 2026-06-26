# Cliff Prop PixelLab Prompts

Generated for LIT-ISO. These prompts are implemented as a runnable script — see `Tools/PixelLab/generate_cliff_tiles.py`. All sprites are generated at **64×32 px** (tile_size=64 isometric 2:1), transparent background.

Import settings in Unity (reference: `pl_stone_01` as baseline):
- PPU: 64 (matches tile_size, 1 tile = 1 unit wide)
- Pivot: Bottom-centre
- Compression: None (sprites)
- Filter: Point (no filter)

---

## Tier 1 — Mesa / Plateau Tops (Row 1 of reference)
Flat diamond-shaped tile tops. Used as surface tiles for elevated plateaus — no vertical depth. Match the stone tile `pl_stone_01..03` colour palette.

**PPU: 32** (same as standard tile, 1-cell footprint)

| Prompt | Output name |
|---|---|
| `isometric plateau top sandy stone pixel art transparent background no base` | `cliff_mesa_top_01` |
| `isometric mesa top mossy grey stone diamond tile pixel art transparent background no base` | `cliff_mesa_top_02` |
| `isometric plateau top dry cracked stone pixel art transparent background no base` | `cliff_mesa_top_03` |

---

## Tier 2 — Large Cliff Blocks (Row 2 of reference)
Volumetric stacked stone blocks, roughly 2×1 isometric cells wide and 2 height-tiers tall. These are placed as props at cliff edges. Grey stone with layered horizontal fracture lines; optional thin grass tuft on top.

**PPU: 22** (sprite covers ~2 cell widths, ~2 height tiers)

| Prompt | Output name |
|---|---|
| `isometric large cliff rock block formation grey stone layered fractures pixel art transparent background no ground no base` | `cliff_block_lg_01` |
| `isometric large cliff boulder grey stone with grass tuft on top isometric pixel art transparent background no ground no base` | `cliff_block_lg_02` |
| `isometric large rocky cliff formation dark grey stone cracked layered pixel art transparent background no ground no base` | `cliff_block_lg_03` |
| `isometric cliff rock mass large warm grey stone rough surface isometric pixel art transparent background no ground no base` | `cliff_block_lg_04` |
| `isometric oversized boulder cliff formation grey stone with lichen patches pixel art transparent background no ground no base` | `cliff_block_lg_05` |

---

## Tier 3 — Small Cliff Rocks (Row 3 of reference)
Compact single-cell rocky outcrops. Shorter, blockier than Tier 2. Used to scatter at cliff bases and along raised terrain edges for visual variety. Same grey stone palette.

**PPU: 32** (1-cell footprint, about 1 height-tier tall)

| Prompt | Output name |
|---|---|
| `isometric small cliff rock outcrop grey stone pixel art transparent background no ground no base` | `cliff_rock_sm_01` |
| `isometric rocky outcrop small dark grey stone cracked isometric pixel art transparent background no ground no base` | `cliff_rock_sm_02` |
| `isometric compact stone boulder grey pixel art transparent background no ground no base` | `cliff_rock_sm_03` |
| `isometric small cliff block grey stone rough layered pixel art transparent background no ground no base` | `cliff_rock_sm_04` |
| `isometric stone chunk grey warm tones isometric pixel art transparent background no ground no base` | `cliff_rock_sm_05` |

---

## Style Notes for All Prompts
- **Colour palette:** warm mid-grey (#8e8e82 range), dark shadow faces (#4a4a42), highlight edges (#c8c8b8). Match `pl_stone_01`.
- **Isometric angle:** 2:1 diamond ratio, standard Unity isometric (26.57° elevation).
- **No ground plane.** The prop floats — the tilemap underneath provides the ground.
- **No drop shadow baked in.** Runtime sort order handles depth.
- **Pixel density:** crisp pixel edges, no anti-aliasing at the silhouette.

---

## In-Game Naming Convention
Register generated sprites under `Assets/Resources/Props/` as:

```
cliff_block_lg_01.png
cliff_block_lg_02.png
...
cliff_rock_sm_01.png
...
cliff_mesa_top_01.png
```

Reference `prop_cliff_block_lg` group in `FoundationContent.BuildDefault()` when wiring up worldgen cliff-edge placement.
