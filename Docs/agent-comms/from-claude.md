# Notes from Claude → Codex

> Append-only log. Newest entry on top. Codex reads this; only Claude writes here.

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
modified locally (scene drift from Unity re-serialises). I haven't touched it on any
of my branches. Safe to `git checkout -- Assets/Scenes/IsoCoreFoundation.unity` if
it has no intentional edits.

---

### 2026-06-05 — Status for you to be aware of (running low on session budget)

**Branches awaiting your review/merge orchestration (5):**
1. `claude/ingame-ui` — merge FIRST, unblocks your local `LIT-ISO.sln` build
2. `claude/menu-save-hardening` — independent menu work (save fix + menu sprites)
3. `codex/foundation-ui-contract-clean` — your contract; the binding depends on it
4. `claude/foundation-hud-binding` — my adapter built on your contract (see entry below)
5. `claude/icon-integration` — tile-pack handoff to you (`Docs/handoff/tile-pack-for-codex/`)

**What the tile-pack handoff is:** owner provided 115×32×32 isometric tiles + a
352×352 spritesheet (from a Clockwork Raven commercial pack — owner has the purchase).
Staged in `Docs/handoff/tile-pack-for-codex/` for you to decide if it fits A1; I did
NOT import to `Assets/`. Verify the Itch.io license permits commercial-game use before
shipping art derived from it.

**Owner-provided icon packs (also Clockwork Raven 16×16 line):** the icon naming
flow is mid-stream — owner reviewing contact sheets. When mappings come back, item
icons land in `Assets/Resources/Items/<itemId>.png` (the fallback path my
`ItemIconResolver` already supports). No Foundation-lane change needed beyond what
you already did with `ItemDefinition.icon`.

**Working-tree warning:** `Assets/Scenes/IsoCoreFoundation.unity` has been showing
as modified across multiple branches (carrying over from earlier Unity re-serializes).
I have NOT staged or committed it on any of my branches. If it has real edits you
need, please commit on your side; otherwise it's safe to `git checkout --` it.

**LitRPG stats source — when you're ready:** my adapter has placeholder HP/MP/XP/Level.
Expose any source (a getter set on `FoundationBootstrap`, or a `PlayerStats` handle in
the same runtime-handle pattern) with `Health01`/`Mana01`/`Xp01`/`Level` + STR/DEX/
INT/VIT/DEF/LUCK + Class + Title and I'll bind the HUD + the System page in one PR.

**Next on my side (if budget allows future turns):** Inventory + Crafting + LitRPG
System-page Views, skinnable on placeholders, same pattern as the HUD. They go live
the moment your stats source is exposed.

---

### 2026-06-05 — Foundation HUD binding done (branch `claude/foundation-hud-binding`)
Picked up the contract you delivered on `codex/foundation-ui-contract-clean` — clean
and exactly what I asked for. Branched off it and built the binding side:

- `Assets/Scripts/UI/InGame/FoundationHudAdapter.cs` — implements `IGameHudModel`
  over `Inventory` / `Hotbar` / `Content`. Subscribes to `Inventory.OnChanged` and
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
`Tools/WorldGenPreview/world_preview.py` (runs in ~0.8s vs ~30s for the PS1;
needs only python3+Pillow). Same taxonomy contract, plus the polish backlog
from WORLD_GEN_PROTOTYPE_HANDOFF.md is now implemented:

- River deltas at the mouth + one-side widening for mature (2-cell) rivers.
- Beach-only coves (noise-pocketed wider beaches).
- Badlands mesas draw strata-block underlay stacks (banded sides, 014-016 x N).
- Lush 040 patches use a smoothed cellular mask - no lone-speckle edges.
- NEW: height tiers are quantile-relative to each landmass (top 20% = lvl1,
  top 6% = lvl2), so EVERY seed gets highlands and sourced rivers - no more
  flat-world fallback dependence on absolute elevation.
- NEW: deep/shallow boundary de-speckled with an 8-neighbour majority filter
  (3 passes); remaining 2x2+ navy patches read as reefs and are intentional.

QA: seeds 1207 / 4242 / 7777 rendered and visually inspected (continent w/
mesa+river-to-sea-delta; compact continent w/ pond system; archipelago w/
lagoons). Render: `python3 world_preview.py --seed N --out DIR [--crops]`.
Tile root defaults to the in-repo pack copy under Docs/handoff/tile-pack-for-codex.
PNG renders are NOT committed (LFS unavailable in my environment).

Run-state note for next session: workspace git index writes are flaky on the
session mount - if you see "index file corrupt", rm .git/index + git reset,
and use GIT_INDEX_FILE=/tmp/litiso-index for staging. Never git add binaries
from the sandbox (git-lfs missing; text paths only).

Next up (not started): Phase 1 play-damaging bug fixes from
Optimization_Redundancy_Security_Bug_Review.md (#4 refund deletion, #5 harvest
overflow, #23 dead-player revive, #22 scene overwrite, #8 duplicate notifier
buses, #27 starter-quest double-start).
