#!/usr/bin/env python3
"""Renders the per-biome rule sample grids from BIOME_TILE_PROP_RULES.md.

Each board below is the literal ASCII grid from the rules doc: characters map
to pack tiles (terrain drawn first, then the optional prop on the same cell
rect). Run:  python3 render_biome_samples.py --out /tmp/biome-samples
"""
import argparse
import os
from PIL import Image, ImageDraw

# char -> (terrain_tile, prop_tile_or_None)
BOARDS = [
    ("MEADOW - calm 037 field; flowers cluster near shrub anchors; logs at edges; boulders only by cliffs", {
        '.': (37, None), 'f': (37, 41), 'F': (37, 44), 's': (37, 43),
        't': (37, 45), 'l': (37, 49), 'r': (37, 65), 'g': (37, 46),
    }, [
        "............",
        "..s.f.......",
        ".sfFg.......",
        "..fg....t...",
        "............",
        ".....l......",
        "..........r.",
        ".t..........",
    ]),
    ("FOREST - 040 floor; canopy MASS (029 + 027/028 accents); mossy logs+stumps in clearings; no field flowers", {
        ',': (40, None), 'C': (29, None), 'c': (27, None), 'D': (28, None),
        'l': (40, 50), 'S': (40, 52), 's': (40, 43), 't': (40, 45),
    }, [
        "CCcC,,,,CCCC",
        "CCCCC,s,CCcC",
        "cCDCC,,,CCCC",
        "CCC,,l,,,DCC",
        ",,,,,S,,,CCC",
        ",s,,,,t,,,,C",
        "CC,,t,,,,s,,",
        "CCCc,,CCC,,C",
    ]),
    ("BADLANDS - cracked 017/018 floor + dark dirt; rubble + brown rock clusters; sprout dirt ONLY at meadow border", {
        '-': (17, None), '=': (18, None), 'd': (3, None), 'u': (12, None),
        'k': (17, 53), 'K': (17, 56), 'j': (18, 57), 'p': (19, None),
        'q': (20, None), '.': (37, None), 't': (37, 45),
    }, [
        "--=--d--=---",
        "-kK---u--=--",
        "--j----kK---",
        "=---u----j--",
        "--=---d---=-",
        "p-q--p--q--p",
        "..t.....t...",
        "............",
    ]),
    ("COAST - meadow -> beach dirt -> navy ocean; foam stones ON the waterline; swells/sparkles deep side only", {
        '.': (37, None), 's': (37, 43), 'b': (0, None), 'B': (10, None),
        'o': (21, None), '#': (92, None), 'w': (92, 70), 'W': (92, 78),
        'x': (88, None), '*': (92, 83), 'n': (101, None),
    }, [
        "..s.........",
        "............",
        "bBbbBobbBbbo",
        "bbBbbbBbbBbb",
        "w##W#w##W##w",
        "###x####x###",
        "#*##n##*###n",
        "x######x####",
    ]),
    ("RIVER - light 104 stream, wash 106-108 at banks, rare 114 rapids; grass banks + sand patches; lowland foam stones", {
        '.': (37, None), '~': (104, None), '%': (106, None), '!': (114, None),
        'w': (104, 70), 'b': (10, None), 's': (37, 43), 'f': (37, 41),
    }, [
        "....b~~b....",
        ".s..b~%b..f.",
        "....b~~b....",
        "....b%~bb...",
        ".....b~!~b..",
        ".f...b~~%b.s",
        "......bw~b..",
        "......b~~b..",
    ]),
    ("CRAG (tier 3+) - bare stone: cobble/slab caps + boulder clusters; NO vegetation above the treeline", {
        'd': (0, None), 'q': (61, None), 'Q': (62, None), 'r': (61, 65),
        'R': (61, 67), 'P': (61, 64), '.': (37, None), 'g': (37, 65),
    }, [
        "qQqqrqqQqqqr",
        "qqRqqqPqqQqq",
        "Qqqqqrqqqqqq",
        "qPqQqqqRqqQq",
        "ddqdddqqdddd",
        "..g.........",
        "............",
        "............",
    ]),
]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="/tmp/biome-samples")
    ap.add_argument("--tile-root", default=None)
    ap.add_argument("--scale", type=int, default=2)
    args = ap.parse_args()

    repo = os.path.dirname(os.path.dirname(os.path.abspath(os.path.dirname(__file__) + "/.")))
    tile_root = args.tile_root or os.path.join(
        repo, "Docs", "handoff", "tile-pack-for-codex", "isometric tileset", "separated images")
    os.makedirs(args.out, exist_ok=True)

    S = args.scale
    TILE, STEPX, STEPY, PROP_LIFT = 32 * S, 16 * S, 8 * S, -5

    cache = {}

    def tile(n):
        if n not in cache:
            img = Image.open(os.path.join(tile_root, f"tile_{n:03d}.png")).convert("RGBA")
            cache[n] = img.resize((img.width * S, img.height * S), Image.NEAREST)
        return cache[n]

    boards_px = []
    for title, mapping, grid in BOARDS:
        H, W = len(grid), len(grid[0])
        bw = (W + H) * STEPX + 64
        bh = (W + H) * STEPY + TILE + 110
        board = Image.new("RGBA", (bw, bh), (8, 8, 10, 255))
        ox, oy = bw // 2, 70
        for y in range(H):
            for x in range(W):
                ch = grid[y][x]
                terrain, prop = mapping[ch]
                sx = ox + (x - y) * STEPX - TILE // 2
                sy = oy + (x + y) * STEPY
                board.alpha_composite(tile(terrain), (sx, sy))
                if prop is not None:
                    board.alpha_composite(tile(prop), (sx, sy + PROP_LIFT))
        d = ImageDraw.Draw(board)
        d.text((16, 12), title, fill=(235, 235, 235, 255))
        boards_px.append(board)

    # stack all boards into one review sheet
    sheet_w = max(b.width for b in boards_px)
    sheet_h = sum(b.height for b in boards_px) + 10 * (len(boards_px) - 1)
    sheet = Image.new("RGB", (sheet_w, sheet_h), (8, 8, 10))
    yoff = 0
    for b in boards_px:
        sheet.paste(b.convert("RGB"), ((sheet_w - b.width) // 2, yoff))
        yoff += b.height + 10
    out = os.path.join(args.out, "biome-rule-samples.png")
    sheet.save(out)
    print(out)


if __name__ == "__main__":
    main()
