# OpenGameArt 8D Motion Training Index

Purpose: convert the approved OpenGameArt 8-direction character pack into a structured LIT-ISO training/reference dataset.

## Source

- Source page: `https://opengameart.org/content/400-items-basehumanmale-orc-skeleton`
- License: CC-BY 4.0
- Attribution recorded locally: LinksDream / OpenGameArt page credits
- Training approval: owner approved local training use on 2026-06-08
- Source root: `C:\Projects\Pixel Pipeline\sources\opengameart\400-items-basehumanmale-orc-skeleton\extracted`

## Tooling

Indexer:

`Tools/AssetForge/build_oga_8d_training_index.py`

Launcher:

`Tools/AssetForge/build_oga_8d_training_index.ps1`

Training launcher:

`Tools/LoRA/start_oga_8d_motion_training.ps1`

Pause command:

`powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\pause_litiso_training.ps1 -OutputName litiso_oga8d_motion_direction_v1`

Status command:

`powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\status_litiso_training.ps1 -OutputName litiso_oga8d_motion_direction_v1`

## Dataset Output

Dataset root:

`C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\oga_8d_motion_direction_v1`

Generated files:

- `dataset_manifest.json`
- `index\frames_index.jsonl`
- `index\layer_summary.json`
- `index\action_direction_counts.csv`
- `ready_training_subset\metadata.jsonl`
- `ready_training_subset\train.txt`
- `ready_training_subset\val.txt`
- `ready_training_subset\qa_report.json`
- `ready_training_subset\ready_subset_contact_sheet.png`
- `ready_training_subset\NOTICE_ATTRIBUTION.md`

Result:

- Full parsed index: 243,024 frames
- Layer count: 498
- Ready copied subset: 488 `BaseHumanMale` frames
- Train split: 440 frames
- Validation split: 48 frames
- Ready subset QA: pass

## Direction Contract

LIT-ISO canonical order:

`S, SE, E, NE, N, NW, W, SW`

Source camera mapping:

| LIT-ISO | Source camera |
|---|---|
| S | CAM7 |
| SE | CAM6 |
| E | CAM5 |
| NE | CAM4 |
| N | CAM3 |
| NW | CAM2 |
| W | CAM1 |
| SW | CAM0 |

## Training Run

Body-only pilot output name:

`litiso_oga8d_motion_direction_v1`

Body-only training output:

`C:\Projects\LoRA-Training\outputs\litiso_oga8d_motion_direction_v1`

Body-only control/status:

`C:\Projects\LoRA-Training\control\litiso_oga8d_motion_direction_v1`

Body-only pilot settings:

- Base checkpoint: `C:\Projects\ComfyUI\models\checkpoints\DreamShaper_8_pruned.safetensors`
- Dataset: ready `BaseHumanMale` subset
- Max steps: 1000
- Save every: 200
- Rank: 32
- Learning rate: `0.00004`
- Resolution: 512
- Batch size: 1
- Category filter: `oga_8d_motion_direction`

Body-only result:

- Status: complete
- Final checkpoint: `C:\Projects\LoRA-Training\outputs\litiso_oga8d_motion_direction_v1\litiso_oga8d_motion_direction_v1_final.safetensors`
- Synced to ComfyUI: `C:\Projects\ComfyUI\models\loras\litiso_oga8d_motion_direction_v1_final.safetensors`
- Eval contact sheet: `C:\Projects\Pixel Pipeline\generated\litiso_oga8d_motion_direction_v1_eval\oga8d_direction_eval_contact_sheet.png`
- QC verdict: not accepted for production; it produced concept-style fantasy characters and weak direction control.

## Composite Dataset Follow-Up

The body-only pilot proved the training stack works, but body-only examples are too weak. A second dataset composites OGA layers into full characters.

Composite tool:

`Tools/AssetForge/build_oga_8d_composite_dataset.py`

Composite launcher:

`Tools/AssetForge/build_oga_8d_composite_dataset.ps1`

Composite dataset root:

`C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\oga_8d_composite_motion_v1`

Composite result:

- Records: 2,440 frames
- Presets: forest guard, iron knight, arcane mage, cloak rogue, skeleton archer
- QA: pass
- Contact sheet: `C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\oga_8d_composite_motion_v1\composite_contact_sheet.png`

Composite training launcher:

`Tools/LoRA/start_oga_8d_composite_training.ps1`

Composite output name:

`litiso_oga8d_composite_motion_v1`

Composite result:

- Status: complete
- Steps: 1,200
- Final checkpoint: `C:\Projects\LoRA-Training\outputs\litiso_oga8d_composite_motion_v1\litiso_oga8d_composite_motion_v1_final.safetensors`
- Synced to ComfyUI: `C:\Projects\ComfyUI\models\loras\litiso_oga8d_composite_motion_v1_final.safetensors`
- Eval contact sheet: `C:\Projects\Pixel Pipeline\generated\litiso_oga8d_composite_motion_v1_eval\oga8d_direction_eval_contact_sheet.png`
- QC verdict: not accepted for production; outfit coherence improved, but the model still produced concept-art outputs, occasional scene backgrounds, and unreliable direction control.

## Self-Review Verdict

The dataset and training infrastructure worked. The text-only LoRA strategy did not meet the asset-quality bar.

Keep:

- OGA indexed dataset
- OGA composite dataset
- body-only and composite LoRA checkpoints as comparison baselines
- eval sheets and QC verdicts as negative evidence

Do not keep pursuing blindly:

- More text-only LoRA runs on DreamShaper expecting exact 8D sprite output

Next best implementation direction:

- Use OGA frames as template/control references, not just text captions.
- Add an Asset Forge generation path that uses a selected OGA action/direction frame as an image/template guide, then applies LIT-ISO/Sprixen-style pixel cleanup.
- Train future style LoRAs on approved LIT-ISO-style generated/curated assets, while OGA teaches motion/direction through conditioning or template alignment.

## Template-Guided Generation Smoke

Implemented first Asset Forge template-guided ComfyUI path on 2026-06-08.

Tooling:

- `Tools/AssetForge/queue_oga_template_guided_requests.py`
- `Tools/AssetForge/queue_oga_template_guided_requests.ps1`
- `Tools/AssetForge/run_oga_template_guided_smoke.ps1`
- `Tools/AssetForge/build_review_contact_sheet.py`
- `Tools/AssetForge/comfy_generation_worker.py`

Behavior:

- The queue helper selects a canonical OGA action/direction frame using the LIT-ISO camera mapping above.
- The Comfy worker detects local `reference_image` values for `character`, `npc`, and `mob` jobs when `template_guidance.enabled` is true.
- The worker prepares the OGA frame on a 512x512 neutral canvas, uploads it to ComfyUI, and switches the workflow to img2img by adding `LoadImage -> VAEEncode -> KSampler latent_image`.
- The generated image still goes through the existing deterministic cleanup and strict QA review-pack path.
- Generation manifests record `template_guidance`, source reference path, prepared template upload path, denoise, checkpoint, LoRA, and seed.

Smoke command:

`powershell -NoProfile -ExecutionPolicy Bypass -Command "& 'Tools\AssetForge\run_oga_template_guided_smoke.ps1' -ProjectRoot 'C:\Projects\Unity-Projects\LIT-ISO' -Action Walk -Direction S -JobPrefix 'oga_template_cyan_knight_sweep' -DenoiseValues @(0.62,0.70,0.78) -Seed 91300 -ReplaceExisting"`

Smoke output:

- `Assets/Generated/_Review/oga_template_cyan_knight_comparison.png`
- `Assets/Generated/_Review/oga_template_cyan_knight_sweep_comparison.png`
- `Assets/Generated/_Review/oga_template_cyan_knight_sweep_d062_walk_s`
- `Assets/Generated/_Review/oga_template_cyan_knight_sweep_d070_walk_s`
- `Assets/Generated/_Review/oga_template_cyan_knight_sweep_d078_walk_s`

Smoke verdict:

- `denoise 0.42`: too conservative; keeps too much bare OGA body anatomy.
- `denoise 0.62`: starts transforming into a cyan/orange armored sprite while preserving the south walk pose.
- `denoise 0.70`: best first balance; better armor/readability while retaining the template orientation.
- `denoise 0.78`: starts losing grounded sprite silhouette and becomes too stylized/thin.

Current conclusion:

Template-guided img2img is the right mechanical path for 4D/8D pose control, but it is not yet production art. The next quality lever is stronger approved LIT-ISO/Sprixen-style visual conditioning, not another text-only motion LoRA run.

## Style-Guided Template Update

Added style-reference support after the first template-guided smoke.

New behavior:

- `style_reference_image` is separate from `reference_image`.
- `reference_image` remains the pose/template source.
- `style_reference_image` is uploaded separately and applied through `IPAdapterUnifiedLoader -> IPAdapter`.
- Style settings are captured under `style_guidance`:
  - `preset`, default `STANDARD (medium strength)`
  - `weight`
  - `start_at`
  - `end_at`
  - `weight_type`
- Style reference images are prepared on a neutral 512x512 canvas before upload so IPAdapter does not see mostly transparent/black pixels.

Reference crop:

- Source sheet: `Assets/Resources/Characters/Player/ReferenceKnight_Idle_512x1024.png`
- Extracted cell: `Assets/Generated/_Review/_StyleRefs/reference_knight_front_cell.png`
- Extractor: `Tools/AssetForge/extract_sprite_sheet_cell.py`

Style-smoke evidence:

- `Assets/Generated/_Review/oga_template_style_lock_comparison.png`
- `Assets/Generated/_Review/oga_template_refknight_style_sweep_d070_walk_s`
- `Assets/Generated/_Review/oga_template_ironknight_refknight_style_d070_walk_s`

Updated verdict:

- IPAdapter style guidance works on this ComfyUI install after switching from `IPAdapterModelLoader` to `IPAdapterUnifiedLoader`.
- Bare OGA body templates still dominate silhouette too much.
- Composited OGA templates are better control sources than bare BaseHumanMale frames.
- `iron_knight` composite template plus reference-knight style guidance is currently the best local path tested.
- The output is still too spindly for final production; next tuning should focus on better composited templates, lower anatomy leakage, and eventually ControlNet/OpenPose rather than relying only on img2img.

Queued 4D composite requests:

- `Tools/AssetForge/queue_oga_composite_template_guided_requests.py`
- `Tools/AssetForge/queue_oga_composite_template_guided_requests.ps1`
- Queued request prefix: `oga4d_refknight_style_iron_knight_walk_*`
- Directions queued: `S, E, N, W`
- Dry-run verified: `oga4d_refknight_style_iron_knight_walk_e`

4D walk generation checkpoint:

- Generated all four queued directions: `S, E, N, W`
- Contact sheet: `Assets/Generated/_Review/oga4d_refknight_style_iron_knight_walk_contact.png`
- Packed row sheet: `Assets/Generated/_Review/oga4d_refknight_style_iron_knight_walk_sheet.png`
- Packed manifest: `Assets/Generated/_Review/oga4d_refknight_style_iron_knight_walk_sheet_manifest.json`
- Structural QA: all four review packs passed

4D walk verdict:

- Direction separation is partially working.
- The method preserves some direction intent, especially side-facing outputs.
- The art is not production-accepted: silhouettes are too thin, equipment identity drifts between directions, and the north frame is too different from the south/east/west set.
- Next test should tune fewer variables around the current best path: composited template + reference knight style, with `style_weight` and `denoise` sweeps. Do not go back to text-only LoRA for this problem.

## Style Matrix And Per-Direction Style Checkpoint

Added matrix runner:

- `Tools/AssetForge/run_oga_composite_style_matrix.ps1`

Matrix tested:

- Template: `iron_knight`, `Walk`, `S`
- Denoise: `0.62`, `0.70`, `0.78`
- Style weights: `0.50`, `0.68`
- Contact sheet: `Assets/Generated/_Review/oga_matrix_refknight_style_iron_knight_Walk_S_matrix.png`

Matrix verdict:

- Best south-facing setting from this pass: `style_weight 0.50`, `denoise 0.70`.
- `0.62` under-transforms or keeps too much template leakage.
- `0.78` tends to lose grounded sprite structure.
- Higher style weight does not automatically improve identity; it can make the sprite smaller/thinner.

Best-setting 4D pass:

- Contact: `Assets/Generated/_Review/oga4d_refknight_style_sw050_d070_walk_contact.png`
- Sheet: `Assets/Generated/_Review/oga4d_refknight_style_sw050_d070_walk_sheet.png`
- Manifest: `Assets/Generated/_Review/oga4d_refknight_style_sw050_d070_walk_sheet_manifest.json`

Per-direction style references:

- Cell contact: `Assets/Generated/_Review/_StyleRefs/reference_knight_cell_contact.png`
- South style: `Assets/Generated/_Review/_StyleRefs/reference_knight_s_cell.png`
- East style: `Assets/Generated/_Review/_StyleRefs/reference_knight_e_cell.png`
- North style: `Assets/Generated/_Review/_StyleRefs/reference_knight_n_cell.png`
- West style: `Assets/Generated/_Review/_StyleRefs/reference_knight_w_cell.png`

Per-direction 4D pass:

- Contact: `Assets/Generated/_Review/oga4d_refknight_perdir_sw050_d070_walk_contact.png`
- Sheet: `Assets/Generated/_Review/oga4d_refknight_perdir_sw050_d070_walk_sheet.png`
- Manifest: `Assets/Generated/_Review/oga4d_refknight_perdir_sw050_d070_walk_sheet_manifest.json`

Per-direction verdict:

- South and west improved versus the single front-style reference.
- East and north remain unstable.
- North reads too front-facing, so the current reference-sheet direction mapping is not reliable enough.
- The next meaningful fix is not more random seeds. It is a stronger control path: either ControlNet/OpenPose conditioning or curated per-direction reference templates whose direction labels are verified before generation.

## Notes

This is a motion/direction LoRA source, not the final LIT-ISO visual style source. Its best job is to help the local generator understand 8-direction actions and frame continuity. Final style should still be refined from approved original LIT-ISO/Sprixen-quality project art.

Do not move raw OGA frames into Unity runtime folders unless final shipped credits include the required CC-BY attribution.
