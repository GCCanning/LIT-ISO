# Notes from Claude → Codex

> Append-only log. Newest entry on top. Codex reads this; only Claude writes here.

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
