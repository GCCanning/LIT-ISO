# LIT-ISO — Handoff for Next Session

**Project:** LIT-ISO — Unity 6.3 isometric (IsometricZAsY) game, pivoting from a
LitRPG prototype to a **cozy survival / crafting / building** loop.
**Date of handoff:** 2026-06.

---

## ⚠️ READ FIRST: there are TWO parallel tracks in this project

A second AI ("Codex") built a clean-room rebuild while the welcome-screen / biome
work was happening. Both currently compile and coexist (different scenes, different
assemblies). **Before doing anything, the human must choose which track is canonical.**

### Track A — Legacy `Assembly-CSharp` (the older prototype + my recent work)
- **Scenes:** `Assets/Scenes/MenuScene.unity` → `Assets/Scenes/SampleScene.unity`,
  plus `InfinitePlainsPrototype.unity`.
- **World:** `Assets/Scripts/IsoWorldChunkManager.cs` (2446 LoC, Burst chunk streaming)
  + `Assets/Scripts/IsoBiomeDefinition.cs`.
- **Player:** `Assets/Scripts/IsoPlayerController.cs` (1170 LoC).
- **Editor build:** `Tools/LIT-ISO/Setup/Full Golden Path Setup`
  (`QuickPlayTestSetup.cs` + `IsoWorldSetup.cs`).
- **Recent work in this track (mine):**
  - Welcome/main-menu system: `WelcomeScreenManager.cs` (Minecraft-style New/Load/
    Options, campfire background auto-loaded from `Resources/UI/CampfireMenu.png`),
    `WorldManager.cs` (persistent seed/difficulty + JSON world saves in
    `persistentDataPath/LitIsoWorlds`), `GameStartupManager.cs` (ensures Camera +
    EventSystem at runtime). `MenuScene.unity` is wired (EventSystem + background).
  - Added a **Forest** biome (`BiomeKind.Forest = 7`); split Plains (dry) / Forest
    (wet) by moisture; restricted the **starter world to Plains+Forest only** (the
    "garish tiles" were Desert/Temple/Frozen biome tiles bleeding in).
  - Worked around broken raised-grass slices (`plains-sliced_57–62` were fine on
    inspection; the real garish source was the **multi-biome set**, now fixed to
    Plains+Forest in both `QuickPlayTestSetup` and `IsoWorldSetup`).
  - Smoothed spawn terrain (bigger flat starter zone, lower hill frequency, gentler
    lowlands) and enabled wall-slide to stop the player catching on cliff edges.
- **Status / open items (Track A):** NOT re-verified in play after the last edits.
  To test: run `Tools/LIT-ISO/Setup/Full Golden Path Setup`, then open `MenuScene`
  → Play → New Game. Expect green Plains+Forest, no garish tiles, walkable spawn.

### Track B — `IsoCore.Foundation` (Codex's clean-room rebuild — INTENDED CANONICAL)
- **Assembly:** `IsoCore.Foundation` (+ `.Editor`), isolated — no reference to
  `Assembly-CSharp`. Code under `Assets/Scripts/IsoCoreFoundation/`
  (Blocks, Building, Crafting, Farming, Harvesting, Inventory, Items, Mobs, Player,
  World, Core, Editor, UI).
- **Scene:** `Assets/Scenes/IsoCoreFoundation.unity`, constructed by
  `FoundationBootstrap` (the ONLY constructor of world/player/inventory).
- **Editor menu:** `Tools/LIT-ISO/ISO-Core Foundation/` → `Build Foundation Scene`,
  `Generate Content Assets`, `Validate Foundation`, `Run Golden Path`.
- **State:** Editor validation **21/21 PASS** (12 blocks, 3 block-groups, 5 biomes,
  21 items, 4 placeables, 14 recipes, 4 resource nodes, 3 mobs). Runtime-hardened
  (9 fixes: placement soft-lock, chunk centring on raised terrain, mob spawn
  geometry, swept collision, harvest/craft overflow, camera clear flags).
- **Art:** uses **generated placeholder cubes/boxes** — first-read gameplay still
  looks like coloured blocks. Replacing this is Milestone A.
- **Docs:** `Docs/IsoCoreFoundation/01..10` — read `01` (orientation), `07`
  (migration plan), `10` (clone backlog / art batches). Validation checklist in `06`.

---

## The decision the human must make

**Which track is canonical going forward?** Codex's migration plan (doc 07) says the
Foundation wins and legacy is REPLACED/RETIRED. The Foundation also fixes the *root*
problems the legacy track kept fighting (one unified terrain sampler instead of the
duplicated Burst/C# math; a real per-cell occupancy layer for solid/placed blocks;
hardened, swept collision). 

**Recommendation:** adopt **Track B (Foundation) as canonical**, and salvage the two
genuinely-good Track A assets:
1. The **welcome menu / world-select** (`WelcomeScreenManager` + `WorldManager`
   seed/difficulty + JSON saves) — repoint "Play" from `SampleScene` to
   `IsoCoreFoundation.unity` and feed the seed into `FoundationBootstrap`.
2. The **Plains/Forest art direction + the campfire menu art**.
Then let the legacy `IsoWorldChunkManager`/`IsoPlayerController`/`IsoBiomeDefinition`
world be retired per migration-plan Phase D once the Foundation reaches parity.

(If the human instead wants to ship the legacy world, ignore Track B and just
finish play-testing Track A — but that re-introduces the dual-sampler / no-occupancy
debt the Foundation was built to escape.)

---

## Load-bearing invariants BOTH tracks must honor (do not "fix" these)

- Grid: `IsometricZAsY`, `cellSize = (1, 0.5, 1)`.
- Render: `GraphicsSettings.transparencySortMode = CustomAxis`,
  `transparencySortAxis = (0, 1, -0.26)`  (Unity's documented Z-as-Y value;
  `-0.26 = cellSize.y * -0.5 - 0.01`).
- `TilemapRenderer.mode = Individual` (Chunk mode hides characters behind tiles).
- Height model: layers `Height_0..7` = Unity layer `10 + height`; player sorting
  layer `10 + height`. Movement is resolved by an explicit world-query API
  ("ask the world, don't push physics"); foot collider is trigger-only.
- `maxWalkStepHeight = 0` → you must **jump** to climb; confirm whether survival
  wants ramps/stairs before changing this.

---

## Recommended next steps (Foundation-canonical path)

1. **Validate the slice.** Open `IsoCoreFoundation.unity` → `Tools/LIT-ISO/ISO-Core
   Foundation/Build Foundation Scene` → Play → `Validate Foundation`. Walk the
   manual play-mode checklist in `Docs/IsoCoreFoundation/06_Validation_Report.md`
   (move, harvest, craft, place, farm, mobs, no soft-lock). Capture any issues.
2. **Decide survival scope** (energy/food/temperature in or out) before content
   breadth — see doc 07 §5.2.
3. **Milestone A — replace placeholder cubes with original pixel art** (doc 10).
   Author in batches via the asset pipeline (AssetForge / Sprixen), starting with
   **A1 terrain tops** then **A2 terrain blocks** (highest visual impact), using the
   prompt template in doc 10. Target: 2:1 iso, PPU 16, point filter, bottom-center
   pivot, no baked shadow. Wire sprites into `FoundationContent` / the renderer.
4. **Save/load** — implement `FoundationSaveData` (modified cells, placed objects,
   inventory, crops, clock, mobs) over `IsoWorld` deltas + `Inventory` slots.
5. **Port the welcome menu** to load `IsoCoreFoundation.unity` and pass
   `WorldManager.Seed/Difficulty` into `FoundationBootstrap`; reuse the JSON
   world-select. Keep the campfire background.
6. **Phase C re-integration**, one system at a time onto foundation APIs:
   tools/durability → harvesting; survival needs → player; weather → world; then
   quests/combat/towns. Provide shims for legacy singletons (`PlayerHealth`,
   `PlayerStats`, …) only as needed.
7. **Phase D — retire legacy** scripts/scenes once the foundation play-tests clean.

### Hard rules while both tracks exist
- Never load a legacy scene **and** `IsoCoreFoundation.unity` additively together.
- Never add `QuickPlayTestSetup` / `IsoWorldSetup` to the Foundation scene
  (`FoundationBootstrap` is the only bootstrap).
- One inventory instance, passed explicitly (no mixed `.Instance` / `Find`).

---

## Key files index

| Purpose | Path |
|---|---|
| Foundation orientation / audit | `Docs/IsoCoreFoundation/01_Project_Orientation.md` |
| Foundation migration plan | `Docs/IsoCoreFoundation/07_Migration_Plan.md` |
| Foundation art/clone backlog | `Docs/IsoCoreFoundation/10_CleanRoom_Clone_Backlog.md` |
| Foundation validation + play checklist | `Docs/IsoCoreFoundation/06_Validation_Report.md` |
| Foundation bootstrap | `Assets/Scripts/IsoCoreFoundation/Core/FoundationBootstrap.cs` |
| Foundation editor menu | `Assets/Scripts/IsoCoreFoundation/Editor/FoundationMenu.cs` |
| Legacy welcome menu | `Assets/Scripts/UI/WelcomeScreenManager.cs` |
| Legacy world config (seed/difficulty/saves) | `Assets/Scripts/World/WorldManager.cs` |
| Legacy biome definitions builder | `Assets/Scripts/Editor/IsoWorldSetup.cs` |
| Legacy world/collision | `Assets/Scripts/IsoWorldChunkManager.cs` |
