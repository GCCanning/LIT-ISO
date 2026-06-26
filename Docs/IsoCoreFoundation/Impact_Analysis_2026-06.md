# LIT-ISO — Impact Analysis (2026-06)

> Companion to `00_Canonical_Game_Design.md`. Ranks the most impactful changes to
> make **right now**, given the gap between the current build and the canonical
> vision. Two lenses: **(A) high-leverage features** that move toward the vision,
> and **(B) cleanups/fixes** to current problems. Ranked by **impact ÷ effort**.

---

## How to read this

- **Impact** = how much it moves the game toward the canonical vision or unblocks
  other work. **Effort** = rough build cost (S/M/L/XL).
- The single biggest truth about this project right now: **the systems are far ahead
  of the art and the hygiene.** The Foundation slice already does terrain, harvest,
  farm, build, craft, dungeons, interiors, stats, quests, abilities, and a trial
  spine. What's missing is (1) the *art* to make it not look like coloured blocks,
  (2) several *named owner mechanics* (guild-building, camping/night, affinities,
  boats), and (3) *repo hygiene* so the work isn't at risk.
- **Therefore the highest-impact moves are not "build more systems" — they are
  "lock the art direction" and "secure + reconcile what exists," then add the few
  owner mechanics that define the game's identity.**

---

## Ranked summary (the whole picture at a glance)

| # | Item | Lens | Impact | Effort | Priority |
|---|---|---|---|---|---|
| 1 | Commit & branch-land the ~3,200-file working tree | B | High | S | **DO FIRST** |
| 2 | Lock ONE art style + ship the terrain/character set | A/B | Very High | L | **DO FIRST** |
| 3 | Wire Trial scoring → rank → class offers → skill points (close the day-7 loop) | A | Very High | M | **DO FIRST** |
| 4 | Reconcile design docs + the `maxWalkStepHeight` invariant drift | B | Med | S | **DO FIRST** |
| 5 | Night danger + campfire safe-radius + tent sleep (core survival identity) | A | Very High | M | High |
| 6 | Real unified save/load (`FoundationSaveData`) | A/B | High | M | High |
| 7 | Guild building w/ paint-grid interior + station bonuses | A | High | L | High |
| 8 | Dungeon prep loop: rarity-gated, hint-found, consumables required | A | High | M | High |
| 9 | Affinities: element→effect, usage + drop growth, rank ceiling | A | Med-High | M | Med |
| 10 | Town tiers + market services (store/guild/library/alchemist/forge) | A | High | L | Med |
| 11 | Purposeful NPCs via existing character creator | A | Med | M | Med |
| 12 | STR→jump, finish stat mechanics pass | A | Med | S | Med (quick win) |
| 13 | Boat-gated continents | A | Med | L | Later |
| 14 | Horses | A | Low-Med | M | Later |
| 15 | Generated-asset / repo policy enforcement (`.gitignore`, LFS, quarantine) | B | Med | S | Ongoing |

**Do-first cluster (this sprint): #1, #2, #3, #4.** They are the unblockers — one is
trivial-but-urgent (commit), one is the long pole (art), one closes the signature
loop (day-7), one removes contradiction risk (docs/invariant).

---

## A. High-leverage features toward the vision

### A1 — Close the Seven-Day loop: scoring → rank → offers → skill points  `[#3, Very High / M]`
This is the **game's identity moment** and it's the most "almost done" big feature.
The UI exists (`ClassAssignmentView` void ceremony, rank display, offers with rarity,
evidence receipts) and the **data spine exists** (Evidence/Marks/XP channels/Titles/
Classes/Professions all validate). The **runtime that turns scored actions into a
rank and generates class offers is still a TODO** (ledger #134). Wiring it:

- makes the 7 days *mean something* (right now they're ungraded in practice),
- delivers the owner's **skill-points-scale-with-rank** mechanic (the concrete payoff),
- unlocks the entire post-trial long game.

Highest impact-per-effort of the big features because so much scaffolding is already
in place. **Build this next.**

### A2 — Night danger + camping wards  `[#5, Very High / M]`
The owner's most distinctive survival mechanic and currently absent. Pieces to add:

- night spawns more powerful/aggressive mobs that **actively hunt** the player;
- **campfire projects a safe radius** at night (campfire prop already exists & is
  craftable/animated — add the radius + aggro-suppression);
- **tent** to sleep and fast-forward night, usable only inside the radius;
- **camp-gear rarity vs. area-danger** check → ward-failure chance → gear damage.

This single system delivers "survival, like a LitRPG book," gives travel its tension
(A-travel below), and makes preparation matter. Medium effort because mob AI, day/
night clock, and the campfire prop already exist — this is mostly new behavior glue.

### A3 — Guild building with paintable interior  `[#7, High / L]`
The owner's chosen base-building system (replaces doc-15's homestead ladder). Reuses
two things the build already has: the **BiomeSketch paint-the-grid tool** and the
**enterable pocket-interior tech** (taverns/library). New work: guild as a rankable
entity, **interior grid that expands with rank**, **paint-from-inventory** inside it,
and **craft-station bonuses that scale inside the guild**. Large but high-identity;
schedule after the do-first cluster and A2.

### A4 — Dungeon prep loop  `[#8, High / M]`
Reframe existing dungeons (deterministic rooms, tiered portals, rewards, history all
exist) to match the vision: **rare + hint-gated** spawning (not free), **rank by
distance**, and a **required prep checklist** (food, water, potions, sharpening
stones, mana stones). New consumable item types + a prep-gate UI (doc-17 checklist
view). Turns dungeons from "walk into a portal" into "mount an expedition."

### A5 — Affinities  `[#9, Med-High / M]`
Extend the existing `ability-affinity-core` slice to the owner's spec: each element
maps to an **effect verb** (ice=slow/debuff, fire=burn DoT, poison=DoT), affinities
**grow via usage + rare drops**, and the **ceiling is gated by trial rank**. Gives
combat/magic flavour and feeds the rank-matters theme.

### A6 — Town tiers + market services  `[#10, High / L]`
Procedural towns at four tiers (campsite→village→town→city) where **rank gates item
rarity, cost, accessibility** and exposes services (general store, guild, library,
alchemist, forge). Large because it needs an economy/vendor layer (currently "not
extracted" per the retirement register) and town placement in worldgen. High impact
on the explore-and-trade loop; sequence after survival + dungeons.

### A7 — Stat mechanics finish (incl. STR→jump)  `[#12, Med / S]`
Quick win. DEX→speed/cooldown already in; add **STR→jump height** and confirm the
rest of the six stats have a real mechanical hook. Small, satisfying, supports
"stats matter."

### A8 — Purposeful NPCs  `[#11, Med / M]`; A9 — Boats  `[#13, Med / L]`; A10 — Horses  `[#14, Low-Med / M]`
Vision features, but later. NPCs need a dialogue/role layer (start with service +
hint-givers using the existing character creator). Boats gate the *next* continent —
only matters once the starter continent is full enough to leave. Horses are a
mobility nicety best added after travel/camping feel is proven.

---

## B. Cleanups & fixes to current problems

### B1 — Land the ~3,200-file uncommitted working tree  `[#1, High / S]`  **URGENT**
The biggest *risk* in the project. A large amount of 06-12/13 work (UI overhaul,
worldgen rules, menu, biome tooling) sits **uncommitted in REVIEW** across the tree,
plus thousands of generated PNGs. This is one bad `git reset` or disk event from
serious loss. Action:
- triage the tree: commit genuine source/asset work onto its branch(es) in small PRs;
- decide the generated-asset folders (`$out/`, `GeneratedAssets/`, AI Toolkit) →
  commit-to-LFS or `.gitignore` (ledger #41, #42 are open on exactly this);
- clear ledger #36 ("commit remaining working-tree drift").
Low effort, high protection. **Do before any new feature work.**

### B2 — Art style-lock: **RESOLVED → PixelLab assets**  `[#2, Very High / L]`
> **OWNER DECISION (2026-06): the PixelLab-generated tiles, props, and assets are
> the canonical art set. That is what the game ships with.** This closes the
> long-standing style-lock question — stop spawning parallel unaccepted experiments
> (Greenwake, Reference32, black-mage v6–v13, LoRA/ControlNet passes); those become
> scratch, not the art direction.

Remaining work is now execution, not decision:
- **Promote** the PixelLab tiles/props into `Assets/Resources/Tiles` /
  `Resources/Decorations` / `Tilemaps` with clean Sprite import metas (PPU/pivot
  consistent) and provenance — many are already promoted.
- **Wire the sampler/renderer** to select the PixelLab art by semantic role per biome
  (in flight via the organic worldgen work).
- **Quarantine the rejected experiments** out of the repo (see B1/B5): the
  `Assets/Generated/LoRA`, `_Review`, SpriteForge `$out/`, and BiomeSketch review
  outputs are no longer the art path and should be untracked to stop repo churn.
- **Fill gaps** the PixelLab set is missing (player character, mobs, some props) by
  generating *to the PixelLab style*, not new styles.

This makes B2 a normal large build task rather than a blocked decision — the biggest
remaining lever on game "feel."

### B2b — Current refinement priorities (owner, 2026-06)
The owner's near-term focus is **refining what exists**, not new vision features.
Ordered as given:
1. **Biomes need work** — generation rules + per-biome look (ties to the organic
   worldgen work and the PixelLab tile pools).
2. **Shaders** — sprite/lighting shaders for depth and mood.
3. **Lighting** — day/night, ambient, campfire/glow; overall atmosphere.
4. **Game feel** — the moment-to-moment polish across the above.
5. **Build `.bat` robustness** — `Tools/BuildGame.bat` / `BuildScript.cs` must build
   a working `.exe` *even after new content/scenes/scripts are added*, so the owner
   can always test the real game (see B7 below).
6. **Dungeon edge-collision** — invisible perimeter wall (see B8 below).

### B7 — Make the build `.bat` resilient  `[High / S-M]`
The owner needs to build and play-test the real `.exe` reliably. Current build tooling
(`BuildScript.cs`, `BuildGame.bat`, `GameBuilder.cs`) was recently pointed at
`MenuScene` + `IsoCoreFoundation` (good), but it must **not break when new scenes,
scripts, or assets are added**. Harden it to: auto-include the canonical scenes,
fail loudly with a readable error (not a silent half-build), validate scenes exist
before building, and write `build_info.txt`. Small/medium effort, high value because
it unblocks all play-testing of the refinements above.

### B8 — Dungeon perimeter invisible-wall collision  `[Med / M]`
**Owner spec:** dungeon/instance edge tiles that border the void must collide at the
**outer edge of the tile** — picture an invisible wall standing at the tile edge that
faces the void, rising infinitely upward, wrapping the entire perimeter of edge tiles
that touch nothing. The player (who occupies the middle of a tile) should be stopped
exactly at that border, not allowed to drift over the lip into the void. Implement as
a per-cell occupancy/solid flag on the void-facing edges of the walkable region (the
Foundation already has a per-cell occupancy layer and world-query collision — this is
adding void-border solids to the dungeon/instance generators rather than new physics).
Pairs with the 06-10 "void rework" (walkable tiles only + void particles).

### B2-prior — (superseded) Lock ONE art style and ship the core set
> Superseded by B2 above — the decision is made (PixelLab). Kept as a pointer.

### B3 — Reconcile design docs + the `maxWalkStepHeight` drift  `[#4, Med / S]`
Two contradictions to close now that the canonical bible exists:
- **Docs:** `00_Canonical_Game_Design.md` now supersedes 15 & 17 (done in this batch).
  Make sure `AGENTS.md`/`CLAUDE.md` and onboarding reading lists point at 00.
- **Invariant drift:** `AGENTS.md`, `CLAUDE.md`, and doc 16 all list
  `maxWalkStepHeight = 0` as a **hard, do-not-fix invariant** — but it was changed to
  **1** on 2026-06-13 by owner direction (`from-claude.md`). Either restore 0 or
  (recommended, since the owner asked for walk-up-one-tile) **update the invariant
  text in all three contract docs to `=1`** so "invariant" stays trustworthy. Small
  but important: contradictory invariants erode the whole contract.

### B4 — Real unified save/load  `[#6, High / M]`
Saves exist piecemeal (dungeon v8, map v10, explored cells) but the unified
`FoundationSaveData` (progression, inventory, modified cells, crops, clock, guild,
camp state) is still WIP. The vision adds **a lot** of persistent state (guild
interior layout, affinities, trial progress, boat/continent unlocks) — get the
single save spine solid **before** those land, or they'll each invent ad-hoc
persistence. Cross-lane (Codex core + Claude menu/continue); already has an agreed
path.

### B5 — Generated-asset/repo policy enforcement  `[#15, Med / S, ongoing]`
Doc 22's "Unity imports only promoted assets; review/training/LoRA output stays out
of `Assets/`" is the right policy but is leaking (the working tree shows review PNGs
and `$out/` churn). Enforce via `.gitignore` + a quarantine convention so the repo
stays lean and Unity doesn't import junk. Pairs with B1.

### B6 — Continue the legacy retirement (background)  `[Med / M, ongoing]`
Migration is well underway (inventory/stats/quests extracted; adapters detached from
legacy singletons). Keep extracting weather/towns/economy **into Foundation** as A6/
A2 need them, then delete the retired `Assembly-CSharp` world per doc 07 Phase D.
Don't let the two stacks linger longer than necessary — but this is steady-state, not
a fire.

---

## Recommended sequencing

1. **Sprint 0 (hygiene, days):** B1 commit/triage tree · B3 doc + invariant
   reconcile · B5 asset policy · A7 STR→jump quick win.
2. **Sprint 1 (identity loop):** A1 day-7 scoring→rank→offers→skill points ·
   B4 unified save/load (parallel, Codex).
3. **Sprint 2 (survival identity):** A2 night/camping wards · A4 dungeon prep loop.
4. **Sprint 3 (base + flavour):** A3 guild paint-grid interior · A5 affinities.
5. **Sprint 4+ (world breadth):** A6 town tiers/markets · A8 NPCs · A9 boats ·
   A10 horses.
6. **Throughout:** B2 art style-lock (owner decision + steady promotion) and B6
   legacy retirement run as continuous tracks, not blocked behind sprints.

**The one thing to decide today:** B2's **single art style-lock**. It's the long
pole, it's gating the game's whole "feel," and it's the only do-first item that is a
*decision* rather than a build task. Everything else can proceed in parallel once the
working tree is committed (B1).
