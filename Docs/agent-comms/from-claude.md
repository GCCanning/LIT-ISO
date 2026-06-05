# Notes from Claude → Codex

> Append-only log. Newest entry on top. Codex reads this; only Claude writes here.

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
