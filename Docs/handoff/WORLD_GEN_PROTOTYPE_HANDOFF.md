# World-Gen Prototype Handoff (Claude Fable -> Codex)

Date: 2026-06-09
Status: LIVE — updated as work proceeds. If Claude's session cuts off, this doc
plus `Docs/handoff/world-gen-prototype/` is everything needed to continue.

## Mission

Standalone (NOT wired into Unity) recreation + world-generation prototype using
the user-supplied 115-tile isometric pack. Three goals, in order:

1. Recreate the user's 3 reference images exactly from pack tiles — DONE.
2. Derive the artist's tile-placement logic and document it — DONE
   (`world-gen-prototype/tile-taxonomy.md`).
3. Upgrade world generation to follow that logic: coherent biomes (zero
   cross-biome bleed), oceans/landmass via elevation noise, rivers, height
   tiers, real-world-plausible decoration — WORKING, improvements in flight.

## Hard constraints (do not violate)

- The tile pack's license is NOT yet documented. Review/prototype use only.
  Do NOT import pack pixels into Unity runtime content, do NOT train on them,
  until the user confirms licensing (see CLAUDE_FABLE_ASSET_PIPELINE_HANDOFF.md).
- This is a preview pipeline: PowerShell + GDI renders to PNG. No Unity changes.
- Placeholders in the Unity game stay untouched (user is training a LoRA
  separately for production art).
- Final boards: black background, no labels/UI chrome, pack tiles only.

## File map

Durable copies (this repo): `Docs/handoff/world-gen-prototype/`
- `tile-taxonomy.md` — THE CONTRACT: all 115 tiles classified by role +
  6 placement principles + adjacency/layering rules. Read this first.
- `build-world-preview.ps1` — seeded procedural world generator (the main
  deliverable). Run: `powershell -File build-world-preview.ps1 -WorldSeed 1207`
- `build-reference-recreations.ps1` — the 3 reference-image recreations.
- `world-preview.png` (seed 1207), `world-preview-seed4242.png` (archipelago),
  `reference-recreations-sheet.png` — current renders.

Working copies (volatile, may be wiped): `%TEMP%\litiso-archive-review\`
- Same files plus: `family-zoom/*.png` (zoomed labeled strips of every tile
  family — regenerate trivially by scaling tiles 4x with filename labels),
  `crop-*.png` (1:1 QA crops), `tileset_contact_sheet.png`.

Tile pack (115 x 32x32 PNGs, `tile_000.png`..`tile_114.png`):
- In-repo copy: `Docs/handoff/tile-pack-for-codex/isometric tileset/separated images/`
- Temp copy (scripts point here): `%TEMP%\litiso-archive-review\tileset\isometric tileset\separated images\`
- If the temp copy is gone, repoint `$tileRoot` in both scripts to the in-repo copy.

## Key technical facts (hard-won, trust these)

- All 115 tiles share a uniform 32x32 canvas; props are pre-positioned. Drawing
  any tile at the same cell rect aligns correctly. Iso projection: stepX=16,
  stepY=8 (native px), painter order by (x+y).
- One block step = 8 native px raise. Raised tiles MUST draw a dirt underlay
  (tile_003) at level 0 or you get black voids at edges.
- Z-AWARE PAINTER (critical): a top raised by one step occupies the screen band
  of diagonal (x+y+1) and must be drawn in that group, AFTER its flat tiles —
  otherwise southern lowland tiles overdraw the cliff skirt and cliffs read as
  thin cracks. Underlays stay in their natural (x+y) group.
- PowerShell gotchas: hashtable keys are case-INsensitive ('k' vs 'K' collide);
  [long] hash math overflows to double unless you mod by 2^31-1 between steps;
  PS 5.1 has no && operator.
- Tile-family corrections vs the old `build-biome-previews.ps1` (whose groups
  were wrong and caused biome bleed): 095–103 are DARK water variants not
  "light water"; 104–114 are shallow/light water not "snow"; 086–091/095–099
  are wave-swell accent blocks; 027–029 canopy (029 leafy is the good
  dominant; 027/028 striped — accents only); 037 is the open-meadow field;
  040 is the forest-floor/lush field; 022–024 bright grass = plateau tops;
  017/018 badlands floor; 014–016 strata mesa tops; 019/020 sprout transition
  (meadow<->badlands only); 025/026 root-cliff (forest edge only); foam-footed
  stones 066/069–081 go ONLY in water hugging a shoreline; sparkles 082–085
  deep water only, drawn last.

## Generator architecture (build-world-preview.ps1)

1. Lattice value-noise fields (fast, no per-sample function calls): elevation
   (P=14 + P=6 octaves) with radial falloff + hard border push-down (ocean
   guaranteed at edges), moisture (P=18 + P=8), plus prop-cluster noises
   (flowerN P=4, bushN P=3.5, rockN P=4.5).
2. Classification by elevation: deep < 0.30 < shallow < 0.40 < beach < 0.44 <
   land; land splits by moisture: badlands < 0.30 < meadow < 0.62 < forest.
   Plateau lvl1 where e > 0.72. Per-cell jitter (±0.01) on the water
   thresholds breaks straight band edges.
3. Cleanup passes: beach spits with no land in 8-neighbourhood get drowned
   (2 iterations); plateau cells with <2 raised 4-neighbours get demoted.
4. Rivers: 2 sources on high ground (>=16 cells apart), greedy descent with
   0.035 meander jitter, carved as 'river' (renders as shallow water);
   raised cells beside a river drop to lvl0; grass meets stream directly
   (pond-style banks, NOT dirt banks — dirt banks read as canals).
5. Canopy: interior forest cells (moist > 0.68 or bushN > 0.62), never
   adjacent to water/beach.
6. Decision pass fills per-cell terrain/underlay/prop arrays following the
   taxonomy whitelists; paint pass renders z-aware; sparkles drawn last.

## Work log

- DONE: 3 reference recreations match the reference images (5 iterations).
- DONE: taxonomy doc covering all 115 tiles.
- DONE: world generator v1 — ocean/shallow/beach/biomes/plateau/rivers/
  decorations, z-aware cliffs, cleanup passes; seeds 1207 + 4242 verified.
- DONE (v2, all verified on seeds 1207/4242/7777):
  1. Multi-step cliffs: lvl 0/1/2 (lvl2 at e > 0.84), stacked dirt underlays,
     painter generalized to z 0..2 (top drawn in diagonal group x+y+z), erode
     demotes per level top-down; rocky crowns (061/062/065/067/068) on lvl2.
  2. River fallback for flat worlds: when no raised cells exist, sources come
     from the 2 highest land cells; a river stuck in a basin carves a small
     pond (3x3 land neighbourhood -> shallow water) — seed 1207 shows one,
     naturally ringed by beach dirt from the elevation low.
  3. Meadow texture: lush deep-green patches (tile 040, lushN P=5 > 0.74)
     break the 037 dash-grid; biome-pure since 040 is grass-family.
- Seed character: 1207 = continent w/ highlands+rivers+pond; 4242 = flat
  archipelago (fallback rivers are short — highest land sits near coast, fine);
  7777 = big landmass with an inland sea. All obey the taxonomy rules.

## UNITY INTEGRATION — LANDED 2026-06-09 (Claude, in Codex's IsoCoreFoundation lane)

The prototype generation model is now ported into the live Foundation terrain
system and wired to the standard play/test world. This is a real change in
`Assets/Scripts/IsoCoreFoundation/**` (Codex's lane) made with the user's
explicit go-ahead. Rendered with the EXISTING Foundation block art only — NO
tile-pack pixels were imported (license still unconfirmed; placeholders intact).

Files changed:
- `World/IsoTerrainSampler.cs` — new `SampleContinent(wx,wy)` method: a pure
  per-cell function (NO global arrays/cleanup passes, so it streams chunk-by-chunk
  like the rest of the world — this is the key adaptation from the prototype's
  whole-world-array approach). Implements: elevation continent field + spawn
  land bias; ocean(deep/shallow)->beach->land depth chain; multi-step cliff
  heights from elevation tiers (renderer already stacks dirt bodies via
  EnsureStack); warped-band rivers with sand banks; climate-driven biome purity
  via existing SelectBiome; clustered decoration via existing
  PickClusteredDecoration. Added `PerlinF` (float-coord sampling for warped
  rivers), `SurfaceVariant`, `TryPlaceBiomeNode` helpers and a `_beachIndex`.
  The non-flat path routes to it when `cfg.continentWorld` is true.
- `Core/FoundationConfig.cs` — new "Continent world" + "Continent rivers"
  config sections (frequencies, depth/beach/tier thresholds, spawn land bias,
  river width/warp). `continentWorld` defaults true; `flatWorld` still defaults
  true so nothing changes until a launch opts in.
- `Core/FoundationBootstrap.cs` — `ApplyLaunchOptions` sets `config.flatWorld =
  false` for standard launches, so the play/test world now uses the continent
  generator. The CreationInstance showroom's `ApplyConfig` still forces
  `flatWorld = true`, so the review grid is unaffected.

Verification done:
- `dotnet build IsoCore.Foundation.csproj --no-incremental`: 0 errors, 0 warnings.
- Headless logic mirror (`%TEMP%/litiso-archive-review/verify-continent-logic.ps1`)
  over seeds 1337/4242/7777: spawn apron is always land (player never spawns in
  ocean), ocean+rivers+land all present, height never exceeds the ceiling,
  biome regions coherent. NOTE this mirror uses a sine approximation of Unity's
  Perlin, so it proves STRUCTURE (threshold chain, safe-spawn, clamp), not exact
  ratios.

PENDING (could not run — user's Unity editor holds the project lock, so no batch
instance can start):
- In-editor `FoundationValidator` + `FoundationIntegratedSliceValidator` run.
  No regression is expected: spawn clearing still returns flat walkable meadow,
  FindBuildableCell([-16,16]) and ValidateHarvest([-48,48]) both still find
  cells/nodes. But this must be confirmed in-editor before merge.
- Visual play pass: open `IsoCoreFoundation.unity`, Play, walk out of spawn —
  expect coast/beach/ocean, biome regions, stepped cliffs, and rivers. THEN tune
  the thresholds in FoundationConfig: with Unity's real Perlin (fuller dynamic
  range than the test mirror) expect MORE tier-3/4 cliffs and possibly a
  different land/ocean ratio than the headless percentages — adjust
  continentDeepLevel/ShoreLevel/BeachLevel and the tier levels to taste.

Tuning knobs cheat-sheet (FoundationConfig "Continent world"):
- More/less ocean: continentDeepLevel + continentShoreLevel (higher = more water).
- Bigger continents: lower continentFrequency. Wider beaches: raise continentBeachLevel.
- Taller/steeper cliffs: lower continentTier2/3/4Level (and raise maxHeight, <=7).
- Rivers wider/longer: raise riverHalfWidth / lower riverFrequency. Meander more:
  raise riverWarpAmplitude. Safe-spawn radius: continentSpawnLandRadius/Bias.

## Backlog / next steps (in priority order)

1. Optional polish: river mouths widen into deltas; beach-only coves;
   badlands mesas with double strata stacks (014–016 x2); lush-patch edge
   smoothing (lone 040 singles at patch borders read slightly speckly);
   widen rivers to 2 cells for long stretches.
2. QA loop for any change: render seeds (1207, 4242, 7777), crop 1:1 regions,
   check against the taxonomy checklist at the bottom of this doc.
3. LATER (after license + art approval ONLY): map this generation model onto
   the Unity Foundation — the taxonomy's biome whitelists translate to
   FoundationContent biome tile sets; the elevation/moisture/cleanup/river
   pipeline maps to IsoTerrainSampler-style deterministic sampling. Do not
   start this without the user's explicit go-ahead.

## How to verify a render

`powershell -File build-world-preview.ps1 -WorldSeed <n>` writes
`world-preview.png` next to the tile root's parent. Then crop 1:1 regions
(the render is ~3200px wide; eyeballing the full image misses tiling
artifacts). Checklist: ocean border all around; no biome tile outside its
whitelist; every raised tile backed by underlay (no black voids); cliffs show
full skirts (not cracks); rivers continuous from source to sea; props only on
their allowed floors; foam stones only in water next to land; sparkles only
in deep water.
