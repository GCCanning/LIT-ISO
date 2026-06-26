# 22 — Skills, Abilities & VFX Design

> Design pass for the active ability layer: the skills/spells players cast, the in-game
> effect each one produces, the VFX that sells it, and an **emergent F–S class** system
> earned over the opening **7-day arc**.
> Builds on the **existing** `FoundationAbilitySystem` + affinity + Class-discovery code.
> Companion to `15_LitRPG_System_Bible.md` and the in-house **VFX Lab**
> (`Tools/AssetForge/VFXLab/`).

> **Revision (owner direction):** Callings are **removed**. Everyone starts with basic
> **stamina skills**; **magic is unlocked during the first 7 days**; the **Class** you end
> up with is *emergent* — it reflects what you did and which magic you opened. Class tiers
> are **F → S** (not Novice/Adept/…). Targeting is **cursor-aimed**. Active slots cap at
> **4**. Cozy/non-lethal framing is in.

---

## 0. TL;DR

We are **not** building a new ability system, and we are **not** keeping Callings.

- The active-ability **economy already exists** (`FoundationAbilitySystem`: cost, cooldown,
  affinity-scaled power, XP, evidence, system messages).
- The **emergent class system already exists** (`ClassDefinition` discovered via Trial
  evidence + element affinities) — this *replaces* Callings as the identity layer.
- This doc: (1) deletes Callings, (2) defines the **7-day progression spine**
  (stamina start → unlock magic → class crystallizes), (3) re-tiers everything **F–S**,
  (4) adds the two missing pieces — a **VFX hook** and an **in-world executor** — and
  (5) lays out the roster as **Skills (stamina)** + **Spells (mana, per element)** plus the
  **F–S class table**, with a v1 **status system**.

---

## 1. What already exists (the spine)

`Assets/Scripts/IsoCoreFoundation/Progression/` + `Core/`:

- **`FoundationAbilityDefinition`** — `kind` (Skill/Spell), `resource` (Stamina/Mana),
  `element`, `activity`, `resourceCost`, `cooldownSeconds`, `basePower`, `range`,
  `activityXp`, `skillIds[]`, `evidenceId`, `affinityId`, `description`, `systemMessage`.
- **`FoundationAbilitySystem.TryUseAbility()`** already: enforces cooldown (DEX-shortened),
  spends mana/stamina, scales power by **affinity** (`basePower × affinity multiplier`),
  grants activity XP to mastery skills, records **Trial evidence**, queues a System message.
- **Six abilities live**: `steady_strike`, `guard_step`, `mana_bolt`, `ember_spark`,
  `root_snare`, `stone_skin` (folded into the roster in §6).
- **Eight emergent classes live** (`FoundationContent`): `wayfarer`, `trailblade`,
  `stonehand_delver`, `iron_warden`, `hearthbound_acolyte`, `wildsign_ranger`,
  `ashvein_pyromancer`, `oathbearer` — each discovered from **evidence weights**
  (Exploration/Combat/Survival/…) + **element affinities**.

### Enums we build against

- Elements: `None, Neutral, Ember, Tide, Root, Stone, Gale, Glimmer, Hearth`
- Resources: `Stamina, Mana` · Kinds: `Skill, Spell`
- Activities: `Harvest, Craft, Build, Farm, Explore, Creature, Combat, Magic, Trade, Lore`
- Affinity ranks (power ladder, 7): `Dormant, Basic, Common, Uncommon, Rare, Epic, Perfect`
- Class rarity (7): `Common, CommonPlus, Uncommon, Rare, Epic, Legendary, Mythic`

### The gaps to close

1. **No VFX hook** on the ability data (§2).
2. **No in-world executor** — `TryUseAbility()` only does bookkeeping (§4). The current
   Flash Step is hardcoded in `IsoFoundationPlayer` and must become a real ability.
3. **Callings must be removed** — touches ~10 files (see §8, Slice 0).

---

## 2. Data extension — the VFX + delivery hook

Add to `FoundationAbilityDefinition` (all default to safe no-ops; the live six are unchanged):

| Field | Type | Purpose |
|---|---|---|
| `delivery` | `FoundationAbilityDelivery` | the in-world effect shape (§3) |
| `vfxAnchor` | `FoundationAbilityAnchor` | `Feet / Hand / Self / Target / Path` |
| `vfxCastId` | `string` | effect on cast (at anchor) |
| `vfxTravelId` | `string` | effect riding a projectile / dash trail |
| `vfxImpactId` | `string` | effect at hit / landing |
| `dashTiles` | `float` | Blink distance (tiles) |
| `projectileSpeed` | `float` | Projectile speed (world u/s) |
| `areaRadius` | `float` | Cone/Nova/Aura radius |
| `buffDuration` | `float` | buff/aura/snare duration (s) |
| `statusEffectId` | `string` | optional status on hit (§7) |

```
enum FoundationAbilityDelivery { Melee, Blink, Projectile, Cone, AreaNova, SelfBuff, AuraZone, Heal, Snare, Summon, Utility }
enum FoundationAbilityAnchor   { Feet, Hand, Self, Target, Path }
```

`vfx*Id` strings map to VFX Lab exports under `Assets/Generated/Effects/<category>/<id>`
(the Lab's `exportUnity()` already writes that folder + manifest). Missing sheet → the
executor falls back to a **`WorldFx`** procedural effect by element palette.

---

## 3. Delivery types (in-game effect taxonomy)

Executed with **world-query** APIs (no physics pushing — consistent with the movement
invariant). **Targeting is cursor-aimed**: projectiles fly toward the cursor; Cone/Nova
center on cursor (clamped to `range`); Blink goes toward facing/cursor.

| Delivery | In-game effect | Executed via | VFX anchor |
|---|---|---|---|
| **Melee** | hit in a short facing arc | arc overlap; dmg = `scaledPower` | Hand → Target |
| **Blink** | teleport forward, collision-clamped | Flash Step march (`Walkable()` clamp) | Feet + Path trail |
| **Projectile** | flies to cursor / first mob | `FoundationProjectile` + hit query | Travel + Impact |
| **Cone** | short cone burst ahead | arc overlap within `areaRadius` | Hand, forward |
| **AreaNova** | radial burst at cursor/self | circle overlap `areaRadius` | Target/Self |
| **SelfBuff** | timed buff on caster | `FoundationPlayerStats` mod for `buffDuration` | Self aura |
| **AuraZone** | lingering ground zone | tick query in radius over `buffDuration` | looping, ground |
| **Heal** | restore HP/stamina (self/ally) | `Stats.Heal/RestoreStamina` | Target |
| **Snare** | immobilize/weaken a target | mob status flag for `buffDuration` | Target |
| **Summon** | temporary ally | `MobSpawner.SpawnMobAt` | Target |
| **Utility** | reveal/light/calm (no dmg) | map/light/ThreatCalm hooks | Self/Target |

---

## 4. The executor — `FoundationAbilityDispatcher`

Owned by `FoundationBootstrap`:

1. Input/UI → `AbilitySystem.TryUseAbility(id…)` (cost/cooldown/XP/affinity handled there).
2. On success → `Dispatcher.Execute(ability, result, caster)` switches on `delivery`, using
   `result.scaledPower` as magnitude, cursor for aim.
3. VFX via a small **`FoundationVfx`** resolver: `vfxCastId` at anchor, `vfxTravelId` on
   the projectile/dash, `vfxImpactId` at hit; missing sheet → `WorldFx` fallback.
4. **VFX intensity scales with rank** (`result.affinityRank`): higher rank → more particles,
   brighter palette step, extra impact ring. Progression you can see.

Clean split preserved: `FoundationAbilitySystem` owns rules/economy; dispatcher owns world
effect + visuals.

---

## 5. Progression spine — the 7-day arc (no Callings)

The opening **7 in-game days** are the formative "Awakening." Identity is **earned, not
picked**.

**Day 1 — everyone is the same.** You start as the **Wayfarer** (tier **E**) with a handful
of **stamina Skills** only (Steady Strike, Guard Step, Flash Step). No magic, no class label
beyond Wayfarer.

**Days 1–7 — you specialize by doing.** Two things accumulate:
- **Trial evidence** from your actions (Combat, Exploration, Survival, Craft, Creature, …) —
  this is what the Class system already reads.
- **Element affinity** — if and when you **unlock magic** (see below), casting and aligned
  activities raise that element's affinity F→S.

**Unlocking magic (during the 7 days).** Magic is optional and discovered, not given. Trigger
options (see §10 open questions; recommend the first):
- Find an **elemental Source** in the world — a Skillseed / shrine / relic (the Bible already
  has *Skillseed Awakening*, *Skillseed Sprout*, and *Skillseed* material as hooks). Touching a
  Source opens that element's **first spell** (e.g. an Ember Source → `ember_spark`).
- From then on, affinity grows the element F→S and unlocks its higher spells.

**Class crystallizes (~day 7) and keeps refining.** Your **evidence profile + affinities**
resolve to one of the emergent **Classes** below. No magic unlocked → martial classes
(Trailblade, Iron Warden). Magic unlocked → caster/hybrid classes (Ashvein Pyromancer,
Hearthbound Acolyte, Wildsign Ranger…). The class can keep upgrading tier as you go.

### The F–S ladder (replaces Novice/Adept/… and the old rank names)

One mental model everywhere — class tier **and** affinity rank display as F→S:

| Tier | Class rarity (enum) | Affinity rank (enum) | Feel |
|---|---|---|---|
| **F** | Common | Dormant | untrained / latent |
| **E** | CommonPlus | Basic | beginner (Wayfarer starts here) |
| **D** | Uncommon | Common | competent |
| **C** | Rare | Uncommon | skilled |
| **B** | Epic | Rare | expert |
| **A** | Legendary | Epic | master |
| **S** | Mythic | Perfect | legendary / capstone |

> Implementation: keep the enums as-is (stable ids/saves); add a display map
> `rarity/rank → "F".."S"`. No data migration needed.

### Other progression dials (unchanged)

- **DEX** shortens all cooldowns. **INT** grows the mana pool (more casts). **VIT** grows
  stamina (more skills). **Activity XP** feeds mastery skills + character level.
- **Neutral/utility skills** (Flash Step, Mana Bolt, Steady Strike) have no element, so they
  progress on a per-skill **mastery rank F–S** (same shape as today's Flash Step: distance
  up / cooldown down). The dispatcher reads affinity rank *or* mastery rank for VFX scaling.

---

## 6. The roster

Organized by **what powers it**, not by Calling. **Skill** = stamina (everyone, from
fairly early). **Spell** = mana (only after unlocking that element's magic). Cost/CD/Pow/Range
are starting values to tune. **[live]** = already in `FoundationContent`.

### 6a. Stamina Skills — universal, martial/movement/utility (mastery rank F–S)

| Ability | Cost | CD | Pow | Range | Delivery | VFX (cast → travel → impact) | Unlock |
|---|---:|---:|---:|---:|---|---|---|
| **Steady Strike** `steady_strike` **[live]** | 10 | 0.7 | 1.15 | 1.35 | Melee | slash @ target | Day 1 |
| **Guard Step** `guard_step` **[live]** | 8 | 1.2 | 0.75 | 1.0 | SelfBuff (parry/footing) | slash/dust @ feet | Day 1 |
| **Flash Step** `flash_step` (fold in current dash) | 8 | 3.0→ | — | — | Blink (`dashTiles` 1.5→3.5) | flashstep smoke @ feet + Path trail | Day 1 |
| **Tool Tune** | 8 | 6 | 1.0 | 0 | SelfBuff (next tool/weapon swings empowered) | arcane sparkle @ hand | char level |
| **Bulwark** | 12 | 8 | 1.0 | 2.0 | Cone (block + knockback) | dust burst forward | char level |
| **Ground Slam** | 14 | 6 | 1.4 | 2.5 | AreaNova (dmg + brief stun) | dust burst + ring @ feet | char level |
| **Cyclone Cut** | 12 | 6 | 1.3 | 2.5 | Cone (spin: dmg + knock) | gale swirl burst | char level |
| **Tailwind** | 9 | 8 | 1.0 | 0 | SelfBuff (move speed + stamina regen) | gale swirl @ feet | char level |
| **Overclock** | 14 | 30 | 1.0 | 0 | SelfBuff (haste: CD + attack speed) | arcane swirl aura | high level |

### 6b. Elemental Spells — mana, unlocked during the 7-day arc (power by affinity F–S)

| Element | Ability | Cost | CD | Pow | Range | Delivery | Status | VFX |
|---|---|---:|---:|---:|---:|---|---|---|
| Neutral | **Mana Bolt** `mana_bolt` **[live]** | 9 | 0.9 | 1.0 | 4.5 | Projectile | — | manabolt → arcane burst |
| Ember | **Ember Spark** `ember_spark` **[live]** | 12 | 1.1 | 1.25 | 4.0 | Projectile | Burn | fire projectile → fire burst |
| Ember | **Emberbrand** | 14 | 18 | 1.5 | 0 | SelfBuff (weapon ignites) | Burn-on-hit | fire rise @ hand |
| Tide | **Raincall** | 13 | 12 | 1.1 | 3.5 | AuraZone (heal crops/animals, soothe heat mobs) | Regen | ice/tide fall (rain) |
| Tide | **Tidal Lash** | 11 | 1.2 | 1.2 | 4.0 | Projectile | Slow | ice projectile → ice shatter |
| Root | **Root Snare** `root_snare` **[live]** | 11 | 1.4 | 0.85 | 3.75 | Snare | Root | root burst @ target |
| Root | **Spore Puff** | 9 | 7 | 0.8 | 2.5 | Cone (weaken/calm) | Slow | poison swirl cone |
| Root | **Quickroot** | 8 | 7 | 0.9 | 0 | SelfBuff (gather/move speed) | Haste | green rise @ feet |
| Stone | **Stone Skin** `stone_skin` **[live]** | 13 | 2.0 | 0.95 | 0 | SelfBuff (mitigation) | Guard | dust/grey aura @ self |
| Stone | **Aegis Field** | 18 | 28 | 1.3 | 4 | AuraZone (party DEF aura) | Guard | stone glimmer dome |
| Glimmer | **Wisp Bolt** | 12 | 1.1 | 1.2 | 4.5 | Projectile (bonus vs gloom) | — | arcane/heal projectile → glimmer impact |
| Glimmer | **Lantern Flare** | 13 | 8 | 1.3 | 3.5 | AreaNova (light, strong vs night mobs) | — | heal/arcane nova, bright |
| Glimmer | **Precision Bolt** | 11 | 1.0 | 1.3 | 5 | Projectile (high crit) | — | manabolt(arcane) → impact |
| Hearth | **Warm Hearth** | 8 | 6 | 0.8 | 0 | SelfBuff (regen + comfort) | Regen | heal rise @ self |
| Hearth | **Soothing Brew** | 10 | 8 | 1.4 | 4 | Heal (HP+stamina, self/ally) | Regen | heal sparkle fall @ target |
| Hearth | **Gather Round** | 18 | 30 | 1.6 | 5 | AreaNova (party buff + Threat Calm) | Haste | heal nova, bright |

> Cross-element gateway: the first magic you unlock typically opens with that element's
> cheapest Projectile or Self/AuraZone spell; higher-cost spells gate behind affinity tier.

### 6c. Emergent Classes (F–S) — earned, not chosen

Discovered from your **evidence profile** (what you did) + **element affinities** (what magic
you opened). Tiers below map the existing `FoundationClassRarity`. A/S slots are open for
future capstone classes.

| Class (id) | Tier | Earned by (evidence + affinity) |
|---|---|---|
| **Wayfarer** `wayfarer` **[live]** | E | default starting class — generalist, no specialization yet |
| **Trailblade** `trailblade` **[live]** | D | Exploration + Combat + Survival; gale/stone lean, little/no magic |
| **Stonehand Delver** `stonehand_delver` **[live]** | D | Mining/Build + Survival; Stone affinity |
| **Iron Warden** `iron_warden` **[live]** | C | Combat + Survival + Guard play; martial, low magic |
| **Hearthbound Acolyte** `hearthbound_acolyte` **[live]** | C | Hearth affinity + care/creature/craft evidence |
| **Wildsign Ranger** `wildsign_ranger` **[live]** | C | Exploration + Creature + Root/Glimmer affinity |
| **Ashvein Pyromancer** `ashvein_pyromancer` **[live]** | B | high Ember affinity + heavy Magic evidence |
| **Oathbearer** `oathbearer` **[live]** | B | mixed high evidence + multi-element affinity |
| *(open)* | A | Legendary — late-arc multi-element / mastery capstone |
| *(open)* | S | Mythic — endgame world-state capstone class |

---

## 7. Status system (v1 scope)

A small, fixed, **data-driven** set — each is a timer + one stat/flag modifier, **single
application** (no stacking in v1), shown as a small icon with a countdown. Works for player
**and** mobs.

| Status | Effect | Applied by (examples) |
|---|---|---|
| **Burn** | damage over time | Ember Spark, Emberbrand |
| **Root** | cannot move | Root Snare |
| **Slow** | reduced move/attack speed | Spore Puff, Tidal Lash |
| **Haste** | + move speed, − cooldowns | Quickroot, Gather Round, Overclock |
| **Guard** | damage reduction | Stone Skin, Aegis Field |
| **Regen** | heal over time | Warm Hearth, Soothing Brew, Raincall |

Data shape: `FoundationStatusDefinition { id, kind, magnitude, durationSeconds, tickSeconds,
icon, vfxLoopId }`. A `FoundationStatusController` on the player/mob applies, ticks, and
clears them and renders the icon row.

**Out of v1 (later):** stacking, refresh-vs-extend rules, resistances, dispel/cleanse,
and **element combos** (e.g. wet + Ember = extra burst, Burn vs Root cancels). Add once
the core six feel good.

---

## 8. VFX Lab → element mapping

Current Lab palettes: `arcane, fire, ice, poison, slash, heal, dust, smoke`; motions
`burst, swirl, rise, fall, slash, projectile, smoke`; presets incl. `flashstep`, `manabolt`.

| Element | Palette | Cast motion | Projectile/Impact | Lab gap to add |
|---|---|---|---|---|
| Neutral | arcane | burst | manabolt / arcane burst | — |
| Ember | fire | rise | fire projectile / fire burst | fire **projectile** preset |
| Tide | ice | fall | ice projectile / ice shatter | **tide** palette (blue-teal) |
| Root | poison | swirl | root burst | **root/vine** preset (ground-anchored) |
| Stone | dust | burst | dust ring | **stone shards** preset |
| Gale | arcane→(new) | swirl | wind streak | **gale** palette (pale cyan) + swirl preset |
| Glimmer | heal | fall/burst | sparkle | **glimmer** preset (golden bokeh) |
| Hearth | heal | rise | warm glow | **hearth** palette (amber-warm) |

Finishing the elemental set ≈ **6 small VFX Lab additions**, same shape as the smoke/mana-bolt
work already done.

---

## 9. Implementation slice plan

- **Slice 0 — remove Callings.** Strip `FoundationCallingDefinition`, the welcome-menu
  **Calling picker**, `ConfigureLaunch(...callingId)` / `SelectCalling`, and their save/
  validator references (~10 files). Default everyone to the **Wayfarer** class. Keep the
  emergent Class system. *Do this first so the picker stops shipping.*

- **Slice 1 — make the existing kit cast (vertical slice).** Add §2 fields; build
  `FoundationAbilityDispatcher` (Blink + Projectile + SelfBuff/Heal); fold the hardcoded dash
  into `flash_step`; replace the placeholder ability loadout VM so **Q/E/R/F call
  `TryUseAbility` + dispatch**; **cursor-aimed** projectiles; `WorldFx` fallback visuals.

- **Slice 2 — F–S display + class crystallization.** Add the `rarity/rank → "F".."S"`
  display map across HUD/character panel; show the current Class + tier; wire the day-7
  crystallization milestone (needs a day counter — confirm §10).

- **Slice 3 — VFX sheet pipeline.** Import VFX Lab exports to `Resources/Effects/<id>`;
  `FoundationVfx` plays sheets (WorldFx fallback); intensity by rank. Add the 6 missing
  palettes/presets.

- **Slice 4 — combat shapes + status v1.** Add Melee/Cone/AreaNova/Snare + the six-status
  controller (§7).

- **Slice 5 — magic-unlock + roster fill.** Implement the elemental **Source** unlock;
  register the full §6 roster + missing `Affinity` defs per element; balance pass.

- **Slice 6 — ability UI.** Cooldown radial, cost/ready states, slot assignment from the
  character panel.

---

## 10. Open design questions

1. **The "7 days" mechanic.** Is it a literal 7-in-game-day timer (a soft season), and does
   the class *crystallize* at day 7 or just *trend* toward a class continuously? Is there a
   day counter today, or do we add one? *(Assumed: 7-day soft arc, class trends and locks a
   tier at day 7, keeps refining after.)*
2. **Magic unlock trigger.** Elemental **Source** object in the world (recommended), an
   affinity threshold, or a quest beat (*Skillseed Awakening*)? Can a player open **multiple**
   elements in one run, or is the first one a soft commitment?
3. **A/S-tier classes.** What are the Legendary/Mythic capstone classes, and what world-state
   do they change (per the Bible's capstone spirit)?
4. **Status depth after v1.** When do we add stacking, resistances, and element combos?
5. **Respec.** The Bible has *Respec at the Old Well* — can players re-roll class/affinity
   focus, and at what cost?

*Settled: Callings removed · cursor-aimed targeting · 4 active slots + hold-X wheel · cozy
non-lethal framing in · F–S tiers · status v1 = Burn/Root/Slow/Haste/Guard/Regen.*

*Next step after sign-off: Slice 0 (remove Callings) → Slice 1 (make the kit cast).*
