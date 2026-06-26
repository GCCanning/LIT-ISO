# PixelLab Asset Audit & Implementation Plan (2026-06-13)

Task #23-25. Covers: PixelLab generator/asset audit, what's wired vs. not, the
implementation plan, and the ring-of-biomes test world spec.

## 1. Generator script inventory (`Tools/PixelLab/`)

- `pixellab_common.py`, `pixellab_proxy.py` — shared API client/auth.
- Tiles: `generate_tilesets.py` + `tiles_1_plains.bat` .. `tiles_9_planks.bat`
  (9 tile families submitted: plains, beach, snow, mountain_stone, dungeon_stone,
  farming, interior, planks, + 1 more).
- Props: `generate_props.py` + `props_1_plains_submit.bat` .. `props_10_lights.bat`
  (10 prop batches: plains, forest, snow, mountain/ores, camp, buildings, tavern,
  library/guild, town, lights).
- Catalog/special: `generate_catalog_props.py`, `catalog_1_ores_submit.bat`,
  `catalog_1b_chest.bat`, `catalog_2_status.bat`, `make_chest_ranks.py`.
- Variants/blends: `generate_palette_variants.py`, `generate_texture_variants.py`,
  `generate_tile_blends.py`, `variants_1/2/3_*.bat`.
- Characters/UI/scenes: `generate_character_set.py`, `generate_class_scene.py`,
  `generate_menu_scene.py`, `generate_ui_set.py`, `generate_ui_polish.py`,
  `animate_class_scene.py`, `animate_menu_scene.py`, `import_menu_frames.py`,
  `character_0/1/2_*.bat`.
- QA/utility: `audit_prop_footprints.py`, `clean_white_bg.py`,
  `export_training_dataset.py`.

All of these ran successfully and produced output under
`Tools/BiomeSketch/assets/{tile,prop}/review_candidates/pixellab_*`.

## 2. Promotion status

### Tiles (`review_candidates/pixellab_tilesets/` → `Assets/Resources/Tiles/`)

| Family | Tiles | Promoted? | Wired into a biome surfaceGroup? |
|---|---|---|---|
| plains | 16 | yes | yes (plains biome, in use) |
| beach | 16 | yes (#19) | **NO** — fixed this session, see §3 |
| snow | 16 | yes | yes (snow biome) |
| mountain_stone | 16 | partial (~4/16 used) | partial — 12 unused variants |
| dungeon_stone | 16 | yes | yes (dungeon) |
| farming | 16 | yes | yes (farm plots) |
| planks | 16 | yes | yes (building floors) |
| **interior** | 16 | **NO — zero promotion** | n/a |

### Props (`review_candidates/pixellab_props/` → triaged in
`Tools/BiomeSketch/biome_assignments_triage.json`)

Per the #21 triage: forest 27 assigned, meadow 39, mountain 19, snow 14,
beach 2, coast 2, water 1, dungeon 3, plus 37 `unassigned` and 43
`retireCandidates` (interior/town furniture, intentionally out of scope for
outdoor biomes).

Cross-referencing triage prop IDs against
`Assets/Scripts/IsoCoreFoundation/Core/FoundationContent.cs`: 51 are already
defined as blocks/nodes. Of the remaining ~47 "missing", most are
retire-candidates or duplicates of existing nodes under different names. The
genuinely actionable gaps (outdoor scatter props referenced by biome JSON
`decor`/feature lists but with no `ResourceNodeDefinition`) are:

- **Ore ladder**: `ore_copper`, `ore_gold`, `ore_iron`, `ore_manacrystal`,
  `ore_silver`, `ore_starmetal` — zero Node definitions exist (only
  `copper_vein` does). Mountain/snow biomes reference an ore progression that
  isn't backed by data.
- **Ambient light props**: `glowbug`, `wisp` — referenced in snow/forest/dungeon
  "ambient" decor notes, no Node.
- **Forest decor**: `forest_dead_tree`, `forest_stump` — forest.json feature
  list references these distinctly from the existing `stump`/`log`.
- **`rock_outcrop`** — referenced by beach.json `decor.drySandRocks` (and
  mountain/snow decor) but not defined; closest existing is `shared_gray_rock`
  / `plains_rock`.

## 3. What was wired this session (Claude lane, data only)

- `beach.json` `surfaceBasePool`/`surfaceAccents`/`bandOrder`/`transitionTo`
  rewritten to consume the promoted `beach_00..15` family (wetBand/dryApron/
  accent split, classified by pixel inspection). See from-claude.md
  2026-06-13 entry for the Codex-side `BlockGroupDefinition` request.
- This plan doc + a new from-claude.md spec (below) covering the ore ladder,
  ambient props, forest decor nodes, `rock_outcrop`, and the ring-of-biomes
  test world.

## 4. Remaining implementation plan

### 4a. Codex work (C# / IsoCoreFoundation) — specced in from-claude.md 2026-06-13 (this entry)

1. Beach `BlockGroupDefinition` + surfaceGroup wiring (carried over from prior
   entry, still outstanding).
2. Add `ResourceNodeDefinition`s for the ore ladder (copper/iron/silver/gold/
   manacrystal/starmetal), `glowbug`, `wisp`, `forest_dead_tree`,
   `forest_stump`, `rock_outcrop`, using the promoted PixelLab prop art
   (`Tools/BiomeSketch/assets/prop/review_candidates/pixellab_props/{forest,ores,ambient}`).
3. Add corresponding `BiomeNodeSpawn` entries to the relevant
   `BiomeDefinition` assets per the density/band notes already in each
   biome's `*.json` `decor`/feature blocks.
4. Ring-of-biomes test world extension to `FoundationCreationInstanceShowroom`
   — full spec in from-claude.md.

### 4b. Claude-lane follow-ups (data/art, not done this session — future task)

- Promote remaining 12 unused `mountain_stone` variants if mountain biome
  wants more surface variety (currently only ~4/16 in use).
- `interior` tile family (16 tiles) is fully unpromoted — only relevant if/when
  building interiors get a PixelLab pass; not blocking outdoor biome work, so
  deferred.
- Cross-check `unassigned` (37 props) list against new building/settlement
  work as that comes up — none of these block the ring world.

## 5. Ring-of-biomes test world (task #25)

See from-claude.md 2026-06-13 entry for the full Codex spec. Summary: extend
`FoundationCreationInstanceShowroom` with a `BuildBiomeRing()` step that lays
out 6 wedge sectors (plains center untouched, ring = forest, meadow,
mountain, snow, beach, dungeon-adjacent stub) around the existing showroom,
each painted with that biome's real `surfaceGroup` tiles via `AddRect`/
`AddCell`, plus a constantly-running weather particle instance per sector
(snow sector → `SnowController`/blizzard `WeatherDefinition.particlePrefab`,
forest/meadow → light rain, mountain → fog-tinted overlay, beach/plains →
none) instantiated directly (not via the global singleton `WeatherManager`,
since multiple sectors need simultaneous distinct weather).
