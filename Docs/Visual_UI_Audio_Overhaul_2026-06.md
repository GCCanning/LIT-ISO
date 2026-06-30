# LIT-ISO Visual, UI, HUD, Audio, and Atmosphere Overhaul

Status: implementation contract draft, 2026-06

## Goal

Make the game feel alive and readable by unifying four player-facing layers:

1. HUD and menus: a Terraria/Romestead-inspired content model with LIT-ISO's dark-fantasy PixelLab skin.
2. Fast inventory access: a hotbar that pages through the full backpack, not just the first fixed row.
3. Fluid abilities: Q/E/R/F quick-cast plus a hold wheel that supports fast assign, swap, inspect, and one-shot cast.
4. World presence: Built-in pipeline sprite shaders, weather, wind, ambient motion, and complete SFX coverage.

This is not a new art direction. PixelLab remains the source style. Reference games inform content density and ergonomics only.

## Current Runtime Anchors

- Main menu: `Assets/Scripts/UI/WelcomeScreenManager.cs`
- Loading screen: `Assets/Scripts/UI/LoadingScreen.cs`
- Shared visual tokens: `Assets/Scripts/UI/Theme/LitIsoTheme.cs`
- In-game HUD: `Assets/Scripts/UI/InGame/GameUIController.cs`
- Main tab panel: `Assets/Scripts/UI/InGame/CharacterPanelView.cs`
- Inventory/crafting/map views: `Assets/Scripts/UI/InGame/InventoryView.cs`, `CraftingView.cs`, `WorldMapView.cs`
- Ability wheel: `Assets/Scripts/UI/InGame/AbilityWheelView.cs`
- Runtime ability loadout: `Assets/Scripts/UI/InGame/FoundationAbilityLoadout.cs`
- Inventory and hotbar model: `Assets/Scripts/IsoCoreFoundation/Inventory/Inventory.cs`, `Hotbar.cs`
- World tint shader path: `Assets/Scripts/IsoCoreFoundation/World/SpriteAmbient.cs`, `Assets/Shaders/SpriteAmbient.shader`
- Weather and wind: `Assets/Scripts/IsoCoreFoundation/World/FoundationWeatherVisuals.cs`, `WindSway.cs`
- Audio: `Assets/Scripts/IsoCoreFoundation/Audio/SfxManager.cs`, `WorldAudioController.cs`

## HUD Content Model

The target is a compact, information-rich survival RPG HUD:

- Vitals block: HP, MP, stamina, XP, level, rank/class/calling, day/time, weather icon.
- Hotbar: 9 visible slots, with page indicator and row cycling across the whole inventory.
- Ability strip: Q/E/R/F active skills with cooldown, cost, disabled state, and element color.
- Equipment glance: weapon, offhand/tool, armor rating, accessory slots, and durability warning.
- Objective tracker: pinned quest, current survival need, and next System prompt.
- Minimap block: biome name, weather, player marker, known POIs, expand to map tab.
- Toast stack: pickups, craft results, quest updates, level-ups, unlocks, and warnings.
- Context prompt: targeted action, keybind, blocked reason, and expected result.

The player should be able to play moment-to-moment from the HUD without opening the full System panel.

## System Panel Tabs

The main player panel should become a tabbed "System Book" with these tabs:

1. Inventory: backpack grid, sort/filter/search, split/drop/favorite/lock, quick assign to hotbar row.
2. Equipment: Terraria-style paper doll with weapon, offhand/tool, head, chest, legs, feet, hands, accessory slots, cosmetic preview.
3. Skills: learned skills, assignable combat abilities, passives, cooldown/cost info, drag/drop to Q/E/R/F.
4. Quest Log: active, available, completed, pinned objective controls, rewards, giver/location.
5. Map: explored map, pins, biome labels, settlements, dungeons, camp, custom markers.
6. Crafting: station recipes, have/need costs, quantity, craft queue, blocked reasons.
7. Character: stats, class/calling, rank, titles, affinities, resistances, unspent points.
8. Professions: gathering/crafting tracks, unlocks, station bonuses, recipe tiers.
9. Journal/System: System messages, evidence log, trial score forecast, tutorials, glossary.
10. Settings: audio, UI scale, HUD layout, keybinds, accessibility, graphics intensity.

Additional recommended surfaces:

- Build mode palette: separate from inventory so construction can be fast and readable.
- Storage/vendor split views: Terraria-like side-by-side grids with quick transfer.
- Ability loadout editor: available from Skills and from the hold wheel.
- Compendium: discovered mobs, drops, biomes, crops, recipes, and weather effects.

## Hotbar Paging

Current behavior selects only the first `Hotbar.Size` inventory slots. Target behavior:

- Keep 9 visible slots.
- Add `ActiveRow` and `Rows = ceil(Inventory.SlotCount / VisibleSlotCount)`.
- Visible hotbar slot `i` maps to inventory slot `ActiveRow * VisibleSlotCount + i`.
- Number keys select visible slot 1-9.
- Mouse wheel cycles selected slot by default.
- Shift + mouse wheel, Tab, or dedicated keys cycle hotbar row.
- The HUD shows row pips, for example `Row 2/5`.
- Inventory tab highlights the currently exposed row.
- Save data persists both selected visible slot and active row.

First files likely touched:

- `Assets/Scripts/IsoCoreFoundation/Inventory/Hotbar.cs`
- `Assets/Scripts/IsoCoreFoundation/Core/FoundationSaveData.cs`
- `Assets/Scripts/IsoCoreFoundation/Core/FoundationBootstrap.cs`
- `Assets/Scripts/IsoCoreFoundation/Player/PlayerInteraction.cs`
- `Assets/Scripts/UI/InGame/FoundationHudAdapter.cs`
- `Assets/Scripts/UI/InGame/GameUIController.cs`
- `Assets/Scripts/IsoCoreFoundation/Editor/FoundationIntegratedSliceValidator.cs`

## Ability Wheel

Current behavior already supports Q/E/R/F and hold X. Target behavior:

- Hold X opens a radial wheel centered on the cursor/player.
- Inner ring: current Q/E/R/F loadout.
- Outer ring: learned abilities grouped by element and readiness.
- Flick toward an inner slot and release to cast that slot.
- Drag an outer ability onto Q/E/R/F to assign.
- Right-click or modifier opens details without assigning.
- Wheel remains readable with controller later: right stick chooses, face buttons assign.
- Cooldown and affordability should be visible in the HUD and the wheel.
- The wheel should pause only player combat input, not the whole world, unless a settings option says otherwise.

First files likely touched:

- `Assets/Scripts/UI/InGame/AbilityWheelView.cs`
- `Assets/Scripts/UI/InGame/FoundationAbilityLoadout.cs`
- `Assets/Scripts/UI/InGame/GameUIController.cs`
- `Assets/Scripts/IsoCoreFoundation/Progression/FoundationAbilitySystem.cs`
- `Assets/Scripts/IsoCoreFoundation/Progression/FoundationAbilityDefinition.cs`

## PixelLab Asset Plan

Use the existing scripts as the base:

- `Tools/PixelLab/generate_menu_scene.py`
- `Tools/PixelLab/generate_class_scene.py`
- `Tools/PixelLab/generate_ui_set.py`
- `Tools/PixelLab/generate_ui_polish.py`

Add or extend scripts for:

- scene backgrounds: main menu, loading, class assignment, world creation, character creation, options, death/respawn.
- full UI kit: panel frames, tabs, buttons, slots, scrollbars, tooltips, minimap frame, quest tracker, ability wheel, equipment slot frames.
- icons: equipment slots, skill elements, quest markers, map pins, weather states, settings categories.
- contact sheets: one contact sheet per batch with stable IDs and target runtime path.
- promotion: approved files copy into exact `Assets/Resources/UI/...` paths with `.meta` preserved or generated by Unity.

All generated UI art must use crisp pixel edges, transparent backgrounds for components, no text baked into sprites, and no copyrighted style imitation.

## Atmosphere and Shader Plan

Keep the Built-in Render Pipeline. Do not make URP a dependency for this overhaul.

Low-risk shader and lighting improvements:

- Expand `SpriteAmbient` from simple global tint into a small family of sprite materials:
  - ambient lit sprite
  - vegetation sway sprite or transform-sway fallback
  - water shimmer sprite
  - additive glow sprite
  - silhouette/contact shadow sprite
- Keep `Shader.SetGlobalColor("_AmbientColor")` for day/night/weather.
- Add global wind parameters for shader-capable sprites, but keep `WindSway` for trees and props that need transform motion.
- Add weather intensity settings so rain, snow, mist, wind, lightning, and leaf drift can scale smoothly.
- Add ambient details: fireflies, falling leaves, dust motes, chimney smoke, water sparkle, cave motes, dungeon fog pockets.

Weather should stay visual-only until a separate gameplay-weather pass is explicitly scoped.

## Audio Plan

The audio systems exist, but coverage is thin. Add sound by event category, not one-off calls.

Core event categories:

- Player movement: footstep grass, dirt, stone, wood, snow, water edge; sprint and landing variants.
- Tools: chop, mine, hoe, till, plant, water, harvest, pickup, place, invalid placement.
- Combat: weapon swing, hit flesh, hit stone/wood, projectile cast, projectile impact, shield/block, player hurt, mob hurt, mob death.
- Mobs: idle chirp/growl, alert, attack windup, attack, hurt, death, sleep/passive loops for settlements.
- UI: hover, click, tab switch, panel open/close, assign skill, error, confirm, delete warning.
- Progression: XP tick, level up, rank up, skill unlock, quest complete, System message, class assignment reveal.
- World: rain loop, wind gust, thunder, snow hush, forest birds, beach waves, mountain wind, cave drip, dungeon ambience, campfire crackle.
- Economy/crafting: craft success, station loop, coin, vendor buy/sell.

Implementation shape:

- Keep `SfxManager.Play(key)` for simple 2D one-shots.
- Add a typed facade, for example `FoundationAudioEvents`, so systems call semantic methods instead of raw strings.
- Add surface-aware footsteps using the player's current cell biome/block.
- Add mob audio keys to mob definitions or a small lookup table by mob archetype.
- Add clip variants by suffix, for example `footstep_grass_01`, `footstep_grass_02`, with random selection and pitch variation.
- Add a validation report listing missing audio keys separately from missing visual assets.

First files likely touched:

- `Assets/Scripts/IsoCoreFoundation/Audio/SfxManager.cs`
- `Assets/Scripts/IsoCoreFoundation/Audio/WorldAudioController.cs`
- new `Assets/Scripts/IsoCoreFoundation/Audio/FoundationAudioEvents.cs`
- `Assets/Scripts/IsoCoreFoundation/Player/IsoFoundationPlayer.cs`
- `Assets/Scripts/IsoCoreFoundation/Player/PlayerInteraction.cs`
- `Assets/Scripts/IsoCoreFoundation/Mobs/Mob.cs`
- `Assets/Scripts/UI/InGame/LevelUpView.cs`
- `Assets/Scripts/UI/InGame/InGameNotificationView.cs`
- `Assets/Scripts/IsoCoreFoundation/Editor/FoundationIntegratedSliceValidator.cs`

## Recommended First PR

Build the "HUD access and feel" slice first:

1. Hotbar row paging over the whole inventory.
2. HUD row indicator and visible row mapping.
3. Ability wheel interaction polish and assignment feedback.
4. Basic UI SFX routing for tab/click/assign/error.
5. Editor validation for hotbar row save/load and required UI/SFX keys.

This gives immediate player-facing value without waiting for a full asset pass.

## Recommended Second PR

Build the "world feels alive" slice:

1. Surface-aware footsteps.
2. Mob idle/alert/hurt/death SFX hooks.
3. Level-up, quest-complete, skill-unlock, and System-message sounds.
4. Biome ambience validation for `Resources/Audio/Ambient/Biome/<biomeId>`.
5. Weather intensity tuning and wind/leaf/dust particle variants.

## Done Definition

- The HUD exposes survival, inventory, combat, and quest state without opening menus.
- Any inventory row can be exposed on the hotbar within one input.
- Any learned skill can be assigned to Q/E/R/F in under two seconds.
- Every major player action produces a readable animation, sound, or UI response.
- Weather changes are visible and audible.
- Trees and lightweight vegetation move subtly even in clear weather and more during wind/rain.
- UI assets and audio assets have manifests, validators, and exact runtime paths.
