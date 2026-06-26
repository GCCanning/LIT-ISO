# 26 — Asset Review & Settlement Tiering Design

> A review of the PixelLab/generated art we actually have, mapped to the Beta plan (doc 25),
> plus a design for purposeful, varied **hamlet → village → market town → frontier city**
> settlements built on top of the systems that already exist (`IsoSettlementSampler`,
> `StreamingAssets/worldgen/settlements.json`).

---

## Part 1 — Asset inventory (what we can build with today)

Inventoried from `Assets/Resources/Decorations` (110 props), `Assets/Art/BiomeDecorations`
(incl. `_Towns`, `_HighDetail_Unused`), `Resources/Tiles` (136), `Resources/FoundationInteriors`
(59), and `Generated/`. The big takeaway: **most of the art the plan needs already exists** —
several phases are de-risked.

### 1a. Workstations — *unlocks Phase C crafting depth immediately*
`anvil`, `furnace`, `crafting_table`, `cooking_station`, `alchemy_table`, `loom`,
`sawmill_bench`, `tanning_rack`, `grindstone`, `rune_station`, `weapon_rack`, `brazier`,
`keg_rack`, `bar_counter`. → We can give the `StationType` enum (Hand/Workbench/Furnace/
CookingPot/Tannery) real, distinct station art and **add stations** (sawmill, grindstone,
loom, alchemy, rune) without new art.

### 1b. Ore / metal chain — *de-risks Phase C1 (the crafter spine)*
`ore_copper`, `ore_iron`, `ore_silver`, `ore_gold`, `ore_starmetal`, `ore_manacrystal`,
`copper_vein`. → The full copper→iron→steel→…→starmetal progression the plan calls for has
**art ready**; C1 becomes pure data (bars, tools, smelt recipes), no art blocker.

### 1c. Tiered settlement buildings — *the hamlet→city visual ladder, already drawn*
Rank variants `r1 / r2 / r3` exist for: **tavern**, **guild_hall**, **library**, **shop**
(in `Resources/Decorations`). Plus standalone town buildings in `Art/BiomeDecorations/_Towns`
(`blacksmith_shop_with_anvil`, `medieval_tavern_with_sign`, `small_medieval_cottage`,
`stone_guard_tower`, `stone_well_with_wooden_roof`, `wooden_market_stall`) and high-detail
4x building art in `_HighDetail_Unused` (`town_center`, `barracks`, `farm`, `stable`, `house`,
`archery_range`, `tower`). → r1=hamlet, r2=town, r3=city maps 1:1 to the existing
`buildingRanks` gating.

### 1d. Settlement infrastructure
- **Roads/paths:** `stone_path`, `plains_pale_stone_path`, `plains_packed_path`,
  `forest_stone_path`, `forest_mud_path`, and a full set of grass→path edge/corner transitions
  (`greenwake_grass_to_path_*`). → dirt paths (hamlet) vs stone roads (town/city) by tier.
- **Lighting:** `torch_standing` (simple, hamlet), `torch_wall`, `lantern_post` (town lamps),
  `candle_lantern`, `brazier` (city, grander). → a clean 3-tier lighting ramp.
- **Fortifications:** `stoneWall_S`, `stoneWallArchway_S` (gate), `isometric_ruined_stone_wall`,
  `stone_guard_tower`, `tower_4x`, `reference32_ai_candidate_gate_sheet`. → walls + gate + towers
  for the `walled_market_town` preset.
- **Market/civic:** `market_stall_red`, `market_stall_blue`, `guild_notice_board`,
  `guild_banner_stand`, `tavern_sign`, `stone_well_with_wooden_roof`.
- **Farms/crops:** `farm_00`…`farm_15` (16 field tiles), `wheat_crop`, `scarecrow`, plus the
  Farming system. → crop rings around any settlement.

### 1e. World dressing (biome props)
Rich per-biome sets with size/age variants: plains trees (`plains_tree_v2_*`, young variants),
rocks (`plains_rock_v2_*`, big variants), bushes (`plains_bush_v2_*`), forest set
(`forest_oak_tree`, `forest_pine`, `forest_deep_oak_tree`, `forest_mushrooms`, `forest_stump`),
cacti set, `flower`, `glowbug`, `wisp`, `boat_rowing`, `dock_post`. → biome variety is well-covered.

### 1f. Characters / NPC art
Class/player sheets (`LitIsoCreator_Adventurer/Mystic/Ranger/Smith`, `ReferenceKnight`,
`BlackMage_Idle`, `HollowedLight`) and a **full directional walk cycle** (`witch run E/N/…`).
Plus the LPC layered character system (quarantined but available). → enough to put a
**placeholder wandering villager** on screen now; bespoke townsfolk variety is a later art pass.

### 1g. Gaps (small, flag for the art pipeline)
- **Wooden fences / palisade** — none found. You explicitly want wooden fences around hamlets;
  today we only have *stone* walls. → one small PixelLab gen batch (wood fence straight/corner/
  post/gate), or temporarily reuse `stoneWall` at hamlet tier.
- **Townsfolk variety** — one walk cycle exists; a handful of villager looks (and an NPC vendor
  pose) would make towns feel less clone-y. Later art pass via LPC.
- A few **r1/r2/r3** building types are missing intermediate art for `blacksmith`/`market`
  (we have singletons, not ranked) — fine for Beta, nice-to-have later.

---

## Part 2 — Settlement tiering design

### What already exists (don't rebuild)
`IsoSettlementSampler` (884 lines) + `settlements.json` already implement: deterministic site
selection, **distance-banded tiers** (`hamlet → village → market_town → frontier_city`),
rank-gated building pools, footprint/flatness validation, door-toward-plaza orientation, a
road spine with ≥2 wilderness exits, outskirt farm/pasture/dock rings, spawn-ring exclusion,
and **layout presets** (`crossroads_hamlet`, `river_or_coast_village`, `walled_market_town`
with a **gate** district). Tiers already carry `stockTier` (1–4) and `services`.

So your vision is ~70% scaffolded. The work is **expressing tier identity visually + populating
it with life**, not building a town generator.

### The four tiers (formalized to your spec)

| Tier (band) | Buildings | Roads | Lighting | Fortification | Crops | Civic / Market | NPC density | Stock/Loot |
|---|---|---|---|---|---|---|---|---|
| **Hamlet** (small, r1) | general store (`shop_r1`), maybe `tavern_r1`, a cottage | dirt/packed paths, cross shape | `torch_standing` | **wooden fences** (perimeter), no wall | wheat fields + `scarecrow` around the edge | a well or campfire center | 1–3 wandering | tier 1 (basics) |
| **Village** (medium, r1–r2) | + `guild_hall_r1`, forge/alchemy stations, more cottages | packed → first **stone** segments near plaza | `torch` + first `lantern_post` | wooden fences, optional low stone wall | larger crop ring + pasture | 1–2 `market_stall`, notice board | 3–6 | tier 2 |
| **Market Town** (large, r2–r3) | `shop_r2/r3`, `guild_hall_r2`, `library_r2`, `tavern_r2/r3`, blacksmith | **stone roads**, ring-plus-spokes | `lantern_post` rows + `brazier` at gates | **stone wall + gate + guard tower(s)** (`walled_market_town`) | crop district + outskirt farms | market square (multiple stalls), banners | 6–12 + a vendor or two | tier 3 (forge/market) |
| **Frontier City** (rare-city, r2–r3) | r3 across the board + `town_center`, `barracks`, civic buildings | wide stone avenues, plaza paving, fancier street furniture | dense `lantern_post` + `brazier` | full **walls + multiple gates + towers** | farm belt outside the walls | grand market + `specialty_vendor` | 12–20, scheduled routes | tier 4 (specialty/best loot) |

### Purposeful, varied layout (districts)
Lean on the existing `districts` in the layout presets and make placement *zone* by purpose so
towns read as designed, not scattered:
- **Plaza** (center): well/campfire, banners, the highest-rank civic building.
- **Market**: stalls + `shop` clustered, widest paths, most foot traffic (NPC magnet).
- **Crafting**: the workstation props (forge/anvil/furnace, sawmill, loom, tannery) grouped.
- **Guild / Library**: civic block (job board, contracts).
- **Residential**: cottages/houses on side lanes.
- **Gate + Walls** (town+): perimeter ring with gate(s) and guard tower(s); road spine passes
  through the gate.
- **Farm belt** (outskirts): crop fields + scarecrows outside the built area / walls.
Variety comes from the **layout preset** chosen per site (cross vs spine-with-branch vs
ring-plus-spokes) and the biome (coast → dock district; meadow → bigger farm belt).

### The real work (gaps to close — none are huge)
1. **Tier-gated prop & infrastructure pools.** Today `outdoorProps` is one flat list. Split it
   per tier and add **perimeter generation**: wooden fences (hamlet/village), stone walls + gate
   + towers (town/city), lighting tier, and road-material upgrade (dirt→stone) by band. Mostly
   `settlements.json` data + a perimeter pass in `IsoSettlementSampler`.
2. **District-zoned placement.** Ensure buildings/props stamp into their district (market vs
   crafting vs residential) rather than generic lots, so towns read purposefully.
3. **NPC wandering — the one genuinely new system.** No townsfolk/NPC spawner exists. Add a
   settlement-aware NPC populator: spawn N wanderers by tier, walk them along roads/between
   buildings (reuse the `Mob` movement + world-query path; passive behaviour), with optional
   day/night schedules (home at night, market by day). Use the `witch run` walk cycle as the
   placeholder look. This is what makes towns *feel alive*.
4. **Vendor / loot wiring.** Turn `stockTier` + `services` (specialty_vendor, market_trade) into
   actual shop stock and loot quality — ties directly into Phase C (crafting quality) and
   Phase D (loot) of the master plan.
5. **Wooden-fence art** (Part 1g) — one small gen batch.

### Priority (high-impact + cheap → expensive)
- **Cheap, high-impact:** tier-gated prop/lighting/road pools + perimeter fences/walls (data +
  one sampler pass) → instantly makes the four tiers look distinct. Wooden-fence gen batch.
- **Medium:** district-zoned placement; NPC wanderers (placeholder art).
- **Later:** vendor stock/economy depth, scheduled NPC routines, bespoke townsfolk art, ranked
  blacksmith/market building art.

---

*Cross-refs: master plan `25`, ability/VFX `22`, profession/crafter brief `24`. Settlement code:
`Assets/Scripts/IsoCoreFoundation/World/IsoSettlementSampler.cs`; data:
`Assets/StreamingAssets/worldgen/settlements.json`.*
