#!/usr/bin/env python3
"""Close-up isometric composites of the REAL tile/prop art, per biome and
across biome blends, so the owner can judge how the proposed worldgen rules
look with the actual sprites (not 1-px color maps).

Reuses the biome pools / blend band / blue-noise logic from
preview_proposed_rules.py and composites Assets/Resources art:

  Tiles/<id>.png        32x32 iso tiles, game pivot (0.5, 0.75), PPU 32.
                        screenX=(x-y)*16, screenY=(x+y)*8, painter order x+y.
                        Art families park the diamond at different y offsets,
                        so we detect the diamond's widest row per tile from
                        the alpha channel and normalize every family to one
                        anchor (otherwise sand/water/dungeon sit 5-9 px off
                        and seams open at family borders).
  Decorations/<id>.png  mostly 128x128 @ 100 PPU -> scaled by 32/100 (~41px),
                        anchored bottom-center of opaque bbox on the cell's
                        diamond center, drawn in a second back-to-front pass.

Outputs closeup_*.png (48x48 cells, 2x nearest upscale) into
Tools/BiomeSketch/previews/ plus closeups.json (manifest consumed by
build_previews_page.py).  Run:
    python3 Tools/WorldGenPreview/render_closeups.py
    python3 Tools/WorldGenPreview/build_previews_page.py
"""
import os, sys, json, math, re
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import preview_proposed_rules as rules           # noise + pools + blend logic

ROOT  = os.path.abspath(os.path.join(HERE, "..", ".."))
TILES = os.path.join(ROOT, "Assets", "Resources", "Tiles")
DECOR = os.path.join(ROOT, "Assets", "Resources", "Decorations")
OUT   = os.path.join(ROOT, "Tools", "BiomeSketch", "previews")

SIZE = 48                  # cells per side
HW, HH = 16, 8             # half tile width/height in px (cellSize 1 x 0.5)
OX, OY = SIZE * HW, 56     # canvas origin for cell (0,0) diamond center
W, H = SIZE * 32, (SIZE * 2 - 1) * HH + OY + 24
BG = (20, 23, 28)
VOID = (10, 12, 16)        # planned dungeon void color (#0a0c10)

h01, smooth, fbm = rules.h01, rules.smooth, rules.fbm
pick = rules.pick_weighted
border_offset, blue_noise_props = rules.border_offset, rules.blue_noise_props

# Biome pools from the proposal preview; mountain stone_* has no promoted art
# -> substitute stone_block / badlands_1 / badlands_2 (noted in the caption).
BIOMES = {k: dict(v) for k, v in rules.BIOMES.items()}
BIOMES["mountain"] = dict(base=["badlands_1", "badlands_2"],
                          accents=["stone_block"], rate=0.22)

def surface(biome, x, y, accent_scale=1.0):
    b = BIOMES[biome]
    if h01(x, y, 7) < b["rate"] * accent_scale and b["accents"]:
        return pick(b["accents"], x, y, 11)
    return pick(b["base"], x, y, 13)

def blended_surface(bL, bR, x, y, d, band=6.0):
    t = max(0.0, min(1.0, 0.5 + d / band))
    use_right = h01(x, y, 23) < smooth(t)
    k = abs(d) / (band * 0.5)
    return surface(bR if use_right else bL, x, y,
                   accent_scale=min(1.0, 0.25 + 0.75 * k))

# ---------------- art loading / anchoring ----------------
_tile_cache = {}
def tile(tid):
    """-> (RGBA image, anchor_row) where anchor_row is the image row that
    must land on the cell's diamond center (widest alpha row)."""
    if tid in _tile_cache: return _tile_cache[tid]
    im = Image.open(os.path.join(TILES, tid + ".png")).convert("RGBA")
    a = im.getchannel("A").load()
    widths = []
    for y in range(im.height):
        xs = [x for x in range(im.width) if a[x, y] > 40]
        widths.append((max(xs) - min(xs) + 1) if xs else 0)
    full = [y for y, w in enumerate(widths) if w >= im.width - 1]
    anchor = full[0] if full else widths.index(max(widths))
    _tile_cache[tid] = (im, anchor)
    return _tile_cache[tid]

_prop_cache = {}
def prop(pid):
    """-> (scaled RGBA, ax, ay): paste at (cx-ax, cy-ay) to stand the prop's
    opaque bottom-center on the diamond center."""
    if pid in _prop_cache: return _prop_cache[pid]
    p = os.path.join(DECOR, pid + ".png")
    if not os.path.exists(p):
        _prop_cache[pid] = None
        return None
    im = Image.open(p).convert("RGBA")
    ppu = 100
    meta = p + ".meta"
    if os.path.exists(meta):
        m = re.search(r"spritePixelsToUnits:\s*(\d+)", open(meta).read())
        if m: ppu = int(m.group(1))
    s = 32.0 / ppu
    if abs(s - 1.0) > 0.01:
        im = im.resize((max(1, round(im.width * s)),
                        max(1, round(im.height * s))), Image.NEAREST)
    bb = im.getchannel("A").getbbox() or (0, 0, im.width, im.height)
    ax, ay = (bb[0] + bb[2]) // 2, bb[3]      # bottom-center of opaque pixels
    _prop_cache[pid] = (im, ax, ay)
    return _prop_cache[pid]

def have(pid): return os.path.exists(os.path.join(DECOR, pid + ".png"))

class Iso:
    def __init__(self, bg=BG):
        self.img = Image.new("RGB", (W, H), bg)
        self.ground = {}        # (x,y) -> [tile ids bottom..top]
        self.props = {}         # (x,y) -> prop id
    def center(self, x, y):
        return (x - y) * HW + OX, (x + y) * HH + OY
    def render(self):
        for s in range(2 * SIZE - 1):                 # ground, back to front
            for x in range(max(0, s - SIZE + 1), min(SIZE, s + 1)):
                y = s - x
                for tid in self.ground.get((x, y), ()):
                    im, anchor = tile(tid)
                    cx, cy = self.center(x, y)
                    self.img.paste(im, (cx - 16, cy - anchor), im)
        for s in range(2 * SIZE - 1):                 # props, back to front
            for x in range(max(0, s - SIZE + 1), min(SIZE, s + 1)):
                y = s - x
                pid = self.props.get((x, y))
                if not pid: continue
                pr = prop(pid)
                if not pr:
                    print("  !! missing prop art:", pid); continue
                im, ax, ay = pr
                cx, cy = self.center(x, y)
                self.img.paste(im, (cx - ax, cy + 4 - ay), im)
        return self.img
    def save(self, name):
        im = self.render()
        im = im.resize((im.width * 2, im.height * 2), Image.NEAREST)
        os.makedirs(OUT, exist_ok=True)
        im.save(os.path.join(OUT, name))
        print("wrote", name, im.size)

def scatter(c, specs, mask=None, keep=None):
    """specs: (ids, grid_k, density, seed). mask(x,y)->bool gates placement,
    keep(x,y)->0..1 extra probability (density ramps near blend borders)."""
    for ids, k, dens, seed in specs:
        ids = [i for i in ids if have(i)]
        if not ids: continue
        for (x, y) in blue_noise_props(SIZE, k, dens, seed):
            if (x, y) in c.props: continue
            if mask and not mask(x, y): continue
            if keep and h01(x, y, 31) >= keep(x, y): continue
            c.props[(x, y)] = pick(ids, x, y, seed + 5)

MANIFEST = []
def emit(c, name, title, note):
    c.save(name)
    MANIFEST.append({"file": "previews/" + name, "title": title, "note": note})

MEADOW_TREES  = ["plains_tree_v2_0", "plains_tree_v2_1", "plains_tree_v2_3",
                 "plains_tree_v2_0_young", "plains_tree_v2_1_young"]
MEADOW_BUSHES = ["plains_bush_v2_0", "plains_bush_v2_1", "plains_bush_v2_2",
                 "plains_bush_v2_3"]
MEADOW_ROCKS  = ["plains_rock_v2_0", "plains_rock_v2_1", "plains_rock_v2_2",
                 "plains_rock_v2_3"]
FLOWERS       = ["flower", "flower_tulip", "tuft"]
FOREST_TREES  = ["forest_pine", "forest_oak_tree", "forest_deep_oak_tree"]
FOREST_SMALL  = ["forest_bush", "forest_moss_bush", "forest_mushrooms",
                 "forest_stump"]

# ================= renders =================
def r_meadow():
    c = Iso()
    for y in range(SIZE):
        for x in range(SIZE):
            c.ground[(x, y)] = [surface("meadow", x, y)]
    scatter(c, [(MEADOW_TREES, 9, 0.55, 101), (MEADOW_BUSHES, 6, 0.40, 102),
                (MEADOW_ROCKS, 11, 0.45, 103), (FLOWERS, 5, 0.40, 104)])
    emit(c, "closeup_meadow.png", "CLOSE-UP: meadow surface + blue-noise props",
         "Real art, 48x48 cells. plains2 base/accent pool (rate 0.15) with "
         "plains_flower_grass/tufts accents; trees/bushes/rocks/flowers placed "
         "by the C2 blue-noise contract (no clumps, min spacing).")

def r_forest():
    c = Iso()
    canopy = {}
    for y in range(SIZE):
        for x in range(SIZE):
            c.ground[(x, y)] = [surface("forest", x, y)]
            if fbm(x, y, 0.10, 411, 3) > 0.60:        # canopy patches
                c.ground[(x, y)].append(pick(["canopy_1", "canopy_2", "canopy_3"], x, y, 41))
                canopy[(x, y)] = True
    scatter(c, [(FOREST_TREES, 6, 0.7, 201), (FOREST_SMALL, 5, 0.5, 202)],
            mask=lambda x, y: (x, y) not in canopy)
    emit(c, "closeup_forest.png", "CLOSE-UP: forest floor + canopy patches + props",
         "forest_grass_base with floor/leaf-litter/moss/underbrush accents "
         "(rate 0.30); canopy_1-3 tile patches form dense groves; "
         "forest_pine / oak / deep-oak trees plus bushes, mushrooms and stumps "
         "between them.")

def r_blend(name, bL, bR, title, note, propL, propR, seed=5):
    c = Iso()
    def bx(y): return SIZE / 2 + border_offset(y, seed, amp=8)
    for y in range(SIZE):
        for x in range(SIZE):
            c.ground[(x, y)] = [blended_surface(bL, bR, x, y, x - bx(y))]
    def ramp(side):
        def keep(x, y):
            d = (x - bx(y)) * side
            return 0.0 if d < 1 else min(1.0, d / 10.0)   # B4 density ramp
        return keep
    scatter(c, propR, keep=ramp(+1))
    scatter(c, propL, keep=ramp(-1))
    emit(c, name, title, note)

def r_coast():
    c = Iso()
    def b1(y): return 15 + border_offset(y, 31, amp=5)    # water -> sand
    def b2(y): return 29 + border_offset(y, 32, amp=5)    # sand -> meadow
    for y in range(SIZE):
        for x in range(SIZE):
            d1, d2 = x - b1(y), x - b2(y)
            if d1 < 0:
                g = ["water_deep"]
                if h01(x, y, 61) < 0.10:
                    g = [pick(["water_deep_2", "water_deep_3"], x, y, 62)]
                if -4 < d1 and h01(x, y, 63) < 0.45:      # swell rim band
                    g.append(pick(["water_swell_1", "water_swell_2"], x, y, 64))
                c.ground[(x, y)] = g
            elif d2 < -1.5:
                c.ground[(x, y)] = [pick(["sand_1", "sand_1", "sand_2"], x, y, 65)]
            else:
                c.ground[(x, y)] = [blended_surface("beach", "meadow", x, y, d2, band=4.0)]
    scatter(c, [(["shore_stone"], 6, 0.35, 301)],
            mask=lambda x, y: 1 < x - b1(y) and x - b2(y) < -2)
    scatter(c, [(FLOWERS, 5, 0.4, 302), (MEADOW_BUSHES, 7, 0.4, 303)],
            mask=lambda x, y: x - b2(y) > 2)
    emit(c, "closeup_coast.png", "CLOSE-UP: coastline water -> sand -> meadow (B6)",
         "water_deep with deep-variant accents, water_swell_1/2 overlays on the "
         "4-cell rim band, sand_1/sand_2 beach, dithered sand->meadow blend; "
         "shore stones on wet sand. NOTE: water art sits ~7px lower than land "
         "in-game (sunken-water look); normalized here so the bands read cleanly.")

def r_town():
    c = Iso()
    cx = cy = SIZE // 2
    def on_road(x, y):  return abs(x - cx) <= 1 or abs(y - cy) <= 1
    def shoulder(x, y): return abs(x - cx) == 2 or abs(y - cy) == 2
    def plaza(x, y):    return abs(x - cx) <= 4 and abs(y - cy) <= 4
    for y in range(SIZE):
        for x in range(SIZE):
            if plaza(x, y) or on_road(x, y):
                c.ground[(x, y)] = ["dirt", "stone_path"]   # path overlays dirt
            elif shoulder(x, y) and not plaza(x, y):
                c.ground[(x, y)] = ["dirt"]
            else:
                c.ground[(x, y)] = [surface("meadow", x, y)]
    buildings = [((cx - 6, cy - 6), "tavern_r1"), ((cx + 6, cy - 6), "shop_r1"),
                 ((cx - 6, cy + 6), "guild_hall_r1"), ((cx + 6, cy + 6), "library_r1")]
    stalls = [((cx - 3, cy + 6), "market_stall_red"), ((cx + 6, cy - 3), "market_stall_blue")]
    lamps  = [((cx - 4, cy - 4), "lantern_post"), ((cx + 4, cy - 4), "lantern_post"),
              ((cx - 4, cy + 4), "lantern_post"), ((cx + 4, cy + 4), "lantern_post")]
    extras = [((cx + 2, cy - 6), "guild_notice_board"), ((cx - 6, cy + 2), "campfire_new")]
    for (p, pid) in buildings + stalls + lamps + extras:
        if not have(pid):
            print("  !! town prop missing:", pid); continue
        c.props[p] = pid
        if pid.endswith("_r1"):                       # dirt pad under buildings
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    q = (p[0] + dx, p[1] + dy)
                    if not on_road(*q) and not plaza(*q): c.ground[q] = ["dirt"]
    scatter(c, [(MEADOW_TREES, 8, 0.5, 501), (MEADOW_BUSHES, 6, 0.35, 502),
                (FLOWERS, 5, 0.35, 503)],
            mask=lambda x, y: max(abs(x - cx), abs(y - cy)) > 10
                              and not on_road(x, y) and not shoulder(x, y))
    emit(c, "closeup_town_core.png", "CLOSE-UP: town core (D2-D4) with real buildings",
         "stone_path cross roads on dirt with dirt shoulders, 9x9 plaza; "
         "tavern/shop/guild-hall/library r1 sprites on dirt pads, market stalls "
         "and lantern posts on the plaza rim, notice board + campfire; decor "
         "suppressed inside the core (C4/D7), meadow scatter outside.")

def r_dungeon():
    c = Iso(bg=VOID)
    cx = cy = SIZE // 2
    def inside(x, y):
        if not (0 <= x < SIZE and 0 <= y < SIZE): return False
        r = math.hypot(x - cx, y - cy)
        return fbm(x, y, 0.07, 909, 3) * 0.55 + (1 - (r / 26.0) ** 1.7) * 0.6 > 0.55
    floor = {(x, y) for y in range(SIZE) for x in range(SIZE) if inside(x, y)}
    for (x, y) in floor:
        rim = any((x + dx, y + dy) not in floor
                  for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        if rim:
            t = pick(["dungeon2_00", "dungeon2_01", "dungeon2_10", "dungeon2_13"], x, y, 71)
        elif h01(x, y, 72) < 0.05:
            t = pick(["dungeon2_05", "dungeon2_14"], x, y, 73)      # lava cracks
        elif h01(x, y, 74) < 0.22:
            t = pick(["dungeon2_08", "dungeon2_15", "dungeon2_02"], x, y, 75)
        else:
            t = pick(["dungeon2_03", "dungeon2_06", "dungeon2_11", "dungeon2_12"], x, y, 76)
        c.ground[(x, y)] = [t]
    scatter(c, [(["brazier", "torch_standing"], 9, 0.5, 601),
                (["dungeon_chest_wood", "candle_lantern", "book_stack"], 11, 0.4, 602)],
            mask=lambda x, y: (x, y) in floor and not any(
                (x + dx, y + dy) not in floor
                for dx in (-1, 0, 1) for dy in (-1, 0, 1)))
    emit(c, "closeup_dungeon.png", "CLOSE-UP: dungeon island on the void (planned rework)",
         "Irregular walkable island of dungeon2_* floors on a pure #0a0c10 void; "
         "dark block tiles (dungeon2_00/01/10/13) form the rim, mossy/rune "
         "accents and rare lava cracks inside; braziers, torches, chests and "
         "lanterns kept off the edge.")

def r_mountain():
    c = Iso()
    for y in range(SIZE):
        for x in range(SIZE):
            e = (y / SIZE) + 0.16 * (fbm(x, y, 0.06, 808, 3) - 0.5)
            if e < 0.34:   c.ground[(x, y)] = [surface("meadow", x, y)]
            elif e < 0.55: c.ground[(x, y)] = [blended_surface("meadow", "mountain", x, y, (e - 0.45) * 28)]
            else:          c.ground[(x, y)] = [surface("mountain", x, y, accent_scale=1.2)]
    scatter(c, [(["ore_copper", "ore_iron", "shared_gray_rock"], 8, 0.5, 701)],
            mask=lambda x, y: y / SIZE > 0.58)
    scatter(c, [(MEADOW_TREES, 9, 0.5, 702), (FLOWERS, 6, 0.35, 703)],
            mask=lambda x, y: y / SIZE < 0.28)
    emit(c, "closeup_mountain.png", "CLOSE-UP: meadow -> mountain strata",
         "stone_scree/stone_mossy/stone_cracked/stone_snowcap are now promoted "
         "to Resources/Tiles (mountain_stone tile_0/1/6/15 respectively, "
         "WORLDGEN task #19) and resolve to real art instead of fallback. "
         "Judge the blend/strata shape and ore scatter as well as the rock "
         "palette.")

if __name__ == "__main__":
    r_meadow()
    r_forest()
    r_blend("closeup_blend_forest_meadow.png", "forest", "meadow",
            "CLOSE-UP: forest -> meadow blend band (B1-B4)",
            "6-cell dithered crossfade of the real base/accent pools; tree "
            "density ramps down to lone trees at the border, meadow flowers "
            "ramp up on the other side.",
            propL=[(FOREST_TREES, 6, 0.7, 211), (FOREST_SMALL, 5, 0.45, 212)],
            propR=[(MEADOW_TREES, 10, 0.4, 213), (FLOWERS, 5, 0.45, 214),
                   (MEADOW_BUSHES, 7, 0.4, 215)])
    r_blend("closeup_blend_snow_meadow.png", "snow", "meadow",
            "CLOSE-UP: snow -> meadow blend band (B1-B3, A5)",
            "legal cold/temperate adjacency; snow2 pool dither-fades into the "
            "plains2 pool instead of a hard palette switch; gray rocks on the "
            "snow side, tufts/bushes on the meadow side.",
            propL=[(["shared_gray_rock", "rock"], 8, 0.45, 221),
                   (["forest_dead_tree"], 12, 0.35, 222)],
            propR=[(MEADOW_TREES, 10, 0.4, 223), (FLOWERS, 5, 0.45, 224)],
            seed=9)
    r_coast()
    r_town()
    r_dungeon()
    r_mountain()
    with open(os.path.join(OUT, "closeups.json"), "w", encoding="utf-8") as f:
        json.dump(MANIFEST, f, indent=1)
    print("manifest ->", os.path.join(OUT, "closeups.json"))
