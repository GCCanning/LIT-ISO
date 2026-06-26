"""
Remove white/near-white BACKGROUNDS from prop candidates without touching
white pixels inside the sprite (highlights, foam, eyes...).

Method: flood fill from all four image edges through near-white pixels only;
whatever the flood reaches is background -> transparent. Interior whites are
unreachable and survive. A 1px alpha feather is NOT applied (pixel art).

Usage:
  py -3 clean_white_bg.py <folder-or-png> [more ...]
  py -3 clean_white_bg.py C:\\Users\\garyc\\OneDrive\\Desktop\\PixelArt\\Props\\tavern

Originals are kept as <name>.orig.png the first time a file is cleaned.
"""

import os
import sys
from collections import deque

from PIL import Image

TOL = 28  # channel distance from pure white that still counts as background


def is_whiteish(px):
    r, g, b, a = px
    return a > 0 and r >= 255 - TOL and g >= 255 - TOL and b >= 255 - TOL


def clean(path):
    im = Image.open(path).convert("RGBA")
    px = im.load()
    w, h = im.size
    seen = [[False] * w for _ in range(h)]
    q = deque()
    for x in range(w):
        for y in (0, h - 1):
            if is_whiteish(px[x, y]) and not seen[y][x]:
                seen[y][x] = True; q.append((x, y))
    for y in range(h):
        for x in (0, w - 1):
            if is_whiteish(px[x, y]) and not seen[y][x]:
                seen[y][x] = True; q.append((x, y))
    cleared = 0
    while q:
        x, y = q.popleft()
        px[x, y] = (0, 0, 0, 0)
        cleared += 1
        for nx, ny in ((x+1, y), (x-1, y), (x, y+1), (x, y-1)):
            if 0 <= nx < w and 0 <= ny < h and not seen[ny][nx] and is_whiteish(px[nx, ny]):
                seen[ny][nx] = True
                q.append((nx, ny))
    if cleared:
        orig = path[:-4] + ".orig.png"
        if not os.path.exists(orig):
            Image.open(path).save(orig)
        im.save(path)
    return cleared


def main():
    targets = sys.argv[1:]
    if not targets:
        sys.exit(__doc__)
    pngs = []
    for t in targets:
        if os.path.isdir(t):
            for root, _, files in os.walk(t):
                pngs += [os.path.join(root, f) for f in files
                         if f.endswith(".png") and not f.endswith(".orig.png")
                         and not f.startswith("_")]
        elif t.endswith(".png"):
            pngs.append(t)
    total = 0
    for p in pngs:
        n = clean(p)
        if n:
            print(f"cleaned {os.path.basename(p)}: {n}px background removed")
            total += 1
    print(f"\n{total}/{len(pngs)} files had white background removed.")


if __name__ == "__main__":
    main()
