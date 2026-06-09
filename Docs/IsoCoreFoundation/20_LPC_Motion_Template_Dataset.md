# LPC Motion Template Dataset

## Purpose

This dataset is for humanoid motion, direction, frame order, and tool/action captioning.
It is not final LIT-ISO actor art.

## Generated Batch

Review source:

`Assets/Generated/_Review/LPC_MaleFemaleTrainingBatch_v2`

LoRA-ready dataset:

`C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\lpc_motion_male_female_v1`

Counts:

- 18 composed source sheets
- 2,008 sliced PNG frames
- 2,008 matching text captions
- 1,808 train images
- 200 validation images

Included bases:

- male leather adventurer
- female forest scout

Included tools:

- none
- axe
- hammer
- pickaxe
- hoe
- shovel
- watering
- rod
- longsword slash

Included actions:

- spellcast
- thrust
- walk
- slash
- shoot
- hurt

Included directions:

- north
- west
- south
- east
- hurt only has south because that is the LPC row available in this sheet layout.

## Training Launcher

Dry run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\start_lpc_motion_template_training.ps1 -DryRun
```

Start:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\start_lpc_motion_template_training.ps1
```

Pause:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\pause_litiso_training.ps1 -OutputName litiso_lpc_motion_template_v1
```

Status:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\status_litiso_training.ps1 -OutputName litiso_lpc_motion_template_v1
```

Resume:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\LoRA\start_lpc_motion_template_training.ps1 -ResumeLatest
```

## Use In Pipeline

Use this LoRA as a motion/template helper only. It should not be the final style authority.

The intended sequence is:

1. Train/evaluate `litiso_lpc_motion_template_v1`.
2. Generate a small actor-motion evaluation sheet.
3. Compare against approved LIT-ISO actor references.
4. Add a separate LIT-ISO style LoRA or reference-conditioning pass.
5. Only then attempt production player/NPC/mob generation.

## License Note

The dataset package copies LPC source credits and license files into `provenance/`.
Do not ship generated derivatives without preserving attribution and confirming license compatibility.
