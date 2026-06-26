# 30 — Comprehensive Biome Improvement Plan

> Synthesises the asset inventory (`28_Biome_Asset_Inventory.md`) and the system review
> (`29_Biome_System_Review.md`) into one plan to make biomes feel **natural, purposeful, and
> distinct**. Two root problems, a target identity per biome, and a phased roadmap.

---

## 0. TL;DR — the two root problems

1. **Biomes are homogenised at the prop layer.** They differ at the *tile* level (meadow grass vs
   forest floor vs snow), but **every land biome shares one hard-coded decoration ladder**
   (`PickClusteredDecoration`), so the *props* — trees, bushes, flowers, tufts, rocks — are placed
   the same everywhere. A forest floor between trees looks like a meadow; "snow" is a green pine
   taiga. Biomes read same-y because their ground *cover* is identical.
2. **Placement is cluttered and ignores the authored design.** Every cell competes through all six
   prop classes (first hit wins), a flat **5% grass-tuft scatter leaves almost no bare ground**, and
   the four decoration noise fields are small multiples of one base frequency so their peaks
   **coincide** — groves, bushes, rocks and flowers pile onto the same ground. Crucially, the sampler
   **ignores the authored `biome_suite.json` contract** (density, featureScale, negativeSpace,
   occupancy/Poisson rules) and the biomes' own `nodes[].chancePerCell`, using only hard-coded
   `_cfg.deco*` constants. The design intent exists in data — it was just never wired up.

Plus: **half the Bible's biomes aren't real.** Live: meadow, forest, snow, beach, mountain. Desert is
**dead code** (its temp/moisture ranges are never set, so it never generates). Sunspool Fields,
Duskwick Marsh, and Glowcap Grotto have **no implementation**. Mountain's three height bands
(mossy/scree/snowcap) are authored but **not implemented** — every tier is the same grass+stone.

> Note: `config.flatWorld` defaults true, but `FoundationBootstrap.ApplyLaunchOptions` forces it
> **false** for standard play, so the continent/biome path *is* what you're seeing in-game.

---

## 1. Asset reality (from `28`)

- **Rich:** Mosswake Meadow (plains), Brindlecap Woods (forest) — full tile + prop sets. Honeyshale
  Cliffs (stone) and Winterwool Pines (snow) — rich *ground tiles*.
- **Thin/empty:** **Duskwick Marsh — essentially empty** (no peat/bog tile, murky water, mud↔water
  shore, reeds/cattails/lily pads). **Glowcap Grotto fungal — near-empty** (no luminous fungus,
  crystal/wet-stone, glowcaps, stalagmites). **Winterwool props empty** despite rich snow tiles (no
  snow-laden tree, ice tile, frosted rock/bush). **Sunspool/Kindlestep thin** (lean on dry-tint
  plains; missing reeds/bees and cracked-clay/cinder/dune variety).
- **Scale outliers (urgent):** props are mostly 128px, but the 9 cactus props are ~14–37px and the
  small-decor set (bush/rock/flower/log/stump/tuft/copper_vein/shore_stone) is 32px — they render
  broken next to full-size props. *(The new `FoundationSpriteScale` normalization fixes the
  mechanism; these just need their `heightUnits` set — see Phase 4.)*
- BiomeSketch counts are inflated by `sync_*`/`review_*` pipeline copies; true unique coverage is
  smaller. `Art/BiomeDecorations` has prompt-string filenames and `(1)/(2)` dupes.

---

## 2. Target biome identities

Each biome needs a one-line identity, a palette, a *dominant* ground cover, signature props, a density
feel, and a weather mood — so placement and art can aim at a target instead of a generic outdoors.

| Biome | Identity | Ground / palette | Dominant cover | Signature props | Density | Weather |
|---|---|---|---|---|---|---|
| **Mosswake Meadow** | gentle starter plains | bright grass, clover, soft dirt | flower-meadow + open grass | lone broad trees, flower patches, small rocks | airy, lots of open | Clear/Mist |
| **Brindlecap Woods** | dense mossy forest | moss, leaf litter, rich dirt | tree groves + fern undergrowth | oak/deep-oak, ferns, mushrooms, stumps, logs | dense cores, clear glades | Mist |
| **Sunspool Fields** | dry golden grassland | gold dry grass, warm dirt | tall dry grass + sparse | wheat-like tufts, lone dry trees, beehive, reeds at edges | open, windy | Wind |
| **Duskwick Marsh** | murky wetland | peat, mud, dark water | reed beds + bog pools | reeds/cattails, lily pads, dead snags, mud mounds | wet, broken by water | Drizzle/Mist |
| **Honeyshale Cliffs** | stone hill country | shale, exposed ore, sparse grass | rock fields + scree | boulders, ore veins, hardy shrubs, goats | sparse, vertical | Wind |
| **Kindlestep Badlands** | cracked desert/scrub | sand, cracked clay, cinder | bare sand + cactus clusters | cacti (re-scaled!), dry bushes, bleached rock | very sparse, big negative space | Wind/heat |
| **Winterwool Pines** | snow taiga | snow, blue shadow, ice | snow-laden pines + drifts | snow pines, frosted rocks/bushes, ice patches | medium, calm | Snow |
| **Glowcap Grotto** | luminous fungal cave | wet stone, glowlichen | glowcap clusters + crystal | glowcaps, stalagmites, crystals, glowbugs | clustered glow, dark gaps | none (cave) |

---

## 3. The plan — phased

### Phase B1 — Placement engine overhaul *(the universal "natural/purposeful" fix; benefits every biome at once)*
Rework `PickClusteredDecoration` into a zone-first pipeline:
- **B1.1 Negative space.** Add a low-frequency *clearing* mask: above a threshold a cell spawns
  **nothing**. Kill or hard-gate the universal 5% grass-tuft scatter. Bare ground becomes the default;
  features become the exception. *(Biggest single calm-down.)*
- **B1.2 One dominant cover per zone.** A single low-freq decision picks the cell's zone (grove /
  flower-meadow / thicket / rock-field / **bare**), then places **only that zone's** cover — no more
  trees+bushes+flowers+rocks competing on the same cell.
- **B1.3 Decorrelate the noise.** Give the zone fields genuinely different frequencies (or a Worley/
  cellular partition) so patches don't all peak on the same ground.
- **B1.4 Organic clumping + spacing.** Within a zone, place with blue-noise/min-distance + footprint
  reservation (honour the `occupancyPolicy`/Poisson rules already authored in `biome_suite.json`),
  so trees form tight stands with gaps instead of an even speckle.
- **B1.5 Terrain relation.** Tie cover to terrain: reeds/bushes hug water, scree on steep slopes,
  flowers prefer flat ground, rocks on higher/rockier cells.
- **Done when:** open glades and bare ground exist, each patch has one clear cover type, and stands
  clump naturally.

### Phase B2 — Per-biome prop divergence
Replace the single shared prop ladder with a **per-biome cover profile** (driven by each biome's own
`nodes` table + a small per-biome zone weighting), so forest ≠ meadow ≠ snow at the prop layer:
forest = groves + ferns + mushrooms; meadow = grass + flowers + the rare lone tree; snow = snow-pines
+ frosted rock; stone = rock fields + ore; etc. **Wire the sampler to read `biome_suite.json` +
`nodes[].chancePerCell`** instead of the hard-coded `_cfg.deco*` constants.

### Phase B3 — Activate the broken/missing live biomes
- **Fix desert:** set its `temperatureRange`/`moistureRange`/priority and add it to `biomeOrder` so it
  actually generates.
- **Mountain height strata:** implement the authored mossy/scree/snowcap bands so elevation reads.
- **Beach:** wire `driftwood` into the node table; widen the transition ring.

### Phase B4 — Asset gap-fill (via BiomeSketch / Asset Forge)
Priority order from the inventory:
1. **Duskwick Marsh from scratch** — peat/bog tiles, murky + mud↔water shore, reeds/cattails/lily
   pads/snags. (Highest gap; the biome is empty.)
2. **Glowcap Grotto fungal set** — glowlichen floor, crystal/wet-stone tiles, glowcaps, stalagmites.
3. **Winterwool props** — snow-laden pine, ice tile, frosted rock/bush.
4. **Re-scale** the cactus + 32px small-decor set, and **set `heightUnits`** on all props so the new
   normalization sizes them correctly (finishes the scale pass).
5. **Sunspool/Kindlestep distinctiveness** — dry grass/wheat, cracked-clay/cinder/dune tiles.

### Phase B5 — New Bible biomes + identity polish
Stand up **Sunspool Fields, Duskwick Marsh, Glowcap Grotto** as real biomes (climate niche + tile/
prop/mob tables) once B4 assets exist. Then per-biome polish: distinct ground floors (so forest floor
≠ meadow), transition tiles, palette tint, and biome-locked weather/audio.

---

## 4. Sequencing & impact

| Phase | What it fixes | Effort | Risk | Depends on |
|---|---|---|---|---|
| **B1 Placement engine** | the clutter, for ALL biomes at once | M–L | med (worldgen) | — |
| **B2 Per-biome props** | biomes stop looking same-y | M | med | B1 |
| **B3 Activate biomes** | desert/mountain/beach become real | S–M | low–med | — (parallel) |
| **B4 Asset gap-fill** | marsh/cave/snow/scale | L (art) | low | art pipeline |
| **B5 New biomes + polish** | the full 8-biome world | L | med | B4 |

**Recommended order:** **B1 first** (it's the "natural/purposeful" win you asked for and lifts every
biome immediately), then **B2** (divergent props) and **B3** (activate desert/mountain) in parallel,
with **B4** art gap-fill running alongside, and **B5** to finish the Bible's full roster.

The lowest-risk, highest-impact starting slice remains **B1.1 + B1.2** — negative space + one dominant
cover per zone — which I can implement as a contained rework of `PickClusteredDecoration`.

---

## 5. Open questions
1. **Scope of new biomes:** ship the 5 live biomes polished, or commit to the full 8 (needs the marsh/
   cave/Sunspool art)?
2. **Asset pipeline:** generate the missing marsh/cave/snow art via BiomeSketch/Asset Forge now, or
   design the placement engine first against existing assets and backfill?
3. **Mountain verticality:** how dramatic should cliffs/strata be vs. the cozy tone?

*Sources: `28_Biome_Asset_Inventory.md`, `29_Biome_System_Review.md`, `15_LitRPG_System_Bible.md`,
`18_Biome_Generation_And_Asset_Placement_Rules.md`, and `IsoTerrainSampler` / `biome_suite.json`.*
