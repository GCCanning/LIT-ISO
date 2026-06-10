# Biome Tile & Prop Rules

The placement contract for world generation, per biome. Derived from the
115-tile pack's internal design (see `Docs/handoff/world-gen-prototype/
tile-taxonomy.md`) and Minecraft-style coherence rules: every tile and prop
belongs to a biome; nothing spawns where it makes no physical sense.

Sample grids: `Tools/WorldGenPreview/render_biome_samples.py` renders one
board per biome from the exact grids below (`--out DIR` -> review PNG). The
script's BOARDS table is the machine-readable form of this document.

## Global rules (all biomes)

1. **Tiles are terrain, props are dressing.** Field/hedge blocks (027-040,
   017/018, 092-114) fill a cell; props (041-085 transparency art) sit ON a
   cell. A prop never replaces terrain; terrain never spawns as a prop.
2. **One prop per cell, anchored placement.** Props cluster around anchors
   (water, cliff bases, grove edges) - never uniform scatter.
3. **Vegetation grows on grass only** (037/040/canopy floors). Never on
   stone, sand, cracked badlands floor, or crag tiers (height >= 3).
4. **Trees never stand on a cliff lip** (any neighbour a step down) - they
   swap to a shrub so canopies don't overhang stone faces.
5. **Water props stay in water.** Foam-footed stones (066, 069-081) ONLY in
   water cells adjacent to land/rock, at lowland elevation. Sparkles
   (082-085) only in open deep water. Swells (086-099) only on the deep side
   of a depth boundary.
6. **Transition tiles are exclusive.** Sprout dirt 019/020: meadow<->badlands
   border only. Root-cliff 025/026: forest edge facing lower ground only.
   Beach dirt: land<->water only - climate never places beach inland.
7. **Calm fields.** One dominant field tile >= 80% per region; busy variants
   are accents. Decoration density never exceeds ~15% of cells in a region.

## Per-biome contract

| Biome | Field tiles | Accent tiles | Props (allowed ONLY these) | Forbidden |
|---|---|---|---|---|
| **Meadow** | 037 | lush 040 patches | trees (oak art), flowers 041/042/046/047 + tulips 044 (patches near shrubs/water), shrubs 043/045, logs 048-052 (near forest edge), grey boulders 065/067 (cliff bases only) | water props, strata, canopy blocks, brown rocks, ORE |
| **Forest** | 040 floor | canopy mass 029 (dominant) + 027/028 (accents); root-cliff 025/026 at low edges | trees (oak art), mossy logs 050-052, stumps 052, shrubs 043/045, tufts 045 - in clearings only; copper veins ride rock outcrop clusters (~1 in 6 rock rolls); canopy cells carry NO props | field flowers in canopy, foam stones, strata |
| **Taiga** (cold) | 037 | - | pines + oaks (non-pack art, pine-dominant), tufts 045, logs, rocks 065, copper in outcrops | flowers (too cold), water props |
| **Badlands/Desert** | 017/018 | dark dirt 003, rubble 011-013 | brown rocks 053-060 (clusters), copper veins | ALL vegetation, grey wet stones, foam stones |
| **Beach** | 000/010/021 | - | sparse brown rocks 053-056; foam stones at the waterline cell only | vegetation (trees/bushes/flowers), strata |
| **River** | 104 stream | wash 106-108 against banks, rapids 114 (rare) | foam stones 069-081 in lowland streams only | swells, sparkles (ocean-only) |
| **Ocean** | 092 navy (seamless) | speckled 093/094/102/103 <= 15%, slab 100/101 | foam stones on the land edge; sparkles 082-085 deep & open only; swells 086-099 near the shallow rim | light 104-114 family (grid seams; rivers only) |
| **Crag (h>=3)** | cobble 061 / slab 062 caps | strata 014-016 (badlands mesas) | grey boulders 064/065/067/068, pinnacles 064 at the back of clusters | ALL vegetation |

## Non-pack props (current placeholders, replaced by the LoRA later)

Oak tree, pine, and the copper vein are not pack art. They obey the same
rules: trees on grass floors only (meadow/forest clearings/taiga), pine
dominant in taiga and minority elsewhere; copper veins spawn ONLY inside
rock-outcrop clusters (forest, taiga, badlands - never meadow/beach). The
copper vein temporarily wears pack brown-rock art (tile_058) so it stops
rendering as a placeholder box; swap when LoRA ore art lands.

## Adjacency / chain rules

- Depth chain, never skipped: deep ocean -> shallow rim -> beach dirt or
  foam-stone shoreline -> land. Deep water never touches land.
- Rivers: light water, grass banks meeting the stream directly (pond-style)
  with occasional sand bank patches; river surface sits one heigh