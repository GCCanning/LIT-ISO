#!/usr/bin/env python3
"""Prepare review artifacts for black mage isometric generation.

The source is front-facing; this tool normalizes it and creates a direction
contract/prompt pack. True NE/NW/SE/SW views still require AI, manual art, or
model-assisted repainting. The preview marks deterministic transforms as
scaffolds only.
"""

from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont


DEFAULT_SOURCE = Path("C:/Users/garyc/OneDrive/Desktop/PixelArt/BlackMage1.png")
DEFAULT_OUTPUT_ROOT = Path("Assets/Generated/_Review/black_mage_iso_style_lock_v1")
DIRECTIONS = ["NE", "NW", "SE", "SW"]


def load_font(size: int) -> ImageFont.ImageFont:
    for name in ("segoeui.ttf", "arial.ttf", "calibri.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def rel(project_root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(project_root.resolve()).as_posix()
    except ValueError:
        return str(path).replace("\\", "/")


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int] | None:
    return image.convert("RGBA").split()[-1].getbbox()


def normalize_sprite(source: Image.Image, size: tuple[int, int], fill: float = 0.82) -> Image.Image:
    rgba = source.convert("RGBA")
    bbox = alpha_bbox(rgba)
    if not bbox:
        return Image.new("RGBA", size, (0, 0, 0, 0))
    crop = rgba.crop(bbox)
    max_w = int(size[0] * fill)
    max_h = int(size[1] * fill)
    scale = min(max_w / crop.width, max_h / crop.height)
    resized = crop.resize((max(1, int(crop.width * scale)), max(1, int(crop.height * scale))), Image.Resampling.NEAREST)
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    x = (size[0] - resized.width) // 2
    y = size[1] - resized.height - 6
    canvas.alpha_composite(resized, (x, max(0, y)))
    return canvas


def scaffold_direction(base: Image.Image, direction: str) -> Image.Image:
    # This is explicitly not final art; it gives scale/anchor references only.
    image = base.copy()
    if direction in {"NW", "SW"}:
        image = image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    if direction in {"NE", "NW"}:
        canvas = Image.new("RGBA", image.size, (0, 0, 0, 0))
        shifted = image.crop((0, 0, image.width, image.height - 4))
        canvas.alpha_composite(shifted, (0, 0))
        image = canvas
    else:
        canvas = Image.new("RGBA", image.size, (0, 0, 0, 0))
        shifted = image.crop((0, 4, image.width, image.height))
        canvas.alpha_composite(shifted, (0, 4))
        image = canvas
    return image


def draw_review_sheet(project_root: Path, output_root: Path, source: Image.Image, normalized: Image.Image, scaffolds: dict[str, str]) -> str:
    cell_w = 150
    cell_h = 170
    width = cell_w * 6
    height = 240
    sheet = Image.new("RGBA", (width, height), (16, 18, 20, 255))
    draw = ImageDraw.Draw(sheet)
    title = load_font(18)
    small = load_font(10)
    draw.text((12, 12), "Black Mage isometric generation review v1", fill=(238, 238, 230, 255), font=title)
    draw.text((12, 38), "Source is front-facing. Direction cells are scale/anchor scaffolds, not final art.", fill=(190, 198, 186, 255), font=small)

    def paste_cell(index: int, label: str, image: Image.Image, scale: int = 1) -> None:
        x = index * cell_w + 10
        y = 74
        draw.rectangle((x, y, x + 128, y + 128), fill=(26, 29, 32, 255), outline=(58, 66, 60, 255))
        display = image
        if scale != 1:
            display = image.resize((image.width * scale, image.height * scale), Image.Resampling.NEAREST)
        px = x + (128 - display.width) // 2
        py = y + (128 - display.height) // 2
        sheet.alpha_composite(display, (px, py))
        draw.text((x, y + 136), label, fill=(210, 218, 205, 255), font=small)

    paste_cell(0, "source crop scale", normalize_sprite(source, (64, 96), 0.9), 1)
    paste_cell(1, "128 anchor", normalized, 1)
    for idx, direction in enumerate(DIRECTIONS, start=2):
        with Image.open(project_root / scaffolds[direction]) as image:
            paste_cell(idx, f"{direction} scaffold", image.convert("RGBA"), 1)

    out = output_root / "black_mage_iso_review_sheet.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out)
    return rel(project_root, out)


def prompt_for(direction: str) -> str:
    direction_text = {
        "NE": "north-east isometric view, character facing away-right, back and right side visible",
        "NW": "north-west isometric view, character facing away-left, back and left side visible",
        "SE": "south-east isometric view, character facing toward-right, front and right side visible",
        "SW": "south-west isometric view, character facing toward-left, front and left side visible",
    }[direction]
    return (
        "LIT-ISO pixel character sprite, black mage wizard, compact isometric RPG sprite, "
        f"{direction_text}, preserve tall black hat with tan brim patch, brown hair, black robe with orange belt accents, "
        "dark gloves, crooked staff with orange crystal, transparent background, no floor, no shadow, no text, "
        "match supplied 32x32 isometric tileset/46x32 critter pixel density, crisp pixel art, bottom-center anchor"
    )


def main() -> int:
    parser = argparse.ArgumentParser(description="Build black mage isometric review artifacts.")
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--output-root", type=Path, default=DEFAULT_OUTPUT_ROOT)
    args = parser.parse_args()

    project_root = args.project_root.resolve()
    source_path = args.source.resolve()
    output_root = (project_root / args.output_root).resolve() if not args.output_root.is_absolute() else args.output_root.resolve()
    output_root.mkdir(parents=True, exist_ok=True)

    with Image.open(source_path) as image:
        source = image.convert("RGBA")
    normalized = normalize_sprite(source, (128, 128), 0.82)
    normalized_path = output_root / "black_mage_normalized_128.png"
    normalized.save(normalized_path)

    scaffolds: dict[str, str] = {}
    for direction in DIRECTIONS:
        image = scaffold_direction(normalized, direction)
        out = output_root / f"black_mage_{direction}_scaffold_not_final.png"
        image.save(out)
        scaffolds[direction] = rel(project_root, out)

    review_sheet = draw_review_sheet(project_root, output_root, source, normalized, scaffolds)
    prompts = [
        {
            "direction": direction,
            "prompt": prompt_for(direction),
            "negative_prompt": "front-facing only, side-scroller sprite, chibi front portrait, floor, base, shadow, text, duplicate, blurry, antialiased, smooth painting, 3D render",
            "status": "prompt_contract_not_generated",
        }
        for direction in DIRECTIONS
    ]
    manifest = {
        "schema": "lit_iso.asset_forge.black_mage_iso_review.v1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "source": rel(project_root, source_path),
        "source_bbox": list(alpha_bbox(source) or []),
        "normalized": rel(project_root, normalized_path),
        "review_sheet": review_sheet,
        "style_profile": "Tools/AssetForge/style_profiles/litiso_iso_reference_v1.json",
        "directions": DIRECTIONS,
        "scaffolds": scaffolds,
        "generation_prompts": prompts,
        "status": "review_ready_generation_not_run",
        "notes": [
            "True isometric directions require AI/manual repainting from the reference; scaffolds are not final art.",
            "Use NE/NW/SE/SW first because supplied critter pack is diagonal-isometric.",
            "Keep generated outputs out of Unity until human art approval.",
        ],
    }
    manifest_path = output_root / "black_mage_iso_review_manifest.json"
    with manifest_path.open("w", encoding="utf-8") as handle:
        json.dump(manifest, handle, indent=2)
        handle.write("\n")

    print(json.dumps({"manifest": rel(project_root, manifest_path), "review_sheet": review_sheet, "directions": DIRECTIONS}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
