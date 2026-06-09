# Legacy Retirement Register

Canonical runtime: `Assets/Scripts/IsoCoreFoundation/**` plus the menu/uGUI bridge needed to launch and display it.

This register tracks old `Assembly-CSharp` systems that should not be expanded. Useful ideas can be re-authored into Foundation, but the old runtime code should be quarantined once the canonical UI/menu no longer references it.

Current owner decision: merge or extract anything useful into Foundation first, then retire everything that remains redundant. Do not preserve old game modes for their own sake.

## Retirement Workflow

1. Confirm the feature exists in Foundation or write the Foundation replacement.
2. Move useful design notes, data names, or validator expectations into `Docs/IsoCoreFoundation/**`.
3. Add or extend a Foundation validator check so the replacement cannot regress.
4. Remove canonical references to the legacy code.
5. Quarantine/delete the retired legacy scripts, scenes, and generated assets in a separate cleanup PR.

Never delete a legacy folder just because it looks old. Delete it after a Foundation equivalent is validated or the register marks it as design-only noise.

## Keep For Canonical Flow

| Path | Reason | Exit condition |
|---|---|---|
| `Assets/Scripts/UI/WelcomeScreenManager.cs` | Canonical menu shell; calls `FoundationBootstrap.ConfigureLaunch(...)` and loads `IsoCoreFoundation`. | Keep; migrate its local world-list metadata to Foundation save metadata later. |
| `Assets/Scripts/UI/InGame/**` | uGUI HUD/panels/adapters for Foundation. | Keep; adapters should bind Foundation handles only. |
| `Assets/Scripts/Core/SystemNotifier.cs` | Current Foundation notification bridge uses this global UI channel. | Replace with a Foundation-owned system feed view, then retire. |
| `Assets/Scripts/Editor/BuildSettingsConfigurator.cs` | Maintains canonical build scene order. | Keep if it points only at `MenuScene` and `IsoCoreFoundation`. |
| `Assets/Scripts/Editor/GoldenPathTools.cs` | Useful editor entry point if it remains Foundation-first and optional for quarantined tools. | Keep only Foundation-safe menu items. |

## Re-Author Into Foundation

| Legacy area | Existing path | Foundation destination |
|---|---|---|
| Weather | `Assets/Scripts/World/Weather/**` | `IsoCoreFoundation/World` plus save DTO weather layer. |
| Towns/settlement | `Assets/Scripts/Towns/**` | `IsoCoreFoundation/Settlement` or `IsoCoreFoundation/Building`. |
| Economy/vendors | `Assets/Scripts/Economy/**` | Foundation NPC/settlement economy, data-first. |
| Combat/spells/status effects | `Assets/Scripts/Combat/**` | Foundation mobs/player actions/progression evidence. |
| Guild board | `Assets/Scripts/Guilds/**` | Foundation quest/guild-board definitions already started in `FoundationContent`. |
| Dungeon concepts | `Assets/Scripts/World/Dungeons/**`, `Assets/Scripts/DungeonInstanceSystem.cs` | Foundation modular dungeon system. |
| Starter-zone/tutorial ideas | `Assets/Scripts/World/StarterZoneGenerator.cs`, `Assets/Scripts/Quests/TutorialSequence.cs` | Foundation tutorial notifier, pinned goals, starter-region generator. |
| World metadata/save list | `Assets/Scripts/World/WorldManager.cs` | Foundation save metadata service and menu world-select adapter. |

## Retire After Extraction

These are superseded by Foundation and should be moved to a non-Unity archive or deleted after any useful design notes are copied into docs:

- `Assets/Scripts/Crafting/**`
- `Assets/Scripts/Gameplay/**`
- `Assets/Scripts/Player/**`
- `Assets/Scripts/Quests/**`
- `Assets/Scripts/Phase2*.cs`
- `Assets/Scripts/*_Phase2.cs`
- `Assets/Scripts/IsoWorldChunkManager.cs`
- `Assets/Scripts/IsoPlayerController.cs`
- `Assets/Scripts/Isometric*.cs`
- `Assets/Scripts/ProceduralIsoTilemapGenerator.cs`
- `Assets/Scripts/WorldSeedManager.cs`
- `Assets/Scripts/ZoomController.cs`
- `Assets/Scripts/SunController.cs`
- `Assets/Scripts/SceneValidator.cs`
- `Assets/Scripts/ScoringWeightCalculator.cs`
- `Assets/Scripts/ProximityPenaltySystem.cs`
- `Assets/Scripts/ActionTracker.cs`
- `Assets/Scripts/AdventurerPlayerSetup.cs`

## Current Extraction Status

| Area | Status | Notes |
|---|---|---|
| Inventory/hotbar/crafting | Extracted | Foundation has `Inventory`, `Hotbar`, `CraftingSystem`, and uGUI/IMGUI adapters. Retire legacy equivalents after menu save metadata no longer needs them. |
| Character stats/progression/quests | Extracted | Foundation progression, stats, callings, quest read state, and hooks are canonical. Retire old `PlayerStats`, `XPSystem`, and `QuestManager` references from UI. |
| Tavern/interiors/portals/dungeons | Partially extracted | Foundation has pocket instances, tavern, portals, deterministic dungeon layout, and dungeon save history. Keep extracting encounter/reward ideas only. |
| Weather/survival pressure | Not extracted | Treat legacy weather as design reference only; implement fresh Foundation weather with save DTOs. |
| Town/economy/NPCs | Not extracted | Keep concepts only; implement new Foundation settlement/economy/NPC definitions. |
| Menu world list/save metadata | Partially detached | `WelcomeScreenManager` now launches only through `FoundationBootstrap.ConfigureLaunch`; migrate its local JSON world-list metadata to Foundation save metadata before final menu/save cleanup. |
| Notifications | Still shared | `SystemNotifier` remains the global uGUI display; next step is a Foundation-owned System feed view. |

## Retired Scenes

Build Settings should contain only:

1. `Assets/Scenes/MenuScene.unity`
2. `Assets/Scenes/IsoCoreFoundation.unity`

Retire/archive:

- `Assets/Scenes/SampleScene.unity`
- `Assets/Scenes/InfinitePlainsPrototype.unity`
- `Assets/Scenes/ProceduralIsoMap.unity`
- `Assets/Scenes/ProceduralTest.unity`
- `Assets/Scenes/Scene_Biome_Empty.unity`
- `Assets/Scenes/Examples/**`

## Generated Asset Policy

Unity should import only promoted assets. Review, training, prompt, LoRA, and smoke-test outputs belong outside `Assets/` unless they are deliberately promoted with provenance.

Promote into:

- `Assets/Resources/Foundation*`
- `Assets/IsoCoreFoundation/**`
- `Assets/Art/**` when Claude's art lane owns it

Quarantine outside Unity import:

- `Assets/Generated/_Review/**`
- `Assets/Generated/_TrainingInbox/**`
- `Assets/Generated/_Datasets/**` retired/ignored; external datasets now live under `C:/Projects/Pixel Pipeline/datasets/lit_iso/**`
- bulky LPC/recovered/training folders

## Guardrails

- Foundation uGUI adapters must not reference retired singleton systems (`PlayerHealth`, `PlayerMana`, `PlayerStats`, `XPSystem`, `PlayerInventory`, `QuestManager`).
- `WelcomeScreenManager` must not create or write `WorldManager`; Foundation launch data flows through `FoundationBootstrap.ConfigureLaunch`.
- New gameplay systems should be added under `Assets/Scripts/IsoCoreFoundation/**`.
- Legacy code should not be fixed except to unblock retirement, extraction, or compile-safe quarantine.
- Do not delete old code in the same PR that rewires gameplay. Prefer: extract useful behavior, validate, then retire.
- Build Settings stay canonical: `MenuScene` then `IsoCoreFoundation`.
- Generated review/training assets stay out of Unity import unless deliberately promoted with provenance and LFS.
