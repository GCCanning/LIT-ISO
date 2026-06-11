# LIT-ISO World Generation Spec — "A World, Not Scattered Tiles"
**Date:** 2026-06-10 · **Author:** Claude (from owner directives + 3 owner-authored golden vignettes)
**Implementer:** Codex (terrain sampler lane) · **Acceptance data:** `Tools/BiomeSketch/vignettes/*.json`

## Goal
Replace per-cell noise painting with a **layered, rule-driven generator** (Minecraft-style):
low-frequency world skeleton → biome regions → carved water features → banded shorelines →
clustered decoration. Every layer obeys structural rules the owner demonstrated in vignettes;
those vignettes are the acceptance tests.

## The owner's vignettes (ground truth)
| File | Demonstrates |
|---|---|
| `river_plains_v1.json` | River: 3–4 wide widening downstream, mud aprons, +1/cell walkable banks |
| `beach_coast_v1.json` | Coast: grass→sand band (3–6, stepping down)→waterline; swell tiles at contact; dunes |
| `lake_plains_v1.json` | Lake: elliptical blob in a bowl, 1–2 cell DIRT ring, shore stones in shallows, tree density rising with elevation around the rim |

**Universal law derived from all three: grass never touches water, anywhere.**
Apron material by water type: river = mud (`forest_mud_path` inner / `shared_mud_dark` outer),
ocean = sand, lake = dirt.

## Architecture — six ordered layers

### L0 · World skeleton (seeded noise fields, very low frequency)
- `C` continentalness → ocean / coast / inland macro-shape
- `E` relief → flat vs hilly
- `T` temperature, `M` moisture → biome axes
All deterministic from world seed. These are *region-scale* fields (features hundreds of cells wide), not per-cell jitter.

### L1 · Base elevation
- `height = f(C, E)` quantized to 0..7. Ocean where C < threshold (water h0).
- **Smoothing constraint: |Δheight| ≤ 1 between neighbors** (relaxation pass after quantize). Cliffs are a *deliberate later feature*, never noise artifacts.
- Coast band: land within N cells of ocean clamps to 0–1, rising gradually inland to 3–4 (owner rule).

### L2 · Biome regions
- Whittaker-style lookup on (T, M) for land cells: current 5 biomes now, System-Bible 8 later. **Biome is decided per region blob, not per cell.**
- Transitions blend over 3–6 cells: interleaved surface tiles + cross-faded decor densities. Hard single-cell biome edges are a test failure.

### L3 · Water features (carve, then apron)
- **Rivers:** trace a path from high-M inland springs downhill to ocean/lake. Channel 3 wide at source, widening to 4–5 downstream. Carve banks so terrain steps down **1 height per cell** to the waterline on both sides (always walkable). Mud apron 1 cell each side at water height.
- **Lakes:** at local minima in high-M regions: elliptical blob (r 3–8) at h0; enforce surrounding bowl (height rises with distance from rim for ≥4 cells); dirt ring 1–2 wide at h0.
- **Ocean shore:** sand band 3–6 wide stepping h2→h1→h0; final sand cell h0 meets water h0 (no cliffs into sea). Local dunes (sand up to h2) allowed if every shore-normal path still descends monotonically.
- **Water surface variety:** body = `water`/`water_deep`; `deep_2/3` ~10% sprinkle in open water; `swell_1/2` concentrated at shoreline contacts + rare open-water whitecaps.

### L4 · Structures & guarantees
Spawn flat-zone guarantee, distance-tiered dungeon portals (existing systems), future POIs — placed after terrain so they can demand local flatness without fighting the sampler.

### L5 · Decoration (per-biome rule tables, banded + clustered)
- **Clustering, not sprinkle:** tree positions via grove centers (low-freq points) + Poisson-disk fill at biome density inside grove radius; rocks as 2–4-stone outcrops; single-cell accents (flowers, tufts, stumps) at ~10–15%.
- **Band legality** (from vignettes): trees/flowers only on grass; rocks on dry aprons (sparse); shore_stones only in shallow water near the land contact; nothing on open deep water.
- **Elevation affinity:** tree density increases with height around water bowls (lake vignette rim pattern).
- **Tile variety:** 85% biome base tile + 15% single-cell accent variants, never two identical accent cells adjacent.

## Data-driven schema (datapack-style — owner-endorsed reference:
## https://datapack.wiki/wiki/worldgen/custom-worldgen)
Model the data layout on Minecraft's datapack worldgen, simplified for a heightmap
iso world. Generator reads JSON (StreamingAssets or TextAssets), so biomes/features
are moddable and OWNER-AUTHORABLE without code:

```
worldgen/
  noise_params.json          // L0: frequencies/octaves for C, E, T, M channels
  surface_rules.json         // ordered condition chain -> tile
  biomes/<id>.json           // one file per biome
  features/<id>.json         // configured features (WHAT a thing is)
```

**surface_rules.json** (ordered, first match wins — encodes the apron law):
```json
[
 {"if": {"waterAdjacent": "river"},  "tile": "forest_mud_path"},
 {"if": {"waterAdjacent": "lake"},   "tile": "dirt"},
 {"if": {"waterAdjacent": "ocean"},  "tile": "sand_2"},
 {"if": {"nearOcean": 4},            "tile": "sand_2"},
 {"if": {"biome": "*"},              "tile": "$biome.surfaceBase"}
]
```

**biomes/meadow.json** (biome source params + surface + feature list):
```json
{
 "params": {"temperature": [0.3, 0.7], "moisture": [0.0, 0.45]},
 "surfaceBase": "plains_grass_base",
 "surfaceAccents": [{"tile": "plains_flower_grass", "w": 7},
                    {"tile": "plains_grass_tufts", "w": 7},
                    {"tile": "plains_dry_grass", "w": 6}],
 "elevation": {"range": [0, 2], "relief": "gentle"},
 "features": ["lone_plains_tree", "plains_bush_patch", "rock_outcrop_small"],
 "spawns": {"mobs": ["deer", "fox"], "nodes": ["tree", "rock", "bush"]}
}
```

**features/<id>.json** — configured vs PLACED split (their best idea):
```json
{
 "id": "forest_grove",
 "configured": {"type": "decoration_cluster",
                "entries": [{"id": "forest_oak_tree", "w": 5},
                            {"id": "forest_deep_oak_tree", "w": 3}]},
 "placement": {"groveCenters": {"per100Cells": 1.2, "radius": 4},
               "fillDensity": 0.55, "falloffDensity": 0.15,
               "band": "grass", "heightBias": "uphill"}
}
```

Skip from the datapack model: density-function expressions, 3D cave noise,
vertical sections — heightmap world doesn't need them. Keep: noise router channel
naming (continents/erosion/temperature/vegetation ≈ our C/E/T/M), surface-rule
condition chains, configured/placed feature split, per-biome JSON.

## Owner rule additions (2026-06-10, second pass) — AUTHORED DATA SHIPPED
The complete starter data set now exists at **`Assets/StreamingAssets/worldgen/`**
(noise_params, surface_rules, 4 biomes, 12 features) using only current tiles.
New rules encoded there:
- **Water tile semantics:** ocean = `water_deep` family (deep_2/3 variation, swells
  at shore contact); lakes AND rivers = light `water` tile. One water language
  world-wide — no mixed tilesets within a body.
- **Beach band (revised):** ocean → wet sand (2–3) → dirt (1–2) → grass; rocks
  along the water edge and some in the shallows.
- **Cliff coasts:** a low-frequency `cliffiness` field sampled along the coastline
  varies cliff-top height continuously (1–4) — some stretches towering, some low,
  with coves (cliffiness < 0.45) carving stepped sand access. Coastlines undulate
  like real coasts.
- **Mountain stratification:** promoted where elevation field is high; surface
  bands grass_3 (h3-4) → badlands (h4-5) → stone_block caps (h5+); rock-prop
  density and copper-ore frequency BOTH scale with elevation (data in
  biomes/mountain.json stratification table).
- **Forest→plains transition:** tree density fades grove→sporadic over 4–7 cells
  while flower/long-grass (tuft) patch density rises — plains read as open and
  flowery, forest as dense and littered.

## Acceptance tests (validator additions — run against generated chunks)
1. **No grass-water adjacency anywhere.** (The single most important check.)
2. Neighbor height delta ≤ 1 except cells flagged as deliberate cliff features.
3. River cross-sections: width ≥3, mud apron present both sides, bank profile +1/cell.
4. Coast transect: band order grass→sand(3–6, descending)→water; no cliff at waterline.
5. Lake transect: water→dirt ring→grass with rising bowl ≥4 cells.
6. Decor band legality (zero violations) + tree nearest-neighbor distribution must reject uniform-random (proves clustering).
7. Accent tile rate 10–20%, no adjacent identical accents.
Golden vignettes in `Tools/BiomeSketch/vignettes/` are reference profiles for 3–5.

## Implementation order (suggested)
1. L1 smoothing + coast clamp (kills height spikes immediately)
2. Universal apron rule + L3 lakes (smallest carve feature)
3. L3 rivers (path trace + bank carve)
4. L5 cluster decoration + band legality
5. L2 region biomes + transitions
6. Validator acceptance tests throughout
