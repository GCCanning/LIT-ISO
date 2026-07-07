# LIT-ISO

An original isometric survival-crafting **LitRPG**, built in Unity. You wake up transmigrated into another world with seven days to prepare before an all-seeing System grades everything you did and hands you a class. From there: a hand-built homestead, distance-gated dungeons and towns, elemental affinities, and a world of NPCs worth actually knowing.

Built solo, in the open, with an AI collaborator doing most of the implementation under human direction. Pre-alpha — playable, still finding its shape.

<p>
  <img src="Assets/Resources/Tiles/bt_forest_tile_8.png" width="72" height="72" alt="forest tile">
  <img src="Assets/Resources/Tiles/bt_frozenmountain_tile_6.png" width="72" height="72" alt="frozen mountain tile">
  <img src="Assets/Resources/Tiles/bt_grotto_grotto_glowing_moss_09.png" width="72" height="72" alt="grotto tile">
  <img src="Assets/Resources/Tiles/bt_badlands_badlands_cinder_01_11.png" width="72" height="72" alt="badlands tile">
</p>

<sub>A few of the shipped biome tiles — original, PixelLab-generated art. No polished gameplay screenshot yet; that's still on the to-do list.</sub>

## The pitch

You transmigrate into another world. The System speaks first: *"You have 7 days to prepare. Good luck."* For seven in-game days, everything you do is silently scored — gathering, fighting, building, exploring, trading, how you treat people. On day seven you're pulled into a floating void, and the System hands you a rank (F–S) and a set of class offers scaled to how well you did.

From there the long game opens up: a **Class** (combat identity — Ranger, Mage, Duelist...) and a **Profession** (crafting identity — Blacksmith, Alchemist, Farmer...), both leveled by actually doing the thing. Distance from your starter town scales danger and reward — farther out means harder enemies, deeper dungeons, rarer loot, and higher-tier settlements. Stats are mechanical, not cosmetic: STR hits harder *and* jumps higher, DEX moves faster and cools down quicker.

The full design bible lives at [`Docs/IsoCoreFoundation/00_Canonical_Game_Design.md`](Docs/IsoCoreFoundation/00_Canonical_Game_Design.md) — start there for anything design-related.

## Where things stand

This is an active WIP, not a finished game. Rough snapshot across the feature set (see [`Docs/FeatureAtlas.html`](Docs/FeatureAtlas.html) for the live, searchable version — open it in a browser):

| Status | Meaning | Count |
|---|---|---|
| Shipped | Built and working | 20 |
| Partial | Built, not fully wired end-to-end | 12 |
| Planned | Designed, not started | 10 |
| Blocked / Deferred | Waiting on something else | 3 |

Shipped so far: procedural isometric terrain across 8 JSON-driven biomes, harvest/farm/build/craft loops, day/night cycle, the transmigration intro, the 7-day trial scoring spine, abilities and combat, a working HUD and minimap, and a character creator. In flight: the void class-assignment ceremony end-to-end, elemental affinities, town tiers and NPC economy, organic worldgen rules.

## Tech

- **Engine:** Unity `6000.3.11f1` (Unity 6.3), **Built-in render pipeline** (not URP)
- **Language:** C#
- **Canonical assembly:** `IsoCore.Foundation` — the legacy `Assembly-CSharp` world is being retired in favor of it
- **Grid:** isometric (`IsometricZAsY`), world-query movement (no physics pushing), pooled-sprite manual iso-sort rather than Unity's Tilemap renderer
- **Platform target:** Windows
- **Version control:** Git + Git LFS for binaries (`.png`, `.wav`, `.fbx`, `.ttf`, etc. — see `.gitattributes`)

A handful of engineering invariants are load-bearing and intentionally not "cleaned up" — see `AGENTS.md` for the full list (grid cell size, sort axis, height-layer scheme, step-height climbing). If something there looks odd, it's probably deliberate.

## Repo layout

```
Assets/
  IsoCoreFoundation/   canonical game code, data, and original art root
  Scenes/              IsoCoreFoundation.unity is the entry scene
  Resources/Tiles/     shipped biome tile art (bt_<biome>_<name>.png)
  StreamingAssets/      worldgen JSON (biome_suite.json, settlements.json)
  Generated/           AI-assisted art pipeline output + review sheets (WIP/scratch-heavy)
Docs/                  design bible, handovers, feature atlas, doc index
Tools/                 editor utilities, asset pipelines, build scripts
Build/                 local build output (not committed)
```

## Getting started

1. Open the project in **Unity 6000.3.11f1** (Unity Hub will offer to install it if you don't have it).
2. Open scene `Assets/Scenes/IsoCoreFoundation.unity` and press Play — `FoundationBootstrap` wires up the world.
3. Editor menu `Tools/LIT-ISO/ISO-Core Foundation/` has entry points for building the Foundation scene, generating content assets, validating the Foundation, and running the golden-path smoke test.
4. Worldgen changes need a **new world** to take effect — existing saves won't pick up biome/rule edits.

## Documentation map

Start at [`Docs/HANDOVER.md`](Docs/HANDOVER.md) — the single live handover, updated every session. Then:

- [`Docs/FeatureAtlas.html`](Docs/FeatureAtlas.html) — every feature, status, and the files/assets behind it
- [`Docs/IsoCoreFoundation/00_Canonical_Game_Design.md`](Docs/IsoCoreFoundation/00_Canonical_Game_Design.md) — design source of truth
- [`Docs/IsoCoreFoundation/Impact_Analysis_2026-06.md`](Docs/IsoCoreFoundation/Impact_Analysis_2026-06.md) — ranked to-do
- [`AGENTS.md`](AGENTS.md) — engineering contract (invariants + git workflow); `CLAUDE.md` mirrors it for AI sessions
- [`Docs/INDEX.md`](Docs/INDEX.md) — full doc index

## Branching

Three long-lived branches: **Development** (default, all active work) → **Working** (polished features only) → **Production** (releases, PR-only). `main` is vestigial pre-restructure history.

Branch `feat/<task>` off Development, PR back in. Once a feature's polished, merge it into Working; Working merges into Production at release points. Never commit directly to Working or Production, never force-push.

## Art & credits

Shipped art is an original, **PixelLab-generated** tile/prop/asset set — the game is built to that style, not to any reference project's actual pixels or content. Character sprites use the Liberated Pixel Cup (LPC) generator; full attribution is in [`CREDITS_CHARACTERS.txt`](CREDITS_CHARACTERS.txt) and [`CREDITS_VFX.txt`](CREDITS_VFX.txt).

Reference projects (ISO-CORE / ISO-Tile) inform system shape and scale only — never their pixels, audio, or content.

---

Unreleased personal project. Not currently licensed for reuse or redistribution.
