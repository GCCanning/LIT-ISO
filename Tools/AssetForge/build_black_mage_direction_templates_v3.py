#!/usr/bin/env python3
"""Build an 8-direction black mage scaffold set.

These are deterministic control templates, not final art. The goal is to give
img2img a real S/E/N/W shape before the next ComfyUI pass, especially for the
north/back view that must not show the face.
"""

from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw

from build_black_mage_direction_templates_v2 import PALETTE, draw_template as draw_diagonal_template


CANONICAL_DIRECTIONS = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"]
DEFAULT_SOURCE = Path("Assets/Generated/_Review/black_mage_iso_style_lock_v1/black_mage_normalized_128.png")
DEFAULT_OUTPUT_ROOT = Path("Assets/Generated/_Review/black_mage_direction_templates_v3")


DIRECTION_PROMPT = {
    "S": "south isometric front view, both eyes and front robe readable",
    "SE": "south-east isometric view, character facing toward-right, front and right side visible",
    "E": "east isometric side view, right side profile, one side of hat and robe readable",
    "NE": "north-east isometric view, character facing away-right, back and right side visible, no face visible",
    "N": "north isometric back view, back of hat and robe visible, no face visible",
    "NW": "north-west isometric view, character facing away-left, back and left side visible, no face visible",
    "W": "west isometric side view, left side profile, one side of hat and robe readable",
    "SW": "south-west isometric view, character facing toward-left, front and left side visible",
}


def rel(root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path.resolve()).replace("\\", "/")


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def line(draw: ImageDraw.ImageDraw, points: list[tuple[int, int]], fill: tuple[int, int, int, int], width: int) -> None:
    draw.line(points, fill=fill, width=width, joint="curve")


def staff(draw: ImageDraw.ImageDraw, x: int, top: int, bottom: int, lean: int) -> None:
    line(draw, [(x + lean, top), (x, bottom)], PALETTE["outline"], 6)
    line(draw, [(x + lean, top), (x, bottom)], PALETTE["staff"], 3)
    cx = x + lean
    draw.rectangle([cx - 5, top - 9, cx + 5, top + 3], fill=PALETTE["outline"])
    draw.rectangle([cx - 3, top - 7, cx + 3, top + 1], fill=PALETTE["crystal"])
    draw.point([(cx, top - 8), (cx - 4, top - 3), (cx + 4, top - 3)], fill=PALETTE["gold"])


def feet(draw: ImageDraw.ImageDraw, left_x: int = 50, right_x: int = 70) -> None:
    draw.rectangle([left_x, 116, left_x + 11, 122], fill=PALETTE["outline"])
    draw.rectangle([right_x, 116, right_x + 11, 122], fill=PALETTE["outline"])
    draw.rectangle([left_x + 2, 115, left_x + 10, 119], fill=PALETTE["dark"])
    draw.rectangle([right_x + 1, 115, right_x + 9, 119], fill=PALETTE["dark"])


def draw_front() -> Image.Image:
    image = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.polygon([(41, 43), (82, 40), (96, 49), (68, 56), (34, 51)], fill=PALETTE["outline"])
    draw.polygon([(45, 44), (80, 42), (89, 48), (67, 53), (39, 49)], fill=PALETTE["dark"])
    draw.polygon([(46, 42), (67, 14), (86, 43), (74, 50), (55, 50)], fill=PALETTE["outline"])
    draw.polygon([(51, 41), (67, 18), (81, 42), (72, 48), (57, 48)], fill=PALETTE["black"])
    draw.rectangle([54, 40, 75, 48], fill=PALETTE["gold"])
    draw.polygon([(48, 52), (79, 52), (89, 111), (64, 121), (38, 111)], fill=PALETTE["outline"])
    draw.polygon([(52, 57), (76, 57), (84, 107), (64, 116), (43, 107)], fill=PALETTE["black"])
    draw.polygon([(51, 49), (76, 50), (78, 66), (64, 75), (49, 65)], fill=PALETTE["outline"])
    draw.polygon([(55, 52), (72, 52), (74, 63), (64, 70), (53, 63)], fill=PALETTE["skin"])
    draw.rectangle([55, 51, 73, 56], fill=PALETTE["hair"])
    draw.point([(59, 60), (68, 60)], fill=PALETTE["outline"])
    draw.line([(55, 82), (76, 82)], fill=PALETTE["orange"], width=3)
    draw.line([(64, 84), (64, 110)], fill=PALETTE["orange"], width=2)
    draw.line([(49, 68), (37, 84), (36, 96)], fill=PALETTE["outline"], width=7)
    draw.line([(79, 68), (92, 84), (92, 96)], fill=PALETTE["outline"], width=7)
    draw.line([(50, 69), (39, 84), (38, 95)], fill=PALETTE["dark"], width=4)
    draw.line([(78, 69), (90, 84), (90, 95)], fill=PALETTE["dark"], width=4)
    staff(draw, 95, 38, 111, 7)
    feet(draw)
    return image


def draw_back() -> Image.Image:
    image = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    staff(draw, 37, 38, 111, -5)
    draw.polygon([(42, 41), (65, 13), (86, 42), (75, 50), (53, 49)], fill=PALETTE["outline"])
    draw.polygon([(48, 40), (65, 18), (80, 41), (72, 47), (56, 47)], fill=PALETTE["black"])
    draw.polygon([(45, 43), (80, 42), (93, 49), (69, 56), (35, 50)], fill=PALETTE["outline"])
    draw.polygon([(49, 45), (78, 44), (87, 48), (69, 53), (40, 49)], fill=PALETTE["dark"])
    draw.rectangle([57, 42, 72, 49], fill=PALETTE["gold"])
    draw.polygon([(47, 55), (80, 54), (93, 111), (64, 121), (35, 111)], fill=PALETTE["outline"])
    draw.polygon([(52, 59), (76, 58), (87, 108), (64, 116), (40, 108)], fill=PALETTE["black"])
    draw.polygon([(50, 48), (78, 48), (84, 66), (65, 74), (45, 64)], fill=PALETTE["outline"])
    draw.polygon([(53, 51), (75, 51), (79, 63), (65, 69), (49, 62)], fill=PALETTE["dark"])
    draw.line([(64, 61), (64, 111)], fill=PALETTE["orange"], width=3)
    draw.line([(53, 77), (74, 82), (83, 106)], fill=PALETTE["mid"], width=3)
    draw.line([(50, 69), (39, 85), (39, 97)], fill=PALETTE["outline"], width=7)
    draw.line([(78, 69), (89, 85), (89, 97)], fill=PALETTE["outline"], width=7)
    feet(draw)
    return image


def draw_side(direction: str) -> Image.Image:
    mirror = direction == "W"
    image = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    sx = -1 if mirror else 1

    def px(x: int) -> int:
        return 128 - x if mirror else x

    staff(draw, px(92), 38, 112, 8 * sx)
    hat_poly = [(px(42), 43), (px(65), 14), (px(86), 44), (px(74), 51), (px(52), 50)]
    draw.polygon(hat_poly, fill=PALETTE["outline"])
    draw.polygon([(px(48), 42), (px(65), 18), (px(80), 43), (px(72), 48), (px(56), 48)], fill=PALETTE["black"])
    draw.polygon([(px(43), 44), (px(82), 42), (px(94), 49), (px(72), 55), (px(38), 50)], fill=PALETTE["outline"])
    draw.polygon([(px(47), 45), (px(79), 44), (px(88), 48), (px(70), 52), (px(42), 49)], fill=PALETTE["dark"])
    draw.rectangle([min(px(58), px(72)), 41, max(px(58), px(72)), 48], fill=PALETTE["gold"])
    draw.polygon([(px(50), 55), (px(77), 54), (px(88), 111), (px(63), 121), (px(41), 111)], fill=PALETTE["outline"])
    draw.polygon([(px(54), 58), (px(74), 58), (px(83), 107), (px(63), 116), (px(45), 107)], fill=PALETTE["black"])
    draw.polygon([(px(55), 50), (px(75), 51), (px(78), 65), (px(66), 72), (px(53), 64)], fill=PALETTE["outline"])
    draw.polygon([(px(58), 52), (px(73), 53), (px(74), 63), (px(66), 68), (px(56), 62)], fill=PALETTE["skin"])
    draw.rectangle([min(px(58), px(74)), 52, max(px(58), px(74)), 57], fill=PALETTE["hair"])
    draw.point([(px(69), 60)], fill=PALETTE["outline"])
    draw.line([(px(77), 68), (px(90), 84), (px(90), 96)], fill=PALETTE["outline"], width=7)
    draw.line([(px(76), 69), (px(88), 84), (px(88), 95)], fill=PALETTE["dark"], width=4)
    feet(draw, 52, 68)
    return image


def draw_template(direction: str) -> Image.Image:
    if direction in {"NE", "NW", "SE", "SW"}:
        return draw_diagonal_template(direction)
    if direction == "S":
        return draw_front()
    if direction == "N":
        return draw_back()
    if direction in {"E", "W"}:
        return draw_side(direction)
    raise ValueError(f"Unsupported direction: {direction}")


def make_sheet(source_path: Path, templates: dict[str, Path], output_path: Path) -> None:
    cell_w = 154
    cell_h = 168
    columns = 3
    rows = 3
    sheet = Image.new("RGBA", (cell_w * columns, 54 + cell_h * rows), (16, 18, 20, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((12, 10), "Black mage direction templates v3", fill=(238, 242, 238, 255))
    draw.text((12, 30), "8D scaffold controls only. Review/temp; not final art.", fill=(160, 170, 164, 255))

    cells = [("style", source_path)] + [(direction, templates[direction]) for direction in CANONICAL_DIRECTIONS]
    for index, (label, path) in enumerate(cells):
        x = (index % columns) * cell_w + 12
        y = 54 + (index // columns) * cell_h + 8
        draw.rectangle([x, y, x + 128, y + 128], fill=(26, 29, 32, 255), outline=(58, 66, 60, 255))
        image = Image.open(path).convert("RGBA").resize((128, 128), Image.Resampling.NEAREST)
        sheet.alpha_composite(image, (x, y))
        draw.text((x, y + 136), label, fill=(210, 218, 205, 255))
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)


def prompt_for(direction: str) -> str:
    return (
        "LIT-ISO pixel character sprite, black mage wizard, compact isometric RPG sprite, "
        f"{DIRECTION_PROMPT[direction]}, tall black hat with tan patch, black robe with orange belt accents, "
        "one crooked staff with orange crystal, transparent background, no floor, no shadow, crisp pixel art"
    )


def main() -> int:
    parser = argparse.ArgumentParser(description="Build 8D black mage direction templates.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--source", default=str(DEFAULT_SOURCE))
    parser.add_argument("--output-root", default=str(DEFAULT_OUTPUT_ROOT))
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    source_path = project_root / args.source
    output_root = project_root / args.output_root
    output_root.mkdir(parents=True, exist_ok=True)

    templates: dict[str, Path] = {}
    for direction in CANONICAL_DIRECTIONS:
        image = draw_template(direction)
        out = output_root / f"black_mage_{direction}_template_v3.png"
        image.save(out)
        templates[direction] = out

    sheet_path = output_root / "black_mage_direction_templates_v3_sheet.png"
    make_sheet(source_path, templates, sheet_path)
    manifest = {
        "schema": "lit_iso.asset_forge.black_mage_direction_templates.v3",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "source": rel(project_root, source_path),
        "review_sheet": rel(project_root, sheet_path),
        "directions": CANONICAL_DIRECTIONS,
        "scaffolds": {direction: rel(project_root, path) for direction, path in templates.items()},
        "generation_prompts": [
            {
                "direction": direction,
                "prompt": prompt_for(direction),
                "negative_prompt": "wrong direction, floor, base, shadow, spell effect, magic circle, duplicate, scene, prop, text, blurry, antialias haze",
                "status": "template_v3_prompt_contract_not_generated",
            }
            for direction in CANONICAL_DIRECTIONS
        ],
        "status": "review_ready_generation_not_run",
        "notes": [
            "These templates are deterministic direction controls, not final character art.",
            "S/E/N/W are new in v3 and are the blocker for a true 4D sheet.",
            "North intentionally hides the face and exposes the back silhouette.",
            "Use with low-denoise img2img plus original mage style reference.",
        ],
    }
    manifest_path = output_root / "black_mage_direction_templates_v3_manifest.json"
    write_json(manifest_path, manifest)
    print(json.dumps({"manifest": rel(project_root, manifest_path), "review_sheet": rel(project_root, sheet_path), "templates": len(templates)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
