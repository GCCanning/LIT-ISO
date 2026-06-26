# Docs Index — start here

Navigation hub. Open only what your current task needs (keeps context lean).

## Always read at session start
- [`/AGENTS.md`](../AGENTS.md) — shared contract: lanes, invariants, git, comms.
- **[`IsoCoreFoundation/00_Canonical_Game_Design.md`](IsoCoreFoundation/00_Canonical_Game_Design.md)
  — CANONICAL game design. Source of truth. Supersedes bibles 15 & 17.**
- [`agent-comms/task-ledger.md`](agent-comms/task-ledger.md) — who's doing what now.
- The other agent's note: [`agent-comms/from-codex.md`](agent-comms/from-codex.md)
  / [`agent-comms/from-claude.md`](agent-comms/from-claude.md).

## Where things are
- **CURRENT full handover (feed this to a fresh AI):**
  [`HANDOVER_2026-06-13.md`](HANDOVER_2026-06-13.md)
- **Big-picture state & next steps (historical):** [`HANDOFF_NEXT_SESSION.md`](HANDOFF_NEXT_SESSION.md)
  — superseded by the handover above.
- **Canonical game (Foundation) design:** [`IsoCoreFoundation/`](IsoCoreFoundation/)
  - **`00_Canonical_Game_Design.md` — THE source of truth for game design
    (Seven-Day-Trial frame + cozy survival-crafting substance). Read first.**
  - `01_Project_Orientation.md` — audit of the legacy codebase.
  - `03_Foundation_Architecture.md` — how the Foundation is built.
  - `06_Validation_Report.md` — editor checks + manual play-mode checklist.
  - `07_Migration_Plan.md` — legacy → Foundation disposition.
  - `10_CleanRoom_Clone_Backlog.md` — **the art/content roadmap (Milestones A/B/C)**.
  - `15_LitRPG_System_Bible.md` — **SUPERSEDED by 00** (kept for detailed system
    tables: skills, items, quests, biomes, tile names). Cozy framing only.
  - `17_Seven_Day_Trial_Game_Bible.md` — **SUPERSEDED by 00** (kept for trial-spine
    detail: Evidence/Marks, classes, professions, grades). Trial framing only.
  - `18_Biome_Generation_And_Asset_Placement_Rules.md` — generated tile/prop
    review gates, biome placement rules, transition policy, and Foundation
    import sequence.
  - `22_Legacy_Retirement_Register.md` — what to keep, re-author into
    Foundation, quarantine, or retire as the legacy Assembly-CSharp world winds
    down.

## Editor entry points (Unity menu)
- Foundation: `Tools/LIT-ISO/ISO-Core Foundation/{Build Foundation Scene, Generate
  Content Assets, Validate Foundation, Run Golden Path}`.
- Legacy (retiring): `Tools/LIT-ISO/Setup/Full Golden Path Setup`.

> Root-level `*.md` files (ASSET_CATALOG, GOLDEN_PATH_COMPLETE, etc.) are older
> working notes. Treat `Docs/` as the source of truth; consolidate/prune later.
</content>
