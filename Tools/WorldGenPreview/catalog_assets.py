#!/usr/bin/env python3
"""
Color + naming catalog for LIT-ISO PixelLab tiles/props.

Scans Assets/Resources/Tiles and Assets/Resources/Decorations, extracts a palette
and color stats per asset (ignoring transparent pixels), infers a family from the
filename prefix, and classifies each tile by ROLE (base / accent / edge-transition)
using color variance + alpha. Emits Tools/WorldGenPreview/asset_catalog.json which
the biome builder + worldgen prototype consume.

Run: python3 Tools/WorldGenPreview/catalog_assets.py
"""
import glob, json, os, colorsys
from collections import Counter
import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
TILES = os.path.join(ROOT, "Assets", "Resources", "Tiles")
DECOS = os.path.join(ROOT, "Assets", "Resources", "Decorations")
OUT = os.path.join(os.path.dirname(__file__), "asset_catalog.json")

# Family prefix -> (biome affinity, semantic kind). Longest prefix wins.
FAMILY = [
    ("plains2", "meadow", "ground"), ("plains_flower", "meadow", "ground"),
    ("plains_grass", "meadow", "ground"), ("plains", "meadow", "prop"),
    ("grass", "meadow", "ground"), ("flower", "meadow", "prop"),
    ("forest", "forest", None), ("canopy", "forest", "ground"),
    ("pine", "forest", "prop"), ("tree", "meadow", "prop"), ("bush", "meadow", "prop"),
    ("sand", "beach", "ground"), ("shore", "beach", "prop"), ("dock", "beach", "prop"),
    ("boat", "beach", "prop"), ("cactus", "badlands", "prop"),
    ("badlands", "badlands", "ground"),
    ("snow", "snow", "ground"), ("stone_snowcap", "snow", "ground"),
    ("stone", "mountain", "ground"), ("scree", "mountain", "ground"),
    ("ore", "mountain", "prop"), ("copper_vein", "mountain", "prop"),
    ("rock", "meadow", "prop"), ("shared_gray_rock", "mountain", "prop"),
    ("dirt", "any", "ground"), ("soil", "farm", "ground"), ("farm", "farm", "ground"),
    ("scarecrow", "farm", "prop"),
    ("water", "ocean", "ground"),
    ("dungeon", "dungeon", "ground"), ("lava", "dungeon", "ground"),
    ("fire_trap", "dungeon", "ground"),
    ("planks", "structure", "ground"), ("stone_path", "structure", "ground"),
    ("stone_block", "structure", "ground"),
    # settlement / interiors / stations / lighting
    ("guild", "settlement", "prop"), ("library", "settlement", "prop"),
    ("shop", "settlement", "prop"), ("tavern", "settlement", "prop"),
    ("market", "settlement", "prop"), ("anvil", "station", "prop"),
    ("alchemy", "station", "prop"), ("loom", "station", "prop"),
    ("furnace", "station", "prop"), ("crafting", "station", "prop"),
    ("cooking", "station", "prop"), ("grindstone", "station", "prop"),
    ("sawmill", "station", "prop"), ("tanning", "station", "prop"),
    ("rune_station", "station", "prop"), ("keg", "station", "prop"),
    ("bar_counter", "station", "prop"), ("book", "settlement", "prop"),
    ("weapon_rack", "station", "prop"), ("campfire", "any", "prop"),
    ("lantern", "any", "prop"), ("torch", "any", "prop"), ("brazier", "any", "prop"),
    ("candle", "any", "prop"), ("glowbug", "any", "prop"), ("wisp", "any", "prop"),
    ("log", "forest", "prop"), ("stump", "forest", "prop"), ("tuft", "meadow", "prop"),
]


def family_of(name):
    best = None
    for pre, biome, kind in FAMILY:
        if name.startswith(pre) and (best is None or len(pre) > len(best[0])):
            best = (pre, biome, kind)
    if best:
        return best[1], best[2]
    return "unknown", None


def analyze(path):
    im = Image.open(path).convert("RGBA")
    a = np.asarray(im).reshape(-1, 4)
    opaque = a[a[:, 3] > 40][:, :3].astype(np.float32)
    cover = float((a[:, 3] > 40).mean())
    if len(opaque) == 0:
        return dict(mean=[0, 0, 0], hsv=[0, 0, 0], palette=[], coverage=0.0, variance=0.0)
    mean = opaque.mean(axis=0)
    var = float(opaque.var(axis=0).mean())  # color spread within the tile
    r, g, b = float(mean[0]) / 255.0, float(mean[1]) / 255.0, float(mean[2]) / 255.0
    h, s, v = colorsys.rgb_to_hsv(r, g, b)
    h, s, v = float(h), float(s), float(v)
    # dominant palette: quantize to 5 levels per channel, take top buckets
    q = (opaque // 51 * 51 + 25).astype(int)
    keys = Counter(map(tuple, q))
    pal = [list(map(int, c)) for c, _ in keys.most_common(4)]
    return dict(mean=[int(x) for x in mean], hsv=[round(h, 3), round(s, 3), round(v, 3)],
                palette=pal, coverage=round(cover, 3), variance=round(var, 1))


def tile_role(name, st):
    """base = solid biome body; accent = decorated variant (flowers/tufts/mud);
    edge = transition/border variant; numbered families: low variance -> base,
    high variance -> accent, mid -> edge candidate."""
    n = name.lower()
    if any(k in n for k in ("flower", "tuft", "mud", "litter", "underbrush", "moss",
                            "swell", "deep", "path", "cracked", "scree", "snowcap")):
        return "accent"
    if st["variance"] > 1100:
        return "accent"
    if st["variance"] > 600:
        return "edge"
    return "base"


def scan(folder, kind_default):
    out = []
    for p in sorted(glob.glob(os.path.join(folder, "*.png"))):
        name = os.path.basename(p)[:-4]
        biome, kind = family_of(name)
        st = analyze(p)
        rec = dict(name=name, biome=biome, kind=kind or kind_default, **st)
        if (kind or kind_default) == "ground":
            rec["role"] = tile_role(name, st)
        out.append(rec)
    return out


def main():
    tiles = scan(TILES, "ground")
    decos = scan(DECOS, "prop")
    catalog = dict(tiles=tiles, decorations=decos)

    # biome grouping summary
    biomes = {}
    for rec in tiles + decos:
        b = rec["biome"]
        biomes.setdefault(b, {"ground_base": [], "ground_accent": [], "ground_edge": [],
                              "props": [], "palette": []})
        if rec["kind"] == "ground":
            role = rec.get("role", "base")
            biomes[b][f"ground_{role}"].append(rec["name"])
            biomes[b]["palette"].append(rec["mean"])
        else:
            biomes[b]["props"].append(rec["name"])
    for b, g in biomes.items():
        if g["palette"]:
            arr = np.array(g["palette"])
            g["mean_color"] = [int(x) for x in arr.mean(axis=0)]
        g.pop("palette", None)
    catalog["biome_groups"] = biomes

    with open(OUT, "w") as f:
        json.dump(catalog, f, indent=1)

    print("cataloged", len(tiles), "tiles +", len(decos), "props ->", OUT)
    for b in sorted(biomes):
        g = biomes[b]
        print("[" + b + "]", "mean=" + str(g.get("mean_color")),
              "base=" + str(len(g["ground_base"])),
              "accent=" + str(len(g["ground_accent"])),
              "edge=" + str(len(g["ground_edge"])),
              "props=" + str(len(g["props"])))


if __name__ == "__main__":
    main()
