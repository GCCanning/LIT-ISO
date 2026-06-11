#!/usr/bin/env python3
"""LIT-ISO standalone world-generation preview (Python port of
Docs/handoff/world-gen-prototype/build-world-preview.ps1, plus the polish
backlog from WORLD_GEN_PROTOTYPE_HANDOFF.md).

Renders a seeded procedural world to PNG using the 115-tile isometric pack.
Follows the placement contract in Docs/handoff/world-gen-prototype/tile-taxonomy.md.
PREVIEW ONLY - nothing here touches Unity content.

Run:  python3 world_preview.py --seed 1207 --out /tmp/worldgen
Polish added over the PS1 version:
  * river mouths widen into deltas; long rivers widen to 2 cells downstream
  * beach-only coves (noise-pocketed wider beaches)
  * badlands mesas use strata blocks for their underlay stacks (banded sides)
  * lush-patch (tile 040) edge smoothing - no lone speckles
"""
import argparse
import math
import os
import random
from PIL import Image

LAND = ("meadow", "forest", "badlands")
WATER = ("deep", "shallow", "river")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--seed", type=int, default=1207)
    ap.add_argument("--width", type=int, default=56)
    ap.add_argument("--height", type=int, default=42)
    ap.add_argument("--scale", type=int, default=2)
    ap.add_argument("--out", default="/tmp/worldgen")
    ap.add_argument("--tile-root", default=None,
                    help="folder with tile_000.png..tile_114.png; defaults to the in-repo pack")
    ap.add_argument("--crops", action="store_true", help="also save 1:1 inspection crops")
    args = ap.parse_args()

    seed, W, H, S = args.seed, args.width, args.height, args.scale
    repo = os.path.dirname(os.path.dirname(os.path.abspath(os.path.dirname(__file__) + "/.")))
    tile_root = args.tile_root or os.path.join(
        repo, "Docs", "handoff", "tile-pack-for-codex", "isometric tileset", "separated images")
    os.makedirs(args.out, exist_ok=True)

    # ---------------------------------------------------------------- helpers
    tile_cache = {}

    def tile(n):
        if n not in tile_cache:
            img = Image.open(os.path.join(tile_root, f"tile_{n:03d}.png")).convert("RGBA")
            if S != 1:
                img = img.resize((img.width * S, img.height * S), Image.NEAREST)
            tile_cache[n] = img
        return tile_cache[n]

    def hash01(x, y, salt):
        h = (x * 374761393 + y * 668265263 + (seed + salt) * 1274126177) % 2147483647
        h ^= h >> 13
        h = (h * 1103515245) % 2147483647
        h ^= h >> 7
        return (h % 100000) / 100000.0

    def pick(arr, x, y, salt):
        return arr[int(hash01(x, y, salt) * len(arr)) % len(arr)]

    def noise_field(period, nseed):
        lw = math.ceil(W / period) + 2
        lh = math.ceil(H / period) + 2
        rnd = random.Random(seed * 7919 + nseed)
        lat = [[rnd.random() for _ in range(lh)] for _ in range(lw)]
        f = [[0.0] * H for _ in range(W)]
        for y in range(H):
            gy = y / period
            iy = int(gy)
            fy = gy - iy
            fy = fy * fy * (3 - 2 * fy)
            for x in range(W):
                gx = x / period
                ix = int(gx)
                fx = gx - ix
                fx = fx * fx * (3 - 2 * fx)
                a, b = lat[ix][iy], lat[ix + 1][iy]
                c, d = lat[ix][iy + 1], lat[ix + 1][iy + 1]
                f[x][y] = (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy
        return f

    # ---------------------------------------------------------- world fields
    n1 = noise_field(14.0, 101)
    n2 = noise_field(6.0, 202)
    m1 = noise_field(18.0, 303)
    m2 = noise_field(8.0, 404)
    flowerN = noise_field(4.0, 505)
    bushN = noise_field(3.5, 606)
    rockN = noise_field(4.5, 707)
    lushN = noise_field(5.0, 808)
    coveN = noise_field(6.0, 909)

    elev = [[0.0] * H for _ in range(W)]
    moist = [[0.0] * H for _ in range(W)]
    for y in range(H):
        for x in range(W):
            nx = 2.0 * x / (W - 1) - 1.0
            ny = 2.0 * y / (H - 1) - 1.0
            d = math.sqrt(nx * nx + ny * ny) / 1.4142
            edge = max(abs(nx), abs(ny))
            e = (n1[x][y] * 0.62 + n2[x][y] * 0.38) * 0.72 + (1.0 - d) * 0.52 - 0.20
            if edge > 0.72:
                e -= (edge - 0.72) * 1.6  # guarantee ocean at the world border
            elev[x][y] = e
            moist[x][y] = m1[x][y] * 0.75 + m2[x][y] * 0.25

    # Height tiers are RELATIVE to this landmass (real-world logic: every
    # continent has high ground), so highlands/rivers exist on every seed.
    land_elevs = sorted(elev[x][y] for y in range(H) for x in range(W)
                        if elev[x][y] >= 0.44)
    def quantile(q):
        if not land_elevs:
            return float("inf")
        return land_elevs[min(len(land_elevs) - 1, int(q * len(land_elevs)))]
    t_lvl1 = quantile(0.80)
    t_lvl2 = quantile(0.94)

    kind = [[""] * H for _ in range(W)]
    lvl = [[0] * H for _ in range(W)]
    for y in range(H):
        for x in range(W):
            e = elev[x][y] + (hash01(x, y, 9) - 0.5) * 0.012  # break straight band edges
            m = moist[x][y]
            if e < 0.30:
                kind[x][y] = "deep"
            elif e < 0.40:
                kind[x][y] = "shallow"
            elif e < 0.44:
                kind[x][y] = "beach"
            else:
                kind[x][y] = "badlands" if m < 0.30 else ("meadow" if m < 0.62 else "forest")
                if e > t_lvl1:
                    lvl[x][y] = 1
                if e > t_lvl2:
                    lvl[x][y] = 2

    def kind_at(x, y):
        if x < 0 or x >= W or y < 0 or y >= H:
            return "deep"
        return kind[x][y]

    N4 = ((1, 0), (-1, 0), (0, 1), (0, -1))

    # cleanup: de-speckle the deep/shallow boundary (8-neighbour majority);
    # kills checkerboard dithering on gentle elevation gradients while
    # keeping curved coastlines intact
    for _ in range(3):
        flips = []
        for y in range(H):
            for x in range(W):
                k = kind[x][y]
                if k not in ("deep", "shallow"):
                    continue
                other = "shallow" if k == "deep" else "deep"
                n_other = sum(1 for dy in (-1, 0, 1) for dx in (-1, 0, 1)
                              if not (dx == 0 and dy == 0)
                              and kind_at(x + dx, y + dy) == other)
                if n_other >= 5:
                    flips.append((x, y, other))
        for x, y, nk in flips:
            kind[x][y] = nk

    # cleanup: drown beach spits with no land in their 8-neighbourhood
    for _ in range(2):
        for y in range(H):
            for x in range(W):
                if kind[x][y] != "beach":
                    continue
                has_land = any(kind_at(x + dx, y + dy) in LAND
                               for dy in (-1, 0, 1) for dx in (-1, 0, 1))
                if not has_land:
                    kind[x][y] = "shallow"

    # cleanup: inland lakes are LIGHT water (taxonomy: deep navy is ocean-only).
    # Flood-fill deep from the border; unconnected deep pockets become shallow.
    ocean = [[False] * H for _ in range(W)]
    stack = []
    for x in range(W):
        for y in (0, H - 1):
            if kind[x][y] == "deep" and not ocean[x][y]:
                ocean[x][y] = True
                stack.append((x, y))
    for y in range(H):
        for x in (0, W - 1):
            if kind[x][y] == "deep" and not ocean[x][y]:
                ocean[x][y] = True
                stack.append((x, y))
    while stack:
        cx, cy = stack.pop()
        for dx, dy in N4:
            tx, ty = cx + dx, cy + dy
            if 0 <= tx < W and 0 <= ty < H and kind[tx][ty] == "deep" \
                    and not ocean[tx][ty]:
                ocean[tx][ty] = True
                stack.append((tx, ty))
    for y in range(H):
        for x in range(W):
            if kind[x][y] == "deep" and not ocean[x][y]:
                kind[x][y] = "shallow"

    # taxonomy: deep water never touches beach/land directly - pad with shallow
    for y in range(H):
        for x in range(W):
            if kind[x][y] == "deep" and any(
                    kind_at(x + dx, y + dy) in ("beach",) + LAND for dx, dy in N4):
                kind[x][y] = "shallow"

    # cleanup: erode terrace fragments per level, top level first
    for level in (2, 1):
        for y in range(H):
            for x in range(W):
                if lvl[x][y] != level:
                    continue
                n = sum(1 for dx, dy in N4
                        if 0 <= x + dx < W and 0 <= y + dy < H and lvl[x + dx][y + dy] >= level)
                if n < 2:
                    lvl[x][y] = level - 1

    # POLISH: beach-only coves - noise pockets widen the beach into the land
    for y in range(H):
        for x in range(W):
            if kind[x][y] != "beach" or coveN[x][y] <= 0.66:
                continue
            for dx, dy in N4:
                tx, ty = x + dx, y + dy
                if 0 <= tx < W and 0 <= ty < H and lvl[tx][ty] == 0 \
                        and kind[tx][ty] in ("meadow", "forest"):
                    kind[tx][ty] = "beach"

    # ------------------------------------------------------------------ rivers
    best = {}
    for y in range(2, H - 2):
        for x in range(2, W - 2):
            if lvl[x][y] >= 1:
                best[(x, y)] = elev[x][y]
    if not best:  # flat world fallback: source from the highest land cells
        for y in range(2, H - 2):
            for x in range(2, W - 2):
                if kind[x][y] in LAND:
                    best[(x, y)] = elev[x][y]
    sources = []
    for (px, py), _ in sorted(best.items(), key=lambda kv: -kv[1]):
        if all(abs(sx - px) + abs(sy - py) >= 16 for sx, sy in sources):
            sources.append((px, py))
        if len(sources) >= 2:
            break

    river_cells = {}
    river_paths = []
    for sx, sy in sources:
        cx, cy = sx, sy
        path = []
        for _ in range(300):
            k = kind[cx][cy]
            if k in ("deep", "shallow"):
                break
            river_cells[(cx, cy)] = True
            path.append((cx, cy))
            best_e, bx, by = float("inf"), cx, cy
            for dx, dy in N4:
                tx, ty = cx + dx, cy + dy
                if not (0 <= tx < W and 0 <= ty < H) or (tx, ty) in river_cells:
                    continue
                te = elev[tx][ty] + hash01(tx, ty, 31) * 0.035  # meander
                if te < best_e:
                    best_e, bx, by = te, tx, ty
            if (bx, by) == (cx, cy):
                # stuck in a basin: end the river in a small carved pond
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        tx, ty = cx + dx, cy + dy
                        if 1 <= tx < W - 1 and 1 <= ty < H - 1 \
                                and kind[tx][ty] in LAND + ("beach",):
                            river_cells[(tx, ty)] = True
                break
            cx, cy = bx, by
        river_paths.append(path)

    # POLISH: widen long rivers downstream + open a delta at the mouth
    widen = {}
    for path in river_paths:
        n = len(path)
        for i, (cx, cy) in enumerate(path):
            if i + 1 < n:
                fx, fy = path[i + 1][0] - cx, path[i + 1][1] - cy
            elif i > 0:
                fx, fy = cx - path[i - 1][0], cy - path[i - 1][1]
            else:
                continue
            perp = ((-fy, fx), (fy, -fx))
            near_mouth = i >= n - 4 and any(
                kind_at(cx + dx, cy + dy) in ("shallow", "deep") for dx, dy in
                ((2, 0), (-2, 0), (0, 2), (0, -2), (1, 1), (-1, -1), (1, -1), (-1, 1)))
            if near_mouth:  # delta: widen both sides for the last cells
                sides = perp
            elif i >= 14:   # mature river: widen one consistent side
                sides = (perp[0],)
            else:
                continue
            for dx, dy in sides:
                tx, ty = cx + dx, cy + dy
                if 0 <= tx < W and 0 <= ty < H and kind[tx][ty] in LAND + ("beach",):
                    widen[(tx, ty)] = True
    river_cells.update(widen)

    for (rx, ry) in river_cells:
        kind[rx][ry] = "river"
        lvl[rx][ry] = 0
    # river banks: only drop raised cells beside the river (no cliff-walled canals)
    for y in range(H):
        for x in range(W):
            if lvl[x][y] >= 1 and any(kind_at(x + dx, y + dy) == "river" for dx, dy in N4):
                lvl[x][y] = 0

    # hydrology stats - lets head-less runs verify every seed has water features
    n_land = sum(1 for y in range(H) for x in range(W) if kind[x][y] in LAND)
    n_river = sum(1 for y in range(H) for x in range(W) if kind[x][y] == "river")
    mouths = sum(1 for p in river_paths if p and any(
        kind_at(p[-1][0] + dx, p[-1][1] + dy) in ("shallow", "deep") for dx, dy in N4))
    print(f"seed {seed}: land {100.0 * n_land / (W * H):.0f}%  "
          f"rivers {len(river_paths)} ({n_river} cells, {mouths} reach open water)")

    # canopy: interior forest cells, clumped, never on the waterline
    canopy = [[False] * H for _ in range(W)]
    for y in range(H):
        for x in range(W):
            if kind[x][y] != "forest" or lvl[x][y] != 0:
                continue
            if moist[x][y] < 0.68 and bushN[x][y] < 0.62:
                continue
            if all(kind_at(x + dx, y + dy) not in ("deep", "shallow", "river", "beach")
                   for dx, dy in N4):
                canopy[x][y] = True

    # POLISH: lush mask (tile 040 patches) with cellular smoothing - no speckles
    lush = [[lushN[x][y] > 0.74 for y in range(H)] for x in range(W)]
    for _ in range(2):
        nxt = [[False] * H for _ in range(W)]
        for y in range(H):
            for x in range(W):
                n = sum(1 for dx, dy in N4
                        if 0 <= x + dx < W and 0 <= y + dy < H and lush[x + dx][y + dy])
                nxt[x][y] = (n >= 2) if lush[x][y] else (n >= 3)
        lush = nxt

    # ---------------------------------------------------------- decision pass
    d_terrain = [[-1] * H for _ in range(W)]
    d_under = [[-1] * H for _ in range(W)]
    d_prop = [[-1] * H for _ in range(W)]
    sparkles = []

    for y in range(H):
        for x in range(W):
            k, L = kind[x][y], lvl[x][y]
            terrain = under = prop = -1
            near_land = near_shallow = near_badlands = near_meadow = False
            near_lvl_up = False
            for dx, dy in N4:
                tx, ty = x + dx, y + dy
                nk = kind_at(tx, ty)
                if nk in ("beach",) + LAND:
                    near_land = True
                if nk in ("shallow", "river"):
                    near_shallow = True
                if nk == "badlands":
                    near_badlands = True
                if nk == "meadow":
                    near_meadow = True
                if 0 <= tx < W and 0 <= ty < H and lvl[tx][ty] > L:
                    near_lvl_up = True

            r = hash01(x, y, 11)
            if k == "deep":
                if near_shallow and hash01(x, y, 12) < 0.15:
                    terrain = pick((87, 88, 90, 95, 96, 98), x, y, 13)  # surf swell
                elif r < 0.75:
                    terrain = 92
                elif r < 0.85:
                    terrain = 101
                else:
                    terrain = pick((93, 94, 102, 103), x, y, 14)
                if not near_shallow and hash01(x, y, 15) < 0.025:
                    sparkles.append((x, y, pick((82, 83, 84, 85), x, y, 16)))
            elif k in ("shallow", "river"):
                if near_land and r < 0.25:
                    terrain = pick((106, 107, 108), x, y, 17)  # shore wash
                elif r < 0.85:
                    terrain = 104
                else:
                    terrain = pick((105, 109, 110), x, y, 18)
                if near_land and hash01(x, y, 19) < 0.05:
                    prop = pick((70, 71, 73, 78, 80, 81), x, y, 20)  # foam-footed stones
                elif hash01(x, y, 21) < 0.015:
                    terrain = 114
            elif k == "beach":
                terrain = 0 if r < 0.60 else (10 if r < 0.85 else 21)
            elif k == "meadow":
                if L >= 1:
                    under = 3
                    terrain = pick((22, 23, 24), x, y, 22)
                    if L == 2 and rockN[x][y] > 0.45:
                        prop = pick((61, 62, 65, 67, 68), x, y, 23)  # rocky crown
                else:
                    if near_badlands:
                        terrain = pick((19, 20), x, y, 24)  # sprout transition
                    elif lush[x][y]:
                        terrain = 40  # lush patch (smoothed mask)
                    elif r < 0.90:
                        terrain = 37
                    else:
                        terrain = pick((38, 39), x, y, 25)
                    if terrain == 37:
                        if flowerN[x][y] > 0.68 and hash01(x, y, 26) < 0.6:
                            prop = pick((41, 42, 44, 46, 47, 43, 45), x, y, 27)
                        elif bushN[x][y] > 0.80 and hash01(x, y, 28) < 0.5:
                            prop = pick((30, 31, 35), x, y, 29)
                        elif near_lvl_up and hash01(x, y, 30) < 0.04:
                            prop = pick((65, 67), x, y, 31)  # cliff-base boulder
            elif k == "forest":
                if L >= 1:
                    under = 3
                    terrain = pick((22, 23, 24), x, y, 32)
                    if L == 2 and rockN[x][y] > 0.45:
                        prop = pick((61, 62, 65, 67, 68), x, y, 33)
                elif canopy[x][y]:
                    terrain = pick((29, 29, 29, 27, 28), x, y, 34)  # leafy-dominant canopy
                else:
                    if near_badlands or not near_land:
                        terrain = pick((25, 26), x, y, 35)  # root-cliff forest edge
                    else:
                        terrain = 40
                    if terrain == 40:
                        if bushN[x][y] > 0.50 and hash01(x, y, 36) < 0.55:
                            prop = pick((30, 31, 32, 33, 34, 35, 36), x, y, 37)
                        elif near_meadow and hash01(x, y, 38) < 0.08:
                            prop = pick((48, 49, 50, 51, 52), x, y, 39)  # edge logs
            elif k == "badlands":
                if L >= 1:
                    # POLISH: strata underlay stacks - banded mesa sides
                    under = pick((14, 15, 16), x, y, 46)
                    terrain = pick((14, 15, 16), x, y, 40)  # strata mesa top
                    if rockN[x][y] > 0.55:
                        prop = pick((53, 56, 57), x, y, 41)
                else:
                    terrain = 17 if r < 0.60 else (18 if r < 0.85 else 3)
                    if rockN[x][y] > 0.68 and hash01(x, y, 42) < 0.55:
                        prop = pick((53, 54, 55, 56, 57, 58, 59, 60), x, y, 43)
                    elif hash01(x, y, 44) < 0.03:
                        terrain = pick((11, 12, 13), x, y, 45)  # rubble accent
            d_terrain[x][y] = terrain
            d_under[x][y] = under
            d_prop[x][y] = prop

    # -------------------------------------------------------------- paint pass
    TILE = 32 * S
    STEPX = 16 * S
    STEPY = 8 * S
    RAISE = 8 * S       # one block step
    PROP_LIFT = -5      # prop lift onto the tile top (matches PS1 at S=2)

    canvas_w = (W + H - 2) * STEPX + TILE + 64
    canvas_h = (W + H - 2) * STEPY + TILE + 96
    img = Image.new("RGBA", (canvas_w, canvas_h), (0, 0, 0, 255))
    origin_x = canvas_w // 2
    origin_y = (canvas_h - (W + H - 2) * STEPY) // 2

    def draw(x, y, n, off):
        sx = origin_x + (x - y) * STEPX - TILE // 2
        sy = origin_y + (x + y) * STEPY - TILE // 2 + off
        t = tile(n)
        img.alpha_composite(t, (sx, sy))

    # z-aware painter: a top raised z steps draws in diagonal group (x+y+z),
    # AFTER that diagonal's flat tiles; underlays stay in their natural group.
    for ssum in range(W + H + 1):
        for z in range(3):
            diag = ssum - z
            if diag < 0:
                continue
            for y in range(H):
                x = diag - y
                if x < 0 or x >= W:
                    continue
                L = lvl[x][y]
                if z < L:
                    if d_under[x][y] >= 0:
                        draw(x, y, d_under[x][y], -z * RAISE)
                elif z == L:
                    if d_terrain[x][y] >= 0:
                        draw(x, y, d_terrain[x][y], -z * RAISE)
                    if d_prop[x][y] >= 0:
                        draw(x, y, d_prop[x][y], -z * RAISE + PROP_LIFT)

    for sx, sy, sn in sparkles:
        draw(sx, sy, sn, 0)

    out_png = os.path.join(args.out, f"world-preview-seed{seed}.png")
    img.convert("RGB").save(out_png)
    print(out_png)

    if args.crops:
        cw, ch = img.width, img.height
        boxes = {
            "center": (cw // 2 - 400, ch // 2 - 300, cw // 2 + 400, ch // 2 + 300),
            "west": (max(0, cw // 4 - 400), ch // 2 - 300, cw // 4 + 400, ch // 2 + 300),
            "south": (cw // 2 - 400, min(ch - 600, ch * 3 // 4 - 300),
                      cw // 2 + 400, min(ch, ch * 3 // 4 + 300)),
        }
        for name, box in boxes.items():
            p = os.path.join(args.out, f"crop-{name}-seed{seed}.png")
            img.crop(box).convert("RGB").save(p)
            print(p)


if __name__ == "__main__":
    main()
