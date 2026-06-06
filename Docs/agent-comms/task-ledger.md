# Task Ledger

> Claim a task here (add a row) BEFORE you start it, so the other agent doesn't grab
> it. Keep your own rows updated. Status: TODO / WIP / REVIEW / DONE / MERGED.
> Edit carefully and `git pull --rebase` first to avoid clobbering the other's rows.

| Task | Owner | Branch | Status | Notes |
|---|---|---|---|---|
| Repo + collaboration setup | Claude | claude/repo-setup | MERGED | git/LFS/AGENTS/comms |
| FoundationBootstrap seed/world launch API | Codex | codex/integrated-slice-validation | MERGED | ConfigureLaunch API validated |
| Validate Foundation play-test (doc 06 checklist) | Codex | codex/integrated-slice-validation | MERGED | automated 37/37 + 24/24 |
| Port welcome menu -> IsoCoreFoundation.unity | Claude | claude/menu-port | MERGED | ConfigureLaunch wired in LaunchWorld |
| Build settings -> IsoCoreFoundation (shared cfg) | Claude | claude/menu-port | MERGED | slot 1 repoint validated |
| Menu visual style-spec | Claude | claude/menu-style-spec | MERGED | Docs/menu-style-spec.md |
| Harden world-save (sanitize names, surface failures) | Claude | claude/menu-save-hardening | MERGED | metadata-save robustness |
| In-game HUD uGUI | Claude | claude/ingame-ui | MERGED | GameUIController + skinnable HUD bars |
| Expose Foundation UI binding contract | Codex | codex/foundation-ui-contract-clean | MERGED | Ready event + Inventory/Hotbar/Content handles |
| Foundation HUD binding | Claude | claude/foundation-hud-binding | MERGED | FoundationHudAdapter wires HUD to Foundation runtime |
| Commit UI metas | Claude | claude/fix-ui-metas | MERGED | orphaned .meta files |
| Game-feel batch (animation/audio/FX/pause/ambient) | Codex | codex/game-feel-batch | MERGED | PlayerAnimator, AmbientLightController, PauseMenu, SfxManager, WorldFx etc. |
| Resources/audio/items (Claude art lane) | Claude | claude/resources-audio-items | MERGED | item icons, SFX, music, decorations, SpriteAmbient shader |
| Tilemap/AssetForge/docs (Codex art lane) | Codex | codex/tilemap-assetforge-docs | MERGED | plains+forest tiles, props, AssetForge pipeline, LoRA tools |
| Tools/scripts | Claude | claude/scripts-and-tools | MERGED | PowerShell helpers, Plans/ |
| HUD live stats binding | Claude | claude/hud-live-stats | MERGED | FoundationHudAdapter wired to PlayerHealth/Mana/XPSystem events |
| Options screen with real volume controls | Claude | claude/options-screen | WIP | mirrors PauseMenu sliders (master/sfx/music via PlayerPrefs) |
| Commit remaining working-tree drift | Claude | claude/working-tree-cleanup | TODO | modified metas, BiomeDefinition_Plains, Packages, ProjectSettings |
| Real save/load (FoundationSaveData) cross-lane | Codex+Claude | - | TODO | Codex: serialize world state; Claude: wire Continue in menu. Agree file format first |
| Milestone A1 terrain-top art (original) | Codex | - | TODO | grass/dirt/sand/snow tops, PPU 16 |
| Asset Forge Unity reimport | Codex | - | TODO | Tools > Asset Forge > Reimport Generated Assets (needs Unity open) |
| AI Toolkit folder decision | Both | - | TODO | commit (LFS) or .gitignore the Unity AI plugin |
| GeneratedAssets UUID folders decision | Both | - | TODO | 22 UUID folders of AI-generated WAV; commit or ignore |
