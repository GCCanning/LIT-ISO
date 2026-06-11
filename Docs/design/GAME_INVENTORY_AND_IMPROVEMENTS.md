# Game Inventory & Improvement Plan

> Audit date: 2026-06-10. Source of truth: `Assets/Scripts/IsoCoreFoundation/Core/FoundationContent.cs`
> (code-built content set; the baker mirrors it to assets) plus the ~97 Foundation system scripts.
> Legacy `Assets/Scripts` outside `IsoCoreFoundation/` (~151 scripts) is being retired and is noted
> only where it still fronts the Foundation (menu/HUD adapters). Analysis only — no code changed.

---

## 1. Content Inventory

### Headline counts

| Category | Count | Category | Count |
|---|---|---|---|
| Blocks | 29 (7 groups) | Skills | 14 |
| Items (total) | 42 | Callings | 7 |
| — Tools | 13 | Classes (hidden trial) | 8 |
| — Foods | 4 | Professions | 8 |
| Recipes | 29 | Abilities | 6 |
| Placeables | 12 | Affinities | 7 |
| Crafting stations | 4 (+Hand) | Titles | 6 |
| Resource nodes | 11 | XP channels | 8 |
| Mobs | 3 | Evidence event types | 18 |
| Crops | 2 | Quests | 5 |
| Biomes | 5 | Dungeons | 1 family × 6 tiers |
| Interiors | 3 | World events / board / expeditions | 4 / 2 / 1 |

### Blocks (29, in 7 groups)

| Group | Blocks | Notes |
|---|---|---|
| grass_blocks | grass_1/2/3 | walkable |
| sand_blocks | sand_1/2 | beach only; no vegetation by rulebook |
| snow_blocks | snow_1/2 | **unused** — "snow" biome renders as taiga on grass until snow art exists |
| badlands_blocks | badlands_1/2 | used by the "desert" biome |
| forest_blocks / canopy_blocks | forest_floor; canopy_1/2/3 | canopy cells are the vegetation |
| water_blocks | water, water_deep ×3, water_swell ×2 | Water collision |
| misc / built | dirt, soil, stone_block (Solid), stone_path, wood_floor, dungeon_floor_1–5 | stone_block doubles as dungeon wall |

### Items (42)

| Sub-category | Items |
|---|---|
| Resources (8) | wood, stone, fiber, slime_goo*, hide*, wheat, copper_ore, copper_bar |
| Foods (4) | apple (15), carrot (12), roasted_apple (24), camp_stew (40) — `foodRestore` values **never consumed by any system** |
| Seeds (2) | carrot_seeds, wheat_seeds |
| Block items (3) | stone_block_item, stone_path_item, wood_floor_item |
| Tools (13) | axe/pickaxe/shovel/sword × wood/stone/copper (tiers 1–3) + hoe. Durability on all |
| Placeable items (12) | workbench, chest, lantern, furnace, campfire, fireplace, tavern_door, tavern_plot, tavern_building, library_plot, library_building, rootcellar_portal |

\* **No source**: hide and slime_goo only drop from mob defeat, which cannot happen (see §2 Mobs/Combat). They also have **no recipe uses** — double dead-end.

### Tools — tier × type matrix

| | Axe | Pickaxe | Shovel | Sword | Hoe |
|---|---|---|---|---|---|
| T1 wood | ✔ | ✔ | ✔ | ✔ | ✔ (only tier) |
| T2 stone | ✔ | ✔ | ✔ | ✔ | — |
| T3 copper | ✔ | ✔ | ✔ | ✔ | — |
| **Has a function** | yes (chop) | yes (mine; mandatory on ore) | **no — no dig mechanic** | **no — no combat input** | yes (till) |

### Recipes (29, by station)

| Station | # | Recipes |
|---|---|---|
| Hand | 5 | workbench, campfire, wood_axe, wood_shovel, wood_sword, tavern_door (6 incl. door) |
| Workbench | 17 | fireplace, tavern_plot/building, library_plot, rootcellar_portal, wood_pickaxe, stone tools ×4, copper tools ×4, stone_path, wood_floor, chest, lantern, stone_block, furnace |
| Furnace | 1 | smelt_copper (2 ore → 1 bar) |
| CookingPot (campfire/fireplace) | 2 | roasted_apple, camp_stew |

### Placeables (12)

| Placeable | Kind | Notes |
|---|---|---|
| workbench, furnace | CraftingStation | |
| chest | Container | StorageSystem-backed, contents saved |
| lantern | Decoration | emits light r2.2 |
| campfire | Camp T1 + CookingPot | ward r5.5, recovery ×1.25, light r3.0 |
| fireplace | Camp T2 + CookingPot | ward r7.0, recovery ×1.55 |
| tavern_door / tavern_building | Entrance → tavern_common_room | building via 3×3 plot construction (24 wood/10 stone/8 fiber) |
| tavern_plot, library_plot | Construction | plot → building conversion with resource cost |
| library_building | Entrance → library_archive | |
| rootcellar_portal | Entrance → rootcellar_starter dungeon | |

### Resource nodes (11)

tree, pine, stump, log (wood; axe-preferred) · rock, shore_stone (stone; pickaxe-preferred) · copper_vein (pickaxe **mandatory**) · bush (fiber + 50% apple) · flower, flower_tulip, tuft (fiber). Hits 2–9; only shore_stone blocks movement.

### Mobs (3)

| Mob | Behavior | Threat tier | Contact dmg | Drops (unobtainable) |
|---|---|---|---|---|
| deer | Skittish | 0 | 0 | hide 1–2 |
| slime | Passive (aggressive in dungeons) | 1 | 4 | slime_goo 1–2 |
| fox | Skittish (aggressive in dungeons T4+) | 2 | 6 | hide 1 |

No `Hostile` overworld mob exists despite the enum, Goblin-Bane title, and goblin_raid_chain world event.

### Crops (2) & Farming

carrot_crop (3 stages × 8 s) and wheat_crop (4 stages × 7 s); both return produce + 50% seed-back. Hoe tills → soil → plant → E to harvest.

### Biomes (5)

| Biome | Surface | Nodes | Mobs | Distinct? |
|---|---|---|---|---|
| meadow | grass | tree, rock, bush, flowers, tufts, logs | deer, slime | spawn biome; no ore |
| forest | forest_floor + canopy | dense tree, bush, copper_vein 1% | deer, fox, slime | best wood/ore |
| desert (badlands) | badlands | rock, copper_vein 1.5% | slime | sparse |
| beach | sand | rock 2% only | fox, slime | nearly empty |
| snow (taiga) | grass | tree+pine, rock, copper_vein | deer, fox | pine variety only |

### LitRPG progression data

- **Callings (7):** Hearthwarden, Greenhand, Stonewright, Threadsmith, Pathlighter, Bramblebound, Lanternblade — stat bonuses + 3 starter skills + 3 branch ids each (branches are data-only).
- **Skills (14):** foraging, woodcraft, mining, farming, cooking, crafting, building, exploration, creaturecraft, combat, warding, spellcraft, trade, lorekeeping. XP flows from hooks; trade/lorekeeping have almost no granting sources.
- **Abilities (6):** Steady Strike, Guard Step (stamina); Mana Bolt, Ember Spark, Root Snare, Stone Skin (mana). Costs/cooldowns/power/affinity-scaling all implemented — **but `TryUseAbility` is only invoked by the editor validator; no runtime input binding, and `scaledPower` affects nothing in the world.**
- **Affinities (7):** ember, tide, root, stone, gale, glimmer, hearth (awaken at 10). Tide is granted only by `mob_calmed` (untriggerable) → effectively unreachable; glimmer only via dungeon result.
- **Titles (6):** First Night Survivor, Village Shield, Campfire Captain, Trail Cook, Goblin-Bane, Returned For Them — all mechanical with hidden-class keys; Goblin-Bane has no goblin to feed it.
- **XP channels (8):** character, class, profession, skill_mastery, adventurer_rank, guild_rank, mosswake_reputation, rootcellar_clearance. guild_rank has no granting source.
- **Evidence events (18):** harvest ×3, craft ×2, place_path, till_soil, crop_harvest, cook_fire_meal, rest_at_camp, mob_defeated*, mob_calmed*, ability-use ×6. (*untriggerable at runtime.)
- **Classes (8) / Professions (8):** trial-weighted hidden classes (CommonPlus→Epic) and professions; data drives the seven-day-trial grade forecast.
- **Quests (5):** Acts 1–2. Quests 1–4 fully hooked via `FoundationProgressionHooks`. **Quest 5 "The Rootcellar Below" has zero objective hooks (enter_cellar / recover_relic / return_home never advance) — uncompletable**, and "Memory Amber" exists in no item table.
- **Dungeons:** one family (Mosswake Rootcellar), tiers 1–6 by distance from spawn; DungeonResults ×1, Expedition templates ×1, GuildBoard entries ×2, WorldEvents ×4 — **events/board/expeditions are data-only; no runtime system reads them.**
- **Interiors (3):** tavern_common_room (authored `TavernHearthSnug` layout w/ props), library_archive (generic floor + library dressing), rootcellar dungeon instances.

---

## 2. Systems Map

**World generation** — `IsoTerrainSampler` is the single deterministic sampler for both render and collision (the key legacy fix); chunked streaming (12-cell chunks, 7×7 ring), 5 biomes by temp/moisture noise, spawn clearing, water bands, node scatter per biome. Solid and shippable. Biggest gap: biomes differ mostly by palette — beach/desert have almost nothing to do in them; no rivers/landmarks/POIs to aim a 45-minute walk at.

**Save/load** — `FoundationSaveData` v9 JSON: player, inventory/hotbar, full progression (trial, titles, affinities, channels), modified cells, placeables, chest contents, crops, active instance/dungeon + history, explored map, mobs, day-night time, region shifts. Manual save via pause menu; menu-driven load with metadata. Gap: no autosave and a single implicit slot per world; death (if added) has nothing to lean on.

**Day/night** — 10-minute deterministic cycle with sun/moon arc, ambient tint, moonlight strength; drives camp fatigue and weather. Works. Gap: nothing else changes at night — no night spawns, so darkness is cosmetic pressure only.

**Weather** — `FoundationWeatherVisuals`: Clear/Mist/Drizzle/Snow particle + tint moods from biome climate, time, and seed. Explicitly visual-only. Gap: zero gameplay coupling (no crop boost, no mood-driven events — `resource_bloom` world event exists as data and would be the natural hook).

**Farming** — till/plant/grow/harvest loop complete with events feeding progression and tutorial; crops survive save/load. State: done for slice scale. Gap: only 2 crops, no watering/seasons, and harvested food has no consumption mechanic, so farming's payoff dead-ends.

**Storage** — `StorageSystem` with per-cell containers, chest UI, contents in saves, QoL favorites/loadouts layered on. State: healthy. Gap: single chest type, no sorting "deposit all", no station-pulls-from-chests.

**Dungeons** — strongest system: seeded room-and-corridor generator (48–96 cells, 8–18 rooms, loops, arena, decorations, exit portal), tier 1–6 portals colored by distance, aggressive mob placements scaling with tier, dungeon history + clearance XP channel, full save of in-dungeon state. Gap: with no way to damage mobs, every dungeon is a walk-to-the-exit; "complete" is a right-click at the exit with no loot moment (XP/title result only); mob roster inside is slime-only below T4.

**Progression (trial/evidence/titles/affinities)** — the LitRPG spine is the most complete system in the game: evidence → category weights → grade forecast, XP channels, title thresholds, affinity awakening with effect multipliers, system-message feed with channels, seven-day trial lifecycle, all serialized. Gap: several loops feed it (harvest/craft/farm/camp) but combat/magic/social evidence is unreachable at runtime, which skews every trial toward gatherer classes.

**Abilities** — clean resource/cooldown/affinity-scaling service + read-state API for UI. Gap (critical): not bound to any player input and produces no world effect; 6 finished abilities are invisible to players.

**Mobs/combat** — wildlife wanders, animates, despawns; dungeon mobs aggro and deal contact damage with floating text and SFX. **There is no player attack path: nothing calls `Mob.MarkDefeated`/`MarkCalmed`, swords have no use, and `FoundationPlayerStats.Damage` can reach 0 HP with no death handling anywhere.** Combat is currently "be slowly chewed, walk away."

**Building/placement** — placement grid with footprints, light emitters, camp tiers, plot→building construction with resource costs, interiors with authored tavern layout, entrance/exit instancing. State: good. Gap: no walls/roof pieces — "A Roof Before Rain" quest is floors+lantern+chest; no repair/move/pickup of placed objects.

**Camping/survival** — camp ward suppresses low-tier spawns by tier/radius, night fatigue cuts recovery to 20% away from fire, rest heals, evidence flows. Nice original mechanic. Gap: no hunger/temperature tie-in, and since overworld mobs barely threaten, the ward protects against little.

**Notifications/UX** — system message feed with channel settings, tutorial notifier driven by real events (tolerant ordering), interaction overlay, context menus, floating text, map overlay (mini + large, explored cells saved), HUD modes (Basic/Adventure/Hidden), pause menu with volume + save. State: unusually polished for this stage. Gap: quest 5 and ability UI are the holes; legacy `FoundationHudAdapter`/menu still bridge from Assembly-CSharp.

---

## 3. Top 15 Improvements (45-minute vertical-slice impact order)

| # | What | Why (player-visible impact) | Effort |
|---|---|---|---|
| 1 | **Wire melee combat**: LMB with sword on a mob → damage → mob HP → `MarkDefeated` → drops + evidence | The single biggest hole. Unlocks swords (4 dead items), hide/slime_goo (2 dead items), mob_defeated evidence, Goblin-Bane/Village Shield titles, combat skill XP, and makes dungeons a game instead of a corridor walk. Mob.cs already has hurt/die frames and Defeated plumbing — only the player→mob damage call is missing | **M** |
| 2 | **Make food edible**: context-menu/hotbar "Eat" consuming `foodRestore` as HP (and/or stamina) | 4 food items + 2 cooking recipes + farming's entire payoff currently do nothing. One small consumer function lights up cooking, farming, foraging, and camp stew at once | **S** |
| 3 | **Death + respawn**: at 0 HP, fade out, respawn at last campfire/spawn, drop-nothing-but-lose-time penalty (+ "Returned" system message) | 0 HP currently does nothing, which players will find within minutes of a T1 dungeon. A soft cozy-appropriate penalty also makes camps/food matter | **S** |
| 4 | **Bind abilities to hotkeys (1–6 or Q/E/R) and give them world effects** (Mana Bolt/Ember Spark damage using `scaledPower`, Root Snare slows, Stone Skin damage reduction, Guard Step dash) | 6 fully tuned abilities + the whole affinity-multiplier system are invisible. This is the "LitRPG moment" of the demo and feeds magic evidence → Pyromancer-class trial outcomes | **M** |
| 5 | **Add 1–2 hostile mobs (goblin + cave rat or gloom slime)** with overworld night/forest spawns and dungeon presence | 3 docile mobs is the thinnest table in the game. A goblin pays off the existing Goblin-Bane title, goblin_raid_chain event, and gives the sword something to do; `MobBehavior.Hostile` already exists | **M** |
| 6 | **Dungeon loot moment**: reward chest in the arena/exit room (copper ore, seeds, a unique trinket) instead of XP-only completion | "Complete and exit" with no item payoff is anticlimactic; a chest gives the 45-minute run a visible prize and exercises StorageSystem | **S** |
| 7 | **Hook quest 5 objectives** (enter_cellar / recover_relic / return_home) and add a `memory_amber` item to the dungeon chest | The quest chain dead-ends at its climax — the only quest that points at the dungeon is uncompletable. Hooks 1–4 show exactly the pattern to copy | **S** |
| 8 | **Uses for hide & slime_goo**: 2–3 recipes (bedroll → respawn point, slime lamp, repair paste for tool durability) | Even with #1 done, the drops need sinks; repair paste also softens durability frustration | **S** |
| 9 | **Night danger outside camp ward**: hostile spawn weight at night, suppressed inside ward radius | Makes the campfire/fireplace tier system (already built) and night fatigue mean something; creates the classic first-night arc the trial fiction promises | **M** |
| 10 | **Creature calming interaction**: RMB "Calm" on skittish/passive mobs (cost: stamina or food item; synergy with Root Snare) calling `MarkCalmed` | Unlocks the entire non-violent lane: creaturecraft skill, mob_calmed evidence, tide affinity (currently unreachable), Bramblebound/Greenhand callings, Returned For Them title | **M** |
| 11 | **2 more crops + 2 buff meals** (e.g. potato + pumpkin; "miner's stew" = +mining yield, "scout's loaf" = +move speed) | Doubles farming/cooking variety for trivial data cost; buff meals give cooking a reason beyond raw restore (activeBuffs list already exists in progression) | **S** |
| 12 | **Give the shovel a job or cut it**: dig dirt/sand → soil/path conversion, or chance-based buried cache on beaches | 3 of 13 tools do nothing; a beach treasure-dig also fixes "beach biome is empty" cheaply | **S** |
| 13 | **Trigger one world event at runtime**: resource_bloom after rain (weather already tracks Drizzle) spawning rare nodes briefly | The WorldEvents/GuildBoard tables are 100% dormant data. One event proves the loop and makes weather gameplay-relevant | **M** |
| 14 | **Autosave** on day rollover + entering/leaving instances | Pause-menu-only saving will lose demo progress; DayNightSystem and instance transitions are clean hook points | **S** |
| 15 | **Tier-4 tool tease (iron)**: iron_vein node in badlands/T3+ dungeons, iron bars, one visibly better pickaxe | Copper is reachable in ~15 minutes; a visible next rung keeps the back half of a 45-minute session pulling forward | **M** |

**Suggested slice order:** 2 → 3 → 1 → 6 → 7 (core loop closes), then 4 → 5 → 9 (combat feel + threat), then quick wins 8/11/12/14 as time allows.

---

## Appendix: dead-end content checklist

| Issue | Items |
|---|---|
| No source at runtime | hide, slime_goo (mob drops; mobs undefeatable) |
| No use | hide, slime_goo, all 3 shovels, all 4 swords, all 4 foods (no eat), snow_1/2 blocks |
| Data with no runtime consumer | WorldEvents ×4, GuildBoardEntries ×2, ExpeditionTemplates ×1, Calling branch ids, ability `scaledPower`/`range` |
| Unreachable progression | tide affinity, mob_defeated/mob_calmed/ability evidence (6), guild_rank channel, quest 5 |
| Single-entry tables | dungeon family, dungeon result, expedition, hoe tier |
