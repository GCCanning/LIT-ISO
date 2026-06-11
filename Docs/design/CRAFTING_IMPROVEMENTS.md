# Crafting Improvements — Content Proposals (for owner approval)

> Status: **proposal only — no content changed.** `Core/FoundationContent.cs` is untouched.
> Companion to `GAME_INVENTORY_AND_IMPROVEMENTS.md` (audit 2026-06-10). The UX side of
> crafting (greyed-out uncraftable recipes with "Need X" reasons, station-grouped +
> name-sorted list with section headers, Craft All batch button with max count, missing
> ingredient highlighting) already ships in `CraftingView` / `FoundationCraftingAdapter`;
> this pass added selected-row highlighting and scroll-position preservation. What remains
> is **content**, which lives in Codex's lane and needs sign-off.

## 1. Give dead-end drops a sink (audit appendix: hide, slime_goo)

Both items only drop from mob defeat and currently have zero recipe uses. Even before
melee combat lands (audit rec #1), recipes can be added now — they simply stay greyed
out with "Need Hide x2", which the crafting UI already communicates well.

| Proposed recipe | Station | Inputs | Output | Why |
|---|---|---|---|---|
| Bedroll | Workbench | 2 hide, 4 fiber | bedroll (placeable, `isCampsite`-like rest point, no ward) | Pays off hide; soft respawn (now implemented) can prefer it later |
| Slime Lamp | Workbench | 2 slime_goo, 1 lantern | slime_lamp (light r2.8, green tint) | Pays off slime_goo; cheap decoration variety |
| Repair Paste | CookingPot | 1 slime_goo, 2 fiber | repair_paste (use on held tool: +25% durability) | Softens durability frustration; second slime_goo sink |

## 2. Shovels: give them a job or cut the line (audit rec #12)

Three of thirteen tools (wood/stone/copper shovel) do nothing. Two options, in order of
preference:

1. **Dig mechanic (small):** shovel primary-use on `dirt`/`sand` converts the cell to
   `soil` (one-step alternative to hoe tilling on those blocks only), and on beach sand
   has a small chance (LUCK-scaled) of a buried cache (2-4 stone / 1 copper_ore /
   1 wheat_seeds). Fixes "beach biome is empty" at the same time.
2. **Cut:** remove the three shovel recipes and items. Cheaper, but shrinks the
   tool matrix and orphans "Threadsmith"-style crafting XP sources.

## 3. Widen the food chain (audit rec #11; eat path now implemented)

Food is now consumable (LMB with food on the hotbar restores `foodRestore` HP), which
makes cooking depth worthwhile. Current chain is apple → roasted_apple and
carrot+wheat → camp_stew. Proposals:

| Item / recipe | Values | Notes |
|---|---|---|
| bread (CookingPot: 2 wheat) | foodRestore 18 | Gives wheat a use besides camp_stew |
| roasted_carrot (CookingPot: 1 carrot) | foodRestore 18 | Mirrors roasted_apple; teaches "cooking upgrades food" |
| miner's stew (CookingPot: 1 camp_stew + 1 copper_ore... or hide) | foodRestore 40 + buff: +1 mining yield, 3 min | First buff meal; `activeBuffs` list already exists in progression |
| scout's loaf (CookingPot: 1 bread + 1 apple) | foodRestore 24 + buff: +10% move speed, 3 min | Second buff archetype |

Buff meals need a small consumer in code (timed modifier store); raw-restore items work
with the new eat path as-is, so bread/roasted_carrot are zero-code content adds.

## 4. Recipe-list polish that needs content metadata (optional)

- **Recipe unlock tiers:** every recipe is visible from minute one (29 rows). A
  `revealStation`/`revealSkill` field would let the list hide copper recipes until the
  player has seen a furnace, keeping the early list short. UI already supports any
  filtering the adapter applies.
- **Categories within stations:** a `group` field (Tools / Building / Food / Stations)
  would allow second-level headers in the existing station-grouped list.

## 5. Smelting QoL (content-side)

`smelt_copper` (2 ore → 1 bar) is the only Furnace recipe. If iron tease (audit rec #15)
lands, consider also `smelt_iron` and a `torch` recipe (1 wood + 1 fiber, Hand) so the
Furnace/Hand groups don't look like single-entry stubs.

---

**Priority suggestion:** 3 (bread + roasted_carrot, zero-code) → 1 (hide/slime sinks,
pairs with combat rec #1) → 2 option 1 (shovel dig) → 5 → 4.
