# Night Danger + Campfire Wards — A2 (2026-06-13)

Implements Impact Analysis item **A2** additively on top of the existing
day/night clock, mob spawner, and `FoundationCampingSystem` (which already
had per-prop campfire wards and a "rest until dawn" interaction).

## Files changed

- `Assets/Scripts/IsoCoreFoundation/Core/DayNightSystem.cs`
  - Added `public bool IsNight => NightFactor >= 0.45f;` — a single shared
    night-danger signal (same threshold the camping system already used
    internally).
- `Assets/Scripts/IsoCoreFoundation/Core/FoundationConfig.cs`
  - New `[Header("Night")]` block with tunables (see below).
- `Assets/Scripts/IsoCoreFoundation/Survival/FoundationCampingSystem.cs`
  - `Init(...)` now takes an optional `FoundationConfig cfg` (bootstrap passes
    `config`).
  - Added public `IsNight` (delegates to `DayNightSystem.IsNight`).
  - `ActiveCampRadius` now falls back to `FoundationConfig.campfireSafeRadius`
    if a campsite prop's own `campWardRadius` is 0 (existing campfire/fireplace
    defs already set 5.5/7.0, so this is a safety net for future props).
  - `RollMobSpawnWard` now factors in `FoundationConfig.campfireWardStrength`:
    breach chance += `(1 - campfireWardStrength)`, on top of the existing
    per-mob `campWardIgnoreChance` and tier-gap scaling.
- `Assets/Scripts/IsoCoreFoundation/Mobs/MobSpawner.cs`
  - `TrySpawn()` computes `nightDanger = _camping.IsNight && (breachedWard ||
    !_camping.AtCampsite)` and passes it to `SpawnMob`, which calls
    `Mob.ApplyNightDanger(...)` with the three new config multipliers.
- `Assets/Scripts/IsoCoreFoundation/Mobs/Mob.cs`
  - New `ApplyNightDanger(damageMul, speedMul, aggroMul)` sets per-instance
    multipliers (default 1 = no change).
  - `EffectiveMoveSpeed` / `EffectiveContactDamage` / `EffectiveAttackRange`
    wrap the `MobDefinition` values with these multipliers; `Update()` and
    `TryAttack()` use the effective values instead of the raw def values.
  - A normally-**passive** mob that was spawned with `nightDanger=true` will
    flip to aggressive once the player comes within
    `max(wanderRadius, attackRange) * nightAggroRangeMultiplier` — i.e. night
    danger can turn wildlife into hunters, not just buff existing hostiles.
- `Assets/Scripts/IsoCoreFoundation/Core/FoundationBootstrap.cs`
  - One-line change: `Camping.Init(Player, Placement, DayNight, Progression,
    InteractionOverlay, config);`

## New FoundationConfig tunables (defaults)

| Field | Default | Meaning |
|---|---|---|
| `nightMobDamageMultiplier` | 1.4 | Contact damage multiplier for night-danger mobs |
| `nightMobSpeedMultiplier` | 1.3 | Move-speed multiplier for night-danger mobs |
| `nightAggroRangeMultiplier` | 1.3 | Attack-range / aggro-range multiplier for night-danger mobs |
| `campfireSafeRadius` | 5.0 | Fallback safe radius (world units) if a campsite prop's `campWardRadius` is 0 |
| `campfireWardStrength` | 0.85 | 0-1; lower values add `(1-strength)` extra ward-breach chance at night |

All defaults are modest (~1.3-1.4x) per the spec, and only apply to mobs
spawned while `IsNight` is true **and** the spawn point is outside an active
campfire ward (or the ward was breached). Daytime and warded spawns are
unaffected — existing balance is preserved.

## What's stubbed / follow-up

- **Tent / sleep interaction**: no tent item or prop currently exists in
  `FoundationContent.cs` (confirmed via repo-wide search). The existing
  `FoundationCampingSystem.RestAt(PlaceableInstance camp)` already provides
  the "sleep to dawn" behaviour (heals to full, sets `DayNight.SetTime(0.26f)`,
  records `rest_at_camp` evidence) when interacting with a campfire/fireplace
  while `CanRestAt()` is true (within `campWardRadius`, clamped >= 1.2). This
  is effectively the A2 "sleep" mechanic already, just keyed off the campfire
  rather than a separate tent.
  - **Follow-up if a distinct tent is wanted**: add a `tent_item` /
    `Placeable("tent", ...)` entry in `FoundationContent.cs` with
    `isCampsite = true` (or a new `isTent` flag), a recipe
    (`craft_tent`), and route its interaction through the existing
    `FoundationCampingSystem.RestAt` (or a thin `SleepAt` wrapper that also
    requires `IsNight` and an active campfire within range, per the original
    spec wording "interact with tent while near an active campfire at
    night"). No new pipeline is needed beyond a placeable def + recipe + an
    `InteractionKind` hookup in `PlaceableInstance`/`PlayerInteraction`.
- **Camp-gear rarity vs. area-danger / gear-damage on ward failure** (mentioned
  in the A2 writeup) is **not** implemented — out of scope for this medium
  first pass; `campfireWardStrength` only affects whether a mob spawn is
  suppressed, not gear durability.
- Night-tier biasing toward higher-tier `BiomeMobSpawn` entries was **not**
  implemented as a separate weighted-table change; instead, night danger is
  expressed via the per-mob multipliers + passive-to-aggressive flip, which
  is data-driven via existing `BiomeMobSpawn`/`MobDefinition` fields
  (`threatTier`, `wanderRadius`, `attackRange`, `contactDamage`, `moveSpeed`)
  without needing new biome data.

## Playtest steps

1. Open the Foundation scene, enter play mode.
2. Speed up time: either wait for `DayNightSystem.time` to cross ~0.7-0.8
   (deep night, `IsNight` true), or in the editor select the `DayNightSystem`
   component and drag `time` to ~0.8, or call `DayNight.SetTime(0.8f)` from
   a debug hook / the integrated slice validator pattern
   (`boot.DayNight.SetTime(...)`).
3. **Away from camp**: stand in the open away from any placed campfire. New
   mob spawns near you should now hit harder, move faster, and previously
   "Passive" wildlife (e.g. deer/fox) should start chasing once you're within
   their boosted aggro range — watch for `-X HP` floating text being larger
   than during the day, and passive mobs beginning to path toward the player.
4. **Near a placed campfire**: place a `campfire_item` (in starter inventory)
   and stand within ~5.5m (its `campWardRadius`). Repeat the same night
   conditions — most low/equal-tier mob spawns near you should be suppressed
   (`RollMobSpawnWard` returns true), and any that do spawn nearby should be
   far less likely to be "night-danger" (per the `!_camping.AtCampsite`
   gate), confirmed by the system feed messages ("Campsite aura active...",
   "presses through the fire ward" on rare breaches).
5. **Rest to dawn**: right-click/interact with the campfire while within
   range (`CanRestAt`) — time jumps to dawn (`0.26`), HP/mana fully restore,
   and `IsNight`/night-danger spawning stops until dusk again.
6. Tune `nightMobDamageMultiplier` / `nightMobSpeedMultiplier` /
   `nightAggroRangeMultiplier` / `campfireWardStrength` /
   `campfireSafeRadius` on the `FoundationConfig` asset/instance to taste.
