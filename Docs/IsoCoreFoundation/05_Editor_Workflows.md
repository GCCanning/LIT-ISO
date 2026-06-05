# ISO-Core Foundation — 05: Editor Workflows

> Workflow 5 output. Repeatable Unity menu tools for the foundation, under
> **`Tools/LIT-ISO/ISO-Core Foundation/`**. They reuse the legacy GoldenPath
> compose pattern (thin `[MenuItem]` → static `Run...(bool showDialog) → string`).

## Menu actions

| Menu item | Core method | What it does |
|---|---|---|
| **Audit Project** | `FoundationReports.AuditProject` | Counts module scripts + content databases; writes `Docs/IsoCoreFoundation/Foundation_Audit.md`. |
| **Inventory ISO-CORE Reference** | `FoundationReports.InventoryReference` | Verifies the research-only `iso_core_reference_inventory.json/.csv` exist and reports size; points to `02_IsoCore_Reference_Study.md`. Never wires assets into the scene. |
| **Build Foundation Scene** | `FoundationSceneBuilder.BuildScene` | Prompts to save current scene, then creates a fresh `Assets/Scenes/IsoCoreFoundation.unity` containing one Camera + one `FoundationBootstrap`. Idempotent; no manual surgery. |
| **Generate Content Assets** | `FoundationContentBaker.Bake` | (Optional) bakes the code-built default content to `.asset` files under `Assets/IsoCoreFoundation/GeneratedContent/` for designer editing. Runtime does **not** require these. |
| **Validate Foundation** | `FoundationValidator.Validate` | Editor-side checks (no play mode) over content + cross-refs + scene/bootstrap/camera; writes `06_Validation_Report.md` with a pass/fail table + manual checklist. |
| **Run Golden Path** | `FoundationMenu.RunGoldenPath` | Chains Build → Validate and shows a combined summary dialog. |

## Design properties

- **Repeatable & non-destructive.** Build regenerates the scene from scratch but
  prompts to save first (`SaveCurrentModifiedScenesIfUserWantsTo`). No tool edits
  legacy scenes or scripts.
- **Single-object scene.** The whole runtime graph (world, player, inventory,
  systems, UI) is constructed by `FoundationBootstrap.Awake`, so the scene file is
  just `Camera + FoundationBootstrap` — there is exactly one world/player/inventory
  and nothing to hand-wire (acceptance: no duplicate managers).
- **Reports, not noise.** Audit/Validate write Markdown under
  `Docs/IsoCoreFoundation/` rather than spamming the console.
- **Isolated assembly.** All tools live in `IsoCore.Foundation.Editor`
  (references only `IsoCore.Foundation`), so they cannot accidentally touch legacy
  systems.

## Typical loop

1. `Build Foundation Scene` → opens the new scene.
2. Press **Play** → world streams in, survival loop runs.
3. `Validate Foundation` → editor checks + refreshes `06_Validation_Report.md`.
4. (Optional) `Generate Content Assets` once you want to tune content as `.asset`s.

## Input-handling prerequisite

Movement/placement use the classic `Input` API. Ensure **Project Settings ▸ Player
▸ Active Input Handling** is `Both` or `Input Manager (Old)`. (A migration to the
new Input System is listed in `07_Migration_Plan.md`.)
