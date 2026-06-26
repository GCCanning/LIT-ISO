# Shaders / Lighting / Atmosphere — 2026-06-13

Covers Impact Analysis B2b items #2 (shaders) and #3 (lighting/atmosphere).

## Render pipeline finding

`ProjectSettings/GraphicsSettings.asset` has `m_CustomRenderPipeline: {fileID: 0}` —
**Built-in render pipeline**, not URP. No `Light2D`, no `Universal Render Pipeline`
references anywhere in `Assets/Scripts`. Per the task's conservative guidance, no
pipeline migration was attempted; everything below uses plain SpriteRenderer
colour math + the existing global-shader-uniform ambient system.

## What was already implemented (found, not written this session)

A prior session already built a complete day/night + campfire atmosphere stack:

- **`Assets/Scripts/IsoCoreFoundation/Core/DayNightSystem.cs`** — deterministic
  clock exposing `time`, `NightFactor` (0 day → 1 deep night), `IsNight`,
  `LightColor`/`LightIntensity` (sun/moon), `NightTint` (fullscreen overlay colour
  for dawn/dusk warm wash + night blue).
- **`Assets/Scripts/IsoCoreFoundation/World/AmbientLightController.cs`** — each
  frame computes a single ambient `Color` from `DayNightSystem` (dusk floor → noon
  white → night blue/moon, plus weather dimming via `FoundationWeatherVisuals`) and
  pushes it via `SpriteAmbient.SetAmbient` → `Shader.SetGlobalColor("_AmbientColor")`.
  Wired in `FoundationBootstrap.Awake` right after `DayNight` is created.
- **`Assets/Scripts/IsoCoreFoundation/World/SpriteAmbient.cs`** — shared access to
  the `IsoCore/SpriteAmbient` material/global colour that ground tiles, props, and
  the player all multiply against. One global uniform write, batching intact.
- **`Assets/Scripts/IsoCoreFoundation/Building/CampfireGlow.cs`** — additive,
  radial-gradient sprite child, warm orange, Perlin-noise flicker on intensity and
  scale, fades in with `NightFactor`. Attached in
  `Assets/Scripts/IsoCoreFoundation/Building/PlaceableInstance.cs` (`Setup`) for
  `campfire`/`fireplace` placeables — i.e. campfires ARE per-instance GameObjects
  and already get a glow child automatically when placed.

No changes were needed for items 1–3 of this task's brief; they were verified
working as designed.

## What was added this session

**Per-cell height-based depth tint** (item #4 — the one genuinely missing piece).
`Assets/Scripts/IsoCoreFoundation/World/IsoWorldRenderer.cs`:

- `Configure(...)` now sets `sr.color = DepthTint(cell.Height)` for every ground
  tile's surface SpriteRenderer.
- `EnsureStack(...)` now sets `child.color = DepthTint(i)` for each sub-surface
  stacked level (level `i` below the surface).
- New helper `DepthTint(int height)`:
  - Reference height `DepthTintReferenceHeight = 3` — cells at/above this render
    at full brightness (`Color.white`).
  - Each level below it darkens by `DepthTintPerLevel = 0.06f`, clamped to
    `DepthTintMaxStrength = 0.30f` (so the deepest low ground is still ~70%
    brightness, never crushed to black).
  - Slight cool/blue lean via `DepthTintCool = (0.92, 0.96, 1.05)` multiplied into
    the shade, giving low ground a faint "depth fog" feel.

This is a per-renderer `SpriteRenderer.color` multiply — it composes multiplicatively
with the existing global `_AmbientColor` day/night tint (a separate shader uniform),
so the two effects stack without conflict and at zero extra per-frame cost (computed
once when a tile is configured/recycled, not every frame).

### Config tunables added
All four constants live as `const`/`static readonly` at the top of
`IsoWorldRenderer.cs` near `DepthTint`:
- `DepthTintReferenceHeight` (int, default 3)
- `DepthTintPerLevel` (float, default 0.06)
- `DepthTintMaxStrength` (float, default 0.30)
- `DepthTintCool` (Color, default (0.92, 0.96, 1.05))

Tune these to taste; all are easy single-line edits.

## Follow-ups (not done, larger scope)

- **URP / Light2D migration**: would unlock real `Global Light 2D` + per-prop
  `Light2D` point lights (true additive lighting, normal-map-aware sprite
  lighting). Currently faked via global colour multiply + additive glow sprites.
  This is a project-wide pipeline change — out of scope here, flagged as a future
  upgrade if "true" 2D lighting/shadows become a priority.
- **Custom depth/mood shader**: deferred per task guidance — the C# colour-multiply
  approach above achieves the same visual goal (depth fog by height) without any
  HLSL/ShaderGraph risk.
- **`DungeonRoomFog.cs`**: exists, fully implemented, but intentionally disabled
  (`Init` is a no-op per owner request 2026-06-13) — left as dead code for
  potential re-enable; not reused for overworld depth shading since the owner
  removed the fog concept entirely.
