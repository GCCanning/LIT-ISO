# Task Ledger

> Claim a task here (add a row) BEFORE you start it, so the other agent doesn't grab
> it. Keep your own rows updated. Status: TODO / WIP / REVIEW / DONE.
> Edit carefully and `git pull --rebase` first to avoid clobbering the other's rows.

| Task | Owner | Branch | Status | Notes |
|---|---|---|---|---|
| Repo + collaboration setup | Claude | claude/repo-setup | MERGED | git/LFS/AGENTS/comms |
| FoundationBootstrap seed/world launch API | Codex | codex/integrated-slice-validation | MERGED | ConfigureLaunch API validated |
| Validate Foundation play-test (doc 06 checklist) | Codex | codex/integrated-slice-validation | DONE | automated 37/37 + 24/24; human feel pass still useful |
| Milestone A1 - terrain-top art (original) | Codex | - | TODO | grass/dirt/sand/snow tops, PPU 16 |
| Port welcome menu -> load IsoCoreFoundation.unity | Claude | claude/menu-port | MERGED | validated by Codex integrated gate |
| Build settings -> IsoCoreFoundation (shared cfg) | Claude | claude/menu-port | MERGED | slot 1 repoint validated |
| Menu visual style-spec (palette-agnostic) | Claude | claude/menu-style-spec | MERGED | `Docs/menu-style-spec.md`; execute after A1 sets palette |
| Harden world-save (sanitize names, surface failures) | Claude | claude/menu-save-hardening | MERGED | metadata-save robustness; merged |
| In-game UI = skinnable uGUI (owner decision) | Claude | claude/ingame-ui | MERGED | HUD bar done (`GameUIController`); foundation binding follows on `claude/foundation-hud-binding` |
| Expose Foundation UI binding contract | Codex | codex/foundation-ui-contract-clean | MERGED | Ready event + Inventory/Hotbar/Content/World handles + optional IMGUI HUD |
| Foundation HUD binding | Claude | claude/foundation-hud-binding | REVIEW | Adapter binds uGUI HUD to Foundation runtime; placeholder HP/MP/XP until Codex stats source |
| Real save/load (FoundationSaveData) cross-lane | Codex+Claude | - | TODO | Codex: serialize state + save trigger; Claude: menu Load/Continue. Agree file format + PlayerPrefs keys |

## Up next (unclaimed)
- A2 terrain block faces (original art).
- Save/load `FoundationSaveData`.
- Survival-scope decision (energy/temperature in or out).
