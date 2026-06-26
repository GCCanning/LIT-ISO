# 29 — Biome System Review (Current State + Coherence + Placement)

Status: review only (no code changed). Drone pass to inform a biome-improvement plan.

Sources read:
- `Assets/Scripts/IsoCoreFoundation/Biomes/BiomeDefinition.cs`
- `Assets/Scripts/IsoCoreFoundation/Core/FoundationContent.cs` (biome block, ~L838–1062)
- `Assets/Scripts/IsoCoreFoundation/Core/FoundationConfig.cs` (noise/deco defaults)
- `Assets/Scripts/IsoCoreFoundation/World/IsoTerrainSampler.cs` (assignment + placement)
- `Assets/StreamingAssets/worldgen/biome_suite.json`, `noise_params.json`
- `Docs/IsoCoreFoundation/18_Biome_Generation_And_Asset_Placement_Rules.md`
- `Docs/IsoCoreFoundation/15_LitRPG_System_Bible.md` (§ Biomes And Region Names)

---

## 0. How a cell gets its biome (the runtime path)

Default config is `continentWorld = true` (and `flatWorld = true` is also set —
`flatWorld` is checked first in `Sample()`, so **the live game is the flat-world
path unless `flatWorld` is turned off**). Three independent code paths exist in
`IsoTerrainSampler.Sample()`:

1. **Flat world** (`_cfg.flatWorld`): one uniform meadow surface everywhere, optional
   Perlin rolling hills outside the spawn clearing, and clustered decoration that
   **reuses only the meadow biome's node table**. No other biome is ever sampled here.
   Biome identity is effectively a single biome (meadow) in this mode.
2. **Continent world** (`_cfg.continentWorld`, via `SampleContinent`): the full
   ocean→beach→land→cliff→river model with the climate-table biome selection. This
   is the path the rest of this doc analyses, since it's the only one with multiple
   biomes.
3. **Legacy noise path** (neither flag): simple `SelectBiome(temp,moist)` per cell
   with a flat water threshold. Largely superseded.

Biome assignment in `SampleContinent` (the real multi-biome path):
- Elevation `ContinentElevation` (`continentFrequency = 0.0045`, +3× detail octave,
  +spawn-land bias) drives ocean/beach/land and the cliff **height tier** FIRST.
- Climate fields: `temp = Perlin(...climateFrequency=0.011, salt 1,2)`,
  `moist = Perlin(...climateFrequency=0.011, salt 3,4)`. Same low frequency for both.
- `SelectBiome(t,m)` (A3 climate table): collect every biome whose
  `temperatureRange × moistureRange` rectangle contains (t,m); 0 matches → nearest
  centroid; 1 → that biome; 2+ → highest `climatePriority`, ties by centroid distance.
- A4 lapse: `effectiveTemp = temp − lapseRatePerHeightStep × height`, re-pick biome.
- Mountain override: any cell with `height ≥ elevationGateMinHeight (3)` becomes
  mountain regardless of climate.
- Beach remap: if the climate table picks `beach` for an interior (non-water) cell,
  it is forced to `meadow` (no inland sand).

So in practice biome is **mostly a function of elevation tier**, with climate only
separating meadow/forest/snow on the low tiers. Beach and mountain are gated, not
climate-selected (`climatePriority = -1`).

---

## 1. Per-biome current state

Climate rectangles (from `FoundationContent.cs`):

| Biome | tempRange | moistRange | priority | lapse/step | elevGate | Selected how |
|---|---|---|---|---|---|---|
| meadow | 0.35–0.75 | 0.00–0.45 | 0 | 0.05 | — | climate (dry/temperate) |
| forest | 0.25–0.70 | 0.45–1.00 | 1 | 0.06 | — | climate (wet/temperate) |
| snow | 0.00–0.25 | 0.00–1.00 | 2 | 0.00 | — | climate (cold band, any moisture) |
| beach | 0.00–1.00 | 0.00–1.00 | −1 | — | — | water-adjacency gate only |
| mountain | 0.00–1.00 | 0.00–1.00 | −1 | 0.08 | **3** | height-tier gate only |
| desert | (no range set) | (no range set) | 0 (default) | — | — | **never selected** (see §1f) |

### 1a. meadow ("Mosswake Meadow" — starter plains)
- **Surface:** `meadowRuntimeGroup` variants; B2 base pool `plains2_02/05/07/11/15`
  (w20) + `plains2_08/14` (w12); accents `plains2_*` + `plains_flower_grass`,
  `plains_grass_tufts` at `accentRate = 0.15`.
- **Nodes (chancePerCell):** `tree 0.006`, `rock 0.02`, `bush 0.05`, `flower 0.051`,
  `tulip 0.001`, `tuft 0.099`, `log 0.005`, `stump 0.005`. (A large tail of
  `plains_*`/`campsite_*` entries are registered at **chance 0.0** — they exist only
  so the grove/cluster art-variant picker `FindArtNodeVariant` can find their art;
  they never roll via the per-cell node loop.)
- **Mobs:** deer 1, slime 1, bandit 0.5, advKnight 0.3, advMage 0.2, advRogue 0.3.
- **Transitions:** →forest (4–7), →beach (2–3), →snow (5–8).
- Note: the per-cell `nodes` chances above are only used by the legacy path; in
  `SampleContinent` decoration comes from `PickClusteredDecoration`, which ignores
  `chancePerCell` and uses its own hard-coded densities (see §3).

### 1b. forest ("Brindlecap Woods")
- **Surface:** base pool `forest_grass_base` (60), `forest_floor` (20),
  `forest_moss_grass` (20); accents `forest_floor`, `forest_leaf_litter`,
  `forest_moss_grass`, `forest_dark_underbrush`, `forest_grass_tufts`; accentRate 0.15.
- **Canopy mass:** forest also tiles **`canopy_1/2/3` as terrain** where
  `Perlin(decoForestFrequency=0.04) > 0.72` on height <3; those cells carry no props.
- **Nodes:** `tree 0.046`, `rock 0.02`, `copperVein 0.01`, `flower 0.012`,
  `tuft 0.02`, `log 0.01`, `stump 0.01`, `forestStump 0.01`; large 0.0 art-variant tail.
- **Mobs:** deer 1, fox 1, slime 0.5, bandit 0.6, advRogue 0.3, advKnight 0.2.
- **Transitions:** →meadow (4–7), →snow (5–8).
- `baseHeight 1, variance 3`.

### 1c. snow ("Winterwool Pines" — a TAIGA, not tundra)
- Implemented as a **pine forest on grass ground**, not snow plates (comment: the pack
  has no snow family yet, so `snowRuntimeGroup` is largely a placeholder).
- **Surface:** base pool `snow2_03/04/07/12/13` (16) + lighter `snow2_*`; accents
  `snow2_08/09/10`; accentRate 0.15; thaw tiles `snow2_08/09`.
- **Nodes:** `tree 0.0`, **`pine 0.122`** (its identity prop), `rock 0.03`,
  `copperVein 0.008`, `tuft 0.02`, `log/stump 0.008`.
- **Mobs:** deer 1, fox 0.5, bandit 0.4, advTank 0.25.
- **Climate:** coldest band (temp ≤ 0.25), any moisture, highest priority (2).
- **Transitions:** →meadow, →forest (5–8) with thaw tiles.

### 1d. beach ("coast band")
- Gated by water adjacency, never climate-selected.
- **Surface:** `sand_*` family base pool (wet/dry merged) + sand accents; accentRate 0.12.
- **Nodes:** essentially rock-only (`rock 0.004`, plus shore_stone art). In
  `SampleContinent` beaches are placed sand + `PickRockOutcrop` only (no trees/bushes).
- **Mobs:** fox 0.5, slime 1, advMage 0.25.
- `baseHeight 1, variance 1`. Transition →meadow (2–3).

### 1e. mountain ("Honeyshale Cliffs")
- Gated by `elevationGateMinHeight = 3`; any tall cell becomes mountain.
- **Surface:** base pool `grass_3` (the band 3-4 "mossy shoulder"), accent `stone_mossy`;
  accentRate 0.10. `biome_suite.json` describes 3 height bands (mossy 3-4, scree 4-5,
  snowcap+ore 5-7) but the C# surface pool only encodes the first band.
- **Nodes:** `rock 0.16`, `copperVein 0.04`, `pine 0.012`, `stump 0.006`, plus an
  **ore ladder** `ironVein 0.02 / silverVein 0.01 / goldVein 0.005 /
  manacrystalVein 0.002 / starmetalVein 0.001`.
- **Mobs:** fox 0.5, slime 0.6, bandit 0.5, advTank 0.2.
- `baseHeight 3, variance 2`, lapse 0.08. Transitions →meadow/forest (4–6), →snow (3–5).
- In `SampleContinent`, height ≥3 land takes the **bare-crag branch**:
  `PickRockOutcrop` only — no trees/bushes on stone. So mountains read as rock + ore.

### 1f. desert ("Kindlestep Badlands") — **defined but dead**
- Built in `FoundationContent.cs` with the sand family + cactus props and slime/bandit
  mobs, BUT **`temperatureRange`/`moistureRange` are never set** → both default to the
  `BiomeDefinition` `(0,1)` full range with `climatePriority = 0`. It therefore overlaps
  meadow/forest/snow and never wins `SelectBiome` (lower or equal priority, and beach is
  the only other priority-0 biome). It is also not in `biome_suite.json`'s `biomeOrder`.
  **Net: desert never appears in the world.** Cactus art and warm-ore intent are unused.

### Bible vs. reality
The LitRPG Bible (§494) lists **8 named biomes**: Mosswake Meadow, Brindlecap Woods,
Sunspool Fields (dry grassland), Duskwick Marsh (wetland), Honeyshale Cliffs (stone),
Kindlestep Badlands (desert), Winterwool Pines (snow forest), Glowcap Grotto (cave).
Code ships **5 live** (meadow, forest, snow, beach, mountain) + 1 dead (desert).
**Sunspool Fields, Duskwick Marsh, and Glowcap Grotto have no implementation.** No
wetland, no dry-grassland, no fungal/cave biome exists.

---

## 2. Identity / coherence assessment

| Biome | Reads as a distinct place? | Biggest weakness |
|---|---|---|
| meadow | Yes-ish | It's the *only* low-elevation default; because beach→meadow and desert-dead, almost all flat land is meadow. Tiles are varied but the **prop mix (trees+bushes+flowers+tufts+rocks all competing)** makes it read like generic "outdoors," not specifically a soft starter meadow. |
| forest | **Strongest identity** | Canopy-mass terrain + grove trees genuinely read as woods. Weakness: it shares the same bush/flower/tuft/rock scatter as meadow, so the forest *floor* between trees looks identical to a meadow. |
| snow | Weak/misleading | It's a green pine taiga, not snow — no snow ground art. Without a cold surface family it reads as "forest with pines." Identity depends entirely on the `pine` prop. |
| beach | Thin but clean | Clean sand + sparse rock is coherent, but it's purely a transition ring; nothing makes a beach memorable (no driftwood/shells live; `driftwood` is in suite density but not in node table). |
| mountain | Decent (rock+ore) | Reads as gray rock with ore, which is fine, but `biome_suite.json`'s 3 height bands (mossy/scree/snowcap) are **not implemented** — every mountain tier looks the same `grass_3`+`stone_mossy` mix. No vertical strata = flat-feeling mountains. |
| desert | None | Dead code; never generated. |

**Cross-cutting coherence problem:** because `PickClusteredDecoration` is **shared by
every land biome** with the same hard-coded prop ladder and densities, the *ground cover*
(bushes, flowers, tufts, rocks) is nearly identical across meadow/forest/snow. Biome
distinctiveness currently comes almost entirely from (a) surface tile family and (b) one
or two signature props (canopy for forest, pine for snow, ore for mountain). The biomes
are differentiated at the tile level but **homogenised at the prop level**.

---

## 3. How decoration placement works today — and why it feels cluttered

The land-prop entry point in `SampleContinent` is, per non-canopy, non-crag, height<3
cell, a single call to **`PickClusteredDecoration(wx, wy, biome, densityScale)`**
(`IsoTerrainSampler.cs` ~L990). Crag cells (height ≥3) and beaches use
**`PickRockOutcrop`** instead (rock-only).

`PickClusteredDecoration` evaluates competing prop classes **in order, first hit wins**,
each gated by its own Perlin field + a `Hash01` roll:

1. **Trees / pines** — grove field `Perlin(decoForestFrequency = 0.04, salt 21,22)`.
   Inside a grove (`forest > decoForestThreshold = 0.55`): `chance =
   decoTreeDensityInForest(0.30) × (0.35 + 0.65×depth) × density`. Pine is a 35% minority.
   Rare stump (0.02) / log (0.015) on grove ground.
2. **Bushes** — band field `Perlin(decoForestFrequency × 1.4 = 0.056, salt 61,62)`.
   `chance = decoBushChance(0.022) × density`, ×(3.5–6) inside bands (`bandN > 0.60`),
   ×0.25 on open ground.
3. **Flowers / tulips** — patch field `Perlin(decoForestFrequency × 2.3 = 0.092,
   salt 41,42)`. If `patch > 0.5` and `Hash01 < 0.10×density`.
4. **Mushrooms** — flat `Hash01 < 0.018 × density`.
5. **Grass tufts** — flat `Hash01 < 0.05 × density`. **Universal ~5% scatter** on every
   remaining open cell, any biome.
6. **Rocks / copper** — cluster field `Perlin(decoForestFrequency × 1.7 = 0.068,
   salt 31,32)`. If `rockNoise > decoRockClusterThreshold(0.72)` and
   `Hash01 < decoRockChanceInCluster(0.07) × density`. Copper = ~16% of rock hits.

### Why it feels cluttered (confirmed against code)

1. **Every land cell competes for trees+bushes+flowers+mushrooms+tufts+rocks.**
   Confirmed: one cell falls through all six classes in sequence. There is no "this zone
   is dominated by cover X" concept — every cell independently rolls every layer.

2. **~5% universal grass-tuft scatter leaves little bare ground.** Confirmed: step 5 is a
   flat `Hash01 < 0.05` on any open cell of any biome, applied *after* trees/bushes/
   flowers already claimed their cells. Combined with the other layers, the probability a
   given open cell ends up empty is low → **almost no negative space**.

3. **Four overlapping noise fields at near-identical frequencies.** Confirmed — all are
   small multiples of one base `decoForestFrequency = 0.04`:
   - trees/canopy: `0.04`
   - bush bands: `0.056` (×1.4)
   - rock clusters: `0.068` (×1.7)
   - flower patches: `0.092` (×2.3)
   They differ only by salt and a <2.3× frequency spread, so their high/low regions
   **coincide spatially**. Where the tree field peaks, the bush/rock/flower fields are at
   similar phase → groves, bush bands, rock clumps and flower patches **pile onto the same
   ground** rather than occupying separate areas. The result is "everything happening
   everywhere," which is the core clutter cause.

4. **Within-grove placement is per-cell random, not clumped.** Confirmed: inside a grove
   the only spacing control is `chance = density × (0.35+0.65×depth)`; each cell rolls
   `Hash01(...,71)` independently. There is **no min-distance / blue-noise / footprint
   reservation** (the `occupancyPolicy: "reserve every prop footprint"` from
   `biome_suite.json` and the Poisson-disk note in doc 18 are **not implemented**). Trees
   land on adjacent cells freely, so groves read as a noisy mash rather than discrete trunks
   with gaps. This directly contradicts the design's "readable groves, not uniform scatter."

5. **No dominant-cover-per-zone or terrain relation beyond a couple of hard gates.** The
   only terrain-awareness present is: crag (≥3) = rock-only, cliff-lip trees swapped for
   bushes, beaches = rock-only. There is no notion of "this patch is a meadow of flowers"
   vs "this patch is bare grass" vs "this patch is a thicket" — every patch is all of them.

### What the suite/docs ask for but code doesn't do
`biome_suite.json` declares per-biome `density`, `featureScale`, `negativeSpace`
(`clearingChancePerGrove 0.18`, `minimumClearingRadiusCells 3`), `globalPlacement`
(`minimumWalkLaneWidthCells 2`, `occupancyPolicy reserve footprints`,
`suppressTallDecorNearCliffEdge`). **None of these JSON values are read by the sampler**
— `PickClusteredDecoration` uses only the hard-coded `_cfg.deco*` constants. The biome
`nodes[].chancePerCell` values are likewise ignored in the continent path. So the
authored placement contract and the live behaviour have diverged.

---

## 4. Noise-frequency quick reference (exact values)

| Field | Frequency | Source |
|---|---|---|
| climate (temp & moist) | 0.011 | `climateFrequency` |
| legacy height | 0.06 | `heightFrequency` |
| continent base elevation | 0.0045 (+3× detail) | `continentFrequency` |
| river band / warp | 0.025 / 0.02 | `riverFrequency` / `riverWarpFrequency` |
| grove / canopy (trees) | 0.04 | `decoForestFrequency` |
| bush bands | 0.056 | `decoForestFrequency × 1.4` |
| rock clusters | 0.068 | `decoForestFrequency × 1.7` |
| flower patches | 0.092 | `decoForestFrequency × 2.3` |

Key thresholds: `decoForestThreshold 0.55`, `decoTreeDensityInForest 0.30`,
`decoBushChance 0.022`, `decoRockClusterThreshold 0.72`, `decoRockChanceInCluster 0.07`,
canopy `> 0.72`, flower patch `> 0.5`, bush band `> 0.60`.

---

## 5. Summary of what the placement system needs

1. **Negative space.** Drop or strongly gate the universal 5% tuft scatter; introduce an
   explicit "bare ground" outcome and honour `negativeSpace.clearingChancePerGrove` so
   groves have real clearings.
2. **One dominant cover per zone.** Replace the six-independent-rolls model with a single
   per-zone "cover type" decision (e.g. low-freq field → this patch is *grove* / *meadow-
   flowers* / *thicket* / *bare* / *rock field*), then place only that cover's props.
3. **Decorrelate the noise fields.** Use genuinely different frequencies (and ideally a
   partition/Worley field), not four small multiples of 0.04, so cover types occupy
   *separate* ground instead of stacking.
4. **Clumped (blue-noise) within-grove placement + footprint reservation.** Implement the
   `occupancyPolicy` / Poisson-disk min-distance already specified in `biome_suite.json`
   and doc 18 so trees form readable groves with gaps.
5. **Terrain relation.** Extend beyond the few existing gates: tie cover to slope/height/
   water proximity (bushes and reeds near water, flowers on gentle slopes, scree on steep
   mountain bands) and implement mountain's 3 height bands.
6. **Per-biome prop divergence.** Give each biome a distinct dominant cover and signature
   props rather than sharing one `PickClusteredDecoration` ladder, so the *ground* (not
   just the tile) differs between meadow/forest/snow.
7. **Wire the authored contract.** Make the sampler actually read `biome_suite.json`
   `density`/`featureScale`/`negativeSpace`/`globalPlacement` (currently ignored).
8. **Fix/flesh biome roster.** Either give desert real climate ranges or remove it; decide
   whether Bible biomes (Sunspool dry-grassland, Duskwick marsh, Glowcap grotto) are in
   scope; replace snow's placeholder ground with real cold-surface art.
