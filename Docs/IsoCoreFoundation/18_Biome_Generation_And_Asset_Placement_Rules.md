# LIT-ISO Biome Generation And Asset Placement Rules

Status: review/integration plan  
Owner lane: Codex for Foundation/runtime contract, Claude for art promotion

This document defines how generated tiles and props should flow into the
canonical `IsoCore.Foundation` world. It is not an art approval. Use it with:

- `Temp/WorldGen/art_review_pack/art_review_summary.md`
- `Temp/WorldGen/art_review_pack/art_review_manifest.json`
- `Assets/StreamingAssets/worldgen/runtime_contract.json`

## Research Anchors

- Unity isometric tilemaps: keep `IsometricZAsY`, `TilemapRenderer.mode =
  Individual`, and a project transparency sort axis; these are already
  canonical in `AGENTS.md`.
- Red Blob style biome generation: derive biome from elevation/moisture/climate
  fields, then select surface/feature rules from the resulting semantic biome.
- Poisson/blue-noise placement: clustered props should still enforce minimum
  spacing and footprint occupancy so forests read natural without collapsing
  into noise.

Reference links for later review:

- https://docs.unity3d.com/Manual/Tilemap-Isometric.html
- https://www.redblobgames.com/maps/terrain-from-noise/
- https://www.cs.ubc.ca/~rbridson/docs/bridson-siggraph07-poissondisk.pdf

## Current Data Model

The current worldgen datapack is already close to the desired shape:

- `surface_rules.json`: global ordered rules for water, shoreline, elevation,
  river-adjacent mud, ocean-adjacent sand, and biome fallback.
- `biomes/*.json`: biome temperature/moisture/elevation ranges, base surfaces,
  accents, transition widths, and feature lists.
- `features/*.json`: single, cluster, and negative-space feature placement.
- `transitions.json`: material-pair transition lookup.
- `dungeon_themes.json`: dungeon floor pools and accent rules.
- `runtime_contract.json`: generated bridge between rules, art availability,
  Foundation content definitions, BiomeSketch tile variants, settlement rules,
  prop interaction rules, and biome-suite policy.

The important boundary is:

- **World authority**: footprint cell, integer height, biome/material tags.
- **Presentation**: tile sprite, prop sprite, shadow, transparency, sorting.
- **Do not** let sprite appearance decide traversal, biome, or height.

## Asset Intake Gates

Generated art moves through four gates:

1. **Exists on disk**  
   PixelLab/Sprixen/BiomeSketch output is present, but not yet trusted.

2. **Review pack candidate**  
   `build_art_review_pack.py` inspects alpha, canvas size, bbox, likely
   background leaks, proposed PPU, footprint, and contact sheets.

3. **Human-selected final**  
   One accepted candidate per stable asset id. This matters because PixelLab
   prop harvests usually contain four `frame_N.png` alternatives per id.

4. **Runtime promotion**  
   Claude/art lane copies selected files into runtime art folders and commits
   `.meta`/import settings. Codex then updates/validates the Foundation
   contract and sampler consumption.

Do not promote all candidates. Repeated ids such as `tavern_r1/frame_0..3` are
alternatives, not four separate buildings.

### BiomeSketch review sync

BiomeSketch is allowed to contain the full reviewed tile/prop pool so layout
vignettes can be tested before runtime promotion. Synced review assets live
under:

- `Tools/BiomeSketch/assets/tile/review_sync/`
- `Tools/BiomeSketch/assets/prop/review_sync/`
- `Tools/BiomeSketch/assets/tile/review_candidates/`
- `Tools/BiomeSketch/assets/prop/review_candidates/`

These rows are marked by palette group:

- `synced ready tiles` / `synced ready props`
- `synced conflict tiles` / `synced conflict props`
- `synced held tiles` / `synced held props`

This is still not a runtime import. Runtime worldgen must treat these as
review-space art until the art lane promotes a specific file into the approved
runtime folders and validators prove it resolves.

The `review_sync` layer stores the planner-selected candidate per stable id.
The `review_candidates` layer stores every candidate frame from the full review
pack, which is useful for comparing raw alternatives such as
`tavern_r1/frame_0..3`, `tavern_r2/frame_0..3`, and `tavern_r3/frame_0..3`.

## Tile Rules

### Terrain tiles

Base terrain remains 32x32 isometric diamond tiles unless explicitly marked as
large authored floor art.

Use:

- `plains2_*`: meadow/plains base and accents.
- `snow2_*`: frozen biome base and accents.
- `mountain_stone_*`: mountain/high-elevation stone bands.
- `dungeon2_*`: dungeon floors after import is confirmed.
- `farm_*`: craftable/farming surfaces, not natural overworld fallback.
- `beach/*`: coast/ocean-adjacent bands.
- `planks/*`: interiors/buildings/craftable floors.

Large `dungeon_floor_*` canvases are manual-review items. They can remain
authored special floors, but they should not be mixed into ordinary 32x32
terrain pools without an explicit renderer path.

### Transition tiles

Transitions should be generated deterministically from accepted base tiles when
possible:

- grass -> dirt
- grass -> forest_floor
- grass -> stone
- grass -> sand
- sand -> water
- plains -> snow
- plains -> mountain stone

Directional/natural blend experiments are not active BiomeSketch palette
assets. The current catalog intentionally excludes blend, gradient, and
transition tile artifacts. Neighbor masks should still select transitions
later, but until an accepted transition set exists the runtime should fall back
to base A, not a placeholder.

Current no-credit expansion modes:

- **New tile variants:** `Tools/PixelLab/generate_texture_variants.py --mode
  remix` creates shape-locked variants from one approved source tile. These are
  recorded under `runtime_contract.json.variantGroups`.
- **Gradient blend tiles:** `Tools/PixelLab/generate_tile_blends.py` exists as
  an experiment, but its outputs are currently excluded from BiomeSketch and
  from `runtime_contract.json.blendPairs` because the weird transition/blend
  tiles are not approved for layout work.

These generated pools remain `biomesketch-only` until the art lane promotes
specific rows into runtime `Assets/Resources/Tiles`.

### Tile placement order

For each footprint cell:

1. Compute canonical height.
2. Compute climate/biome from noise + authored overrides.
3. Apply global surface rules in order:
   water, shoreline, river bank, coast band, elevation cap, biome fallback.
4. Select a base tile from biome pool.
5. Apply low-frequency accent variation.
6. Apply transition tile from neighbor materials.
7. Render sprite for presentation only.

## Prop And Feature Placement Rules

All props must resolve through semantic feature rules, never random direct
spray over the tilemap.

### General constraints

- Respect biome kind, material tags, height bands, and transition cells.
- Reserve every occupied footprint before placing the next prop.
- Keep tall props off cliff edges unless a feature explicitly allows it.
- Suppress dense features near paths, water edges, spawn safety zones, and
  building doors.
- Cluster centers should use blue-noise/min-distance placement so forests form
  readable groves instead of uniform scatter.

### Footprints

Current proposed footprints from the review pack:

- Trees: `1x1` or `2x1`; block movement; tall.
- Bushes/flowers/grass: usually `1x1`; often non-blocking.
- Rocks/ores/chests/stumps/logs: `1x1`; usually blocking except tiny decor.
- Stations/workbenches: mostly `2x1`; blocking.
- Buildings:
  - r1: `2x2`
  - r2: `3x3`
  - r3: `4x4`
  Door-cell alignment must be manually verified before any rank evolution
  system relies on them.
- Tents: `2x2`; blocking.
- Ambient effects (`glowbug`, `wisp`): visual-only unless explicitly given an
  interactable.

### Feature categories

- Forest:
  - dense tree groves with negative-space clearings
  - bushes/logs/stumps/mushrooms as subfeatures
  - avoid blocking all routes through a chunk
- Meadow/plains:
  - sparse trees, bush patches, flower/grass accents
  - keep large clear readable traversal lanes
- Mountain:
  - stone strata by height band
  - ore distribution increases with height/stone exposure
  - sparse vegetation
- Snow:
  - reduced decor density
  - ice/snow lake aprons
  - no warm-biome plants unless transition band
- Coast:
  - sand/grass/water edge transitions
  - shore rocks and driftwood near water only
- Dungeon/interior:
  - walkable floor cells only
  - no wall-sprite authority in void instances
  - props must reserve footprints and not block required exits

## Runtime Integration Rules

Codex side should consume promoted assets only after validators can prove:

- Every referenced tile id resolves to a live runtime sprite or an approved
  fallback.
- Every referenced feature id resolves to a live sprite/alias and gameplay
  prototype.
- Runtime contract variant groups are parseable and remain marked review-only
  until promoted. Blend pairs are currently expected to be empty.
- Material tags such as `grass`, `sand`, `stone`, and `water` are not emitted
  as concrete tile ids.
- Multi-cell footprints are represented in placement occupancy before large
  props/buildings enter random placement.
- Light-source tags have a future hook for glow/particle components but do not
  block terrain import.

## Current Review Findings

Latest review pack:

- 736 image candidates scanned.
- 433 selected by the dry-run art promotion planner.
- 320 marked ready-for-art-lane import by the Unity dry-run validator.
- 306 are ready/no-overwrite in the import-ready subset.
- 14 are otherwise ready but conflict with an existing runtime destination.
- 113 are held for review-only/building/manual/occupancy reasons.
- 5 large dungeon floor canvases need manual review.
- No current hard rejects after excluding `.orig.png` backup files.

Current worldgen contract:

- 70 tile ids referenced by rules.
- 94 prop ids referenced by rules.
- 44 concrete feature entries.
- 48 BiomeSketch variant groups.
- 0 BiomeSketch blend-pair groups; blend/gradient/transition palette entries
  are deliberately excluded.
- Support files included in the audit/contract:
  - `biome_suite.json`
  - `settlements.json`
  - `prop_interactions.json`
  - `dungeon_themes.json`
- No missing tile/prop ids in the current audit.
- `forest_log` was normalized to the existing `log` id in
  `features/stump_log_litter.json`.

Representative contact sheets / outputs:

- `C:\tmp\LitIsoWorldGen\art_import_ready_subset\contact_sheets\ready_no_overwrite.png`
- `C:\tmp\LitIsoWorldGen\art_import_ready_subset\contact_sheets\ready_tiles_no_overwrite.png`
- `C:\tmp\LitIsoWorldGen\art_import_ready_subset\contact_sheets\ready_props_no_overwrite.png`
- `Tools/BiomeSketch/review_sync/contact_sheets/synced_ready_tiles.png`
- `Tools/BiomeSketch/review_sync/contact_sheets/synced_ready_props.png`
- `Tools/BiomeSketch/variant_review/remix_variants_tiles_contact_sheet.png`
- `Tools/BiomeSketch/variant_review/gradient_blends_tiles_contact_sheet.png`
  remains as historical experiment output only; it is not registered in the
  active BiomeSketch palette.

Current BiomeSketch review sync:

- 1169 manifest rows considered.
- 116 blend/gradient/transition tile rows excluded.
- 254 previously registered blend/gradient/transition tile entries purged from
  the active palette registry.
- 1053 synced review/candidate entries remain visible in BiomeSketch.
- 531 synced tile entries.
- 522 synced prop entries.
- `tavern_r1`, `tavern_r2`, and `tavern_r3` appear both as selected held
  building rows and as raw `frame_0..3` candidate alternatives.

Current BiomeSketch rule presets:

- `RULE_meadow_to_forest_transition`: open meadow into forest edge, with
  transition tiles, sparse meadow props, denser forest props, and a clear
  traversal lane.
- `RULE_forest_grove_clearing`: clustered trees around a readable clearing,
  showing negative-space placement and stump/bush subfeatures.
- `RULE_mountain_height_bands`: integer height bands, stone/dirt/grass
  elevation transition, and sparse rock placement.
- `RULE_snow_to_mountain_transition`: snow field into raised stone using
  accepted snow and mountain tiles only; no generated blend tiles.
- `RULE_coast_sand_water_edge`: water, sand, and grass shoreline layering with
  shore rocks and sparse vegetation.
- `RULE_tavern_rank_footprint_review`: r1/r2/r3 tavern footprint comparison
  plus raw tavern rank candidate frames for review.
- `RULE_prop_density_with_clear_lanes`: prop density stress test with a cross
  shaped clear movement lane.

## Next Safe Steps

1. Human/Claude selects one final per stable prop id from the review sheets.
2. Art lane promotes selected files and import metadata to runtime folders.
3. Claude/art lane approves which BiomeSketch-only props graduate into runtime
   art.
4. Codex updates `runtime_contract.json` and validators after promotion.
5. Codex adds the sampler rule layer that consumes contract tile groups only
   after sprite-existence validators are green.
6. Codex adds multi-cell feature occupancy before enabling buildings, tents,
   and large tree clusters in runtime placement.
