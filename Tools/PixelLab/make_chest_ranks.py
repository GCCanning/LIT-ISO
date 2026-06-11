"""
LOCAL chest rank derivation - NO API calls, zero generations.

Takes the picked dungeon chest PNG (wooden chest, dark iron banding) and
derives the rank ladder via hue/level shifts in HSV space, per
Docs/handoff/PROP_GENERATION_CATALOG.md section E ("Iron/Gilded = free local
hue+banding recolors"):

  common    - as-is (straight copy)
  uncommon  - greenish brass banding, slightly richer wood
  rare      - cool blue iron banding
  epic      - purple banding with a slight glow lift on the metal pixels
  gilded    - warm gold banding, warmed wood

Usage:
  py -3 make_chest_ranks.py <path\\to\\picked_chest.png>

Output: dungeon_chest_{rank}.png written alongside the input file.
PIL only.
"""

import colorsys
import os
import shutil
import sys

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow required: py -3 -m pip install pillow")

# Pixel classification: the chest is wood (brownish, saturated mid hues) plus
# dark iron banding (low saturation and/or very dark). Outlines stay put.
METAL_SAT_MAX = 0.25   # at/below this saturation -> metal
METAL_VAL_MAX = 0.32   # OR at/below this value (near-black banding/outline)
OUTLINE_VAL_MAX = 0.12 # keep the darkest outline pixels untouched

# rank -> (metal hue 0..1, metal min saturation, metal value multiplier,
#          wood hue shift, wood saturation multiplier, wood value multiplier)
RANKS = {
    "common":   None,  # straight copy
    "uncommon": {"metal_hue": 0.21, "metal_sat": 0.40, "metal_val": 1.05,
                 "wood_hue": 0.01, "wood_sat": 1.10, "wood_val": 1.00},
    "rare":     {"metal_hue": 0.58, "metal_sat": 0.42, "metal_val": 1.05,
                 "wood_hue": -0.01, "wood_sat": 0.95, "wood_val": 0.98},
    "epic":     {"metal_hue": 0.76, "metal_sat": 0.52, "metal_val": 1.28,
                 "wood_hue": 0.02, "wood_sat": 1.05, "wood_val": 1.02},
    "gilded":   {"metal_hue": 0.115, "metal_sat": 0.65, "metal_val": 1.22,
                 "wood_hue": 0.015, "wood_sat": 1.15, "wood_val": 1.06},
}


def classify(h, s, v):
    if v <= OUTLINE_VAL_MAX:
        return "outline"
    if s <= METAL_SAT_MAX or v <= METAL_VAL_MAX:
        return "metal"
    return "wood"


def recolor(im, cfg):
    im = im.convert("RGBA")
    out = im.copy()
    px_in, px_out = im.load(), out.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px_in[x, y]
            if a == 0:
                continue
            h, s, v = colorsys.rgb_to_hsv(r / 255.0, g / 255.0, b / 255.0)
            kind = classify(h, s, v)
            if kind == "outline":
                continue
            if kind == "metal":
                h = cfg["metal_hue"]
                s = max(s, cfg["metal_sat"])
                v = min(1.0, v * cfg["metal_val"])
            else:  # wood
                h = (h + cfg["wood_hue"]) % 1.0
                s = min(1.0, s * cfg["wood_sat"])
                v = min(1.0, v * cfg["wood_val"])
            nr, ng, nb = colorsys.hsv_to_rgb(h, s, v)
            px_out[x, y] = (int(round(nr * 255)), int(round(ng * 255)),
                            int(round(nb * 255)), a)
    return out


def main():
    if len(sys.argv) != 2:
        sys.exit("usage: py -3 make_chest_ranks.py <picked_chest.png>")
    src = sys.argv[1]
    if not os.path.isfile(src):
        sys.exit(f"input not found: {src}")
    base = Image.open(src).convert("RGBA")
    out_dir = os.path.dirname(os.path.abspath(src))
    for rank, cfg in RANKS.items():
        dst = os.path.join(out_dir, f"dungeon_chest_{rank}.png")
        if cfg is None:
            shutil.copyfile(src, dst)
        else:
            recolor(base, cfg).save(dst)
        print("wrote", dst)
    print("\nDone - 5 rank chests derived locally (0 generations spent).")


if __name__ == "__main__":
    main()
