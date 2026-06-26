# FreePixel LoRA Training Notes

## Current State

FreePixel source page:

- <https://freepixel.art/characters>
- Page reports 289 animated characters, 8 directions, 684 total animations, 128px sprites.

Local raw scrape:

- `C:\Projects\Pixel Pipeline\style_examples\freepixel_web_download`
- `9392` PNG frames
- `219` source sheets
- Categories: `mages`, `warriors`, `rogues`, `npcs`, `enemies`
- Captions include category, character name, and action.
- Captions do **not** include direction.

Existing training runs:

- `litisochar_anim_v1`
  - Dataset: `C:\Projects\Pixel Pipeline\style_examples\characters_train_v1`
  - `1400` samples
  - Has direction labels, but it is a narrow melee/action subset.

- `litiso_style_v1`
  - Dataset: `C:\Projects\Pixel Pipeline\style_examples\style_train_v1_freepixel`
  - `633` samples
  - Broad style test, not the full FreePixel scrape.

- `run_litiso_style_v3_train.ps1`
  - Points at the full `freepixel_web_download` dataset.
  - Current problem: full dataset lacks direction captions.

## Recommendation

Train a new LoRA from a regenerated structured FreePixel dataset rather than the current flattened scrape.

The new dataset should preserve:

- `category`: mages, warriors, rogues, npcs, enemies
- `character`: wizard, paladin, skeleton archer, etc.
- `action`: idle, walk, run
- `direction`: south, south-east, east, north-east, north, north-west, west, south-west
- `frame_index`
- `direction_index`

This teaches the LoRA what we actually need to request later:

```text
litiso_style, fp_category mages, fp_character wizard, fp_action idle, fp_direction south, facing south, 8-direction game sprite, full body, transparent background
```

## Dataset Prep Script

Use:

```powershell
python Tools\LoRA\freepixel_structured_dataset.py --directional-only
```

Default output:

```text
C:\Projects\Pixel Pipeline\style_examples\freepixel_web_download_structured
```

Audit the old dataset:

```powershell
python Tools\LoRA\freepixel_structured_dataset.py --audit-existing
```

Quick test scrape:

```powershell
python Tools\LoRA\freepixel_structured_dataset.py --directional-only --limit-sheets 5
```

Important: some FreePixel sheets use only a subset of rows for a given action. Those are useful for broad style learning, but they should not be used for direction-control training unless we can prove their row mapping. The structured scraper therefore supports `--directional-only`, which skips sheets with fewer than 6 populated direction rows.

## Training Command

After the structured dataset is generated, clone the v3 training command but point it at:

```text
C:\Projects\Pixel Pipeline\style_examples\freepixel_web_download_structured
```

Suggested LoRA identity:

```text
litiso_style_directional_v1
```

Suggested starting settings:

- Base: `C:\Projects\ComfyUI\models\checkpoints\DreamShaper_8_pruned.safetensors`
- Resolution: `512`
- Train limit: `5000` first pass, then full if quality improves
- Steps: `3000`
- Rank: `32`
- Learning rate: `0.00004`
- Save every: `750`

## Overnight Directional Run

Use the guarded overnight runner when you want the full staged process:

```powershell
powershell -ExecutionPolicy Bypass -File Tools\LoRA\run_overnight_directional_training.ps1
```

The runner:

- rebuilds the structured dataset with `--directional-only`
- verifies `manifest.json`, `metadata.jsonl`, `fp_direction` captions, transparent PNG output, and skipped partial sheets
- trains a 250-step smoke LoRA before attempting the 3000-step full run
- copies `litiso_style_directional_v1.safetensors` into `C:\Projects\ComfyUI\models\loras`
- writes the overnight log to `C:\Projects\LoRA-Training\outputs\litiso_style_directional_v1\overnight_train.log`
- generates fixed-seed ComfyUI samples if ComfyUI is reachable, otherwise writes a skipped eval manifest

## Why This Should Improve Results

The previous wizard generations showed the model understood "wizard-ish character art", but not strict game-sprite requirements. Direction-aware captions give the model stronger conditioning for:

- 8-direction generation
- idle/walk/run action separation
- one full-body sprite per image
- no scene, text, aura floor, or decorative background
