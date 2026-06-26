# 25 — Master Plan to Beta

> The single roadmap that turns LIT-ISO from "a deep engine with a thin surface" into a
> Beta-worthy game. Synthesises the current **wired** state, the LitRPG research brief
> (`23`), the profession/crafter research brief (`24`), and every feature requested so far
> (VFX engine, castable abilities + wheel, emergent F–S classes, the 7-day arc, professions
> & crafting, status system, Callings removal). Written so an AI agent can execute it slice
> by slice.

---

## 0. The headline

We are **much closer than it looks**. Both research passes reached the same conclusion: the
hard architecture is already built and is on the *right* side of genre best-practice. The
work to Beta is **finishing and surfacing**, not redesigning — mostly content and small data
fields, wired through systems that already run.

- The ability economy, affinity-scaled power, Trial-evidence **emergent classes**, an 8-entry
  **profession** system, a **day-7 crystallization** that already computes class *and*
  profession offers, and even an `F..S` grade enum — **all already exist in code.**
- As of this week the **ability layer has ignition**: 4 abilities cast for real on the Q/E/R/F
  wheel through `FoundationAbilitySystem` + a new dispatcher, with `WorldFx` visuals. Equipment
  and Loot systems were just scaffolded into the bootstrap too.
- What's missing is **teeth and skin**: tiers that grant real perks, a crafting chain past
  copper, item quality, a status system, the 7-day arc surfaced to the player, original art
  over the placeholder cubes, and the polish that makes it feel like a game rather than a tech
  demo.

That's a finishing job. This plan is how an AI completes it.

---

## 1. Definition of "Beta-worthy" (our finish line)

A stranger can sit down and play a **complete 7-day loop** that proves every pillar:

1. **Create a world**, spawn into a cohesive, original-pixel-art starter region.
2. **Start with stamina skills**; learn the world through gather / craft / build / fight-or-soothe.
3. **Unlock magic** during the 7 days by discovering an elemental Source; cast 4+ abilities
   with distinct effects + VFX + status effects on a working wheel.
4. **Run the crafter spine**: gather copper → smelt → craft a tool *with a quality + trait*
   (e.g. a *Fine, Keen* axe) → use it → repair it.
5. **Fight or soothe** mobs, take loot, equip gear, feel measurably stronger.
6. **Day 7: crystallize** an emergent **F–S class** *and* a **profession**, each granting a
   real, named entitlement — not just a label.
7. **Save and reload** the whole thing losslessly.
8. It looks intentional, sounds alive, teaches itself, and the player wants day 8.

If all eight hold, stable and validated, we ship Beta.

---

## 2. Where we actually are (honest baseline)

**Live & working:** world gen/streaming, kinematic movement, inventory/hotbar/storage,
placement, crafting (~30 recipes + station gating), farming, mob spawning (with distance
difficulty tiers + night danger + mobs that can resolve ability casts), day/night, weather,
audio, camping/wards, instance pockets (tavern + deterministic dungeons w/ rewards), death/
respawn, map overlay, progression (6 stats, XP channels, affinities, Trial evidence, **8
emergent classes**, **8 professions**, day-7 offers), QoL, the uGUI HUD, and freshly added
**equipment + loot** scaffolding.

**Just shipped (Slice 1):** ability **ignition** — `flash_step`, `mana_bolt`, `ember_spark`,
`mending_light` cast through the real system on Q/E/R/F + hold-X wheel, cursor-aimed
projectiles that damage mobs, a heal, a collision-clamped blink, all with `WorldFx` visuals.
Plus the in-house **VFX Lab** generator.

**Built-but-thin / dark (the Beta gap):**
- Ability deliveries: only Blink / Projectile / Heal are live; Melee/Cone/AreaNova/Snare/
  SelfBuff/Aura are designed, not built. No status effects yet.
- Affinity gaps: `mending_light` (Glimmer) and any Tide spell have **no affinity wired**, so the
  two coziest magic strands don't progress (brief 23, top fix).
- Classes: F–S is **cosmetic** — rarity changes a label, grants no perk; no class **evolution**.
- Professions: a **label with no teeth** — no tier ladder, no perks, no payload.
- Crafting: metal chain **stops at copper** (iron→starmetal exist as ore/material only); flat
  node yields; **no item quality, no traits, no repair** despite the Bible naming them all.
- 7-day arc: not surfaced — no day counter milestone, preview, or crystallization screen.
- Callings: still present and load-bearing; slated for removal.
- Art: placeholder cubes + limited tiles; ability VFX are procedural fallbacks.

---

## 3. Guiding principles (from the research)

1. **Deepen, don't rebuild.** ~80% is there; reach Beta via content + small data fields wired
   into existing systems.
2. **Every number you earn changes the world you see.** Pair stat/level/affinity gains with a
   visible effect (a soothed biome, a brighter hearth, a named tool). Anti-treadmill: never
   scale mobs 1:1 — soothed regions **de-escalate** via ThreatCalm / RegionShift.
3. **Freeze the 6-stat HUD.** STR/DEX/INT/VIT/DEF/LUCK stay the only stats; facets, elements,
   and affinities are tags behind panels.
4. **The cozy thesis must hold at the ceiling.** At least one **cozy capstone** class/profession
   reachable without combat.
5. **Dual identity is a feature.** Class (how you fight/explore) and Profession (what you make)
   progress in parallel — the "chef who also fights." The data already supports it.

---

## 4. The roadmap — phases, slices, acceptance

Each **slice** = one branch off `main`, a small PR, an extended validator pass, and a play-mode
feel check. Phases A→C are the core loop; D adds depth; E is skin; F/G are ship-readiness.
Art (E) runs in parallel throughout.

### Phase A — Finish the combat/ability kit *(the kit feels real)*
- **A1 — Status system v1.** Burn / Root / Slow / Haste / Guard / Regen as timer + one
  modifier, single-stack, icon row; works on player **and** mobs. (`FoundationStatusDefinition`
  + `FoundationStatusController`.) *Brief 22 §7.*
- **A2 — Remaining deliveries.** Add Melee, Cone, AreaNova, Snare, SelfBuff, AuraZone to the
  dispatcher; wire `steady_strike`, `guard_step`, `root_snare`, `stone_skin` to real effects +
  statuses.
- **A3 — Affinity/element fix (do early — brief 23's #1).** Add **Glimmer** and **Tide**
  affinities so `mending_light` and water spells scale F→S like Ember/Root/Stone.
- **A4 — Wheel polish.** Cooldown sweep + cost/ready states on slots; ability cast SFX.
- **Done when:** all 8 starter abilities cast with distinct effects, VFX, and statuses; every
  element scales by affinity; the wheel shows live cooldowns.

### Phase B — Progression identity *(classes & the 7-day arc come alive)*
- **B1 — Remove Callings.** Strip `FoundationCallingDefinition`, the welcome-menu Calling
  picker, `ConfigureLaunch(...callingId)` / `SelectCalling`, and migrate the save field; default
  to the emergent **Wayfarer** class. *(~10 files — own slice.)*
- **B2 — F–S display everywhere.** Map `FoundationClassRarity` and `FoundationAffinityRank`
  onto the existing **`FoundationGrade {F..S}`** enum; show class + tier on HUD/character panel.
- **B3 — Tier entitlements (brief 23's #2).** Each F–S step grants a concrete payload —
  `statBonuses`, a signature ability id, or a title — so the ladder isn't hollow.
- **B4 — Surface the 7-day arc (brief 23's #3).** Add a day counter + day-7 milestone; a
  "trending toward" class/profession **preview** and a daily **digest** (the offers are already
  computed by `BuildClassOffers` / `BuildProfessionOffers`); a **crystallization screen** on
  day 7.
- **B5 — Class evolution.** Add `ClassDefinition.evolvesFromIds` and re-run scoring at later
  milestones so base classes advance (Trailblade → Wildsign Ranger). *The one genre beat we
  don't yet model.*
- **B6 — A cozy capstone.** Define at least one Legendary/Mythic (A/S) class reachable through
  non-combat play.
- **Done when:** a fresh run reaches day 7 and crystallizes a class **and** profession with real
  perks; the player sees it coming via preview/digest; magic-unlock gates correctly.

### Phase C — Crafter & profession depth *(the cozy spine — brief 24)*
- **C1 — Extend the metal chain.** copper → iron → steel → … → starmetal as content
  (ore → bar → tool → recipe). Instantly deepens tools, mining, and Blacksmith. *Brief 24's #1.*
  **Art ready** — `ore_copper/iron/silver/gold/starmetal/manacrystal` and every workstation
  (anvil, furnace, sawmill, grindstone, loom, tannery, alchemy, rune station) already exist in
  `Resources/Decorations` (asset review `26`), so this is pure data with no art blocker.
- **C2 — Craft quality roll.** Per-instance quality on `ItemStack` (skill + LUCK → Bent…Beloved)
  injected via a `Func` delegate into `CraftingSystem`, exactly like the existing
  `StationAvailable` gate. *Highest depth-per-line.* *Brief 24's #2.*
- **C3 — Item tiers + traits.** Surface Plain→Mythwarm tiers and Warm/Keen/Stout… traits in
  tooltips and apply their effects.
- **C4 — Repair loop.** Turn the dead-end durability fields into repair recipes (a return-to-base
  sink). *Brief 24's #4.*
- **C5 — Profession teeth.** F–S profession tier ladder + perks (yield / quality / speed); show
  the day-7 profession pick in the crystallization screen; route Profession XP from every
  production action. *Brief 24's #3.*
- **C6 — Discovery as reward.** Enforce `unlockedByDefault` so recipes unlock through play;
  scale node yields by skill/tool tier. *Brief 24's #5.*
- **Done when:** gather copper → smelt → craft a *Fine, Keen* axe → use → repair → become a
  Blacksmith at day 7 with a real perk.

### Phase D — Equipment, loot & combat economy
- **D1 — Finish equipment + loot** (just scaffolded): equip gear into slots for stat bonuses;
  loot drops from mobs / dungeons / chests, rolled with the C2 quality system.
- **D2 — Combat readability.** Mob telegraphs, clear damage feedback, and non-lethal options
  (soothe / trap / ward) tied to the A-phase abilities & statuses — honoring the cozy thesis.
- **D3 — Dungeon reward tie-in.** Hook the existing dungeon reward/chest loop into loot + quality.
- **Done when:** kill **or** soothe → loot → equip → measurably stronger; dungeons pay out.

### Phase E — Content, art & world cohesion *(Milestone A — runs in parallel)*
- **E1 — Original terrain art.** Replace placeholder cubes with original pixel tops + blocks via
  the Asset Forge pipeline (doc 10 A1/A2).
- **E2 — Ability VFX sheets.** Author the 8-element effect set in the VFX Lab (the 6 missing
  palettes/presets), import to `Resources/Effects`, swap the `WorldFx` fallback. *Brief 22 §8.*
- **E3 — Biome/mob/resource tables** aligned to the Bible names; the starter region (Mosswake)
  reads as a coherent place.
- **E4 — Starter quest chain** (First Flame, First Field → …) as the spine that guides the 7 days.
- **Done when:** first read looks like an intentional pixel game; the starter quests carry a new
  player through the arc.

### Phase H — Settlements & world liveliness *(runs parallel with E; full design in `26`)*
The settlement generator already exists (`IsoSettlementSampler` + `settlements.json`): five
distance-banded tiers **hamlet → village → market town → frontier city**, rank-gated building
pools (the `r1/r2/r3` art), road spine, outskirt farms, and layout presets including a **walled
town with a gate**. ~70% scaffolded. The work is making each tier *look distinct* and *feel alive*:
- **H1 — Tier-gated identity.** Split the flat prop pool per tier and add a **perimeter pass**:
  wooden fences (hamlet/village) → stone walls + gate + guard towers (town/city); lighting ramp
  (`torch_standing` → `lantern_post` → `brazier`); road-material upgrade (dirt → stone) by band.
  Mostly `settlements.json` data + one sampler pass. *Art mostly ready; one wooden-fence gen batch
  is the only gap.*
- **H2 — District-zoned placement.** Stamp buildings/props into purpose districts (plaza, market,
  crafting, guild/library, residential, gate, farm belt) so towns read as designed, not scattered.
- **H3 — NPC wandering (the one genuinely new system).** A settlement-aware populator spawns
  tier-scaled townsfolk that walk roads/between buildings (reuse `Mob` world-query movement,
  passive behaviour; `witch run` cycle as placeholder), with optional day/night schedules.
- **H4 — Vendor & loot tiers.** Turn `stockTier` (1–4) + `services` (market_trade, specialty_vendor)
  into real shop stock and loot quality — ties into Phase C (quality) and Phase D (loot).
- **Done when:** a hamlet (general store, fences, torches, crop ring) clearly differs from a walled
  market town (stone roads, lamps, gate, market square, guild + library, wandering NPCs) and a
  frontier city (grander streets, best vendors/loot).

### Phase F — Systems hardening
- **F1 — Save/load coverage** for all new state (ability loadout, statuses, profession tier,
  item quality/traits, equipment, day counter, class evolution); bump `FoundationSaveData`
  version with migration.
- **F2 — Unified HUD/UX pass:** ability bar, vitals, quest pin, day/arc clock, notifications,
  UI scale.
- **F3 — Stability & validator:** extend `FoundationIntegratedSliceValidator` to cover every
  new system; performance pass; zero soft-locks.
- **Done when:** full round-trip save/reload is lossless and the validator is green.

### Phase G — Beta polish & QA
- **G1 — Audio:** ability / craft / UI SFX + music beds.
- **G2 — Onboarding:** teach the 7-day arc, abilities, and crafting in-context.
- **G3 — Balance:** costs, cooldowns, affinity curves, quality odds, mob tiers, mana economy
  (note: mana currently only refills at a campfire — decide passive regen here).
- **G4 — Playtest & triage** against the §1 checklist; fix the top issues; ship Beta.

---

## 5. Sequencing & effort

| Phase | Unlocks | Size | Depends on |
|---|---|---|---|
| A — Ability kit | Combat/skills feel complete | M | Slice 1 (done) |
| B — Identity | Emergent F–S classes + 7-day arc | M–L | A3 (affinity), can overlap A |
| C — Crafter | The cozy production spine | M–L | independent; can run with B |
| D — Equip/Loot | Gear & combat economy | M | A, C2 (quality) |
| E — Art/Content | Looks & reads like a game | L | parallel throughout |
| H — Settlements | Living, tiered hamlet→city | M–L | E (art), D (vendor/loot); NPC system is new |
| F — Hardening | Save/UI/stability | M | A–D landed |
| G — Polish/QA | Ship-readiness | M | F |

Critical path: **A → B/C (parallel) → D → F → G**, with **E** and **H** alongside the whole way
(H1/H2 are cheap data wins that can land early; H3's NPC system is the one new build).

---

## 6. How an AI runs this

- **One slice, one branch, one small PR.** Order within a slice: *data → runtime → UI →
  validator → feel check.* Never commit to `main`; honor the invariants in `CLAUDE.md`.
- **Verification is part of every slice:** extend the integrated validator, run a play-mode feel
  pass, capture a screenshot/gif, and check the slice's acceptance line above.
- **Specs already exist:** briefs `23` (classes/abilities) and `24` (professions/crafting) are
  the detailed implementation references for B and C; `22` is the ability/VFX spec.
- **Parallelism:** B and C touch mostly different systems and can run as concurrent tracks; art
  (E) is its own track; D waits on C2's quality roll; F gates G.

---

## 7. Beta-exit checklist

- [ ] All 8 starter abilities cast with distinct effects, VFX, and statuses; affinities scale
      every element.
- [ ] Callings removed; emergent class + profession crystallize at day 7 with real entitlements.
- [ ] Class evolution and at least one cozy A/S capstone reachable.
- [ ] Crafter loop: gather → smelt (iron+) → craft with quality + trait → use → repair.
- [ ] Equip gear / take loot / feel stronger; mobs can be fought **or** soothed.
- [ ] Original pixel art on core tiles/props; element VFX sheets in.
- [ ] Settlements read as tiered & purposeful: hamlet vs walled town vs city are visibly
      distinct, with wandering NPCs and tier-appropriate vendors/loot.
- [ ] Starter quest chain guides days 1–7.
- [ ] Lossless save/reload; validator green; no soft-locks.
- [ ] Audio + onboarding + balance pass done.
- [ ] A fresh playtester finishes day 7 and wants day 8.

---

*Sources: this plan synthesises `23_LitRPG_Research_Brief.md`, `24_Profession_Crafter_Research_Brief.md`,
`22_Skills_Abilities_VFX_Design.md`, `15_LitRPG_System_Bible.md`, and a direct read of the live
runtime graph (`FoundationBootstrap`, `FoundationContent`, `FoundationProgression`).*
