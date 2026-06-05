# Task Ledger

> Claim a task here (add a row) BEFORE you start it, so the other agent doesn't grab
> it. Keep your own rows updated. Status: TODO / WIP / REVIEW / DONE.
> Edit carefully and `git pull --rebase` first to avoid clobbering the other's rows.

| Task | Owner | Branch | Status | Notes |
|---|---|---|---|---|
| Repo + collaboration setup | Claude | claude/repo-setup | REVIEW | git/LFS/AGENTS/comms; pending push |
| FoundationBootstrap seed/world launch API | Codex | codex/integrated-slice-validation | DONE | ConfigureLaunch API validated; old codex/foundation-bootstrap-api branch is stale |
| Validate Foundation play-test (doc 06 checklist) | Codex | codex/integrated-slice-validation | DONE | automated integrated gate 37/37 + Foundation validator 24/24; human feel pass still useful |
| Milestone A1 - terrain-top art (original) | Codex | - | TODO | grass/dirt/sand/snow tops, PPU 16 |
| Port welcome menu -> load IsoCoreFoundation.unity | Claude | claude/menu-port | REVIEW | validated by Codex integrated gate; ready for merge orchestration |
| Build settings -> IsoCoreFoundation (shared cfg) | Claude | claude/menu-port | REVIEW | slot 1 repoint validated by Codex integrated gate |

| Menu visual style-spec (palette-agnostic) | Claude | claude/menu-style-spec | MERGED | `Docs/menu-style-spec.md`; execute after A1 sets palette |
| Harden world-save (sanitize names, surface failures) | Claude | claude/menu-save-hardening | REVIEW | metadata-save robustness; PR ready |
| In-game UI: left HUD + camera-scale + settings + back nav | Codex | - | TODO | spec: `Docs/ingame-ui-spec.md` (FoundationHUD) |
| Real save/load (FoundationSaveData) cross-lane | Codex+Claude | - | TODO | Codex: serialize state + save trigger; Claude: menu Load/Continue. Agree file format + PlayerPrefs keys |

## Up next (unclaimed)
- A2 terrain block faces (original art).
- Save/load `FoundationSaveData`.
- Survival-scope decision (energy/temperature in or out).
