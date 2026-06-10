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
