# LIT-ISO Asset Pipeline Handoff For Claude Fable

Date: 2026-06-09

## Purpose

This handoff covers the current local-first asset pipeline for LIT-ISO. The goal is to build a Sprixen-class workflow without depending on OpenAI, Gemini, Scenario, or any cloud API for the production path.

The pipeline is now centered on:

1. Style-lock reference intake.
2. Local deterministic cleanup and QA.
3. Local ComfyUI generation for characters, tiles, props, items, mobs, and VFX.
4. Dataset capture.
5. Separate LoRA training for tile geometry and critter/character style.
6. Unity import only after human art approval.

## Hard Rules

- Do not import generated review art into Unity until it passes human art approval.
- Do not treat structural QA as art approval.
- Do not train from supplied reference packs until licensing/training rights are confirmed.
- Keep the style-lock sources in review-only space.
- Preserve the existing local pipeline contract instead of replacing it with a second generator.

## Current State

### Style lock sources

The supplied isometric packs were extracted into review-only space:

- `Assets/Generated/_Review/style_lock_sources`

What was analyzed:

- 226 PNGs total.
- Tileset pack: 116 PNGs, mostly `32x32`.
- Critter pack: 110 PNGs, mostly `46x32` frames plus sheets/strips.
- Critter directions are diagonal isometric: `NE`, `NW`, `SE`, `SW`.
- Critter actions observed: `idle`, `run`, `walk`, `burrow`, `unburrow`, `tunnel`.

Style profile:

- `Tools/AssetForge/style_profiles/litiso_iso_reference_v1.json`

Important constraint:

- No license/readme was found in the extracted packs. Treat them as style references only until the license is documented.

### Black mage style lock

The supplied `BlackMage1.png` has been normalized into a review-only style lock pack:

- `Assets/Generated/_Review/black_mage_iso_style_lock_v1/black_mage_normalized_128.png`
- `Assets/Generated/_Review/black_mage_iso_style_lock_v1/black_mage_iso_review_manifest.json`
- `Assets/Generated/_Review/black_mage_iso_style_lock_v1/black_mage_iso_review_sheet.png`

This pack is not final art. It is a prompt/style contract for true isometric generation.

### Diagonal pose controls

I added a diagonal OpenPose control library for `NE`, `NW`, `SE`, and `SW`:

- `Assets/Generated/_Review/_PoseControls/litiso_openpose_diagonal_v1/idle_manifest.json`
- `Assets/Generated/_Review/_PoseControls/litiso_openpose_diagonal_v1/idle_4d_contact.png`

This is the first usable control layer for true diagonal mage generations.

### Black mage request queue

Black mage generation requests are queued in temp staging:

- `Temp/AssetForge/black_mage_requests/black_mage_iso_idle_ne_v6/generation_request.json`
- `Temp/AssetForge/black_mage_requests/black_mage_iso_idle_nw_v6/generation_request.json`
- `Temp/AssetForge/black_mage_requests/black_mage_iso_idle_se_v6/generation_request.json`
- `Temp/AssetForge/black_mage_requests/black_mage_iso_idle_sw_v6/generation_request.json`

The current v6 requests use `batch_count = 4`, cleaned character-only prompts, and dry-run cleanly through the existing Comfy worker contract.

Candidate review tooling is now staged:

- `Tools/AssetForge/build_black_mage_candidate_review_sheet.py`

Validation output:

- `Assets/Generated/_Review/black_mage_iso_renders_v1_validation/_v1_validation_candidate_review_sheet.png`
- `Assets/Generated/_Review/black_mage_iso_renders_v1_validation/_v1_validation_candidate_manifest.json`

Use this same tool after the v6 jobs render. It builds a direction-by-row candidate sheet, includes the style reference, and records alpha/size/QA metadata for manual review.

Do not use v1-v5 as art approval candidates. Current render evidence:

- `Assets/Generated/_Review/black_mage_iso_renders_v1/_directions_review_sheet.png`: NW is closest, but NE/SE/SW contain clutter or invented rings/backdrops.
- `Assets/Generated/_Review/black_mage_iso_renders_v5/_v5_review_sheet.png`: only NW v1 landed.
- v6 is staged but not rendered yet because tile LoRA training is using the GPU.

### Dataset planner

A trainer-compatible dry-run dataset plan now exists:

- `Temp/AssetForge/iso_reference_lora_dataset_plan.json`

It currently plans:

- 211 records total.
- Categories: `tile`, `critter`, `reference`.
- External target path: `C:\Projects\Pixel Pipeline\datasets\lit_iso\style_lock\iso_reference_v1`

This is still dry-run only. External writes should not happen until license/training rights are confirmed.

### Training launchers

Separate dry-run launchers now exist for future LoRA work:

- `Tools/LoRA/start_iso_reference_tile_style_training.ps1`
- `Tools/LoRA/start_iso_reference_critter_style_training.ps1`
- `Tools/LoRA/evaluate_iso_reference_tile_style_lora.ps1`
- `Tools/LoRA/build_lora_eval_contact_sheet.py`
- `Tools/AssetForge/run_post_tile_training_review_pass.ps1`

The shared resumable launcher was also updated so `-DryRun` stays inside `Temp/LoRA`.

## What Is Working Now

- The style-lock packs are analyzed and summarized.
- The black mage reference has a usable normalized style image.
- Diagonal pose controls exist for the four true isometric directions.
- The black mage request contract is queued and dry-runs successfully through the current Comfy worker path.
- The black mage candidate review-sheet builder validates against the existing v1 render folder and is ready for the v6 batch.
- The style-lock dataset planner produces a concrete record count and captions.
- Training can now be split into separate tile and critter passes.
- The tile LoRA post-training evaluator wrapper dry-runs and writes `Temp/LoRA/litiso_iso_reference_tile_style_v1.post_training_eval_plan.json`.
- The generic LoRA eval contact-sheet builder validates against existing PNG folders and is wired into the tile eval wrapper for live runs.
- The post-training review-pass sequencer dry-runs and writes `Temp/AssetForge/post_tile_training_review_pass_v6.json`. It will evaluate the tile LoRA, render v6 mage requests, and build the v6 candidate sheet once training is complete.

## What Is Not Done Yet

- No real black mage images have been rendered yet.
- No dataset has been written to `C:\Projects\Pixel Pipeline\datasets\lit_iso\style_lock\iso_reference_v1`.
- No tile or critter LoRA has been trained yet.
- No generated output has been approved for Unity import.

## Required Next Steps

### 1. Track current tile LoRA training

Tile LoRA training was started after license confirmation:

- Output name: `litiso_iso_reference_tile_style_v1`
- Status file: `C:\Projects\LoRA-Training\control\litiso_iso_reference_tile_style_v1\status.json`
- Latest verified progress: about `800/1000` steps on 2026-06-10.
- Latest verified checkpoint: `C:\Projects\LoRA-Training\outputs\litiso_iso_reference_tile_style_v1\litiso_iso_reference_tile_style_v1_step00800.safetensors`

Do not start additional GPU-heavy renders until this finishes unless the user explicitly prioritizes the mage renders over training.

After completion, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\run_post_tile_training_review_pass.ps1
```

Dry-run validation already selected the latest step-800 checkpoint, saw live progress at about `884/1000`, and correctly held GPU work until training completes.

### 2. Apply the style-lock dataset

Licensing/training rights were confirmed by the user during the follow-up session, and the dataset was materialized outside the repo:

- `C:\Projects\Pixel Pipeline\datasets\lit_iso\style_lock\iso_reference_v1`

Keep tile and critter handling separate if the dataset quality diverges.

### 3. Train the first two LoRAs

Use the split launchers:

- Tile geometry/style LoRA
- Critter/character style LoRA

Do not mix them unless the combined output is clearly better in review.

### 4. Render the black mage directions

After tile training frees the GPU, run the v6 queued requests through ComfyUI for real renders and compare:

- `NE`
- `NW`
- `SE`
- `SW`

The target is true isometric direction consistency, not just a rotated front view.

### 5. Expand from idle into animation

Once the diagonal stills are stable, extend the same control/style pattern to:

- `walk`
- `run`
- `attack`
- `jump`

### 6. Produce tile families from the same style lock

Use the 32x32 tile reference pack to generate or derive:

- grass
- dirt
- transition tiles
- water edges
- stone/rock variants
- forest floor

Tiles should be uniform and empty of unintended props on top of the tile surface.

### 7. Add VFX after the base art is stable

Only after the sprite/tile pipeline is stable, add a VFX generation path for:

- spell bursts
- hit sparks
- healing
- impact flashes
- item pickup

## External Repo Review

I reviewed these two GitHub repos for pipeline relevance:

- [DavidTParks/pixelfy](https://github.com/DavidTParks/pixelfy)
- [KennethJAllen/proper-pixel-art](https://github.com/KennethJAllen/proper-pixel-art)

### Pixelfy

What it is:

- A shut-down open-source AI pixel art SaaS front-end.
- Built with Next.js and a SaaS stack.
- The README says it was integrated directly with Scenario.
- License in the README is AGPLv3.

What is useful:

- Product shell ideas.
- Workflow presentation ideas.
- SaaS-like organization of generate/review/billing flows.

What is not useful for the current pipeline:

- It is not the core generator.
- It is tied to Scenario as a backend concept.
- It is shut down, so treat it as reference code only unless we deliberately want to inherit AGPL constraints.

### Proper Pixel Art

What it is:

- A local tool that converts noisy, high-resolution pixel-art-like images into true-resolution pixel art.
- It exposes CLI, Python API, and a web UI.
- The algorithm uses edge detection, line finding, mesh recovery, and color quantization.

What is useful:

- Very relevant to our deterministic cleanup stage.
- Good reference for recovering a clean pixel grid from AI outputs.
- The parameters map well to our current needs:
  - pixel width estimation
  - initial upscale
  - transparent background recovery
  - quantization

What to take from it:

- Use it as a reference for the cleanup/snap stage.
- If we adopt code, it belongs in the local post-process layer, not in the generation layer.
- It is a much better fit for this pipeline than Pixelfy.

Current integration state:

- `Tools/AssetForge/proper_pixel_art_cleanup.py` wraps the `proper-pixel-art` Python API without vendoring third-party code into the repo.
- `Tools/AssetForge/run_proper_pixel_art_cleanup.ps1` is the PowerShell entry point.
- Comfy and Sprixen request processors now run the sidecar when a generation request includes `proper_pixel_art`, `proper-pixel-art`, or `proper_pixel_art_cleanup` in `post_process`.
- Sidecar output is written under each review folder as `_ProperPixelArt`, with `proper_pixel_art_report.json` and `proper_pixel_art_contact_sheet.png`.
- `review_report.json`, `generation_manifest.json`, and request status JSONs include the sidecar report/contact-sheet paths when present.
- Black mage and tile-family queue scripts now include `proper_pixel_art` in their generated `post_process` lists.
- The default `pixel_width` is intentionally conservative at `1` because most Asset Forge outputs are already normalized before this sidecar runs. Increase it only for large fake-pixel images that need grid recovery.
- If `proper-pixel-art` is not installed, the sidecar writes a `missing_dependency` report and the main pipeline can continue.
- Current machine state: `proper-pixel-art==1.5.1` is installed in `C:\Projects\ComfyUI\.venv`.

Install command when ready:

```powershell
C:\Projects\ComfyUI\.venv\Scripts\python.exe -m pip install proper-pixel-art
```

## Recommended Priority Order For Claude Fable

1. Get the first real black mage diagonal renders out of ComfyUI and inspect direction fidelity.
2. Apply the style-lock dataset outside the repo after license confirmation.
3. Train the tile and critter LoRAs separately.
4. Use the tile LoRA to push the 32x32 terrain family toward the supplied style lock.
5. Use the cleanup stage to recover true pixel grids from AI output.
6. Only then expand into animations, mobs, props, items, and VFX.
7. Do not move anything into Unity until art approval is explicit.

## Relevant Paths

- `Assets/Generated/_Review/style_lock_sources`
- `Assets/Generated/_Review/black_mage_iso_style_lock_v1`
- `Assets/Generated/_Review/_PoseControls/litiso_openpose_diagonal_v1`
- `Temp/AssetForge/black_mage_requests`
- `Temp/AssetForge/iso_reference_lora_dataset_plan.json`
- `Tools/AssetForge/build_litiso_openpose_direction_library.py`
- `Tools/AssetForge/build_iso_reference_lora_dataset.py`
- `Tools/AssetForge/queue_black_mage_iso_requests.py`
- `Tools/AssetForge/build_black_mage_candidate_review_sheet.py`
- `Tools/LoRA/start_iso_reference_tile_style_training.ps1`
- `Tools/LoRA/start_iso_reference_critter_style_training.ps1`
- `Tools/LoRA/evaluate_iso_reference_tile_style_lora.ps1`
- `Tools/LoRA/build_lora_eval_contact_sheet.py`
- `Tools/AssetForge/run_post_tile_training_review_pass.ps1`

## Bottom Line

The pipeline is now at the point where the next real gains come from:

- actual ComfyUI renders for the black mage diagonal views,
- a licensed dataset apply step,
- and two focused LoRA training runs.

Until those happen, the work should stay in review and temp staging.
