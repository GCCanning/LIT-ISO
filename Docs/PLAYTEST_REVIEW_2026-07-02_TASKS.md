# Playtest review tasks — 2026-07-02 (owner-approved work order)

## FOLLOW-UP — 2026-07-03 session (footage 23:42 review + owner requests)
- [x] Footage regressions: coords-value text invisible (uGUI vertical Truncate), static
      status-FX chip mock removed, snow coverage unified (checkerboard), firefly particles
      had no texture (hard green quads). `5b01d8047`
- [x] WORKING MINIMAP: FoundationMapOverlay renders a live MiniTexture (1 texel = 1 explored
      cell, N up, dungeon-aware); HUD map frame shows it via RawImage; decorative map mocks
      and the overlapping IMGUI mini window removed. `25420cf23` (+owner `a892d9d17`)
- [x] Item icons: Shikashi's Fantasy Icons Pack v2 (CC-BY, credited) → all 20 icon-less
      items via Tools/UIKits/shikashi_items_build.py. `e58c56561`
      ⚠ 20 new PNGs untracked — Windows: `git add Assets/Resources/Items/*.png` + commit.
- [x] Skin pass 2: vitals panel/bars, ability chips, ATTACK button on the Kenney dark-wood
      skin (procedural fallbacks kept). `ba6894f27` (+owner `cee82d42a`)
- [x] Character creator review + fixes: no exit besides CONFIRM (added BACK/ESC cancel that
      returns to the menu), HSV sliders snapped mid-drag, baked texture leak. `c9ac57927`
      Still open (noted, not fixed): creator predates the dark-wood skin (flat LitIsoTheme
      styling); RESET reverts to last save rather than defaults; SP bar has no skin sprite.

## STATUS — executed 2026-07-02, branch `feat/playtest-0702-fixes` (off `feat/biome-asset-wiring` @ 0de22597a)
- [x] 1 FloatingText — root cause was a STATIC screen-space design mock in GameUIController
      (BuildCombatText: "-12"/"+40 XP"/"FIRE TRAP!" at fixed screen %). Removed. The world-space
      pooled FloatingText was always fine. `aaf1943eb`
- [x] 2 Hazard-free spawn — guards added at both stamp seams (sampler clamp within
      spawnClearingRadius+12; dungeon generator refuses hazards within 24 cells of origin).
      NOTE: the overworld never emitted fire_trap/lava — the playtest "-12 fire trap" evidence
      was the #1 mock. Guard is defense-in-depth. `7315f4a28`
- [x] 3 Day-band overlap — standalone TrialStatusBanner deleted; the day-band TRIAL chip is the
      single live element, now also hides on trial completion. `a626a9053` (+NUL-padding fix `a9bb88ba1`)
- [x] 4 Top-right HUD — lazy player re-resolve for coords; region caption wired to
      FoundationBiomeDiscovery + compass octant; fake minimap river deleted + RectMask2D;
      notification stack moved below the column (was the "FlashS" chip). `4835534e2`
- [x] 5 Mob facing — MobDefinition.artFacesLeft + flipX in Animate(); hunters track prey while
      stationary. Slime sheet faces LEFT, predator plants RIGHT (checked frames). `a7d6335d4`
- [x] 6 Hit feedback — red flash (night-tint-aware restore), 0.2u walkable-checked knockback
      (attacker origin passed from all damage paths), 0.06s per-mob hit-stop, hurt frames now
      play, SFX verified. Kill-only timeScale dip skipped (optional; risky global). `059e60eda`
- [x] 7 Scale audit — Tools/ScaleAudit/audit_world_scale.py dumps units-vs-player + flags.
      Trees 1.5→3.0/3.2u, cactus 1.5u, dead_scrub 0.6u, tavern 3.2u, library 3.4u, predator
      plants PPU 56→31/31/30 (≈1.0–1.15× player). `4a8fa5dbc`
- [x] 8 Shader pass — altitude ramp now spans all bands (plateau reads), snow-line baked at h5+,
      side faces 0.70→0.62, WaterAmbient shader (shimmer + foam via MPB flags), night
      desaturate/blue-shift with warm-source exemption (fires glow by contrast). Halos already
      existed (CampfireGlow). Per-face LightFromDir keying deferred (needs face split). `e55c6d08a`
- [x] 9/9a Kenney skin — kenney_skin_build.py recolours the CC0 kits to the locked dark-wood
      palette; all 15 UI slots exported; metas' 9-slice borders updated; Kenney credited.
      ⚠ PNGs are LFS-tracked and the agent env has no git-lfs: on Windows run
      `git add Assets/Resources/UI/InGame/*.png` + commit. `31c3a41bc`
- BUILD VERIFICATION still owed (no Unity in agent env): F10-record and eyeball #1 despawn,
  #4 coords/minimap in a build, #5/#6 combat feel, #7 occlusion fade on 3u trees, #8 water/night
  on real tiles, #9 skin look (owner approval of the style before batch restyle elsewhere).

Source: F10 recording `Build/LIT-ISO_2026-07-02_195439/LIT-ISO/Recordings/rec_20260702_195606`
(150 frames + session.json) reviewed frame-by-frame, plus owner direction in chat.
Baseline for this list: branch `feat/biome-asset-wiring` @ `0de22597a` (builds clean, playable).

**Owner said HOLD on portal sparseness/hint-gating (A4). Everything below is approved.**

## 1. FloatingText never despawns  `[bug, small]`
Evidence: "-12", "FIRE TRAP!", "+40 XP" from the spawn fire trap remained pinned at the SAME
screen positions for 60+ s / 40 world units in every frame. Fix lifetime/cleanup in
`Assets/Scripts/IsoCoreFoundation/World/FloatingText.cs` (check: coroutine vs Update timer that
never runs in builds, or screen-space parent never destroyed). Verify in a build, not the editor.

## 2. Hazard-free spawn  `[bug, small]`
Player took -12 fire-trap damage seconds into Day 1 at the spawn clearing. Guard worldgen:
no hazard surface tiles (`fire_trap`, lava — see `IsoFoundationPlayer.HazardSurfaceBlocks`)
within ~12 cells of the origin/spawn clearing. Seam: wherever the sampler stamps hazard
surfaces (IsoTerrainSampler / spawn-clearing flattening). New world required to verify.

## 3. Day-band overlap  `[bug, small]`
`TrialStatusBanner` renders on top of the clock band's phase·biome line (two stacked gold bars
top-centre in every frame). Merge: TrialStatusBanner content ("TRIAL · Day N of 7 · Forecast X")
should BE the day chip in GameUIController's band (single element), or reposition the banner.
Files: `Assets/Scripts/UI/InGame/TrialStatusBanner.cs`, `GameUIController.cs` (day band build ~line 727-750).

## 4. Top-right HUD cluster  `[bug, small-med]`
- Coords X/Y/Z cells are EMPTY in builds → `GameUIController.BindLiveData` player handle is
  null at runtime; trace `GameHudInitializer` binding order in a build (clock binds fine).
- Minimap caption hard-codes "EMBERFALL · NW" + a "Discovered" placeholder — wire to
  `FoundationBiomeDiscovery.ActiveBiomeDisplay` (same pattern as the phase band fix).
- Stray blue diagonal line drawn across the minimap (artifact — river polyline or compass
  needle scaled wrong) and a purple "FlashS" chip overlapping the map panel (mis-anchored
  ability/buff toast). Layering pass on the whole top-right cluster.

## 5. Mobs must FACE the player when hunting  `[feel, small]`
Mob sprites never turn. `Mob._faceDir` already tracks the chase direction — until the owner
supplies 4-direction row mappings (blocked, see HANDOVER_2026-06-29 §4.3), do the cheap 2-dir
version NOW: `_sr.flipX` from `_faceDir.x` sign in `Mob.Animate()`. Confirm base art faces
LEFT or RIGHT first (check a slime/plant frame in `Resources/Characters/predator_plant_1/`).

## 6. Hit feedback on mobs  `[feel, medium — do as one pass]`
Owner: "There is no feedback when you hit a mob — how do other games do it?" Standard kit
(Romestead-class action feel), all cheap and engine-native, add to `Mob.TakeMobDamage`:
1. **White flash** — sprite colour to white/red for 0.06-0.10 s, then restore (careful: night
   tint must restore to its tinted colour, not plain white — store a base colour field).
2. **Knockback nudge** — push `_ground` 0.15-0.25u away from the attacker (walkable-checked,
   reuse ApplySeparation's clamp pattern).
3. **Hit-stop** — freeze the mob's own update ~0.06 s (skip Update while a hitStop timer runs);
   optionally a 1-frame global `Time.timeScale` dip on kill only.
4. **Damage number** — FloatingText at the mob (exists — fix #1 first).
5. **Hurt frames** — `_hurt` frames already load; ensure they actually play on damage.
6. **Sound** — `SfxManager.Play("hit")` exists; verify it fires per melee hit.
The player already gets a camera impulse on confirmed hits (FoundationImpactFeedback) — keep.

## 7. Prop/mob scale audit vs the player  `[art, medium]`
Owner rule: **scale everything off the player = 6 ft.** Player is ~1.1 world-units tall.
From the footage: trees read ~1-1.3× player height (a "6-foot tree"), predator plants ~0.6×.
Targets (real-world logic, pixel-art compression tolerated):
- canopy trees 2.5-3.5× player (2.8-3.8u); young trees/saplings 1.5×; bushes 0.5-0.8×;
- predator plants ≈ 1.0-1.1× player (owner said "player height" on 06-29 — still pending);
- rocks/boulders 0.4-0.9×; buildings (tavern/guild) 2.5-4× at the eave.
Mechanics: mob size = PPU on frame .metas + `MobDefinition.sizeUnits` (shadow); decoration
size = sprite PPU in `Resources/Decorations/*.meta`. `Tools/ScaleAudit/` exists — extend it to
dump a "units tall vs player" table for every decoration/mob and flag off-target ones, then
adjust PPUs in batch. Check `maxTreeHeight`/occlusion fade + TilemapRenderer sort after resize.

## 8. Shader pass (owner priority B2b-2/3 — includes height readability)  `[shader, medium-large]`
Build on what already landed today (SpriteAmbient day/night tint material,
ProjectedSpriteShadow.shader, AtmosphereStrip.shader — all Built-in pipeline, keep it that way):
- **Altitude grading**: extend SpriteAmbient with a per-height tint ramp (cooler + lighter per
  height band, snow-line accumulation at h5+) so elevation reads at a glance.
- **Cliff-face contrast**: darken south/west cliff faces vs top tiles (directional key light
  from DayNightSystem.LightFromDir) so walls pop from floors.
- **Water**: subtle scrolling 2-tone shimmer + animated shoreline foam edge on water tiles.
- **Night grading**: deepen the existing night tint into a proper colour grade (desaturate +
  blue-shift world, EXCEPT warm light sources) so campfires/torches glow by contrast.
- **Light halos**: soft additive radius sprite on campfire/torch/brazier placeables, flicker
  from a noise curve — pairs with the ward ring for the "firelight = safety" language.
- Constraint: Built-in pipeline only (NO URP), pixel-crisp at native scale, no per-frame
  material instantiation (use MaterialPropertyBlocks / shared materials).
The h5-7 plateau reads as flat checkerboard — no altitude cue. Options that fit the Built-in
pipeline + existing work: altitude-tinted ambient (SpriteAmbient already tints per-sprite —
add a per-height lightening/cool shift), stronger cliff-face contrast at high elevations, and/or
snow-line accumulation by height band. Ties into the owner's shaders/lighting priority (B2b-2/3).

## 9. HUD overhaul with a FREE skin  `[ui, large — after 3/4]`
Owner: current HUD "is a mess — find a free alternative that's better."
Recommendation (licence-safe, pixel-native, ships today):
- **Kenney UI packs — CC0** (kenney.nl: "UI Pack", "UI Pack – RPG Expansion", "Fantasy UI
  Borders"): 9-slice panels, bars, buttons, slots; public domain, no attribution required,
  restyle freely to the warm/cozy palette. THE safe default.
- **Mounir Tohami's "Pixel Art GUI Elements" (itch.io, CC0)** — closer to a cozy pixel-RPG
  look (panels, slots, bars) if a softer style is preferred.
- Avoid non-CC0 itch packs without checking terms (many forbid redistribution in source repos).
Plan: keep GameUIController's procedural layout + live bindings (they work), swap the flat
colour fills for 9-sliced CC0 sprites under `Resources/UI/InGame/` (the skin auto-load slots
already exist — see `_DROP_INGAME_UI_HERE.md`), fix hierarchy per the UI handoff review
(vitals TL, day band TC, minimap+quest TR, hotbar BC, abilities BL). The PixelLab shell
(UI_HANDOFF_REVIEW_2026-06-30) can replace the CC0 skin later without relayout — same slots.
Owner must approve the chosen pack's look before the batch restyle.

## 9a. APPROVED (owner, 2026-07-02): Kenney kit + dark-wood adventurer palette
Owner approved proceeding with the K