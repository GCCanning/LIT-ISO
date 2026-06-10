# Tile Reference And Generation Notes

Date: 2026-06-09

## Current Decision

Do not keep spending generation time on prompt-only SD1.5 terrain tiles or on the rejected Greenwake v7 64x64 geometry family.

The supplied `isometric tileset.7z` and `critters.7z` packs are now the style-lock references.

The usable path is style-lock first, geometry second:

1. Extract and analyze the supplied style packs.
2. Match their tile scale, palette, shadow ramps, texture density, and animation direction contract.
3. Build or derive exact compact isometric tile masks at the same apparent scale.
4. Use ComfyUI, ControlNet, or Sprixen only for candidates that are checked against the style-lock pack.
5. Keep every generated tile passing both local structural QA and human art approval before Unity import or dataset capture.

## Style-Lock Analysis

Current analyzed style-lock artifacts:

- `Assets/Generated/_Review/style_lock_sources/style_lock_analysis/STYLE_LOCK_ANALYSIS.md`
- `Assets/Generated/_Review/style_lock_sources/style_lock_analysis/style_lock_inventory.json`
- `Assets/Generated/_Review/style_lock_sources/style_lock_analysis/tileset_contact_sheet.png`
- `Assets/Generated/_Review/style_lock_sources/style_lock_analysis/critters_contact_sheet.png`
- `Assets/Generated/_Review/style_lock_sources/style_lock_analysis/tileset_palette.png`
- `Assets/Generated/_Review/style_lock_sources/style_lock_analysis/critters_palette.png`
- `Tools/AssetForge/style_profiles/litiso_iso_reference_v1.json`

Observed target contract:

- Tileset: 116 PNGs, mostly `32x32` terrain/object tiles.
- Critters: 110 PNGs, mostly `46x32` individual frames plus sheets/strips.
- Critter directions: `NE`, `NW`, `SE`, `SW`.
- Critter actions observed: idle, run, walk, burrow, unburrow, tunnel.
- License/source status: user-supplied reference, no extracted license/readme found. Training use remains blocked until the license is documented.

## Greenwake v7 Status

Greenwake v7 is not production art and should not be imported into Unity gameplay.

Status:

- Structural QA: pass.
- Import metadata tooling: pass.
- Art approval: failed.
- Unity integration: blocked.

The promoted generated copy at `Assets/Generated/Tiles/Greenwake` was removed after user review. Keep the pack only under `_Review` as a rejected geometry scaffold and pipeline test.

## Online Sources Checked

- [tilemapgen](https://github.com/charmed-ai/tilemapgen)
  - Useful as a method reference: geometry/control first, model variation second.
  - Do not treat it as a drop-in dependency yet; our local generator is simpler and Unity-specific.

- [OpenGameArt - Isometric Ground Tiles](https://opengameart.org/content/isometric-ground-tiles)
  - License shown as CC0/public domain on OpenGameArt.
  - Useful for training-safe ground/edge geometry reference if provenance stays attached.

- [OpenGameArt - Free Forest Isometric Pixel Art](https://opengameart.org/content/free-forest-isometric-pixel-art)
  - OpenGameArt page shows CC0, but the linked Itch page states the asset may not be used as a database for AI training.
  - Treat as visual reference only unless licensing is clarified. Do not ingest into LoRA datasets.

- [OpenGameArt - YAR's 64x64 Isometric LPC Sprites and Tiles](https://opengameart.org/content/yars-64x64-isometric-lpc-sprites-and-tiles)
  - Useful shape/scale reference.
  - License requires care and attribution; do not mix into clean training datasets without explicit approval.

## Next Micro Pack

Created first review micro pack:

- `Assets/Generated/_Review/style_locked_micro_pack_v1/style_locked_micro_pack_contact_sheet.png`
- `Assets/Generated/_Review/style_locked_micro_pack_v1/style_locked_micro_pack_manifest.json`

It uses exact supplied source pixels plus controlled hue/value variants. It is review-only until licensing/training rights are documented.

Next practical pass: generate original candidates matching this 5-tile micro pack:

- grass block/top,
- dirt/soil block,
- grass-to-dirt transition,
- water tile,
- stone/rock tile.

Do not generate a full biome family until those five pass visual approval.

## Character/Mob Direction Note

The supplied critter style is diagonal-isometric first. For the black mage review:

- Review sheet: `Assets/Generated/_Review/black_mage_iso_style_lock_v1/black_mage_iso_review_sheet.png`
- Manifest/prompts: `Assets/Generated/_Review/black_mage_iso_style_lock_v1/black_mage_iso_review_manifest.json`

The current black mage source is front-facing. The generated scaffold only normalizes scale and anchor; it is not final direction art. True output should be generated or painted as `NE`, `NW`, `SE`, `SW` first, then expanded if the gameplay controller needs canonical `S, SE, E, NE, N, NW, W, SW`.
