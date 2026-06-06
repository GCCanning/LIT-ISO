# Asset Forge Self-Review

Date: 2026-06-05

## Current State

Asset Forge has a working local review and dataset loop for the `CodexBiomeStarter` tile/prop pack:

- Review pack: `Assets/Generated/_Review/CodexBiomeStarter`
- Approved handoff folders:
  - `Assets/Generated/Tiles/<Biome>`
  - `Assets/Generated/Props/<Biome>`
- Repo-local dataset capture:
  - `Assets/Generated/_Datasets/lit_iso/review_packs/CodexBiomeStarter`
- Experimental LoRA output:
  - `C:\Projects\LoRA-Training\outputs\litiso_tile_prop_v1\litiso_tile_prop_v1_final.safetensors`
- Synced ComfyUI LoRA:
  - `C:\Projects\ComfyUI\models\loras\litiso_tile_prop_v1_final.safetensors`

## Verified Today

- Strict asset QA passed: 36 scanned assets, 36 pass, 0 review.
- Approval copy passed: 34 copied, 2 skipped, 0 failed.
- Dataset capture passed: 34 records.
- Tile/prop handoff validation ran: 34 pass, 2 review.
- LoRA status reports `complete`, 1000/1000, no active trainer process.
- Latest synced LoRA evaluation dry-run produced a valid command summary.

## Quality Result

`litiso_tile_prop_v1_final.safetensors` proves the local training loop works, but it is not production quality. The eval outputs still tend to bake floors/ground context into images and over-generalize terrain/prop concepts.

Use it as an experimental checkpoint only.

## Known Issues

- Two stale generated handoff tiles remain in `Assets/Generated/Tiles`:
  - `Assets/Generated/Tiles/Forest/forest_grass_base.png`
  - `Assets/Generated/Tiles/Plains/plains_bare_dirt.png`
- They are not approved by `review_decisions.json` and have `ppu_not_32`.
- They can be removed with `approve_review_pack.ps1 -PruneUnapproved`, but that is intentionally opt-in because it deletes files.
- Unity generated asset reimport still needs an editor pass via `Tools > Asset Forge > Reimport Generated Assets`.

## Recommended Next Steps

1. Prune stale unapproved generated handoff assets after human approval.
2. Split future datasets into terrain-only and prop-only sets.
3. Add dashboard actions for save decisions, approve, capture dataset, status, sync, and eval.
4. Feed LoRA eval outputs back into the same review-pack schema.
5. Train `litiso_terrain_v1` and `litiso_props_v1` only after each category has stronger source examples.

