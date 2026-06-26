# 28 — Biome Asset Inventory & Coverage Review

> Asset-review pass for the biome-improvement plan. Covers every terrain TILE and
> PROP/decoration in the curated **BiomeSketch** library plus the runtime
> `Resources/` and `Generated/` trees. Review-only — no code/assets touched.
> Date: 2026-06-17.

## Sources scanned

| Dir | Tiles | Props/decor | Notes |
|---|---|---|---|
| `Tools/BiomeSketch/assets/tile` (+ subtrees) | ~1,400 PNG (incl. variants/blends/sync dupes) | — | **Curated library, priority.** Bulk is variant fan-out + sync-pipeline duplicates of a small canonical set. |
| `Tools/BiomeSketch/assets/prop` + `decor` | — | ~560 PNG (incl. variants/sync dupes) | Same — heavy fan-out over a modest canonical set. |
| `Assets/Resources/Tiles` | 136 PNG | — | Runtime tiles (32×32). |
| `Assets/Resources/Decorations` (+ `fences`) | — | ~110 props + 3 fence sheets (rope/wood/stone) | Runtime props. Mostly 128×128; cactus + small-decor outliers. |
| `Assets/Art/BiomeDecorations` (`_Towns`, `_HighDetail_Unused`) | — | 12 root + 6 town + 7 hi-detail(4x) | Town/building art, mostly unused at runtime. |
| `Assets/Generated/Tiles` (Forest/Plains/Shared) | 25 | — | Plains/forest generation only. |
| `Assets/Generated/Props` (Forest/Plains/Shared) | — | 34 | Plains/forest generation only. |

**Key structural finding:** the *distinct canonical* asset set is far smaller than
the file count. BiomeSketch's size comes from (a) seasonal variant fan-out
(`_lush/_dry/_frost/_autumn`), (b) texture/new variants (`_tex01/02`, `_new01-03`),
(c) directional edge blends (8 dirs × variants), and (d) `review_sync` /
`review_candidates` pipeline **copies** of the same files. Real unique coverage is
concentrated in **plains, forest, snow, stone/mountain, sand/beach, water, dungeon,
farming** — and almost nothing biome-specific for marsh, badlands, or cave/fungal.

---

## Canonical biome → asset mapping (by type)

The intended 8 biomes (15_LitRPG_System_Bible.md) and the closest existing asset families:

| Biome (theme) | Closest existing tile family | Closest props |
|---|---|---|
| **Mosswake Meadow** (plains/grass) | `plains2_00–15`, `grass_1–3`, `plains_grass_base/tufts/flower_grass`, `dirt`, `soil` | `plains_tree(_v2)`, `plains_bush(_v2)`, `plains_rock(_v2)`, `plains_small_tree`, `plains_willow`, `flower`, `flower_tulip`, `tuft`, `bush` |
| **Brindlecap Woods** (forest) | `forest_floor`, `forest_grass_base/tufts`, `forest_moss_grass`, `forest_leaf_litter`, `forest_dark_underbrush`, `forest_rich_dirt`, `forest_mud_path`, `canopy_1–3` | `forest_oak_tree`, `forest_deep_oak_tree`, `forest_pine(_young)`, `forest_dead_tree`, `forest_bush`, `forest_moss_bush`, `forest_fern`, `forest_mushrooms`, `forest_stump`, `forest_rock`, `log`, `pine`, `tree`, `stump` |
| **Sunspool Fields** (dry grassland) | `plains_dry_grass`, `plains2_*_dry` variants, `farm_*` (tilled/planted/fallow), `farming_00–15` | `plains_dry_bush`, `scarecrow`, wheat crop prop, `plains_bush_v2_*_dry` |
| **Duskwick Marsh** (wetland) | `forest_mud_path`, `shared_mud_dark`, `water/_deep/_swell`, `forest_water_edge` (no marsh-native tile) | `dock_post`, `boat_rowing`, `shore_stone` (no marsh-native prop) |
| **Honeyshale Cliffs** (stone hills) | `mountain_stone_00–15`, `stone_block/path/cracked/mossy/scree/snowcap`, `badlands_1/2` | `rock`, `shared_gray_rock`, `forest_rock`, `ore_*` (copper/iron/silver/gold/manacrystal/starmetal), `copper_vein` |
| **Kindlestep Badlands** (desert/scrub) | `sand_00–15`, `badlands_1/2`, `lava`, `fire_trap` | `cactus_*` (9), `plains_dry_bush` |
| **Winterwool Pines** (snow) | `snow2_00–15`, `stone_snowcap`, `*_frost` variants, snow blends | `forest_pine` reused (no snow-native prop) |
| **Glowcap Grotto** (cave/fungal) | `dungeon2_00–15`, `dungeon_floor_1–5`, `dungeon_stone_*` | `glowbug`, `wisp`, `forest_mushrooms` reused (no cave-native prop) |

### By prop class (canonical, deduped)
- **Trees:** plains_tree(_v2), plains_small_tree, plains_willow, forest_oak_tree, forest_deep_oak_tree, forest_pine, forest_pine_young, forest_dead_tree, pine, tree.
- **Rocks/boulders:** rock, shared_gray_rock, forest_rock, plains_rock(_v2), shore_stone.
- **Bushes/shrubs:** bush, plains_bush(_v2), plains_dry_bush, forest_bush, forest_moss_bush, forest_fern.
- **Flowers/groundcover:** flower, flower_tulip, tuft, forest_mushrooms, glowbug, wisp.
- **Logs/stumps:** log, stump, forest_stump.
- **Ore/feature:** ore_copper/iron/silver/gold/manacrystal/starmetal, copper_vein.
- **Desert:** cactus_arm/barrel/barrel2/dead/pad/pear/round/small/tall (9).
- **Water/shore:** dock_post, boat_rowing, shore_stone.
- **Built/feature (town & craft, biome-agnostic):** anvil, furnace, grindstone, loom, sawmill_bench, tanning_rack, crafting/alchemy/cooking tables, market stalls, tents (common→mythical), guild/library/shop/tavern building tiers, brazier, torches, lanterns, campfire, scarecrow, chests, fences (rope/wood/stone sheets), wheat crop, ruined wall/pillar.

---

## Coverage & gaps per biome

Rating key: **rich** / **ok** / **thin** / **empty**.

| Biome | Ground tiles | Transitions/edges | Trees | Rocks | Bushes | Flowers/cover | Water/shore | Special | Overall |
|---|---|---|---|---|---|---|---|---|---|
| **Mosswake Meadow** | rich (16 plains2 + grass + dirt + 4 seasons) | rich (grass→dirt/sand/path, 8-dir blends, diag/cross) | rich | rich | rich | ok | ok | ok (fences, paths) | **RICH** |
| **Brindlecap Woods** | rich (floor, moss, litter, underbrush, canopy, rich dirt, mud path) | ok (grass→mud/leaf/dirt, plains→forest blends) | rich | ok | ok | ok (fern, mushrooms) | thin | ok | **RICH** |
| **Sunspool Fields** | ok (dry grass + dry variants + full farm set) | thin (no dry-grass→sand/dirt edges) | thin (reuse plains) | thin | thin (dry_bush only) | thin | empty | ok (scarecrow, wheat) | **OK / THIN** |
| **Honeyshale Cliffs** | rich (16 mountain_stone + 6 stone variants + 4 seasons) | thin (stone↔grass/snow blends exist but few) | n/a | ok | thin | empty | n/a | rich (full ore set, copper_vein) | **OK** |
| **Winterwool Pines** | rich (16 snow2 + snowcap + frost variants + snow blends) | ok (plains/snow/mountain blends) | thin (no snowy tree) | thin | empty | empty | empty | empty | **THIN (props)** |
| **Kindlestep Badlands** | ok (16 sand + badlands_1/2 + lava) | ok (grass→sand, sand→water seasonal blends) | n/a | thin | thin | empty | n/a | ok (cactus set, fire_trap) | **THIN** |
| **Duskwick Marsh** | **empty** (only mud/dark-water reuse) | **empty** | empty | empty | empty | empty | thin (generic water) | thin (dock/boat) | **EMPTY** |
| **Glowcap Grotto** | ok (16 dungeon2 + 5 floor + dungeon_stone) | empty | n/a | empty | empty | thin (glowbug, wisp, reused mushrooms) | empty | empty | **THIN / EMPTY (fungal)** |

### Concrete missing pieces

**Duskwick Marsh — highest priority, essentially no native assets:**
- No marsh/peat/bog ground tile; no murky/stagnant water variant (only clean blue water + deep).
- No reeds, cattails, lily pads, marsh grass, peat tufts, dead/rotting logs in water.
- No mud↔water shore transition; no bog-edge tiles.
- No marsh fauna props (frogs, dragonfly), no wax/fish-trap feature props.

**Glowcap Grotto — fungal layer near-empty:**
- Dungeon stone floor exists, but no luminous-fungus *ground/wall* tiles, no wet-stone variant, no crystal/quartz vein tiles.
- Props: only glowbug + wisp + reused forest_mushrooms. Missing: glowcaps (large luminous mushrooms), crystal clusters, stalagmites/stalactites, glowing moss, relic/ruin props, underground pools.

**Sunspool Fields — borderline:**
- Relies on dry-tint variants of plains; no native gold-grass tile distinct from plains2.
- No dry-grass edge transitions; missing reeds (per bible "reeds, bees"), beehive/bee props, copper-outcrop variant, haybale, dry shrubs beyond `plains_dry_bush`.

**Winterwool Pines — tiles rich, props empty:**
- Snow ground is well-covered; **no snow-laden pine tree, no bare/frosted deciduous, no snow-dusted rock/bush, no ice/frozen-water tile, no snowdrift/icicle props.** Currently must reuse green forest_pine on snow (style mismatch).

**Kindlestep Badlands — thin beyond sand+cactus:**
- No dry/cracked-clay tile distinct from sand; no cinder/scorched tile; no sandstone boulder, dead shrub variety, dunes, bleached-bone/skull feature props, glass-sand variant.

**Honeyshale Cliffs:**
- Ore set is strong, but missing: shale/layered-rock prop, boulder pile, goat/feature, crystal outcrop, cliff-face/height edge tiles distinct from mountain_stone.

**Brindlecap Woods (minor):** no berry bush, no fallen-leaf pile prop, no root/vine groundcover prop, thin on forest rocks/boulders.

---

## Style / quality / scale inconsistencies flagged

1. **Scale outliers in `Resources/Decorations`.** Most props are **128×128**, but a cluster is tiny:
   - All 9 **cactus** props are **~14–37 px** (e.g. `cactus_small` 14×21, `cactus_tall` 37×41). Badlands will look broken next to 128px trees.
   - Small-decor at **32×32**: `bush, copper_vein, flower, flower_tulip, log, rock, shore_stone, stump, tuft`. These are tile-scale, not prop-scale — fine as groundcover but inconsistent with the v2 prop family.
2. **Duplicate / pipeline noise in BiomeSketch.** `review_sync/`, `review_candidates/`, and `sync_all_candidate_*` / `sync_ready_*` / `sync_held_*` are mostly **copies** of canonical files — inflates counts, not coverage. The real library is the loose files in `tile/`, `prop/`, `decor/` plus `variants/`, `blends/`, `gradient_blends/`.
3. **Naming inconsistency / messy filenames** in `Art/BiomeDecorations`: prompt-string filenames with spaces and `(1)/(2)` suffixes (e.g. `isometric_oak_tree (2).png`, long `...no_ground__no_base.png`). `_HighDetail_Unused` 4x building art and `_Towns` 128×128 buildings are not wired to runtime.
4. **Biome-name mismatch.** All asset names use generic terms (plains/forest/snow/mountain/sand/dungeon); none use the canonical biome names (Mosswake/Brindlecap/etc.). A name-mapping table will be needed when wiring assets to the bible's biomes.
5. **`v2` vs v1 prop duplication** for plains (tree/bush/rock have both `plains_x` and `plains_x_v2` plus `_big`/`_young`/`_lg`/`_sm` size cuts). Decide a canonical set to avoid mixed-style placement.

---

## Top gaps to fill (priority order)

1. **Duskwick Marsh — build from scratch:** peat/bog ground tile, murky-water variant, mud↔water shore transitions, reeds/cattails/lily-pad/marsh-grass props.
2. **Glowcap Grotto fungal set:** luminous-fungus ground + wall tiles, wet-stone & crystal-vein tiles, glowcap mushrooms, crystal clusters, stalagmites, glowing moss, underground pool.
3. **Winterwool Pines props:** snow-laden pine, frosted bare tree, snow-dusted rock/bush, ice/frozen-water tile, snowdrift + icicle props.
4. **Re-scale the cactus + small-decor props** (or formally classify the 32px ones as groundcover) so Badlands/Sunspool placements match the 128px prop scale.
5. **Sunspool distinctiveness:** native gold-grass tile + dry-grass edges, reeds, bees/hive, haybale, copper outcrop.
6. **Kindlestep variety:** cracked-clay / cinder tiles, sandstone boulders, dunes, dead-scrub + bone feature props.
7. **Honeyshale cliff faces:** dedicated height/cliff-edge tiles + shale/boulder/crystal props distinct from mountain_stone.
8. **Cleanup (non-art):** prune `sync_*`/`review_*` duplicate copies from the curated count, normalize `Art/BiomeDecorations` filenames, pick canonical v1-vs-v2 plains props.
