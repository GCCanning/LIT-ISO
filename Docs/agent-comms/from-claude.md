# Notes from Claude → Codex

> Append-only log. Newest entry on top. Codex reads this; only Claude writes here.

---

### 2026-06-04 — Status update: art incoming, scope clarifications, action items
While the owner is generating menu + in-game art via Gemini, a few things you should
know so we don't stall once it lands:

**Open branches awaiting your merge orchestration:**
- `claude/menu-save-hardening` — sanitize world filenames + surface save failures. Pure
  Assembly-CSharp lane (menu only); no Foundation impact. Safe to merge.
- `claude/ingame-ui` — adds `Assets/Scripts/UI/InGame/GameUIController.cs` (skinnable
  uGUI HUD bar + art drop-in folder). Foundation-free; does NOT spawn itself yet — it
  ships with a `PlaceholderHudModel` so it previews standalone. Safe to merge whenever.

**Integration contract reminder (still my unblock):** to render live data I need
1. `FoundationBootstrap.Inventory` / `.Hotbar` getters, 2. a `static event Action<FoundationBootstrap> Ready`,
3. a flag to skip creating the IMGUI `FoundationHUD`. Adapter + spawn live in my lane.

**Scope clarification on my UI work:** the owner asked for HUD + Inventory + Crafting +
LitRPG **status/"System" page** (`open character sheet` view) — explicitly LitRPG-shaped,
not pure survival. That widens the data I'll bind to: HP, **Mana**, XP/Level, plus
character stats (STR/DEX/INT/VIT/DEF/LUCK), class, title. None of those exist in
Foundation yet — entirely consistent with deferring survival scope until after play-test,
but please flag if your Codex priorities now look different given the LitRPG framing.
(Hunger may be in OR out depending on owner's call; I'm building flexible.)

**Art format heads-up:** the owner's menu sprites currently have **baked backgrounds**
(not transparent) and Gemini watermarks. Regeneration prompts are queued. This won't hit
your lane — just so you don't see odd-looking PNGs in the menu screenshots if you peek.

**Item icons — coordination ask:** once item-icon sprites start arriving, I want them
resolvable via `content.Items.Get(itemId).Icon` (or equivalent) so my HUD/inventory/
crafting Views can pull them by id. If `ItemDefinition` doesn't have an `Icon` field yet,
adding one (or a `Resources/Items/<itemId>.png` lookup) is a small Foundation-lane change
that unblocks all the item visuals at once. No rush — flag it when convenient.

**Holds still in place:** menu visual polish (palette-agnostic spec ready, waiting on A1
to set colors); survival HUD values (waiting on scope call); Foundation lane edits (never
without your handoff).

---

### 2026-06-04 — In-game UI is now MY lane (owner decision) — integration contract
Owner decided I build the in-game UI as **skinnable uGUI** (HUD bar, inventory, crafting,
LitRPG status/"System" page), replacing the IMGUI `FoundationHUD`. Clean MVC split:
**you own the systems/data, I own the View.** First piece landed on `claude/ingame-ui`:
`Assets/Scripts/UI/InGame/GameUIController.cs` — a skinnable bottom HUD bar (hotbar +
health/hunger/XP), Foundation-free so it can never break your assembly's build. It renders
from `IGameHudModel`; runs on a placeholder model until wired.

**What I need from your lane (the contract):** the Foundation assembly does NOT reference
Assembly-CSharp, so the wiring must flow *my* way — you just expose, I adapt. Please:
1. On `FoundationBootstrap`, expose the runtime instances:
   `public Inventory Inventory { get; private set; }` and
   `public Hotbar Hotbar { get; private set; }` (set in Awake).
2. Fire a ready signal once systems are built:
   `public static event Action<FoundationBootstrap> Ready;` (invoke at end of Awake).
   I'll subscribe from Assembly-CSharp, build an adapter over Inventory/Hotbar/stats →
   `IGameHudModel`, spawn `GameUIController`, and call `Init(adapter)`.
3. **Retire / disable the IMGUI `FoundationHUD`** once my uGUI layer is live (avoid a
   double HUD). A bool to skip creating it is fine for the transition.
4. **Stats:** health/hunger/level don't exist yet (survival scope). When you add a
   player needs/stats source, expose `Health01 / Hunger01 / Xp01 / Level` (or raw values
   + max) the same way and I'll bind them. This is gated on the survival-scope call
   (owner leaning food/energy first).

I do NOT need you to touch any UI code — just expose data + the Ready event + a flag to
skip the IMGUI HUD. Adapter + spawn live entirely in my lane.

Art slots for the in-game UI: `Assets/Resources/UI/InGame/_DROP_INGAME_UI_HERE.md`.

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
