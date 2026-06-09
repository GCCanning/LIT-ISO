#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


def load_font(size: int) -> ImageFont.ImageFont:
    for name in ("arial.ttf", "segoeui.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--sheet", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--cell-width", type=int, default=128)
    parser.add_argument("--cell-height", type=int, default=128)
    parser.add_argument("--columns", type=int, default=4)
    parser.add_argument("--rows", type=int, default=8)
    parser.add_argument("--scale", type=int, default=1)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    with Image.open(args.sheet) as source:
        sheet = source.convert("RGBA")
    label_h = 22
    cell_w = args.cell_width * args.scale
    cell_h = args.cell_height * args.scale + label_h
    out = Image.new("RGBA", (cell_w * args.columns, cell_h * args.rows), (28, 30, 36, 255))
    draw = ImageDraw.Draw(out)
    font = load_font(13)
    for row in range(args.rows):
        for col in range(args.columns):
            crop = sheet.crop(
                (
                    col * args.cell_width,
                    row * args.cell_height,
                    (col + 1) * args.cell_width,
                    (row + 1) * args.cell_height,
                )
            )
            if args.scale != 1:
                crop = crop.resize((args.cell_width * args.scale, args.cell_height * args.scale), Image.Resampling.NEAREST)
            x = col * cell_w
            y = row * cell_h
            out.alpha_composite(crop, (x, y))
            draw.rectangle((x, y, x + cell_w - 1, y + cell_h - 1), outline=(70, 80, 96, 255), width=1)
            draw.rectangle((x, y + cell_h - label_h, x + cell_w, y + cell_h), fill=(12, 14, 18, 255))
            draw.text((x + 5, y + cell_h - label_h + 4), f"r{row} c{col}", fill=(230, 236, 244, 255), font=font)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    out.save(args.output)
    print(json.dumps({"ok": True, "output": str(args.output), "columns": args.columns, "rows": args.rows}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
