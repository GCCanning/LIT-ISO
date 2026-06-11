"""
Organic tile blends — replaces the ordered-dither gradient_blends approach.

Rules (owner-approved 2026-06-11):
  1. BASE TILES ONLY. Never blend into a transition/blend tile.
  2. WATER LAW. No pair may include a water tile — water boundaries use
     material band tiles (mud/sand/dirt), never dithers.
  3. Clustered value-noise threshold, not ordered dither: blobs of 2-3px
     follow the gradient so the edge reads organic, no screen-door pattern.

Direction = where tile B takes over, in SCREEN space:
  n=top, s=bottom, e=right, w=left, ne/nw/se/sw=diagonals.

Output: assets/tile/blends/blend_{a}__{b}_{dir}.png + manifest entries
(group "blends (new)"). Deterministic seeds — rerunning is a no-op visually.

Run:  py -3 make_blends.py
"""

import json, os, random
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
TILES = os.path.join(HERE, "assets", "tile")
OUT = os.path.join(TILES, "blends")

# (tile_a, tile_b) — b takes over along the gradient direction
PAIRS = [
    ("plains2_02", "snow2_03"),           # grass -> snow (thaw band)
    ("plains2_05", "snow2_04"),           # second thaw variant
    ("plains2_02", "mountain_stone_00"),  # grass -> scree (foothills)
    ("snow2_03",   "mountain_stone_03"),  # snow -> snow-dusted stone
    ("plains2_02", "dirt"),               # grass -> dirt (lake apron rim)
    ("plains2_02", "forest_floor"),       # plains -> forest leaf litter
]
DIRS = {  # gradient unit vectors in screen space (x right, y down)
    "n": (0, -1), "s": (0, 1), "e": (1, 0), "w": (-1, 0),
    "ne": (0.707, -0.707), "nw": (-0.707, -0.707),
    "se": (0.707, 0.707), "sw": (-0.707, 0.707),
}
NOISE_AMP = 0.55   # how far blobs stray from the straight edge
CELL = 3           # noise cell size in px -> 2-3px clusters


def cluster_noise(w, h, seed):
    """Smooth value noise: coarse random grid, bilinear upsample -> blobs."""
    rng = random.Random(seed)
    gw, gh = w // CELL + 2, h // CELL + 2
    grid = [[rng.random() for _ in range(gw)] for _ in range(gh)]
    out = [[0.0] * w for _ in range(h)]
    for y in range(h):
        for x in range(w):
            fx, fy = x / CELL, y / CELL
            x0, y0 = int(fx), int(fy)
            tx, ty = fx - x0, fy - y0
            tx = tx * tx * (3 - 2 * tx)  # smoothstep
            ty = ty * ty * (3 - 2 * ty)
            a = grid[y0][x0] * (1 - tx) + grid[y0][x0 + 1] * tx
            b = grid[y0 + 1][x0] * (1 - tx) + grid[y0 + 1][x0 + 1] * tx
            out[y][x] = a * (1 - ty) + b * ty
    return out


def blend(a_img, b_img, dvec, seed):
    w, h = a_img.size
    noise = cluster_noise(w, h, seed)
    dx, dy = dvec
    out = Image.new("RGBA", (w, h))
    ap, bp, op = a_img.load(), b_img.load(), out.load()
    for y in range(h):
        for x in range(w):
            pa, pb = ap[x, y], bp[x, y]
            if pa[3] == 0 and pb[3] == 0:
                continue
            if pa[3] == 0:
                op[x, y] = pb; continue
            if pb[3] == 0:
                op[x, y] = pa; continue
            # gradient 0..1 along direction + clustered noise
            g = ((x / (w - 1) - 0.5) * dx + (y / (h - 1) - 0.5) * dy) + 0.5
            v = g + (noise[y][x] - 0.5) * NOISE_AMP
            op[x, y] = pb if v > 0.5 else pa
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    made = []
    for a, b in PAIRS:
        for name in (a, b):
            assert "water" not in name, f"WATER LAW: {name} may not be dithered"
            assert not name.startswith(("transition_", "blend_")), \
                f"BASE ONLY: {name} is not a base tile"
        pa, pb = os.path.join(TILES, a + ".png"), os.path.join(TILES, b + ".png")
        if not (os.path.exists(pa) and os.path.exists(pb)):
            print("missing tile for pair", a, b); continue
        ia = Image.open(pa).convert("RGBA")
        ib = Image.open(pb).convert("RGBA").resize(ia.size, Image.NEAREST)
        for d, vec in DIRS.items():
            seed = hash((a, b, d)) & 0xffffffff
            name = f"blend_{a}__{b}_{d}"
            blend(ia, ib, vec, seed).save(os.path.join(OUT, name + ".png"))
            made.append((name, ia.size))
        print(f"{a} -> {b}: 8 dirs")

    # manifest update
    mpath = os.path.join(HERE, "assets.js")
    js = open(mpath, encoding="utf-8").read()
    data = json.loads(js[js.index("["):js.rindex("]") + 1])
    data = [e for e in data if not e["name"].startswith("blend_")]
    for name, (w, h) in made:
        data.append({"cat": "tile", "name": name, "path": f"assets/tile/blends/{name}.png",
                     "w": w, "h": h, "ppu": 32, "bx": 0, "by": 8, "bw": w, "bh": h - 8,
                     "group": "blends (new)"})
    open(mpath, "w", encoding="utf-8").write("const ASSETS = " + json.dumps(data) + ";\n")
    print(f"\n{len(made)} blends; manifest now {len(data)} entries")


if __name__ == "__main__":
    main()
