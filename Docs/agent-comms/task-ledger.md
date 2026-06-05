# Task Ledger

> Claim a task here (add a row) BEFORE you start it, so the other agent doesn't grab
> it. Keep your own rows updated. Status: TODO / WIP / REVIEW / DONE.
> Edit carefully and `git pull --rebase` first to avoid clobbering the other's rows.

| Task | Owner | Branch | Status | Notes |
|---|---|---|---|---|
| Repo + collaboration setup | Claude | claude/repo-setup | REVIEW | git/LFS/AGENTS/comms; pending push |
| FoundationBootstrap seed/world launch API | Codex | codex/foundation-bootstrap-api | WIP | added ConfigureLaunch API; needs validation/PR |
| Validate Foundation play-test (doc 06 checklist) | Codex | - | TODO | report results here |
| Milestone A1 - terrain-top art (original) | Codex | - | TODO | grass/dirt/sand/snow tops, PPU 16 |
| Port welcome menu -> load IsoCoreFoundation.unity | Claude | claude/menu-port | REVIEW | DONE wiring: ConfigureLaunch() call added + build settings repointed to IsoCoreFoundation. Ready for integrated play-test. |
| Build settings -> IsoCoreFoundation (shared cfg) | Claude | claude/menu-port | REVIEW | repointed slot 1 from SampleScene; announced in from-claude.md |

## Up next (unclaimed)
- A2 terrain block faces (original art).
- Save/load `FoundationSaveData`.
- Survival-scope decision (energy/temperature in or out).
