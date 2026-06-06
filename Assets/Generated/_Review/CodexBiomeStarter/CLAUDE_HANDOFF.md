# Codex Biome Starter Pack

Generated review pack for forest and plains terrain tiles plus separate decoration props.

Review source:
`Assets/Generated/_Review/CodexBiomeStarter`

Approved generated output:
- `Assets/Generated/Tiles/Forest`
- `Assets/Generated/Tiles/Plains`
- `Assets/Generated/Tiles/Shared`
- `Assets/Generated/Props/Forest`
- `Assets/Generated/Props/Plains`
- `Assets/Generated/Props/Shared`

Import settings:
- Texture Type: Sprite (2D and UI)
- Sprite Mode: Single
- Pixels Per Unit: 32 for terrain, 128 for generated props
- Filter Mode: Point
- Compression: None
- Mip Maps: Off

Review artifacts:
- `_Preview/forest_contact_sheet.png`
- `_Preview/plains_contact_sheet.png`
- `_Preview/shared_contact_sheet.png`
- `_Preview/forest_decorations_contact_sheet.png`
- `_Preview/plains_decorations_contact_sheet.png`
- `_Preview/shared_decorations_contact_sheet.png`
- `review_report.json`
- `review_decisions.json`
- `approval_manifest.json`
- `review_gallery.html`

Implementation guidance:
1. Terrain tiles are terrain-only top tiles. Do not bake trees, bushes, rocks, logs, flowers, props, characters, or buildings into terrain.
2. Decoration props stay separate and should be placed by biome/resource/decor rules.
3. Trees and bushes should participate in actor-behind transparency fading.
4. Rocks can be obstacles, resource nodes, or decoration-only depending on biome rule setup.
5. The approved generated folders are safe handoff folders. Do not copy into `Assets/Resources/**` without an explicit integration pass.

Generation scripts:
- `Temp/GeneratedTiles/make_biome_starter_tiles.ps1`
- `Temp/GeneratedTiles/make_biome_starter_decorations.ps1`
- `Temp/GeneratedTiles/normalize_biome_starter_imports.ps1`
- `Temp/GeneratedTiles/make_biome_pack_review.ps1`
- `Temp/GeneratedTiles/initialize_biome_review_decisions.ps1`
- `Temp/GeneratedTiles/approve_biome_review_assets.ps1`

Unity importer:
- `Assets/Scripts/Editor/AssetForgeGeneratedImportPostprocessor.cs`
- Menu: `Tools > Asset Forge > Reimport Generated Assets`
- Batch method: `AssetForgeGeneratedImportPostprocessor.ReimportGeneratedAssets`

Current caveat:
- If Unity is already open while these files are generated, it may temporarily rewrite metas with old defaults until the new postprocessor compiles and generated assets are reimported.
