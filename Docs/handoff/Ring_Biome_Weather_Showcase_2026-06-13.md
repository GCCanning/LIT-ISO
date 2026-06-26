# Ring-of-Biomes Weather Showcase — 2026-06-13

Branch: `feat/biome-asset-wiring` (uncommitted, layered on existing drift — see
`Biome_Asset_Wiring_2026-06-13.md`).

## Task

Project task #25: "Build ring-of-biomes test world with per-biome weather
effects." Built on top of `IsoTerrainSampler.BiomeShowcaseSeed` (240611), the
fixed-seed quadrant world from the prior biome-asset-wiring session
(west = ocean/beach, north = snow, east = forest, south = mountain,
center = meadow).

## What exists already

`Assets/Scripts/IsoCoreFoundation/World/FoundationWeatherVisuals.cs` is a
visual-only weather pass (already wired into `FoundationBootstrap`) with a
camera-following particle system and four moods: `Clear`, `Mist`, `Drizzle`,
`Snow`. Normally it rerolls mood every ~38s from biome temperature/moisture +
Perlin noise.

`Assets/Scripts/World/Weather/WeatherManager.cs` /
`WeatherDefinition.cs` (legacy `Assembly-CSharp` / `EthraClone.TrialWeek`
namespace, with `OutdoorDamageRoutine`, particle prefabs, gameplay modifiers)
is a **separate, more elaborate system that is not wired into
`IsoCore.Foundation`** — `IsoFoundationPlayer.cs` only references it in a
comment. Out of scope to wire across assemblies in this pass.

`Assets/PixelWeatherAsset/` (Pixel Weather Particles pack, task #12) contains
`Prefabs/Weather/Rain.prefab` and `Snow.prefab` with
`RainController`/`SnowController` — these are **screen-space UI-canvas demo
prefabs** (see `Scripts/UIController.cs`), not world-space particle systems
compatible with `FoundationWeatherVisuals`' isometric world-space particle
follow-cam. Not wired in this pass (see Follow-ups).

## What was wired

### 1. New `Wind` mood + biome -> mood mapping (`FoundationWeatherVisuals.cs`)

- Added `FoundationWeatherMood.Wind` (fast horizontal dust/wind streaks, tan
  tint, low alpha) for the mountain quadrant — previously mountain's cold/dry
  climate (`temp 0.25, moisture 0.22`) fell into the `Snow` bucket alongside
  the actual snow biome, which didn't read as "different weather per biome."
- For the showcase world specifically (`_seed ==
  IsoTerrainSampler.BiomeShowcaseSeed`), `ChooseMood()` now skips the slow
  noise-based reroll and instead maps the player's **current biome id**
  directly via `MoodForBiomeId()`, rechecked every 1.5s so walking between
  quadrants swaps weather promptly:

  | Biome quadrant | Mood    | Effect |
  |-----------------|---------|--------|
  | `snow` (north)  | Snow    | falling snow particles, cool tint |
  | `mountain` (south) | Wind | dust/wind streaks blowing east, tan tint |
  | `forest` (east) | Mist    | light fog/haze drifting, cool-grey tint |
  | `beach` (west)  | Clear   | no particles |
  | `meadow` (center) | Clear | no particles |

- Non-showcase worlds keep the old noise-based reroll, now with an added
  `Wind` branch for cold+dry (`temp < 0.34 && moisture < 0.30`) cells (e.g. a
  naturally-occurring mountain biome) so they no longer get lumped into Snow.

### 2. Per-biome ambient audio swap (`WorldAudioController.cs`)

- Added `SetBiomeSource(IsoWorld, IsoFoundationPlayer, int seed)`, called from
  `FoundationBootstrap` (`worldAudio.SetBiomeSource(World, Player,
  config.seed)`).
- New `_ambBiome` `AudioSource` crossfades (2.5s) to
  `Resources/Audio/Ambient/Biome/<biomeId>` whenever the player's current
  biome id changes (`snow`, `mountain`, `forest`, `beach`, `meadow`, etc.).
- **No clips currently exist** under `Resources/Audio/Ambient/Biome/` —
  `Resources.Load` returns null and `CrossfadeBiomeAmbient` exits quietly
  (fades out whatever was playing, stays silent). This is intentional: the
  hookup is live and will "just work" the moment biome ambience loops
  (wind howl, forest birds, wave/ocean, mountain wind) are dropped into that
  folder with matching ids — no further code changes needed.

## How to load and review

Same `FoundationConfig` settings as the original showcase, in
`Assets/Scripts/IsoCoreFoundation/Core/FoundationConfig.cs` (or via the
inspector on the Foundation bootstrap object):

```
seed = 240611
continentWorld = true
flatWorld = false
```

Walk from the center (meadow, clear) outward into each quadrant:
- North -> snow biome -> snowfall particles appear within ~1.5s.
- South -> mountain biome -> dust/wind streaks blow east.
- East -> forest biome -> light fog/haze drifts in.
- West -> beach/ocean -> particles clear.

`FoundationWeatherVisuals.Label` (property on the `Active` singleton) reports
the current mood name (`Clear`/`Mist`/`Drizzle`/`Snow`/`Wind`) for debug HUD
or console inspection if needed.

## Follow-ups / not done

- **Visual weather particles for the audio-bed biomes are still the generic
  camera-follow particle system** — fine for the task's "ambient effect"
  goal, but if richer per-biome visuals are wanted (proper snowfall sprite
  sheets, rain streaks, etc.), wire the **Pixel Weather Particles pack**
  (`Assets/PixelWeatherAsset/Prefabs/Weather/Rain.prefab`,
  `Snow.prefab`) into `FoundationWeatherVisuals` — requires converting those
  prefabs from screen-space UI canvas particles (`RainController`,
  `SnowController`, `Assets/PixelWeatherAsset/Scripts/`) to world-space
  particle systems compatible with the isometric camera-follow setup, or
  spawning them as a child Canvas overlay instead.
- **Per-biome ambient audio clips** (`Resources/Audio/Ambient/Biome/snow.wav`,
  `mountain.wav`, `forest.wav`, `beach.wav`, `meadow.wav`, etc.) do not exist
  yet — the crossfade hookup is wired and will activate automatically once
  added.
- The legacy `WeatherManager`/`WeatherDefinition` system
  (`Assets/Scripts/World/Weather/`) with `OutdoorDamageRoutine` and gameplay
  modifiers (speed, accuracy, spell damage bonuses) remains unwired to
  `IsoCore.Foundation`. If "weather effects" should eventually include
  gameplay modifiers (not just visuals/audio), that system would need a
  Foundation-side equivalent or an `IsoCore.Foundation`-referenceable port.

## Verified

- Both edited files (`FoundationWeatherVisuals.cs`,
  `WorldAudioController.cs`) and the one-line `FoundationBootstrap.cs` change
  re-read end-to-end via the Read tool; braces/parens/switch arms balanced,
  `IsoWorld.GetBiome(int,int)`, `IsoFoundationPlayer.CurrentCell`
  (`Vector2Int`), and `BiomeDefinition.id` (from `FoundationDefinition`) all
  match existing signatures. No `dotnet`/Unity compile available in-session.
