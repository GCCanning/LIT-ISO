#!/usr/bin/env python3
"""
Build deterministic tile geometry control images for SD1.5 + ControlNet.

These are clean-room templates: line/depth-style guides for ComfyUI tile
generation. They are not final art. Use them to lock geometry while a LoRA or
style reference controls palette/material rendering.
"""

from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageDraw


CANVAS = 512
CX = CANVAS // 2
TOP_Y = 154
HALF_W = 176
HALF_H = 88
HEIGHT_1 = 112
HEIGHT_2 = 176


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def diamond(cx: int = CX, cy: int = TOP_Y, half_w: int = HALF_W, half_h: int = HALF_H) -> list[tuple[int, int]]:
    return [(cx, cy - half_h), (cx + half_w, cy), (cx, cy + half_h), (cx - half_w, cy)]


def shifted(points: list[tuple[int, int]], dx: int = 0, dy: int = 0) -> list[tuple[int, int]]:
    return [(x + dx, y + dy) for x, y in points]


def new_image() -> tuple[Image.Image, ImageDraw.ImageDraw]:
    image = Image.new("RGB", (CANVAS, CANVAS), (0, 0, 0))
    return image, ImageDraw.Draw(image)


def draw_poly(draw: ImageDraw.ImageDraw, points: list[tuple[int, int]], outline: int = 255, width: int = 5) -> None:
    draw.line(points + [points[0]], fill=(outline, outline, outline), width=width, joint="curve")


def draw_grid(draw: ImageDraw.ImageDraw, points: list[tuple[int, int]], lines: int = 4, fill: int = 82) -> None:
    top, right, bottom, left = points
    for index in range(1, lines):
        t = index / lines
        a = lerp_point(left, top, t)
        b = lerp_point(bottom, right, t)
        c = lerp_point(top, right, t)
        d = lerp_point(left, bottom, t)
        draw.line([a, b], fill=(fill, fill, fill), width=2)
        draw.line([c, d], fill=(fill, fill, fill), width=2)


def lerp_point(a: tuple[int, int], b: tuple[int, int], t: float) -> tuple[int, int]:
    return (round(a[0] + (b[0] - a[0]) * t), round(a[1] + (b[1] - a[1]) * t))


def draw_raised(draw: ImageDraw.ImageDraw, height: int, grid: bool = True) -> None:
    top = diamond()
    bottom = shifted(top, 0, height)
    top_top, top_right, top_bottom, top_left = top
    bot_top, bot_right, bot_bottom, bot_left = bottom
    draw.polygon([top_left, top_bottom, bot_bottom, bot_left], fill=(62, 62, 62))
    draw.polygon([top_right, bot_right, bot_bottom, top_bottom], fill=(105, 105, 105))
    draw.polygon(top, fill=(172, 172, 172))
    draw.line([top_left, bot_left], fill=(210, 210, 210), width=4)
    draw.line([top_bottom, bot_bottom], fill=(210, 210, 210), width=4)
    draw.line([top_right, bot_right], fill=(210, 210, 210), width=4)
    draw_poly(draw, top, 245, 5)
    draw.line([bot_left, bot_bottom, bot_right], fill=(226, 226, 226), width=5)
    if grid:
        draw_grid(draw, top, lines=4, fill=115)


def draw_ramp(draw: ImageDraw.ImageDraw, direction: str) -> None:
    top = diamond()
    bottom = shifted(top, 0, HEIGHT_1)
    top_top, top_right, top_bottom, top_left = top
    bot_top, bot_right, bot_bottom, bot_left = bottom
    if direction == "south":
        ramp = [top_top, top_right, bot_bottom, bot_left, top_left]
    elif direction == "north":
        ramp = [bot_top, bot_right, top_bottom, top_left, bot_left]
    elif direction == "east":
        ramp = [top_top, top_right, bot_right, bot_bottom, top_bottom]
    else:
        ramp = [top_top, top_left, bot_left, bot_bottom, top_bottom]
    draw.polygon(ramp, fill=(156, 156, 156))
    draw_poly(draw, ramp, 245, 5)
    draw.line([top_left, top_right], fill=(92, 92, 92), width=3)


def draw_edge(draw: ImageDraw.ImageDraw, direction: str) -> None:
    top = diamond()
    draw.polygon(top, fill=(160, 160, 160))
    draw_grid(draw, top, lines=4, fill=86)
    edges = {
        "north": [top[3], top[0], top[1]],
        "east": [top[0], top[1], top[2]],
        "south": [top[1], top[2], top[3]],
        "west": [top[2], top[3], top[0]],
    }
    draw_poly(draw, top, 145, 3)
    draw.line(edges[direction], fill=(255, 255, 255), width=10, joint="curve")


def draw_template(kind: str) -> Image.Image:
    image, draw = new_image()
    top = diamond()
    if kind == "flat_diamond":
        draw.polygon(top, fill=(170, 170, 170))
        draw_poly(draw, top, 255, 5)
    elif kind == "flat_grid":
        draw.polygon(top, fill=(158, 158, 158))
        draw_grid(draw, top, lines=4, fill=82)
        draw_poly(draw, top, 255, 5)
    elif kind == "raised_block_h1":
        draw_raised(draw, HEIGHT_1)
    elif kind == "raised_block_h2":
        draw_raised(draw, HEIGHT_2)
    elif kind.startswith("edge_"):
        draw_edge(draw, kind.replace("edge_", ""))
    elif kind.startswith("ramp_"):
        draw_ramp(draw, kind.replace("ramp_", ""))
    elif kind == "cliff_south":
        draw_raised(draw, HEIGHT_2, grid=False)
        draw.line([top[1], shifted(top, 0, HEIGHT_2)[2], shifted(top, 0, HEIGHT_2)[3], top[3]], fill=(255, 255, 255), width=9)
    elif kind == "corner_ne":
        draw.polygon(top, fill=(155, 155, 155))
        draw_grid(draw, top, lines=4, fill=80)
        draw.line([top[3], top[0], top[1], top[2]], fill=(255, 255, 255), width=9, joint="curve")
    elif kind == "corner_sw":
        draw.polygon(top, fill=(155, 155, 155))
        draw_grid(draw, top, lines=4, fill=80)
        draw.line([top[1], top[2], top[3], top[0]], fill=(255, 255, 255), width=9, joint="curve")
    else:
        raise ValueError(f"Unknown template kind: {kind}")
    return image


TEMPLATES = [
    "flat_diamond",
    "flat_grid",
    "raised_block_h1",
    "raised_block_h2",
    "edge_north",
    "edge_east",
    "edge_south",
    "edge_west",
    "ramp_north",
    "ramp_east",
    "ramp_south",
    "ramp_west",
    "cliff_south",
    "corner_ne",
    "corner_sw",
]


def build_contact(records: list[dict], out_path: Path) -> None:
    cell = 160
    columns = 5
    rows = (len(records) + columns - 1) // columns
    sheet = Image.new("RGB", (columns * cell, rows * (cell + 24)), (18, 21, 28))
    draw = ImageDraw.Draw(sheet)
    for index, record in enumerate(records):
        image = Image.open(record["path"]).convert("RGB")
        image.thumbnail((140, 140), Image.Resampling.NEAREST)
        x = (index % columns) * cell
        y = (index // columns) * (cell + 24)
        sheet.paste(image, (x + 10, y + 6))
        draw.text((x + 8, y + 148), record["id"][:22], fill=(235, 240, 245))
    sheet.save(out_path)


def main() -> int:
    parser = argparse.ArgumentParser(description="Build tile ControlNet template images.")
    parser.add_argument("--out", type=Path, default=Path(r"C:\Projects\Pixel Pipeline\datasets\lit_iso\controlnet_templates\tile_geometry_v1"))
    parser.add_argument("--replace", action="store_true")
    args = parser.parse_args()

    out = args.out.resolve()
    if out.exists() and args.replace:
        for child in out.iterdir():
            if child.is_file():
                child.unlink()
    out.mkdir(parents=True, exist_ok=True)

    records = []
    for kind in TEMPLATES:
        image = draw_template(kind)
        path = out / f"{kind}.png"
        image.save(path)
        records.append(
            {
                "id": kind,
                "path": str(path),
                "control_type": "line_or_depth_geometry",
                "canvas": [CANVAS, CANVAS],
                "recommended_model": "SD1.5 ControlNet Canny/Lineart/Depth",
                "notes": "Use as geometry control only; final pixels come from SD1.5 checkpoint plus LIT-ISO style LoRA/reference.",
            }
        )

    contact = out / "contact_sheet.png"
    build_contact(records, contact)
    manifest = {
        "schema": "lit_iso.asset_forge.tile_controlnet_templates.v1",
        "created_utc": utc_now(),
        "out": str(out),
        "count": len(records),
        "templates": records,
        "contact_sheet": str(contact),
        "usage": "Pair with SD1.5 ControlNet Canny/Lineart/Depth. Do not use OpenPose for terrain tiles.",
    }
    (out / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps({"out": str(out), "count": len(records), "contact_sheet": str(contact)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
