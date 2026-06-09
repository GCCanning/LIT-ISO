# LIT-ISO QoL And Systems Implementation Blueprint

Purpose: plan the full quality-of-life and LitRPG integration pass before
implementation begins. This lets the team build one tested slice at a time while
the architecture, data contracts, ownership, and validation path are already
decided.

No runtime code was changed for this blueprint.

## Current-State Assumptions

- `IsoCore.Foundation` is canonical.
- The legacy `Assembly-CSharp` world is being retired.
- Foundation lane belongs to Codex:
  - `Assets/Scripts/IsoCoreFoundation/**`
  - `Assets/Scenes/IsoCoreFoundation.unity`
  - `Docs/IsoCoreFoundation/**`
- UI, art, resources, menu, and integration lane belongs to Claude:
  - `Assets/Scripts/UI/**`
  - `World/WorldManager`
  - `GameStartupManager`
  - `Assets/Art/**`
  - `Assets/Resources/**`
- Shared files must stay small and coordinated.
- The worktree is active. Several earlier review risks appear to have fixes in
  progress, including evidence save DTOs, evidence validators, and inventory
  overflow handling. Re-verify before coding against old review notes.

Foundation invariants that this plan must preserve:

- Grid layout: `IsometricZAsY`.
- Grid cell size: `(1, 0.5, 1)`.
- Transparency sort axis: `(0, 1, -0.26)`.
- Height layers: `Height_0..7` map to Unity layer and sorting layer `10+height`.
- Tilemap renderer mode: `Individual`.
- Movement remains world-query based.
- Foot collider remains trigger-only.
- `maxWalkStepHeight=0`.

## Design Goal

Build the player-facing "System" feel and practical QoL spine in one coherent
architecture:

- The System notices what the player does.
- The Trial Evidence Log explains why the player is trending toward certain
  classes, titles, affinities, and grades.
- Players can pin goals and prepare for travel, dungeons, crafting, building,
  trading, and farming without wrestling the UI.
- Inventory, storage, expedition packing, map routing, building placement, and
  party preparation are connected, not isolated menus.
- Coop and split-screen are designed into the data model early, even if the first
  implementation only has one local player.

Implementation should still happen in tested slices. The difference is that each
slice follows this full blueprint instead of discovering the next decision only
after the previous slice is reviewed.

## High-Level Architecture

### 1. Foundation Data Layer

Codex-owned Foundation systems should define pure data contracts first. These
must be serializable, easy to validate, and not dependent on uGUI.

Recommended files:

- `Assets/Scripts/IsoCoreFoundation/QoL/FoundationQoLTypes.cs`
- `Assets/Scripts/IsoCoreFoundation/QoL/FoundationQoLState.cs`
- `Assets/Scripts/IsoCoreFoundation/QoL/FoundationQoLDefinitions.cs`
- `Assets/Scripts/IsoCoreFoundation/QoL/FoundationQoLService.cs`

Core state models:

```csharp
public enum FoundationSystemFeedChannel
{
    Notice,
    Warning,
    TrialEvidence,
    LevelUp,
    SkillUnlock,
    Title,
    Affinity,
    Quest,
    Dungeon,
    Party,
    WorldEvent,
    Inventory,
    Building,
    Travel
}

public enum FoundationPinnedGoalType
{
    None,
    Quest,
    Recipe,
    Title,
    Affinity,
    TrialTendency,
    Expedition,
    Building,
    Profession
}

public enum FoundationLoadoutTemplateType
{
    Custom,
    QuickDelve,
    DeepDelve,
    BossAttempt,
    GatheringRun,
    RescueRun,
    TradeRun,
    BuildRun
}
```

Saved state should include:

- System feed filter settings.
- Pinned goals.
- Favorite and locked inventory slots.
- Saved expedition loadouts.
- Last selected map filters.
- Building overlay preferences.
- Accessibility settings that are gameplay-session specific.
- Coop-ready policy defaults, even before full coop is live.

Avoid making UI views authoritative. UI edits call Foundation services; the
service validates and emits state changes.

### 2. Foundation Service Layer

`FoundationQoLService` should be an observer and coordinator. It should not own
XP, class assignment, inventory truth, or world truth.

It should integrate with:

- `FoundationProgression`
- `FoundationProgressionHooks`
- `Inventory`
- `StorageSystem`
- `PlacementSystem`
- `FarmingSystem`
- `DayNightSystem`
- future travel, dungeon, party, and map systems

Service responsibilities:

- Maintain System feed channel filters.
- Maintain pinned goals.
- Build "goal progress" view models from existing authoritative systems.
- Validate expedition loadouts against inventory, storage, time, fatigue,
  weather, light, route danger, and party composition.
- Handle inventory QoL commands such as sort, quick deposit, stack matching, and
  favorite/lock rules.
- Produce building overlay data from world occupancy and placement rules.
- Produce route and map warning summaries.
- Provide player-specific settings for future split-screen.

Service non-responsibilities:

- Do not award XP.
- Do not determine final class choices.
- Do not bypass placement, inventory, or crafting validation.
- Do not mutate world cells directly.
- Do not talk to Unity UI classes directly.

### 3. UI Adapter Layer

UI code is Claude-owned, so Foundation should expose stable adapters/events that
UI can bind to without reaching into internals.

Recommended UI views:

- `SystemFeedView`
- `SystemFeedFilterView`
- `TrialEvidenceLogView`
- `PinnedGoalView`
- `ExpeditionPrepView`
- `LoadoutTemplateView`
- `StorageQoLView`
- `BuildingOverlayView`
- `MapFilterView`
- `RouteSummaryView`
- `AccessibilitySettingsView`
- `CoopReadyCheckView`

Recommended view-model contracts:

```csharp
public interface IFoundationQoLViewModel
{
    event Action Changed;
    FoundationSystemFeedSettingsReadState FeedSettings { get; }
    FoundationPinnedGoalReadState[] PinnedGoals { get; }
    FoundationExpeditionPrepReadState CurrentExpeditionPrep { get; }
    FoundationInventoryQoLReadState InventoryQoL { get; }
}
```

The Foundation lane can provide read states and command methods. The UI lane
owns visual composition, icons, panel layout, animation, and split-screen
presentation.

### 4. Save/Load Layer

Add QoL state to `FoundationSaveData` only after the compile gate is clean.

Required guarantees:

- Missing QoL state in older saves restores safe defaults.
- Unknown enum values fall back to safe behavior.
- Loadout item IDs are validated against current content.
- Deleted recipes, quests, titles, or affinities do not crash pinned goals.
- Player-specific future fields are versioned.

Add save round-trip coverage to `FoundationIntegratedSliceValidator`.

### 5. Validation Layer

Each slice must add focused validator coverage. Prefer deterministic edit-mode
or batch validation over manual play checks where possible.

Minimum validation categories:

- Compile.
- Save/load round trip.
- Null-safe UI adapter boot.
- Inventory overflow safety.
- No duplicated rewards.
- Event subscription cleanup.
- Old saves/default state migration.
- Split-screen data isolation where player-specific fields exist.

## System Areas

### A. System Feed And Trial Evidence

Goal: the game should feel like a LitRPG from minute one without drowning the
player.

Features:

- Channel filters: Notice, Warning, Trial Evidence, Level Up, Title, Affinity,
  Quest, Dungeon, Party, World Event, Inventory, Building, Travel.
- Priority rules:
  - Critical warnings interrupt.
  - Routine updates go to the log.
  - Repeated low-value events collapse into summaries.
- Trial Evidence Log:
  - recent entries,
  - dominant tendencies,
  - class tendency contribution,
  - grade forecast with uncertainty,
  - obelisk summary source data.
- "Why this class?" data:
  - top evidence IDs,
  - strongest marks,
  - strongest XP channels,
  - highest affinities,
  - titles/provisional titles involved.

Implementation notes:

- Use the existing `FoundationProgression` evidence spine.
- QoL service filters and summarizes messages; it does not create evidence.
- If `FoundationProgressionHooks` already records evidence, verify no duplicate
  `AddActivityXp` reward path remains.

### B. Pinned Goals

Goal: let the player choose what they are actively pursuing.

Pinned goal types:

- Quest.
- Recipe.
- Title.
- Affinity.
- Trial tendency or class forecast.
- Expedition checklist.
- Building project.
- Profession progression.

Rules:

- Support at least one pinned goal in the first slice.
- Model should support three to five pins later.
- Pins must survive save/load.
- Deleted or unavailable content becomes "unavailable" rather than crashing.
- Coop later needs per-player pins and optional shared party pins.

Examples:

- "Craft Workbench: 2/4 wood, 1/2 stone."
- "Root Affinity: gather herbs, calm mobs, plant crops."
- "Deep Delve Prep: low food, no camp kit, light OK."
- "Builder Mark: place roads, repair walls, craft fixtures."

### C. Inventory And Storage QoL

Goal: reduce friction without removing survival preparation.

Features:

- Sort inventory by category, rarity, name, stack size, and recent.
- Filter inventory by item type.
- Search by item name and tag.
- Favorite items so sort/deposit does not move them.
- Lock hotbar slots.
- Quick deposit matching stacks into nearby storage.
- Deposit all except favorites, hotbar, equipped gear, and active loadout items.
- Withdraw missing ingredients for a pinned recipe.
- Return expedition supplies to storage after a trip.

Rules:

- Never delete overflow.
- Never move locked/favorited items unless explicitly confirmed.
- Respect stack caps.
- Use existing `Inventory` and `StorageSystem` validation.
- In split-screen, inventory commands must target a specific player.

First implementation should avoid full crafting-from-storage unless the current
crafting API already supports it cleanly. Start with transfer commands and
progress readouts.

### D. Expedition Prep And Loadouts

Goal: dungeon travel preparation becomes readable and satisfying.

Loadout templates:

- Quick Delve.
- Deep Delve.
- Boss Attempt.
- Gathering Run.
- Rescue Run.
- Trade Run.
- Build Run.
- Custom.

Checklist dimensions:

- Food duration.
- Water/drink if survival scope adds it.
- Potions.
- Antidote/cleanse.
- Light source and fuel.
- Camp kit.
- Repair kit.
- Carry load.
- Weapon/tool durability.
- Weather protection.
- Route time.
- Night travel risk.
- Party readiness.
- Recall/escape item.

Warnings should be soft gates:

- "Low food for route."
- "No camp kit. Night fatigue risk high."
- "No antidote. Poison mobs sighted."
- "Overloaded. Movement and stamina reduced."
- "Storm season. Boat durability risk."

QoL should help the player prepare, not block creative risk.

### E. Building And Hearthclaim QoL

Goal: make isometric building precise, legible, and compatible with future
homestead/guild expansion.

Recommended building model:

- Outdoor Hearthclaim map uses tile placement and prefab footprints.
- Buildings are placed as prefabs with footprint sizes such as `2x2`, `3x3`,
  `4x4`.
- Prefabs support rotations/facings:
  - south-east,
  - south-west,
  - later north-east/north-west if art exists.
- Entering a building teleports to an interior mini-map.
- Interiors are handcrafted/prefab spaces first; full interior building can come
  later if needed.

QoL features:

- Blueprint mode before committing resources.
- Footprint preview.
- Collision/occupancy overlay.
- Door/path connectivity warning.
- Road placement brush.
- Move building command with cost rules.
- Deconstruct refund preview.
- Storage labels.
- Workstation radius indicator.
- Guild expansion boundary preview.

Implementation notes:

- Reuse `PlacementSystem` validation.
- Overlay data should be computed, not stored in scene objects.
- Do not change isometric height/sorting invariants.
- Avoid editing `IsoCoreFoundation.unity` from multiple agents.

### F. Map, Exploration, And Events

Goal: exploration should matter between town, dungeons, coast, and continents.

Features:

- Fog or undiscovered regions.
- Map pins:
  - dungeon,
  - resource node,
  - event,
  - shop,
  - guild,
  - danger sighting,
  - fishing spot,
  - dock,
  - caravan,
  - homestead portal.
- Route summary:
  - travel time,
  - food expected,
  - nightfall risk,
  - weather risk,
  - danger rating,
  - estimated item rarity band.
- Rumor reliability:
  - confirmed,
  - recent,
  - stale,
  - unreliable.
- Guild board integration:
  - dangerous mob sighting,
  - goblin raid,
  - dungeon breach,
  - caravan request,
  - storm warning,
  - rare herb bloom,
  - obelisk anomaly.

Map QoL should also feed expedition prep.

### G. Coop And Split-Screen Readiness

Goal: single-player systems should not block local coop later.

Design all QoL state with future player scoping:

- `playerId`.
- shared party state.
- shared world events.
- player-specific System feed filters.
- player-specific pinned goals.
- party-level expedition ready checks.
- loot policy.
- storage ownership policy.
- building permission policy.

First visible coop QoL:

- Party ready checklist.
- "Player 2 missing food."
- "Shared camp kit packed."
- "Loot policy: free take / round robin / host assigns."
- Compact split-screen System feed mode.

Implementation warning:

- Avoid `Input.GetKeyDown`-style global commands for new QoL actions if an input
  abstraction is available. If not, isolate the input adapter so coop can replace
  it later.

### H. Accessibility And Comfort

Goal: make the UI readable and the game comfortable over long sessions.

Settings:

- HUD scale.
- System feed duration.
- System feed density.
- Flash intensity.
- Screen shake intensity.
- Damage number visibility.
- Colorblind-friendly rarity palette.
- Tooltip delay.
- Hold/toggle options for repeated actions.
- Font size for dense panels.
- Camera zoom sensitivity.
- Separate music, ambience, SFX, UI volume.

Save settings through the existing options path where possible. Coordinate
PlayerPrefs keys with the menu/options lane.

### I. Lighting And Visual Polish

Goal: lighting becomes both atmosphere and gameplay readability.

Short-term, pre-URP:

- Better ambient day/night tint.
- Fake campfire and lantern glow sprites.
- Obelisk pulse overlay.
- Dungeon darkness vignette.
- Rare loot glow/sparkle.
- Weather overlays.
- Player light radius at night.

URP pilot, later:

- Add URP package on a dedicated branch.
- Create a 2D Renderer asset.
- Convert only Foundation test content first.
- Global Light 2D driven by `DayNightSystem`.
- Campfire, lantern, obelisk, and dungeon torch lights.
- Minimal Shadow Caster 2D use.
- Lit materials on player, props, interactables, and key dungeon/building art.

Do not start URP until compile, data, save/load, and core QoL services are
stable.

## Integration Order

### Slice 0: Stabilization Gate

Purpose: make sure the current moving worktree is buildable before adding
another system.

Tasks:

- Re-check recent fixes against the old optimization review.
- Build Foundation runtime/editor projects.
- Run Foundation integrated validator.
- Confirm Trial Evidence save/read DTOs compile and round-trip.
- Confirm item overflow removal/harvest fixes are actually in code.
- Confirm UI notification/day clock additions compile.

Exit criteria:

- Build passes.
- Foundation validation passes.
- Known dirty-worktree changes are either committed, handed off, or explicitly
  ignored for this pass.

### Slice 1: QoL Data Spine

Purpose: add no-visible-UI data contracts and save-safe defaults.

Foundation files:

- `FoundationQoLTypes.cs`
- `FoundationQoLState.cs`
- `FoundationQoLService.cs`
- save DTO additions in `FoundationSaveData.cs`
- bootstrap property in `FoundationBootstrap`

Features:

- Feed filter state.
- One pinned goal slot.
- Inventory favorite/locked slot state model.
- Loadout template state model.
- Accessibility state model.
- Save/load round-trip.

Validation:

- Defaults exist with no save data.
- Save/load restores QoL state.
- Unknown/deleted content IDs do not throw.

### Slice 2: System Feed Filters And Trial Evidence Readouts

Purpose: make the LitRPG System controllable and inspectable.

Foundation:

- Channel filter API.
- Trial Evidence summary read state.
- Grade forecast read state.
- Class tendency read state.
- System feed collapse/summarize rules.

UI lane handoff:

- Add System Feed filter panel.
- Add Trial Evidence Log panel.
- Add "why this forecast" details.

Validation:

- Evidence events produce readout changes.
- Filters hide/show channels without deleting log history.
- Summary collapses repeated low-priority entries.

### Slice 3: Pinned Goals

Purpose: give the player a persistent HUD-level target.

Foundation:

- Pin/unpin commands.
- Progress resolvers for quest, recipe, title, affinity, trial tendency.
- Missing-content fallback.

UI lane handoff:

- HUD pinned-goal widget.
- Compact progress display.
- Pin button on eligible panels.

Validation:

- Pinned quest updates when quest progresses.
- Pinned recipe updates when inventory changes.
- Save/load keeps pins.

### Slice 4: Inventory And Storage QoL

Purpose: reduce item-management friction.

Foundation:

- Sort command.
- Filter/search read state.
- Favorite item command.
- Lock slot command.
- Quick deposit matching stacks.
- Deposit all except protected items.

Validation:

- No overflow loss.
- Locked/favorited items stay in place.
- Stack caps respected.
- Storage unavailable state is safe.

### Slice 5: Expedition Prep And Loadouts

Purpose: make dungeon travel preparation a real, readable loop.

Foundation:

- Loadout templates.
- Prep checklist read state.
- Soft warning generation.
- Auto-pack candidate calculation.
- Optional apply-auto-pack command after validation is stable.

Future systems consumed:

- dungeon rank,
- route distance,
- time of day,
- weather,
- fatigue,
- party members,
- boat durability.

Validation:

- Missing food warning.
- Missing camp kit warning.
- Overload warning.
- Loadout save/load.
- Auto-pack never deletes or duplicates items.

### Slice 6: Building Overlay And Hearthclaim QoL

Purpose: make isometric building precise before expanding base systems.

Foundation:

- Blueprint footprint read state.
- Occupancy overlay read state.
- Door/path warning.
- Deconstruct refund preview.
- Move-building feasibility check.

UI/art handoff:

- Placement overlay visuals.
- Blueprint ghost.
- Road brush icons.
- Building prefab footprint art.

Validation:

- Overlay matches placement rules.
- Invalid placement reasons are stable.
- Refund preview matches actual removal behavior.

### Slice 7: Map, Exploration, And Event QoL

Purpose: connect world events, travel, dungeons, and prep.

Foundation:

- Map pin data.
- Route summary read state.
- Rumor reliability state.
- Guild board event summaries.

Validation:

- Pins save/load.
- Expired sightings are marked stale.
- Route warnings feed expedition prep.

### Slice 8: Coop And Split-Screen QoL Contracts

Purpose: avoid rewriting QoL systems when local coop lands.

Foundation:

- Add `playerId` to player-specific QoL commands and state.
- Party-level ready check.
- Loot policy state.
- Shared vs personal pinned goals.

UI lane handoff:

- Split-screen compact feed.
- Per-player ready state.
- Shared expedition checklist.

Validation:

- Player 1 settings do not overwrite Player 2 settings.
- Shared state remains shared.
- Save/load preserves both scopes.

### Slice 9: Accessibility And Settings

Purpose: make the growing UI comfortable.

Foundation/shared:

- Define stable PlayerPrefs/save keys.
- Add settings read/write contract.
- Wire System feed density, flash, shake, and UI scale where applicable.

UI lane:

- Settings panel controls.
- Tooltip delay and font scale.
- Colorblind rarity palette option.

Validation:

- Defaults are sane.
- Setting changes apply without reload where possible.
- Save/restart preserves settings.

### Slice 10: Lighting Pilot

Purpose: test whether URP 2D lighting is worth the migration cost.

Tasks:

- Branch separately.
- Add URP.
- Configure 2D Renderer.
- Build minimal Foundation lighting test.
- Validate sorting, pixel sharpness, and performance.

Exit criteria:

- No pink/missing materials.
- Sorting invariants intact.
- Pixel Perfect remains sharp.
- Lighting clearly improves dungeons/night/campfires/obelisk.

## Code-Level Integration Sketch

Bootstrap flow:

```text
FoundationBootstrap.Awake
  Build content
  Build world
  Build inventory/hotbar/storage/placement/crafting/farming/daynight
  Build progression
  Build progression hooks
  Build QoL service
  Restore save state if present
  Emit Ready
```

Save flow:

```text
FoundationBootstrap.CaptureState
  Capture world
  Capture inventory/storage
  Capture farming/crops
  Capture day/time
  Capture progression/evidence
  Capture QoL state
```

Event flow:

```text
Gameplay action succeeds
  Authoritative system mutates state
  Progression hook records evidence/reward
  QoL service observes event/state change
  QoL service emits read-state changed
  UI adapter updates visible panels
```

Inventory QoL command flow:

```text
UI requests quick deposit
  QoL service builds transfer plan
  Inventory/Storage validate capacity and protected slots
  QoL service applies transfers through existing APIs
  QoL service emits result summary
```

Expedition prep flow:

```text
Player selects route/dungeon/loadout
  QoL service reads route/time/weather/dungeon/party state
  QoL service compares requirements against inventory/storage
  QoL service produces soft warnings and missing-item list
  Player accepts risk or applies loadout packing
```

Building overlay flow:

```text
Player selects building/road item
  PlacementSystem remains authoritative
  QoL service requests placement diagnostics
  Overlay read state exposes footprint, blockers, door/path warnings
  UI renders ghost/tiles/reasons
```

## Definition Of Ready For Each Slice

Before coding a slice:

- Confirm ownership lane.
- Confirm latest git status and active branches.
- Re-read changed files in the target area.
- Identify validator tests to add before or alongside code.
- Identify save migration/default behavior.
- Identify UI handoff contract if the slice needs UI.

Before handing off a slice:

- Build passes.
- Foundation validator passes or blocker is documented.
- Save/load round-trip passes if state was added.
- No unrelated files were modified.
- `.meta` files are included for new Unity assets/scripts.
- Other agent comms are updated.

## Known Coordination Risks

- UI QoL panels live in Claude lane. Foundation should expose contracts; Claude
  should own final visuals.
- `IsoCoreFoundation.unity` should have one owner at a time.
- URP touches shared project settings and materials. Do not mix it with gameplay
  QoL work.
- Save/load is cross-lane. Keep path and DTO decisions stable.
- Generated art folders are noisy. Do not promote review assets into Resources
  without explicit art-lane coordination.
- Old review reports may be partially stale because fixes are already in
  progress. Re-verify exact code before making fixes.

## Recommended First Implementation After This Blueprint

Start with Slice 0, then Slice 1.

Reason:

- The current workspace already has in-progress save/evidence/UI work.
- QoL depends on stable save/load and progression events.
- A data spine with validators gives UI/art agents something stable to bind
  against without forcing the user to review every micro-slice.

Do not start with URP, expedition UI, or building overlays until the QoL data
service exists and the project is compiling cleanly.
