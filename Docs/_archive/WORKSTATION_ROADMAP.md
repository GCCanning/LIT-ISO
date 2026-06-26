# Workstation Props: Use-Cases & Roadmap

How each crafting/work prop should feel different to open and use, Minecraft-style — plus
what's shipped vs. proposed next.

## Shipped (2026-06-13)

### Timed + fueled crafting (core system)
`RecipeDefinition` now supports `craftTimeSeconds` and an optional `fuelItemId`/`fuelCount`.
`CraftingSystem` runs a single `ActiveJob` per player: inputs (and fuel) are consumed up
front, then `Tick()` (called from `FoundationBootstrap.LateUpdate`) finishes the job and
grants outputs once the timer elapses. While a job is running, that station can't start a
second timed craft — `CanCraft` reports "Busy: crafting X".

### Crafting panel: per-station focus + progress
`PlayerInteraction.CraftingRequested` now carries the station type through
`GameHudInitializer` → `GamePanelsController.OpenCrafting(StationType)` →
`FoundationCraftingAdapter.SetStationFilter`. Opening a furnace shows furnace recipes (plus
hand-craftable ones) first; pressing C still shows everything. The details pane
(`CraftingView`) now renders a fuel line ("Fuel: Wood 0/1"), a "Time: 6s" label for timed
recipes, and a live green progress bar ("Working... 42%") that refreshes every 0.2s while a
job is active.

### Furnace — Smelting
`smelt_copper` is now a timed, fueled recipe: 2 copper ore → 1 copper bar, 6s, consumes
1 wood as fuel. Matches the Minecraft mental model (ore + fuel in, bar out after a wait).
Future ore types (iron, gold) should follow the same pattern with longer times for
higher tiers.

### Tannery — New station
New `StationType.Tannery`, `tannery` placeable (built via Workbench: 6 wood + 4 stone),
and `tan_hide` recipe: 2 hide → 1 leather, 8s, no fuel. Hides already drop from deer and
fox, so this closes a loop that previously had no destination. `leather` is a new resource
item ready to be consumed by armor/bag recipes (see below).

## Proposed next

### Leather sinks (Tannery payoff)
Leather currently has no consumer. Add Workbench recipes like a `leather_satchel`
(+inventory slots or carry capacity) and a `leather_boots`/`leather_cap` armor line using
`leather` + existing tool-tier materials. This gives the tannery a clear reason to exist
beyond "convert one resource to another."

### Loom — Weaving
New `StationType.Loom`, placeable craftable at Workbench from wood + fiber. Recipe
`weave_cloth`: fiber → cloth, timed (~5s), no fuel. Cloth then feeds tailoring recipes
(cloth bags, banners, bedding) the same way leather feeds armor. Pairs naturally with the
existing `fiber` resource which currently has limited uses.

### Anvil — Smithing
New `StationType.Anvil`, requires being placed near/with a Furnace (mirrors how
`campfire`/`fireplace` already grant `CookingPot`). Move the copper tool/weapon recipes
(`craft_copper_axe`, etc.) from Workbench to Anvil and add timed durations (3-5s) plus a
small chance-based "quality" output (normal/fine) as a long-term hook for the
`FoundationProgression` system. Keeps the Workbench focused on construction items.

### Alchemy Table — Potions
New `StationType.AlchemyTable`. Recipes consume foraged items (existing food/resource
items plus new herbs) into potion items with `FoundationPlayerStats` effects (temporary
stamina/health regen boosts). Timed (~4s), no fuel — the "cost" is the rarer ingredients.
Gives foraging a non-food endpoint.

### Mill — Flour/food chain
New `StationType.Mill`. Recipe `grind_wheat`: wheat → flour, timed (~3s). Flour then
feeds a new `bake_bread` recipe at the CookingPot (flour + water → bread, better
`foodRestore` than `roasted_apple`). Extends the existing farming loop (wheat is already
grown) toward a proper food economy.

### Well — Water resource
New `StationType.Well` (or simple `Container`-style placeable). Provides a `water` item on
interact (with a cooldown, not a craft) — needed by the Mill/bakery chain above and
future farming irrigation bonuses.

## Implementation notes for the next pass
- All new placeables/recipes go in `FoundationContent.cs` using the existing
  `Item`/`PlaceItem`/`Placeable`/`Recipe`/`In`/`Out` factory helpers. The `Recipe(...)`
  helper already accepts `craftTimeSeconds`, `fuelItemId`, `fuelCount` as optional args.
- New `StationType` values are safe to add — only `FoundationCraftingAdapter.StationLabel`
  switches on the enum, and it has a `default` case.
- `PlacementSystem.IsStationInRange` is generic (`p.Def.stationType == st`), so a new
  placeable with the right `stationType` works in range-checks with no extra wiring.
- If a station should share availability with another (like `campfire`/`fireplace`
  granting `CookingPot`), set `placeable.stationType` directly rather than adding new
  enum-specific branches.
