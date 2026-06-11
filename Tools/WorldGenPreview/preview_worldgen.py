"""
Reference implementation of Docs/handoff/WORLD_GENERATION_SPEC.md.
Reads Assets/StreamingAssets/worldgen/*.json and renders a preview map so the
owner can QC generated worlds before Codex ports the algorithm into the Unity
sampler. Also runs the spec's acceptance checks and prints a report.

Usage: py -3 preview_worldgen.py [--seed 4242] [--size 160] [--out preview.png]
"""

import argparse, json, math, os, random, sys

try:
    from PIL import Image
except ImportError:
    sys.exit("pip install pillow")

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                     "..", "..", "Assets", "StreamingAssets", "worldgen"))

# ---------------------------------------------------------------- noise

class ValueNoise:
    def __init__(self, seed, freq_cells, octaves):
        self.rng = random.Random(seed)
        self.freq = max(8.0, float(freq_cells))
        self.octaves = max(1, octaves)
        self.perm = list(range(512)); self.rng.shuffle(self.perm)

    def _lattice(self, ix, iy):
        return self.perm[(self.perm[ix & 511] + iy) & 511] / 511.0

    def _smooth(self, t): return t * t * (3 - 2 * t)

    def _sample(self, x, y):
        ix, iy = int(math.floor(x)), int(math.floor(y))
        fx, fy = x - ix, y - iy
        a = self._lattice(ix, iy);     b = self._lattice(ix + 1, iy)
        c = self._lattice(ix, iy + 1); d = self._lattice(ix + 1, iy + 1)
        sx, sy = self._smooth(fx), self._smooth(fy)
        return (a + (b - a) * sx) + ((c + (d - c) * sx) - (a + (b - a) * sx)) * sy

    def at(self, x, y):
        v, amp, f, tot = 0.0, 1.0, 1.0 / self.freq, 0.0
        for o in range(self.octaves):
            v += self._sample(x * f * (2 ** o) + o * 1000, y * f * (2 ** o) + o * 1000) * amp
            tot += amp; amp *= 0.5
        return v / tot

# ---------------------------------------------------------------- generator

def generate(seed, N):
    P = json.load(open(os.path.join(ROOT, "noise_params.json")))
    C = ValueNoise(seed + 1, P["continents"]["freqCells"], P["continents"]["octaves"])
    E = ValueNoise(seed + 2, P["erosion"]["freqCells"], P["erosion"]["octaves"])
    M = ValueNoise(seed + 4, P["moisture"]["freqCells"], P["moisture"]["octaves"])
    CL = ValueNoise(seed + 5, P["cliffiness"]["freqCells"], P["cliffiness"]["octaves"])
    oceanBelow = P["continents"]["oceanBelow"]; coastBelow = P["continents"]["coastBelow"]
    maxH = P["elevation"]["maxHeight"]; mountainAbove = P["elevation"]["mountainAbove"]

    # L0/L1: fields -> elevation
    h = [[0] * N for _ in range(N)]
    water = [[None] * N for _ in range(N)]   # None | 'ocean' | 'lake' | 'river'
    for y in range(N):
        for x in range(N):
            c = C.at(x, y)
            if c < oceanBelow:
                water[y][x] = "ocean"; h[y][x] = 0
            else:
                t = max(0.0, (c - oceanBelow) / (1 - oceanBelow))
                base = t * 5.5 * (0.55 + 0.45 * E.at(x, y))
                if c < coastBelow: base = min(base, 1.0)
                if c * E.at(x, y) > mountainAbove: base = max(base, 4.5 + 2.0 * E.at(x, y))
                h[y][x] = int(min(maxH, base))

    def neighbors(x, y):
        for dx, dy in ((1,0),(-1,0),(0,1),(0,-1)):
            if 0 <= x+dx < N and 0 <= y+dy < N: yield x+dx, y+dy

    def smooth():
        changed = True
        while changed:
            changed = False
            for y in range(N):
                for x in range(N):
                    if water[y][x]: continue
                    lo = min((h[ny][nx] for nx, ny in neighbors(x, y)), default=h[y][x])
                    if h[y][x] > lo + 1:
                        h[y][x] = lo + 1; changed = True
    smooth()

    # coast distance (BFS from ocean)
    INF = 1 << 20
    dist = [[INF] * N for _ in range(N)]
    from collections import deque
    q = deque()
    for y in range(N):
        for x in range(N):
            if water[y][x] == "ocean": dist[y][x] = 0; q.append((x, y))
    while q:
        x, y = q.popleft()
        for nx, ny in neighbors(x, y):
            if dist[ny][nx] > dist[y][x] + 1:
                dist[ny][nx] = dist[y][x] + 1; q.append((nx, ny))

    # cliff coasts: where cliffiness high, raise the shore strip (deliberate cliffs)
    cliff = [[False] * N for _ in range(N)]
    for y in range(N):
        for x in range(N):
            if water[y][x] or not (1 <= dist[y][x] <= 2): continue
            cv = CL.at(x, y)
            if cv > 0.70:
                h[y][x] = max(h[y][x], 3 + (1 if cv > 0.85 else 0)); cliff[y][x] = True
            elif cv > 0.45 and dist[y][x] == 1:
                h[y][x] = max(h[y][x], 1 + (1 if cv > 0.6 else 0)); cliff[y][x] = True

    # L3 lakes: high-moisture inland depressions
    rng = random.Random(seed + 9)
    lakes = []
    for _ in range(max(2, N // 40)):
        for _try in range(60):
            x, y = rng.randrange(8, N - 8), rng.randrange(8, N - 8)
            if water[y][x] or h[y][x] > 2 or M.at(x, y) < 0.55: continue
            r = rng.randint(4, 6)
            if dist[y][x] < r + 16: continue   # lake must be FULLY inland — never touches coast aprons
            if any(abs(x-lx)+abs(y-ly) < 30 for lx, ly, _ in lakes): continue  # lakes keep their distance
            lakes.append((x, y, r))
            for yy in range(y - r, y + r + 1):
                for xx in range(x - r, x + r + 1):
                    if 0 <= xx < N and 0 <= yy < N:
                        d2 = ((xx - x) / r) ** 2 + ((yy - y) / (r * 0.75)) ** 2
                        if d2 <= 1.0: water[yy][xx] = "lake"; h[yy][xx] = 0
            break

    # L3 rivers: springs walk downhill to ocean/lake; widen downstream
    rivers = []
    for _ in range(max(1, N // 60)):
        for _try in range(80):
            x, y = rng.randrange(4, N - 4), rng.randrange(4, N - 4)
            if water[y][x] or h[y][x] < 3 or M.at(x, y) < 0.5: continue
            path, cx, cy = [], x, y
            for _step in range(N * 3):
                path.append((cx, cy))
                if water[cy][cx]: break
                opts = sorted(neighbors(cx, cy), key=lambda p: (h[p[1]][p[0]], rng.random()))
                nx, ny = opts[0]
                if h[ny][nx] > h[cy][cx]: break
                cx, cy = nx, ny
            if len(path) > 12 and water[path[-1][1]][path[-1][0]]:
                rivers.append(path)
                for i, (px, py) in enumerate(path):
                    halfw = 1 if i < len(path) * 0.5 else 2   # 3 wide -> 5 wide
                    for yy in range(py - halfw, py + halfw + 1):
                        for xx in range(px - halfw, px + halfw + 1):
                            if 0 <= xx < N and 0 <= yy < N and water[yy][xx] != "ocean":
                                water[yy][xx] = "river"; h[yy][xx] = 0
                break

    # estuary pass: river/lake water touching (or near) the ocean becomes ocean,
    # so each body keeps ONE water language (owner rule: no mixed water tiles)
    for y in range(N):
        for x in range(N):
            if water[y][x] in ("river", "lake") and dist[y][x] <= 3:
                water[y][x] = "ocean"
    smooth()  # river/lake carving re-smoothed -> walkable banks

    # biomes (region by moisture; mountain by elevation)
    biome = [[None] * N for _ in range(N)]
    for y in range(N):
        for x in range(N):
            if water[y][x]: continue
            if h[y][x] >= 4: biome[y][x] = "mountain"
            else:
                m = M.at(x, y)
                # blended transition band (spec: 4-7 cells): probabilistic mix
                if m >= 0.50: biome[y][x] = "forest"
                elif m <= 0.40: biome[y][x] = "meadow"
                else:
                    pf = (m - 0.40) / 0.10
                    biome[y][x] = "forest" if random.Random((x*73856093) ^ (y*19349663) ^ seed).random() < pf else "meadow"

    # surface tiles via surface rules
    tile = [[None] * N for _ in range(N)]
    def water_adj(x, y):
        kinds = {water[ny][nx] for nx, ny in neighbors(x, y) if water[ny][nx]}
        return kinds
    for y in range(N):
        for x in range(N):
            if water[y][x]:
                tile[y][x] = "water_deep" if water[y][x] == "ocean" else "water"; continue
            adj = water_adj(x, y)
            if cliff[y][x]: tile[y][x] = "grass_1"
            elif "river" in adj: tile[y][x] = "forest_mud_path"
            elif "lake" in adj: tile[y][x] = "dirt"
            elif "ocean" in adj or dist[y][x] <= 3: tile[y][x] = "sand_2"
            elif dist[y][x] <= 5: tile[y][x] = "dirt"
            elif biome[y][x] == "mountain":
                tile[y][x] = "stone_block" if h[y][x] >= 5 else "badlands_1"
            elif biome[y][x] == "forest": tile[y][x] = "forest_grass_base"
            else: tile[y][x] = "plains_grass_base"

    # decor: groves / lone trees / flowers / outcrops / ore / shore stones
    decor = [[None] * N for _ in range(N)]
    rng2 = random.Random(seed + 21)
    grove_centers = [(rng2.randrange(N), rng2.randrange(N)) for _ in range(max(3, int(N * N / 10000 * 1.3)))]
    for y in range(N):
        for x in range(N):
            if water[y][x]:
                land_adj = any(not water[ny][nx] for nx, ny in neighbors(x, y))
                if land_adj and rng2.random() < 0.05: decor[y][x] = "shore_stone"
                continue
            b = biome[y][x]
            if tile[y][x] in ("sand_2", "dirt", "forest_mud_path"):
                if rng2.random() < 0.03: decor[y][x] = "rock"
                continue
            if b == "forest":
                near = min(abs(x - gx) + abs(y - gy) for gx, gy in grove_centers)
                dens = 0.55 if near <= 3 else 0.15
                if rng2.random() < dens: decor[y][x] = "tree"
            elif b == "meadow":
                r = rng2.random()
                if r < 0.015: decor[y][x] = "tree"
                elif r < 0.10: decor[y][x] = "flower"
                elif r < 0.16: decor[y][x] = "tuft"
            elif b == "mountain":
                band = h[y][x]
                if rng2.random() < (0.06 + 0.05 * (band - 3)): decor[y][x] = "rock"
                elif rng2.random() < (0.01 + 0.015 * (band - 3)): decor[y][x] = "ore"

    return dict(N=N, h=h, water=water, biome=biome, tile=tile, decor=decor, cliff=cliff)

# ---------------------------------------------------------------- render + checks

COLORS = {
    "water_deep": (24, 52, 96), "water": (60, 120, 180),
    "sand_2": (214, 196, 140), "dirt": (134, 102, 66), "forest_mud_path": (110, 86, 58),
    "plains_grass_base": (110, 160, 80), "forest_grass_base": (74, 122, 62),
    "badlands_1": (150, 126, 96), "stone_block": (130, 130, 136), "grass_1": (110, 160, 80),
}
DECOR_COLORS = {"tree": (28, 70, 32), "flower": (220, 120, 160), "tuft": (150, 190, 90),
                "rock": (90, 90, 95), "ore": (220, 140, 50), "shore_stone": (180, 180, 185)}

def render(w, path, scale=4):
    N = w["N"]
    img = Image.new("RGB", (N * scale, N * scale))
    px = img.load()
    for y in range(N):
        for x in range(N):
            base = COLORS.get(w["tile"][y][x], (255, 0, 255))
            shade = 0.78 + 0.05 * w["h"][y][x]
            col = tuple(min(255, int(c * shade)) for c in base)
            if w["cliff"][y][x]:
                col = tuple(min(255, c + 18) for c in col)
            for yy in range(scale):
                for xx in range(scale):
                    px[x * scale + xx, y * scale + yy] = col
            d = w["decor"][y][x]
            if d:
                dc = DECOR_COLORS[d]
                for yy in range(max(1, scale - 2)):
                    for xx in range(max(1, scale - 2)):
                        px[x * scale + 1 + xx, y * scale + 1 + yy] = dc
    img.save(path)
    return path

def acceptance(w):
    N = w["N"]; bad_grass_water = 0; bad_delta = 0
    for y in range(N):
        for x in range(N):
            if w["water"][y][x]: continue
            grassy = w["tile"][y][x] in ("plains_grass_base", "forest_grass_base", "grass_1")
            for dx, dy in ((1,0),(-1,0),(0,1),(0,-1)):
                nx, ny = x+dx, y+dy
                if not (0 <= nx < N and 0 <= ny < N): continue
                if grassy and w["water"][ny][nx] and not w["cliff"][y][x]:
                    bad_grass_water += 1
                if not w["water"][ny][nx] and abs(w["h"][y][x]-w["h"][ny][nx]) > 1 \
                   and not (w["cliff"][y][x] or w["cliff"][ny][nx]):
                    bad_delta += 1
    print(f"ACCEPTANCE: grass-touching-water (non-cliff): {bad_grass_water}  "
          f"| illegal height jumps: {bad_delta}")
    return bad_grass_water == 0 and bad_delta == 0

if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--seed", type=int, default=4242)
    ap.add_argument("--size", type=int, default=160)
    ap.add_argument("--out", default=None)
    a = ap.parse_args()
    w = generate(a.seed, a.size)
    out = a.out or os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                f"preview_seed{a.seed}.png")
    render(w, out)
    ok = acceptance(w)
    print(("PASS" if ok else "VIOLATIONS FOUND"), "->", out)
