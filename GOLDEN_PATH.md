# LIT-ISO Golden Path

This is the canonical way to set up and test the current game prototype.

## Primary Scene Flow

Use this menu item first:

- `Tools > LIT-ISO > Golden Path > Run Current Golden Path`

This menu item is a synced wrapper around the active setup tool:

- `Tools > LIT-ISO > Playtest > Quick Play Test`

This is the active golden path because it is additive, keeps the current scene, and wires the current systems together:

- `IsoWorldChunkManager`
- `IsoPlayerController`
- climate biome generation
- player idle sprite/audio
- camera
- lighting/time cycle
- recorder
- gameplay HUD
- in-game settings menu (`I`)
- inventory/interaction components

It also completes the current validation pass:

- runs the LPC player wiring step
- validates the active scene against the current playtest contract
- checks the isometric transparency sort / grid / height-world wiring

Use this only when you want to rebuild a clean prototype scene from scratch:

- `Tools > LIT-ISO > Golden Path > Rebuild Full Playtest Scene`
- `Tools > LIT-ISO > Playtest > Rebuild Full Playtest Scene`

## Unity Tools Menu

All current project tools live under `Tools > LIT-ISO`:

- `Golden Path`: synced one-click entry points and this document.
- `Playtest`: current run paths. Start here.
- `World`: world/scene generation utilities.
- `Setup`: additive HUD/gameplay setup helpers.
- `Assets`: create or refresh ScriptableObject data.
- `Diagnostics`: validation, console log helpers, and editor visibility fixes.
- `Build`: standalone Windows build tools.
- `Legacy`: older procedural generator tools kept for reference only.

Biome asset rules:

- Reference document: `BIOME_ASSET_RULES.md`
- Unity data model: `BiomeAssetRuleSet`
- Creator tool: `Tools > LIT-ISO > Assets > Create Or Update Biome Asset Rules`

## Active Runtime Systems

These are the systems to treat as current:

- World: `IsoWorldChunkManager`
- Biomes: `IsoBiomeDefinition`
- Player: `IsoPlayerController`
- Setup: `QuickPlayTestSetup`
- Full scene builder: `IsoWorldSetup`
- Recorder: `IsoRuntimeRecorder`
- Gameplay layer: `PlayerInventory`, `PlayerHealth`, `IsoInteractionController`, HUD scripts

## Biome Rules For Current Prototype

The natural overworld uses only:

- Plains
- Desert
- Frozen Mountain

Frozen Cave, Temple, and Basic remain asset/test biomes, but they should not appear in natural overworld generation until explicitly reintroduced.

Terrain heights:

- Plains: max height 2
- Desert: max height 2
- Frozen/highland regions: max height 3

This keeps terrain in the “rolling hills” range rather than tall mountain towers.

## Legacy / Caution Areas

These scripts and menu paths are historical or experimental. Do not use them as the source of truth unless deliberately reviving them:

- `Tools > LIT-ISO > Legacy > Procedural Generator`
- `QuickMapSetup`
- `ProceduralIsoTilemapGenerator`
- `IsometricPlayerMovementController`
- `Phase2*` test/demo scripts
- old Witch animation/controller assets

## Animation Direction

Current player state (post-LPC integration):

- Player visuals are now driven by the **LPC layered character system** (`Assets/LPC/`).
- 4 cardinal directions (N/W/S/E). Diagonal movement snaps to the nearest cardinal sprite — same approach as Stardew Valley, Hyper Light Drifter, Tunic.
- 13 animations available: walk, idle, slash, spellcast, thrust, shoot, hurt, run, jump, sit, emote, climb, combat.
- Equipment is composable at runtime (`character.SetEquipment(layer, sheet)`).

The static fallback assets are still on disk:

- `ReferenceKnight_Idle_512x1024.png` — single sprite reference (legacy)
- `HollowedLight_512x1024.png` — walk reference (legacy)
- `Player_Idle_IceHum.wav` — idle audio (still used)

When `Run Current Golden Path` executes:

1. `QuickPlayTestSetup.RunSetup()` builds the world + player as before.
2. `LPCGoldenPathSetup.WireLPCPlayer()` adds an `LPCRoot` child under the Player containing `LPCCharacter` + `LPCAnimator` + `LPCPlayerBridge`. Default starter equipment is auto-equipped from `Assets/LPC/Data/` if those assets exist.
3. The legacy single-sprite `SpriteRenderer` on the Player is disabled (not deleted) so the LPC layers are what render.

If you want only the legacy single-sprite player, manually re-enable that `SpriteRenderer` and remove the `LPCRoot` child.

To regenerate LPC sheet assets after adding more PNGs: `LIT-ISO -> LPC -> Import Sheets from Folder`.

## UI Font

Current game UI font:

- `Assets/Resources/Fonts/antiquity-print.ttf`
- reference sprite sheet: `Assets/Resources/Fonts/Antiquity_SpriteSheet.png`

Use font sizes divisible by 13 for clean spacing:

- 13: tiny labels, stack counts, health text, small prompts
- 26: default HUD labels, buttons, short interaction prompts
- 39: panel headings
- 52+: large title or chapter text
