# Modular Dungeon And Portal Plan

## Implemented First Slice

- Imported owner-provided portal art into `Assets/Resources/FoundationPortals/`.
- Imported a curated Kenney CC0 dungeon subset into `Assets/Resources/FoundationDungeon/Kenney/`.
- Added procedural overworld dungeon portals.
- Portal tier is based on distance from world spawn: farther portals use higher tier colors, larger layouts, more mobs, and stronger reward intent.
- Right-clicking a portal opens an option menu and enters a generated dungeon instance.
- Dungeon instances are deterministic from world seed, dungeon id, portal cell, and tier.
- Dungeon layouts use connected rooms and corridors with a solid boundary ring.
- Dungeon art currently dresses the Foundation collision grid with Kenney props, stairs, columns, barrels, planks, and chests.
- Overworld wildlife spawning pauses while inside an instance, so dungeon encounters come from dungeon rules.

## Core Architecture

Keep three layers separate:

- Portal layer: overworld discovery, color tier, distance scaling, entry interaction.
- Dungeon generation layer: deterministic room graph, bounds, spawn/exit, mobs, decorations, rewards.
- Instance/render layer: moves player to the generated pocket and tells `IsoWorldController` to render only that active bounded region.

The current first slice keeps simple tavern/guild interiors in `FoundationInstanceSystem` and routes dungeon portals through `FoundationDungeonPortalSystem` plus `FoundationDungeonGenerator`.

## Next Implementation Steps

1. Add dungeon save data.
   Save layout seed, dungeon id, portal id, tier, cleared rooms, opened chests, defeated mobs, and current objective state. Regenerate layout from seed, then apply player edits.

2. Add dungeon completion.
   Use the generated exit/stairs/chest as objective markers. Completing a dungeon should dispatch `DungeonResultDefinition` rewards, XP, title progress, affinity progress, and a System message.

3. Add room templates.
   Convert the current rectangle rooms into tagged templates: entrance, combat, treasure, puzzle, rest, boss, exit. Each template should stamp floor, wall, decorations, node markers, and socket directions.

4. Add dungeon families.
   `rootcellar`, `ruined_keep`, `glowcap_grotto`, and `wintercrypt` should each define tile palette, prop pool, mob table, reward table, and layout rules.

5. Add reward scaling.
   Tier should influence reward quantity and rarity:
   - Tier 1: wood, stone, food, basic XP.
   - Tier 2: copper ore, extra seeds, root/hearth affinity.
   - Tier 3: recipes, memory pages, adventurer rank XP.
   - Tier 4+: rare materials, title progress, class evidence, dungeon clearance XP.

6. Add portal spawning rules.
   Replace the fixed initial ring list with worldgen-backed portal sites. Sites should prefer clear walkable cells, avoid water/nodes, and optionally require discovery by proximity.

7. Add dungeon UI feedback.
   On entry show: dungeon name, tier, recommended level, expected rewards, and a short System message. During dungeon show current objective and danger tier.

8. Add validation.
   Extend integrated validation to cover portal spawn count, entry via right-click contract, save/load inside active dungeon, exit return, deterministic room graph, and no overworld mob leakage.

## Design Rules

- Do not alter the canonical isometric grid, height, sorting, movement, or MenuScene launch contract.
- Use third-party art only with provenance recorded. Kenney dungeon art is CC0. The portal license permits commercial use inside a game project and modification, but not standalone resale/NFT use.
- Keep generated dungeon terrain separate from player-made overworld edits in future save schema.
- Dungeons should be deterministic for the same world seed and portal, but visually varied across portal cells and tiers.
- Combat should not become the only solution long term: add calm, sneak, lure, cleanse, rescue, and puzzle objectives as room template tags.
