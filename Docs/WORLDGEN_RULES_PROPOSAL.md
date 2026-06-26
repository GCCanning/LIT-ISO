# Worldgen Rules Proposal — biomes, blending, props, towns

Owner-requested research pass (2026-06-12). Companion tool:
`Tools/BiomeSketch/rules.html` (drag-drop biome/tile/prop triage, exports
`biome_assignments.json` that this proposal consumes as ground truth).

Hard constraint carried through every rule below: the runtime sampler streams
per-cell as a pure function of `(x, y, seed)` (plus cheap neighbour probes).
Every proposed rule is phrased so it stays chunk-local — no global passes.

---

## Part 1 — PixelLab asset inventory (current state + cleanup)

### Generated tile families (PixelArt/Tilesets, 8 families x 16 tiles = 128)

| Family | Promoted to Unity? | Used by rules? |
|---|---|---|
| plains | YES as `plains2_00..15` | meadow base/accent pools |
| snow | YES as `snow2_00..15` | snow base/accent pools |
| dungeon_stone | YES as `dungeon2_00..15` | dungeon_themes.json |
| mountain_stone | YES (2026-06-12) as `stone_scree/stone_mossy/stone_cracked/stone_snowcap` (tile_0/1/6/15) | mountain.json stratification — ids now resolve to real art |
| beach | YES (2026-06-12) as `beach_00..15` | not yet wired into beach.json (Phase-1 B6 wet/dry-sand blend) |
| farming | YES (2026-06-12) as `farm_00..15` | nothing yet (reserved: farm plots, D5) |
| interior | **NO** | nothing (reserved: building interiors) |
| planks | YES (2026-06-12) as `planks_00..15` | nothing yet (reserved: interiors/docks, D5/D6) |

### Generated props (PixelArt/Props, 74 ids x 4 frames = 293 sprites)

Promoted: 53 ids. **Not promoted (21):** all 5 camp tents + bedroll,
library set (bookshelf_short/tall, candelabra, globe_stand, reading_desk),
town set (boat_rowing, dock_post, scarecrow, tavern_sign),
forest extras (fern, log, mushrooms_glow, pine_young), trophy_pedestal.

### Cleanup actions (Phase 0 — data hygiene)

1. **DONE (2026-06-12, Claude task #19).** Promoted `mountain_stone`
   tile_0/1/6/15 -> `stone_scree`/`stone_mossy`/`stone_cracked`/`stone_snowcap`
   (no numbered variants referenced anywhere in worldgen JSON); also promoted
   `beach` -> `beach_00..15`, `farming` -> `farm_00..15`, `planks` ->
   `planks_00..15`. mountain.json already used the exact stone_* ids, so no
   JSON id changes were needed — they now resolve to real art instead of
   fallback. beach/farm/planks rule wiring remains Phase-1+ (D5/D6/B6).
   `interior` family remains unpromoted.
2. Fix `dungeon_floor_1..5`: 256x512 imported at PPU 32 (8–16 world units per
   sprite). Re-author to 32px or set PPU 256 + slice. They also duplicate the
   promoted `dungeon2_*` family — consider retiring them outright.
3. Triage the 61 promoted-but-unreferenced props in `rules.html`; export
   assignments; retire whatever stays unassigned.
4. Delete or wire `StreamingAssets/worldgen/biomes/coast.json` (dead config,
   legacy schema, not in biomeOrder).
5. `desert` biome exists in runtime C# with no JSON, no suite entry, no
   promoted badlands art beyond 2 tiles — either remap hot/dry climate to
   meadow (like inland beach) or commission the badlands set.
6. `beach.json` decor uses feature id `rock_outcrop` where a prop id belongs.
7. `transitions.json` is an autotile manifest for a `tiles_5` transition
   family that was never generated (batch script exists, no output in
   PixelArt). Either run `tiles_5_transitions.bat` or rely on Part 2's
   procedural blending; don't ship a manifest pointing at nothing.
8. Gate the showcase seed (240611) behind a debug flag so players can't type
   into the demo world.
9. BiomeSketch sync manifests reference `C:/tmp/LitIsoWorldGen` — move
   pipeline state inside the repo (Temp/ is gitignored but at least on-disk
   with the project).

---

## Part 2 — Proposed generation rules

### A. Biome layout (macro)

- **A1. Keep the continent skeleton.** Elevation field driving
  ocean → shallow → beach → land tiers already works and streams cleanly.
- **A2. Macro biome cells (Minecraft-style).** Quantize the climate fields to
  jittered Voronoi cells at chunk scale (~48–96 cells across). Each macro
  cell rolls ONE biome from the climate table. Gives coherent, named-feeling
  regions instead of noise blobs; still a pure function of position+seed.
- **A3. Climate table, not nearest-centroid.** Replace `SelectBiome`'s
  nearest-centroid with a Whittaker-style lookup: temperature x moisture
  rectangles per biome (the biome JSONs already declare these ranges — use
  them). Deterministic, designable boundaries; no accidental ties.
- **A4. Elevation-gated biomes.** Mountain is promoted by elevation band
  (noise_params already says `mountainAbove`), never by climate. Apply a
  lapse rate (`temp -= k * height`) so high meadow naturally becomes snow.
- **A5. Adjacency legality.** Biome groups: cold {snow}, temperate {meadow,
  forest}, warm {desert/badlands}. A cold cell may not border a warm cell;
  when the macro grid would do it, the border cell demotes to temperate.
  (This is the Minecraft rule that prevents snow-against-sand jank.)
- **A6. Rivers** stay as warped band noise but gain a moisture bonus within
  ~6 cells (reed/flower accents thrive near water) — cheap: river distance
  is already computed.

### B. Transitions & blending (kills the "random palette" look)

- **B1. Border distance field.** For a cell, estimate distance-to-other-biome
  by probing the macro-cell lookup at 4–8 offsets (cheap, chunk-local).
  Define a blend band of 4–7 cells (forest.json's `transitionTo.meadow`
  already declares exactly this — honor it).
- **B2. Weighted base-pool crossfade.** Inside the band, pick base tiles from
  BOTH biomes' pools with distance-weighted probability (90/10 → 50/50 →
  10/90). This alone replaces the hard palette switch with a dither fade.
- **B3. Accent ramp.** `accentRate` ramps toward the border: forest accents
  (leaf litter) fade out, meadow accents (flowers/tufts) fade in. Owner rule
  already written in forest.json's `_` note — implement it.
- **B4. Feature density ramp.** Tree `groveCentersPer100` scales down across
  the band (grove → sporadic lone trees → none). No tree wall at the border.
- **B5. Wang/blob transition tiles where art exists.** For pairs with
  authored blend art (BiomeSketch gradient_blends, future tiles_5 family):
  4-bit edge mask (N/E/S/W neighbour-is-other-biome) indexes a 16-tile blob
  set. Procedural dither (B2) is the fallback for pairs without art.
- **B6. Beach stays water-locked** (current rule is correct). Add a 2–3 cell
  wet-sand → dry-sand → speckled grass-sand mix using the beach family's
  16 tiles once promoted.
- **B7. Height never blends.** Cliff steps stay crisp (1-step rule retained);
  only the surface palette blends. Cliff-face tile may tint per biome.

### C. Prop & decoration placement

- **C1. One placement contract per prop** (single JSON, fed by the
  rules.html export): `surfaceTags` (grass/sand/stone/snow/interior),
  `biomes`, `minSpacing`, `heightRange`, `nearWater`/`avoidWater`,
  `clusterable`, `footprint`. The sampler consults only this contract —
  today's per-biome hardcoded NS() lists in FoundationContent migrate here.
- **C2. Blue-noise scatter for singles.** Replace raw per-cell hash with a
  jittered-grid pick (one candidate per k x k cell, hash-selected offset):
  cheap Poisson approximation, no accidental clumps, still deterministic.
- **C3. Clusters keep cluster noise** (groves/outcrops) but with radial
  falloff density so edges feather instead of stopping.
- **C4. Keep the good existing rules** as contract fields: no trees on
  cliff lips (swap to bush), bare crag above tier 3, canopy-as-terrain in
  forest mass, no dense decor near paths/doors/spawn lanes.
- **C5. Camp tiering by distance band.** tent_common near spawn ring →
  tent_mythical in far bands (assets exist, 5 tents currently unpromoted).
  Campsites keep min-spacing 28 and never straddle a blend band.
- **C6. Ore ladder by elevation + biome.** copper: forest/mountain tier 3–4;
  iron 4–5; silver/gold 5–6; manacrystal/starmetal 6–7 + dungeon. Matches
  the prop catalog ore set (all 6 generated, promoted).

### D. Settlements & towns

- **D1. Deterministic site selection.** Hash macro cells into candidate sites
  (e.g. 1 in N macro cells rolls a settlement check). Score = flatness
  (tier variance in a 12-cell probe) + water within 20 cells + biome
  weight (meadow > forest edge > beach) + distance band from spawn
  (settlements.json `bands` already define tiers). Site spawns only if
  score clears threshold — pure function of cell+seed, no global pass.
- **D2. Town anchor & lots.** Plaza cell at site center; building lots on a
  coarse lot grid around it (rank-gated pool from settlements.json:
  tavern/shop first band, guild/library higher bands). Validate multi-cell
  footprints flat+dry before stamping; skip lot if invalid (organic
  irregularity for free).
- **D3. Doors face the plaza/road.** Each building lot orients its entrance
  toward the plaza axis; reserve a 2-cell walk lane in front (the global
  `suppressDenseDecorNearDoorCells: 3` already exists — keep).
- **D4. Roads as cost-biased paths.** Stamp a path from plaza to the nearest
  road trunk using greedy downhill-of-cost steps (cost: water/cliff = inf,
  slope high, flat grass low) computed cell-locally toward the neighbour
  settlement direction; surface = `stone_path` core, `dirt`/`forest_mud_path`
  shoulders, autotiled. Towns also connect to the spawn road ring.
- **D5. Outskirt rings.** Core (buildings) → farms ring (farming tileset +
  scarecrow + crop plots) → camp/pasture fringe. Water-adjacent towns get a
  dock arm (dock_post + boat_rowing + planks tiles) — all assets generated,
  currently unpromoted.
- **D6. Interiors reserved.** interior/planks families + library/tavern/guild
  prop sets stage the building-interior milestone; keep them out of outdoor
  scatter pools entirely.
- **D7. Settlement exclusion zones.** No settlement within the spawn ring
  (radius from biome_suite.spawnZone), min spacing between sites enforced by
  macro-cell hashing rate, never inside a blend band or on beach cells
  (beach.json rule retained).

### E. Acceptance gates (extend the preview audit)

- E1. Region size: no biome region under ~12 cells diameter (no speckle
  biomes).
- E2. Border health: every biome pair border shows a blend band; zero hard
  base-tile adjacency of non-neighbour palettes outside cliffs.
- E3. Prop density per biome within declared min/max; zero contract
  violations (prop on illegal surface/height).
- E4. Settlement reachability: every town's plaza connects by road cells to
  the spawn ring (flood-fill in the preview tool only — offline gate, not
  runtime).
- E5. Zero unresolved ids: every tile/prop id in rules resolves to promoted
  art (the audit script already checks ids — make it fail on fallback art).

### Phasing

- **Phase 0** — cleanup list above (data hygiene; unblocks everything).
- **Phase 1** — A3/A4 climate table + elevation gating; B1–B4 procedural
  blending (no new art needed). Biggest visual win.
  **STATUS (2026-06-12): data side DONE, sampler side TODO (Codex).**
  `meadow.json`/`forest.json`/`snow.json`/`beach.json`/`mountain.json` now
  carry `climate{temperatureRange,moistureRange,priority}` (A3),
  `lapseRate{perHeightStep}` + `elevationGate{minHeight}` (A4), and
  `transitionTo.<biome>{cells,baseWeightCurve,accentRamp,featureDensityRamp}`
  for every adjacent pair (B1-B4; forest's prior `transitionTo.meadow` extended
  in place, not duplicated). desert deliberately not added — meadow's hot/dry
  rectangle covers it (see spec note). `IsoTerrainSampler.SelectBiome` /
  `SampleContinent` and `BiomeDefinition.cs` still need the rewrite — full
  pseudocode + exact field names are in `Docs/agent-comms/from-claude.md`
  (2026-06-12 entry).
- **Phase 2** — A2 macro cells + A5 adjacency; B5 Wang blends using
  BiomeSketch blend art; C1/C2 placement contract + blue-noise.
- **Phase 3** — D1–D5 settlements, roads, farms, docks, camps.
- **Phase 4** — E gates wired into the preview audit + showcase seeds.

Ownership note: sampler/runtime = Codex lane (Foundation); rules JSON, art
promotion, BiomeSketch tooling, this doc = Claude lane. Phase boundaries are
clean handoff points.
