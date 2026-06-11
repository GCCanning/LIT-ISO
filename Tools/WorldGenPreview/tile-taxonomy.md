# Isometric Tile Pack — Placement Taxonomy (artist logic)

Derived from the three reference recreations and per-tile inspection of all 115
tiles (`family-zoom/*.png`). This is the contract every generator must follow.

## Core principles the pack is built on

1. **Block world.** Every terrain tile is a *block*: a 2:1 diamond top plus a
   baked-in side skirt (~8 native px). Depth/cliffs are expressed by the skirt,
   not by separate cliff tiles. A raised tile must be backed by a dirt underlay
   block so its exposed base shows earth, never void.
2. **Dirt is the substrate.** Grass, stone and water sit *on or in* dirt. All
   grass blocks have dirt skirts; beaches are dirt; cliffs read as dirt.
3. **Fields are calm, accents are rare.** Large areas use ONE dominant tile
   (>= 80%) with subtle variants. Busy variants (speckles, waves, cracks) are
   accents, never the field.
4. **Props are pre-positioned overlays.** All props share the 32x32 canvas and
   sit correctly when drawn at the same cell rect as their terrain (lifted by
   the terrain's raise). Props never float on water; the only "water props" are
   the foam-footed stones and the sparkle overlays.
5. **Decoration clusters around anchors.** Flowers cluster in meadow openings
   near water/bushes; logs sit at forest edges; rocks cluster into outcrops at
   cliff bases and coasts. Nothing is uniformly scattered.
6. **Biome purity.** A biome only draws from its whitelist. Boundaries use the
   designated transition tiles below; no other cross-family mixing.

## Tile roles (all 115 tiles)

### Dirt 000–021
| Tiles | Role |
|---|---|
| 000–002, 004–010 | Flat dirt field blocks (subtle variants; 003 = darker top) |
| 003 | Dark-top dirt — underlay block for raised tiles / shaded ground |
| 011–013 | Rubble/cracked raised dirt blocks — badlands outcrops, cliff debris |
| 014–016 | Layered strata blocks — exposed cliff ledges, badlands mesas |
| 017–018 | Dark cracked barren floor — badlands field |
| 019–020 | Dirt with green sprouts — ONLY at meadow↔dirt/badlands boundaries |
| 021 | Smooth rounded dirt mound — beach / soft accent |

### Grass 022–040
| Tiles | Role |
|---|---|
| 022–024 | Bright grass blocks (dirt skirt) — plateau/highland tops |
| 025–026 | Root/cliff blocks with grass crest — forest edge facing lower ground |
| 027–029 | Dense hedge/canopy blocks (fill the cell) — forest canopy mass |
| 030–036 | Foliage clump props (varying density) — bushes/undergrowth |
| 037 | Smooth flat light-green field — open meadow floor |
| 038–039 | Fluffy grass mounds — sparse meadow accents |
| 040 | Flat deep-green field with dirt skirt — forest floor / lush lowland |

### Flowers & shrubs 041–047 (props, grass-only)
041/042/046/047 warm blooms · 044 pink tulips · 043/045 leafy shrubs.
Cluster 2–5 cells near water or bush anchors; never isolated singles everywhere.

### Logs 048–052 (props)
048 single, 049 pile, 050–052 mossy with grass tufts. Forest edges & clearings.

### Brown rocks 053–060 (props)
Badlands/dirt outcrops, rim walls, cliff bases on the dry side.

### Grey stone 061–081
| Tiles | Role |
|---|---|
| 061 | Cobble slab block — base course of rock masses |
| 062, 063 | Low cluster / smooth slab — outcrop filler |
| 064 | Tall pinnacle — back row of rock masses |
| 065, 067, 068 | Boulders (single/pair/angular) — outcrops, cliff bases |
| 066, 069–081 | Foam-footed stones — ONLY in water, hugging a shoreline |

### Sparkles 082–085
Transparent glint overlays — deep water only, drawn last, ~2% of cells.

### Dark water 086–103 (ocean)
| Tiles | Role |
|---|---|
| 092, 101 | Flat deep-navy field |
| 093/094/102/103 | Speckled navy — sparse field variants (<= 15%) |
| 086–091, 095–099 | Wave-swell blocks (lit crests) — surf accents on the deep side of a deep↔shallow boundary |
| 100 | Dark slab — deep-water edge filler |

### Light water 104–114 (shallows / lakes / rivers)
| Tiles | Role |
|---|---|
| 104/105, 109–113 | Flat shallow field (rivers, lakes, coastal ring) |
| 106–108 | Wave-streak variants — shore wash at the water↔land edge |
| 114 | Foam/crack tile — rapids or shore foam, very sparse |

## Layering & adjacency rules

- Vertical order in a cell: underlay block → terrain block → prop → overlay.
- Water depth chain: deep ocean → shallow ring → beach dirt or foam-stone
  shoreline → land. Deep water never touches land directly.
- Rivers are shallow (light) water, one cell wide, with dirt banks; they flow
  downhill and exit into the shallow ring.
- Plateau (level 1) tiles are raised one block (8 native px) over a 003 dirt
  underlay; plateau tops are bright grass (022–024) in green biomes, strata or
  rubble blocks (011–016) in badlands.
- Transition whitelist: 019/020 (meadow↔badlands), 025/026 (forest↔lower
  ground), beach dirt (land↔water), foam stones (rock↔water). Nothing else
  crosses biome lines.
