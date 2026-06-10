# SpriteForge — Codex Handoff (P0 done, start at P1)

Date: 2026-06-10 · From: Claude Fable · Spec: `Docs/handoff/SPRITE_FORGE_SPEC.md`
Read the SPEC first; this file is current state + your next steps.

## State: what Claude already built (P0 complete, packer tested)

- `Tools/SpriteForge/` scaffold: `poses/` (VERSION 0.1.0-scaffold + README),
  `workflows/` (empty - yours), `out/` (gitignored), `README.md`.
- `poses/walk/action.json` — the action schema v1 example: frames/fps/loop,
  `anchor_frame` (idle anchoring, SPEC §2), `weapon_hand`, engine defaults,
  8 directions, `mirrorable` map (W-side from E-side for symmetric chars).
- `spriteforge.config.example.json` — engine-lane defaults (frames/video),
  postprocess + QA thresholds. Your P0 leftover: merge these blocks into the
  `asset_forge.local.json` convention so the comfy worker reads ONE config.
- `spriteforge_pack.py` — TESTED frames→sheet packer. Contract: uniform
  cells, shared-baseline alignment (preserves intentional bob, no clipping),
  bottom-center pivots, `sheet.json` sidecar with per-frame source bbox +
  cell rects + pose library version, `preview.png` contact strip.
  Run: `python3 spriteforge_pack.py --frames DIR --action-json poses/<a>/action.json`.
  Do not change the sidecar schema without bumping `lit-iso.spriteforge.sheet.v*`.

## Your work queue (small single-purpose commits, codex/spriteforge-* branches)

P1 — Action Pose Library v1 (GATE: pose sheets reviewed):
  idle(4f)+walk(6f), 8 directions, 512x512 transparent OpenPose renders at
  `poses/<action>/<direction>/frame_###.png`. Derive from your existing
  `Tools/AssetForge/build_litiso_openpose_direction_library.py` + LPC/OGA
  oracle builders — SKELETONS ONLY, never oracle pixels (license). Every
  action's frame 0 = the shared idle anchor pose. Bump poses/VERSION.

P2 — Lane A end-to-end, ONE character (witch idle ref), walk-S
  (GATE: Claude code+output review): extend the comfy worker with mode
  `animation`, engine `frames`: fan out per-frame sub-requests (ControlNet
  openpose template = P1 frame, style LoRA from config), collect to
  `out/<char>/<action>/<dir>/frames/`, run
  `Tools/AssetForge/proper_pixel_art_cleanup.py`, downscale to target_size,
  call spriteforge_pack.py. Request schema: SPEC §4.

P3 — Lane B (GATE: A/B comparison sheet): install One-to-All Animation via
  Kijai WanVideoWrapper nodes (fallback Wan 2.2 I2V), author
  `workflows/one_to_all_pose_i2v.json`, engine `video` path: ref image +
  pose sequence → frames → same cleanup/pack tail as lane A.

P4 — full matrix + QA (GATE: full witch set passes; Gary art-approves):
  remaining v1 actions (run/attack_swing/cast/hurt/death), all 8 dirs
  (honor `mirrorable`), wire `qa_direction_set.py` +
  `qa_against_direction_oracle.py` + pivot-drift/loop-seam checks as a
  pre-review gate per SPEC §7.

P5 — Dashboard tab (GATE: Claude UX review): queue cards, animated 8-dir
  preview, frame grid with per-frame reject → partial regen (lane A: rerun
  selected frame sub-requests same-seed; lane B: frame-window rerun),
  APPROVE → pack + stage install bundle via the existing approve flow.

## Claude's lane (do not build these)

Per-gate reviews; the Unity slicer/import editor utility (sheet.json →
`Assets/Characters/<Name>/AnimationSprites/<Action> <DIR>/` convention);
optimization passes after P5.

## Constraints (non-negotiable)

- Nothing generated enters `Assets/` without QA gate + dashboard approval.
- `out/` stays gitignored; binaries are committed only from Gary's machine.
- Don't touch `World/IsoTerrainSampler.cs` / `Core/FoundationContent.cs`
  (recently repaired after a tooling sync fault — see from-claude.md).
- Placeholder art in the game stays untouched (Gary's LoRA lane).
- VRAM: respect `pause_during_lora_training` when Gary trains.

## Known environment quirks

Claude's session mount intermittently serves truncated file reads; if a
file looks cut mid-function, check `git show HEAD:<path>` and the host copy
before "fixing" it. Your local environment is unaffected.
