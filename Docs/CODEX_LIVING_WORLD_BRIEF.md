# CODEX_LIVING_WORLD_BRIEF.md — the one big push

> **For:** a Codex session run at full token budget, **`./plan` first**, high reasoning effort.
> **Mission in one line:** take the working PixelLab-tiled continent generator (oceans, beaches,
> meadow/forest/snow/mountain already generate live) and elevate it from "distinct ground, same-y
> props, flat light" to a believable, distinct, atmospherically-lit, populated world.
> **Pairs with:** Claude Code runs the gameplay-systems + hygiene loop (`AI_LOOP_PROMPT.md`);
> Codex owns the **world/presentation seam** below. The two almost never touch the same file.
> **How to run:** paste this brief, run `./plan` to produce the phased plan FIRST (no code),
> get it approved, then execute one phase = one PR.

---

## 0. Why this is the push

What ALREADY works (do not rebuild it): the `SampleContinent` path in `IsoTerrainSampler.cs` generates
a real continent — ocean -> shallow -> beach ring (sand variants + foam shore stones) -> land -> multi-
step cliffs -> rivers — placing the promoted PixelLab-style runtime tiles from `Assets/Resources/Tiles`.
The five live biomes (meadow, forest, snow, beach, mountain) each generate with their own tile family,
so the GROUND already reads as distinct. The job is to elevate that working generator, not replace it.

Where it falls short (refinement, from
`Docs/IsoCoreFoundation/29_Biome_System_Review.md` and `30_Biome_Improvement_Plan.md`):

1. **Biomes are homogenised at the prop layer.** Every land biome shares one hard-coded decoration
   ladder (`PickClusteredDecoration` in `IsoTerrainSampler.cs`), so props are placed identically
   everywhere — a forest floor looks like a meadow, "snow" is green taiga.
2. **The authored design is never wired up.** The sampler ignores
   `Assets/StreamingAssets/worldgen/biome_suite.json` (density, featureScale, negativeSpace,
   Poisson/occupancy) and the biomes' own `nodes[].chancePerCell`, using only hard-coded
   `_cfg.deco*` constants. **The intent already exists in data — it was just never connected.**

Plus: half the Bible's biomes aren't real (desert = dead code; Sunspool, Duskwick Marsh, Glowcap
Grotto unimplemented; mountain's mossy/scree/snowcap bands authored but not built), and the
lighting/atmosphere components exist but were never unified into a mood.

That is *perfect* for a big-context, plan-first agent: mostly **connecting and orchestrating
systems that already exist**, not inventing new ones.

---

## 1. Ownership contract — the seam (read this twice)

**Codex OWNS (the world/presentation layer):**
- `Assets/Scripts/IsoCoreFoundation/World/` — `IsoTerrainSampler`, `IsoWorld`,
  `IsoSettlementSampler`, `SpriteAmbient`, `AmbientLightController`, `AmbientParticles`,
  `FoundationWeatherVisuals`, contact/decoration shadows.
- `Assets/Scripts/IsoCoreFoundation/Biomes/BiomeDefinition.cs` and the biome block of
  `FoundationContent.cs` (~L838–1062); `FoundationConfig.cs` noise/deco defaults.
- `Assets/StreamingAssets/worldgen/*.json` (`biome_suite.json`, `noise_params.json`).
- Lighting/atmosphere: `Core/DayNightSystem.cs`, `IsoLightingController.cs`,
  `GraphicsEnhancer.cs`, `Building/CampfireGlow.cs`, sprite/lighting shaders.
- Asset **binding/promotion** of the PixelLab pools into the sampler (semantic role per biome),
  including `FoundationSpriteScale` `heightUnits` on mis-scaled props.

**Codex must NOT touch (Claude's gameplay-systems lane):**
- The 7-day loop (scoring → rank → class offers → skill points), `FoundationSaveData` save/load
  core, `FoundationCampingSystem` mechanics, guild/dungeon **gameplay** logic, progression/stats/
  quests runtime. Read these interfaces; do not edit them.

**If a change needs both sides,** define/extend an interface on the seam and leave the gameplay
side a TODO + ledger note for Claude — do not reach across.

---

## 2. Hard rules (non-negotiable)
- **Invariants stay:** Grid IsometricZAsY, cellSize (1,0.5,1), transparencySortAxis (0,1,-0.26),
  TilemapRenderer.mode = Individual, Height_0..7 = layer 10+height, world-query movement,
  maxWalkStepHeight = 1. Atmosphere work must not perturb sort order or height layering.
- **Art is LOCKED to the PixelLab set.** Bind and place existing PixelLab assets; fill gaps *to
  that style only*. Where a biome's assets are genuinely missing (marsh, grotto, snow props — see
  `28_Biome_Asset_Inventory.md`), **flag the gap as an art-track dependency; do not invent a new
  style** and do not block the phase on it.
- **Anti-collision (critical in a Unity repo):** configure atmosphere via **prefabs /
  ScriptableObjects / code**, never by hand-editing `Assets/Scenes/IsoCoreFoundation.unity`.
  Two agents editing scene YAML is the one thing that creates real merge pain.
- **Git:** `git pull --rebase`; branch `feat/world-<phase>` off main; small PRs; never commit to
  main; never force-push; binaries to LFS. Claim the **"Living World"** lane in
  `Docs/agent-comms/task-ledger.md` before starting, one row per phase.

---

## 3. Phase breakdown (each phase = one approved PR)

**Phase 0 — `./plan` (no code).** Produce the full plan: confirm the seam, list every file per
phase, name the verification for each, and flag asset-gap dependencies. Get it approved before code.

**Phase 1 — Wire the authored contract (highest leverage).** Make `IsoTerrainSampler` honour
`biome_suite.json` (density, featureScale, negativeSpace, Poisson/occupancy) and per-biome
`nodes[].chancePerCell` instead of hard-coded `_cfg.deco*`. Give each biome its **own** decoration
ladder (retire the shared `PickClusteredDecoration` path), **de-correlate the noise fields** so
groves/bushes/rocks/flowers stop piling on the same cell, and restore bare ground (negative space).
This alone makes biomes read as distinct.

**Phase 2 — Make the missing biomes real.** Set desert's temp/moisture ranges (currently dead
code); implement mountain's mossy/scree/snowcap height bands; stand up Sunspool, Duskwick Marsh and
Glowcap Grotto to whatever asset coverage exists, flagging gaps for the art track. Confirm the live
path (`continentWorld`, `flatWorld=false` via `ApplyLaunchOptions`) exercises them.

**Phase 3 — Semantic PixelLab binding + scale fix.** Select tile/prop pools by **biome semantic
role**, and fix the scale outliers (cactus 14–37px, 32px small-decor) by setting `heightUnits` via
`FoundationSpriteScale` so nothing renders broken next to full-size props.

**Phase 4 — Shaders + depth.** Sprite/lighting shaders via `SpriteAmbient`, contact shadows, and
per-biome depth/mood so the scene stops looking flat.

**Phase 5 — Dynamic lighting + day/night atmosphere.** Unify `DayNightSystem`,
`AmbientLightController`, `IsoLightingController`, `CampfireGlow`, weather visuals and particles
into one coherent atmosphere curve: dawn/day/dusk/night ambient colour **per biome**, campfire glow
at night, weather tint. This is where "a world" finally appears.

**Phase 6 — Settlement & POI placement.** Drive `IsoSettlementSampler` from the settlement
blueprints (`27_Settlement_Blueprints_And_Presets.md`) so the continent is populated and purposeful
— villages, camps, landmarks placed by biome and rank, not empty terrain.

**Phase 7 — Golden master & proof.** `WorldgenContractValidator` green; use
`FoundationBiomeMapExporter` to snapshot each biome; build a real `.exe` and capture a
before/after screenshot per biome as the evidence the world changed.

> Sequencing rule: Phase 1 before everything (it unblocks visible distinctness at lowest cost);
> 4–5 (shaders+lighting) deliver the biggest "feel" jump once 1–3 give them something worth lighting.

## 4. Definition of done
The owner launches a normal game, walks across the continent, and **immediately sees** distinct,
purposeful, atmospherically-lit biomes with populated settlements — built from the existing PixelLab
assets, on a green build, with no change to the gameplay-systems lane and no scene-YAML merge debt.

## 5. Recommended Codex settings
- **`./plan` first, always** — this spans four subsystems; plan the whole world before touching it.
- **High reasoning effort + full context window** — hold the sampler, `biome_suite.json`, the asset
  inventory (`28`) and biome docs (`18`, `29`, `30`) in head at once.
- **Long-horizon / agentic, one PR per phase**, human-reviewed; never auto-merge to main.
- **Read the whole subsystem before editing** any single file; prefer extending the authored data
  contract over adding new hard-coded constants.
