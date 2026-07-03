#!/usr/bin/env python3
"""World-scale audit vs the player (playtest 2026-07-02 task #7).

Owner rule: scale EVERYTHING off the player = 6 ft = ~1.1 world units.

Dumps a "units tall vs player" table for every entity that renders in world:
  - resource nodes / placeables  -> heightUnits parsed from FoundationContent.cs
                                    (rendered via FoundationSpriteScale.NormalizeHeight)
  - mobs                         -> rendered = art contentHeight(px) / meta PPU
                                    (mobs are NOT NormalizeHeight'd; PPU is the lever)
  - raw decoration sprites       -> natural size contentH/PPU (informational)

Flags anything outside its owner-approved target band:
  canopy tree 2.8-3.8u | young tree/sapling ~1.65u | bush 0.55-0.9u
  predator plant 1.1-1.2u | rock/boulder 0.44-1.0u | building eave 2.75-4.4u
  person 1.1u | ground cover <=0.35u

Run:  python3 Tools/ScaleAudit/audit_world_scale.py [repo_root]
Out:  Tools/ScaleAudit/out/world_scale_vs_player.csv (+ console summary)
"""
import csv, glob, os, re, sys
from PIL import Image

PLAYER_UNITS = 1.1

# (regex over id, category, lo, hi) — owner bands in world units
BANDS = [
    (r"^(tree|pine|plains_tree(_v2_\d)?|plains_willow)$", "canopy_tree", 2.8, 3.8),
    (r"young|sapling|plains_small_tree", "young_tree", 1.4, 1.9),
    (r"predator_plant", "predator_plant", 1.10, 1.25),
    (r"bush|scrub|fern", "bush", 0.55, 0.90),
    (r"^(rock|shore_stone|plains_rock.*|shared_gray_rock|boulder|.*_vein|ore_.*)$", "rock", 0.44, 1.00),
    (r"building|tavern_r\d|shop_r\d|guild|hall", "building", 2.75, 4.40),
    (r"slime", "slime", 0.35, 0.70),
    (r"deer|fox", "wildlife", 0.60, 1.10),
    (r"bandit|adventurer_|townsfolk|merchant", "person", 1.00, 1.20),
    (r"flower|tulip|tuft|grass|clover|sprig", "ground_cover", 0.10, 0.35),
    (r"stump|log", "stump", 0.30, 0.60),
    (r"cactus", "cactus", 1.20, 2.20),
]

def band(name):
    for p, c, lo, hi in BANDS:
        if re.search(p, name):
            return c, lo, hi
    return "unbanded", None, None

root = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else ".")
res = os.path.join(root, "Assets", "Resources")
content_cs = open(os.path.join(root, "Assets", "Scripts", "IsoCoreFoundation",
                               "Core", "FoundationContent.cs"), encoding="utf-8").read()

def png_metrics(path):
    """(contentH_px, PPU) or None."""
    meta = path + ".meta"
    if not (os.path.exists(path) and os.path.exists(meta)):
        return None
    m = re.search(r"spritePixelsToUnits:\s*([\d.]+)", open(meta, encoding="utf-8").read())
    ppu = float(m.group(1)) if m else 100.0
    im = Image.open(path).convert("RGBA")
    bb = im.split()[3].getbbox()
    if not bb:
        return None
    return bb[3] - bb[1], ppu

rows = []

# ---- resource nodes: Node("id", col, tool, mandatory, hits, h, drops) ----
for m in re.finditer(r'Node\("([\w_]+)",[^;]*?,\s*\d+,\s*([\d.]+)f,', content_cs):
    nid, h = m.group(1), float(m.group(2))
    cat, lo, hi = band(nid)
    rows.append(["node", nid, h, round(h / PLAYER_UNITS, 2), cat, lo, hi,
                 "OFF" if lo and not (lo <= h <= hi) else ""])

# ---- placeables: Placeable("id", col, blocks, kind, station, req, h) ----
for m in re.finditer(r'Placeable\("([\w_]+)",[^;]*?"[\w_]*",\s*([\d.]+)f\)', content_cs):
    pid, h = m.group(1), float(m.group(2))
    cat, lo, hi = band(pid)
    rows.append(["placeable", pid, h, round(h / PLAYER_UNITS, 2), cat, lo, hi,
                 "OFF" if lo and not (lo <= h <= hi) else ""])

# ---- mobs: rendered size = art px / PPU (frames or decoration sprite) ----
MOB_ART = {
    "slime":            "Enemies/Slime/Individual Sprites/slime-move-0.png",
    "slime_common":     "Enemies/Slime/Individual Sprites/slime-move-0.png",
    "slime_rare":       "Enemies/Slime/Individual Sprites/slime-move-0.png",
    "slime_boss":       "Enemies/Slime/Individual Sprites/slime-move-0.png",
    "predator_plant_1": "Characters/predator_plant_1/predator_plant_1-move-0.png",
    "predator_plant_2": "Characters/predator_plant_2/predator_plant_2-move-0.png",
    "predator_plant_3": "Decorations/mobs/predator_plant_3.png",
}
size_units = dict(re.findall(r'(\w+)\.sizeUnits\s*=\s*([\d.]+)f', content_cs))
for mid, rel in sorted(MOB_ART.items()):
    got = png_metrics(os.path.join(res, rel))
    if not got:
        rows.append(["mob", mid, "?", "?", "art missing", None, None, "CHECK"])
        continue
    px, ppu = got
    h = px / ppu
    cat, lo, hi = band(mid)
    rows.append(["mob", mid, round(h, 2), round(h / PLAYER_UNITS, 2), cat, lo, hi,
                 "OFF" if lo and not (lo <= h <= hi) else ""])

# ---- raw decoration sprites (natural px/PPU size, informational) ----
for f in sorted(glob.glob(os.path.join(res, "Decorations", "**", "*.png"), recursive=True)):
    name = os.path.splitext(os.path.basename(f))[0]
    got = png_metrics(f)
    if not got:
        continue
    px, ppu = got
    h = px / ppu
    cat, lo, hi = band(name)
    rows.append(["decoration_art", name, round(h, 2), round(h / PLAYER_UNITS, 2),
                 cat, lo, hi, "off" if lo and not (lo <= h <= hi) else ""])

outdir = os.path.join(root, "Tools", "ScaleAudit", "out")
os.makedirs(outdir, exist_ok=True)
out = os.path.join(outdir, "world_scale_vs_player.csv")
with open(out, "w", newline="") as fh:
    w = csv.writer(fh)
    w.writerow(["kind", "id", "units_tall", "x_player", "band", "band_lo", "band_hi", "flag"])
    w.writerows(rows)

flagged = [r for r in rows if r[7] == "OFF"]
print(f"rows: {len(rows)}  ->  {out}")
print(f"OFF-TARGET (authoritative kinds only): {len(flagged)}")
for r in flagged:
    print("  %-10s %-22s %5s u  (%sx player)  want %s-%s" % (r[0], r[1], r[2], r[3], r[5], r[6]))
