# ISO-Core Foundation — 09: Runtime Hardening

> Record of the adversarial runtime/play-mode review (5 parallel reviewers over the
> module) and the fixes applied. Editor validation (21/21) covers content/wiring but
> not play-mode behaviour; this pass closes that gap before manual play-test.

## Findings & resolutions (9 total: 1 blocker, 2 major, 6 minor)

| # | Sev | Area | Issue | Fix applied |
|---|---|---|---|---|
| 1 | **blocker** | `PlacementSystem` | Could place a solid block / blocking placeable on the player's **own cell** → permanent soft-lock (stuck in a blocked cell). | `CanPlace` now rejects blocking placement on the player's `CurrentCell`; **and** `IsoFoundationPlayer` auto-ejects toward the nearest walkable neighbour if ever standing in a blocked cell (defense-in-depth). |
| 2 | major | `IsoWorldController.PlayerChunk` | Streaming centre derived from the **height-lifted** transform via `WorldToCell` (which assumes the height-0 plane) → mis-centred chunk window on raised terrain (invisible ground/nodes). | Use the player's `CurrentCell` (computed from height-0 `Ground`) for the streaming centre. |
| 3 | major | `MobSpawner` | Spawn ring + despawn radius measured against height-lifted positions → skewed, terrain-dependent geometry. | Added `IsoFoundationPlayer.Ground`; spawner now measures against player `Ground` and each `Mob.Ground` (planar, height-0). |
| — | (mine) | `PlacementSystem.CursorCell` | Mouse→cell ignored tile height, so the ghost/target landed ~one cell off on raised tiles. | `CursorCell` refines by subtracting `height*HeightStep` before re-resolving the cell. |
| 4 | minor | `IsoFoundationPlayer` | No swept collision → a large frame delta could tunnel through a 1-cell-thick blocker. | Movement is now **sub-stepped** (≤0.2u per step, < `TileHalfW`). |
| 5 | minor | `IsoFoundationPlayer` | Up to ½-cell visual penetration into a blocked cell (rounding). | Accepted as cosmetic for the foundation; substep + face-stop noted as future polish. |
| 6 | minor | harvest overflow | `Inventory.Add` leftover ignored → harvested drops silently lost when full. | Harvest is blocked with an "Inventory full!" HUD flash when there is no free slot. |
| 7 | minor | `MobSpawner.PickMob` | Weighted pick could return a 0-weight entry when `Random.value`==0. | Skip `weight<=0` entries; strict `< 0` after accumulation. |
| 8 | minor | `FoundationBootstrap.SetupCamera` | Runtime-created camera kept `clearFlags=Skybox` → wrong background on the pure-runtime path. | Force `clearFlags = SolidColor`. |
| 9 | minor | `CraftingSystem.TryCraft` | Consumed inputs even when outputs wouldn't fit → net item loss. | `CanCraft` now also requires outputs to fit (`Inventory.CanFit`); craft button disables and never consumes on overflow. |

## New/changed APIs

- `IsoFoundationPlayer.Ground` (height-0 position) and auto-eject logic.
- `Inventory.HasEmptySlot()` / `Inventory.CanFit(itemId, count)`.
- `PlacementSystem.Init(..., IsoFoundationPlayer player)` and
  `MobSpawner.Init(..., IsoFoundationPlayer player)` (was `Transform`).

## Status

Content/editor validation remains **21/21** (no content change). All blocker/major
runtime issues resolved. The play-mode checklist in `06_Validation_Report.md` should
now pass; remaining items (½-cell visual penetration, world-drop for overflow) are
tracked as minor polish, not blockers.
