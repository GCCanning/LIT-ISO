# LIT-ISO Visual, Implementation, And Integration Handoff

Audience: another AI agent joining the project.

Read first:

- `AGENTS.md`
- `Docs/INDEX.md`
- `Docs/HANDOFF_NEXT_SESSION.md`
- `Docs/IsoCoreFoundation/10_CleanRoom_Clone_Backlog.md`
- `Docs/IsoCoreFoundation/15_LitRPG_System_Bible.md`
- `Docs/IsoCoreFoundation/16_Future_Improvements_Roadmap.md`
- `Docs/IsoCoreFoundation/17_Seven_Day_Trial_Game_Bible.md`

## Core Game Direction

Build an original cozy isometric survival/crafting/building LitRPG. The player is
transmigrated into a System-governed world as an unclassed newcomer. For the
first seven days, every meaningful action is tracked. At the end of Day 7, an
obelisk presents class options based on a weighted calculation of the player's
actual behavior. Grade controls class rarity.

The game is not a menu-first class picker. It is an action-first identity
system. The player becomes what they practiced.

The current working names:

- **The Ledger**: the LitRPG System.
- **Unwritten**: transmigrated newcomers with no class yet.
- **Proving Week**: the first seven days.
- **Entries**: recorded actions.
- **Marks**: hidden behavioral tags.
- **Hearthclaim**: expandable pocket homestead/base realm.

These names can change later. The mechanics are the important part.

## Visual Perspective

The visual target is readable, cozy, and systemic. The game should feel like a
warm life-sim world with real expedition danger layered underneath.

Visual priorities:

- 2D isometric, clean silhouettes, strong tile readability.
- Cozy settlement/farming/crafting surfaces.
- Dangerous dungeons and wilderness zones that still match the same art
  language.
- Clear LitRPG UI feedback: System notices, ranks, titles, affinities, class
  offers, dungeon results.
- Original art only. ISO-Core/ISO-Tile references are system/scale references,
  not asset sources.

Do not copy pixels, audio, names, UI layouts, mobs, or content from ISO-Core.

### Isometric World Look

The world should be built around:

- ground tiles,
- road/path tiles,
- farms/crops,
- props,
- prefab building exteriors,
- separate instanced building interiors,
- dungeon tile families,
- wilderness biomes,
- ocean/boat tiles later.

Important visual rule: buildings should be footprint prefabs, not fully
freeform wall/roof assemblies for the first version. The player can place roads,
farms, fences, floors, decorations, crafting stations, storage, and terrain
improvements as tiles/objects. Functional buildings are crafted/earned as
prefabs with footprints.

Recommended building visual model:

- Cottage: 2x2 footprint, southeast/southwest facing.
- Smithy: 3x2 footprint.
- Tavern: 3x3 or 4x3 footprint.
- Barn: 3x3 footprint.
- Guild Hall: 4x4+ footprint.
- Dock: shoreline footprint.

Door interaction transitions to a mini interior map. Interiors should feel like
Stardew-like readable rooms in isometric form, with walls staged to avoid
occlusion.

### LitRPG UI Feel

The System UI is a major part of the genre feel. It needs to be legible,
compact, and frequent enough to make the player feel measured.

Core UI surfaces:

- System message feed.
- Status screen.
- Trial Evidence Log.
- Grade forecast.
- Class/profession screens.
- Skill/mastery screen.
- Title screen.
- Affinity screen.
- Guild quest board.
- Expedition prep screen.
- Dungeon result screen.
- Obelisk class ceremony screen.

Example notice tone:

```text
[System Notice]
Foreign soul detected.

[Trial Protocol Initiated]
Duration: 7 days.
Survive. Adapt. Act.
```

The UI should be formal and mythic, but not bloated. Short notices, readable
icons, and clear categories matter more than walls of text.

System message channels needed visually:

- Notice
- Warning
- Trial Evidence
- Level Up
- Skill Unlock
- Title Acquired
- Affinity Resonance
- Quest Update
- Dungeon Alert
- Party Event
- World Event

Split-screen requirement: HUD and System feed must work in compact per-player
panels. Long messages should go to a shared log.

### Art Asset Families Needed

First visual slice should need:

- player/unclassed avatar placeholder,
- weak starter mob,
- starter town ground/path tiles,
- campfire/bedroll/shelter props,
- obelisk/waystone,
- guild board,
- basic trees/bushes/rocks/herbs,
- road and field tiles,
- first dungeon tiles,
- chest/loot container,
- simple cottage exterior,
- simple cottage/tavern-like interior,
- first crafting stations: workbench, campfire, furnace/anvil placeholder,
- UI icons for stats, classes, professions, titles, affinities, warnings.

Dungeon visual families later:

- Root Cellar
- Mine Shaft
- Ruined Watchpost
- Flooded Shrine
- Beast Den
- Trial Vault
- Abandoned Workshop
- Deep Crypt
- Tide/Coastal Dungeon

Magic affinity visual families:

- Ember: warm fire, forging, courage.
- Tide: water, healing, fishing, weather.
- Root: growth, farming, binding.
- Stone: defense, mining, construction.
- Gale: speed, scouting, sailing.
- Glimmer: light, illusion, secrets.
- Umber: shadow, stealth, tracking.
- Aether: obelisks, teleportation, raw System magic.
- Hearth: food, comfort, camp safety, morale.

## Implementation Perspective

The canonical code lane is `IsoCore.Foundation`. Do not revive the retiring
legacy `Assembly-CSharp` world unless explicitly instructed.

Foundation invariants:

- Grid: `IsometricZAsY`, `cellSize (1,0.5,1)`.
- Transparency sort axis: `(0,1,-0.26)`.
- `TilemapRenderer.mode = Individual`.
- Height layers `Height_0..7` map to Unity layer `10+height` and sorting layer
  `10+height`.
- Movement is world-query based.
- Foot collider is trigger-only.
- `maxWalkStepHeight = 0`.

Respect AGENTS ownership:

- Codex owns `Assets/Scripts/IsoCoreFoundation/**`,
  `Assets/Scenes/IsoCoreFoundation.unity`,
  `Docs/IsoCoreFoundation/**`.
- Claude owns menu/art/integration paths listed in `AGENTS.md`.
- Shared files require small changes and coordination.

### First Data-Only Slice

Do not start with huge UI or combat refactors. The next implementation should
be data-first.

Add or define:

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

Initial content targets:

- classes: Trailblade, Iron Warden, Hearthbound Acolyte, Stonehand Delver,
  Wildsign Ranger, Ashvein Pyromancer, Wayfarer, Oathbearer.
- professions: Blacksmith, Alchemist, Cook, Builder, Trader, Farmer, Miner,
  Fisher.
- titles: First Night Survivor, Village Shield, Campfire Captain, Trail Cook,
  Goblin-Bane, Returned For Them.
- affinities: Ember, Tide, Root, Stone, Gale, Glimmer, Hearth.
- events: goblin raid chain, dangerous mob sighting, resource bloom, rival NPC
  party encounter.
- first dungeon: one small starter dungeon with travel prep data and result
  report data.

### Runtime Loop To Prove

The first playable LitRPG loop should be:

```text
Spawn as Unwritten
System grants provisional access
Survive/gather/fight/craft/explore during Proving Week
Trial Evidence records actions
Guild board produces small world events
Prepare for one dungeon expedition
Travel/camp/enter dungeon
Return with result screen, loot, XP, title/affinity progress
Reach Day 7
Obelisk evaluates evidence
Player chooses class and profession path
```

This loop matters more than breadth.

### Systems To Avoid Overbuilding Early

Avoid implementing these before the data spine exists:

- full freeform building/walls/roofs,
- large class trees,
- many dungeon biomes,
- online networking,
- full boat/ocean simulation,
- economy simulation,
- NPC town automation,
- complex procedural dungeon generation,
- permanent title stacking.

Prototype the data and feedback loop first.

## Integration Perspective

Integration has three main tracks:

1. **Foundation runtime integration**
   - Hook progression events to player actions.
   - Emit System messages from action events.
   - Track Trial Evidence per player.
   - Keep player-specific state separate from shared world state.

2. **Content/data integration**
   - Store class/profession/title/affinity/dungeon/event definitions as
     ScriptableObjects or the repo's existing Foundation data pattern.
   - Add validators so invalid IDs fail early.
   - Add placeholder definitions before adding UI polish.

3. **Visual/art integration**
   - Use original generated/authored assets only.
   - Keep footprints, tile pivots, sorting, and scale consistent with
     Foundation isometric rules.
   - Treat prefab building exteriors and interiors as linked instances.

### Event Hooks Needed

Useful hooks:

- `OnTrialStarted`
- `OnTrialDayChanged`
- `OnSystemMessageQueued`
- `OnTrialEvidenceAdded`
- `OnGradeForecastChanged`
- `OnClassOffersGenerated`
- `OnClassSelected`
- `OnProfessionSelected`
- `OnXPChannelChanged`
- `OnSkillMasteryChanged`
- `OnTitleProgressChanged`
- `OnTitleAcquired`
- `OnAffinityChanged`
- `OnAffinityAwakened`
- `OnRankChanged`
- `OnTravelStarted`
- `OnTravelEventRolled`
- `OnCampResolved`
- `OnDungeonEntered`
- `OnDungeonExited`
- `OnDungeonResultGenerated`
- `OnWorldEventStarted`
- `OnWorldEventEscalated`
- `OnGuildBoardEntryAdded`
- `OnHomesteadUnlocked`
- `OnBaseExpanded`
- `OnCoopPlayerJoined`
- `OnCoopPlayerLeft`

### Validation Needed

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
- every food buff has duration, category, and stacking rule,
- every homestead expansion has cost and size delta,
- every coop quest defines ownership policy,
- obelisk cannot generate zero class offers,
- starter seven-day path cannot soft-lock.

## Coop And Split-Screen Requirements

Design for multiple players from the start, even if online networking is later.

Player-specific:

- Trial Evidence
- class
- profession
- stats
- skill mastery
- titles
- affinities
- inventory/personal bag
- buffs/debuffs
- fatigue/hunger
- obelisk class offers

Shared:

- world seed
- day/time
- discovered map where appropriate
- active world events
- guild board
- dungeon state
- homestead/base
- shared storage
- guild rank
- town upgrades

Rule: one combat-focused player must not determine everyone else's class. Each
player gets individual class offers.

## Building/Homestead Recommendation

Use a hybrid building model:

- Tile placement for roads, paths, farms, fences, floors, decor, crops, and
  terrain improvements.
- Prefab footprints for functional buildings.
- Instanced interior maps for enterable buildings.
- Building upgrades swap prefab/interior templates.
- Roads should affect NPC movement, visitor chance, delivery speed, and town
  rating.

First implementation target:

1. Place/remove road tile.
2. Place/remove 2x2 cottage prefab.
3. Rotate cottage southeast/southwest.
4. Validate footprint and doorway.
5. Interact with doorway.
6. Teleport to small interior scene/map.
7. Return to original doorway.
8. Save/load building instance and interior state.

## Immediate Recommendation For Next AI

Start with the data spine, not visuals or giant feature work:

1. Read `17_Seven_Day_Trial_Game_Bible.md`.
2. Inspect existing Foundation data/progression scripts.
3. Add minimal definitions for System messages, Trial Evidence, XP channels,
   titles, affinities, and dungeon result data.
4. Add validators.
5. Add placeholder content.
6. Only then expose a simple debug UI/feed.

The goal is for one action, such as chopping wood, cooking food, blocking an
attack, or discovering a landmark, to produce:

```text
Action -> Evidence -> XP/Title/Affinity Progress -> System Message -> Saved State
```

Once that chain works, the rest of the LitRPG game can grow coherently.
