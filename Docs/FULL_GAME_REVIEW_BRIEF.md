# LIT-ISO — Full Game Review & Reorganization Brief

> **Purpose:** Hand this single document to a capable AI agent (or human reviewer)
> to perform a complete, top-to-bottom review of LIT-ISO and produce a concrete
> reorganization + action plan, a features inventory, and supporting deliverables.
> Self-contained: a reviewer with repo access needs nothing else to start.

---

## 0. Your role & mandate

You are a **senior technical producer + lead engineer + design reviewer** doing a
one-time **full audit** of the LIT-ISO project. You are NOT here to add features.
You are here to **understand everything, judge its real state honestly, and leave
behind a clean map and a prioritized plan** so the next person (likely a solo
owner + one AI) can ship a Beta.

Bias toward **honesty over optimism**. The project's own docs admit it is "a deep
engine with a thin surface." Your job is to verify whether that's still true,
quantify the gap, and lay out the shortest credible path to a playable vertical
slice and then Beta.

Work in **read-only / analysis mode** unless explicitly told to change files.
Propose moves; don't execute large reorganizations until the owner approves the plan.

---

## 1. What LIT-ISO is (orientation)

- **Genre:** Original isometric **survival-crafting LitRPG**. Tagline: *"a survival
  game, like a LitRPG book."*
- **Frame:** Seven-Day-Trial — the player transmigrates into another world, has 7
  days to gather/craft/fight/explore, is silently graded, then on Day 7 the "System"
  assigns an emergent **F–S class** and a **profession** scaled to performance.
- **Substance:** cozy survival-crafting depth (gather → smelt → craft tools w/
  quality+traits → build a homestead), use-based progression, distance = danger +
  rarity, affinity-flavoured magic on a Q/E/R/F ability wheel.
- **Engine:** Unity 6.3, URP/2D, isometric (`IsometricZAsY` grid).
- **Art direction (LOCKED):** the **PixelLab-generated** tile/prop/asset set is
  canonical. LoRA/ControlNet/Greenwake/Reference32 passes are scratch, not direction.
- **Canonical runtime:** `IsoCore.Foundation` (scene `Assets/Scenes/IsoCoreFoundation.unity`,
  built by `FoundationBootstrap`). Legacy `Assembly-CSharp` world is being retired.

### Start-of-review reading (in this order)
1. `CLAUDE.md` + `AGENTS.md` — the engineering contract & invariants.
2. `Docs/IsoCoreFoundation/00_Canonical_Game_Design.md` — **the design source of truth.**
3. `Docs/HANDOVER_2026-06-17.md` (then `_2026-06-13`) — latest full handover.
4. `Docs/IsoCoreFoundation/25_Master_Plan_To_Beta.md` — the existing roadmap & honest baseline.
5. `Docs/INDEX.md` — the doc map (note: it admits root docs are stale working notes).

### Hard invariants — DO NOT propose "fixing" these
`IsometricZAsY`; `cellSize (1,0.5,1)`; sort axis `(0,1,-0.26)`;
`TilemapRenderer.mode = Individual`; `Height_N = Unity layer 10+N`; world-query
movement (foot collider trigger-only); `maxWalkStepHeight = 1` (walk up one step,
jump stacks for taller cliffs). If you think one is wrong, FLAG it for the owner —
do not silently change it.

---

## 2. Scope — review the game *in its entirety*

Cover every layer. For each, capture: **what exists, what actually works, what's
stubbed/placeholder, what's missing, and the risk if left as-is.**

**A. Code & architecture**
- The ~324 C# scripts. Map the two assemblies (`IsoCore.Foundation` vs legacy
  `Assembly-CSharp`): what's live, what's dead, what's duplicated across both.
- Core systems: bootstrap/scene build, world gen & streaming, movement, inventory/
  hotbar/storage, placement/building, crafting + stations, farming, harvesting,
  mobs/combat, abilities + VFX (`FoundationAbilitySystem`, `WorldFx`), day/night,
  weather, camping/wards, dungeons/instances, progression (stats, XP channels,
  affinities, Trial evidence, Day-7 crystallization), save/load, UI (in-game HUD,
  character creator, menus).
- Verdict per system: **Working / Partial / Stub / Broken / Dead.**
- Architecture health: coupling, god-objects, the bootstrap's manual wiring,
  ScriptableObject/data-driven boundaries, assembly definitions, test coverage.

**B. Content & data**
- Recipes, item defs, biome/tile/prop pools, mob defs, class/profession/affinity
  tables. How much of the designed system is actually populated vs scaffolded?
- The crafting chain depth (docs say "past copper" is thin) — verify.

**C. Art & assets**
- Confirm the PixelLab set is what's wired; quantify coverage vs gaps (tiles, props,
  characters, animations, UI). Identify placeholder cubes / missing art blocking
  "looks intentional."
- Flag any non-original or scratch assets still referenced in shipping scenes.

**D. Game-feel / playable surface**
- Can a stranger actually play a coherent loop today? Walk the **8-point
  Beta definition** in `25_Master_Plan_To_Beta.md §1` and grade each point
  (Done / Partial / Missing) with evidence.
- The "deep engine, thin surface" gap: what's built-but-not-surfaced-to-the-player?

**E. Build, tooling & ops**
- The `Tools/` pipeline (AssetForge, SpriteForge, PixelLab, BiomeSketch, build .bat/
  .ps1 scripts), the `Build/` outputs, Git LFS setup, `.gitignore`/`.gitattributes`
  hygiene, scene/editor entry points under `Tools/LIT-ISO/...`.

**F. Documentation & project organization** (this is half the job — see §3)
- The `Docs/` sprawl: numbered series 00–36, loose root docs, `_archive`,
  `_lpc_backup`, `design/`, `handoff/`, `agent-comms/`. What's canonical, what's
  superseded, what's duplicated, what's dead.

---

## 3. The organization problem — propose a clean structure

The repo has accreted: ~50+ design docs across five doc subfolders, scratch art
pipelines, legacy + canonical code side by side, and stale "two-agent / lanes"
references the contract now disowns. **Design a target folder structure** and a
**safe migration path** to it.

Deliverable: a proposed tree for `Assets/`, `Docs/`, and `Tools/` that:
- Cleanly separates **canonical vs legacy vs scratch** (code, docs, art).
- Gives `Docs/` a single obvious entry point and kills duplicate/superseded files
  (move to a clearly-marked `_archive`, don't delete blindly — preserve git history).
- Establishes a naming convention (the numbered `Docs/IsoCoreFoundation/NN_*` series
  is decent — extend or rationalize it).
- Respects Unity constraints: **never move an asset without its `.meta`**; moving
  scripts changes GUIDs/asmdef membership — call out any risk to scene/prefab refs.
- Is delivered as an **ordered, reversible migration checklist** (git-mv steps,
  grouped into small PRs), NOT a single big-bang move. Note which steps need Unity
  open to re-serialize references.

---

## 4. Required deliverables

Produce these as files in the repo (propose paths; e.g. a new `Docs/_review_2026-06/`):

1. **`REVIEW_FINDINGS.md`** — the full audit. Per-system Working/Partial/Stub/Broken
   verdicts with file references, the honest "can you play it" assessment, top risks,
   and quick wins. Lead with a 1-page executive summary.

2. **`ACTION_PLAN.md`** — a prioritized, sequenced plan to (a) a **playable vertical
   slice**, then (b) **Beta** (per the 8-point definition). Use clear milestones with
   exit criteria, effort sizing (S/M/L), dependencies, and "do-this-first" ordering.
   Reconcile with the existing `25_Master_Plan_To_Beta.md` — supersede or update it,
   don't fork a competing plan.

3. **`FEATURES.md` + a polished `FEATURES.pdf`** — a complete feature inventory for
   the owner / a pitch deck. Group by pillar (Trial frame, survival-crafting,
   progression/classes, world/exploration, magic/combat, building/homestead, UX/art).
   For each feature: one-line description, **status badge** (Shipped / In-progress /
   Planned / Cut), and player-facing value. Make the PDF presentable — cover page,
   contents, status legend, clean typography. (Generate via the repo's available
   doc/pdf tooling.)

4. **`REORG_PLAN.md`** — the §3 target structure + the ordered migration checklist.

5. **`DOC_MAP.md`** — a rebuilt, accurate replacement for `Docs/INDEX.md`:
   every doc classified Canonical / Reference / Superseded / Archive, with a
   one-line purpose each. Flag the contradictions you find (e.g. `INDEX.md` still
   references superseded bibles and a second "Codex" agent the contract disowns).

### Suggested extras (do if time allows — high value)
- **`RISK_REGISTER.md`** — ranked risks (technical debt, art gaps, save-format
  fragility, legacy entanglement) with likelihood/impact/mitigation.
- **`SYSTEMS_MAP.md` or a diagram** — how the live systems wire through
  `FoundationBootstrap`; the data flow from world-gen → play loop → Day-7 crystallize.
- **`QUICK_WINS.md`** — <1-day fixes that visibly improve the playable surface.
- **`TEST_PLAN.md`** — a manual play-mode checklist that walks the 7-day loop and
  asserts each pillar, plus what automated coverage is worth adding.
- **A one-page "state of the game" owner brief** in plain language (no jargon).

---

## 5. Method & ground rules

- **Verify, don't trust the docs.** The docs are extensive but self-admittedly
  partly stale. When a doc claims a system works, find the code/scene that proves
  it (or prove it doesn't). Cite `file:line`.
- **Distinguish "exists in code" from "reachable by a player."** This is the central
  honesty test for this project.
- **Quantify.** Counts, percentages, coverage — "30 recipes but only ~6 past copper,"
  not "crafting is thin."
- **Respect the invariants (§1) and the locked PixelLab art direction.** Flag, don't fix.
- **No big-bang changes.** Everything reorganizational is a *proposal* + a reversible,
  PR-sized checklist until the owner approves.
- **Preserve git history** on any move (use `git mv`; never delete-and-recreate).
- Follow `AGENTS.md` git workflow: branch off `main`, small PRs, never commit to `main`,
  binaries via Git LFS.

---

## 6. Output shape

Start by **reading the §1 set**, then post a short **orientation note** (what you
found at a glance + your planned approach) before going deep. Then work the scope in
§2, and finish by producing the §4 deliverables. End with a **5-bullet TL;DR** the
owner can read in 60 seconds: *is it closer to a tech demo or a game, the single
biggest blocker, the fastest path to "playable," and the top 3 next actions.*
