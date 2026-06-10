#!/usr/bin/env python3
"""Build a small review micro-pack from supplied style-lock tiles.

This creates controlled variants from exact source pixels so we can judge the
target style before training or AI generation. Outputs stay under _Review.
"""

from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageEnhance, ImageFont


DEFAULT_SOURCE_ROOT = Path("Assets/Generated/_Review/style_lock_sources/isometric tileset/separated images")
DEFAULT_OUTPUT_ROOT = Path("Assets/Generated/_Review/style_locked_micro_pack_v1")

TILE_SELECTIONS = [
    {"id": "dirt_block", "source": "tile_000.png", "label": "dirt block"},
    {"id": "grass_block", "source": "tile_022.png", "label": "grass block"},
    {"id": "grass_dirt_transition", "source": "tile_019.png", "label": "grass/dirt transition"},
    {"id": "water_tile", "source": "tile_086.png", "label": "water tile"},
    {"id": "stone_tile", "source": "tile_063.png", "label": "stone tile"},
]

VARIANTS = [
    {"id": "source", "hue": 0, "saturation": 1.0, "value": 1.0, "label": "source"},
    {"id": "spring", "hue": 5, "saturation": 1.08, "value": 1.04, "label": "spring"},
    {"id": "autumn", "hue": -13, "saturation": 1.06, "value": 0.96, "label": "autumn"},
    {"id": "wet", "hue": 6, "saturation": 0.9, "value": 0.78, "label": "wet"},
]


def load_font(size: int) -> ImageFont.ImageFont:
    for name in ("segoeui.ttf", "arial.ttf", "calibri.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def rel(project_root: Path, path: Path) -> str:
    return path.resolve().relative_to(project_root.resolve()).as_posix()


def hue_shift(image: Image.Image, degrees: int, saturation: float, value: float) -> Image.Image:
    rgba = image.convert("RGBA")
    if degrees:
        hsv = rgba.convert("HSV")
        h, s, v = hsv.split()
        shift = int(round(degrees / 360 * 255))
        h = h.point(lambda px: (px + shift) % 256)
        hsv = Image.merge("HSV", (h, s, v))
        rgba = hsv.convert("RGBA")
        rgba.putalpha(image.convert("RGBA").split()[-1])
    if saturation != 1.0:
        rgba = ImageEnhance.Color(rgba).enhance(saturation)
    if value != 1.0:
        rgba = ImageEnhance.Brightness(rgba).enhance(value)
    rgba.putalpha(image.convert("RGBA").split()[-1])
    return rgba


def save_contact_sheet(
    project_root: Path,
    entries: list[dict[str, Any]],
    output_path: Path,
) -> None:
    cell = 112
    header = 78
    label_h = 20
    cols = len(VARIANTS)
    rows = len(TILE_SELECTIONS)
    sheet = Image.new("RGBA", (cols * cell + 170, header + rows * (cell + label_h)), (16, 18, 20, 255))
    draw = ImageDraw.Draw(sheet)
    title = load_font(18)
    small = load_font(10)
    draw.text((12, 12), "Style-locked 5-tile micro-pack v1", fill=(238, 238, 230, 255), font=title)
    draw.text((12, 36), "Exact source pixels plus controlled hue/value variants. Review-only.", fill=(180, 188, 178, 255), font=small)
    for col, variant in enumerate(VARIANTS):
        draw.text((170 + col * cell + 8, header - 18), variant["label"], fill=(206, 212, 201, 255), font=small)
    for row, selection in enumerate(TILE_SELECTIONS):
        y = header + row * (cell + label_h)
        draw.text((10, y + 28), selection["label"], fill=(214, 220, 210, 255), font=small)
        for col, variant in enumerate(VARIANTS):
            entry = next(item for item in entries if item["tile_id"] == selection["id"] and item["variant_id"] == variant["id"])
            with Image.open(project_root / entry["path"]) as image:
                sprite = image.convert("RGBA")
            display = sprite.resize((64, 64), Image.Resampling.NEAREST)
            x = 170 + col * cell + 10
            checker = Image.new("RGBA", (64, 64), (22, 24, 26, 255))
            cd = ImageDraw.Draw(checker)
            for cy in range(0, 64, 8):
                for cx in range(0, 64, 8):
                    if (cx // 8 + cy // 8) % 2 == 0:
                        cd.rectangle((cx, cy, cx + 7, cy + 7), fill=(30, 34, 36, 255))
            sheet.alpha_composite(checker, (x, y))
            sheet.alpha_composite(display, (x, y))
            draw.text((x, y + 66), entry["file_name"][:24], fill=(160, 168, 158, 255), font=small)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)


def main() -> int:
    parser = argparse.ArgumentParser(description="Build a review micro-pack from supplied style-lock tiles.")
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--source-root", type=Path, default=DEFAULT_SOURCE_ROOT)
    parser.add_argument("--output-root", type=Path, default=DEFAULT_OUTPUT_ROOT)
    args = parser.parse_args()

    project_root = args.project_root.resolve()
    source_root = (project_root / args.source_root).resolve() if not args.source_root.is_absolute() else args.source_root.resolve()
    output_root = (project_root / args.output_root).resolve() if not args.output_root.is_absolute() else args.output_root.resolve()
    tile_root = output_root / "tiles"
    tile_root.mkdir(parents=True, exist_ok=True)

    entries: list[dict[str, Any]] = []
    for selection in TILE_SELECTIONS:
        source_path = source_root / selection["source"]
        if not source_path.exists():
            raise FileNotFoundError(source_path)
        with Image.open(source_path) as image:
            source = image.convert("RGBA")
        for variant in VARIANTS:
            output = hue_shift(source, variant["hue"], variant["saturation"], variant["value"])
            file_name = f"{selection['id']}_{variant['id']}.png"
            out_path = tile_root / file_name
            output.save(out_path)
            entries.append(
                {
                    "tile_id": selection["id"],
                    "label": selection["label"],
                    "variant_id": variant["id"],
                    "variant_label": variant["label"],
                    "source": rel(project_root, source_path),
                    "path": rel(project_root, out_path),
                    "file_name": file_name,
                    "width": output.width,
                    "height": output.height,
                    "license_status": "unknown_user_supplied_reference_until_documented",
                    "review_status": "style_reference_only",
                }
            )

    contact_sheet = output_root / "style_locked_micro_pack_contact_sheet.png"
    save_contact_sheet(project_root, entries, contact_sheet)
    manifest = {
        "schema": "lit_iso.asset_forge.style_locked_micro_pack.v1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "status": "review_only_not_unity_ready",
        "source_root": rel(project_root, source_root),
        "style_profile": "Tools/AssetForge/style_profiles/litiso_iso_reference_v1.json",
        "contact_sheet": rel(project_root, contact_sheet),
        "tile_count": len(TILE_SELECTIONS),
        "variant_count": len(VARIANTS),
        "entries": entries,
        "notes": [
            "These variants derive from supplied reference pixels and are review-only until licensing/training rights are documented.",
            "Use this as the visual target for generated/original tile candidates.",
            "Do not promote to Unity runtime assets without explicit art approval.",
        ],
    }
    manifest_path = output_root / "style_locked_micro_pack_manifest.json"
    with manifest_path.open("w", encoding="utf-8") as handle:
        json.dump(manifest, handle, indent=2)
        handle.write("\n")
    print(json.dumps({"manifest": rel(project_root, manifest_path), "contact_sheet": rel(project_root, contact_sheet), "entries": len(entries)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
