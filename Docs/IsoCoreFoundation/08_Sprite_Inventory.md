# ISO-Core Foundation — 08: Sprite / Texture Reference Inventory

> **Research-only**, metadata only. Produced by
> `Docs/IsoCoreFoundation/tools/parse_iso_core_sprites.py` (UnityPy) reading the
> installed ISO-CORE Playtest's serialized assets. **No pixels were exported or
> copied** — only names, dimensions, formats, sprite rects, PPU and pivots, to learn
> canonical sizes/sheet layouts so we can **reauthor** our own art. Nothing here is
> wired into the foundation. Companion data:
> `iso_core_sprite_inventory.csv` / `.json`.

## Totals

- **563 Texture2D**, **2,760 Sprite** objects (0 read errors).
- Sprites are sliced from a smaller set of **sheet/atlas textures** (the `sourceTexture`
  column groups sprites by their backing texture).

## Key finding — import scale (PPU)

| PPU (pixels-per-unit) | Sprite count |
|---|---|
| **16** | **2,688** |
| 100 | 55 |
| 15 | 8 |
| 200 | 7 |
| 256 | 2 |

ISO-CORE authors essentially everything at **PPU 16** — i.e. 16 source pixels = 1
world unit. That is the load-bearing number for matching their crisp pixel scale.
(Our placeholder `PlaceholderArt` currently uses PPU 64 for generated shapes; real
reauthored art should target ~16, with a tile diamond ≈ 32×16 or 64×32.)

## Canonical sprite footprints (top sizes)

| Size (px) | Count | Likely use |
|---|---|---|
| 32×48 | 523 | props / characters / mobs (tall, bottom-anchored) |
| 32×32 | 342 | tiles / medium props |
| 16×16 | 330 | icons / small items |
| 64×64 | 320 | large tiles / multi-cell props |
| 52×52 | 128 | medium props |
| 22×22, 17×17 | 116 | small items / UI |
| 24×50, 30×80 | 22 | tall structures |

So: **tiles ≈ 32×32 / 64×64**, **icons ≈ 16×16**, **props/mobs ≈ 32×48** with a
bottom-center pivot — matching the 2:1 iso footprint requirement.

## Category distribution (sprites)

`other 829, mob 747, block 388, tool 199, ui 129, plant 94, biome 85, resource 65,
normalmap 43, tree 37, path 36, effect 31, building 30, crop 22, build 13, item 12`

- **mob 747 sprites** across ~44 mob textures → heavy per-mob **animation frame**
  counts (multi-directional walk/idle sheets).
- **block 388 sprites / 81 textures** → many surface variants per block group
  (confirms the BlockGroup→variants model we already implemented).
- **normalmap (43 sprite + 79 texture)** → confirms the **lit 2.5D** pipeline
  (every art sprite has a normal-map twin for `Light2.5D`).

## Reauthoring guidance (for our own art, later)

When we replace placeholders via AssetForge:

1. **Target PPU 16** project-wide for pixel crispness; point filter, no compression,
   transparent background (already our import convention).
2. **Tiles:** 32×16 or 64×32 diamonds (2:1), centered pivot.
3. **Props / mobs:** 32×48 (or 24×50 for tall), **bottom-center pivot** (~0.5, 0.05).
4. **Icons:** 16×16, used in the hotbar/inventory.
5. Author **mob sheets** as multi-frame, multi-direction (their ~17 frames/mob avg).
6. If we want their lit look, author **normal-map twins** and use URP 2D lights —
   otherwise keep flat-unlit (our current foundation is flat-unlit, which is fine).

> Reminder: these numbers describe ISO-CORE for **learning**. Our art is original;
> we match *scale and layout conventions*, not their pixels.
