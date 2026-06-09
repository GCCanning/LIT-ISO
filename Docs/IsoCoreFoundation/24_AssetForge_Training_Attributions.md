# Asset Forge Training Attributions

Purpose: preserve provenance for any third-party asset source approved for local model training, LoRA training, QA or oracle use.

This file is not a runtime credits screen by itself. It is the source-of-truth note that dataset manifests, model cards, release credits, and export manifests should copy from.

## Approved Training Sources

### OpenGameArt - 400 items/basehumanmale/orc/skeleton

- Source URL: `https://opengameart.org/content/400-items-basehumanmale-orc-skeleton`
- Local source root: `C:\Projects\Pixel Pipeline\sources\opengameart\400-items-basehumanmale-orc-skeleton`
- Local oracle output: `C:\Projects\Pixel Pipeline\generated\oga_8d_character_oracle_v1`
- License: CC-BY 4.0
- Attribution recorded in local manifest: LinksDream / OpenGameArt page credits
- Owner approval: approved for local training on 2026-06-08
- Approved uses:
  - 8-direction motion/direction LoRA or adapter training
  - animation and action classification
  - direction QA oracle
  - sprite-sheet packing reference
- Conditions:
  - Preserve attribution in dataset and model manifests.
  - Label derived models as trained with CC-BY 4.0 source material.
  - Do not ship raw source pixels in final game assets unless final game credits include the required attribution.
  - Keep this distinct from original-only LIT-ISO training data.

## Not Yet Approved For Training

These sources may exist locally as reference/intake, but should not be used for training until explicitly approved:

- FreePixel official packs under `C:\Projects\Pixel Pipeline\sources\freepixel`
- OpenGameArt/Kenney review packs staged outside Unity for buildings/interiors unless individually approved
- Sprixen-generated assets unless their API/license terms and project usage are explicitly recorded
