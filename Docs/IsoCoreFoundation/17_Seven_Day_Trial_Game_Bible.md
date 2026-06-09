# LIT-ISO - Seven Day Trial Game Bible

> Draft v0.4. Corrected core premise.
> This guide defines what must be included for the transmigration, 7-day scoring,
> obelisk class assignment, class/profession progression, dungeon travel, trading,
> crafting, exploration, building, sailing, ranking, affinities, and world-danger
> systems.

## Corrected Core Premise

The player is transmigrated into a real LitRPG world. They do not start with a
fixed class. For the first seven days, every meaningful action is tracked by the
System: combat, gathering, building, crafting, trading, travel, healing, spell
use, risk-taking, survival decisions, social choices, and dungeon activity.

At the end of the seventh day, all unclassed newcomers are summoned to an
obelisk. The obelisk presents class options generated from a weighted calculation
of the player's actions. The player's final grade determines the rarity ceiling
and quality of available class choices.

After class assignment, the player can hold:

- one **Class**, such as Ranger, Warden, Mage, Duelist, Cleric, or Beast Tamer,
- one **Profession**, such as Blacksmith, Alchemist, Cook, Builder, Trader, or
  Farmer.

Both progress by doing what they are good at. A Ranger advances by scouting,
tracking, bow use, ambushes, and wilderness survival. A Blacksmith advances by
smelting, repairing, forging, improving gear, and fulfilling smithing orders.

The starter zone is relatively safe but low-rarity. As the player travels farther
from the starter zone, enemies become more dangerous, dungeons become deeper,
travel becomes more demanding, and item rarity improves.

## System Frame And World Terms

Working lore frame:

- **The Ledger** is the world's System. It records action, risk, intent,
  consequence, growth, and reputation.
- New transmigrated people are called **Unwritten** because they have no class,
  profession, rank, or recorded history.
- The first seven days are the **Proving Week**.
- Recorded actions are **Entries**.
- Hidden behavioral tags are **Marks**, such as Resolve, Mercy, Precision,
  Greed, Endurance, Craft, Discovery, Leadership, Recklessness, and Loyalty.
- Obelisks are civic, magical, and religious infrastructure, not only UI menus.
- Dungeons are unstable mana/ecology knots that react to delvers, failed
  parties, ignored threats, and world events.
- A pocket homestead is a **Hearthclaim**: a stabilized build space connected to
  the player/guild through obelisk magic, dungeon cores, and guild authority.

The tone rule:

> The player is measured, but not doomed. The System records what they practice,
> not what they claim to be.

These names are provisional content names. The mechanics matter more than the
exact terminology.

## Spawn-In LitRPG Experience

The first minutes must immediately feel like a LitRPG without burying the player
in exposition.

Initial state:

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

Opening sequence:

1. Player wakes near a damaged waystone, starter obelisk, roadside shrine, or
   forest clearing.
2. The Ledger identifies an unregistered soul.
3. Temporary access unlocks: Status, Inventory, Map, Trial Log.
4. Temporary abilities unlock: Inspect, Gather, Basic Strike, Basic Craft,
   Emergency Recall.
5. Player immediately performs real actions: gather food, inspect a marker,
   craft a tool, avoid or fight a weak mob, find the road.
6. The Trial Evidence Log begins recording.

Example System notices:

```text
[System Notice]
Foreign soul detected.

[Trial Protocol Initiated]
Duration: 7 days.
Survive. Adapt. Act.

[Temporary Authority Granted]
Access unlocked: Status, Inventory, Map, Trial Log.
Class assignment will occur at the nearest Obelisk after Trial completion.
```

Opening rules:

- Keep System messages short and skippable.
- Make the first action happen within one minute.
- First actions matter, but cannot permanently trap the player into one class.
- The player should see the System react before they reach town.
- The first town should treat Unwritten arrivals as known but risky events.

## Non-Negotiable Design Pillars

1. **The first seven days determine identity.** The player earns class options
   through action, not through a menu at character creation.
2. **The obelisk is the first major milestone.** It is the class-awakening moment
   and should feel consequential.
3. **Grade controls class rarity.** Better scores unlock rarer, stronger, or more
   specialized class choices.
4. **Class and profession are separate.** Combat identity and economic/crafting
   identity both matter.
5. **Progression is use-based.** Doing class/profession-relevant actions improves
   that path.
6. **Distance equals danger and rarity.** The farther from safe zones, the better
   the loot and the worse the risk.
7. **Dungeon travel is part of dungeon gameplay.** Food, potions, camping gear,
   route planning, fatigue, night attacks, and weather are required systems.
8. **Preparation beats raw stats.** The player should win because they packed,
   scouted, traded, crafted, rested, and chose risks well.

## LitRPG Interface Requirements

The System should expose enough information for players to optimize without
turning the game into spreadsheet work.

Must include:

- character sheet,
- class and profession sheet,
- System message feed,
- provisional 7-day score panel,
- Trial Evidence Log,
- action tendency breakdown,
- grade forecast with uncertainty,
- title screen,
- affinity screen,
- skill/mastery screen,
- obelisk class-choice screen,
- skill XP popups,
- title notifications,
- quest log,
- dungeon preparation checklist,
- dungeon result screen,
- map travel time and danger estimate,
- inventory weight/food/potion/camping status,
- item rarity and affix tooltip,
- trade and shop reputation panels.

System message channels:

| Channel | Use |
|---|---|
| Notice | major system state changes |
| Warning | danger, overload, fatigue, hunger, low light |
| Trial Evidence | first-seven-day scoring changes |
| Level Up | character/class/profession/skill growth |
| Skill Unlock | new active/passive ability |
| Title Acquired | title unlocks and title progress |
| Affinity Resonance | magic/element affinity changes |
| Quest Update | quest state changes |
| Dungeon Alert | dungeon rank, modifier, breach, boss state |
| Party Event | NPC/player party events |
| World Event | raids, sightings, caravans, storms, obelisk anomalies |

System feed rules:

- High-priority messages can appear near the player HUD.
- Low-priority messages go to the log.
- Split-screen players need compact personal feeds and a shared event log.
- Repeated actions should batch messages instead of spamming.
- The player should be able to filter channels later.

Avoid:

- long mandatory stat dumps,
- fake choices,
- opaque score math,
- class options that ignore player actions,
- dungeon teleporting that bypasses survival logistics.

## Seven Day Probation Structure

The first seven days are the onboarding campaign and scoring window.

| Day | Purpose | Expected Player Actions | System Output |
|---|---|---|---|
| 1 | Arrival and survival | gather, eat, craft basic tools, find shelter | first stat ticks, survival score begins |
| 2 | Role discovery | fight or avoid mobs, gather, craft, trade, explore | tendency hints unlock |
| 3 | Risk introduction | first cave/ruin, stronger mobs, minor quests | grade forecast appears |
| 4 | Specialization pressure | choose routes: combat, craft, trade, magic, scout | weighted categories diverge |
| 5 | Resource and travel test | plan a trip, camp, manage fatigue and supplies | survival and exploration weights rise |
| 6 | High-value opportunity | optional dungeon, escort, rare node, boss, trade run | high-risk multipliers |
| 7 | Final proof | last scoring actions, return to settlement/obelisk route | final grade lock and summons |

At sunset on Day 7:

1. active scoring freezes,
2. the player receives a summons,
3. nearby unclassed people travel or are transported to the obelisk,
4. the obelisk displays grade and action profile,
5. the player receives class choices,
6. rejected classes enter history but are not available by default.

## Score Categories

The System calculates score from categories, then applies risk, rarity, and
consistency modifiers.

| Category | Tracks | Example Actions |
|---|---|---|
| Combat | direct fighting and threat resolution | kills, boss damage, perfect dodges, parries, trap kills |
| Survival | staying alive under pressure | food management, warmth, shelter, medicine, safe camping |
| Exploration | discovering and mapping | landmarks, distance travelled, hidden rooms, routes, scouting |
| Crafting | making and improving goods | tools, weapons, armor, camp gear, repairs, quality crafts |
| Gathering | resource extraction | mining, logging, foraging, hunting, fishing, rare nodes |
| Magic | spell use and mana control | elemental casting, wards, healing, ritual use, mana efficiency |
| Social | people, factions, quests | trading, reputation, escorting, persuasion, guild work |
| Building | structures and terrain control | shelters, camps, traps, walls, bridges, workstations |
| Trade | economic behavior | profit, arbitrage, market requests, barter value, caravans |
| Support | healing and enabling others | healing, buffing, cooking, rescue, group survival |

## Core Stats And Gameplay Effects

Stats must have direct, felt gameplay impact. If a stat only changes a number on
the character sheet, it is not doing enough.

Primary stats:

| Stat | Affects | Example Breakpoints |
|---|---|---|
| STR | melee damage, tool impact, carry load, heavy armor comfort, shove power | STR 10: carry logs without slow; STR 20: break weak barricades |
| DEX | movement speed, attack speed, dodge window, bow handling, trap placement | DEX 12: faster harvesting; DEX 18: extra dodge invulnerability frames |
| VIT | HP, stamina cap, fatigue resistance, poison/bleed recovery, travel endurance | VIT 15: reduced forced-march fatigue |
| END | stamina recovery, sprint duration, mining/chopping stamina cost, long combat | END 20: full-day travel without heavy fatigue if fed |
| DEF | damage reduction, armor scaling, stagger resistance, trap mitigation | DEF 15: ignore weak nibbler stagger |
| INT | mana pool, spell scaling, crafting discovery, appraisal, dungeon puzzles | INT 12: identify basic runes |
| WIS | perception, tracking, map clues, rare node detection, ambush warning | WIS 14: see danger estimate before route choice |
| CHA | trade prices, persuasion, party morale, hireling cost, guild reputation | CHA 12: extra dialogue option with traders |
| LUCK | rare drops, critical craft quality, event outcomes, hidden title odds | LUCK 15: small chance to upgrade loot roll |

Derived values:

| Derived Value | Inputs | Gameplay |
|---|---|---|
| HP | VIT, DEF, class, gear | survival |
| MP | INT, WIS, class, gear | spellcasting |
| Stamina | VIT, END, food, fatigue | sprinting, tools, combat actions |
| Carry Load | STR, packs, profession perks | expedition planning and loot haul |
| Move Speed | DEX, carry load, terrain, fatigue | travel and combat feel |
| Attack Speed | DEX, weapon weight, class | combat pacing |
| Tool Speed | STR/DEX plus tool tier | gathering and crafting loop |
| Armor Burden | STR, DEF, gear weight | tank builds without free mobility |
| Perception | WIS, light, fatigue, class | hidden nodes, ambushes, tracks |
| Craft Quality | INT/DEX, profession, station, fatigue | item tiers and affixes |
| Social Leverage | CHA, titles, guild rank | shop prices, contracts, party NPCs |

Stat design rules:

- Every build should have tradeoffs.
- Carry load is a real expedition constraint, not a nuisance-only cap.
- Fatigue should reduce effective stats temporarily.
- Food, potions, titles, and class skills can temporarily modify derived values.
- Coop players can specialize: one heavy carrier, one scout, one cook, one tank.

## XP Channels, Skills, And Rankings

Do not use one generic XP bar for everything. LitRPG feel comes from separate
progression tracks that recognize different kinds of work.

Progression channels:

| Channel | Progresses From | Unlocks |
|---|---|---|
| Character Level | achievements, quest completions, discoveries, dungeon clears | stat points, broad survivability |
| Class Level | class-aligned actions | skills, passives, class quests, evolutions |
| Profession Level | production, commissions, station use | recipes, licenses, quality tiers |
| Skill Mastery | repeated tool/weapon/spell use | handling bonuses, techniques |
| Adventurer Rank | guild-reviewed delves, bounties, rescues, reliability | harder quests, party contracts |
| Guild Rank | shared guild/base/town objectives | base expansion, services, recruits |
| Region Reputation | local town/continent work | prices, permissions, NPC trust |
| Dungeon Clearance Rank | dungeon performance | claim rights, records, advanced contracts |

Example character readout:

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

Adventurer rank ladder:

| Rank | Meaning |
|---|---|
| Unwritten | first-seven-day participant, no class |
| Copper | certified for basic local contracts |
| Iron | proven local delver |
| Ash-Silver | survived a breach, raid, or elite contract |
| Goldleaf | trusted for regional threats |
| Star-Iron | continent-level adventurer |
| Obelisk-Ranked | directly recognized by high-tier obelisk records |

Rank gates:

- quest difficulty,
- dungeon threat access,
- party recruitment,
- shop credit,
- travel permits,
- boat routes,
- base expansion,
- town projects,
- class advancement trials.

UI rule:

- The main HUD should not show every bar at once.
- Recent gains should appear in a compact growth feed.
- Full detail belongs in the Status, Class, Profession, Skill, and Rank screens.

## Weighted Class Assignment

Class options are not random. Each class has a weight profile. The player's
7-day action profile is compared to those weights.

Example:

| Class | Main Weights | Secondary Weights | Low Weight |
|---|---|---|---|
| Ranger | Exploration, Combat, Survival | Gathering | Trade |
| Guardian | Combat, Survival, Building | Support | Trade |
| Elementalist | Magic, Combat | Exploration | Building |
| Duelist | Combat, Agility actions, Risk | Exploration | Crafting |
| Cleric | Support, Magic, Social | Survival | Gathering |
| Artificer | Crafting, Magic, Building | Trade | Combat |
| Merchant | Trade, Social, Exploration | Crafting | Combat |
| Beast Tamer | Social, Survival, Combat | Exploration | Crafting |
| Alchemist | Crafting, Gathering, Magic | Trade | Building |
| Pathfinder | Exploration, Survival, Social | Combat | Building |

Calculation concept:

```text
class_fit = sum(category_score * class_weight)
class_offer_score = class_fit + rarity_bonus + title_modifiers + hidden_achievements
```

The game should expose enough to be fair:

- "Your actions strongly favored Exploration and Survival."
- "Your combat record qualifies you for martial classes."
- "Your crafting score was high enough to unlock a profession synergy option."

It should not expose every hidden threshold.

## Grade And Class Rarity

The player's final grade controls the rarity ceiling of obelisk class options.

| Grade | Typical Outcome | Class Rarity Ceiling | Option Count |
|---|---|---|---|
| F | survived poorly or failed key basics | Common | 2 |
| E | basic survival with limited specialization | Common+ | 2-3 |
| D | competent starter performance | Uncommon | 3 |
| C | strong focused actions | Rare | 3 |
| B | high score with risk and consistency | Epic | 3-4 |
| A | excellent performance with rare feats | Legendary | 4 |
| S | exceptional, high-risk, hidden feats | Mythic | 4-5 |

Rarity should mean more than raw power:

- extra skill tree branch,
- better stat growth,
- unique class resource,
- rare profession synergy,
- stronger title compatibility,
- special dungeon interaction,
- access to faction or guild training.

## Class Rarity Examples

| Rarity | Combat Example | Magic Example | Survival/Explore Example | Hybrid Example |
|---|---|---|---|---|
| Common | Swordsman | Apprentice Mage | Scout | Herbal Hand |
| Uncommon | Shieldbearer | Flame Adept | Tracker | Trap Hunter |
| Rare | Warden | Elementalist | Ranger | Alchemical Scout |
| Epic | Iron Vanguard | Stormbinder | Deep Pathfinder | Rune Artificer |
| Legendary | Oath Guardian | Starfire Magus | Far-Road Ranger | Beastbound Alchemist |
| Mythic | Aegis of the Seventh Gate | Worldflame Heir | Horizon Walker | Relic-Wrought Sovereign |

Class names should be original and fit the world, but the function must remain
clear. Players should understand what a class does from name, icon, and short
description.

## Action Evidence Examples

The 7-day score should track not just totals, but action style. Two players can
both kill ten enemies and still qualify for different classes.

| Evidence Type | What It Means | Possible Class Bias |
|---|---|---|
| Melee kills | direct close combat | Swordsman, Duelist, Berserker |
| Shield blocks | protection and endurance | Guardian, Warden, Bulwark |
| Bow kills | ranged precision | Archer, Ranger, Marksman |
| Trap kills | preparation and control | Trapper, Hunter, Tactician |
| Avoided encounters | stealth and route planning | Scout, Pathfinder, Rogue |
| Healing others | support identity | Cleric, Medic, Saint |
| Crafted gear used successfully | practical crafting | Artificer, Smith-Knight |
| Profit from trade route | economic play | Merchant, Broker, Caravaner |
| Cooking before danger | preparation/support | Cook, Hearthkeeper, Provisioner |
| Long-distance mapping | exploration | Ranger, Pathfinder, Cartographer |
| Survived night without shelter | grit/risk | Survivalist, Wildling |
| High mana efficiency | spell discipline | Mage, Elementalist, Ritualist |
| Tamed or spared mobs | ecology/social | Beast Tamer, Druid, Wildspeaker |

## Titles

Titles are one of the cheapest high-impact LitRPG systems. They make strange
player behavior feel recognized.

Title rules:

- Titles unlock from deeds, patterns, failures, risks, and social choices.
- Titles can unlock dialogue, class candidates, recipes, guild board entries,
  discounts, or small bonuses.
- Permanent title stat stacking can break balance, so only a limited number of
  titles should be active at once.
- Some titles are cosmetic, some are mechanical, and some are hidden class keys.

Examples:

| Title | Condition | Possible Effect |
|---|---|---|
| First Night Survivor | survive first unsafe night | small fatigue resistance |
| Village Shield | repel a settlement raid | town reputation, defender class bias |
| Campfire Captain | keep party fed/rested across expeditions | better camp recovery |
| Trail Cook | cook useful travel meals repeatedly | Cook/Hearth class bias |
| Goblin-Bane | stop goblin threats repeatedly | bonus against goblin factions |
| Returned For Them | rescue abandoned party member | support/leader class bias |
| Storm-Touched | survive sea storm | Tide/Gale affinity nudge |
| Overburdened Fool | travel while badly overloaded | joke title, carry lesson, minor STR evidence |
| Map-Blessed | discover many landmarks | cartography/exploration bonuses |
| Oathkeeper | complete escort without casualty | better party recruitment trust |

Example notice:

```text
[Title Acquired]
Campfire Captain

Condition met:
Prepared field meals and prevented party fatigue during expeditions.

Effect:
+5% camp rest efficiency while title is active.
Unlocks Hearthkeeper class candidate during class assignment.
```

## Affinity System

Affinities make magic feel discovered through play instead of selected from a
flat spell menu.

Start with a readable affinity set:

| Affinity | Gameplay Identity |
|---|---|
| Ember | heat, courage, forging, burst damage, cooking fire |
| Tide | healing, fishing, weather, cleansing, movement |
| Root | farming, growth, binding, stamina, homestead expansion |
| Stone | defense, mining, construction, endurance |
| Gale | speed, scouting, ranged combat, sailing |
| Glimmer | light, illusion, enchantment, secrets, luck |
| Umber | shadow, stealth, leatherwork, tracking |
| Aether | obelisks, teleportation, raw mana, rare System skills |
| Hearth | food, comfort, camp safety, morale, party buffs |

Rare mixed affinities:

| Mixed Affinity | Inputs | Identity |
|---|---|---|
| Stormglass | Gale + Glimmer | navigation, crit magic, storm reading |
| Ironroot | Stone + Root | defense, farming, base building |
| Ashbloom | Ember + Root | growth through damage, fire-resistant crops |
| Moonwell | Tide + Glimmer | healing illusions, dream/shrine quests |
| Hearthflame | Hearth + Ember | cooking buffs, morale combat, camp wards |

Affinity sources:

- spell use,
- elemental damage survived,
- biome exposure,
- shrine interactions,
- dungeon themes cleared,
- food/elixirs consumed,
- enchanted gear,
- profession work,
- titles earned,
- animal/spirit bonds,
- obelisk events.

Affinity effects:

- spell unlock rate,
- mana cost,
- resistance,
- crafting/enchanting options,
- class offer candidates,
- dungeon interactions,
- dialogue/shrine reactions,
- boat/weather handling,
- farming or cooking variants.

Example notice:

```text
[Affinity Resonance]
Repeated field cooking, camp defense, and party morale recovery detected.

Hearth Affinity awakened: Minor
Effects:
+3% camp recovery
+5% Hearth recipe discovery rate
Hearthkeeper class candidate unlocked
```

Implementation rule:

- For the first playable version, implement only a small subset: Ember, Tide,
  Root, Stone, Gale, Glimmer, plus one rare affinity such as Hearth.
- The data model should support more affinities later.

## Obelisk Class Ceremony

The obelisk is the end of tutorial and beginning of the real game.

Required sequence:

1. player approaches or is summoned,
2. other unclassed NPCs are present,
3. the System shows final grade,
4. score category summary appears,
5. notable deeds and titles appear,
6. class options appear by rarity,
7. each class shows:
   - rarity,
   - role,
   - stat growth,
   - starting active skill,
   - passive trait,
   - progression method,
   - recommended profession synergies,
8. player selects one class,
9. first class quest unlocks,
10. world map expands beyond starter zone.

NPCs should also receive classes based on their visible behavior. This reinforces
that the System is world-level, not player-only.

Obelisk types:

| Obelisk | Role |
|---|---|
| Measure | assigns first class after the Proving Week |
| Accord | registers parties, guilds, contracts, town law |
| Return | safe-zone travel anchor |
| Depth | dungeon records, delving claims, breach warnings |
| Broken | unstable quests, corrupted records, hidden trials |

Example final notice:

```text
[Seven Days Recorded]
[You Were Not What You Claimed To Be]
[You Were What You Practiced]
```

## Class And Profession Split

The class/profession split is core to the game.

Class:

- combat, magic, exploration, survival identity,
- determines active abilities and combat/exploration role,
- progresses through relevant class actions,
- unlocks class quests and advanced branches.

Profession:

- crafting, economy, production, settlement identity,
- determines recipes, quality systems, trade options, service unlocks,
- progresses by doing work,
- can be changed or expanded later through guilds, but not trivially.

Examples:

| Class | Profession | Playstyle |
|---|---|---|
| Ranger | Alchemist | gather rare herbs, brew expedition supplies, scout dungeons |
| Warden | Blacksmith | tank threats, forge armor, maintain party gear |
| Elementalist | Cook | fire magic, camp safety, heat-based food buffs |
| Duelist | Trader | fast travel, escort contracts, high-risk route commerce |
| Cleric | Builder | support party, create safe camps and recovery sites |
| Beast Tamer | Farmer | raise animals, grow feed, tame wilderness mobs |
| Artificer | Enchanter | craft devices, enhance gear, unlock dungeon mechanisms |

Possible early class candidates:

| Class | Evidence Pattern |
|---|---|
| Trailblade | melee, travel, scouting, survival |
| Iron Warden | shield use, damage taken, ally protection |
| Hearthbound Acolyte | cooking, healing, camp support |
| Stonehand Delver | mining, dungeon diving, blunt weapons |
| Wildsign Ranger | scouting, bow use, beast tracking |
| Ashvein Pyromancer | fire exposure, spell use, risk-taking |
| Wayfarer | mapping, travel, escape, route planning |
| Oathbearer | quest completion, protection, leadership |
| Rune-Touched | shrine study, magic tools, obelisk interactions |
| Fortune Broker | trade, negotiation, delivery contracts |

## Class Progression

Class XP should come primarily from actions that match the class.

Example class XP sources:

| Class | Primary XP | Secondary XP |
|---|---|---|
| Ranger | scouting, tracking, bow kills, map discovery | camping, trap use, beast study |
| Warden | blocking, guarding, base defense, taunt use | armor crafting, escort quests |
| Elementalist | spell damage, mana control, elemental puzzles | crafting catalysts |
| Cleric | healing, cleansing, buffs, rescue | social quests, undead dungeons |
| Duelist | one-on-one combat, dodges, counters | arena tasks, weapon mastery |
| Beast Tamer | taming, feeding, commanding pets | ecology quests, non-lethal wins |
| Artificer | device use, magical crafting, trap mechanisms | dungeon puzzles, repairs |

Class levels unlock:

- active skills,
- passives,
- class resources,
- weapon proficiencies,
- dungeon interactions,
- branch choices,
- advanced class quests.

## Profession Progression

Professions improve through work output, quality, and economic use.

| Profession | Progresses By | Unlocks |
|---|---|---|
| Blacksmith | smelting, forging, repairing, orders | weapon tiers, armor, tool mods |
| Alchemist | brewing, identifying plants, potion use | potions, bombs, antidotes, catalysts |
| Cook | meals, preservation, party food planning | buffs, long-travel rations, morale meals |
| Builder | shelters, camps, defenses, stations | stronger structures, bridges, outposts |
| Trader | buying/selling, routes, contracts | better prices, caravans, market rumors |
| Farmer | tilling, growing, breeding crop traits | high-yield crops, feed, reagents |
| Tailor | cloth, bags, insulation, repair | packs, cold gear, light armor |
| Scribe | maps, scrolls, contracts, records | quest hints, dungeon maps, skill scrolls |
| Enchanter | runes, affixes, mana materials | gear traits, dungeon keys, wards |

Profession levels unlock:

- recipes,
- quality tiers,
- station upgrades,
- shop services,
- hireable NPC tasks,
- guild ranks,
- passive income opportunities.

## Production Crafting Pillars

Crafting needs to support both cozy life-sim play and expedition preparation.
The core production pillars are:

| Pillar | Loop | Outputs |
|---|---|---|
| Mining | prospect, dig, haul, refine | stone, ore, gems, coal, salts |
| Smelting | fuel furnace, process ore, manage quality | ingots, alloys, slag byproducts |
| Forging | shape metal, temper, repair | weapons, armor, tools, fittings |
| Leatherwork | hunt, skin, tan, stitch | light armor, packs, straps, saddles |
| Tailoring | gather fiber, weave, sew, insulate | clothing, bags, robes, weather gear |
| Woodwork | chop, saw, carve, join | furniture, bows, handles, structures |
| Alchemy | identify reagents, brew, stabilize | potions, oils, bombs, antidotes |
| Enchanting | inscribe, bind mana, add affixes | runes, wards, enchanted gear |
| Cooking | prep ingredients, combine buffs, preserve | meals, rations, feasts |
| Farming | till, plant, water, breed traits | food, fibers, reagents, feed |

Production rules:

- Raw resource quality should matter through the whole chain.
- Stations determine which recipes are available and the quality ceiling.
- Profession level improves speed, quality control, recipe discovery, and waste
  reduction.
- Fatigue, bad lighting, and poor tools reduce quality or raise mistake chance.
- Coop should support assembly-line play: miner, smelter, smith, enchanter,
  cook, and trader can all contribute to one expedition.
- Rare dungeon materials should unlock special crafts without replacing the
  value of ordinary settlement production.

Crafting should produce both direct gear and logistics items:

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

## Magic, Enchanting, And Item Affixes

Magic should connect combat, crafting, exploration, and settlement progression.

Magic pillars:

| Pillar | Use |
|---|---|
| Elemental magic | combat, terrain hazards, cooking heat, smelting boosts |
| Restoration | healing, cleansing, fatigue recovery, crop recovery |
| Warding | camp safety, base security, dungeon protection |
| Utility magic | light, repair, water, bridge/rope alternatives |
| Ritual magic | portals, obelisk interactions, dungeon locks |
| Enchanting | item affixes, station upgrades, boat protection |

Enchantment rules:

- Enchantments require a base item, reagent, rune pattern, mana source, and
  station.
- Low-level enchanting adds one simple affix.
- Higher rarity gear can hold more affixes or stronger affixes.
- Failed enchanting should damage durability, consume reagents, or add unstable
  side effects rather than deleting the item by default.
- Enchanter, Artificer, Scribe, Mage, and certain rare classes should receive
  different ways to manipulate affixes.

Example affixes:

| Affix | Effect |
|---|---|
| Keen | increased crit chance or weak-point damage |
| Sturdy | increased durability and block stability |
| Warm | cold resistance and camp comfort |
| Feathered | reduced weight or armor burden |
| Conductive | better lightning/fire/frost spell scaling |
| Harvesting | rare gathering chance |
| Warded | reduced ambush or curse risk |
| Tidal | boat handling, fishing, or storm resistance |

## Cooking, Food Buffs, And Food-Based Classes

Cooking should be a real LitRPG system, not only hunger recovery. Food is how
players prepare for travel, dungeons, weather, crafting sessions, and boss fights.

Food categories:

| Category | Use |
|---|---|
| Travel rations | slow hunger loss, low spoilage, light weight |
| Camp meals | fatigue recovery, morale, warmth |
| Combat meals | HP, stamina, attack speed, resistance |
| Craft meals | craft quality, focus, mistake reduction |
| Gathering meals | rare node chance, tool stamina cost |
| Magic meals | mana recovery, elemental affinity, ward strength |
| Social meals | reputation, NPC bond, party morale |

Buff rules:

- One main meal buff at a time.
- Snacks can add small secondary buffs.
- Better cooking profession increases duration and secondary effects.
- Eating poorly before travel should matter.
- Spoilage creates planning pressure unless preserved.

Food examples:

| Food | Effect |
|---|---|
| Trail Biscuit | long-duration hunger buffer, low weight |
| Mushroom Broth | warmth, stamina recovery, minor poison resistance |
| Skewered Meat | HP and STR for short combat window |
| Honeyed Porridge | fatigue recovery and morale |
| Glowcap Tonic | night vision and perception |
| Miner Stew | tool stamina reduction, carry load bonus |
| Hearth Feast | party-wide comfort and rested buff |

Cooking class/profession paths:

- **Cook profession:** recipes, preservation, camp meals, feast buffs.
- **Provisioner profession:** travel packs, ration efficiency, party logistics.
- **Battle Chef class:** combat buffs from prepared meals, thrown spice bombs,
  morale recovery.
- **Hearthkeeper class:** camp safety, fatigue recovery, party support.
- **Alchemical Cook hybrid:** potion-food combinations with stronger but riskier
  effects.

## World Danger And Rarity Gradient

The world is arranged by practical distance and danger, not just level bands.

| Zone | Distance | Danger | Typical Rarity | Notes |
|---|---|---|---|---|
| Starter Zone | 0-1 travel segment | low | Common, Uncommon | safe roads, weak mobs, basic resources |
| Outer Fields | 1-2 segments | low-medium | Uncommon | first travel planning, stronger nights |
| Wild Roads | 2-3 segments | medium | Uncommon, Rare | camp required for most trips |
| Frontier Dungeons | 3-4 segments | medium-high | Rare | supply planning becomes mandatory |
| Deep Wilds | 4-6 segments | high | Rare, Epic | weather, fatigue, ambush risk |
| Red Zones | 6+ segments | severe | Epic, Legendary | party or advanced prep recommended |
| Mythic Regions | special access | extreme | Legendary, Mythic | class/profession milestones required |

Distance should create decisions:

- Do I carry more food or leave room for loot?
- Do I buy potions or risk cheaper travel?
- Do I bring a camping kit or race the daylight?
- Do I hire a guide?
- Do I travel with pack animals?
- Do I build a forward camp?

## World Events

Events make the world feel alive and make the guild board matter.

Event categories:

| Event Type | Example | Player Response |
|---|---|---|
| Raid | goblin raid on a hamlet, caravan, camp, or player base | defend, evacuate, negotiate, set traps |
| Sighting | dangerous mob spotted near road or dungeon | scout, avoid, hunt, report |
| Migration | beasts move into a zone after weather or dungeon breach | adapt routes, hunt, build defenses |
| Resource bloom | rare herbs, ore, or mana flowers appear briefly | travel fast, compete, protect claim |
| Weather crisis | storm, frost, heatwave, fog | prepare food, shelter, gear |
| Dungeon breach | dungeon mobs spill into nearby zones | close breach, farm risk, evacuate NPCs |
| Merchant event | caravan arrives, auction, black-market trader | trade, escort, protect |
| Rival team event | another adventuring party clears, fails, or contests content | ally, race, rescue, negotiate |
| Settlement request | town asks for supplies, repairs, medicine | deliver goods, gain reputation |
| Obelisk anomaly | class stone reacts, hidden trial opens | investigate, report, exploit |

Event severity:

| Severity | Scope | Example |
|---|---|---|
| Minor | local and optional | wolf tracks near herb patch |
| Moderate | quest-board issue | goblin scouts on west road |
| Major | changes route or town state | goblin raid forming |
| Severe | multi-day consequence | dungeon breach threatens starter zone |
| World | region-scale event | red-zone boss migrates into frontier |

## Guild Quest Board And Rumor System

The guild board is the information hub for events. It should not only list static
quests.

Board item types:

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

Example board entries:

| Entry | Type | Timer | Outcome If Ignored |
|---|---|---|---|
| Goblin scouts seen near the old mill | Sighting | 2 days | may escalate to raid |
| Ogre tracks north of Briar Copse | Dangerous mob rumor | 1 day | road danger increases |
| Root Cellar breathing mold again | Dungeon report | 3 days | dungeon modifier changes |
| Copper caravan needs guards | Escort | leaves at dawn | trade stock delayed |
| Unknown party missing near Glasswell | Rescue | 2 days | NPC team lost or corrupted |
| Blue herbs blooming after rain | Resource bloom | until night | rare ingredient disappears |

Rumor reliability:

- Low rank board posts can be incomplete.
- Higher WIS, Scout classes, Cartographer/Scribe professions, or guild rank can
  improve information accuracy.
- False rumors should be rare and explainable, not random punishment.

Escalation model:

```text
sighting -> confirmed threat -> active quest -> raid/breach/event consequence
```

Example goblin raid chain:

1. Day 2: "goblin tracks near west road"
2. Day 3: "farm tools stolen"
3. Day 4: "goblin camp found"
4. Day 5: player can scout, sabotage, negotiate, or ignore
5. Day 6: raid occurs if unresolved
6. Result affects shop stock, NPC safety, road danger, and titles

## Random Encounters And NPC Adventuring Teams

Other teams make the world feel like a real LitRPG ecosystem. The player is not
the only one progressing.

NPC team types:

| Team Type | Behavior | Interaction |
|---|---|---|
| Rookie party | low-rank group making mistakes | rescue, teach, trade, compete |
| Professional guild team | efficient and well-supplied | hire, buy intel, lose race to objective |
| Rival team | targets same dungeon/resource | race, duel, negotiate split |
| Merchant escort | traveling with goods | guard, trade, ambush event |
| Wounded survivors | failed dungeon run | heal, loot ethically, escort |
| Suspicious party | poachers, bandits, exploiters | report, fight, bargain |
| Specialist team | miners, mages, beast handlers | unlock profession/class hints |

Encounter outcomes:

- reputation gain/loss,
- party member recruit,
- map intel,
- shop discount,
- rival hostility,
- shared dungeon clear,
- stolen objective,
- rescue title,
- rumor unlock.

NPC teams should have visible rank/class/profession tags after the obelisk system
is introduced.

Party roles:

| Role | Purpose |
|---|---|
| Vanguard | holds threat, blocks paths, protects weaker members |
| Striker | burst damage and weak-point attacks |
| Scout | traps, routes, tracks, ambush warning |
| Binder | slows, roots, controls enemy movement |
| Mender | healing, cleansing, recovery |
| Delver | locks, mining, dungeon resources, mechanisms |
| Arcanist | seals, wards, magic puzzles, elemental threats |
| Quartermaster | supplies, food, repairs, camp setup |
| Caller | leader role, marks targets, triggers party tactics |

Party customs:

- Before a delve, parties perform a **Load Count**: food, water, rope, torches,
  potions, bedrolls, repair kits.
- Loot division can use Need, Use, Ledger Share, Contract Claim, or Party Share.
- Abandoning allies can create negative reputation or titles.
- Rescuing other teams can create allies, recruits, map intel, or rival respect.

## Exploration Systems

Exploration needs explicit rewards and mechanics.

Exploration inputs:

- map fog,
- route length,
- terrain difficulty,
- danger estimate,
- landmarks,
- resource biomes,
- dungeon entrances,
- hidden shrines,
- campsites,
- weather zones,
- monster territories.

Exploration rewards:

- XP,
- class evidence,
- map reveal,
- fast route,
- dungeon discovery,
- rare node location,
- camp location,
- trade route,
- title,
- lore,
- profession recipe.

Exploration mechanics:

| Mechanic | Gameplay |
|---|---|
| Landmark discovery | reveals nearby route/resource/dungeon |
| Cartography | converts exploration into maps that can be sold or used |
| Tracking | follows mob trails, party trails, rare beast signs |
| Surveying | identifies buildable camp/base locations |
| Route safety | repeated clearing lowers danger |
| Biome expertise | classes/professions gain bonuses in known biomes |
| Hidden POIs | perception, maps, rumors, and weather reveal secrets |

Exploration should interact with coop: one player can scout while another builds,
cooks, trades, or prepares gear.

## Travel System

Travel to dungeons must be immersive and mechanically relevant.

Travel resources:

- time,
- stamina,
- fatigue,
- hunger,
- thirst,
- temperature,
- light,
- carry weight,
- weather exposure,
- route danger,
- map knowledge.

Travel modes:

| Mode | Speed | Risk | Cost |
|---|---|---|---|
| cautious | slow | lower ambush, better discoveries | more food/time |
| normal | medium | normal | baseline |
| forced march | fast | fatigue, injury, missed discoveries | high stamina/food |
| stealth travel | slow | lower detection | class/skill dependent |
| caravan travel | medium | safer, costly | money/favor |

Travel events:

- weather front,
- injured traveller,
- broken bridge,
- mob tracks,
- hidden shrine,
- merchant camp,
- nightfall warning,
- supply spoilage,
- rare gathering node,
- ambush,
- dungeon rumor.

## Continents, Ocean Travel, And Boats

The world should eventually expand beyond one landmass. Continents create a
clean progression gate: the player can see rumors of distant regions before they
can safely reach them.

Ocean travel loop:

1. discover or buy a coastal map,
2. gain port access or craft a boat,
3. stock food, water, repair materials, bait, lantern oil, and storm gear,
4. choose a route,
5. handle weather, fishing, durability, and encounters,
6. arrive at a new coast, port, island, dungeon, or hazard.

Boat progression:

| Tier | Role | Unlock |
|---|---|---|
| Rowboat | short coastal travel, fishing | starter port quest |
| Skiff | island hopping | woodwork/leatherwork |
| Sailboat | continent crossing in fair weather | guild license or shipwright |
| Reinforced cutter | storms and dangerous waters | rare wood, iron fittings |
| Enchanted vessel | red-zone oceans, portal storms | enchanting, dungeon core |

Boat systems:

- durability,
- storage,
- crew/passenger slots,
- sail/handling rating,
- storm resistance,
- anchor/camp-at-sea option,
- fishing spots,
- sea monster danger,
- cargo weight,
- repair kits,
- dock fees and port reputation.

Storms should damage durability, spoil supplies, slow travel, or force route
changes. Good preparation should reduce risk: reinforced hulls, weather charms,
trained sailors, better maps, and high WIS route reading.

Fishing should be Stardew-like in spirit: readable, skill-based, quick, and tied
to location, time, weather, bait, rod quality, and player stats. Fishing feeds
cooking, alchemy, trade, taming, and rare quest chains.

## Dungeon Expedition Preparation

Before entering a dungeon, the player should see a preparation checklist.

Required expedition items:

- food,
- water or waterskin,
- healing potions,
- mana potions if relevant,
- antidotes or status cures,
- bedroll or camping set,
- fire starter,
- torch/lantern/oil,
- repair kit,
- rope,
- lockpicks or pry tool,
- map/chalk markers,
- empty inventory capacity.

Optional but valuable:

- class-specific kit,
- profession tools,
- bait,
- trap kit,
- scrolls,
- ward stones,
- weather gear,
- pack animal,
- hireling/companion,
- dungeon-specific resistance item.

Preparation should not hard-block entry, but underprepared entry should be costly.

Expedition templates:

| Template | Use | Supplies |
|---|---|---|
| Quick Delve | short local dungeon | food, torch, potion |
| Deep Delve | multi-floor dungeon | food, water, camp kit, potions, repair kit |
| Boss Attempt | known elite/boss | resistance food, scrolls, antidotes, spare gear |
| Gathering Run | mining/herbs/fishing | tools, carry bags, light, route food |
| Rescue Run | missing party/event | medical kit, ropes, extra food, escort plan |

The prep screen should support warnings, not hard locks:

```text
[Expedition Warning]
Mossjaw Burrow
Threat: D
Known threats: goblins, snare traps, poison moss
Recommended: antidotes, torch oil, slashing weapon

Missing: antidote
Risk: poison recovery will be difficult.
```

## Dungeon Result Screen

Every dungeon should end with a LitRPG-style report. This reinforces ranking,
progression, loot, party contribution, and title progress.

Result screen fields:

- dungeon name,
- threat rank,
- clear grade,
- depth reached,
- rooms discovered,
- hidden rooms found,
- party injuries,
- supplies consumed,
- contracts completed,
- loot recovered,
- class XP gained,
- profession XP gained,
- skill mastery gained,
- affinity changes,
- title progress,
- town/guild reputation changes,
- world event consequences.

Example:

```text
[Dungeon Cleared]
Mossjaw Burrow - Rank D

Clear Grade: B
Depth reached: 3/3
Party injuries: 1 minor
Loot recovered: 14 items
Contracts completed: 2
Hidden rooms found: 1

Class XP gained:
Trailblade +320
Iron Warden +280

Profession XP gained:
Cook +90
Miner +140

Title progress:
Goblin-Bane 72%
Campfire Captain 40%
```

## Camping And Night Rules

If the player is away from a safe settlement at night, they need a camp.

Camp requirements:

- safe tile or clearing,
- bedroll,
- campfire,
- fuel,
- food,
- enough light,
- optional shelter if weather is active.

If the player camps properly:

- fatigue reduces,
- food can be cooked,
- buffs can be applied,
- minor crafting/repair is available,
- night passes with manageable event risk.

If the player does not camp:

- fatigue accumulates,
- stamina cap drops,
- perception drops,
- movement slows,
- monster encounter chance rises,
- some night-only predators begin tracking,
- the player may be forced into a dangerous rest state.

Camp quality:

| Quality | Conditions | Outcome |
|---|---|---|
| Poor | no fire, bad weather, hungry | fatigue remains, high ambush chance |
| Basic | fire, bedroll, food | normal rest, low event chance |
| Good | shelter, cooked meal, light | buff, lower danger |
| Excellent | guarded camp, wards, comfort | strong buff, rare safe-night events |

## Fatigue System

Fatigue makes travel planning matter.

Fatigue increases from:

- long travel,
- forced march,
- combat,
- mining/logging,
- carrying too much,
- cold/rain exposure,
- poor sleep,
- hunger/thirst,
- dungeon modifiers.

Fatigue effects:

- lower stamina cap,
- slower stamina recovery,
- reduced dodge/parry timing,
- lower gathering speed,
- worse crafting quality,
- higher chance of mistakes,
- lower perception.

Fatigue recovery:

- sleep,
- campfire rest,
- good meals,
- potions,
- safe inns,
- class/profession perks,
- campsite upgrades.

## Homestead, Farming, And Pocket Base

The player should eventually gain a personal base space. The cleanest structure
is a pocket-dimension homestead: an empty or mostly empty build world connected
to the player/guild through a portal, deed, obelisk shard, or guild license.

Core concept:

- Starter towns are public hubs.
- The player can later unlock a private **Homestead Realm**.
- The realm starts small and mostly empty.
- The player places tiles, buildings, farms, crafting stations, storage, and
  decorations.
- Guild progression expands the realm boundary and unlocks new tile palettes.

Why this works:

- It supports Stardew-style farming and layout play.
- It avoids overworld base griefing/path conflicts.
- It gives coop players a shared home space.
- It turns building into progression.
- It creates a safe return point after dungeon expeditions.

Unlock options:

| Unlock Method | Meaning |
|---|---|
| Post-obelisk reward | first personal base after class assignment |
| Guild deed | base tied to Adventurer Guild progression |
| Profession license | Builder/Farmer/Trader paths expand base features |
| Dungeon core | clearing dungeons expands realm size |
| Town favor | settlements grant portal anchors |

Base systems:

- tile placement,
- soil/farm plots,
- irrigation,
- storage,
- crafting rooms,
- cooking/kitchen,
- animal pens,
- guest beds,
- portal/obelisk anchor,
- defensive wards,
- trophy displays,
- guild service rooms,
- hireling/NPC workstations.

Base stats:

| Stat | Effect |
|---|---|
| Comfort | rest quality, NPC happiness, buff strength |
| Utility | crafting speed, station adjacency, storage links |
| Security | raid resistance, theft reduction, portal stability |
| Prosperity | shop visitors, trade value, passive income |
| Fertility | crop speed, mutation chance, animal health |
| Arcana | enchanting, portal range, rare events |
| Prestige | guild rank, recruit quality, special visitors |

Expansion model:

| Guild/Base Rank | Realm Size | Unlock |
|---|---|---|
| Rank 1 | small clearing | bedroll, campfire, starter farm |
| Rank 2 | cottage plot | storage, workbench, basic crops |
| Rank 3 | homestead | kitchen, animal pen, portal storage |
| Rank 4 | guild lodge | party rooms, hirelings, service boards |
| Rank 5 | estate realm | specialized biomes, rare crop zones |
| Rank 6+ | pocket settlement | NPC residents, shops, dungeon anchors |

Farming systems:

- crop seasons or biome requirements,
- soil quality,
- watering/irrigation,
- fertilizer,
- crop traits,
- animal feed,
- rare seeds from dungeons,
- profession XP for Farmer/Cook/Alchemist,
- base food supply for expeditions,
- cooking chain integration.

Base raid rule:

- Pocket base should not be constantly attacked.
- Raids happen through events, portal instability, failed defenses, or accepted
  high-risk contracts.
- The player should be able to prepare defenses, hire guards, or disable raids
  for casual/cozy settings.

## Isometric Building System Direction

The building system should be hybrid, not fully freeform. This is based on the
pattern used by comparable cozy/survival/base-building games:

| Reference Pattern | What It Proves | LIT-ISO Takeaway |
|---|---|---|
| Stardew Valley farm buildings | large buildings use fixed footprints, placement validation, upgrades, interiors, and move as a unit | use prefab exteriors for barns, taverns, smithies, sheds, and guild halls |
| Stardew Valley sheds/farmhouse | a small exterior can represent a larger customizable interior | make taverns, cottages, and workshops enterable interior maps |
| Palia housing plot | instanced personal plot, unlockable build area, building categories, decor limits | pocket homestead/guild realm should be an instanced build zone with expansion ranks |
| Dinkum deeds | town buildings are placed through deeds/licenses and can be moved later | buildings should be crafted/earned as deeds, not spammed from a raw build menu |
| Necesse settlements | rooms, settlers, storage permissions, and work areas create useful base logic | later NPC residents need room/workstation ownership rules |
| Core Keeper | workbench tiers unlock new crafting/building options | construction should advance through station tiers and profession unlocks |
| Valheim | freeform structure pieces are deep but require stability, snapping, and support rules | avoid Valheim-style full wall/roof freeform for the first version |
| Unity Isometric Z-as-Y Tilemap | stacked isometric tiles are supported, but sorting/tall-object layering must be controlled | keep tall buildings as managed objects/prefabs, not arbitrary wall stacks |

Recommended approach:

- **Terrain, roads, farms, fences, paths, and decoration:** tile placement.
- **Functional buildings:** crafted prefab footprints placed on the isometric
  grid.
- **Building interiors:** separate instanced interior maps entered by
  interaction.
- **Major upgrades:** swap the exterior prefab and interior layout template.

This keeps the game readable, cheaper to author, easier to support in coop, and
less likely to break pathfinding.

Design decision:

**Use prefab footprints for buildings. Use tile placement for the land around
them and for interior decoration.**

This gives players meaningful layout control without requiring us to solve every
hard problem of isometric freeform wall-building on day one.

Why not pure tile-by-tile buildings:

- isometric walls occlude players and furniture,
- roof/wall layering becomes expensive quickly,
- pathfinding and collision rules get fragile,
- split-screen readability suffers,
- every custom shape needs interior logic,
- art scope grows too fast.

Prefab footprint rules:

| Building | Footprint | Rotations | Interior |
|---|---|---|---|
| Tent | 1x1 | southeast/southwest | no or tiny rest scene |
| Cottage | 2x2 | southeast/southwest | small home |
| Tavern | 3x3 or 4x3 | southeast/southwest | tavern interior |
| Smithy | 3x2 | southeast/southwest | forge/workshop |
| Barn | 3x3 | southeast/southwest | animal interior |
| Guild Hall | 4x4+ | southeast/southwest | guild lodge |
| Dock | shoreline-shaped | fixed by coast | boat access |

Placement rules:

- Buildings snap to the isometric tile grid.
- Buildings reserve a rectangular or mask-based footprint.
- Placement preview shows valid/invalid tiles.
- Rotation is limited to southeast and southwest for art scope.
- Entrances must connect to road/path tiles or valid walkable tiles.
- Road tiles are placed manually by the player.
- Roads improve NPC movement, visitor chance, delivery speed, and town rating.
- Workstations can exist inside interiors or as outdoor placed objects.
- Exterior footprint and interior map are linked by a building instance ID.

Interior entry:

- Player right-clicks or presses interact on the doorway.
- The game fades/loads/teleports to a mini interior map.
- Interior is isometric, with walls arranged for readability.
- Interiors can have furniture placement, workstations, NPCs, storage, rooms,
  beds, and service counters.
- Coop entry should support all local/online players entering together or one
  player entering while others remain outside, depending on the activity.

Interior advantages:

- Tavern can feel large without consuming huge overworld footprint.
- Walls, roofs, lighting, NPC seating, and counters are easier to author.
- Performance is easier to control.
- Decorating stays cozy without making overworld collision impossible.

Building progression:

| Progression | Unlocks |
|---|---|
| Builder profession | more footprints, cheaper placement, structure durability |
| Guild rank | bigger base boundary, guild buildings, service rooms |
| Town reputation | civic buildings, visitors, contracts |
| Dungeon cores | pocket realm expansion, rare room templates |
| Crafting professions | smithy, kitchen, alchemy lab, tannery, enchanting room |

Recommended first implementation:

1. Place/remove road tile.
2. Place/remove 2x2 cottage prefab.
3. Rotate cottage southeast/southwest.
4. Validate footprint and doorway.
5. Interact with doorway.
6. Teleport to small interior scene/map.
7. Return to original doorway.
8. Save/load building instance and interior state.

Research sources:

- Stardew Valley Carpenter's Shop / farm building placement:
  https://wiki.stardewvalley.net/Carpenter%27s_Shop
- Stardew Valley farmhouse/interior upgrade pattern:
  https://wiki.stardewvalley.net/Farmhouse
- Palia Housing Plot instanced build area:
  https://palia.wiki.gg/wiki/Housing_Plot
- Dinkum building/deed placement pattern:
  https://dinkum.fandom.com/wiki/Deeds
- Necesse settlement room/settler pattern:
  https://necessewiki.com/Settlements
- Core Keeper crafting/workbench-tier pattern:
  https://core-keeper.fandom.com/wiki/Crafting
- Valheim freeform building and structural-integrity reference:
  https://valheim.fandom.com/wiki/Building
- Unity Isometric Z-as-Y Tilemap reference:
  https://docs.unity.cn/2022.1/Documentation/Manual/Tilemap-Isometric-CreateIso.html

## Town And Guild Growth

The player base can evolve from camp to homestead to guild town, but growth
should be controlled by licenses and milestones.

Town growth axes:

- buildable area,
- building count,
- NPC residents,
- visitor traffic,
- quest board tier,
- shop services,
- crafting station tier,
- defensive rating,
- portal stability,
- farming output,
- dock/boat access.

Growth should be gated by:

- guild rank,
- town reputation,
- base comfort/security/utility,
- completed civic quests,
- available resources,
- profession licenses,
- cleared dungeon cores.

The pocket realm starts at a fixed size. As guild or town rank increases, new
chunks unlock at the edges. This provides Stardew-style layout mastery while
still letting the world scale.

## Coop And Split-Screen Requirements

The game needs to support split-screen and coop. This affects core system design
from the start.

Required assumptions:

- Multiple players can have independent classes.
- Multiple players can have independent professions.
- The party shares world state, quests, dungeons, events, and homestead.
- Some progression is individual, some is shared.
- UI must work for split-screen without huge stat panels covering both players.

Player-specific state:

- class,
- profession,
- stats,
- equipment,
- inventory or personal bag,
- skill XP,
- titles,
- action evidence,
- fatigue,
- hunger/thirst,
- buffs/debuffs,
- obelisk class offers.

Shared state:

- world seed,
- current day/time,
- discovered map areas,
- active events,
- guild board,
- dungeon state,
- homestead/base,
- shared storage,
- faction reputation where appropriate,
- completed party quests.

Coop scoring during first seven days:

- Each player gets individual class offers.
- Party actions can contribute partial evidence to nearby players.
- Support actions must be valued: healing, cooking, tanking, scouting, trading,
  carrying supplies.
- Avoid letting one combat-focused player determine everyone else's class.

Split-screen UI requirements:

- compact per-player HUD,
- local player inventory panels,
- shared map/quest board screen with ownership indicator,
- clear player color/icon,
- readable prompts near each player,
- pause/menu behavior that does not break the other player in online coop,
- controller support as a first-class requirement.

Coop expedition logistics:

- party supply checklist,
- shared camp setup,
- role-based prep warnings,
- downed-player rescue,
- loot ownership rules,
- trade/quest reward split,
- dungeon entry confirmation,
- retreat vote or leader decision.

Network/split-screen design constraints:

- Do not assume a single global `Player` object.
- All player-affecting systems need player IDs.
- Save data must serialize multiple characters.
- Quest objectives need owner policy: personal, party-shared, proximity-shared,
  or guild-shared.
- Dungeon rewards need loot policy.
- Obelisk class assignment must run per player.

## Dungeon Design Requirements

Dungeons must feel like expeditions, not isolated rooms.

Each dungeon needs:

- travel time,
- recommended supplies,
- entrance conditions,
- threat rating,
- primary enemy families,
- environmental hazards,
- expected duration,
- rest/camp constraints,
- loot rarity band,
- class/profession interactions,
- quest hooks,
- escape rules,
- map persistence.

Dungeon outcomes:

- clear,
- partial clear,
- forced retreat,
- rescue,
- resource haul,
- boss kill,
- secret found,
- faction objective,
- profession objective,
- class milestone.

## Dungeon Families

| Family | Theme | Main Demands | Rewards |
|---|---|---|---|
| Root Cellar | old farm storage, roots, pests | food, antidote, cutting tools | seeds, food, compost, early recipes |
| Mine Shaft | ore tunnels, cave-ins | torches, pickaxe, rope, repair kit | ore, gems, stone, blacksmith materials |
| Ruined Watchpost | broken defenses, undead/echoes | weapons, light, camping kit | armor, maps, defense blueprints |
| Flooded Shrine | water, spirits, disease | water gear, antidotes, magic wards | relics, healing items, cleric unlocks |
| Beast Den | lair ecology, territorial mobs | bait, traps, stealth, armor | hides, pets, beast tamer progress |
| Trial Vault | System-made challenge rooms | rank token, combat kit, potions | class evidence, rare skills, titles |
| Abandoned Workshop | constructs and machinery | repair kit, tools, mana | artificer parts, recipes, devices |
| Deep Crypt | death magic, curses | light, cleanse, food, camping | rare loot, cleric/paladin paths |

## Quests

Quest types:

- survival quests,
- class evidence quests,
- profession orders,
- dungeon contracts,
- escort quests,
- trade requests,
- gathering commissions,
- bounty quests,
- rescue quests,
- exploration/map quests,
- faction reputation quests,
- obelisk milestone quests.

Quest rules:

- Quests should feed action scoring.
- Quests should reveal possible class/profession paths.
- Quests should sometimes compete for time within the 7-day window.
- Quests should have multiple solution styles when possible.

Starter quest chain:

1. **A Body In A New World**: eat, drink, inspect status, make a tool.
2. **First Shelter**: craft bedroll or shelter before night.
3. **Smoke Means Safety**: build and light campfire.
4. **The Road To The Obelisk**: discover obelisk marker and learn Day 7 rule.
5. **Choose Your Proof**: complete one combat, craft, trade, or exploration task.
6. **First Contract**: complete a local request for reputation and score.
7. **Into The Root Cellar**: optional Day 2 dungeon unlock.

## Trading And Economy

Trading should be viable as both profession path and survival strategy.

Needs:

- buy/sell prices,
- local demand,
- shop stock refresh,
- caravan schedules,
- item rarity pricing,
- faction discounts,
- reputation,
- trade rumors,
- route danger,
- cargo weight,
- market requests.

Trading progression:

- buy underpriced goods,
- sell needed goods at remote shops,
- fulfill contracts,
- unlock better route info,
- hire guards or caravans,
- invest in shop standing,
- earn profession XP.

Trader class/profession hooks:

- appraise unknown items,
- reveal demand,
- reduce travel tariffs,
- negotiate dungeon salvage rights,
- unlock delivery quests,
- buy rare maps.

## Items To Include

Core categories:

- weapons,
- armor,
- tools,
- profession tools,
- food,
- water containers,
- potions,
- antidotes,
- camping gear,
- light sources,
- crafting materials,
- dungeon keys,
- maps,
- scrolls,
- trade goods,
- quest items,
- trophies,
- monster parts,
- building materials.

Rarity:

| Rarity | Example Use |
|---|---|
| Common | starter zone gear, food, basic materials |
| Uncommon | outer zone resources, improved tools |
| Rare | dungeon loot, specialized profession materials |
| Epic | deep-zone drops, boss components |
| Legendary | major quest/dungeon rewards |
| Mythic | obelisk, ancient systems, endgame zones |

Affixes:

- Sturdy,
- Keen,
- Warm,
- Light,
- Heavy,
- Silent,
- Burning,
- Chilled,
- Venomous,
- Restorative,
- Lucky,
- Durable,
- Mana-Touched,
- Dungeon-Bound,
- Obelisk-Marked.

## Mobs And World Scaling

Mobs should scale by zone, biome, time of day, dungeon type, and player noise.

Starter zone:

- weak pests,
- wolves/boars equivalent,
- slimes,
- small undead/echoes,
- low-risk bandits or scavengers.

Outer zones:

- pack predators,
- armored insects,
- stronger humanoids,
- elemental slimes,
- burrowers,
- dungeon spillover mobs.

Deep zones:

- elite beasts,
- magic predators,
- corrupted adventurers,
- dungeon-born constructs,
- rare named mobs,
- bosses and wandering threats.

Night rules:

- safe roads become less safe,
- light reduces attack chance,
- cooking smells can attract mobs,
- noise increases detection,
- some materials only spawn at night,
- some classes/professions benefit from night travel.

## Obelisk And Class Examples

Common class offers:

- Swordsman,
- Archer,
- Novice Mage,
- Scout,
- Guard,
- Healer,
- Brawler,
- Herbalist.

Uncommon:

- Shieldbearer,
- Flame Adept,
- Tracker,
- Skirmisher,
- Field Medic,
- Trap Hunter,
- Apprentice Summoner.

Rare:

- Ranger,
- Warden,
- Elementalist,
- Duelist,
- Cleric,
- Beast Tamer,
- Artificer,
- Spellblade.

Epic:

- Stormbinder,
- Iron Vanguard,
- Deep Pathfinder,
- Rune Artificer,
- Wildspeaker,
- Shadow Duelist,
- Relic Seeker.

Legendary:

- Horizon Ranger,
- Oath Guardian,
- Starfire Magus,
- Beastbound Sovereign,
- Saint of the Road,
- Master Artificer.

Mythic:

- Worldwalker,
- Obelisk Chosen,
- Aegis of the Seventh Gate,
- Worldflame Heir,
- Fatebound Architect.

## Professions

Starter professions:

- Blacksmith,
- Alchemist,
- Cook,
- Builder,
- Trader,
- Farmer,
- Tailor,
- Scribe.

Advanced professions:

- Enchanter,
- Cartographer,
- Beast Breeder,
- Dungeon Appraiser,
- Herbalist,
- Jeweler,
- Engineer,
- Innkeeper,
- Caravan Master,
- Runecarver.

Profession assignment can be:

- chosen after class selection,
- recommended by the obelisk,
- unlocked by guild apprenticeship,
- changed through cost and time,
- expanded through rare licenses.

## What The Guide Still Needs

This guide should keep expanding until these are specified:

- exact scoring formulas,
- all score category weights,
- stat formulas and breakpoints,
- complete Day 1-7 objective list,
- obelisk UI flow,
- class rarity tables,
- class trees,
- profession trees,
- cooking buffs and food spoilage tables,
- world event tables,
- guild-board rumor/escalation tables,
- starter zone map requirements,
- first dungeon layout requirements,
- travel event tables,
- camping recipes and camp upgrades,
- fatigue tuning,
- food spoilage rules,
- potion/status effect list,
- shop inventories,
- trade route model,
- quest reward model,
- mob families by zone,
- loot rarity tables,
- homestead realm unlocks and base expansion costs,
- split-screen UI rules,
- coop save/reward ownership rules,
- final naming pass for System/Ledger/Obelisk terminology,
- title condition/effect tables,
- affinity source/reward tables,
- dungeon result screen schema,
- rank gate tables,
- skill mastery definitions,
- first 20-minute playable slice,
- first 2-hour playable slice,
- validation checks for all data IDs.

## Implementation Targets

Data types to add or extend:

- `TrialScoreDefinition`
- `TrialScoreState`
- `ScoreCategoryDefinition`
- `PlayerOriginProfile`
- `SystemMessageDefinition`
- `SystemMessageChannel`
- `NotificationQueue`
- `TrialEvidenceProfile`
- `EvidenceEventDefinition`
- `EvidenceWeightTable`
- `ClassBiasRule`
- `ProfessionBiasRule`
- `TrialMilestoneDefinition`
- `XPChannelDefinition`
- `SkillMasteryDefinition`
- `LevelCurve`
- `ProgressionEvent`
- `ObeliskClassOfferDefinition`
- `ClassDefinition`
- `ProfessionDefinition`
- `ClassEvidenceRule`
- `RankGradeDefinition`
- `TitleDefinition`
- `TitleCondition`
- `TitleEffect`
- `AffinityDefinition`
- `AffinityScoreProfile`
- `AffinitySourceEvent`
- `AffinityThresholdReward`
- `SpellDefinition`
- `EnchantDefinition`
- `AdventurerRankDefinition`
- `GuildRankDefinition`
- `RegionReputationDefinition`
- `DungeonDefinition`
- `DungeonFamilyDefinition`
- `DungeonResultDefinition`
- `DungeonThreatRankDefinition`
- `ExpeditionTemplateDefinition`
- `PartyLoadout`
- `ExpeditionSupplyManifest`
- `TravelRouteDefinition`
- `TravelEventDefinition`
- `CampDefinition`
- `FatigueState`
- `ShopDefinition`
- `TradeGoodDefinition`
- `QuestContractDefinition`
- `WorldEventDefinition`
- `GuildBoardEntryDefinition`
- `RumorDefinition`
- `NpcPartyDefinition`
- `EncounterDefinition`
- `FoodBuffDefinition`
- `RecipeBuffDefinition`
- `HomesteadRealmDefinition`
- `BaseExpansionDefinition`
- `BaseStatDefinition`
- `PlayerProfileState`
- `PartyState`
- `CoopRewardPolicy`

Event hooks:

- OnTrialStarted
- OnTrialDayChanged
- OnSystemMessageQueued
- OnScoreCategoryChanged
- OnTrialEvidenceAdded
- OnClassEvidenceChanged
- OnGradeForecastChanged
- OnObeliskSummoned
- OnClassOffersGenerated
- OnClassSelected
- OnProfessionSelected
- OnXPChannelChanged
- OnSkillMasteryChanged
- OnTitleProgressChanged
- OnTitleAcquired
- OnAffinityChanged
- OnAffinityAwakened
- OnRankChanged
- OnTravelStarted
- OnTravelEventRolled
- OnCampStarted
- OnCampResolved
- OnFatigueChanged
- OnDungeonDiscovered
- OnDungeonEntered
- OnDungeonExited
- OnDungeonResultGenerated
- OnShopStockRefreshed
- OnWorldEventStarted
- OnWorldEventEscalated
- OnGuildBoardEntryAdded
- OnRumorResolved
- OnNpcPartyEncountered
- OnFoodBuffApplied
- OnHomesteadUnlocked
- OnBaseExpanded
- OnBaseStatChanged
- OnCoopPlayerJoined
- OnCoopPlayerLeft

Validation:

- every class has weights,
- every profession has progression actions,
- every XP channel has a level curve,
- every title has condition and effect policy,
- every affinity has sources and threshold rewards,
- every grade maps to rarity ceilings,
- every rank has gate definitions,
- every dungeon has travel requirements,
- every dungeon result references valid reward/progression definitions,
- every route has time and danger,
- every shop stock references valid items,
- every quest reward references valid definitions,
- every event has trigger, timer, severity, and consequence,
- every guild-board entry resolves to quest, rumor, event, or shop data,
- every food buff has duration, category, and stacking rule,
- every base expansion has cost and size delta,
- every coop quest defines ownership policy,
- obelisk cannot produce zero class offers,
- starter seven-day path cannot soft-lock.

## Immediate Next Slice

The next design-to-code slice should be data only:

1. Add `SystemMessageFeed` with Notice, Warning, Trial Evidence, Level Up,
   Title, Affinity, Quest, Dungeon, Party, and World Event channels.
2. Add score categories, `TrialScoreState`, and `TrialEvidenceProfile`.
3. Track action evidence for Day 1 and Day 2.
4. Add grade forecast.
5. Add basic XP channels: Character, Class, Profession, Skill Mastery, Guild.
6. Add class evidence and placeholder class definitions for Trailblade, Iron
   Warden, Hearthbound Acolyte, Stonehand Delver, Wildsign Ranger, Ashvein
   Pyromancer, Wayfarer, Oathbearer.
7. Add an obelisk offer generator with placeholder classes.
8. Add profession definitions for Blacksmith, Alchemist, Cook, Builder, Trader,
   Farmer, Miner, Fisher.
9. Add title definitions for First Night Survivor, Village Shield, Campfire
   Captain, Trail Cook, Goblin-Bane, Returned For Them.
10. Add affinity definitions for Ember, Tide, Root, Stone, Gale, Glimmer, Hearth.
11. Add travel prep data and expedition templates for the first dungeon.
12. Add first dungeon result screen data.
13. Add first world events: goblin raid chain, dangerous mob sighting, resource
   bloom, rival NPC team encounter.
14. Add food buffs for travel, combat, crafting, and fatigue recovery.
15. Add homestead realm data as locked post-obelisk content.
16. Add coop/player ownership assumptions to save data before implementing
   class assignment.
17. Add validator checks for grade, class, profession, XP channels, titles,
   affinities, dungeon travel, dungeon results, events, food buffs, homestead
   data, and coop ownership.
