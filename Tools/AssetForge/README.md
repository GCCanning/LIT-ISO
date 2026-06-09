# Asset Forge Validation Tools

These scripts support the LIT-ISO Asset Forge pipeline outside the Unity editor.

## Local Configuration

Use `Tools\AssetForge\asset_forge.local.example.json` as the local contract for
machine-specific Asset Forge paths and defaults. Copy it to
`Tools\AssetForge\asset_forge.local.json` for local use when a worker needs exact
ComfyUI, LoRA, or generated-output roots.

The sample records:

- ComfyUI URL/root/output root,
- LoRA training root and ComfyUI LoRA root,
- pixel pipeline review/dataset/handoff roots,
- default pack/export names,
- required clean-room provenance fields,
- promotion and training readiness gates.

Current caveat: this is a documentation/configuration layer. Existing
worker-owned PowerShell scripts still take their current parameters and may not
read `asset_forge.local.json` directly.

## Queued ComfyUI Worker

The dashboard now has two queued Comfy actions for the Generate workspace:

- `Comfy dry run` validates the queue item, config, manifest path, and planned
  output paths without calling ComfyUI or spending GPU time.
- `Run Comfy worker` consumes the queued request, submits a local ComfyUI job,
  cleans the returned image, writes review-pack metadata, and runs strict QA.

Current supported real-generation modes are intentionally small:

```text
tile -> 32x32 diamond alpha terrain tile
prop -> 128x128 transparent bottom-anchored sprite
item -> 64x64 transparent centered icon
```

Character, mob, NPC, and animation jobs should still use the deterministic draft
worker until the 4D sheet contract and animation QA are complete.

Run an existing queued request from PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\process_generation_request_comfy.ps1 -JobName <RequestFolderName> -DryRun
```

Then remove `-DryRun` when ComfyUI is running and you want a real generation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\process_generation_request_comfy.ps1 -JobName <RequestFolderName>
```

The worker reads `Tools\AssetForge\asset_forge.local.json` when present, falling
back to `asset_forge.local.example.json`. The relevant config values live under
`comfyui.worker_defaults`: checkpoint, optional LoRA, LoRA strength, sampler,
scheduler, steps, CFG, dimensions, timeout, and supported modes.

## Style Profiles

Style profiles live under:

```text
Assets\Generated\_StyleProfiles\<ProfileId>\style_profile.json
```

The first profile is:

```text
Assets\Generated\_StyleProfiles\lit_iso_foundation_v1\style_profile.json
```

The dashboard discovers profiles through:

```text
GET /api/assetforge/style-profiles
```

When a style profile is selected in Generate mode, Asset Forge merges the profile
global prompt, mode-specific prompt hint, and user prompt into the final prompt
that the worker receives. It also saves the original user prompt and writes a
request-local snapshot:

```text
Assets\Generated\_Review\_Requests\<JobName>\Inputs\style_profile.snapshot.json
```

Comfy review packs copy that snapshot to:

```text
Assets\Generated\_Review\<JobName>\style_provenance.json
```

This is the clean-room quality-lock path for future approved references, palette
rules, and LoRA stacks. Do not add extracted commercial-game pixels to a style
profile.

## PixelArt Style Dataset Intake

Use the approved local `PixelArt` folder as a style-reference source:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\build_pixelart_style_dataset.ps1 -Replace
```

Build the old broad tile bucket only for auditing. For training, prefer the
stricter sets below:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\build_pixelart_style_dataset.ps1 -Category tile -OutDataset "C:\Projects\Pixel Pipeline\datasets\lit_iso\style_lock\pixelart_local_tiles_v1" -Replace
```

Build clean exterior terrain surfaces:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\build_pixelart_style_dataset.ps1 -Category terrain_tile -OutDataset "C:\Projects\Pixel Pipeline\datasets\lit_iso\style_lock\pixelart_local_terrain_tiles_v1" -Replace
```

Build raised/block tile geometry references:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\build_pixelart_style_dataset.ps1 -Category height_block -OutDataset "C:\Projects\Pixel Pipeline\datasets\lit_iso\style_lock\pixelart_local_height_blocks_v1" -Replace
```

Build the current SD1.5 + ControlNet tile-style LoRA dataset:

```powershell
& "C:\Projects\ComfyUI\.venv\Scripts\python.exe" Tools\AssetForge\build_pixelart_style_dataset.py --out-dataset "C:\Projects\Pixel Pipeline\datasets\lit_iso\style_lock\pixelart_local_tile_geometry_v1" --category terrain_tile --category height_block --replace
```

This strict set intentionally excludes prop-like blocks, resource cubes, walls,
ladders, bridges, logs, leaves, chests, and magic/special blocks. Those are
captured separately as `material_block` references when needed:

```powershell
& "C:\Projects\ComfyUI\.venv\Scripts\python.exe" Tools\AssetForge\build_pixelart_style_dataset.py --out-dataset "C:\Projects\Pixel Pipeline\datasets\lit_iso\style_lock\pixelart_local_material_blocks_v1" --category material_block --replace
```

Build the separate dungeon/building interior dataset:

```powershell
& "C:\Projects\ComfyUI\.venv\Scripts\python.exe" Tools\AssetForge\build_pixelart_style_dataset.py --out-dataset "C:\Projects\Pixel Pipeline\datasets\lit_iso\style_lock\pixelart_local_interiors_v1" --category interior_tile --category interior_wall --category interior_door --category interior_prop --replace
```

Build the scraped FreePixel character/mob/NPC reference dataset after license
review:

```powershell
& "C:\Projects\ComfyUI\.venv\Scripts\python.exe" Tools\AssetForge\build_freepixel_character_dataset.py --out-dataset "C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\freepixel_characters_v1" --replace
```

Outputs:

```text
C:\Projects\Pixel Pipeline\datasets\lit_iso\style_lock\pixelart_local_v1
C:\Projects\Pixel Pipeline\datasets\lit_iso\style_lock\pixelart_local_tiles_v1
C:\Projects\Pixel Pipeline\datasets\lit_iso\style_lock\pixelart_local_terrain_tiles_v1
C:\Projects\Pixel Pipeline\datasets\lit_iso\style_lock\pixelart_local_height_blocks_v1
C:\Projects\Pixel Pipeline\datasets\lit_iso\style_lock\pixelart_local_tile_geometry_v1
C:\Projects\Pixel Pipeline\datasets\lit_iso\style_lock\pixelart_local_material_blocks_v1
C:\Projects\Pixel Pipeline\datasets\lit_iso\style_lock\pixelart_local_interiors_v1
C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\freepixel_characters_v1
```

Dry-run the tile-geometry LoRA launch:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\start_pixelart_tile_style_training.ps1 -DryRun
```

Dry-run the separate interior LoRA launch:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\start_pixelart_interior_style_training.ps1 -DryRun
```

Dry-run the FreePixel character-style LoRA launch:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\start_freepixel_character_style_training.ps1 -DryRun
```

Technical split:

- LoRA should learn the PixelArt/LIT-ISO palette, material rendering, and edge style.
- ControlNet/depth/line templates should enforce tile geometry.
- Do not rely on a style LoRA alone to create strict 2:1 isometric tile shapes.
- Keep exterior terrain/height blocks separate from dungeon/building interiors.

## SD1.5 + ControlNet Tile Geometry

Build clean-room tile geometry control images:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\build_tile_controlnet_templates.ps1 -Replace
```

Output:

```text
C:\Projects\Pixel Pipeline\datasets\lit_iso\controlnet_templates\tile_geometry_v1
```

This pack contains line/depth-style controls for flat diamonds, grid tops,
raised blocks, edges, ramps, cliffs, and corners. These are geometry controls,
not final art.

Important ComfyUI note:

- `control_v11p_sd15_openpose` is for characters and poses.
- Terrain tiles use the installed SD1.5 Canny ControlNet model
  `control_v11p_sd15_canny_fp16.safetensors`.
- ComfyUI may need a restart/model refresh after installing new ControlNet
  models.
- The intended stack is: SD1.5 checkpoint + tile-style LoRA + tile geometry
  ControlNet template + deterministic pixel cleanup.

The dashboard can also import approved local references into the selected style
profile. Use the `Import reference` controls in Generate mode, or call:

```text
POST /api/assetforge/import-style-reference
```

Required fields:

```json
{
  "profile_id": "lit_iso_foundation_v1",
  "source_path": "C:/path/to/approved_reference.png",
  "asset_mode": "tile",
  "reference_id": "forest_grass_ref_001",
  "license": "project_internal_or_explicitly_licensed",
  "author": "LIT-ISO"
}
```

The imported image is copied under:

```text
Assets\Generated\_StyleProfiles\<ProfileId>\references\<AssetMode>\
```

and receives a sibling `.meta.json` with SHA-256, license/source metadata,
allowed-use policy, and clean-room review notes.

## Sprixen-Style Dashboard Slice

The dashboard is organized around Sprixen-like local workspaces, minus 3D and map
building:

- Generate: prompt/reference/style profile to queued worker request.
- Animate: idle/walk/run/attack/cast/hurt/death sheet request setup.
- Direction Set: 4D first (`S,E,N,W`), 8D experimental.
- Tiles: terrain-only tile prompts with prop/tree/object negatives.
- Props: decorations with bottom-center anchors and no baked ground.
- Items: transparent icon prompts and 64px QA.
- VFX: local particle sheet handoff and VFX Lab.
- Review/Dataset/LoRA/Unity: approval, capture, evaluation, and export checks.

`Player 4D full set` is the stable character-sheet preset. `Player 8D
experimental` exists for future tests but should not be treated as production
until identity and anchor QA are stable.

Outputs are written through the normal review path:

```text
Assets\Generated\_Review\<JobName>\generation_manifest.json
Assets\Generated\_Review\<JobName>\review_report.json
Assets\Generated\_Review\<JobName>\review_decisions.json
Assets\Generated\_Review\<JobName>\strict_asset_quality_report.json
Assets\Generated\_Review\<JobName>\Terrain|Decorations|Items\...
```

### Direction Set QA

Use the lightweight direction QA helper to compare generated one-frame review
jobs before packing them into a sheet:

```powershell
python Tools\AssetForge\qa_direction_set.py --output Assets\Generated\_Review\direction_set_qa.json --expected-directions S,E,N,W --job S=<SouthJobName> --job E=<EastJobName> --job N=<NorthJobName> --job W=<WestJobName>
```

The tool reads each
`Assets\Generated\_Review\<JobName>\generation_manifest.json`, measures the
first generated review image, and writes per-frame alpha bounds, coverage,
centroid, size, rough color buckets, adjacent drift checks, and outlier warnings.

Use the oracle QA helper when a generated 4D set should match an approved
direction sheet manifest such as the reference knight idle oracle:

```powershell
python Tools\AssetForge\qa_against_direction_oracle.py --project-root . --oracle-manifest Assets\Generated\_Review\litiso_reference_knight_idle_4d_sheet_manifest.json --output Assets\Generated\_Review\litiso_control_refknight_idle_oracle_qa.json --job S=litiso_control_refknight_idle_s --job E=litiso_control_refknight_idle_e --job N=litiso_control_refknight_idle_n --job W=litiso_control_refknight_idle_w
```

The tool reads each job's review `generation_manifest.json`, measures every
generated image, compares alpha bbox size, centroid, and coverage against the
matching oracle frame, and writes per-direction deltas plus warnings.

Run a focused oracle denoise sweep when deciding whether template guidance is
still worth exploring for a direction:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "& 'Tools\AssetForge\run_litiso_oracle_denoise_sweep.ps1' -Direction W -TemplateDenoiseValues @(0.24,0.32,0.40) -JobPrefix litiso_oracle_sweep_refknight_w -ReplaceExisting"
```

The sweep queues each value, runs the Comfy worker, builds a contact sheet, and
writes oracle QA. Current west evidence:

```text
Assets\Generated\_Review\litiso_oracle_sweep_refknight_w_idle_w_denoise_sweep.png
Assets\Generated\_Review\litiso_oracle_sweep_refknight_w_idle_w_denoise_sweep_oracle_qa.json
```

Verdict from the first sweep: `0.24`, `0.32`, and `0.40` denoise all stayed too
similar and still carried high RGB/style drift. Treat this as evidence to pivot
toward training/curated direction references, not as a reason for more blind
prompt churn.

Capture an approved oracle sheet as training/evaluation data:

```powershell
& 'C:\Projects\ComfyUI\.venv\Scripts\python.exe' Tools\AssetForge\capture_direction_oracle_dataset.py --project-root . --oracle-manifest Assets\Generated\_Review\litiso_reference_knight_idle_4d_sheet_manifest.json --pack-name reference_knight_idle_4d_oracle
```

The first captured pack is:

```text
C:\Projects\Pixel Pipeline\datasets\lit_iso\direction_oracles\reference_knight_idle_4d_oracle
```

It contains four captioned direction records, copied reference frames, the packed
sheet/contact sheet, hashes, and a capture manifest. This is an anchor/eval pack,
not enough data for standalone LoRA training by itself.

Build a new oracle directly from an approved 4D sheet:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "& 'Tools\AssetForge\build_direction_oracle_factory.ps1' -Sheet 'Assets\Generated\_Review\litiso_reference_knight_idle_4d_sheet.png' -PackName 'reference_knight_idle_4d_factory_smoke' -Frame @('S=0,0','E=1,0','N=2,0','W=3,0') -CharacterDescription 'armored knight with cyan energy trim, amber runes, dark hood, glowing sword' -Action 'idle pose' -CaptureDataset"
```

The factory:

- slices explicit `Direction=Column,Row` cells from a sheet
- normalizes every frame to a fixed `128x128` transparent cell
- bottom-center anchors the visible sprite
- writes an oracle manifest, packed row sheet, contact sheet, and validation JSON
- optionally captures a matching dataset pack under `C:\Projects\Pixel Pipeline\datasets\lit_iso`

Smoke-test output:

```text
Assets\Generated\_Review\_DirectionOracles\reference_knight_idle_4d_factory_smoke
C:\Projects\Pixel Pipeline\datasets\lit_iso\direction_oracles\reference_knight_idle_4d_factory_smoke
```

Use this factory as the preferred intake path for future approved male/female
adventurer, guard, villager, and mob direction sheets.

Build/update the combined direction-oracle dataset index:

```powershell
& 'C:\Projects\ComfyUI\.venv\Scripts\python.exe' Tools\AssetForge\build_direction_oracle_dataset_index.py --project-root . --include reference_knight_idle_4d_factory_smoke --include lpc_male_leather_adventurer_walk_4d_oracle_v2 --include lpc_female_forest_scout_walk_4d_oracle_v2 --include lpc_male_plate_guard_walk_4d_oracle_v2
```

Current index:

```text
C:\Projects\Pixel Pipeline\datasets\lit_iso\direction_oracles\_index
```

It currently contains `52` balanced `S/E/N/W` records across thirteen packs
after the LPC motion expansion pass. This crosses the first experimental
LoRA threshold for a direction-anchor LoRA. It is not enough for final
LIT-ISO art style training because most records are LPC-derived body/direction
anchors rather than approved final-style sprites.

Dry-run the first direction-anchor training launch:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\start_direction_oracle_anchor_training.ps1 -DryRun
```

Start it only when the laptop is free:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\start_direction_oracle_anchor_training.ps1
```

Pause safely:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\pause_litiso_training.ps1 -OutputName litiso_direction_oracle_anchor_v1
```

After a checkpoint or final LoRA exists, sync it into ComfyUI:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\sync_lora_to_comfyui.ps1 -OutputName litiso_direction_oracle_anchor_v1
```

Plan or run the direction-anchor evaluation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\eval_litiso_direction_oracle_anchor_v1.ps1 -DryRun -Limit 4
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\eval_litiso_direction_oracle_anchor_v1.ps1 -Limit 4
```

Run the more useful pixel-style stack check:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\eval_litiso_direction_oracle_anchor_v1.ps1 -Limit 4 -OutputDir "C:\Projects\Pixel Pipeline\generated\litiso_direction_oracle_anchor_v1_eval_pixelredmond_triggered" -StyleLora PixelArtRedmond-Lite64.safetensors -StyleStrength 1.05 -LoraStrength 0.38 -PromptPrefix "PixelArtRedmond, Pixel Art, crisp low resolution pixel art sprite, 64x64 game sprite, hard square pixels, limited palette," -PromptSuffix "no floor, no gray background, no circle, no concept art, transparent background, isolated sprite, nearest-neighbor pixel art"
```

Clean any eval manifest with the production character cleanup pipeline:

```powershell
& "C:\Projects\ComfyUI\.venv\Scripts\python.exe" Tools\AssetForge\clean_lora_eval_outputs.py --manifest "C:\Projects\Pixel Pipeline\generated\litiso_direction_oracle_anchor_v1_eval_pixelredmond_triggered\manifest.json"
```

Evaluation output defaults to:

```text
C:\Projects\Pixel Pipeline\generated\litiso_direction_oracle_anchor_v1_eval
```

Review the generated `direction_oracle_anchor_eval_contact.png` for the real
question this LoRA is meant to answer: south should face camera, north should be
a back view, and east/west should become true side views. Final LIT-ISO style
still needs a separate style LoRA or style-reference pass.

### ControlNet 4D Direction Smoke

Build the hand-authored 4D OpenPose controls:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\build_litiso_openpose_direction_library.ps1 -Action Idle -Replace
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\build_litiso_openpose_direction_library.ps1 -Action Walk -Replace
```

Queue 4D ControlNet requests against the reference knight style crop:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\queue_litiso_controlnet_direction_requests.ps1 -Action Idle -JobPrefix litiso_control_refknight -StyleReference Assets\Generated\_Review\_StyleRefs\reference_knight_front_cell.png -Replace
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\queue_litiso_controlnet_direction_requests.ps1 -Action Walk -JobPrefix litiso_control_refknight -StyleReference Assets\Generated\_Review\_StyleRefs\reference_knight_front_cell.png -ControlStrength 0.88 -Replace
```

Queue an oracle-template pass when generated directions must preserve the
approved 4D reference camera/framing:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\queue_litiso_controlnet_direction_requests.ps1 -Action Idle -Directions N,W -JobPrefix litiso_oracle_refknight -StyleReference Assets\Generated\_Review\_StyleRefs\reference_knight_front_cell.png -StyleWeight 0.50 -ControlStrength 0.98 -OracleManifest Assets\Generated\_Review\litiso_reference_knight_idle_4d_sheet_manifest.json -TemplateDenoise 0.42 -Replace
```

Run one queued job:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\process_generation_request_comfy.ps1 -JobName litiso_control_refknight_idle_s -ReplaceExisting
```

Current evidence:

```text
Assets\Generated\_Review\litiso_control_refknight_idle_4d_contact.png
Assets\Generated\_Review\litiso_control_refknight_idle_4d_sheet.png
Assets\Generated\_Review\litiso_control_refknight_idle_4d_direction_qa.json
Assets\Generated\_Review\litiso_control_refknight_walk_4d_contact.png
Assets\Generated\_Review\litiso_control_refknight_walk_4d_sheet.png
Assets\Generated\_Review\litiso_control_refknight_walk_4d_direction_qa.json
Assets\Generated\_Review\litiso_oracle_refknight_idle_nw_contact.png
Assets\Generated\_Review\litiso_oracle_refknight_idle_nw_sheet.png
Assets\Generated\_Review\litiso_oracle_refknight_idle_nw_oracle_qa.json
```

Verdict: this proves the local ControlNet/OpenPose path and gives repeatable
4D idle/walk artifacts. Oracle-template conditioning improves direction intent,
especially north/back view, but it is still a technical milestone rather than
production art. The next quality work is a low-denoise oracle sweep and a better
style/direction LoRA or curated direction references.

## Production-Ready Local Workflow

1. Start ComfyUI and confirm the local URL from the config, normally
   `http://127.0.0.1:8188`.
2. Generate candidates into a review pack under
   `Assets\Generated\_Review\<PackName>`.
3. Keep provenance with the pack: prompt, negative prompt, workflow/model/LoRA,
   seed, generated UTC, source, clean-room note, and category.
4. Run strict QA with `-FailOnReview`.
5. Review in the local dashboard and write explicit approve/reject decisions.
6. Approve only passing decisions into generated handoff folders.
7. Validate the handoff folders.
8. Capture approved examples into the external Pixel Pipeline dataset.
9. Train or evaluate category-specific LoRAs from approved examples only.
10. Re-enter LoRA output through a new review pack before promotion.

Promotion-ready means: strict QA passes, review decision is approved, provenance
is present and clean-room compliant, rejected frames are absent, Unity import
metadata is valid, and handoff validation passes.

Training-ready means: only approved examples are captured, a
`dataset_readiness_summary.json` exists, labels are category-specific, and
terrain/prop concepts are not mixed.

## Validate Unity Exports

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\validate_asset_forge_exports.ps1
```

This scans `Assets\Generated` for Asset Forge `manifest.json` files and writes:

```text
Assets\Generated\asset_forge_export_validation.json
```

An export is considered promotion-ready only when it has action sheets, valid import metadata, no rejected frames, and no QA failures.

Dataset/audit manifests under `Assets\Generated` are skipped and reported separately, so FreePixel training samples do not pollute the Unity-ready export count.

## Evaluate Sprixen LoRA Checkpoints

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\evaluate_asset_forge_sprixen_checkpoint.ps1 -UseLatestAvailable
```

Use `-DryRun` to verify prompts and checkpoint selection without queueing ComfyUI work.

## Watch Training Health

```powershell
powershell -ExecutionPolicy Bypass -File Tools\LoRA\watch_sprixen_frame_training.ps1
```

The watcher reports progress, ETA, checkpoint availability, log age, and a health value. If `Health` is `likely_stalled`, the training log has not advanced recently and the run should be inspected or resumed before waiting longer.

The current Sprixen trainer saves LoRA checkpoints but not optimizer state, so it cannot truly resume from `step01500` or `step02250`. If the old process is confirmed inactive, restart the full run without rebuilding the dataset or smoke pass:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\run_sprixen_frame_training.ps1 -SkipDataset -SkipSmoke
```

For a guarded restart path, use:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\restart_stalled_sprixen_frame_training.ps1
```

Use `-DryRun` first to write `Temp\LoRA\sprixen_frame_recovery_manifest.json` without starting a new training process.

## Start Final Evaluation Watcher

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\start_sprixen_final_eval_watcher.ps1
```

This starts a background watcher that waits for the fresh final Sprixen LoRA, then runs the fixed Asset Forge evaluation prompts into `TempEvalFinal`. It uses `-AfterRecoveryStart` so stale pre-restart checkpoints are ignored.

## Audit Sprixen Eval Quality

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\audit_sprixen_eval_outputs.ps1
```

This scans the final LoRA eval PNGs and writes `Temp\LoRA\sprixen_eval_quality_report.json`. Outputs are blocked when they have opaque background corners, foreground bounds too large for a sprite frame, or too many estimated palette colors for a tiny pixel sprite.

## Watch Then Evaluate

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\watch_and_evaluate_sprixen_checkpoint.ps1 -Target latest
```

Use `-Target step02250` or `-Target final` when you want the script to wait for a specific checkpoint before running the fixed Asset Forge evaluation prompts.

After a guarded restart, add `-AfterRecoveryStart` so old checkpoint files from the previous run are ignored:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\watch_and_evaluate_sprixen_checkpoint.ps1 -Target step00750 -AfterRecoveryStart
```

## Create Smoke Export

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\create_asset_forge_smoke_export.ps1
```

This creates a deterministic fake Asset Forge character export so the Unity-side promotion validator can be tested without spending GPU time.

## Build And Approve A Review Pack

The stable wrapper around the current biome starter generator is:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\build_review_pack.ps1 -ApprovePassing -ReplaceExisting
```

This runs generation, import-metadata normalization, review report generation, decision initialization, strict asset QA, and approval copying. With `-ApprovePassing`, strict QA fails closed if any asset is blank, has opaque corners/background, or otherwise needs review.

Approval is fail-closed. If `review_report.json` has issues for an asset, `review_decisions.json` marks that asset `needs_edit`, and `approve_review_pack.ps1` refuses to promote it while it is still marked `approved`. The approval wrapper also refuses duplicate decision IDs, unknown decision values, decisions missing from the report, and report items without decisions.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\approve_review_pack.ps1 -PackName CodexBiomeStarter -ReplaceExisting
```

Use `-PruneUnapproved` only when you intentionally want to remove generated handoff PNGs that are not approved by the current decisions file:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\approve_review_pack.ps1 -PackName CodexBiomeStarter -ReplaceExisting -PruneUnapproved
```

Approved assets go only to generated handoff folders:

```text
Assets/Generated/Tiles/<Biome>
Assets/Generated/Props/<Biome>
```

They are not copied into `Assets/Resources/**` by this tool.

Validate the generated tile/prop handoff folders without deleting anything:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\validate_tile_prop_handoff.ps1 -PackName CodexBiomeStarter
```

The current handoff validator flags two stale generated tiles as not approved: `forest_grass_base.png` and `plains_bare_dirt.png`. They remain in place until `-PruneUnapproved` is run intentionally.

## Capture Approved Assets For LoRA

Once decisions are correct, capture approved/rejected review examples into the
external Pixel Pipeline dataset folder:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\capture_dataset_from_review.ps1 -PackName CodexBiomeStarter
```

This writes a LoRA-ready `metadata.jsonl` plus image/caption pairs under:

```text
C:\Projects\Pixel Pipeline\datasets\lit_iso\review_packs\<PackName>
```

`capture_dataset_from_review.ps1` defaults to the external Pixel Pipeline
dataset root, refuses missing source images, refuses approved items that still
have review-report issues, and writes
`metadata\dataset_readiness_summary.json`.

## Strict Asset Quality Scan

Use the stricter QA pass to catch common model failures before assets enter training:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\test_strict_asset_quality.ps1 -InputPath Assets\Generated\_Review\CodexBiomeStarter
```

This writes `strict_asset_quality_report.json` beside the scanned folder and flags issues like blank alpha, opaque corners/backgrounds, wrong terrain size, and likely prop base plates.

Use `-FailOnReview` in automation to make the scan exit nonzero unless all scanned PNGs are export-ready:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\test_strict_asset_quality.ps1 -InputPath Assets\Generated\_Review\CodexBiomeStarter -FailOnReview
```

## Local Review Dashboard

For reliable image previews, serve the dashboard from the project root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\serve_dashboard.ps1 -Port 4191
```

Then open:

```text
http://127.0.0.1:4191/Tools/AssetForge/Dashboard/index.html
```

You can also open the static file directly, but some browsers restrict local image loading:

```text
Tools\AssetForge\Dashboard\index.html
```

Load:

```text
Assets\Generated\_Review\CodexBiomeStarter\review_report.json
Assets\Generated\_Review\CodexBiomeStarter\review_decisions.json
```

The dashboard shows QA cards, filters terrain/props, edits decisions, and downloads an updated `review_decisions.json`.

The downloaded decisions include `biome` and `destination_path`, so they are compatible with `approve_review_pack.ps1`.

### Queue Generation Requests

The Generate workspace can now save worker-ready request manifests through the local dashboard server:

```text
POST /api/assetforge/save-generation-request
```

The route writes only local handoff artifacts. It does not run ComfyUI, import into Unity, or approve assets. Requests land under:

```text
Assets/Generated/_Review/_Requests/<JobName>
```

Each queued request contains:

- `generation_request.json` - full prompt, mode, canvas, clip, post-process, and Unity intent
- `worker_queue_item.json` - the worker contract for the eventual ComfyUI processor
- `request_status.json` - queued status and intended review-pack location
- `Review/review_report.placeholder.json` - empty placeholder until generation writes real PNGs
- `Review/review_decisions.placeholder.json` - empty placeholder until generation writes real PNGs

The status route reports `requestQueueCount` and recent queued jobs. This keeps the pipeline local-first: dashboard request -> queued manifest -> ComfyUI worker -> deterministic cleanup -> review pack -> strict QA -> approval.

The dashboard also checks ComfyUI health through:

```text
GET /api/comfy/status
```

That route is safe when ComfyUI is offline. When online, it reports the configured URL, queue pending/running counts, and basic system/device stats for the dashboard status strip.

### Run The Local Draft Worker

Until the ComfyUI processor is connected, use the local deterministic draft worker to prove the full review-pack path:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\process_generation_request.ps1 -JobName <JobName> -ReplaceExisting
```

The dashboard also exposes this as `Run local draft worker` in the Generate workspace. It queues the current request, generates local transparent PNG drafts, and writes:

```text
Assets/Generated/_Review/<JobName>/review_report.json
Assets/Generated/_Review/<JobName>/review_decisions.json
Assets/Generated/_Review/<JobName>/strict_asset_quality_report.json
Assets/Generated/_Review/<JobName>/generation_manifest.json
```

This worker is for workflow validation and local fallback assets. It intentionally writes pending review decisions by default; production art should still come from the ComfyUI/provider path and pass through the same review and QA files.

The dashboard Request Queue panel lists recent queued jobs from `GET /api/assetforge/status`. Use `Show status` to inspect a queued request and `Run worker` to process an existing queued job without retyping its manifest.

### Review Pack Browser

Generated packs are first-class dashboard resources now:

```text
GET /api/assetforge/review-packs
POST /api/assetforge/load-review-pack
```

The dashboard `Review Packs` panel lists recent packs with pass/review counts and loads a pack without manually selecting JSON files. This is the preferred way to inspect outputs such as:

```text
real_comfy_forest_grass_tile_v4
real_comfy_ironwood_axe_item_v4
real_comfy_forest_bush_prop_v4
```

Current quality note: prompt-only Comfy works acceptably for simple terrain, some items, and single full-body character sprites after chroma cleanup. Props can still drift into miniature scenes. Keep those in review until reference conditioning or a narrower prop LoRA is wired.

### Local Comfy Defaults

Machine-local settings live in ignored file:

```text
Tools/AssetForge/asset_forge.local.json
```

The example config documents per-mode defaults. On this machine, tile/prop use `litiso_tile_prop_v1_final.safetensors`, item generation uses `PixelArtRedmond-Lite64.safetensors`, and character/NPC/mob generation uses `litiso_style_directional_v1.safetensors`. The Comfy worker still runs deterministic cleanup and strict QA after every generation.

## Optional Sprixen Provider

Sprixen is now an optional source provider for Asset Forge, not a replacement for
local QA or training. It feeds the same review-pack format as ComfyUI:

```text
POST /api/assetforge/process-generation-request-sprixen
GET /api/sprixen/status
```

Keep the API key out of git. Either set an environment variable before starting
the dashboard server:

```powershell
$env:SPRIXEN_API_KEY = "spx_live_your_key_here"
```

or put the key in ignored local config:

```text
Tools\AssetForge\asset_forge.local.json
```

under:

```json
{
  "sprixen": {
    "api_key": "",
    "base_url": "https://api.sprixen.com/v1"
  }
}
```

Always dry-run before spending credits:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\process_generation_request_sprixen.ps1 -JobName <RequestFolderName> -DryRun
```

Then run only after the prompt, mode, project id, and target count look correct:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\process_generation_request_sprixen.ps1 -JobName <RequestFolderName>
```

Sprixen output is copied into:

```text
Assets\Generated\_Review\<JobName>
```

with `sprixen_generation_manifest.json`, `review_report.json`,
`review_decisions.json`, `strict_asset_quality_report.json`, and
`generation_manifest.json`. No output is approved, promoted, or used for LoRA
training until it passes strict QA and receives an explicit review decision.

### Cyan-Knight Training Batch

The first style-alignment batch lives at:

```text
Tools\AssetForge\training_batches\B1_cyan_knight_style_alignment.json
```

It targets 120 approved examples:

```text
32 characters/NPCs/mobs
24 props
24 items
20 terrain tiles
20 VFX/HUD sprites
```

Queue the batch as Sprixen requests without spending credits:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\queue_training_batch_requests.ps1 -Provider sprixen
```

Each queued request can then be dry-run or run from the dashboard. The helper
caps each request to 3 variants because Sprixen sprite generations are limited
to small variant batches; rerun promising jobs with new names/seeds if we need
more candidates for a category.

## Pause/Resume Tile And Prop LoRA Training

Start a laptop-friendly local run on the current approved tile/prop dataset:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\start_resumable_litiso_training.ps1 -OutputName litiso_tile_prop_v1 -Dataset "C:\Projects\Pixel Pipeline\datasets\lit_iso\review_packs\CodexBiomeStarter" -TrainLimit 34 -MaxSteps 1000 -SaveEvery 100
```

Check progress:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\status_litiso_training.ps1 -OutputName litiso_tile_prop_v1
```

For machine-readable status:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\status_litiso_training.ps1 -OutputName litiso_tile_prop_v1 -Json
```

Pause safely:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\pause_litiso_training.ps1 -OutputName litiso_tile_prop_v1
```

Resume from the latest checkpoint:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\start_resumable_litiso_training.ps1 -OutputName litiso_tile_prop_v1 -Dataset "C:\Projects\Pixel Pipeline\datasets\lit_iso\review_packs\CodexBiomeStarter" -TrainLimit 34 -MaxSteps 1000 -SaveEvery 100 -ResumeLatest
```

The current trainer watches `C:\Projects\LoRA-Training\control\<OutputName>\pause.request`, writes `status.json`, and saves checkpoints under `C:\Projects\LoRA-Training\outputs\<OutputName>`.
For active runs, `status_litiso_training.ps1 -Json` also reports
`observed_progress` from the live `tqdm` log so training does not look stuck
between checkpoint/status writes.

Sync the latest checkpoint to ComfyUI when a run is ready for evaluation:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\sync_lora_to_comfyui.ps1 -OutputName litiso_tile_prop_v1
```

Plan or run evaluation for the latest synced LoRA:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\eval_latest_synced_lora.ps1 -OutputName litiso_tile_prop_v1 -DryRun
```

The first completed tile/prop LoRA is experimental, not production default. See:

```text
Docs\IsoCoreFoundation\13_AssetForge_Self_Review.md
```
