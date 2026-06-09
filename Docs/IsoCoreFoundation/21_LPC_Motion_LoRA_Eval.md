# LPC Motion Template LoRA Eval

Generated: 2026-06-07

## Checkpoint

- LoRA: `litiso_lpc_motion_template_v1_final.safetensors`
- Training output: `C:\Projects\LoRA-Training\outputs\litiso_lpc_motion_template_v1`
- Synced ComfyUI copy: `C:\Projects\ComfyUI\models\loras\litiso_lpc_motion_template_v1_final.safetensors`
- Dataset: `C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\lpc_motion_male_female_v1`

## Evaluation Runs

### DreamShaper Base

- Output: `C:\Projects\Pixel Pipeline\generated\litiso_lpc_motion_template_v1_eval`
- Contact sheet: `lpc_motion_template_eval_contact.png`
- Result: failed for production sprite generation.

DreamShaper dominated the LoRA and produced portraits, concept-art characters, props, and background scenes. The LoRA weakly influenced character/action words, but not enough to enforce 64x64 transparent sprite-frame output.

### PixelartSpritesheet Base

- Output: `C:\Projects\Pixel Pipeline\generated\litiso_lpc_motion_template_v1_eval_pixelartspritesheet_smoke`
- Contact sheet: `lpc_motion_template_eval_contact.png`
- Result: partially useful, not production-ready.

The sprite-sheet checkpoint kept outputs pixel-art and sprite-like, but it strongly prefers repeated mini sprite rows inside each generated image. This can be salvaged for frame recovery experiments, but it is not yet a clean single-frame generator.

## Verdict

The first LPC LoRA is useful as a motion-template experiment, but it is not the final actor generator. The next concrete step is a controlled frame-recovery path:

1. Use the PixelartSpritesheet base for sprite-like output.
2. Detect repeated mini frames inside generated PNGs.
3. Recover individual frames by connected-component or grid segmentation.
4. Normalize each recovered frame to 64x64/128x128 bottom-center anchored cells.
5. Feed only approved recovered frames into the final style-lock dataset.

Do not train longer on this exact setup until we have either:

- a better single-frame sprite checkpoint, or
- a post-generation recovery path that turns sheet-like outputs into usable frames.

## Frame Recovery Pass

Implemented:

- `Tools/AssetForge/recover_sprite_frames_from_sheet.py`
- `Tools/AssetForge/recover_sprite_frames_from_sheet.ps1`

Test input:

- `C:\Projects\Pixel Pipeline\generated\litiso_lpc_motion_template_v1_eval_pixelartspritesheet_smoke`

Recovered output:

- `C:\Projects\Pixel Pipeline\generated\litiso_lpc_motion_template_v1_recovered_frames_v1`

Result:

- Source images: 4
- Recovered normalized frames: 16
- Caption sidecars: 16
- Contact sheet: `recovered_frame_contact_sheet.png`

The recovery pass successfully extracted the four repeated mini sprites from each PixelartSpritesheet smoke output, removed the flat background, and normalized them to 64x64 transparent bottom-centered cells. This makes the PixelartSpritesheet failure mode salvageable for review and future dataset capture.

Next recommended step: add an approval/capture wrapper that copies accepted recovered frames into a dedicated `recovered_motion_candidates` dataset folder with provenance back to the generated sheet, prompt, LoRA, checkpoint, and source LPC dataset.

## Dataset Capture Pass

Implemented:

- `Tools/AssetForge/capture_recovered_sprite_frames.py`
- `Tools/AssetForge/capture_recovered_sprite_frames.ps1`

Captured dataset:

- `C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\recovered_motion_candidates_v1`

Dataset contents:

- Images: 16
- Captions: 16
- Train split: 14
- Validation split: 2
- QA status: structural pass, direction QC failed
- Metadata: `metadata.jsonl`
- Provenance: source recovered manifest and source generation manifest
- Contact sheet: `approved_recovered_frames_contact_sheet.png`

This completes the first four-step salvage loop:

1. Recover frames from sheet-like generated outputs.
2. Normalize each frame to transparent 64x64 cells.
3. Capture approved candidates into a dataset with captions and metadata.
4. Preserve provenance and QA so these candidates can be reviewed before entering the final style-lock LoRA.

Human QC later rejected these recovered generated frames for direction correctness. They are now quarantined:

- Dataset status: `quarantined_direction_failed`
- Training allowed: `false`
- Quarantine marker: `QUARANTINED_DIRECTION_QC.json`

The extractor remains useful, but recovered AI frames must not enter a direction/action training set unless they pass visual direction QC.

## Direction Oracle

Implemented:

- `Tools/AssetForge/build_lpc_direction_oracle.py`
- `Tools/AssetForge/build_lpc_direction_oracle.ps1`
- `Tools/AssetForge/quarantine_recovered_motion_dataset.ps1`

Known-good direction oracle output:

- `C:\Projects\Pixel Pipeline\generated\lpc_direction_oracle_v1`
- Contact sheet: `lpc_cardinal_direction_oracle.png`

The oracle uses original LPC dataset metadata, not generated outputs. It proves the available source directions are:

- South: front-facing / face toward camera
- East: screen-right profile
- North: back-facing / face hidden
- West: screen-left profile

Diagonal directions are not available from this LPC source set and must not be fabricated by relabeling cardinal frames. For LIT-ISO 8D, diagonals need true reviewed source frames for `south-east`, `north-east`, `north-west`, and `south-west`, or a separate pose/reference system that can produce distinct three-quarter facings.
