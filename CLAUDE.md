# CLAUDE.md

Read **`AGENTS.md`** first — it is the shared contract (goal, canonical decision,
invariants, ownership lanes, git workflow, agent comms). This file just mirrors the
non-negotiables so they're always in context.

- Canonical game = `IsoCore.Foundation` (Track B). Legacy `Assembly-CSharp` is being retired.
- **Invariants (do not "fix"):** `IsometricZAsY`, `cellSize (1,0.5,1)`,
  sort axis `(0,1,-0.26)`, `TilemapRenderer Individual`, `Height_N = layer 10+N`,
  world-query movement (trigger-only foot collider), `maxWalkStepHeight=0`.
- **My lane:** menu / art / integration. **Codex's lane:** `IsoCoreFoundation/**` +
  its scene. Never edit Codex's lane or its scene without a handoff.
- **Git:** pull→branch (`claude/<task>`)→small PR. Never commit to `main`. Binaries are LFS.
- Start-of-session reading: `Docs/INDEX.md`, `Docs/agent-comms/from-codex.md`, `Docs/agent-comms/task-ledger.md`.

All assets we ship are **original** — clone ISO-CORE's structure, never its content.
