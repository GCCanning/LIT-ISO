# LIT-ISO — Canonical Game Design Bible

> **Status: CANONICAL. Source of truth for design direction.**
> Supersedes `15_LitRPG_System_Bible.md` (cozy framing) and
> `17_Seven_Day_Trial_Game_Bible.md` (trial framing). Those two remain on disk
> as detailed reference for systems folded in here, but where they disagree with
> this document, **this document wins.**
>
> Established by owner direction, 2026-06. Engineering invariants and lane/owner
> rules still live in `AGENTS.md`; this is the *game* contract, not the build
> contract.

---

## 0. How this resolves the doc-15 / doc-17 conflict

The earlier review surfaced two competing bibles. The owner has resolved it:
**it is one game.**

- **The Seven-Day-Trial is the FRAME** (from doc 17): transmigration, the System,
  the 7-day Proving Week, the void class-assignment screen, F–S grading, classes
  + professions, distance-scaled danger and rarity.
- **The cozy survival-crafting systems are the SUBSTANCE that fills the frame**
  (from doc 15): crafting/farming/building depth, skill mastery, tactile
  progression, a world you make warmer and more personal.

The mood is **"survival game, like a LitRPG book."** Cozy is the texture; survival
pressure and visible LitRPG progression are the spine. The player is not here to
"become the strongest killer" *or* to live a consequence-free farm life — they are
a transmigrant who survives, prepares, scores well, and grows into a class and a
home through what they actually practice.

Where the owner's vision **adds new mechanics** not in either bible, or **overrides**
them, it is flagged inline with **[OWNER — NEW]** or **[OWNER — OVERRIDES]**.

---

## 1. Concept / Pitch

LIT-ISO is an **original isometric survival-crafting LitRPG** played as if you were
the protagonist of a transmigration progression-fantasy novel. You create a world
and difficulty, then meet the **System** — a world-spanning intelligence that
narrates your arrival, measures your actions, and eventually awards your class.

You wake transmigrated into another world with **seven days to prepare** before the
System assigns your identity. You gather, craft, fight, explore, dive shallow
dungeons, and help (or ignore) the people you meet. Everything is silently graded.
On day seven you are pulled into a floating void where the System presents your
**rank (F–S)** and a set of **class choices** scaled to how well you did. From there
the long game opens up: a hand-built homestead/guild, distance-gated dungeons and
towns, affinity-flavoured magic, ocean crossings to harder continents, and a world
of purposeful NPCs.

**Pillars** (merged from doc 17 §"Non-Negotiable Design Pillars," reaffirmed by owner):

1. **The first seven days determine identity.** You earn class options through
   action, not a character-creation menu.
2. **The obelisk/void assignment is the first major milestone** and should feel
   consequential.
3. **Grade controls class rarity and starting power.** Better scores unlock rarer
   classes and more starting skill points. **[OWNER — OVERRIDES doc 17]** doc 17
   only tied grade to *class rarity*; the owner adds that grade also grants more
   **initial skill points** (see §4).
4. **Class and profession are separate** — combat identity and economic/crafting
   identity both matter.
5. **Progression is use-based.** Doing class/profession/affinity-relevant actions
   improves that path.
6. **Distance equals danger and rarity.** Farther from the safe starter town =
   harder enemies, deeper/higher-rank dungeons, better loot, higher-tier towns.
7. **Preparation beats raw stats.** You win because you packed, scouted, traded,
   crafted, rested, and chose risks well — overnight travel, camping, and dungeon
   prep are required systems, not flavour.
8. **Stats are mechanical, not cosmetic.** **[OWNER]** DEX = faster (movement,
   and per current build, cooldowns); STR = more damage **and jump higher**.

**Visual / UX target** **[OWNER]**: clean, minimalist UI. The character/creation
screen is laid out and styled **like Stardew Valley**. The recurring signature
moment is **the player floating in space looking at the System**, which speaks in
short System messages.

**Art direction — LOCKED** **[OWNER, 2026-06]**: the game uses the **PixelLab-generated
tiles, props, and assets** as its canonical art set. Earlier parallel experiments
(LoRA/ControlNet tile passes, Greenwake, Reference32, black-mage iterations) are
**scratch, not the art direction** — the PixelLab pool is what we build with. New art
fills gaps *to the PixelLab style*.

---

## 2. The Opening — System Boot & Transmigration

**[OWNER]** Flow:

1. **World setup** — player creates the world and chooses difficulty.
2. **Character creation** — Stardew-style layout; clean minimalist UI. The focus is
   the player floating in space, facing the System.
3. **System speaks** (the void intro): *"You have been transmigrated to another
   world. You have 7 days to prepare. Good luck."*
4. **First load-in** — the world boots up around you "like your system is booting
   up" (a literal System-boot aesthetic on first entry).
5. The Proving Week begins immediately.

This already has partial implementation: `TransmigrationIntro.cs` (typed System boot
lines + glitch/double-vision wake, armed only for fresh worlds) and the
`ClassAssignmentView` void ceremony. Keep these aligned to the Stardew-clean,
floating-in-void aesthetic.

Carry over doc 17's opening discipline: System messages are **short and skippable**;
the first real action happens within ~1 minute; temporary authority unlocks Status,
Inventory, Map, and a Trial Log; the player sees the System react before reaching
the first town.

Initial state (doc 17, retained):

```text
Name: Unknown
Origin: Unregistered
Status: Transmigrant / Unwritten
Class: Pending
Profession: Pending
Trial Duration: 7 Days
Grade: Unassigned
System Authority: Provisional
```

---

## 3. The Seven-Day Proving Week (the first loop)

For seven in-game days, **every meaningful action is silently scored** by the System
(the "Ledger" frame from doc 17 — combat, gathering, building, crafting, trading,
travel, healing, magic use, risk-taking, survival decisions, social choices, dungeon
activity). The build already has a Trial scoring data spine (Evidence, XP channels,
Marks/behavioral tags, Titles) validated in `06_Validation_Report.md`.

**Daily texture** (cozy core loop from doc 15, retained):

| Phase | Player action | Reward |
|---|---|---|
| Dawn prep | eat, check board, pick tools, choose a goal | buffs, route clarity |
| Work | gather, farm, mine, chop, fish, build, craft | materials, skill XP, discoveries |
| Explore | push into woods, caves, ruins, beaches, snow | map unlocks, rare nodes, dungeon hints |
| Encounter | avoid, fight, trap, soothe, or befriend mobs | drops, recipes, ecology shifts |
| Return | refine goods, upgrade home, fulfill requests | progress, trust, score |
| Evening | cook, decorate, journal, plan, **camp if travelling** | next-day bonuses, survival |

**Survival pressure during the week** **[OWNER — clarifies the open "survival scope"
question from doc 07]**: night is dangerous (see §8). Travel takes real time. The
week is a genuine *prepare-and-survive* window, not a sandbox tutorial.

> **Open question:** exact scored action weights and the day-length (real minutes per
> in-game day) are not specified by the owner. Tune during first-hour playtests.

---

## 4. Class Assignment & Post-Trial Progression

### 4.1 The void assignment (end of day 7)

**[OWNER]** At the end of the seventh day the player is brought back to the floating
void screen. The System presents:

- the player's **overall rank, F–S**, derived from the week's scored actions;
- a set of **class choices** whose rarity ceiling scales with rank (higher rank =
  rarer, stronger, more specialized options).

This is the `ClassAssignmentView` ceremony already built (rank display, axis
receipts, offers with rarity colours + evidence receipts). Bind the real scoring →
offer generation runtime to it (currently a TODO).

### 4.2 Class + Profession

After assignment the player holds **one Class** and **one Profession**, both
leveled by use (doc 17, retained):

- **Class** = combat/adventuring identity (e.g. Ranger, Warden, Mage, Duelist,
  Cleric, Beast Tamer). Advances through class-relevant action (a Ranger via
  scouting, tracking, bow use, ambushes, wilderness survival).
- **Profession** = economic/crafting identity (e.g. Blacksmith, Alchemist, Cook,
  Builder, Trader, Farmer). Advances by doing the craft (a Blacksmith via smelting,
  repairing, forging, fulfilling orders).

> **Reconciliation note:** doc 15's seven **Callings** (Hearthwarden, Greenhand,
> Stonewright, Threadsmith, Pathlighter, Bramblebound, Lanternblade) and their
> branches/capstones map onto this Class+Profession model — most are *profession*
> identities (Greenhand→Farmer, Stonewright→Builder, Threadsmith→Smith/Tailor,
> Hearthwarden→Cook) while Lanternblade/Pathlighter lean *class*. The build's
> existing Calling data can be retained as the profession layer and/or the
> pre-assignment "tendency" tags. The Stardew-clean class roster (Ranger/Mage/etc.)
> is the combat layer the owner named. **Open question:** keep doc-15 Calling names,
> adopt doc-17 generic class names, or run both (Calling = profession, Class =
> combat)? Recommend the last; confirm with owner.

### 4.3 Skill points

**[OWNER — NEW vs both docs]** Everyone starts with **1 skill point + a starter
class**. The number of starting skill points **increases with trial score** (higher
rank = more points). Points are spent in the skill web (`SkillWebView` /
`FoundationSkillWeb` already scaffolded). This is the concrete payoff that makes the
7-day grade matter mechanically beyond class rarity.

> **Open question:** how skill points accrue *after* the trial (per level? per
> milestone?) is unspecified. Doc 15's reward cadence (skill tick every ~20 min,
> branch every 3–5 sessions) is a reasonable default to propose.

### 4.4 Affinities

**[OWNER — NEW, only lightly present in build's affinity data]** Affinities give
magic and skills a **flavour and effect**:

- ice = slow / debuff
- fire = burn (damage-over-time tick)
- poison = damage-over-time / debuff
- (others to follow the same "element → effect verb" pattern)

Rules:

- **Higher rank = higher affinity ceiling** (the cap an affinity can reach scales
  with trial grade).
- **Affinities grow through usage** and through **rare drops / finds**.

The build already has elemental affinity ranks/scaling in the `ability-affinity-core`
slice — extend it to match this design (per-element effect verbs, usage growth,
drop-based growth, rank-gated ceiling).

### 4.5 Stats

Classic six stats are retained from doc 15 (STR/DEX/INT/VIT/DEF/LUCK) and the HUD
stays simple. **[OWNER — OVERRIDES the "cozy, soft" stat framing of doc 15]** stats
are **mechanically significant**:

- **DEX** = faster generally. (Current build: +move speed and −ability cooldowns per
  DEX point — keep.)
- **STR** = more damage **and jump higher**. **[OWNER — NEW]** STR affecting jump
  height is a new coupling; wire STR into `IsoFoundationPlayer` jump.
- INT/VIT/DEF/LUCK keep their doc-15 roles (mana/recipe discovery; HP/stamina/weather
  resist; damage reduction; rare-drop odds) unless playtests say otherwise.

### 4.6 Item rarity

Doc 15's 8-tier quality ladder (Plain→Mythwarm) and material traits are retained as
the itemization spine. **[OWNER]** item **rarity is gated by source**: higher-rank
**towns** and higher-rank **dungeons** (and distance from spawn) yield rarer items at
higher cost (see §5, §9).

---

## 5. World & Settlements

**[OWNER]** The world is filled with **random towns of differing tiers**:

```text
campsite  →  village  →  town  →  city
```

- **Higher town rank = higher item rarity, higher cost, and higher accessibility**
  of advanced goods/services.
- Town rank correlates with **distance from the starter town** (the safe spawn).

**Markets / town services** **[OWNER]** — each settlement offers some subset of:

- General store
- Guild
- Library
- Alchemist
- Forge

This supersedes doc 15's loose village/faction name pools, which are **retained only
as a naming reservoir** (Mosswake, Hearthmere, Brindlewick, etc.) for procedurally
generated towns. The doc-15 faction list is likewise demoted to flavour until/unless
a faction system is designed.

> **Open question:** do town *services* scale with town tier (e.g. only cities have a
> high-rank forge)? The owner's "higher rank = higher accessibility" implies yes —
> confirm the service-by-tier matrix.

---

## 6. Guild Building (player base)

**[OWNER — NEW; this replaces doc 15's generic "homestead → settlement" building
ladder as the primary base-building system]**

- The player can **build their own guild**.
- **Upgrading the guild's rank enlarges its interior.**
- The interior is laid out with the **biome-sketch paint-the-grid tool**: you
  **paint the grid with tiles from your inventory**. The grid **expands as the guild
  ranks up**.
- **Craft stations get better bonuses when placed inside your guild**, and those
  bonuses **improve as guild/levels rise**.

This is a major, concrete system. It reuses the existing BiomeSketch paint-the-grid
tooling and the existing pocket-instance/interior tech (taverns, library already
build enterable interiors). The doc-15 building categories (shelter/work/storage/
travel/safety/community/ecology props) and the building-progression feelings are
**retained as the palette of paintable/placeable objects** inside the guild and the
world — but the *structure* is now "rank-gated guild interior you paint," not a fixed
Camp→City personal homestead track.

The WORKSTATION_ROADMAP station chains (Furnace/Tannery shipped; Loom/Anvil/Alchemy/
Mill/Well proposed) feed directly into "craft stations with guild bonuses."

> **Open questions:** How is guild rank raised (currency? quests? materials?)? Is the
> guild a single fixed plot, or can it be sited/moved? Does "build your own guild"
> coexist with joining the town **Guild** service in §5, or are they the same
> institution at different scales? Recommend: town Guild = quest/board hub; player
> guild = your buildable base that can affiliate with it. Confirm with owner.

---

## 7. Travel

**[OWNER]** Travel across biomes toward dungeons should **feel like an adventure** —
deliberately **not too quick**.

- A journey **may require travelling overnight**, which means you must bring a
  **camping set + campfire** (see §8).
- **Horses** arrive eventually as a mobility upgrade. **[OWNER — NEW]** (Not yet in
  build; future milestone.)
- **Boats / ocean crossings** **[OWNER — NEW]**: the **starter continent is
  surrounded by water**. You **build a boat to cross the ocean** to harder places
  (higher-rank continents/regions). This is the macro-progression gate between
  difficulty tiers of the world.

This makes movement speed (DEX) and preparation meaningful, and frames exploration as
expeditionary rather than instant.

> **Open question:** is overworld travel continuous (walk the whole way) or are there
> fast-travel/route systems once discovered (doc 15's Pathlighter "Safe Route"
> capstone)? Owner says "not too quick" and "may need to travel overnight," implying
> continuous travel at least for first crossings.

---

## 8. Camping, Night, and Danger

**[OWNER — NEW, central survival mechanic]**

- **At night, more powerful and aggressive mobs come out and actively hunt the
  player.**
- The player is safe from them **only when at their campfire, within its radius.**
- From within the campfire's safe radius the player **sleeps in their tent to pass
  the night more quickly.**
- **Camping sites / gear have a rarity factor.** If you camp in a **too-powerful
  area with weak camping gear**, there is a **chance a powerful mob ignores the
  "wards," attacks you, and damages your camping gear.**

Design implications:

- Campfire = a placeable that projects a **safety radius** at night. (Build already
  has craftable animated campfire/fireplace props — extend with a night-ward radius.)
- Camping gear has a **tier/rarity** that must match or exceed the **area's danger
  tier**; mismatch introduces a ward-failure chance with gear-damage consequences.
- Tent = the sleep-to-skip-night object, usable only inside the safe radius.

This ties directly into §7 (overnight travel) and §9 (dungeon expeditions): you
prepare camp gear appropriate to where you're going.

> **Open question:** does ward-failure scale continuously with the gear-vs-area gap,
> or is it a flat chance past a threshold? Unspecified — propose a scaling chance.

---

## 9. Dungeons & Expedition Prep

**[OWNER]**

- **Dungeons span ranks**; **further from the spawn town = higher-rank dungeon.**
  (Build already does distance-tiered portals + deterministic procedural rooms.)
- **Dungeons are sparse**, with **hints via towns/quests** to find them — they are
  discovered, not handed to you.
- You **must prepare in advance**: **food, water, potions, sharpening stones for
  tools, mana stones**, etc. **[OWNER — NEW consumable types]** — water as a
  resource, tool-sharpening stones, and mana stones are new prep items to add.

This reframes the existing dungeon system: keep the deterministic layout/rewards/
portal-history tech, but (a) make portals **rare and hint-gated** rather than freely
spawned, and (b) require a **prep checklist** (doc 17's "dungeon preparation
checklist" UI) gated on consumables the player must craft/buy/carry.

Item rarity inside a dungeon scales with its rank (§4.6).

> **Open question:** are dungeons one-shot (cleared then gone) or persistent/
> repeatable? Build has portal "fresh/claimed/cleared" states implying persistence;
> owner says "sparse" — likely persistent-but-rare. Confirm.

---

## 10. NPCs

**[OWNER]** The world should have **purposeful NPCs who interact with you**, created
with the **existing character creator**. "Purposeful" = they have a role/reason to
interact (quests, trade, services, hints toward dungeons), not ambient set-dressing.

Doc 15's NPC name/role pools and neighbor-quest framework are **retained as content
seed material**. The existing CharacterCreator tooling is the authoring path for NPC
appearances (it's already wired into New Game per the ledger).

> **Open question:** scope of NPC interaction (full dialogue trees? request boards +
> shops only?) is unspecified. Owner emphasizes *purpose* — recommend starting with
> service NPCs (the §5 market roles) + quest/hint givers before any deep social sim.

---

## 11. Biomes & Weather

**[OWNER]** Biomes are **generated with appropriate rules**, and **each biome has its
own weather effects.**

This is exactly the in-flight "organic world generator" work (6-layer generation,
Whittaker climate table, elevation/lapse-rate gating, adjacency rules, blend bands —
see `WORLDGEN_RULES_PROPOSAL.md`) plus the imported Pixel Weather particles. The
canonical target:

- Rule-driven biome placement (no random palette scatter).
- Per-biome weather: e.g. meadow/forest light rain, snow biome snowfall, desert/beach
  heat shimmer, mountain fog + wind (mapping from `from-claude.md` showcase spec).
- Weather should interact with survival/travel where sensible (visibility, comfort).

Doc 15's named biome set (Mosswake Meadow, Brindlecap Woods, Sunspool Fields, Duskwick
Marsh, Honeyshale Cliffs, Kindlestep Badlands, Winterwool Pines, Glowcap Grotto) and
tile-name groups are **retained as the biome/tile naming layer** for the generator.

---

## 12. Progression Summary (one-screen reference)

```text
TRANSMIGRATION  →  PROVING WEEK (7 days, all actions scored)
                         │
                  DAY-7 VOID ASSIGNMENT
                  ├─ Rank F–S  (from scored actions)
                  ├─ Class offers  (rarity ceiling scales with rank)
                  └─ Starting skill points  (count scales with rank)
                         │
                  LONG GAME
                  ├─ Class (combat) + Profession (craft), use-based growth
                  ├─ Skill points → skill web
                  ├─ Affinities (element → effect; usage + rare-drop growth; rank-capped)
                  ├─ Stats matter (DEX=speed, STR=damage+jump, …)
                  ├─ Build & rank up your guild (paint-grid interior, station bonuses)
                  ├─ Towns: campsite→village→town→city (rank = rarity/cost/access)
                  ├─ Travel = adventure (overnight, camp gear, night hunters, wards)
                  ├─ Dungeons: rare, hint-gated, rank-by-distance, prep-required
                  ├─ Build a boat → cross ocean → harder continents
                  └─ Horses (future mobility)
```

---

## 13. What is retained, added, or overridden (quick map)

| Topic | Source | Disposition |
|---|---|---|
| Transmigration, System, 7-day trial, void ceremony | doc 17 | **Retained as the frame** |
| F–S grading; Class + Profession; use-based growth | doc 17 | **Retained** |
| Distance = danger + rarity | doc 17 + owner | **Retained, reaffirmed** |
| Crafting/farming/building/tools depth; skill trees; item tiers; quest types | doc 15 | **Retained as substance** |
| Six stats (STR/DEX/INT/VIT/DEF/LUCK), simple HUD | doc 15 | **Retained** |
| Callings + branches/capstones | doc 15 | **Demoted to profession layer / tendency tags** (reconcile, §4.2) |
| Cozy "no-combat-needed, soft stats" tone | doc 15 | **Overridden** — survival pressure + mechanical stats are core |
| Personal Camp→City homestead ladder | doc 15 | **Replaced** by rank-gated **paintable guild** (§6) |
| Starting skill points scale with rank | owner | **NEW** |
| Affinities (element→effect, usage+drop growth, rank ceiling) | owner | **NEW** (extend existing affinity slice) |
| STR → jump height | owner | **NEW** |
| Town tiers w/ market services (store/guild/library/alchemist/forge) | owner | **NEW** (supersedes loose village pools) |
| Build-your-own guild w/ paint-grid interior + station bonuses | owner | **NEW** |
| Night hunters + campfire safe-radius + tent skip + ward-failure gear damage | owner | **NEW** |
| Dungeon prep consumables (water, sharpening stones, mana stones) | owner | **NEW** |
| Boat-gated continents; horses | owner | **NEW** |
| Rule-based biomes w/ per-biome weather | owner | **Reaffirms in-flight worldgen + weather work** |

---

## 14. Consolidated open questions (for owner sign-off)

1. **Class vs Calling naming** — keep doc-15 Callings as professions and add generic
   combat Classes, or pick one vocabulary? (Recommend: Calling=profession,
   Class=combat.)
2. **Trial scoring weights** and **in-game day length**.
3. **Post-trial skill-point accrual** (per level? per milestone?).
4. **Affinity ward-failure / growth curves** — exact scaling.
5. **Town services by tier** — does the service set scale with town rank?
6. **Guild specifics** — how rank is raised; fixed vs. movable plot; relationship to
   the town "Guild" service.
7. **Overworld travel** — fully continuous, or fast-travel after discovery?
8. **Dungeon persistence** — one-shot vs. repeatable.
9. **NPC interaction depth** — services + hints first, or dialogue sim?
10. **Survival meters** — does the owner want explicit food/water/energy bars (water
    is now a dungeon-prep item, implying at least thirst), or only situational
    pressure (night, travel, camp)?

---

## 15. Supersession notice

This document **supersedes** `15_LitRPG_System_Bible.md` and
`17_Seven_Day_Trial_Game_Bible.md` as the canonical design direction. Those files are
kept for their detailed system tables (skill nodes, item traits, quest seeds, name
pools, trial-spine definitions) which this bible folds in by reference. When any
conflict arises, **`00_Canonical_Game_Design.md` is authoritative.** Engineering
invariants remain governed by `AGENTS.md` (note: the `maxWalkStepHeight` invariant is
under active revision — see `Impact_Analysis_2026-06.md`).
