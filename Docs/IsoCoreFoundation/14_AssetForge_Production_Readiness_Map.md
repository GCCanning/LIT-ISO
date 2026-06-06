# Asset Forge Production Readiness Map

Date: 2026-06-05

## Canonical Local Loop

1. Generate or import candidate PNGs into `Assets/Generated/_Review/<PackName>`.
2. Run `review_report.json` QA plus strict image QA.
3. Review assets in the local dashboard.
4. Approve only assets that pass the strict contract.
5. Copy approved assets to `Assets/Generated/Tiles` and `Assets/Generated/Props`.
6. Validate the handoff folders against the current decisions.
7. Capture approved examples into the repo-local dataset folder.
8. Train or refine category-specific LoRAs.
9. Evaluate LoRA outputs through the same review loop.

## Ready

- Fail-closed approval.
- Strict image QA.
- Non-destructive tile/prop handoff validation.
- Repo-local dataset capture.
- Static polished dashboard shell.
- Resumable local LoRA training controls.
- LoRA sync and latest-synced eval dry-run.

## Experimental

- The combined `litiso_tile_prop_v1` LoRA.
- Combined tile/prop datasets.
- Older FreePixel/directional scripts for character research.

## Production Gates

An asset is not production-ready unless:

- transparent background,
- no opaque corners,
- terrain is strict 32x32 terrain-only PNG,
- prop is isolated with no floor/base plate,
- import settings are point-filtered, no mipmaps, correct PPU/pivot,
- current `review_decisions.json` approves it,
- strict QA and handoff validation pass.

## Next Implementation Slice

1. Add dashboard backend endpoints for save/approve/capture/status/sync/eval.
2. Convert LoRA eval outputs into review packs automatically.
3. Add category-specific terrain and prop presets.
4. Build stronger terrain-only and prop-only datasets.
5. Train separate `litiso_terrain_v1` and `litiso_props_v1` checkpoints.

