# LIT-ISO Review Report: Optimization, Redundancy, Security, And Bug Risks

Purpose: review-only handoff for another AI to fix. No code was changed during
this review.

Scope:

- `Assets/Scripts/IsoCoreFoundation/**`
- relevant UI integration under `Assets/Scripts/UI/InGame/**`
- relevant legacy/menu integration paths where they affect the canonical
  Foundation game

Context:

- `IsoCore.Foundation` is canonical.
- The legacy `Assembly-CSharp` world is being retired.
- Workspace is dirty with other agents' in-progress changes. Treat findings as
  current-state review, not blame.
- `dotnet build IsoCore.Foundation.csproj` could not run because no .NET SDK is
  installed on PATH in this shell. Unity compile/batch validation still needs to
  be run after the first fixes.

## Executive Summary

Top risks to fix first:

1. **Likely compile blocker:** progression read/save DTOs are out of sync with
   new Trial Evidence/title/affinity/System message assignments.
2. **New LitRPG evidence system is not wired to gameplay hooks:** actions still
   award old activity XP but do not call `RecordEvidence`.
3. **Item loss bugs:** removing blocks/placeables and harvesting can delete
   items when inventory is full or report drops that were not actually added.
4. **Canonical/legacy drift:** menu/setup/save/UI paths still point at or fall
   back to legacy systems.
5. **Scaling risks:** linear placeable lookups, per-prop occlusion, one
   SpriteRenderer per tile, global input APIs, and synchronous saves will hurt
   split-screen/coop and larger homesteads.

Recommended fix order:

1. Fix compile blockers in `FoundationProgressionReadState` and
   `FoundationProgressionSaveData`.
2. Run Unity compile and Foundation validation.
3. Wire gameplay events to `RecordEvidence` and decide whether evidence owns XP.
4. Add save/load round-trip checks for Trial Evidence, XP channels, titles,
   affinities, and System messages.
5. Fix item-loss paths.
6. Bridge Foundation `SystemFeed` to canonical UI.
7. Fence/retire legacy scene/menu/data paths.
8. Optimize placeable lookup and high-frequency world/UI systems.

## Critical Findings

### 1. Progression DTOs Are Out Of Sync And Likely Block Compile

Files:

- `Assets/Scripts/IsoCoreFoundation/Progression/FoundationProgression.cs:64`
- `Assets/Scripts/IsoCoreFoundation/Progression/FoundationProgression.cs:75`
- `Assets/Scripts/IsoCoreFoundation/Progression/FoundationProgression.cs:408`
- `Assets/Scripts/IsoCoreFoundation/Progression/FoundationProgression.cs:420`
- `Assets/Scripts/IsoCoreFoundation/Progression/FoundationProgression.cs:478`
- `Assets/Scripts/IsoCoreFoundation/Progression/FoundationProgression.cs:633`
- `Assets/Scripts/IsoCoreFoundation/Core/FoundationSaveData.cs:157`

Problem:

`CaptureReadState()` assigns:

- `trial`
- `xpChannels`
- `titleProgress`
- `affinities`
- `systemMessages`

but `FoundationProgressionReadState` only declares:

- `calling`
- `skills`
- `quests`
- `unlockedRewards`
- `recentUnlocks`
- `activeBuffs`
- `regionShifts`

Save state has the same mismatch. `FoundationProgression.CaptureState()` writes:

- `trialScores`
- `xpChannels`
- `titleProgress`
- `affinityScores`
- `acquiredTitles`
- `systemMessages`

and `RestoreState()` reads them, but `FoundationProgressionSaveData` only
declares fields through `regionShifts`.

Why it matters:

This should fail C# compile. Even if a partial file exists later, current visible
DTOs are inconsistent with usage.

Suggested fix:

- Add missing fields to `FoundationProgressionReadState`.
- Add missing fields to `FoundationProgressionSaveData`.
- Add/verify read-state structs for trial, XP channel, title, and affinity data.
- Add/verify helper methods referenced by `FoundationProgression`:
  `CaptureTrialReadState`, `CaptureXpChannelReadStates`,
  `CaptureTitleReadStates`, `CaptureAffinityReadStates`,
  `CaptureTrialScores`, `CaptureKeyValueArray`, `RestoreTrialScores`,
  `RestoreKeyValueArray`, `ApplyEvidenceWeights`, `ApplyXpGrants`,
  `ApplyTitleProgress`, `ApplyAffinityProgress`, `SumTrialScores`,
  `GradeForTotal`.
- Bump save version if older saves need migration.

### 2. Build Verification Could Not Run Locally

Command attempted:

```text
dotnet build IsoCore.Foundation.csproj
```

Result:

```text
No .NET SDKs were found.
```

Suggested fix:

- Another AI should run Unity editor compile or Unity batchmode validation after
  the DTO fix.
- Do not rely on the current `37/37` old validation report until the current
  dirty workspace compiles.

## High Severity Findings

### 3. Trial Evidence System Is Defined But Not Wired To Gameplay

Files:

- `Assets/Scripts/IsoCoreFoundation/Progression/FoundationProgression.cs:246`
- `Assets/Scripts/IsoCoreFoundation/Progression/FoundationProgressionHooks.cs:64`
- `Assets/Scripts/IsoCoreFoundation/Progression/FoundationProgressionHooks.cs:199`

Problem:

`FoundationProgression.RecordEvidence()` exists and applies:

- evidence weights,
- XP grants,
- title progress,
- affinity progress,
- System messages.

But `FoundationProgressionHooks` currently calls `AddActivityXp(...)`, not
`RecordEvidence(...)`. That means harvesting, crafting, placing, tilling, crop
harvesting, mob defeat, and mob calming do not advance the new LitRPG evidence
spine.

Why it matters:

The headline game mechanic depends on actions becoming Trial Evidence. Without
this, the System feed, grade forecast, titles, affinities, and class assignment
cannot feel real.

Suggested fix:

- Map successful gameplay events to evidence IDs:
  - wood drops -> `harvest_wood`
  - stone/copper drops -> `harvest_stone`
  - fiber/apple drops -> `harvest_forage`
  - `craft_workbench` -> `craft_workbench`
  - `craft_campfire` -> `craft_campfire`
  - `stone_path_item` placement -> `place_path`
  - till soil -> `till_soil`
  - crop harvest -> `crop_harvest`
  - mob defeated -> `mob_defeated`
  - mob calmed -> `mob_calmed`
- Decide whether evidence events own XP grants. If yes, remove or reduce direct
  `AddActivityXp` calls to avoid double-awarding.

### 4. Removing Blocks/Placeables Can Delete Refund Items

File:

- `Assets/Scripts/IsoCoreFoundation/Building/PlacementSystem.cs:274`
- `Assets/Scripts/IsoCoreFoundation/Building/PlacementSystem.cs:278`
- `Assets/Scripts/IsoCoreFoundation/Building/PlacementSystem.cs:289`

Problem:

Removal clears the world occupant or solid block, destroys the instance, then
calls `_inv.Add(...)` for the refund. `Inventory.Add` returns leftover, but the
result is ignored.

Why it matters:

If inventory is full, the placed object/block is gone and the refund can be
lost. This is a player-facing data loss bug.

Suggested fix:

- Preflight inventory capacity before clearing/destroying:
  `CanFit(refundItem, 1)`.
- Or create a dropped item entity when inventory is full.
- Only mutate world state after refund/drop handling is guaranteed.

### 5. Harvest Drop Accounting Can Lie And Lose Overflow

File:

- `Assets/Scripts/IsoCoreFoundation/Harvesting/HarvestSystem.cs:16`
- `Assets/Scripts/IsoCoreFoundation/Harvesting/HarvestSystem.cs:27`
- `Assets/Scripts/IsoCoreFoundation/Harvesting/HarvestSystem.cs:28`

Problem:

`RollDrops` calls `inv.Add(d.itemId, amount)` and ignores leftovers, then records
the full rolled `amount` into `granted`.

Why it matters:

Progression/floating text can claim items were granted even if the inventory
could not accept them. Overflow drops can be silently deleted.

Suggested fix:

- Capture leftover from `Inventory.Add`.
- Calculate `added = amount - leftover`.
- Add only `added` to `granted`.
- Define overflow policy: block harvest, drop item in world, or intentionally
  discard with explicit feedback.

### 6. Menu World Metadata And Foundation Save Data Are Split

Files:

- `Assets/Scripts/UI/WelcomeScreenManager.cs:70`
- `Assets/Scripts/UI/WelcomeScreenManager.cs:612`
- `Assets/Scripts/UI/WelcomeScreenManager.cs:647`
- `Assets/Scripts/IsoCoreFoundation/Core/FoundationBootstrap.cs:372`

Problem:

Menu metadata writes `.world.json` under `LitIsoWorlds`, while Foundation saves
runtime state under a seed-qualified folder using `FoundationBootstrap`.
Launching configures name/seed but does not obviously load a Foundation save
after `FoundationBootstrap.Ready`.

Why it matters:

The menu can show a world slot that does not restore the actual Foundation world
state. Another AI may accidentally extend the wrong save model.

Suggested fix:

- Define one canonical world-slot model.
- Menu metadata should reference the Foundation save path or be derived from
  Foundation save metadata.
- Continue/Load should call `FoundationBootstrap.Load` after Foundation scene
  readiness.

### 7. Legacy Scene/Menu Tools Still Point At Retired Flow

Files:

- `Assets/Scripts/Editor/BuildSettingsConfigurator.cs:21`
- `Assets/Scripts/Editor/QuickPlayTestSetup.cs:70`
- `Assets/Scripts/Managers/GameStartupManager.cs:31`

Problem:

Build settings may target `IsoCoreFoundation`, but some setup/startup scripts
still reference `SampleScene` or legacy launch flow.

Why it matters:

Agents can run the wrong scene, fix the wrong system, or test a retired path.

Suggested fix:

- Rename legacy menu items/scripts as retired.
- Update active menu/startup flow to use `FoundationBootstrap.ConfigureLaunch()`
  and `IsoCoreFoundation`.
- Add validator that canonical play path does not reference `SampleScene`.

### 8. Duplicate System Notification Buses

Files:

- `Assets/Scripts/IsoCoreFoundation/Progression/FoundationSystemMessageFeed.cs:6`
- `Assets/Scripts/Core/SystemNotifier.cs:17`
- `Assets/Scripts/IsoCoreFoundation/Progression/FoundationProgression.cs:258`
- `Assets/Scripts/UI/InGame/FoundationNotificationBridge.cs:54`
- `Assets/Scripts/UI/InGame/FoundationNotificationBridge.cs:82`

Problem:

Foundation has `SystemMessageFeed`. UI has global/legacy `SystemNotifier`.
`FoundationNotificationBridge` subscribes to stat and quest events, but not to
`Progression.SystemFeed.Queued`, so evidence/title/affinity messages may never
reach the uGUI notifications.

Why it matters:

System feedback is core to the LitRPG feel. Duplicated buses also create
redundancy and confusion.

Suggested fix:

- Short term: bridge `Progression.SystemFeed.Queued` into `SystemNotifier`.
- Medium term: choose one canonical message bus for Foundation UI.
- Remove or bound `SystemNotifier`'s unused queue. It enqueues but does not drain
  or throttle.

### 9. Foundation UI Falls Back To Legacy State

Files:

- `Assets/Scripts/UI/InGame/FoundationHudAdapter.cs`
- `Assets/Scripts/UI/InGame/FoundationCharacterSheetAdapter.cs`

Problem:

Foundation-facing UI adapters also fall back to legacy singletons/stats.

Why it matters:

Canonical Foundation UI can silently render legacy `PlayerStats`, `XPSystem`,
`PlayerHealth`, etc. if Foundation references are missing. This hides
integration errors.

Suggested fix:

- In the canonical Foundation scene, fail visibly or show empty Foundation state
  rather than falling back.
- Keep any legacy fallback under explicitly named legacy adapters.

## Medium Severity Findings

### 10. Placeable Lookup Is O(n) On Per-Frame Paths

Files:

- `Assets/Scripts/IsoCoreFoundation/Player/PlayerInteraction.cs:62`
- `Assets/Scripts/IsoCoreFoundation/Building/PlacementSystem.cs:251`
- `Assets/Scripts/IsoCoreFoundation/Building/PlacementSystem.cs:235`
- `Assets/Scripts/IsoCoreFoundation/Building/PlacementSystem.cs:293`

Problem:

`UpdateHighlight()` calls `PlaceableAtCell()` every frame. That scans
`_placeables`. Other operations also linearly scan the same list.

Why it matters:

Large homesteads and split-screen multiply the cost.

Suggested fix:

- Maintain `Dictionary<long, PlaceableInstance>` keyed by cell.
- Optionally maintain station-type indexes for crafting station checks.
- Keep list only for deterministic save iteration if needed.

### 11. Per-Prop Occlusion Faders Will Scale Poorly

Files:

- `Assets/Scripts/IsoCoreFoundation/World/IsoWorldController.cs:160`
- `Assets/Scripts/IsoCoreFoundation/World/PropOcclusionFader.cs`

Problem:

Every spawned resource node gets its own occlusion fader. Each fader checks
player/renderer bounds every `LateUpdate`.

Why it matters:

Dense forests, large view radius, and split-screen will produce many per-frame
renderer checks.

Suggested fix:

- Centralize occlusion into a manager.
- Check only props near each player cell.
- Update at lower cadence or on player-cell change.

### 12. Ground Rendering Uses One SpriteRenderer Per Tile

Files:

- `Assets/Scripts/IsoCoreFoundation/World/IsoWorldRenderer.cs:15`
- `Assets/Scripts/IsoCoreFoundation/World/IsoWorldRenderer.cs:309`

Problem:

Ground uses pooled `SpriteRenderer`s per visible cell, plus extra stack renderers
for raised terrain.

Why it matters:

This is acceptable for prototype scale, but risky for high view radius,
split-screen, tall terrain, pocket-base editing, and many biomes.

Suggested fix:

- Set a renderer/object-count budget now.
- Long term: chunk mesh or Tilemap chunks for ground.
- Keep individual SpriteRenderers for actors, props, and interactables.

### 13. Chunk Retargeting Sorts Full Reveal Queue

Files:

- `Assets/Scripts/IsoCoreFoundation/World/IsoWorldController.cs:83`
- `Assets/Scripts/IsoCoreFoundation/World/IsoWorldController.cs:120`

Problem:

On chunk boundary crossing, controller rebuilds desired cells and sorts
`_showQueue` by distance. Reveal work is amortized, but the sort is immediate.

Why it matters:

Large view radius can cause chunk-crossing hitches.

Suggested fix:

- Generate cells in ring/radius order.
- Bucket by distance.
- Sort only newly entered bands.

### 14. Crop Growth Uses One Update Per Crop

File:

- `Assets/Scripts/IsoCoreFoundation/Farming/CropInstance.cs`

Problem:

Each crop ticks independently in `Update()`.

Why it matters:

Large farms/homesteads will scale badly.

Suggested fix:

- Move growth ticking to `FarmingSystem`.
- Use batched simulation, day/time events, or next-growth timestamps.

### 15. Runtime Texture/Sprite Copies Are Cached Forever

Files:

- `Assets/Scripts/IsoCoreFoundation/World/IsoWorldRenderer.cs:202`
- `Assets/Scripts/IsoCoreFoundation/World/IsoWorldRenderer.cs:236`

Problem:

Bordered/flat sprite variants create runtime `Texture2D` and `Sprite` copies and
cache them forever.

Why it matters:

Small tile set is fine. Many biome/building tiles can accumulate memory.

Suggested fix:

- Bake bordered/flat variants offline.
- Or cap cache by asset set and destroy generated textures on teardown.

### 16. Synchronous Save/Load Will Stall Large Worlds

Files:

- `Assets/Scripts/IsoCoreFoundation/Core/FoundationBootstrap.cs:243`
- `Assets/Scripts/IsoCoreFoundation/Core/FoundationBootstrap.cs:255`
- `Assets/Scripts/IsoCoreFoundation/Core/FoundationBootstrap.cs:279`

Problem:

Save captures DTOs, pretty-prints JSON, creates directories, and writes
synchronously on the main thread.

Why it matters:

Large modified cells, crops, storage, mobs, and future guild/base state will
stall frames.

Suggested fix:

- Disable pretty JSON for runtime autosaves.
- Write to temp path then atomic replace.
- Debounce saves.
- Copy Unity state into DTOs on main thread, then serialize/write off-thread.

### 17. Save Path Sanitization Needs Length/Reserved-Name Guard

File:

- `Assets/Scripts/IsoCoreFoundation/Core/FoundationBootstrap.cs:425`

Problem:

`SanitizePathPart` replaces invalid filename characters but does not bound
length or guard reserved Windows names.

Why it matters:

User-provided world names can create fragile save paths.

Suggested fix:

- Normalize whitespace.
- Cap length.
- Reject or rewrite reserved names like `CON`, `PRN`, `AUX`, `NUL`, `COM1`.
- Append stable hash when truncated.

### 18. Global Input APIs Block Clean Split-Screen

Files:

- `Assets/Scripts/IsoCoreFoundation/Player/IsoFoundationPlayer.cs:73`
- `Assets/Scripts/IsoCoreFoundation/Player/PlayerInteraction.cs:55`
- `Assets/Scripts/IsoCoreFoundation/Building/PlacementSystem.cs:46`
- `Assets/Scripts/IsoCoreFoundation/Farming/FarmingSystem.cs:36`

Problem:

Player, interaction, placement, and farming read global keyboard/mouse input.

Why it matters:

Split-screen/coop needs per-player input context and per-player camera/cursor.

Suggested fix:

- Introduce a `FoundationPlayerInputContext`.
- Inject input/camera per player.
- Remove `Camera.main` fallbacks from canonical split-screen paths.

### 19. Stat Schema Is Not Settled

Files:

- `Assets/Scripts/IsoCoreFoundation/Core/FoundationTypes.cs:20`
- `Assets/Scripts/UI/InGame/FoundationCharacterSheetAdapter.cs`

Problem:

Foundation currently exposes `STR, DEX, INT, VIT, DEF, LUCK`. Design docs call
for broader LitRPG effects including END/WIS/CHA/carry/social leverage. Legacy
adapters map old stats into Foundation-shaped fields.

Why it matters:

Combat, movement, crafting, UI, class generation, food buffs, and coop roles all
depend on stable stat vocabulary.

Suggested fix:

- Freeze canonical stat schema before implementing more formulas.
- Either add END/WIS/CHA now or explicitly model them as derived values.
- Remove ambiguous legacy stat mapping in canonical Foundation UI.

### 20. `FoundationContent.BuildDefault()` Is Becoming A Monolith

File:

- `Assets/Scripts/IsoCoreFoundation/Core/FoundationContent.cs:53`

Problem:

`BuildDefault()` now contains blocks, items, placeables, mobs, crops, biomes,
recipes, callings, skills, quests, System messages, evidence, XP channels,
titles, affinities, classes, professions, dungeons, expeditions, board entries,
and world events.

Why it matters:

It is hard to review, hard to merge, and easy for multiple agents to conflict.

Suggested fix:

- Split into domain builders:
  `FoundationBlockContent`, `FoundationProgressionContent`,
  `FoundationDungeonContent`, etc.
- Or move to baked ScriptableObjects after schema stabilizes.

### 21. Validator Coverage Lags New LitRPG Databases

Files:

- `Assets/Scripts/IsoCoreFoundation/Core/FoundationContent.cs:24`
- `Assets/Scripts/IsoCoreFoundation/Editor/FoundationValidator.cs:36`

Problem:

Content now includes System messages, evidence, XP channels, titles, affinities,
classes, professions, dungeons, expeditions, dungeon results, guild board, and
world events. Validator still mostly validates the older block/item/quest spine.

Why it matters:

Validation can pass while new LitRPG content references broken IDs.

Suggested fix:

- Add checks for:
  - evidence weights and grant references,
  - XP channel IDs,
  - title IDs and thresholds,
  - affinity IDs and threshold rewards,
  - class weights/preferred affinity IDs,
  - profession skill IDs,
  - dungeon result IDs,
  - dungeon supply item IDs,
  - board quest/event IDs,
  - world event trigger/consequence policy.

Note:

`FoundationContentBaker.cs` currently does include the new database folders and
asset creation loops. The validator is the main gap.

### 22. Foundation Scene Builder Can Overwrite Canonical Scene

Files:

- `Assets/Scripts/IsoCoreFoundation/Editor/FoundationSceneBuilder.cs:19`
- `Assets/Scripts/IsoCoreFoundation/Editor/FoundationSceneBuilder.cs:36`

Problem:

Builder creates an empty scene and saves directly to the canonical Foundation
scene path. It asks about unsaved scenes, but does not explicitly warn that it is
replacing the canonical scene contents.

Why it matters:

One menu click can wipe scene setup.

Suggested fix:

- Add explicit destructive confirmation with target path.
- Or build to a temp scene unless a `force` flag/menu confirmation is used.

### 23. `FoundationPlayerStats.RecalculateVitals` Can Revive Dead Players

File:

- `Assets/Scripts/IsoCoreFoundation/Progression/FoundationPlayerStats.cs:196`
- `Assets/Scripts/IsoCoreFoundation/Progression/FoundationPlayerStats.cs:200`

Problem:

`Health = Health <= 0f ? MaxHealth : ...`. Any stat recalculation after death can
restore full HP.

Why it matters:

Future combat/death systems can be bypassed by level-up/class/stat changes.

Suggested fix:

- Track initialized/dead state separately.
- Only initialize vitals to max during construction/new character/calling
  assignment if intended.
- Preserve zero/dead health during generic stat recalculation.

## Low Severity / Hygiene Findings

### 24. `SystemNotifier` Queue Is Redundant

File:

- `Assets/Scripts/Core/SystemNotifier.cs:58`
- `Assets/Scripts/Core/SystemNotifier.cs:93`

Problem:

Messages are enqueued, but `OnMessage` is invoked immediately and there is no
processing/drain path.

Suggested fix:

- Remove queue.
- Or implement actual capped/throttled queue processing.

### 25. Runtime `Resources.Load` Is Scattered

Examples:

- `Assets/Scripts/IsoCoreFoundation/Mobs/Mob.cs`
- `Assets/Scripts/IsoCoreFoundation/Player/PlayerAnimator.cs`
- `Assets/Scripts/IsoCoreFoundation/World/TileSpriteResolver.cs`
- `Assets/Scripts/IsoCoreFoundation/Harvesting/DecorationSpriteResolver.cs`
- `Assets/Scripts/UI/InGame/ItemIconResolver.cs`

Problem:

Some loads are cached; mob animation loading may happen per instance.

Suggested fix:

- Centralize asset lookup/caching.
- Load animation sets per definition, not per spawned instance.
- Later move to explicit references/addressables.

### 26. Persistent HUD Objects Need Teardown Policy

File:

- `Assets/Scripts/UI/InGame/GameHudInitializer.cs:58`

Problem:

Initializer creates multiple `DontDestroyOnLoad` UI roots.

Why it matters:

Returning to menu/reloading Foundation can leave stale UI roots or stale
adapters unless every path disposes correctly.

Suggested fix:

- Parent UI under Foundation bootstrap for scene-local lifetime, or add explicit
  teardown on menu return/scene unload.

### 27. Starter Quests Are Started In Multiple Places

Files:

- `Assets/Scripts/IsoCoreFoundation/Progression/FoundationProgression.cs:169`
- `Assets/Scripts/IsoCoreFoundation/Progression/FoundationProgressionHooks.cs:64`

Problem:

Constructor starts one quest; hooks start several more.

Suggested fix:

- Centralize starter quest IDs in content/config.
- Let bootstrap or a dedicated starter-sequence system start them once.

## Additional Test And Validation Recommendations

Add tests or validator checks for:

- Unity compile after DTO fix.
- Save/load round-trip of:
  - Trial Evidence scores,
  - XP channels,
  - title progress,
  - acquired titles,
  - affinity scores,
  - System messages.
- Full-inventory harvest behavior.
- Full-inventory block/placeable removal behavior.
- Foundation `SystemFeed` reaching the uGUI notifier.
- Legacy scene references not present in canonical launch path.
- Placeable dictionary/index consistency after place/remove/save/load.
- Obelisk cannot generate zero class offers once class generator exists.
- Starter seven-day path cannot soft-lock.

## Fix Package Suggestions For Another AI

Small, safe PR sequence:

1. **Compile DTO PR**
   - Add missing read/save fields.
   - Add helper methods if absent.
   - Run Unity compile.

2. **Evidence Wiring PR**
   - Map `FoundationProgressionHooks` events to evidence IDs.
   - Prevent double XP.
   - Bridge evidence System messages to UI.

3. **Data Loss PR**
   - Fix `HarvestSystem.RollDrops` accounting.
   - Fix block/placeable refund flow.
   - Add validation/manual tests for full inventory.

4. **Validator PR**
   - Expand validator for new LitRPG databases.
   - Add save round-trip checks for progression state.

5. **Architecture Fence PR**
   - Mark legacy scene/setup/data paths as retired.
   - Prevent canonical launch from falling back silently to legacy state.

6. **Performance PR**
   - Add cell-keyed placeable dictionary.
   - Defer larger renderer/occlusion/input rewrites until after compile and data
     correctness are stable.
