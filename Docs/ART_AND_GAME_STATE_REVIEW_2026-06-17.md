# LIT-ISO Game-State and Art Production Review

Audit date: 2026-06-17  
Scope: canonical `IsoCore.Foundation` runtime, promoted runtime art, current working tree, and art needed for ChatGPT-assisted generation.

## Executive verdict

LIT-ISO is no longer a systems prototype. The Foundation runtime has enough connected systems for a real vertical slice: procedural biomes, harvesting, farming, crafting, building, camping, dungeons, interiors, progression, inventory/equipment, mobs, townsfolk scaffolding, menus, maps, weather, audio hooks, and save/load infrastructure.

The visual layer is now the limiting factor.

The strongest existing art is the PixelLab terrain/prop library. The weakest areas are:

1. Inventory and crafting icons: only 8 of 58 currently defined item IDs have matching runtime icons.
2. Characters and NPCs: current generated player sheets are crude front-facing placeholders and do not match the detailed isometric world art.
3. Creatures: slime has a real animation set; deer, fox, bandits, adventurers, villagers, and merchants do not have equivalent production art.
4. Crops: carrot and wheat grow as runtime color/shape representations rather than authored stage sprites.
5. UI symbols: the panel skin exists, but most actions, stats, tabs, skills, abilities, status effects, map markers, and item categories lack a coherent icon family.
6. Visual consistency: the current menu background is painterly/illustrative while the game world is crisp isometric pixel art.
7. Import consistency: runtime tiles are consistently PPU 32, but promoted props use mixed PPU values and many promoted sprites use bilinear filtering/compression instead of the point-filtered, uncompressed target.

The next art sprint should not create more terrain variants. It should close player-facing holes in this order:

1. Current inventory icon set.
2. Player/NPC animation set.
3. Creature set.
4. Crop growth stages.
5. Ability, skill, status, and UI icon family.
6. Combat/harvest/building VFX.
7. Pixel-art menu/class-assignment backgrounds.

## Verified current state

- `dotnet build IsoCore.Foundation.csproj --no-restore`: passes with 0 warnings and 0 errors.
- Canonical scene: `Assets/Scenes/IsoCoreFoundation.unity`.
- Canonical runtime: `IsoCore.Foundation`.
- Current branch during audit: `feat/biome-asset-wiring`.
- Working tree is heavily modified and contains uncommitted promoted art, code, package imports, and generated/review outputs. This review did not modify those files.
- Runtime art roots inspected:
  - `Assets/Resources/Tiles`: 136 PNGs.
  - `Assets/Resources/Decorations`: 234 PNGs.
  - `Assets/Resources/Items`: 8 PNGs.
  - `Assets/Resources/UI`: 39 PNGs.
  - `Assets/Resources/Characters`: 7 PNG sheets plus a large layered-character data tree.
  - `Assets/Resources/Enemies`: one complete slime animation family.
- Foundation currently defines 58 inventory/equipment IDs that resolve icons by exact filename from `Assets/Resources/Items/<itemId>.png`.

## Current game-state assessment

### Strong enough for a vertical slice

- World generation and biome selection.
- PixelLab terrain pools for plains, forest, snow, mountain, beach, farming, planks, water, and dungeon surfaces.
- Harvest nodes and biome-specific prop placement.
- Inventory, hotbar, equipment slots, storage, crafting, fuel/timed recipes, and placeables.
- Farming loop with carrot and wheat.
- Day/night, weather visuals, ambient particles, campfire glow, and music/ambient hooks.
- Camping/ward mechanics and rest.
- Dungeons, portals, rooms, rewards/history, and pocket interiors.
- Progression data: stats, skills, affinities, callings/classes, evidence, quests, and trial structures.
- Town/building art coverage is substantially ahead of the currently playable town systems.

### Present but visually incomplete

- Player character and character creator.
- Bandits, adventurer parties, villagers, and merchants.
- Deer and fox.
- Equipment appearance and held-item visuals.
- Crop stages.
- Combat readability and ability effects.
- Inventory/crafting readability.
- Status effects and progression feedback.
- Building placement previews and construction-state art.
- Dungeon theme identity beyond floor/portal/prop basics.

### Still structurally incomplete or placeholder-facing

- Some UI adapters still contain placeholder behavior.
- Several Foundation objects retain runtime box/cube fallbacks when a sprite does not resolve.
- Humanoid mobs explicitly fall back to colored blobs without character sheets.
- Crops interpolate colors and scale rather than switching authored growth sprites.
- The current item-icon fallback is an empty/faint slot.
- The menu background does not visually match the shipping art direction.

## Existing art review

### Terrain

Status: good breadth, medium consistency, low priority for new generation.

The promoted terrain set covers the core biome surfaces and has enough variants to avoid obvious repetition. The strongest families are plains, snow, farming, planks, dungeon stone, and water. The contact sheets show a coherent chunky-isometric vocabulary, but there are still differences in:

- top-face height and cube-side depth;
- pixel density;
- contrast and saturation;
- whether a tile reads as a flat surface, raised block, or miniature terrain diorama;
- lighting direction and edge highlights.

Recommendation: stop broad terrain generation. Curate one approved subset per semantic role and only generate specific missing transitions, cliffs, shore edges, paths, and construction surfaces.

### Props and buildings

Status: broad and visually strong, but requires import normalization and runtime curation.

Existing coverage includes vegetation, rocks, ore nodes, stations, lights, tents, town stalls, guild/library/tavern props, ranked buildings, docks, boat art, and dungeon objects. These are the best visual assets in the project.

Issues:

- Mixed PPU values across promoted props.
- Many promoted sprites use bilinear filtering and compressed texture import settings.
- Some silhouettes are highly detailed while older props are much simpler.
- Several “candidate” or duplicate variants remain in review/generated folders.
- Some world props need distinct inventory-icon versions rather than using a scaled-down prop.

Recommendation: preserve the current PixelLab prop style as the visual reference set. Normalize import settings and choose one canonical variant per stable asset ID before generating more props.

### Player character

Status: replacement required.

The four generated `LitIsoCreator_*_512x1024` sheets are functional contract tests, not shipping art. They are front-facing, extremely simple, and visually incompatible with the isometric terrain and high-detail props. They also do not provide the animation breadth needed for harvesting, tools, combat, casting, damage, death, resting, fishing, or interaction.

The runtime currently consumes an 8-row by 4-frame sheet. Any replacement must either preserve that contract or be paired with an animator change.

Recommendation: generate one high-quality neutral base character first, prove idle/walk in all required directions, then build modular hair, skin, clothing, armor, weapons, and accessories around the approved proportions.

### Mobs and NPCs

Status: critical gap.

Slime has idle, move, attack, hurt, and death art. The current content set also includes deer, fox, bandit, four adventurer roles, villagers, merchants, and a bandit campfire. These do not have comparable production animation families.

Recommendation: use a shared animation contract:

- four directions minimum;
- idle: 4 frames;
- walk: 4 frames;
- attack/action: 4 frames;
- hurt: 2 frames;
- death: 4 frames;
- transparent background;
- bottom-center pivot;
- no baked ground shadow.

For humanoids, share the player skeleton/proportions so equipment and animation work can be reused.

### Crops and plants

Status: major gameplay-readability gap.

Only carrot and wheat are currently plantable, and each needs authored growth stages:

- carrot: 3 stages;
- wheat: 4 stages.

The wider game direction also needs potato, pumpkin, herbs, mushrooms, magical plants, saplings, flowers, and biome-specific forage. These should wait until the two current crops are fully integrated.

### UI

Status: usable structure, incomplete visual language.

The dark navy/bronze panel skin is appropriate for the System/LitRPG framing and is a better direction than generic fantasy parchment. Existing bars, slots, buttons, rows, and panels provide a usable base.

Missing families:

- inventory categories and item rarity frames;
- tab icons;
- stat icons;
- skill icons;
- ability icons;
- affinity icons;
- buff/debuff/status icons;
- quest objective markers;
- map markers;
- context actions;
- equipment-slot silhouettes;
- crafting-station icons;
- trial rank/class/profession emblems;
- controller prompts and input glyphs.

The UI should use one restrained palette and consistent icon stroke/outline rules. Avoid mixing detailed rendered item art with flat monochrome symbols without a deliberate framing rule.

### Menu and cinematic art

Status: visually attractive but style-incompatible.

The current menu background is a painterly landscape. It creates a strong mood, but it does not look like the same game as the crisp isometric PixelLab world.

Recommendation: replace it with a high-resolution pixel-art establishing scene using the same terrain, building, foliage, campfire, portal, and lighting language as gameplay. The class-assignment void should receive a matching pixel-art cinematic background rather than a separate illustration style.

### Audio and VFX

Status: foundation present, content-light.

Music, ambience, player, and SFX folders exist, but the overall sound set is small relative to the number of systems. Visual effects are mostly procedural particles/glows. Missing visual production assets include:

- weapon arcs and impact sparks;
- gathering hit effects;
- resource break bursts;
- item pickup sparkle;
- crafting completion;
- cooking steam;
- healing/rest;
- fire, ice, poison, root, stone, wind, glimmer, and hearth ability effects;
- status-effect loops;
- portal activation;
- dungeon reward reveal;
- weather splashes and footprints.

## Exact current inventory-icon gap

### Existing icons

- `apple`
- `copper_ore`
- `fiber`
- `hide`
- `stone`
- `wood`
- `wood_axe`
- `wood_pickaxe`

### Missing icons: resources, food, and seeds

- `slime_goo`
- `leather`
- `carrot`
- `wheat`
- `roasted_apple`
- `camp_stew`
- `carrot_seeds`
- `wheat_seeds`
- `copper_bar`
- `iron_ore`
- `silver_ore`
- `gold_ore`
- `manacrystal_ore`
- `starmetal_ore`

### Missing icons: block items

- `stone_block_item`
- `stone_path_item`
- `wood_floor_item`

### Missing icons: tools

- `wood_shovel`
- `wood_sword`
- `stone_axe`
- `stone_pickaxe`
- `stone_shovel`
- `stone_sword`
- `copper_axe`
- `copper_pickaxe`
- `copper_shovel`
- `copper_sword`
- `hoe`

### Missing icons: placeables and construction

- `workbench_item`
- `chest_item`
- `lantern_item`
- `furnace_item`
- `tannery_item`
- `campfire_item`
- `fireplace_item`
- `tavern_door_item`
- `tavern_plot_item`
- `tavern_building_item`
- `library_plot_item`
- `library_building_item`
- `rootcellar_portal_item`

### Missing icons: equipment

- `iron_longsword`
- `oak_shortbow`
- `apprentice_staff`
- `plate_cuirass`
- `leather_jerkin`
- `iron_helmet`
- `plate_greaves`
- `iron_kite_shield`
- `travelers_cape`

Total current gap: 50 icons.

## Recommended generation batches

### Batch 1: current playable inventory

Generate the 50 missing icons above, plus replacements for the existing 8 so all 58 share one style.

Production specification:

- 64x64 final PNG.
- Transparent background.
- True pixel art with a deliberately limited palette.
- Object fills roughly 75-85% of the canvas.
- One consistent upper-left key light.
- Dark navy-brown outline, not pure black.
- No text, frame, rarity glow, floor, pedestal, or baked shadow.
- Readable at 24x24 and 32x32.
- Filename must exactly match the item ID.

Do not generate 1024x1024 final runtime icons. A large AI source may be used, but it should be cleaned and downsampled to a controlled 64x64 pixel grid before import.

### Batch 2: player base and action set

Generate:

- neutral adventurer base;
- four cardinal idle directions;
- four cardinal walk directions;
- axe swing;
- pickaxe swing;
- hoe/till;
- sword attack;
- bow attack;
- staff cast;
- hurt;
- death;
- sit/rest;
- carry/hold.

After the base is approved, generate modular:

- 6 skin tones;
- 12 hairstyles;
- 6 facial-hair options;
- 8 starter tops;
- 6 legwear options;
- 6 shoes/boots;
- cape;
- leather armor;
- plate armor;
- helmet;
- shield;
- axe, pickaxe, shovel, hoe, sword, bow, staff.

### Batch 3: current creature and NPC set

Generate production animation sets for:

- deer;
- fox;
- bandit;
- adventurer knight;
- adventurer mage;
- adventurer rogue;
- adventurer tank;
- villager body/outfit variants;
- merchant body/outfit variants.

The humanoids should reuse the player base proportions and directional contract.

### Batch 4: crops

Generate:

- carrot stages 0-2;
- wheat stages 0-3;
- harvested/empty remnant variants if desired;
- seed-bag icons aligned with the inventory set.

### Batch 5: abilities and LitRPG identity

Generate icons for the currently defined ability set and progression families:

- martial attacks/guard/mobility;
- mana bolt;
- ember/fire;
- root/snare;
- stone/defense;
- the seven affinities: Ember, Tide, Root, Stone, Gale, Glimmer, Hearth;
- all current skills;
- trial evidence categories;
- ranks F through S;
- class rarity frames;
- title badge;
- quest, journal, map, inventory, crafting, skills, spells, character, settings, and System tabs.

### Batch 6: moment-to-moment VFX

Generate sprite strips or small atlases for:

- axe chop;
- pickaxe strike;
- sword slash;
- arrow hit;
- magic impact;
- tree/rock/ore break;
- crop harvest;
- item pickup;
- crafting complete;
- heal;
- burn;
- poison;
- slow/frost;
- root bind;
- stone shield;
- wind dash;
- glimmer reveal;
- hearth ward.

### Batch 7: matching menu/cinematic scenes

Generate:

- main menu establishing shot;
- Proving Week intro/transmigration void;
- day-7 class assignment ceremony;
- loading-screen variants for plains, forest, snow, mountain, dungeon, town, and camp at night.

These should be pixel-art scenes derived from the in-game palette and asset proportions, not painterly concept art.

## Forward-looking asset backlog from the canonical game design

These are not required for the immediate current build, but generation should anticipate them.

### Survival and camping

- tent tiers and damaged states;
- bedroll;
- water flask and filled/empty states;
- rations;
- sharpening stone;
- mana stone;
- ward charm;
- repair kit;
- camp durability/status icons;
- camp radius marker and ward-failure VFX.

### Guild/base building

- guild rank emblems;
- floor/wall/trim tile families;
- doors, windows, stairs, railings, rugs, banners;
- station bonus indicators;
- construction blueprint icons;
- damaged/under-construction variants;
- room-purpose symbols.

### Towns and economy

- general-store goods;
- forge, alchemist, library, guild, stable, dock, and inn signage;
- coins and currency denominations;
- sell/buy/compare icons;
- NPC portraits or dialogue busts;
- service-role markers;
- campsite, village, town, and city map icons.

### Dungeons

- dungeon-rank emblems;
- prep-checklist icons;
- keys, relics, memory items, treasure maps;
- chest rarity tiers;
- traps;
- shrine/altar art;
- boss telegraphs;
- biome-specific dungeon floor, wall-edge, doorway, and decoration sets.

### Affinities and combat

- elemental projectiles and impacts;
- burn, poison, slow, stun, shield, bleed, healing, haste, and ward status icons;
- affinity crystals/materials;
- spell scrolls;
- mana and stamina consumables;
- weapon and armor tier families.

### Farming and gathering

- potato, pumpkin, herbs, berries, magical plants, mushrooms, orchard fruit;
- sapling and tree-growth stages;
- watering and fertilizing visuals;
- forage icons;
- fish, bait, fishing rod, bobber, and catches.

### Travel

- horse directions and animation set;
- saddles and tack;
- rowboat and larger boat states;
- dock pieces;
- camp travel map markers;
- continent/region emblems.

## ChatGPT generation prompt contract

Use the same style clause for every production request:

> Original crisp isometric pixel art for LIT-ISO, a cozy survival-crafting LitRPG. Match the established PixelLab runtime assets: chunky readable shapes, restrained natural palette, dark navy-brown outlines, warm upper-left lighting, square hard-edged pixels, transparent background, no blur, no anti-aliasing, no text, no watermark, no baked ground plane, no painterly rendering, and no imitation of another game's copyrighted assets.

Inventory icon suffix:

> 64x64 inventory icon, centered, object fills 75-85% of canvas, readable at 24x24, transparent background, no frame, no rarity glow, no cast shadow.

World prop suffix:

> 2:1 isometric view, transparent background, bottom-center anchor, no base plate, no baked shadow, designed to sit on a 32 PPU isometric tile grid.

Character/mob suffix:

> Directional game sprite, transparent background, consistent proportions across frames, bottom-center foot anchor, no perspective drift, no camera movement, no baked shadow.

## Required quality gates for generated files

Every generated asset should pass:

1. Exact semantic ID and filename.
2. Transparent alpha with no white/black background leakage.
3. Nearest-neighbor scaling only.
4. No semi-transparent anti-aliased edge pixels unless intentionally used for glow.
5. Consistent light direction.
6. Consistent pixel density.
7. Correct bottom-center pivot for world sprites.
8. Correct footprint and readable silhouette in the actual game camera.
9. No text or AI artifacts.
10. Side-by-side contact-sheet approval before promotion.
11. Runtime import settings normalized:
    - Sprite texture type.
    - Point filtering.
    - Compression disabled for pixel art.
    - Mipmaps disabled.
    - Consistent PPU by asset family.
12. Test at gameplay scale, not only zoomed-in.

## Recommended immediate deliverables

The most efficient next package is:

1. A unified 58-icon current inventory set.
2. Carrot and wheat stage sprites.
3. Deer and fox animation sheets.
4. One approved player base sheet with idle/walk/action tests.
5. A compact UI symbol sheet covering tabs, stats, affinities, and core actions.

Completing those five deliverables would remove the most visible placeholder behavior from the existing playable loop without waiting for new systems.
