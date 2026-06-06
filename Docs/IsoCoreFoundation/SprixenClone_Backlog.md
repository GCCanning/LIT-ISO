# Sprixen-Style Asset Forge Backlog

This backlog defines a local, clean-room Asset Forge for LIT-ISO. "Sprixen-style"
means the workflow shape only: generate, compare, clean up, review, approve,
capture datasets, train/evaluate LoRAs, and export game-ready assets. Do not copy
Sprixen UI, code, branding, prompts, assets, or data.

The forge output must remain original LIT-ISO content and must follow the project
clean-room rules in `10_CleanRoom_Clone_Backlog.md` and the current generated-asset
quality bar in `11_AssetForge_Sprixen_Quality_Standard.md`.

## Product Goals

- Run fully local by default: local web UI, local queue, local filesystem outputs,
  and local model/LoRA configuration.
- Support fast asset authoring for terrain tiles, items, props, crops, mobs,
  effects, UI icons, and audio.
- Keep generation exploratory but promotion strict: nothing enters Unity import
  locations without review, approval, metadata, and manifest entries.
- Capture every approved/rejected example as training and evaluation data.
- Provide a LoRA lab with resumable jobs, checkpoints, comparison sheets, and
  rollback-friendly output directories.
- Export Unity-ready PNG/WAV/metadata packs without touching canonical art,
  resources, scenes, or scripts until a human/owner approves integration.

## Milestone 0 - Local Forge Shell

Goal: a small local dashboard over the existing generated-pack workflow.

- Create a local dashboard that reads generated pack manifests, review reports,
  contact sheets, and gallery HTML.
- Show pack status: generated, normalized, reviewed, decisioned, approved,
  exported, imported.
- Add mode presets: terrain, item icon, prop, crop, mob, effect, UI, audio.
- Add queue controls: enqueue, pause, resume, cancel, retry failed jobs.
- Store jobs in durable JSON or SQLite so interrupted sessions can resume.
- Keep all outputs under explicit generated/review roots with pack names, timestamps,
  model identifiers, prompt versions, seeds, and tool versions.

Acceptance:
- Dashboard can inspect an existing review pack without regenerating assets.
- A stopped process can restart and recover queued/running/completed job state.
- No files are written to `Assets/Art/**`, `Assets/Resources/**`, scenes, or scripts.

## Milestone 1 - Generation Pipeline

Goal: reproducible local generation for every asset category.

- Define category schemas: output size, PPU, pivot, transparent/opaque rules,
  allowed animation frames, naming, tags, and Unity import metadata.
- Add prompt template library with shared style suffixes and category-specific
  negatives.
- Support seed sweeps, variation batches, prompt A/B tests, model A/B tests,
  and LoRA weight sweeps.
- Generate terrain and props separately so decoration never bakes into terrain.
- Produce manifests per batch with source prompt, seed, model, LoRA, sampler,
  dimensions, intended PPU, category, biome, item id, and license/source notes.
- Add deterministic contact sheet generation for fast visual comparison.

Acceptance:
- Terrain, prop, item, effect, and audio dry runs produce complete manifests.
- Re-running a saved job reproduces the same filenames and generation settings.

## Milestone 2 - Cleanup And QA

Goal: turn raw outputs into consistent, reviewable game assets.

- Add image cleanup: trim/center, alpha cleanup, nearest-neighbor scaling,
  palette drift checks, edge transparency checks, and optional outline cleanup.
- Add category validators:
  - Terrain: dimensions, transparency, no baked props, tile readability.
  - Items: 16x16 readability, transparent background, no cropped silhouette.
  - Props: bottom-center anchor, scale bounds, no base plates.
  - Mobs/crops: frame consistency, silhouette stability, pivot consistency.
  - Effects: readable loop frames, no excessive haze, transparent background.
  - UI: nine-slice/sprite size constraints, contrast, pixel consistency.
  - Audio: duration, peak/true-peak, silence trim, loop-point metadata.
- Generate review reports with pass/warn/fail results and thumbnails.
- Add manual review fields: approve, reject, needs edit, reason tags, notes.
- Add safe approval copy step that only promotes selected assets.

Acceptance:
- Every generated asset has a machine QA status before manual approval.
- Review decisions are durable and can be reloaded by dashboard and scripts.

## Milestone 3 - Dataset Capture

Goal: make every review decision useful for future model and LoRA improvement.

- Capture approved images/audio plus full generation metadata.
- Capture rejected examples with reason tags: anatomy, blur, baked shadow, style
  drift, wrong perspective, low readability, bad alpha, wrong category, duplicate.
- Capture edited outputs as paired before/after examples.
- Produce dataset exports by category, biome, quality tier, prompt family, and
  asset role.
- Add dataset privacy/source metadata: original generated, hand-authored edit,
  licensed reference metadata only, or excluded.
- Add dedupe hashes and near-duplicate reports.

Acceptance:
- Approved and rejected datasets can be exported without manual file sorting.
- Each dataset item has enough metadata to reconstruct how it was produced.

## Milestone 4 - LoRA Lab With Pause/Resume

Goal: local LoRA experiments that are resumable, comparable, and safe to abandon.

- Add experiment records: dataset snapshot, base model, training config, trigger
  tokens, captions, validation prompts, output directory, and owner notes.
- Support pause/resume for training jobs with checkpoint discovery.
- Add checkpoint cadence, checkpoint pruning policy, and explicit "keep" marks.
- Generate validation grids at fixed prompts/seeds for every checkpoint.
- Compare LoRA runs by category score, rejection reasons, style consistency, and
  human preference.
- Track winning LoRAs and compatible generation presets per asset category.
- Add rollback: disable a LoRA preset without deleting experiment artifacts.

Acceptance:
- A LoRA training job can be paused, restarted, and continue from checkpoint.
- Validation grids are comparable across runs because prompts/seeds are fixed.
- The dashboard can mark a LoRA/preset as approved, experimental, or retired.

## Milestone 5 - Unity Export And Import Handoff

Goal: produce Unity-ready packs without crossing ownership lanes.

- Export approved packs with:
  - PNG/WAV files.
  - Unity `.meta` files or import settings manifest.
  - Category manifests.
  - Sprite pivot/PPU/filter/mipmap settings.
  - Audio import settings and loop metadata.
  - Contact sheets and review report.
  - Handoff notes for the importing owner.
- Add export profiles:
  - Foundation generated review pack.
  - Foundation approved generated pack.
  - Claude art/integration handoff pack.
  - Dataset-only export.
- Add validation that blocks export when required metadata is missing.
- Keep generated exports separate from canonical authored art unless explicitly
  approved by the owner/human.

Acceptance:
- A Unity export can be reviewed from manifests alone before import.
- Import settings are deterministic and match category quality rules.

## Milestone 6 - Asset Coverage Backlog

Goal: broaden the forge from terrain experiments into production asset coverage.

### Tiles

- Terrain tops: grass, dirt, sand, snow, clay, soil, stone path, wood floor,
  shallow water, deep water, marsh, beach.
- Terrain edges: raised block faces, cliffs, ramps, corners, water banks.
- Transitions: biome edges, path blends, shorelines, snow overlays, tilled soil.
- Rules: no props baked into tiles, readable at gameplay zoom, consistent PPU.

### Items

- Raw resources: wood, stone, fiber, clay, ore, bars, seeds, forage.
- Food and crops: cooked/uncooked states, crop products, potions/elixirs.
- Tools: axe, hoe, pickaxe, shovel, sword tiers and damaged variants.
- Quest/rare items: gems, relics, monster drops, map fragments.
- Rules: 16x16 first-read readability, inventory silhouette over detail.

### Props And Placeables

- Nature: trees, bushes, flowers, rocks, ore nodes, logs, mushrooms, reeds.
- Crafting stations: workbench, furnace, kiln, anvil, cooking pot, loom, chest.
- Home/building: walls, doors, rugs, beds, lights, fences, gates, bridges.
- Utility: signs, barrels, crates, wells, paths, garden objects.
- Rules: bottom pivot, no baked ground shadow, obstacle/occluder tags.

### Crops And Vegetation

- Crop growth stages, watered/dry state, harvest-ready state, dead/wilted state.
- Saplings and regrowth variants.
- Biome-specific plants: cactus, snowberry, marsh reeds, beach grass.
- Rules: frame-to-frame size stability and clear harvest readability.

### Mobs And Characters

- Wildlife: deer, fox, frog, fish, bee, butterfly, firefly.
- Hostiles: slime and simple fantasy creatures.
- Character equipment previews for tool tiers.
- Rules: idle/walk first, consistent directional framing, bottom-center pivot.

### Effects

- Tool hits, harvest poofs, placement sparkle, water splash, fire, smoke, dust,
  rain/snow particles, damage numbers, pickup glints.
- Rules: short loops or one-shots, transparent frames, no blur haze.

### UI

- Hotbar, inventory, crafting, world select, buttons, panels, status bars,
  item-count badges, icons for categories and tools.
- Rules: low-noise, readable at game resolution, no overdecorated panels.

### Audio

- UI: select, confirm, cancel, inventory move, craft complete.
- Tools: axe, pickaxe, hoe, shovel, sword swing, placement, pickup.
- World: footstep surfaces, water, fire, wind, rain, snow, ambience loops.
- Creatures: short readable barks and movement accents.
- Rules: original generated or authored audio only, normalized loudness, clean
  silence trimming, loop metadata where needed.

## Milestone 7 - Production Controls

Goal: make the forge dependable for repeated production use.

- Add config versioning and migration for job/dataset/experiment records.
- Add pack diffing: what changed since last approved export.
- Add provenance report for every asset selected for shipping.
- Add license/source compliance checks before export.
- Add per-category budget limits for file count, size, and generation time.
- Add failure recovery: partial batch cleanup, retry lists, orphan detection.
- Add audit log: who approved what and when.

Acceptance:
- A pack can be audited from final Unity asset back to prompt/model/dataset/job.
- Rejected or retired assets cannot be accidentally exported as approved.

## Immediate Next Slice

1. Build the dashboard reader for existing review packs.
2. Promote the current PowerShell review/approval flow into durable job records.
3. Add approve/reject UI over `review_report.json` and `review_decisions.json`.
4. Add dataset capture for approved/rejected terrain and prop examples.
5. Add one LoRA experiment record type with pause/resume checkpoint tracking.

