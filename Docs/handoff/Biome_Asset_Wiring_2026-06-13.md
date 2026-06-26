# Biome Asset Wiring — 2026-06-13

Branch: `feat/biome-asset-wiring` (off `main`, uncommitted — see note at bottom).

## Summary

Wired previously-promoted-but-unused PixelLab tiles/props into the live biome
data (`FoundationContent.cs`) and terrain sampler (`IsoTerrainSampler.cs`), and
added a real **mountain** biome plus a **snow** biome surface group.

## New surface blocks / groups (`FoundationContent.cs`)

- `stone_snowcap`, `stone_scree`, `stone_cracked`, `stone_mossy` — promoted
  mountain stone variants, added to the mountain surface group
  (`worldgen_contract_stone_blocks`, aliased `mountainGroup`).
- `forest_mud_path` — added to a new `runtime_forest_surface_blocks` group
  alongside the existing forest floor/grass/litter/moss/underbrush variants.
- `runtime_snow_surface_blocks` — new group built from `snow2_00`..`snow2_15`
  promoted snow tiles.

## New resource nodes

- Ore ladder: `ore_iron`, `ore_silver`, `ore_gold`, `ore_manacrystal`,
  `ore_starmetal` (pickaxe, decreasing chance — mirrors `copper_vein` pattern),
  wired into the new mountain biome's rock outcrops.
- Forest deadwood: `forest_dead_tree` (axe, joins the tree-variant pool as a
  rare grove option — tagged so `IsTreeNode`/cliff-lip logic treats it as a
  tree), `forest_stump` (axe, ~1% in groves).
- Campsite props: `campsite_fire`, `campsite_lantern`, `campsite_table`,
  `campsite_chest` — placed via the new `TryPlaceCampsite` (see below).

## Biomes

- `meadow`, `forest`, `beach` — existing biomes extended with the new
  art-variant pools (forest oak/deep-oak/pine/bush/moss-bush/mushrooms/dead
  tree/stump; meadow/beach unchanged in climate/height, only node pools grew).
- `snow` (0.12 temp / 0.45 moisture) — new biome using
  `runtime_snow_surface_blocks`, height 1-2, nodes: pine, oak, shared gray
  rock (sparse — taiga look, not dense forest).
- `mountain` (0.25 temp / 0.22 moisture, height 3 base / variance 2) — new
  biome using the stone surface group with the snowcap/scree/cracked/mossy
  variants, ore ladder on rock outcrops.

## `IsoTerrainSampler.cs` changes

- `FindArtNodeVariant(wx, wy, salt, biome, ...ids)` — new helper: picks a
  deterministic random *variant* among several candidate node ids that have
  art, instead of always using one fixed id. Used for trees, bushes, and
  rocks so plains/forest variant families (`plains_tree_v2_*`,
  `plains_bush_v2_*`, `plains_rock_v2_*`, `forest_*`) actually get used
  instead of sitting unreferenced.
- `IsTreeNode()` — generalized the old `id == "tree" || id == "pine"` check so
  the cliff-lip bush-swap logic also covers `forest_oak_tree`,
  `forest_deep_oak_tree`, `forest_dead_tree`, `plains_tree_v2_*`, willow, etc.
- Mushroom patches (`forest_mushrooms`) added to the flower-patch decoration
  step (~1.8% inside patches).
- `TryPlaceCampsite()` — new: places a small 3x3 campsite cluster
  (fire/lantern/table/chest/log/stump) on a sparse grid (spacing 28 cells,
  ~11.5% chance per grid cell, kept away from the spawn apron) in meadow/forest
  biomes at height <= 2. Called from the flat-world, continent, and showcase
  land paths whenever a cell didn't already get a decoration.
- `BiomeShowcaseSeed` (240611) + `SampleBiomeShowcase()` /
  `SampleShowcaseLand()` — a fixed-seed quadrant world (west = ocean/beach,
  north = snow, east = forest, south = mountain stone/ore bands, center =
  meadow) for visually reviewing all newly-wired biomes together without
  waiting on the climate noise to roll the right combination naturally.
  Set `FoundationConfig.seed = 240611` (and `continentWorld = true`,
  `flatWorld = false`) to load it.

## Verified

- Both edited files re-read end-to-end; brace/paren structure and C# syntax
  check out (no `dotnet`/Unity available in-session to compile — manual
  review only).
- All new node lookups go through `FindArtNode`/`FindArtNodeVariant`, which
  already gate on `DecorationSpriteResolver.Resolve(node) != null`, so any id
  without promoted art silently falls back rather than rendering a
  placeholder box.

## Not done / open

- New `.asset` ScriptableObject ids referenced above are created in code via
  `FoundationContent`'s runtime registration helpers (`Block`, `Node`,
  `Biome`, `GroupFromExisting`), not as separate `.asset` files — consistent
  with how the rest of `FoundationContent.cs` registers content, so no new
  binary/asset files were added.
- Did not touch climate centers/heights of pre-existing biomes (meadow,
  forest, beach) beyond adding to their node pools, per the "additive, low
  risk" brief.
- **Branch not committed.** The working tree already has a large pre-existing
  uncommitted drift (the ~3,200-file tree noted in `CLAUDE.md`/handover docs
  as Priority 1, blocked on this sandbox lacking `git-lfs`). These biome
  wiring edits are layered on top of that same dirty tree on
  `feat/biome-asset-wiring` and were intentionally left uncommitted rather
  than risk a bad partial commit mixing unrelated drift — commit/triage on
  the owner's machine where git-lfs is available.
- Recommended next check: load the project with `seed = 240611`,
  `continentWorld = true`, `flatWorld = false` and walk through all four
  biome quadrants to confirm the new snow/mountain/forest variants render
  with real PixelLab art (not placeholder boxes) and ore veins appear on
  mountain rock outcrops.

## 2026-06-13 follow-up: STR-jump + static verification

### A7 — STR-driven jump mechanics

Added two new properties to `FoundationPlayerStats.cs` (Progression), following
the existing baseline-DEX-of-8 pattern but for STR (also baseline 8):

- `JumpClimbBonus` (int): `(STR - 8) / 4`, clamped to `[0, 3]`. Every 4 STR
  above baseline grants +1 extra climbable height-step while airborne.
- `JumpHeightMultiplier` (float): `1 + (STR - 8) * 0.03`, clamped to
  `[0.85x, 1.45x]`. Purely cosmetic hop-arc scalar (+/-3% per STR point).

Wired into `IsoFoundationPlayer.cs`:
- `Walkable()`'s jump-climb check now allows
  `targetH - _jumpStartHeight <= cfg.jumpClimbSteps + _stats.JumpClimbBonus`.
- `Refresh()`'s visual hop lift is now
  `cfg.jumpHeightUnits * _stats.JumpHeightMultiplier * 4 * t * (1-t)`.

Both evaluate to neutral (0 bonus / 1.0x) at the baseline STR of 8, so default
characters and existing traversal balance are unchanged. DEX-driven move-speed
and cooldown mechanics were not touched.

### Static verification of prior biome-asset-wiring session

Re-read `IsoTerrainSampler.cs` (823 lines) and `FoundationContent.cs`
(1441 lines) end-to-end via the Read tool (not bash, per instructions — the
bash mount view is consistent with Read here, both files parse as complete,
well-formed C# with matching top-level class/namespace closing braces).

All node ids referenced by the sampler are registered with matching ids in
`FoundationContent.cs`: `forest_oak_tree`, `forest_deep_oak_tree`,
`forest_dead_tree`, `forest_pine`, `forest_bush`, `forest_moss_bush`,
`forest_mushrooms`, `forest_stump`, `campsite_fire/lantern/table/chest`,
`ore_iron/silver/gold/manacrystal/starmetal`,
`stone_snowcap/scree/cracked/mossy`, `runtime_forest_surface_blocks`,
`runtime_snow_surface_blocks`, `snow2_00`..`snow2_15`, `forest_mud_path`.

Art check (via `DecorationSpriteResolver.Resolve` -> `Resources/Decorations/<SpriteId>.png`
for nodes, where `SpriteId = visualId ?? id`; tile groups -> `Resources/Tiles/<id>.png`):
all of the above have matching art files present
(`forest_oak_tree.png`, `forest_deep_oak_tree.png`, `forest_dead_tree.png`,
`forest_pine.png`, `forest_bush.png`, `forest_moss_bush.png`,
`forest_mushrooms.png`, `forest_stump.png`, `campfire_new.png`
(campsite_fire's visualId), `lantern_post.png` (campsite_lantern),
`tavern_table.png` (campsite_table), `dungeon_chest_wood.png` (campsite_chest),
`ore_iron.png`, `ore_silver.png`, `ore_gold.png`, `ore_manacrystal.png`,
`ore_starmetal.png`, `stone_snowcap.png`, `stone_scree.png`,
`stone_cracked.png`, `stone_mossy.png`, `forest_mud_path.png`, and the full
`snow2_00.png`..`snow2_15.png` set).

**No missing-art ids found.** Everything wired in the prior session has a
corresponding art file and should render in-game (subject to live in-editor
confirmation, which this sandbox cannot run).
