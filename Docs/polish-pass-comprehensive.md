# LIT-ISO — Comprehensive Polish Pass Plan

> Goal: take the isometric survival/crafting prototype to a **visibly, audibly, and mechanically polished** state. This document is both a **prioritized ~2-hour execution track** (P0→P2) and a **complete "no bad idea" backlog** for beyond that.

---

# Project Overview
- **Game Title:** LIT-ISO (working title) — an isometric survival/crafting sandbox.
- **High-Level Concept:** Explore a procedurally generated isometric world; harvest trees/rocks/bushes/ore, craft tools and stations, farm crops, and survive a day/night cycle. Cozy-survival tone with a glowing flame-knight protagonist.
- **Players:** Single player (a large orphaned multiplayer/LitRPG "TrialWeek" codebase exists but is NOT wired in — see Risks).
- **Inspiration / Reference Games:** Stardew Valley (cozy loop), Don't Starve (day/night danger, art mood), Dofus/Albion (iso presentation).
- **Tone / Art Direction:** Pixel-art isometric, 32px tiles, warm daytime / cool moonlit night, soft ambient motes.
- **Target Platform:** StandaloneWindows64 (mobile dependency resolver present; not the focus).
- **Screen Orientation / Resolution:** Landscape; Pixel Perfect Camera at 640×360 reference, assetsPPU 32.
- **Render Pipeline:** Built-in (confirmed). All sprite shaders are Built-in defaults plus the custom `IsoCore/SpriteAmbient` day/night tint shader.

## Current State (verified baseline)
- Live flow: `MenuScene` (WelcomeScreenManager) → New Game → `IsoCoreFoundation` scene → `FoundationBootstrap.Awake()` builds the entire game in code.
- **Content (all code-defined in `FoundationContent.BuildDefault()`):** 13 blocks, ~30 items, 4 placeables (workbench/chest/lantern/furnace), 4 resource nodes (tree/rock/bush/copper_vein), 3 mobs (deer/slime/fox), 2 crops (carrot/wheat), 5 biomes (meadow/forest/desert/beach/snow), 15 recipes.
- **World currently runs in `flatWorld` mode** (meadow-only, grass) with rolling hills + clustered tree/bush/rock decorations re-enabled. The 5-biome generator exists but is bypassed by `flatWorld=true`.
- **Rendering polish already in place:** pixel-perfect camera, flush tiles + baked light borders, near-on-top depth sort, walk-behind prop fade, day/night sun/moon shadows + global ambient tint shader, frame-amortized chunk streaming (radius 2), directional knight animator, ambient particles (pollen/fireflies), smooth camera follow.
- **The live HUD is the uGUI `GameUIController`** (`Assets/Scripts/UI/InGame/`, namespace `LitIso.UI.InGame`), spawned by `GameHudInitializer` on `FoundationBootstrap.Ready`, which then **disables the IMGUI `FoundationHUD`**. HUD polish must target `GameUIController` + its adapters, not the IMGUI HUD.
- **0 console errors/warnings** at rest — clean baseline.

## Biggest opportunities (summary)
1. **Near-silent game:** only ONE audio clip exists (player idle hum). Music + all SFX missing → audio is the single biggest perceived-polish gap.
2. **Empty UI art:** no item icons (hotbar/inventory show blanks) and no HUD skin frames (uGUI HUD is flat-color procedural).
3. **Finished art sitting unused:** `Assets/Generated/Tiles/**` (25 Plains+Forest tiles incl. transition + water-edge), `Assets/Generated/Props/**` (11), `Assets/Art/BiomeDecorations/**` (25 incl. wheat crop, flower, log, stump, pine, barrel, chest, ruins, town buildings), a fully-animated **slime** (21 frames), and LPC character sheets — all importable for big visual wins.
4. **Mobs render as placeholder boxes** despite slime animation existing.
5. **Flat single-biome world** hides the 5-biome generator + water.
6. **Survival has no stakes:** vitals/hunger/combat not driving gameplay; night isn't dangerous.

---

# Game Mechanics

## Core Gameplay Loop
**Explore → Harvest → Craft → Build/Farm → Survive the night → repeat, expanding capability.**
Current loop is functional but lacks feedback and stakes. Polish targets:
- **Harvest feel:** node shake, debris particles, depletion pop, floating "+2 Wood".
- **Survival stakes:** hunger drains over time; eating crops/apples restores it; health tied to hunger; mobs become dangerous at night, pushing the player to craft light + shelter.
- **Progression read:** day counter, simple goals/hints, audible/visual feedback for crafting and level-ups.

## Controls and Input Methods
- **Input backend:** Legacy `Input` axes (`Horizontal`/`Vertical`) drive `IsoFoundationPlayer`; `PlayerInteraction` routes E/mouse for harvest/place/farm. (Project also has New Input System available, but live code uses Legacy — keep consistent.)
- Polish: on-screen control hints panel (toggle), interaction prompt already exists; add a hover/selection highlight on the targeted tile/node, and a pause menu (Esc).

---

# UI
Target the **live uGUI `GameUIController`** (`Assets/Scripts/UI/InGame/`):
- **Bottom HUD bar:** centered hotbar (9 slots) + vitals (HP / hunger / XP-level). Currently flat-color; skin with `Resources/UI/InGame/` art (slot, slot_selected, bar fills/track, bar_bg).
- **Panels:** Inventory (bound to live data), Crafting (placeholder → bind to live `CraftingSystem`), Character Sheet (placeholder). Skin with `panel`, `inv_slot`, `craft_panel`, `craft_button`.
- **Item icons:** drop `Resources/Items/<itemId>.png` (lowercase snake_case) — resolves automatically via `ItemIconResolver`. Needed for ~12 core items first: `wood, stone, fiber, hide, wheat, copper_ore, copper_bar, apple, carrot, slime_goo, wood_axe, wood_pickaxe` (extend to all ~30).
- **Menu:** `WelcomeScreenManager` auto-loads `Resources/UI/Menu/{background,logo,panel}` (missing; falls back to `CampfireMenu.png`). Add `background`, `logo`, `panel` for a finished main menu.
- **New UX:** pause/settings overlay (volume sliders), scene fade in/out, control-hints, death/respawn screen.

Wireframe (HUD, bottom of screen):
```
[ HP ▓▓▓▓▓░░ ]                                                   ⏱ 06:24 Day 2
[ Hunger ▓▓▓░ ]        [1][2][3][4][5][6][7][8][9]   <- hotbar (selected = glow)
[ XP ▓▓░░ Lv3 ]
```

---

# Key Asset & Context

## Live edit targets (namespace `IsoCore.Foundation` unless noted)
- World gen/stream: `World/IsoTerrainSampler.cs`, `World/IsoWorld.cs`, `World/IsoWorldController.cs`, `World/IsoWorldRenderer.cs`, `Core/IsoGrid.cs`, `Core/FoundationConfig.cs`.
- Content: `Core/FoundationContent.cs` (the catalog — single-file content edits).
- Player: `Player/IsoFoundationPlayer.cs`, `Player/PlayerAnimator.cs`.
- Harvest/nodes/mobs/farming: `Harvesting/ResourceNode.cs`, `Harvesting/HarvestSystem.cs`, `World/MobSpawner.cs` + mob runtime, `Farming/FarmingSystem.cs`.
- Day/night & FX: `Core/DayNightSystem.cs`, `World/AmbientLightController.cs`, `World/AmbientParticles.cs`, `World/DecorationShadow.cs`, `World/PropOcclusionFader.cs`, `Shaders/SpriteAmbient.shader`.
- Resolvers (filename-based art injection — no code change to add art): `World/TileSpriteResolver.cs`, `Harvesting/DecorationSpriteResolver.cs`, `UI/InGame/ItemIconResolver.cs`.
- HUD (LIVE, namespace `LitIso.UI.InGame`): `UI/InGame/GameUIController.cs`, `GameHudInitializer.cs`, `FoundationHudAdapter.cs`, `FoundationInventoryAdapter.cs`, `GamePanelsController.cs`, `CraftingView.cs`, `CharacterSheetView.cs`.

## Unused-but-ready art to import (low risk, high ROI)
- `Assets/Generated/Tiles/{Forest,Plains,Shared}/*` — 25 tiles incl. transitions + `*_water_edge` (⚠ **PPU 64**, live tiles are **PPU 32** — must normalize to 32 on import, pivot (0.5,0.75), Point filter).
- `Assets/Generated/Props/{Forest,Plains,Shared}/*` — 11 biome trees/bushes/rocks (128×128, PPU 128).
- `Assets/Art/BiomeDecorations/*` — `isometric_..._wheat_crop` (crop!), `isometric_flower`, `isometric_log`, `isometric_stump`, `isometric_pine_tree (1)`, `isometric_barrel`, `isometric_wooden_loot_chest`, `isometric_gray_rock (1)`, ruins, + `_Towns/` (6 buildings), `_HighDetail_Unused/` (7).
- `Assets/Resources/Enemies/Slime/` — `slime-Sheet.png` + 21 individual frames (idle/move/hurt/die/attack) — ready to animate.
- `Assets/LPC/**` — full LPC layered character art (body/hair/armor) for future player customization.

## Asset-generation targets (use AI generation where no source exists)
- Item icons (12 core → all 30), UI skin frames (slot/bars/panels), menu art (background/logo/panel), 2 music tracks (day/night), SFX set (footstep/chop/mine/harvest/pickup/craft/UI/mob), optional extra mob sprites.

## Conventions to honor
- Pixel art: **Point** filter, **No compression**, no mipmaps. Tiles PPU 32, pivot (0.5,0.75). Decorations bottom-anchored pivots. Runtime resolves art by **exact lowercase filename**.
- All new behavior gated behind `FoundationConfig` flags so it's reversible.
- Always confirm `IsoCore.Foundation` namespace — duplicate `ResourceNode`/`CraftingSystem`/camera classes exist in the orphaned codebase.

---

# Implementation Steps

> Tiered. **P0 = must-land this session** (highest polish/risk ratio). **P1 = strong wins if time allows.** **P2 = stretch.** Each step lists role, dependencies, parallelizable. Asset generation runs concurrently with code work.

## PHASE 0 — Safety & baseline (P0, ~8 min)
1. **Capture baseline + confirm clean.** Screenshot day & night in-editor, confirm 0 console errors, note current `FoundationConfig` flag values.
   - Role: developer · Deps: none · Parallelizable: no (gates everything)
2. **Reaffirm live-edit guardrails.** Verify HUD = `GameUIController` (not IMGUI); list duplicate-class hazards. No code change.
   - Role: developer · Deps: none · Parallelizable: yes

## PHASE 1 — Wire up finished-but-unused art (P0, ~30 min) — biggest ROI/lowest risk
3. **Import slime animation → live mobs.** Add a `MobAnimator` (mirror `PlayerAnimator`) that plays the slime idle/move frames from `Resources/Enemies/Slime/`; route through `MobSpawner` so `slime` renders animated instead of a placeholder box. Add `deer`/`fox` art if available, else keep boxes tinted.
   - Role: developer · Deps: 1 · Parallelizable: yes
4. **Port extra decorations → `Resources/Decorations/` + wire nodes.** Bring `wheat_crop`(crop), `flower`, `log`, `stump`, `pine_tree`, `gray_rock` into Resources (Point, bottom pivot, sized to tile). Add new resource-node / decoration ids in `FoundationContent` (e.g. `flower` ambient deco, `log`/`stump` harvestable wood) and add them to the clustered-decoration scatter.
   - Role: developer · Deps: 1 · Parallelizable: yes
5. **Import Generated biome tilesets → `Resources/Tiles/` (PPU-normalized to 32).** Bring Plains+Forest base/dirt/path/water_edge + transition tiles in; map them to existing block ids and add a few new blocks (e.g. `leaf_litter`, `mud_path`). Verify flush tessellation at PPU 32.
   - Role: developer · Deps: 1 · Parallelizable: yes (art import) — code wiring depends on 5

## PHASE 2 — Audio (P0, ~22 min) — biggest perceived-polish gap
6. **SfxManager + core SFX.** Add a lightweight `SfxManager` (pooled AudioSources) and a `Resources/Audio/SFX/` set: footstep, chop (axe), mine (pickaxe), harvest-complete, item pickup, craft success, UI click, mob hit. Generate clips. Wire trigger points (player step, `HarvestSystem`, crafting, hotbar/UI, mob).
   - Role: developer · Deps: 1 · Parallelizable: yes (generation concurrent)
7. **Day/night music + ambient bed.** Add `Resources/Audio/Music/{day,night}` tracks and a day birdsong / night crickets ambient loop; cross-fade by `DayNightSystem.NightFactor` via a small `WorldAudioController`. (Re-use intent of orphaned `DayNightMusicManager` but implement clean against Foundation `DayNightSystem`.)
   - Role: developer · Deps: 1 · Parallelizable: yes
8. **Global volume + mute, persisted.** `AudioListener.volume` + per-bus (music/sfx/ambient) sliders saved to PlayerPrefs (consumed by Phase 5 pause menu).
   - Role: developer · Deps: 6,7 · Parallelizable: no

## PHASE 3 — UI art & game-feel juice (P1, ~30 min)
9. **Item icons.** Generate 12 core icons → `Resources/Items/<itemId>.png` (auto-resolves). Verify in hotbar/inventory. Extend toward all 30 if time.
   - Role: developer · Deps: 1 · Parallelizable: yes (generation)
10. **Skin the uGUI HUD.** Generate `slot`, `slot_selected`, `bar_track`, `bar_health_fill`, `bar_hunger_fill`, `bar_xp_fill`, `bar_bg`, `panel`, `inv_slot` → `Resources/UI/InGame/`; confirm `GameUIController` picks them up. Bind the **Crafting** panel to the live `CraftingSystem` (currently placeholder).
   - Role: developer · Deps: 1 · Parallelizable: art generation yes; binding depends on read of GameUIController
11. **Harvest juice.** On hit: node shake + small debris particle burst + SFX; on depletion: scale-pop + dust. Add **floating pickup text** ("+2 Wood") and a **hover/selection highlight** on the targeted node/tile.
   - Role: developer · Deps: 3? no — independent · Parallelizable: yes
12. **Player juice.** Footstep dust puff while walking; ensure knight sheet uses **Point** filter for crispness; confirm consistent drop shadow via `DecorationShadow`-style cast.
   - Role: developer · Deps: 1 · Parallelizable: yes

## PHASE 4 — Gameplay depth & stakes (P1, ~25 min)
13. **Functional survival vitals.** Hunger drains over time; eating `apple`/`carrot` restores (items already have `foodRestore`); health regenerates when fed, drains when starving. Bind HP/hunger to the HUD bars (Step 10).
   - Role: developer · Deps: 10 · Parallelizable: no
14. **Night danger + basic combat.** Slimes aggro/spawn more at night (use `DayNightSystem`); simple melee: player hit applies damage using slime hurt/die frames; mob contact damages player. Drops on death (existing `slime_goo`).
   - Role: developer · Deps: 3 · Parallelizable: partly
15. **Placeable light source (campfire/torch).** A craftable placeable that emits a warm additive glow sprite at night (local "cut-out" against the ambient darkness) — makes the day/night tint gameplay-relevant. Reuse `lantern` placeable or add `campfire`.
   - Role: developer · Deps: none · Parallelizable: yes

## PHASE 5 — World variety & UX shell (P1/P2, ~20 min)
16. **Enable multi-biome world (reversible).** Flip `flatWorld=false` (or add a curated forest+meadow+water mode) so the 5-biome generator + water + shorelines show, using the imported biome tiles (Step 5). Keep a config switch to revert. Verify streaming + walkability + spawn clearing still safe.
   - Role: developer · Deps: 5 · Parallelizable: no
17. **Water polish.** Animate water (frame-swap or UV scroll on `water`/`*_water_edge`); add shoreline transition tiles.
   - Role: developer · Deps: 5,16 · Parallelizable: yes
18. **Shell UX.** Esc pause menu (Resume/Settings/Quit to Menu) with volume sliders (Step 8); scene fade in/out on load; toggleable control-hints; day counter + clock already in `DayNightSystem`.
   - Role: developer · Deps: 8 · Parallelizable: yes
19. **Menu art.** Generate `Resources/UI/Menu/{background,logo,panel}` for a finished main menu.
   - Role: developer · Deps: none · Parallelizable: yes (generation)

## PHASE 6 — Verify, tune, document (P0 close-out, ~15 min)
20. **Play Mode smoke tests** (single-shot, throttle-immune pattern): world builds; mobs animate; SFX/music play; vitals tick; harvest juice + floating text fire; pause menu opens; multi-biome streams without errors. Capture **before/after** day & night screenshots.
   - Role: developer · Deps: all · Parallelizable: no
21. **Console clean + restore MenuScene + update this plan with "what shipped".**
   - Role: developer · Deps: 20 · Parallelizable: no

---

# Comprehensive Backlog ("no idea is bad" — beyond the 2-hour track)

### Visual / rendering
- Tile **auto-transition/blending** using the generated transition tiles (Wang/bitmask edges).
- **Sprite atlasing** for tiles/decorations (batching + kills seam bleed).
- Per-pixel 2D lighting via **URP 2D Renderer + normal maps** (large migration — replaces ambient shader; needs normal maps; documented earlier as high-effort).
- Weather: rain/snow particles + wet/scroll overlays (orphaned `WeatherDefinition` hints at intent).
- Cloud shadows drifting across the ground; god-rays at dawn/dusk.
- Tall-grass sway / decoration idle wind shader.
- Reflections in water; ripples where player/mobs enter.
- Screen-space vignette + subtle bloom on the knight's sword/fireflies (Built-in post).
- Seasonal palette shifts.

### Audio
- Footstep variation by surface (grass/sand/snow/stone/wood).
- Adaptive music layers (calm day → tense night).
- Positional SFX for mobs/water/campfire.
- Stinger on craft/level-up/new-day.

### Gameplay depth
- **Stamina** for sprint/harvest; **XP/level** progression (orphaned `XPSystem` for reference).
- Tool tiers gate harvest speed (already partially modeled) + visual tool swing.
- Inventory weight / chest storage UI (chest placeable exists).
- Quests/objectives + day counter goals (orphaned `Quests/*` for reference).
- Cooking at furnace/campfire (recipes → food).
- Building/structure placement using the **town building art** (`_Towns/`, `_HighDetail_Unused/`).
- Fishing at water; mining depth/caves.
- More mobs + simple AI states (idle/wander/flee/aggro).
- Save/Load (orphaned `SaveGameManager_Phase2` for reference; implement clean against Foundation state).

### UX / UI
- Finish Character Sheet panel (LitRPG "System" window aesthetic — art slots already specified).
- Tooltips on items/recipes; recipe "craftable now" highlighting.
- Minimap / compass.
- Controller support (New Input System is installed).
- Settings: resolution, fullscreen, key rebinding, brightness.
- First-time tutorial flow.

### Content & art integration
- Import **LPC layered character** for player customization (body/hair/armor swap).
- Port remaining `Art/BiomeDecorations` (barrel, chest, ruins) as containers/lootables.
- Full 30-item icon set + tool/placeable icons.
- Crop visual stages (wheat art exists; add growth-stage frames).

### Code health / de-risking
- **Quarantine the orphaned codebase** (Phase2/TrialWeek/EthraClone, legacy Combat/Economy/Guilds/Quests/Towns/Dungeons/Weather/tilemap generators, duplicate `ResourceNode`/`CraftingSystem`/camera) into an `_Legacy` asmdef or folder — reduces confusion & compile surface. ⚠ Careful: `WelcomeScreenManager` + the `GameHudInitializer` uGUI lane live in Assembly-CSharp alongside it; move, don't delete, and keep those.
- Remove the now-redundant IMGUI HUD build cost (it's built every frame then disabled) — either skip building it when the uGUI lane is present, or formally retire it.
- Consolidate day/night to the single Foundation `DayNightSystem`.
- Add an asmdef boundary so live game code can't accidentally reference orphaned classes.

### Performance
- Profile streaming under multi-biome; tune `viewRadiusChunks` / `StreamCellsPerFrame`.
- Object-pool mobs, particles, floating text.
- Bake `FoundationContent` to `.asset` via `FoundationContentBaker` for faster startup (optional).

---

# Verification & Testing
- **Compile gate:** 0 errors/warnings after every phase (`Unity.GetConsoleLogs`).
- **Play Mode smoke tests** (single-shot, wall-clock/immediate to survive editor update throttling; RunCommand polling exits play mode, so each test must self-contain and write results to `SessionState`):
  - World builds; player = knight; **mobs animate** (slime frames advance).
  - **Audio:** `SfxManager` plays a clip on harvest; music `AudioSource.isPlaying` true; volume prefs persist.
  - **Vitals:** hunger decreases over time; eating raises it; HP bound to HUD.
  - **Juice:** harvest spawns debris + floating text; selection highlight tracks target.
  - **Multi-biome:** sampling a wide region yields >1 biome and water; streams with no errors; spawn clearing walkable.
  - **Pause menu** opens on Esc; volume slider changes `AudioListener`/bus volume.
- **Visual verification:** `Unity.SceneView.Capture2DScene` before/after, at **noon** and **deep night**, plus a zoom on harvest juice and the skinned HUD.
- **Edit-mode determinism** where play-mode is flaky: verify resolver/material/PPU/pivot via `RunReadOnlyCommand` (no reflection in inline scripts).
- **Close-out:** restore `MenuScene`, console clean, screenshots captured, plan annotated with shipped vs deferred.

---

# Risks & Mitigations
- **Dual codebase / duplicate classes** → always confirm `IsoCore.Foundation`; never edit orphaned twins. HUD work targets `GameUIController`.
- **PPU mismatch** on imported Generated tiles (64 vs 32) → normalize on import or they render half-size.
- **Multi-biome regressions** (walkability, water trapping spawn, streaming spikes) → behind a `FoundationConfig` flag, keep `flatWorld` fallback, test spawn clearing.
- **Asset-generation latency** → run generations concurrently with code; treat each art batch as independent/parallel; flat-color/placeholder fallbacks already exist so partial art never breaks the build.
- **Play-mode test flakiness** (RunCommand exits play mode; editor throttles updates) → single-shot tests writing to SessionState; fall back to edit-mode deterministic checks + screenshots.
- **Scope:** P0 (Phases 0–2 + close-out) is the commitment; P1/P2 land opportunistically.
