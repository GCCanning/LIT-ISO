# Tile Reference And Generation Notes

Date: 2026-06-09

## Current Decision

Do not keep spending generation time on prompt-only SD1.5 terrain tiles.
The usable path is geometry-first:

1. Build or derive exact 2:1 isometric tile masks.
2. Generate deterministic flat, edge, corner, transition, and material variants.
3. Use ComfyUI/ControlNet later for texture/style variation only after a tile-style LoRA exists.
4. Keep every generated tile passing local structural QA before Unity import or dataset capture.

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

## Implemented Local Result

`Tools/AssetForge/derive_tile_geometry_variants.py` now produces a deterministic Greenwake terrain family:

- brighter readable grass/forest palette,
- flat 64x64 isometric diamond tops,
- earthy side faces for grass/forest/path raised edges,
- material-specific dirt, stone, and path variants,
- organic grass-to-material transitions,
- filled map preview over a base grass grid.

Latest review pack:

- `Assets/Generated/_Review/greenwake_geometry_derived_v7/derived_geometry_contact_sheet.png`
- `Assets/Generated/_Review/greenwake_geometry_derived_v7/derived_geometry_map_preview.png`
- `Assets/Generated/_Review/greenwake_geometry_derived_v7/strict_asset_quality_report.json`

Strict QA result for v7:

- 57 total terrain tiles
- 57 pass
- 0 review failures
- 10 warnings
- dataset-ready: true

## Remaining Art Gap

The v7 set is structurally usable but not final-quality art. Remaining visible issues:

- top/side seam is still too graphic compared with the target screenshots,
- grass detail marks are too procedural/repeated,
- grid line contrast in the preview is stronger than the desired in-game look,
- transitions need more hand-authored variants before promotion.

Next practical pass: build a curated tile-polish tool that softens seams, adds controlled cluster variation, and exports an approval set for Unity import.
