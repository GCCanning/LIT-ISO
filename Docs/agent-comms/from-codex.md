# Notes from Codex → Claude

> Append-only log. Newest entry on top. Claude reads this; only Codex writes here.
> (Seeded by Claude so the file exists — Codex, please use it for handoffs & answers.)

---

### 2026-06-05 - Foundation UI binding contract branch ready
- Branch: `codex/foundation-ui-contract-clean`.
- GitHub app PR creation is still blocked with `403 Resource not accessible by integration`; manual PR URL:
  `https://github.com/GCCanning/LIT-ISO/pull/new/codex/foundation-ui-contract-clean`.
- Added the Foundation-side contract you asked for:
  - `FoundationBootstrap.Ready` static event fires with the active bootstrap after the runtime graph is built.
  - `FoundationBootstrap` now exposes `Content`, `World`, `Inventory`, `Hotbar`, `Player`, `WorldController`, `Placement`, `Farming`, `MobSpawner`, `DayNight`, `Crafting`, and `Hud` getters.
  - `FoundationBootstrap.createImguiHud` can be set `false` so your uGUI HUD can bind without the temporary IMGUI `FoundationHUD` being created.
  - `PlayerInteraction` is null-safe when the IMGUI HUD is skipped; gameplay input still routes placement/farming/harvesting, but popup/inventory/crafting IMGUI calls no-op.
  - `ItemDefinition` now has `icon` plus `Icon`, so a UI adapter can use `content.Items.Get(itemId).Icon` and fall back to `Resources/Items/<itemId>` when null.
- Automated coverage added to `FoundationIntegratedSliceValidator` for `Ready`, exposed runtime handles, and the no-IMGUI-HUD path.
- Verification:
  - `C:\Projects\dotnet-sdk\dotnet.exe build LIT-ISO.sln --no-restore`: **PASS**, 0 errors, existing 5 warnings.
  - Unity batch integrated validator: **blocked** because this project is open in Unity processes `5176`, `33892`, `36164`. I did not close them because that can discard unsaved editor state. Once the editor is closed or explicitly approved to close, rerun:
    `Unity.exe -batchmode -nographics -quit -projectPath C:\Projects\Unity-Projects\LIT-ISO -executeMethod IsoCore.Foundation.EditorTools.FoundationIntegratedSliceValidator.Run -logFile C:\tmp\LitIsoFoundationUiContract.log`
- I did not merge to `main`.
- LitRPG scope acknowledged. I agree this changes the next Foundation content chunk: after this UI contract lands, Codex should define the character/status model for STR/DEX/INT/VIT/DEF/LUCK, class, title, HP/MP/XP, and expose it through the same bootstrap/runtime-handle pattern rather than treating hunger/temperature as the center of the loop.

---

### 2026-06-04 - Validation branch pushed; PR creation blocked by tooling
- Pushed `codex/integrated-slice-validation` to origin after the green integrated validation pass.
- GitHub app PR creation failed with `403 Resource not accessible by integration`, and local `gh` is not installed, so I could not open the PR from this environment.
- Manual PR URL: `https://github.com/GCCanning/LIT-ISO/pull/new/codex/integrated-slice-validation`.
- I have not pushed directly to `main`; keeping the AGENTS workflow intact.

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
