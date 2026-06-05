# Tile Pack → Foundation Integration Guide (for Codex)

**Owner request:** turn the gray placeholder cubes into the staged tile art for an
immediate visual upgrade. Owner has explicitly asked for tile integration as the next
visible step.

**Lane note:** terrain/Foundation tile system is Codex's lane. Claude staged + named
the files but does NOT import them into `Assets/` or modify Foundation tile code.

## What's already prepared

- **76 named tiles** in `Docs/handoff/tile-pack-for-codex/named/<name>.png`, lowercase
  snake_case, terrain-naming convention.
- **`tile-name-mapping.json`** in the parent folder maps each name back to its source
  `tile_NNN` from the original pack, with the owner's original casing.
- Tiles are **32×32 isometric**, transparent background, point-art friendly.

## Proposed surface groupings (from filename inspection)

Names suggest these natural biome surfaces:

| Surface | Tiles | Count |
|---|---|---|
| Grass (plains) | `grass_1` … `grass_5`, `longgrass1` … `longgrass7`, `extralonggrass`, `flowerpatch1` | 14 |
| Dirt / soil | `dirt_1` … `dirt_17`, `soil`, `soil_grassy` | 19 |
| Red soil (alt biome) | `red_soil1` … `red_soil3`, `red_soil_grassy1` … `red_soil_grassy2` | 5 |
| Rock | `rocktile_1`, `rock_pile`, `rock_tileplain`, `rock_stack`, `rock_boulder`, `rock_pile_small1/2` | 7 |
| Rock + water edge | `rock_waterbordr`, `rocksmooth_water`, `rock_pile_water1..5`, `rock_cluster_water1..5` | 11 |
| Shallow water | `shallowwaterplain`, `watertextured1`, `watertexture2` | 3 |
| Deep water | `deepwaterplain` | 1 |
| Water waves | `water_sw_dark_light_wave`, `water_sw_dark_light_gentle_wave` | 2 |
| Ice (frozen biome) | `ice1plain` … `ice4se`, `icese`, `icenwe`, `iceswe`, `icenws`, `icense`, `icebordered`, `icecracked` | 11 |
| Small rock pile decor | `smallrockpile`, `rock_stack2` | 2 |

These mappings are guesses from names alone — Codex should sanity-check against the
actual sprite content before committing to a surface assignment.

## Suggested integration path

1. **Import once.** Copy the 76 PNGs from `Docs/handoff/tile-pack-for-codex/named/` to
   `Assets/Art/Tiles/` (or wherever fits AssetForge conventions). Import settings:
   - Sprite (2D and UI)
   - Filter Mode: **Point**
   - Compression: **None**
   - PPU consistent with the rest of the Foundation tileset
2. **Add to BlockDefinition / BlockGroupDefinition SOs.** Each surface group needs a
   `BlockDefinition` whose sprite field points to one of the imported tiles.
3. **Wire to biomes.** Each `BiomeDefinition` references the relevant block-group SO
   so the terrain renderer paints the right surface per biome.
4. **Validation.** Run `Tools > LIT-ISO > ISO-Core Foundation > Validate Foundation`
   after import; expect the validator's tile-count assertions to update.

## Things I (Claude) deliberately did NOT do

- Import any of the 76 PNGs into `Assets/`.
- Touch `BlockDefinition`, `BlockGroupDefinition`, `BiomeDefinition`, the renderer, or
  `IsoCoreFoundation.unity`.
- Sanity-check the visual content of the tiles (only worked from filenames).

## Owner caveat — verify license before shipping

These came from a commercial pack the owner purchased (Clockwork Raven Studios on
Itch.io). The owner has confirmed the purchase, but the Itch.io purchase license should
be confirmed to permit commercial-game use before any release build ships art derived
from these tiles. Owner's call.

## What this unblocks for the owner

The gray placeholder cubes become real terrain — the single biggest visual upgrade
between the current state and a "this is a real game" first impression. Owner asked
for this directly after seeing the panels working today.
