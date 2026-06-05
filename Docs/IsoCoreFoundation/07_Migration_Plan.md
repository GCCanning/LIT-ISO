# ISO-Core Foundation — 07: Migration Plan

> Workflow 7 output. How the isolated experimental foundation becomes the canonical
> base for our own RPG/survival game, and how legacy LIT-ISO systems map onto it.
> Grounded in [01_Project_Orientation](01_Project_Orientation.md).

## 1. Canonical vs legacy — current state

The foundation is an **isolated assembly** (`IsoCore.Foundation`, no reference to
`Assembly-CSharp`). The legacy project is **untouched and still compiles**. The new
scene `IsoCoreFoundation.unity` contains **only** foundation systems, so there is no
duplicate world/player/inventory at runtime (acceptance #4/#5, constraint #5).

The two stacks coexist safely because they share no types and live in different
scenes. Migration is therefore *additive and reversible*, not a rip-out.

## 2. System disposition

Legend: **REUSED** (adopted by foundation), **REPLACED** (foundation supersedes),
**DEFERRED** (kept, re-integrate later), **RETIRED** (delete once foundation is canonical).

| Legacy system | Disposition | Notes |
|---|---|---|
| Editor GoldenPath / SceneValidator compose pattern | **REUSED** | Pattern reused in `FoundationMenu`/`FoundationValidator`. |
| AssetForge import conventions, asset-path roots | **REUSED (later)** | Adopt when real pixel art replaces placeholders. |
| `IsoBiomeDefinition` (legacy) | **REPLACED** | by foundation `BiomeDefinition` (+ `BlockGroupDefinition`). |
| `BiomeAssetRuleSet` (dead/unwired) | **RETIRED** | superseded by biome node/mob spawn rules. |
| `IsoWorldChunkManager` (2446 LoC, dual sampler) | **REPLACED** | by `IsoWorld` + single `IsoTerrainSampler` (no Burst mirror). |
| `IsoPlayerController` (1170 LoC) | **REPLACED** | by `IsoFoundationPlayer` (+ `PlayerInteraction`). |
| `IsometricPlayerMovementController`, `Phase2PlayerController` | **RETIRED** | obsolete prototypes. |
| `Gameplay/ItemDefinition`, `ResourceNode(Definition)`, `PlayerInventory`, `CraftingSystem` | **REPLACED** | re-authored clean inside the foundation (stripped of TrialWeek/profession coupling). Legacy versions deferred until retired. |
| `UI/HotbarUI` | **REPLACED** | by index-addressable `Hotbar` + `FoundationHUD`. |
| Quests / Dungeons / Weather / Combat / Towns / Guilds / Market / LitRPG progression | **DEFERRED** | re-integrate onto the foundation only after the core loop is solid (constraint: don't overbuild). |
| `CurrencySystem` | **DEFERRED (clean)** | self-contained with `LoadState`; port when economy is needed. |

## 3. Avoiding duplicate player/world/inventory

Rules to keep exactly one of each as the project converges:

1. **One scene at a time is canonical.** Legacy scenes (`SampleScene`,
   `InfinitePlainsPrototype`) and `IsoCoreFoundation.unity` are never loaded
   additively together.
2. **One bootstrap.** `FoundationBootstrap` is the only constructor of world/
   player/inventory. Never add legacy `QuickPlayTestSetup`/`IsoWorldSetup` to the
   foundation scene.
3. **One inventory instance**, passed explicitly (no `.Instance`/`Find` mixing).
4. When legacy systems are re-integrated, they must consume the foundation's
   `IsoWorld`/`Inventory` APIs — not spin up their own managers.

## 4. Path to canonical

**Phase A — Validate the slice (now).** Run `Build Foundation Scene` → Play →
`Validate Foundation`. Confirm the manual checklist in `06`. Fix any gameplay
issues found in real play.

**Phase B — Promote the data/render fidelity.**
- Unify the sampler choice: keep `IsoTerrainSampler` as the single source of truth;
  if the legacy chunk manager is ever reused, collapse its Burst `*Static` + C#
  duplicate into this one sampler first (Orientation §7.1).
- Swap placeholder sprites for AssetForge-imported pixel art (2:1, bottom-center,
  point filter). Optionally move the ground renderer onto a `Tilemap` honoring the
  legacy `IsometricZAsY` + `(0,1,-0.26)` contract for batching (recorded divergence
  in `03 §1`).
- Add persistence: implement `FoundationSaveData` over `IsoWorld` modified-cell
  deltas + `Inventory` slots (seams already scaffolded; ISO-CORE per-world-space
  model, Study §5.9).

**Phase C — Re-integrate meta systems onto the foundation**, one at a time, each
consuming foundation APIs: tools/tiers → harvesting; survival needs
(energy/temperature) → player; then weather → world; then quests/combat/towns.
Provide shims only for the singletons those legacy systems expect (`PlayerHealth`,
`PlayerStats`, …) — or re-author them survival-first (Orientation §5 note).

**Phase D — Retire legacy.** Once the foundation covers a legacy system's role and
play-tests clean, delete the legacy script and its scene wiring (REPLACED→removed,
RETIRED items first). Keep the research-only ISO-CORE inventory out of production
permanently.

## 5. Next steps after foundation validation

1. Play-test the slice; capture issues against `06`'s checklist.
2. Decide the survival-pressure scope (energy/temperature in vs out) before content breadth.
3. Replace placeholder art via AssetForge; verify sorting/no clipping.
4. Add save/load.
5. Begin Phase C re-integration in priority order (tools → needs → weather → quests).
