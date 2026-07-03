#!/usr/bin/env python3
"""Shikashi's Fantasy Icons Pack v2 -> Assets/Resources/Items/<itemId>.png

Fills every item id that has no icon yet (ItemIconResolver falls back to
Resources/Items/<id>). Source: Tools/UIKits/Shikashi/ (CC-BY: credit
"Matt Firth (shikashipx)" + game-icons.net - see CREDITS_VFX.txt).
32x32 cells addressed as (row, col) on the transparent spritesheet.
Some icons get a palette shift so one glyph serves related items.

Run: python3 Tools/UIKits/shikashi_items_build.py  (from repo root)
"""
import colorsys, os, sys, uuid
from PIL import Image

ROOT = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else ".")
SHEET = os.path.join(ROOT, "Tools", "UIKits", "Shikashi",
                     "Shikashi's Fantasy Icons Pack v2", "#1 - Transparent Icons.png")
OUT = os.path.join(ROOT, "Assets", "Resources", "Items")
TEMPLATE_META = os.path.join(OUT, "apple.png.meta")

# itemId -> (row, col, recolour)   recolour: None | (hue_shift 0..1, sat_mul, val_mul)
ICONS = {
    "camp_stew":               (19, 8,  None),          # cauldron on fire
    "campfire_item":           (4,  2,  None),          # campfire
    "carrot":                  (14, 6,  None),
    "chest_item":              (11, 11, None),          # treasure chest
    "copper_bar":              (17, 3,  (-0.07, 1.05, 0.82)),  # gold ingot -> copper
    "fireplace_item":          (4,  2,  (-0.02, 0.85, 0.80)),  # deeper hearth campfire
    "furnace_item":            (4,  4,  None),          # blacksmith anvil/forge
    "lantern_item":            (10, 9,  None),
    "library_building_item":   (13, 8,  None),          # open book
    "library_plot_item":       (13, 12, (0.0, 0.75, 1.0)),     # old map, faded
    "roasted_apple":           (14, 0,  (0.02, 0.75, 0.72)),   # apple -> baked brown
    "rootcellar_portal_item":  (11, 0,  None),          # runestone
    "slime_goo":               (20, 7,  None),          # green powder pile
    "stone_block_item":        (17, 1,  None),          # stone
    "stone_path_item":         (17, 1,  (0.0, 0.85, 0.78)),    # darker stone
    "tavern_building_item":    (13, 15, None),          # bottle of wine
    "tavern_door_item":        (11, 9,  None),          # brass key
    "tavern_plot_item":        (13, 12, (0.03, 1.05, 0.95)),   # old map, warm
    "wheat":                   (14, 7,  None),          # sweetcorn (closest crop glyph)
    "wood_floor_item":         (19, 11, None),          # wooden beam
}

def recolour(im, hs, sm, vm):
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            h = (h + hs) % 1.0
            s = min(1.0, s * sm)
            v = min(1.0, v * vm)
            r2, g2, b2 = colorsys.hsv_to_rgb(h, s, v)
            px[x, y] = (int(r2 * 255), int(g2 * 255), int(b2 * 255), a)
    return im

sheet = Image.open(SHEET).convert("RGBA")
template = open(TEMPLATE_META, encoding="utf-8").read() if os.path.exists(TEMPLATE_META) else None

for item, (r, c, rec) in sorted(ICONS.items()):
    dst = os.path.join(OUT, item + ".png")
    if os.path.exists(dst):
        print(f"  skip {item} (icon already exists)")
        continue
    cell = sheet.crop((c * 32, r * 32, c * 32 + 32, r * 32 + 32))
    if not cell.getbbox():
        print(f"  !! EMPTY cell for {item} at ({r},{c}) - check mapping")
        continue
    if rec:
        cell = recolour(cell, *rec)
    cell.save(dst)
    meta = dst + ".meta"
    if template and not os.path.exists(meta):
        lines = template.splitlines(keepends=True)
        out = []
        for ln in lines:
            out.append(f"guid: {uuid.uuid4().hex}\n" if ln.startswith("guid:") else ln)
        open(meta, "w", encoding="utf-8", newline="").write("".join(out))
    print(f"  {item}.png  <- sheet ({r},{c}){' recoloured' if rec else ''}")
print("done")
