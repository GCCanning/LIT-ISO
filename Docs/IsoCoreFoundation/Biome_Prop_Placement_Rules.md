# Biome Prop Placement Rules

## Purpose

Procedural chance never decides whether a prop belongs somewhere. It only decides
whether an already-eligible prop appears.

The runtime evaluates each candidate in this order:

1. Biome node whitelist.
2. Sprite availability under `Resources/Decorations`.
3. Surface substrate compatibility.
4. Elevation range.
5. Water-proximity modifier.
6. Independent tree, ground-cover, or geology cluster noise.
7. Authored `chancePerCell`.
8. Stable-ground and cliff/water-edge clearance for blocking or large props.

Vegetation restores its own biome surface when a transition tile was borrowed, so
a tree cannot remain visually planted on a foreign stone transition tile.

## Ecological Basis

The model follows broad real-world controls rather than species simulation:

- Climate, topography, moisture, soil, and elevation jointly shape plant
  communities.
- Wetlands favor saturated soils and water-tolerant vegetation.
- Thin, steep, high-elevation soils support sparse vegetation.
- Rock outcrops and shallow dry soils should remain mostly bare.
- Vegetation forms communities and patches rather than uniform random scatter.

References:

- https://www.nps.gov/im/sodn/ecosystems.htm
- https://www.nps.gov/yose/learn/nature/wetlands.htm
- https://www.nps.gov/glac/learn/nature/soils.htm
- https://www.nps.gov/yose/learn/nature/plants.htm
- https://www.nps.gov/care/learn/nature/naturalfeaturesandecosystems.htm

## Runtime Substrates

`Organic`, `Mud`, `Sand`, `Snow`, `Stone`, `Cinder`, `Moss`,
`GoldenGrass`, `Constructed`, and `Water`.

Constructed and water surfaces reject all natural props unless a future explicit
rule opts in.

## Current Biome Matrix

| Biome | Vegetation substrates | Geology substrates | Character |
|---|---|---|---|
| Meadow | Organic | Organic, Stone | Open grass, sparse groves and outcrops |
| Forest | Organic, Moss | Organic, Moss, Stone | Dense tree and undergrowth patches |
| Snow | Snow | Snow, Stone | Pine-led cold vegetation and rock pockets |
| Marsh | Mud, Moss | None | Low wet vegetation and deadwood, water-biased |
| Grotto | Moss | Moss, Stone | Low luminous flora placeholder and dense geology |
| Sunspool | GoldenGrass | None | Open warm grass and flower/shrub patches |
| Badlands | Cinder, Sand | Cinder, Stone | Cactus, dry scrub, exposed rock and ore |
| Frozen Mountain | None | Snow, Stone | Bare cold rock and ore |
| Mountain | None | Stone, Snow | Bare high-tier rock and ore |
| Beach | None | Sand, shallow shore water | Sparse stable shore outcrops only |

## Current Limits

- Generated resource nodes still occupy one anchor cell. Multi-cell footprint
  reservation must be coordinated with movement, harvesting, respawn, and saves.
- Path and settlement clearance are handled by their existing systems, not by a
  chunk-wide prop competition pass.
- Biome-specific PixelLab props under `Assets/Art/Biomes` are not promoted by
  filename guessing. They need stable runtime node IDs before entering rules.
- A future suite v3 should serialize these typed rules directly and use a
  deterministic chunk-halo planner for spacing, adjacency, and footprint winners.
