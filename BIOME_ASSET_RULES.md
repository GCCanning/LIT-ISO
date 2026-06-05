# LIT-ISO Biome Asset Rules

This is the first formal pass for deciding which assets belong in each natural biome. The goal is to keep generation intentional: no random tile-family pollution, no decorations hiding cliff edges, and no resource nodes spawning where movement/collision is ambiguous.

Runtime generation currently uses `IsoBiomeDefinition`. The companion data model for future data-driven placement is `BiomeAssetRuleSet`.

## Global Placement Rules

- Natural overworld biomes are Plains, Desert, and Frozen/highland hills.
- Frozen Cave, Temple, and Basic stay disabled from natural surface generation.
- Do not place decorations or resource nodes on immediate height-edge cells.
- Do not place trees on transition cells unless the transition specifically allows them.
- Ground tiles may blend in transition bands, but decorations/resources should be stricter.
- Heights are rolling hills, not mountains:
  - Plains: height 0-2
  - Desert: height 0-2
  - Frozen/highland: height 0-3

## Plains

Purpose: starter-friendly grassland with light vegetation, scattered trees, and occasional rocks.

Terrain:
- Flat: `NeighbourTile_Plains_FlatGrass`, `RandomTile_Plains_FlatGrass`
- Raised: `NeighbourTile_Plains_RaisedGrass`, `RandomTile_Plains_RaisedGrass`
- Decorations: `RandomTile_Plains_Plants`

Prefab candidates:
- `Assets/Prefabs/Decorations/Plains/Tree_Round.prefab`
- `Assets/Prefabs/Decorations/Plains/TreeStump.prefab`
- `Assets/Prefabs/Decorations/Plains/Chest.prefab` as rare point of interest, not common decoration

Resources:
- Oak Tree: common on height 0-1, not on height edges
- Rock: uncommon on height 0-2

Density targets:
- Decoration density: `0.025-0.04`
- Resource density: `0.015-0.025`

## Desert

Purpose: open readable sand fields, sparse dry props, more rocks than plants.

Terrain:
- Flat: `NeighbourTile_Desert_FlatSand`, `NeighbourTile_Desert_FlatSandWave`, `RandomTile_Desert_FlatSand`
- Raised: `NeighbourTile_Desert_RaisedSand`, `NeighbourTile_Desert_RaisedSandWave`, `RandomTile_Desert_RaisedSand`
- Decorations: `RandomTile_Desert_Coral`, `RandomTile_Desert_Planks`

Prefab candidates:
- None currently natural-surface specific.
- Temple props should not leak into desert until ruins/POIs are added.

Resources:
- Rock: common on height 0-2
- Oak Tree: disabled

Density targets:
- Decoration density: `0.006-0.014`
- Resource density: `0.018-0.03`

## Frozen/Highland Hills

Purpose: cold rolling highlands with snow, exposed ground, and mineral/rock emphasis. Height 2-3 should feel like hills, not sheer mountains.

Terrain:
- Flat: `NeighbourTile_FrozenMountain_FlatSnow`, `RandomTile_FrozenMountain_FlatSnow`, `RandomTile_FrozenMountain_FlatGround`
- Raised: `NeighbourTile_FrozenMountain_RaisedSnow`, `RandomTile_FrozenMountain_RaisedSnow`
- Decorations: Frozen Mountain decoration tiles only

Prefab candidates:
- None currently specific. Use tile decorations first.

Resources:
- Rock: common on height 1-3
- Oak Tree: disabled for v1

Density targets:
- Decoration density: `0.012-0.022`
- Resource density: `0.02-0.035`

## Transition Rules

Plains to Desert:
- Ground: mostly primary biome with sparse secondary patches.
- Decorations: dry grass/plants near Plains side, desert coral/planks near Desert side.
- Resources: rocks allowed, trees only on Plains-dominant cells.

Plains to Frozen/highland:
- Ground: grass/snow breakup.
- Decorations: reduce plants; allow rocks.
- Resources: rocks allowed, trees only on Plains-dominant cells.

Desert to Frozen/highland:
- Ground: sandy snow/exposed ground transition.
- Decorations: very sparse.
- Resources: rocks allowed.

## Disabled Natural Surface Sets

Frozen Cave:
- Keep for underground/cave generation later.
- Do not use in overworld biome sampling.

Temple:
- Keep for authored/rare POIs, ruins, interiors, and lava zones.
- Do not blend temple tiles into normal desert/plains transitions.

Basic:
- Debug/fallback only.

## Implementation Notes

- `IsoBiomeDefinition` remains the active terrain tile source today.
- `BiomeAssetRuleSet` is the next data-driven layer for prefab/resource placement.
- The generator should consume these rules in this order:
  1. biome and transition sample
  2. final height
  3. reject height-edge cells for objects
  4. choose biome-local decoration/resource rule
  5. apply transition multipliers
  6. place deterministic object with spacing checks
