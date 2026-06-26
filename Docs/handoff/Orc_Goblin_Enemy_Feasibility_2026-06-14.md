# Orc/Goblin Enemy Feasibility — CraftPix "Free Top-Down Orc Game Character" Pack

**Date:** 2026-06-14
**Status:** Research only — no asset import or code changes made.
**Source pack (staging, outside repo):** `outputs/orcpack/` (CraftPix, 3 orc designs: Orc1/Orc2/Orc3,
PNG + PSD + Aseprite sources, `With_shadow` / `Without_shadow` variants, `Tiled_files/`)

---

## 1. Visual / style fit

**Slime reference** (`Assets/Resources/Enemies/Slime/slime-Sheet.png`, individual frames in
`Assets/Resources/Enemies/Slime/Individual Sprites/`): a small (32x25px @ 32 ppu) blob — simple
rounded shape, flat saturated blue fill, thin dark outline, 1-2 highlight pixels, minimal detail.
Very "cute/simple mascot" pixel art — low detail budget, single facing + flipX.

**Orc pack** (inspected `orc1_idle_with_shadow.png` 256x256, `orc1_walk_with_shadow.png`
384x256, `orc1_attack_with_shadow.png` 512x256, plus `orc2_idle_with_shadow.png` and
`orc3_idle_with_shadow.png`): chibi-proportioned goblin/orc humanoids — oversized head, short
limbs, green skin, simple weapon (spear/staff for Orc1, dual blades/dagger for Orc2/Orc3 which
read more "armored/elite"). Thick dark outlines, flat color blocking with 1-2 shade ramps, soft
drop shadow baked into the "With_shadow" sheets. Top-down 3/4 perspective (slightly more
"top-down RPG" angle than the slime's near-side-on view, but compatible — both are flat-shaded
chibi pixel art with heavy outlines and minimal internal detail).

**Verdict on style:** confirmed compatible. Both are "cute chibi, thick outline, flat shading"
pixel art. The orc's slightly more saturated/cartoony green and the spear/weapon silhouettes are
a believable "next tier up" from a blob slime — reads as a goblin-tier humanoid threat without
clashing with the slime's tone. Orc2/Orc3 (darker teal/armored variants) read as visually
"tougher" than Orc1, supporting a tiering plan (see §4).

**Scale / frame size:**
- `orc1_idle_with_shadow.png` = 256x256 → a **4x4 grid of 64x64 frames** (4 directions x 4
  idle frames each).
- `orc1_walk_with_shadow.png` = 384x256 → a **4x6 grid of 64x64 frames** (4 directions x 6
  walk frames).
- `orc1_attack_with_shadow.png` = 512x256 → a **4x8 grid of 64x64 frames** (4 directions x 8
  attack frames).
- `orc1_hurt_with_shadow.png` = 384x256 → likely 4x6 @ 64x64 (or 4x4 @ 96x64 — needs
  confirmation at slice time, but 64x64 is the consistent unit across the other sheets).
- `orc1_death_with_shadow.png` = 512x256 → 4x8 @ 64x64, consistent with attack.

So **each orc frame is a 64x64 canvas**, vs. the slime's 32x25 canvas — roughly **2x the linear
size** of the slime sprite sheet's per-frame canvas, but the *character itself* fills most of
that 64x64 (head-to-toe), where the slime's blob fills most of its 32x25.

**Proposed scale to read as "person-sized" next to the slime/player on the `cellSize (1,0.5,1)`
grid:**
- Keep `pixelsPerUnit = 32` (matches slime, and matches the project's general pixel-art unit
  convention) → a 64x64 orc frame becomes **2.0 x 2.0 world units** tall/wide at
  `visualScale = 1`. That is too large — a 2-unit-tall humanoid towers over a ~1-unit grid cell
  and over the slime (32px / 32ppu = 1.0 unit at visualScale 1, then slime uses
  `visualScale: 0.85-1.08` per its definitions, so slime renders ~0.85-1.1 units tall).
- Recommend either:
  - **(a) Keep `pixelsPerUnit = 32` but set `visualScale ≈ 0.45-0.55`** on the
    `EnemyDefinition` (consistent with how the slime already uses `visualScale` to fine-tune
    apparent size) — this scales the 2.0-unit sprite down to ~0.9-1.1 units, i.e. roughly
    "one grid cell tall," comparable to the slime and a believable goblin next to a ~1.6-1.8
    unit player. **This is the simplest option** — no new pixelsPerUnit convention, reuses the
    existing per-definition scale knob.
  - **(b) Set `pixelsPerUnit = 64`** on the orc's `EnemyDefinition` (the field is per-asset,
    so this doesn't affect the slime) — a 64px frame then maps to 1.0 world unit natively,
    and `visualScale` can stay near 1.0 for fine-tuning. Cleaner long-term if more "64px-canvas"
    packs get added later, since it avoids relying on a sub-1 visualScale fudge factor.
  - **Recommendation: (b)** — `pixelsPerUnit = 64`, `visualScale ≈ 1.0-1.15` to make the orc
    read slightly larger/taller than the slime (it's a humanoid goblin, should feel like a
    bigger threat), while staying well within one grid cell footprint. Adjust empirically once
    in-scene next to the player and slime.

---

## 2. Animation / controller gap

`SlimeEnemyController` (`Assets/Scripts/Gameplay/SlimeEnemyController.cs`) is a single-facing
controller: it picks one sprite per animation state and uses `spriteRenderer.flipX` (via
`FaceTarget`) to mirror left/right. It has no concept of "front/back" sprites — a creature
walking "up" (away from camera) looks identical to one walking "down."

The orc pack's With_shadow sheets are **already organized as 4 directional rows per sheet**
(down/front, up/back, left, right — standard CraftPix top-down convention), which is more
direction data than the current controller can consume.

**Two options:**

- **(a) Flip-only, front-facing (slime-style).** Slice out only the "front" (down-facing) row
  from each sheet (idle/walk/attack/hurt/death), ignore the back/left/right rows entirely, and
  drive it exactly like the slime: `flipX` for left/right movement, always show the front sprite
  facing the camera. **Pros:** zero controller changes — reuse `SlimeEnemyController` (or a
  thin copy/rename to `MeleeEnemyController` with melee-appropriate tuning) verbatim, same
  `BuildSprites`/`Animate`/`AnimateOnce` pipeline, same `EnemyDefinition` Texture2D[] arrays.
  Fastest to ship; matches "minimal scope" goal. **Cons:** wastes ~75% of the pack's directional
  art; orc will visibly "moonwalk" when moving away from the camera (front sprite used for all
  directions) — a known acceptable simplification for many top-down games and already how the
  slime behaves (slime has no back sprite either).

- **(b) Small directional state machine.** Slice front + back + left rows (right = left
  flipped via `flipX`, halving slicing work), add a `facing` enum (Down/Up/Left/Right) derived
  from movement/aim vector, and extend `EnemyDefinition` with per-direction Texture2D[] arrays
  (e.g. `idleFramesDown/Up/Side`, `moveFramesDown/Up/Side`, etc.) or a small nested struct.
  Controller picks the active array set based on `facing` each frame. **Pros:** much better
  readability of movement, looks noticeably more polished, makes full use of the
  pack. **Cons:** triples the slicing/import work (3x sheets x 5 animations x N frames per
  orc), requires new fields on `EnemyDefinition` (a ScriptableObject schema change touching the
  shared slime asset type — needs care not to break existing slime `.asset` files, though
  additive fields with defaults are safe), and a materially larger controller rewrite/extension
  with direction-resolution logic and more edge cases (e.g., attack animation direction lock).

**Recommendation: (a), flip-only front-facing**, as a `MeleeEnemyController` (copy of
`SlimeEnemyController` with the same idle/move/attack/hurt/die state machine, renamed/lightly
adapted — e.g. melee enemies may want a slightly different attack hitbox/timing default, but
the animation/facing pipeline is identical). This matches the project's existing simplification
(slime also has no directional sprites), reuses `EnemyDefinition` unchanged, and is a same-day
slicing job per orc. Option (b) is worth revisiting later as a *separate, opt-in* enhancement to
`EnemyDefinition` (additive fields default to empty/unused so slimes are unaffected) once
there's a stronger case for facing-accurate enemies (e.g., player-facing bosses).

---

## 3. Asset promotion plan (Orc1, With_shadow)

Target: `Assets/Resources/Enemies/Goblin/` (mirroring `Assets/Resources/Enemies/Slime/`
structure), with an `Individual Sprites/` subfolder for sliced frames.

**Source sheets to promote** (from `outputs/orcpack/PNG/Orc1/With_shadow/`):
| Sheet | Size | Grid (64x64 cells) | Use |
|---|---|---|---|
| `orc1_idle_with_shadow.png` | 256x256 | 4x4 | idleFrames — slice **row 0 (front)**, 4 frames |
| `orc1_walk_with_shadow.png` | 384x256 | 4x6 | moveFrames — slice **row 0 (front)**, 6 frames |
| `orc1_attack_with_shadow.png` | 512x256 | 4x8 | attackFrames — slice **row 0 (front)**, 8 frames |
| `orc1_hurt_with_shadow.png` | 384x256 | likely 4x6 @ 64x64 (verify) | hurtFrames — front row |
| `orc1_death_with_shadow.png` | 512x256 | 4x8 | dieFrames — front row, 8 frames |

(Skip `orc1_run_with_shadow.png`, `orc1_walk_attack_front_with_shadow.png`,
`orc1_run_attack_front_with_shadow.png` for the minimal v1 — they map to states
`SlimeEnemyController`/`EnemyDefinition` doesn't currently have a slot for; revisit if a "run"
or "walk+attack combo" state is added later.)

**Slicing step (required before Unity import):** Like the slime's
`Assets/Resources/Enemies/Slime/Individual Sprites/slime-<anim>-<N>.png`, each orc sheet is a
**multi-frame strip** and must be sliced into individual per-frame PNGs (64x64 each, front row
only) before populating `EnemyDefinition`'s `Texture2D[]` arrays — those arrays are
one-texture-per-frame, not a sliced spritesheet reference. Naming convention to mirror the
slime: `Assets/Resources/Enemies/Goblin/Individual Sprites/goblin-idle-0.png` ...
`goblin-idle-3.png`, `goblin-move-0..5.png`, `goblin-attack-0..7.png`, `goblin-hurt-0..N.png`,
`goblin-die-0..7.png`.

**Slicing approach:** crop the front-facing row (row index 0, assuming row order is
down/up/left/right per CraftPix convention — confirm row order visually before cropping, since
some CraftPix packs order rows as front/left/right/back or similar) at y=0..63, x = i*64 for
each frame i. This can be done with any image tool (Aseprite, since `.aseprite` sources are
included in the pack and already have the frames as named layers/tags — likely the cleanest
path is exporting frame-by-frame from the per-direction `.aseprite` files in
`ASEPRITE/Orc1/Orc1_idle/orc1_front_idle.aseprite` etc., which are *already* single-direction
front-only sources and avoid the row-identification guesswork entirely).

**EnemyDefinition mapping** (new asset, e.g. `Assets/World/Enemies/Enemy_Goblin_Common.asset`):
- `idleFrames` ← `goblin-idle-0..3.png` (4 entries)
- `moveFrames` ← `goblin-move-0..5.png` (6 entries)
- `attackFrames` ← `goblin-attack-0..7.png` (8 entries)
- `hurtFrames` ← `goblin-hurt-0..N.png`
- `dieFrames` ← `goblin-die-0..7.png`
- `pixelsPerUnit` = 64 (per §1 recommendation)
- `visualScale` ≈ 1.0-1.15
- Frame durations: orc has more frames per animation than the slime (e.g. 8-frame attack vs.
  slime's 5), so `attackFrameDuration`/`dieFrameDuration` etc. may need to be shorter
  (~0.05-0.06s) to keep total animation length comparable to the slime's.

---

## 4. Suggested tiering

Map the 3 orc designs to a "goblin" enemy family, one tier above the F-rank slimes
(slime common: 20 HP / 4 dmg / 10 XP; slime rare: 48 HP / 8 dmg / 10 XP — per
`Assets/World/Enemies/Enemy_Slime_Common.asset` and `Enemy_Slime_Rare.asset`).

| Design | Suggested role | Rank | maxHealth | contactDamage | xpReward | Notes |
|---|---|---|---|---|---|---|
| Orc1 (green, spear) | Goblin Common | E | 40-50 | 6-8 | 20-30 | Baseline goblin grunt, step up from slime rare |
| Orc2 (teal/armored, dual blades) | Goblin Rare | E (high end) | 65-80 | 10-14 | 35-50 | Faster/more aggressive, tint or recolor variant optional |
| Orc3 (dark armored, larger blade) | Goblin Elite | D | 100-130 | 15-20 | 60-90 | Mini-boss/elite spawn, lower spawn rate, possibly higher `attackRange`/slower `attackCooldown` for a "heavy hitter" feel |

`moveSpeed`/`detectionRadius`/`leashRadius`/`attackRange`/`attackCooldown` can start from the
slime rare's values (1.95 / 5.2 / 7.5 / 0.48 / 0.9) and be tuned upward slightly for Orc2/Orc3
(goblins should feel more dangerous and persistent than slimes — e.g. larger
`detectionRadius` ~5.5-6.5, slightly faster `moveSpeed` ~1.6-2.2).

---

## 5. Verdict

**Conditional yes.** The art style is a clean fit (cute chibi, thick outlines, flat shading —
no jarring mismatch with the slime), and Orc1/2/3 naturally support a 3-tier goblin family
slotting in just above the slimes. The main cost is **slicing**: the pack ships multi-frame
strips (and per-direction Aseprite sources), and `EnemyDefinition` needs individual per-frame
PNGs like the slime's `Individual Sprites/` folder — this is mechanical but non-trivial busywork
(roughly 25-30 frames per orc for the minimal idle/walk/attack/hurt/death front-facing set).
Recommend option (a) (flip-only, front-facing, reuse the slime's controller pattern as a new
`MeleeEnemyController`) to keep scope tight — directional sprites are a nice-to-have, not a
blocker.

**Next concrete step:** slice **Orc1's front-facing frames** (idle/walk/attack/hurt/death) from
either the With_shadow PNG sheets (row 0, 64x64 cells) or, preferably, the per-direction
`orc1_front_*.aseprite` sources in `ASEPRITE/Orc1/`, export as individual PNGs into
`Assets/Resources/Enemies/Goblin/Individual Sprites/`, then create one `EnemyDefinition` asset
(`Enemy_Goblin_Common.asset`) and a `MeleeEnemyController` prefab to validate scale
(`pixelsPerUnit = 64`, `visualScale ≈ 1.0`) in-scene next to the player and an existing slime
before committing to slicing Orc2/Orc3.
