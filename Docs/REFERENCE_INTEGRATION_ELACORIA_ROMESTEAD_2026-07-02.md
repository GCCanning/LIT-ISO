# Reference Integration — Elacoria + Romestead (2026-07-02)

One coherent improvement pass adapting design principles from two reference games into
LIT-ISO's existing Foundation systems. Clean-room throughout: no reference code, assets,
names, or content were viewed, extracted, or imported.

---

## 1. Baseline (verified live, not from handover claims)

- Branch `feat/biome-asset-wiring`, 24 commits ahead of origin, un-pushed.
- **Working tree was NOT clean**: ~22 tracked scripts carried uncommitted in-flight work
  (atmosphere overlay wiring, projected shadows, ability VFX flag, worldgen config floors,
  camera impact impulse) plus 65 untracked files (FoundationAtmosphereOverlay.cs,
  FoundationImpactFeedback.cs, AtmosphereTextureImporter.cs, 2 shaders, Alenia VFX
  textures, LitRpgUIStudio + PixelLab UI tooling). A first `git status` through the
  session sandbox falsely reported clean — the sandbox FUSE view of the repo caches
  aggressively and **must not be trusted for git state** (see §8).
- **Compile state**: Unity Editor session 2026-07-02 06:17 compiled clean —
  `[BiomeSuite] applied 8/8`, `FoundationIntegratedSliceValidator.BiomeEcology 4/4`
  (Logs/console_output.log). No `error CS`.
- **Standalone build**: last good build `Build/LIT-ISO_2026-06-26_143929/`. A build
  attempt today 05:53 failed on a since-fixed syntax error in FoundationImpactFeedback.cs
  (fixed on disk 05:59); the build was not re-run after the fix.
- RTK-prefixed shell tooling: **no RTK requirement exists in AGENTS.md/CLAUDE.md** —
  documented here as required; raw git/shell used as the permitted fallback.

## 2. Clean-room methodology

- Reference learning came ONLY from: official Steam store pages (Elacoria app 3907080,
  Romestead app 1805320) and a published third-party review (Netto's Game Room,
  2026-05-26). No game was launched, no files of either game were opened, no screenshots
  of the installed copies were taken.
- The `C:\Users\garyc\Downloads\Romestead` copy shows indicators of unofficial
  distribution; per the clean-room rules it was **not executed, opened, or inspected**.
- No reference content, names, lore, or UI art entered the repository. All shipped work
  below is original LIT-ISO code.

## 3. Comparison matrix

| Observation (public sources) | Principle | LIT-ISO fit | Integration seam | Impact | Effort | Risk | Decision |
|---|---|---|---|---|---|---|---|
| Romestead: "Survive the hordes … in the night. Build defenses and light your torches!" | Night pressure must be *communicated*, not just simulated | Night-danger mults + campfire wards already exist but were invisible | DayNightSystem, FoundationCampingSystem, HUD | High | S | Low | **ADOPT** |
| Romestead: defenses/lighting visibly mark safety | Safety must be seeable | campWardRadius existed with no visual | new CampfireWardRing (LineRenderer, pooled) | High | S | Low | **ADOPT** |
| Romestead review: readable day-prep / night-defense rhythm | Announce transitions before danger | SystemFeed/overlay already exist | dusk + nightfall one-shot warnings in camping system | High | S | Low | **ADOPT** |
| Romestead: HUD information hierarchy (day band, vitals, hotbar) | Phase/danger legible at a glance | GameUIController day band showed hard-coded sample biome | live biome name + phase-coloured band | Med-High | S | Low | **ADOPT** (full Romestead-layout HUD clone stays owner-gated, §6) |
| Elacoria: "Discover, Research… Knowledge is often more valuable than gold" | Exploration → visible progression | Trial evidence spine already exists; biome entry was silent | new FoundationBiomeDiscovery + `biome_discovered` evidence | High | M | Low-Med | **ADOPT** |
| Elacoria: biomes/regions materially different, world continues beyond screen | Biome identity | Owner's in-flight atmosphere/weather work already targets this | landed + preserved; no duplicate manager built | High | — | — | **PRESERVE owner work** |
| Elacoria: infinite world, full digging/destruction, underground oceans | Voxel-scale world mutability | Contradicts deterministic worldgen + tilemap invariants | — | — | XL | High | **REJECT** |
| Both: 8-player co-op | Multiplayer | Explicitly out of scope | — | — | XL | High | **REJECT** |
| Romestead: carry-one-log physical hauling, throwable resources | Tactile encumbrance | Charming but conflicts with LitRPG inventory/hotbar spine and owner's systems | — | Med | L | High | **REJECT (revisit later)** |
| Romestead: gods/offerings talent tree | Meta-progression via patrons | LIT-ISO already has affinities + Trial ranks (identity!) | — | — | L | Med | **REJECT — keep LIT-ISO identity** |
| Romestead: citizens with jobs/automation | Purposeful NPCs reduce grind | Matches canonical §10 but is a Sprint-4 feature | — | High | L | Med | **DEFER** (ranked next steps) |

## 4. Implemented (exact files)

All four improvements interlock around the **campfire-ward / night-preparation loop** —
the Impact-Analysis A2 "core survival identity" item — plus discovery feeding the Trial.

1. **Night rhythm readability** — `Assets/Scripts/IsoCoreFoundation/Survival/FoundationCampingSystem.cs`
   - One-shot dusk warning (t∈[0.68,0.80)) and nightfall warning (DayNightSystem.IsNight
     edge), routed through SystemFeed + interaction overlay, bypassing the anti-spam
     window; message differs when already inside a ward.
2. **Visible campfire ward** — `Assets/Scripts/IsoCoreFoundation/Survival/CampfireWardRing.cs` (+ .meta, new)
   - Single pooled LineRenderer ring on the active camp, world-space radius identical to
     the actual ward check (mechanically honest), ember-pulse colour, fades in from dusk,
     hidden by day. No per-frame allocations (positions cached, colour-only updates).
3. **Night hunters read as dangerous** — `Assets/Scripts/IsoCoreFoundation/Mobs/Mob.cs`
   - `ApplyNightDanger` now also applies a blood-warm sprite tint over the shared
     day/night ambient material so empowered mobs are identifiable at a glance.
4. **Biome discovery journal** — `Assets/Scripts/IsoCoreFoundation/World/FoundationBiomeDiscovery.cs` (+ .meta, new)
   - Polls the player cell (0.6 s), skips pocket instances; first visit per biome fires
     `Discovered: <Biome>` overlay toast + `biome_discovered` Trial evidence
     (Exploration 3 / Survival 1, exploration + character XP — new entry in
     `Assets/Scripts/IsoCoreFoundation/Core/FoundationContent.cs`), with a plain-XP
     fallback if the evidence id is missing. Publishes `ActiveBiomeDisplay` for the HUD.
5. **HUD phase band** — `Assets/Scripts/UI/InGame/GameUIController.cs`
   - Day band now shows the REAL biome ("Dusk · Winterwool Pines") instead of the
     hard-coded design sample, and the phase text is colour-coded (day parchment, dawn
     gold, dusk amber, night danger-red).
6. **Wiring + save spine** — `Assets/Scripts/IsoCoreFoundation/Core/FoundationBootstrap.cs`,
   `Assets/Scripts/IsoCoreFoundation/Core/FoundationSaveData.cs`
   - BiomeDiscovery created in bootstrap after atmosphere systems; snapshot/restore wired
     into CaptureSaveData/ApplySaveData.

## 5. Save / performance impact

- `FoundationSaveData.CurrentVersion` 12 → **13**; new field `string[] discoveredBiomes`.
  Migration: JsonUtility leaves the field null on old saves → `Restore(null)` starts an
  empty journal (the biome you load into re-announces once). No other migration needed.
- Performance: discovery poll is one `GetBiome` per 0.6 s; ward ring is one LineRenderer,
  recomputed only when camp/radius changes; mob tint is a one-time colour set. No new
  per-frame allocations; no unbounded object creation.
- Determinism: worldgen untouched.

## 6. Rejected / deferred (reasons)

- Full Romestead-layout HUD rebuild: owner-gated behind approved PixelLab shell assets
  (`Docs/UI_HANDOFF_REVIEW_2026-06-30.md` "Implementation Gate") and a Romestead HUD
  screenshot the owner wants to supply. Only gate-respecting improvements shipped.
- Voxel digging / infinite world / multiplayer / physical hauling / gods system: see
  matrix — identity, invariant, or scope conflicts.

## 7. Validation performed vs. remaining

Done this session:
- Static verification of every touched file (brace/paren balance vs HEAD, full-file host
  reads, hunk-by-hunk diff review of Bootstrap/Content/GameUIController).
- Baseline evidence gathered from Editor console log + build log (see §1).

**Could not be run from this session** (no .NET/Unity in the sandbox; documented per the
"proportional verification" rule): Unity compile, FoundationValidator,
FoundationIntegratedSliceValidator, batchmode build. An automated attempt to run
`Tools/BuildGame.bat` and the commit script via desktop control was **blocked**: the
computer-use permission dialog timed out twice (2026-07-02, no user present to approve).
This is the precise external blocker for the standalone build. **Owner run-list:**
0. Double-click `Tools/commit_reference_pass_2026-07-02.bat` (two commits, no push).
1. Focus Unity (or run `Tools/BuildGame.bat` with the editor closed).
2. Editor.log / console: expect no `error CS`, `[BiomeSuite] applied 8/8`.
3. `Tools/LIT-ISO/ISO-Core Foundation/Validate Foundation` + integrated slice validator
   (save-roundtrip checks cover the v13 field automatically).
4. NEW world smoke: walk between biomes → "Discovered:" toasts + Trial evidence in feed;
   HUD band shows live biome + phase colours; place/stand near campfire at dusk → ember
   ward ring fades in; dusk + nightfall warnings fire once each; night mobs outside the
   ward spawn tinted.
5. Save → reload: rediscovered biomes must NOT re-toast (discoveredBiomes round-trip).
6. Load a pre-v13 save: must load, journal starts fresh.

## 8. Incidents & risks (full disclosure)

- **Sandbox FUSE staleness**: the session's Linux-side view of the repo served stale file
  sizes/content for minutes at a time. This caused (a) a false-clean `git status` at
  session start and (b) a false "file truncated" alarm mid-session, in response to which
  three files (FoundationCampingSystem.cs, Mob.cs, FoundationSaveData.cs) were rebuilt
  from HEAD + this session's edits. Camping and SaveData had no uncommitted owner deltas
  (verified byte-identical structure); Mob.cs had one (`castLongShadow: true` on the
  depth-polish attach), which was restored. **Residual risk**: any owner edit in Mob.cs
  outside lines 80–155 or SaveData below line 112 that preserved exact line counts would
  have been lost — both files match HEAD line-for-line in count, so this is unlikely, but
  the owner should eyeball `git diff` on those two files before committing.
- **Do not run repo git from the Cowork sandbox**: git-lfs is absent there, so every
  LFS-tracked binary shows as phantom-modified; index refreshes time out. All commits
  should be made from Windows (script provided: `Tools/commit_reference_pass_2026-07-02.bat`).
- **Untracked-but-load-bearing sources**: tracked FoundationBootstrap.cs references
  untracked FoundationAtmosphereOverlay.cs / FoundationImpactFeedback.cs — a fresh clone
  cannot compile until those are committed. The commit script stages them.
- **Alenia VFX pack license** (`Assets/Resources/VFX/Atmosphere/Alenia/`): CC BY 4.0 +
  no-redistribution-as-assets. Use in the game is permitted **with attribution** — add
  "Alenia Studios — Pixel Art Atmospheric VFX Pack" to the credits file before shipping.
  Textures are third-party (owner-added), not PixelLab; acceptable as VFX, flagging for
  owner awareness.

## 9. Before / after (player experience)

Before: night made mobs statistically stronger with no warning, no visible safe zone, and
no visual difference on empowered mobs; the HUD claimed the player was always in
"Emberfall Woods"; crossing into a new biome did nothing.

After: dusk warns you to reach firelight; nightfall announces the hunt; your campfire
projects a visible ember ward exactly matching the mechanical radius; hunters outside it
are visibly blood-tinted; the day band tells you the true region and phase at a glance;
and every first footstep into a new biome is a scored System moment that feeds the
Proving-Week grade — gathering→camp prep→night survival→exploration→Trial score now form
one visible loop.

## 10. Ranked next improvements

1. Land + verify this pass (owner build + smoke list in §7), push the 24+2 commits.
2. Close the day-7 loop (Impact Analysis A1): scoring → rank → class offers → skill
   points — highest identity value, mostly scaffolded.
3. Tent sleep-to-skip from within the ward ring (RestAt exists; surface it as the
   night-preparation payoff) + camp-gear tier vs area danger messaging.
4. Romestead-informed HUD layout implementation once the PixelLab shell assets pass the
   UI handoff gate (owner to supply layout screenshot).
5. Dungeon prep loop (A4): rare hint-gated portals + prep checklist consumables (water,
   sharpening stones, mana stones).
6. Purposeful NPC role scaffolding (A8) reusing SettlementTownsfolkSpawner + character
   creator.
