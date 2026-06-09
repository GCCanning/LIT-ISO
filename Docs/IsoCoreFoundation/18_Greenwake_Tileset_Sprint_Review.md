# Greenwake Tileset Sprint Review

## Scope

This sprint moves the Greenwake terrain work from isolated Sprixen samples toward a ready Unity 2D isometric tileset workflow. No generated tile has been promoted into `Assets/Generated/Tiles/Greenwake` yet; all outputs remain in review or request staging.

## Ten Things Completed

1. Locked the selected material masters from owner QC:
   - grass `v1`
   - dirt `v1`
   - forest floor `v1`
   - stone `v2`
   - path `v2`

2. Created the curated selected-master review pack:
   - `Assets/Generated/_Review/greenwake_height_material_masters_selected_v1`

3. Created selected-master visual review outputs:
   - `selected_masters_contact_sheet.png`
   - `selected_masters_9x9_preview.png`

4. Added terrain pack analysis tooling:
   - `Tools/AssetForge/analyze_terrain_pack.py`
   - output: `terrain_harmony_analysis.json`
   - output preview: `terrain_harmony_analysis_preview.png`

5. Measured master-pack contrast and palette drift:
   - value range: `0.2579`
   - saturation range: `0.5467`
   - max color count: `27`
   - recommendation: generate transitions before Unity promotion; avoid destructive recolor.

6. Added raised/flat terrain profile QA:
   - `Tools/AssetForge/test_strict_asset_quality.ps1`
   - supports `-TerrainProfile flat|raised_block`
   - selected masters strict QA: `5 pass / 0 review`

7. Added Sprixen raised-tile prompt support that no longer forces every raised tile to be grass.

8. Added tile-family request staging:
   - `Tools/AssetForge/queue_tile_family_requests.py`
   - queued 25 core-shape request folders, 2 candidates each, all dry-run ready.

9. Added Unity-style handoff atlas tooling:
   - `Tools/AssetForge/build_tileset_handoff.py`
   - output: `Handoff/greenwake_selected_height_masters_atlas.png`
   - output: `Handoff/greenwake_selected_height_masters_handoff.json`

10. Added the explicit Greenwake family spec:
   - `Tools/AssetForge/tile_family_specs/greenwake_height_tile_family_v1.json`

## Current Art Read

The selected masters are usable as style anchors, but they still contrast heavily when placed directly beside each other. That is expected because they are pure material tiles. The fix is not broad recolor; it is transition coverage:

- grass to dirt
- grass to forest floor
- dirt to path
- stone embedded into dirt or forest floor
- edge/corner variants that preserve shared side-face lighting

## Ready For Next Sprixen Batch

Queued core-shape requests are ready under:

`Assets/Generated/_Review/_Requests`

The staged set is:

- 5 materials
- 5 shapes per material:
  - flat top
  - north edge
  - south edge
  - east edge
  - west edge
- 2 candidates per request
- 50 planned candidates total

Do not run all 25 at once unless credit spend is acceptable. Recommended next live batch:

1. grass flat top
2. dirt flat top
3. grass south edge
4. dirt south edge
5. grass-to-dirt transition edge, after adding transition request generation

## Import Contract To Resolve

Before Unity promotion, resolve one pivot/PPU contract:

- Current selected pack logical pivot: `{ x: 0.5, y: 0.25 }`
- Existing approval script terrain meta defaults: `{ x: 0.5, y: 0.75 }`
- Selected pack PPU: `64`
- Existing older terrain approval path expected `32`

The recommended contract for these 64px height tiles is:

- PPU `64`
- pivot `{ x: 0.5, y: 0.25 }`
- texture type Sprite
- sprite mode Multiple for atlas, Single for loose PNGs
- filter Point
- mipmaps off
- compression none

## Self-Review

What improved:

- We stopped using destructive recolor after it visibly degraded quality.
- Sprixen is now being used for high-value material/geometry masters instead of endless variants.
- QA now distinguishes flat terrain from raised blocks.
- We can stage large tile-family batches without spending credits.

Main remaining risk:

- Sprixen may not follow directional edge/corner prompts reliably without reference conditioning. Generate a tiny edge pilot before the full 50-candidate batch.

## Greenwake Terrain Starter Kit v1

Current concrete baseline:

`Assets/Generated/_Review/GreenwakeTerrainStarterKit_v1`

This pack is review-only and has not been promoted into runtime `Resources`.

Included tiles:

- `greenwake_grass_flat_v1.png`
- `greenwake_dirt_flat_v1.png`
- `greenwake_grass_to_dirt_north_v1.png`
- `greenwake_grass_to_dirt_south_v1.png`
- `greenwake_grass_to_dirt_east_v1.png`
- `greenwake_grass_to_dirt_west_v1.png`
- `greenwake_grass_raised_block_v1.png`
- `greenwake_dirt_raised_block_v1.png`

Review artifacts:

- `starter_kit_contact_sheet.png`
- `starter_kit_9x9_preview.png`
- `manifest.json`
- `review_decisions.json`
- `review_report.json`
- `strict_asset_quality_report.json`

Strict QA result:

- 8 total
- 8 pass
- 0 review
- 2 warnings
- dataset ready: true

Known review concern:

- Both raised blocks pass, but QA warns that side faces dominate the top face. Check readability in Unity before promotion.

Next gate:

- Owner reviews the contact sheet and 9x9 preview. If accepted, create the Unity review scene/import metadata. If rejected, refine only this eight-tile starter kit before generating more terrain.

Next best action:

- Add transition-request staging for grass/dirt and grass/forest-floor, then run a tiny Sprixen pilot of 4 to 6 requests total.

## Reference Lock Pilot Update

Confirmed from Sprixen docs and local request handling that `referenceImageUrl` is supported. I wired selected-master source URLs back into the staged Greenwake tile requests so future Sprixen calls can use the approved master image as a style reference.

Tiny reference-locked pilot results:

- `greenwake_grass_flat_top_ref_v1`
- `greenwake_grass_south_edge_ref_v1`
- `greenwake_dirt_south_edge_ref_v1`

Output comparison:

- `Assets/Generated/_Review/greenwake_reference_lock_pilot_v1/reference_lock_comparison.png`

Result:

- Reference lock significantly improves palette/style adherence compared with prompt-only requests.
- It still does not reliably obey exact edge geometry. The grass south-edge reference result copied the master look more than it changed the tile shape.

Decision:

- Use Sprixen/reference output as material masters.
- Use local deterministic geometry masks for exact flat/edge/transition shapes.
- Keep live Sprixen generation for new material looks, not every edge/corner permutation.

## Local Hybrid Geometry Pass

Added and ran:

- `Tools/AssetForge/derive_tile_geometry_variants.py`

Output:

- `Assets/Generated/_Review/greenwake_geometry_derived_v1/derived_geometry_contact_sheet.png`
- `Assets/Generated/_Review/greenwake_geometry_derived_v1/derived_geometry_map_preview.png`
- `Assets/Generated/_Review/greenwake_geometry_derived_v1/derived_geometry_manifest.json`
- `Assets/Generated/_Review/greenwake_geometry_derived_v1/strict_asset_quality_report.json`

Generated local review set, pass 1:

- 5 materials: grass, dirt, forest floor, stone, path
- 5 core shapes per material: flat top, south edge, north edge, east edge, west edge
- 4 transition families: grass-to-dirt, grass-to-forest-floor, grass-to-stone, grass-to-path
- 4 transition directions per family
- 41 review tiles total

Validation:

- Strict terrain QA: `41 pass / 0 review`
- Dataset ready structurally: `true`

Art read:

- Core flat/edge shapes are now consistent enough for in-engine mockup testing.
- Transition masks are functional but still too straight/geometric for final art. They should be softened with noisy/organic edge masks before promotion.
- This no-credit pipeline is the correct direction: Sprixen gives the style-locked material master; Asset Forge owns repeatable geometry, pivots, manifests, and QA.

## Local Hybrid Geometry Pass 2

Updated:

- `Tools/AssetForge/derive_tile_geometry_variants.py`

Changes:

- Replaced hard transition splits with deterministic organic/noise masks.
- Added diagonal/corner transition variants:
  - north-east
  - north-west
  - south-east
  - south-west
- Kept output deterministic and no-credit; the same selected material masters produce the same tile set.

Output:

- `Assets/Generated/_Review/greenwake_geometry_derived_v1/derived_geometry_contact_sheet.png`
- `Assets/Generated/_Review/greenwake_geometry_derived_v1/derived_geometry_map_preview.png`
- `Assets/Generated/_Review/greenwake_geometry_derived_v1/derived_geometry_manifest.json`
- `Assets/Generated/_Review/greenwake_geometry_derived_v1/strict_asset_quality_report.json`

Generated local review set, pass 2:

- 5 materials
- 25 core flat/edge tiles
- 32 transition/corner tiles
- 57 review tiles total

Validation:

- Strict terrain QA: `57 pass / 0 review`
- Dataset ready structurally: `true`

Art read:

- This is now mockup-ready for a Greenwake terrain import/review scene.
- Core edge shapes are readable and consistent.
- Transition pieces are less synthetic than pass 1, but still not final-Sprixen-quality. They need either better organic masks, hand cleanup, or a Sprixen Style Lock/project-guided pass for final production.

Recommended next action:

- Build a review-only Unity tile palette/package from `greenwake_geometry_derived_v1` and place it in a small 9x9/16x16 test scene or preview tilemap.
- Do not promote to runtime biome generation until the in-engine review confirms pivots, height-edge readability, and terrain blending at camera scale.

## Local Hybrid Geometry Pass 3

Updated:

- `Tools/AssetForge/derive_tile_geometry_variants.py`

Changes:

- Added guarded material palettes so grass cannot inherit dirt-side colors from the raised master image.
- Replaced cropped texture sampling with procedural pixel-detail clusters derived from the approved material palette.
- Reduced heavy rim/outline weight and used procedural side faces.

Output:

- Same review folder: `Assets/Generated/_Review/greenwake_geometry_derived_v1`
- Strict terrain QA: `57 pass / 0 review`

Art read:

- Structurally stable and more consistent than pass 1/2.
- Still below the desired Sprixen-level final art quality. The local method is now useful as a geometry/QA fallback, not as the final production-art path.
- The remaining gap is painterly texture charm and natural blend design, not file correctness.

Decision:

- Stop spending effort trying to make local masks alone carry final art quality.
- Use local generation for deterministic fallback, mockups, and QA scaffolding.
- Use Sprixen Style Lock for final-art transition candidates.

## Sprixen Style Lock Transition Batch

Added:

- `Tools/AssetForge/queue_greenwake_style_lock_requests.py`

Queued review-only requests:

- `Assets/Generated/_Review/_Requests/greenwake_*_stylelock_v1`
- 4 transition families:
  - grass-to-dirt
  - grass-to-forest-floor
  - grass-to-stone
  - grass-to-path
- 8 directions per family:
  - north
  - south
  - east
  - west
  - north-east
  - north-west
  - south-east
  - south-west
- 32 request folders total
- 2 candidates per request

Important:

- These are staged only. No Sprixen calls were run.
- Each request includes the approved base material `reference_image_url`.
- Each request marks `sprixen_style_lock_required = true` and expects the local Sprixen tiles project ID from `asset_forge.local.json`.

Recommended next credit spend:

- Do not run all 32 first.
- Run only 4 pilot requests:
  - `greenwake_grass_to_dirt_north_stylelock_v1`
  - `greenwake_grass_to_dirt_east_stylelock_v1`
  - `greenwake_grass_to_forest_floor_north_stylelock_v1`
  - `greenwake_grass_to_path_east_stylelock_v1`
- If those pass visual QC, expand to the full transition set.

## Sprixen Style Lock Pilot Result

Ran only the four approved pilot requests:

- `greenwake_grass_to_dirt_north_stylelock_v1`
- `greenwake_grass_to_dirt_east_stylelock_v1`
- `greenwake_grass_to_forest_floor_north_stylelock_v1`
- `greenwake_grass_to_path_east_stylelock_v1`

Outputs:

- `Assets/Generated/_Review/greenwake_stylelock_pilot_v1_contact_sheet.png`
- `Assets/Generated/_Review/greenwake_stylelock_pilot_v1_summary.json`
- 8 candidates total
- Strict QA: `8 pass / 0 review`

Codex visual picks:

- grass-to-dirt north: `v2`, usable but too subtle
- grass-to-dirt east: `v2`, best transition result in this pilot
- grass-to-forest-floor north: `v1`, usable; `v2` reads like a raised-side tile and should not be used for flat transition
- grass-to-path east: `v2`, better than `v1`, which is too broad/patchy

Decision:

- Sprixen Style Lock is worth continuing. It beats the local procedural fallback for final-art candidates.
- Do not run all 32 queued requests yet.
- Next credit spend should complete only cardinal directions for the materials that worked:
  - grass-to-dirt north/south/east/west
  - grass-to-path north/south/east/west
- Rerun forest-floor with a stricter prompt: flat top only, no raised side walls, no block, no cliff.
- Hold diagonal/corner requests until cardinal transitions are visually approved.

## Core Pilot Update

Ran a tiny Sprixen pilot for:

- `greenwake_grass_flat_top_v1`
- `greenwake_dirt_flat_top_v1`
- `greenwake_grass_south_edge_v1`
- `greenwake_dirt_south_edge_v1`

Outputs:

- `Assets/Generated/_Review/greenwake_tile_core_pilot_v1/core_pilot_contact_sheet.png`
- `Assets/Generated/_Review/greenwake_tile_core_pilot_v1/core_pilot_assembly_preview.png`

QC:

- Grass flat tops are structurally correct but too bright/neon compared with the selected grass master.
- Dirt flat tops are closer; candidate 1 reads better than candidate 2.
- Grass south edges are structurally correct but palette-dripped toward bright green.
- Dirt south edge candidate 1 is plausible; candidate 2 drifted toward olive and should be rejected.

Implication:

- Sprixen can generate the required geometry, but style-anchor adherence is weak without stronger reference conditioning.
- Next live request should be a transition pilot only after tightening prompts around the selected master names and palette language.

Staged without spending credits:

- `greenwake_grass_to_dirt_north_blend_v1`
- `greenwake_grass_to_dirt_south_blend_v1`
- `greenwake_grass_to_dirt_east_blend_v1`
- `greenwake_grass_to_dirt_west_blend_v1`

## Sprixen Style Lock Cardinal Batch Result

Ran the next controlled Style Lock batch:

- `greenwake_grass_to_dirt_south_stylelock_v1`
- `greenwake_grass_to_dirt_west_stylelock_v1`
- `greenwake_grass_to_path_north_stylelock_v1`
- `greenwake_grass_to_path_south_stylelock_v1`
- `greenwake_grass_to_path_west_stylelock_v1`
- `greenwake_grass_to_forest_floor_north_flatonly_stylelock_v2`

Combined with the previous pilot, reviewed:

- 20 candidates total
- Strict QA: `20 pass / 0 review`
- Contact sheet: `Assets/Generated/_Review/greenwake_stylelock_cardinal_batch_v1_contact_sheet.png`
- Summary: `Assets/Generated/_Review/greenwake_stylelock_cardinal_batch_v1_summary.json`

Codex candidate picks:

- grass-to-dirt north: `v2`
- grass-to-dirt south: `v1`
- grass-to-dirt east: `v2`
- grass-to-dirt west: `v1`
- grass-to-path south: `v1`
- grass-to-path east: `v2`
- grass-to-path west: `v1`
- grass-to-forest-floor north flat-only: `v1`

Reject / rerun:

- grass-to-path north: rerun required; both candidates read like raised/side-wall tiles.
- grass-to-path south `v2`: reject; raised-side/block language.
- grass-to-dirt west `v2`: reject; raised-side/block language.
- grass-to-forest-floor original `v2`: reject; raised-side/block language.

Decision:

- Style Lock is still the right final-art route.
- Flat-only prompt language helped and should be used for transition requests.
- Do not spend on diagonal/corner transitions yet.
- Next small batch should be:
  - grass-to-path north flat-only retry
  - grass-to-forest-floor south/east/west flat-only
  - optional grass-to-dirt north reroll with stronger dirt intrusion
