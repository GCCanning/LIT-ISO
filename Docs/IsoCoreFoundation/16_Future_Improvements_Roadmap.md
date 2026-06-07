# LIT-ISO Future Improvements Roadmap

> Draft v0.1. Planning document only.
> Scope: future work for the canonical `IsoCore.Foundation` game, organized by
> phases, ownership, release gates, and acceptance criteria.

## Purpose

This roadmap turns the current Foundation direction and LitRPG System Bible into a
sequenced future plan. It is intentionally clean-room: LIT-ISO may learn from
genre expectations and system scale, but must not copy pixels, audio, prose,
names, factions, quest text, classes, mechanics text, or authored content from
any reference game, book, serial, asset pack, or sample project.

The target fantasy remains:

- cozy isometric survival, crafting, building, and exploration,
- visible LitRPG progression that rewards care and mastery,
- a homestead and valley that become warmer, safer, stranger, and more personal,
- combat as one option among soothing, warding, befriending, rerouting, or
  understanding creatures and biomes.

## Non-Negotiable Invariants

These are project invariants and must not be "fixed" during roadmap work:

- Grid uses `IsometricZAsY`.
- Grid `cellSize` remains `(1,0.5,1)`.
- Camera sorting keeps `transparencySortAxis (0,1,-0.26)`.
- `TilemapRenderer.mode` remains `Individual`.
- Height layers `Height_0..7` map to Unity layer `10+height`.
- Height sorting layers remain `10+height`.
- Movement stays world-query based.
- The foot collider remains trigger-only.
- `maxWalkStepHeight` remains `0`.
- `IsoCore.Foundation` is canonical; legacy `Assembly-CSharp` world code is
  retirement-only unless explicitly called out for migration.

## Ownership Model

Owner tags used below:

| Owner Tag | Lane | Primary Responsibility |
|---|---|---|
| Codex/Foundation | Foundation lane | `Assets/Scripts/IsoCoreFoundation/**`, `Assets/Scenes/IsoCoreFoundation.unity`, `Docs/IsoCoreFoundation/**`, runtime data contracts, validators, world systems |
| Claude/UI | UI and integration lane | `Assets/Scripts/UI/**`, menu flow, HUD views, panels, view adapters, startup integration |
| Claude/Art | Art and content import lane | `Assets/Art/**`, `Assets/Resources/**`, authored sprites, audio, icons, visual polish assets |
| Shared | Shared lane | `ProjectSettings/**`, packages, git metadata, cross-lane contracts, release coordination |
| Human/Design | Product lane | Final creative calls, naming approvals, license decisions, milestone signoff |

Rules:

- One owner per `.unity` scene at a time.
- Cross-lane edits require a handoff or explicit shared-task agreement.
- Art/audio/assets must be newly authored or explicitly licensed for this game.
- Foundation may define IDs, definitions, progression rules, and acceptance specs.
- UI and art lanes may bind, present, import, and polish those definitions.

## Roadmap Summary

| Phase | Goal | Primary Owners | Exit Gate |
|---|---|---|---|
| 0 | Stabilize collaboration, locks, and current contracts | Codex/Foundation, Claude/UI, Human/Design | No lane conflicts, invariants validated, planning accepted |
| 1 | Ship a first 20-minute cozy LitRPG vertical slice | Codex/Foundation, Claude/UI, Claude/Art | New game to first quest completion feels complete |
| 2 | Add persistent homestead, crafting depth, and save/load | Codex/Foundation, Claude/UI | Save, load, build, craft, and progress survive sessions |
| 3 | Expand biomes, routes, ecology, and creature handling | Codex/Foundation, Claude/Art | Two or more regions have distinct systems and rewards |
| 4 | Add NPCs, economy, settlement life, and civic growth | Codex/Foundation, Claude/UI, Claude/Art | A living hamlet loop drives requests, trade, and trust |
| 5 | Build long-arc LitRPG depth and advanced content | Codex/Foundation, Human/Design | Branches, region shifts, ruins, and masterworks support alpha |
| 6 | Polish, optimize, make accessible, and release | Shared | Beta and release-candidate gates pass |
| 7 | Post-release improvements and live backlog | Shared | Updates are scoped, tested, and preserve clean-room identity |

## Phase 0: Stabilization And Planning Lock

Phase 0 exists to keep the team from building on ambiguous contracts or hidden
working-tree drift.

### Foundation Systems

Owner: Codex/Foundation

- Reconfirm runtime handles exposed from `FoundationBootstrap`: content, inventory,
  hotbar, stats, progression, launch config, and future save/load entry points.
- Keep progression systems data-driven and machine-readable.
- Keep item, quest, calling, skill, biome, tile, and mob IDs stable.
- Make all validators check the non-negotiable isometric and movement invariants.
- Separate Foundation data from presentation concerns so UI can bind without
  mutating core game state.

Acceptance:

- Foundation scene validates without changing grid, sorting, height, or movement
  invariants.
- Public runtime handles are documented enough for UI adapters.
- No new dependency on retiring legacy world systems.
- Any partially implemented system has a clear future contract or is marked as
  placeholder.

### UI

Owner: Claude/UI

- Confirm HUD, quest tracker, character sheet, crafting, day/time, notifications,
  and pause/settings contracts are aligned with Foundation handles.
- Keep HUD compact: HP, MP, XP, level, hotbar, pinned quest, day/weather/season,
  and concise status icons.
- Keep detailed stats behind panels and codex-style views.
- Preserve Back, Close, and Esc behavior across panels.

Acceptance:

- UI reads Foundation state through adapters rather than duplicating gameplay
  logic.
- Missing Foundation data fails gracefully with clear placeholder behavior.
- HUD and panels do not overlap across common desktop aspect ratios.

### Art And Content

Owner: Claude/Art with Codex/Foundation specs

- Maintain a clean-room content registry for tile names, item IDs, mob names,
  quest seeds, audio moods, and UI icon needs.
- Verify every candidate external pack or generated asset before shipping.
- Separate placeholder, generated review, prototype, and ship-ready assets.
- Make source-of-truth content IDs match the LitRPG System Bible direction.

Acceptance:

- No unverified asset is treated as ship-ready.
- Art import settings are deterministic and documented.
- Placeholder visuals are easy to identify and replace.

### World And Biomes

Owner: Codex/Foundation

- Define starter biome resource tables, traversal rules, hazard tags, creature
  roles, and region-shift hooks.
- Keep early world generation readable and deterministic from seed.
- Reserve clean names for Mosswake Meadow, Brindlecap Woods, and first route
  transitions.

Acceptance:

- Starter region data supports gathering, building, farming, path repair, and a
  first creature/ecology beat.
- Seeded generation is deterministic enough for automated validation.

### NPCs And Economy

Owner: Codex/Foundation for simulation contracts, Claude/UI for presentation

- Define minimal data shapes for requests, vendors, prices, trust, schedules,
  and settlement services.
- Keep NPC behavior out of the first slice unless it supports the vertical slice
  directly.
- Decide which early economy values are barter, coin, favor, material order, or
  service unlocks.

Acceptance:

- Economy data can be introduced without rewriting inventory or quest systems.
- NPC request UI can bind to data without assuming final dialogue systems.

### QA And Tooling

Owner: Codex/Foundation, Shared where tools cross lanes

- Keep a golden-path validation suite for scene build, launch config, inventory,
  hotbar, progression, quest updates, and save/load once available.
- Add editor validation for clean-room asset status where practical.
- Track test gaps explicitly.

Acceptance:

- Validators can be run before PR review.
- Validation failures name the broken invariant or contract directly.

### Performance

Owner: Codex/Foundation for runtime, Claude/UI and Claude/Art for presentation

- Establish budgets for tile count, generated objects, active particles, audio
  sources, UI rebuilds, and world queries.
- Start simple profiling early, before systems become hard to unwind.

Acceptance:

- A baseline scene has recorded frame-time and memory expectations.
- Performance regressions have an owner and reproduction path.

### Accessibility

Owner: Shared

- Decide early defaults for HUD scale, text size, contrast, captions, remapping,
  autosave messaging, input repeat behavior, and motion intensity.
- Avoid relying on color alone for critical state.

Acceptance:

- Accessibility requirements are tracked as ship requirements, not polish-only
  nice-to-haves.

### Phase 0 Exit Gate

Phase 0 is complete when:

- this roadmap is accepted as the planning baseline,
- active branch ownership is clear,
- the target invariants are validated,
- clean-room asset policy is visible to both agents,
- no one needs to edit outside their lane to start Phase 1.

## Phase 1: First 20-Minute Cozy LitRPG Vertical Slice

The player should be able to start a new world, choose or inherit a Calling, gather
materials, craft or place something useful, complete a starter quest, see stats
move, and save enough state to believe the world is theirs.

### Foundation Systems

Owner: Codex/Foundation

- Lock starter Calling selection and fallback behavior.
- Award XP from early actions: gather, craft, place, farm, path repair, and
  creature calm or defeat.
- Ship the first starter quest chain as data:
  - `First Flame, First Field`
  - `A Roof Before Rain`
  - `Thread, Twig, and Tin`
  - `Fixing the South Path`
  - `The Rootcellar Below`
- Add clear objective event hooks for inventory gain, item craft, tile place,
  workstation use, crop action, path repair, and encounter resolution.
- Keep character stats classic and readable: STR, DEX, INT, VIT, DEF, LUCK.
- Keep HP, MP, XP, level, Calling, title, and current quest visible through
  runtime handles.
- Implement first-pass recipe discovery and unlock tracking.

Acceptance:

- A new player gets visible progression in the first 5 minutes.
- A first session completes at least one quest without debug-only commands.
- Progression events are deterministic and testable.
- UI can present all first-slice state without reaching into Foundation internals.

### UI

Owner: Claude/UI

- Present a compact in-game HUD with:
  - HP, MP, XP, and level,
  - selected hotbar item,
  - pinned quest objective and reward preview,
  - day/time strip,
  - concise system notifications.
- Show character sheet values:
  - Calling,
  - title,
  - level,
  - STR, DEX, INT, VIT, DEF, LUCK,
  - top current unlocks or buffs.
- Add first-pass inventory, crafting, and system panels if not already shipped.
- Ensure settings can adjust volume and HUD scale.

Acceptance:

- The first slice is playable without reading debug logs.
- Notifications are short, warm, and useful.
- UI panel state survives common interactions: Esc, Back, Close, tab switches,
  and repeated open/close.
- Text does not overlap at supported desktop resolutions.

### Art And Content

Owner: Claude/Art with Human/Design approval; Codex/Foundation supplies specs

- Create or import clean-room starter visuals for:
  - Mosswake Meadow terrain tops,
  - dirt, sand, snow, clay, soil, water, wood, and path families,
  - starter tools,
  - starter resources,
  - first workbench or work stump,
  - campfire or first light source,
  - first home/storage objects,
  - a minimal set of item icons.
- Create or select original SFX for gathering, crafting, placing, UI open/close,
  quest update, level-up, and safe-night ambience.
- Keep all placeholders clearly marked.

Acceptance:

- No copied reference pixels, audio, or asset-pack content ships by accident.
- Art has matching `.meta` files where applicable.
- Import settings preserve pixel clarity and isometric readability.
- All visual assets used in the first slice have an owner and license status.

### World And Biomes

Owner: Codex/Foundation

- Make Mosswake Meadow the starter region with readable resource distribution.
- Add a short route objective, such as a blocked path or repairable crossing.
- Add a first Brindlecap edge or Rootcellar-style destination as a preview of
  deeper exploration.
- Keep resource nodes and tile modifications stable enough for save/load.
- Include at least one world pressure that is cozy but meaningful: rain, night,
  low light, limited inventory, bridge repair, or tool durability.

Acceptance:

- Starter resources support the first quest chain without grind.
- A player can understand where to go from world cues and UI pinning.
- The first route unlock changes access, resources, or safety.

### NPCs And Economy

Owner: Codex/Foundation for data, Claude/UI for presentation

- Add a simple notice board, trader stub, or request source if it supports the
  first slice.
- Avoid full economy simulation in Phase 1.
- Seed item value categories for later: common material, crafted good, meal,
  tool, rare resource, quest item.

Acceptance:

- If any request or trade exists, it uses stable IDs and inventory contracts.
- Economy values do not break early crafting or quest pacing.

### QA And Tooling

Owner: Codex/Foundation

- Add a golden path:
  - launch from menu,
  - create world with seed and Calling,
  - load Foundation scene,
  - gather material,
  - craft item,
  - place object or tile,
  - update quest,
  - gain XP,
  - open UI panels,
  - save,
  - reload,
  - verify retained state.
- Add validation for missing content IDs and broken quest objective references.

Acceptance:

- The vertical-slice golden path passes in editor automation or a documented
  manual checklist.
- Failures show actionable messages.

### Performance

Owner: Codex/Foundation, Claude/UI, Claude/Art

- Target stable desktop play in the starter scene at 60 FPS on the development
  machine.
- Cap high-churn notifications, particles, and floating text.
- Pool repeated effects used by gathering, crafting, placing, and combat.
- Avoid UI rebuilds every frame when event-driven updates are enough.

Acceptance:

- Starter slice has no obvious hitches during gather, craft, place, and quest
  update loops.
- No single first-slice UI or FX system dominates frame time.

### Accessibility

Owner: Shared

- Add or verify:
  - HUD scale control,
  - readable text contrast,
  - remappable movement and action controls,
  - no color-only quest or vitals indicators,
  - captions or visual equivalents for important audio cues,
  - pause behavior that works during menus.

Acceptance:

- A keyboard-only player can complete the first slice.
- Critical quest and survival information remains visible without audio.

### Phase 1 Exit Gate

Phase 1 is complete when:

- the first 20-minute loop is playable end to end,
- the player sees level, stat, skill, recipe, or quest progress quickly,
- at least one home or route improvement has a persistent result,
- first-slice art and audio are clean-room approved or clearly placeholder,
- golden path validation passes,
- both agents can work Phase 2 without changing Phase 1 contracts.

## Phase 2: Persistent Homestead And Crafting Depth

Phase 2 turns the first slice into a repeatable multi-session game loop.

### Foundation Systems

Owner: Codex/Foundation

- Implement robust `FoundationSaveData` covering:
  - world name and seed,
  - difficulty,
  - active Calling,
  - stats,
  - level and XP,
  - skill XP,
  - quest progress,
  - inventory,
  - hotbar,
  - modified cells,
  - placed objects,
  - crops,
  - day/time,
  - weather and season state,
  - region shifts,
  - discovered recipes.
- Add save migration versioning from the start.
- Add atomic save write behavior to reduce corruption risk.
- Add storage containers and storage-aware crafting.
- Build the first meaningful workstation chain: work stump or bench, cooking pot,
  kiln or forge, loom or sewing station.
- Add early farming depth: tilling, watering, crop growth, seed return, and crop
  quality.
- Add durability or repair only if it improves pacing rather than busywork.

Acceptance:

- Save/load round-trips preserve player, world, inventory, crops, quests, and
  placed objects.
- Old saves fail gracefully or migrate predictably.
- Crafting can use local inventory and clearly scoped storage sources.
- Farming has at least one meaningful choice beyond waiting.

### UI

Owner: Claude/UI

- Add usable inventory, storage, crafting, character, quest log, and settings
  screens.
- Add clear save feedback: saving, saved, save failed, autosave disabled, and
  loaded world.
- Add recipe filters and categories.
- Add item tooltips with name, tier, quality, traits, use, and source hints.
- Add crop and workstation interaction prompts.

Acceptance:

- The player can find recipes and understand missing ingredients.
- Save/load feedback is visible and unambiguous.
- Tooltips fit within screen bounds and avoid stat-wall overload.

### Art And Content

Owner: Claude/Art with Codex/Foundation specs

- Expand item icon coverage for starter resources, tools, meals, seeds, and
  crafting outputs.
- Add workstation sprites and interaction states.
- Add crop growth stages for first crop families.
- Add home/storage/building props for the homestead stage.
- Add warm, legible effects for craft completion, harvest, level-up, and saved
  game feedback.

Acceptance:

- Every Phase 2 recipe has a readable icon or approved placeholder.
- Crop stages are distinguishable at gameplay zoom.
- Buildable objects read clearly on all supported height layers.

### World And Biomes

Owner: Codex/Foundation

- Add biome-aware resource tables for starter meadow, forest edge, clay/water,
  and early stone or mine access.
- Add path, bridge, stair, or route upgrades that persist.
- Add simple weather and season hooks to affect crops, comfort, or hazards.
- Add first home comfort score and rest quality effect.

Acceptance:

- Each early biome contributes at least one distinct resource or system.
- Route upgrades create meaningful shortcuts, safety, or resource access.
- Weather and season affect decisions without blocking progress unfairly.

### NPCs And Economy

Owner: Codex/Foundation, Claude/UI

- Add request-board data and a small request rotation.
- Add first vendor or caravan schedule.
- Define trust, favor, price, and stock unlocks.
- Keep economy values conservative until playtest data exists.

Acceptance:

- Requests can be completed using real inventory and crafting outputs.
- Rewards can include recipe, material, favor, coin, pattern, or route hint.
- Vendor stock is deterministic enough for testing.

### QA And Tooling

Owner: Codex/Foundation

- Add save/load tests for each serialized category.
- Add data validation for:
  - missing item definitions,
  - invalid recipe ingredients,
  - quest objective references,
  - crop definitions,
  - workstation unlocks,
  - duplicate IDs.
- Add manual save-corruption recovery checklist.

Acceptance:

- Save validation catches missing or malformed core state.
- Content data errors fail before playtest where possible.

### Performance

Owner: Codex/Foundation, Claude/UI, Claude/Art

- Profile save/load time and file size.
- Cache definition lookups by stable IDs.
- Pool crop and workstation indicators.
- Avoid scanning the full world every frame for crafting, storage, or quest
  objectives.

Acceptance:

- Save and load complete fast enough to feel reliable in the starter region.
- Storage and recipe UI remain responsive with expected Phase 2 item counts.

### Accessibility

Owner: Shared

- Support larger tooltips and panel text.
- Add input options for repeated actions such as watering, planting, crafting,
  splitting stacks, and transferring inventory.
- Make save state and errors visible without relying on sound.

Acceptance:

- Repetitive crafting and farming can be performed without excessive clicking.
- Players can recover from save/load errors with clear messaging.

### Phase 2 Exit Gate

Phase 2 is complete when:

- save/load is trustworthy for normal play,
- the homestead loop supports multiple sessions,
- crafting and farming have meaningful choices,
- requests and trade begin to connect the player to the valley,
- all Phase 2 systems have validation coverage or documented manual checks.

## Phase 3: Biomes, Routes, Ecology, And Creature Handling

Phase 3 makes the world feel larger, stranger, and more responsive.

### Foundation Systems

Owner: Codex/Foundation

- Implement region definitions with:
  - biome identity,
  - resource tables,
  - mob tables,
  - weather and season biases,
  - route requirements,
  - region-shift state,
  - safe-route state.
- Add creature resolution paths:
  - avoid,
  - scare,
  - fight,
  - trap,
  - soothe,
  - feed,
  - relocate,
  - tame,
  - den conversion.
- Add Warding and Creaturecraft progression hooks.
- Add non-lethal encounter outcomes as first-class quest objective types.
- Add route unlocks that modify traversal, safety, and NPC travel.

Acceptance:

- At least two regions play differently without relying only on visual changes.
- Creature handling supports non-combat progression.
- Region shifts persist and affect resource, threat, or travel behavior.

### UI

Owner: Claude/UI

- Add map or region panel showing discovered routes, pins, and safe paths.
- Add creature info cards or codex entries unlocked through observation.
- Add region status indicators: safety, weather, resources, and active shifts.
- Add clear encounter prompts for non-lethal options.

Acceptance:

- Players can understand why a region changed.
- Non-combat options are discoverable without tutorial walls.
- Map and codex panels remain readable with multiple regions.

### Art And Content

Owner: Claude/Art with Codex/Foundation specs

- Add clean-room biome sets for Brindlecap Woods, Duskwick Marsh, Honeyshale
  Cliffs, Kindlestep Badlands, Winterwool Pines, and Glowcap Grotto as needed by
  production order.
- Add biome-specific props, resource nodes, hazards, and ambient FX.
- Add creature sprites, silhouettes, animations, and SFX for the first creature
  families.
- Add route art: bridges, ridge stairs, signposts, lantern paths, boardwalks,
  blocked paths, and repaired states.

Acceptance:

- Each biome reads distinctly at gameplay zoom.
- Creature states are visually readable: calm, alert, hostile, trapped,
  befriended, or converted.
- Route changes have before/after clarity.

### World And Biomes

Owner: Codex/Foundation

- Expand world generation into region bands or connected areas.
- Add resource identity:
  - meadow: herbs, berries, grass, clay,
  - forest: wood, mushrooms, fiber, resin,
  - marsh: reeds, fish, wax, mud,
  - cliffs: stone, ore, crystal,
  - badlands: sand, glass, cactus, warm ore,
  - snow forest: pine, frostsalt, wool, ice,
  - grotto: glowcaps, quartz, relics.
- Add hazards that can be understood and mitigated.

Acceptance:

- Every unlocked region has at least one reason to revisit.
- Hazards have readable counterplay.
- Generated worlds preserve navigability and route goals.

### NPCs And Economy

Owner: Codex/Foundation, Claude/UI

- Add region-linked requests and caravans.
- Add price and stock changes from safe routes, region shifts, and trust.
- Let NPCs ask for ecological outcomes, not only item delivery.

Acceptance:

- Opening a safe route changes at least one economy or NPC behavior.
- Requests introduce biome mechanics without forcing a single solution.

### QA And Tooling

Owner: Codex/Foundation

- Add seed matrix validation for each active biome.
- Add encounter outcome tests for combat and non-combat resolution paths.
- Add region-shift persistence tests.
- Add route unlock and blocked-route validation.

Acceptance:

- Multiple seeds generate valid starter and expansion routes.
- Region shifts survive save/load.
- Creature outcomes produce expected XP, quest, and loot changes.

### Performance

Owner: Codex/Foundation, Claude/UI, Claude/Art

- Add world-generation profiling.
- Use chunk or region activation rules for far-away objects, effects, and mobs.
- Add AI and encounter update budgets.
- Keep pathfinding and world queries bounded.

Acceptance:

- Region expansion does not make starter performance regress sharply.
- Inactive regions do not run full simulation every frame.

### Accessibility

Owner: Shared

- Provide map pins, route labels, and high-contrast traversal cues.
- Add options for reducing weather opacity, flashing, or intense ambient effects.
- Make creature state readable through iconography and motion, not only color.

Acceptance:

- Region navigation remains understandable for low-vision and color-blind users.
- Weather and biome FX do not block core gameplay readability.

### Phase 3 Exit Gate

Phase 3 is complete when:

- multiple biomes are playable and mechanically distinct,
- creature handling has at least three meaningful non-lethal outcomes,
- routes and region shifts persist,
- map and codex UI explain the world without long tutorials,
- performance remains stable across active regions.

## Phase 4: NPCs, Economy, Settlement Life, And Civic Growth

Phase 4 turns the homestead into a community.

### Foundation Systems

Owner: Codex/Foundation

- Implement NPC definitions with:
  - identity,
  - role,
  - schedule,
  - trust level,
  - request preferences,
  - shop or service hooks,
  - favorite meals or items,
  - region interests.
- Add settlement services:
  - notice board,
  - guest bed,
  - well,
  - market rug,
  - shared kitchen,
  - watch post,
  - shrine or civic hearth.
- Add civic milestones that unlock visitors, services, festivals, trade, and
  settlement-wide buffs.
- Add economy categories:
  - material orders,
  - crafted goods,
  - meals,
  - contracts,
  - barter,
  - favor,
  - special stock.
- Keep prices deterministic enough for testing and adjustable by data.

Acceptance:

- At least three NPCs can visit or settle with distinct roles.
- Civic upgrades create mechanical changes, not only decoration.
- Economy rewards support crafting, farming, routes, and exploration.

### UI

Owner: Claude/UI

- Add dialogue/request presentation that stays concise.
- Add settlement panel with services, visitors, trust, and civic milestones.
- Add vendor UI and request-board UI.
- Add calendar or event strip if festivals or schedules need it.
- Add clear turn-in, partial progress, and reward-preview states.

Acceptance:

- Players can understand who needs what, why it matters, and what they will get.
- Vendor and request UI works with keyboard and mouse.
- NPC UI does not overwhelm the HUD.

### Art And Content

Owner: Claude/Art with Human/Design approval

- Add first NPC portraits or sprites in original style.
- Add civic building and settlement props.
- Add market, guest, festival, and route-travel visuals.
- Add UI icons for trust, favor, service unlocks, and request types.
- Add NPC voicelets or nonverbal SFX only if clean-room and non-intrusive.

Acceptance:

- NPCs are visually distinct and readable at game scale.
- Civic buildings have clear states: locked, buildable, under construction,
  active, upgraded.
- Art supports original world identity rather than reference imitation.

### World And Biomes

Owner: Codex/Foundation

- Let settlement upgrades affect local safety, crop growth, trade, routes, or
  comfort.
- Add NPC travel lines that use safe routes where available.
- Add local events such as visitor arrival, storm prep, market day, and harvest
  help.

Acceptance:

- Settlement growth changes world behavior.
- NPC travel respects route and safety state.
- Events reward preparation without punishing casual pacing too harshly.

### NPCs And Economy

Owner: Codex/Foundation

- Add trust tiers and request chains.
- Add basic supply and demand rules tied to season, biome unlocks, and civic
  buildings.
- Add helper abilities from bonds, such as watering, hauling, route scouting,
  crafting discounts, shop restock, or ward checks.

Acceptance:

- NPC trust unlocks at least one real helper behavior.
- Economy progression avoids runaway wealth from a single item.
- Requests can be fulfilled through multiple player specialties where possible.

### QA And Tooling

Owner: Codex/Foundation, Claude/UI

- Add tests for NPC schedule state, request generation, request completion,
  trust rewards, vendor stock, and civic unlocks.
- Add UI validation for missing NPC names, icons, reward strings, and service
  definitions.

Acceptance:

- NPC and economy data can be validated before playtest.
- Request boards do not generate impossible requests.

### Performance

Owner: Codex/Foundation

- Add NPC AI level-of-detail rules.
- Limit schedule updates to meaningful ticks rather than per-frame checks.
- Use bounded search for nearby service points, routes, and storage.

Acceptance:

- Settlement growth does not cause AI or UI update spikes.
- NPCs degrade gracefully when offscreen or far away.

### Accessibility

Owner: Shared

- Support adjustable dialogue text size.
- Include captions or text for important NPC barks and event cues.
- Allow slower dialogue timing or manual advance.
- Avoid requiring fast reaction time for civic or social events.

Acceptance:

- NPC and economy loops remain playable with larger text and reduced motion.
- Event information is available in text form.

### Phase 4 Exit Gate

Phase 4 is complete when:

- the settlement feels alive across multiple days,
- NPCs and economy create reasons to craft, farm, explore, and build,
- civic upgrades change systems,
- requests and vendors are testable and balanced enough for alpha content,
- UI explains social and economy state clearly.

## Phase 5: Long-Arc LitRPG Depth And Advanced Content

Phase 5 adds the progression fantasy depth that supports longer play.

### Foundation Systems

Owner: Codex/Foundation

- Add Calling tiers: Novice, Adept, Artisan, Luminary, Mythwarm.
- Add branch choices for each Calling:
  - Hearthwarden: Cook, Caretaker, Festival Host,
  - Greenhand: Cropkeeper, Beastfriend, Orchard Sage,
  - Stonewright: Mason, Roadmaker, Hall Builder,
  - Threadsmith: Toolwright, Weaver, Relic Tinker,
  - Pathlighter: Scout, Cartographer, Ruin Guide,
  - Bramblebound: Herbalist, Denkeeper, Wildspeaker,
  - Lanternblade: Patroller, Shieldhand, Gloombreaker.
- Add deeper skill trees using node types:
  - Ease,
  - Yield,
  - Insight,
  - Expression,
  - Utility,
  - Harmony.
- Add masterwork items with names, traits, history, and evolving perks.
- Add dungeons and encounter spaces that support combat and non-combat success.
- Add region transformations from major civic, ecological, and story choices.
- Add Act 2 and Act 3 story systems without locking future prose too early.

Acceptance:

- Calling branches provide meaningful choices without blocking general skills.
- Skill trees reward playstyle diversity.
- Masterworks and region transformations persist and are inspectable.
- Dungeons have cozy-adjacent identity, not only combat rooms.

### UI

Owner: Claude/UI

- Add skill tree screens with readable node categories.
- Add Calling branch selection and respec or preview flow where approved.
- Add masterwork item history and trait inspection.
- Add codex pages for discovered biomes, creatures, recipes, relics, and region
  shifts.
- Add quest log grouping by Hearth, Craft, Field, Path, Creature, Neighbor, Lore,
  and Civic quests.

Acceptance:

- Players can plan progression without needing external notes.
- Skill and branch UI remains readable with many unlocks.
- Codex information is unlocked through play and avoids spoilers by default.

### Art And Content

Owner: Claude/Art with Human/Design approval

- Add advanced tools, station upgrades, branch-themed icons, dungeon props,
  region transformation visuals, and masterwork visual states.
- Add enemy and creature families across low, mid, high, elite, and boss tiers.
- Add music layers or ambience changes for seasons, regions, safe routes, and
  civic milestones.

Acceptance:

- Advanced content still reads as original LIT-ISO, not a collage of references.
- Boss and elite designs support readable mechanics.
- Music and ambience changes support state without becoming noisy.

### World And Biomes

Owner: Codex/Foundation

- Add deeper biome loops:
  - seasonal resource changes,
  - rare node schedules,
  - ecological consequences,
  - alternate route solutions,
  - den conversion,
  - region safety levels.
- Add caves, mines, ruins, rootcellars, and other encounter spaces.
- Add long-term projects such as landmarks, greenhouses, civic halls, safe
  routes, and patrol networks.

Acceptance:

- Long-term projects have visible intermediate states.
- Region transformation changes play, visuals, or economy.
- Late-game routes and dungeons do not invalidate cozy base-building.

### NPCs And Economy

Owner: Codex/Foundation, Claude/UI

- Add multi-step NPC arcs.
- Add festivals and seasonal market events.
- Add advanced shop stock from trust, safe routes, rare resources, and civic
  buildings.
- Add crafting commissions and special orders.
- Add economy sinks that feel constructive: civic upgrades, festival prep,
  route maintenance, masterwork experiments, and settlement services.

Acceptance:

- Economy remains useful beyond early recipes.
- NPC arcs unlock gameplay or world changes, not only dialogue.
- Festivals and events are optional but rewarding.

### QA And Tooling

Owner: Codex/Foundation, Shared

- Add balance simulation helpers for XP, recipes, crop growth, trade values, and
  request generation.
- Add progression integrity tests for branch unlocks, respec, masterwork traits,
  and region transformations.
- Add content coverage reports for missing icons, sprites, audio, recipes,
  quests, mobs, and biome resources.

Acceptance:

- Alpha content coverage is measurable.
- Progression cannot soft-lock from missing branch, recipe, or quest data.
- Balance tools catch obvious runaway values before playtest.

### Performance

Owner: Codex/Foundation, Claude/UI, Claude/Art

- Add profiling scenarios for:
  - dense settlement,
  - active market,
  - rain or snow,
  - many crops,
  - dungeon encounter,
  - large inventory,
  - map/codex screens.
- Optimize serialization, world queries, pathfinding, AI updates, and UI
  allocation hotspots.
- Add memory budget review for sprites, audio, generated assets, and Resources.

Acceptance:

- Dense content scenarios remain within agreed frame-time and memory budgets.
- UI and gameplay systems avoid avoidable garbage allocation spikes.

### Accessibility

Owner: Shared

- Add options for:
  - reduced flashing,
  - reduced camera shake,
  - simplified combat timing,
  - larger interact prompts,
  - high-contrast cursor and targeting,
  - longer notification duration,
  - full caption coverage for critical cues.

Acceptance:

- Advanced content remains playable with reduced motion and larger text.
- Combat and timed interactions offer accessibility assists where practical.

### Phase 5 Exit Gate

Phase 5 is complete when:

- long-form progression supports alpha playtest,
- Callings and skills are meaningful but not restrictive,
- region transformation and masterwork systems are functional,
- advanced content has validation and coverage reports,
- accessibility options cover core and advanced loops.

## Phase 6: Beta, Release Candidate, And 1.0 Readiness

Phase 6 turns a feature-complete game into a shippable game.

### Foundation Systems

Owner: Codex/Foundation

- Freeze save format except for documented migrations.
- Lock main progression economy.
- Lock quest objective contracts.
- Remove or quarantine debug-only content.
- Add crash-safe, corruption-resistant save behavior.
- Add final validation for every scene, definition, recipe, quest, mob, biome,
  and route.

Acceptance:

- No known save corruption path remains unhandled.
- All required systems have migration or compatibility tests.
- Debug-only code and content cannot appear in normal release flow.

### UI

Owner: Claude/UI

- Complete menu, HUD, inventory, crafting, character, quest, map, codex, vendor,
  settings, save/load, and accessibility screens.
- Run responsive layout checks across supported resolutions.
- Add localization-ready string organization if localization is in scope.
- Confirm all UI states have empty, loading, error, success, hover, focus, and
  selected states where relevant.

Acceptance:

- No panel traps the player without Back, Close, or Esc.
- UI does not overlap or clip essential text in supported resolutions.
- Keyboard navigation works for core menus.

### Art And Content

Owner: Claude/Art, Human/Design

- Replace placeholders or explicitly mark them as acceptable temporary content.
- Verify licenses for every shipped asset.
- Ensure all binary assets are tracked appropriately, including Git LFS where
  required.
- Finalize first-pass audio mix and music transitions.
- Check visual readability at gameplay zoom and target resolutions.

Acceptance:

- Asset provenance is known.
- No reference-derived or unapproved art/audio ships.
- Art and audio support gameplay readability.

### World And Biomes

Owner: Codex/Foundation, Claude/Art

- Lock final 1.0 region list.
- Verify traversal, route unlocks, hazard readability, resource availability,
  and region shifts across seeds.
- Remove unreachable or dead-end procedural states.

Acceptance:

- All required regions are reachable and completable.
- Critical resources cannot become permanently unavailable.
- Procedural seeds used for test coverage remain valid.

### NPCs And Economy

Owner: Codex/Foundation, Claude/UI

- Balance prices, request rewards, vendor stock, festival rewards, trust gains,
  and civic upgrade costs.
- Remove impossible or duplicate request combinations.
- Add fallback behavior when an NPC, shop, item, or region is unavailable.

Acceptance:

- Economy supports multiple playstyles without one dominant exploit.
- Request generation cannot soft-lock progression.
- NPC services remain understandable and reliable.

### QA And Tooling

Owner: Shared

- Run gate suites:
  - smoke tests,
  - golden path,
  - save/load migration,
  - seed matrix,
  - content validation,
  - UI layout pass,
  - accessibility checklist,
  - performance scenes,
  - release build launch.
- Add issue triage categories:
  - blocker,
  - save-risk,
  - progression-risk,
  - accessibility-risk,
  - performance-risk,
  - content-risk,
  - polish.

Acceptance:

- Blockers and save-risk issues are closed before release candidate.
- Known issues are documented and acceptable for the release target.
- Release build can be launched and played through core loops.

### Performance

Owner: Shared

- Set release budgets:
  - stable target FPS,
  - maximum acceptable hitch during save/load,
  - maximum memory target,
  - maximum scene-load time,
  - maximum UI panel open hitch,
  - maximum active audio/FX budget.
- Profile release builds, not only editor play mode.
- Optimize only measured bottlenecks.

Acceptance:

- Release build meets agreed budgets in target scenarios.
- Remaining performance risks are documented with mitigation plans.

### Accessibility

Owner: Shared

- Complete final accessibility checklist:
  - readable text scaling,
  - HUD scaling,
  - remappable controls,
  - captions or visual equivalents,
  - color-blind-safe critical states,
  - reduced motion,
  - pause availability,
  - save feedback,
  - input repeat options,
  - keyboard navigation for core UI.

Acceptance:

- A player can complete the primary loop without audio.
- A player can complete the primary loop with keyboard-only input.
- Critical information is not color-only.

### Phase 6 Exit Gate

Phase 6 is complete when:

- beta gates pass,
- release-candidate gates pass,
- save, progression, and content blockers are closed,
- asset provenance is clean,
- accessibility and performance requirements are met or explicitly signed off,
- 1.0 build is ready for distribution.

## Phase 7: Post-Release And Live Backlog

Phase 7 keeps future improvements disciplined after 1.0.

### Candidate Updates

Owner: Shared

- New biomes and region stories.
- Additional Calling branches.
- More NPC arcs and festivals.
- Advanced cooking, farming, creaturecraft, and trade chains.
- Cosmetic building sets.
- New accessibility options from player feedback.
- Performance improvements from real play data.
- Mod or content-pack support only after core save/versioning policy is mature.

Acceptance:

- Updates do not break existing saves without migration.
- New content respects clean-room rules.
- Roadmap additions have owners, scope, and validation gates.

## Release Gate Definitions

### Prototype Gate

Required before broad internal iteration:

- Foundation scene loads reliably.
- Grid, sorting, height, and movement invariants validate.
- Player can move, gather, craft, place, and see inventory.
- HUD shows basic vitals and selected tool.
- At least one quest or objective can update.
- All assets are original, licensed, or marked placeholder.

### Vertical Slice Gate

Required before external or owner-facing slice review:

- New game to first quest completion is playable.
- First 20 minutes include visible LitRPG progress.
- Save/load works for slice state or limitations are explicit.
- UI explains the core loop without debug text.
- Starter art/audio direction is representative or clearly scoped as placeholder.
- Golden path validation passes.

### Alpha Gate

Required before feature-complete balancing:

- Save/load covers all active systems.
- Core loops exist: gather, craft, build, farm, explore, encounter, trade or
  request, rest, progress.
- Multiple regions and multiple NPC/request systems are playable.
- Callings, skills, quests, and item quality have functional depth.
- Content validation catches missing definitions and impossible objectives.
- Performance is acceptable in dense test scenes.

### Beta Gate

Required before release polish:

- Feature set is locked except bug fixes and approved polish.
- Progression can be played across the intended 1.0 arc.
- Economy, request, and resource loops are balanced enough for broad testing.
- UI is complete enough for all core systems.
- Accessibility options cover core requirements.
- Known blockers, save-risk issues, and progression soft-locks are closed.

### Release Candidate Gate

Required before 1.0:

- Release build launches and completes the core loop.
- Save migration and corruption recovery have been tested.
- Asset provenance is reviewed.
- No unapproved reference-derived art, audio, names, or text remain.
- Performance budgets pass in release builds.
- Accessibility checklist is complete or explicitly signed off.
- Known issues are documented.

## Cross-Cutting Acceptance Criteria

These criteria apply to every phase.

### Clean-Room Criteria

- No copied pixels, audio, prose, quest text, class text, faction identity, or
  named content from any reference.
- References may inform system shape, expected scope, and usability lessons only.
- Every shipped asset has a provenance answer: authored, generated for LIT-ISO,
  purchased with approved license, or placeholder not for release.
- Generated assets must be reviewed for originality and consistency before use.
- Naming should come from the LIT-ISO bible or new original approvals.

### Foundation Criteria

- Public runtime contracts are stable or versioned.
- World, inventory, progression, save, and quest systems use stable IDs.
- Procedural generation is deterministic from seed where needed for testing.
- Save/load covers every mutable player-facing system before that system becomes
  release content.
- Validators protect grid, sorting, height, movement, and data integrity.

### UI Criteria

- UI reads gameplay state through contracts or adapters.
- HUD remains compact and does not become a stat wall.
- Detailed data belongs in panels, character sheet, codex, map, or quest log.
- Every modal or panel has a clear close path.
- Text, icons, and controls fit at supported resolutions.

### Content Criteria

- New systems ship with enough content to prove the loop.
- Content IDs are machine-readable and stable.
- Placeholder content is labeled and tracked.
- Each biome, NPC, creature, and quest type has at least one mechanical purpose.
- Rewards should include frequent small wins and occasional larger milestones.

### QA Criteria

- Every phase has a golden path or manual checklist.
- Automated validation should catch broken IDs, missing definitions, invalid
  references, and invariant drift where practical.
- Save/load tests cover normal play and failure cases.
- Bugs are triaged by player impact, especially save risk and progression risk.

### Performance Criteria

- Profile representative release builds, not only editor play mode.
- Prefer event-driven UI and progression updates.
- Avoid unbounded world scans and per-frame definition lookups.
- Pool repeated visual and audio effects.
- Track dense settlement, many crops, active market, large inventory, region
  streaming, and dungeon scenarios.

### Accessibility Criteria

- Critical information is visible without audio.
- Critical information is not color-only.
- Core play supports keyboard-only control.
- HUD and text scaling are available.
- Reduced motion and readable contrast are treated as release requirements.
- Repetitive actions should have ergonomic input support.

## Open Decisions To Resolve

These decisions need explicit owner or human signoff before they become production
commitments:

| Decision | Proposed Owner | Why It Matters |
|---|---|---|
| Exact Phase 1 starter quest order | Human/Design, Codex/Foundation | Controls first-session pacing |
| Calling selection timing for new worlds | Codex/Foundation, Claude/UI | Affects menu flow, save data, and onboarding |
| First shippable art source for terrain tops | Human/Design, Claude/Art | Determines clean-room asset path |
| Save-file migration policy | Codex/Foundation | Prevents future save breakage |
| First NPC roster | Human/Design, Codex/Foundation, Claude/Art | Locks writing, art, economy, and UI needs |
| Economy unit: coin, favor, barter, or hybrid | Human/Design, Codex/Foundation | Shapes rewards and vendor balance |
| Supported input devices at 1.0 | Shared | Defines UI and accessibility scope |
| Release platform and minimum spec | Shared | Defines performance and build gates |

## Near-Term Recommended Work

Recommended next work, assuming current branch reviews and save/load work continue:

1. Finish and validate Foundation save/load core.
2. Lock starter Calling and quest objective contracts.
3. Add Phase 1 golden-path validation around gather, craft, place, XP, quest, and
   save/load.
4. Approve clean-room starter terrain and resource icon path.
5. Ensure UI panels bind only to stable Foundation handles.
6. Define first request-board and trader data shapes without full economy scope.
7. Establish performance and accessibility baseline checklists for the starter
   scene.
