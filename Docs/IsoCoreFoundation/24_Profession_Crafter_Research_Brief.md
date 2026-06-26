# 24 — Profession & Crafter Research Brief

> Research + design pass for LIT-ISO's **profession / crafter side**. Clean-room: every
> mechanic below is learned from the *shape* of cozy-survival and LitRPG crafting systems —
> no game's names, IP, or text is reused.
> Builds **on what we already have**: the live crafting/station/tool/gathering/farming code
> and the emergent **F–S class + 7-day Trial** progression in `FoundationProgression`.
> Companion to `15_LitRPG_System_Bible.md` and `22_Skills_Abilities_VFX_Design.md`.

---

## 0. TL;DR

We do **not** need a new profession system — we already have one. `ProfessionDefinition`,
a `Professions` database, profession **XP channels**, and a day-7 **profession offer**
(scored from skill XP + primary-activity) are all live. The crafter side is currently
**thin** (shallow recipe chains, one durability/quality concept, fixed stations). The
high-leverage work is to **deepen what exists**, not rebuild:

1. **Promote Professions to a first-class, leveled identity** that mirrors the F–S class
   spine (professions already have an XP channel; give them a tier ladder + perks + a
   day-7 "specialization" pick that parallels class crystallization).
2. **Add one refine step** between gather and craft so production reads as a *chain*
   (ore → bar → tool is already half-built via the furnace; extend the pattern).
3. **Add craft quality + material traits as data on the output stack** — the Bible already
   names the tiers (Plain→Mythwarm) and labels (Bent→Beloved); wire them to crafter skill
   level + LUCK, reusing the existing `ItemStack.durability` int as a precedent for
   per-instance data.
4. **Tier the stations** (Workbench L1→L3 unlocking recipe sets) using the existing
   `StationAvailable(StationType)` gate — cheap, because the gate already exists.

Most of these are **content + small data fields**, not architecture. Section 7 ranks them.

---

## 1. Current state (what the code actually does today)

Read from the live repo, not the docs.

### Crafting
- **`CraftingSystem`** (`Crafting/CraftingSystem.cs`): consumes `inputs` → produces
  `outputs`, gated by `StationAvailable(StationType)` (station-in-range). Supports **timed
  crafts** (`craftTimeSeconds`) with a single `ActiveJob` slot, and **optional fuel**
  (`fuelItemId`/`fuelCount`). No quality, no skill scaling, no recipe-unlock check at craft
  time (`unlockedByDefault` exists on the recipe but isn't enforced in `TryCraft`).
- **`RecipeDefinition`**: `station`, `inputs[]`, `outputs[]`, `craftTimeSeconds`, fuel,
  `unlockedByDefault`. Stations enum: `None, Hand, Workbench, Furnace, CookingPot, Tannery`.
- **~30 live recipes** in `FoundationContent.cs`: tools (wood/stone/copper × axe/pick/
  shovel/sword), `smelt_copper` (furnace, timed, wood fuel), `tan_hide` (tannery, timed),
  cooking (`cook_roasted_apple`, `cook_camp_stew` at CookingPot), blocks, furniture,
  civic plots/buildings.

### Items / tools / quality
- **`ItemDefinition`**: `category` (Resource/Block/Tool/Food/Placeable/Misc), `toolType`,
  `toolTier` (1=wood…), `maxDurability`, `durabilityLossPerUse`, `foodRestore`, plus
  placement refs (`placeBlockId`/`placeableId`/`plantCropId`).
- **Tools**: live tiers are **wood(1) → stone(2) → copper(3)** for Axe/Pick/Shovel/Sword,
  plus a single Hoe. Durability scales with tier (80 → 150 → 260). Higher ore tiers exist as
  *materials* (`iron_ore`, `silver_ore`, `gold_ore`, `manacrystal_ore`, `starmetal_ore`) but
  **no iron+ tools, bars, or recipes yet** — the metal chain stops at copper.
- **Quality / traits**: **not implemented in code.** `ItemStack` carries a per-instance
  `durability` int but no quality grade or trait flags. The Bible's 8 tiers and 8 quality
  labels and 8 material traits are **design-only** right now.

### Gathering / farming
- **`ResourceNode` + `HarvestSystem.RollDrops`**: weighted `ItemDrop[]` per node, gated by
  required `ToolType`; some nodes `mandatory` (need the tool), others hand-harvestable.
  ~40 node types (tree/pine/rock/copper_vein/iron…starmetal veins/bushes/flowers/cacti/
  campsite props). Drops are flat — **no tool-tier or skill bonus to yield/quality yet.**
- **`FarmingSystem`**: hoe tills surface → `soil`; seeds (`plantCropId`) plant a
  `CropInstance` that self-grows; mature crops harvested with E. Save/restore of crops
  works. Crop **traits / soil memory** from the Bible are **not implemented**.

### Progression — the part that's further along than expected
`FoundationProgression.cs` + `FoundationTypes.cs` already implement a rich LitRPG spine:
- **12+ skills** live (`foraging, woodcraft, mining, farming, cooking, crafting, building,
  exploration, creaturecraft, combat, warding, spellcraft, trade, lorekeeping`), each tagged
  with a `FoundationProgressionActivity` (Harvest/Craft/Build/Farm/Explore/Creature/Combat/
  Magic/Trade/Lore) and a node-kind. `AddActivityXp(activity, amount, skillIds…)` routes XP.
- **8 emergent Classes** (`wayfarer…oathbearer`), discovered at day-7 from **Trial evidence
  weights + affinity scores** → `BuildClassOffers()` returns the top 3.
- **8 Professions already defined** (`blacksmith, alchemist, cook, builder, trader, farmer,
  miner, fisher`), each with a `primaryActivity` + `progressionSkillIds[]`. At day-7,
  `BuildProfessionOffers()` scores each profession by (sum of its skill XP + its primary
  activity's skill XP) and returns the top 3 — **a profession is offered and selected exactly
  like a class.** `SelectedProfessionId` persists in the Trial lifecycle save.
- A **`Profession` XP channel** (`FoundationXpChannel.Profession`) is live; evidence events
  already grant it (e.g. `X(Profession, "builder", 2)`, `X(Profession, "farmer", 2)`).
- **7 affinities** (ember/tide/root/stone/gale/glimmer/hearth) with a 7-rank ladder
  (`Dormant…Perfect`) and effect multipliers (1.05× → 1.75×).
- Grades **F–S** are computed from total trial score; affinity ranks map to the same ladder.

**So "a Profession" today** = a data-defined production identity, scored from your skill XP
and primary activity, offered (top-3) and chosen at day-7 alongside your class, with a live
XP channel — but **with no tier ladder, no perks, and no in-game mechanical payload yet.**
It is a label waiting for depth. That's the opportunity.

---

## 2. What the research says (clean-room mechanic patterns)

Patterns abstracted from cozy-survival, survival-crafting, LitRPG, and economy MMOs.
Sources in §8.

### (a) Gather → refine → craft production chains
- The satisfying loop is a **chain, not a swap**: raw node drop → a *processed* intermediate
  (smelt/tan/mill/dry/press) → a finished good. Processed materials — not raw hoarding — are
  what "open the map." (Survival-crafting consensus; Valheim/Terraria progression.)
- **Gate content behind infrastructure**, not just resource counts: you advance because you
  *built the next station*, which unlocks the next material, which unlocks the next station.
- **We already have one such chain** (`copper_ore → smelt → copper_bar → copper tools`). The
  win is to make this the **default shape** for every material line, not the exception.

### (b) Profession specialization & progression depth
- Modern designs **reward specialization over generalist crafting**: no one crafter does
  everything well, which creates trade and cooperation. (Profession-progression writeups;
  economy MMOs cap simultaneous professions, e.g. "master up to N of M.")
- **A profession is a leveled identity**, not a checkbox: leveling it unlocks recipes,
  efficiency, and quality ceilings the generalist can't reach. The depth comes from
  **profession level gating the quality tier you can hit**, not just *what* you can make.
- LitRPG convention that fits us: **one combat/class identity + one profession identity**,
  both granting skills/perks and both "significant." Crafter-only protagonists are a
  recognized, marketable subgenre (max-crafting / survival-engineering stories).

### (c) Item quality / tiers / traits
- **Two orthogonal axes** read best: a **material/recipe TIER** (what it's made of — our
  Plain→Mythwarm) and a **craft QUALITY roll** (how well *this instance* came out — our
  Bent→Beloved). Tier is deterministic from inputs; quality is a roll.
- **Quality is driven by crafter skill + materials** (Dwarf Fortress: quality = creator skill
  + material; RimWorld: skill-gated quality bands, top band adds flavor art). Higher skill →
  higher *floor* and *ceiling* on the quality roll.
- **Traits/affixes are the depth multiplier**: a small set of named traits (our Warm/Keen/
  Stout/Light/Rooted/Bright/Lucky/Quiet) that materials carry into outputs and that stack/
  combine. Endgame "masterwork" systems (Diablo-style) let a finished item be *pushed
  further* — extra ranks, a capstone bonus — which maps cleanly to our **Storied/Mythwarm**
  named-item capstone.

### (d) Crafting stations & station upgrades
- **Station tiers unlock recipe sets and speed**: a Workbench you upgrade (L1→L5) enables
  higher-tier recipes and item upgrades; upgrades come from **building adjacent extension
  props** within a radius (chopping block, tanning rack, adze…). This is a *spatial,
  visible* progression — your workshop literally grows.
- Stations often also require **coverage/placement** conditions (roof, % enclosure) as a
  soft base-building objective. We don't need that in Beta, but the **"upgrade by adjacency"**
  pattern is cheap and cozy and fits our placement system.

### (e) Tool tiers & durability
- **Early tools are intentionally weak** (burn durability fast, slow gathering) to make the
  first upgrade feel great. Tool tier should affect **speed, yield, and which nodes are
  harvestable** (we already gate by tool *type*; gate harder nodes by *tier*).
- **Durability is a soft sink** that drives return-to-base loops and repair gameplay (repair
  kits, station repair). We have durability fields but **no repair recipe/loop yet.**

### (f) Economy / trade & sinks
- Player/NPC economies stay healthy via **sinks**: durability/repair, consumable buff-food,
  station upkeep, and **"meta-crafts"** that consume *older finished gear* into new recipes
  (keeps low-tier output relevant and pulls currency down-tier).
- **Specialization + interdependence** is the strongest sink-and-faucet engine: harvesters
  feed crafters feed adventurers. Our single-player frame replaces "other players" with
  **NPC requests / the notice board / caravans** (Trade skill + `trader` profession already
  exist as the hook).

### (g) Making non-combat progression deep
- Non-combat is attractive **when it is as deep as combat**: real trees, visible numbers,
  frequent small rewards, and *system exploitation* (combine soil + crop + weather + meal +
  tool mod + perk). The Bible already commits to this; the code needs the knobs to combine.

---

## 3. Refined PROFESSION model (mapped to our code)

**Principle:** a Profession is the **crafter-side mirror of the emergent Class** — same
day-7 crystallization, same F–S tier ladder, same "earned by doing" feel — but it reads your
**production skills** instead of your combat/exploration evidence. We already build profession
offers next to class offers at day-7; this section gives that offer *teeth*.

### 3.1 The roster (reuse the 8 live professions; group them)
Keep the live ids. Organize them so each owns a production chain and a couple of our skills:

| Profession (live id) | Primary activity | Owns skills | Chain it deepens |
|---|---|---|---|
| **Blacksmith** `blacksmith` | Craft | crafting, mining | ore → bar → metal tools/gear (extend past copper) |
| **Alchemist** `alchemist` | Craft | foraging, lorekeeping | herbs → reagents → potions/preserves |
| **Cook** `cook` | Craft | cooking, trade | crops/fish → prepped food → buff meals |
| **Builder** `builder` | Build | building, woodcraft | wood/stone → planks/blocks → structures |
| **Farmer** `farmer` | Farm | farming, foraging | soil → crops (traits) → seeds/ingredients |
| **Miner** `miner` | Harvest | mining, warding | nodes → ore/stone/crystal (tier-gated) |
| **Fisher** `fisher` | Harvest | foraging, cooking | water nodes → fish → Tide food work |
| **Trader** `trader` | Trade | trade, lorekeeping | requests → routes → economic unlocks |

> Gatherers (Miner/Fisher/Farmer) feed refiners/makers (Blacksmith/Alchemist/Cook/Builder);
> Trader is the sink/faucet. That's the interdependence engine in single-player form, with
> NPCs/board standing in for other players.

### 3.2 Profession tiers (reuse the F–S display map)
Add a **profession level → F–S tier** ladder, driven by the **existing `Profession` XP
channel** (which already has `xpPerLevel`). Mirror the affinity/class ladder so the whole
game speaks one language:

| Tier | Feel | Unlocks at this tier |
|---|---|---|
| **F** | dabbler | base recipes of the chain; can hit *Bent–Usable* quality only |
| **E** | apprentice (day-7 pick lands here) | the profession's signature station recipe set |
| **D** | journeyman | refine step efficiency; *Balanced* quality ceiling |
| **C** | skilled | a profession **perk** (see 3.4); *Fine* ceiling |
| **B** | expert | tier-up recipes (e.g. iron line for Blacksmith); *Masterful* |
| **A** | master | second perk; *Charmed/Awakened*; station L3 recipes |
| **S** | legendary | **named masterwork** capstone (Storied/Mythwarm); world-flavored |

Implementation: profession level already computes from its XP channel (`xp/perLevel`). Add a
`tier` field + the same `rarity/rank → "F".."S"` map proposed for classes in doc 22 §5.
**No new progression subsystem.**

### 3.3 How it relates to the emergent class + 7-day arc
- **Days 1–7:** doing production work raises the relevant **skills** (already happens via
  `AddActivityXp`) and the **`Profession` XP channel** (already granted by some evidence
  events — extend coverage so every harvest/refine/craft/build/farm/trade tick also nudges
  the matching profession channel).
- **Day 7 crystallization:** `CompleteTrial()` already produces **both** a top-3 **class
  offer** *and* a top-3 **profession offer**, and persists the chosen `selectedProfessionId`.
  Surface this in the same crystallization UI as the class pick: "Your hands learned a
  trade — choose your Profession." Class = *how you act in the world*; Profession = *what you
  make*. They're independent, so a Trailblade-Blacksmith and a Hearthbound-Cook are both
  legal and flavorful.
- **After day 7:** profession keeps leveling F→S via its XP channel; class keeps refining
  tier. This is the "one combat identity + one craft identity" LitRPG shape, already wired.
- **Respec hook:** the Bible's *Respec at the Old Well* can re-offer the profession pick
  (re-run `BuildProfessionOffers`) — cheap, since offers are recomputed from current state.

### 3.4 Profession perks (small, data-driven)
A perk is a passive read by existing systems — keep them to one stat/flag each (mirrors the
status-v1 philosophy in doc 22 §7):
- **Blacksmith:** tools you craft start at +1 quality band; −10% durability loss on your gear.
- **Cook:** your meals' buff duration +25%; one extra output on stews.
- **Farmer:** +1 crop trait roll chance; tilled soil retains moisture a stage longer.
- **Miner:** tier-gated nodes cost one fewer hit; +1 ore on rich veins.
- **Builder:** structure recipes cost −1 of their bulk material; place radius +1.
- **Alchemist:** reagent refine yields +1; small chance of a bonus trait on potions.
- **Fisher:** Tide-aligned food gives comfort; better catch table near docks.
- **Trader:** board/NPC requests pay +15%; one extra daily special order.

Each is a single multiplier/flag the relevant system already has a place to read.

---

## 4. CRAFTING DEPTH proposal (fits our engine)

Ordered so each step is a small, isolated addition to live code.

### 4.1 Make production a chain (refine step everywhere)
Generalize the `copper_ore → smelt_copper → copper_bar → tools` pattern:
- **Metals:** add `iron_ore→smelt→iron_bar→iron tools`, then silver/gold/mana/star as
  higher tiers (ores already exist as items + nodes; just add bars + smelt recipes + tools).
  This alone extends the tool ladder from tier 3 to tier 7 with **zero new systems** — only
  `Tool(...)` + `Recipe(...)` + `smelt_*` entries.
- **Fibers/cloth:** `fiber → (spin) → thread → (weave) → cloth` at a Loom station (new
  `StationType.Loom`).
- **Food:** `crop/fish → (prep) → ingredient → (cook) → buff meal` (CookingPot already
  exists; add a prep step at Workbench/Cutting board).
- **Alchemy:** `herb → (refine) → reagent → (brew) → potion` at a new `StationType.Alembic`.
- Each refine step is just a **timed recipe with fuel** — the exact shape `smelt_copper`
  already uses. Refine steps are the natural place to hang **profession XP** and **quality
  rolls**.

### 4.2 Item tiers + quality + traits (per-instance data on the output)
- **Tier** = deterministic from the recipe/material (Plain→Mythwarm, already named). Store as
  an int on `ItemDefinition` or derive from the highest-tier input. Drives UI color + base
  stats.
- **Quality** = a **roll at craft time**, floored/ceiled by **crafter skill level + LUCK**
  (DF/RimWorld pattern). Bands = the Bible's Bent→Beloved. Store **per-instance** on the
  output `ItemStack` — extend the struct the way `durability` already lives there (add a
  `quality` byte and a `traits` bitmask/short).
- **Traits** = the 8 named material traits (Warm/Keen/Stout/Light/Rooted/Bright/Lucky/Quiet).
  Materials carry a trait; refining/crafting **passes traits through** to the output with some
  probability scaled by skill. Traits are small stat/flag mods (same engine as statuses/
  perks). This is the **system-exploitation** depth the Bible wants (soil+crop+meal+tool+perk).
- **Where the roll lives:** add an optional post-craft hook in `CraftingSystem.TryCraft` /
  `Tick` (right where outputs are added) that asks `FoundationProgression` for the crafter's
  skill level and rolls quality/traits before `_inv.Add`. The crafting system already has no
  skill awareness; injecting a `Func<…>` (like the existing `StationAvailable` delegate) keeps
  it decoupled.

### 4.3 Station tiers / upgrades (reuse the station gate)
- Keep `StationType`, but add a **station LEVEL** to `PlaceableDefinition` (or track per
  placed instance). `StationAvailable` already answers "is a Workbench in range?"; extend the
  bootstrap's provider to answer **"is a level-≥N Workbench in range?"** and add
  `minStationLevel` to `RecipeDefinition`. Higher recipes simply require a higher station
  level — a one-field gate on top of the gate we already have.
- **Upgrade by adjacency** (cozy, visible): placing specific **extension props** next to a
  station raises its level (e.g. a grindstone + tool rack next to the Workbench → L2). This
  reuses the **placement system** wholesale; the station just queries nearby placeables. New
  stations to add over time: **Loom, Alembic, Kiln, Mill, Cutting Board**.

### 4.4 Tool tiers & durability loop
- Extend the tool ladder to the ores already present (iron→starmetal), via 4.1. Make **node
  hardness gate by tool tier** (some veins already `mandatory`; add a `minToolTier` so iron+
  veins need stone+ picks, star veins need late picks) — a single comparison in the harvest
  path.
- **Close the durability loop:** add **repair recipes** (item + a little of its material at
  the matching station) and/or **repair kits** (the Bible names them). This turns the existing
  durability fields into an actual sink + return-to-base loop.

### 4.5 Recipe unlock progression
- `RecipeDefinition.unlockedByDefault` exists but isn't enforced. Add an **unlock gate**:
  enforce it in `CanCraft`, and unlock recipes from **profession tier**, **quest rewards**
  (`FoundationRewardType.Recipe` already exists and is granted), **skill levels**, and
  **lorekeeping** (ancient recipes). This makes recipe discovery a progression reward instead
  of a flat list — the Bible's "Recipe insight" notifications already exist as the UI hook.

---

## 5. How crafting/professions feed the 7-day arc

- **Days 1–7 (Awakening):** every gather/refine/craft/build/farm/trade action already grants
  **activity XP → skills**; extend it to also nudge the **matching Profession XP channel** and
  record **Trial evidence** (Crafting/Gathering/Building/Trade categories already exist in
  `TrialEvidenceCategory`). So a player who spends the week smithing naturally trends toward
  **Blacksmith** *and* toward a craft-leaning class — both offered at day-7.
- **Day 7 (Crystallization):** the player picks **a Class and a Profession** from their top-3
  offers (both already computed and persisted). First profession tier (E) unlocks the
  signature station recipe set.
- **Day 8+ (Mastery):** profession levels F→S, unlocking refine efficiency, quality ceilings,
  perks, station-L3 recipes, and finally a **named masterwork** (Storied/Mythwarm) — the
  Bible's "Storied Masterwork" / "Tools With Names" capstone, now a profession capstone.

### Beta-scoped crafter loop (the vertical slice to target)
A single, satisfying chain that exercises the whole stack with minimal new code:

1. **Gather** `copper_ore` from a vein (tool-tier gated) → Mining/Miner XP + Gathering
   evidence.
2. **Refine** at the **Furnace** (`smelt_copper`, timed, wood fuel) → `copper_bar` +
   Crafting/Blacksmith XP. *(Already works — add the quality/profession hooks here first.)*
3. **Craft** a `copper_axe` at a **Workbench** → **quality roll** (skill+LUCK) produces e.g.
   a *Fine, Keen* axe; Blacksmith XP; Crafting evidence.
4. **Use** the better axe → faster/higher-yield harvest → close the loop.
5. **Repair** the axe at the Workbench when durability runs low (new repair recipe) → sink.
6. By day-7, this behavior makes **Blacksmith** a top-3 profession offer; picking it unlocks
   the **iron line** (next tier) and a Blacksmith perk.

Everything in steps 1–2 exists today. Steps 3, 5, 6 are the **new** Beta work, and they're
small (a quality roll, a repair recipe, profession XP routing + tier unlocks).

---

## 6. Where each recommendation lands in the code

| Recommendation | Touches | New vs. extend |
|---|---|---|
| Profession tier ladder + F–S display | `FoundationProgression`, `ProfessionDefinition`, HUD | extend (XP channel exists) |
| Route profession XP from all production actions | `FoundationProgressionHooks`, evidence events | extend |
| Day-7 profession pick UI (next to class) | UI coordinator, `CompleteTrial` already returns offers | extend |
| Profession perks (1 flag each) | `ProfessionDefinition` + reader in each system | new data, tiny |
| Gather→refine→craft chains (metals/fiber/food/alchemy) | `FoundationContent` recipes/items/tools | content only |
| Item tier int | `ItemDefinition` | new field |
| Craft quality roll + per-instance quality/traits | `ItemStack` (+`quality`/`traits`), `CraftingSystem` hook | new fields + 1 hook |
| Material traits pass-through | `ItemDefinition` trait tag, refine/craft logic | new data + small logic |
| Station level + `minStationLevel` gate | `PlaceableDefinition`/instance, `RecipeDefinition`, station provider | extend the existing gate |
| Station upgrade-by-adjacency | placement query | reuse placement |
| New stations (Loom/Alembic/Kiln/Mill) | `StationType` enum + placeables + recipes | content + 1 enum |
| Tool ladder to iron→starmetal + `minToolTier` node gate | content + harvest comparison | content + tiny logic |
| Repair recipes / kits (durability sink) | recipes + a repair path | content + small logic |
| Enforce `unlockedByDefault` + unlock from profession/quest/skill | `CraftingSystem.CanCraft`, progression | extend |

---

## 7. Prioritized recommendations

### High-impact + cheap given our code (do first — Beta)
1. **Extend the metal chain copper→iron→…→starmetal** (ores/nodes already exist; add bars +
   smelt recipes + tools). Instantly deepens tools, mining, and Blacksmith with **content
   only**. This is the spine of the Beta crafter loop.
2. **Add craft quality + per-instance quality on `ItemStack`, rolled from crafter skill +
   LUCK.** One hook in `CraftingSystem` (mirroring the `StationAvailable` delegate pattern),
   one struct field. Biggest "depth per line of code" on the list.
3. **Give Professions a tier ladder + day-7 pick surfaced in the crystallization UI.** The
   offers are already computed and saved; this is mostly UI + an F–S map + routing profession
   XP from all production actions. Turns the existing label into a real identity.
4. **Add a repair recipe loop.** Durability fields exist but are a dead-end sink; repair
   closes the return-to-base loop cheaply and makes tool tiers matter.
5. **Enforce recipe unlocks** (`unlockedByDefault` + unlock from profession tier/quests).
   One check in `CanCraft`; converts the flat recipe list into a progression reward.

### Medium (Beta+ / first content expansion)
6. **Material traits pass-through** (Warm/Keen/Stout…) — the system-exploitation depth; small
   data + logic, but wants quality (#2) landed first.
7. **Station levels + `minStationLevel`** and **upgrade-by-adjacency** — reuses the station
   gate and placement system; gives workshops visible growth.
8. **One full secondary chain** (fiber→thread→cloth at a Loom, *or* herb→reagent→potion at an
   Alembic) to prove the multi-profession pattern beyond metal.
9. **`minToolTier` node gating** so harder veins demand better picks — ties tool tiers to
   gathering progression.

### Expensive / later
10. **NPC/board-driven economy + sinks** (special orders, meta-crafts that consume old gear,
    caravans) — the single-player stand-in for player interdependence. Big content + Trade
    systems work; the `trader` profession and Trade skill are the hooks.
11. **Named masterwork (Storied/Mythwarm) capstone** — per-item history + evolving trait;
    the S-tier profession capstone. Wants quality + traits + a per-instance item-history
    record; high-value but late.
12. **Crop traits / soil memory / seasons** (the farming depth in the Bible) — its own
    subsystem; valuable for Farmer but larger than the Beta crafter slice.
13. **Full station enclosure/coverage conditions** — cozy base-building polish, not needed for
    the core loop.

---

## 8. Sources

Clean-room: used for *mechanic shape only*, not names/IP/text.

- [Profession Progression Systems Reward Specialization (dogfighter-game.com)](https://dogfighter-game.com/profession-progression-systems-reward-specialization-over-general-crafting/)
- [10 Best Cozy Crafting Games (TheGamer)](https://www.thegamer.com/best-cozy-crafting-games/)
- [Best Cozy Games With Crafting Mechanics (GameRant)](https://gamerant.com/best-cozy-games-with-crafting-mechanics-ranked/)
- [Top 10 Survival Games with the Best Crafting Systems (AllKeyShop)](https://www.allkeyshop.com/blog/top-10-survival-games-with-the-best-crafting-systems-top-v/)
- [Survival Game Design — Principles, Examples, Template (gamedesignskills.com)](https://gamedesignskills.com/game-design/survival/)
- [Balancing survival gameplay and RPG progression in Conan Exiles (Game Developer)](https://www.gamedeveloper.com/design/balancing-survival-gameplay-and-rpg-progression-in-i-conan-exiles-i-)
- [20 Crafting Systems That Actually Feel Good (Fiction Horizon)](https://fictionhorizon.com/crafting-systems-that-actually-feel-good/)
- [Valheim Workbench Upgrades — Level 5 guide (GameSpot)](https://www.gamespot.com/articles/valheim-workbench-upgrades-how-to-enhance-your-crafting-station-to-level-5/1100-6488182/)
- [Valheim Workbench (Valheim Wiki / Fandom)](https://valheim.fandom.com/wiki/Workbench)
- [Diablo 4 Masterworking Guide (Icy Veins)](https://www.icy-veins.com/d4/guides/masterworking-guide/)
- [Dwarf Fortress — Item quality (DF Wiki)](https://dwarffortresswiki.org/index.php/DF2014:Item_quality)
- [RimWorld — Quality (RimWorld Wiki)](https://rimworldwiki.com/wiki/Quality)
- [Professions / cooperative crafting & economy (WAKFU official tutorials & forums)](https://www.wakfu.com/en/mmorpg/tutorials/424877-professions)
- [Zero Combat, Max Crafting — crafting-only LitRPG (Royal Road)](https://www.royalroad.com/fiction/137305/zero-combat-max-crafting-the-unkillable-saint)
- [The Nameless Engineer — survival-engineering LitRPG (Royal Road)](https://www.royalroad.com/fiction/151723/the-nameless-engineer-litrpg-progression-survival)
- [Professions in RPGlit — discussion (Royal Road forums)](https://www.royalroad.com/forums/thread/123166)
