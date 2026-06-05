# AGENTS.md — shared contract for Claude & Codex

> Both agents read this at the start of every session. Keep it short.
> `CLAUDE.md` is a pointer to this file; **edit both if rules change.**

## Goal
Build an **original** cozy isometric survival/crafting/building game, using the
ISO-CORE/ISO-Tile reference only for *system shape and scale* — **never** copy its
pixels, audio, or content. All art/audio we ship is authored fresh (see
`Docs/IsoCoreFoundation/10_CleanRoom_Clone_Backlog.md`).

## Canonical decision
`IsoCore.Foundation` (Track B) is the **canonical** game. The legacy
`Assembly-CSharp` world is being retired. Full context:
`Docs/HANDOFF_NEXT_SESSION.md`. Doc map: `Docs/INDEX.md`.

## Invariants — DO NOT "fix" these
- Grid `IsometricZAsY`, `cellSize (1,0.5,1)`.
- `transparencySortAxis (0,1,-0.26)`, `TilemapRenderer.mode = Individual`.
- Height layers `Height_0..7` = Unity layer `10+height`; sorting layer `10+height`.
- Movement is world-query based (foot collider is trigger-only). `maxWalkStepHeight=0`.

## Ownership lanes — never edit the other agent's lane without a handoff
| Lane | Owner | Paths |
|---|---|---|
| Foundation (canonical) | **Codex** | `Assets/Scripts/IsoCoreFoundation/**`, `Assets/Scenes/IsoCoreFoundation.unity`, `Docs/IsoCoreFoundation/**` |
| Menu / art / integration | **Claude** | `Assets/Scripts/UI/**`, `World/WorldManager`, `GameStartupManager`, `Assets/Art/**`, `Assets/Resources/**` |
| Shared — tiny PRs, announce first | either | `ProjectSettings/**`, `Packages/manifest.json`, `*.gitattributes`, `*.gitignore`, this file |

**One owner per `.unity` scene. Never have both agents in the same scene.**

## Git workflow
1. `git pull --rebase` before starting.
2. Branch: `claude/<task>` or `codex/<task>`. **Never commit to `main`.**
3. Small, single-purpose commits. Commit `.meta` files alongside their assets.
4. Open a PR; the other agent or the human reviews; squash-merge; delete branch.
5. **Never force-push a shared branch.** Never commit `Library/`, `.dotnet/`, builds.
6. Binaries (png/wav/fbx/…) are Git LFS — already configured in `.gitattributes`.

One-time per machine (enables smart scene merge):
```
git config merge.unityyamlmerge.driver '"<UnityPath>/Tools/UnityYAMLMerge" merge -p %O %A %B %A'
```

## Talking to each other (async)
- Append a note for the other agent in your own file (no shared-file conflicts):
  `Docs/agent-comms/from-claude.md` (Claude writes) / `from-codex.md` (Codex writes).
- Claim/track work in `Docs/agent-comms/task-ledger.md` before starting a task.
- Read the other agent's file + the ledger at session start.
