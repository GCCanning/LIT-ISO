# LIT-ISO Biome Asset Inventory

Generated from the project folder on 2026-05-20.

## Project Totals

| Type | Count |
| --- | ---: |
| PNG images | 560 |
| Unity assets | 553 |
| Prefabs | 29 |
| Animation clips | 16 |
| Audio clips | 2 |
| Materials | 2 |
| Animator controllers | 1 |

## Current Biome Assets

Biome definitions live in `Assets/World/Biomes/`.

| Biome | Asset | Decoration Chance | Raised Threshold | Height Noise Scale | Notes |
| --- | --- | ---: | ---: | ---: | --- |
| Plains | `BiomeDefinition_Plains.asset` | 0.018 | 0.68 | 0.08 | Grass terrain with plants. |
| Desert | `BiomeDefinition_Desert.asset` | 0.008 | 0.72 | 0.065 | Sand terrain with sparse coral/rocks/planks. |
| Frozen Mountain | `BiomeDefinition_FrozenMountain.asset` | 0 | 0.58 | 0.095 | Snow terrain, currently bare. |
| Frozen Cave | `BiomeDefinition_FrozenCave.asset` | 0.012 | 0.62 | 0.075 | Raised cave floor/wall set with cave decorations. |
| Temple | `BiomeDefinition_Temple.asset` | 0 | 0.7 | 0.07 | Stone/lava set, currently bare. |
| Basic | `BiomeDefinition_Basic.asset` | 0 | 0.66 | 0.08 | Test/fallback tile set. |

## Isometric Tile Families

| Family | Tile Assets | Sprite Coverage | Primary Use |
| --- | ---: | --- | --- |
| Basic | 36 | 17 flat, 19 raised | Test geometry, fallback, debug biome. |
| Plains | 72 | 40 flat, 24 raised, 9 decoration | Grasslands, light forest, starter areas. |
| Desert | 66 | 21 flat, 23 raised, 22 decoration | Sand, dry ruins, plank paths, sparse rocks/coral. |
| Frozen Mountain | 53 | 26 flat, 22 raised, 21 decoration | Snowfields, cliffs, rocky mountain edges. |
| Frozen Cave | 43 | 23 raised, 11 wall, 9 decoration | Underground icy/cave regions. |
| Temple | 61 | 15 flat, 24 raised, 15 decoration, 20 animated lava frames | Ruins, lava, temple interiors/exteriors. |

## Rule Tiles

Neighbour rule tiles live in `Assets/Tilemaps/Isometric/RuleTiles/NeighbourTiles/`.

- `NeighbourTile_Basic_FlatFloor`
- `NeighbourTile_Basic_RaisedFloor`
- `NeighbourTile_Desert_FlatSand`
- `NeighbourTile_Desert_FlatSandWave`
- `NeighbourTile_Desert_RaisedSand`
- `NeighbourTile_Desert_RaisedSandWave`
- `NeighbourTile_FrozenCave_RaisedFloor`
- `NeighbourTile_FrozenCave_RaisedWall`
- `NeighbourTile_FrozenMountain_FlatSnow`
- `NeighbourTile_FrozenMountain_RaisedSnow`
- `NeighbourTile_Plains_FlatGrass`
- `NeighbourTile_Plains_RaisedGrass`
- `NeighbourTile_Temple_AnimatedLava`
- `NeighbourTile_Temple_LavaWall`

Random tiles live in `Assets/Tilemaps/Isometric/RuleTiles/RandomTiles/`.

- `RandomTile_Desert_Coral`
- `RandomTile_Desert_FlatSand`
- `RandomTile_Desert_Planks`
- `RandomTile_Desert_RaisedSand`
- `RandomTile_FrozenCave_Coral`
- `RandomTile_FrozenCave_RaisedFloor`
- `RandomTile_FrozenMountain_FlatGround`
- `RandomTile_FrozenMountain_FlatSnow`
- `RandomTile_FrozenMountain_RaisedSnow`
- `RandomTile_Plains_FlatGrass`
- `RandomTile_Plains_Plants`
- `RandomTile_Plains_RaisedGrass`
- `RandomTile_Temple_FlatStone`
- `RandomTile_Temple_RaisedBlueStone`
- `RandomTile_Temple_RaisedStone`

## Decoration Prefabs

Prefab decorations live in `Assets/Prefabs/Decorations/`.

### Plains

- `Chest.prefab`
- `TreeStump.prefab`
- `Tree_Round.prefab`

### Temple

- `Door_Wall.prefab`
- `Jar_Large.prefab`
- `Jar_Small.prefab`
- `Pillar_Short.prefab`
- `Pillar_Short_Striped.prefab`
- `Pillar_Tall_Striped.prefab`
- `Pillar_Wall_Short_Left.prefab`
- `Pillar_Wall_Short_Right.prefab`
- `Pillar_Wall_Short_Striped_Left.prefab`
- `Pillar_Wall_Short_Striped_Right.prefab`
- `Pillar_Wall_Tall_Striped_Left.prefab`
- `Pillar_Wall_Tall_Striped_Right.prefab`
- `Sarcophagus_1.prefab`
- `Sarcophagus_2.prefab`
- `Statue_Cat.prefab`

## Gameplay Resource Assets

Items live in `Assets/World/Items/`.

| Item | Id | Category |
| --- | --- | --- |
| Coin | `coin` | Currency |
| Copper Ore | `copper_ore` | Resource |
| Pinecone | `pinecone` | Resource |
| Stone | `stone` | Resource |
| Treesap | `treesap` | Resource |
| Wood | `wood` | Resource |

Resource node definitions live in `Assets/World/ResourceNodes/`.

| Node | Spawn Chance | Cooldown | Drops |
| --- | ---: | ---: | --- |
| Oak Tree | 0.06 | 45s | Wood 1-3 at 100%, Pinecone 0-2 at 40%, Treesap 0-1 at 20% |
| Rock | 0.04 | 60s | Stone 1-3 at 100%, Copper Ore 0-1 at 30% |

Note: these definitions currently have no assigned node sprites in the asset files.

## Character And Audio Assets

Player:

- Current player sheet: `Assets/Resources/Characters/Player/HollowedLight_512x1024.png`
- Older witch assets still exist under `Assets/Characters/Witch/`:
  - 16 animation clips
  - 8-direction run sprite folders
  - static sprite set
  - `WitchController.controller`

Audio:

- `Assets/Audio/Music/Music_Day_AmbientExploration.flac`
- `Assets/Audio/Music/Music_Night_HarpTheme.flac`

## Generation Rule Candidates

| Biome | Ground Rules | Raised Rules | Decoration Tiles | Prefab/Resource Rules |
| --- | --- | --- | --- | --- |
| Plains | Use flat grass neighbour/random tiles. | Use raised grass neighbour/random tiles. | Plants, grass, flowers. Increase density toward 0.03-0.05. | Trees and rocks on flat cells; chest as rare point of interest. |
| Desert | Mix flat sand and sand wave tiles. | Use raised sand and raised sand wave. | Coral/rocks/bones/planks. Keep sparse, 0.006-0.015. | Rocks common; planks can form paths/patches; chest rare. |
| Frozen Mountain | Mix flat snow and flat ground. | Raised snow with rocky decoration. | Add mountain rocks/snowcaps; currently unused. | Rocks/mineral nodes, sparse tree/stump only if desired. |
| Frozen Cave | Use raised floor as walkable base and walls as blocked/height terrain. | Wall/raised floor split should be explicit. | Crystals/coral/spikes. | Copper/stone nodes, crystal resource nodes later. |
| Temple | Flat stone plus lava channels. | Raised stone/blue stone/lava wall. | Use statues, jars, pillars, sarcophagi. | Temple prefabs need deterministic placement and collision categories. |
| Basic | Keep as debug. | Keep as debug. | None. | None. |

## Recommended Next Data Model

To make biome generation controllable without hardcoding every asset:

- Add weighted tile sets to `IsoBiomeDefinition` for flat, raised, decoration, hazard, and path tiles.
- Add biome-specific prefab spawn tables with spawn chance, min spacing, allowed heights, and collision mode.
- Add resource-node spawn tables per biome instead of global node spawning.
- Add local patch/cluster generation for paths, lava, snow/ground breakup, and forest groves.
- Add biome transition rules so neighboring biomes blend at chunk edges instead of hard switching.
