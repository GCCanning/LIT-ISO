# Icon & Tile Import Report (2026-06-05)

Generated from owner's mappings via the namer.html tool.

## Summary
- Items copied: **52** -> `Assets/Resources/Items/`
- Tiles staged: **76** -> `Docs/handoff/tile-pack-for-codex/named/`
- Tile -> source mapping: `Docs/handoff/tile-pack-for-codex/tile-name-mapping.json`

## Items (Assets/Resources/Items/)
Resolves at runtime via `ItemIconResolver`: `content.Items.Get(itemId).Icon`
falling back to `Resources/Items/<itemId>.png`. Names normalised to lowercase
snake_case (filesystem-safe).

- `bandana_burgundy`
- `berseker_helmet`
- `captain_helmet_v1`
- `captain_helmet_v2`
- `captain_helmet_v3`
- `copper_bagged`
- `copper_bar_refined`
- `copper_bar_unrefined`
- `copper_block`
- `copper_chunk`
- `copper_helmet`
- `copper_ore`
- `copper_piece`
- `copper_powder`
- `fire_bomb`
- `fire_funnel`
- `fire_scattershot`
- `fire_slice`
- `fire_summon`
- `fire_sword`
- `fire_volcano`
- `fire_wave`
- `fire_wave_multi`
- `fireball`
- `fireball_long`
- `gold_helmet`
- `gold_necklace`
- `ignition`
- `incinerate`
- `iron_helmet`
- `jeweled_necklace`
- `leather_belt`
- `leather_boots`
- `leather_helmet`
- `light_iron_helmet`
- `mage_cloth_head`
- `rain_of_fire`
- `ruby_common`
- `ruby_epic`
- `ruby_legendary`
- `ruby_mythic`
- `ruby_ore`
- `ruby_pieces`
- `ruby_rare`
- `ruby_transcendant`
- `ruby_uncommon`
- `steel_helmet`
- `steel_necklace`
- `steel_ring_diamond`
- `steel_ring_plain`
- `tungsten_helmet`
- `void_armor`

## Tiles (handoff to Codex)
Staged for the Foundation terrain renderer / Milestone A1. Names normalised the
same way. Codex decides whether to import into Assets/ (their lane).

- `deepwaterplain`  (from t092, originally 'DeepWaterPlain')
- `dirt_1`  (from t000, originally 'Dirt_1')
- `dirt_10`  (from t009, originally 'Dirt_10')
- `dirt_11`  (from t010, originally 'Dirt_11')
- `dirt_12`  (from t011, originally 'Dirt_12')
- `dirt_13`  (from t012, originally 'Dirt_13')
- `dirt_14`  (from t013, originally 'Dirt_14')
- `dirt_15`  (from t014, originally 'Dirt_!5')
- `dirt_16`  (from t015, originally 'Dirt_!6')
- `dirt_17`  (from t016, originally 'Dirt_!7')
- `dirt_2`  (from t001, originally 'Dirt_2')
- `dirt_3`  (from t002, originally 'Dirt_£')
- `dirt_4`  (from t003, originally 'Dirt_4')
- `dirt_5`  (from t004, originally 'Dirt_5')
- `dirt_6`  (from t005, originally 'Dirt_6')
- `dirt_7`  (from t006, originally 'Dirt_7')
- `dirt_8`  (from t007, originally 'Dirt_8')
- `dirt_9`  (from t008, originally 'Dirt_9')
- `extralonggrass`  (from t036, originally 'ExtraLongGrass')
- `flowerpatch1`  (from t041, originally 'FlowerPatch1')
- `grass_1`  (from t022, originally 'Grass_1')
- `grass_2`  (from t023, originally 'Grass_2')
- `grass_3`  (from t024, originally 'Grass_3')
- `grass_4`  (from t027, originally 'Grass_4')
- `grass_5`  (from t028, originally 'Grass_5')
- `ice1plain`  (from t104, originally 'Ice1Plain')
- `ice2ne`  (from t105, originally 'Ice2NE')
- `ice3nw`  (from t106, originally 'Ice3NW')
- `ice4se`  (from t107, originally 'Ice4SE')
- `icebordered`  (from t113, originally 'IceBordered')
- `icecracked`  (from t114, originally 'IceCracked')
- `icense`  (from t112, originally 'IceNSE')
- `icenwe`  (from t109, originally 'IceNWE')
- `icenws`  (from t111, originally 'IceNWS')
- `icese`  (from t108, originally 'IceSE')
- `iceswe`  (from t110, originally 'IceSWE')
- `longgrass1`  (from t029, originally 'LongGrass1')
- `longgrass2`  (from t030, originally 'LongGrass2')
- `longgrass3`  (from t031, originally 'LongGrass3')
- `longgrass4`  (from t032, originally 'LongGrass4')
- `longgrass5`  (from t033, originally 'LongGrass5')
- `longgrass6`  (from t034, originally 'LongGrass6')
- `longgrass7`  (from t035, originally 'LongGrass7')
- `red_soil1`  (from t017, originally 'Red_Soil1')
- `red_soil2`  (from t018, originally 'Red_Soil2')
- `red_soil3`  (from t021, originally 'Red_Soil3')
- `red_soil_grassy1`  (from t019, originally 'Red_Soil_Grassy1')
- `red_soil_grassy2`  (from t020, originally 'Red_Soil_Grassy2')
- `rock_boulder`  (from t065, originally 'Rock_Boulder')
- `rock_cluster_water1`  (from t077, originally 'Rock_Cluster_Water1')
- `rock_cluster_water2`  (from t078, originally 'Rock_Cluster_Water2')
- `rock_cluster_water3`  (from t079, originally 'Rock_Cluster_Water3')
- `rock_cluster_water4`  (from t080, originally 'Rock_Cluster_Water4')
- `rock_cluster_water5`  (from t081, originally 'Rock_Cluster_Water5')
- `rock_pile`  (from t062, originally 'Rock_Pile')
- `rock_pile_small1`  (from t075, originally 'Rock_Pile_Small1')
- `rock_pile_small2`  (from t076, originally 'Rock_Pile_Small2')
- `rock_pile_water1`  (from t070, originally 'Rock_Pile_Water1')
- `rock_pile_water2`  (from t071, originally 'Rock_Pile_Water2')
- `rock_pile_water3`  (from t072, originally 'Rock_Pile_Water3')
- `rock_pile_water4`  (from t073, originally 'Rock_Pile_Water4')
- `rock_pile_water5`  (from t074, originally 'Rock_Pile_Water5')
- `rock_stack`  (from t064, originally 'Rock_Stack')
- `rock_stack2`  (from t068, originally 'Rock_stack2')
- `rock_tileplain`  (from t063, originally 'Rock_TilePlain')
- `rock_waterbordr`  (from t066, originally 'Rock_WaterBordr')
- `rocksmooth_water`  (from t069, originally 'RockSmooth_Water')
- `rocktile_1`  (from t061, originally 'RockTile_1')
- `shallowwaterplain`  (from t101, originally 'ShallowWaterPlain')
- `smallrockpile`  (from t067, originally 'SmallRockPile')
- `soil`  (from t025, originally 'Soil')
- `soil_grassy`  (from t026, originally 'Soil_grassy')
- `water_sw_dark_light_gentle_wave`  (from t087, originally 'Water_SW_Dark_Light_Gentle_WAVE')
- `water_sw_dark_light_wave`  (from t086, originally 'Water_SW_Dark_Light_Wave')
- `watertexture2`  (from t103, originally 'WaterTexture2')
- `watertextured1`  (from t102, originally 'WaterTextured1')

## Auto-corrections applied
- **UK keyboard Shift typos in tile names:** the `£` (Shift+3) and `!` (Shift+1)
  symbols in `Dirt_*` entries were treated as the digits the user intended.
  - `Dirt_£` -> `dirt_3`
  - `Dirt_!5` -> `dirt_15`
  - `Dirt_!6` -> `dirt_16`
  - `Dirt_!7` -> `dirt_17`

## Duplicates skipped
First occurrence wins; second is skipped and flagged here. Owner can rename the
duplicate in the namer.html tool and re-export to recover the missed one.

- `materials.t006` ("Copper_Block") - clashes with `materials.t003`
- `spells.t007` ("Fire Wave") - clashes with `spells.t010`

## What's NOT here yet
Lots of icons still unnamed in the namer.html tool - this batch only includes
mappings the owner exported. Re-export any time and I will run the same import.
