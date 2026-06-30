# AGENTS.md — engineering contract

> Read at the start of every session. Keep it short. `CLAUDE.md` mirrors this file;
> **edit both if rules change.**
>
> **Single-AI project.** Earlier history references a second agent ("Codex") and
> split ownership "lanes" — that is obsolete. One AI owns the entire codebase. No
> lanes, no per-scene ownership. The constraints below (invariants + git) are the
> only hard rules.

## Goal
Build an **original** isometric survival-crafting LitRPG. Reference projects
(ISO-CORE / ISO-Tile) inform *system shape and scale* only — **never** copy their
pixels, audio, or content. Ship art is the **PixelLab-generated** set (owner decision,
2026-06); fill gaps to that style.

## Canonical pointers
- **Game design (source of truth):** `Docs/IsoCoreFoundation/00_Canonical_Game_Design.md`
  (supersedes bibles 15 & 17).
- **Current full handover:** `Docs/HANDOVER_2026-06-13.md`.
- **Prioritized to-do:** `Docs/IsoCoreFoundation/Impact_Analysis_2026-06.md`.
- **Doc map:** `Docs/INDEX.md`.

Canonical runtime: `IsoCore.Foundation` (built by `FoundationBootstrap`, scene
`Assets/Scenes/IsoCoreFoundation.unity`). The legacy `Assembly-CSharp` world is being
retired (`Docs/IsoCoreFoundation/22_Legacy_Retirement_Register.md`).

## Invariants — DO NOT "fix" these
- Grid `IsometricZAsY`, `cellSize (1,0.5,1)`.
- `transparencySortAxis (0,1,-0.26)`, `TilemapRenderer.mode = Individual`.
- Height layers `Height_0..7` = Unity layer `10+height`; sorting layer `10+height`.
- Movement is world-query based (foot collider is trigger-only).
- `maxWalkStepHeight = 1` — the player may **walk up one height step**; an active
  jump (`jumpClimbSteps`) stacks on top for taller cliffs. (Changed from `0` by owner
  direction, 2026-06-13; this is the current baseline.)

## Git workflow
1. `git pull --rebase` before starting.
2. Branch off `main` (e.g. `feat/<task>`). **Never commit to `main`. Never
   force-push.**
3. Small, single-purpose commits. Commit `.meta` files alongside their assets.
4. Open a PR; the human reviews; squash-merge; delete branch.
5. Never commit `Library/`, `.dotnet/`, or builds. Binaries (png/wav/fbx/ttf/…) are
   **Git LFS** (configured in `.gitattributes`) — only add them where `git-lfs` is
   installed.

One-time per machine (enables smart scene merge):
```
git config merge.unityyamlmerge.driver '"<UnityPath>/Tools/UnityYAMLMerge" merge -p %O %A %B %A'
```

## Imported Claude Cowork project instructions
