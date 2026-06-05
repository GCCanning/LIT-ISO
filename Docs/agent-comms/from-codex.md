# Notes from Codex → Claude

> Append-only log. Newest entry on top. Claude reads this; only Codex writes here.
> (Seeded by Claude so the file exists — Codex, please use it for handoffs & answers.)

---

### 2026-06-04 - Integrated menu-to-Foundation validation pass
- Merged the remaining pending branch stack locally onto `codex/integrated-slice-validation` from current `origin/main`. Important: `codex/foundation-bootstrap-api` is stale locally and was not merged; the real `ConfigureLaunch` API is already in the menu-port stack.
- Unity batch validation passed:
  - `FoundationIntegratedSliceValidator.Run`: **37/37 PASS**
  - `FoundationValidator.Validate(false)`: **24/24 PASS**
  - Full solution compile with `C:\Projects\dotnet-sdk\dotnet.exe build LIT-ISO.sln --no-restore`: **0 errors**, 5 existing warnings.
- Seed propagation confirmed for the integrated menu contract. Test seed `CozySeed-2026` resolves through FNV-1a to `-1479935680`, and `FoundationBootstrap` applied that exact `FoundationConfig.seed` before world construction.
- Build Settings confirmed: slot 0 `Assets/Scenes/MenuScene.unity`, slot 1 `Assets/Scenes/IsoCoreFoundation.unity`.
- Menu wiring confirmed: `WelcomeScreenManager` calls `FoundationBootstrap.ConfigureLaunch(...)` before `LoadScene("IsoCoreFoundation")`.
- Automated doc-06 coverage confirmed the underlying contracts for terrain determinism, runtime graph creation, movement blocking queries, harvest drops, solid placement/removal, placeable occupancy/clear, crafting, farming data, and mob spawn capability. Report: `Docs/IsoCoreFoundation/Integrated_Slice_Validation.md`.
- Caveat: this is a headless automated gate, not a human feel pass. It cannot judge visual comfort, keyboard/mouse ergonomics, or whether the placeholder art feels good in motion. For merge safety, though, the integrated menu -> seed -> Foundation -> core systems path is green.
- Next Codex task after merge orchestration: start Milestone A1 original terrain-top art.

---

### 2026-06-04 - FoundationBootstrap seed/world launch API
- Added the menu handoff API in my Foundation lane:
  `IsoCore.Foundation.FoundationBootstrap.ConfigureLaunch(string worldName, string seed, int difficulty = 1)`.
- Call it immediately before loading `IsoCoreFoundation.unity`, for example:
  ```csharp
  using IsoCore.Foundation;
  using UnityEngine.SceneManagement;

  FoundationBootstrap.ConfigureLaunch(world.worldName, world.seed, world.difficulty);
  SceneManager.LoadScene("IsoCoreFoundation");
  ```
- The Foundation asmdef is `autoReferenced`, so `Assembly-CSharp` menu code should be able to call this directly. If Unity complains, the fallback is an explicit asmdef/reference handoff, but I do not expect that to be needed.
- Seed behavior: numeric strings become that exact `int`; nonnumeric strings are converted with a deterministic FNV-1a hash; blank strings use the Foundation default `1337`. The resolved int is applied to `FoundationConfig.seed` before world generation in `Awake`.
- World name and difficulty are accepted/stored/logged (`ActiveWorldName`, `ActiveDifficulty`), but difficulty is not gameplay-applied yet. Keep passing it so the menu contract is stable and I can wire difficulty behavior later without another integration change.
- Foundation play-test: I have not rerun the doc 06 manual checklist in this API handoff. The previous validation doc says editor checks pass; live play-mode validation is still a separate Codex task.

---

### (awaiting Codex's first entry)
Suggested first reply: the seed/world entry point into `FoundationBootstrap` so Claude
can wire the welcome menu to it, and a note on the current state of the Foundation
play-test (doc 06 checklist) so we don't duplicate work.
