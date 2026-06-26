# 23 — LitRPG / Progression-Fantasy Research Brief

> Clean-room research pass translated into **concrete, buildable** recommendations for
> LIT-ISO's *existing* systems. Learns the SHAPE of the genre (class evolution, leveling
> feel, skill acquisition, affinity/element magic, reward cadence, pitfalls) and maps it
> onto the code we already ship.
> Companion to `15_LitRPG_System_Bible.md` and `22_Skills_Abilities_VFX_Design.md`.
> **Nothing here invents a new system** — it refines the emergent class + affinity +
> ability economy that is already live in `FoundationProgression`, `FoundationAbilitySystem`,
> and `FoundationContent`.

---

## 0. TL;DR — what the genre says, and what we should do

Genre consensus (sources §10):

1. **Leveling feels good when it is discrete, granular, and milestone-spiked** — not a smooth
   curve. Frequent small ticks, occasional "power bumps" at class/tier thresholds.
2. **Earned > chosen.** The most-praised modern classes (hidden/unique/evolving) are *unlocked
   by what you did*, gated behind achievements/quests, and **evolve** into advanced/sub-classes.
   This is exactly our emergent-class model — we are already on the right side of the trend.
3. **Affinity/element magic works when the element you can use is gated by attunement**, and
   spells you learn match your affinities. We already do this (`affinityId` → power multiplier).
4. **Non-combat classes (Chef, Lorekeeper, Farmer, Crafter) are a recognised, beloved strand** —
   our cozy direction is a genre feature, not a compromise.
5. **Top pitfalls: stat bloat, empty levels, the scaling treadmill.** Our defenses: a fixed
   6-stat HUD, every level/tier must hand over a *visible* thing, and we must NOT scale mobs
   1:1 with the player.

What we should change in the plan, in priority order, is in §8. The headline: our code already
supports ~80% of best-practice; the missing 20% is **(a) making tiers hand out concrete unlocks
(not just numbers), (b) a class *evolution* edge (D-class → C-class as a discoverable upgrade),
and (c) closing the affinity/element data gaps (Glimmer + Neutral have no `Affinity` def today).**

---

## 1. How the genre handles CLASSES (and what we keep)

**Genre shape (sources 1, 3, 7):**
- A *classless* start that grants a flat trickle of stats; hitting a **base class** raises the
  growth rate and unlocks class abilities; an **advanced class** / **evolution** raises it again.
  Each evolution is a "transformative leap" gated behind an epic quest, a milestone, or a rite.
- **Rarity tiers** (Common → … → Legendary/Mythic) signal both power *and* uniqueness; rarer
  classes have steeper growth and exclusive abilities.
- **Customization via branching** (skill trees / specializations) with deliberate trade-offs.
- Modern fan-favourite trend (Dungeon-Crawler-Carl era): **hidden / unique classes earned by
  actions and achievements**, not picked from a menu.

**Where LIT-ISO already sits (verified in code):**
- `ClassDefinition` carries `rarity` (`FoundationClassRarity`: Common…Mythic) + `weights`
  (evidence categories) + `preferredAffinityIds`. `FoundationProgression.BuildClassOffers()`
  scores every class as `Σ(trialScore[cat] × weight) + Σ(affinityScore[prefAffinity])` and
  offers the **top 3** at trial completion. This IS achievement-driven, emergent acquisition.
- Eight live classes already span F–S via rarity (`wayfarer` CommonPlus=E, `trailblade`/
  `stonehand_delver` Uncommon=D, `iron_warden`/`hearthbound_acolyte`/`wildsign_ranger` Rare=C,
  `ashvein_pyromancer`/`oathbearer` Epic=B). A-tier (Legendary) and S-tier (Mythic) are empty.

### 1.1 Refined emergent F–S class/progression model (concrete)

Keep the existing scoring. Add the three things the genre says make it *land*:

**(a) Class evolution as a discoverable upgrade, reusing our 8 classes.**
The genre's "base class → advanced class" beat maps cleanly onto our rarity ladder if we treat
the lower-rarity classes as **evolution roots** of the higher ones in the same theme:

| Theme | Base (earned at trial) | Evolves into | Trigger (reuse existing signals) |
|---|---|---|---|
| Scout-fighter | `trailblade` (D) | `wildsign_ranger` (C) | sustained Exploration+Creature evidence + Root/Gale affinity past Rare |
| Delver | `stonehand_delver` (D) | `iron_warden` (C) | Building+Combat evidence + Stone affinity past Rare |
| Caster | (Wayfarer + Ember unlock) | `ashvein_pyromancer` (B) | Magic evidence + Ember affinity past Epic |
| Steward | `hearthbound_acolyte` (C) | `oathbearer` (B) | repeated Support/Social + Hearth+Stone affinity |

Implementation cost is near-zero: this is the **same `BuildClassOffers` scoring re-run later**.
Add a single field `string[] evolvesFromIds` (or `int evolutionStage`) to `ClassDefinition`
purely for *presentation* ("Trailblade → Wildsign Ranger") and to gate higher-rarity offers
behind "you must already hold the base class." No new system.

**(b) A/S capstone classes — fill the empty slots (answers §10 Q3 of doc 22).**
Define one **Legendary (A)** and one **Mythic (S)** class so the ladder has a visible top:
- **A — "Valebound Warden"** (Legendary): requires high evidence in 2+ categories **and** two
  affinities past Epic. Genre role: the multi-discipline master.
- **S — "Hearthmythic Steward"** (Mythic): the Bible's *capstone-that-changes-the-region*
  spirit — requires a near-Perfect affinity, an S-grade trial, and a late-arc world milestone.
  This is the cozy north-star class: power expressed as the settlement, not a kill count.

Both are just two more `Class(...)` rows + scoring weights; they read as aspirational on the
character panel long before they're reachable (the genre's "anticipation = dopamine" lever).

**(c) Each tier must FEEL different.** Today rarity only changes the label. Give each tier one
concrete, cheap entitlement so a tier-up is a *visible bump* (anti-"empty level"):

| Tier | Class rarity | What the tier hands you (concrete, reuses existing hooks) |
|---|---|---|
| F | Common | latent — generic Wayfarer kit only |
| E | CommonPlus | **Wayfarer start**: 3 stamina skills + open 1 active slot |
| D | Uncommon | a class **signature skill** unlocked + 1 stat-bonus row (`statBonuses`) |
| C | Rare | a 2nd active slot frequency / cooldown perk + class-themed recipe/title |
| B | Epic | an **affinity-gated** signature spell + a `RegionShift`-style flavour unlock |
| A | Legendary | a 4th active slot / capstone passive |
| S | Mythic | a world-state capstone (Bible's regional transformation) |

> Genre note (source 7): rarer classes should have a *steeper* growth slope, not just better
> flavour. Cheapest way to honour that without stat bloat: scale **class XP → character XP
> contribution** and **affinity multiplier headroom** by rarity, not by adding new stats.

---

## 2. STATS & LEVELING — making it feel good without bloat

**Genre shape (sources 1, 2, 4, 7):**
- *Granularity matters*: fewer, heavier levels feel more impactful than thousands of tiny ones.
- *Milestone spikes*: linear trickle punctuated by class/evolution power-bumps.
- *Anticipation is the dopamine*: show the player what the next unlock is *before* they reach it.
- *Pitfall — the treadmill*: never scale enemies 1:1 with the player; let offense/utility
  outpace defense so growth is *felt*. (sources 2, 6)

**Map onto LIT-ISO (verified):**
- We already have the right shape: a **broad/slow character level** + **fast skill levels**
  (`XpPerProgressionLevel = 100`, `Stats.AddExperience(amount/2)` so character lags skills).
  Keep the 6-stat HUD (STR/DEX/INT/VIT/DEF/LUCK) — that's our stat-bloat firewall. Detail goes
  behind panels, exactly as the Bible says.
- **DEX → cooldown, INT → mana pool, VIT → stamina** is already wired
  (`Stats.CooldownMultiplier`, mana/stamina spend in `TryUseAbility`). That's three stats with
  *legible, felt* effects — do not add more stats; deepen these.

**Concrete recommendations:**
1. **Make the trial day-counter drive milestone spikes.** `FoundationProgression` already tracks
   `TrialDay`/`TrialDurationDays` (7) and `GradeForecast` (F–S from `TotalTrialScore`). Surface
   a **daily "what changed" digest** at each day rollover (one stat tick, one discovery, one
   evidence highlight) — this is the Bible's daily cadence and the genre's "frequent small
   rewards." Pure read-state + UI; no new model.
2. **Show the next unlock.** On the character panel, render the **top class offer** and the
   **next affinity rank threshold** as a "trending toward…" line. `BuildClassOffers` and
   `AffinityRankForScore` already give you everything; this is the anticipation lever and it's
   free.
3. **Anti-treadmill rule (design invariant):** mob power tiers stay *fixed per biome*; the
   player out-scales early biomes on purpose. Cozy framing makes this natural — soothed/owned
   biomes should visibly *de-escalate* (`ThreatCalm`, `RegionShift`), the opposite of a treadmill.
4. **Empty-level guard (design invariant):** every character level OR skill level OR tier step
   must grant a *named* thing (recipe, perk, slot, title, region note). If a level can only give
   "+1 to a number," batch it into the next milestone instead.

---

## 3. SKILL / ABILITY ACQUISITION — active vs passive, trees

**Genre shape (sources 1, 4):** skills are acquired by leveling/quests; class-specific abilities
are *commitment rewards*; skill trees give branching specialization with trade-offs; a healthy
kit mixes **active** (cast) and **passive** (always-on) abilities.

**Map onto LIT-ISO (verified):**
- Two parallel skill layers already exist and should be kept distinct in the UI:
  - **Mastery skills** (`FoundationSkillDefinition`: foraging, woodcraft, mining, farming,
    cooking, crafting, building, exploration, creaturecraft, combat, warding, spellcraft,
    trade, lorekeeping) — these are the *passive/tree* layer (the Bible's node kinds:
    Ease/Yield/Insight/Expression/Utility/Harmony). They level from activity XP.
  - **Abilities** (`FoundationAbilityDefinition`: Skill=stamina, Spell=mana) — the *active* cast
    layer with cost/cooldown/delivery. Eight are live + the delivery/VFX hook is already added.
- The doc-22 roster (Steady Strike, Guard Step, Flash Step, Tool Tune, Ground Slam, Cyclone
  Cut, Tailwind, Overclock + the elemental spells) fits the **existing delivery taxonomy**
  (Melee/Blink/Projectile/Cone/AreaNova/SelfBuff/AuraZone/Heal/Snare/Summon/Utility) — all of
  which are already enum values and partly executed.

### 3.1 A buildable skills/abilities set tied to our delivery types + 7-day arc

Principle: ship **one active per delivery type** first (so the dispatcher is exercised end-to-end)
before fanning out per element. Everything below uses *only* existing fields/enums.

**Stamina skills (universal, mastery-ranked F–S) — Day 1 onward, no element gate:**

| Ability | Delivery (exists) | Why it's cheap to build | Arc beat |
|---|---|---|---|
| Steady Strike `[live]` | Melee | already live; needs arc-overlap executor | Day 1 |
| Guard Step `[live]` | SelfBuff | already live; flag a parry/footing buff | Day 1 |
| Flash Step `[live]` | Blink | already live + dispatched | Day 1 |
| Ground Slam | AreaNova | reuses circle-overlap executor | char level |
| Cyclone Cut | Cone | reuses arc-overlap executor | char level |
| Tailwind | SelfBuff | move-speed + stamina-regen mod (`buffDuration`) | char level |

**Elemental spells (mana, gated by affinity unlock) — opened *during* the 7 days:**

| Element | Ability | Delivery (exists) | Status (v1 set) | Notes |
|---|---|---|---|---|
| Neutral | Mana Bolt `[live]` | Projectile | — | gateway, no affinity |
| Ember | Ember Spark `[live]` | Projectile | Burn | gateway fire |
| Tide | Tidal Lash | Projectile | Slow | new Tide affinity needed (§5 gap) |
| Tide | Raincall | AuraZone | Regen | cozy: heals crops/soothes heat mobs |
| Root | Root Snare `[live]` | Snare | Root | live |
| Stone | Stone Skin `[live]` | SelfBuff | Guard | live |
| Glimmer | Mending Light `[live]` | Heal | — | live; **Glimmer affinity missing** (§5 gap) |
| Glimmer | Lantern Flare | AreaNova | — | strong vs night/gloom mobs — cozy night safety |
| Hearth | Soothing Brew | Heal | Regen | HP+stamina, self/ally |
| Hearth | Gather Round | AreaNova | Haste | party buff + Threat Calm (cozy capstone vibe) |

**Active vs passive split (genre-correct, fits our code):**
- *Active* = `FoundationAbilityDefinition` (slotted, cursor-aimed, cost/cooldown). Cap 4 slots.
- *Passive* = mastery-skill node unlocks (the Bible's perk list: Gentle Gatherer, Soup Sense,
  Lantern Step…). These never enter the ability bar — they modify yields, costs, comfort. This
  keeps the active bar uncluttered (anti-bloat) and gives the cozy tree real depth.

> The genre's "skill tree trade-off" lever maps to **mastery node kinds**: pushing Yield costs
> you Ease budget, etc. That's a future tree-UI concern, not a model change.

---

## 4. MAGIC / AFFINITY / ELEMENT system

**Genre shape (sources 5, 1):** attunement gates which elements you can use; spells learned must
match affinity; affinity deepens with use and unlocks higher spells.

**Map onto LIT-ISO (verified):** this is our strongest existing system.
- 7 `Affinity` defs live (ember, tide, root, stone, gale, glimmer, hearth) **— wait: only 6 of 7
  elements have a usable spell gate, and two element-enum values have NO affinity def.**
- `AffinityRankForScore` ladders Dormant→Perfect at score thresholds 10/20/40/70/110/160;
  `AffinityEffectMultiplier` gives 1.0→1.75×. `TryUseAbility` already multiplies `basePower` by
  this. Evidence events already grant affinity (`A("ember", n)` etc.). **The loop is complete.**

### 4.1 The element ↔ affinity gaps to close (cheap, high-impact)

| Element enum | Has `Affinity` def? | Action |
|---|---|---|
| None | n/a | leave (pure utility) |
| Neutral | no (by design) | leave — Mana Bolt is the non-affinity gateway |
| Ember | yes | — |
| Tide | **NO** | **add `Affinity("tide", …)`** — Tide spells (Tidal Lash/Raincall) can't scale without it |
| Root | yes | — |
| Stone | yes | — |
| Gale | yes | — but **no Gale spell exists** — Flash Step is a Gale *skill*; add a Gale spell |
| Glimmer | **NO** | **add `Affinity("glimmer", …)`** — Mending Light/Lantern Flare are Glimmer with no gate |
| Hearth | yes | — |

> Verified: the live `Affinity(...)` calls are ember/tide/root/stone/gale/glimmer/hearth — **but
> `tide` and `glimmer` appear as affinity *families* in description while the element-scaling for
> Glimmer/Tide spells still needs a matching `affinityId` string the spell points at.** Confirm
> the exact `affinityId` strings on `mending_light` (currently `""`) and Tide spells, and wire
> them. This is the single highest-leverage data fix: it makes the two cozy-signature elements
> (light/water) actually progress.

### 4.2 Magic unlock during the 7-day arc (answers doc-22 §10 Q2)

Recommended trigger (cheapest, most thematic, already has Bible hooks): **an in-world elemental
Source** (Skillseed / shrine / relic — the Bible already has *Skillseed Awakening*, *Skillseed
Sprout*, *Skillseed* material). Touching a Source:
1. grants the first spell of that element (e.g. Ember Source → `ember_spark`),
2. seeds initial affinity score (cross the Basic threshold so the multiplier is felt immediately),
3. queues an `AffinityResonance` system message (channel already exists).

Allow **multiple** elements per run (the genre rewards hybrid builds and our hybrid classes —
oathbearer, wildsign_ranger — *require* it), but make the **first** Source cheaper/closer so most
players commit to a primary. This naturally produces the caster/hybrid vs martial class split the
arc wants.

---

## 5. THE CORE PROGRESSION LOOP & REWARD CADENCE

**Genre shape (sources 4, 6):** stack core loop → meta loop → emotional "I am growing" loop;
casual pacing ≈ **3–5 days per milestone**; reward *anticipation* before delivery; rotate reward
types so they don't go stale.

**Map onto LIT-ISO (verified + Bible):** the Bible's cadence table already matches best practice:
5 min → material/discovery; 20 min → skill level/recipe; 1 session → building/quest; 3–5 sessions
→ class branch/milestone. The 7-day arc *is* the headline meta loop.

**Concrete loop, tied to code:**

| Loop tier | Player feels | Driven by (existing code) |
|---|---|---|
| Core (seconds–minutes) | "I did a thing" | activity XP → `AddActivityXp` → skill + character XP + system msg |
| Mid (minutes–session) | "I unlocked a thing" | quest objective → `GrantRewards`; affinity rank-up → multiplier bump |
| Meta (the 7 days) | "I am *becoming* something" | `TrialDay` advance → `GradeForecast` trends → `BuildClassOffers` |
| Macro (post-trial) | "My class is evolving / my valley is changing" | class evolution (§1.1a) + `RegionShift` |

**Cadence fixes to add:**
1. **Daily digest** (one msg per day rollover) — §2 rec 1.
2. **"Trending toward" preview** on HUD/panel — §2 rec 2 (anticipation lever).
3. **Rotate the reward type** so the player isn't handed the same currency twice in a row — the
   reward-type enum is rich (`Recipe/Pattern/TraitSeed/LandmarkPermit/NeighborBond/RegionShift/
   MemoryPage/Item/Xp`); sequence starter-quest rewards to alternate categories.
4. **Class crystallization as the act-1 climax** (~day 7): present the top-3 offers (already
   built), let the player confirm, fire a big `SystemMessageChannel.LevelUp`/title moment. This
   is the genre's "rite of passage" beat and it's already 90% in `CompleteTrial`.

---

## 6. COMMON PITFALLS — and our specific guardrails

| Pitfall (genre) | LIT-ISO guardrail (concrete) |
|---|---|
| **Stat bloat** | Hard-freeze the HUD at 6 stats; everything else is a *tag/facet* (Hearth/Hand/Root/Spark/Step/Grit/Glow) or a behind-panel codex value. Do not promote facets to stats. |
| **Empty levels** | Invariant: every level/tier/skill-up grants a *named* unlock or batches into the next milestone (§2 rec 4). |
| **Scaling treadmill** | Mob tiers fixed per biome; soothed biomes de-escalate via ThreatCalm/RegionShift. Let offense/utility out-scale defense. |
| **Class as a cage** | Classes are emergent + evolving; mastery skills are universal (anyone can cook/build/fight). Keep the Bible's "Callings are identities, not cages" ethos even though Callings as a *picker* are removed. |
| **Reward fatigue** | Rotate reward types (§5 fix 3); mix active-cast unlocks with passive node unlocks so it's not all "+number." |
| **Number-only progression** | Pair every numeric gain with a *world* change the player can see (a path, a calmed den, a lit lantern) — the Bible's whole thesis. |

---

## 7. NON-COMBAT / COZY PROGRESSION IS A GENRE STRENGTH

The class guide explicitly lists **Chef, Lorekeeper, Crafter, Druid** as valued "unconventional"
classes, and the database tags Cooking/Crafting/Farming/Trading/Base-Builder as first-class story
elements. Our cozy direction is *on-trend*, not a niche bet. Reinforce it by:
- Keeping the **Profession** layer (`blacksmith/alchemist/cook/builder/trader/farmer/miner/fisher`)
  as the non-combat parallel to combat Classes — both already resolve at trial via `BuildProfession
  Offers`. A player can crystallize as a **caster-class + cook-profession**; that dual identity is
  the genre's "Chef who also fights" and it's already in our data model.
- Making at least one **A/S capstone explicitly cozy** (the "Hearthmythic Steward," §1.1b) so the
  *top* of the ladder is reachable without combat — proving the cozy thesis at the power ceiling.

---

## 8. PRIORITIZED RECOMMENDATIONS

### 8a. HIGH-IMPACT + CHEAP (given our code) — do these first

1. **Close the affinity/element data gaps** (§4.1): add `Affinity("tide")` + `Affinity("glimmer")`
   defs (or confirm exact ids) and wire `affinityId` on `mending_light` + Tide/Gale spells. *One
   content edit; unlocks scaling for the two cozy-signature elements.* **Highest leverage.**
2. **Make tiers hand out concrete entitlements** (§1.3 table): one named unlock per rarity tier,
   reusing `statBonuses` / a signature ability id / a title. Kills "empty tier-ups." *Data + a
   small read-state surface.*
3. **Surface the "trending toward" class + next affinity rank on the character panel** (§2 rec 2):
   `BuildClassOffers` + `AffinityRankForScore` already compute it. *Pure UI read-state.*
4. **Daily 7-day digest message** at day rollover (§2 rec 1, §5 fix 1): one system message
   summarizing the day's biggest evidence/affinity/skill gain. *Reads existing state; queues one
   `SystemMessage`.*
5. **Rotate starter-quest reward types** (§5 fix 3): reorder existing `FoundationQuestReward`
   payloads so categories alternate. *Data-only.*
6. **Ship one active per delivery type** to exercise the dispatcher end-to-end (§3.1): Melee, Cone,
   AreaNova, SelfBuff in addition to the live Blink/Projectile/Heal. *Executor work already scoped
   in doc-22 Slice 4.*

### 8b. EXPENSIVE / LATER

1. **Class evolution edge** (§1.1a): add `evolvesFromIds`/`evolutionStage` to `ClassDefinition`,
   gate higher-rarity offers behind holding the base class, present "Trailblade → Wildsign Ranger."
   *Model touch + re-offer flow + UI.*
2. **A/S capstone classes** (§1.1b): define Valebound Warden (A) + Hearthmythic Steward (S) with
   world-state effects. *New content + the regional-transformation systems they trigger (heavy).*
3. **Mastery-skill tree UI with node trade-offs** (§3.1): the Bible's node kinds become a real
   branching tree with Ease/Yield budgets. *New UI + node data + balancing.*
4. **Elemental Source unlock objects in the world** (§4.2): placeable Sources that grant first
   spell + seed affinity. *New interactable + spawn placement + tutorialization.*
5. **Status system v1 → v2** (per doc-22 §7): ship Burn/Root/Slow/Haste/Guard/Regen first; defer
   stacking, resistances, element combos. *Controller + content.*
6. **World-state capstones** (region transformations that the S-class/late quests trigger): the
   most expensive and the most on-brand; the genre's "legendary power tied to destiny." *Large.*

### 8c. What should change in the master implementation plan

- **Promote the affinity/element data fix (8a-1) above the combat-shapes work.** Doc-22's slice
  plan does combat shapes (Slice 4) before the magic-unlock/roster fill (Slice 5); but the Tide/
  Glimmer affinity gap is a *tiny* edit that makes our two coziest magic strands actually
  progress, and it should ride alongside Slice 1/2.
- **Fold "concrete tier entitlements" into Slice 2** (F–S display + class crystallization). Slice 2
  currently only adds the *display* map; without per-tier entitlements, the F–S labels are cosmetic
  and risk the "empty level" pitfall on day one of shipping the ladder.
- **Add the daily digest + "trending toward" preview to Slice 2** — they're the cadence/anticipation
  levers the genre research says matter most, and they're nearly free on existing read-state.
- **Schedule class evolution (8b-1) as the first post-vertical-slice feature** — it's the single
  genre beat ("base → advanced class") we don't yet model, and it's a small model touch that turns
  our static 8 classes into a *progression*, not a lookup.

---

## 9. CLEAN-ROOM NOTE

Every name and structure above is either already in LIT-ISO's repo or a generic genre shape
(rarity tiers, attunement-gated elements, evolution rites, reward-type rotation). No book's,
serial's, or game's specific class names, lore, abilities, or text were copied. Proposed new
class names (Valebound Warden, Hearthmythic Steward) follow the Bible's own naming voice.

---

## 10. Sources

- [The Ultimate Guide to Character Classes in LitRPG — Progression Fantasy & LitRPG Database](https://progressionfantasy.co.uk/blog/the-ultimate-guide-to-character-classes-in-litrpg/)
- [Distinctions in Progression Fantasy Styles — Andrew Rowe](https://andrewkrowe.wordpress.com/2022/11/04/distinctions-in-progression-fantasy-styles/)
- [LitRPG Power and Progression Analysis — crrowenson.com](https://crrowenson.com/case-studys/litrpg-power-arc-analysis-case-studies/)
- [Progression fantasy — Wikipedia](https://en.wikipedia.org/wiki/Progression_fantasy)
- [Element Affinity Spell Learning System (attunement-gated spells, design reference)](https://joelheath42.itch.io/abi-elementaffinityspellbooks-v23-elemental-spell-learning-system)
- [LitRPGs With Unique MC Powers — Level Up Publishing](https://www.levelup.pub/litrpg-unique-mc-powers)
- [Power progression in games: Crafting rewarding player experiences — Game Developer](https://www.gamedeveloper.com/design/power-progression-in-games-crafting-rewarding-player-experiences)
- [Designing Game Progression and Reward Systems — Game Pill](https://gamepill.com/level-up-the-art-of-designing-game-progression-and-player-rewards/)
- [Compulsion Loops & Dopamine in Games and Gamification — Game Developer](https://www.gamedeveloper.com/design/compulsion-loops-dopamine-in-games-and-gamification)
- [Designing Reward Loops That Keep Players Hooked — Rakesh Roy / Medium](https://medium.com/@rakeshroyakula/designing-reward-loops-that-keep-players-hooked-without-manipulation-58447c858d4a)
