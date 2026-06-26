# CLAUDE.md

Read **`AGENTS.md`** (engineering contract) and
**`Docs/HANDOVER_2026-06-13.md`** (full handover) first. This file mirrors the
non-negotiables so they're always in context.

- **You are the sole AI on this project.** Ignore any "lanes" / "Codex" / second-agent
  references in older docs — you own the entire codebase.
- Canonical game = `IsoCore.Foundation`. Legacy `Assembly-CSharp` is being retired.
- **Design source of truth:** `Docs/IsoCoreFoundation/00_Canonical_Game_Design.md`.
  **Prioritized to-do:** `Docs/IsoCoreFoundation/Impact_Analysis_2026-06.md`.
- **Art:** ship the **PixelLab-generated** tiles/props/assets; fill gaps to that style.
- **Invariants (do not "fix"):** `IsometricZAsY`, `cellSize (1,0.5,1)`,
  sort axis `(0,1,-0.26)`, `TilemapRenderer Individual`, `Height_N = layer 10+N`,
  world-query movement (trigger-only foot collider), `maxWalkStepHeight=1`
  (walk up one step; jump stacks for taller cliffs).
- **Git:** pull → branch off `main` (`feat/<task>`) → small PR. Never commit to
  `main`. Binaries are Git LFS (only add where `git-lfs` is installed).
- Start-of-session reading: `Docs/HANDOVER_2026-06-13.md`, then `Docs/INDEX.md`.

All assets we ship are **original** — clone ISO-CORE's structure, never its content.
