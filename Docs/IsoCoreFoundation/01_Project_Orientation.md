# ISO-CORE Foundation — 01: Project Orientation

> Workflow 1/2 output. Audit of the existing **LIT-ISO** codebase to orient the
> ISO-CORE foundation rebuild (isometric survival / crafting / building loop).
> Every claim below is cited to a concrete file path. Read this before
> Workflow 3 (Foundation Architecture).

---

## 1. Overview — What LIT-ISO Is Today

LIT-ISO is a **Unity (Unity 6.3-class project, URP/2D)** isometric LitRPG game with
a working procedural-world prototype. The current state:

- **Isometric 2D**, tilemap-based. Grid is `IsometricZAsY`; rendering contract is
  `transparencySortAxis == (0,1,-0.26)`, TilemapRenderer `Individual` + `TopRight`
  sort (enforced in `Assets/Scripts/SceneValidator.cs` and
  `Assets/Scripts/Editor/GoldenPathTools.cs`).
- **Chunked, infinite procedural world** streamed at runtime by
  `Assets/Scripts/IsoWorldChunkManager.cs` (chunkSize 32). Climate → biome →
  height pipeline runs on a Burst `IJobParallelFor`.
- **Height-aware**: discrete height layers `0..7`
  (`HeightLayerCount = 8`, `MaxSupportedHeight = 7`). Each height has its own
  physics layer `Height_0..7` (= Unity layer `10 + height`) with its own collider
  tilemap. Player sorting layer is `10 + height`.
- **Biome-driven**: `Assets/Scripts/IsoBiomeDefinition.cs` ScriptableObjects drive
  per-height tiles, decoration clusters, and directional cliff colliders;
  climate (temperature / moisture / continental height) selects + blends biomes.
- **Movement is explicit, not physics-pushed**: the player resolves all terrain
  collision/height through the chunk manager's query API (the "world movement
  contract"), with a kinematic `Rigidbody2D` (`gravityScale = 0`) and a
  trigger-only foot collider.
- A surrounding **LitRPG meta stack** exists (classes, XP, titles, mana, quests,
  dungeons, weather, combat, towns, guilds, economy) — mostly singleton
  MonoBehaviours + ScriptableObject definitions, loosely coupled by C# events.

---

## 2. Current Major Systems

Status legend: **REUSE** (take as-is or near), **REUSE+REFACTOR**, **RISKY**
(conflicts / duplication), **REPLACE**, **DEFER**.

### World / Terrain

| File | Responsibility | Status |
|------|----------------|--------|
| `Assets/Scripts/IsoWorldChunkManager.cs` (2446 LoC) | Chunk streaming, climate→biome, height/terrain gen, per-height collision, spawning, and the authoritative movement-query API (`EvaluateFootprintMove`, `GetMaxFootprintHeight`, `GetHeightAtCell`, `WorldToGroundCell`, `IsFootprintBlockedByTerrain`). | REUSE+REFACTOR / RISKY |
| `Assets/Scripts/IsoBiomeDefinition.cs` | Per-biome climate ranges, per-height tiles + variants, clustered decoration, per-direction cliff colliders. The wired biome model. | REUSE |
| `Assets/Scripts/BiomeAssetRuleSet.cs` | Weighted tile/prefab/resource-node placement with height/transition/edge gating; models `blocksMovement` + resource nodes. **Not referenced by the chunk manager (dead).** | REPLACE / DEFER |
| `Assets/Scripts/World/WorldManager.cs` | DontDestroyOnLoad singleton holding `WorldName/Seed(string)/Difficulty`. | REUSE |
| `Assets/Scripts/World/StarterZoneGenerator.cs` | Fixed-layout starter area painting its **own** tilemaps + spawning starter content. Creates **no colliders / Height_ layers**. | RISKY |

### Player / Movement

| File | Responsibility | Status |
|------|----------------|--------|
| `Assets/Scripts/IsoPlayerController.cs` (1170 LoC) | Canonical iso player: kinematic movement, discrete height tracking, jump arcs, sprite anim/sorting, tile selection, audio — resolves all terrain collision via the world contract. | REUSE+REFACTOR |
| `Assets/Scripts/IsometricPlayerMovementController.cs` (43 LoC) | Naive `Input.GetAxis` + `MovePosition`, no height/collision awareness. Obsolete prototype. | REPLACE |
| `Assets/Scripts/Phase2PlayerController.cs` (113 LoC) | Adapter registering the player with TrialWeek/ActionTracker meta. Not a controller. | DEFER |
| `Assets/Scripts/AdventurerPlayerSetup.cs` | Player setup/wiring helper. | DEFER |

### Items / Inventory / Crafting

| File | Responsibility | Status |
|------|----------------|--------|
| `Assets/Scripts/Gameplay/ItemDefinition.cs` | Item type SO (`itemId`, category, `maxStack`). Clean, data-driven. | REUSE |
| `Assets/Scripts/Gameplay/ResourceNodeDefinition.cs` | Harvestable node SO + `ItemDrop` weighted drop table, respawn/spacing. | REUSE |
| `Assets/Scripts/Gameplay/ResourceNode.cs` | World harvestable instance (`Harvest`, range, respawn). Collider is **trigger-only** (no solid occupancy). | REUSE |
| `Assets/Scripts/Gameplay/PlayerInventory.cs` | Stack-count inventory singleton + `OnStackChanged`. **No persistence**; mixed `.Instance` vs `FindFirstObjectByType` access. | REUSE (+fix) / RISKY |
| `Assets/Scripts/Gameplay/IsoInteractionController.cs` | Right-click / E harvest bridge via `OverlapCircleAll`. Shares right-click with tile-select. | REUSE |
| `Assets/Scripts/Crafting/RecipeDefinition.cs` | Recipe SO (ingredients, station, profession, XP, category). MMO-scoped but `Anywhere`/`Workbench` path usable. | REUSE (heavyweight) |
| `Assets/Scripts/Crafting/CraftingSystem.cs` | `Craft()` consume→produce path + profession XP/leveling; couples `EthraClone.TrialWeek`, `PlayerHealth.Instance`. | REUSE (craft) / DEFER (professions) |
| `Assets/Scripts/UI/HotbarUI.cs` | 7-slot discovery-order bar (legacy `UI.Text`); silently drops items past slot 7. | REPLACE |

### Editor / Build Tooling

| File | Responsibility | Status |
|------|----------------|--------|
| `Assets/Scripts/Editor/GoldenPathTools.cs` | One-click orchestrator (`RunCurrentGoldenPath` / `ValidateCurrentSceneInternal`); delegates + AppendStatus reporting. | REUSE (template) |
| `Assets/Scripts/Editor/QuickPlayTestSetup.cs` | Additive, idempotent scene bootstrapper (`RunSetup(bool)`) for ~12 systems incl. World/Player/Inventory. | REUSE (extend) / RISKY |
| `Assets/Scripts/SceneValidator.cs` | Runtime auto-heal + validates iso render contract and the world-height layer contract (`expectedLayer = 10 + height`). | REUSE (pattern) |
| `Assets/Scripts/Editor/IsoWorldSetup.cs` | From-scratch **destructive** scene builder + SO asset creation; defines canonical asset-path roots. | REUSE (roots) / RISKY |
| `Assets/Scripts/Editor/GameBuilder.cs` | One-click Windows standalone build (`BuildGame`). Hard-codes `InfinitePlainsPrototype.unity`. | DEFER |
| `Assets/Editor/AssetForge/AssetForgeAutomation.cs` | Manifest → sprite slice / animator / prefab pipeline; PPU 128/64, point filter, 8-dir order. Regex manifest parser (brittle). | REUSE (conventions) |

### Meta Systems (quests / dungeons / weather / combat / towns / guilds)

| File | Responsibility | Status |
|------|----------------|--------|
| `Assets/Scripts/Quests/QuestDefinition.cs`, `QuestManager.cs` | Quest data SO + Notify-driven, fully decoupled tracker. | REUSE / DEFER |
| `Assets/Scripts/Quests/TutorialSequence.cs` | Scripted onboarding (Notify* pattern). | DEFER |
| `Assets/Scripts/World/Dungeons/DungeonDefinition.cs`, `DungeonEntrance.cs`, `DungeonManager.cs` | Dungeon metadata + entrance trigger + clear/reward bookkeeping. Actual loading is an unimplemented TODO. Entrance/Manager bind `PlayerHealth` + the whole progression stack. | DEFER / RISKY |
| `Assets/Scripts/World/Weather/WeatherDefinition.cs`, `WeatherManager.cs` | Biome-driven weather VFX/audio + outdoor DoT. Decoupled (Camera/Particle/Audio only). | DEFER / REUSE |
| `Assets/Scripts/Combat/SpellDefinition.cs`, `StatusEffectDefinition.cs` | Spell/status data SOs. | REUSE (data) |
| `Assets/Scripts/Combat/SpellCaster.cs`, `SpellProjectile.cs`, `SpellAoE.cs`, `StatusEffectHandler.cs` | Casting + delivery; damage hard-wired to concrete `SlimeEnemyController`, no `IDamageable`, no `LayerMask` filtering. | REPLACE / RISKY |
| `Assets/Scripts/Towns/SettlementDefinition.cs`, `BuildingInstance.cs`, `TownManager.cs` | Building/placement + material-cost economy. **Instantiates at a point with no grid snap / footprint / collision check.** | DEFER / RISKY |
| `Assets/Scripts/Guilds/GuildConfig.cs`, `GuildManager.cs` | Social/meta layer; loose event coupling to Quests only. | DEFER |
| `Assets/Scripts/Economy/CurrencySystem.cs` | 5-tier currency, self-contained, has `LoadState`. | REUSE |
| `Assets/Scripts/Economy/MarketManager.cs`, `AuctionHouse.cs` | P2P market / timed auctions; AuctionHouse binds TownManager + TrialWeek time + hardcoded ids. | DEFER / RISKY |

### Player Progression Cluster (LitRPG)

| File | Responsibility | Status |
|------|----------------|--------|
| `Assets/Scripts/Player/PlayerStats.cs`, `PlayerMana.cs`, `ClassSystem.cs`, `XPSystem.cs`, `TitleSystem.cs` + `Assets/Scripts/Gameplay/PlayerHealth.cs` | `static Instance` singletons, densely cross-wired, backed by SO definitions. LitRPG progression, not survival. | REPLACE / RISKY |

---

## 3. Reusable Systems (and Why)

These are the assets ISO-CORE should build **on top of** or copy patterns from.

- **Editor golden-path / validator pattern** — `GoldenPathTools.cs`,
  `QuickPlayTestSetup.cs`, `SceneValidator.cs`. The convention is gold: every
  editor entry point is a thin `[MenuItem]` wrapper over a
  `public static string Run...(bool showDialog)` core that returns a log string,
  so tools **compose** (Golden Path chains setup + LPC wiring + validation).
  Reuse verbatim. Menu root `Tools/LIT-ISO/<Category>/<Action>` with explicit
  priorities; the 40–60 priority band is free for a new
  `Tools/LIT-ISO/ISO-Core Foundation/...` category. Validation style is
  `FindFirstObjectByType<T>` presence + reference-wired + contract checks
  (`cellLayout == IsometricZAsY`, transparency axis, height-layer contract).
- **Isometric grid + render contract** — the `IsometricZAsY` grid, sort axis
  `(0,1,-0.26)`, and `Height_N` / sorting-layer `10 + height` model
  (`SceneValidator.ValidateWorldHeightContract`). Load-bearing; ISO-CORE must
  honor it, not reinvent it.
- **Movement contract API** — `IsoWorldChunkManager.EvaluateFootprintMove` /
  `FootprintMoveEvaluation` / `GetMaxFootprintHeight` consumed by
  `IsoPlayerController` and `SlimeEnemyController`. A clean, explicit
  "ask the world, don't push physics" collision model — exactly the right
  foundation for survival/building, *once the sampling duplication is unified*.
- **Inventory / item / crafting data spine** — `ItemDefinition`,
  `ResourceNodeDefinition` + `ItemDrop`, `RecipeDefinition` + `Ingredient`,
  plus the runtime path `PlayerInventory` → `ResourceNode.Harvest` →
  `CraftingSystem.Craft`. Clean, event-driven, data-first. This is a directly
  reusable gather→craft loop (strip the profession-XP/market/auction weight).
- **Currency** — `CurrencySystem.cs`: self-contained, no item coupling, already
  has a `LoadState` persistence hook.
- **Asset import conventions** — `AssetForgeAutomation.cs`: PPU 128 (characters) /
  64 (tiles), point filter, uncompressed, readable, 8-direction order
  `S,SE,E,NE,N,NW,W,SW`. Adopt for ISO-CORE art import.
- **Canonical asset-path roots** — from `IsoWorldSetup.cs`:
  `Assets/Tilemaps/Isometric/{RuleTiles,Colliders,Tiles/BiomeDecorations}`,
  `Assets/World/{Biomes,Lighting,Items}`. Reuse these layouts.

---

## 4. Risky / Conflicting Systems

- **Terrain collision — duplicated sampling math (HIGHEST RISK).** In
  `IsoWorldChunkManager.cs`, the Burst `ChunkDataJob` (~lines 293–495) has
  `*Static` versions of every sampler (`SampleClimateStatic`,
  `SelectBiomeIndex`, `SampleBiomeBlendStatic`, `CalculateTerrainHeightStatic`,
  `SmoothHillHeightStatic`, `GetContinentalHeightWeightStatic`) **hand-mirrored**
  by C# instance versions (~lines 1907–2262) plus a duplicated starter-zone
  override in `SampleCell`. A code comment explicitly warns it "MUST mirror the
  Burst job exactly so player collision/height queries agree with painted tiles."
  Any divergence makes visible tiles disagree with collision/height queries.
- **Two coexisting collision notions.** Movement is gated by both discrete
  height-delta (`heightDelta > maxStepHeight` → cliff) **and** layer-collider
  overlap (`IsFootprintBlockedByTerrain` → `Physics2D.OverlapCircleNonAlloc` on
  `Height_N`). The blocker refactor is half-finished (`ignoreCollider` overloads
  threaded through; legacy `CanMoveBetweenPositions(maxJumpHeight)` is dead-ish).
- **No SampleCell cache** — Perlin recomputed on every height/movement query
  (called in a scan loop in `WorldToGroundCell`, 5× per `GetMaxFootprintHeight`,
  up to 3× per frame in player resolve + binary search). Correctness + perf risk.
- **Starter-zone duplication** — `StarterZoneGenerator.cs` paints its own tiles
  with **no colliders/Height_ layers**, competing with the chunk manager's
  built-in circular starter override → its terrain has no collision under the
  movement contract. Pick one path.
- **World/player/inventory duplication** —
  - Seed: `WorldManager.Seed` (string) vs `IsoWorldChunkManager.seed` (int).
  - Three player movement scripts (`IsoPlayerController`,
    `IsometricPlayerMovementController`, `Phase2PlayerController` adapter).
  - `PlayerInventory` is a **non-persistent** singleton while neighbours
    (CraftingSystem/CurrencySystem/MarketManager) are `DontDestroyOnLoad`; mixed
    `.Instance` vs `FindFirstObjectByType<PlayerInventory>()` access
    (Auction/Market) signals an unresolved single-source-of-truth problem.
  - Two scene bootstrappers (`QuickPlayTestSetup` additive vs `IsoWorldSetup`
    destructive) construct Grid+ChunkManager+Player with overlapping hard-coded
    constants — an ISO-CORE setup would be a *third* builder unless it delegates.
- **Combat physics interference.** Combat uses unfiltered `Physics2D.RaycastAll`/
  `OverlapCircleAll` with **no LayerMask**. If ISO-CORE adds solid-block/terrain
  colliders on the same 2D layers, those queries will hit terrain and
  `GetComponentInParent<SlimeEnemyController>()` silently no-ops.
- **Town building placement** — `TownManager.PlaceBuilding` `Instantiate`s at a
  single `worldPosition` with no grid snap / footprint reservation / collision
  check; directly overlaps an ISO-CORE building system. Highest building-dup risk.

---

## 5. Systems to Defer (for now)

Defer entirely until the ISO-CORE survival/build loop exists. None of these touch
world/terrain/grid generation, so **none block** the rebuild:

- **Quests** — `Quests/QuestManager.cs` (+ `QuestDefinition`, `TutorialSequence`).
- **Dungeons** — `World/Dungeons/*` (loading is an unimplemented TODO).
- **Weather** — `World/Weather/*` (cosmetic/event layer, biome-string driven).
- **Combat** — `Combat/*` (needs `IDamageable` + LayerMask refactor first).
- **Towns / Building economy** — `Towns/*`.
- **Guilds** — `Guilds/*`.
- **Economy beyond currency** — `Economy/MarketManager.cs`, `AuctionHouse.cs`.
- **LitRPG progression** — classes/XP/titles/mana professions.

Note: these assume external singletons exist (`PlayerHealth`, `PlayerStats`,
`PlayerMana`, `PlayerInventory`, `XPSystem`, `CurrencySystem`, `TitleSystem`,
`SystemNotifier`, `ActionTracker`, `WorldFloatingText`). Dungeons, Towns, and
Combat will **not compile** without these names — preserve the names or provide
shims if/when they are reintroduced.

---

## 6. Systems Likely to Replace

- `Assets/Scripts/IsometricPlayerMovementController.cs` — obsolete prototype
  controller, superseded by `IsoPlayerController`.
- `Assets/Scripts/UI/HotbarUI.cs` — discovery-order, fixed-7, legacy `UI.Text`;
  ISO-CORE needs an index-addressable, selectable survival hotbar.
- `Assets/Scripts/BiomeAssetRuleSet.cs` — better survival/placement data model
  (`blocksMovement`, resource nodes) but **dead/unwired**; reconcile with
  `IsoBiomeDefinition` rather than running both.
- Combat delivery (`SpellProjectile`/`SpellAoE`/`StatusEffectHandler`) — concrete
  `SlimeEnemyController` coupling; will not generalize without an `IDamageable`
  interface.
- LitRPG progression singletons (`Player/*`) — replace with a survival-oriented
  stat/needs model if/when needed.

---

## 7. Current Technical Debt Relevant to ISO-CORE Gameplay

1. **Terrain-collision separation.** The single highest-risk item is the
   **dual-path sampling math** (Burst `*Static` vs C# instance) in
   `IsoWorldChunkManager.cs`. ISO-CORE must collapse these into **one shared
   sampler** consumed by both the job and the query API before extending —
   otherwise placed blocks / new terrain will desync visible tiles from
   collision/height.
2. **Solid-block behavior.** There is **no general solid-voxel/occupancy concept**.
   Solidity = terrain cliff faces (`cliffColliderTile` / `CollisionBlock.asset`,
   per-direction `colliderCliff{N/S/E/W}`) plus decoration prefabs flagged
   `blocksMovement` *in the unwired `BiomeAssetRuleSet`*. The player foot collider
   is **always a trigger** (forced trigger during jumps); terrain blocking is
   enforced by the world query, not physics. **Any future placed/built solid
   block MUST register into the matching `Height_h` layer + collider tilemap** —
   spawning a bare collider will not block the player. ISO-CORE needs a new
   per-cell occupancy layer rather than overloading `cliffColliderTile`.
3. **Render / collision / nav / interaction data separation.**
   - Render vs collision: **cleanly separated** (independent tilemaps; collider
     maps render-disabled). Keep.
   - Height + interaction (biome/transition/edge): **NOT separated** from render —
     recomputed on demand from noise via `SampleCell`, **uncached**. ISO-CORE
     should add a cached per-cell data layer (height, biome, occupancy, blocked).
   - Collision is **height-layer-keyed** (8 layers): solidity depends on the
     querying entity passing the correct `collisionHeight`.
4. **Movement contract.** `IsoPlayerController` resolves *all* terrain movement
   through `world.EvaluateFootprintMove` (6-iteration binary-search slide +
   optional axis wall-slide); if `world` is null, collision **silently disables**
   (player walks through everything). Vertical traversal is gated by
   `maxWalkStepHeight = 0` (no auto step-up; climbing needs a jump) — survival
   stairs/ramps require changing this contract, which is **duplicated** in
   `QuickPlayTestSetup` (`SetupPlayer` + `EnsurePlayerComponents`, incl. a
   duplicated `spriteHeightOffsetPerLevel` line — minor bug).
5. **Persistence gap.** No save/load anywhere in the item/inventory/crafting
   layer (only `CurrencySystem.LoadState` exists). The `stacks` (itemId→count)
   dict makes a serialize path easy but it must be authored.
6. **Namespace coupling.** Crafting + economy pull in `EthraClone.TrialWeek`;
   isolate the reusable item/recipe/inventory core from that namespace first.

---

## 8. Handoff Notes for Workflow 3 (Foundation Architecture)

**Reuse as-is (or near):**
- The **editor compose pattern** (`Run...(bool)` cores + thin `[MenuItem]`) and
  the **validator pattern** (`SceneValidator` / `GoldenPathTools` AppendStatus).
  Create `Tools/LIT-ISO/ISO-Core Foundation/Run` + `Validate` that **delegate
  into `QuickPlayTestSetup.RunSetup(false)`** and add only ISO-CORE steps —
  do **not** write a third world/player/inventory builder.
- The **isometric render + height-layer contract** (`IsometricZAsY`, sort axis
  `(0,1,-0.26)`, `Height_N` = layer `10 + height`). Honor it verbatim.
- The **item/recipe/resource-node SOs** + **`PlayerInventory`/`ResourceNode`/
  `CraftingSystem.Craft`** gather→craft spine.
- **`IsoBiomeDefinition`** as the biome model; **`CurrencySystem`** if currency
  is needed; **AssetForge import conventions** + **asset-path roots**.

**Build fresh (or refactor before reuse):**
- **Unify the sampler.** Extract one shared, deterministic, *cached* terrain
  sampler used by both the Burst job and the query API. This is prerequisite #1
  before any building/placement work. (Refactor inside `IsoWorldChunkManager` or
  lift into a standalone `IsoTerrainSampler`.)
- **Decompose the two god-classes** (`IsoWorldChunkManager` 2446 LoC,
  `IsoPlayerController` 1170 LoC): separate world-gen / collision-query /
  rendering / spawning, and movement / rendering / audio / tile-select.
- **Per-cell occupancy layer** for placed/built solid blocks, wired into the
  `Height_h` collider tilemaps (new work — not extendable from cliff/node code).
- **Inventory single-source-of-truth + persistence:** pick `.Instance` vs
  `FindFirstObjectByType`, decide DontDestroyOnLoad, add a `stacks` save schema.
- **Index-addressable hotbar** to replace `HotbarUI`.
- **Movement contract decision:** confirm `maxWalkStepHeight`/ramp policy; fix the
  null-world silent-disable; de-duplicate the player-setup tunables.

**Decisions to make before Workflow 3 starts:**
- One **starter-zone** path (chunk-manager override vs `StarterZoneGenerator`).
- One **seed** type (string vs int) across `WorldManager` / chunk manager.
- One **biome/placement** data model (`IsoBiomeDefinition` vs `BiomeAssetRuleSet`).
- Target **scene** (`SampleScene.unity` golden vs `InfinitePlainsPrototype.unity`
  in `GameBuilder`) before adding build steps.
- Whether the **LitRPG meta stack** is kept (affects whether shims for
  `PlayerHealth`/`PlayerStats`/etc. are required).
