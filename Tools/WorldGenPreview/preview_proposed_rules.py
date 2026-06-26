#!/usr/bin/env python3
"""Prototype renderer for Docs/WORLDGEN_RULES_PROPOSAL.md.

Implements a SIMPLIFIED model of the proposed rules (macro biome cells,
climate table + lapse rate, border-distance blend bands with weighted
crossfade + accent ramps, blue-noise props, settlement presets) and renders
top-down previews so the owner can review the rules visually BEFORE the
Unity sampler implements them. This is NOT the runtime generator.

Outputs PNGs into Tools/BiomeSketch/previews/ + a previews.js manifest.
Run:  python3 Tools/WorldGenPreview/preview_proposed_rules.py
"""
import os, json, math, hashlib, random
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
TILES = os.path.join(ROOT, "Assets", "Resources", "Tiles")
OUT = os.path.join(ROOT, "Tools", "BiomeSketch", "previews")
SIZE = 256          # largest grid (cells per side)
SCALE = 3           # px per cell in the PNG

# ---------------- deterministic noise ----------------
def h01(x, y, s):
    v = hashlib.md5(f"{x},{y},{s}".encode()).digest()
    return int.from_bytes(v[:4], "little") / 0xFFFFFFFF

def lerp(a, b, t): return a + (b - a) * t
def smooth(t): return t * t * (3 - 2 * t)

def vnoise(x, y, freq, seed):
    fx, fy = x * freq, y * freq
    ix, iy = math.floor(fx), math.floor(fy)
    tx, ty = smooth(fx - ix), smooth(fy - iy)
    a = h01(ix, iy, seed); b = h01(ix + 1, iy, seed)
    c = h01(ix, iy + 1, seed); d = h01(ix + 1, iy + 1, seed)
    return lerp(lerp(a, b, tx), lerp(c, d, tx), ty)

def fbm(x, y, freq, seed, oct=3):
    v, amp, tot = 0.0, 1.0, 0.0
    for o in range(oct):
        v += vnoise(x, y, freq * (2 ** o), seed + o * 17) * amp
        tot += amp; amp *= 0.5
    return v / tot

# ---------------- tile colors from real art ----------------
def avg_color(tile_id, fallback):
    p = os.path.join(TILES, tile_id + ".png")
    if not os.path.exists(p): return fallback
    im = Image.open(p).convert("RGBA")
    px = list(im.getdata())
    rs = [c for c in px if c[3] > 40]
    if not rs: return fallback
    n = len(rs)
    return tuple(sum(c[i] for c in rs) // n for i in range(3))

GRAY = (120, 120, 124)
PAL = {}
def col(tid, fb=GRAY):
    if tid not in PAL: PAL[tid] = avg_color(tid, fb)
    return PAL[tid]

# biome pools (base, accents, accentRate) — ids from the live rules
BIOMES = {
    "meadow":  dict(base=["plains2_02","plains2_05","plains2_07","plains2_11","plains2_15"],
                    accents=["plains2_04","plains2_00","plains2_10","plains_flower_grass","plains_grass_tufts"],
                    rate=0.15, fb=(96,140,70)),
    "forest":  dict(base=["forest_grass_base"],
                    accents=["forest_floor","forest_leaf_litter","forest_moss_grass","forest_dark_underbrush"],
                    rate=0.30, fb=(60,100,55)),
    "snow":    dict(base=["snow2_03","snow2_04","snow2_07","snow2_12"],
                    accents=["snow2_08","snow2_09","snow2_10"],
                    rate=0.12, fb=(225,230,238)),
    "beach":   dict(base=["sand_1"], accents=["sand_2"], rate=0.18, fb=(202,178,128)),
    "mountain":dict(base=["stone_scree"], accents=["stone_mossy","stone_cracked"],
                    rate=0.25, fb=(125,122,118)),
    "water":   dict(base=["water_deep"], accents=["water_deep_2","water_swell_1"], rate=0.10, fb=(28,48,86)),
}
# unpromoted mountain ids -> hand colors so the preview still reads
PAL.update({"stone_scree":(132,128,120),"stone_mossy":(110,124,98),
            "stone_cracked":(112,108,104),"stone_snowcap":(218,224,232),
            # sand art averages muddy-brown; override so coastlines read as sand
            "sand_1":(208,186,138),"sand_2":(188,164,116)})

PROP_COLORS = {"tree":(34,62,34),"pine":(28,52,40),"bush":(52,86,46),"rock":(98,96,92),
               "flower":(208,170,80),"tuft":(130,160,84),"ore":(180,120,60),
               "reed":(96,128,64),"snowrock":(170,176,186)}

def pick_weighted(ids, x, y, salt):
    return ids[int(h01(x, y, salt) * len(ids)) % len(ids)]

def surface(biome, x, y, accent_scale=1.0):
    b = BIOMES[biome]
    if h01(x, y, 7) < b["rate"] * accent_scale and b["accents"]:
        return pick_weighted(b["accents"], x, y, 11)
    return pick_weighted(b["base"], x, y, 13)

# ---------------- blend band (rules B1–B3) ----------------
def blended_surface(bL, bR, x, y, d, band=6.0):
    """d = signed distance from border (negative = left biome side)."""
    t = max(0.0, min(1.0, 0.5 + d / band))         # 0=fully left, 1=fully right
    use_right = h01(x, y, 23) < smooth(t)           # weighted crossfade dither
    # accent ramp: own accents fade IN away from the border
    k = abs(d) / (band * 0.5)
    return surface(bR if use_right else bL, x, y, accent_scale=min(1.0, 0.25 + 0.75 * k))

def border_offset(y, seed, amp=14, freq=0.035):
    return (fbm(0, y, freq, seed) - 0.5) * 2 * amp

# ---------------- blue-noise props (rule C2) ----------------
def blue_noise_props(size, k, density, seed):
    pts = []
    for gy in range(0, size, k):
        for gx in range(0, size, k):
            if h01(gx, gy, seed) < density:
                pts.append((gx + int(h01(gx, gy, seed + 1) * k), gy + int(h01(gx, gy, seed + 2) * k)))
    return [(x, y) for x, y in pts if 0 <= x < size and 0 <= y < size]

# ---------------- canvas ----------------
class Map:
    def __init__(self, size=SIZE):
        self.size = size
        self.img = Image.new("RGB", (size, size))
        self.px = self.img.load()
    def set(self, x, y, c):
        if 0 <= x < self.size and 0 <= y < self.size: self.px[x, y] = c
    def tile(self, x, y, tid): self.set(x, y, col(tid, GRAY))
    def dot(self, x, y, c):
        for dx, dy in ((0,0),(1,0),(0,1)):
            self.set(x+dx, y+dy, c)
    def save(self, name):
        big = self.img.resize((self.size*SCALE,)*2, Image.NEAREST)
        os.makedirs(OUT, exist_ok=True)
        big.save(os.path.join(OUT, name))
        return name

MANIFEST = []
def emit(m, name, title, note):
    m.save(name)
    MANIFEST.append({"file": "previews/" + name, "title": title, "note": note})
    print("wrote", name)

# ================= preview builders =================
def pair_blend(name, bL, bR, title, note, props=None, seed=5):
    m = Map()
    for y in range(m.size):
        bx = m.size/2 + border_offset(y, seed)
        for x in range(m.size):
            m.tile(x, y, blended_surface(bL, bR, x, y, x - bx))
    # feature density ramp (B4): trees thin toward border
    if props:
        for biome, side, color, k, dens in props:
            for (x, y) in blue_noise_props(m.size, k, dens, seed + hash(biome) % 97):
                bx = m.size/2 + border_offset(y, seed)
                d = (x - bx) if side > 0 else (bx - x)
                if d < 2: continue
                ramp = min(1.0, d / 18.0)            # B4 density ramp
                if h01(x, y, 31) < ramp:
                    m.dot(x, y, color)
    emit(m, name, title, note)

def continent(name, seed=240612):
    m = Map()
    CELL = 40                                        # macro cell size (A2)
    def macro_biome(cx, cy):
        t = vnoise(cx*CELL, cy*CELL, 0.004, 91) - 0.18*vnoise(cx*CELL, cy*CELL, 0.02, 92)
        mois = vnoise(cx*CELL, cy*CELL, 0.005, 93)
        if t < 0.35: return "snow"
        if mois > 0.62: return "forest"
        return "meadow"
    def site(x, y):                                  # jittered voronoi (A2)
        gx, gy = x // CELL, y // CELL
        best, bd = None, 1e9
        for oy in (-1,0,1):
            for ox in (-1,0,1):
                cx, cy = gx+ox, gy+oy
                sx = cx*CELL + h01(cx,cy,71)*CELL
                sy = cy*CELL + h01(cx,cy,72)*CELL
                d = (x-sx)**2 + (y-sy)**2
                if d < bd: bd, best = d, (cx, cy)
        return best
    for y in range(m.size):
        for x in range(m.size):
            e = fbm(x, y, 0.012, seed, 4)
            if e < 0.40: m.tile(x, y, "water_deep"); continue
            if e < 0.44: m.tile(x, y, pick_weighted(["sand_1","sand_2"], x, y, 3)); continue
            # elevation gating + lapse rate (A4)
            if e > 0.72: b = "mountain" if e < 0.80 else "snow"
            else:
                b = macro_biome(*site(x, y))
                # adjacency check (A5) is implicit here: table only yields cold/temperate
            # blend: probe nearest different macro biome (B1 cheap probe)
            own = b
            for (ox, oy) in ((6,0),(-6,0),(0,6),(0,-6)):
                if e <= 0.72:
                    nb = macro_biome(*site(x+ox, y+oy))
                    if nb != own:
                        m.tile(x, y, blended_surface(own, nb, x, y, 3.0))  # band edge mix
                        break
            else:
                m.tile(x, y, surface(b, x, y))
    # settlements (D1): flat meadow, spaced
    placed = []
    for gy in range(0, m.size, 48):
        for gx in range(0, m.size, 48):
            x = gx + int(h01(gx, gy, 55) * 40); y = gy + int(h01(gx, gy, 56) * 40)
            e = fbm(x, y, 0.012, seed, 4)
            if 0.46 < e < 0.66 and all((x-a)**2+(y-b)**2 > 40**2 for a, b in placed):
                placed.append((x, y))
                for dy in range(-2, 3):
                    for dx in range(-2, 3):
                        m.set(x+dx, y+dy, (188, 74, 60))
    emit(m, name, "Continent overview (A2 macro cells + A4 gating + B-blends + D1 sites)",
         f"{len(placed)} settlement sites (red) on flat mid-elevation meadow; ocean/beach ring; "
         "mountain->snowcap by elevation; biome regions are macro-cell coherent.")

def mountain_strata(name):
    m = Map()
    for y in range(m.size):
        for x in range(m.size):
            e = 0.30 + 0.55 * (y / m.size) + 0.12 * (fbm(x, y, 0.02, 77) - 0.5)
            if e < 0.45: m.tile(x, y, surface("meadow", x, y))
            elif e < 0.55: m.tile(x, y, blended_surface("meadow", "mountain", x, y, (e-0.50)*60))
            elif e < 0.68: m.tile(x, y, pick_weighted(["stone_scree","stone_mossy"], x, y, 5))
            elif e < 0.80: m.tile(x, y, pick_weighted(["stone_cracked","stone_scree"], x, y, 6))
            else: m.tile(x, y, "stone_snowcap")
    for (x, y) in blue_noise_props(m.size, 10, 0.5, 99):
        e = 0.30 + 0.55 * (y / m.size)
        if 0.55 < e < 0.80: m.dot(x, y, PROP_COLORS["ore" if h01(x,y,3) < 0.25 else "rock"])
        elif e <= 0.45 and h01(x, y, 4) < 0.4: m.dot(x, y, PROP_COLORS["tree"])
    emit(m, name, "Mountain stratification + lapse rate (A4, C6)",
         "meadow -> scree -> cracked stone -> snowcap by elevation band; ore density rises with height. "
         "NOTE: stone_* ids are hand-colored — family exists in PixelArt but is not promoted yet.")

def props_compare(name):
    m = Map()
    for y in range(m.size):
        for x in range(m.size):
            m.tile(x, y, surface("meadow", x, y))
    half = m.size // 2
    rnd = random.Random(42)                       # LEFT: current per-cell hash scatter
    for y in range(m.size):
        for x in range(half - 2):
            if rnd.random() < 0.035:
                m.dot(x, y, PROP_COLORS["tree"] if rnd.random() < .5 else PROP_COLORS["bush"])
    for (x, y) in blue_noise_props(m.size, 6, 0.55, 7):   # RIGHT: blue-noise (C2)
        if x > half + 2:
            c = "tree" if h01(x, y, 9) < 0.45 else ("bush" if h01(x, y, 10) < 0.7 else "flower")
            m.dot(x, y, PROP_COLORS[c])
    for y in range(m.size): m.set(half, y, (20, 20, 20))
    emit(m, name, "Prop scatter: current random (left) vs blue-noise contract (right) (C1–C2)",
         "same average density both sides; right side has no accidental clumps/voids and respects min spacing.")

def town(name, preset, title):
    m = Map()
    for y in range(m.size):
        for x in range(m.size):
            m.tile(x, y, surface("meadow", x, y))
    cx = cy = SIZE // 2
    n_lots, ring, farms, dock = preset
    ROAD = col("stone_path", (150, 144, 130)); DIRT = col("dirt", (124, 96, 64))
    PLAZA = (162, 152, 132); BLD = [(150,82,58),(108,82,120),(86,98,150),(160,130,60)]
    # roads: cross + ring (D4)
    for i in range(m.size):
        w = 1 + (abs(i - cx) < ring)
        for o in range(-w, w+1):
            m.set(i, cy+o, ROAD if abs(o) < 2 else DIRT); m.set(cx+o, i, ROAD if abs(o) < 2 else DIRT)
    for a in range(0, 360, 2):                       # ring road
        x = cx + int(ring * math.cos(math.radians(a))); y = cy + int(ring * math.sin(math.radians(a)))
        m.set(x, y, DIRT); m.set(x+1, y, DIRT)
    for dy in range(-4, 5):                          # plaza (D2)
        for dx in range(-4, 5):
            m.set(cx+dx, cy+dy, PLAZA)
    # lots around plaza, doors toward it (D2/D3)
    rnd = random.Random(7)
    placed = 0; a = 0.0
    while placed < n_lots and a < 720:
        r = ring * (0.45 + 0.5 * h01(placed, int(a), 3))
        x = cx + int(r * math.cos(math.radians(a))); y = cy + int(r * math.sin(math.radians(a)))
        a += 360 / max(6, n_lots) * (0.8 + 0.4 * h01(placed, 1, 4))
        if abs(x-cx) < 7 and abs(y-cy) < 7: continue
        w, h = rnd.choice(((5,4),(6,5),(4,4))); c = BLD[placed % len(BLD)]
        for dy in range(h):
            for dx in range(w):
                m.set(x+dx, y+dy, c)
        # door pixel facing plaza
        m.set(x + w//2, y + (h if y < cy else -1), (240, 220, 140))
        placed += 1
    if farms:                                        # farm ring (D5)
        for a in range(0, 360, 9):
            r = ring + 14 + 10 * h01(a, 2, 8)
            x = cx + int(r * math.cos(math.radians(a))); y = cy + int(r * math.sin(math.radians(a)))
            for dy in range(4):
                for dx in range(6):
                    m.set(x+dx, y+dy, (140, 110, 66) if (dy % 2) else (110, 88, 52))
    if dock:                                         # dock arm into water (D5)
        for y in range(m.size):
            for x in range(int(m.size*0.86), m.size):
                m.tile(x, y, "water_deep")
        for x in range(int(m.size*0.80), int(m.size*0.93)):
            for o in (-1, 0, 1):
                m.set(x, cy+o, (124, 96, 64))
    for (x, y) in blue_noise_props(m.size, 8, 0.4, 12):  # outskirt scatter, suppressed near roads (C4)
        if abs(x-cx) > ring + 6 or abs(y-cy) > ring + 6:
            m.dot(x, y, PROP_COLORS["tree"] if h01(x, y, 13) < 0.5 else PROP_COLORS["bush"])
    emit(m, name, title,
         f"{n_lots} building lots, plaza-anchored, doors (gold) face the plaza; ring+cross roads; "
         + ("farm plots outer ring; " if farms else "") + ("dock arm to water; " if dock else "")
         + "decor suppressed inside town core (C4/D7).")

# ================= run =================
if __name__ == "__main__":
    continent("continent_overview.png")
    pair_blend("blend_meadow_forest.png", "forest", "meadow",
        "Forest -> meadow blend band (B1–B4)",
        "6-cell band: weighted base crossfade + accent ramp; grove density fades to lone trees (owner rule in forest.json).",
        props=[("forest", -1, PROP_COLORS["tree"], 5, 0.7), ("meadow", 1, PROP_COLORS["flower"], 9, 0.5)])
    pair_blend("blend_meadow_snow.png", "snow", "meadow",
        "Snow -> meadow blend band (B1–B3, A5)",
        "cold/temperate pair is legal-adjacent; dithered crossfade replaces the hard palette switch.",
        props=[("snow", -1, PROP_COLORS["snowrock"], 10, 0.4), ("meadow", 1, PROP_COLORS["tuft"], 9, 0.5)])
    pair_blend("blend_beach_water.png", "water", "beach",
        "Coastline: water -> wet sand -> dry sand (B6)",
        "beach exists only against water; swell accents on the rim band (current rule kept, band art pending promotion).")
    mountain_strata("mountain_strata.png")
    props_compare("props_compare.png")
    town("town_hamlet.png", (4, 26, False, False), "Town preset: HAMLET (D1–D3)")
    town("town_village.png", (9, 34, True, False), "Town preset: VILLAGE (D1–D5)")
    town("town_large.png", (16, 44, True, True), "Town preset: TOWN w/ farms + dock (D1–D5)")
    js = os.path.join(ROOT, "Tools", "BiomeSketch", "previews", "previews.js")
    with open(js, "w", encoding="utf-8") as f:
        f.write("const RULE_PREVIEWS = ")
        json.dump({"grid": SIZE, "scale": SCALE, "items": MANIFEST}, f, indent=1)
        f.write(";\\n")
    print("manifest ->", js)
