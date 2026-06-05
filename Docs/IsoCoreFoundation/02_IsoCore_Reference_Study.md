# ISO-CORE Reference Study

A read-only, educational reverse-engineering study of the ISO-CORE playtest build, conducted to inform the design of our own isometric foundation. No game assets, art, audio, or code were copied or extracted for reuse. Only catalog metadata, manifest strings, and embedded MonoScript path names were inspected to understand structure and architecture. Everything below is an *inference for learning*; the product we build is original.

---

## 1. Method

What was inspected, all read-only:

- **Addressables catalog** (`catalog.json`, Addressables v1.22.3, StandaloneWindows64): enumerated `m_InternalIds` (1,146 asset-path keys) plus 1 string label. The remaining 1,139 catalog keys are 32-hex asset GUIDs (internal, uncategorizable). Provider is `LegacyResourcesProvider`, so assets ship inside the build rather than as remote bundles.
- **Addressables settings** (`settings.json`): runtime content path (`StreamingAssets/aa/`), `IgnoreFailures:true`.
- **ScriptingAssemblies** (`ScriptingAssemblies.json`): enumerated the engine module list to identify the render/2D/streaming stack.
- **DLL string extraction**: printable-string scan of `Assembly-CSharp.dll` (255 KB, 3,475 ASCII runs). The assembly was **not loaded or executed** — only ASCII runs were read. The DLL embeds full `\Assets\Scripts\...` MonoScript paths, giving an authoritative class→folder map, plus runtime identifier names (fields, coroutines, method names).

**Legal / ethical note:** This is a read-only learning exercise. No sprites, prefabs, ScriptableObjects, scenes, audio, or source code were copied, decompiled into reusable form, or redistributed. Inferences are derived from metadata and naming conventions only. Our implementation will be independently authored.

---

## 2. Engine Stack Observed

Confirmed from `ScriptingAssemblies.json` and in-code type references — a **Unity URP 2D** project:

- **URP**: `Unity.RenderPipelines.Universal.Runtime`, `.Universal.2D.Internal`, `.Config.Runtime`, plus ShaderGraph libraries.
- **2D stack**: `Unity.2D.PixelPerfect`, `Unity.2D.Tilemap.Extras`, `Unity.2D.SpriteShape.Runtime`, `Unity.2D.Animation.Runtime`, `Unity.2D.IK.Runtime`, `Unity.2D.Common`, plus `UnityEngine.TilemapModule`, `SpriteMaskModule`, `GridModule`.
- **Cinemachine**: `Cinemachine.dll` (confirmed in-code: `CinemachineVirtualCamera`, `CinemachineCameraShake`).
- **Addressables**: `Unity.Addressables` v1.22.3, `Unity.ResourceManager`, `Unity.ScriptableBuildPipeline` — runtime content via catalog at `StreamingAssets/aa/`, `IgnoreFailures:true`.
- **Also present**: TextMeshPro, Visual Scripting (Flow/Core/State), Burst, Collections, Mathematics, AI.Navigation, Timeline, SpriteShape.

**Key observation:** Despite a URP-2D + Tilemap base, world generation lives under `WorldGeneration\3D\` and code references temperature/elevation/height columns. This implies a **2.5D / HD-2D-style isometric world built on a tilemap with per-column height**, not a flat 2D grid. The pervasive `...NormalMap` sprite twins and `Obj/Light/Light2.5D` / `MultyDirectionLightTest` confirm a **lit 2.5D sprite pipeline**.

---

## 3. Content Taxonomy

Categorized from the 1,147 catalog keys. Full per-asset listings are in the companion inventory files already written:

- `C:/Projects/Unity-Projects/LIT-ISO/Docs/IsoCoreFoundation/iso_core_reference_inventory.json`
- `C:/Projects/Unity-Projects/LIT-ISO/Docs/IsoCoreFoundation/iso_core_reference_inventory.csv`

| Category | Count | Examples |
|---|---|---|
| other | 690 | `Graphics/*` atlases (~603), world nodes (`Obj/Nature/Trees`, `Obj/Nature/StonesAndOres`, `Obj/Nature/Ruins`, `Obj/Relics/SkeletalRemains`), `Obj/Terrain/Tile`, `Obj/Mechanics/Chunk`, `Obj/Nature/Water/DynamicWater` |
| items | 114 | `Data/Items/{BlockItems,ElixirItems,FoodItems,PlaceableItem,PotionItems,ResourcesItems,ToolItems}` (e.g. `Copper`, `CopperOre`, `cookedMeat`) |
| buildings | 69 | `Obj/Buildings/CraftingTable`, `Furnace_Left`/`Furnace_Right`, `SewingStation_left/right`, `MagicTable` |
| ui | 65 | `Obj/UI/*` (inventory slots, shop tiles, `IngredientDisplay`, `ProcessUIPointer`, `HealthBar`/`EnergyBar`/`HeartContainer`, `FastTravelButton`) + TextMeshPro infra |
| crops | 63 | `Obj/Nature/{Farming,EdiblePlants,Flowers,Plants,Mushrooms,AquaticPlants}` (e.g. `Carot`, `Pumpkin`, `Wheat`, `Voidroot`) |
| blocks | 50 | `Data/Blocks/GrassBlocks/GrassBlock1-8`, clay/sand/snow/wooden-floor/underwater-dirt variants |
| placeables | 36 | `Data/Items/PlaceableItem/{CraftingTable,CarrotSeeds,PumpkinSeeds,BirchSeed,Acorn,PineCone,Coconut,GateCrystal,TeleportPillar}` |
| tools | 33 | `Data/Items/ToolItems/{Copper,Iron,Steel}{Axe,Hoe,Pickaxe,Shovel,Sword}` + `Obj/Products/Tools/*` |
| mobs | 15 | `Data/MobClasses/{Armadillo,Deers,Fish,Fox,Frogs,Slimes}` ↔ `Obj/Mobs/*` |
| blockGroups | 6 | `ClayBlocks`, `GrassBlocks`, `SandBlocks`, `SnowBlocks`, `UnderwaterDirtBlocks`, `WoodenFloorBlocks` |
| biomes | 3 | `BiomeProfiles/{Desert,Forest,SnowForestC}` |
| scenes | 3 | `Scenes/SampleScene`, `Scenes/StartScreen`, label `SampleScene` |

Note: `other` (690) is ~99% art (603 `Graphics/*` atlases + ~87 non-harvestable `Obj/*` props), and `Graphics/` retains scratch/WIP names (`Image2 - Kopie`, `hh`, `stoneeeeee`) — not load-bearing for the data model.

---

## 4. Inferred Data Model

The address scheme uses three roots that map cleanly onto a two-tier data architecture: **`Data/`** (designer-authored ScriptableObject "rules"), **`Obj/`** (runtime prefabs, the "things"), **`Graphics/`** (raw art atlases + normal-map twins). Class names from the DLL confirm the SO/prefab split.

Inferred ScriptableObject shapes (from naming + embedded item-class hierarchy `Item → {BlockItem, PlaceableItem, ToolItem, FoodItem, PotionItem, ElixirItem, CollectableItem, ShopItem}`):

- **`BlockDefinition`** — one logical surface variant. A `Block` has art/normal-map refs, surface type, and a sorting/height offset (`heightOffsetInBlocksForSortingOrder`). Only 4 block types are pickup-able (`BlockItems`: Dirt/Grass/Sand/Snow); most blocks are terrain-only.
- **`BlockGroup`** — a palette of interchangeable visual variants for one surface (e.g. `GrassBlocks` ↔ `GrassBlock1-8`). Mid-tier of a 3-tier hierarchy.
- **`BiomeDefinition` / `BiomeProfile`** — top tier: maps noise (temperature/elevation) → biome → referenced `BlockGroup`s + `BiomeElement`/`BiomeElementGroup` decoration sets. 3 biomes: Desert, Forest, SnowForest.
- **`ItemDefinition`** (base `Item`) — id, display, stack rules, sprite. Subtypes: `BlockItem`, `ToolItem` (durability/`toolBoost`/`toolRestriction`, tier+archetype), `FoodItem` (energy restore), `PotionItem`/`ElixirItem` (effect), `ResourcesItem`, `PlaceableItem`.
- **`PlaceableDefinition`** (`PlaceableItem`) — links an item to its placement-target prefab in `Obj/Buildings/*` (incl. multi-tile `_left`/`_right` and state variants); seeds/saplings are placeables that spawn growing prefabs.
- **`RecipeDefinition`** — implied by `producableItems`/`craftingList`/`ingredients` + the **item → recipe → product** chain: each craftable has a "product/preview" prefab under `Obj/Products/{Buildings,Recources,Tools}` distinct from its equipped/placed prefab (e.g. `Copper(Kiln)` smelted variant; `CopperAxCraftingTable` preview vs equipped `CopperAx`). Recipes are bound to stations (`CraftingTable`, `Furnace`, `CookingPot`, `MagicTable`).
- **`MobDefinition`** (`MobClass`) — paired data + prefab + art; spawn weighting keyed on biome/daytime/height; friendly vs hostile flag (`friendlyMobs`/`hostileMobs`).

**Progression chains encoded in data:** ore (`CopperOre`/`IronOre`/`GoldOre`) → ingot (`Copper`/`Iron`/`Gold`, + `(Kiln)` smelted) → `Steel`; tools tier up Copper → Iron → Steel across {Axe, Hoe, Pickaxe, Shovel, Sword}; alchemy `ElixirItems` + `PotionItems` with a void-tier endgame (`VoidRoot` → `VoidDust` → `VoidPotion`, `GateCrystal` + `TeleportPillar` fast-travel).

---

## 5. Inferred Gameplay Loop & System Architecture

Authoritative class→folder map from embedded MonoScript paths reveals a domain-partitioned architecture.

**Managers / coordinators:**
- `GameFileManager` — top-level world load/save orchestrator (owns `LoadWorld` coroutine, multi-world-space IDs).
- `Terrain` — central world driver (owns `LoadWorldSapceWithDelay`, chunk load/render queues, block lookup tables).
- `ChunkLoader` — streaming chunk manager: `chunkLoadingQueue`/`chunkRenderingQueue`, `Queue/DequeueChunkForLoading`, `batchLoadChunks`, `GetChunksInRadius`, `chunkRadius`.
- `MobSpawner` / `InsectSpawner` — weighted, biome/daytime/height-gated spawners with per-`MobChunk` population caps.
- `SettingsManager`, `AudioManager`, `MusicAndAmbientManager`, `StartScreenManager`.
- A lightweight service-locator wiring (property-injected `manager`/`generator` backing fields) connects `Terrain`, `WorldGeneration`, and `GameFileManager`.

**Addressable-driven content:** designers author `Data/*` ScriptableObjects and `Obj/*` prefabs; the runtime resolves them through the Addressables catalog at `StreamingAssets/aa/` with `IgnoreFailures:true`, decoupling content from code.

**The explore → collect → craft → build → expand spine:**
1. **Boot/menu** — `StartScreenManager` lists saved worlds (`WorldDisplay`), create/delete/load with overworld + indoor world-space IDs.
2. **World gen** — noise-driven (`NoiseFunction`, `Noise`, `seaLevel`, height columns) procedural biome/island terrain; `BiomeProfile` maps noise → biome → `BlockGroup`/`BiomeElement`.
3. **Streaming (explore)** — world split into chunks loaded/rendered via radius queues onto a Unity Tilemap; `PlayerBlockXRay` + sprite masks + `heightOffsetInBlocksForSortingOrder` handle isometric/height draw sorting; assets streamed via Addressables.
4. **Collect** — tiered, durability-limited tools harvest/destroy blocks (`GetBlock`/`DestroyBlock`/`HoeBlock`); drops flow into `PlayerInventory`/`ToolBar`.
5. **Craft** — at `CraftingTable`/`Furnace`/`CookingPot`/`MagicTable` from `ingredients`/`craftingList`, producing `Obj/Products/*`.
6. **Build/expand** — `Builder` (`CanObjectBeBuild`, `objectToBuild`, `CleanBuildArea`) places `PlaceableItem`/blocks, including indoor spaces, chests, shops; farming and fast-travel (`GateCrystal`/`TeleportPillar`) extend reach.
7. **Survival pressure** — `PlayerEnergySystem` (food) + `PlayerTemperature`/`HeatSource` (cold/rain/water drain) gate activity, modulated by `WeatherAndDayTime` day/night + weather.
8. **Ecology** — `MobSpawner`/`InsectSpawner` populate chunks (Deer/Fox/Frog/Fish friendly, hostile tables) keyed on biome/daytime/height with population caps.
9. **Persistence** — `SavingSystem`/`GameFileManager` serialize block deltas + metadata per world-space via dictionary/terrain-key wrappers, with versioned migration (`AdaptOldWorld`).

**Net:** a chunk-streamed, isometric (2.5D, height-aware) procedural survival-crafting-sandbox — Stardew/Terraria-adjacent loop on URP-2D + Tilemap + Cinemachine + Addressables.

---

## 6. Lessons to Apply to Our Foundation

**Imitate structurally:**
- **Three-root content layout** (`Data/` SO rules, `Obj/` prefabs, `Graphics/` art) — clean, designer-friendly, decouples authoring from runtime.
- **Three-tier terrain model** (`BiomeProfile` → `BlockGroup` → `Block`) — biomes pick palettes of interchangeable surface variants; this scales to many visuals with little code.
- **Addressables-driven content resolution** so content is data, not hard references.
- **Chunk streaming with load/render queues + radius governor** around the player; per-column height + sorting offset for isometric depth (`PlayerBlockXRay` occlusion pattern).
- **Item-class hierarchy** (`Item` base → Block/Tool/Food/Potion/Placeable subtypes) and the **item → recipe → product/preview** split (separate equipped vs preview prefabs).
- **Station-bound recipes** and **tier × archetype** progression tables (Copper/Iron/Steel × tool kinds) for cheap, legible depth.
- **Per-world-space save model** with versioned migration (`AdaptOldWorld`) — design save versioning in from day one.

**Reauthor (do not imitate):**
- All art, prefabs, ScriptableObjects, and code — these are original work.
- Clean up the naming hygiene: fix the developer's typos (`GroundEddeting`, `Conection`, `Controler`, `Recources`, `WorldGewnerator`, `IngredientDysplay`) and strip scratch/WIP atlas names.
- Decide deliberately whether to keep the survival layer (temperature/energy) or ship a lighter sandbox; ISO-CORE bolts survival onto crafting — we should choose intentionally.
- Replace the implicit service-locator wiring with explicit, testable dependency injection.
- Evaluate the alchemy/void endgame as optional scope rather than core.

---

## 7. Handoff Notes for Workflow 3

- **Inputs ready:** this study plus `iso_core_reference_inventory.json` / `.csv` (1,147 categorized keys) are the source of truth for the synthesizer.
- **Target data model to scaffold first:** `BiomeDefinition`, `BlockGroupDefinition`, `BlockDefinition`, `ItemDefinition` (+ subtypes), `PlaceableDefinition`, `RecipeDefinition`, `MobDefinition` — as ScriptableObjects under a `Data/` root, with prefabs under `Obj/` and art under `Graphics/`.
- **Flag to synthesizer — world resource nodes as a distinct sub-type** hidden inside the `other` bucket: ~34 `Obj/Nature/Trees/*`, ~24 `Obj/Nature/StonesAndOres/*` ore/stone nodes, ~20 `Obj/Nature/Ruins/*`, lootable `Obj/Relics/SkeletalRemains*`. These are harvestable world objects, not items — model them separately.
- **Minimum viable vertical slice:** 1 biome → 1 block group → block variants → harvest with 1 tool → 1 recipe at 1 station → 1 placeable. Prove the explore→collect→craft→build spine before breadth.
- **Engine baseline to set up:** URP-2D, Tilemap (+Extras), PixelPerfect, Cinemachine, Addressables, TextMeshPro; plan for the 2.5D lit-sprite pipeline (normal-map twins, `Light2.5D`) and height-aware sorting early.
- **Scene shape:** single gameplay scene + start menu (`SampleScene` + `StartScreen`); `StartScreenManager`-style world create/delete/load over multi-world-space save IDs.
- **Caveat to carry forward:** all behavioral claims are inferred from metadata, naming, and field clusters — no method bodies were read. Validate assumptions against our own design intent before locking them in.
