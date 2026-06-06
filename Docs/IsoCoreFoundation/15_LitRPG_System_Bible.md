# LIT-ISO LitRPG System Bible

> Draft v0.1, clean-room design pass.  
> Purpose: define the original LitRPG direction for Foundation systems, Claude UI
> binding, quest/content naming, itemization, biomes, tiles, and progression.

## Clean-Room Promise

LIT-ISO should learn from the shape of successful LitRPG and progression fantasy,
not copy any book, serial, game, class, faction, mechanic text, lore, or asset.
Royal Road and Kindle/Audible trends show the audience likes visible progression,
class identity, clever system use, crafting, base-building, and long arcs. LIT-ISO's
original angle is cozy, tactile progression: the player grows by making a wild
place warmer, safer, stranger, and more personal.

Research surfaces checked:

- Royal Road LitRPG and progression discovery pages.
- Royal Road crafting, farming, and base-building LitRPG examples.
- LitRPG Vault tag pages for base-building and progression fantasy.
- Current LitRPG trope/worldbuilding summaries and community recommendation trends.

Useful references:

- https://www.royalroad.com/fictions/best-rated?genre=litrpg
- https://www.royalroad.com/fiction/64085/stubbing-jan-20-the-chronicles-of-emberstone-farm
- https://litrpgvault.com/tags/base-building/
- https://litrpgaudiobook.com/guides/worldbuilding-tropes

## Creative North Star

**LIT-ISO is a cozy isometric survival-crafting LitRPG where your character sheet is
also the world you build.**

Power is visible in:

- the path you paved,
- the soup you learned to cook,
- the bridge that opened a new biome,
- the field that remembers your care,
- the lanterns that make night gentler,
- the workshop that turns scraps into named gear,
- the strange mobs that stop attacking because you understood their ecology.

The player fantasy is not "become the strongest killer." It is "become the person
this valley can gather around."

## Market Lessons To Keep

| Pattern | LIT-ISO Response |
|---|---|
| LitRPG readers want visible progression quickly. | Give level, class, first skill XP, first recipe, and first home upgrade in the opening 20 minutes. |
| Cozy cannot mean consequence-free. | Use storms, crop blight, night hazards, winter prep, broken bridges, and neighbor requests as pressure. |
| Crafting/base-building can carry progression fantasy. | Make workstations, rooms, paths, storage, lights, and civic buildings mechanically meaningful. |
| Non-combat progression is attractive when it is deep. | Farming, building, cooking, cartography, creature handling, and trade should have real trees. |
| Long arcs work when there are frequent small rewards. | Daily reward cadence: one material gain, one skill tick, one discovery, one request step. |
| Readers like system exploitation. | Let players combine traits: soil + crop + weather + meal + tool mod + class perk. |
| Stat dumps can become exhausting. | Keep HUD simple; put detail behind panels and codex pages. |

## Core Stats

The UI-facing stats stay classic and readable:

| Stat | Meaning | Primary Gameplay |
|---|---|---|
| STR | Strength | tool impact, hauling, block breaking, melee pushback |
| DEX | Dexterity | movement, dodge windows, gathering speed, precise harvesting |
| INT | Intelligence | mana pool, recipe discovery, machines, rune puzzles |
| VIT | Vitality | health, stamina recovery, weather resistance |
| DEF | Defense | damage reduction, armor use, trap safety |
| LUCK | Luck | rare drops, crop mutations, event odds, quality spikes |

Derived values:

| Value | Formula Direction | Use |
|---|---|---|
| HP | VIT plus gear and food | survival and combat |
| MP | INT plus relics and meals | active skills, wards, runes |
| XP | broad activity milestones | character level |
| Stamina | VIT plus food plus comfort | tools, sprinting, building |
| Carry | STR plus bags plus settlement upgrades | hauling and inventory pressure |
| Comfort | home decor plus meals plus bonds | rest buffs and NPC visits |
| Threat Calm | LUCK plus ecology quests plus warding | reduces local aggression |

## Cozy Mastery Facets

Facets are not replacement stats. They are progression tags used by skills, quests,
recipes, and dialogue.

| Facet | Theme | Examples |
|---|---|---|
| Hearth | care, food, home, morale | better meals, rest buffs, NPC trust |
| Hand | craft precision, tool feel | quality, repair speed, workstation bonuses |
| Root | farming, soil, ecology | crop traits, animal trust, seasonal resistance |
| Spark | invention, runes, curiosity | recipe clues, machines, shrine puzzles |
| Step | scouting, routes, movement | map reveal, shortcuts, safe paths |
| Grit | endurance, safety, resolve | weather, armor, hazards |
| Glow | charm, story presence, attunement | spirits, dialogue, festivals |

## Core Loop

| Phase | Player Action | Reward |
|---|---|---|
| Dawn Prep | eat, check board, pick tool loadout, choose quest pin | buffs, route clarity |
| Work | gather, farm, mine, chop, fish, build, craft | materials, skill XP, small discoveries |
| Explore | push into woods, caves, ruins, beaches, snow paths | map unlocks, rare nodes, lore |
| Encounter | avoid, soothe, trap, fight, cleanse, or befriend mobs | essences, recipes, ecology shifts |
| Return | refine goods, upgrade home, fulfill requests | settlement XP, class XP, trust |
| Evening | cook, decorate, journal, talk, plan crafts | story flags, rest quality, next-day bonuses |

## Character Leveling

Character level is broad and slow enough to feel important. Skill levels are faster.

| Level Range | Name | Expected Unlocks |
|---:|---|---|
| 1-5 | New Arrival | starter Calling, basic tools, first home upgrades |
| 6-15 | Settled Hand | class action, tier 2 tools, first dungeon, NPC helpers |
| 16-30 | Valley Artisan | class branch, civic buildings, advanced stations |
| 31-50 | Hearth Luminary | region transformations, masterwork gear, rare crops |
| 51+ | Mythwarm Steward | capstones, legendary quests, world-state changes |

Reward cadence:

| Time Investment | Reward |
|---|---|
| 5 minutes | material gain, small discovery, request progress |
| 20 minutes | skill level, recipe, map reveal, home improvement |
| 1 session | building upgrade, quest completion, new route |
| 3-5 sessions | class branch, settlement milestone, story beat |
| long term | region transformation, masterwork item, capstone |

## Classes: Callings

Callings are identities and bonuses, not cages. Any player can learn any skill.

| Calling | Fantasy | Main Stats | Role |
|---|---|---|---|
| Hearthwarden | cook, caretaker, safe-night planner | VIT, LUCK | settlement anchor |
| Greenhand | farmer, grower, animal friend | VIT, DEX | food and living resources |
| Stonewright | builder, mason, road-maker | STR, DEF | base expansion |
| Threadsmith | crafter, tailor, tool tuner | DEX, INT | gear and utility |
| Pathlighter | explorer, mapper, ruin-reader | DEX, LUCK | discovery |
| Bramblebound | wild-magic herbalist, denkeeper | INT, LUCK | ecology and mobs |
| Lanternblade | protector, patrol fighter | STR, DEF | defense and delving |

### Calling Progression

| Tier | Level Range | Unlock Style |
|---|---:|---|
| Novice | 1-5 | starter perk, starter recipe set, title |
| Adept | 6-15 | class action and identity passive |
| Artisan | 16-30 | branch choice, advanced recipes, settlement role |
| Luminary | 31-50 | signature ability and rare structures |
| Mythwarm | 51+ | capstone that changes regional behavior |

### Branches

| Calling | Branch A | Branch B | Branch C |
|---|---|---|---|
| Hearthwarden | Cook | Caretaker | Festival Host |
| Greenhand | Cropkeeper | Beastfriend | Orchard Sage |
| Stonewright | Mason | Roadmaker | Hall Builder |
| Threadsmith | Toolwright | Weaver | Relic Tinker |
| Pathlighter | Scout | Cartographer | Ruin Guide |
| Bramblebound | Herbalist | Denkeeper | Wildspeaker |
| Lanternblade | Patroller | Shieldhand | Gloombreaker |

### Capstones

| Calling | Capstone |
|---|---|
| Hearthwarden | Day Feast: one meal sets a whole-day theme for the settlement. |
| Greenhand | Remembering Fields: fields mutate crops based on care history. |
| Stonewright | Civic Landmark: build one regional structure that permanently changes services. |
| Threadsmith | Storied Masterwork: a crafted item gains a name, history, and evolving trait. |
| Pathlighter | Safe Route: discovered paths become fast travel and safer NPC travel lines. |
| Bramblebound | Den Accord: pacified mob dens become resource biomes instead of hazards. |
| Lanternblade | Patrol Legend: patrol routes reduce threat and unlock heroic town events. |

## Skill Trees

| Skill | Activities | Unlocks |
|---|---|---|
| Foraging | herbs, mushrooms, berries, fibers | node reading, rare sprouts, preserve recipes |
| Woodcraft | chopping, shaping, planting trees | timber tiers, bridges, grove care, furniture |
| Mining | stone, ore, crystals, clay | reinforced tools, cellar rooms, furnace chains |
| Farming | soil, watering, crop rotation | crop traits, trellises, seed memory, greenhouses |
| Cooking | meals, preserves, drinks | buff meals, comfort feasts, NPC favorites |
| Crafting | tools, clothes, components | quality grades, mod slots, repair kits |
| Building | floors, walls, roofs, decor, utilities | room types, civic structures, defense layouts |
| Exploration | mapping, climbing, ruins | shortcuts, landmarks, hidden resources |
| Creaturecraft | mobs, animals, spirits | calm, lure, tame, relocate, den conversion |
| Warding | traps, lights, patrol posts | non-lethal defenses, threat shaping |
| Trade | requests, vendors, caravans | better prices, special orders, visitor schedules |
| Lorekeeping | journals, relics, dialogue | story quests, shrine upgrades, ancient recipes |

Skill node types:

| Node Type | Effect |
|---|---|
| Ease | reduces stamina, time, or material friction |
| Yield | improves quantity or quality |
| Insight | reveals hidden traits, recipes, or map clues |
| Expression | adds cosmetic, layout, or personalization options |
| Utility | unlocks tools, machines, stations, or movement |
| Harmony | improves NPC, animal, mob, or biome relationships |

Example perks:

- Gentle Gatherer
- Double Knot
- Warm Swing
- Soup Sense
- Tile Whisper
- Better Boots
- Soft Landing
- Kindling Touch
- Lucky Ladle
- Root Reader
- Friendly Critical
- Well-Fed Focus
- Lantern Step
- Moss Memory
- Craft Encore
- Quiet Hands
- Compost Whisper
- Field Memory
- Steady Hammer
- Bright Ward

## Item Quality And Tiers

Simple item tiers for UI:

| Tier | Name | Meaning |
|---:|---|---|
| 1 | Plain | functional baseline |
| 2 | Sturdy | better durability |
| 3 | Kindled | warm/cozy trait |
| 4 | Bright | efficiency or precision trait |
| 5 | Woven | multiple material traits |
| 6 | Runed | magic-lite or machine trait |
| 7 | Storied | named item with history |
| 8 | Mythwarm | rare masterwork tied to quest or landmark |

Craft quality labels:

- Bent
- Usable
- Balanced
- Fine
- Masterful
- Charmed
- Awakened
- Beloved

Material traits:

| Trait | Effect |
|---|---|
| Warm | improves comfort, cooking, cold resistance |
| Keen | improves tool precision and harvest quality |
| Stout | improves durability, DEF, building strength |
| Light | improves movement, stamina cost, DEX |
| Rooted | improves farming, soil, ecology |
| Bright | improves wards, lantern effects, INT |
| Lucky | improves rare outcomes, LUCK |
| Quiet | lowers mob aggression, improves stealth harvest |

## Tools

| Tool | Early Use | Later Identity |
|---|---|---|
| Handaxe | chop small wood | shape groves, harvest resin, carve beams |
| Mattock | soil and clay | terrace slopes, reveal buried roots, prep irrigation |
| Pick | stone and ore | tune crystal veins, open ruin locks |
| Sickle | grass and herbs | collect seed clouds, harvest fibers cleanly |
| Hammer | build and repair | mass-place patterns, reinforce structures |
| Lantern | light and safety | reveal echoes, calm mobs, mark paths |
| Satchel | carry goods | sort, preserve, route materials to storage |
| Fishing Rod | fish and reeds | lure water spirits, recover sunken relics |

Tool name seeds:

- Sprig Hatchet
- Brindlewood Axe
- Glowbark Cleaver
- Hearthpine Feller
- Moonwillow Axe
- Pebble Pick
- Tinmoss Pick
- Honeybronze Pick
- Glowquartz Pick
- Echo-Ore Delver
- Dewtine Hoe
- Reedflax Hoe
- Sunspool Cultivator
- Rootwake Hoe
- Skillseed Planter
- Wickknife
- Charm Awl
- Rune Mallet
- Hearth Tongs
- Threadloom Shuttle
- Questmark Chisel
- Glowglass Lens
- Memory Mortar
- Skillweave Needle
- Lanternbell Hammer

## Combat And Mob Handling

Combat exists, but it is only one answer.

| Path | Focus | Non-Lethal Option |
|---|---|---|
| Guard | shields, patrols, sturdy gear | drive mobs away |
| Skirmish | dodging, quick tools, terrain use | disable or exhaust |
| Warden | wards, traps, lights | contain and redirect |
| Beastwise | creature knowledge | calm, tame, relocate |
| Relic Arts | old tools, lantern effects | cleanse corruption |

Mob roles:

| Mob Type | Behavior | Player Options | Rewards |
|---|---|---|---|
| Skitterlings | steal loose resources | fence, scare, feed, trap | fiber, tiny gears, curios |
| Mucklings | spoil soil and ponds | cleanse, relocate, compost | rich loam, pond reeds |
| Embermotes | gather near heat | cool, contain, befriend | warm ash, kiln boost |
| Briarbacks | guard wild thickets | calm, fight, prune habitat | thornwood, rare seeds |
| Hollow Masks | ruin-bound echoes | light, solve memory, combat | relics, lore pages |
| Gloomknots | night hazard clusters | ward, disperse, convert | shadow sap, ward recipes |

## Building Progression

| Stage | Unlocks | Feeling |
|---|---|---|
| Camp | bedroll, fire, crate, work stump | survival |
| Homestead | cabin, garden plots, kitchen, storage | belonging |
| Workshop Yard | workbench, kiln, forge, loom, mill | capability |
| Hamlet Core | paths, notice board, guest beds, well | community |
| Civic Hearth | hall, shrine, market, watch posts | identity |
| Living Settlement | NPC routines, festivals, biome effects | home as system |

Building categories:

- Shelter: tent, bedroll, cabin, guest room, bath nook.
- Work: workbench, kiln, forge, loom, cooking pot, seed table.
- Storage: crate, chest, pantry, ore bin, seed cabinet, cold cellar.
- Travel: plank path, cobble path, ridge stair, bridge, signpost.
- Safety: lantern post, bell, fence, watch perch, wardstone.
- Community: notice board, well, tea table, market rug, festival arch.
- Ecology: compost bin, rain jar, bee box, animal pen, den marker.

## Farming Progression

| System | Early | Advanced |
|---|---|---|
| Soil | wet/dry plots | soil personality, compost blends |
| Seeds | buy and find | breed traits through care history |
| Seasons | crop restrictions | extenders, greenhouses, root cellars |
| Animals | feed and collect | trust skills, helper tasks |
| Irrigation | manual watering | channels, pumps, rain jars |
| Crop Traits | bigger yield | glow, hardy, fragrant, quickroot, sweetcore |

Crop trait seeds:

- Hardy
- Sweetcore
- Quickroot
- Glowveined
- Rainfed
- Sunhungry
- Frostkissed
- Twinpod
- Hearty
- Fragrant
- Giant
- Memory-Bound

## Quest Framework

Quest type rules:

| Quest Type | Purpose | Example |
|---|---|---|
| Hearth Quest | improves home comfort | build a proper kitchen before first frost |
| Craft Quest | teaches production chain | make lantern glass from sand, ash, and copper wire |
| Field Quest | teaches farming depth | restore tired soil with compost and clover |
| Path Quest | opens map routes | repair ridge stairs to reach high meadows |
| Creature Quest | reframes mobs | learn why mucklings gather near the old pond |
| Neighbor Quest | builds relationships | craft a weatherproof sign for a shy trader |
| Lore Quest | mystery and history | decode plaques beneath the ruined kiln |
| Civic Quest | settlement milestone | build a shared well and invite the first resident |

Reward types:

| Reward | Use |
|---|---|
| Recipe | new craft, build, or meal |
| Pattern | visual building or furniture style |
| Trait Seed | unlocks crop or item trait |
| Landmark Permit | unlocks civic construction |
| Neighbor Bond | helper ability or shop stock |
| Region Shift | changes resource tables or mob behavior |
| Memory Page | lore and hidden recipe clue |
| Calling Token | class branch or respec unlock |

## Main Story: The Quiet Light

The valley is recovering from an old overbright civilization that tried to bind
every useful thing into perfect systems. The player is not restoring that empire.
They are learning which parts to mend, which to compost, and which to leave asleep.

| Act | Theme | Main Unlock |
|---|---|---|
| Act 1: First Fire | shelter and belonging | homestead systems |
| Act 2: Green Roads | reconnection | routes, visitors, trade |
| Act 3: Old Lamps | history and consequence | ruins, relic crafting, mob origins |
| Act 4: Many Hearths | community | hamlet growth and civic buildings |
| Act 5: Chosen Glow | stewardship | regional transformations |

## Quest Chain Seeds

Starter and village:

- First Flame, First Field
- A Roof Before Rain
- The Hoe Remembers
- Soup for Six
- Fixing the South Path
- Every Hearth Needs a Bell
- The Cabbage That Leveled Up
- Welcome to Mosswake

Crafting progression:

- Thread, Twig, and Tin
- The Bench With Opinions
- Measure Twice, Enchant Once
- A Better Handle
- Hearthmade Standards
- The Guildmark Test
- Tools With Names
- Masterwork in Miniature

Farming and cozy:

- Seeds After Sundown
- Rain Debt
- The Moonplum Promise
- Bees in the Bluegrass
- Winterwool Preparations
- A Festival of Turnips
- The Scarecrow's Complaint
- Jam for the Road

System mystery:

- The Menu Behind the Mirror
- Skillseed Awakening
- A Level Too Many
- The Missing Attribute
- Patch Notes from Nowhere
- The Quest That Wouldn't Complete
- Unspent Points
- The Soft Cap
- The Name Beneath Your Name
- Respec at the Old Well

Exploration and dungeon:

- Lanterns Underfoot
- The Rootcellar Below
- Honeyshale Trouble
- Echoes in the Mine
- The Door That Requires Kindness
- Brindlecap Descent
- A Boss With No Treasure
- Claiming the Unclaimed

Late game:

- Worldroot Waking
- The Last Empty Tile
- Hearths Across the Valley
- The Eighth Height
- Beyond the Fogbutton Gate
- The Mythwarm Recipe
- A Village Worth Defending
- When the System Sleeps

## Biomes And Region Names

Primary Foundation biomes:

| Biome | Game Role | Tile Mood | Resource Focus | Mob Mood |
|---|---|---|---|---|
| Mosswake Meadow | starter plains | soft greens, flowers, paths | grass, herbs, berries, clay | gentle, tutorial |
| Brindlecap Woods | forest | moss, roots, amber leaves | wood, mushrooms, fiber, resin | skittering, hidden |
| Sunspool Fields | dry grassland | gold grass, warm dirt | wheat, reeds, copper, bees | fast, bright |
| Duskwick Marsh | wetland | peat, reeds, dark water | reeds, fish, wax, mud | tricky, slippery |
| Honeyshale Cliffs | stone hills | shale, exposed ore | stone, ore, crystal, goats | sturdy, territorial |
| Kindlestep Badlands | desert/scrub | sand, cinder, clay | sand, glass, cactus, warm ores | heat, burrowers |
| Winterwool Pines | snow forest | snow, needles, blue shadows | pine, frostsalt, wool, ice | cold, slow |
| Glowcap Grotto | cave/fungal | luminous fungi, wet stone | glowcaps, quartz, relics | strange, magical |

Region name pool:

- Emberfen Hollow
- Mosswake Meadow
- Brindlecap Woods
- Sunspool Fields
- Lanternroot Grove
- Duskwick Marsh
- Palegrain Downs
- Ambermelt Orchard
- Thimblethorn Thicket
- Hearthglass Hills
- Bluewhorl Wetlands
- Fogbutton Vale
- Cindermint Copse
- Starbarrow Prairie
- Honeyshale Cliffs
- Moonmulch Fen
- Brackenbell Forest
- Rustpetal Scrub
- Cloudmoss Rise
- Glowcap Grotto
- Ashberry Flats
- Silverdrip Ravine
- Kindlestep Badlands
- Deepmirth Caverns
- Winterwool Pines
- Quietquartz Ridge
- Sootwillow Bog
- Goldleaf Common
- Threadriver Basin
- Nightjar Dell

## Terrain And Tile Names

Core terrain groups:

| Group | Tile Names |
|---|---|
| Grass | Hearthgrass, Dewmoss, Mosswake Grass, Cloverplain, Amberleaf Litter, Brackenbed, Fallen Needles, Flowergrass |
| Dirt | Crumbloam, Ashloam, Sunbaked Loam, Rootwoven Soil, Pebblepatch, Weathered Dirt |
| Clay | Honeyclay, Runeclay, Redpan Clay, Softclay |
| Sand | Riverpearl Sand, Cindergrit, Palegrain Sand, Shellsand, Siltglass, Sunspool Sand |
| Snow | Frostfelt, Winterwool Snow, Moonchalk, Bluecrust Snow, Snowcap Needles, Quiet Frost |
| Soil | Rootwoven Soil, Compost Soil, Tilled Crumbloam, Wet Loam |
| Water | Reedmire, Bluewhorl Water, Moonlit Shallows, Siltwater |
| Stone path | Oldpath Cobble, Lanternstone, Wickstone Paving, Mossbrick |
| Wood floor | Hearthplank Flooring, Weathered Boardwalk, Brindlewood Plank, Moonwillow Deck |
| Special | Glowlichen Floor, Crystalroot Vein, Thornroot Tangle, Sporecap Ground, Mistpeat |

A1 terrain-top target names:

- grass_heartgrass_01..08
- dirt_crumbloam_01..06
- sand_riverpearl_01..06
- snow_frostfelt_01..08
- clay_honeyclay_01..04
- soil_rootwoven_01..03
- water_bluewhorl_01..04
- wood_hearthplank_01..08
- path_oldcobble_01..04

## Materials

Common:

- Kindling Reed
- Softwood Sprig
- Brindlewood
- Glowbark
- Hearthpine
- Moonwillow
- Threadroot
- Amber Resin
- Dewthread Fiber
- Reedflax
- Mossfelt
- Cloudcotton
- Bristlegrass Twine
- Pebble Iron
- Warm Copper
- Tinmoss Ore
- Honeybronze
- Blueglass Shard
- Glowquartz
- Sootstone
- Silverleaf Ore
- Stargrit
- Frostsalt
- Hearthcoal
- Lantern Oil
- Waxcap Wax
- Spore Silk
- Sunpetal Dye
- Cindermint Oil
- Quiet Bone

Rare:

- Levelglass
- Skillseed
- Memory Amber
- Questchalk
- Echo Ore
- Charmthread
- Hearthcore
- Dawnshard
- Moonmarrow
- Runeclay
- Fateflax
- Luckstone
- Glowmilk Sap
- Starlace Fiber
- Spirit Wax
- Nameleaf
- Resonant Bark
- Tiny Relic Gear
- Bound Ember
- Soft Mana Salt

## Crops And Food

Common crops:

- Buttonbarley
- Sunspool Wheat
- Dewbean
- Hearthroot
- Blush Turnip
- Honey Carrot
- Roundcap Cabbage
- Lantern Pea
- Thimble Onion
- Brindle Corn

Fruit:

- Amberapple
- Moonplum
- Cinderpear
- Bluebell Berry
- Glowfig
- Honeyquince
- Duskwick Grape
- Winterwool Peach
- Starberry
- Mossmelon

Magical crops:

- Skillseed Sprout
- Rune Pumpkin
- Questleaf Herb
- Mana Mint
- Hearthblossom
- Memory Sage
- Fateflax
- Glowcap Mushroom
- Charmroot
- Levelbean

Cooked foods:

- Hearthroot Stew
- Sunspool Loaf
- Amberapple Tart
- Dewbean Skillet
- Glowfig Jam
- Moonplum Porridge
- Honeycarrot Soup
- Lantern Pea Mash
- Brindlecorn Cakes
- Skillseed Tea
- Mossmelon Cooler
- Cindermint Biscuits
- Buttonbarley Bowl
- Hearthguard Hotpot
- Starberry Shortcake

## Weapons And Adventuring Gear

- Training Ladle
- Bramble Baton
- Hearthguard Pan
- Glowcap Wand
- Reedbow
- Wickshort Sword
- Amberhook Spear
- Moonchalk Staff
- Honeybronze Buckler
- Lanternshot Sling
- Cindermint Dagger
- Stargrit Mace
- Runed Garden Fork
- Kindling Rapier
- Echo Lantern Shield

## Mobs

Friendly and neutral:

- Puffmole
- Dewbun
- Lantern Snail
- Mossback Hen
- Buttonboar
- Thimblegoat
- Honeywing Bee
- Woolcap Ram
- Brindlecalf
- Glowfin Minnow
- Rootnose Piglet
- Pebbleback Tortoise
- Cabbage Sprite
- Hearthmoth
- Cloudmoss Grazer
- Belltoad
- Reedwhisker
- Moonplum Bat
- Softshell Beetle
- Amberkit

Hostile low tier:

- Sootmite
- Thornskitter
- Mire Nipper
- Bracken Imp
- Pebble Gnawer
- Ashcap Sporeling
- Wickrat
- Grudge Grub
- Rootsnare
- Mudblink

Hostile mid tier:

- Glowfang
- Cinderback Boar
- Hollowcap Shambler
- Rune-Marked Wisp
- Thornmantle Stalker
- Siltjaw Snapper
- Emberfen Wretch
- Bristle Revenant
- Honeyshale Golem
- Memory Leech

Hostile high tier:

- Level Eater
- Questless Knight
- Echo Husk
- Starved Relic
- Lanternless Shade
- Worldroot Maw
- Moonmarrow Giant
- Fatebound Harrier
- Deepmirth Warden
- Name-Hollowed Mage

Elite and boss seeds:

- The Bramble Baron
- Old Sootroot
- Mallowfen the Stuck
- The Lantern-Eater
- Captain No-Level
- Grudgewick Matron
- The Honeyshale Colossus
- Bellmouth of the Deepmirth
- Sir Hollowtitle
- The Unclaimed Reward
- Thatch-Crowned Mirelord
- The Echo Under the Stairs
- Mother Glowcap
- Cinderjaw the Warm
- The Rootbound Registrar

## Dungeons And Encounters

Cozy-adjacent dungeons:

- The Rootcellar Below
- Brindlecap Burrow
- The Old Guild Pantry
- Hearthglass Mine
- Lanternroot Tunnels
- The Forgotten Apiary
- Dewmoss Drain
- The Moonchalk Quarry
- Sootwillow Sink
- Cabbage Sprite Warrens

Dangerous dungeons:

- Deepmirth Descent
- The Hollow Ledger
- Trial of the Unlit Gate
- The Starbarrow Steps
- Echo-Ore Shaft
- The Questless Keep
- Waxcap Catacombs
- The Levelglass Vault
- Thornmantle Maze
- Moonmarrow Hollow

World encounters:

- Wandering Recipe Trader
- Fallen Skillstone
- Overgrown Shrine
- Runaway Crafting Bench
- Midnight Crop Bloom
- Lost Caravan Kettle
- Lantern Moth Swarm
- Tiny Rift in the Compost
- Rain-Sung Meteor
- The Talking Scarecrow Trial

## Factions And Settlements

Factions:

- Hearthbinders Guild
- The Levelwrights
- Lanternkeepers
- Root and Rune Society
- Brindlecap Foragers
- The Quiet Cartographers
- Waxmark Crafters
- Starbarrow Rangers
- The Skillseed Circle
- Hollow Ledger Company
- Moonspool Weavers
- The Copperpan Compact
- Emberfen Wardens
- Guild of Small Repairs
- The Unclaimed

Villages and places:

- Mosswake
- Hearthmere
- Brindlewick
- Lanternrest
- Tinbell Crossing
- Ambermill
- Dewpost
- Kindling Row
- Sunspool Hamlet
- Moonmulch
- Wickford
- Bellroot
- Glowfen
- Starbarrow
- Cabbage End

NPC seeds:

- Toma Hearthbell
- Nessa Reedwick
- Bram Button
- Elowen Tinmoss
- Pippa Glowfen
- Orrin Kettlemark
- Sella Moonspool
- Jun Brindle
- Mara Softstep
- Fenna Waxcap
- Cob Vale
- Lio Amberhand
- Mira Questleaf
- Dax Copperpan
- Iven Rootwell
- Bessa Woolwick
- Noll Brightbarley
- Tilly Threadroot
- Rune Halfsong
- Ada Lanternby

## UI And System Message Tone

System messages should be brief, warm, and useful. Avoid long stat walls unless the
player opens a detail screen.

Examples:

- Calling awakened: Greenhand.
- STR increased by 1. Heavy tools feel lighter.
- New recipe insight: Hearthroot Stew.
- The field remembers steady watering.
- Mosswake Meadow feels safer after sunset.
- Your Sprig Hatchet gained the Warm trait.
- Quest updated: A Roof Before Rain.
- Region shift: Brindlecap Woods threat decreased.

HUD priorities:

- HP, MP, XP, Level.
- Hotbar and selected tool.
- Compact quest pin.
- Optional day/weather/season.
- Status icons, not text paragraphs.

System page priorities:

- Class and title.
- STR/DEX/INT/VIT/DEF/LUCK.
- Current Calling XP and branch.
- Top 3 active buffs.
- Recent unlocks.

## Implementation Roadmap

### R1: UI Contract Content

- Lock `FoundationBootstrap.Stats` as the live source for HP/MP/XP/Level and six stats.
- Add class and title values to the character sheet.
- Add simple level-up event and XP gain source.

### R2: Skills And Callings

- Add `CallingDefinition` and `SkillDefinition` data.
- Start with 7 Callings and 12 skill trees.
- Award skill XP from existing harvest, craft, place, farm, and mob events.

### R3: Quests

- Add `QuestDefinition`, `QuestObjective`, and `QuestProgress`.
- Ship the starter chain:
  - First Flame, First Field
  - A Roof Before Rain
  - Thread, Twig, and Tin
  - Fixing the South Path
  - The Rootcellar Below

### R4: Content Naming Pass

- Rename or alias placeholder content toward this bible.
- Keep item ids stable and machine-readable, e.g. `hearthroot`, `warm_copper`,
  `sprig_hatchet`, `mosswake_grass_01`.

### R5: Biome And Tile Pass

- Implement A1 terrain tops using the tile groups above.
- Align biome resource tables with named region roles.
- Add biome-specific mob tables.

### R6: Save/Load

- Save character stats, calling, skill XP, quest progress, inventory, modified cells,
  placed objects, crops, day/night, and region shifts.

## Immediate Codex Tasks Suggested

1. Land the LitRPG stats source cleanly.
2. Add a small `CallingDefinition` data type and starter Calling selection.
3. Add XP events for harvest, craft, place, farm, and mob defeat/calm.
4. Implement the starter quest chain as data, even if UI display remains simple.
5. Use A1 tile names when generating or importing terrain-top assets.

## Immediate Claude Tasks Suggested

1. Bind HUD and System page to `FoundationBootstrap.Stats`.
2. Show class/title/stat layout using the Callings above.
3. Keep quest UI compact: pinned quest, objective count, and reward preview.
4. Use the UI tone examples for notifications.
5. Leave biome/terrain content implementation to the Codex/Foundation lane.

