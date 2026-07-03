@echo off
cd /d C:\Projects\Unity-Projects\LIT-ISO
git push origin --delete claude/foundation-hud-binding claude/full-content-prep claude/hud-polish-and-tile-handoff claude/ingame-panels claude/land-session-drift claude/menu-save-hardening claude/prop-scale-ladder claude/session-batch-ui-worldgen codex/ability-affinity-core codex/foundation-ui-contract-clean codex/integrated-slice-validation codex/litrpg-foundation-systems codex/spriteforge-p1 codex/spriteforge-p3 feat/biome-asset-wiring
git fetch --prune
git branch -r
