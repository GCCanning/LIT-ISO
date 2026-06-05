# Asset Forge Validation Tools

These scripts support the LIT-ISO Asset Forge pipeline outside the Unity editor.

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
