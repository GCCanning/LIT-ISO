# LIT-ISO — Biome Palette System & World-Generator Upgrade Prompt

> Built from a colour review of every world-gen-relevant tile/prop in
> `C:\Users\garyc\OneDrive\Desktop\PixelArt` (biome tilesets, floors/walls, and
> nature props) cross-referenced with the live generator
> (`IsoTerrainSampler.cs`, `BiomeDefinition.cs`, `FoundationConfig.cs`, and
> `StreamingAssets/worldgen/biomes/*.json`).
>
> Goal: fix the three problems you called out — **tiles too noisy**, **height
> doesn't feel natural**, **prop density doesn't feel natural** — by (1) giving
> each biome a disciplined palette, and (2) a concrete upgrade prompt for the
> generator. Colours are averaged/dominant samples of the actual art, so they
> describe what the tiles really are, not an idealised guess.

---

## 1. Art inventory (world-gen relevant)

| Folder | Files | Role | Maps to biome family |
|---|---|---|---|
| `Tilesets/plains` | 16 | grass ground | meadow (`plains2_*`) |
| `Tilesets/beach` | 16 | sand ground | beach (`sand_*`) |
| `Tilesets/farming` | 16 | tilled soil + planted rows | farm rings (`farm_*`) |
| `Tilesets/mountain_stone` | 16 | grey rock ground | mountain (`stone_*`) |
| `Tilesets/snow` | 16 | snow ground | snow |
| `Tilesets/dungeon_stone` | 16 | dark cool stone | dungeon interiors |
| `Tilesets/planks` | 16 | wood decking | docks (`planks_*`) |
| `Floors` / `Walls` / `Doors` | 24 / 22 / 4 | interior building shells | settlements/interiors |
| `Props/forest/*` | 30 | pine, dead tree, fern, stump, mushrooms | forest |
| `Props/camp/*`, `Props/buildings/*`, `Props/guild/*`, `Props/chest/*`, `Props/ambient/*` | ~300 | camp/town/dungeon set dressing | settlements |
| `FreePixel_Sorted/Foliage` | 99 | bushes, shrubs, grass tufts | meadow/forest accents |
| `FreePixel_Sorted/Trees` | 97 | named trees (apple, ash, autumn, bamboo, baobab, bare/winter, pine…) | per-biome canopy |
| `FreePixel_Sorted/Rocks` | 95 | boulders, outcrops | mountain/beach/forest |
| `FreePixel_Sorted/RPG_Environment/Nature_Props` | 70 | bush/flower/grass/log/mushroom families | all land biomes |
| `FreePixel_Sorted/Blocks` | 98 | cube/terrain blocks | height/cliff faces |

The `FreePixel_Sorted/Trees` names are descriptive enough to biome-sort directly:
`pine/spruce/fir` → forest+snow; `autumn-*`, `apple`, `ash`, `oak` → forest/meadow
fringe; `bamboo`, `banana`, `baobab`, `palm` → warm coast/tropical hinterland;
`bare-tree-winter`, `*-snow` → snow; `dead-tree` → blighted/dungeon approach.

---

## 2. Sampled colours → per-biome palette system

Hex values below are the **average** and the **dominant buckets** sampled from the
actual tiles (outline-black and anti-alias fringe filtered out where noted). The
design rule for every biome: **one dominant ground value (~70%)**, **1–2 close
variations (~22%)**, **a tiny accent set (~8%)** — never the current flat pool of
5–7 equal bases + 11 accents.

### Meadow / plains  — `Tilesets/plains` (avg `#649041`)
| Token | Hex | Use |
|---|---|---|
| grass.base | `#6E9A3E` | the dominant ground tile (one tile, ~70%) |
| grass.var-light | `#80A040` | subtle sun variation (~12%) |
| grass.var-deep | `#5A7C34` | subtle shade variation (~12%) |
| grass.shadow | `#406020` | slope/under-canopy, cliff-adjacent |
| grass.accent | `#80C040` / flowers `#C8C24A` | flower/tuft accents, **≤6% of cells** |
| prop.green | `#204020`→`#408020` | bushes, long grass, lone trees |

### Beach / coast — `Tilesets/beach` (avg `#9D896F`)
| Token | Hex | Use |
|---|---|---|
| sand.base | `#E0C0A0` | dominant dry sand (~75%) |
| sand.var | `#C0A080` | subtle ripple variation |
| sand.wet | `#A08060` | shoreline band against water |
| sand.shadow | `#6E5A44` | dune shade |
| prop.stone | `#736F78` | sparse shore rocks only (no trees on sand) |

### Forest — `Props/forest` + `forest_floor/canopy_*` (canopy avg `#453F2E`, foliage `#7A7F57`)
| Token | Hex | Use |
|---|---|---|
| floor.base | `#3E4A2A` | shaded leaf-litter ground (dominant) |
| floor.var | `#4A5630` | dappled-light variation |
| canopy.mass | `#1E3416` / `#26401C` | dense canopy terrain tiles (the forest "mass") |
| trunk | `#402020` / `#604040` | tree props |
| accent.moss/fern | `#406020` | moss patches, ferns, mushrooms |
| accent.glow | `#40C0E0` | glow-mushroom ambient (rare, night) |

### Snow / taiga — `Tilesets/snow` (avg `#B6CACE`)
| Token | Hex | Use |
|---|---|---|
| snow.base | `#E6ECEE` | dominant snow (~78%) |
| snow.var | `#CFE0E6` | subtle drift variation |
| snow.shadow | `#A0C0E0` | **blue** shadow in hollows / north faces |
| snow.deep | `#7C9AB0` | cliff-base shade |
| prop | bare/winter trees `#5A5A52`, pine `#26401C` capped white |

### Mountain stone — `Tilesets/mountain_stone` (avg `#687271`)
| Token | Hex | Use |
|---|---|---|
| stone.base | `#7A7E7C` | dominant grey rock |
| stone.var | `#606060` | banding variation |
| stone.shadow | `#3E4444` | cliff faces / crevices |
| stone.cap | `#C0E0E0` | snow/ice highlight on high peaks |
| stone.moss | `#608080` | low-altitude mossy tint (transition to forest) |
| prop.rock | `#736F78` / `#808080` | boulders, outcrops |

### Dungeon stone — `Tilesets/dungeon_stone` (avg `#45514E`) + `Floors`/`Walls`
| Token | Hex | Use |
|---|---|---|
| floor.base | `#46524F` | cool dark stone floor |
| floor.var | `#38484A` | variation |
| wall | `#2A3636` | wall blocks / void rim |
| accent.teal | `#204040`→`#406060` | damp/lichen accents |
| hazard.lava | `#E0701C` / glow `#FFC24A` | (for the new lava/fire-trap tiles) |

### Farm ring — `Tilesets/farming` (avg `#4E3B2F`)
| Token | Hex | Use |
|---|---|---|
| soil.base | `#6A4A2E` | tilled soil (`farm_00..03`, dominant) |
| soil.furrow | `#402A1C` | furrow shadow lines |
| crop.row | `#5E7A30` | planted-row variants (`farm_04..11`, accents) |

### Docks / wood — `Tilesets/planks` (avg `#675F50`)
| Token | Hex | Use |
|---|---|---|
| wood.base | `#86603E` | decking (`planks_00..03`) |
| wood.var | `#808060` | weathered plank |
| wood.shadow | `#403418` | board gaps |

### Global water (referenced by sampler, family `water_*`)
| Token | Hex | Use |
|---|---|---|
| water.deep | `#1C2C4A` (navy) | open ocean (one seamless field — keep speckle ≤15%) |
| water.shallow | `#2E5A6E` | shore band |
| water.swell | `#3E7E92` | rim-band wave highlights only |

**Cross-biome value discipline (the anti-noise rule):** within a biome, every
ground tile must sit within a narrow value range around `*.base` (≈ ±12% luma).
Accents may shift hue but not value. This is what stops the ground "fizzing."

---

## 3. Why it currently looks noisy / unnatural (diagnosis, with real values)

**Tile noise.** In `StreamingAssets/worldgen/biomes/meadow.json` the meadow has a
`surfaceBasePool` of **7 tiles at near-equal weight** (`plains2_02/05/07/11/15`
@20, `08/14` @12) and **11 `surfaceAccents`** at `accentRate 0.15`. Every cell
independently rolls `Hash01(wx,wy,7)` to pick a base and `Hash01(wx,wy,8/9)` for
an accent (`IsoTerrainSampler.cs` §B2/B3, ~lines 434–463). With 7 visually
distinct bases firing at random per cell, the ground reads as static/confetti
instead of a field. Other biomes follow the same shape.

**Height.** Land height is a hard 4-step function of elevation
(`continentTier2Level 0.62`, `Tier3 0.74`, `Tier4 0.85`, `maxHeight 4`,
~lines 333–337). Adjacent cells snap between integer tiers with no intermediate
slope, so hills read as stacked plateaus/cliffs. The sort-order ceiling caps at 7,
but only 4 tiers are used, so each step is large and abrupt.

**Prop density.** Decoration is per-cell: `PickClusteredDecoration(...,
densityScale)` and canopy `Perlin > 0.72`. Beach/crag special-cases exist, but
the meadow/forest land path can place a node on a large fraction of cells, and the
base pool noise compounds it — there's no "breathing room" of plain ground
between feature clumps, so density reads as uniform clutter rather than groves with
clearings.

---

## 4. THE UPGRADE PROMPT (hand to an implementer or paste into an AI agent)

> **Task: make LIT-ISO's continent world-gen read as natural, hand-placed terrain
> — less tile noise, believable height, grove-style prop density — using the
> per-biome palette system in `Docs/WORLDGEN_PALETTE_AND_UPGRADE_PROMPT.md`.
> Touch only the generator + biome JSON; keep the streaming/pure-function and
> isometric sort/height invariants in `AGENTS.md`.**
>
> **A. Kill the ground noise (biome JSON + `SurfaceVariantBlended`/accent roll).**
> 1. Restructure every biome's `surfaceBasePool` to one dominant base (~70% weight)
>    + at most two *low-contrast* variations (~15% each). Demote the rest to
>    `surfaceAccents`. Enforce the value-discipline rule (§2): variations stay
>    within ≈±12% luma of the base; reject any "base" tile that is actually a
>    flowered/decorated tile (those are accents).
> 2. Drop `accentRate` from `0.15` to **`0.04–0.06`** for ground biomes, and cut
>    each `surfaceAccents` list to **3–4 tiles** max (the strongest reads only).
> 3. Make accents *cluster* instead of per-cell sprinkle: gate the accent roll on a
>    low-frequency Perlin mask (reuse `decoForestFrequency`-style field) so flower
>    tufts/leaf-litter appear in patches, not single random cells. Same
>    `Hash01` for the in-patch pick, but only inside `noise > threshold`.
> 4. Keep the speckle on water ≤15% (already correct) — don't add base variety to
>    the ocean field.
>
> **B. Natural height (`IsoTerrainSampler.SampleContinent` height block + config).**
> 1. Raise usable tiers: use the full 0–7 sort ceiling instead of 4. Replace the
>    4 hard thresholds with a continuous map `height = round(smoothstep(beachLevel,
>    1.0, e) * heightCeiling)` then clamp, so elevation→height is gradual.
> 2. Add **slope-aware smoothing**: when a cell's height differs from the max of its
>    4-neighbours by >1, pull it toward them (one-step max delta per cell except at
>    intentional cliff bands) so hills ramp instead of step. Keep it a cheap fixed
>    4-neighbour probe (no global pass — preserve streaming).
> 3. Reserve true cliffs for a *minority* band (e.g. only where the elevation
>    gradient is steep, detected via the existing `ContinentElevation` neighbour
>    probe) so cliffs feel deliberate, not everywhere.
> 4. Apply the per-biome `relief` hint already in the JSON (`"gentle"` etc.) as a
>    height-variance multiplier so meadows stay rolling and mountains stay craggy.
>
> **C. Natural prop density (`PickClusteredDecoration` + canopy + node rates).**
> 1. Target *coverage*, not per-cell chance: tune so land averages roughly
>    **forest ~35–45%**, **meadow ~12–18%**, **beach ~3–5%**, **crag ~8%** of cells
>    carrying a prop, with the rest plain ground (visible clearings).
> 2. Drive placement from a low-freq "grove" Perlin field so trees/bushes form
>    clumps with empty margins; inside a grove, vary species using the biome's tree
>    list (use the descriptive `FreePixel_Sorted/Trees` names to biome-sort:
>    pine/spruce→forest&snow, autumn/oak/apple→forest fringe, bamboo/baobab/palm→
>    warm coast, bare/winter→snow). One canopy "mass" tile set already exists for
>    forest — keep using it for the dense core, props for the edges.
> 3. Keep the existing good rules: no trees on cliff lips (swap to bush), bare crag
>    = rock outcrops only, no trees on sand. Extend the same restraint to snow
>    (sparse) vs forest (dense).
> 4. Scale density by distance-from-spawn only mildly; let biome identity dominate.
>
> **D. Palette wiring.** Ensure each biome's chosen tiles match its palette tokens
> in §2 (e.g. meadow ground = the `grass.base` tile, snow shadow tiles carry the
> blue `#A0C0E0` shade, mountain faces use `stone.shadow`). Where two biomes meet,
> the transition accent ramp (already in `transitionTo`) should fade between the two
> palettes' accents only — never introduce a third family.
>
> **Acceptance check:** generate the biome-showcase seed (`240611`) and a few random
> seeds; confirm (1) each biome reads as a coherent colour field with occasional
> patches, not per-cell confetti; (2) hills ramp with only occasional deliberate
> cliffs; (3) forests are clumped groves with clearings, meadows are mostly open
> with flower patches, beaches nearly bare. No change to chunk streaming, the
> isometric sort axis, or `maxWalkStepHeight` behaviour.

---

## 5. Suggested concrete first edits (smallest high-impact set)

1. **`meadow.json` / `forest.json` / `snow.json` / `beach.json` / `mountain.json`:**
   collapse `surfaceBasePool` to dominant+2, trim `surfaceAccents` to 3–4, set
   `accentRate` `0.05`.
2. **`FoundationConfig.cs`:** raise effective height ceiling usage (map elevation
   across 0–6 instead of 1–4); add a `heightSmoothing` toggle/strength field.
3. **`IsoTerrainSampler.cs`:** add the smoothstep height map + 4-neighbour slope
   clamp in the land block (~lines 333–348); gate the §B3 accent roll behind a
   low-freq patch mask; add the grove-coverage targets to `PickClusteredDecoration`.

These three touch the noise, height, and density causes directly and are all
local/pure (streaming-safe).
```

I can implement step 5 (the JSON palette trims + the height/accent/density code changes) next if you want — say the word and I'll do it as proper edits with the invariants preserved.
