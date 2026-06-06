# Asset Forge Sprixen-Style Quality Standard

This document defines the local quality bar for LIT-ISO generated assets. The goal is not to clone Sprixen's code, UI, brand, or assets. The goal is to match the useful product shape: generate, compare, clean up, review, approve, and export game-ready assets locally.

## Current Production Target

The first reliable production slice is biome asset packs:

- terrain tiles
- separate decoration props
- contact sheets
- review gallery
- manifests
- Unity import metadata
- reproducible generation scripts

The canonical review pack lives at:

`Assets/Generated/_Review/CodexBiomeStarter`

## Quality Rules

### Terrain Tiles

- `32x32` transparent PNG.
- Pixel art only, no antialias haze.
- Point filtering, no mipmaps.
- `spritePixelsToUnits: 32`.
- Terrain-only. No trees, bushes, rocks, logs, flowers, characters, buildings, or props baked onto the tile.
- Variants should differ by terrain surface, not by random decoration.
- Transitions should remain readable at gameplay zoom.

### Decoration Props

- `128x128` transparent PNG for first-pass biome props.
- Bottom-center anchored.
- Point filtering, no mipmaps.
- `spritePixelsToUnits: 128` by default so a `128x128` prop occupies roughly one world tile unless a biome rule intentionally overrides scale.
- Never baked onto terrain.
- Trees and bushes should be treated as occluders and participate in actor-behind transparency fading.
- Rocks may be obstacles, resource nodes, or decoration-only depending on biome rules.

### Review Artifacts

Every generated pack should include:

- source PNGs
- matching `.meta` files
- `manifest.json`
- mode-specific manifests such as `decorations_manifest.json`
- contact sheets
- `review_report.json`
- `review_gallery.html`
- a handoff document for the importing agent

## Local Workflow

1. Generate into `Assets/Generated/_Review/<PackName>`.
2. Keep generated terrain and decorations separated.
3. Normalize Unity import metadata after Unity touches the files.
4. Run the pack review report.
5. Review contact sheets and `review_gallery.html`.
6. Initialize `review_decisions.json`.
7. Copy only approved assets into generated Unity import locations.
8. Claude or the human then wires approved generated assets into canonical game systems.

## Scripts For Current Starter Pack

- `Temp/GeneratedTiles/make_biome_starter_tiles.ps1`
- `Temp/GeneratedTiles/make_biome_starter_decorations.ps1`
- `Temp/GeneratedTiles/normalize_biome_starter_imports.ps1`
- `Temp/GeneratedTiles/make_biome_pack_review.ps1`
- `Temp/GeneratedTiles/initialize_biome_review_decisions.ps1`
- `Temp/GeneratedTiles/approve_biome_review_assets.ps1`

Run order:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Projects\Unity-Projects\LIT-ISO\Temp\GeneratedTiles\make_biome_starter_tiles.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Projects\Unity-Projects\LIT-ISO\Temp\GeneratedTiles\make_biome_starter_decorations.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Projects\Unity-Projects\LIT-ISO\Temp\GeneratedTiles\normalize_biome_starter_imports.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Projects\Unity-Projects\LIT-ISO\Temp\GeneratedTiles\make_biome_pack_review.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Projects\Unity-Projects\LIT-ISO\Temp\GeneratedTiles\initialize_biome_review_decisions.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Projects\Unity-Projects\LIT-ISO\Temp\GeneratedTiles\approve_biome_review_assets.ps1" -ReplaceExisting
```

Unity should then compile `AssetForgeGeneratedImportPostprocessor` and run:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Projects\Unity-Projects\LIT-ISO" -executeMethod AssetForgeGeneratedImportPostprocessor.ReimportGeneratedAssets -logFile "C:\tmp\LitIsoAssetForgeGeneratedReimport.log"
```

If Unity is already open, use the editor menu instead:
`Tools > Asset Forge > Reimport Generated Assets`.

## Acceptance Bar

A generated pack is ready for Unity review when:

- all PNGs have matching `.meta` files,
- terrain and decoration dimensions match their category,
- terrain PPU is 32,
- prop/decor PPU is 128 unless a biome rule intentionally overrides it,
- filter mode is Point,
- mipmaps are disabled,
- alpha transparency is enabled,
- previews render without obvious style drift,
- props are not baked into terrain,
- handoff notes describe how to import safely.

## Next Sprixen-Clone Milestones

1. Add an Asset Forge web review page that reads `review_report.json` and supports approve/reject.
2. Promote the current PowerShell review/approval gate into the Asset Forge web dashboard.
3. Add mode presets for terrain, decoration, item, mob, effect, and character.
4. Add comparison grids for prompt/model/LoRA/seed variations.
5. Add dataset capture for approved and rejected examples.
6. Add local LoRA evaluation against this same review pipeline.
