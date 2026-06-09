#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from PIL import Image

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--sheet", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--cell-width", type=int, default=128)
    parser.add_argument("--cell-height", type=int, default=128)
    parser.add_argument("--column", type=int, default=0)
    parser.add_argument("--row", type=int, default=0)
    parser.add_argument("--trim", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    with Image.open(args.sheet) as source:
        image = source.convert("RGBA")
    left = args.column * args.cell_width
    top = args.row * args.cell_height
    right = left + args.cell_width
    bottom = top + args.cell_height
    if left < 0 or top < 0 or right > image.width or bottom > image.height:
        raise ValueError(f"Cell is outside sheet bounds: sheet={image.size} cell=({left},{top},{right},{bottom})")
    cell = image.crop((left, top, right, bottom))
    if args.trim:
        bbox = cell.getchannel("A").getbbox()
        if bbox:
            cell = cell.crop(bbox)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    cell.save(args.output)
    print(json.dumps({"ok": True, "output": str(args.output), "size": list(cell.size)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
