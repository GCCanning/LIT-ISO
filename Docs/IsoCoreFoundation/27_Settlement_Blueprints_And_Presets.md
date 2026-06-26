# 27 — Settlement Blueprints & Presets

> Tiled design presets for **hamlet → village → market town → frontier city**, built to feed
> the existing `IsoSettlementSampler` + `settlements.json`. Defines, per tier: the layout
> blueprint, the asset palette (buildings, **rope/wood/stone fences**, roads, lighting, crops,
> civic props, workstations), the **NPC population**, and the data + import work to ship it.
> Companion to asset review `26` and master plan `25` (Phase H).

---

## 1. Design goals

A settlement should answer three questions at a glance: *how big / safe is this?* (perimeter +
scale), *what can I do here?* (visible services — shop, forge, guild, market), and *is it alive?*
(NPCs moving with purpose). Tiers escalate all three. Variety comes from layout preset (cross /
spine / ring) + biome (coast adds a dock, meadow a bigger farm belt).

## 2. The three-step boundary ramp (your fences)

The new fence art slots straight into the tier ladder:

| Fence | Read | Primary use | Tiers |
|---|---|---|---|
| **Rope** (post + draped rope) | soft / decorative | crop plots, garden & market cordons, pasture edges, plaza delineation | all tiers (dressing) |
| **Wooden** (straights/corners/intersections/**gates**) | rustic boundary | settlement perimeter + farm/animal pens; hamlet entrance = wooden gate | **hamlet, village** |
| **Stone** (straights/corners/intersections/**gates**) | permanent / civilized | settlement boundary + estate/garden walls; pairs with stone wall + guard tower + gate art | **market town, city** |

So perimeter by tier: **wood fence (hamlet/village) → stone fence + stone wall + towers (town/city)**,
with **rope** as universal soft dressing.

## 3. Blueprint legend

```
.  ground/grass      ~  crop field        =  packed/dirt path    #  stone road
f  wooden fence      r  rope fence        W  stone wall          G  gate
*  torch (hamlet)    !  lantern post      ^  brazier (city)      o  well / plaza center
S  shop (r1/r2/r3)   T  tavern            H  guild hall          L  library
B  blacksmith        C  cottage/house     M  market stall        K  workstation (crafting)
t  guard tower       F  farm building     n  NPC wander node     V  vendor NPC (at a shop)
```

## 4. Tier blueprints

### 4a. HAMLET — small, r1, "a rest stop with a shop" (~18×18)
Cross paths, central well, a general store + a cottage or two, **wooden fence** with one gate,
a crop ring outside, simple torches. 1–3 wandering NPCs.

```
~ ~ ~ . . f f f G f f f . . ~ ~ ~
~ ~ . f . . . = . . . f . . ~ ~ .
. . f . C . . = . . S V f . . . .
. . f . . . . = . . . n f . . . .
f f f = = = = o = = = = f f f . .
. . f . . n . = . . . . f . . . .
. . f . . . . = . T . . f . . . .
~ ~ . f . . . = . . . f . . ~ ~ .
~ ~ ~ . . f f f G f f f . . ~ ~ ~
```
- **Buildings:** `shop_r1` (general store, with a vendor NPC), optional `tavern_r1`, 1–2 `cottage`.
- **Perimeter:** wooden fence + 1 gate on the main road. **Rope** fences around the crop ring.
- **Lighting:** `torch_standing` at the gate and well.
- **Services/stock:** tier 1 (basic_crafting, camp_trade, rest).
- **NPCs:** 1–3 townsfolk wandering the cross + 1 shop vendor.

### 4b. VILLAGE — medium, r1–r2, "you can actually outfit here" (~28×24)
Spine with a branch, plaza + well, store + guild board + a forge/alchemy station, a market stall
or two, wooden fence (optional first stone segment near the plaza). 3–6 NPCs.
- **Buildings:** `shop_r1/r2`, `guild_hall_r1`, `tavern_r1`, 2–3 `cottage`, a `blacksmith`/forge.
- **Crafting district:** `anvil` + `furnace` (+ `sawmill_bench`) grouped behind the plaza.
- **Perimeter:** wooden fence; **rope** around the larger crop ring + pasture.
- **Lighting:** `torch_standing` + first `lantern_post` on the spine.
- **NPCs:** 3–6 townsfolk + 1–2 vendors.

### 4c. MARKET TOWN — large, r2–r3, **walled** (~42×36)
Ring road + spokes, plaza with banners + well, a **market square** (stalls + `shop_r2/r3`), a
**crafting quarter** (blacksmith + workstations), `guild_hall_r2` + `library_r2`, residential
lanes, **stone wall + gate(s) + guard towers**, lantern rows, farm belt *outside* the walls.

```
~ ~ ~ F ~ ~ W W W W W G W W W W W ~ ~ F ~ ~ ~
~ ~ ~ ~ ~ W ! . C . # . . C . ! W ~ ~ ~ ~ ~ ~
. . . . W . . C . # . K K . . . W . . . . . .
t . . W . M M . . # . K B K . . . W . . . t .
. . W . M M M . . # . . K . . . C . W . . . .
. . G # # # # # # o # # # # # # # # G . . . .
. . W . S S V . . # . . H . L . C . W . . . .
t . . W . S . . . # . . H . L . . W . . . t .
. . . . W . n . . # . . . n . . W . . . . . .
~ ~ ~ ~ ~ W W ! W W G W W ! W W W ~ ~ ~ ~ ~ ~
~ ~ F ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ F ~ ~
```
- **Districts:** plaza (center) · market (stalls + shops) · crafting (blacksmith + `anvil`/`furnace`/
  `grindstone`/`loom`/`tannery`) · civic (guild + library) · residential (cottages) · gate/walls ·
  farm belt (outside).
- **Perimeter:** **stone wall + stone fence** infill, **2 gates**, **guard towers** at corners.
- **Lighting:** `lantern_post` rows along the ring; `brazier` flanking gates.
- **Stock/loot:** tier 3 (advanced_crafting, forge, alchemy, market_trade, library, tavern_jobs).
- **NPCs:** 6–12 townsfolk wandering districts + 2–4 vendors; a guard or two near gates.

### 4d. FRONTIER CITY — rare, r3, "a destination" (~58×48)
Wide stone avenues + grand plaza with `town_center`, multiple market squares, a full crafting
quarter, `guild_hall_r3` + `library_r3` + `tavern_r3`, `barracks`, residential blocks, **full
stone walls + multiple gates + towers**, dense lanterns + braziers, farm belt beyond the walls.
- **Perimeter:** full stone walls, **3+ gates**, towers along the run.
- **Lighting:** dense `lantern_post` + `brazier` at plazas/gates → reads bright and lived-in at night.
- **Stock/loot:** tier 4 (+ `specialty_vendor`, best loot).
- **NPCs:** 12–20 townsfolk on scheduled routes + multiple vendors + guards.

## 5. NPC population (making it alive)

You already have the tech: `MobDefinition.appearanceId` renders humanoid NPCs on the player's
multi-directional sheets, with `wanderRadius`/`repathSeconds` movement and factions. The gap is a
**friendly townsfolk type** and a **settlement-aware spawner**.

- **New `townsfolk` mob archetype(s):** `behaviour = Passive`, `faction = Wildlife` (neutral),
  `contactDamage = 0`, `appearanceId = "random"`, small `wanderRadius`. A couple of variants
  (villager, merchant) for variety. *No new asset type required — reuses the character sheets;
  only civilian-look variety is a later art nicety.*
- **Settlement-aware spawn:** a populator seeds N townsfolk per tier (hamlet 1–3 … city 12–20) at
  the **NPC wander nodes** (`n`) and **vendor NPCs** (`V`) at shop doors, bounded to the settlement
  footprint so they patrol roads/districts instead of drifting into the wilderness.
- **Optional schedule (later):** market/plaza by day, home/tavern by night — sells "alive" hardest.
- **Reuse:** the same distance-from-spawn difficulty gradient keeps wilderness bandits/adventurers
  as the *dangerous* wanderers; townsfolk are the *safe* ones inside the perimeter.

## 6. Preset data (what to put in `settlements.json`)

Extend the existing schema (additive — the sampler ignores unknown keys until the perimeter pass
reads them) so each band declares its identity:

```jsonc
// per band (hamlet/village/market_town/frontier_city), add:
"perimeter":  { "fence": "wood",  "wall": false, "gates": 1, "towers": 0 },   // hamlet/village
"perimeter":  { "fence": "stone", "wall": true,  "gates": 2, "towers": 2 },   // market_town
"perimeter":  { "fence": "stone", "wall": true,  "gates": 3, "towers": 4 },   // frontier_city
"lighting":   "torch",            // hamlet → "torch" | town "lantern" | city "lantern+brazier"
"roadMaterial":"packed",          // hamlet "packed" → town/city "stone"
"cropFence":  "rope",             // all tiers: rope around crop/garden plots
"npcCount":   [1,3],              // → [3,6] village, [6,12] town, [12,20] city
"districtProps": {                // which props seed which district
  "market":   ["market_stall_red","market_stall_blue","guild_banner_stand"],
  "crafting": ["anvil","furnace","grindstone","loom","tanning_rack","sawmill_bench"],
  "civic":    ["guild_notice_board","well"],
  "plaza":    ["well","campfire_new"]
}
```

A new **`fenceSets`** block maps the sliced art to logical kits the perimeter pass stamps:

```jsonc
"fenceSets": {
  "rope":  { "straight": ["rope_straight_ne","rope_straight_nw"], "post": "rope_post" },
  "wood":  { "straight": ["wood_fence_straight_ne","wood_fence_straight_nw"],
             "corner": ["wood_fence_corner_n","wood_fence_corner_e","wood_fence_corner_s","wood_fence_corner_w"],
             "tee": "wood_fence_tee", "cross": "wood_fence_cross",
             "post": "wood_fence_post", "gate": ["wood_fence_gate_ne","wood_fence_gate_nw"] },
  "stone": { "straight": ["stone_fence_straight_ne","stone_fence_straight_nw"],
             "corner": ["stone_fence_corner_n","stone_fence_corner_e","stone_fence_corner_s","stone_fence_corner_w"],
             "tee": "stone_fence_tee", "cross": "stone_fence_cross",
             "post": "stone_fence_post", "gate": ["stone_fence_gate_ne","stone_fence_gate_nw"] }
}
```

> These fence ids are the **target names from slicing** (Part 7). Until the art is imported they're
> forward references; the perimeter pass (Phase H1) is the consumer, so nothing breaks in the
> meantime.

## 7. Fence import & slicing contract (ready once you drop the files in)

Drop the three sheets here: `Assets/Art/BiomeDecorations/_Towns/fences/` as
`rope_fence_sheet.png`, `wood_fence_sheet.png`, `stone_fence_sheet.png`.

- **Slice** each sheet into the named segments above. Iso needs **both diagonal straights**
  (NE–SW, NW–SE), the four corners, a T and a cross intersection, an end post, and **gate** variants
  for each diagonal.
- **Import settings (project contract):** point filter, no mipmaps, no compression, **pivot
  bottom-center**, transparent. Set **PPU so one segment spans a cell edge** and stands at a
  believable height next to the player — stone is 12×12 source, so it imports at a smaller PPU
  (≈12–16) or scales up on placement; I'll match wood/rope once I have their dimensions.
- **Wire** each kit into `fenceSets`; the perimeter pass picks straights/corners by footprint edge
  and drops a gate where the road spine crosses the boundary.
- **Player build:** also register a craftable "wooden fence" / "stone fence" placeable so players
  can fence their own homestead (ties into the crafting plan).

### What I need from you
1. Save the 3 PNGs to the path above (or tell me where they are).
2. Confirm the **wood** and **rope** pixel dimensions (stone is 12×12).
3. Confirm each sheet contains both iso diagonal orientations of the straights + gates (from the
   thumbnails it looks right; I'll verify on slice).

---

*Cross-refs: settlement code `Assets/Scripts/IsoCoreFoundation/World/IsoSettlementSampler.cs`;
data `Assets/StreamingAssets/worldgen/settlements.json`; asset review `26`; master plan `25` Phase H.*
