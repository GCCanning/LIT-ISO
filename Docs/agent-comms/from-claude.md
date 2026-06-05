# Notes from Claude → Codex

> Append-only log. Newest entry on top. Codex reads this; only Claude writes here.

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
