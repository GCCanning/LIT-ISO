# LIT-ISO URP Lighting And Quality-of-Life Research

Purpose: determine whether to integrate Unity URP 2D lighting, and identify
quality-of-life/polish features that fit LIT-ISO.

No code changes were made.

## Current Project State

Observed locally:

- Unity version: `6000.3.11f1`.
- `Packages/manifest.json` does **not** currently include
  `com.unity.render-pipelines.universal`.
- `ProjectSettings/GraphicsSettings.asset` has `m_CustomRenderPipeline: {fileID: 0}`.
- `ProjectSettings/QualitySettings.asset` quality levels also have
  `customRenderPipeline: {fileID: 0}`.
- Current Foundation rendering uses Built-in pipeline plus custom/faked effects:
  - `IsoCore/SpriteAmbient` day/night tint shader.
  - `CampfireGlow` explicitly notes it is a faked light, not real `Light2D`.
  - Pixel Perfect Camera package is installed and used by Foundation.

Conclusion:

URP 2D lighting is viable, but it is a render-pipeline migration. It is not a
small toggle. It should happen after compile/data correctness stabilizes.

## What URP 2D Lighting Would Add

Unity URP 2D supports a dedicated 2D Renderer and 2D lighting path. Useful
capabilities for LIT-ISO:

- Global 2D light for day/night ambient color.
- Spot/point-style 2D lights for torches, lanterns, campfires, spells, portals,
  obelisks, glowing crystals, and dungeon hazards.
- Freeform/Sprite lights for windows, hearths, magic circles, boat lanterns,
  cave glow, and boss telegraphs.
- Light blend styles for separate lighting channels such as:
  - normal world light,
  - warm additive magic/fire,
  - subtractive darkness/fog/curse,
  - mask-driven material highlights.
- Shadow Caster 2D for walls, trees, buildings, dungeon props, and placed
  structures.
- Sprite Lit materials and normal maps, allowing crafted/painted pixel art to
  catch light with more depth.
- URP post-processing such as color adjustments and bloom for obelisks,
  class-awakening moments, magic, rare loot, and night ambience.

## Fit For LIT-ISO

URP lighting fits the game very well because the design has many systems where
light is gameplay, not just decoration:

- dungeon darkness,
- campfire safety,
- night monster risk,
- lantern/torch preparation,
- obelisk class ceremony,
- affinity magic,
- rare item glow,
- hearth/cooking buffs,
- boat storms and lighthouse/dock lights,
- pocket homestead mood,
- goblin raid warning fires,
- dungeon breach effects.

The strongest reason to migrate is not "prettier scenes." It is that light can
become a readable survival/dungeon resource.

## Recommended URP Migration Strategy

Do **not** migrate the whole game blindly.

Recommended path:

1. Create an isolated URP lighting branch.
2. Add URP package.
3. Create URP Asset with 2D Renderer.
4. Assign the pipeline asset in Graphics and Quality settings.
5. Create `Sprite-Lit` material path for Foundation sprites.
6. Keep existing isometric invariants:
   - `IsometricZAsY`
   - `cellSize (1,0.5,1)`
   - `transparencySortAxis (0,1,-0.26)`
   - height sorting layers
   - `TilemapRenderer.mode = Individual`
7. Convert only the Foundation scene first.
8. Build a tiny lighting test scene or test mode:
   - one ground tile,
   - player,
   - tree,
   - campfire,
   - obelisk,
   - day/night global light,
   - one shadow caster.
9. Verify:
   - sprites render,
   - sorting still works,
   - pixel-perfect still works,
   - no tile seams,
   - no material pink/missing shader,
   - day/night still readable,
   - performance remains acceptable.
10. Only then migrate real art/materials.

## URP Risks

High risks:

- Existing custom ambient shader likely needs replacement or URP-compatible
  rewrite.
- All SpriteRenderers/Tile renderers need lit-compatible materials if they are
  supposed to receive 2D lights.
- Pixel-perfect + post-processing can create softness if configured badly.
- 2D lights and shadows add render texture and overdraw cost.
- Shadow casters on many props can get expensive.
- Current renderer uses many per-cell SpriteRenderers; adding per-pixel lighting
  to every tile may be costly.
- Any automated material conversion can be one-way for scenes/assets, so do it
  on a branch with backups.

Mitigations:

- Start with global light + a few local lights.
- Use unlit materials for background/far/decorative tiles that do not need
  dynamic lighting.
- Use lit materials only on player, props, interactables, buildings, dungeon
  walls, obelisks, and important tiles.
- Use Shadow Caster 2D sparingly and combine where possible.
- Keep fake additive glows for cheap sparkle where real shadows are not needed.
- Profile before enabling normal maps everywhere.

## Lighting Feature Recommendations

### P0: Low-Risk Lighting Without Full URP

These can happen before URP:

- Better day/night color grading through existing ambient system.
- Campfire/lantern additive glow sprites.
- Obelisk pulse/glow overlay.
- Dungeon darkness vignette.
- Rare loot sparkle/glow.
- Weather overlays: fog, rain streaks, snow, lightning flash.
- Player light radius at night using soft overlay/fake light.

Use this if the team needs visible quality quickly.

### P1: URP Pilot

First URP pilot should include:

- Global Light 2D controlled by `DayNightSystem`.
- Campfire Light 2D.
- Lantern Light 2D.
- Obelisk Light 2D.
- One dungeon room with darkness and torch shadows.
- One building wall/prop using Shadow Caster 2D.
- Sprite-Lit material on player and one prop.

Success criteria:

- Visual quality is clearly better than fake glows.
- No sorting regressions.
- No pixel softness.
- No noticeable frame hitch with current visible-cell count.

### P2: Full Lighting Language

Once stable:

- Affinity lights:
  - Ember: warm orange flicker.
  - Tide: blue/green caustic shimmer.
  - Root: soft green pulse.
  - Stone: low amber/white rim.
  - Gale: pale moving streaks.
  - Glimmer: prismatic sparkle.
  - Hearth: warm broad comfort light.
- Dungeon families with lighting identity:
  - Root Cellar: green spores, warm lantern pockets.
  - Mine Shaft: hard torch light, deep black edges.
  - Flooded Shrine: blue reflections and ripple highlights.
  - Trial Vault: clean System-white glow.
  - Deep Crypt: subtractive cold darkness.
- Pocket homestead mood lights:
  - hearth,
  - windows,
  - crafting stations,
  - portal anchor,
  - farm fireflies.

## Quality-of-Life And Polish Features That Fit The Game

These are based on the current mechanics and the LitRPG direction.

### System / LitRPG QoL

- Configurable System feed filters by channel:
  Notice, Warning, Trial Evidence, Level Up, Title, Affinity, Quest, Dungeon,
  Party, World Event.
- Trial Evidence Log with "recent actions" and "dominant tendencies."
- Grade forecast meter with uncertainty.
- "Why did I get this class?" obelisk summary.
- Title progress tracker.
- Affinity resonance popups and affinity screen.
- Dungeon result screen with clear grade, loot, XP, injuries, title progress,
  affinity changes, and party contribution.
- Pin one tracked title/affinity/class goal to HUD.

### Dungeon And Expedition QoL

- Expedition loadout templates:
  Quick Delve, Deep Delve, Boss Attempt, Gathering Run, Rescue Run.
- Prep warnings instead of hard locks:
  missing food, missing antidote, low light, overloaded, no camp kit.
- Party Load Count screen.
- Auto-pack from storage for saved loadouts.
- "Return supplies to storage" after expedition.
- Dungeon retreat confirmation with estimated losses.
- Map notes for discovered dungeon hazards.

### Inventory / Crafting QoL

- Sort inventory by category.
- Stack to nearby chests.
- Quick deposit to matching storage.
- Favorite/lock items to prevent deposit/sell.
- Craft from nearby storage in homestead.
- Recipe pinning.
- Crafting queue for smelting/cooking.
- Missing ingredient source hints.
- Compare tool/weapon stats.
- Repair all equipped tools.
- Item rarity border colors.
- Tooltip tags: food, potion, camp gear, quest, crafting, affinity.

### Building / Homestead QoL

- Placement preview with footprint and doorway.
- Road connectivity overlay.
- Rotate building southeast/southwest.
- Move building after placement with cost/time.
- Blueprint mode: plan without resources, then build.
- Grid visibility toggle.
- Storage labels and filters.
- Homestead room/building list.
- NPC room validity overlay.
- Farm irrigation/water overlay.
- "Find item in storage" search.

### Exploration QoL

- Fog-of-war map.
- Landmark discovery popups.
- Route danger/time estimate.
- Known resource node markers.
- Player map pins.
- Rumor reliability markers.
- "Last seen" dangerous mob marker.
- Cartography profession map export/sell.
- Biome expertise indicator.

### Coop / Split-Screen QoL

- Per-player System feed plus shared event log.
- Player color/icon and ping marker.
- Shared quest board with owner/assist labels.
- Individual class evidence tracking.
- Shared expedition checklist.
- Loot policy setting:
  free-for-all, party share, need/use, host decides.
- Ready check before dungeon entry, sleep, boat departure, obelisk ceremony.
- Split-screen-safe compact HUD.
- Controller-first menu navigation.

### Accessibility / UX QoL

- UI scale slider.
- Font size slider.
- Colorblind-safe rarity palette option.
- Screen shake toggle.
- Flash intensity toggle.
- Hold/toggle sprint.
- Remappable controls.
- Pause behavior rules for local vs online coop.
- Tutorial reminder log.
- Reduce System notification frequency option.
- Auto-save slot rotation.

### Visual/Audio Juice

- Footstep variation by terrain.
- Tool impact particles: wood chips, stone sparks, herb leaves.
- Floating pickup text.
- Craft success sparkle/audio.
- Campfire crackle and heat shimmer.
- Rain/snow/wind ambience.
- Dawn/dusk stingers.
- Class awakening animation.
- Title acquired flourish.
- Affinity resonance color pulse.
- Goblin raid warning bell.
- Boat creak/storm audio.

## Practical Recommendation

Short term:

1. Do not start full URP migration until current compile/save/data issues are
   fixed.
2. Add fake/foundation-safe lighting polish first:
   campfire, lantern, obelisk, dungeon darkness, rare loot glow.
3. Add QoL systems that directly support the planned loop:
   System feed filters, Trial Evidence Log, expedition templates, inventory
   quick deposit, placement previews.

Medium term:

1. Create a URP pilot branch.
2. Use one small scene/slice to prove 2D Renderer + Pixel Perfect + isometric
   sorting.
3. If it works, migrate Foundation materials in controlled batches.

Long term:

Use URP 2D lighting as a gameplay language:

- darkness means preparation,
- fire means safety,
- obelisk light means System authority,
- affinity colors reveal class/magic identity,
- dungeon lighting tells the player what kind of danger they are entering.

## Sources

- Unity Manual: URP for 2D game development:
  https://docs.unity3d.com/Manual/2d-urp-landing.html
- Unity Manual: 2D lighting in URP:
  https://docs.unity3d.com/Manual/urp/2d-index.html
- Unity Manual: setup 2D Renderer asset:
  https://docs.unity3d.com/Manual/urp/Setup.html
- Unity Manual: 2D Renderer Data asset:
  https://docs.unity3d.com/Manual/urp/2DRendererData-overview.html
- Unity Manual: Light Blend Styles:
  https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/LightBlendStyles.html
- Unity Manual: Sprite Lit Shader Graph:
  https://docs.unity3d.com/Manual/urp/prebuilt-shader-graphs-urp-sprite-lit.html
- Unity Manual: Shadow Caster 2D:
  https://docs.unity3d.com/Manual/urp/2DShadows.html
- Unity Manual: Pixel Perfect:
  https://docs.unity3d.com/Manual/com.unity.2d.pixel-perfect.html
- Unity Manual: URP Color Adjustments:
  https://docs.unity3d.com/Manual/urp/Post-Processing-Color-Adjustments.html
