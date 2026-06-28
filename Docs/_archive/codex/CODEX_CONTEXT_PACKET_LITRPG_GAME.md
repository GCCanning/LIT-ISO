# LIT-ISO Codex Context Packet

Use this file to brief another Codex AI.

Primary source docs:

- `Docs/IsoCoreFoundation/17_Seven_Day_Trial_Game_Bible.md`
- `Docs/handoff/Visual_Implementation_Integration_Handoff.md`
- `Docs/IsoCoreFoundation/15_LitRPG_System_Bible.md`
- `Docs/IsoCoreFoundation/16_Future_Improvements_Roadmap.md`
- `AGENTS.md`

## One-Sentence Game Pitch

LIT-ISO is an original cozy isometric survival/crafting/building LitRPG where the
player is transmigrated into a System-governed world, survives a seven-day
classless trial, is judged by an obelisk based on actual actions, then grows
through class, profession, guild rank, dungeon diving, exploration, crafting,
homestead building, party play, and world events.

## Core Premise

The player does not choose a class at character creation. They spawn into a
LitRPG world as an unclassed transmigrant. For the first seven in-game days,
every meaningful action is tracked:

- combat,
- gathering,
- crafting,
- building,
- cooking,
- trading,
- mining,
- fishing,
- healing,
- spell use,
- exploration,
- dungeon activity,
- risk-taking,
- camping,
- social choices,
- helping or abandoning others.

At the end of Day 7, the player is summoned to an obelisk. The obelisk presents
class options generated from the player's weighted action profile. The final
grade controls the rarity ceiling and number/quality of class options.

After this, the player has:

- one **Class** for adventuring/combat/survival identity,
- one **Profession** for crafting/economy/settlement identity.

Both progress by doing relevant actions.

## Lore Frame

Working names:

- **The Ledger**: the world's System. It records effort, risk, intent,
  consequence, reputation, and growth.
- **Unwritten**: transmigrated newcomers with no class, profession, rank, or
  recorded history.
- **Proving Week**: the seven-day classless evaluation period.
- **Entries**: recorded actions.
- **Marks**: hidden behavioral tags such as Resolve, Mercy, Precision, Greed,
  Endurance, Craft, Discovery, Leadership, Recklessness, Loyalty.
- **Obelisks**: magical/civic System infrastructure.
- **Hearthclaim**: the player's expandable pocket homestead/base realm.

Tone rule:

> The player is measured, but not doomed. The System records what they practice,
> not what they claim to be.

The System should feel formal, mythic, and alive, but not verbose.

Example notices:

```text
[System Notice]
Foreign soul detected.

[Trial Protocol Initiated]
Duration: 7 days.
Survive. Adapt. Act.
```

Obelisk final ceremony example:

```text
[Seven Days Recorded]
[You Were Not What You Claimed To Be]
[You Were What You Practiced]
```

## Spawn-In Experience

Initial player state:

```text
Name: Unknown
Origin: Unregistered
Status: Transmigrant / Unwritten
Class: Pending
Profession: Pending
Trial Duration: 7 Days
Grade: Unassigned
System Authority: Provisional
```

Opening flow:

1. Player wakes near a damaged waystone, starter obelisk, roadside shrine, or
   wilderness clearing.
2. The Ledger identifies an unregistered soul.
3. Temporary access unlocks: Status, Inventory, Map, Trial Log.
4. Temporary abilities unlock: Inspect, Gather, Basic Strike, Basic Craft,
   Emergency Recall.
5. Player quickly performs real actions: gather food, inspect marker, craft a
   tool, avoid/fight weak mob, find the road.
6. Trial Evidence begins recording.

## Seven-Day Trial

The first seven days are tutorial, survival test, and identity engine.

| Day | Purpose | Example Actions |
|---|---|---|
| 1 | arrival/survival | eat, gather, craft basic tool, find shelter |
| 2 | role discovery | fight/avoid mobs, trade, explore, gather |
| 3 | risk introduction | cave/ruin, stronger mobs, minor quests |
| 4 | specialization pressure | combat, craft, trade, magic, scout routes |
| 5 | travel/resource test | plan trip, camp, manage fatigue/supplies |
| 6 | high-value opportunity | dungeon, escort, rare node, risky contract |
| 7 | final proof | last scoring actions, return/summon to obelisk |

Score categories:

- Combat
- Survival
- Exploration
- Crafting
- Gathering
- Magic
- Social
- Building
- Trade
- Support
- Risk

Evidence should track quality, not raw spam. Cutting 300 trees should not
automatically make a legendary woodcutter. Gathering wood, building shelter,
crafting tools, surviving a storm, and upgrading a base is meaningful
survival/building evidence.

## Grade And Class Assignment

Grade controls class rarity:

| Grade | Meaning | Class Rarity Ceiling |
|---|---|---|
| F | poor survival | Common |
| E | basic survival | Common+ |
| D | competent | Uncommon |
| C | strong focused actions | Rare |
| B | high score and risk | Epic |
| A | exceptional | Legendary |
| S | hidden/high-risk excellence | Mythic |

Class assignment rules:

- Evidence determines candidate pool.
- Grade determines rarity ceiling.
- Titles/hidden achievements can unlock rare branches.
- Coop players are judged individually.
- Player chooses from generated options, not a fixed menu.

Early class candidates:

- Trailblade: melee, travel, scouting, survival.
- Iron Warden: shield use, damage taken, ally protection.
- Hearthbound Acolyte: cooking, healing, camp support.
- Stonehand Delver: mining, dungeon diving, blunt weapons.
- Wildsign Ranger: scouting, bow use, beast tracking.
- Ashvein Pyromancer: fire exposure, spell use, risk-taking.
- Wayfarer: mapping, travel, escape, route planning.
- Oathbearer: quest completion, protection, leadership.
- Rune-Touched: shrine study, magic tools, obelisk interactions.
- Fortune Broker: trade, negotiation, delivery contracts.

## Class And Profession Split

Class is adventuring identity:

- combat,
- magic,
- exploration,
- survival,
- active skills,
- passives,
- class quests,
- class evolutions.

Profession is economic/world identity:

- crafting,
- farming,
- trade,
- production,
- settlement services,
- recipes,
- station upgrades,
- commissions,
- building permits.

Profession examples:

- Blacksmith
- Alchemist
- Cook
- Builder
- Trader
- Farmer
- Miner
- Fisher
- Leatherworker
- Enchanter
- Cartographer
- Sailor

Example combinations:

- Ranger / Alchemist
- Warden / Blacksmith
- Elementalist / Cook
- Duelist / Trader
- Cleric / Builder
- Beast Tamer / Farmer
- Artificer / Enchanter

## Progression Systems

Use separate XP/rank channels:

- Character Level
- Class Level
- Profession Level
- Skill Mastery
- Adventurer Rank
- Guild Rank
- Region Reputation
- Dungeon Clearance Rank

Example readout:

```text
Character Level 8
Class: Trailblade Level 5
Profession: Cook Level 4
Sword Mastery: 6
Foraging: 9
Adventurer Rank: Copper II
Guild Rank: Stone I
Greenwake Reputation: Trusted
```

Do not show all bars on the HUD. Use compact recent-growth feed plus deeper
status screens.

## Stats

Stats must physically affect gameplay.

- STR: melee damage, tool impact, carry load, heavy armor comfort, shove power.
- DEX: movement speed, attack speed, dodge timing, bow use, trap handling.
- VIT: HP, stamina cap, fatigue resistance, poison/bleed recovery.
- END: stamina recovery, sprint duration, mining/chopping endurance.
- DEF: damage reduction, armor scaling, stagger resistance.
- INT: mana pool, spell scaling, crafting discovery, appraisal.
- WIS: perception, tracking, hidden nodes, rare routes, ambush warning.
- CHA: prices, persuasion, recruit quality, party morale.
- LUCK: rare drops, craft crits, event rolls, unusual class offers.

Derived values:

- HP
- MP
- Stamina
- Carry Load
- Move Speed
- Attack Speed
- Tool Speed
- Armor Burden
- Perception
- Craft Quality
- Social Leverage

## Titles

Titles recognize player behavior and unlock options. Only a limited number
should be active to avoid stacking problems.

Examples:

- First Night Survivor
- Village Shield
- Campfire Captain
- Trail Cook
- Goblin-Bane
- Returned For Them
- Storm-Touched
- Overburdened Fool
- Map-Blessed
- Oathkeeper

Titles can affect:

- class candidates,
- NPC dialogue,
- small bonuses,
- guild board access,
- shop discounts,
- affinity nudges,
- building permits.

## Affinities

Affinities are discovered through behavior, exposure, shrines, dungeons,
professions, food, titles, and gear. They affect magic and non-combat systems.

Core affinities:

- Ember: heat, courage, forging, burst damage, cooking fire.
- Tide: healing, fishing, weather, cleansing, movement.
- Root: farming, growth, binding, stamina, homestead expansion.
- Stone: defense, mining, construction, endurance.
- Gale: speed, scouting, ranged combat, sailing.
- Glimmer: light, illusion, enchantment, secrets, luck.
- Umber: shadow, stealth, leatherwork, tracking.
- Aether: obelisks, teleportation, raw System magic.
- Hearth: food, comfort, camp safety, morale, party buffs.

Mixed affinities:

- Stormglass: Gale + Glimmer.
- Ironroot: Stone + Root.
- Ashbloom: Ember + Root.
- Moonwell: Tide + Glimmer.
- Hearthflame: Hearth + Ember.

First implementation should use a smaller subset:

- Ember
- Tide
- Root
- Stone
- Gale
- Glimmer
- Hearth

## Dungeon Diving

Dungeons are expeditions, not instant combat rooms.

Expedition loop:

1. Discover dungeon or select guild board contract.
2. Choose party: solo, NPC, coop player, hired adventurer.
3. Prepare supplies: food, potions, torches, repair kits, camp kit.
4. Travel to dungeon.
5. Camp if journey is long.
6. Enter dungeon.
7. Manage fatigue, light, carry weight, injuries, loot.
8. Extract or retreat.
9. Return to town.
10. Dungeon result screen grants XP, loot, title/affinity progress, reputation.

Party roles:

- Vanguard
- Striker
- Scout
- Binder
- Mender
- Delver
- Arcanist
- Quartermaster
- Caller

Party culture:

- Parties perform a Load Count before delves.
- Loot division can use Need, Use, Ledger Share, Contract Claim, or Party Share.
- Abandoning allies can cause negative reputation/titles.
- Rescuing teams can create allies, recruits, map intel, or rival respect.

Expedition templates:

- Quick Delve
- Deep Delve
- Boss Attempt
- Gathering Run
- Rescue Run

Dungeon result screen should show:

- dungeon name,
- threat rank,
- clear grade,
- depth reached,
- injuries,
- supplies consumed,
- contracts completed,
- loot recovered,
- class/profession/skill XP,
- affinity changes,
- title progress,
- reputation changes,
- world consequences.

## World Events And Guild Board

The guild board is the world pulse, not just static quests.

Board entries:

- confirmed quests,
- rumors,
- sightings,
- bounties,
- dungeon reports,
- trade requests,
- missing party notices,
- escort jobs,
- resource bloom notices,
- raid warnings,
- class/profession training notices.

Events:

- goblin raid,
- dangerous mob sighting,
- migration,
- resource bloom,
- weather crisis,
- dungeon breach,
- merchant caravan,
- rival party event,
- settlement request,
- obelisk anomaly.

Example chain:

1. Goblin tracks near west road.
2. Farm tools stolen.
3. Goblin camp found.
4. Player can scout, sabotage, negotiate, or ignore.
5. Raid happens if unresolved.
6. Outcome affects shops, NPC safety, road danger, titles, and reputation.

## Crafting And Production

Crafting supports cozy play and dungeon survival.

Production pillars:

- mining,
- smelting,
- forging,
- leatherwork,
- tailoring,
- woodworking,
- alchemy,
- enchanting,
- cooking,
- farming,
- fishing.

Crafting should produce:

- weapons,
- armor,
- tools,
- bags,
- camp kits,
- boat parts,
- road tiles,
- building licenses,
- furniture,
- food,
- potions,
- wards,
- enchantment materials,
- repair kits,
- trade goods.

Crafting chain example:

```text
mine copper -> smelt bars -> forge pot/tool -> cook better travel stew
-> stamina/fatigue buff -> deeper dungeon possible -> rare ore
-> better forge/station
```

## Food And Cooking

Cooking is a real LitRPG system, not just hunger recovery.

Food categories:

- travel rations,
- camp meals,
- combat meals,
- craft meals,
- gathering meals,
- magic meals,
- social meals.

Cooking paths:

- Cook profession
- Provisioner profession
- Battle Chef class
- Hearthkeeper class
- Alchemical Cook hybrid

Food affects:

- fatigue,
- stamina,
- warmth,
- morale,
- attack speed,
- resistances,
- craft quality,
- rare node chance,
- mana recovery,
- reputation.

## Building And Homestead

Use a hybrid building system.

Do:

- tile placement for roads, farms, fences, floors, decor, crops, terrain.
- prefab footprints for functional buildings.
- instanced interior maps for enterable buildings.
- building upgrades swap exterior/interior templates.

Do not start with fully freeform isometric wall/roof building. It will be
expensive, hard to sort visually, and risky for pathfinding/split-screen.

Functional building examples:

- 2x2 cottage
- 3x2 smithy
- 3x3 or 4x3 tavern
- 3x3 barn
- 4x4+ guild hall
- dock

Pocket base:

- starts as small Hearthclaim,
- expands with guild rank/town trust/dungeon cores,
- supports farming, crafting stations, storage, NPC rooms, roads, portals,
  decorations, training yard, animal pens.

First building implementation:

1. Place/remove road tile.
2. Place/remove 2x2 cottage prefab.
3. Rotate southeast/southwest.
4. Validate footprint and doorway.
5. Interact with doorway.
6. Teleport to small interior map.
7. Return to doorway.
8. Save/load building instance and interior state.

## Travel, Ocean, And Continents

Distance equals danger and rarity. The farther from safe zones, the better the
loot and the worse the risk.

Travel resources:

- time,
- stamina,
- fatigue,
- hunger,
- thirst,
- temperature,
- light,
- carry weight,
- weather,
- route danger,
- map knowledge.

Ocean later:

- boat durability,
- cargo,
- storms,
- fishing,
- ports,
- island chains,
- new continents,
- sea dungeons,
- trade routes.

Storms should damage boats, spoil supplies, slow travel, force route changes, or
create rare fishing/loot opportunities.

## Visual Direction

Visual target:

- readable 2D isometric,
- cozy settlement/farming/crafting surfaces,
- dangerous but visually compatible wilderness/dungeons,
- strong silhouettes,
- clear tile footprints,
- restrained but magical System UI,
- original clean-room assets only.

Asset families needed first:

- Unwritten player placeholder,
- weak starter mob,
- starter town tiles,
- road/path tiles,
- campfire/bedroll/shelter,
- obelisk/waystone,
- guild board,
- trees/bushes/rocks/herbs,
- first dungeon tiles,
- chest/loot prop,
- 2x2 cottage exterior,
- cottage/tavern interior,
- workbench,
- furnace/anvil placeholder,
- UI icons for stats/classes/professions/titles/affinities/warnings.

## Implementation And Integration

Canonical game is `IsoCore.Foundation`.

Respect invariants:

- Grid `IsometricZAsY`, `cellSize (1,0.5,1)`.
- `transparencySortAxis (0,1,-0.26)`.
- `TilemapRenderer.mode = Individual`.
- Height layers `Height_0..7` = Unity layer `10+height`; sorting layer
  `10+height`.
- Movement is world-query based.
- Foot collider is trigger-only.
- `maxWalkStepHeight=0`.

Owner lane:

- Codex owns `Assets/Scripts/IsoCoreFoundation/**`,
  `Assets/Scenes/IsoCoreFoundation.unity`,
  `Docs/IsoCoreFoundation/**`.
- Claude owns menu/art/integration paths per `AGENTS.md`.

Data-first implementation targets:

- `SystemMessageFeed`
- `SystemMessageDefinition`
- `SystemMessageChannel`
- `TrialScoreState`
- `TrialEvidenceProfile`
- `EvidenceEventDefinition`
- `EvidenceWeightTable`
- `XPChannelDefinition`
- `SkillMasteryDefinition`
- `ClassDefinition`
- `ProfessionDefinition`
- `TitleDefinition`
- `AffinityDefinition`
- `AdventurerRankDefinition`
- `GuildRankDefinition`
- `DungeonDefinition`
- `ExpeditionTemplateDefinition`
- `DungeonResultDefinition`
- `GuildBoardEntryDefinition`
- `WorldEventDefinition`

First loop to prove:

```text
Action -> Evidence -> XP/Title/Affinity Progress -> System Message -> Saved State
```

Then prove:

```text
Spawn as Unwritten
Survive/gather/fight/craft/explore
Trial Evidence records actions
Guild board generates small threat
Prepare and enter first dungeon
Return with dungeon result screen
Day 7 obelisk evaluates player
Choose class/profession
```

## Coop And Split-Screen

Design for multiple players from the start.

Individual state:

- trial evidence,
- class,
- profession,
- stats,
- skill mastery,
- titles,
- affinities,
- inventory/personal bag,
- buffs/debuffs,
- fatigue/hunger,
- obelisk class offers.

Shared state:

- world seed,
- day/time,
- discovered map where appropriate,
- active events,
- guild board,
- dungeon state,
- homestead/base,
- shared storage,
- guild rank,
- town upgrades.

Rule: one combat-focused player must not determine everyone else's class.
Everyone gets individual class offers.

## Validation Needed

Validators should check:

- every class has weights and XP sources,
- every profession has progression actions,
- every grade maps to class rarity ceilings,
- every XP channel has a level curve,
- every title has condition/effect policy,
- every affinity has source and threshold rewards,
- every rank has gate definitions,
- every dungeon has travel requirements,
- every dungeon result references valid rewards,
- every guild-board entry resolves to quest, rumor, event, or shop data,
- every food buff has duration/category/stacking rule,
- every homestead expansion has cost and size delta,
- every coop quest defines ownership policy,
- obelisk cannot generate zero class offers,
- starter seven-day path cannot soft-lock.

## What Another Codex Should Do First

1. Read `AGENTS.md`.
2. Read this packet.
3. Read `Docs/IsoCoreFoundation/17_Seven_Day_Trial_Game_Bible.md` for full
   detail.
4. Inspect existing `Assets/Scripts/IsoCoreFoundation/**` progression/data
   patterns.
5. Implement the data spine before UI polish:
   - System messages,
   - Trial Evidence,
   - XP channels,
   - titles,
   - affinities,
   - first class/profession definitions,
   - first dungeon result data,
   - validators.

The goal is not to build every system immediately. The goal is to make the game
feel like a LitRPG as soon as the player performs ordinary actions.
