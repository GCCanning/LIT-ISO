# SpriteForge — Implementation Spec & Production Plan

Owner: Codex (implementation) · Claude Fable (review/optimization/cleanup per gate)
Status: APPROVED FOR BUILD (Gary, 2026-06-10)
Goal: a godmodeai-class sprite-generation tool for LIT-ISO — single character
image in, production-ready 8-direction animated sprite sheets out — built on
the EXISTING AssetForge/LoRA pipeline, not from scratch.

## 0. What we are building (and not building)

IN: action-pose-library-driven character animation generation; two engine
lanes (A: existing frame-by-frame ControlNet path, B: pose-driven video model
path); pixel-art post-processing; sprite-sheet packing with bounding boxes;
a review/approve UI; one-click install into the game's Resources convention.
Tiles/props/icons keep using the existing tile/prop modes — same queue, same
review gates — SpriteForge adds the ANIMATION capability.

OUT (non-goals): public SaaS, billing, accounts, arbitrary-style generality,
3D, cloning godmodeai's UI. Quality target: match godmode output for OUR
locked art style at OUR resolutions, not in general.

## 1. What already exists (reuse, do not rebuild)

- `Tools/AssetForge/comfy_generation_worker.py` + `sprixen_generation_worker.py`
  — queue-driven ComfyUI workers; request JSON with modes
  {tile, prop, item, character, npc, mob}; manifest output. EXTEND with an
  `animation` mode; do not fork.
- `asset_forge.local(.example).json` — machine-local config (ComfyUI url/root,
  per-mode checkpoint/LoRA defaults). Add an `animation` mode_defaults block
  and a `video_models` section (see §3).
- OpenPose direction library: `build_litiso_openpose_direction_library.py` —
  the seed of the Action Pose Library (§2).
- Direction oracles + QA: `build_lpc_direction_oracle.py`,
  `build_oga_8d_character_oracle.py`, `qa_direction_set.py`,
  `qa_against_direction_oracle.py` — reuse as automated acceptance checks.
- Pixel cleanup: `proper_pixel_art_cleanup.py` (+ runner) — the post-process
  pass for lane B video frames.
- Sheet tooling: `pack_review_spritesheet.py`, `extract_sprite_sheet_cell.py`,
  `recover_sprite_frames_from_sheet.py`, `build_sprite_sheet_cell_contact.py`.
- Review flow: `build_review_pack.ps1` / `approve_review_pack.ps1` /
  `capture_approved_review_pack.py`, `Dashboard/` + `serve_dashboard.ps1`.
- Motion LoRAs/datasets: `LoRA/start_oga_8d_motion_training.ps1`,
  `start_lpc_motion_template_training.ps1`, sprixen frame training — lane A
  style/motion conditioning.

## 2. Action Pose Library (the consistency backbone)

This is what makes godmode's output coherent: every action is generated
against a CANNED skeletal pose sequence, anchored on a shared idle pose.

- Format: `Tools/SpriteForge/poses/<action>/<direction>/frame_###.png`
  (OpenPose skeleton renders, 512x512, transparent bg) + `action.json`
  (frame count, fps, loop flag, anchor frame index, weapon hand tag).
- Directions: 8 (S, SE, E, NE, N, NW, W, SW) — matching the game's needs;
  the game currently uses 4+idle, generate 8 so we never redo this.
- v1 actions (priority order): idle (4f), walk (6f), run (6f), attack_swing
  (6f), cast (6f), hurt (3f), death (5f). ALL start from the shared idle
  pose frame (godmode's V3 lesson — anchoring kills drift).
- Sources: derive from the existing LPC/OGA oracle datasets (already
  normalized, license-clean) via the existing oracle builders; hand-fix in
  the dashboard where joints read wrong. Build once, version it
  (`poses/VERSION`), reuse forever.

## 3. Engine lanes

LANE A — frame-by-frame (exists, productionize): per-frame ControlNet
(OpenPose template from §2) + style LoRA via the comfy worker. Best for
small/pixel sprites; already proven by the black-mage runs. Work: wire
`animation` requests to fan out N frame sub-requests + collect.

LANE B — pose-driven video (new, the godmode method): One-to-All Animation
(CVPR 2026, ComfyUI integration via Kijai's WanVideoWrapper; runs in 16 GB
VRAM) and/or Wan 2.2 I2V as fallback. Input: character ref image + §2 pose
sequence; output: frames → `proper_pixel_art_cleanup` → downscale to target
sprite size. Work: install nodes, author the ComfyUI workflow JSON, teach
the worker an `engine: "video"` request flavor, frame extraction.

Per-action engine choice lives in `action.json` defaults and is overridable
per request. Expect lane A to win at 32-64 px sprites, lane B at 96 px+.

## 4. Request schema (extends the existing worker contract)

```json
{
  "mode": "animation",
  "engine": "frames | video",
  "character_ref": "path/to/idle.png",
  "action": "walk",
  "directions": ["S","SE","E","NE","N","NW","W","SW"],
  "style_profile": "litiso_iso_reference_v1",
  "target_size": 64,
  "seed": 1207,
  "output": "Tools/SpriteForge/out/<character>/<action>/"
}
```
Output contract per direction: `frames/frame_###.png` (transparent,
aligned), `sheet.png` (single row, uniform cells), `sheet.json` (cell size,
fps, loop, per-frame pivot + bounding boxes), `preview.webp`.

## 5. Sheet packer + Unity install

- Packer: extend `pack_review_spritesheet.py` → `spriteforge_pack.py`:
  uniform cells sized to the max frame bbox + 1px padding, bottom-center
  pivot, deterministic order, `sheet.json` sidecar.
- Unity import: a small editor utility (Claude's lane, review phase) that
  slices `sheet.png` by `sheet.json` into the existing animation convention
  (`Assets/Characters/<Name>/AnimationSprites/<Action> <DIR>/...` — same
  layout the Witch uses) so existing animators bind without code changes.
- INVARIANT: nothing lands in `Assets/` without passing the §7 review gate.
  Generated work stays in `Tools/SpriteForge/out/` (gitignored) until
  approved.

## 6. Review UI (extend the existing Dashboard)

Add a SpriteForge tab to `Tools/AssetForge/Dashboard/`:
- queue status (reads worker manifests), per-job cards;
- animated preview (play sheet at fps, all 8 directions side by side);
- frame grid with per-frame REJECT toggles → "regenerate selected frames"
  (lane A: rerun those frame sub-requests, same seed elsewhere; lane B:
  rerun with frame-window conditioning) — this is godmode's partial regen;
- APPROVE button → runs packer → stages an install bundle + review pack
  via the existing approve flow.

## 7. QA gates (automated before human review)

Run `qa_direction_set.py` + `qa_against_direction_oracle.py` on every job:
direction correctness (oracle match), frame alignment jitter (pivot drift
<= 1px), palette compliance vs style profile, transparent-bg integrity,
loop seam check (first/last frame delta). Jobs failing QA never reach the
dashboard as "ready" — they show as "needs regen" with the failing metric.

## 8. Production phases (Codex builds; Claude reviews at each gate)

P0 (0.5d): scaffold `Tools/SpriteForge/` (poses/, workflows/, out/), add
  `animation` mode to worker schema + config example. GATE: schema review.
P1 (1-2d): Action Pose Library v1 — idle+walk, 8 directions, from oracle
  datasets; `action.json` format + VERSION. GATE: pose sheets eyeballed.
P2 (1-2d): Lane A end-to-end for ONE character (witch ref) walk-S:
  fan-out, collect, cleanup, pack. GATE: Claude code review + output review.
P3 (1-2d): Lane B install (One-to-All / WanVideoWrapper nodes), workflow
  JSON, video→frames→cleanup path, same packer. GATE: A/B comparison sheet
  walk-S vs lane A; pick per-size defaults.
P4 (1d): full 8-direction matrix + remaining v1 actions; QA gates wired.
  GATE: full witch set passes QA; Gary art-approves.
P5 (1-2d): Dashboard tab + partial regen + approve/install bundle.
  GATE: Claude UX/code review.
P6 (ongoing): style LoRA integration as Gary's training lands; black-mage
  character run; mob set (slime/deer/fox upgrade per audit).
Total: ~6-9 working days to a complete v1.

## 9. Risks & mitigations

- Video-model identity drift at tiny sizes → lane A is the default below
  64px; downscale hides lane B artifacts above that.
- VRAM contention with LoRA training → worker already queues; add a config
  flag to pause animation jobs while training runs (reuse pause_litiso
  pattern).
- Pose library quality ceiling → it IS the product; budget hand-fix time in
  P1 and version it.
- License hygiene: LPC/OGA datasets carry CC/GPL art licenses — fine for
  POSE SKELETONS (no pixels reused); never ship oracle pixels. Note in
  poses/README.
- Repo hygiene: generated outputs are large/binary — `Tools/SpriteForge/out/`
  goes in .gitignore; only approved, installed assets enter git (LFS), and
  only from Gary's machine.

## 10. Division of labor

- Codex: everything in §8 P0-P5 inside Tools/SpriteForge + worker extension.
  Small single-purpose commits per phase on codex/spriteforge-* branches.
- Claude Fable: per-gate review (correctness, perf, style, dead code),
  the Unity slicer/import utility (§5), optimization passes, and this spec's
  upkeep. Findings go to Docs/agent-comms/from-claude.md per convention.
- Gary: art approval at P4/P6 gates; owns style LoRA training; pushes
  binaries.
