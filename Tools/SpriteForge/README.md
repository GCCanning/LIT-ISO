# SpriteForge

Godmode-class sprite animation generation for LIT-ISO, built on the existing
AssetForge pipeline. Spec: `Docs/handoff/SPRITE_FORGE_SPEC.md`.
Codex handoff/state: `Docs/handoff/SPRITEFORGE_CODEX_HANDOFF.md`.

## Layout

- `poses/` — the Action Pose Library (versioned; see poses/README.md).
  `poses/<action>/<direction>/frame_###.png` + `poses/<action>/action.json`.
- `workflows/` — ComfyUI workflow JSON for engine lanes (lane B video).
- `out/` — generated jobs (GITIGNORED — nothing here enters git; approved
  assets are installed into Assets/ from Gary's machine only).
- `spriteforge_pack.py` — frames → sheet.png + sheet.json (+ preview strip).
- `build_action_pose_library.py` — deterministic P1 pose library builder.
- `validate_action_pose_library.py` — P1 gate validator for pose frames.
- `spriteforge.config.example.json` — SpriteForge-specific config; merge into
  asset_forge.local.json conventions is Codex P0 work.

## P2 lane-A command

This command writes review output only under `Tools/SpriteForge/out/witch/walk/S`.

```powershell
.\Tools\SpriteForge\run_lane_a_walk_s_witch.ps1
C:\Projects\ComfyUI\.venv\Scripts\python.exe Tools\SpriteForge\validate_lane_a_output.py --character witch --action walk --direction S
```

The human review image for the P2 gate is
`Tools/SpriteForge/out/witch/walk/S/preview_x4.png`.

For the P2 conditional-pass fix sweep:

```powershell
.\Tools\SpriteForge\run_lane_a_walk_s_witch_fix_sweep.ps1
```

The selected fix artifact is
`Tools/SpriteForge/out/p2_fix_sweep/d038_c062_bob/witch/walk/S/preview_x4.png`.

`d038_c062_bob` is the Lane A default after the P2 fix gate:

- `template_denoise`: `0.38`
- `style_weight`: `0.72`
- `control_strength`: `0.62`
- `palette_lock`: `true`

## P3 lane-B command

Lane B is the pose-driven video path. P3 installs/checks the
WanVideoWrapper/One-to-All stack and proves the video-frames -> cleanup ->
packer tail without importing anything into Unity.

```powershell
C:\Projects\ComfyUI\.venv\Scripts\python.exe Tools\SpriteForge\check_lane_b_stack.py
.\Tools\SpriteForge\run_lane_b_walk_s_witch.ps1 -FromLaneAFrames
```

Preferred Wan 2.2 5B model install:

```powershell
C:\Projects\ComfyUI\.venv\Scripts\python.exe Tools\SpriteForge\download_wan22_models.py
C:\Projects\ComfyUI\.venv\Scripts\python.exe Tools\SpriteForge\check_lane_b_stack.py
```

Default model set:

- `C:\Projects\ComfyUI\models\text_encoders\umt5-xxl-enc-bf16.safetensors`
- `C:\Projects\ComfyUI\models\diffusion_models\Wan2_2-TI2V-5B-FastWanFullAttn_bf16.safetensors`
- `C:\Projects\ComfyUI\models\vae\Wan2_2_VAE_bf16.safetensors`

P3 review artifacts:

- `Tools/SpriteForge/out/lane_b/p3_stack_report.json`
- `Tools/SpriteForge/out/lane_b/witch/walk/S/lane_b_manifest.json`
- `Tools/SpriteForge/out/lane_b/p3_ab_comparison.png`

The current gate is allowed to report `installed_restart_required`,
`installed_missing_models`, or `installed_restart_required_missing_models`;
live Wan video rendering is not enabled until the node classes are visible
after ComfyUI restart and the Wan model files exist under
`C:\Projects\ComfyUI\models`.

## Output contract (per character/action/direction)

```
out/<character>/<action>/<direction>/
  frames/frame_000.png ...   transparent, uniform canvas, aligned
  sheet.png                  single row, uniform cells, 1px padding
  sheet.json                 cell size, fps, loop, pivots, bounding boxes
  preview.png                contact strip for quick review
```

## Invariants

1. Every action's frame 0 derives from the shared idle anchor pose.
2. Nothing reaches `Assets/` without passing QA gates + dashboard approval.
3. Pose skeletons may derive from LPC/OGA oracle datasets; their PIXELS may
   never ship (license hygiene).
4. `out/` stays out of git.

## P1 pose-library commands

Use the project-local Python that has Pillow available.

```powershell
C:\Projects\ComfyUI\.venv\Scripts\python.exe Tools\SpriteForge\build_action_pose_library.py --replace
C:\Projects\ComfyUI\.venv\Scripts\python.exe Tools\SpriteForge\validate_action_pose_library.py
```

The validator writes `poses/p1_gate_report.json`. The P1 review gate is human
eyeballing of `poses/idle/idle_pose_contact_sheet.png` and
`poses/walk/walk_pose_contact_sheet.png` after the automated report passes.
