# Phase 3 — settlements/towns (D1-D7) — 2026-06-14

## What was built

- **`Assets/Scripts/IsoCoreFoundation/World/IsoSettlementSampler.cs`** (new):
  pure `(macro cell, seed)` settlement module loaded from `settlements.json`.
  - **D1** `SampleSite`: hashes 96x96-cell macro cells, jitters a plaza center,
    excludes the spawn ring (D7a), pre-rolls chunk-ring/eligibility before any
    expensive probing, then scores flatness + water proximity + biome weight +
    spawn-distance band against `scoreThreshold`. Spacing vs. neighbouring
    macro cells enforced via `minimumSettlementSpacingChunks` (D7).
  - **D2/D3** `TryLot`: coarse lot grid (`lotGridSpacingCells`), picks a
    building from `buildingPool` filtered by the band's `buildingRanks`,
    validates a flat/dry footprint via the cheap probe, computes a door cell
    facing the plaza axis, reserves a 2-cell walk lane.
  - **D4** `TryRoad`: greedy spine from the plaza toward the spawn ring/origin,
    width from `roadWidthCells`, capped at `SpawnRingRadiusCells`.
  - **D5** `TryRing`: building ring -> farm ring (`farmRingStartNorm..EndNorm`)
    -> pasture fringe; dock arm along the site axis when `site.NearWater`.
  - **D7b**: beach/blend-band/flatness probe via `IsoTerrainSampler.SettlementProbeCell`.

- **`IsoTerrainSampler.cs`**: added `_settlements` field + `ApplySettlement`,
  called at the end of `SampleContinent` (after biome/decor, before the
  campsite pass). Stamps plaza (`stone_path` + campfire center), buildings
  (`stone_block`, solid, height-raised), door/walk-lane (`stone_path`, decor
  suppressed), roads (`stone_path`/`forest_mud_path` shoulder), farms
  (`FarmSurfacePool` + sparse `scarecrow`), pasture (decor suppressed), and
  docks (`PlanksSurfacePool` + `dock_post`/`boat_rowing`). Also added
  `SettlementProbeCell` (cheap O(1) water/beach/height/blend-band/biome probe)
  and `BiomeAtClimate` (cheap road-shoulder biome lookup).

- **`FoundationContent.cs`**: registered `farm_00..15` and `planks_00..15` as
  `BlockDefinition`s + `farm_blocks`/`planks_blocks` groups (art was already
  promoted to `Resources/Tiles` in Phase 1 but never wired). Registered four
  new `ResourceNodeDefinition`s: `scarecrow`, `dock_post`, `boat_rowing`,
  `tavern_sign` (campsite-prop pattern: `hitsToHarvest=99`, no drops).

- **Promoted art**: copied `boat_rowing.png`, `dock_post.png`, `scarecrow.png`,
  `tavern_sign.png` (128x128) from
  `Tools/BiomeSketch/assets/prop/review_sync/ready_no_overwrite/{town,tavern}/`
  into `Assets/Resources/Decorations/`, with matching `.png.meta` (spriteMode 2,
  full-canvas rect, new GUIDs) following the existing `lantern_post` pattern.

## Tunable

- All D1-D7 thresholds live in `SettlementsConfig` (macro cell size, spawn-ring
  margin, score weights/threshold, plaza radius, lot grid spacing, road width,
  farm/pasture ring norms, spacing) — defaults hand-mirrored from
  `settlements.json`, which is the live source via `SettlementsConfig.LoadOrDefault()`.
- `FarmSurfacePool` / `PlanksSurfacePool` (weighted tile pools) and the
  scarecrow/dock_post/boat_rowing chance constants live in `IsoTerrainSampler.cs`
  near `ApplySettlement` — small settlement-local pools, not worth a JSON section.
- `buildingPool`, `bands[]`, `outdoorProps[]` all read from `settlements.json`.

## Known gaps / future pass

- **D6 interiors**: deliberately NOT implemented. Buildings are solid
  `stone_block` boxes from the outside; `interior`/`planks`-as-flooring and
  library/tavern/guild interior prop sets remain excluded from outdoor pools.
- `IsWalkLaneCell` in `IsoSettlementSampler` is currently a no-op stub (always
  false) — the 2-cell walk lane reservation is structurally present but not
  yet carving extra lane cells beyond the door cell itself.
- D4 roads are a simple greedy downhill-of-cost spine, not a full cost-map A*;
  multiple settlements' roads are not yet guaranteed to connect to each other.
- Dock arm + `boat_rowing`/`dock_post` are wired but only trigger when
  `site.NearWater` and within `norm <= 0.6` of the plaza — not yet validated
  against a real coastal macro cell in-engine.

## How to test

- Settlements are **excluded from the spawn clearing and the biome-showcase
  seed** (`BiomeShowcaseSeed = 240611` short-circuits before `ApplySettlement`
  runs). Use any other seed (e.g. the default project seed, or `seed=1`) and
  walk/teleport several macro cells (96 world cells) away from spawn in any
  direction — `minimumSettlementSpacingChunks=8` and the score threshold mean
  not every macro cell spawns a town, so explore a few cells out.
- Look for a `stone_path` plaza with a campfire at its center, `stone_block`
  buildings around it with door-facing stone paths, a road spine running back
  toward spawn, and (on land biomes) a `farm_00..15` ring with occasional
  scarecrows just outside the buildings.

## Compile status

Both `IsoTerrainSampler.cs` and `FoundationContent.cs` were structurally
repaired after mid-edit truncation (brace/depth-balance verified: both end
cleanly at depth 0). All new field/type references (`BiomeTilePoolEntry`,
`BiomeDefinition.PickWeighted`, `IsoCell.SolidBlock`, `SettlementCellKind`,
`SettlementsConfig.LoadOrDefault`) were checked against their declarations.
Manual review only — no Unity batch compile was run.
