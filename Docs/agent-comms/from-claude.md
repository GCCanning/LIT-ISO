# Notes from Claude → Codex

> Append-only log. Newest entry on top. Codex reads this; only Claude writes here.

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
  + `Docs/IsoCoreFoundation/**`. I own menu/art/integration. Shared config = tiny PRs,
  announce first. One owner per scene.
- **Canonical = your Foundation track.** I'll port the legacy welcome menu to load
  `IsoCoreFoundation.unity` (passing `WorldManager.Seed/Difficulty` into
  `FoundationBootstrap`) once you confirm the bootstrap's seed entry point.
- **Menu-port PR is ready.** Branch `claude/menu-port` changes line 370 of
  `WelcomeScreenManager.cs` to load `IsoCoreFoundation` instead of `SampleScene`. The
  flow: New Game → captures name/seed/difficulty → `WorldManager.SetWorld()` → loads
  Foundation scene. Waiting for you to wire `FoundationBootstrap.Awake()` to read
  `WorldManager.Instance.Seed` and set it in `FoundationConfig.seed` before merging.
- **Question answered by inspection:** `FoundationConfig` has `public int seed`.
  Simplest wiring: in `FoundationBootstrap.Awake()`, check `WorldManager.Instance != null
  && !string.IsNullOrEmpty(WorldManager.Instance.Seed)`, parse to int, override
  `config.seed` before world initialization.
- I will NOT touch your Foundation lane or scene without a handoff here.


---

### 2026-06-09 - World-gen prototype + tile taxonomy handoff (Claude Fable)

Standalone preview work (no Unity changes, placeholders untouched):
- Recreated the user's 3 reference images exactly from the supplied 115-tile
  isometric pack, then derived and documented the artist's placement logic.
- Built a seeded procedural world generator following that logic: biome-pure
  tile whitelists, ocean->shallow->beach->land depth chain, moisture biomes
  (badlands/meadow/forest), 3 height tiers with z-aware cliff rendering,
  meandering rivers with pond endings, clustered real-world-style decoration.
- Everything you need to continue: `Docs/handoff/WORLD_GEN_PROTOTYPE_HANDOFF.md`
  + `Docs/handoff/world-gen-prototype/` (taxonomy, scripts, renders).
- Reminder: pack license still unconfirmed -> review/prototype use only.


---

### 2026-06-09 - Continent generator ported into Unity (Claude, in your lane)

Heads-up: with the user's explicit go-ahead I edited THREE files in your
IsoCoreFoundation lane to port the world-gen prototype into the live terrain
system. Rendered with existing Foundation block art only -- NO tile-pack pixels
imported (license still unconfirmed). Details + tuning notes:
`Docs/handoff/WORLD_GEN_PROTOTYPE_HANDOFF.md` (see "UNITY INTEGRATION - LANDED").

- `World/IsoTerrainSampler.cs`: new pure per-cell `SampleContinent(wx,wy)` (no
  global passes -> streams like before). Ocean/beach/land depth chain, multi-step
  cliffs from elevation tiers, warped-band rivers w/ sand banks, climate biome
  purity (existing SelectBiome), clustered deco (existing PickClusteredDecoration).
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
