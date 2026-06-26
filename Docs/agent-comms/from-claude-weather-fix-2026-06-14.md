# Weather effects fix — 2026-06-14

## Root cause

The per-biome weather mapping built in tasks #25/#26
(`FoundationWeatherVisuals.MoodForBiomeId` — snow biome -> Snow particles,
mountain -> Wind/dust, forest -> Mist) was gated behind
`_seed == IsoTerrainSampler.BiomeShowcaseSeed` (240611). That seed is **only
ever referenced by the weather/audio files themselves** — nothing in the
launch/menu flow (`FoundationConfig.seed = 1337` default, `ConfigureLaunch`,
welcome menu, etc.) ever sets seed to 240611. So the "ring-of-biomes test
world" with reliable per-biome weather was never reachable from normal play;
it only existed as dead code behind an unused seed check.

For the normal game (any other seed), `ChooseMood()` fell through to the old
noise-based reroll: ~38-60s between rerolls, with fairly narrow
temperature/moisture/Perlin-noise conditions, and the spawn clearing is
always `meadow` (temp 0.55, moisture 0.55) which never satisfies any of the
Snow/Wind/Drizzle/Mist conditions — only "Clear". A player near spawn (or one
who hasn't waited a full reroll cycle in a snow/mountain/forest biome) would
see no particles at all, which matches "weather effects still aren't in".

The weather controller itself (`FoundationWeatherVisuals`), its particle
system setup, and its wiring into `FoundationBootstrap.Awake()` (creates
`FoundationWeatherVisuals` on a dedicated GameObject and calls `Init(...)`)
were all correct and present in the normal gameplay scene — this was not a
missing-component or broken-reference issue, and `IsoWorld.GetBiome` /
`IsoFoundationPlayer.CurrentCell` / `BiomeDefinition.id` all still match their
Phase 1 `IsoTerrainSampler` signatures.

## Fix

`Assets/Scripts/IsoCoreFoundation/World/FoundationWeatherVisuals.cs`:

- Replaced the `_seed == BiomeShowcaseSeed` gate with a generalized
  `TryMoodForBiomeId(biomeId, out mood)` check that runs for **every** world/
  seed.
- `snow` -> Snow, `mountain` -> Wind, `forest` -> Mist trigger immediately
  (1.5s recheck) regardless of seed, exactly like the showcase used to.
- Biomes not in that map (meadow, beach, desert, etc.) fall through unchanged
  to the existing climate/noise-based reroll, preserving variety for those
  regions.

Net effect: walking from the meadow spawn into a snow, mountain, or forest
biome in the normal gameplay scene now shows snowfall / dust-wind streaks /
mist within ~1.5s, with no seed dependency. The showcase seed 240611 still
works the same way (it now just uses the same general code path).

## Verified

- Re-read `FoundationWeatherVisuals.cs` end-to-end after edit: braces/parens
  balanced, `TryMoodForBiomeId` signature/usages consistent, no leftover
  references to the removed `IsoTerrainSampler.BiomeShowcaseSeed` gate in
  this file.
- No other files reference `FoundationWeatherVisuals.MoodForBiomeId`, so the
  rename to `TryMoodForBiomeId` has no other call sites to update.
- `dotnet`/Unity compile not available in this session; static review only.
