# ISO-CORE Foundation — 03: Foundation Architecture

> Workflow 3 output. Designs the new ISO-CORE-style foundation built **inside**
> LIT-ISO as a fully isolated module. Grounded in
> [01_Project_Orientation](01_Project_Orientation.md) and
> [02_IsoCore_Reference_Study](02_IsoCore_Reference_Study.md).

---

## 0. Core decision — full isolation

The foundation is built as a **self-contained assembly**
(`Assets/Scripts/IsoCoreFoundation/IsoCore.Foundation.asmdef`) with **no
references** to the existing global `Assembly-CSharp`. Everything lives under
namespace `IsoCore.Foundation`.

Why (from Orientation §4/§7/§8):
- The existing `IsoWorldChunkManager` (2446 LoC) carries the project's single
  highest-risk debt — **duplicated Burst `*Static` vs C# sampling math** that must
  be hand-mirrored. Building on it inherits that fragility.
- The meta stack (quests/dungeons/combat/towns/guilds/LitRPG progression) is
  densely cross-wired via `static Instance` singletons. Touching it risks compile
  breakage across unrelated systems.
- The brief mandates an **isolated** build first and **no competing active
  world/player/inventory systems** in the final scene.

Isolation resolves **all** open decisions from Orientation §8 at once:

| Open decision | Resolution |
|---|---|
| Starter-zone path | New: sampler emits a flat spawn clearing. Neither legacy path used. |
| Seed type | New: single `int` seed in `FoundationConfig`. `WorldManager` not used. |
| Biome/placement model | New: `BiomeDefinition → BlockGroupDefinition → BlockDefinition` (ISO-CORE three-tier). Legacy `IsoBiomeDefinition`/`BiomeAssetRuleSet` not referenced. |
| Target scene | New: `Assets/Scenes/IsoCoreFoundation.unity`. |
| LitRPG meta stack | Not referenced ⇒ **no shims required**. |

The existing project stays **100% compilable and untouched**; the foundation is
additive. Migration of legacy systems into this foundation is deferred to
[07_Migration_Plan](07_Migration_Plan.md).

---

## 1. Clean data separation (the brief's central rule)

Each concern is a **distinct layer**, never conflated:

| Concern | Owner | Representation |
|---|---|---|
| Rendered visuals | `IsoWorldRenderer` | pooled `SpriteRenderer`s, manual iso sort |
| Collision / blocking | `IsoCell.Solid` + `IsoCell.OccupantId` | per-cell flags, queried (no physics push) |
| Navigation / height | `IsoCell.Height` + `IsoCell.Walkable` | per-cell `byte` height, cached |
| Item / resource data | `ItemDatabase`, `ResourceNode` | SO definitions + world instances |
| Placeable / building data | `IsoCell.OccupantId` + `PlaceableInstance` | per-cell occupancy + prefab instance |
| Interaction data | `IInteractable` on world objects | component contract |

The per-cell `IsoCell` struct is the **single source of truth** for height,
biome, block, walkability and occupancy. Render reads from it; it never reads from
render. This is the explicit fix for Orientation §7.3 (height/interaction not
separated, recomputed-per-query, uncached) — here every cell is sampled **once**
and cached in its chunk.

### Render approach — deliberate divergence, documented

The foundation renders **everything** (ground blocks, placeables, nodes, mobs,
player) as pooled `SpriteRenderer`s with a **single deterministic iso sort
formula** (`IsoGrid.SortingOrder`), rather than a Unity isometric `Tilemap`.

Rationale: a uniform manual-sort sprite pipeline is fully deterministic and
inspectable, has no hidden tilemap/sorting-axis coupling, and depends on **zero
pre-authored tile/RuleTile assets** (placeholder sprites are generated at runtime
by `PlaceholderArt`). This is the lowest-risk path to a *working, recoverable*
slice. A `Tilemap`-backed ground renderer honoring the legacy `IsometricZAsY` +
`(0,1,-0.26)` contract is recorded as a **future optimization** in the migration
plan, not a foundation requirement.

---

## 2. Iso world model

- **Coordinates:** integer cell `(cx, cy)` + `byte height`. World→cell and
  cell→world are pure functions in `IsoGrid` (2:1 footprint: tileW=1.0, tileH=0.5;
  each height level lifts visual Y by `heightStep`).
- **Sort:** `SortingOrder(cx,cy,height,layer)` increases with `(cx+cy)` (iso
  depth) then height then entity layer — one formula, no per-system drift.
- **Chunk:** `IsoChunk` = `ChunkSize²` array of `IsoCell`, generated once by the
  **single** `IsoTerrainSampler` (no Burst mirror — one deterministic code path,
  killing Orientation §4's highest risk by construction).
- **World:** `IsoWorld` owns a `Dictionary<ChunkCoord, IsoChunk>` and the **query
  API** — `GetCell`, `IsSolid`, `GetHeight`, `IsWalkable`, `TryPlaceBlock`,
  `TryPlaceOccupant`, `ClearOccupant`. This is the "ask the world, don't push
  physics" contract (Orientation §3) rebuilt clean.
- **Controller:** `IsoWorldController` (MonoBehaviour) streams chunks within
  `viewRadius` of the player and drives `IsoWorldRenderer` with pooled sprites for
  only the visible band (performance reqs: chunked, pooled, bounded).

### Terrain sampler (single source of truth)

`IsoTerrainSampler.Sample(cx,cy) → IsoCell`: deterministic value-noise from the
`int` seed → temperature/moisture/continental → biome selection (weighted by
climate range) → height column → surface block from the biome's `BlockGroup`. A
circular **spawn clearing** around origin is flattened and de-mobbed for a safe
start. One method, called once per cell, cached in the chunk — visible tiles and
collision/height queries cannot desync.

---

## 3. Data model (ScriptableObjects + code-built defaults)

All definitions are `ScriptableObject`s (so designers can author `.asset`s and the
editor tool can bake them) **and** are buildable in code via
`FoundationContent.BuildDefault()` (so the runtime never blocks on disk assets).
Mirrors ISO-CORE's `Data/` SO model (Study §4) without copying any asset.

| Definition | Key fields (per brief's "Desired New Foundation Design") |
|---|---|
| `BlockDefinition` | id, displayName, group, color/sprite, `CollisionMode {Walkable,Solid,Water,Decorative}`, harvest tool req, drops, height/layer behavior |
| `BlockGroupDefinition` | id, displayName, ordered `BlockDefinition` variants |
| `BiomeDefinition` | id, displayName, climate ranges (temp/moist), surface `BlockGroup`, height params, resource-node spawn rules, mob spawn rules, decoration rules |
| `ItemDefinition` | id, displayName, icon/color, `ItemCategory`, maxStack, use behavior, optional block/placeable ref |
| `PlaceableDefinition` | id, displayName, sprite/color, footprint, blocksMovement, interaction kind, required item, optional crafting-station type |
| `RecipeDefinition` | id, station type, inputs[], outputs[], unlock flag |
| `MobDefinition` | id, displayName, sprite/color, biome spawn rules, movement, drops, passive/hostile |
| `ResourceNodeDefinition` | id, displayName, sprite/color, required tool, weighted drop table, respawn |

Databases (`BlockDatabase`, `ItemDatabase`, …) are plain C# lookup wrappers
(id→definition) — not SOs — held by a single `FoundationContent` content set.
Cross-references are validated by `FoundationValidator` (recipe inputs exist,
placeables reference real blocks/items, biomes reference real block groups, etc.).

---

## 4. Gameplay systems

- **Inventory** (`Inventory`, plain C#): fixed slot list, stack-aware
  add/remove/query, `OnChanged` event. Single source of truth — fixes Orientation
  §4's mixed-access bug by being the only inventory and passed explicitly.
- **Hotbar** (`Hotbar`): selection index over the first N inventory slots;
  number-key + scroll selection. Replaces the legacy discovery-order `HotbarUI`.
- **Harvesting** (`HarvestSystem`, `ResourceNode`): nearest-node-in-range
  detection (iterate active nodes — cheap at slice scale, no physics overlap),
  optional tool gating, weighted drops → `Inventory`, deplete + optional respawn.
- **Building** (`PlacementSystem`, `PlaceableInstance`): selected hotbar item →
  ghost preview at targeted cell (mouse→cell) → validity (in-world, not occupied,
  item owned) → consume item → write `IsoCell` occupancy / set block → spawn
  instance → refresh render. **Placed solids register into the cell occupancy
  layer**, so the player's world-query collision blocks against them (the explicit
  fix for Orientation §7.2 "solid block needs real occupancy, not a bare
  collider").
- **Crafting** (`CraftingSystem`, `CraftingStation`): `TryCraft(recipe)` — checks
  inputs + station proximity (if `station != None`), consumes inputs, produces
  outputs into inventory.
- **Mobs** (`Mob`, `MobSpawner`): wander AI sampling walkable cells via the world
  query; pooled, biome/radius-gated spawn with a population cap; passive/hostile
  flag; drops on removal.
- **Player** (`IsoFoundationPlayer`): kinematic continuous movement; per-axis
  world-solidity check (slide on block); height sets visual Y + sort; no null-world
  silent-disable (fails loud). `PlayerInteraction` routes E=harvest/interact,
  LMB=place, RMB=remove, I=inventory, C=crafting, 1-9/scroll=hotbar.
- **UI** (`FoundationHUD`, IMGUI): hotbar strip, inventory panel, crafting list,
  interaction prompt, validation banner. IMGUI chosen for **zero-asset
  reliability**; uGUI/pixel-perfect upgrade is future scope.

---

## 5. Save/load hooks

No full persistence in the MVP, but the seams exist: `IsoWorld` exposes block
**deltas** (cells whose occupancy/block changed from sampler output) and
`Inventory` serializes its `(itemId,count)` slots. `FoundationSaveData` (DTO) +
`ISaveable` markers are scaffolded so a `JsonUtility` save can be added without
reshaping systems. Mirrors ISO-CORE's per-world-space delta model (Study §5.9)
conceptually; full impl deferred.

---

## 6. Editor & validation workflows (WF5/WF6)

Menu root **`Tools/LIT-ISO/ISO-Core Foundation/`** (priority band 40–60, free per
Orientation §3), reusing the `Run...(bool showDialog) → string log` compose
pattern from `GoldenPathTools`:

- **Audit Project** → regenerates a systems audit report.
- **Inventory ISO-CORE Reference** → re-runs the catalog/DLL inventory.
- **Build Foundation Scene** → creates/refreshes `IsoCoreFoundation.unity` with a
  single `FoundationBootstrap` object (+ camera). Idempotent, no manual surgery.
- **Generate Content Assets** → bakes `FoundationContent.BuildDefault()` to `.asset`
  files (optional; runtime doesn't need them).
- **Validate Foundation** → editor-side checks (no play mode needed): content
  databases non-empty + cross-refs valid; scene exists with bootstrap; all
  acceptance-criteria systems present. Emits
  [06_Validation_Report](06_Validation_Report.md).
- **Run Golden Path** → chains Build → Validate.

`FoundationBootstrap` builds the entire runtime graph on `Awake` (world, player,
spawner, UI) so the scene is **one object** — trivially regenerable, never
hand-wired, and there is exactly **one** world/player/inventory (no duplication).

---

## 7. Performance contract

Chunked generation; only `viewRadius` chunks resident; pooled sprites for ground +
entities; bounded per-frame work (player resolve + nearest-node scan over a small
active set); no global renderer/tilemap scans; sampler runs once per cell and is
cached. Editor validation replaces expensive runtime checks.

---

## 8. Module map (WF4 deliverable)

```
Assets/Scripts/IsoCoreFoundation/
  IsoCore.Foundation.asmdef
  Core/    IsoGrid, PlaceholderArt, FoundationConfig, FoundationContent, FoundationBootstrap, IInteractable
  Blocks/  BlockDefinition, BlockGroupDefinition, BlockDatabase
  Biomes/  BiomeDefinition, BiomeDatabase
  Items/   ItemDefinition, ItemDatabase
  Building/ PlaceableDefinition, PlaceableDatabase, PlacementSystem, PlaceableInstance
  Crafting/ RecipeDefinition, RecipeDatabase, CraftingSystem, CraftingStation
  Harvesting/ ResourceNodeDefinition, ResourceNode, HarvestSystem
  Inventory/ Inventory, Hotbar
  World/   IsoCell, IsoChunk, IsoTerrainSampler, IsoWorld, IsoWorldRenderer, IsoWorldController
  Mobs/    MobDefinition, MobDatabase, Mob, MobSpawner
  Player/  IsoFoundationPlayer, PlayerInteraction
  UI/      FoundationHUD
  Editor/  IsoCore.Foundation.Editor.asmdef, FoundationMenu, FoundationSceneBuilder, FoundationValidator, FoundationReports
Assets/Scenes/IsoCoreFoundation.unity
```

Handoff → Workflow 4: implement in dependency order (Core → Data defs+databases →
World → Inventory → Player → Harvest → Building → Crafting → Mobs → UI →
Bootstrap), then Workflow 5 editor tooling, then Workflow 6 validation.
