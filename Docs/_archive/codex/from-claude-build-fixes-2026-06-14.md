# Build Fixes — 2026-06-14

## 1. Purple/magenta tiles — ROOT CAUSE + FIX

**Root cause:** `Assets/Shaders/SpriteAmbient.shader` (`IsoCore/SpriteAmbient`, guid
`da320276474470d4daa353b428d21d34`) is the shader used by `Resources/Materials/SpriteAmbient.mat`,
which is in turn assigned via `SpriteAmbient.Material` (`Assets/Scripts/IsoCoreFoundation/World/SpriteAmbient.cs`)
to **every** world tile and prop `SpriteRenderer` in `IsoWorldRenderer.cs`
(`newSr.sharedMaterial = SpriteAmbient.Material` and `stackSr.sharedMaterial = SpriteAmbient.Material`).

`ProjectSettings/GraphicsSettings.asset` `m_AlwaysIncludedShaders` only listed the three
built-in shaders (Standard, Sprites/Default, UI/Default — fileIDs 10753/10770/10783).
The custom `IsoCore/SpriteAmbient` shader was **not** in the list. Because nothing else
in the project statically references this shader by name (it's only assigned at
runtime via `Resources.Load<Material>`), Unity's build-time shader stripper drops it
from the player build. At runtime every renderer using `SpriteAmbient.Material` then
falls back to the built-in error shader, which renders as magenta/purple — exactly
the "random purple textures replacing tiles" symptom (random because it affects
every tile/prop using that shared material, i.e. basically the whole world).

**Fix applied:** added an entry to `m_AlwaysIncludedShaders` in
`ProjectSettings/GraphicsSettings.asset`:

```yaml
  m_AlwaysIncludedShaders:
  - {fileID: 10753, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 10770, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 10783, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 4800000, guid: da320276474470d4daa353b428d21d34, type: 3}
```

**Other shaders checked:**
- `Assets/Shaders/SpritesDiffuseBumped.shader` (guid `2a51404700d1184458ad0bc26a6aae78`) —
  referenced only by `Assets/Materials/IsoWorldLit.mat` and `Assets/Materials/IsoWorldDecorations.mat`.
  Neither material is referenced anywhere in C# code, prefabs, or scenes that I could find —
  these appear to be orphaned/legacy assets, not part of the active SpriteAmbient render
  path. Left untouched; not added to Always Included since nothing currently uses them.
- World tiles do **not** use Unity's `TilemapRenderer`/Tilemap component at all —
  `IsoWorldRenderer.cs` builds per-cell `SpriteRenderer`s directly and assigns
  `SpriteAmbient.Material` to all of them (tiles and props alike), so this single
  fix covers both.

## 2. Town finalization — `IsWalkLaneCell`

`Assets/Scripts/IsoCoreFoundation/World/IsoSettlementSampler.cs`: implemented the
previously no-op `IsWalkLaneCell` (was hardcoded `return false`).

Door cell + walk lane are both local-footprint coordinates (`lx,ly` in
`[0,fw) x [0,fh)`), so the "2-cell walk lane toward the plaza" from D3 can't
literally extend outside the footprint bounding box in this code path (that's
already covered by Plaza/Road cells in `SampleCell`). Implemented instead as:
the door cell plus up to 2 cells immediately *behind* the door on the interior
side, along the same face axis `DoorLocal()` uses
(`faceX/faceY = -sign(gx)/-sign(gy)`). These cells are returned as
`SettlementCellKind.DoorLane` (same as the door itself), which
`IsoTerrainSampler.ApplySettlement` already renders as a clear `stone_path` with
no node/decor — so the immediate entrance approach inside the footprint is
guaranteed walkable/decor-free, consistent with `suppressDenseDecorNearDoorCells: 3`.

Verified brace balance around the edited region (`TryLot`, `DoorLocal`,
`IsWalkLaneCell`) — all blocks close correctly.

Skimmed `ApplySettlement`'s D1-D7 switch in `IsoTerrainSampler.cs` (lines ~543-650):
PlazaCenter/Plaza/Road/Building/DoorLane/Farm/Pasture/Dock cases are all
implemented and consistent with the WORLDGEN_RULES_PROPOSAL Part D spec — no other
obvious half-done stubs found there worth touching in this pass.

## 3. Seed / town distance guidance

- **Default world seed = `1337`** (`FoundationConfig.cs: public int seed = 1337`,
  also the fallback throughout `FoundationBootstrap.cs`). This is **not** the
  biome-showcase seed `240611` (`IsoTerrainSampler.BiomeShowcaseSeed`), so
  `ApplySettlement`/town generation runs normally for the default seed.
- `macroCellSizeCells = 96`, `chunkSize = 12`, spawn ring radius =
  `spawnRingRadiusChunks(2) * chunkSize(12) + spawnRingMarginCells(12) = 36` cells.
- Hamlet band = chunkRing [3,10] -> centerClearing in [36,120] cells from origin.
  At `cellSize (1, 0.5, 1)` that's roughly 36-120 world units along X/Y (Z-as-Y
  doesn't change planar distance).
- I replicated the D1 hash (`seedHash = seed*2654435761 + 0x9e3779b9`,
  `settlementSeedHash = seedHash ^ 0xA17B5C2D`, then `Hash01`/eligibility roll)
  for seed 1337 and brute-forced macro cells in rings -60..60. **No hamlet-band
  (chunkRing 3-10) macro cell passes the eligibility roll near spawn** for seed
  1337 — the closest passing eligibility rolls are:
  - **Macro cell (1, 0)** -> plaza center ≈ world (136, 42), chunkRing 11
    (**village** band, 0.018 chance, rolled 0.014 — passes). This is the
    nearest likely town: roughly **136 cells / world-units due east (+X)** of
    spawn, just past the hamlet band into village-band distance.
  - Macro cell (-4, 1) -> plaza center ≈ (-330, 152), chunkRing 27 (village),
    further away (west).

  Note: eligibility-roll passing is necessary but not sufficient — the full D1
  score (flatness/water/biome vs. `scoreThreshold = 0.35`) and the min-spacing
  tie-break still need to pass against real terrain, which this standalone
  hash replication doesn't probe. **Recommendation for the owner:** from spawn,
  walk roughly **east (+X), ~130-140 cells**, to most likely encounter the
  nearest generated settlement for the default seed 1337. If that macro cell's
  terrain fails the flatness/water score, the next-nearest candidate is well to
  the west (~330,150) and considerably farther.
