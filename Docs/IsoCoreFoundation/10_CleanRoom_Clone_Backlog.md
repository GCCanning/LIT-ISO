# ISO-Core Foundation - 10: Clean-Room Clone Backlog

This is the working backlog for an ISO-CORE-shaped starting point using original
LIT-ISO assets. It is derived from metadata only:

- `iso_core_reference_inventory.json/.csv`: catalog names and category counts.
- `iso_core_sprite_inventory.json/.csv`: sprite counts, dimensions, PPU, pivots,
  texture/sprite names, and atlas membership.

No ISO-CORE pixels, audio, prefabs, scenes, ScriptableObjects, or code are copied
into this project. The target is to match scale, readability, and system coverage
while reauthoring our own content.

## Reference Targets

| Area | ISO-CORE metadata count | LIT-ISO clone target |
|---|---:|---|
| Biomes | 3 | Forest, desert, snow, meadow/beach as extensions |
| Block groups | 6 | Grass, dirt, clay, sand, snow, water/underwater, wood floor |
| Blocks | 50 | 6-8 variants per major surface plus soil/path/specials |
| Items | 114 | Resources, food, seeds, blocks, placeables, tools, potions |
| Placeables | 36 | Stations, storage, paths, lights, bridges, garden objects |
| Tools | 33 | Copper/Iron/Steel x axe/hoe/pickaxe/shovel/sword plus previews |
| Mobs/insects | 15 | Deer, fox, slime, frog, fish, armadillo, bee, butterfly, firefly |
| Crop/plant prefabs | 63 | Crops, saplings, flowers, mushrooms, aquatic plants, cactus set |
| Buildings/product prefabs | 69 | Stations, crafting previews, house/interior props |
| UI | 65 | Inventory, toolbar, crafting, world select, health/energy/temp |

## Art Scale Rules

- Project art target: PPU 16.
- Terrain tiles: 32x16 or 64x32 top diamonds; cube/block visuals may use 64x64.
- Props, mobs, crops: mostly 32x48, bottom-center pivot.
- Icons: 16x16.
- Style: crisp 2:1 isometric pixel art, transparent background, point-filtered,
  no baked drop shadow, no blur or anti-aliasing.
- Optional lighting parity: author normal-map twins later for URP 2D lighting.

## Milestone A - Playable Clone Starter Pack

Goal: replace generated placeholder boxes with original sprites for the existing
foundation loop: explore, collect, craft, build, farm, fight/light ecology.

| Batch | Original assets to author | Target |
|---|---|---|
| A1 terrain tops | grass x8, dirt x6, sand x6, snow x8, clay x4, soil x3, water x4, wood floor x8, stone path x4 | 64x32, PPU 16 |
| A2 terrain blocks | raised grass/dirt/sand/snow/clay/water edge cube faces, cliff side variants | 64x64, PPU 16 |
| A3 resources | tree x6, stump, bush x3, rock x4, copper/iron/gold ore veins, skeletal remains, ruins x4 | 32x48/64x64 |
| A4 stations | workbench, crafting table, furnace, kiln, cooking pot, anvil, chest, bed, lantern | 32x48/64x64 |
| A5 farming | carrot, wheat, potato, pumpkin, sugar beet, voidroot, seed icons, 3-4 growth stages each | crops 32x48, icons 16x16 |
| A6 tools/items | copper/iron/steel axe, hoe, pickaxe, shovel, sword; wood/stone/fiber/ores/bars/food icons | icons 16x16, product props 32x48 |
| A7 mobs | deer, fox, slime, frog, fish, armadillo | 32x48, idle/walk 4 directions first |
| A8 UI | hotbar slot, inventory slot, craft button, health, energy, temperature, world row, item count badges | 16/32 px slices |

Done when the `IsoCoreFoundation` scene no longer depends on generated cube/box
sprites for first-read gameplay.

## Milestone B - System Parity Layer

Goal: add the systems visible in the reference taxonomy that are not yet present
in LIT-ISO foundation.

| System | Required implementation |
|---|---|
| Save/load | `FoundationSaveData` with modified cells, placed objects, inventory, crops, clock, mobs |
| Addressable-style content roots | `Data/`, `Obj/`, `Graphics/` authoring layout with generated SOs/prefabs |
| Survival layer | energy, food restore, temperature/cold/rain/water drain, heat sources |
| Tool durability | per-tool durability and tier restrictions for blocks/nodes |
| Station breadth | furnace/kiln/cooking/anvil/magic table recipe filters |
| Multi-tile placement | bridge, house, windmill, sewing station, gates, left/right station parts |
| World menu | start screen with create/delete/load world slots |
| Mob ecology | biome/day/night spawn tables, population caps, insects as ambience |

## Milestone C - Full Asset Breadth

Goal: approach the reference content shape while keeping every asset original.

- Expand block groups toward 50 block definitions.
- Expand items toward 100+ inventory definitions.
- Expand placeables toward 36 buildables.
- Expand crops/plants toward 60+ vegetation prefabs.
- Expand mobs/insects toward 15 creature classes.
- Add normal-map twins for the final lit 2.5D look.

## AssetForge Prompt Template

Use this shared suffix for all generated original art:

```text
2:1 isometric pixel art for an original cozy survival crafting game, transparent
background, crisp square pixels, PPU 16 target, point-filtered look, no blur, no
anti-aliasing, no painterly rendering, no baked ground shadow, bottom-center
anchor for props and characters, consistent forest/desert/snow palette family.
Do not copy or imitate ISO-CORE assets; create a new original sprite.
```

For terrain tops:

```text
64x32 isometric diamond tile, top surface only, seamless edges, transparent
background, original [SURFACE] variant [N].
```

For props/crops/mobs:

```text
32x48 transparent sprite, bottom-center anchor, readable silhouette, original
[OBJECT], no base plate.
```

For icons:

```text
16x16 pixel-art inventory icon, transparent background, original [ITEM], high
readability at small size.
```

## Current Next Step

Build Milestone A as generated original art batches, then wire those sprites into
the existing `FoundationContent` and renderer before moving to save/load. The
highest-value first import is A1/A2 terrain because it changes the whole read of
the world immediately.
