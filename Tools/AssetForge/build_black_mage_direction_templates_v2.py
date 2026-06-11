#!/usr/bin/env python3
"""Build stronger black mage direction templates for scaffold-guided generation.

These are not final sprites. They are deterministic, review-only control
templates designed to give img2img a real directional silhouette instead of a
front-facing mirrored source.
"""

from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw


DIRECTIONS = ["NE", "NW", "SE", "SW"]
DEFAULT_SOURCE = Path("Assets/Generated/_Review/black_mage_iso_style_lock_v1/black_mage_normalized_128.png")
DEFAULT_OUTPUT_ROOT = Path("Assets/Generated/_Review/black_mage_direction_templates_v2")


PALETTE = {
    "outline": (18, 17, 22, 255),
    "black": (35, 32, 42, 255),
    "dark": (47, 43, 54, 255),
    "mid": (68, 59, 67, 255),
    "orange": (196, 103, 40, 255),
    "gold": (222, 160, 73, 255),
    "skin": (239, 199, 143, 255),
    "hair": (97, 59, 43, 255),
    "staff": (126, 86, 49, 255),
    "crystal": (234, 115, 31, 255),
}


def rel(root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path.resolve()).replace("\\", "/")


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def draw_pixel_line(draw: ImageDraw.ImageDraw, points: list[tuple[int, int]], fill: tuple[int, int, int, int], width: int = 3) -> None:
    draw.line(points, fill=fill, width=width, joint="curve")


def draw_staff(draw: ImageDraw.ImageDraw, direction: str) -> None:
    if direction in {"NE", "NW"}:
        x = 91 if direction == "NE" else 37
        lean = 7 if direction == "NE" else -7
    else:
        x = 94 if direction == "SE" else 34
        lean = 9 if direction == "SE" else -9
    draw_pixel_line(draw, [(x + lean, 37), (x, 111)], PALETTE["outline"], 6)
    draw_pixel_line(draw, [(x + lean, 37), (x, 111)], PALETTE["staff"], 3)
    cx = x + lean
    draw.rectangle([cx - 5, 28, cx + 5, 39], fill=PALETTE["outline"])
    draw.rectangle([cx - 3, 30, cx + 3, 37], fill=PALETTE["crystal"])
    draw.point([(cx, 29), (cx - 4, 34), (cx + 4, 34)], fill=PALETTE["gold"])


def draw_hat(draw: ImageDraw.ImageDraw, direction: str) -> None:
    mirror = direction in {"NW", "SW"}
    back = direction in {"NE", "NW"}
    brim_y = 37 if back else 42
    crown_tip = (47, 15) if mirror else (82, 15)
    crown_base_left = (43, 40)
    crown_base_right = (86, 41)
    if mirror:
        crown_base_left, crown_base_right = (42, 41), (85, 40)
    draw.polygon([crown_base_left, crown_tip, crown_base_right, (76, 50), (54, 50)], fill=PALETTE["outline"])
    draw.polygon([(48, 40), crown_tip, (82, 40), (74, 48), (55, 48)], fill=PALETTE["black"])
    if back:
        draw.polygon([(54, 37), (80, 38), (90, 45), (71, 52), (42, 47)], fill=PALETTE["outline"])
        draw.polygon([(55, 39), (78, 40), (85, 44), (69, 49), (45, 45)], fill=PALETTE["dark"])
        draw.rectangle([57, 34, 70, 41], fill=PALETTE["gold"])
    else:
        draw.polygon([(40, brim_y), (82, brim_y - 2), (95, brim_y + 7), (68, brim_y + 14), (35, brim_y + 8)], fill=PALETTE["outline"])
        draw.polygon([(43, brim_y + 1), (80, brim_y), (90, brim_y + 6), (67, brim_y + 11), (39, brim_y + 6)], fill=PALETTE["dark"])
        draw.polygon([(55, brim_y - 2), (72, brim_y - 3), (69, brim_y + 6), (53, brim_y + 7)], fill=PALETTE["gold"])


def draw_body(draw: ImageDraw.ImageDraw, direction: str) -> None:
    back = direction in {"NE", "NW"}
    mirror = direction in {"NW", "SW"}
    if back:
        robe = [(48, 54), (78, 53), (92, 111), (63, 121), (35, 111)]
        hood = [(50, 45), (78, 45), (83, 65), (65, 73), (45, 64)]
        draw.polygon(robe, fill=PALETTE["outline"])
        draw.polygon([(52, 58), (76, 57), (87, 108), (63, 116), (40, 108)], fill=PALETTE["black"])
        draw.polygon(hood, fill=PALETTE["outline"])
        draw.polygon([(53, 48), (75, 48), (79, 63), (65, 69), (49, 62)], fill=PALETTE["dark"])
        draw.line([(64, 60), (64, 111)], fill=PALETTE["orange"], width=3)
        draw.line([(53, 77), (73, 82), (82, 105)], fill=PALETTE["mid"], width=3)
    else:
        face_shift = -5 if mirror else 5
        robe = [(47, 55), (79, 55), (89, 111), (63, 121), (38, 110)]
        draw.polygon(robe, fill=PALETTE["outline"])
        draw.polygon([(51, 59), (76, 59), (84, 107), (63, 116), (43, 107)], fill=PALETTE["black"])
        draw.polygon([(51 + face_shift, 47), (75 + face_shift, 49), (78 + face_shift, 66), (63 + face_shift, 75), (48 + face_shift, 64)], fill=PALETTE["outline"])
        draw.polygon([(54 + face_shift, 50), (72 + face_shift, 51), (74 + face_shift, 63), (63 + face_shift, 70), (52 + face_shift, 62)], fill=PALETTE["skin"])
        draw.rectangle([54 + face_shift, 50, 74 + face_shift, 56], fill=PALETTE["hair"])
        draw.rectangle([60 + face_shift, 60, 63 + face_shift, 64], fill=PALETTE["outline"])
        draw.line([(55, 82), (76, 81)], fill=PALETTE["orange"], width=3)
        draw.line([(62, 84), (62, 110)], fill=PALETTE["orange"], width=2)

    # Feet / bottom anchor.
    draw.rectangle([49, 116, 61, 122], fill=PALETTE["outline"])
    draw.rectangle([67, 116, 79, 122], fill=PALETTE["outline"])
    draw.rectangle([51, 115, 60, 119], fill=PALETTE["dark"])
    draw.rectangle([68, 115, 77, 119], fill=PALETTE["dark"])


def draw_arm(draw: ImageDraw.ImageDraw, direction: str) -> None:
    if direction in {"NE", "SE"}:
        draw.line([(79, 67), (91, 82), (91, 95)], fill=PALETTE["outline"], width=7)
        draw.line([(80, 68), (89, 83), (89, 94)], fill=PALETTE["dark"], width=4)
        draw.rectangle([85, 92, 94, 101], fill=PALETTE["outline"])
    else:
        draw.line([(49, 67), (37, 82), (37, 95)], fill=PALETTE["outline"], width=7)
        draw.line([(48, 68), (39, 83), (39, 94)], fill=PALETTE["dark"], width=4)
        draw.rectangle([34, 92, 43, 101], fill=PALETTE["outline"])


def draw_template(direction: str) -> Image.Image:
    image = Image.new("RGBA", (128, 128), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    # Draw staff behind body for away views, in front for toward views.
    if direction in {"NE", "NW"}:
        draw_staff(draw, direction)
        draw_body(draw, direction)
        draw_hat(draw, direction)
        draw_arm(draw, direction)
    else:
        draw_body(draw, direction)
        draw_hat(draw, direction)
        draw_arm(draw, direction)
        draw_staff(draw, direction)
    return image


def draw_sheet(project_root: Path, source_path: Path, templates: dict[str, Path], output_path: Path) -> None:
    cell_w = 156
    cell_h = 170
    header_h = 56
    sheet = Image.new("RGBA", (cell_w * 5, header_h + cell_h), (16, 18, 20, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((12, 10), "Black mage direction templates v2", fill=(238, 242, 238, 255))
    draw.text((12, 30), "Control templates only. Use as img2img scaffold, not final art.", fill=(160, 170, 164, 255))

    def paste(index: int, label: str, path: Path) -> None:
        x = index * cell_w + 12
        y = header_h + 8
        draw.rectangle([x, y, x + 128, y + 128], fill=(26, 29, 32, 255), outline=(58, 66, 60, 255))
        image = Image.open(path).convert("RGBA").resize((128, 128), Image.Resampling.NEAREST)
        sheet.alpha_composite(image, (x, y))
        draw.text((x, y + 136), label, fill=(210, 218, 205, 255))

    paste(0, "style/source", source_path)
    for index, direction in enumerate(DIRECTIONS, start=1):
        paste(index, direction, templates[direction])
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)


def prompt_for(direction: str) -> str:
    direction_text = {
        "NE": "north-east isometric view, character facing away-right, back and right side visible, no face visible",
        "NW": "north-west isometric view, character facing away-left, back and left side visible, no face visible",
        "SE": "south-east isometric view, character facing toward-right, front and right side visible",
        "SW": "south-west isometric view, character facing toward-left, front and left side visible",
    }[direction]
    return (
        "LIT-ISO pixel character sprite, black mage wizard, compact isometric RPG sprite, "
        f"{direction_text}, tall black hat with tan patch, black robe with orange belt accents, "
        "one crooked staff with orange crystal, transparent background, no floor, no shadow, crisp pixel art"
    )


def main() -> int:
    parser = argparse.ArgumentParser(description="Build stronger black mage direction templates.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--source", default=str(DEFAULT_SOURCE))
    parser.add_argument("--output-root", default=str(DEFAULT_OUTPUT_ROOT))
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    source_path = project_root / args.source
    output_root = project_root / args.output_root
    output_root.mkdir(parents=True, exist_ok=True)

    templates: dict[str, Path] = {}
    for direction in DIRECTIONS:
        template = draw_template(direction)
        out = output_root / f"black_mage_{direction}_template_v2.png"
        template.save(out)
        templates[direction] = out

    sheet_path = output_root / "black_mage_direction_templates_v2_sheet.png"
    draw_sheet(project_root, source_path, templates, sheet_path)
    manifest = {
        "schema": "lit_iso.asset_forge.black_mage_direction_templates.v2",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "source": rel(project_root, source_path),
        "review_sheet": rel(project_root, sheet_path),
        "directions": DIRECTIONS,
        "scaffolds": {direction: rel(project_root, path) for direction, path in templates.items()},
        "generation_prompts": [
            {
                "direction": direction,
                "prompt": prompt_for(direction),
                "negative_prompt": "wrong direction, floor, base, shadow, spell effect, magic circle, duplicate, scene, prop, text, blurry, antialias haze",
                "status": "template_v2_prompt_contract_not_generated",
            }
            for direction in DIRECTIONS
        ],
        "status": "review_ready_generation_not_run",
        "notes": [
            "These templates are deterministic direction controls, not final character art.",
            "NE/NW intentionally hide the face and expose back/side robe silhouette.",
            "Use with low-denoise img2img plus original mage style reference.",
        ],
    }
    manifest_path = output_root / "black_mage_direction_templates_v2_manifest.json"
    write_json(manifest_path, manifest)
    print(
        json.dumps(
            {
                "manifest": rel(project_root, manifest_path),
                "review_sheet": rel(project_root, sheet_path),
                "templates": len(templates),
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
