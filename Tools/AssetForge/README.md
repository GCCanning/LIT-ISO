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
8. Capture approved examples into the repo-local dataset.
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

Once decisions are correct, capture approved/rejected review examples into a repo-local dataset folder:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\capture_dataset_from_review.ps1 -PackName CodexBiomeStarter
```

This writes a LoRA-ready `metadata.jsonl` plus image/caption pairs under:

```text
Assets\Generated\_Datasets\lit_iso\review_packs\<PackName>
```

`capture_dataset_from_review.ps1` refuses `-DatasetRoot` values outside the repo, refuses missing source images, refuses approved items that still have review-report issues, and writes `metadata\dataset_readiness_summary.json`.

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

## Pause/Resume Tile And Prop LoRA Training

Start a laptop-friendly local run on the current approved tile/prop dataset:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\start_resumable_litiso_training.ps1 -OutputName litiso_tile_prop_v1 -Dataset "Assets\Generated\_Datasets\lit_iso\review_packs\CodexBiomeStarter" -TrainLimit 34 -MaxSteps 1000 -SaveEvery 100
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

<<<<<<< Updated upstream
Resume from the latest checkpoint:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\start_resumable_litiso_training.ps1 -OutputName litiso_tile_prop_v1 -Dataset "Assets\Generated\_Datasets\lit_iso\review_packs\CodexBiomeStarter" -TrainLimit 34 -MaxSteps 1000 -SaveEvery 100 -ResumeLatest
```

The current trainer watches `C:\Projects\LoRA-Training\control\<OutputName>\pause.request`, writes `status.json`, and saves checkpoints under `C:\Projects\LoRA-Training\outputs\<OutputName>`.

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
