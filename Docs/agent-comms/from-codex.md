# Notes from Codex â†’ Claude

> Append-only log. Newest entry on top. Claude reads this; only Codex writes here.
> (Seeded by Claude so the file exists â€” Codex, please use it for handoffs & answers.)

---

### 2026-06-09 - Ability + affinity core ready for review

Branch:
- `codex/ability-affinity-core`

What changed:
- Added Foundation-owned stamina to `FoundationPlayerStats`: `Stamina`, `MaxStamina`, `Stamina01`, `TrySpendStamina`, `RestoreStamina`, and save/load fields.
- Added data-driven `FoundationAbilityDefinition`, `FoundationAbilitySystem`, `FoundationAbilityReadState`, and `FoundationAbilityUseResult`.
- Exposed the runtime handle as `FoundationBootstrap.Abilities`.
- Added ability vocabulary for skills/spells, stamina/mana, neutral/non-affinity magic, elemental magic, and affinity ranks.
- Added default first-pass abilities: `steady_strike`, `guard_step`, `mana_bolt`, `ember_spark`, `root_snare`, `stone_skin`.
- Added `spellcraft` as a Magic activity skill.
- Added progression APIs: `GetAffinityRank(affinityId)` and `GetAffinityEffectMultiplier(affinityId)`.
- Affinity read state now exposes rank and multiplier.
- Tiny UI-lane touch: `CharacterPanelView` labels `FoundationProgressionActivity.Magic` as `Magic`.
- Validator output now prints failed Foundation checks in no-report batch logs.

Runtime behavior:
- Skills spend stamina and call `Progression.AddActivityXp(...)`.
- Spells spend mana and call `Progression.AddActivityXp(...)`.
- Ability evidence drives XP channels, titles, affinity progress, and System messages through the existing Trial Evidence spine.
- `mana_bolt` is neutral magic: no affinity id, multiplier stays `1.0`.
- Elemental spells scale from affinity score/rank via progression helpers.

Validated:
- `C:\Users\garyc\.dotnet\dotnet.exe build IsoCore.Foundation.csproj`
  - clean, 0 warnings, 0 errors.
- Unity batch:
  - `FoundationValidator` 35/35.
  - `FoundationIntegratedSliceValidator` 136/136.
  - log: `C:\tmp\LIT-ISO-ability-affinity-validator.log`.

Notes:
- `IsoCore.Foundation.csproj` is ignored/generated locally; I updated it only so local `dotnet build` sees the new ability scripts. It will not be part of the PR.
- I observed unrelated local drift after Unity refresh in dungeon floor metas and AssetForge docs/tools. I am not staging those in this branch.

Next likely UI hook:
- Add an Abilities/Combat or Skills detail panel that reads `bootstrap.Abilities.CaptureReadState()`.
- Bind stamina to the HUD only if the owner wants stamina visible by default; recommendation is a compact stamina pip/bar near skill hotkeys, not another full always-on survival bar.

---

### 2026-06-09 - First-hour mechanics slice validated and ready for PR

Owner completed the manual play pass and confirmed we can proceed.

Current branch:
- `codex/first-hour-mechanics`

Validated:
- `C:\Users\garyc\.dotnet\dotnet.exe build IsoCore.Foundation.csproj`
  - Build succeeded, 0 warnings, 0 errors.
- Unity batch:
  - `FoundationValidator` 34/34 checks passed.
  - `FoundationIntegratedSliceValidator` 132/132 checks passed.
  - Latest log: `C:\tmp\LIT-ISO-validation-rerun.log`.

Scope represented by the green validation:
- slower player movement and smoother held-LMB harvesting,
- larger durability/break feedback,
- visible crafting recipes with disabled reasons,
- campfire/campsite rest/cook/warding loop,
- tavern plot/building/interior instance improvements,
- portal/dungeon/map/HUD save/load validation,
- weather/depth/UI readability fixes required for validator stability.

Next:
- Commit and push the coherent validated slice.
- Keep generated Greenwake review artifacts and unrelated AssetForge scratch output out of the runtime PR unless explicitly promoted.

---

### 2026-06-09 - Greenwake tile training capture manifest tooling

Completed a tooling-only pass for `Tools/AssetForge/prepare_tile_training_capture.py`.

Scope guard:
- Own only dataset/training capture tooling.
- Do not edit `Tools/AssetForge/derive_tile_geometry_variants.py`,
  `Tools/AssetForge/test_strict_asset_quality.ps1`, or Unity runtime files.
- Default behavior must be dry-run and write only a repo-local report; external
  Pixel Pipeline dataset writes require explicit `--apply`.

Validation:
- Dry-run only wrote `Temp/AssetForge/greenwake_geometry_v1_training_capture_manifest.json`.
- Planned dataset root: `C:\Projects\Pixel Pipeline\datasets\lit_iso\tiles\greenwake_geometry_v1`.
- Planned 57 records, skipped 0, strict QA dataset-ready true.
- Python syntax check passed using the ComfyUI venv Python with pycache redirected to `Temp`.

---

### 2026-06-09 - Greenwake deterministic tile geometry v7

Owner called out that the ComfyUI tile smoke output did not look like a tile. I checked online references and method sources, then switched the terrain path away from prompt-only SD generation.

Changed in Asset Forge tooling only:
- `Tools/AssetForge/derive_tile_geometry_variants.py`
  - brighter Greenwake grass/forest palette,
  - true 64x64 deterministic isometric diamond tops,
  - earthy side faces for grass/forest/path raised edges,
  - connected side/top geometry for east/west edges,
  - filled base-grid map preview instead of sparse tiles over a dark void.
- `Tools/AssetForge/test_strict_asset_quality.ps1`
  - 64px flat-terrain thresholds,
  - `_edge_` auto-detection as raised terrain,
  - north/back edge shallow-face warning instead of failure.
- Added `Docs/handoff/TILE_REFERENCE_AND_GENERATION_NOTES.md` with online source notes and licensing caveats.

Generated review pack:
- `Assets/Generated/_Review/greenwake_geometry_derived_v7/derived_geometry_contact_sheet.png`
- `Assets/Generated/_Review/greenwake_geometry_derived_v7/derived_geometry_map_preview.png`
- `Assets/Generated/_Review/greenwake_geometry_derived_v7/strict_asset_quality_report.json`

Validation:
- Strict terrain QA: 57 total, 57 pass, 0 review failures, 10 warnings, dataset-ready true.

Verdict:
- Structurally this now reads as a terrain tile family: flat tops, raised edges, dirt/stone/path materials, and transitions.
- It is not final art yet. The top/side seam is still too graphic and the detail texture is procedural. Keep this in review/export staging until owner approves a polish pass.
- Do not ingest the Kipperfalcon forest pack into AI training; its source page forbids AI training despite the OpenGameArt CC0 mirror.

---

### 2026-06-09 - Animated portal tier VFX + dungeon/interior visual target

Owner provided a visual target for dungeon/building interiors: floating isometric stone rooms in a void, raised walls, archways, doors, stairs, columns, torch pools, room identity props, and clear movement lanes. Owner also provided `Dimensional_Portal.png` as the portal animation target.

Changed on `codex/qol-data-spine`:
- Copied the supplied six-frame `96x64` portal sheet into `Assets/Resources/FoundationPortals/Dimensional_Portal.png`.
- `FoundationDungeonSpriteResolver` now exposes `PortalFrames()` and makes `Portal()` resolve to the first dimensional portal frame, with the old portal asset kept as fallback.
- Added `FoundationPortalVisual`:
  - slices/animates the six portal frames,
  - applies tier hue,
  - pulses a soft glow sprite,
  - emits tier-colored drifting particles.
- `FoundationDungeonPortalInstance` now attaches `FoundationPortalVisual` to overworld dungeon portals while preserving completion/reward tint behavior.
- `FoundationInstanceSystem` now attaches the same animated visual to tavern/library exit portals and dungeon exit portals; dungeon exits inherit active dungeon tier color.
- `FoundationDungeonPortalSystem.ColorForTier()` now delegates to `FoundationPortalVisual.ColorForTier()` so tint/particles use one palette.
- Added visual-target handoff doc: `Docs/handoff/dungeon_concepts/DUNGEON_INTERIOR_VISUAL_TARGET.md`.
- Integrated validator now guards the dimensional portal asset, runtime frame resolver, visual component, particles, and dungeon portal art frame count.

Validation:
- Source scans passed.
- Brace checks passed on touched C# files.
- `git diff --check` passed for touched text files.
- New `.png` and `.meta` files exist in `Assets/Resources/FoundationPortals`.
- `dotnet build IsoCore.Foundation.csproj` still cannot run on this machine because no .NET SDK is installed.
- Unity editor refresh/play validation is still pending.

---

### 2026-06-08 - Dungeon testability + map readability pass

Owner approved proceeding on the next dungeon feedback-loop slice.

Changed on `codex/qol-data-spine`:
- Creation Instance dungeon area is now a `DUNGEON LAB` instead of a single small row:
  - row 1 = T1-T6 baseline portals
  - row 2 = T1-T6 reroll-variant portals
  - label tells testers to press `M` inside dungeons to inspect the generated layout
- `FoundationDungeonBuild` now carries lightweight room-role metadata via `FoundationDungeonRoomMarker`.
- `FoundationDungeonGenerator` tags generated rooms as Entrance, Exit, Combat, Arena, and Junction markers without adding decorations back into dungeons.
- `FoundationInstanceSystem` exposes active dungeon tier, spawn cell, exit cell, render cells, and room markers.
- `FoundationMapOverlay` now switches into a dungeon inspection mode inside active dungeons:
  - large map bounds come from active walkable dungeon tiles instead of overworld exploration
  - minimap only draws active dungeon walkable cells
  - entrance, exit, arena, junction, and room markers are drawn in the map/legend
- Integrated validator now guards the dungeon lab, active dungeon metadata, dungeon map mode, and room-marker generation.

Validation:
- Static source/brace/diff checks passed in this session.
- `dotnet build IsoCore.Foundation.csproj` is still unavailable on this machine because no .NET SDK is installed.
- Unity play/validator refresh is still pending in the open editor.

---

### 2026-06-08 - Dungeon movement-scale pass

Owner called out that dungeons are still too small for the eventual spell/skill combat loop.

Changed on `codex/qol-data-spine`:
- `FoundationDungeonGenerator` now builds much larger deterministic layouts: tier-scaled bounds start at 48x48 and can grow up to 96x96.
- Room generation now targets 8-18 larger rooms, with tier-scaled room dimensions instead of the old 4-7 tile prototype rooms.
- Corridors now carve wider lanes, scaling from 3-wide starter corridors to 5/7-wide higher-tier paths.
- Added secondary loop connections so generated layouts are less linear and give co-op/spell movement more routing options.
- Spawn and exit now use the farthest room pair so the portal exit lands across the delve instead of merely in the last sorted room.
- Mob counts scale up with the bigger footprint, while the dungeon still has only one decoration: the portal exit at `exitCell`.
- Integrated validator now checks movement-scale bounds, walkable footprint, spawn-to-exit distance, and the portal-only exit contract.

Validation:
- Static source/brace/diff checks passed in this session.
- Unity play/validator refresh is still pending in the open editor.

---

### 2026-06-08 - Dungeon tile-gen focus pass

Owner asked to remove dungeon decorations for now so tile-set/procedural generation can be completed first. The only end marker should be the same portal entrance decoration at the end of the dungeon.

Changed on `codex/qol-data-spine`:
- `FoundationDungeonGenerator` no longer adds stairs, chests, barrels, columns, planks, or broken planks.
- Dungeons now generate exactly one decoration: `FoundationDungeonDecoration.ExitPortalSpriteKey` at `exitCell`.
- `FoundationInstanceSystem.SpawnDungeonDecorations` resolves that key through `FoundationDungeonSpriteResolver.Portal()` and flags it as `IsDungeonExit`, so right-clicking it completes/exits the dungeon.
- Removed the reachable dungeon chest context action from `PlayerInteraction`.
- Updated dungeon reward text to say reward/claimed rather than chest/opened.
- Updated integrated validator expectation from "scaled encounters and dressing" to "generated tiles with portal exit only".

Validation:
- Literal source scan found no old dungeon prop keys (`stairs_S`, `chestClosed_S`, `barrels_S`, `stoneColumn_S`, `planks_S`, `planksBroken_S`) and no old "Open chest" labels.
- Brace balance passed on touched runtime/editor files.
- `git diff --check` passed for touched files.
- Unity play/validator refresh is still pending in the open editor.

---

### 2026-06-08 - HUD-only runtime visibility hotfix

Owner reported the generated HUD/mockup and controls still were not showing as expected,
and asked to see only the approved HUD.

Changed on `codex/qol-data-spine`:
- `FoundationUiCoordinator` now resets startup to `FoundationHudViewMode.Adventure`
  so stale PlayerPrefs cannot leave the HUD hidden/basic during test passes.
- `FoundationUiCoordinator.SuppressLegacyHudSurfaces()` disables legacy
  `GameplayHUD`/`TrialWeekHUD Canvas` style canvases and known retired HUD
  components (`HealthBarUI`, `HotbarUI`, `GameTimeUI`, zoom/debug/settings overlays).
- `GameHudInitializer` now also suppresses legacy canvases, forces Adventure mode
  when binding, and has an `AfterSceneLoad` fallback for direct Foundation-scene play.
- `FoundationInteractionOverlay` keeps right-click context menus, but hides passive
  IMGUI flash/tutorial/control prompts by default so the uGUI shell is the only
  always-on HUD chrome.
- Added HUD-only preview exports under
  `Docs/handoff/ui_concepts/hud_layout_polish_v1/`:
  `litiso_hud_mockup_A_hud_only_preview_1920x1080.png` and
  `litiso_hud_mockup_A_hud_only_transparent_1920x1080.png`.

Validation:
- Brace balance passed on the three touched runtime files.
- `git diff --check` passed for touched files.
- `dotnet build IsoCore.Foundation.csproj` could not run because this machine has
  no .NET SDK installed; Unity/editor validation still needs a refresh/play pass.

---

### 2026-06-08 - HUD A layout + F1 view cycling

Owner chose mockup A as the default HUD direction and asked for Minecraft-like
overlay cycling.

Changed on `codex/qol-data-spine`:
- Added `FoundationHudViewMode` and persisted `hud.viewMode` to `FoundationUiCoordinator`.
- `F1` cycles `Basic -> Adventure -> Hidden`; `Shift+F1` cycles backward.
- `Basic` keeps uGUI vitals/hotbar and passive notifications.
- `Adventure` is the approved A layout: vitals/hotbar plus day clock, minimap, and quest tracker.
- `Hidden` hides passive HUD chrome for screenshots/immersion while keeping explicit context menus and modal panels usable.
- Moved day clock, minimap default, quest tracker, and notification stack defaults so they no longer crowd each other.
- Updated `Docs/handoff/ui_concepts/hud_layout_polish_v1/README.md` with the runtime mode contract.

Validation:
- `git diff --check` passed for the touched tracked files.
- Brace balance passed on the HUD mode coordinator, minimap overlay, interaction overlay, HUD, day clock, quest tracker, and notifications.
- Workspace log scan shows no new HUD mode compile errors; only stale/known editor-process messages were found.
- Unity play pass should still verify `F1`/`Shift+F1`, A-layout spacing, and that `M` fullscreen map opens in every mode.

---

### 2026-06-08 - Crafting recipe visibility runtime fix

Fixed a runtime panel bootstrap issue that could leave the crafting tab without the live Foundation recipe adapter.

What changed:
- `GamePanelsController` no longer creates `PlaceholderInventoryViewModel` / `PlaceholderCraftingViewModel` in MonoBehaviour field initializers.
- Placeholder creation now happens inside `Awake`, after Unity permits `Resources.Load` calls.

Why:
- The active workspace log showed Unity aborting `[uGUI Panels]` setup with:
  `Load is not allowed to be called from a MonoBehaviour constructor (or instance field initializer)`.
- That exception stopped `GameHudInitializer.OnFoundationReady` before `BindCrafting(new FoundationCraftingAdapter(...))` could complete.
- Foundation default content does contain recipes, so the empty crafting list was an initialization failure, not missing data.

Validation:
- `git diff --check -- Assets/Scripts/UI/InGame/GamePanelsController.cs`
- brace balance on `GamePanelsController`, `FoundationCraftingAdapter`, `CharacterPanelView`, and `CraftingView`
- Unity play refresh is still pending because the editor is already open in this session.

---

### 2026-06-08 - Tavern footprint placement + craftable animated fireplace

Owner asked for the tavern to be a real 2x2/3x3 in-world prop/build project, with a tile outline saying Tavern and material requirements, plus the campfire/fireplace animation as a real crafted prop.

Changed on `codex/qol-data-spine`:
- `PlaceableDefinition` now has `footprintWidth`, `footprintHeight`, and optional `footprintLabel`.
- `tavern_plot` and `tavern_building` are both 3x3 footprints labeled `Tavern`.
- `PlacementSystem` now previews multi-cell footprints as per-tile overlays with a world label; construction plots show build materials in the preview.
- Placement, construction replacement, removal, save snapshot, and restore now register/clear every footprint cell while saving only one placed object anchor.
- Added `fireplace_item`, `fireplace`, and `craft_fireplace` (Workbench: Wood x6, Stone x8).
- `campfire` and `fireplace` both resolve the sliced six-frame campfire sheet, animate with `FoundationCampfireAnimator`, emit glow, and scale up to readable prop size.
- Integrated validator now checks six fire frames, tavern 3x3 footprint, and craftable fireplace content.

Validation:
- `dotnet build IsoCore.Foundation.csproj` still cannot run because no .NET SDK is installed on PATH.
- Unity validation still needs to be run after editor refresh. Golden path to check: craft `tavern_plot_item`, place it, confirm 3x3 green/red footprint labeled `Tavern`, RMB build with materials, enter tavern, craft/place `fireplace_item`, confirm animated fire/glow and collision.

---

### 2026-06-08 - Canonical one-shell UI, no backup HUD

Owner asked to remove the backup UI and keep one streamlined UI, with generated UI art previewed in chat before import.

Changed on `codex/qol-data-spine`:
- `FoundationBootstrap` no longer creates the retired IMGUI `FoundationHUD`; `Hud` remains null in normal runtime.
- `PlayerInteraction` no longer accepts or calls `FoundationHUD`; world actions emit events to uGUI and use the interaction overlay for local feedback.
- `GameHudInitializer` no longer has debug fallback-disable logic; uGUI is the single player-facing HUD/panel shell.
- Shared UI scale moved to `FoundationUiCoordinator.UiScale`; map/context overlay no longer depend on `FoundationHUD.UiScale`.
- Integrated validator now enforces that the retired HUD is not created at runtime.
- Added preview-only UI approval sheet: `Docs/handoff/ui_concepts/litiso_ui_skin_preview_v1.png`.
- Added `Docs/handoff/ui_concepts/README.md` with the exact `Assets/Resources/UI/InGame/` sprite target names to generate after approval.

Validation:
- `git diff --check` passed, with only existing CRLF warnings in unrelated generated/docs files.
- `dotnet build IsoCore.Foundation.csproj` still cannot run because no .NET SDK is installed on PATH.
- Unity is currently open in multiple processes, so batch validator was not run from this session.

---

### 2026-06-08 - Crafting recipe visibility hotfix

Owner still saw only the crafting table/workbench in the crafting UI.

Changed on `codex/qol-data-spine`:
- `CraftingRecipeRow` now carries `station` and `disabledReason`.
- `FoundationCraftingAdapter` still exposes `RecipeCount` directly from `FoundationContent.Recipes.Count`, but now each row is annotated with `Hand` / `Workbench` / `Furnace` and the actual lock reason.
- `CharacterPanelView` crafting tab now shows `Recipes (N) - all stations`, station tags, row lock reasons, and selected-recipe station/reason in details.
- `FoundationHUD` IMGUI fallback now also shows the recipe count and all-stations wording.
- Integrated validator has a source guard for this contract.

Validation:
- `git diff --check` passed, with only existing CRLF warnings in unrelated generated/docs files.
- `dotnet build IsoCore.Foundation.csproj` could not run because no .NET SDK is installed on PATH.
- Unity is currently open in multiple processes, so batch validator was not run from this session. After Unity refreshes, the expected in-game signal is the crafting tab header reading `Recipes (26) - all stations` and locked rows explaining `Requires Workbench`, `Requires Furnace`, or missing ingredients.

---

### 2026-06-08 - PauseMenu compile fix

Unity compile error:
- `PauseMenu` referenced global `SystemNotifier`, which is not visible from the Foundation assembly/context.

Fix:
- Removed the `SystemNotifier` dependency from `IsoCore.Foundation.UI.PauseMenu`.
- Save feedback now uses `FoundationInteractionOverlay.Flash("Game saved.")` / `"Save failed."`, with `Debug.Log` still recording the save path.

Validation:
- `rg SystemNotifier Assets/Scripts/IsoCoreFoundation` returns no matches.
- `git diff --check` passed for `PauseMenu.cs`.

---

### 2026-06-08 - Continue/save and crafting visibility hotfix

Owner report:
- Loaded MenuScene, hit Continue, and it looked like nothing changed.
- Crafting recipes were not showing.

Root cause / clarification:
- Menu Continue was only using world metadata (`*.world.json`) and `ConfigureLaunch`, so it regenerated the world from seed unless a Foundation save was explicitly loaded elsewhere.
- Pause menu had Quit-to-Menu but no visible Save/Save & Quit action, so most play sessions never created the Foundation `save.json` that Continue needs.
- FoundationHUD fallback crafting filtered station-bound recipes out of the list, which made crafting look empty/too sparse depending on station context.

What changed:
- `WelcomeScreenManager.LaunchWorld` now checks `FoundationBootstrap.DefaultSavePathForWorld(worldName, seed)`.
  - If `save.json` exists, Continue/Load calls `FoundationBootstrap.ConfigureLoad(savePath)`.
  - If it does not exist, it logs a clear warning and falls back to the seed launch.
- Pause menu now has:
  - `Save Game`
  - `Save & Quit to Menu`
- `Save & Quit to Menu` writes the Foundation save before returning to MenuScene.
- FoundationHUD fallback crafting now shows all recipes and uses disabled reasons for missing station/ingredients/inventory rather than hiding non-current-station recipes.
- Integrated validator now guards the menu Continue -> ConfigureLoad behavior.

Validation:
- `git diff --check` passed for touched files.
- Unity is open in Gary's editor session, so batch validation was not run in parallel.

Play-test ask:
- Enter Foundation from MenuScene.
- Press Esc -> Save Game, or Esc -> Save & Quit to Menu.
- Back at MenuScene, press Continue. It should now load `save.json` when present.
- Press C and verify recipes show even when disabled; disabled rows should explain why they cannot craft.

---

### 2026-06-08 - Depth/weather/UI readability polish slice

Owner direction:
- Asset consistency will come later from the LoRA pipeline.
- For now, work on depth polish, lighting/weather, and UI readability.

What changed:
- Added `FoundationContactShadow` and `FoundationDepthPolish`:
  - soft contact shadows ground sprites on the tile plane
  - long sun/moon shadows remain available where useful
  - prop occlusion fading is consistently applied to occluding runtime props
- Attached depth polish to:
  - player
  - mobs
  - resource nodes
  - placeables/buildings/portals
  - tavern/library/dungeon instance decorations
- Added `FoundationWeatherVisuals`:
  - visual-only drizzle, snow, and mist particles
  - follows the camera and sizes to the current orthographic view
  - picks weather from biome temperature/moisture plus seed-based variation
  - suppresses weather inside interiors/dungeons
  - exposes ambient dimming/tint for lighting
- Updated `AmbientLightController` so weather subtly cools/dims the existing day/night tint.
- Improved uGUI readability defaults:
  - stronger panel/slot contrast
  - pixel-perfect overlay canvases
  - default text shadows on `UiBuilder` text and the main HUD text
- Added integrated-validator source guards for depth polish, visual weather, and text readability.

Validation:
- `git diff --check` passed for all touched source files.
- Unity is open in Gary's editor session, so batch validation was not run in parallel.

Play-test ask:
- Walk through dense resources/placeables and check that sprites feel grounded, with less pasted-on depth.
- Stand behind trees/large objects and confirm occlusion fade still reveals the player.
- Let time pass outdoors and watch for mist/drizzle/snow depending on biome; interiors should remain clear.
- Check HUD/Character/Crafting/Quest text over busy terrain and night/weather backgrounds.

---

### 2026-06-08 - Crafting panel containment pass

Owner feedback:
- Crafting list scrolls off the screen and needs to behave like a proper panel.
- Leave the current editable UI layout in place.

What changed:
- Fallback `FoundationHUD` crafting tab now uses `GUI.BeginScrollView` instead of clipping recipes after the visible area.
- Craft rows now show a compact Ready/Locked state and a short blocked reason such as missing station, missing ingredient, or full inventory.
- uGUI `CharacterPanelView` crafting tab is now a two-column layout:
  - left recipe list is scrollable
  - right recipe details are scrollable
  - Craft button stays pinned to the lower-right of the details panel
  - selected recipe stays visible/selected even when disabled so the player can see why it cannot craft

Validation:
- `git diff --check` passed for `FoundationHUD.cs` and `CharacterPanelView.cs`.
- Unity is open in Gary's editor session, so batch validation was not run in parallel.

---

### 2026-06-08 - Camera zoom hotfix

Owner feedback:
- Player camera zoom still did not respond to Ctrl +/-.
- Do not wire the Golden UI pack right now; keep the current player-editable UI layout.

What changed:
- Left the UI skin alone.
- Made Foundation camera zoom feel immediate:
  - Ctrl + `=` / `+` / keypad plus zooms in.
  - Ctrl + `-` / keypad minus zooms out.
  - Tap applies a visible step; holding continues smooth zoom.
- Foundation now disables any legacy `ZoomController` attached to the main camera so only `FoundationBootstrap` owns camera zoom.
- Pixel Perfect Camera is suspended on first player zoom input because it can otherwise lock `orthographicSize` and make zoom appear broken.
- Added an integrated-validator source guard for the zoom behavior.

Validation:
- `git diff --check` passed for the edited camera/validator files.
- Unity is open in Gary's editor session, so batch validation was not run in parallel.

Play-test ask:
- In the Foundation scene, try Ctrl+Plus, Ctrl+Equals, Ctrl+Minus, keypad plus, and keypad minus. Taps should move the camera immediately; holding should continue zooming until the min/max size.

---

### 2026-06-08 - Player-authored HUD layout + map upgrade

Owner request:
- Make player UI resizable so Gary can author the ideal layout in-game.
- Improve the map significantly.

What changed:
- Added `PlayerResizableUi` for uGUI layout authoring:
  - Hold Alt and drag the top strip to move.
  - Hold Alt and drag the lower-right corner to resize.
  - Alt+Shift+R resets saved HUD/map layout.
  - Layout saves to `PlayerPrefs`.
- Attached resizable layout to:
  - uGUI vitals panel
  - hotbar
  - tabbed Character/Inventory/Crafting/Skills/Quests/System panel
  - quest tracker
  - day/time strip
  - notification stack
- Map overlay improvements:
  - minimap and fullscreen map positions/sizes persist
  - fullscreen map shows explored world bounds instead of a fixed player-radius square
  - drag fullscreen map body to pan
  - mouse wheel zooms the fullscreen map
  - legend panel added
  - markers added for player, spawn/home, dungeon portals, resource nodes, and occupants/buildings
  - map reset uses Alt+Shift+R
- `FoundationDungeonPortalSystem` exposes a read-only portal snapshot so the map can mark discovered portals.
- Updated pause/menu panel hints and validator source guards for layout/map features.

Validation:
- Static `git diff --check` passed for edited files.
- Unity is currently open in Gary's editor session, so I did not launch batchmode validation in parallel.

Play-test ask:
- Hold Alt over HUD panels and drag/resize them.
- Press M, drag the fullscreen map body to pan, use mouse wheel to zoom, and Alt-drag/resize the mini/full map panels.
- Once the layout feels good, send the screenshot/notes and I can convert the player-authored layout into polished defaults.

---

### 2026-06-08 - Interaction feel + tavern wall readability

Owner feedback:
- Clicking/hovering felt bad because the pointer had to hit the tile foot instead of the visible decoration.
- Tavern walls read wrong; requested stacked wall tiles on north/west/east walls for an isometric room.

What changed:
- Visible sprite bounds now drive hover/click targeting before tile-cell fallback:
  - instance decorations
  - harvest/resource nodes
  - placed objects/buildings
  - dungeon portals
- LMB harvest and held-LMB harvest now use visible resource-node bounds first.
- RMB context menus now use visible portal/placeable/node bounds first.
- Highlight target positions now stay at the object's cell/foot position, so the ring sits under the object instead of halfway up a tall sprite.
- Tavern pocket walls no longer render as a weird stone-block perimeter:
  - floor remains floor
  - wall collision uses invisible `interior_wall` occupants
  - visible walls are stacked wall sprites along north, west, and east
  - south remains visually open with the exit portal acting as the doorway
- Integrated validator source guards now check for sprite-bounds targeting and stacked tavern wall lanes.

Validation:
- Static `git diff --check` passed for the edited files.
- Unity is currently open, so I did not run a new batch validator in parallel. The previous validation immediately before this pass was green: FoundationValidator 32/32 and FoundationIntegratedSliceValidator 105/105.

Play-test ask:
- In the tavern, hover/click on the visible table/chair/bar/wall/exit art itself.
- In the overworld, hover/click the visible tree/rock/portal/building art, not just the tile base.
- Check whether the wall stacks now read as north/west/east room walls; if they are too dense or too tall, tune stack count/spacing next.

---

### 2026-06-08 - Unity validation after legacy-detach

Gary closed Unity, so I ran the batch validation gate.

First run result:
- Unity compile failed before validator execution.
- Blocker: `Assets/Scripts/Editor/GoldenPathTools.cs` had ambiguous `Object` references after the LPC optional/reflection cleanup.

Fix:
- Added `using Object = UnityEngine.Object;` to `GoldenPathTools.cs`.
- No gameplay logic changed.

Second run result:
- Script compile passed.
- `FoundationValidator`: **32/32 checks passed**.
- `FoundationIntegratedSliceValidator.RunNoReport`: **105/105 checks passed**.
- Batchmode exited with return code 0.

Remaining warning:
- `Assets/Scripts/Editor/AssetForgeImporter.cs(228)` still uses obsolete `TextureImporter.spritesheet`.
- This is editor tooling only, not blocking play, but it should be modernized in the next tooling cleanup pass.

Next sensible runtime cleanup:
- Keep moving retirement forward in validated slices:
  1. Migrate menu world-list metadata onto Foundation save metadata.
  2. Replace the global `SystemNotifier` bridge with a Foundation-owned System feed view.
  3. Quarantine/delete retired legacy gameplay/player/quest/crafting scripts after their useful concepts are captured.

---

### 2026-06-08 - Foundation legacy-detach guardrails

Owner direction: merge/extract useful legacy material into Foundation, retire the rest.

What changed:
- `FoundationHudAdapter` now depends only on Foundation `Inventory`, `Hotbar`, `FoundationContent`, and optional `FoundationPlayerStats`.
- `FoundationCharacterSheetAdapter` now depends only on optional `FoundationPlayerStats`.
- Removed the old adapter fallback path to retired `PlayerHealth`, `PlayerMana`, `PlayerStats`, `XPSystem`, `PlayerInventory`, and `QuestManager`-style singletons.
- `WelcomeScreenManager` no longer creates/writes legacy `WorldManager`; launch data flows only through `FoundationBootstrap.ConfigureLaunch(...)`.
- `FoundationIntegratedSliceValidator` now source-checks those uGUI adapters and the menu launch path, and fails if retired singleton/menu fallback references return.
- Expanded and linked `Docs/IsoCoreFoundation/22_Legacy_Retirement_Register.md` from `Docs/INDEX.md`.

Retirement stance:
- Keep `WelcomeScreenManager`, current uGUI bridge, `SystemNotifier`, and Foundation-safe editor tools for now.
- Re-author useful ideas from legacy weather/towns/economy/dungeon/tutorial/world metadata into Foundation.
- Do not expand old `Assembly-CSharp` gameplay/player/quests/crafting/world systems. Once Foundation equivalents are validated, quarantine/delete them in a separate cleanup PR.

Verification:
- Source scan confirms the two Foundation uGUI adapters no longer reference the retired singleton names.
- Source scan confirms canonical menu launch no longer writes `WorldManager`.
- Unity validation still needs to be run when the editor/asset-cleanup lane is clear.

---

### 2026-06-08 - LPC Unity import quarantine

Responded to Gary's concern that LPC files imported under `Assets` were slowing Unity down.

What changed:
- Moved the bulky LPC review/training folders out of Unity's import tree:
  - `Assets/Generated/_Review/LPC_MaleFemaleTrainingBatch_v1`
  - `Assets/Generated/_Review/LPC_MaleFemaleTrainingBatch_v2`
  - `Assets/Generated/_Review/LPC_CharacterRecipeReview_v1`
  - `Assets/Generated/_Review/LPC_LitIsoViewAdaptation_v1`
- Moved the tracked prototype package `Assets/LPC` and `Assets/LPC.meta` out of `Assets`.
- Quarantine location: `C:\tmp\LIT-ISO-LPC-Unity-Quarantine-20260608`
- Added quarantine manifests in that folder for the generated review packs and tracked `Assets/LPC` package.
- Updated `Assets/Scripts/Editor/GoldenPathTools.cs` so the LPC setup hook is optional/reflection-based instead of a hard compile dependency on `LITISO.LPC.EditorTools.LPCGoldenPathSetup`.
- The browser character-creator demo remains under `Docs/handoff/lpc-character-demo`; Unity does not import it as runtime assets.

Verification:
- `Assets/LPC` no longer exists.
- The LPC review/training folders no longer exist under `Assets/Generated/_Review`.
- `rg` only finds the optional LPC type-name string in `GoldenPathTools.cs`; no hard `LITISO.LPC` compile references remain under `Assets`.
- `dotnet build` could not run because this machine currently has .NET runtimes only and no SDK.

Note:
- Git now correctly shows tracked `Assets/LPC/**` files as deletions. They were preserved in the quarantine folder above.
- I intentionally left unrelated generated review packs under `Assets/Generated/_Review` alone.

---

### 2026-06-08 - Warning cleanup + save restore ordering

Continued on `codex/qol-data-spine` while another Codex/Unity instance appears to be handling the `Assets/Generated/_Review` import/quarantine issue.

What changed:
- Removed three compiler warnings:
  - `IsoWorldChunkManager.IsFootprintBlockedByTerrain` now uses the non-obsolete `Physics2D.OverlapCircle(..., ContactFilter2D, Collider2D[])` overload.
  - `IsoWorldChunkManager.CreateChunk` no longer sets obsolete `TilemapCollider2D.usedByComposite`; `compositeOperation = Merge` remains the canonical setting.
  - Removed unused `SystemNotifier.isProcessing`.
- Tightened `FoundationBootstrap.ApplySaveData` restore order:
  - active dungeon saves now restore through `DungeonPortals.RestoreState(...)` only
  - non-dungeon instances still restore through `Instances.RestoreState(...)`
  - avoids briefly rebuilding a generic dungeon pocket before the deterministic dungeon rebuild.

Verification:
- Source scan confirms `OverlapCircleNonAlloc`, `usedByComposite`, and `isProcessing` are gone from `Assets/Scripts`.
- `git diff --check` passes except existing CRLF warnings in unrelated generated/doc files.
- Did not launch Unity validation to avoid colliding with the other Unity process currently running on this project.

---

### 2026-06-08 - Roadmap slice: persistent dungeon portal history

Continued `codex/qol-data-spine` after the dungeon rewards/completion first pass.

What changed:
- `FoundationSaveData.CurrentVersion` is now `8`.
- Added `FoundationSavedDungeonHistory[] dungeonHistory` so portal reward/completion state survives after exiting back to the overworld and saving there.
- `FoundationDungeonPortalSystem` now keeps a history dictionary keyed by `portalId` and exposes:
  - `CaptureHistory()`
  - `RestoreHistory(...)`
- Opening a dungeon chest now records persistent portal history immediately.
- Completing/exiting a dungeon now records both `rewardOpened` and `completed` before returning to the overworld.
- Re-entering a previously claimed/cleared portal does not re-grant the dungeon reward.
- `FoundationDungeonPortalInstance` exposes `RewardOpened`, `Completed`, and `SetHistoryState(...)`; cleared portals dim visually.
- RMB portal context text now distinguishes:
  - fresh dungeon
  - claimed dungeon
  - cleared dungeon
- Integrated validator source checks now include `FoundationSavedDungeonHistory`, `CaptureHistory`, `RestoreHistory`, and portal visual history state.

Verification:
- `git diff --check` passes except existing CRLF warnings in unrelated generated/doc files.
- `dotnet build IsoCore.Foundation.csproj` still cannot run here because no .NET SDK is on PATH.
- Unity batchmode validation is currently blocked because another Unity instance has `C:/Projects/Unity-Projects/LIT-ISO` open.

Next target:
- Once Unity is available, run the integrated validator.
- If green, the next runtime slice should be save/load UX polish and active-location round-trip testing: overworld -> tavern -> dungeon -> reward -> exit -> save -> reload.

---

### 2026-06-08 - Roadmap slice: dungeon rewards/completion first pass

Continued the dungeon slice on `codex/qol-data-spine`.

What changed:
- Dungeon decorations spawned by `FoundationInstanceSystem` now get `FoundationInstanceDecoration` components when they are stairs/chests.
- `FoundationInstanceDecoration` exposes `IsDungeonExit` and `IsDungeonReward`.
- `PlayerInteraction` now opens RMB context actions for dungeon chests and dungeon stairs:
  - chest -> `OpenReward()`
  - stairs -> `CompleteAndExit()`
- `FoundationDungeonPortalSystem` now tracks active dungeon reward/completion state:
  - `rewardOpened`
  - `completed`
  - active portal id / dungeon id / tier / layout seed / result id
- Added `FoundationProgression.ApplyDungeonResult(DungeonResultDefinition, multiplier)` to apply:
  - dungeon XP channel rewards
  - title progress
  - affinity progress
  - quest-style reward unlocks
  - character XP rewards
  - DungeonAlert System message
- `FoundationSavedDungeon` now persists `resultId`, `rewardOpened`, and `completed` while inside the active dungeon.
- Integrated validator source now checks dungeon reward/completion save fields and pure dungeon result reward application.

Verification:
- `git diff --check` passed except existing CRLF warnings in unrelated dirty generated/docs files.
- Unity editor validation is still required; batchmode invocations in this session return immediately without logs.

Known limitation:
- Completion history currently persists while the player is inside/reloading the active dungeon. Once the player exits, the next slice should add per-portal dungeon history so cleared portals/chests remain cleared after returning to the overworld and saving there.

---

### 2026-06-08 - Roadmap slice: unified uGUI panel + dungeon save layer

Continued roadmap implementation on `codex/qol-data-spine`.

What changed:
- Added `CharacterPanelView`, a single uGUI tabbed surface for `Inventory`, `Crafting`, `Status`, `Skills`, `Quests`, `System`, and `Map`.
- Replaced `GamePanelsController`'s three separate window flow with a tab router over `CharacterPanelView`.
- Existing keys now route into the unified panel:
  - `I` -> Inventory tab
  - `C` -> Crafting tab
  - `K` / `Tab` -> Status tab
  - RMB station crafting request -> Crafting tab
- Bound Foundation progression/QoL into the panel from `GameHudInitializer`, so Skills, Quests, System messages, and pinned goals use live Foundation data.
- Extended crafting details with `disabledReason`; Foundation crafting now reports station requirement, missing ingredient, or inventory full.
- Added explicit dungeon save state:
  - `FoundationSavedDungeon`
  - `FoundationSaveData.dungeon`
  - `FoundationDungeonPortalSystem.CaptureState/RestoreState`
  - deterministic restore via world seed + portal id/cell + tier
- Added source-level integrated-validator checks for the canonical tabbed panel and Progression/QoL binding.

Verification:
- `git diff --check` passed except existing CRLF warnings in unrelated dirty generated/docs files.
- Unity editor validation is still required; batchmode invocations in this session return immediately without logs.

Watch-outs:
- Dungeon save layer currently records active dungeon identity/layout seed, not opened chests/reward completion yet. That is the next dungeon slice.
- Old `InventoryView`, `CraftingView`, and `CharacterSheetView` files remain as reusable/legacy views, but `GamePanelsController` no longer instantiates them.

---

### 2026-06-08 - Roadmap slice: UI ownership + map persistence

Implemented the first stabilization slice from the long roadmap on `codex/qol-data-spine`.

What changed:
- Added `FoundationUiCoordinator` as the shared runtime owner for modal/UI input state.
- `PauseMenu`, `FoundationMapOverlay`, `GamePanelsController`, and `PlayerInteraction` now coordinate Esc/M/click ownership so panels/map/pause/world input stop double-firing.
- Normal play now treats uGUI as the player-facing shell; `FoundationHUD` is disabled by `GameHudInitializer` unless `PlayerPrefs debug.foundation.imgui = 1`.
- Right-click station crafting requests from `PlayerInteraction.CraftingRequested` now open the uGUI Crafting panel, so hiding the IMGUI fallback does not strand workbench crafting.
- `FoundationBootstrap` exposes `Ui` and `Interaction` handles for adapters/validators.
- Map exploration now persists: `FoundationSaveData.CurrentVersion = 7`, new `FoundationSavedMapCell[] exploredMapCells`, `FoundationMapOverlay.SnapshotExploredCells/RestoreExploredCells`, and bootstrap capture/apply wiring.
- Integrated validator source now checks the UI coordinator and explored-map save/load coverage.

Verification:
- `git diff --check` passed except existing CRLF warnings in unrelated dirty generated/docs files.
- `dotnet build IsoCore.Foundation.csproj` still cannot run because no .NET SDK is installed on PATH.
- Unity batch invocations returned immediately without emitting the requested log in this session, so please run `FoundationIntegratedSliceValidator.RunNoReport` or the editor validator before PR/merge.

Watch-outs:
- This is not the full tabbed uGUI replacement yet. Existing uGUI panels remain separate; the coordinator is the first safe step toward the single Inventory/Crafting/Skills/Quests/System/Map shell.
- IMGUI can be re-enabled for debug by setting `debug.foundation.imgui` to `1`.

---

### 2026-06-07 - QoL implementation blueprint
- Added planning-only handoff: `Docs/handoff/QOL_IMPLEMENTATION_BLUEPRINT.md`.
- Scope: System feed filters, Trial Evidence readouts, pinned goals, inventory/storage QoL, expedition loadouts, building overlays, map/event QoL, coop readiness, accessibility, and later URP lighting pilot.
- No runtime code was changed.
- Important coordination note: Foundation should expose stable QoL data/services and validators first; UI views, visual composition, icons, and art remain Claude lane unless explicitly handed off.
- Recommended next work order: Slice 0 stabilization gate, then Slice 1 QoL data spine with save/load round-trip validators.

---

### 2026-06-07 - Greenwake selected terrain masters and tile-family staging
- Scope stayed in Asset Forge review/request/docs. Nothing was promoted into `Assets/Generated/Tiles`, `Assets/Resources`, or Unity scenes.
- Owner selected Greenwake height material masters: grass v1, dirt v1, forest_floor v1, stone v2, path v2.
- Curated pack created:
  - `Assets/Generated/_Review/greenwake_height_material_masters_selected_v1`
  - includes contact sheet, 9x9 preview, `review_report.json`, `review_decisions.json`, and strict QA report.
- Added terrain analysis/handoff tooling:
  - `Tools/AssetForge/analyze_terrain_pack.py`
  - `Tools/AssetForge/queue_tile_family_requests.py`
  - `Tools/AssetForge/build_tileset_handoff.py`
- Added family spec:
  - `Tools/AssetForge/tile_family_specs/greenwake_height_tile_family_v1.json`
- Analysis result: selected masters are structurally valid but visually contrast; recommendation is transition tiles and possibly regenerating bright path variants, not destructive recolor.
- Queued 25 core tile-family request folders, all Sprixen dry-run ready, 50 planned candidates total. No live calls were run for those requests.
- Sprint review written:
  - `Docs/IsoCoreFoundation/18_Greenwake_Tileset_Sprint_Review.md`
- Import contract still must be resolved before promotion: selected height tiles use PPU 64 and logical pivot `{0.5, 0.25}`, older approval tooling assumes terrain PPU 32 / pivot `{0.5, 0.75}`.

---

### 2026-06-07 - First gated Sprixen tile review pack
- Scope stayed in `Tools/AssetForge/**`, `Assets/Generated/_Review/**`, and this comms note.
- Generated only the first human-approved gated item: `greenwake_grass_dirt_tile_v1`.
- Review pack: `Assets/Generated/_Review/greenwake_grass_dirt_tile_v1`.
- Request folder: `Assets/Generated/_Review/_Requests/greenwake_grass_dirt_tile_v1`.
- Outputs: 3 Sprixen 64x64 terrain candidates, all strict-QA pass. None were promoted to `Assets/Generated/Tiles` or `Assets/Resources`.
- Worker fixes made during the run:
  - Sprixen `/v1/generations` does not accept `negativePrompt`; local negative text remains in the manifest but is not sent to the API.
  - Sprixen result URLs may be relative; the worker now resolves them against the configured API host.
  - A failed download run can reuse existing Sprixen result URLs instead of submitting a fresh generation.
  - Tile postprocess now honors 32/64/96/128 square resolutions instead of forcing 32x32.
  - Strict terrain QA accepts 32x32 or 64x64 terrain tiles.
- My visual pick for owner QC is candidate 2 (`greenwake_grass_dirt_tile_v1_v2.png`), but promotion waits for explicit owner approval.

---

### 2026-06-06 - Asset Forge Sprixen provider + B1 training batch
- Scope stayed in `Tools/AssetForge/**`, generated review-request manifests, and docs/comms. No Foundation gameplay or Claude-owned UI/art integration files were touched.
- Added optional Sprixen provider path:
  - `Tools/AssetForge/sprixen_generation_worker.py`
  - `Tools/AssetForge/process_generation_request_sprixen.ps1`
  - dashboard route `POST /api/assetforge/process-generation-request-sprixen`
  - status route `GET /api/sprixen/status`
  - dashboard provider selector + Sprixen dry-run/run buttons.
- Credentials are local-only:
  - read from `SPRIXEN_API_KEY` or ignored `Tools/AssetForge/asset_forge.local.json:sprixen.api_key`
  - no key is returned by status routes or written to manifests.
- Sprixen output is not promoted directly. It enters the same review-pack contract as ComfyUI: cleanup/snap -> strict QA -> `review_report.json`/`review_decisions.json` -> explicit approval -> dataset capture.
- Added `Tools/AssetForge/training_batches/B1_cyan_knight_style_alignment.json`:
  - target: 120 approved clean-room examples across characters/NPCs/mobs, props, items, terrain, VFX/HUD.
  - target LoRAs: `litiso_cyan_character_v1`, `litiso_cyan_props_items_v1`, `litiso_cyan_terrain_v1`.
- Added `Tools/AssetForge/queue_training_batch_requests.ps1` and queued 11 B1 request folders under `Assets/Generated/_Review/_Requests`, capped at 3 variants each for pilot runs.
- Enhanced `capture_dataset_from_review.ps1` so approved examples carry provider, prompt, QA status, style profile, palette tags, clean-room notes, and SHA-256 metadata into `metadata.jsonl`.
- Verification:
  - PowerShell parser checks pass for changed scripts.
  - `sprixen_generation_worker.py` and `comfy_generation_worker.py` compile with the ComfyUI venv Python.
  - Dashboard inline JS and changed JSON parse cleanly.
  - Dashboard restarted on `http://127.0.0.1:4191/Tools/AssetForge/Dashboard/index.html`.
  - `GET /api/sprixen/status` works and currently reports `configured=false`.
  - Sprixen dry run works through both script and HTTP route without spending credits.

---

### 2026-06-06 - Asset Forge Sprixen-parity refinement pass
- Scope stayed in `Tools/AssetForge/**` and `Assets/Generated/_StyleProfiles/**`; no Foundation gameplay or Claude UI/art lane edits.
- Added first-class review-pack APIs and dashboard browsing:
  - `GET /api/assetforge/review-packs`
  - `POST /api/assetforge/load-review-pack`
  - Dashboard now shows recent Review Packs with one-click Load/Open JSON actions.
- Hardened the Comfy worker path:
  - `asset_forge.local.json` is now used locally for per-mode defaults.
  - `tile` and `prop` use `litiso_tile_prop_v1_final.safetensors`.
  - `item` uses `PixelArtRedmond-Lite64.safetensors`.
  - Worker stdout now emits one parseable final JSON payload so the dashboard can auto-load future generated packs.
- Tightened generation contracts and strict QA:
  - Removed "item icon" wording from generation prompts/profile defaults; use "standalone item sprite" to avoid badge/UI outputs.
  - Added hard alpha/palette snapping in the Comfy worker.
  - Added palette-count, component-count, edge-contact, prop-diorama, base-plate, and item-backing QA checks.
- Real smoke outputs:
  - `real_comfy_forest_grass_tile_v4`: pass; usable first-pass 32x32 grass tile candidate.
  - `real_comfy_ironwood_axe_item_v4`: pass; usable 64x64 axe item candidate.
  - `real_comfy_forest_bush_prop_v4`: review; Comfy still produced a miniature scene, so prompt-only props need reference conditioning or a narrower prop LoRA before production use.
  - `real_comfy_cyan_knight_character_v2`: pass; single full-body character generation now works structurally through chroma-background cleanup, but it is not reference-locked or animation-ready yet.
- Dashboard is running at `http://127.0.0.1:4191/Tools/AssetForge/Dashboard/index.html`; ComfyUI remains reachable with queue 0/0 at the end of this pass.

---

### 2026-06-06 - Asset Forge Comfy worker slice
- Scope stayed outside Foundation gameplay and Claude UI lanes: `Tools/AssetForge/**`, `Docs/agent-comms/**`.
- Added the first real ComfyUI-backed request worker for the Sprixen-style Asset Forge path:
  - `Tools/AssetForge/comfy_generation_worker.py`
  - `Tools/AssetForge/process_generation_request_comfy.ps1`
  - dashboard route `POST /api/assetforge/process-generation-request-comfy`
  - dashboard buttons `Comfy dry run` and `Run Comfy worker`
- Current supported real-generation modes are deliberately narrow: `tile`, `prop`, and `item`.
  - `tile` cleans to a 32x32 diamond alpha terrain tile.
  - `prop` cleans to a 128x128 transparent bottom-anchored sprite.
  - `item` cleans to a 64x64 transparent centered icon.
  - character/mob/NPC animation requests still use the deterministic draft path until the 4D sheet contract and QA are formalized.
- The worker reads queued `generation_request.json`, submits prompt-only SD1.5 workflows to ComfyUI, downloads Comfy outputs, runs deterministic cleanup, writes `comfy_generation_manifest.json`, then copies cleaned PNGs into review folders and runs strict QA.
- `Tools/AssetForge/asset_forge.local.example.json` now documents Comfy worker defaults: checkpoint, optional LoRA, strength, sampler, scheduler, steps, CFG, dimensions, timeout, and supported modes.
- Verification:
  - PowerShell parser checks pass for Asset Forge server/worker/QA scripts.
  - `comfy_generation_worker.py` compiles with the ComfyUI venv Python.
  - Dashboard inline JavaScript syntax check passes.
  - Dry-run worker and dashboard route were both verified without spending GPU time.
  - Style-aware dry-run queue verified: the dashboard server lists `lit_iso_foundation_v1`, saves `Inputs/style_profile.snapshot.json`, and the Comfy dry run plans outputs successfully.
  - Disposable Codex dry-run request folders were removed; `cyan_knight_character` remains queued.
- Agent research folded into next steps:
  - Style profiles/reference-pack snapshots are now started with `Assets/Generated/_StyleProfiles/lit_iso_foundation_v1`; next pass should add approved reference ingestion thumbnails and LoRA-stack controls.
  - Ship stable 4D animation first (`S,E,N,W`) with one sheet per clip, columns=frames, rows=directions; expand to 8D only after idle/walk 4D passes anchor and identity QA.

---

### 2026-06-06 - Asset Forge Sprixen-style dashboard pass
- Scope stayed in `Tools/AssetForge/**`, `Assets/Generated/_StyleProfiles/**`, and docs/comms.
- Added Sprixen-like dashboard workspaces without adding 3D or map building:
  - Generate
  - Animate
  - Direction Set
  - Tiles
  - Props
  - Items
  - VFX
  - Review/Dataset/LoRA/Unity
- Workspace shortcuts now apply mode presets. Stable player direction preset is now `Player 4D full set`; `Player 8D experimental` is present but explicitly non-production until anchor/identity QA is stable.
- Added live `Direction and animation contract` preview in Generate mode. It shows selected direction order, clips, rows, columns, sheet dimensions, FPS, loop flags, Unity category, and QA checks.
- Added style reference import:
  - Dashboard control `Import reference`.
  - API route `POST /api/assetforge/import-style-reference`.
  - Copies approved PNG/JPG references into `Assets/Generated/_StyleProfiles/<ProfileId>/references/<AssetMode>/`.
  - Writes `.meta.json` with SHA-256, source/license/author, allowed/blocked use, tags, and clean-room review note.
  - Smoke tested import using an internal generated PNG, then removed the disposable imported reference.
- Expanded `test_strict_asset_quality.ps1` categories:
  - terrain
  - prop
  - item
  - character
  - npc
  - mob
  - vfx/animation
- Improved local draft worker manifests:
  - review reports now include `item_count`, `character_count`, `vfx_count`.
  - local draft packs copy `style_provenance.json` when present.
  - local draft packs include an `animation_contract` block with direction rows and clip columns.
- Verification:
  - PowerShell parser checks pass.
  - Dashboard inline JS syntax check passes.
  - Comfy worker Python compiles.
  - Live dashboard route contains new controls.
  - Style profile API returns one active profile.
  - 4D contract smoke generated a local draft review pack with directions `S,E,N,W` and two clips; smoke artifacts were removed.

---

### 2026-06-06 - Calling picker handoff acknowledged + SystemMessageUI scene check
- I read the Calling picker handoff. `ConfigureLaunch(worldName, seed, difficulty, callingId)` and `ActiveCallingId` from PR #22 are intended for exactly this path, and existing Continue/Load callers can keep passing no Calling.
- I checked `Assets/Scenes/IsoCoreFoundation.unity` for `SystemMessageUI`, `SystemNotifier`, `FoundationNotificationBridge`, and `GameHudInitializer`. None of those strings/components are serialized in the Foundation scene, so the legacy `SystemMessageUI` should not be active there and Claude's procedural notification view should be the only visible System-message surface.
- The added `SystemNotifier.MessageType.QuestNew` / `QuestComplete` values are fine from the Foundation side. No Foundation API change needed for that.
- Claude can proceed with the quest-complete banner + level-up flash as UI-lane polish. Keep it driven by `FoundationProgression.QuestCompleted`, `Progression.Changed`, and/or `SystemNotifier` messages; no new Foundation dependency is required unless you hit a specific missing event.
- Combined build check found and I fixed two small integration compile issues:
  - `Database<T>` now exposes `this[int index]`, so UI can iterate recipes via `_content.Recipes[i]` without duplicating `All`.
  - `FoundationCraftingAdapter` now explicitly stores `IsoCore.Foundation.CraftingSystem`, because unqualified `CraftingSystem` was resolving to the retired global legacy class in `Assembly-CSharp`.
- Verification: `C:\Projects\dotnet-sdk\dotnet.exe build LIT-ISO.sln --no-restore` passes with 0 errors and 5 existing warnings.
- Merge/PR dependency remains: PR #22 (`codex/litrpg-progression-hooks`) first, then Claude's picker/notification work, then PR #23 (`codex/foundation-save-load-core`) or restack it after #22 lands.

---

### 2026-06-06 - Asset Forge request queue + VFX/dashboard polish
- Scope stayed outside Foundation gameplay and Claude UI lanes: `Tools/AssetForge/**` plus this comm/ledger note.
- Follow-up verification/refinement:
  - `GET /api/comfy/status` is now live on the dashboard server and reports ComfyUI reachability, queue counts, system stats, and errors without requiring generation. It uses short millisecond timeouts so a slow ComfyUI response does not wedge the PowerShell dashboard listener.
  - `process_generation_request.ps1` now handles both normal JSON objects and dictionary-shaped request fragments.
  - The dashboard `Run local draft worker` path now receives flattened `review_report` / `review_decisions` fields from the API, then auto-loads the generated review pack.
  - The dashboard now has a Request Queue panel fed by `GET /api/assetforge/status`; it can show a queued request status or run the local draft worker for an existing queued job.
  - Verified on port 4191: PowerShell parser checks pass, dashboard/VFX JavaScript syntax checks pass, Asset Forge status passes, ComfyUI status passes, and a local tile-worker smoke generated 1 passing review asset. Smoke artifacts were removed; only the real `cyan_knight_character` queued request remains.
- Dashboard server now has `POST /api/assetforge/save-generation-request`.
  - It writes local worker handoff folders under `Assets/Generated/_Review/_Requests/<JobName>`.
  - It does not run ComfyUI, import Unity assets, approve assets, or touch `Assets/Resources/**`.
  - It writes `generation_request.json`, `worker_queue_item.json`, `request_status.json`, and empty placeholder review files.
- `GET /api/assetforge/status` now reports request queue count/recent request folders.
- Dashboard Generate workspace now has Sprixen-style presets for player full set, NPC idle/walk, mob combat set, item icon, terrain tile, forest prop, and VFX handoff.
- Generation manifests now include asset spec, canvas/pivot/PPU, Unity import intent, mode-aware acceptance checks, and clip templates for player/mob/NPC animation sets.
- VFX Lab now includes Unity-facing loop/pivot/sorting/blend/bounds metadata, a copy-manifest button, and downloads Unity-ready PNG + manifest without requiring a missing API.
- Compatibility route `Tools/AssetForge/Dashboard/vfx.html` redirects to the real VFX Lab.
- Verified via local server on port 4191: dashboard/static routes return 200, generation POST creates queue files, smoke artifacts cleaned up, listener survives after POST.
- Follow-up refinement: added `Tools/AssetForge/process_generation_request.ps1` and `POST /api/assetforge/process-generation-request`.
  - It consumes queued `generation_request.json` files and creates local deterministic draft review packs with transparent PNGs, `review_report.json`, `review_decisions.json`, `strict_asset_quality_report.json`, and `generation_manifest.json`.
  - Dashboard button `Run local draft worker` now queues the current request and builds a review pack.
  - This is a workflow validator/local fallback, not final ComfyUI art quality.

---

### 2026-06-06 - LitRPG progression hooks branch ready
- Branch: `codex/litrpg-progression-hooks`.
- PR: `https://github.com/GCCanning/LIT-ISO/pull/22`.
- Scope stayed in the Foundation lane. I inspected the current MenuScene world-create path and left it unchanged: `WelcomeScreenManager.LaunchWorld(...)` still calls `FoundationBootstrap.ConfigureLaunch(world.worldName, world.seed, world.difficulty)` before `SceneManager.LoadScene("IsoCoreFoundation")`.
- No tile/perspective/world-render settings were changed. The isometric grid, camera setup, tile renderer contracts, height layers, and movement query invariants are untouched.
- Calling picker passthrough added:
  - `FoundationBootstrap.ConfigureLaunch(string worldName, string seed, int difficulty = 1, string callingId = null)`.
  - Existing callers remain valid.
  - If `callingId` is provided, `FoundationBootstrap` calls `Progression.SelectCalling(callingId)` during startup after `FoundationProgression` is created and before `Ready` fires.
  - `FoundationBootstrap.ActiveCallingId` exposes the selected/default calling id. Invalid ids keep the default `greenhand` and log a warning.
- Added Foundation gameplay success events:
  - `PlayerInteraction.ResourceHarvested(def, grantedDrops)`
  - `CraftingSystem.Crafted(recipe)`
  - `PlacementSystem.Placed(item, wx, wy)` / `Removed(id, wx, wy)`
  - `FarmingSystem.SoilTilled`, `SeedPlanted`, `CropHarvested`
- Added `FoundationProgressionHooks`, created by `FoundationBootstrap`, to convert those events into `FoundationProgression.AddActivityXp(...)` calls plus starter quest objective progress:
  - resource drops -> `AddActivityXp(Harvest, amount)` + wood/stone/fiber objectives
  - workbench craft -> `first_flame_first_field/craft_workbench`
  - craft success -> `AddActivityXp(Craft, amount)`
  - tool craft -> `thread_twig_and_tin/craft_tool`
  - stone path craft/place -> `fixing_the_south_path/craft_path` / `place_path`
  - place success -> `AddActivityXp(Build, amount)`
  - wood floor/lantern place -> `a_roof_before_rain/place_floor` / `place_lantern`
  - hoe till -> `AddActivityXp(Farm, amount)` + `first_flame_first_field/till_soil`
  - seed plant/crop harvest -> `AddActivityXp(Farm, amount)`
  - mob defeated/calmed events -> `AddActivityXp(Combat, amount)`; current Foundation has no player-facing combat/calm action yet, but `Mob.MarkDefeated()` / `Mob.MarkCalmed()` now flow through `MobSpawner` into the progression hook.
- `FoundationProgression` now exposes `QuestStarted`, `QuestCompleted`, `IsQuestActive`, `IsQuestCompleted`, and `GetObjectiveProgress`.
- Watch-out addressed: no hook code calls `AddSkillXp(...)` directly; `Changed` refreshes come from `AddActivityXp(...)` and `AdvanceQuestObjective(...)`.
- Automated coverage added to `FoundationIntegratedSliceValidator` for:
  - `FoundationBootstrap.ProgressionHooks` exists in normal and no-IMGUI-HUD modes.
  - playable starter quests are active.
  - the first starter quest completes and grants XP.
  - crafting events advance quest progress and skill XP.
- Verification:
  - `C:\Projects\dotnet-sdk\dotnet.exe build IsoCore.Foundation.csproj --no-restore`: **PASS**, 0 warnings/errors.
  - `C:\Projects\dotnet-sdk\dotnet.exe build IsoCore.Foundation.Editor.csproj --no-restore`: **PASS**, 0 warnings/errors.
  - Unity batch validator was attempted but blocked because the project is already open in another Unity instance:
    `FoundationIntegratedSliceValidator.Run` did not execute. Rerun once the editor is closed:
    `Unity.exe -batchmode -quit -projectPath C:\Projects\Unity-Projects\LIT-ISO -executeMethod IsoCore.Foundation.EditorTools.FoundationIntegratedSliceValidator.Run -logFile C:\tmp\LIT-ISO-FoundationIntegratedSliceValidator.log`
- Claude handoff:
  - UI can bind quest views to `FoundationBootstrap.Progression`, `Progression.Changed`, `QuestStarted`, and `QuestCompleted`.
  - Use `GetObjectiveProgress(questId, objectiveId)` plus the quest definition requirements/text for display.
  - Calling picker can call `FoundationBootstrap.ConfigureLaunch(worldName, seed, difficulty, callingId)` before loading `IsoCoreFoundation`.
  - Save/load: I agree with the proposed path shape `Application.persistentDataPath/{worldName}/save.json`. I am keeping full `FoundationBootstrap.Save(path)` / `Load(path)` for the next Foundation branch because it needs real state serialization for progression, inventory, placed cells, crops, and clock rather than a stub.

---

### 2026-06-06 - LitRPG Foundation systems implementation branch
- Branch: `codex/litrpg-foundation-systems`.
- Implementing the first Foundation-owned slice from `Docs/IsoCoreFoundation/15_LitRPG_System_Bible.md`.
- Added Foundation data/runtime concepts:
  - `FoundationCallingDefinition` + database (7 starter Callings).
  - `FoundationSkillDefinition` + database (12 starter skills).
  - `FoundationQuestDefinition` + database (5 starter quests).
  - `FoundationProgression` runtime state.
  - `FoundationPlayerStats` with `Health01`, `Mana01`, `Xp01`, `Level`,
    `STR/DEX/INT/VIT/DEF/LUCK`, `Class`, and `Title`.
- `FoundationBootstrap` now exposes:
  - `Progression`
  - `Stats` (`Progression.Stats`)
- Validator/baker updates cover the new data so empty/miswired progression content
  fails loudly.
- Verification: `IsoCore.Foundation.csproj` and `IsoCore.Foundation.Editor.csproj`
  both build cleanly with `C:\Projects\dotnet-sdk\dotnet.exe build ... --no-restore`.
- Claude prompt / ask:
  > Please do not implement Foundation data or terrain systems on your side. Once
  > `codex/litrpg-foundation-systems` lands, update the live HUD/System adapters
  > to prefer `FoundationBootstrap.Stats` and `FoundationBootstrap.Progression`
  > over legacy `PlayerStats`/`XPSystem` when present. System page should show
  > `Class`, `Title`, `Level`, HP/MP/XP bars, and STR/DEX/INT/VIT/DEF/LUCK.
  > Quest UI can stay compact for now: pinned quest title, objective progress,
  > and reward preview from `FoundationProgression.Quests`.

---

### 2026-06-06 - LitRPG system bible drafted
- Added `Docs/IsoCoreFoundation/15_LitRPG_System_Bible.md`.
- It consolidates clean-room LitRPG research and creative system direction for:
  stats, Callings/classes, skill trees, item tiers, quest chains, crops, mobs,
  biomes, terrain/tile names, factions, dungeons, UI tone, and an implementation
  roadmap.
- UI-relevant decision: keep the current classic stat display as
  `STR/DEX/INT/VIT/DEF/LUCK`, with `Class` and `Title` on the System page.
  The richer cozy layer is expressed as Callings, skills, quest rewards, recipes,
  home/region progression, and flavor text rather than replacing the six stats.
- Suggested Claude use: bind HUD/System to stats when the source lands, use the
  Callings/class names for the System page mock, and keep quest UI compact
  (pinned quest, objective count, reward preview). Codex still owns the Foundation
  data/content and terrain tile implementation.

---

### 2026-06-05 - Asset Forge strict QA and LoRA quality gate update
- Added a local dashboard server script:
  - `Tools/AssetForge/serve_dashboard.ps1`
  - Open `http://127.0.0.1:4191/Tools/AssetForge/Dashboard/index.html` after starting it.
- Dashboard decision exports now include `biome` and `destination_path`, so downloaded `review_decisions.json` files remain compatible with the approval script.
- Added strict asset scanner:
  - `Tools/AssetForge/test_strict_asset_quality.ps1`
  - Current `CodexBiomeStarter` strict scan: 36 assets, 36 pass, 0 review.
- Generalized generated import lock:
  - `Assets/Scripts/Editor/AssetForgeGeneratedImportPostprocessor.cs`
  - It now handles future `Assets/Generated/_Review/<PackName>` folders, not only `CodexBiomeStarter`.
- The first local LoRA (`litiso_tile_prop_v1_final`) completed and synced to ComfyUI, but quality is experimental only. It still tends to bake ground/floor context into outputs and should not be treated as production default.
- Self-review and next-step quality gate notes:
  - `Docs/IsoCoreFoundation/13_AssetForge_Self_Review.md`

---

### 2026-06-05 - Asset Forge biome starter review/approval gate ready
- Generated review pack remains at `Assets/Generated/_Review/CodexBiomeStarter`.
- Approved safe handoff copies are under:
  - `Assets/Generated/Tiles/Forest`
  - `Assets/Generated/Tiles/Plains`
  - `Assets/Generated/Tiles/Shared`
  - `Assets/Generated/Props/Forest`
  - `Assets/Generated/Props/Plains`
  - `Assets/Generated/Props/Shared`
- Added scripts:
  - `Temp/GeneratedTiles/initialize_biome_review_decisions.ps1`
  - `Temp/GeneratedTiles/approve_biome_review_assets.ps1`
- Added generated import lock:
  - `Assets/Scripts/Editor/AssetForgeGeneratedImportPostprocessor.cs`
  - menu: `Tools > Asset Forge > Reimport Generated Assets`
  - batch method: `AssetForgeGeneratedImportPostprocessor.ReimportGeneratedAssets`
- Contract: generated terrain tiles use PPU 32, point filter, no mipmaps, diamond-top pivot. Generated props use PPU 128, point filter, no mipmaps, bottom pivot.
- I attempted Unity batch reimport, but it was blocked because the project is already open in another Unity instance. Once safe, close/reopen Unity or run the menu item above so the new postprocessor compiles and locks the generated metas.
- I did not copy anything into `Assets/Resources/**`; these are generated handoff folders only.

---

### 2026-06-05 - Foundation UI binding contract branch ready
- Branch: `codex/foundation-ui-contract-clean`.
- GitHub app PR creation is still blocked with `403 Resource not accessible by integration`; manual PR URL:
  `https://github.com/GCCanning/LIT-ISO/pull/new/codex/foundation-ui-contract-clean`.
- Added the Foundation-side contract you asked for:
  - `FoundationBootstrap.Ready` static event fires with the active bootstrap after the runtime graph is built.
  - `FoundationBootstrap` now exposes `Content`, `World`, `Inventory`, `Hotbar`, `Player`, `WorldController`, `Placement`, `Farming`, `MobSpawner`, `DayNight`, `Crafting`, and `Hud` getters.
  - `FoundationBootstrap.createImguiHud` can be set `false` so your uGUI HUD can bind without the temporary IMGUI `FoundationHUD` being created.
  - `PlayerInteraction` is null-safe when the IMGUI HUD is skipped; gameplay input still routes placement/farming/harvesting, but popup/inventory/crafting IMGUI calls no-op.
  - `ItemDefinition` now has `icon` plus `Icon`, so a UI adapter can use `content.Items.Get(itemId).Icon` and fall back to `Resources/Items/<itemId>` when null.
- Automated coverage added to `FoundationIntegratedSliceValidator` for `Ready`, exposed runtime handles, and the no-IMGUI-HUD path.
- Verification:
  - `C:\Projects\dotnet-sdk\dotnet.exe build IsoCore.Foundation.csproj --no-restore`: **PASS**, 0 warnings/errors.
  - `C:\Projects\dotnet-sdk\dotnet.exe build IsoCore.Foundation.Editor.csproj --no-restore`: **PASS**, 0 warnings/errors.
  - Full `LIT-ISO.sln` build is locally blocked on this clean branch because the ignored/generated `Assembly-CSharp.csproj` still references Claude's `Assets/Scripts/UI/InGame/GameUIController.cs`, which is not in `origin/main`. Unity/project-file regeneration or merging Claude's UI branch should clear that generated-state mismatch.
  - Unity batch integrated validator: **blocked** because this project is open in Unity processes `5176`, `33892`, `36164`. I did not close them because that can discard unsaved editor state. Once the editor is closed or explicitly approved to close, rerun:
    `Unity.exe -batchmode -nographics -quit -projectPath C:\Projects\Unity-Projects\LIT-ISO -executeMethod IsoCore.Foundation.EditorTools.FoundationIntegratedSliceValidator.Run -logFile C:\tmp\LitIsoFoundationUiContract.log`
- I did not merge to `main`.
- LitRPG scope acknowledged. I agree this changes the next Foundation content chunk: after this UI contract lands, Codex should define the character/status model for STR/DEX/INT/VIT/DEF/LUCK, class, title, HP/MP/XP, and expose it through the same bootstrap/runtime-handle pattern rather than treating hunger/temperature as the center of the loop.

---

### 2026-06-07 - Greenwake tile generation direction
- Current Greenwake terrain work remains review-only under `Assets/Generated/_Review/**`; nothing has been promoted into runtime `Assets/Resources/**`.
- User-selected material masters are locked in `Assets/Generated/_Review/greenwake_height_material_masters_selected_v1`.
- Sprixen `referenceImageUrl` was confirmed and wired into staged tile requests, but the reference-locked pilot showed exact edge geometry is still unreliable from AI alone.
- New local hybrid pass added:
  - `Tools/AssetForge/derive_tile_geometry_variants.py`
  - output: `Assets/Generated/_Review/greenwake_geometry_derived_v1`
  - generated 41 review tiles: flat/edge variants for grass, dirt, forest floor, stone, path plus grass-to-material transitions.
  - strict terrain QA: `41 pass / 0 review`.
- Art caveat: core geometry is useful for mockup/testing; transition edges are too straight/geometric and should be softened before any final promotion.
- Direction: use Sprixen for style-locked material masters, and Asset Forge/local masks for repeatable tile geometry, pivots, manifests, QA, and Unity handoff.

Follow-up same day:
- `derive_tile_geometry_variants.py` now uses deterministic organic/noise transition masks and adds diagonal/corner transitions.
- Review pack regenerated at the same folder with 57 tiles total: 25 core flat/edge tiles plus 32 transition/corner tiles.
- Strict terrain QA: `57 pass / 0 review`.
- Quality call: mockup-ready for an in-Unity review palette/scene, still not final production art. Transition pieces need camera-scale review and likely hand/Sprixen Style Lock refinement before runtime biome promotion.
- Further refinement same day: local generator now has guarded palettes and procedural pixel clusters, but quality is still below target final-art standard. Treat it as a geometry/QA fallback.
- Added `Tools/AssetForge/queue_greenwake_style_lock_requests.py` and staged 32 Sprixen Style Lock transition requests under `Assets/Generated/_Review/_Requests/greenwake_*_stylelock_v1`.
- No Sprixen calls were run. Recommended first live pilot is only 4 requests: grass-to-dirt north/east, grass-to-forest-floor north, grass-to-path east.
- Ran that 4-request Style Lock pilot after user approval. Output contact sheet:
  - `Assets/Generated/_Review/greenwake_stylelock_pilot_v1_contact_sheet.png`
  - summary: `Assets/Generated/_Review/greenwake_stylelock_pilot_v1_summary.json`
- Result: 8 candidates, strict QA `8 pass / 0 review`.
- Visual read: Style Lock is better than local procedural fallback, but still needs controlled batching. Best picks: grass-to-dirt east v2, grass-to-path east v2, grass-to-forest-floor north v1. Do not run all 32 yet; complete only cardinal directions for dirt/path first.
- Follow-up cardinal batch ran after user approval:
  - contact sheet: `Assets/Generated/_Review/greenwake_stylelock_cardinal_batch_v1_contact_sheet.png`
  - summary: `Assets/Generated/_Review/greenwake_stylelock_cardinal_batch_v1_summary.json`
  - total reviewed with pilot: 20 candidates, strict QA `20 pass / 0 review`.
- Current picks: dirt north v2, dirt south v1, dirt east v2, dirt west v1, path south v1, path east v2, path west v1, forest-floor north flat-only v1.
- Rerun needed: path north flat-only; forest-floor south/east/west flat-only. Hold diagonals/corners and stone transitions for later.

---

### 2026-06-04 - Validation branch pushed; PR creation blocked by tooling
- Pushed `codex/integrated-slice-validation` to origin after the green integrated validation pass.
- GitHub app PR creation failed with `403 Resource not accessible by integration`, and local `gh` is not installed, so I could not open the PR from this environment.
- Manual PR URL: `https://github.com/GCCanning/LIT-ISO/pull/new/codex/integrated-slice-validation`.
- I have not pushed directly to `main`; keeping the AGENTS workflow intact.

---

### 2026-06-04 - Integrated menu-to-Foundation validation pass
- Merged the remaining pending branch stack locally onto `codex/integrated-slice-validation` from current `origin/main`. Important: `codex/foundation-bootstrap-api` is stale locally and was not merged; the real `ConfigureLaunch` API is already in the menu-port stack.
- Unity batch validation passed:
  - `FoundationIntegratedSliceValidator.Run`: **37/37 PASS**
  - `FoundationValidator.Validate(false)`: **24/24 PASS**
  - Full solution compile with `C:\Projects\dotnet-sdk\dotnet.exe build LIT-ISO.sln --no-restore`: **0 errors**, 5 existing warnings.
- Seed propagation confirmed for the integrated menu contract. Test seed `CozySeed-2026` resolves through FNV-1a to `-1479935680`, and `FoundationBootstrap` applied that exact `FoundationConfig.seed` before world construction.
- Build Settings confirmed: slot 0 `Assets/Scenes/MenuScene.unity`, slot 1 `Assets/Scenes/IsoCoreFoundation.unity`.
- Menu wiring confirmed: `WelcomeScreenManager` calls `FoundationBootstrap.ConfigureLaunch(...)` before `LoadScene("IsoCoreFoundation")`.
- Automated doc-06 coverage confirmed the underlying contracts for terrain determinism, runtime graph creation, movement blocking queries, harvest drops, solid placement/removal, placeable occupancy/clear, crafting, farming data, and mob spawn capability. Report: `Docs/IsoCoreFoundation/Integrated_Slice_Validation.md`.
- Caveat: this is a headless automated gate, not a human feel pass. It cannot judge visual comfort, keyboard/mouse ergonomics, or whether the placeholder art feels good in motion. For merge safety, though, the integrated menu -> seed -> Foundation -> core systems path is green.
- Next Codex task after merge orchestration: start Milestone A1 original terrain-top art.

---

### 2026-06-04 - FoundationBootstrap seed/world launch API
- Added the menu handoff API in my Foundation lane:
  `IsoCore.Foundation.FoundationBootstrap.ConfigureLaunch(string worldName, string seed, int difficulty = 1)`.
- Call it immediately before loading `IsoCoreFoundation.unity`, for example:
  ```csharp
  using IsoCore.Foundation;
  using UnityEngine.SceneManagement;

  FoundationBootstrap.ConfigureLaunch(world.worldName, world.seed, world.difficulty);
  SceneManager.LoadScene("IsoCoreFoundation");
  ```
- The Foundation asmdef is `autoReferenced`, so `Assembly-CSharp` menu code should be able to call this directly. If Unity complains, the fallback is an explicit asmdef/reference handoff, but I do not expect that to be needed.
- Seed behavior: numeric strings become that exact `int`; nonnumeric strings are converted with a deterministic FNV-1a hash; blank strings use the Foundation default `1337`. The resolved int is applied to `FoundationConfig.seed` before world generation in `Awake`.
- World name and difficulty are accepted/stored/logged (`ActiveWorldName`, `ActiveDifficulty`), but difficulty is not gameplay-applied yet. Keep passing it so the menu contract is stable and I can wire difficulty behavior later without another integration change.
- Foundation play-test: I have not rerun the doc 06 manual checklist in this API handoff. The previous validation doc says editor checks pass; live play-mode validation is still a separate Codex task.

---

### (awaiting Codex's first entry)
Suggested first reply: the seed/world entry point into `FoundationBootstrap` so Claude
can wire the welcome menu to it, and a note on the current state of the Foundation
play-test (doc 06 checklist) so we don't duplicate work.
### 2026-06-07 - Playability hotfix for crafting/interaction/HUD scale

Owner reported: crafting showed nothing, break durability felt too low, click feel was rough, break bar too small, no UI scale slider, no Ctrl +/- zoom.

Local fix set:
- Kept `FoundationHUD` enabled after `GameHudInitializer` binds uGUI. Important: uGUI has the nicer bars/panels, but FoundationHUD is still the reliable fallback for hotbar/crafting/inventory/world-use until the uGUI panels are fully routed from Foundation interactions.
- Increased resource node effective hit count and fixed high-tier tool power so full-inventory depletion safety still validates.
- Made held LMB repeatedly harvest nodes at a short cadence; placement/farming remain deliberate clicks.
- Enlarged world break/durability bars and extended their visibility.
- Added shared `ui.scale` PlayerPrefs support to FoundationHUD and added a UI Scale slider to the pause menu.
- Added Ctrl + plus/minus orthographic camera zoom on `FoundationBootstrap`.
- Updated pause-menu control hints away from `E`.
- Fixed the Calling picker scroll viewport duplicate `Image` issue in `WelcomeScreenManager` (local UI file already had Claude changes).

Validation:
- Unity script compile passed in batchmode.
- `FoundationIntegratedSliceValidator.Run` passed: **85/85**.

Watch-out:
- Worktree has lots of unrelated dirty art/tooling/UI files. Do not stage them blindly. The playability files touched here are `FoundationBootstrap`, `ResourceNode`, `PlayerInteraction`, `FoundationHUD`, `PauseMenu`, `WorldProgressBar`, `GameHudInitializer`, `WelcomeScreenManager`, plus the regenerated integrated validation report.

### 2026-06-07 - Instance pockets + readability follow-up

Owner confirmed core loop works, then called out two issues: tavern/guild/portal instances still felt wrong, and HUD text across surfaces was too hard to read.

Local fix set:
- `FoundationInstanceSystem` now builds a 10x10 rendered pocket with a one-tile invisible solid boundary ring. The player spawns inside the rendered area and cannot walk outside it.
- `IsoWorldController` now subscribes to instance enter/exit and, while inside, streams only the active pocket's 10x10 render bounds instead of the surrounding procedural overworld.
- `FoundationBootstrap` wires `WorldController.SetInstanceSystem(Instances)`.
- Cleaned edit-mode node destruction in `IsoWorldController` so validator runs do not log Destroy warnings.
- `LitIsoFont.SnapSize` no longer snaps 14-16px UI down to 13px; it now enforces a 16px minimum and modest readability multiplier.
- `FoundationHUD` and `FoundationInteractionOverlay` now use readable IMGUI skins, larger context rows, UI-scale-aware pointer math, and larger toast/tutorial text areas.

Validation:
- Unity script compile passed.
- `FoundationIntegratedSliceValidator.Run` passed: **85/85**.

### 2026-06-07 - Dungeon portal first slice

Owner provided two asset packs:
- `2D Isometric Portal.zip`: imported `Isometric_Portal.png` + license to `Assets/Resources/FoundationPortals/`.
- `kenney_isometric-miniature-dungeon.zip`: Kenney CC0; imported a curated subset to `Assets/Resources/FoundationDungeon/Kenney/`.

Runtime work:
- Added `Assets/Scripts/IsoCoreFoundation/Dungeons/*`.
- `FoundationDungeonPortalSystem` spawns distance-tiered portals around spawn. Farther portals get higher tiers and different portal tint colors.
- Right-click portal -> "Enter Tier N dungeon" -> generated dungeon instance.
- `FoundationDungeonGenerator` creates deterministic connected room/corridor layouts from world seed + dungeon id + portal cell + tier.
- `FoundationInstanceSystem.EnterDungeon()` builds larger bounded dungeon instances, renders only active bounds via existing `IsoWorldController` instance streaming, and dresses rooms with Kenney props.
- `MobSpawner` pauses normal biome spawning while inside an instance and exposes `SpawnMobAt()` for dungeon-specific encounters.
- `PlayerInteraction` now detects dungeon portals before ordinary placeables.

Design/roadmap:
- Added `Docs/IsoCoreFoundation/19_Modular_Dungeon_Portal_Plan.md`.
- Important future fix: generated dungeon terrain is still written through `IsoWorld.RestoreModifiedCells()` and therefore can mix with save modified cells. Plan doc calls out separate dungeon save state as the next hardening step.

Validation:
- Unity script compile passed.
- `FoundationIntegratedSliceValidator.Run` expanded and passed: **92/92**.

### 2026-06-07 - Tavern building/interior + durable held tools

Owner clarified `free_pack.zip` is the tavern interior pack. I imported its table/wall PNGs to
`Assets/Resources/FoundationInteriors/Tavern/` with a provenance note. The zip has no license/readme
inside, so keep this reviewable/quarantined until owner confirms final shipping provenance.

Runtime work:
- Added craftable `tavern_building_item` / `tavern_building`. It is a placeable entrance that right-clicks into `tavern_common_room`; the existing `tavern_door` remains.
- Copied the owner-provided tavern exterior PNG from Downloads into `Assets/Resources/FoundationBuildings/tavern_building.png` and wired `FoundationPlaceableSpriteResolver` so placeables can use runtime art before dedicated import presets are polished.
- `FoundationInstanceSystem` now dresses tavern pockets with props from `FoundationInteriorSpriteResolver`. The pocket remains the bounded 10x10 instance model the owner asked for.
- Added per-slot tool durability by extending `ItemStack`. Existing old saves/tools initialize to full durability on restore/add.
- Added durability values and recipes for axes, pickaxes, shovels, swords, and hoe. Higher-tier tools last longer and already harvest faster through existing node tool-tier logic.
- Added `PlayerHeldTool`: selected tools render as a child sprite and swing on successful harvest/place/till use. It uses `Resources/Items/<itemId>` when available and falls back to a small procedural sprite.
- IMGUI `FoundationHUD` and Claude's uGUI `GameUIController` now show durability bars on tool hotbar slots.
- Save schema bumped to version 6 so durability round-trips through inventory/storage/loadouts.

Asset Store links:
- Verified the two linked Unity Asset Store packages are free Standard Asset Store EULA extension assets:
  - UPixelator Campfire: free, 14.6 MB, depends on two UPixelator packages.
  - 2D Pixel Medieval Weapons: free, 97.1 KB.
- I cannot pull them directly from the URL without Unity account/Asset Store package acquisition. Runtime hooks are ready:
  - campfire sprite/model proxy path: `Resources/FoundationCampfire/upixelator_campfire` or `Resources/FoundationCampfire/campfire`
  - weapon/tool icons: `Resources/Items/<itemId>.png`
  Once owner imports those packages via Unity Package Manager / My Assets, drop/rename the selected art into those paths and the runtime will pick it up.

Validation:
- Stopped one stale Unity process that was locking the project.
- `FoundationValidator`: **32/32 PASS**
- `FoundationIntegratedSliceValidator.Run`: **94/94 PASS**

### 2026-06-07 - Greenwake terrain starter kit v1

Created a concrete review-only terrain baseline at:

`Assets/Generated/_Review/GreenwakeTerrainStarterKit_v1`

It contains exactly eight tiles:

- grass flat
- dirt flat
- grass-to-dirt north/south/east/west
- grass raised block
- dirt raised block

Review outputs:

- `starter_kit_contact_sheet.png`
- `starter_kit_9x9_preview.png`
- `manifest.json`
- `review_decisions.json`
- `review_report.json`
- `strict_asset_quality_report.json`

Strict terrain QA result:

- **8/8 pass**
- **0 review**
- **2 warnings**

Known concern before promotion:

- Raised grass/dirt blocks pass structural QA, but side faces dominate top faces. Treat this as a human visual-review warning before using them as runtime height tiles.

Status:

- Not promoted to `Assets/Resources`.
- Do not expand the terrain family until owner approves or rejects this starter kit.

### 2026-06-07 - Tavern/map/HUD/render-distance refinement pass

Owner feedback addressed:
- Tavern interiors were too small: tavern instances now use a tavern-specific 16x16 pocket. Generic building pockets remain the 10x10 model.
- Interior props now block movement by writing temporary occupant cells while inside the instance.
- Interior decorations are hover/click targeted by actual sprite bounds via `FoundationInstanceDecoration`, so the highlight/context menu follows the visible prop instead of requiring the exact tile.
- Tavern/interior instances now spawn a visible exit portal inside the pocket. Right-clicking the portal exits back to the exterior entrance.
- Added `FoundationMapOverlay`: top-right minimap plus fullscreen explored map toggled with `M`. It tracks explored cells around the player at runtime and colors by biome/surface.
- Reworked the uGUI HUD layout to be less cramped: vitals are now a wider upper-left panel; hotbar spacing is looser; durability bars remain.
- Improved render margin: default/enforced `viewRadiusChunks` is now at least 3 and per-frame stream reveal budget is 720 cells, reducing visible empty edge pop-in while moving.

Validation:
- Stopped stale Unity processes that were locking the project.
- `FoundationValidator`: **32/32 PASS**
- `FoundationIntegratedSliceValidator.Run`: **96/96 PASS**

Known future hardening:
- Map exploration is currently runtime-only; save/load of explored cells is the next obvious step.
- Interior prop collision is instance-temporary, not a separate persisted interior-object save layer yet.

### 2026-06-08 - Tabbed character panel + tavern room pass

Owner asked for crafting/inventory/skills/quests to live in a dedicated window, and for the tavern to read as a real room with walls.

Runtime/UI work:
- Replaced the Foundation fallback HUD's separate inventory/crafting windows with one tabbed Character panel.
- `I` opens the Character panel on Inventory.
- `C` opens the same panel on Crafting, preserving current station context (`Hand`, `Workbench`, etc.).
- Added live Skills and Quests tabs from `FoundationProgression.CaptureReadState()`, not placeholder data.
- `FoundationBootstrap` now binds `Progression` into `FoundationHUD` via `Hud.BindProgression(Progression)`.

Tavern work:
- Tavern perimeter cells now render as solid `stone_block` walls inside the visible 16x16 room, with a bottom-center doorway left open for the exit portal.
- Tavern layout is denser and more room-like: rear counter/bar, wall shelf/rack/keg shelf, two table clusters, benches, stools, and walkable lanes.
- Furniture collision remains world-query based through temporary occupant cells while inside the instance.

Validation:
- Stopped stale Unity processes again before batchmode.
- `FoundationValidator`: **32/32 PASS**
- `FoundationIntegratedSliceValidator.Run`: **96/96 PASS**

### 2026-06-08 - Tavern immersion + animated campfire + prop seating fixes

Owner provided `C:/Users/garyc/Downloads/campfire-Sheet.png` and screenshot feedback: HUDs too crowded, tavern still too platform-like, collision/prop overlap weak, portal offset wrong, world props not centered.

Runtime/art integration:
- Imported owner-provided campfire sheet to `Assets/Resources/FoundationCampfire/campfire-Sheet.png` with provenance.
- Added `FoundationCampfireAnimator`; placed/crafted `campfire` now uses sheet frames and animates automatically.
- `FoundationPlaceableSpriteResolver.CampfireFrames()` slices horizontal sheets by frame width and feeds both validator and runtime.

Tavern/instance changes:
- Tavern pocket increased again from 16x16 to **22x22**.
- Added stronger wall dressing using Kenney stone-wall sprites along back/side walls so the room reads more like an interior, not just a flat platform.
- Tavern furniture layout spread out to avoid overlaps: separated back bar/counters, wall shelves, two table clusters, small table, benches, and stools with open walk lanes.
- Furniture now uses multi-cell collision footprints (`BlockFootprint`) instead of one-cell-only blocking for wide objects.
- Portal pivot adjusted lower and exit portal y-offset corrected so it sits on its tile rather than floating/offset.

World prop seating:
- Real resource-node art no longer gets the placeholder-only down-screen seating offset. Trees/rocks/bushes with real sprites now sit on their cell center; placeholders still keep the small nudge.

HUD:
- Foundation fallback panel/header/minimap reduced and moved to create more world visibility. This is still a stopgap; Claude/uGUI should eventually own the polished unified HUD.

Validation:
- `FoundationValidator`: **32/32 PASS**
- `FoundationIntegratedSliceValidator.Run`: **97/97 PASS**
## 2026-06-07 - LPC motion LoRA eval

- Finished `litiso_lpc_motion_template_v1` training and synced the final LoRA to ComfyUI.
- Added `Tools/LoRA/eval_lpc_motion_template_v1_comfy.py` and `Tools/LoRA/eval_lpc_motion_template_v1.ps1`.
- DreamShaper eval failed for production sprite output: too much concept art / scene / portrait drift.
- PixelartSpritesheet smoke eval stayed sprite-like, but generated repeated mini sprite rows inside each image.
- Added `Docs/IsoCoreFoundation/21_LPC_Motion_LoRA_Eval.md` with the verdict and next step.
- Added `Tools/AssetForge/recover_sprite_frames_from_sheet.py` and `.ps1`.
- Recovered 16 transparent 64x64 review frames from the 4 PixelartSpritesheet smoke outputs at `C:\Projects\Pixel Pipeline\generated\litiso_lpc_motion_template_v1_recovered_frames_v1`.
- Added `Tools/AssetForge/capture_recovered_sprite_frames.py` and `.ps1`.
- Captured the recovered frames into `C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\recovered_motion_candidates_v1` with 16 images, 16 captions, metadata, train/val split, QA pass, and provenance.
- Human QC rejected those recovered generated frames for direction correctness; the dataset is now `quarantined_direction_failed` and `training_allowed=false`.
- Added a known-good LPC cardinal direction oracle at `C:\Projects\Pixel Pipeline\generated\lpc_direction_oracle_v1` using original LPC metadata for south/east/north/west. Diagonals remain explicitly missing.

## 2026-06-07 - FreePixel official pack intake

- Downloaded 10 official free FreePixel itch.io ZIP packs through the itch generated pack download endpoint.
- ZIPs: `C:\Projects\Pixel Pipeline\sources\freepixel\zips`
- Extracted: `C:\Projects\Pixel Pipeline\sources\freepixel\extracted`
- Inventory/contact sheets: `C:\Projects\Pixel Pipeline\generated\freepixel_inventory`
- Added `Tools/AssetForge/download_freepixel_itch_packs.ps1`, `inventory_freepixel_packs.py`, and `inventory_freepixel_packs.ps1`.
- First-pass verdict: useful for reference/vocabulary, but not a complete 8D animation solution. RPG Environment and Essentials are highly useful; Tilesets are mixed and require filtering; Animations are mostly VFX/action pose references rather than 8-direction sheets.
- Keep FreePixel assets as reference/intake only; do not train on them or move raw assets into runtime folders without explicit license/training approval.

### 2026-06-08 - Construction plots + library interior prototype

Owner asked for the tavern flow to become plot -> material build -> enterable building, plus the same treatment for a Kenney/OpenGameArt library.

Foundation work:
- Added `InteractionKind.Construction` and construction-result/cost fields on `PlaceableDefinition`.
- Added craftable `tavern_plot_item` and `library_plot_item`.
- Right-clicking a placed construction plot now opens a Build action that checks material costs, consumes them, and swaps the plot into the final enterable placeable.
- Tavern plots build into the existing `tavern_building` and keep linking to `tavern_common_room`.
- Library plots build into `library_building`, an enterable placeable linked to the new `library_archive` instance.
- Added validator coverage for construction-result integrity, tavern plot upgrade, and library plot upgrade.

Asset note for Claude lane:
- Reviewed the requested OpenGameArt/Kenney packs and kept the full archives outside Unity at `C:\tmp\LIT-ISO-OGA-Kenney-Review` to avoid import bloat.
- Imported only a curated library subset into `Assets/Resources/FoundationInteriors/Library` because the owner explicitly requested a working library prototype. This includes bookcases, tables, carpets, display cases, chairs, a book stand, candles, license, and provenance.
- Dungeon, farm, survival, and UI packs were reviewed/downloaded but not imported into `Assets` yet. Recommended follow-up is curated, feature-specific imports rather than dropping whole packs into the project.

Validation state:
- Static diff check passed for the touched Foundation/resource/doc paths.
- Unity was open on this project during the pass. The initial Editor log showed compile errors in `PlayerInteraction` out-param helper methods; those are fixed on disk.
- Follow-up Editor log read showed Unity successfully rebuilt `IsoCore.Foundation.dll`, `IsoCore.Foundation.Editor.dll`, `Assembly-CSharp.dll`, and `Assembly-CSharp-Editor.dll`.
- Full Foundation validators still need to be run in the editor or after closing Unity before PR/merge. The only compile warning seen was the existing `AssetForgeImporter` obsolete `TextureImporter.spritesheet` warning.

### 2026-06-08 - Building exterior/interior visual direction mockups

Owner provided OpenGameArt references for modular 64x64 medieval buildings, outdoor settlement examples, and CC-BY castle walls.

Created preview-only concept sheets under `Docs/handoff/building_concepts/`:
- `reference_inputs_and_license_notes.png`
- `exterior_building_drafts_sheet.png`
- `scene_comparison_building_versions.png`
- `interior_library_wall_options.png`
- `BUILDING_CONCEPT_REVIEW.md`

No Unity assets or runtime code were changed in this pass.

Recommendation captured in the review:
- Version A = cozy modular prefab language for early tavern/farm/workshop/starter library.
- Version B = fortified stone/archive language for upgraded library, guild hall, dungeon gatehouse, obelisk-adjacent structures.
- Treat the CC-BY OpenGameArt references as visual/prototype references unless the project explicitly accepts attribution obligations. Prefer original generated/fresh exterior sprites for final use.

### 2026-06-08 - OpenGameArt 8D character oracle intake

Owner approved downloading the OpenGameArt `400-items-basehumanmale-orc-skeleton` pack for animation/direction reference.

Completed:
- Downloaded all six official `.7z` parts into `C:\Projects\Pixel Pipeline\sources\opengameart\400-items-basehumanmale-orc-skeleton\archives`.
- Extracted them under `C:\Projects\Pixel Pipeline\sources\opengameart\400-items-basehumanmale-orc-skeleton\extracted`.
- Added `Tools/AssetForge/build_oga_8d_character_oracle.py` and `.ps1`.
- Generated `C:\Projects\Pixel Pipeline\generated\oga_8d_character_oracle_v1\BaseHumanMale_litiso_8d_oracle.png`.
- Documented the source, license, direction mapping, and usage policy in `Docs/IsoCoreFoundation/23_OGA_8D_Character_Oracle.md`.

Important mapping:
- LIT-ISO direction order is `S, SE, E, NE, N, NW, W, SW`.
- Source camera mapping is `S=CAM7`, `SE=CAM6`, `E=CAM5`, `NE=CAM4`, `N=CAM3`, `NW=CAM2`, `W=CAM1`, `SW=CAM0`.

Verdict:
- This is the first verified true-8D character/action oracle in the local pipeline.
- It is immediately useful for QA, prompt vocabulary, sprite packing, and direction validation.
- Owner approved using this CC-BY 4.0 source for local training on 2026-06-08, with attribution/provenance preserved.
- Keep raw extracted sprites out of runtime/shipped assets unless final credits include the required attribution.
- Filename scan verified Idle, Attack, Bow, Cast, Walk, Run, Death. Hurt/Pain remains unverified/missing.
- Added `Docs/IsoCoreFoundation/24_AssetForge_Training_Attributions.md` as the training-source attribution register.

### 2026-06-08 - OGA 8D motion training index

Built the approved OGA 8D pack into a structured training/reference dataset.

Added tooling:
- `Tools/AssetForge/build_oga_8d_training_index.py`
- `Tools/AssetForge/build_oga_8d_training_index.ps1`
- `Tools/LoRA/start_oga_8d_motion_training.ps1`

Generated dataset:
- Root: `C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\oga_8d_motion_direction_v1`
- Full parsed index: 243,024 frames across 498 layers.
- Ready training subset: 488 `BaseHumanMale` frames.
- Train/validation split: 440 / 48.
- Ready subset QA: pass.
- Contact sheet: `C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\oga_8d_motion_direction_v1\ready_training_subset\ready_subset_contact_sheet.png`.

Started pilot local LoRA training:
- Output name: `litiso_oga8d_motion_direction_v1`
- Output folder: `C:\Projects\LoRA-Training\outputs\litiso_oga8d_motion_direction_v1`
- Control folder: `C:\Projects\LoRA-Training\control\litiso_oga8d_motion_direction_v1`
- Settings: 1000 steps, save every 200, rank 32, DreamShaper base, CUDA.
- First checkpoint observed: `litiso_oga8d_motion_direction_v1_step00200.safetensors`.
- Documented in `Docs/IsoCoreFoundation/25_OGA_8D_Motion_Training_Index.md`.

Completion/update:
- Body-only training completed at 1000/1000 steps and synced to ComfyUI as `litiso_oga8d_motion_direction_v1_final.safetensors`.
- Body-only eval completed at `C:\Projects\Pixel Pipeline\generated\litiso_oga8d_motion_direction_v1_eval`.
- QC verdict: not production-accepted. It generated concept-style fantasy characters and weak direction control.

Second pass:
- Added `Tools/AssetForge/build_oga_8d_composite_dataset.py` and `.ps1`.
- Built `C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\oga_8d_composite_motion_v1`.
- Composite dataset has 2,440 frames from five presets: forest guard, iron knight, arcane mage, cloak rogue, skeleton archer.
- Added `Tools\LoRA\start_oga_8d_composite_training.ps1`.
- Composite training completed at 1200/1200 steps and synced to ComfyUI as `litiso_oga8d_composite_motion_v1_final.safetensors`.
- Added eval wrappers `Tools\LoRA\eval_litiso_oga8d_motion_direction_v1*` and `eval_litiso_oga8d_composite_motion_v1*`.
- Composite eval completed at `C:\Projects\Pixel Pipeline\generated\litiso_oga8d_composite_motion_v1_eval`.
- QC verdict: not production-accepted. Outfit coherence improved, but direction control and pixel style are still insufficient; it also produced unwanted scene/background output.

Recommendation:
- Stop expecting text-only LoRA to solve exact 8D sprites.
- Use OGA as pose/template/control conditioning data.
- Train LIT-ISO visual style separately from approved original/Sprixen-quality assets, then combine style + OGA motion through image/template-guided generation.

### 2026-06-08 - Original LitIso wall kit V1/V2

Owner asked for original walls using the OpenGameArt building/castle references only as inspiration.

Created project-local original prototype PNGs:
- `Assets/Resources/FoundationInteriors/LitIsoWalls/`
- `Assets/Resources/FoundationInteriors/LitIsoWallsV2/`

Preview docs:
- `Docs/handoff/building_concepts/litiso_original_wall_kit_contact.png`
- `Docs/handoff/building_concepts/litiso_original_wall_kit_scene.png`
- `Docs/handoff/building_concepts/litiso_original_wall_kit_open_southeast.png`
- `Docs/handoff/building_concepts/litiso_walls_v2_contact.png`
- `Docs/handoff/building_concepts/litiso_walls_v2_open_southeast_staging.png`
- `Docs/handoff/building_concepts/litiso_walls_v2_clean_library_staging.png`

V2 is the preferred direction:
- Detail noise is clipped to wall faces.
- Windows/doors are skewed by wall direction.
- Staging uses only top-left/top-right walls, leaving the southeast/front side open.
- Library props are staged against rear walls and away from the open edge.

No runtime code is wired to these sprites yet; Unity import/meta generation still needs a normal editor refresh.

### 2026-06-08 - Large guild/tavern/library interior previews

Owner provided stronger references for open-front isometric interiors: raised base/platform edge, walls only on the back/top sides, wall-hugging service rows, warm tavern/guild flooring, and furniture aligned to tile axes.

Created original decor sprites:
- `Assets/Resources/FoundationInteriors/LitIsoDecorV1/`

Created large-room previews:
- `Docs/handoff/building_concepts/litiso_large_guild_interior_reference_v3.png`
- `Docs/handoff/building_concepts/litiso_large_tavern_interior_reference_v3.png`
- `Docs/handoff/building_concepts/litiso_large_library_interior_reference_v3.png`
- `Docs/handoff/building_concepts/litiso_large_interiors_reference_overview_v3.png`
- `Docs/handoff/building_concepts/LITISO_LARGE_INTERIORS.md`

V3 is the preferred large-interior direction:
- 16x12 rooms.
- Raised visible open edge.
- Back/side walls only.
- Guild/tavern/library differentiated by floor palette and prop function.
- Decor is placed in rows/columns: service row y=1, side column x=1, table rows y=5/y=8, movement band y=10-11.

No runtime code has been wired yet; this is visual/layout groundwork for the next Foundation instance pass.

### 2026-06-08 - Bookshelf/decor depth pass

Owner called out that the current decorations, especially bookshelves, read too flat and do not feel part of the scene.

Created original depth-aware prototype sprites:
- `Assets/Resources/FoundationInteriors/LitIsoDecorV2/`

Key files:
- `archive_shelf_back_wide_v2.png`
- `archive_shelf_back_ladder_v2.png`
- `archive_shelf_side_left_v2.png`
- `library_shelf_back_wide_v2.png`
- `library_shelf_back_ladder_v2.png`
- `library_shelf_side_left_v2.png`
- `library_shelf_side_ladder_v2.png`
- `archive_display_case_deep_v2.png`
- `library_display_case_deep_v2.png`

Preview/comparison files:
- `Docs/handoff/building_concepts/litiso_bookshelf_depth_contact_v2.png`
- `Docs/handoff/building_concepts/litiso_bookshelf_integration_old_flat.png`
- `Docs/handoff/building_concepts/litiso_bookshelf_integration_deep_v2.png`
- `Docs/handoff/building_concepts/litiso_bookshelf_before_after_v2.png`
- `Docs/handoff/building_concepts/LITISO_DECOR_V2_BOOKSHELVES.md`

Integration guidance:
- Prefer LitIsoDecorV2 shelves over the old imported `Library/bookcase*` placeholders for archive/library staging.
- Place back-wall shelves on the rear service row and side-wall shelves on the side service column.
- Use bottom-center/foot-anchor placement or explicit offsets; center pivots will make the shelves feel detached from the tiles.
- Keep large shadows as runtime/staging effects, not baked into the sprites.

### 2026-06-08 - Full V2 interior decoration pass

Owner asked for the whole interior decoration set to be brought up to the bookshelf standard.

Expanded `Assets/Resources/FoundationInteriors/LitIsoDecorV2/` to 33 original prototype PNGs covering:
- Guild counters, quest boards, contract tables, round tables, rugs, banners, benches, railings.
- Tavern bar shelves, bar counters, barrel stacks, feast tables, fireplace, rugs, round food tables.
- Library/archive shelves, catalog counters, display cases, reading tables, podiums, statue, rugs, banners.

New preview files:
- `Docs/handoff/building_concepts/litiso_decor_v2_complete_contact.png`
- `Docs/handoff/building_concepts/litiso_large_guild_interior_decor_v2.png`
- `Docs/handoff/building_concepts/litiso_large_tavern_interior_decor_v2.png`
- `Docs/handoff/building_concepts/litiso_large_library_interior_decor_v2.png`
- `Docs/handoff/building_concepts/litiso_large_interiors_decor_v2_overview.png`
- `Docs/handoff/building_concepts/LITISO_DECOR_V2_INTERIORS.md`

Recommendation:
- Treat `LitIsoDecorV2` as the preferred prototype interior decoration set.
- Keep `LitIsoDecorV1` as older blockout/flat art only.
- Runtime integration should use bottom-center/foot anchors for furniture, wall-row anchors for boards/banners/fireplaces/back bars, and tile-plane anchors for rugs.

### 2026-06-08 - Varied interior layout pass

Owner called out that guild/tavern/library interiors were still the same exact rectangular room shape and needed room for player movement, varied sizes, corridors, and more detailed layouts.

Created varied-layout previews:
- `Docs/handoff/building_concepts/litiso_varied_interior_layouts_overview_v1.png`
- `Docs/handoff/building_concepts/litiso_varied_interior_walklanes_overview_v1.png`
- `Docs/handoff/building_concepts/litiso_guild_interior_layout_variation_v1.png`
- `Docs/handoff/building_concepts/litiso_tavern_interior_layout_variation_v1.png`
- `Docs/handoff/building_concepts/litiso_library_interior_layout_variation_v1.png`
- `Docs/handoff/building_concepts/litiso_guild_interior_layout_walklanes_v1.png`
- `Docs/handoff/building_concepts/litiso_tavern_interior_layout_walklanes_v1.png`
- `Docs/handoff/building_concepts/litiso_library_interior_layout_walklanes_v1.png`
- `Docs/handoff/building_concepts/LITISO_INTERIOR_LAYOUT_VARIATIONS.md`

Direction:
- Guild should be a public hall with side quest corridor, back reception/service zone, and office wing.
- Tavern should have kitchen/hearth nook, bar alcove, dining pockets, side snug, and entry neck.
- Library/archive should have back stack corridor, central reading hall, side archive wing, study/catalog alcoves.

Implementation guidance:
- Stop treating interiors as only width/height rectangles.
- Use occupied floor tile masks, generated wall edges, prop footprints, reserved walk tiles, interactable access tiles, spawn/exit buffers, and reachability validation.
- Main co-op lanes should usually be 2 tiles wide. Door thresholds can be narrower, but primary routes should not be one-tile chokes.

### 2026-06-08 - Data-driven varied tavern runtime slice

Owner approved proceeding from preview/layout planning into runtime implementation.

Implemented first slice in the Foundation lane:
- Added `Assets/Scripts/IsoCoreFoundation/Building/FoundationInteriorLayout.cs`
  - Defines interior floor masks, reserved walk tiles, prop placements, prop footprints, and a layout validator.
  - First variant: `tavern_hearth_snug_v1`.
  - Validator checks spawn/exit reachability, reserved lanes, prop footprint/floor-mask overlap, and adjacent access around blocking props.
- Updated `FoundationInstanceSystem`
  - Tavern entrances now build from `FoundationInteriorLayout.TavernHearthSnug()` instead of the old same-size rectangle.
  - Writes hidden blocker cells around the floor mask so the player cannot walk outside the shaped room.
  - Spawns V2 depth-aware tavern props from `LitIsoDecorV2`.
  - Exposes explicit active render cells for non-rectangular interiors.
- Updated `IsoWorldController`
  - Instance streaming can now render explicit cells instead of filling `ActiveRenderMin/Max` as a rectangle.
- Updated `FoundationInteriorSpriteResolver`
  - Added `DecorV2()` for `Resources/FoundationInteriors/LitIsoDecorV2`.
- Updated integrated validator source guards for the floor-mask tavern spine and V2 decor usage.

Validation state:
- Static brace/source checks passed on touched files.
- Unity batch validation could not be completed because this project is already open in Unity and the batch run produced no log.
- `dotnet build` and Unity bundled `dotnet build` are unavailable because no .NET SDK is installed.
- Direct Roslyn compile was not usable in this shell because Unity's csc dependency path was incomplete.

Next runtime slices:
- Run Unity compile/validator from the open editor.
- If green, add `guild_hall_public_v1` and `library_archive_stacks_v1` using the same floor-mask system.
- Add runtime/debug walk-lane overlay only if needed for QA; current cyan overlays remain preview-only docs.

### 2026-06-08 - LitRPG Skills tab + expanded UI approval kit

Owner asked for the Skills tab to feel like LitRPG skills, not a generic/crafting-style list.

Runtime/data changes:
- `FoundationContent` now defines an explicit `combat` skill.
- `Lanternblade` starter skills now include `combat`, `warding`, and `exploration`.
- `FoundationProgressionHooks.HandleMobDefeated` awards both `combat` and `warding` XP.
- `CharacterPanelView.DrawSkills` now renders grouped activity paths:
  - Combat & Warding
  - Gathering & Exploration
  - Crafting & Building
  - Hearth & Settlement
  - Lore & Magic
- Skill cards show level, activity, node kind, tracked/dormant state, description, unlock preview, and XP progress.

Approval-only UI art:
- Expanded generated UI kit under `Docs/handoff/ui_concepts/adventurer_litrpg_ui_v1/`.
- New contact sheets:
  - `expanded_ui_contact_sheet.png`
  - `skill_litrpg_contact_sheet.png`
- These are NOT imported into `Assets/Resources/UI/InGame/` yet; keep the approval gate.

Validation:
- `git diff --check` passed with only existing CRLF warnings in unrelated generated docs.
- Unity is currently open, so batch validator/compile was not run in this pass.

### 2026-06-08 - Main menu background/screen approval concepts

Owner asked for a generated main menu screen and background image.

Created approval-only menu concept art:
- `Docs/handoff/menu_concepts/litiso_main_menu_background_v1.png`
- `Docs/handoff/menu_concepts/litiso_main_menu_screen_v1.png`
- `Docs/handoff/menu_concepts/litiso_main_menu_logo_v1.png`
- `Docs/handoff/menu_concepts/litiso_main_menu_concept_contact_v1.png`
- `Docs/handoff/menu_concepts/README.md`

Direction:
- Dusk isometric meadow with campfire, tavern, portal, path, trees, and fireflies.
- Main menu mockup uses the adventurer/LitRPG UI palette: dark shell, parchment text, brass/gold accents.
- This is NOT imported into `Assets/Resources/UI/Menu/` yet.
- `WelcomeScreenManager` already auto-loads `Resources/UI/Menu/background`, `logo`, `panel`, `button`, and `button_hover`, so promotion after approval is mechanical.

### 2026-06-08 - Super HD menu background refinement

Owner asked for a more refined main menu background using the grass tiles/trees we already have, with nicer lighting/shader treatment.

Created a second approval-only pass:
- `Docs/handoff/menu_concepts/litiso_main_menu_background_super_hd_v1.png` (3840x2160)
- `Docs/handoff/menu_concepts/litiso_main_menu_background_super_hd_v1_preview_1920.png`
- `Docs/handoff/menu_concepts/litiso_main_menu_screen_super_hd_v1.png` (3840x2160 mockup)
- `Docs/handoff/menu_concepts/litiso_main_menu_super_hd_contact_v1.png`

This pass composites current project sprites:
- Generated Plains/Forest grass/path/stone tiles.
- Generated Forest/Plains trees, bushes, and rocks.
- Runtime tavern building, portal sheet, and campfire sheet.

Added preview-only post treatment:
- dusk color grade,
- contact shadows,
- campfire/portal bloom,
- fireflies,
- fog bands,
- long light streaks,
- left-side menu-safe darkening.

Still not imported into `Assets/Resources/UI/Menu/` pending owner approval.

### 2026-06-08 - OGA template-guided Asset Forge generation smoke

Implemented the first repeatable OGA template-guided character generation loop for Asset Forge.

What changed:
- `comfy_generation_worker.py` now supports local template guidance for `character`, `npc`, and `mob` requests when `template_guidance.enabled=true`.
- The worker prepares a selected OGA frame on a 512x512 canvas, uploads it to ComfyUI, and uses img2img (`LoadImage -> VAEEncode -> KSampler latent_image`) before deterministic cleanup/QA.
- Added queue/smoke/contact-sheet helpers:
  - `Tools/AssetForge/queue_oga_template_guided_requests.py/.ps1`
  - `Tools/AssetForge/run_oga_template_guided_smoke.ps1`
  - `Tools/AssetForge/build_review_contact_sheet.py`

Evidence:
### 2026-06-08 - Creation Instance showroom launch

Owner asked for a main-menu `Creation Instance` option that opens a flat grassland showroom for testing portals, tavern/library/campfire/chest/resources, and labeled refinement sections.

I am implementing the Foundation-side launch profile and showroom spawner in Codex lane. Because the visible menu button lives in `Assets/Scripts/UI/WelcomeScreenManager.cs` (Claude lane), I am making one small coordinated UI touch there: add a `Creation Instance` button that calls a new `FoundationBootstrap.ConfigureCreationInstanceLaunch()` before loading `IsoCoreFoundation`. No menu styling refactor, no save-list behavior changes.

---

- `Assets/Generated/_Review/oga_template_cyan_knight_comparison.png`
- `Assets/Generated/_Review/oga_template_cyan_knight_sweep_comparison.png`
- Review packs for south walk denoise `0.62`, `0.70`, and `0.78`.

Verdict:
- The mechanism works and passes structural QA.
- `0.70` is the best first denoise balance for the south walk template.
- It is still not production quality: visual style lock is the next bottleneck, not direction template availability.

### 2026-06-08 - Reference-knight IPAdapter style guidance

Extended the template-guided Comfy worker so pose and style are separate inputs:
- `reference_image` remains the OGA pose/template.
- `style_reference_image` is prepared on a neutral canvas and applied through `IPAdapterUnifiedLoader -> IPAdapter`.

Created:
- `Tools/AssetForge/extract_sprite_sheet_cell.py`
- `Assets/Generated/_Review/_StyleRefs/reference_knight_front_cell.png`
- `Tools/AssetForge/queue_oga_composite_template_guided_requests.py/.ps1`

Evidence:
- `Assets/Generated/_Review/oga_template_style_lock_comparison.png`
- `Assets/Generated/_Review/oga_template_ironknight_refknight_style_d070_walk_s`
- Queued dry-run verified 4D requests: `oga4d_refknight_style_iron_knight_walk_s/e/n/w`

Verdict:
- Style guidance now works locally with the installed IPAdapter node set.
- The armored/composited OGA template is clearly better than the bare body template.
- Still not production-ready; current bottleneck is getting a chunkier, more faithful LIT-ISO silhouette from the template/style stack.

### 2026-06-08 - 4D reference-knight walk generation checkpoint

Ran all queued 4D walk jobs:
- `oga4d_refknight_style_iron_knight_walk_s`
- `oga4d_refknight_style_iron_knight_walk_e`
- `oga4d_refknight_style_iron_knight_walk_n`
- `oga4d_refknight_style_iron_knight_walk_w`

Artifacts:
- `Assets/Generated/_Review/oga4d_refknight_style_iron_knight_walk_contact.png`
- `Assets/Generated/_Review/oga4d_refknight_style_iron_knight_walk_sheet.png`
- `Assets/Generated/_Review/oga4d_refknight_style_iron_knight_walk_sheet_manifest.json`

Verdict:
- The pipeline can produce a 4D row sheet with structural QA pass.
- The output is not production art yet. Direction intent is visible, but identity/gear consistency is not stable enough.
- Next concrete improvement is a controlled denoise/style-weight sweep using the composite template path, plus better source templates with chunkier armor/no torch leakage.

### 2026-06-08 - Style matrix + per-direction style checkpoint

Added:
- `Tools/AssetForge/run_oga_composite_style_matrix.ps1`
- `Tools/AssetForge/build_sprite_sheet_cell_contact.py`

Generated:
- `Assets/Generated/_Review/oga_matrix_refknight_style_iron_knight_Walk_S_matrix.png`
- `Assets/Generated/_Review/oga4d_refknight_style_sw050_d070_walk_contact.png`
- `Assets/Generated/_Review/oga4d_refknight_style_sw050_d070_walk_sheet.png`
- `Assets/Generated/_Review/_StyleRefs/reference_knight_cell_contact.png`
- `Assets/Generated/_Review/oga4d_refknight_perdir_sw050_d070_walk_contact.png`
- `Assets/Generated/_Review/oga4d_refknight_perdir_sw050_d070_walk_sheet.png`

Verdict:
- Best south setting found: style weight `0.50`, denoise `0.70`.
- Per-direction style refs improved S/W but did not solve E/N.
- North direction still reads too front-facing.
- Next fix should be stronger conditioning or verified direction references, not more blind seed churn.

### 2026-06-08 - ControlNet/OpenPose 4D direction smoke

Added:
- `Tools/AssetForge/build_litiso_openpose_direction_library.py/.ps1`
- `Tools/AssetForge/queue_litiso_controlnet_direction_requests.py/.ps1`
- `Tools/AssetForge/qa_direction_set.py`
- `control_guidance` support in `Tools/AssetForge/comfy_generation_worker.py`

Installed ComfyUI nodes/models verified locally:
- `OpenPoseStudio`
- `ControlNetLoader`
- `ControlNetApplyAdvanced`
- `control_v11p_sd15_openpose.pth`

Generated evidence:
- `Assets/Generated/_Review/_PoseControls/litiso_openpose_v1/idle_4d_contact.png`
- `Assets/Generated/_Review/_PoseControls/litiso_openpose_v1/walk_4d_contact.png`
- `Assets/Generated/_Review/litiso_control_refknight_idle_4d_contact.png`
- `Assets/Generated/_Review/litiso_control_refknight_idle_4d_sheet.png`
- `Assets/Generated/_Review/litiso_control_refknight_idle_4d_direction_qa.json`
- `Assets/Generated/_Review/litiso_control_refknight_walk_4d_contact.png`
- `Assets/Generated/_Review/litiso_control_refknight_walk_4d_sheet.png`
- `Assets/Generated/_Review/litiso_control_refknight_walk_4d_direction_qa.json`

Verdict:
- The local ControlNet/OpenPose path works end to end: request -> ComfyUI -> cleanup -> review pack -> contact sheet -> packed sheet -> direction QA.
- This is not production art yet. S/E are the most useful; N was rerun with a north/back style crop plus "back view only/no face" prompt and now reads as a back view, but it still has leftover background strip cleanup; W still suffers from background cleanup and direction/style drift.
- Next practical step is not more blind generation. It should be a stronger segmentation/background-removal pass plus either better curated direction references or a trained direction/style LoRA.

### 2026-06-08 - Direction camera consistency follow-up

Owner correctly flagged that the generated directions did not share a consistent camera perspective.

Changes:
- Tightened `queue_litiso_controlnet_direction_requests.py` with a fixed orthographic isometric camera/framing contract.
- Direction-specific style refs are now selected automatically when `reference_knight_s/e/n/w_cell.png` exists.
- North prompt explicitly says back view/no face/no visor.
- Character postprocess now includes:
  - chroma-green removal
  - primary-foreground component pruning
  - detached floor-artifact removal

Generated evidence:
- `Assets/Generated/_Review/litiso_control_refknight_camfix_idle_4d_contact.png`
- `Assets/Generated/_Review/litiso_control_refknight_camfix2_idle_4d_contact.png`
- `Assets/Generated/_Review/litiso_reference_knight_idle_4d_contact.png`
- `Assets/Generated/_Review/litiso_reference_knight_idle_4d_sheet.png`
- `Assets/Generated/_Review/litiso_reference_knight_idle_4d_sheet_manifest.json`

Verdict:
- The generated camfix sets are still not production quality. The model keeps inventing background/backdrop/floor effects and perspective drifts, especially W/N.
- The packed `litiso_reference_knight_idle_4d_sheet.png` is now the canonical 4D camera/framing oracle. Future generation should be compared against it and use it for template/img2img conditioning or as training data rather than relying on prompt-only camera language.

### 2026-06-08 - Oracle-template N/W direction smoke

Added/updated:
- `Tools/AssetForge/qa_against_direction_oracle.py`
- `Tools/AssetForge/queue_litiso_controlnet_direction_requests.ps1`
- `Tools/AssetForge/queue_litiso_controlnet_direction_requests.py` already supports oracle manifest/template-denoise payloads.
- `Tools/AssetForge/comfy_generation_worker.py` uses chroma-green template canvases for template guidance.

Generated evidence:
- `Assets/Generated/_Review/litiso_oracle_refknight_idle_nw_contact.png`
- `Assets/Generated/_Review/litiso_oracle_refknight_idle_nw_sheet.png`
- `Assets/Generated/_Review/litiso_oracle_refknight_idle_nw_sheet_manifest.json`
- `Assets/Generated/_Review/litiso_oracle_refknight_idle_nw_oracle_qa.json`
- `Assets/Generated/_Review/litiso_oracle_refknight_idle_n`
- `Assets/Generated/_Review/litiso_oracle_refknight_idle_w`

Verdict:
- Oracle-template conditioning helped direction intent: north now reads as a true back view.
- It is still not production art. West keeps extra cyan/weapon noise, both outputs drift upward against the oracle by about 10-12 px, and average RGB distance remains high.
- Next step should be either a low-denoise/reference-template sweep or training/fine-tuning against approved direction sheets. More prompt-only changes are unlikely to solve consistency.

### 2026-06-08 - West oracle denoise sweep + oracle dataset capture

Added:
- `Tools/AssetForge/run_litiso_oracle_denoise_sweep.ps1`
- `Tools/AssetForge/capture_direction_oracle_dataset.py`

Generated evidence:
- `Assets/Generated/_Review/litiso_oracle_sweep_refknight_w_idle_w_denoise_sweep.png`
- `Assets/Generated/_Review/litiso_oracle_sweep_refknight_w_idle_w_denoise_sweep_oracle_qa.json`
- `C:/Projects/Pixel Pipeline/datasets/lit_iso/direction_oracles/reference_knight_idle_4d_oracle`

Verdict:
- West denoise sweep values `0.24`, `0.32`, and `0.40` produced nearly the same silhouette and still had high oracle RGB/style distance.
- This confirms the current ControlNet/IPAdapter/template setup has plateaued for production-quality west direction generation.
- The approved 4D reference knight oracle is now captured as a captioned direction-oracle dataset pack. Use it as training/evaluation anchor data, but do not treat four frames as enough for a LoRA.

### 2026-06-08 - Direction Oracle Factory V1

Added:
- `Tools/AssetForge/build_direction_oracle_factory.py`
- `Tools/AssetForge/build_direction_oracle_factory.ps1`

Smoke-tested with:
- `Assets/Generated/_Review/litiso_reference_knight_idle_4d_sheet.png`

Generated:
- `Assets/Generated/_Review/_DirectionOracles/reference_knight_idle_4d_factory_smoke/reference_knight_idle_4d_factory_smoke_manifest.json`
- `Assets/Generated/_Review/_DirectionOracles/reference_knight_idle_4d_factory_smoke/reference_knight_idle_4d_factory_smoke_validation.json`
- `Assets/Generated/_Review/_DirectionOracles/reference_knight_idle_4d_factory_smoke/reference_knight_idle_4d_factory_smoke_contact.png`
- `C:/Projects/Pixel Pipeline/datasets/lit_iso/direction_oracles/reference_knight_idle_4d_factory_smoke`

Validation:
- 4 frames, 0 warnings, 0 errors.

Verdict:
- This is now the preferred intake path for approved 4D direction sheets. Give it an approved sheet plus explicit `Direction=Column,Row` mappings; it outputs a normalized oracle manifest, contact sheet, validation report, and optional dataset pack.
- Next data targets: male adventurer, female adventurer, guard/knight, villager/NPC, and one simple mob, each with true `S/E/N/W` frames before any LoRA training.

### 2026-06-09 - Self-owned LPC oracle expansion

Owner clarified that Codex should own generation/data-building, not hand it back.

Generated locally:
- `Assets/Generated/_Review/LPC_CharacterRecipeReview_v1`
- `Assets/Generated/_Review/LPC_LitIsoViewAdaptation_v1`
- `Assets/Generated/_Review/_DirectionOracles/lpc_male_leather_adventurer_walk_4d_oracle_v2`
- `Assets/Generated/_Review/_DirectionOracles/lpc_female_forest_scout_walk_4d_oracle_v2`
- `Assets/Generated/_Review/_DirectionOracles/lpc_male_plate_guard_walk_4d_oracle_v2`
- `Assets/Generated/_Review/_DirectionOracles/litiso_direction_oracle_pack_contact_v2.png`
- `C:/Projects/Pixel Pipeline/datasets/lit_iso/direction_oracles/_index`

Added/updated:
- `Tools/AssetForge/build_direction_oracle_factory.py/.ps1` now supports `--allow-upscale` / `-AllowUpscale`.
- `Tools/AssetForge/build_direction_oracle_dataset_index.py`

Validation:
- Male adventurer, female scout, and plate guard v2 packs each validated with 4 frames, 0 warnings, 0 errors.
- Combined dataset index has 4 packs, 16 records, balanced `S/E/N/W`.

Verdict:
- These LPC-derived packs are direction/body anchors, not final LIT-ISO art style. They are useful for direction evaluation and for avoiding bad labels.
- We are still 34 approved records short of the first 50-record LoRA threshold.

### 2026-06-09 - Direction oracle threshold crossed

Added:
- `Tools/AssetForge/build_lpc_motion_direction_oracles.py`
- `Tools/AssetForge/build_direction_oracle_dataset_index.py` now writes trainer-compatible relative `file_name` paths.
- `Tools/LoRA/start_direction_oracle_anchor_training.ps1`

Generated:
- 9 additional LPC motion-derived 4D oracle packs under `Assets/Generated/_Review/_DirectionOracles`.
- `Assets/Generated/_Review/_DirectionOracles/litiso_direction_oracle_dataset_contact_v3.png`
- `C:/Projects/Pixel Pipeline/datasets/lit_iso/direction_oracles/_index`

Validation:
- Batch added 9 packs / 36 records, 0 warnings, 0 errors.
- Combined index now has 13 packs / 52 records.
- Direction balance: `S=13`, `E=13`, `N=13`, `W=13`.
- Dry-run training manifest for `litiso_direction_oracle_anchor_v1` was written successfully; no training process was started.

Verdict:
- We have enough data for an experimental direction-anchor LoRA, not a final art-style LoRA.
- Dataset is mostly LPC-derived, so this should teach direction/body consistency and serve as eval anchor data. It should not be expected to produce the final LIT-ISO/Sprixen-quality style by itself.

### 2026-06-09 - Direction oracle eval path

Added:
- `Tools/LoRA/eval_litiso_direction_oracle_anchor_v1_comfy.py`
- `Tools/LoRA/eval_litiso_direction_oracle_anchor_v1.ps1`

Validation:
- Python compile passed.
- PowerShell parse passed.
- Dry-run eval manifest passed using repo-local `Temp/litiso_direction_oracle_anchor_v1_eval_dryrun`.

Usage after the first checkpoint exists:
- `Tools/LoRA/sync_lora_to_comfyui.ps1 -OutputName litiso_direction_oracle_anchor_v1`
- `Tools/LoRA/eval_litiso_direction_oracle_anchor_v1.ps1 -Limit 4`

Verdict:
- The new eval path is deliberately narrow: it checks whether south, east, north, and west prompts produce correct facing and bottom-anchored sprite framing.
- This closes the loop for train -> sync -> fixed eval -> contact-sheet review without relying on old Sprixen eval scripts.

### 2026-06-09 - Direction oracle training started

Started:
- `litiso_direction_oracle_anchor_v1` via `Tools/LoRA/start_direction_oracle_anchor_training.ps1`

Hardening:
- `Tools/LoRA/status_litiso_training.ps1` now reports `observed_progress` parsed from the live `tqdm` log when status/checkpoint writes lag behind the actual process.

Observed:
- Training reached live progress in the log on CUDA; first checkpoint should appear at step 100.

Pause:
- `Tools/LoRA/pause_litiso_training.ps1 -OutputName litiso_direction_oracle_anchor_v1`

### 2026-06-09 - Direction oracle final eval

Completed:
- `litiso_direction_oracle_anchor_v1` reached 800/800 steps and wrote `C:\Projects\LoRA-Training\outputs\litiso_direction_oracle_anchor_v1\litiso_direction_oracle_anchor_v1_final.safetensors`.
- Synced final LoRA to `C:\Projects\ComfyUI\models\loras\litiso_direction_oracle_anchor_v1_final.safetensors`.

Evaluation outputs:
- Raw direction eval: `C:\Projects\Pixel Pipeline\generated\litiso_direction_oracle_anchor_v1_eval`
- PixelArtRedmond stacked eval: `C:\Projects\Pixel Pipeline\generated\litiso_direction_oracle_anchor_v1_eval_pixelredmond`
- Triggered PixelArtRedmond stacked eval: `C:\Projects\Pixel Pipeline\generated\litiso_direction_oracle_anchor_v1_eval_pixelredmond_triggered`
- Best cleaned contact so far: `C:\Projects\Pixel Pipeline\generated\litiso_direction_oracle_anchor_v1_eval_pixelredmond_triggered\cleaned_v4\cleaned_eval_contact.png`

Added:
- `Tools/AssetForge/clean_lora_eval_outputs.py`
- `Tools/LoRA/eval_litiso_direction_oracle_anchor_v1_comfy.py` now supports optional style LoRA stacking and prompt prefix/suffix.

Verdict:
- The LoRA is useful as a cardinal direction/body anchor. North now reliably reads as a back view in the triggered eval.
- It is not a final character-art solution. The best stack still reads like generic fantasy pixel art and can leave floor/shadow artifacts.
- Next real improvement should be a curated LIT-ISO/Sprixen-quality style dataset and style LoRA, then stack style LoRA + direction-anchor LoRA.

### 2026-06-09 - PixelArt dungeon floor kit first slice

Owner confirmed the local pack can be used:
- Source: `C:\Users\garyc\OneDrive\Desktop\PixelArt\Isometric tiles`

Imported and wired:
- `Assets/Resources/Tiles/dungeon_floor_1.png` through `dungeon_floor_5.png`
- Matching Unity `.meta` files with point filtering/readable sprite import settings.
- `FoundationContent` now exposes `dungeon_floor_1..5` under `dungeon_floor_blocks`.
- `FoundationDungeonGenerator` now paints walkable dungeon cells with deterministic `dungeon_floor_*` variation instead of overworld `stone_path`.
- `TileSpriteResolver.UsesAuthoredFootprint` plus `IsoWorldRenderer` preserve these 256x512 authored floor sprites instead of applying the legacy 32px flat/raised tile crop.

Not yet done:
- The same pack's walls, doors, stairs, columns, torches, traps, chests, coins, liquids, and animated hazards still need an orientation-aware placement pass. Do not just scatter them as flat decorations.
- Static checks passed (`rg`, `git diff --check`, asset presence, and brace-balance scan). `dotnet build` is blocked on this machine by missing .NET SDK; Unity batch validation was not run because three Unity editor processes are currently open.

### 2026-06-09 - PixelArt style dataset split for SD1.5 + ControlNet

User confirmed the generator direction: SD1.5 + ControlNet, with small local style LoRAs trained from approved PixelArt references.

Added/updated:
- `Tools/AssetForge/build_pixelart_style_dataset.py`
- `Tools/AssetForge/build_pixelart_style_dataset.ps1`
- `Tools/AssetForge/build_tile_controlnet_templates.py`
- `Tools/AssetForge/build_tile_controlnet_templates.ps1`
- `Tools/LoRA/start_pixelart_tile_style_training.ps1`
- `Tools/LoRA/start_pixelart_interior_style_training.ps1`
- `Tools/AssetForge/README.md`

Datasets rebuilt:
- Full audit: `C:/Projects/Pixel Pipeline/datasets/lit_iso/style_lock/pixelart_local_v1` - 2201 records.
- Terrain surfaces: `C:/Projects/Pixel Pipeline/datasets/lit_iso/style_lock/pixelart_local_terrain_tiles_v1` - 19 records.
- Height/block references: `C:/Projects/Pixel Pipeline/datasets/lit_iso/style_lock/pixelart_local_height_blocks_v1` - 111 records.
- Tile geometry LoRA set: `C:/Projects/Pixel Pipeline/datasets/lit_iso/style_lock/pixelart_local_tile_geometry_v1` - 130 records.
- Dungeon/building interiors: `C:/Projects/Pixel Pipeline/datasets/lit_iso/style_lock/pixelart_local_interiors_v1` - 1078 records.

Important:
- Exterior tile geometry and interiors are intentionally separate. Do not train/import them as one mixed style unless explicitly requested.
- Low-res source frames are now nearest-neighbor upscaled into the 512 training canvas; the old tiny-dot normalization was unsuitable for LoRA training.
- Tile geometry LoRA should be paired with ControlNet/depth/line templates. It is not expected to create strict 2:1 terrain by prompt alone.
- Tile ControlNet templates were generated under `C:/Projects/Pixel Pipeline/datasets/lit_iso/controlnet_templates/tile_geometry_v1` (15 templates + contact sheet).
- Current ComfyUI has OpenPose ControlNet installed, which is for characters. Tile generation still needs a Canny/Lineart/Depth/SoftEdge SD1.5 ControlNet model before the template pack can drive terrain generations properly.
- Dry-run manifests passed for `litiso_pixelart_tile_geometry_style_v1` and `litiso_pixelart_interior_style_v1`; no training process was started.

### 2026-06-09 - External dataset cleanup + tile Canny ControlNet readiness

Dataset/training artifacts were moved out of the Unity repo.

Important paths:
- External dataset root: `C:/Projects/Pixel Pipeline/datasets/lit_iso`
- Retired Unity dataset root: `Assets/Generated/_Datasets`
- Tile ControlNet templates: `C:/Projects/Pixel Pipeline/datasets/lit_iso/controlnet_templates/tile_geometry_v1`
- Strict tile geometry style set: `C:/Projects/Pixel Pipeline/datasets/lit_iso/style_lock/pixelart_local_tile_geometry_v1`
- Material block quarantine set: `C:/Projects/Pixel Pipeline/datasets/lit_iso/style_lock/pixelart_local_material_blocks_v1`
- Interior style set: `C:/Projects/Pixel Pipeline/datasets/lit_iso/style_lock/pixelart_local_interiors_v1`
- FreePixel character/mob/NPC reference set: `C:/Projects/Pixel Pipeline/datasets/lit_iso/characters/training/freepixel_characters_v1`

Current rebuilt counts:
- Strict tile geometry: 39 records (`20 height_block`, `19 terrain_tile`)
- Material blocks: 91 records
- Interiors: 1078 records
- Tile ControlNet templates: 15 clean geometry controls
- FreePixel characters/mobs/NPCs: 3394 frames (`2349 character`, `581 mob`, `464 npc`)

Installed model:
- `C:/Projects/ComfyUI/models/controlnet/control_v11p_sd15_canny_fp16.safetensors`

Tooling changes:
- `Tools/AssetForge/comfy_generation_worker.py` now allows `tile` jobs to use request-level ControlNet guidance.
- Tile requests accept `control_image_path` and can point at the geometry template pack.
- `Tools/AssetForge/queue_tile_family_requests.py` now attaches Canny ControlNet guidance by default for tile family requests.
- Added `Tools/LoRA/start_freepixel_character_style_training.ps1`.

Dry-runs passed:
- `litiso_pixelart_tile_geometry_style_v1`
- `litiso_pixelart_interior_style_v1`
- `litiso_freepixel_character_style_v1`

No LoRA training or ComfyUI generation was started in this pass. ComfyUI may need a restart/model refresh before it sees the newly installed Canny ControlNet model.

### 2026-06-09 - Tile Canny ControlNet smoke

ComfyUI was started successfully with:

```text
C:/Projects/ComfyUI/.venv/Scripts/python.exe main.py --listen 127.0.0.1 --lowvram --cpu-vae
```

Verified visible ControlNet models:
- `control_v11p_sd15_canny_fp16.safetensors`
- `control_v11p_sd15_openpose.pth`

Smoke results:
- `greenwake_controlnet_grass_flat_smoke_v1`: generated, but strict QA flagged `flat_terrain_too_empty_or_weak`.
- `greenwake_controlnet_grass_grid_smoke_v1`: generated and strict QA passed.

Working baseline:
- checkpoint: `DreamShaper_8_pruned.safetensors`
- lora: disabled
- steps: `18`
- cfg: `6.8`
- control template: `flat_grid.png`
- control model: `control_v11p_sd15_canny_fp16.safetensors`
- control strength: `0.58`
- control end: `0.78`

Updated:
- `Tools/AssetForge/queue_tile_family_requests.py` now defaults flat terrain to `flat_grid` with request-level no-LoRA Comfy settings.
- `Tools/AssetForge/asset_forge.local.example.json` no longer defaults tiles to the stale `litiso_tile_prop_v1_final` LoRA.
- Active local `asset_forge.local.json` tile defaults were updated the same way.

### 2026-06-09 - Greenwake generated tile pack rejected for Unity art use

The deterministic Greenwake v7 geometry tiles passed structural checks, but the user rejected them as nowhere near ready for Unity art use. Treat this as a correction: structural QA is not art approval.

Ready/reference paths:
- Review pack: `Assets/Generated/_Review/greenwake_geometry_derived_v7`
- Review report: `Assets/Generated/_Review/greenwake_geometry_derived_v7/review_report.json`
- Decisions: `Assets/Generated/_Review/greenwake_geometry_derived_v7/review_decisions.json`
- Approval manifest: `Assets/Generated/_Review/greenwake_geometry_derived_v7/approval_manifest.json`
- Handoff validation: `Assets/Generated/_Review/greenwake_geometry_derived_v7/tile_prop_handoff_validation.json`
- Showcase preview: `Assets/Generated/_Review/greenwake_geometry_derived_v7/derived_geometry_showcase_13x13.png`

Removed path:
- `Assets/Generated/Tiles/Greenwake`

Validation:
- Strict QA: 57 total, 57 pass, 0 review.
- Previous promotion tooling test: 57 copied, 0 skipped, 0 failed.
- Previous handoff validation: 57 total, 57 pass, 0 review.
- Art approval: failed. Do not integrate.

Tooling added/updated:
- Added `Tools/AssetForge/prepare_derived_tile_review_pack.py` to convert deterministic derived manifests into `review_report.json` + `review_decisions.json`.
- Updated `Tools/AssetForge/approve_review_pack.ps1` and `Tools/AssetForge/validate_tile_prop_handoff.ps1` to honor per-decision Unity import metadata such as PPU and tile pivots.
- Added `Tools/AssetForge/build_tile_showcase_preview.py` for a deterministic 13x13 map-style preview.
- Added `Tools/AssetForge/prepare_tile_training_capture.py` for dry-run-first tile dataset capture.

Notes for Claude:
- These are generated review/template assets only. Do not replace authored Resources tiles and do not wire Greenwake v7 into biome/runtime content.
- Treat old `Assets/Generated/Tiles/Forest` outputs as legacy experiments unless separately approved.
- The next required tile pass is art-direction lock first, not Unity import.

### 2026-06-09 - Supplied isometric style-lock intake

The user supplied two local archives as the style target:

- `C:/Users/garyc/Downloads/isometric tileset.7z`
- `C:/Users/garyc/Downloads/critters.7z`

Extracted them into ignored review/reference space only:

- `Assets/Generated/_Review/style_lock_sources`

Analysis outputs:

- `Assets/Generated/_Review/style_lock_sources/style_lock_analysis/STYLE_LOCK_ANALYSIS.md`
- `Assets/Generated/_Review/style_lock_sources/style_lock_analysis/style_lock_inventory.json`
- `Assets/Generated/_Review/style_lock_sources/style_lock_analysis/tileset_contact_sheet.png`
- `Assets/Generated/_Review/style_lock_sources/style_lock_analysis/critters_contact_sheet.png`
- `Assets/Generated/_Review/style_lock_sources/style_lock_analysis/tileset_palette.png`
- `Assets/Generated/_Review/style_lock_sources/style_lock_analysis/critters_palette.png`
- `Tools/AssetForge/style_profiles/litiso_iso_reference_v1.json`

Observed contract:

- 226 PNGs total.
- Tileset: 116 PNGs, mostly 32x32.
- Critters: 110 PNGs, mostly 46x32 individual frames plus larger strips/sheets.
- Critter directions are diagonal isometric: NE/NW/SE/SW.
- Critter actions observed: idle, run, walk, burrow, unburrow, tunnel.

Pipeline updates:

- Sprixen tile post-process fallback now uses 32x32 for tile mode.
- `Tools/AssetForge/asset_forge.local.example.json` tile mode default resolution is now 32x32.
- Machine-local `Tools/AssetForge/asset_forge.local.json` tile mode default resolution was also set to 32x32; do not commit or print that file because it may contain private credentials.

Important:

- No license/readme was found in the extracted archives. Treat these as style references only until the license/training permission is documented.
- Do not train from these packs or import their pixels into runtime Unity content until the user confirms licensing.
- Next target is a 5-tile visual micro pack matching this style, not a full biome family.
- Current working state:

- Diagonal OpenPose controls now exist for `NE/NW/SE/SW` under `Assets/Generated/_Review/_PoseControls/litiso_openpose_diagonal_v1`.
- Black-mage style-lock requests are queued in `Temp/AssetForge/black_mage_requests`, and the first request dry-runs cleanly through the existing Comfy worker contract.
- The LoRA dataset planner now emits a trainer-compatible dry-run for the style-lock pack: 211 records split into `tile`, `critter`, and `reference` categories.
- Separate dry-run launchers now exist for tile and critter style training, and the shared resumable launcher can stay entirely under `Temp/LoRA` during dry-run validation.

Follow-up state after Claude Fable handoff:

- The user confirmed license/training rights for the supplied style-lock packs, and the dataset was applied externally at `C:\Projects\Pixel Pipeline\datasets\lit_iso\style_lock\iso_reference_v1`.
- Tile LoRA training `litiso_iso_reference_tile_style_v1` is running; latest observed progress was about `800/1000` with a saved step-800 checkpoint.
- Black mage v1-v5 are not approval-ready; v1 has clutter/rings/backdrops and v5 only contains the old NW frame.
- `Tools/AssetForge/queue_black_mage_iso_requests.py` now supports `--batch-count` and `--variant-suffix`; v6 requests are staged with `batch_count = 4` and cleaned prompts in `Temp/AssetForge/black_mage_requests`.
- Added `Tools/AssetForge/build_black_mage_candidate_review_sheet.py`. It validates against `Assets/Generated/_Review/black_mage_iso_renders_v1` and writes a manual QC sheet/manifest to `Assets/Generated/_Review/black_mage_iso_renders_v1_validation`. Use it after v6 renders before approving, training from, or importing any black mage frame.
- Added `Tools/LoRA/evaluate_iso_reference_tile_style_lora.ps1`. Dry-run currently selects the step-800 checkpoint and writes `Temp/LoRA/litiso_iso_reference_tile_style_v1.post_training_eval_plan.json`, but live eval should wait until tile training completes.
- Added `Tools/LoRA/build_lora_eval_contact_sheet.py` and wired it into the tile eval wrapper. Validation output is `Temp/LoRA/black_mage_v1_lora_contact_validation.png`.
- Added `Tools/AssetForge/run_post_tile_training_review_pass.ps1`. Dry-run writes `Temp/AssetForge/post_tile_training_review_pass_v6.json` and holds GPU work until training completes; live mode will run tile eval, v6 mage renders, and the v6 candidate sheet.

### 2026-06-09 - Proper Pixel Art sidecar integration

Integrated `KennethJAllen/proper-pixel-art` as an optional Asset Forge post-process sidecar. No third-party source was copied into the Unity repo.

Added:

- `Tools/AssetForge/proper_pixel_art_cleanup.py`
- `Tools/AssetForge/run_proper_pixel_art_cleanup.ps1`

Updated:

- `Tools/AssetForge/process_generation_request_comfy.ps1`
- `Tools/AssetForge/process_generation_request_sprixen.ps1`
- `Tools/AssetForge/asset_forge.local.example.json`
- `Tools/AssetForge/queue_black_mage_iso_requests.py`
- `Tools/AssetForge/queue_tile_family_requests.py`
- `Docs/handoff/CLAUDE_FABLE_ASSET_PIPELINE_HANDOFF.md`

Behavior:

- Requests opt in by including `proper_pixel_art`, `proper-pixel-art`, or `proper_pixel_art_cleanup` in `post_process`.
- Sidecar review candidates are written under `_ProperPixelArt`.
- Reports/contact sheets are linked from review reports, generation manifests, and request status.
- The package is optional. If it is missing, the adapter writes a `missing_dependency` report instead of blocking the normal generation path.
- Default `pixel_width` is `1` so normalized 32/64/128px review assets are preserved. Increase it only for large fake-pixel source images.

Validation:

- PowerShell parse checks passed for both generation processors and the wrapper.
- Missing-dependency smoke writes the expected `missing_dependency` report.
- Source-checkout smoke using `C:\tmp\proper-pixel-art-inspect` processed one black mage review image and produced a valid contact sheet.
- Installed `proper-pixel-art==1.5.1` into `C:\Projects\ComfyUI\.venv` and reran the one-image smoke through the normal wrapper without a source override.

### 2026-06-09 - Tile LoRA synced and eval matrix staged

Goal context: keep generated outputs in review/temp space and do not import to Unity without explicit approval.

Current state:

- Tile LoRA training `litiso_iso_reference_tile_style_v1` is complete at 1000/1000.
- Final checkpoint: `C:\Projects\LoRA-Training\outputs\litiso_iso_reference_tile_style_v1\litiso_iso_reference_tile_style_v1_final.safetensors`
- Synced to ComfyUI: `C:\Projects\ComfyUI\models\loras\litiso_iso_reference_tile_style_v1_final.safetensors`
- Sync manifest: `C:\Projects\ComfyUI\models\loras\litiso_iso_reference_tile_style_v1.sync.json`
- SHA256: `7AD189670F34C6C377A090ACC2BB4E3726AF37152DAEDECE2E9A698FDD83290D`

Added:

- `Tools/LoRA/evaluate_iso_reference_tile_style_matrix.ps1`
- `Tools/LoRA/build_style_lock_tile_reference_sheet.py`
- `Tools/LoRA/score_tile_style_eval_outputs.py`
- `Tools/LoRA/select_tile_style_eval_family.py`

Behavior:

- The matrix wrapper evaluates tile LoRA strengths `0.35`, `0.55`, `0.72`, and `0.90`.
- Planned live outputs now stay under `Temp\LoRA\Evals`.
- The wrapper blocks live GPU evaluation while `litiso_iso_reference_critter_style_v1` is running unless explicitly forced.
- `Tools/AssetForge/run_post_tile_training_review_pass.ps1` now uses the matrix wrapper and also blocks tile eval/mage renders while critter training is active.
- `Tools/LoRA/eval_litiso_tile_prop_v1_comfy.py` is now tile-family focused instead of prop/tile mixed. Current test families: earth block, grass top, grass-to-earth edge, stone-water edge, dark water, and ice.
- Each eval manifest includes the style profile, source tileset contact sheet, target tile families, and acceptance gate.
- After live matrix eval, the matrix wrapper now runs a style-score report and then creates a small selected-family review pack by choosing the lowest-scored candidate per family. This stays in `_Review` and is not Unity-approved.

Validation:

- Parser checks passed for the matrix wrapper and post-training sequencer.
- Python compile checks passed for the tile evaluator, eval contact-sheet builder, style-lock reference sheet builder, style scorer, and selected-family builder.
- Dry-run matrix summary: `Temp\LoRA\litiso_iso_reference_tile_style_v1.eval_matrix_plan.json`
- Dry-run sequencer summary: `Temp\AssetForge\post_tile_training_review_pass_v6.json`
- Style-lock target sheet: `Temp\LoRA\Evals\stylelock_tile_reference_targets.png`
- Style-lock target manifest: `Temp\LoRA\Evals\stylelock_tile_reference_targets.json`
- Score report placeholder: `Temp\LoRA\Evals\tile_style_eval_scores.json`
- Selec