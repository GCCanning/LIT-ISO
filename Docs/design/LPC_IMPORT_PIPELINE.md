# LPC Import Pipeline — Runtime Palette + Sheet-Slice Model

Status: FOUNDATION. Spec + importer + 12-item validated sample. Scaling to all 765 and
the C# rewrite are later steps. The C# change-list below is **untested-by-design** (this is
a Unity project and cannot be compiled in the import environment).

---

## 1. Why we are changing the model

Today's catalog (v3, 47 items) stores art as `.png.bytes` split **per (item × layer × animation
× bodytype × variant)** — ~288 files/item. Importing all 765 items that way is ~300k–600k files,
impractical for Unity's asset DB and Resources folder.

New model (decision; not relitigated here):

- Store **ONE universal sheet per (item × bodytype)** at a single base palette.
- **Slice** the per-animation band at runtime using a recorded row layout.
- **Recolor** variants at runtime from LPC palette definitions (base ramp → target ramp LUT).

Result: ~288 files/item collapses to **~2 files/item** (male + female). The 12-item sample
produced **23 sheets (1.92/item)** — one item (tunic) is female-only in v1 so it ships 1 sheet.

---

## 2. Measured LPC universal row layout (the load-bearing fact)

### Source reality (v1 repo)

The v1 repo does **not** ship one giant universal sheet per item. It ships **one PNG per
animation** under each body path, e.g. `spritesheets/body/bodies/male/walk.png`. Frame size is
**64 px**. Measured dimensions (body/male, all confirmed on disk):

| anim (catalog id) | v1 PNG file       | cols | rows | px (W×H)   |
|-------------------|-------------------|------|------|------------|
| walk              | walk.png          | 9    | 4    | 576 × 256  |
| spellcast (cast)  | spellcast.png     | 7    | 4    | 448 × 256  |
| thrust            | thrust.png        | 8    | 4    | 512 × 256  |
| slash             | slash.png         | 6    | 4    | 384 × 256  |
| shoot             | shoot.png         | 13   | 4    | 832 × 256  |
| hurt              | hurt.png          | 6    | **1**| 384 × 64   |
| idle              | idle.png          | 2    | 4    | 128 × 256  |
| run               | run.png           | 8    | 4    | 512 × 256  |
| jump              | jump.png          | 5    | 4    | 320 × 256  |
| sit               | sit.png           | 3    | 4    | 192 × 256  |
| emote             | emote.png         | 3    | 4    | 192 × 256  |
| climb             | climb.png         | 6    | **1**| 384 × 64   |
| combat            | combat_idle.png   | 2    | 4    | 128 × 256  |
| backslash         | backslash.png     | 13   | 4    | 832 × 256  |
| halfslash         | halfslash.png     | 6    | 4    | 384 × 256  |

### Reconciliation of the rows=8 discrepancy

- The current game catalog claims `rows: 8` (8 directions) and its existing `.png.bytes` for walk
  measure **576 × 512 = 9 cols × 8 rows**. Those 8-row sheets were **derived** (someone synthesized
  the 4 diagonal directions) — they are NOT what the v1 source ships.
- The raw v1 source is genuinely **4-directional**: **rows = N, W, S, E (top → bottom)** for normal
  animations; **hurt** and **climb** are a single row (S only).
- The new model stores the **raw 4-row v1 art**. `rows` is therefore **per-animation** (4 for most,
  1 for hurt/climb), not a single global `8`. The runtime must read row-count per animation, not
  assume a constant.

### Stitched universal sheet (what the importer produces)

The importer **concatenates the per-animation v1 PNGs vertically** into ONE universal sheet per
(item × bodytype), padding width to the widest animation (left-aligned), and records each
animation's `rowOffset` (in rows) + `rows` + `cols`. Example — `lpc/body/male.png.bytes` =
**832 × 3456** (13 cols max × 54 rows total across 15 animations). Per-item layout for body:

```
walk      off 0  rows 4 cols 9      idle  off 21 rows 4 cols 2
spellcast off 4  rows 4 cols 7      run   off 25 rows 4 cols 8
thrust    off 8  rows 4 cols 8      jump  off 29 rows 4 cols 5
slash     off 12 rows 4 cols 6      sit   off 33 rows 4 cols 3
shoot     off 16 rows 4 cols 13     emote off 37 rows 4 cols 3
hurt      off 20 rows 1 cols 6      climb off 41 rows 1 cols 6
                                    combat off 42 rows 4 cols 2
                                    backslash off 46 rows 4 cols 13
                                    halfslash off 50 rows 4 cols 6
```

Items only stitch the animations they actually ship. The sword sample stitches only
`walk, hurt, idle, combat` (13 rows total) because that is all the v1 def declares.

### Runtime slice math

For animation A on a sheet of frame size `F=64`, with texture origin at bottom-left (Unity):
- `xPx = frame * F`  (frame in `0 .. A.cols-1`)
- `direction` index d in `0 .. A.rows-1` maps to rowOrder `[N, W, S, E]` (clamp d to A.rows-1 for
  hurt/climb which only have S).
- Band top in sheet-rows = `A.rowOffset`; the row within the band = `d`.
- `yPx_fromTop = (A.rowOffset + d) * F`
- Unity rect (y from bottom): `yPx = sheetHeightPx - yPx_fromTop - F`
- `Rect(xPx, yPx, F, F)`

> NOTE: only 4 directions exist in source. If the game still wants 8 logical facings, mirror
> W↔E and reuse the nearest cardinal for diagonals **at runtime** (do not bake 8 rows).

---

## 3. New catalog schema (v4)

```jsonc
{
  "version": 4,
  "frameSize": 64,
  "defaultAnimation": "walk",
  "rowOrder": ["N", "W", "S", "E"],     // NEW: documents direction order, 4-dir source
  "pixelsPerUnit": 64,
  "pivotX": 0.5, "pivotY": 0.5,
  "bodyTypes": ["male", "female"],
  "items": [
    {
      "id": "lpc/weapon_sword_arming",
      "slot": "weapon",                  // body|head|eyes|hair|shirt|pants|shoes|weapon|offhand|hands|accessory
      "kind": "equipment",               // NEW: "cosmetic" | "equipment"
      "equipSlot": "weapon",             // NEW (equipment only): weapon|offhand|head|chest|legs|feet|hands|shoulders|back|waist|accessory
      "displayName": "Arming Sword",
      "matchBodyColor": false,
      "paletteMaterial": "metal",        // NEW: body|cloth|metal|hair|eye|null — which palette family recolors this item
      "baseVariant": "brass",            // NEW: the variant the stored sheet is authored in (source ramp)
      "variants": ["brass","bronze",...],
      "animations": [                    // NEW: per-item row layout (only anims this item ships)
        {"id":"walk","rowOffset":0,"rows":4,"cols":9,"fps":10},
        {"id":"hurt","rowOffset":4,"rows":1,"cols":6,"fps":8},
        ...
      ],
      "draws": [                         // CHANGED: one entry per bodytype, references a base sheet
        {"bodyType":"male","sheet":"Characters/Layers/lpc/weapon_sword_arming/male","sortOrder":9},
        {"bodyType":"female","sheet":"Characters/Layers/lpc/weapon_sword_arming/female","sortOrder":9}
      ]
    }
  ]
}
```

### How DrawDef changes

| v3 (today)                                              | v4 (new)                                             |
|---------------------------------------------------------|------------------------------------------------------|
| `template` with `{anim}/{body}/{variant}` placeholders  | `sheet` = ONE base sheet path per `bodyType`         |
| One pre-colored sheet **per animation per variant**     | One base sheet per bodytype; slice anim + recolor variant at runtime |
| `animations[]` global (cols/idleFrame/walkStartFrame)   | `animations[]` is **per item** (rowOffset/rows/cols/fps); only anims the item ships |
| `rows: 8` global constant                               | `rowOrder` documents 4 dirs; rows are per-animation  |

`sortOrder` is still the LPC `zPos` (z-order). Multi-layer items (e.g. weapon fg+bg, capes) are
flattened into the single base sheet by the importer (composited by zPos), so a v4 item normally has
exactly `bodyTypes.length` draws. (If we later need split front/behind-body layers we can re-introduce
multiple draws with distinct sheets + sortOrders — the schema already supports it.)

---

## 4. Palette recolor algorithm

### Palette file format (`palette_definitions/<material>/<material>_ulpc.json`)

A JSON object: `variantName -> [hex ramp]`. All variants in a file share the same ramp length
(a shadow→highlight progression). Measured ramp lengths: body=6, cloth=6, metal=6, hair=6, eye=3.
There is **no literal `"source"` key** in the ulpc palettes (unlike the guide's generic example);
the **source/base ramp is the ramp of `baseVariant`** (the variant the stored PNG is authored in).
The importer records `baseVariant` per item (first declared variant, or first palette key when the
def has no explicit variants).

### Recolor = per-pixel LUT (from PALETTE_RECOLOR_GUIDE.md)

1. Build the **base ramp** = `palette[baseVariant]` (N hex colors).
2. Build the **target ramp** = `palette[targetVariant]` (same N colors).
3. For each pixel of the base sheet: find the base-ramp index `i` whose color matches within
   **tolerance ±1 per channel** (R,G,B). If matched, replace with `targetRamp[i]` (preserve alpha).
   Tolerance absorbs anti-alias/compression drift. Unmatched pixels pass through unchanged.
4. If `targetVariant == baseVariant`, skip (no-op).

This is direction/animation-agnostic — it operates on the whole stitched base sheet once per
(item, variant) and the result is cached. `matchBodyColor` items (body, most heads) use the
**body** palette and follow the chosen skin tone instead of an independent variant.

`paletteMaterial: null` (e.g. cyclops eyes' literal "cyclops" variant set partly) means: ship every
variant's pixels as-is from whatever the base sheet holds, OR (preferred at scale) resolve the
material via the same overlap heuristic the importer uses. For the sample, inference resolved
eyes→`eye`, sword→`metal`, tunic/shoes→`cloth`, shield→`cloth` automatically.

---

## 5. Category → slot / kind / equipSlot mapping (all 10 categories)

`kind=cosmetic` items appear in the character creator (basics). `kind=equipment` items are RPG gear
(loot, equippable by player + NPCs) and carry an `equipSlot`.

| Source category | Sub-path rule                         | slot      | kind      | equipSlot   |
|-----------------|---------------------------------------|-----------|-----------|-------------|
| body            | —                                     | body      | cosmetic  | —           |
| head            | `/eyes/` or type_name=eyes            | eyes      | cosmetic  | —           |
| head            | heads / faces / eyebrows / nose / ears| head      | cosmetic  | —           |
| hair            | incl. beards / mustaches (facial hair)| hair      | cosmetic  | —           |
| torso           | shirts / dresses / vest / aprons      | shirt     | cosmetic  | —           |
| torso           | `/armour/`, chainmail, `/jacket/`     | shirt     | equipment | chest       |
| torso           | `/cape/`, `/backpack/`                | accessory | equipment | back        |
| torso           | `/waist/`                            | accessory | equipment | waist       |
| legs            | pants / skirts / shorts / leggings    | pants     | cosmetic  | —           |
| legs            | armour (greaves)                      | pants     | equipment | legs        |
| feet            | shoes / sandals / slippers / socks    | shoes     | cosmetic  | —           |
| feet            | armour (armoured boots)               | shoes     | equipment | feet        |
| arms            | gauntlets / bracers / shoulders / wrists | hands  | equipment | hands       |
| headwear        | helmets / hats / hoods / coverings    | head      | equipment | head        |
| headwear        | `/neck/`                             | accessory | equipment | accessory   |
| weapons         | sword / blunt / polearm / ranged / magic | weapon | equipment | weapon      |
| weapons         | `/shields/` or type_name=shield       | offhand   | equipment | offhand     |
| tools           | rod / smash / thrust / whip           | weapon    | equipment | weapon      |

(`shoulders` as a distinct `equipSlot` exists in the enum for future arms sub-splitting; the sample
maps all arms to `hands`. Refine sub-path rules when scaling to 765.)

---

## 6. EXACT C# change-list (untested-by-design — do NOT implement in this step)

### CharacterLayerCatalog.cs

1. `CharacterLayerCatalog`: bump `version` to 4. Remove the global `rows` field (or keep for
   back-compat but ignore it). Add `string[] rowOrder` (default `{"N","W","S","E"}`). Remove the
   class-level `AnimationDef[] animations` (animations are now per-item) — or keep a global default
   set only as a fallback.
2. `AnimationDef`: replace `idleFrame`/`walkStartFrame`-centric shape with
   `{ string id; int rowOffset; int rows; int cols; float fps; }`. (idle is now its own animation;
   walk no longer doubles as idle, so `idleFrame`/`walkStartFrame` are dropped — idle/combat bands
   are sliced directly.)
3. `ItemDef`: add `string kind`, `string equipSlot`, `string paletteMaterial`, `string baseVariant`,
   and `AnimationDef[] animations` (per-item). Change `draws` to the new `DrawDef` (below). Add
   helpers: `bool IsEquipment => kind == "equipment";` and `AnimationDef Anim(string id)`.
4. `DrawDef`: replace `{ string template; int sortOrder; string Resolve(anim,body,variant) }` with
   `{ string bodyType; string sheet; int sortOrder; }`. Drop `Resolve` (no placeholders); add
   `string SheetFor(string bodyType)` on `ItemDef` returning the matching draw's `sheet`.
5. Update `Animation(id)` lookups to be per-item (the catalog-level one becomes a convenience that
   reads the body item's animation set, or is removed).

### CharacterCompositor.cs

6. Add a **palette loader + recolor LUT**: load `palette_definitions/<material>/<material>_ulpc.json`
   (ship these into `Resources/Characters/Palettes/` as `.json.bytes` or embed in the catalog),
   build `Dictionary<Color32,Color32>` from `baseVariant` ramp → target ramp, cache per
   (material, baseVariant, targetVariant).
7. Add `Color32[] Recolor(Color32[] src, lut)` — per-pixel tolerance-±1 match/replace, alpha
   preserved. Skip when target==base.
8. Change `LoadLayerPixels` to load the **whole base sheet once per (item, bodyType)** (not per
   anim/variant). Cache by `sheet` path. Decode the same way (`TextAsset` `<sheet>.png.bytes` →
   `Texture2D.LoadImage`).
9. Rewrite `Bake(appearance, animId)`:
   - For each selected `(itemId, variant)`: get `ItemDef`, its `AnimationDef` for `animId` (skip the
     item if it lacks that animation), load its base sheet for `appearance.bodyType`, recolor by the
     item's `paletteMaterial` + `baseVariant` → chosen variant (or skin tone if `matchBodyColor`).
   - **Slice** the animation band: source rect at `(0, A.rowOffset*F)` size `(A.cols*F, A.rows*F)`,
     read into a per-item band buffer. Width varies per item; clamp/letterbox to the max `A.cols`
     across selected items for the output sheet.
   - Composite bands by `sortOrder` into the output sheet sized `maxCols*F × A.rows*F`.
   - Build sprites: `rows = A.rows` (4 or 1, not 8), `cols = A.cols`. Update `BakeResult`
     (`rowCount = A.rows`; drop `idleFrame`/`walkStartFrame` or map idle→its own anim).
10. `SwatchColor`: load the base sheet's walk band, recolor to the variant, then average — same
    logic, just sourced from the sliced+recolored band instead of a per-variant file.

### LayeredAppearance.cs

11. No structural change required for cosmetics — `Selection()` still yields `(itemId, variant)`.
    For equipment, add equipped-gear entries (player/NPC) feeding `Selection()`: extend
    `accessories` or add an `equipped` list keyed by `equipSlot`. `Sanitize`/`Random` should pull
    from cosmetic items only for the creator (`kind=="cosmetic"`), and equipment is layered on top
    at runtime from the inventory/loot system.
12. `Random(catalog)` and `FixSlot` should filter `cat.Slot(slot).Where(i => i.kind=="cosmetic")`
    so the creator never rolls a plate cuirass as the "shirt".

---

## 7. Importer (`scripts/lpc_import.py`)

- Parses `sheet_definitions/*`, maps category → slot/kind/equipSlot (§5).
- Resolves each animation's source PNG via two v1 patterns:
  A) `<basepath>/<anim>.png` (single base sheet), or
  B) `<basepath>/<anim>/<baseVariant>.png` (per-variant dir; picks the base variant).
- Composites multi-layer items (by zPos) and **stitches all available animations vertically** into
  ONE `.png.bytes` per (item × bodytype) under `Assets/Resources/Characters/Layers/lpc/<item>/`.
- Records `paletteMaterial` (from def `recolors`, else inferred by variant↔palette overlap),
  `baseVariant`, per-item `animations[]` row layout, and aggregates credits into `lpc_credits.txt`.
- Emits `layer_catalog.json` (v4).
- Parameterizable: `--items sample` (default, the 12-item set), `--items all` (the 765), or a
  comma-separated list of sheet_definition relpaths. `--src` / `--dst` configurable.

Run sample:
```
python scripts/lpc_import.py --items sample
```

---

## 8. Sample validation (12 items)

23 sheets / 12 items = 1.92 per item (tunic is female-only in v1 → 1 sheet). 0 missing sheets,
catalog parses, credits 10 KB. See the import report in the handoff for the per-item table.

## 9. Open questions for the scale-up step

- 8-direction game vs 4-direction source: confirm runtime mirroring (W↔E) + diagonal fallback is
  acceptable, or whether the derived 8-row art must be kept for some items.
- 128px `slash_128`/`backslash_128`/`halfslash_128` weapon "oversize" layers are skipped in the
  foundation (custom_animation, 2× frame). Decide whether the game needs them.
- Body types beyond male/female (muscular/teen/pregnant/child) — include or drop for v1 import.
- Where to ship palette JSONs for the runtime (Resources `.json.bytes` vs embed-in-catalog).
- Equipment layering order vs cosmetics (e.g. cape-behind-body) when re-introducing multi-draw items.
