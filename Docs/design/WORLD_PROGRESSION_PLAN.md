# LIT-ISO — World Progression & Living Settlement Design Plan

> Status: design proposal, v1. Builds on the existing `IsoCoreFoundation` content/runtime, the `BiomeSketch` browser editors, and the current worldgen samplers. Nothing here replaces shipped systems — every new mechanic hooks an existing one. Author/preview path is the existing `settlement_editor.html` / `interior_editor.html` / character tools, which already export JSON that folds into `FoundationContent` and the samplers.

---

## 1. Vision & Pillars

LIT-ISO is a 2D isometric survival/crafting/RPG where the player climbs a **diegetic litRPG power ladder** while the world around them — settlements, NPCs, professions, economy — behaves like a **living Minecraft-style village** that grows from a lone campsite into a walled city. The fantasy is "watch a frontier civilize as you grow strong enough to matter to it." The System narrates your climb; the village reacts to it.

**Pillars**

1. **The climb is legible.** Levels, stats, skills, classes, **Adventurer Ranks (F→S)**, titles, and affinities are all shown through a single diegetic "System" feed. The player always knows what got stronger and why. (Hooks: `FoundationProgression`, `SystemMessageFeed`, `FoundationPlayerStats`.)
2. **Mastery, not just numbers.** Skills level through use and *evolve* into named successors; the player climbs an **Adventurer Rank ladder (F → E → D → C → B → A → S)** every band of levels. Higher levels stay meaningful via multiplicative rank jumps, not flat +1s.
3. **Settlements are organisms.** A place is defined by its *building count and tier* (campsite → city). Growth is earned, not spawned. Each tier unlocks professions, services, and defenses.
4. **NPCs have jobs, routines, and opinions.** Professions bind to claimed job-site blocks; NPCs work/gather/sleep on schedules and gossip about the player, shifting prices and reception.
5. **Everything is previewable before it ships.** Any settlement, interior, or NPC appearance can be authored and seen in the browser tools, exported as JSON, and folded into the game with no code change for content.

---

## 2. Progression System (litRPG)

The progression backbone **already exists and is mature** — this section formalizes and extends it rather than inventing it. Current runtime (`FoundationProgression.cs`) already tracks: a selected **Calling**, calling XP/level, **skills that gain XP by activity**, **quests**, **titles** (threshold-based), **affinities** (ranked, with effect multipliers), **XP channels**, a **7-day Trial** that ends by *offering* a class + profession based on accumulated evidence, and a **System message feed** with typed channels.

### 2.1 Levels & XP

- **Calling level** is the headline level. XP is flat per level (`XpPerProgressionLevel = 100`) — keep this linear *within* a rank; the felt power curve comes from rank-ups (F→S, §2.5) and skill evolutions, not from inflating per-level XP.
- XP sources route through `AddActivityXp(activity, amount, skillIds…)`, which already: (a) adds calling XP, (b) adds XP to the matching skill(s) by `FoundationProgressionActivity`, and (c) adds half to `FoundationPlayerStats` experience. Keep this single chokepoint; new content just calls it with the right activity.
- **XP source table:**

| Activity | Example source (existing systems) | Routed skill family |
|---|---|---|
| Harvesting | Trees/rocks/ore veins, bushes/cacti (`ResourceNode`) | Gathering, Mining |
| Combat | deer/fox/slime, dungeon mobs (`MobDatabase`) | Weapon/spell skills |
| Crafting | Furnace/Tannery/Workbench timed jobs | Smithing, Tanning, Cooking |
| Farming | Crops reaching harvest (`CropDefinition`) | Farming |
| Building | Plot → building construction (`tavern_plot` etc.) | Construction |
| Exploration/Dungeon | Portals, dungeon clears (`DungeonResultDefinition`) | via `ApplyDungeonResult` XP grants |
| Social | Trade, quest turn-ins, festivals (new) | Bartering, Renown |

### 2.2 The System UI / notifications

`SystemMessageFeed` + `SystemMessageChannel` already exist and are used for Title/Affinity/Dungeon events. Standardize the diegetic voice across all gains:

- **Notification grammar:** boxed, terse, second-person. `[ Skill Up ] Woodcutting → Lv. 12`, `[ Rank Up ] You have advanced to Rank D.`, `[ Skill Evolution ] Steady Strike → Cleaving Strike.`, `[ Title Acquired ] Hearth-Keeper.`
- **Channels to add** to the existing enum: `LevelUp`, `RankUp`, `SkillEvolution`, `Reputation`. Reuse `TitleAcquired`/`AffinityResonance`/`DungeonAlert` as-is.
- Priority field already exists (the `Queue(channel, text, source, priority)` signature). Rank-ups = highest priority (full-screen flourish); skill-ups = low (transient toast).

### 2.3 Skills: level-through-use + evolutions

Skills already gain XP by activity and carry an `unlocks[]` array and `FoundationSkillNodeKind`. Extend `FoundationSkillDefinition` with an **evolution link**:

- Add `string evolvesToSkillId` and `int evolveAtLevel` (e.g. 25). When a skill crosses `evolveAtLevel`, the runtime swaps tracked XP into the successor skill (carrying overflow), fires a `SkillEvolution` System message, and grants the successor's first ability. This is the classic Royal Road "Cleave (Lv 25) → Whirlwind Cleave" beat.
- Evolutions are **named, not numbered**, to feel like power-system milestones.
- Abilities (`FoundationAbilityDefinition` / `FoundationAbilitySystem`) are the *payload* of skill unlocks: existing `steady_strike, guard_step, mana_bolt, ember_spark, root_snare, stone_skin` become the rank-1 unlocks; evolutions grant rank-2 successors.

### 2.4 Combat classes (party composition) & their rank evolutions

**Class = combat identity.** The player picks **exactly one** combat class at the end of the 7-day Trial, chosen from the classic litRPG party four: **Knight, Tank, Rogue, Mage**. Together they are the "perfect party" archetype set — bruiser DPS, defender, agility/crit striker, arcane caster. A class is *not* a soft tag: it defines starting stats, the starting ability loadout, and a rank-gated **class evolution tree**. (Professions are a fully separate track — see §2.7.)

Mapped to code: `FoundationCallingDefinition` is reused as the class definition (`startingTitle`, `branchIds`, `starterSkillIds`, `statBonuses`, `capstone`). The default `greenhand` calling remains the *pre-Trial* unclassed state; the Trial's `CompleteTrial` → class offer resolves it into one of the four. `starterSkillIds` map to the **existing abilities** (`steady_strike, guard_step, mana_bolt, ember_spark, root_snare, stone_skin`). `branchIds` become the class evolution branches selected via `SelectCallingBranch` at rank thresholds.

**The four starting classes:**

| Class | Role / fantasy | Core stats | Starting skills (existing abilities) | Evolution tree (rank-gated branches) |
|---|---|---|---|---|
| **Knight** | Melee bruiser / sustained DPS; the front-line attacker who trades blows and carries the kill pressure | STR / VIT (offense-leaning) | `steady_strike`, `guard_step` | **C:** Blademaster *(crit/combo DPS)* or Vanguard *(weapon-master bruiser)* → **A:** Warlord → **S:** Sword Saint |
| **Tank** | Defender / aggro anchor; soaks damage, holds the line, controls enemy attention | VIT / DEF (defense-leaning) | `stone_skin`, `guard_step` | **C:** Guardian *(taunt/shield)* or Sentinel *(retaliation/reflect)* → **A:** Bulwark → **S:** Aegis Lord |
| **Rogue** | Agility / crit / stealth striker; burst from concealment, mobility, control | AGI / DEX (speed-leaning) | `root_snare`, `guard_step` *(dodge/reposition — placeholder agility skill `shadow_step` to be added)* | **C:** Assassin *(crit/stealth burst)* or Ranger *(ranged/trap kiting)* → **A:** Nightblade → **S:** Phantom |
| **Mage** | Arcane DPS / utility; ranged elemental damage, crowd control, and the party's **support/sustain** branch | INT / MND (caster) | `mana_bolt`, `ember_spark` | **C:** Elementalist *(burst AoE DPS)*, **Spellblade** *(hybrid melee-caster)*, or **Cleric** *(heal/buff support)* → **A:** Archmage / Hierophant → **S:** Magus / Saint |

> **New ability to author:** Rogue needs a dedicated agility starter (proposed `shadow_step` — a short dash/dodge/reposition). Until it exists, Rogue opens with `root_snare` + a borrowed mobility use of `guard_step`. This is the only net-new ability the class set requires; everything else maps to shipped abilities.

**The missing healer — solved on the Mage branch + professions.** A 4-of-{Knight, Tank, Rogue, Mage} party has no dedicated healer by default. Three overlapping answers:
1. **Mage → Cleric** is an explicit class-evolution branch at Rank C (heal/buff/cleanse), so a party that wants sustain specs one of its Mages into the support role. Archmage vs. Hierophant/Saint at Rank A is the "DPS caster vs. support caster" fork.
2. **Potions & food** cover baseline out-of-combat and emergency sustain, sourced from the **Herbalist / Cook / Innkeeper professions** (§2.7) — so sustain is a *crafting/economy* answer, not only a class slot. This is deliberate: it makes professions matter to combat.
3. **Party synergy** is built around it: Tank holds aggro → Knight/Rogue burst → Mage controls/sustains. Each class is intentionally incomplete alone, which is the litRPG "perfect party" fantasy and a reason for co-op/NPC-companion play.

**Class evolution = a rank-gated breakthrough.** Branch picks (`SelectCallingBranch`) unlock at Rank **C** (first specialization), Rank **A** (advanced form), and Rank **S** (capstone, the `capstone` field). Each evolution grants a named successor ability via the skill-evolution system (§2.3) and is announced on the `RankUp`/`SkillEvolution` System channels.

### 2.5 Adventurer Rank ladder (F → S)

**Rank is the combat power gate the player climbs.** It replaces the old Novice→Mythwarm tier names with a litRPG-standard **Adventurer Rank ladder: F → E → D → C → B → A → S** (with optional **SS / SSS / EX** as post-game). Numeric **levels live *within* each rank**; crossing a rank boundary is the *ceremony*. Rank — not raw level — gates skill evolutions, class evolutions, and access to higher-tier dungeons, towns, and zones. Keep all rank flavor **combat-oriented** (the player is an adventurer climbing in martial power), distinct from profession ranks (§2.7).

| Rank | Level band | Stat multiplier | Unlocks at this rank (combat power gate) |
|---|---|---|---|
| **F** | 1–5 | ×1.00 | Adventurer registered; class chosen at end of Trial; starter skills seeded. Access: campsite/hamlet zones, F-rank dungeons (Rootcellar-tier). |
| **E** | 6–15 | ×1.25 | +1 ability slot; first power spike. Access: village zones, E-rank dungeons; basic guild-board contracts. |
| **D** | 16–30 | ×1.50 | Skill `evolveAtLevel` thresholds come online (named skill evolutions). Access: town zones, D-rank dungeons; mid-tier portals. |
| **C** | 31–50 | ×1.80 | **First class evolution branch** (`SelectCallingBranch`). Access: city zones, C-rank dungeons; regional travel. |
| **B** | 51–70 | ×2.20 | Second skill-evolution wave; NPCs greet by title. Access: B-rank dungeons, frontier zones; settlement reputation cap raised. |
| **A** | 71–90 | ×2.60 | **Advanced class evolution.** Affinity Perfect ranks reachable. Access: A-rank dungeons, elite zones; named-boss content. |
| **S** | 91+ | ×3.00 | **Class capstone** (the `capstone` field); unique titles; you "affect the world." Access: S-rank dungeons, endgame zones. |
| *SS / SSS / EX* | *post-game* | *×3.5 / ×4.0 / ×5.0* | *(optional endgame ranks) — mythic content, world-boss raids, prestige titles. Reserve for a post-launch power tier.* |

**Avoiding "+10 stats means nothing at 100":** apply the **per-rank multiplier** (×1.00 → ×3.00 column) to stat-derived combat numbers inside `FoundationPlayerStats`, so each rank-up re-scales the whole sheet rather than adding a flat increment. This is the Royal Road "rank jumps reset the power baseline" pattern. Mob/dungeon power bands are tuned per rank (the F–S dungeon access column) so content stays threatening as the player climbs.

**Code rename — `FoundationCallingTier` Novice→Mythwarm ⇒ F→S.** The enum in `FoundationTypes.cs` is currently `enum FoundationCallingTier { Novice, Adept, Artisan, Luminary, Mythwarm }` (5 values, gated at levels 6/16/31/51 in `FoundationProgression.TierForLevel`). The 7-rank ladder above adds two bands, so the enum should be **expanded and renamed**. Recommended target enum (`FoundationAdventurerRank { F, E, D, C, B, A, S }`, optionally `+ SS, SSS, EX`). The **5→7 mapping** of the *existing* values is:

| Current enum value | Old level gate | New rank | New level gate |
|---|---|---|---|
| `Novice` | 1–5 | **F** | 1–5 |
| `Adept` | 6–15 | **E** | 6–15 |
| `Artisan` | 16–30 | **D** | 16–30 |
| `Luminary` | 31–50 | **C** | 31–50 |
| `Mythwarm` | 51+ | *split into* **B / A / S** | 51–70 / 71–90 / 91+ |

So `Novice→F, Adept→E, Artisan→D, Luminary→C`, and the old top tier `Mythwarm` splits into the three high ranks **B/A/S** (new level gates 51/71/91). Update `TierForLevel` to the seven-band thresholds above and rename the field `startingTier`→`startingRank` on `FoundationCallingDefinition`. (C# enum identifiers can't be bare letters `F`/`E`/… without a prefix issue — they're valid single-letter identifiers, so `FoundationAdventurerRank.F` etc. compile fine.)

### 2.6 Professions (separate track from combat class)

**Professions are a fully separate progression track from your combat class.** They are **use-leveled** gathering/crafting/social roles — you get better by *doing the work*, not by spending combat XP — and each is tied to a **job-site block/station**. They level on the **same F→S scale** as Adventurer Rank, but as an independent meter per profession, and they grant **crafting/economy perks** rather than combat power.

**The rule, stated plainly:**
- The **player picks ONE combat class** (Knight/Tank/Rogue/Mage) at the end of the Trial.
- The player can **practice MANY professions** in parallel, each leveling independently F→S through use.
- **NPCs are defined primarily by a profession** (the job-site they claim, §5.1) **+ an optional combat rank** (guards/adventurer NPCs).

Mapped to code: reuse `ProfessionDatabase` (each profession has a `primaryActivity` + `progressionSkillIds`). Profession rank is driven by the matching `FoundationProgressionActivity` XP already routed through `AddActivityXp` (§2.1) — e.g. Smithing activity at the Furnace levels the Blacksmith profession. Each profession binds to an existing station/block:

| Profession | Job-site / station (existing) | Track / activity | Sample perks by rank (F→S) |
|---|---|---|---|
| **Blacksmith** | Furnace + forge/anvil | Crafting (Smithing) | Better tool/weapon tiers; lower craft time; rare-material recipes at A/S |
| **Tanner** | Tannery station | Crafting (Leatherwork) | Higher leather yield/quality; armor recipes |
| **Farmer** | farm plots, scarecrow | Farming | Crop yield/speed; rare seed unlocks; quality produce |
| **Innkeeper** | tavern building/interior | Social | Better rest buffs; tavern jobs; lodging income |
| **Merchant** | shop / market_stall | Bartering | Better buy/sell margins; rarer stock; caravan access |
| **Scholar** | library | Study (Lore) | Skill tomes; faster skill XP; appraisal/identify |
| **Herbalist** | herb plots / forage nodes | Gathering | **Potions (healing/cure/buff)** — covers party sustain (§2.4); reagent yield |
| **Miner** | ore veins / mine | Mining | Ore yield/quality; rare-vein detection; bar refining |
| **Cook** | campfire / CookingPot / fireplace | Crafting (Cooking) | **Buff foods & sustain** (§2.4); festival dishes; stamina/HP regen meals |

Profession rank-ups fire on the `RankUp` System channel too, with profession flavor (`[ Profession Rank Up ] Blacksmith → Rank C`), kept visibly distinct from combat-rank ceremonies. Because Herbalist/Cook/Innkeeper produce the potions and buff-foods, the **party's missing-healer problem is partly an economy/profession answer**, not only a class slot (§2.4).

### 2.7 NPCs as leveled entities

NPCs reference the same System. Give each NPC a lightweight `npcLevel`, a **primary profession tag** (the job-site they claim, reusing `ProfessionDatabase`), and an **optional combat Adventurer Rank** for fighters (guards, wandering adventurers). This pays off three ways: (1) gossip can say "the blacksmith reached profession Rank C"; (2) higher-tier settlements legitimately field higher-rank guards/NPCs (see §4 defenses); (3) the player can perceive relative power diegetically (`[ Appraisal ] Town Guard — Rank D`).

---

## 3. NPC System

### 3.1 Generation pipeline (appearance)

NPC bodies reuse the **player appearance stack** already in code: `FoundationCharacterAppearance(SaveData/Definition)` + `FoundationCharacterAppearanceCatalog`, authored by the browser tools `Tools/CharacterCreator`, `Tools/FarmerCharacterBuilder`, and `Tools/StardewNpcSheetGenerator`.

Pipeline:

1. **Author** in `CharacterCreator` / `StardewNpcSheetGenerator`: pick `bodyType`, `skinPaletteId`, `hairId`+palette, `outfitId`+palette; the tool emits a `FoundationCharacterAppearanceSaveData` JSON + a sprite sheet (`directionCount`, `framesPerRow`, `spriteResource`).
2. **Catalog** the appearance into `FoundationCharacterAppearanceCatalog` with a stable `id` and `provenanceId` (already a field — use it to trace which tool/seed produced the sheet).
3. **Bind** to an NPC archetype (below) by referencing the appearance `id`. Settlements pull a biome-appropriate appearance pool at spawn so a snow-village smith looks different from a desert one.

### 3.2 NPC archetypes / professions

Reuse `ProfessionDatabase` (each profession has a `primaryActivity` + `progressionSkillIds`). Map professions to the buildings/stations that already exist:

| Profession | Job-site (existing) | Service | Activity |
|---|---|---|---|
| Innkeeper | tavern (`tavern_building`, tavern interior) | rest, tavern_jobs | Social |
| Guild Clerk | guild_hall | guild_board | Social |
| Librarian | library | library (lore/skill tomes) | Study |
| Merchant | shop / market_stall | market_trade | Bartering |
| Blacksmith | forge/anvil (interior_editor blacksmith) + Furnace | smithing | Crafting |
| Tanner | Tannery station | leatherwork | Crafting |
| Farmer | farm-ring plots, scarecrow | farming | Farming |
| Guard | walls/braziers (city tier) | defense | Combat |
| Cook | campfire/fireplace (CookingPot) | food | Crafting |

### 3.3 Embedded AI — options & tradeoffs

The user wants "embedded AI driving behavior/dialogue." Three approaches:

| Approach | Cost | Latency | Determinism | Offline | Best for |
|---|---|---|---|---|---|
| **A. Utility / behavior-tree AI** (no LLM) | None | Instant | Full | Yes | Movement, schedules, work, combat — the *behavior* layer |
| **B. Hybrid: authored lines + cached/templated LLM** | One-time (gen at build) | Instant at runtime | High | Yes | Dialogue, flavor, gossip strings |
| **C. Runtime LLM per interaction** | $ per line, ongoing | 0.5–3 s | Low (varies) | No | Open-ended conversation, emergent quests |

**Recommendation (phased, cheap-and-deterministic first):**

- **Behavior is always A.** Schedules, pathing, job-site work, flee/aggro — pure utility AI / state machines. Never put an LLM in the movement loop. This guarantees the "living village" runs offline at 60 fps.
- **Dialogue starts at B.** Use an LLM **at authoring time** (offline, in the tools) to generate a *bank* of barks and contextual lines per profession × tier × biome × mood, keyed by gossip/reputation state. Ship the bank as data. Runtime just selects a line by context — instant, deterministic, free, offline. This is the "embedded AI" the player feels, without the runtime cost.
- **C is an opt-in late-game layer.** Gate runtime LLM behind a player setting and only for *named* NPCs (the innkeeper you keep returning to), with the System framing it diegetically ("this soul thinks for itself"). Always fall back to the bank when offline/over budget. Cache responses to disk keyed by (npc, topic, reputation bucket) so repeat questions are free.

This sequencing matches the user's intent: *start cheap/deterministic, layer AI in.*

### 3.4 NPC memory + gossip/reputation

- **Per-NPC memory:** a small ring buffer of recent player interactions (traded, helped, attacked-nearby, completed-their-quest). Drives line selection in the bank.
- **Settlement gossip layer (Minecraft-style):** a hidden, shared per-settlement reputation score the player accrues. Like Minecraft villager gossip, it **spreads between NPCs** (witnessed acts propagate) and **decays** over time. It modifies: trade prices (`market_trade`), guild-board offers, greeting tier, and whether guards tolerate the player. Hook the existing **affinity/title** plumbing for the player-facing surface (a "Renown" affinity per region, surfaced through the `Reputation` System channel).
- NPCs reference the System about the *player*: `[ Whisper ] They say you cleared the Rootcellar.`

---

## 4. Settlement Tier Ladder

The editor (`settlement_editor.html`) and the runtime sampler (`IsoSettlementSampler`) already agree on a tier model. The editor tiers are **campsite → hamlet → village → town → city**; the sampler bands are `hamlet / village / market_town / frontier_city` with a `stockTier`. This plan **unifies them**: campsite is the editor-only transient tier (no permanent buildings, no sampler band), and town/city map to `market_town`/`frontier_city`.

| Tier | Footprint (editor `fx×fy`) | Plaza radius | Building count | Ranks present | Buildings (from pool) | Professions / roles | Services unlocked | Defenses | Growth trigger → next tier |
|---|---|---|---|---|---|---|---|---|---|
| **Campsite** | 12×12 | 1 | 0 buildings (3–5 camp props) | — | tents, bedrolls, `campfire_new`, keg_rack, `market_stall_red` | 1–2 nomads/traveling trader | rest (campfire ward), camp_trade (stall) | none (campfire ward radius only) | Player builds first permanent plot (e.g. `tavern_plot` → building) |
| **Hamlet** | 18×18 | 2 | 2–4 | r1 | tavern_r1, guild_hall_r1, library_r1, shop_r1 (2×2) | Innkeeper, 1 vendor, 1 farmer | rest, tavern_jobs, camp_trade/market_trade, guild_board | rudimentary (braziers, notice board) | Reach building count ≥ 4 **and** stockTier-1 economy active |
| **Village** | 28×24 | 3 | 4–8 | r1, r2 | + r2 variants (3×3): tavern_r2, shop_r2, guild_hall_r2, library_r2 | + Blacksmith, Tanner, Librarian, Guild Clerk | + library, specialty crafting stations | low wall segments / gate posts | Building count ≥ 8 **and** a guild_hall present |
| **Town** (`market_town`) | 42×36 | 4 | 8–14 | r2, r3 | r2+r3 (3×3/4×4): shop_r3 (specialty_vendor), guild_hall_r3 | + Merchant guildmaster, Cook, multiple farmers | + specialty_vendor, market square, festivals | partial walls + day guards | Building count ≥ 14 **and** all 4 building types present |
| **City** (`frontier_city`) | 58×48 | 5 | 14–24 | r2, r3 | full pool, multiple of each | full roster + Guards (leveled, §2.6) | all services, caravans, banking | full walls, gates, 24h guard patrols | (terminal tier) |

**How a settlement "grows":** growth is a **building-count + service threshold** check, not a timer. Each new plot the player (or NPC builders) completes increments the count; when count and the required service mix are met, the settlement promotes a tier — footprint expands, plaza radius grows, the sampler's `buildingRanks` band widens (so higher-rank lots become eligible), `stockTier` rises (richer vendor stock), and new professions move in. This maps 1:1 onto the editor's `TIERS[]` definition (`fx, fy, plaza, ranks, cmin, cmax`) and the sampler's `SettlementBand` (`footprintCellsX/Y, buildingRanks, buildingCountMin/Max, stockTier`).

**Editor mapping is exact.** The editor already encodes:
`campsite{fx12,fy12,plaza1,ranks[],cmin3,cmax5,camp[...]}`, `hamlet{18,18,1,[r1],2-4}`, `village{28,24,3,[r1,r2],4-8}`, `town{42,36,4,[r2,r3],8-14}`, `city{58,48,5,[r2,r3],14-24}`. Footprints `2x2/3x3/4x4` come from rank via `FOOT[r]`. So the tables above are **authored and previewable today** — no schema change needed for the layouts.

---

## 5. "World Feels Alive" Systems

### 5.1 Professions ↔ job-site blocks (Minecraft model)

An unemployed NPC scans nearby unclaimed job-sites and **claims** one, adopting that profession (table in §3.2). Job-sites are existing placeables/stations: Furnace → Blacksmith, Tannery → Tanner, tavern_building → Innkeeper, library → Librarian, farm plots/scarecrow → Farmer, shop/market_stall → Merchant, campfire/fireplace → Cook. If a building is destroyed/unclaimed, the NPC reverts to unemployed and re-scans. This directly mirrors Minecraft's POI-block binding and means **professions emerge from what the player builds**, reinforcing Pillar 3.

### 5.2 Daily schedules

A simple time-of-day state machine per NPC (no LLM):

- **Morning:** leave bed → walk to job-site → work (play the station's existing job/craft animation; farmers tend crops; guards patrol).
- **Midday gather-at-bell:** drift toward the plaza (social mingling, gossip exchange).
- **Evening:** return home; vendors shutter stalls.
- **Night / rain / raid:** retreat indoors (into interiors authored via `interior_editor.html`), lights via existing `emitsLight` placeables (lanterns, braziers, campfire).

Weather already exists (rain/snow); reuse it as a schedule input (retreat indoors on rain), matching Minecraft behavior.

### 5.3 Gossip / reputation

Per §3.4: hidden shared settlement reputation, spreads + decays, modifies prices/offers/greetings/guard tolerance, surfaced via a regional Renown affinity + `Reputation` System channel.

### 5.4 Economy / trading loop

- Vendors stock from the band's `stockTier` (1→4 by tier): higher tiers carry rarer goods (silver/gold/manacrystal/starmetal-tier items, better tools).
- Prices flex on (a) gossip reputation and (b) supply: if the player floods a vendor with hide, leather price drops locally for a while.
- **Production chains close the loop:** Tanner buys hide → sells leather; Blacksmith buys ore/bars → sells tools. This makes the player's harvesting feel woven into village life and gives NPCs a reason to "work" at their station.
- Guild board (`guild_board` service) issues fetch/clear contracts that pay coin + Renown — the social XP channel.

### 5.5 Settlement events

- **Festivals** (town+): plaza fills, temporary stalls (reuse market_stall props), buff foods (camp_stew/roasted_apple). Triggered on tier-up or seasonally.
- **Caravans** (town/city): a traveling-trader campsite (the campsite tier reused as a transient node!) spawns on a road spine, trades for a day, leaves. Elegant reuse of the campsite layout.
- **Raids** (any tier with defenses): mobs (slime/fox today; expand the bestiary — combat depth is a known gap) attack at night; guards defend; success/failure shifts reputation and may damage buildings (reverting professions per §5.1).

### 5.6 Biome-flavored variation

The editor already authors a **layout per (tier × biome)**, and biomes carry distinct surface/accent/prop pools. So a desert hamlet uses cacti + sand props and desert-palette NPC appearances; a snow village uses snow props and heavier outfits; a beach town gets docks (`dock_post`, `boat_rowing`) and a fisher profession. Biome drives ground tiles, prop pool, NPC appearance pool, and which professions are common (fisher on beach, miner near mountain ore).

---

## 6. How It Maps to Existing Systems & Editors

| Concern | Authored in (editor/tool) | Exported as | Folds into (runtime) |
|---|---|---|---|
| Biome tiles/accents/node chances | `biome_preview.html` | biome JSON | `FoundationContent` biomes / `IsoTerrainSampler` |
| Settlement layout per tier×biome | `settlement_editor.html` | `variants[]` JSON (`tier, biome, footprint, plaza, buildingCount, buildings[], props[]`) | `IsoSettlementSampler` bands + lot placement |
| Building interiors | `interior_editor.html` | interior layout JSON (floor mask + wall stacks + furniture anchors) | `FoundationInteriorLayout` (currently tavern-only; extend to guild/library/shop/blacksmith/inn) |
| NPC appearance + sprite sheets | `CharacterCreator` / `FarmerCharacterBuilder` / `StardewNpcSheetGenerator` | `FoundationCharacterAppearanceSaveData` + sheet | `FoundationCharacterAppearanceCatalog` |
| Dialogue/gossip banks | offline LLM in tooling (Phase 2+) | line-bank JSON keyed by profession×tier×biome×mood | NPC dialogue selector (new, data-driven) |
| Progression content (callings/skills/classes/professions/titles/affinities/quests) | code today (`FoundationContent.cs`), optional editor later | databases | `FoundationProgression` |

**Key point for the user:** the settlement JSON the editor already exports (`tier, biome, footprint, plaza, buildingCount, buildings[], props[]`) is exactly the shape `IsoSettlementSampler` consumes (`SettlementBand` + `SettlementBuildingEntry`). The tier ladder in §4 is therefore **previewable today** by switching the editor's tier dropdown — the only new authoring is interiors for the non-tavern buildings and the NPC line-banks.

---

## 7. Phased Implementation Roadmap

**Phase 1 — Living village skeleton (quick wins, ~2–3 wk)**
- NPC behavior layer: utility-AI schedules (work/gather/sleep/retreat-indoors), pathing to job-sites. (A only — no LLM.)
- Profession ↔ job-site claiming on existing buildings/stations.
- Wire `settlement_editor` tier ladder end-to-end so all five tiers spawn correctly from the sampler bands.
- System feed polish: add `LevelUp/RankUp/SkillEvolution/Reputation` channels + notification grammar.

**Phase 2 — Progression depth + dialogue banks (~3–4 wk)**
- Skill evolutions (`evolvesToSkillId`/`evolveAtLevel`) + per-rank stat multipliers in `FoundationPlayerStats`.
- Rank-up ceremonies tied to the renamed `FoundationAdventurerRank` (F→S) crossings; class-evolution branch picks at Rank C/A/S; hook the existing Trial → class offer as the first major beat (class chosen).
- Offline-LLM dialogue/gossip bank generation in tooling; runtime line selector (approach B).
- Interiors for guild/library/shop/blacksmith/inn via `interior_editor.html`.

**Phase 3 — Economy, gossip & events (~3–4 wk)**
- Hidden shared settlement reputation (spread+decay) → prices/offers/greetings/guards; Renown affinity surface.
- Production-chain trading loop; stockTier-driven vendor inventories.
- Festivals + caravans (campsite-as-transient-node) + night raids (needs bestiary expansion — fold in new mobs).
- Settlement growth promotion (building-count + service thresholds → tier-up).

**Phase 4 — Ambitious / opt-in (~open-ended)**
- Runtime LLM (approach C) for named NPCs, gated, cached, with offline fallback to the bank.
- Leveled NPCs + diegetic Appraisal of NPC tier.
- Player-foundable settlements that grow under NPC builders.
- Combat depth pass (new mobs/abilities) to make raids and higher tiers meaningful.

---

## 8. Open Questions / Forks

1. **Runtime LLM vs. hybrid bank (the big one).** *Recommendation: ship B (offline-generated banks) as the default and only ever path for v1; treat C (runtime LLM) as a Phase 4 opt-in for named NPCs with hard cost caps and offline fallback.* Rationale: the "living village" must run deterministically offline at frame rate; LLMs belong in authoring, not the simulation loop. This honors "start cheap/deterministic, layer AI in."
2. **Growth: player-driven vs. autonomous.** Do settlements grow only when the *player* builds, or can NPC builders auto-grow a town over time? *Recommendation: player-seeded, NPC-assisted* — player places the first plots, NPCs complete and later add lots, so growth feels earned but not chore-gated.
3. **Tier vs. flat stat scaling.** Confirm the per-tier multiplier (§2.5) is acceptable vs. designers who prefer additive scaling. The multiplier is what keeps level 100 meaningful, but it requires re-tuning mob/dungeon power bands per tier.
4. **Campsite as both a starter tier AND a caravan node.** Reusing the campsite layout for traveling traders is elegant but means "campsite" is overloaded. *Recommendation: keep one layout, tag instances `home_camp` vs `caravan` so behavior/lifetime differ.*
5. **Editor parity.** The editor's `town`/`city` ids vs. the sampler's `market_town`/`frontier_city` ids must be reconciled (an alias map in the JSON import). Low effort, but decide now to avoid drift.
6. **NPC level visibility.** Should NPC levels be always-visible, Appraisal-gated (a skill the player levels), or hidden? *Recommendation: Appraisal-gated* — turns "reading the room" into a progression reward.
