# Notes from Claude → Codex

> Append-only log. Newest entry on top. Codex reads this; only Claude writes here.

---

### 2026-06-19 — Worldgen province rework (coherent biomes + climate + downhill rivers)

Replaced the continent generator's per-cell biome roll with a **macro-province**
layer so biomes read as large, coherent, hand-crafted-feeling regions instead of
the per-cell patchwork. Ports the validated prototype
(`Tools/WorldGenPreview/prototype_worldgen_v2.py`). Still a pure, local, O(1)
per-cell function of `(seed, wx, wy)` — no global map — so the sampler streams
chunk-by-chunk exactly as before. Invariants untouched.

**What changed**

- **`IsoTerrainSampler.cs`**
  - New **jittered-grid Voronoi provinces**: `ProvinceSite(gi,gj)` (deterministic
    jittered site), `ResolveProvince(wx,wy,…)` (nearest of the 3×3 surrounding
    sites + lateral edge distance for the border band).
  - New **coherent climate fields**: `ClimateTemperature(fx,fy)` = north→south
    latitude triangle-gradient blended with low-frequency noise; lapse subtracted
    at the province SITE. `ClimateMoisture(fx,fy,e)` = low-freq noise + coastal
    proximity bonus (uses elevation as the cheap coast proxy).
  - New **`ProvinceBiome(gi,gj,sx,sy)`**: runs `SelectBiome` (the existing
    Whittaker rectangles) on the site-centroid lapse-adjusted climate; beach/
    mountain fall back to meadow so a province's BASE biome is always a climate
    biome. Backed by a tiny 8-slot ring cache keyed on `(gi,gj)` (perf only;
    correctness never depends on it).
  - **`SampleContinent` biome step rewired**: cell biome = its province biome,
    then the existing **local mountain elevation gate** and **beach-against-water**
    remap apply unchanged. Ocean/shore/beach/river/height logic untouched.
  - **Border band**: the B1 blend now only engages when `edgeDistCells <=
    provinceBorderBand`, and `NeighbourBiome` was repointed at the neighbouring
    PROVINCE biome — so seams blend accents toward the neighbour while the base
    stays coherent. B2/B3/B4 (crossfade/accent/feature ramps) are unchanged.
  - **Rivers**: new `RiverDistance` evaluates `riverChannelCount` separately-salted
    warped bands and keeps the nearest, so the world gets a FEW purposeful
    waterways instead of a dense web; `IsRiverValley` adds a cheap 4-neighbour
    downhill/valley gate (no global trace). Spawn-apron exclusion kept.
  - `BiomeAtClimate` and `SettlementProbeCell` now resolve biome via the province
    path (removed the stale per-cell `Perlin` climate samples).

- **`FoundationConfig.cs`** — new fields (defaults):
  - `provinceSize = 28`, `provinceJitter = 0.42`, `provinceBorderBand = 4`
  - `latitudeWeight = 0.55`, `latitudePeriod = 2600`, `temperatureNoiseWeight = 0.45`,
    `climateLapsePerHeightStep = 0.06`, `coastalMoistureBonus = 0.25`
  - `riverChannelCount = 3`
  - **Retuned existing:** `climateFrequency 0.011 → 0.006` (gentler within-province
    variation), `riverFrequency 0.025 → 0.012` (fewer/broader rivers).

- **`FoundationContent.cs`** — `desert` biome was left at the default full-range
  climate rectangle (0..1 × 0..1, priority 0), so it tied meadow everywhere and only
  lost on centroid distance. Pinned it to the genuinely **hot+dry** corner
  (`temp 0.72–1.0`, `moist 0.0–0.32`, priority 1, lapse 0.05) so only arid province
  sites resolve to desert. All other biome tile/prop pools were already derived from
  the PixelLab catalog (`asset_catalog.json`) and match — meadow→plains2_*, forest→
  forest_*, snow→snow2_*, mountain→stone_*, beach→sand_* — so no pool rewiring needed.

**Adjacency legality**: snow (cold) and desert (hot) sites are physically far apart
under the smooth latitude+noise climate (period 2600 ≫ province spacing 28), so they
can't be adjacent provinces; and the accent blend only fires for *declared*
transitions (snow declares only meadow/forest, desert declares none), so a snow↔desert
seam can never blend. No extra guard was needed.

**How to validate**
- `dotnet build IsoCore.Foundation.csproj` (sandbox had no dotnet; written
  compile-safe — owner please confirm clean).
- Unity menu **Tools/LIT-ISO/ISO-Core Foundation/Validate Foundation**.
- In-editor: load a continent seed (default config seed 1337, `continentWorld=true`,
  `flatWorld=false`) and confirm large coherent biome regions, gentle internal
  variation, a few meandering rivers, and natural seams. Tune `provinceSize`,
  `latitudeWeight`, `climateFrequency`, `riverChannelCount` to taste.
- NOTE: `Tools/WorldGenPreview/preview_worldgen.py` is a *separate* Python reference
  that reads `Assets/StreamingAssets/worldgen/*.json`; it does **not** reflect these
  C# sampler changes. The algorithm itself is validated by
  `prototype_worldgen_v2.py` / `worldgen_v2_compare.png`; the live check is in-Unity.

**Risks / not verified**
- No dotnet/Unity in the sandbox, so this is a static-review compile pass only.
- River downhill is a local valley gate, not a full steepest-descent trace (the
  streaming sampler can't do a global trace); rivers read as fewer/purposeful but
  won't perfectly follow a single source→sea path.
- `latitudePeriod`/`latitudeWeight` defaults are a starting point; if biome bands
  feel too striped or too noisy, that's the first knob to turn.

---

### 2026-06-13 — Action animations wired to gameplay + ability-wheel flicker fix

**1. All 6 LPC animations are now reachable in play (were baked but never
triggered).** New `LayeredCharacterActions` (Assembly-CSharp, attached to the
player automatically by `LayeredCharacterPlayerHook` next to
`LayeredCharacterAnimator`) calls `PlayOneShot`:
- **Basic attack = `E`** (owner-chosen), weapon-aware: bow/ranged→`shoot`,
  spear/polearm→`thrust`, magic/staff/wand→`cast`, sword/axe/blunt or
  unarmed→`slash`. Weapon read from the equipped `slot=="weapon"` accessory.
- **Spell cast** ← `SpellCaster.OnSpellCast` (keys 1–4): Projectile delivery →
  `shoot`, otherwise `cast`.
- **Hurt** ← `PlayerHealth.OnHealthChanged`: any health decrease → `hurt`.
- Movement is NOT locked during a one-shot (owner choice); facing keeps updating.
- Purely visual — deals no damage. If/when a basic-attack damage move lands,
  hook the same `E` press.

**⚠ Key overlap to confirm (owner said "maybe E"):** `E` is already the interact
key (`IsoInteractionController`/`DungeonEntrance`) AND the ability-wheel "E"
slot. Attack is suppressed while any UI modal is open (incl. the hold-X wheel),
but it will still co-fire with a contextual interact, and once the real ability
runtime lands, tapping `E` would both cast the E quick-skill and swing. The key
is a single field (`LayeredCharacterActions.attackKey`) — easy to rebind, or I
can gate attack to only fire when slot E is unassigned. Say the word.

**2. Ability-wheel (hold-X) screen flicker fixed.** Root cause: `AbilityWheelView`
opened the wheel and set the `"abilityWheel"` modal, which made
`FoundationUiCoordinator.BlocksWorldInput` true, which the next frame forced the
wheel closed (`wheelOpen = X && !blocked`), clearing the modal, reopening it… a
1-frame open/close loop. Fix: added `FoundationUiCoordinator.HasBlockingModalExcept`
/ `BlocksWorldInputExcept`, and the wheel now (a) stays open purely on X-held,
ignoring its own modal + pointer-over-its-own-UI, and (b) only gates the INITIAL
open on *other* panels being open. No contract change; just stable now.

**Files:** `LayeredCharacterActions.cs` (new), `LayeredCharacterPlayerHook.cs`
(attach it), `AbilityWheelView.cs` (flicker), `FoundationUiCoordinator.cs` (+2
helpers). Couldn't run a Unity compile in this env — please play-check: E swings
(and matches equipped weapon), keys 1–4 play cast/shoot, taking damage plays
hurt, and hold-X opens a steady (non-flickering) wheel.

---

### 2026-06-13 — Creator UI reskin + slider/swatch colour pickers + weapons/armour gear import

Owner-approved follow-ups to the v3 catalog work. All Claude-lane (creator UI +
import pipeline); no Foundation contract changes.

**1. Character creator UI reskinned with the shared Menu art.**
`CharacterCreatorUI` now loads `Resources/UI/Menu` sprites (panel / button +
button_hover/button_pressed / slider_track / slider_handle / input_frame),
9-sliced, so the creator matches the welcome/main-menu look. If any sprite is
missing it falls back to flat panels + outlines, so it always renders.

**2. Colour selection is now a slider + live swatch (was `< >` arrows).**
Skin tone, eye colour, and each clothing slot's colour use a horizontal slider
that scrubs the variant list, with a colour swatch beside it. The swatch colour
comes from new `CharacterCompositor.SwatchColor(def, variant, body)` — the
average of the variant's non-outline opaque pixels on the walk sheet (cached).
Item selection (which hair/shirt/pants/shoes) stays as `< >` arrows. Body type
stays arrows. Creator remains **basic customization only** — no gear.

**3. Weapons & armour imported as equippable gear (catalog still v3, now 47
items, was 31).** `import_lpc.py` curated set extended with LPC `weapon/` +
`armour/` sets, plus a new rule: **layers with a `custom_animation` key are
skipped** (LPC's oversize slash/thrust sheets are 128/192px cells outside our
64px 6-anim grid; only the *universal* weapon layers — weapon-in-hand across all
6 classic animations — are imported). New slots: `weapon` (longsword, dagger,
rapier, mace, waraxe, spear, bow, gnarled staff, simple staff), `shield`,
`armor_torso` (plate/leather/chainmail), `armor_legs`, `armor_feet`,
`armor_shoulders`. These are **NOT in the creator** — they render via
`CharacterEquipmentVisuals.Equip(itemId, variant)` from in-game drops (e.g.
`Equip("lpc/weapon_sword_longsword", "longsword")`), compositing by LPC `zPos`.
Verified: all 11,484 catalog-referenced layer files exist (0 missing), gear
covers all 6 animations × both bodies × every variant.

**Your slice / FYI (Codex/gameplay lane, not done here):** the drop/equipment
system can already render any of these by id, but there's **no slot-exclusivity
logic** — `EquipAccessory` just appends to the `accessories` list, so equipping
two torsos (or two weapons) would stack. When the inventory/equip flow lands,
add one-item-per-region replacement (suggest mapping the new slot names →
equipment slots and unequipping the previous occupant before `Equip`).

**Build note:** couldn't run a Unity compile in this environment (engine module
DLLs aren't present); changes are `CharacterCreatorUI.cs` (rewrite),
`CharacterCompositor.cs` (+`SwatchColor`), and the regenerated
`layer_catalog.json` + credits. Please do a play check: open the creator (New
Game) → panels/buttons/sliders show the Menu skin, colour sliders + swatches
work for skin/eyes/hair/shirt/pants/shoes, Randomize/Done OK; then test a gear
`Equip` call renders in-hand/on-body across directions.

---

### 2026-06-13 — LPC character creator: all 6 classic animations + player rescale (v3 catalog)

`layer_catalog.json` is now **v3**. Every wardrobe item (all 31 curated LPC
items) now ships sheets for **all 6 classic LPC animations** — `walk` (idle +
9-frame walk), `cast` (7), `thrust` (8), `slash` (6), `shoot` (13), `hurt` (6)
— not just walk. These are the "rows 0-20" block present on every LPC sheet
(every curated source is >= 832x1344px), so coverage is identical across body,
hair, clothes, hats, etc. — no missing-row fallback needed.

- `CharacterCompositor.Bake(appearance, animId)` now bakes one animation at a
  time (`animId` defaults to `catalog.defaultAnimation` = "walk").
- `LayeredCharacterAnimator` bakes "walk" eagerly (drives idle/walk from
  movement, as before) and the other 5 lazily/cached. New API:
  `PlayOneShot("slash"|"thrust"|"shoot"|"cast"|"hurt", onComplete)` plays the
  action once at the current facing row then returns to walk/idle — **not
  wired to any gameplay trigger yet** (no combat system calls it). If/when
  combat lands, this is the hook.
- `CharacterCreatorUI` preview has a new `<  Animation  >` cycler (below the
  rotate/Walk controls) so you can sanity-check every equipped item across
  all 6 animations.
- **Player rescale**: `pixelsPerUnit` changed 50 -> 64 (64px frame = 1 world
  unit, matching the old PlayerAnimator's 128px@128ppu reference scale). This
  was the previous "player looks oversized next to tiles/props" issue —
  purely a catalog value, no animation/layout changes. If still too big/small,
  adjust `pixelsPerUnit` in `import_lpc.py` and re-run (catalog-only regen,
  instant).
- `Pixel Pipeline/character_forge/README.md` updated for the v3 contract.

Old v2-layout files (`lpc/<item>/l<N>/<body>/<variant>.png.bytes`, no `{anim}`
folder) are orphaned on disk — harmless, just unused.

---

### 2026-06-13 — Dungeon overhaul: tiered rooms, lava/fire traps, void cells outside the layout, streaming everywhere

Owner request, all of this is Codex-lane (`IsoCoreFoundation/Dungeons`,
`IsoCoreFoundation/World`, `IsoCoreFoundation/Core/FoundationContent.cs`,
plus possibly the root `IsoWorldChunkManager.cs`). I audited the current
state first — summary below each item — then the ask.

#### 1. Corridor width: tighten to 2-4 tiles

`FoundationDungeonGenerator.CorridorRadiusForTier` (lines 185-193) currently
returns a radius (1-3) that's used to dig corridors — radius 1 = 3 tiles
wide, radius 3 = 7 tiles wide at high tiers, which is wider than asked.
Please clamp so corridor width = `radius*2+1` lands in **2-4 tiles** across
all tiers — e.g. radius 0 (1 tile, too narrow) should become a minimum digwidth
of 2 by digging an asymmetric 2-wide strip rather than a centered odd-radius
strip, and the max should cap at radius ~1.5 equivalent (4 wide) instead of
the current radius-3/7-wide at tier 6. A simple `Mathf.Clamp(width, 2, 4)`
on the corridor cross-section in `Connect()` (lines 159-183) should do it —
exact tiling approach is your call, just keep it in [2,4] for every tier.

#### 2. Room variety as tiers go up

Already mostly there (`roomMin`/`roomMax` widen with tier, lines 41-42,
`roomCount` grows 8→18). No change needed structurally — just make sure
whatever room-kind logic (`Spawn`/`Exit`/`Combat`/`Arena`/`Junction`,
lines 315-380) keeps producing a good mix at higher tiers (more `Arena` rooms
since the ≥150-cell threshold becomes easier to hit with larger `roomMax`).
Flag if you think the existing mix needs rebalancing once lava traps (below)
are added — traps probably shouldn't spawn in `Spawn`/`Junction` rooms.

#### 3. Lava/fire trap tiles (new — nothing like this exists today)

Confirmed zero `lava`/`fire`/`trap`/`hazard` entries anywhere in
`FoundationContent.cs` or `IsoCell.cs`. Needed:

- New block defs in `FoundationContent.cs` `Blocks` registry: `lava` and
  `fire_trap` (or one `dungeon_lava` block reused for both visual states —
  your call). I can generate PixelLab tile art for these (animated-looking
  lava/fire tiles) if you give me the target `Assets/Resources/Tiles/<id>`
  filenames — ping back here with the exact IDs you pick and I'll run the
  generator and promote the art (Claude lane, `Tools/PixelLab` +
  `Assets/Resources/Tiles`).
- `IsoCell` needs a hazard flag (e.g. `public bool Hazardous;` or reuse
  `SurfaceBlockId == "lava"`/`"fire_trap"` as the check — simplest is just
  checking `SurfaceBlockId` string, no new field needed if you're OK with
  that).
- `FoundationDungeonGenerator.BuildCells` (lines 205-238): after normal floor
  assignment, stamp a tier-scaled fraction of non-`Spawn`/non-`Junction`
  floor cells (suggest ~3-6% of `Combat`/`Arena` room floor cells, scaling up
  with tier) with `surfaceBlockId = "lava"` or `"fire_trap"` instead of the
  normal `DungeonFloorBlock()` pick. Keep `underBlockId` as the normal floor
  so removing/extinguishing a trap later reveals normal floor.
- Gameplay: damage-on-enter when the player's cell has a hazard surface block
  — wire into whatever movement/collision check currently reads
  `IsoCell`/`SurfaceBlockId` (similar pattern to `WeatherManager`'s
  `OutdoorDamageRoutine` / `PlayerHealth.TakeDamage`, which I can point you
  to if useful — that's Claude-lane code I wrote, in
  `Assets/Scripts/World/Weather/WeatherManager.cs` lines 275-291).

#### 4. Void cells outside the dungeon layout + ambient VFX

Confirmed: every cell in a dungeon's render bounds (+1 padding) currently
gets a real floor-or-wall block — there's no "nothing here" cell, and
`IsoWorldRenderer.Configure` (lines 163-204) always renders *something*
(falls back to a magenta placeholder cube for unresolved block IDs, lines
167/188 — never skips rendering).

Ask: any render cell whose `SurfaceBlockId` is empty/a new sentinel
`"void"` should be **skipped entirely** by `IsoWorldRenderer.Configure` (no
sprite, no placeholder cube — literally nothing drawn, so the player sees
through to the background/skybox). Concretely:
- `FoundationDungeonGenerator.BuildCells`: cells inside the render-bounds
  rectangle but NOT part of any room/corridor footprint (currently forced to
  `WallBlock = "stone_block"`) should instead get `surfaceBlockId = "void"`
  and `solidBlock = false` (not walkable, but also not a wall — see below for
  how that interacts with collision). Actual room/corridor floor and the
  walls immediately ringing a room/corridor stay as they are now (so players
  still see walls around rooms, not floating floors with no edges) — only
  cells beyond that immediate wall ring become void.
- `IsoWorldRenderer.Configure`: add an early-out for
  `cell.SurfaceBlockId == "void"` (or null/empty) — don't rent/configure a
  SpriteRenderer for it at all.
- Collision/walkability: void cells should block movement (player can't walk
  into the void) but render as nothing — i.e. treat like `SolidBlock` for
  `IsoWorld.IsWalkable`/`IsBlocked` purposes even though they're not drawn as
  a wall block. Either add a distinct check (`SurfaceBlockId == "void" =>
  blocked`) in `IsoCell.Blocked`/`IsoWorld.IsWalkable`, or just set
  `solidBlock = true` on void cells but give the renderer's void check
  priority over the normal solid-block sprite path (i.e. void check happens
  before the `block.color`/placeholder fallback, regardless of `solidBlock`).
- **Ambient VFX for void**: clone the pattern in
  `Assets/Scripts/IsoCoreFoundation/World/AmbientParticles.cs` (116 lines —
  code-built `ParticleSystem`, day/night cross-fade between two presets) into
  a new `VoidAmbientParticles.cs` (or extend `AmbientParticles` with a third
  "Void" preset) that activates when the player's current cell or camera
  view contains void cells — dark drifting motes/ash, low alpha, using
  `Assets/PixelWeatherAsset/Materials/Fog.mat` + `fog.png` as a starting
  texture (only existing fog-style asset in the project) tinted near-black/
  deep-purple. Doesn't need to be fancy — same emitter-attached-to-camera
  approach as `AmbientParticles` is fine.

#### 5. "Map only updates as you move" — apply everywhere including dungeons

Good news: the Foundation pipeline (`IsoWorldController` +
`IsoWorld.GetOrCreateChunk`, in `Assets/Scripts/IsoCoreFoundation/World/`)
**already does this** — chunks/cells are generated lazily on first access and
streamed by player-chunk position (`IsoWorldController` doc comment: "Only
re-streams when the player crosses a chunk boundary"), and dungeons already
reuse this same controller via `FoundationInstanceSystem`'s render-cell
override. So if the player is seeing the *whole* dungeon rendered at once
right now, the likely cause is `FoundationDungeonGenerator` building+pushing
the *entire* `FoundationDungeonBuild.cells`/`renderCells` set as the
"desired"/instance cell list in one shot (bypassing the normal radius-based
streaming for instance cells), rather than `IsoWorldController` itself.
Please check: when `_instanceRenderCells`/`explicitInstanceCells` is set
(`IsoWorldController.cs` ~line 34/120), does `DrainStreaming`/`Retarget` still
respect `viewRadius`/`StreamCellsPerFrame`, or does it dump everything in
`_instanceRenderCells` immediately? If the latter, gate it the same way the
overworld is gated (only show cells within `viewRadius` of the player's
current position within the instance, re-stream as they move through
corridors/rooms) — same per-frame budget (`StreamCellsPerFrame`) should
apply. The legacy `IsoWorldChunkManager.cs` (root `Assets/Scripts/`, ~2449
lines) is a separate older system — confirm whether it's still active
anywhere; if it's dead code now that Foundation streaming covers everything,
flag it for removal in a future cleanup (don't delete now, just note it).

No blocking order between items 1-5 — 3 and 4 are the biggest net-new pieces.
Ping back here once you've picked the lava/fire-trap block IDs so I can
generate/promote the tile art.

#### Addendum (same day) — use the already-promoted `dungeon2_00..15` set, lava tiles already exist

Owner pointed out the `dungeon2_00..15` family (`Assets/Resources/Tiles/`,
32x32, already promoted, NOT yet wired into `FoundationDungeonGenerator`
which currently only uses `dungeon_floor_1..5` + `stone_block`) already
contains lava/fire-trap art — no new PixelLab generation needed for item 3.
I inspected all 16 (4x4 grid, row-major, `dungeon2_00` = top-left):

- **Lava/fire trap tiles**: `dungeon2_05` and `dungeon2_14` — both glowing
  orange cracked-rock tiles (14 is the brighter variant). Use these directly
  for item 3's `lava`/`fire_trap` block IDs (e.g. register two blocks
  `lava` → `dungeon2_05`, `fire_trap` → `dungeon2_14`, or one `lava` block
  with two visual variants picked by hash like `DungeonFloorBlock()` does).
- **Plain stone floor variants** (gray, safe to use like
  `dungeon_floor_1..5`): `dungeon2_00`, `01`, `03`, `06`, `09`, `12`.
- **Overgrown/mossy floor variants** (good for higher-tier "ruins" rooms):
  `dungeon2_08`, `15`.
- **Wall/decorative blocks**: `dungeon2_02` (rune sigil wall — good for
  `Spawn`/`Exit` room walls or a special marker wall), `dungeon2_07` and
  `dungeon2_11` (plain stone block walls — replace/augment `stone_block` as
  `WallBlock`).
- **Dark "void-ish" floor tiles**: `dungeon2_04` and `dungeon2_13` (near-black
  smooth/speckled tiles). These are NOT a substitute for the true
  render-skipped void cells in item 4, but could be used as a transition
  ring (1 cell) between real floor and true-void cells if a hard cutoff looks
  bad — your call, optional.

Concrete ask: replace `FoundationDungeonGenerator`'s `DungeonFloorBlock()`
pool (currently `dungeon_floor_1..5`, lines 17-24/240-248) with the
`dungeon2_00/01/03/06/09/12` set (+ `08`/`15` weighted lower for
"overgrown" flavor at higher tiers if you want tier-based palette variety),
and `WallBlock = "stone_block"` (line 10) with `dungeon2_07`/`dungeon2_11`
(rotate or hash-pick between the two for variety; reserve `dungeon2_02` for
`Spawn`/`Exit` room walls specifically as a landmark). Then layer in the
`lava`/`fire_trap` hazard stamping from item 3 using `dungeon2_05`/`14`.
`dungeon_floor_1..5`/`stone_block` can stay defined for back-compat.

---

### 2026-06-13 — PixelLab asset audit: missing nodes + ring-of-biomes test world spec

Task #23-25. Full audit in `Docs/PIXELLAB_ASSET_PLAN.md` — please read that
first for context on what's promoted vs. wired. This entry is the actionable
spec; three independent pieces, do in any order.

#### 1. Missing ResourceNodeDefinitions (ore ladder + decor)

`FoundationContent.cs` has no Node entries for these, but they're referenced
by biome JSON decor/feature lists. Art is in
`Tools/BiomeSketch/assets/prop/review_candidates/pixellab_props/{ores,ambient,forest,mountain}/`
(promote the relevant PNGs to `Assets/Resources/Props/` the same way existing
nodes were promoted, then define via the `Node(id, color, toolType, mandatory,
hits, h, drops)` factory around line 328):

- `ore_copper`, `ore_iron`, `ore_silver`, `ore_gold`, `ore_manacrystal`,
  `ore_starmetal` — pickaxe-type, progression difficulty matches name order
  (copper easiest, starmetal hardest). Drops should feed the matching
  ingot/material chain if one exists, else a raw-ore item per tier.
- `glowbug`, `wisp` — ambient light props, no tool requirement, low/no hits
  (decorative + maybe a tiny light-dust drop). Referenced by snow/forest/
  dungeon "ambient" decor notes.
- `forest_dead_tree`, `forest_stump` — forest decor, axe-type, similar to
  existing `stump`/`log` but distinct art/footprint per the triage.
- `rock_outcrop` — referenced by `beach.json` `decor.drySandRocks` (density
  0.03, "apron" band) and mountain/snow decor; pickaxe-type, similar tier to
  `shared_gray_rock`.

Then add `BiomeNodeSpawn` entries to the relevant `BiomeDefinition` assets:
ore ladder → mountain (+ snow for upper tiers per existing decor notes),
`glowbug`/`wisp` → snow/forest/dungeon ambient, `forest_dead_tree`/
`forest_stump` → forest, `rock_outcrop` → beach (`decor.drySandRocks`) +
mountain/snow per their decor blocks. Use the density values already given in
each biome JSON's `decor` section as `chancePerCell`.

#### 2. Beach BlockGroupDefinition (carried over from prior entry, still open)

Unchanged from the entry below — still needed if not already done.

#### 3. Ring-of-biomes showcase world

Add a new build step to `FoundationCreationInstanceShowroom.cs` (or a sibling
static class if you'd rather keep the file size down), e.g.
`BuildBiomeRing(FoundationWorld world, ...)`, called from `PrepareWorld`/
`BuildShowroom` after the existing showroom content. Goal: player can walk in
a ring around spawn and see every biome's real surface tiles plus its
characteristic weather running constantly, for visual QA.

- Layout: 6 wedge/ring sectors around the existing showroom area (expand
  `MinX/MaxX/MinY/MaxY` bounds as needed — current bounds are
  `(-32,-28)..(54,30)`, suggest growing the ring out to roughly a 40-48 tile
  radius). Sectors: forest, meadow, mountain, snow, beach, dungeon-stone
  (use the existing dungeon_stone tile group for the 6th wedge as a stand-in
  "dungeon entrance" look — no need for actual dungeon logic).
- Each sector: paint with that biome's real `surfaceGroup`/tile pool via
  `AddRect`/`AddCell` (same helpers used elsewhere in this file) — pull tile
  IDs from each biome's `BlockGroupDefinition`/`surfaceBasePool` so this
  reflects the live data (forest_grass_base, meadow's grass family,
  mountain_stone, snow tiles, beach_00.., dungeon_stone).
- Weather per sector — **do not** route through the global singleton
  `WeatherManager` (it only supports one active weather for the whole scene).
  Instead, for each sector, look up the matching `WeatherDefinition` (e.g. via
  `Resources.Load` or a small inline array) and if it has a
  `particlePrefab`, `Instantiate` it directly at the sector's center
  world position, parented to that sector's anchor (not the camera), and
  `Play()` it so it loops continuously regardless of player position:
  - snow sector → blizzard/snow `WeatherDefinition.particlePrefab`
    (`PixelWeatherAsset` `SnowController`).
  - forest + meadow → light rain (`RainController`).
  - mountain → fog/low-visibility tint if a fog particle/overlay exists,
    otherwise skip (no fog asset known — leave a TODO comment).
  - beach, dungeon-stone, plains(center) → no constant weather (clear).
- Keep it additive/optional: gate the whole ring behind a bool field (e.g.
  `public bool buildBiomeRing = true;`) on the showroom config so it can be
  disabled without touching the rest of the showroom.

No rush on ordering between #1 and #3 — #1 unblocks biome decor density, #3
is the visual showcase the player asked for. Ping back in this file if any of
the tile/prop IDs referenced above don't match what's actually in
`Assets/Resources/` and I'll re-check the promotion step.

---

### 2026-06-13 — beach_00..15 art is promoted but not wired into the beach biome at runtime

Follow-up to #19/#20. Confirmed today: the `beach` runtime biome still renders
with the legacy `sand_1`/`sand_2` blocks (`FoundationContent.cs` lines ~69-70,
`IsoTerrainSampler.SurfaceVariant` fallback at lines 313 and 453) — the
promoted 16-tile `beach_00..15` PixelLab family (`Assets/Resources/Tiles/`,
done in #19) has no `BlockGroupDefinition` and isn't referenced by any
`BiomeDefinition.surfaceGroup`.

I've classified the 16 tiles by pixel inspection and staged the intended pool
in `Assets/StreamingAssets/worldgen/biomes/beach.json` → `surfaceBasePool`
(`wetBand`: beach_02/08/14 dark wet sand + beach_03 shells; `dryApron`:
beach_00/12/06 light sand + beach_01/07/13 ripple texture) and
`surfaceAccents` (beach_05/11 grass tufts, beach_09/15 pebbles, beach_04/10
seaweed clumps — `accentRate` 0.12). This mirrors the shape of
`meadow.json`/`snow.json`'s pools, which I believe you already have a plan to
consume per the Phase 1 sampler spec below.

When you build that sampler work (or sooner, if it's a quick win on its own):
please create a `BlockGroupDefinition` for `beach` sourcing
`beach_00..15` per the wetBand/dryApron/accent split above, point the beach
`BiomeDefinition.surfaceGroup` at it, and update the `SurfaceVariant(...,
"sand_1")` fallback calls (lines 313/453) to `"beach_00"` so even the
no-surfaceGroup fallback case shows the new art instead of the old flat-color
sand block. `sand_1`/`sand_2` can stay defined in `FoundationContent.cs` for
back-compat (other things may reference them) — they just shouldn't be the
*visible* beach surface anymore.

---

### 2026-06-12 — Phase 1 worldgen DATA done (A3/A4 climate table + B1-B4 blend bands): sampler spec for you

Task #20. Read `Docs/WORLDGEN_RULES_PROPOSAL.md` Phase 1 (A3/A4, B1-B4). Confirmed
by reading `IsoTerrainSampler.cs` + `BiomeDefinition.cs`: **none of A3/A4/B1-B4 are
implemented yet** — `SelectBiome` is still pure nearest-centroid on
`BiomeDefinition.temperature/moisture` (single point, not a rectangle), there is
no lapse rate, and there is no border/blend logic anywhere in `SampleContinent`.
So this is all new sampler work, not a refinement. I've data-specced everything
in `Assets/StreamingAssets/worldgen/biomes/*.json`; this entry is the
implementation spec. All field names below are EXACT — match them.

#### A3 — climate table (replace SelectBiome's nearest-centroid)

Every biome JSON now has a `climate` block:
```json
"climate": {
  "temperatureRange": [tMin, tMax],
  "moistureRange": [mMin, mMax],
  "priority": <int>
}
```
Values per biome: meadow `[0.35,0.75]x[0.0,0.45]` prio 0, forest
`[0.25,0.7]x[0.45,1.0]` prio 1, snow `[0.0,0.25]x[0.0,1.0]` prio 2, beach/mountain
`[0.0,1.0]x[0.0,1.0]` prio -1 (never win — see note below).

**New `SelectBiome(t, m)`** (still a pure function of the lapse-adjusted climate
point, A3):
1. Collect every biome whose `climate.temperatureRange`/`moistureRange`
   rectangle contains `(t, m)` (inclusive).
2. If 0 matches: fall back to the OLD nearest-centroid behavior using
   `BiomeDefinition.temperature/moisture` as a safety net (don't hard-fail —
   rectangles don't tile the full unit square cleanly at every seam; the
   nearest-centroid fallback covers any gap cells).
3. If 1 match: that's the biome.
4. If 2+ matches: pick the one with the HIGHEST `climate.priority` (ties broken
   by nearest-centroid distance as today).
5. beach (`priority: -1`) and mountain (`priority: -1`) will basically never win
   step 4 against meadow/forest/snow (priority 0/1/2) — this is intentional.
   beach/mountain are NOT selected via this table; they're gated separately
   (beach by `SampleContinent`'s existing elevation/water-adjacency + the
   existing `if (biomeIndex == _beachIndex) biomeIndex = _meadowIndex;` remap;
   mountain by A4 elevation gate below). Their `climate` rectangles only exist
   so step 2's fallback table is complete for any non-continent caller of
   `SelectBiome`. **No desert.json was added** (proposal cleanup item 5) — per
   the "remap hot/dry to meadow" option, meadow's rectangle (`temp 0.35-0.75,
   moisture 0.0-0.45` = hot+dry) already covers desert's climate niche, so a
   desert SelectBiome candidate naturally resolves to meadow via priority 0
   with zero new data. If you'd rather commission desert art later, flag it and
   I'll add `desert.json` + a suite entry then.

#### A4 — elevation-gated mountain + lapse rate

`meadow.json`/`forest.json`/`mountain.json` each have a new `lapseRate` block:
```json
"lapseRate": { "perHeightStep": <float> }
```
meadow `0.05`, forest `0.06`, mountain `0.08`. snow/beach have none (treat as 0).

**Pseudocode for `SampleContinent`'s land branch** (where `SelectBiome(temp,
moist)` is currently called at line ~326):
```csharp
// A4 step 1: provisional biome from RAW climate (for lapse rate lookup only)
int provisional = SelectBiome(temp, moist);
var provBiome = BiomeAt(provisional);
float lapse = provBiome?.lapseRatePerHeightStep ?? 0f;  // new field, see below

// A4 step 2: lapse-adjust temperature by height BEFORE the real biome pick.
// height is the cliff-tier (1-4) computed a few lines below today - you'll need
// to compute elevation/height tier BEFORE calling SelectBiome now, or do a
// two-pass: pick height tier first (pure fn of e), then lapse-adjust temp,
// then pick biome.
float effectiveTemp = temp - lapse * height;  // height = tier 1-4 as computed today
int biomeIndex = SelectBiome(effectiveTemp, moist);

// A4 step 3: elevation gate for mountain - independent of climate table.
// mountain.json's new "elevationGate.minHeight": 3
if (height >= mountainBiome.elevationGateMinHeight)
    biomeIndex = _mountainIndex;

// existing beach remap stays AFTER this:
if (biomeIndex == _beachIndex) biomeIndex = _meadowIndex;
```
Net effect: a meadow cell (temp ~0.5) at height 4 gets `effectiveTemp = 0.5 -
0.05*4 = 0.30`, which falls into the gap between meadow's range (starts at 0.35)
and snow's range (ends at 0.25) — the nearest-centroid fallback resolves that
gap. At height 7: `0.5 - 0.35 = 0.15` lands squarely in snow's range
`[0.0,0.25]`. This is the "high meadow becomes snow" effect from the proposal.
Forest's steeper 0.06/step crosses into snow sooner (treeline). Tune the two
constants in meadow.json/forest.json if the crossover height feels wrong in
engine — they're data, not magic numbers in code.

`BiomeDefinition.cs` needs new serialized fields to carry this from JSON into
the runtime asset (currently only `temperature`/`moisture` exist):
```csharp
public Vector2 temperatureRange = new Vector2(0f, 1f);
public Vector2 moistureRange = new Vector2(0f, 1f);
public int climatePriority = 0;
public float lapseRatePerHeightStep = 0f;
public int elevationGateMinHeight = -1; // -1 = not elevation-gated; mountain = 3
```
Whatever your JSON->ScriptableObject loader is (FoundationContent biome import),
map `climate.temperatureRange/moistureRange/priority`, `lapseRate.perHeightStep`,
and `elevationGate.minHeight` -> these fields (mountain.json is the only biome
with `elevationGate`; others omit it -> default -1). I used `3` for mountain to
match the existing `stratification[0].band[0]`; feel free to instead read
`noise_params.elevation.mountainAbove` directly if you'd rather keep one source
of truth for the elevation threshold — either is fine, just pick one and note
which here if you change it.

#### B1-B4 — border distance field + blend band + crossfade + ramps

Every biome JSON now has `transitionTo.<otherBiomeId>` entries shaped like:
```json
"transitionTo": {
  "<otherBiome>": {
    "cells": [minWidth, maxWidth],
    "baseWeightCurve": "linear",
    "accentRamp": {
      "ownAccentWeightAtNear": <0..1>,
      "ownAccentWeightAtFar": <0..1>,
      "otherAccentWeightAtNear": <0..1>,
      "otherAccentWeightAtFar": <0..1>
    },
    "featureDensityRamp": {
      "groveCentersPer100AtNear": <float>,
      "groveCentersPer100AtFar": <float>
    }
  }
}
```
Pairs filled in: meadow<->forest (cells [4,7]), meadow<->beach (cells [2,3],
mirrors `beach.json transitionTo.meadow`), meadow<->snow (cells [5,8], elevation-
driven), forest<->snow (cells [5,8]), mountain<->meadow / mountain<->forest
(cells [4,6]), mountain<->snow (cells [3,5]), snow<->meadow / snow<->forest
(cells [5,8], carries `thawTiles: ["snow2_08","snow2_09"]`), beach<->meadow
(cells [2,3]). mountain's transitionTo uses `rockPropDensityAtNear/AtFar`
instead of `groveCentersPer100AtNear/AtFar` in its `featureDensityRamp` (see B4
below).

"Near"/"Far" convention: **Near = this biome's interior side of the band, Far =
the border / other-biome's interior side.** So `ownAccentWeightAtNear=1.0,
ownAccentWeightAtFar=0.2` means "my own accents dominate deep in my territory
and fade to 20% at the border."

**Pseudocode** (B1-B4, runs once per land cell after `biomeIndex` is finalized
by A3/A4 above):
```csharp
// B1: border distance field - probe the SAME SelectBiome (post-lapse) at N
// offsets around (wx,wy). Cheap: reuse the existing 4-neighbour pattern already
// used for cliff-lip detection (ContinentTier probes), extended to 8 offsets at
// a fixed probe radius (try radius = 1 cell first; widen to 2-3 if band reads
// too abrupt - climate fields are low-frequency and macro-cells are large, so
// probing radius 1-3 with the SAME effectiveTemp/moist climate field - not
// re-rolling noise - should be enough).
// For each of the 8 directions, find the NEAREST cell (within maxCells of any
// transitionTo entry for biomeIndex) whose SelectBiome result differs from
// biomeIndex. distanceToBorder = min over directions of that probe distance
// (clamped to the matching transitionTo[otherBiome].cells range).

// B2: if distanceToBorder <= transitionTo[otherBiome].cells[1] (inside the band):
float t = Mathf.InverseLerp(cells[1], cells[0], distanceToBorder);
// t=0 at the far edge of the band (cells[1] away = border), t=1 deep inside
// (cells[0] or less = fully "own"). baseWeightCurve "linear" = use t directly;
// reserve other curve names (e.g. "smoothstep") for future tuning without a
// schema change - if absent, treat as "linear".
float ownWeight = t;        // weight for THIS biome's surfaceBasePool
float otherWeight = 1f - t; // weight for the OTHER biome's surfaceBasePool
// Pick the surface tile by rolling Hash01(wx,wy,salt) against ownWeight: if
// < ownWeight, sample from biomeIndex's surfaceBasePool/surfaceBase as today;
// else sample from the OTHER biome's surfaceBasePool/surfaceBase. This is the
// B2 "weighted base-pool crossfade" - replaces the hard palette switch.

// B3: accent ramp - same t, lerp the accent ROLL CHANCE (not which accent, the
// chance of rolling an accent at all from each side's surfaceAccents list):
float ownAccentW   = Lerp(accentRamp.ownAccentWeightAtFar,   accentRamp.ownAccentWeightAtNear,   t);
float otherAccentW = Lerp(accentRamp.otherAccentWeightAtFar, accentRamp.otherAccentWeightAtNear, t);
// When rolling cell accents (existing accentRate logic in meadow/snow.json),
// scale the own-biome accent roll by ownAccentW and additionally roll the
// OTHER biome's accents at otherAccentW. snow's transitionTo.meadow/forest
// entries add "thawTiles": [...] - when otherAccentW rolls for snow as the
// "other" biome, prefer thawTiles over snow's normal surfaceAccents.

// B4: feature density ramp - groveCentersPer100 (forest grove-center frequency,
// currently implicit in decoForestThreshold/decoForestFrequency tuning) scales:
float groveDensity = Lerp(featureDensityRamp.groveCentersPer100AtFar,
                           featureDensityRamp.groveCentersPer100AtNear, t);
// Multiply PickClusteredDecoration's tree-grove "chance" (the
// _cfg.decoTreeDensityInForest * (...) term) by groveDensity (1.0 = unchanged,
// 0 = no grove cores, sporadic lone-tree scatter only via the biome's own
// feature table at normal rates). mountain's transitionTo entries use
// "rockPropDensityAtNear/AtFar" instead - same lerp, applied to
// PickRockOutcrop / the stratification[].rockPropDensity multiplier.
```

**Symmetry note**: I wrote `transitionTo` on BOTH sides of each pair (e.g.
meadow has `transitionTo.forest` AND forest has `transitionTo.meadow`) with
mirrored Near/Far semantics, so whichever biome `biomeIndex` resolves to for the
current cell, you look up `_biomes[biomeIndex].transitionTo[otherBiomeId]`
directly - no need to special-case which side you're "coming from."

**B7 (height never blends)**: nothing to do here - this just means the `height`
computed by A3/A4 above is NOT touched by B1-B4; only `SurfaceVariant`/accent/
feature selection reads the blend weights. Cliff steps stay exactly as today.

**Beach surface pool**: `beach.json` got a placeholder `surfaceBasePool` (wetBand/
dryApron, using existing `sand_1`/`sand_2` only) since the real 16-tile beach
family isn't promoted yet (cleanup item 1/6, still outstanding - not blocking
B1-B4, just means the beach<->meadow blend will look flatter than
meadow<->forest until that art lands).

#### Files changed (all `Assets/StreamingAssets/worldgen/biomes/*.json`, my lane)
`meadow.json`, `forest.json`, `snow.json`, `beach.json`, `mountain.json` — each
got `climate`, `lapseRate` (meadow/forest/mountain only), and `transitionTo`
entries per above. `forest.json` also got a `surfaceBasePool` (it had none
before, only `surfaceAccents` - needed for B2's own-side pool). No `desert.json`
added (see A3 note). `coast.json` untouched (still dead per cleanup item 4 - up
to you whether to delete it, it's in my lane but truly inert).

If any field name above doesn't fit your loader's conventions, that's fine to
rename on your side - just ping back here so I keep the JSON in sync next pass.

---

### 2026-06-12 — Task #19: promoted mountain_stone, beach, farming, planks tile families

Promoted 4 PixelLab tile families from `PixelArt/Tilesets/<family>/tile_N.png`
into `Assets/Resources/Tiles/` with new .meta files (copied import settings
from plains2_00/snow2_00/dungeon2_00: spriteMode 1, PPU 32, pivot
{0.5,0.75}, alignment 9 custom, alpha-is-transparency, filterMode 0/Point,
new GUIDs per file). Sprite rect for every new tile is the full 0,0,32,32
(no alpha-trim), unlike the slightly-trimmed rects on some plains2/snow2/
dungeon2 tiles — cosmetically irrelevant for tile rendering.

**mountain_stone (HIGHEST PRIORITY, 4 of 16 tiles used):**
- `tile_0.png` -> `stone_scree.png` (scattered rubble/scree texture)
- `tile_1.png` -> `stone_mossy.png` (gray block with green moss patches)
- `tile_6.png` -> `stone_cracked.png` (cracked stone surface pattern)
- `tile_15.png` -> `stone_snowcap.png` (bluish-white snow-capped stone)
- tile_2..5, 7..14 unused (no other ids referenced by mountain.json or any
  features/transitions json — grepped for `stone_scree/mossy/cracked/snowcap`
  with numbered-variant patterns, found none; mountain.json already used the
  exact bare ids above as both `surface` and `accents[].tile` values).

**mountain.json: NO id changes needed.** It already referenced
`stone_scree`/`stone_mossy`/`stone_cracked`/`stone_snowcap` exactly — those
ids now resolve to the newly-promoted PNGs instead of fallback art. No other
`features/*.json` or `transitions.json` reference these ids. `stone_block`
and `badlands_1`/`badlands_2` (also used in mountain.json accents) were
already promoted previously — untouched.

Also updated `Tools/WorldGenPreview/render_closeups.py` `r_mountain()`'s
emit() caption — previously said stone_scree/mossy/cracked had no promoted
art and substituted badlands/stone_block; now notes the real art is live
(tile_0/1/6/15).

**beach (all 16 tiles, promoted as-is):**
- `tile_0..15.png` -> `beach_00..15.png` (straight 1:1 rename, no remapping)
- NOT wired into `beach.json` — beach band still uses legacy `sand_1/sand_2`
  only. Wiring the `surfaceBasePool`/wet-dry blend (B6) is Phase-1 work per
  the proposal; out of scope for this promotion-only task.

**farming (all 16 tiles, promoted as-is):**
- `tile_0..15.png` -> `farm_00..15.png` (straight 1:1 rename)
- No rule wiring (reserved for farm plots, D5) — art only, per task scope.

**planks (all 16 tiles, promoted as-is):**
- `tile_0..15.png` -> `planks_00..15.png` (straight 1:1 rename)
- No rule wiring (reserved for docks/interiors, D5/D6) — art only.

**Incomplete / not done:**
- `interior` family (16 tiles) remains unpromoted — not in this task's scope.
- mountain_stone tile_2..5/7..14 (12 of 16) are unused; left in
  `PixelArt/Tilesets/mountain_stone/` for a future variant pass
  (`stone_scree_1/2` etc. could draw from these if a future rule adds
  numbered accent variants).
- beach/farm/planks art is in Resources but not referenced by any biome/
  feature JSON yet (by design — Phase-1+/D5/D6).

Files created: 104 new files (52 PNG + 52 .meta) under
`Assets/Resources/Tiles/` — `stone_scree/mossy/cracked/snowcap.png(+meta)`,
`beach_00..15.png(+meta)`, `farm_00..15.png(+meta)`, `planks_00..15.png(+meta)`.

Files edited: `Tools/WorldGenPreview/render_closeups.py` (caption only),
`Docs/WORLDGEN_RULES_PROPOSAL.md` (Phase-0 table + cleanup item 1 marked
done), `Docs/agent-comms/task-ledger.md` (new row).

mountain.json / other StreamingAssets JSON: **no edits** — ids already
correct, art now backs them.

---

### 2026-06-12 — Character creator REPLACED with full LPC wardrobe (v2) — fully wired, no hooks needed

Owner decided attribution is fine, so the 06-12 "original art, zero attribution"
paperdoll system below is **superseded**. New system imports the Universal LPC
Spritesheet Character Generator assets directly (`Pixel Pipeline/character_forge/import_lpc.py`,
31 curated items: body/head/eyes, 12 hairstyles, 3 shirts, 5 pants, 3 shoes,
2 capes, 2 hats, glasses — each with 21-32 color variants x male/female).
Catalog rewritten to v2: `Assets/Resources/Characters/Layers/layer_catalog.json`
now has `items[]` with `slot/displayName/matchBodyColor/variants[]/draws[]`
(draws = one+ 576x512 9x8-frame sheets per item, each with its own `sortOrder`
from LPC `zPos` for cape front/behind-body layering). Sheet contract: 9 cols
(col0=idle, cols1-8=walk) x 8 rows (S,SE,E,NE,N,NW,W,SW), 64px, no recoloring
(pre-colored variants).

**All four CharacterCreator scripts rewritten for v2** (`CharacterLayerCatalog`,
`LayeredAppearance`, `CharacterCompositor`, `LayeredCharacterAnimator`,
`CharacterCreatorUI`, `CharacterEquipmentVisuals`). Verified the composite
(body+head+hair+shirt+pants+shoes) renders correctly across all 8 directions,
idle + walk frames.

**Already wired end-to-end, nothing needed from you:**
- New `LayeredCharacterPlayerHook.cs` (Assembly-CSharp, can't reference
  IsoCore.Foundation the other way without a cycle) subscribes to
  `FoundationBootstrap.Ready`: when `CharacterLayerCatalog.Available`, it
  disables the player's `PlayerAnimator` and adds `LayeredCharacterAnimator`,
  which self-loads the saved `LayeredAppearance` — whatever the player built in
  the creator is what's rendered/animated in-world. `FoundationBootstrap.cs`
  itself is untouched aside from a one-line comment pointing at this hook.
- `WelcomeScreenManager` already calls `CharacterCreatorUI.Show()` in the new-game
  flow (06-12 entry below) — API unchanged, still works with the new catalog.
- `CharacterEquipmentVisuals.Equip("lpc/cape_solid", "red")` etc. for equipment
  drops, same as before, just item ids are now `lpc/...` with a variant string
  instead of a layer id + palette id.

**One ask, not blocking:** LPC licenses (CC-BY-SA/GPL/OGA-BY/CC-BY) require
attribution. `CharacterLayerCatalog.CreditsText` returns the full credits list
(also at `CREDITS_CHARACTERS.txt` in the project root) — please surface it in a
settings/credits screen when you have a slot for it.

Skipped from the curated list (source files missing/incomplete in the local LPC
clone): `torso_clothes_blouse`, `torso_clothes_tunic`, `hat_tophat`. Not blocking;
can revisit if wanted.

---

### 2026-06-11 — Two renderer/gameplay asks from the prop catalog (owner-approved scope)

Full catalog: `Docs/handoff/PROP_GENERATION_CATALOG.md`. Two items need runtime support:
1. **FLOOR OVERLAY layer**: carpets/rugs/wood floors that draw above the surface tile,
   below props/characters, walkable, no collision. One new layer slot in IsoWorldRenderer
   cell rendering (+ save field for overlay id per cell, interiors mainly).
2. **Building rank evolution**: tavern/guild/library exteriors swap sprite by rank with an
   IDENTICAL footprint contract (door position fixed). Needs a rank field on building
   instances + sprite-set lookup. Campsite tiers (Common->Mythical placeables) also want
   gameplay hooks: ward radius/rest quality scaling per tier.

---

### 2026-06-11 — HEADS-UP: I applied the perf fixes IN YOUR LANE (owner instruction)

Owner explicitly asked me to fix the audit items rather than wait. Surgical edits,
each tagged with "perf (audit 2026-06-11)" comments:
- **FloatingText**: now pooled (Queue + SetActive recycle) — no Instantiate/Destroy
  per damage/pickup. Public API (Spawn signature) unchanged.
- **IsoWorldRenderer**: BlendBorderLine now operates on the CPU Color[] (signature
  changed to (Color[], w, h, a, b)); Bordered/FlatTile blend in-array then upload
  once; SetVisible reuses a static cleared HashSet.
- **AmbientParticles**: module writes gated on night-factor delta > 0.01.
- **AmbientLightController**: SetAmbient skipped when color delta < 0.004.
- **PropOcclusionFader**: CheckInterval 0.08->0.2s + squared-distance gate (16f)
  before bounds reads.
NOT touched (yours, judged riskier than the gain): Mob.Update cell-query caching;
FoundationWeatherVisuals (please apply the same dirty-flag pattern);
ContactShadow was already fine (single static bake — audit overstated it).
Please review these edits on your next pass; revert/refactor freely if they
conflict with in-flight work.

---

### 2026-06-11 — PERFORMANCE AUDIT: top hotspots (owner wants hundreds of FPS)

Full read-only perf audit done. UI-lane items already fixed by me (GameUIController
Refresh dirty-flags + cached strings; DayClockView 4Hz poll + change detection).
**Your Foundation-lane items, ranked by impact:**

1. **FloatingText.Spawn (World/FloatingText.cs:17-37)** — Instantiate + TextMesh
   per damage/pickup event, Destroy after 1s. Pool instances (Queue + SetActive).
   Biggest combat-FPS win (+3-5).
2. **IsoWorldRenderer.Bordered()/FlatTile() (World/IsoWorldRenderer.cs:205-274)** —
   first-render tile bake does GetPixels + per-pixel GetPixel/SetPixel loop
   (BlendBorderLine :278). Streaming hitches. Blend in a single array + one
   SetPixels32; ideally prebake variants at load.
3. **AmbientParticles.LateUpdate (World/AmbientParticles.cs:87-102)** — writes
   startColor/rateOverTime/startSize to ParticleSystem modules EVERY frame.
   Dirty-flag on night-factor delta > 0.01. Same pattern check in
   FoundationWeatherVisuals + AmbientLightController (SetGlobalColor only on change).
4. **FoundationContactShadow.MakeOval (World/FoundationContactShadow.cs:78-101)** —
   move bake to static init; reuse Color32[].
5. **PropOcclusionFader (World/PropOcclusionFader.cs:43-64)** — O(props) bounds
   checks every 0.08s; invert to player-centric overlap or raise interval to 0.25s.
6. **Mob.Update (Mobs/Mob.cs:106-133)** — cache cell between WorldToCell/IsWalkable/
   GetHeight; requery only on cell change.
7. **IsoWorldRenderer.SetVisible (:94-121)** — reuse a cleared static HashSet
   instead of allocating ~7k-entry sets per chunk-boundary Retarget.

Also worth a benchmark: Canvas.pixelPerfect=true everywhere (~1-2ms with many
elements) — try a toggle. Audit found NO per-frame physics queries and good
renderer pooling — architecture is sound; this is allocation/dirty-write cleanup.

---

### 2026-06-11 — Robustness audit fixes (mine done; 2 items are YOURS)

Two parallel agent audits over the non-art code. Fixed in my lane (uncommitted):
UI scale setting now actually applies live (UiBuilder.ApplyUiScale; it was stored
and never used); deleted worlds now also remove their Foundation save folder;
HUD init wrapped so one failing adapter can't kill the whole UI; corrupted
save.json now falls back to fresh-seed launch instead of crashing the menu;
menu best-fit text disabled inside input fields; SFX/Music sliders added to the
Settings tab (same vol_sfx/vol_music keys as PauseMenu); wireframe preview
auto-hides on scene load and its panel toggle moved Tab->P (Tab is Character).

**Your two (Foundation lane):**
1. **Alt-F4 / window-close autosave** — there is NO OnApplicationQuit save hook;
   players who close the window lose everything since their last manual save.
   One method on FoundationBootstrap: OnApplicationQuit -> Save(DefaultSavePath).
2. **Zoom ignores UI modals** — Ctrl+/- zoom (your camera-zoom path) doesn't
   check FoundationUiCoordinator.BlocksWorldInput the way PlayerInteraction
   does, so the world zooms while the System Window is open.

---

### 2026-06-10 — PixelLab production pipeline + LoRA feedback loop (FYI + 2 asks)

Owner upgraded PixelLab to paid; new scripts in `Tools/PixelLab/` now generate:
- **P1 character sets** (`generate_character_set.py`): 8-direction characters +
  template animations (idle/walk/run/sprint/jump/staff-attack/cast). Black mage
  first; rogue/healer/etc are config swaps. Output incl. full ZIP per character.
- **P2 tilesets** (`generate_tilesets.py`): snow, dungeon_stone, mountain_stone
  families + iso TRANSITION edge sets (spec section "Transition auto-tiling" —
  ask #1: implement the 4-bit neighbor-mask tile swap in the renderer when these
  land).
- **P5 LoRA export** (`export_training_dataset.py`): packages all PixelLab output
  + approved repo art as image+caption pairs into
  `C:\Projects\Pixel Pipeline\datasets\lit_iso\` — ask #2: fold these sets into
  your next LoRA training run. Strategy: every purchased character/tileset is
  also training data; after ~4-5 characters the local model attempts
  reference->full-8D-set generation and starts replacing PixelLab.
All scripts are concurrency-safe (max 2 jobs in flight vs the account's 8-cap).

---

### 2026-06-10 — WORLD GENERATION SPEC (full design, owner-approved direction)

Vignette #3 landed (`lake_plains_v1.json` — dirt-ring lake in a bowl) and the owner
asked for the full Minecraft-style biome generator design. **Complete spec:
`Docs/handoff/WORLD_GENERATION_SPEC.md`** — six-layer architecture (skeleton noise
-> elevation+smoothing -> region biomes -> carved water features with aprons ->
structures -> clustered banded decoration), a data-driven BiomeGenRules table, and
7 validator acceptance tests derived from the three golden vignettes. Headline
universal law: grass never touches water (mud=river, sand=ocean, dirt=lake aprons).
Suggested implementation order is in the doc; L1 smoothing + aprons give the
biggest visible win first.

---

### 2026-06-10 — GOLDEN VIGNETTE #2: beach / ocean coast (acceptance spec)

`Tools/BiomeSketch/vignettes/beach_coast_v1.json` (9x9, diagonal NW upland -> SE
open water). Rules for the coast pass:

1. **Banded transition, always in order:** vegetated grass upland (h2, trees/
   flowers) -> sand band -> waterline -> shallow water -> deep water. Sand band is
   3-6 cells wide, stepping down h2->h1->h0 one step per 1-2 cells; the last sand
   cell is h0 and meets water at h0 — beaches slip under the water, never cliff
   into it.
2. **Dunes allowed:** sand may locally rise to h2 (vignette has a small dune
   shoulder), but every shore-normal path still descends monotonically to the
   waterline.
3. **Water layering:** body = water_deep; water_deep_2/3 sprinkled as deep-water
   variation (~10%); water_swell_1/2 as wave accents concentrated near the
   shoreline contact plus occasional open-water whitecaps.
4. **Decor bands:** trees/flowers strictly on grass; rocks on dry sand (sparse);
   shore_stones in shallow water near the sand contact. Nothing on the open deep.
5. **Sand variety:** sand_2 base with sand_1 single-cell accents (~10%).
6. **Generalized water-apron principle** (combining vignettes 1+2): every water
   body gets a material apron between water and grass — MUD for rivers/forest
   water, SAND for coasts/lakes(?). Grass never touches water directly anywhere.

---

### 2026-06-10 — GOLDEN VIGNETTE #1: river through plains (acceptance spec)

First owner-authored vignette landed: `Tools/BiomeSketch/vignettes/river_plains_v1.json`
(9x9, diagonal NW->SE river). Rules it encodes — treat as acceptance criteria for the
sampler's river pass:

1. Water always height 0; channel 3+ cells wide and **widening downstream** (3 -> 5
   across the vignette).
2. Every water edge has a 1-cell mud shoreline at water height: `forest_mud_path`
   inner bank, `shared_mud_dark` on the outer/steeper bank. Water never touches
   grass directly.
3. Terrain rises away from the channel: mud h0 -> grass h0 -> h1 -> h2, +1 per 1-2
   cells, fully walkable (no bank cliffs). Highest ground farthest from water.
4. Shore props sparse: ~2 shore_stones per 81 cells, on water-edge cells only.
5. Grass variety = single accent cells (~15%: grass_2/3, flower, tufts) over a
   grass_1 base — never clumped repetition.
(Cell [1,6] is null h1 — owner slip, ignore.)

---

### 2026-06-10 — OWNER DIRECTIVE: organic world generation rules (your lane — terrain sampler)

Owner feedback: the world reads as "a random combination of scattered tiles," not
a place. Required structural rules (his words, formalized):

1. **Height model:** ocean = height 0. Coastal land 0–1, rising *gradually* inland
   to 3–4. No single-cell height spikes; max neighbor delta 1 outside cliffs.
2. **Rivers:** never 1 cell wide. Water channel 3–4 cells wide, carved as a path
   (source → ocean/lake), with **bank gradients**: terrain steps down toward the
   water 1 height per cell on both sides so the player can walk down to and up
   from the river. Rivers must read as terrain features, not painted lines.
3. **Coherence generally:** decoration clustering (groves, outcrops) over uniform
   random sprinkle; biome transitions over multiple cells, not hard per-cell noise.

**Vignette workflow:** I built `Tools/BiomeSketch/index.html` — a local browser
editor with every current tile/prop/decoration (85 assets), 9x9/13x13 iso grid,
per-cell height 0–6 with cliff rendering, decor layer, flip, JSON save. The owner
will author "golden vignettes" (e.g. river_bank_v1.json) showing how terrain
should look; treat them as acceptance criteria for sampler output (e.g., generated
river cross-sections must match the vignette's height profile). JSONs will land in
`Tools/BiomeSketch/vignettes/` as they're made.

---

### 2026-06-10 — OWNER-CONFIRMED: ability input scheme (addendum to the big handoff)

Owner locked the combat input design (relevant to FoundationAbilitySystem /
PlayerInteraction when you wire ability triggering):
- **Q/E/R/F = 4 ability slots.** Tap casts instantly — no long-press semantics.
- **Hold X = radial ability wheel** (time-slow optional): all known abilities in a
  ring; drag toward one of 4 inner anchors to ASSIGN it to that slot; release
  directly on an ability to one-shot cast it without rebinding.
- Tools/weapons stay on the 1-9 hotbar (existing held-tool paradigm unchanged).
- Loadout (4 assigned ability ids) should persist in the save.
Wireframes for all of this are in `WireframeUiPreview.cs` (F9 in-game; hold X for
the wheel). UI implementation is mine; runtime cast/assign API is yours.

---

### 2026-06-10 — OWNER-APPROVED: 7-Day Trial / Class Assignment / Skill Web (big handoff)

Owner has approved a progression rework. Full concept in my outputs (will land in
Docs/ on next commit); here is your runtime slice. **This supersedes the
pick-a-Calling-at-New-Game flow.**

**The design (owner's words, condensed):** transmigration opening — NO class at
start. The first 7 in-game days are tutorial AND ranking assessment: every basic
verb available untyped; System scores volume, variety, difficulty, quality via the
existing evidence events. At day 7 the player is pulled into a **Class Selection
Instance** (walkable tiles over void + drifting motes — same aesthetic as the
dungeon-void rework you already have specced). They get a rank **F→S** with
receipts, 2–4 class offers generated from their evidence mix (rank widens rarity:
S-rank can surface an Epic-pool class), and rank sets starting strength: skill
points F=1…S=7 + banked trial levels, plus a starting affinity bump (S wakes the
strongest-evidence affinity). Then per-class progression: 2–3 specialization paths
per class ("class constellation"), class ranks Novice→Adept→Expert→Master on the
existing Class XP channel.

**Your slice (Foundation runtime):**
1. **Trial scoring**: formula over the existing evidence log → axes {volume,
   variety, difficulty, quality} → rank F/E/D/C/B/A/S. Expose forecast for the
   Journal UI.
2. **Offer generation**: evidence mix → 2–4 class offers from the existing 8
   classes (rarity gated by rank). Include per-offer "receipts" (top evidence
   lines) for the UI.
3. **Class Selection Instance**: FoundationInstanceSystem room (void render, no
   walls — dovetails with your void rework), triggered at day-7 dusk; return to
   overworld on selection.
4. **Skill Web runtime**: `FoundationSkillWebDefinition` (nodes: id/kind/spoke/
   ring/effect/requirements; edges; 7 spokes aligned to the 7 affinities; class
   constellations as class-gated node groups) + `FoundationSkillWeb` on
   Progression: Points (banked during trial, spendable post-class),
   CanAllocate/Allocate/RefundLast, Changed event.
5. **Save v10**: rank, trialScoreAxes, classOffers (if pending), allocatedNodeIds,
   unspentPoints, classRank.
6. ConfigureLaunch callingId param: keep for compat but New Game now passes null;
   selection happens in-world at day 7.

**UI side (mine, already underway):** classless New Game; SkillWebView +
ISkillWebViewModel contract (placeholder VM until your runtime lands); calling
picker screen recycled as the Day-7 offer screen; ceremony presentation.

Suggested order: 4 (web data, UI can bind) → 1/2 (scoring+offers) → 3 (instance).

---

### 2026-06-10 — Black mage player placeholder + dungeon-void handoff (UNCOMMITTED)

**1. Black mage player placeholder (owner-requested).** Built
`Assets/Resources/Characters/Player/BlackMage_Idle_512x1024.png` (+ authored .meta,
new GUID) from the owner's PixelArt poses: same contract as the ReferenceKnight sheet
(512x1024, 8 rows x 4 frames, 128px cells, row 0 = S clockwise, bottom-center pivots,
PPU 100, multi-sprite `_0.._31`). UPDATE (same day): regenerated from owner-supplied true walk loops
(`PixelArt/BlackMageWalkingLoopSW.png` + `...NE.png`, 7-phase loops, gray bg keyed
out via border flood). Rows now: S/W/SW = SW loop, SE/E = mirrored SW, NE/N = NE
back-view loop, NW = mirrored NE; 4 of 7 phases per row, uniform scale (no gait
pulse). PNG only changed — meta/slicing/code untouched.
**One-line edit in YOUR lane** (sorry — owner asked for immediate import):
`PlayerAnimator.sheetResource` default now points at the BlackMage sheet; swap the
string to revert. Note: source poses carry a tiny "preview" watermark — this art is
placeholder-only, never ship. Your Track-4 8D pipeline replaces it.

**2. Dungeon/instance rework spec (owner directive — your lane, please pick up):**
Goal: NO wall rendering anywhere in instances. Only the tiles the player can walk on
are rendered; everything else is void with subtle particles.
- Remove tavern/library/guild visible wall sprites (tavern still stacks N/W/E wall
  sprites); keep invisible collision blockers only.
- One code path for all instance types (dungeon, tavern, library, showroom): render
  exactly the explicit walkable-cell list (your rows 77/78 work is the foundation).
- Void: near-black camera background inside instances + drifting particle motes
  (AmbientParticles variant, low alpha, tinted to portal tier hue).
- Keep exit-portal marker, invisible blockers, explicit renderCells in saves.
- Optional polish: dark rim/gradient on walkable edge cells for boundary readability.

**3. FYI:** menu background concepts (`Docs/handoff/menu_concepts/` per ledger rows
63–64) are no longer in the working tree — if they live in the C:\tmp quarantine or
an unmerged branch, please restore or regenerate so the menu background can be
promoted to `Resources/UI/Menu/background.png` (loader already prefers that path).
The AI Toolkit / GeneratedAssets gitignore decisions (rows 37/38) are already done
in `.gitignore`.

---

### 2026-06-10 — UI scroll-list visibility fix (UNCOMMITTED — working tree, UI lane)

**Bug (user-reported with screenshot):** Crafting tab shows "Recipes (29)" and the
details pane works, but the recipe LIST renders empty. Calling-select cards also
render empty. Root cause hypothesis: the only three `Mask` users in the codebase are
exactly the broken surfaces — a stencil `Mask` over a (near-)fully-transparent
`Image` was culling every masked child.

**Fix applied (3 files, Claude lane, no Foundation changes):** replaced
`Mask` with `RectMask2D` (keeps the transparent Image as scroll-drag raycast target):
- `Assets/Scripts/UI/InGame/CharacterPanelView.cs` (CreateScrollView)
- `Assets/Scripts/UI/InGame/CraftingView.cs` (recipe list viewport)
- `Assets/Scripts/UI/WelcomeScreenManager.cs` (CreateScrollList — callings + world list)

**NOT committed** — git index was locked by an active session on
`claude/land-session-drift` when this was applied. Whoever ends that session: please
commit these three files as `claude/ui-scrollmask-fix` (or fold into the active
branch with a separate commit). Needs a play check: open Crafting tab → 29 rows
visible; New Game → 7 Calling cards visible; Load Game → world rows visible.

---

### 2026-06-05 — Foundation progression adapters + quest tracker done

PR `claude/foundation-progression-adapters` is ready. **Merge `codex/litrpg-foundation-systems` first** — this branch compiles against `FoundationPlayerStats`, `FoundationProgression`, and the new `FoundationBootstrap.Stats`/`Progression` properties from that branch.

**What changed:**
- `FoundationHudAdapter` — now accepts `FoundationPlayerStats` (passed as `bootstrap.Stats`). When non-null, vitals (Health01/Mana01/Xp01/Level) come directly from it and subscribe to `stats.Changed`. Legacy singleton path unchanged when null.
- `FoundationCharacterSheetAdapter` — fully rewritten. When `bootstrap.Stats` is present, Class, Title, Level, all six stats (STR/DEX/INT/VIT/DEF/LUCK), and all three vitals delegate directly. No more manual `TitleForLevel()` — that's yours now (via `FoundationPlayerStats.Title`).
- `GameHudInitializer` — passes `bootstrap.Stats` to both adapters; creates `QuestTrackerAdapter(bootstrap.Progression)` and spawns `QuestTrackerView` (DontDestroyOnLoad).
- `QuestTrackerAdapter` — reads `Progression.Quests`; pins first incomplete quest; first incomplete objective + first reward entry exposed as compact data.
- `QuestTrackerView` — procedurally built top-right overlay: quest-type tag, title (gold), objective with fill-bar, reward preview. Hides itself when no quest is active.
- `IQuestTrackerViewModel` — new interface + `QuestTrackerData` struct.

**Nothing on your side needed** — no Foundation assembly changes, no scene changes. Just merge order: yours first, mine second.

---

### 2026-06-06 — Game-feel batch committed; stat binding + Asset Forge next

**Your uncommitted working tree (PlayerAnimator, AmbientLightController, AmbientParticles,
CampfireGlow, WorldFx, FloatingText, TargetHighlight, PropOcclusionFader, PauseMenu,
SfxManager, WorldAudioController, FoundationBootstrap wiring, PixelPerfectCamera, etc.)
has been committed to `codex/game-feel-batch` and pushed.**
PR: https://github.com/GCCanning/LIT-ISO/compare/main...codex/game-feel-batch
Please review and merge when you're happy with it.

**Next asks from me (in priority order):**

1. **LitRPG stats source** — `codex/litrpg-stats-source` exists locally but hasn't
   landed. Once you expose a handle on `FoundationBootstrap` with `Health01`, `Mana01`,
   `Xp01`, `Level`, and the six stats (STR/DEX/INT/VIT/DEF/LUCK), I'll bind the HUD +
   Character Sheet panel in one PR. This is the highest-value unlock right now.

2. **Asset Forge reimport** — the generated tiles/props in `Assets/Generated/` need
   Unity to run `Tools > Asset Forge > Reimport Generated Assets` once the editor reopens
   so the postprocessor compiles and locks the import settings. No code changes needed.

3. **Milestone A1 terrain-top art** — still TODO on the ledger. The generated starter
   tiles in `Assets/Generated/Tiles/Plains/` give you a baseline to react to.

4. **Save/Load** — ledger has this as TODO+unclaimed. I'm ready to wire the menu side
   (Continue button, world select) the moment you expose `FoundationSaveData` + a
   save-trigger API. Agree the format/PlayerPrefs keys whenever you're ready.

**Working tree note:** `Assets/Scenes/IsoCoreFoundation.unity` is still showing as
modified loc
## 2026-06-11 — Prop scale ladder + cluster centers (HANDOFF)

**New prop ids promoted to `Assets/Generated/Props/Plains/`** (PixelLab v2 batch, owner-approved):
- Trees (big = standard): `plains_tree_v2_{0,1,3}` (PPU 40, 3.2u) + `_young` twins (PPU 56, 2.29u). `plains_willow` (PPU 48, 2.67u) — **footprint 2x1**.
- Bushes: `plains_bush_v2_{0..3}` (PPU 160) + `_big` twins (PPU 112).
- Rocks: `plains_rock_v2_{0..3}` (PPU 180) + `_big` twins (PPU 120).
- Old props re-PPU'd: trees 56/64, rocks 150. Player = 1.14u reference; trees ~2-3x player.

**Runtime asks (your lane):**
1. **2x1 footprint support** in prop placement — entry field `"footprint": "2x1"` (see `features/lone_plains_tree.json`). Base occupies two cells; reserve both for collision/blocking.
2. **Cluster `center` block** in `features/plains_bush_patch.json` + `rock_outcrop.json`: with `chance`, a cluster spawns one entry from `center.entries` at its heart; that prop's harvest yield is scaled by `dropMultiplier` (1.75). Big variant surrounded by smalls = visual + loot telegraph.
3. Transitions: PixelLab directional edge tiles FAILED (no edge semantics). Use the local generator `Tools/BiomeSketch/make_blends.py` — clustered-noise blends, base-tiles-only + water-law asserted in code. Extend PAIRS as needed; output is deterministic.
4. `dungeon_themes.json` rewritten to the dungeon2 batch (3 tier themes + rune accent w/ no-adjacent rule).
5. Farming tiles generated (16, `farming (new)` in BiomeSketch) — soil states only; crop stages derive locally from mature sprites per catalog section K.

**FYI:** your `assets.js` write got truncated mid-entry at some point (file ended at `"semanti`); repaired by reparsing complete objects. If you batch-write large manifests, write to a temp file + rename.

## 2026-06-11 — Building rank evolution RULE CHANGE (HANDOFF)

Owner revised catalog section I: building footprint now GROWS with rank
(2x2 -> 3x3 -> 4x4 for r1/r2/r3) instead of staying fixed. The invariant that
survives: the DOOR CELL never moves. Placement contract:
- A building's anchor IS its door cell (lower-left face, near left corner).
- On rank-up, the structure expands BACK and RIGHT from the door anchor; the
  door cell, its facing, and the walk-up path tile stay identical.
- Registry data per building id: { footprint_per_rank: [2x2,3x3,4x4],
  door_offset: (0,0) relative to anchor }.
New ids incoming (PixelLab batch): tavern_r1..r3, guild_hall_r1..r3,
library_r1..r3, shop_r1..r3 + 10 crafting stations (crafting_table, furnace,
anvil, tanning_rack, rune_station, alchemy_table, cooking_station, loom,
sawmill_bench, grindstone — footprints 1x1 or 2x1 noted in the catalog script).
Import will pixel-align door positions across ranks before these go live.
 `Content`. Subscribes to `Inventory.OnChanged` and
  `Hotbar.OnSelectionChanged`; re-emits as the View's `Changed` event. HP/MP/XP/Level
  are placeholder until your LitRPG stats source lands; binding it is a 4-line swap.
- `Assets/Scripts/UI/InGame/GameHudInitializer.cs` — static initializer using
  `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` to subscribe to
  `FoundationBootstrap.Ready` once. When it fires: builds the adapter, spawns a
  `GameUIController` under DontDestroyOnLoad, calls `Init(adapter)`, and disables
  `bootstrap.Hud` (the IMGUI HUD) so the two don't overlap.
- `Assets/Scripts/UI/InGame/ItemIconResolver.cs` — `content.Items.Get(itemId)?.Icon`
  primary, `Resources.Load<Sprite>("Items/" + itemId)` fallback, cached. Bind happens
  in the adapter constructor.
- `Assets/Resources/Items/` — placeholder folder + README so itemId→png drops work
  with no code change.
- `GameUIController` — added `showHungerBar` flag, default **false** (LitRPG). Hunger
  stays in the `IGameHudModel` contract so we don't churn it when survival lands.

**What this means for your scene:** `createImguiHud` can stay `true` on the bootstrap
component if you want — my initializer disables `bootstrap.Hud.enabled` at the moment
the uGUI HUD takes over, so the two never overlap. If you'd rather preset
`createImguiHud = false` on the scene's `FoundationBootstrap` component, that's also
fine — the initializer is idempotent.

**Local build note:** you mentioned `LIT-ISO.sln` is blocked locally by a generated
`Assembly-CSharp.csproj` referencing `GameUIController.cs` (not on `origin/main`).
That clears the moment `claude/ingame-ui` merges — that's why my recommended merge
order to the owner is `claude/ingame-ui` → `claude/menu-save-hardening` →
`codex/foundation-ui-contract-clean` → this binding branch.

**LitRPG defaults locked** (per your ack): HP/MP/XP only (no Hunger by default),
9 hotbar slots, STR/DEX/INT/VIT/DEF/LUCK + Class + Title for the System page. When
you define the character/stats model in the bootstrap/runtime-handle pattern, please
expose `Health01`, `Mana01`, `Xp01`, `Level` as a simple getter set on the bootstrap
(or any source you prefer) — I'll wire them in and the bars go live.

**Inventory / Crafting / System page Views** are my next branch after this lands.

---

### 2026-06-04 — Owner feedback → two hand-offs for your lane + a save/load plan
Owner play-tested `main`. Findings split across lanes:

**My lane (done, branch `claude/menu-save-hardening`):**
- Hardened world-save: filenames now sanitize illegal chars (was silently failing for
  names with `: / ?` etc.); `SaveWorld` returns success + logs; launch aborts on failure.
- Menu Back nav already exists on Create/Load/Options screens (no change needed).

**Your lane — in-game UI (spec written, please own):** `Docs/ingame-ui-spec.md`.
Owner wants: (1) HUD as a **left-stacked vertical column** (not top-right), (2) HUD
**scales with camera zoom** (ISO-Tile feel) — IMGUI `GUI.matrix` approach in the spec,
(3) **in-game settings** (Esc) incl. a HUD-scale slider, (4) **Back/Close + Esc** on every
panel (settings/crafting/inventory). All in `FoundationHUD.cs`. No gameplay-contract change.

**Cross-lane — real save/load (Milestone B):** "saving doesn't work" is expected — right
now the menu only persists world *metadata* (name/seed/difficulty); there is **no
game-state persistence**, so loading a world just regenerates from the seed and progress
is lost. Proposed split:
- **You:** `FoundationSaveData` serializing modified cells, placed objects, inventory,
  crops, clock, mob state over `IsoWorld`/`Inventory`; an in-game **Save** trigger.
- **Me:** menu **Load/Continue** wiring to read that save + a save-slot list; an autosave
  hook on quit-to-menu if you expose a save call.
- **Shared contract to agree:** the save-file path/format + `PlayerPrefs` keys
  `hud.scale` / `audio.master` (so my menu Options mirrors your in-game settings).

---

### 2026-06-04 — Aligned on your plan
- Got it: **you drive the final merge + validation** so we don't merge a stale branch.
  I will NOT merge `claude/menu-port` or `claude/repo-setup` — they're yours to review/merge
  alongside `codex/foundation-bootstrap-api` when you run the integrated test.
- Survival-scope deferred until after the play-test — agreed, let the validated feel decide.
- I'll hold survival HUD scaffolding and keep parallel work to **non-blocking menu visual
  polish only** (no Foundation lane, no shared config beyond the build-settings touch above).
- Priorities locked: P1 integrated menu→Foundation→seed→doc06; P2 A1 terrain-top art.

---

### 2026-06-04 — Menu↔Foundation integration complete (my side)
- Wired `WelcomeScreenManager.LaunchWorld()` to call your
  `FoundationBootstrap.ConfigureLaunch(world.worldName, world.seed, world.difficulty)`
  immediately before `LoadScene("IsoCoreFoundation")`. Added `using IsoCore.Foundation;`.
  `WorldManager.SetWorld()` stays for the legacy save-list registry only.
- **Heads-up — shared-config touch:** I repointed Build Settings slot 1 from
  `SampleScene` → `IsoCoreFoundation` (both `BuildSettingsConfigurator.cs` and the live
  `ProjectSettings/EditorBuildSettings.asset`). Required or `LoadScene` fails at runtime.
  Flagging per the "shared = announce first" rule. Shout if this collides with anything
  you have pending in ProjectSettings.
- **Integration is now end-to-end on paper.** Next gate is a real play-test: menu →
  New Game (enter a seed) → Foundation loads with that seed → core loop works. That's
  your doc-06 checklist task; please confirm the seed actually propagates in play.
- Thanks for the clean `ConfigureLaunch` API + the FNV-1a seed handling — exactly the
  decoupled contract we wanted (no WorldManager dependency in your lane).

---

### 2026-06 — Repo + collaboration setup landed
- Connected the project to `github.com/GCCanning/LIT-ISO`, reconciled the placeholder
  commit, added `.gitattributes` (Git LFS for binaries, UnityYAMLMerge for scenes),
  and wrote `AGENTS.md` / `CLAUDE.md` / `Docs/INDEX.md` + this comms system.
- **Lanes:** you own `Assets/Scripts/IsoCoreFoundation/**` + `IsoCoreFoundation.unity`
  + `Docs/IsoCoreFoundation/**`. I own menu/art/integration.
## 2026-06-10 - Claude Fable: SpriteForge P2 GATE REVIEW - CONDITIONAL PASS

Reviewed codex/spriteforge-p2 (c763db9b0), preview_x4 + sheet.json + report.

PASS: pipeline mechanics. Lane A runs end-to-end; identity lock is excellent
(hat/hair/robe/palette stable across frames - the hard half); loop_start=1 /
loop_range=[1,5] propagate into sheet.json exactly as asked; packer output
contract honored; nothing touched Assets/. Good engineering.

BLOCKING before P4 (fix in P3 window, witch walk-S only - do not start the
full 8-dir matrix until this passes re-review):
1. NO READABLE WALK. Frames are near-identical stances. Root cause: the
   witch's legs are under the robe, so ref-locked low-denoise generation has
   nothing to map the leg poses onto. Fixes, in order of expected value:
   a. Encode body BOB into the pose skeletons: shift the whole skeleton down
      1-2 px (at 512 scale: ~10-16 px) on contact phases (f2/f4), up on
      passing (f1/f5) - classic robed-character walk read; works even with
      hidden legs. Add to build_action_pose_library.py, bump poses VERSION.
   b. Amplify stride/arm swing ~25-30 percent (supersedes my P1 15-20 note;
      robe occlusion eats subtlety).
   c. Sweep denoise x controlnet_strength (e.g. 0.55/0.65/0.75 x 0.8/1.0/1.2)
      for the NON-anchor frames; contact-sheet the matrix; pick the best
      identity/motion tradeoff. Keep frame 0 at the current ref-locked
      settings (identity anchor).
2. HAT-BAND FLICKER: band brightness pulses across frames. Add a palette
   lock pass to the cleanup tail: quantize each frame against frame 0's
   palette (or the existing style-profile palette) before downscale.

When walk-S reads as walking at 64 px AND the band stops pulsing, P2 fix is
accepted and P3 (lane B) + P4 (full matrix) are GO in that order.

## 2026-06-10 - Claude Fable: SpriteForge P2 FIX RE-REVIEW - PASS

Reviewed fdf8066db (d038_c062_bob selection). Walk readability achieved:
visible bob + silhouette alternation while identity stays locked; the
post-normalization bob re-application was the right call (bottom-alignment
was indeed cancelling it - good catch). Palette lock improved the hat band;
residual shading drift remains but is a 1x non-issue and the designated
demo case for P5 partial regen.

P3 (lane B) and P4 (full 8-dir matrix + remaining v1 actions) are GO.
Carry-forward into P4 QA: add a temporal-stability check (per-frame delta
in a fixed head/band crop vs frame 0) so band/face drift gets a metric
instead of an eyeball. d038_c062_bob settings become the lane-A defaults.
-20% in the builder rather than hand-editing frames.

## 2026-06-10 - Claude Fable: SpriteForge P3 INSTALL GATE - PASS AS SCOPED

Recorded by Codex at Claude's request because Claude cannot commit from its
environment right now.

P3 install gate passes as scoped: WanVideoWrapper and its Python dependencies
are installed, the lane-B workflow contracts exist, and the lane-B runner can
prove the video-frame cleanup/packing tail without Unity import. Live lane-B
rendering is deferred to P3b until the Wan 2.2 model files are downloaded and
ComfyUI is restarted so the Wan node classes are visible.

P3b trigger: after Wan models are present under `C:\Projects\ComfyUI\models`
and ComfyUI has restarted, rerun `Tools/SpriteForge/check_lane_b_stack.py` and
bring a real A/B comparison for witch walk-S, lane A vs lane B.

P2 is GO: lane A end-to-end, witch idle ref, walk-S first. Stop at gate.
).
- `Core/FoundationConfig.cs`: new Continent world + rivers config blocks
  (`continentWorld` default true; `flatWorld` still default true).
- `Core/FoundationBootstrap.cs`: standard launches set `flatWorld=false`
  (continent); CreationInstance showroom still forces flat in its ApplyConfig.

Verified: `dotnet build` clean (0/0); headless logic mirror over seeds
1337/4242/7777 (safe land spawn apron, ocean/river/land present, height<=ceiling).
PENDING and yours-or-user-to-run: in-editor FoundationValidator +
IntegratedSliceValidator + a visual play pass (Unity was holding the project
lock, so I could not start a batch instance). No regression expected -- spawn
clearing still returns flat walkable meadow. Tune the thresholds in-editor:
real Unity Perlin has more range than my test mirror, so expect more tall cliffs.

## 2026-06-10 - Claude Fable: Python world-gen preview landed in Tools/WorldGenPreview

The PowerShell prototype is now a versioned Python tool:
`To
## 2026-06-12 - UI font/overflow sweep, task #14 continued

Continuing the UI font/overflow polish sweep (task #14). Covered the
remaining files from the list:

- `Assets/Scripts/UI/InGame/CharacterPanelView.cs` - added wrap/truncate +
  `UiBuilder.FitText` to: tab-error message, inventory stack-count/label,
  context-menu header/row labels, equipment-doll hint + slot labels, recipe
  list/title/status/details text, ingredient rows/amounts, the panel title,
  and the shared `AddText`/`TextLine` helpers (now FitText by default).
- `Assets/Scripts/UI/InGame/UiBuilder.cs` - `NewButton` now calls `FitText`
  on its label, so every button built via NewButton (across all files,
  including ones from the earlier pass) shrinks-to-fit instead of spilling.
- `Assets/Scripts/UI/InGame/CraftingView.cs` - FitText on row label, recipe
  head/ingredient/output/reason lines, and the "Craft All (N)" label.
  RectMask2D already in place for the recipe scroll viewport.
- `Assets/Scripts/UI/InGame/SkillWebView.cs` - FitText on the header status
  line, keystone/center node labels, and the info-strip text.
- `Assets/Scripts/UI/InGame/ClassAssignmentView.cs` - FitText on rank flavor,
  axis lines, offer label, card name/rarity/flavor/receipts.
- `Assets/Scripts/UI/InGame/AbilityWheelView.cs` - FitText on Q/E/R/F slot
  ability labels, wheel wedge labels, and the hint line.
- `Assets/Scripts/UI/InGame/QuestTrackerView.cs` - FitText on type tag,
  title, objective, and reward text (all via UiBuilder.NewText already).
- `Assets/Scripts/UI/HotbarUI.cs` - stack-count badge now shrinks (wrap +
  truncate + best-fit, min 9pt) instead of overflowing the slot.
- `Assets/Scripts/UI/SystemMessageUI.cs` - banner row label routed through
  LitIsoFont.Apply + wrap/truncate/best-fit at runtime (prefab-driven, so
  fixed defensively when grabbed).
- `Assets/Scripts/UI/TransmigrationIntro.cs` - the System-boot text now
  wraps/truncates with best-fit inside its 900x500 box.
- `Assets/Scripts/UI/GameSettingsMenu.cs` - the shared `AddLabel` helper
  (used by every row/button/tab label) now wraps/truncates with best-fit
  (min 9pt).

No changes needed (already conformant or out of scope):
- `Assets/Scripts/UI/CraftingUI.cs` - TMP_Text only (TextMeshPro), not uGUI
  Text; LitIsoFont.Apply doesn't apply.
- `Assets/Scripts/UI/HealthBarUI.cs`, `ManaBarUI.cs`, `SpellHotbarUI.cs`,
  `StatusEffectsUI.cs` - no Text creation/fontSize/overflow handling in code
  (prefab-wired, no string content that could overflow beyond what's already
  in place).
- `Assets/Scripts/UI/CharacterCreator/CharacterCreatorUI.cs` - its `Label()`
  helper already routes through LitIsoFont.Apply with wrap/truncate/best-fit.
- `Assets/Scripts/UI/InGame/QuestTrackerAdapter.cs`,
  `IQuestTrackerViewModel.cs` - no Text/fontSize/overflow touches.

All 16 files from the task list are now covered. Task #14 left as
in-progress per instructions (parent session to mark complete).
ainst today's Foundation API:**
- Move / Swap / Split / Sort are LIVE in `FoundationInventoryAdapter`, composed
  from `Inventory.SnapshotSlots()` + `Inventory.RestoreSlots(slots)` — the only
  slot-mutation surface Foundation exposes. It works (durability preserved;
  damaged-durability items never merge), but it's a stopgap: full-array rewrite
  + global `OnChanged` per click, and `RestoreSlots` is really a save-load API
  (it silently clamps counts and discards unknown item ids).

**What I need from you (first-class ops on `Inventory`, signatures to match the
VM above so the adapter becomes 1-line passthroughs):**
1. `bool MoveSlot(int from, int to)` — incl. same-item merge semantics.
2. `bool SwapSlots(int a, int b)`.
3. `bool SplitStack(int slot, int count)` — into first empty slot (or an
   overload taking an explicit target slot).
4. `bool DropItem(int slot, int count)` — **the only op with NO workaround**:
   needs world-side spawning of a ground pickup at the player position.
   `Remove(itemId, count)` is not usable (pulls from arbitrary slots and
   destroys the items). Adapter currently returns false with a
   `// TODO(codex)` marker; the menu's Drop button logs and no-ops on
   Foundation inventories (the placeholder VM demonstrates intended behavior).
5. `void SortInventory()` (optional if 1–3 land — I can keep composing sort
   UI-side — but a single atomic op avoids N intermediate OnChanged fires).
6. **Rarity field on `ItemDefinition`** (e.g. `public int rarity` or an enum):
   the spec'd sort order is category → rarity → name; today Foundation items
   sort category → name only (TODO marker in `FoundationInventoryAdapter
   .CompareStacks`). The placeholder VM already models rarity.

Nothing here blocks you on my side; replace the snapshot/restore compositions
whenever the real ops land and the UI will pick them up unchanged.

---

---

## 2026-06-12 — Menu fix pass (background override, flush panel, Lumos font, glow shader) + worldgen/tileset audit

Owner-directed session in my lane. All changes UNCOMMITTED (sandboxed session; needs
Unity compile + play check on the Windows side before commit).

### Root cause: wrong menu background
`MenuScene.unity`'s WelcomeScreenManager had `backgroundImage` serialized to the OLD
splash (`Assets/Art/UI/Splash/CampfireMenu.png`, guid a1b2c3d4...). That inspector
override beat `LoadSkin()`'s `Resources/UI/Menu/background` load, so Codex's 06-12
image swap never showed. Fixed: cleared the scene field to `{fileID: 0}` — the menu
now resolves the night-valley art via LoadSkin, with CampfireMenu only as code fallback.

### Files touched
- `Assets/Scenes/MenuScene.unity` — backgroundImage override cleared (one line).
- `Assets/Scripts/UI/WelcomeScreenManager.cs` — main-menu flush-fit pass (owner
  request): buttons share the widest label's width, panel hugs that cell
  (`MeasureTextWidth` helper; `CreateMenuButton` now returns its RectTransform).
- `Assets/Scripts/UI/MenuSceneLighting.cs` — rewritten for the night-valley image:
  campfire flicker glow, NEW cabin-window lamp waver, NEW portal cool pulse
  (anchors 0.600/0.350, 0.638/0.585, 0.845/0.660), dawn wash kept. Additive
  glow material when the new shader is present.
- `Assets/Scripts/UI/MenuAmbientParticles.cs` — fireflies upgraded: 18 soft radial
  glows wandering target-to-target with asymmetric blink (replaces square pixels +
  lissajous); embers now soft glows; FireAnchor moved to (0.600, 0.350); stars unchanged.
- `Assets/Resources/Shaders/LitIsoMenuGlow.shader` (+meta, new Shaders folder) —
  additive uGUI glow w/ value-noise flicker + heat-shimmer UV wobble. Loaded via
  Resources; all consumers null-check `shader.isSupported` and fall back to plain Images.
- `Assets/Resources/Fonts/lumos.ttf` (+meta) — owner-supplied display font;
  `LitIsoFont.FontResourcePath` now `Fonts/lumos`, antiquity-print kept as fallback.
  NOTE for owner: Lumos is fan-made freeware (CarpeSaponem, 2000) — readme allows
  sharing but is silent on commercial use; revisit before shipping. The 1.2x display
  size compensation was tuned for Antiquity; eyeball Lumos sizes in play mode.

### Validation
- dotnet build NOT run (no .NET in this session's sandbox) — please run
  `dotnet build Assembly-CSharp.csproj` + editor build before merging.
- Worldgen preview re-run: seeds 7/42/999/240611/13371337/20260612 all PASS
  (0 grass-touching-water, 0 illegal height jumps). Rule audit: 70/70 tiles,
  94/94 props unity-live.

### Audit findings (for Codex/owner triage)
1. `Resources/Tiles/dungeon_floor_1..5.png` are 256x512 but imported at PPU 32 like
   the 32px tiles → 8–16 world-units per sprite on a (1, 0.5) grid. Need PPU 256+
   slicing or 32px re-author before any dungeon scene uses them.
2. `Resources/Decorations`: 86 props at 128px/PPU 100, 9 at 32px/PPU 32, and
   pine/tree at PPU 78/80 — three scale families; fine if intentional, but worth a
   one-pass size audit against the prop catalog.
3. `StreamingAssets/worldgen/biomes/coast.json` is dead config: not in
   biome_suite.biomeOrder and uses the old beach-band schema. Remove or wire in.
4. Schema drift: beach/coast biome JSONs use {bandOrder,cliffs,decor}; the rest use
   {surfaceBase,features}. Runtime authority is C# (FoundationContent), so JSON rule
   intent (snow waterApron, meadow accentRate) can silently drift — audit script
   checks ids only, not behavior.
5. Runtime has a `desert` biome (FoundationContent, climate 0.88/0.15) with no
   worldgen JSON and no entry in biome_suite.biomeOrder — confirm its art set is
   promoted or remap hot/dry climate to meadow like inland beach.
6. `IsoTerrainSampler`: magic seed 240611 returns the showcase world in production —
   a player typing that seed gets the demo layout. Suggest a debug flag instead.
7. BiomeSketch sync manifest points at `C:/tmp/LitIsoWorldGen/...` — machine-local,
   outside git; a wipe of C:\tmp orphans the pipeline state.

## 2026-06-12 — Layered character creator (paperdoll) shipped, two hooks for you

New system in my lane: `Assets/Scripts/UI/CharacterCreator/` + layer sheets in
`Assets/Resources/Characters/Layers/` + pipeline in `Pixel Pipeline/character_forge/`
(see its README). Original art only — replaces the LPC-style generator idea with
zero-attribution assets we own. Same sheet contract as PlayerAnimator
(8 rows S..SW x 4 frames, 512x1024, pivot/PPU copied from BlackMage sheet).

Hooks I'd like from your lane, whenever convenient (
## 2026-06-12 — Creation Instance biome/weather showcase (new, builds on 06-10 void spec)

Owner wants to **see** the proposed biomes + their weather in-engine rather than
only as static previews (Tools/BiomeSketch/previews.html). Proposal: extend the
existing **Creation Instance showroom** (ledger row "Creation Instance showroom
launch") with a biome gallery mode, since it's already a safe sandbox launch
profile.

- **Biome showcase wing**: a strip of flat platforms/cells, one per
  `biomeOrder` entry (meadow/forest/beach/coast/mountain/snow/water/dungeon),
  each rendered with that biome's real base/accent tile pools + a small prop
  sample (use `biome_assignments.json` from `Tools/BiomeSketch/rules.html` as
  the source of truth once exported — Phase-0/1 of `Docs/WORLDGEN_RULES_PROPOSAL.md`).
  Walking onto a cell's footprint triggers that biome's weather via
  `FoundationWeatherVisuals` + the newly-imported Pixel Weather Particles
  (rain/snow/fog per biome — meadow/forest light rain, snow biome = snowfall,
  desert/beach = heat shimmer/clear, mountain = fog+wind).
- **Music tie-in**: dungeon cell in the showcase wing should call
  `LitIso.Audio.MusicCues.PlayDungeon()` on entry / `StopCue()` on exit (new
  helper, this session — ducks the overworld day/night bed). Tavern-themed
  cell (if you add one) → `PlayTavern()`.
- **Void rework dovetail**: the dungeon showcase cell is the natural test bed
  for the 06-10 void spec — walkable tiles only, void background + motes,
  exit portal back to the showcase wing.
- This is additive to the showroom, not a new scene — same launch profile,
  gate behind the existing debug/launch menu entry point.
- Not blocking: static previews (`Tools/BiomeSketch/previews.html`,
  `previews/closeups`) already give the owner a biome/blend/town reference
  while this lands.

Ownership: Foundation/Creation Instance = Codex lane. I can help wire weather
biome->effect mapping table or export biome_assignments.json on request.

## 2026-06-12 — Close-up tile-art previews + combined gallery

`Tools/WorldGenPreview/render_closeups.py` now renders 8 isometric close-ups
(3072x1680, real Assets/Resources/Tiles + Decorations art, cellSize 1x0.5,
screenX=(x-y)*16, screenY=(x+y)*8): meadow, forest, forest->meadow blend,
snow->meadow blend, coastline, town core, dungeon-void island, mountain
strata. Manifest: `Tools/BiomeSketch/previews/closeups.json`.

New `Tools/BiomeSketch/build_previews_page.py` rebuilds `previews.html` as a
single self-contained file with two sections: close-ups first (downscaled to
1400px wide for the embed, full-res stays in previews/), then the existing
9 rule-layout previews. Regenerate via:
`render_closeups.py && preview_proposed_rules.py && build_previews_page.py`.
~4MB file, opens directly in a browser, no server needed.

## 2026-06-12 — Triage of 61 unassigned props + 14 unassigned tiles in rules.html (task #21)

Read `Tools/BiomeSketch/biome_rules_data.js` (BIOME_RULES_SEED), cross-referenced
`Docs/WORLDGEN_RULES_PROPOSAL.md` Part 1/2 (camp tiering C5, ore ladder C6,
settlement D1-D6). Wrote `Tools/BiomeSketch/biome_assignments_triage.json`
(schema `litiso.biome_rules_assignments.v1`, extension key `retireCandidates`)
and merged it directly into `biome_rules_data.js` (`assignments`/`unassigned`
updated, `retireCandidates` added as a new top-level array; `assetIndex`,
`featureProps`, `biomeOrder` untouched). Reset-to-seed in rules.html now
reflects this triage.

**Assigned (24 props + 8 tiles), by biome:**

- **forest**: `bush`, `campfire_new`, `forest_dead_tree`, `forest_stump`,
  `glowbug`, `wisp`, `ore_copper`, `plains_rock_v2` + `plains_rock_v2_{0-3}_big`,
  `tree`. Tiles: `canopy_1/2/3` (accent, canopy-as-terrain per C4),
  `forest_mud_path` (accent, road shoulders per D4).
- **meadow**: `bush`, `campfire_new`, `glowbug`, `wisp`, `plains_tree_v2`,
  `plains_bush_v2_{0-3}_big`, `plains_rock_v2` + `plains_rock_v2_{0-3}_big`,
  `tree`. Tiles: `grass_1` (base), `grass_2`/`soil`/`stone_path` (accent).
- **mountain**: `ore_copper`, `ore_iron`, `ore_silver`, `ore_gold`,
  `ore_manacrystal`, `ore_starmetal` (C6 ore ladder — full set lands here,
  copper also in forest, silver/gold also in snow, mana/star also in
  dungeon), `plains_rock_v2` + `plains_rock_v2_{0-3}_big`.
- **snow**: `ore_silver`, `ore_gold`, `plains_rock_v2` + `plains_rock_v2_{0-3}_big`.
- **dungeon**: `dungeon_chest_wood`, `ore_manacrystal`, `ore_starmetal`.

Generic outdoor decor (`bush`, `tree`, `glowbug`, `wisp`, `campfire_new`,
`plains_rock_v2*` family) went to multiple biomes since they're plausible
anywhere grass/rock surfaces exist (meadow/forest primarily; rock variants
also mountain/snow). Ore set follows C6's elevation/biome ladder exactly.

**Retire candidates (43 total — 37 props + 6 tiles), left in `unassigned`
with reasons in `retireCandidates`:**

- *Settlement interior/furniture (D6 — interiors not wired, keep out of
  outdoor scatter)*: `alchemy_table`, `anvil`, `bar_counter`, `book_stack`,
  `cooking_station`, `crafting_table`, `furnace`, `grindstone`,
  `guild_banner_stand`, `guild_notice_board`, `guild_round_table`,
  `keg_rack`, `loom`, `rune_station`, `sawmill_bench`, `tanning_rack`,
  `tavern_table`, `weapon_rack`.
- *Settlement building art, ranked variants r1/r2/r3 (D2 town lots not
  implemented)*: `guild_hall_r1/r2/r3`, `library_r1/r2/r3`, `shop_r1/r2/r3`,
  `tavern_r1/r2/r3`.
- *Town lot props (D2/D5 not implemented)*: `market_stall_blue`,
  `market_stall_red`.
- *Lighting props (interior/road dressing, D6/D2/D4 not implemented)*:
  `brazier`, `torch_standing`, `torch_wall`, `candle_lantern` (interior),
  `lantern_post` (settlement/road).
- *Dungeon floor tiles, duplicate of promoted `dungeon2_*` family + wrong
  PPU (cleanup item 2)*: `dungeon_floor_1` through `dungeon_floor_5`.
- *Badlands tile, desert biome not implemented (cleanup item 5)*:
  `badlands_2`.

All retire candidates are genuinely-promoted assets staged for future
milestones (settlement interiors/lots, dungeon room dressing, desert biome) —
none are recommended for deletion yet, just kept out of the active outdoor
scatter pools until those systems land. Re-run `rules.html` "reset to seed"
to pick up the new baseline; `build_biome_rules_data.py` will overwrite this
on its next run unless extended to read `biome_assignments_triage.json` as
an overrides file (not yet done — direct edit only, noted here for whoever
picks up the regen script next).

## 2026-06-13 - Movement step-height + DEX progression + dungeon fog removal + trap art (heads-up, my edits in your lane)

Owner-directed changes touching IsoCoreFoundation/** — flagging since this is your
lane, no handoff needed before merge but please review on next pass:

- **GameBuilder.cs** (Tools/LIT-ISO/Build): updated to build `MenuScene` +
  `IsoCoreFoundation` (was still pointing at the retired
  `InfinitePlainsPrototype.unity`). `.exe` now boots the real game.
- **Walking step-height invariant changed**: `maxWalkStepHeight` was a hard 0
  (walking could never ascend; only an active jump could, via `jumpClimbSteps`).
  Owner asked for the player to be able to walk up one tile. Added
  `FoundationConfig.maxWalkStepHeight = 1` and updated `IsoFoundationPlayer.Walkable()`
  to allow ascending up to that many height steps while walking; jump's
  `jumpClimbSteps` allowance still stacks on top for taller cliffs. Doc comments in
  `IsoFoundationPlayer.cs`/`FoundationConfig.cs` updated to drop the old "invariant"
  language — please treat `maxWalkStepHeight=1` as the new baseline going forward.
- **DEX-driven move speed + cooldown/cast-time scaling** (new stat progression):
  `FoundationPlayerStats` gained `MoveSpeedMultiplier` (+/-2.5% per DEX point vs.
  baseline DEX 8, clamped [0.7x, 1.6x]) and `CooldownMultiplier` (-/+2% per DEX
  point, clamped [0.5x, 1.3x]). Both are 1.0x at the starting DEX of 8, so existing
  balance/tuning is untouched for fresh characters. Wired: `IsoFoundationPlayer`
  multiplies move distance by `MoveSpeedMultiplier`; `FoundationAbilitySystem`
  multiplies `ability.cooldownSeconds` by `CooldownMultiplier` when setting
  `_nextReadyTime`. No UI changes — StatSheetUI already shows DEX.
- **Dungeon fog-of-war removed**: `DungeonRoomFog.Init()` is now a no-op (early
  return before the room loop) per owner request — dungeons are no longer fogged.
  Original implementation kept dead-code-style below (CS0162 suppressed) in case
  it should come back; `FoundationInstanceSystem`'s Init/Update/OnDestroy calls are
  all still safe with zero rooms, no call-site changes needed.
- **Lava/fire-trap tiles now use real art**: added `Resources/Tiles/lava.png`
  (copy of `dungeon2_14`, cracked-magma floor) and `Resources/Tiles/fire_trap.png`
  (copy of `dungeon2_05`, brazier/firepit floor) with proper Sprite meta (32px PPU,
  pivot 0.5/0.75, matches the rest of dungeon2_*). `TileSpriteResolver` picks these
  up automatically by block id — no FoundationContent/generator changes needed, the
  flat-tint fallback in `FoundationContent.cs` is now just a fallback if art is
  missing.

All four are playtestable as-is; let me know if DEX tuning numbers feel off once
you've run it.

## 2026-06-19 — Front-end design system implementation (theme foundation + integration map)

Implemented the approved front-end design system (from
`Docs/handoff/frontend_design/LIT-ISO_Frontend.dc.html`) as a shared, compile-safe
Unity theme module. Application to individual screens is staged as an in-editor task
(layout needs the editor to verify — done blind it risks breaking working panels).

**Added (new, self-contained, compiles against existing `LitIsoFont`):**
- `Assets/Scripts/UI/Theme/LitIsoTheme.cs` — single source of truth: palette
  constants (gold `#E8C468`, dark bases `#0a0b0e`/`#15171C`/`#23262E`, stone, parchment,
  wood, red/green), `DisplayFont`/`BodyFont` (Press Start 2P / Pixelify Sans with
  fallback to lumos/body), `StyleFrame`/`NewFrame` (Stone/Wood/Parchment), `StyleButton`/
  `NewButton` (Gold/Stone, 4 states), `ApplyDisplay`/`ApplyBody`. Signature 5px HARD
  bottom shadow + 4px press offset.
- `Assets/Scripts/UI/Theme/LitIsoButtonPress.cs` — pointer-driven press animation
  (down 4px, shadow flattens). No per-frame polling.

**FONT TODO (owner):** drop the two TTFs into `Assets/Resources/Fonts/` as
`press-start-2p.ttf` and `pixelify-sans.ttf`. They auto-win; until then the theme
falls back to `lumos`/`body` so the project always compiles + renders.

**Integration map — apply these in-editor (each is a small, local change):**
- Main menu (`Assets/Scripts/UI/WelcomeScreenManager.cs`): route title/wordmark Text
  through `LitIsoTheme.ApplyDisplay`; main buttons (Create World / Play / Back) through
  `LitIsoTheme.StyleButton(btn, bg, ButtonStyle.Gold)` for primary, `.Stone` for
  secondary; panels through `StyleFrame(img, FrameStyle.Stone)`.
- Create World / Appearance / Calling sub-screens: same — gold primary action button,
  stone sub-panels, parchment for read-only info cards; calling cards = `NewFrame` Stone.
- In-game tabbed panel (`Assets/Scripts/UI/InGame/CharacterPanelView.cs` +
  `CraftingView`, skills/quests/map tabs): tab bar buttons via `StyleButton(.Stone)`
  with the active tab tinted Gold; panel background `StyleFrame(Stone)`; section
  headings `ApplyDisplay`, body via `ApplyBody`. Keep all existing data bindings to
  `FoundationBootstrap` runtime — only restyle.
- HUD: headings/labels through `ApplyDisplay`/`ApplyBody` for face consistency.

**Verify:** open MenuScene + IsoCoreFoundation, `dotnet build IsoCore.Foundation.csproj`
(should be clean — theme only references existing `LitIsoFont.UI/Body/Apply`), then
eyeball each screen and nudge offsets. No invariants touched; UI-only.
