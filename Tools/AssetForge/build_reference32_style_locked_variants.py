#!/usr/bin/env python3
"""Build deterministic Reference32 tile variants from the locked style pack.

This is intentionally local and review-only: it preserves the supplied 32x32
tile silhouettes and pixel structure while producing controlled palette
variants for art-direction review and future training captions.
"""

from __future__ import annotations

import argparse
import colorsys
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw


VARIANTS = [
    {
        "id": "source",
        "label": "source",
        "hue_shift": 0.0,
        "saturation": 1.0,
        "value": 1.0,
        "note": "Unmodified locked reference tile.",
    },
    {
        "id": "litiso_green",
        "label": "LIT-ISO green",
        "hue_shift": 0.012,
        "saturation": 1.08,
        "value": 1.05,
        "note": "Slightly brighter overworld/plains bias.",
    },
    {
        "id": "deep_forest",
        "label": "deep forest",
        "hue_shift": -0.018,
        "saturation": 1.06,
        "value": 0.82,
        "note": "Cooler shaded forest bias.",
    },
    {
        "id": "rainy",
        "label": "rainy",
        "hue_shift": 0.028,
        "saturation": 0.84,
        "value": 0.86,
        "note": "Lower-saturation wet-weather bias.",
    },
    {
        "id": "autumn",
        "label": "autumn",
        "hue_shift": -0.085,
        "saturation": 1.12,
        "value": 0.96,
        "note": "Warmer seasonal biome bias.",
    },
]


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def rel(root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path)


def clamp01(value: float) -> float:
    return max(0.0, min(1.0, value))


def transform_pixel(
    rgba: tuple[int, int, int, int],
    hue_shift: float,
    saturation: float,
    value: float,
) -> tuple[int, int, int, int]:
    r, g, b, a = rgba
    if a == 0:
        return rgba
    h, s, v = colorsys.rgb_to_hsv(r / 255.0, g / 255.0, b / 255.0)
    h = (h + hue_shift) % 1.0
    s = clamp01(s * saturation)
    v = clamp01(v * value)
    nr, ng, nb = colorsys.hsv_to_rgb(h, s, v)
    return (int(round(nr * 255)), int(round(ng * 255)), int(round(nb * 255)), a)


def transform_tile(image: Image.Image, variant: dict[str, Any]) -> Image.Image:
    source = image.convert("RGBA")
    output = Image.new("RGBA", source.size, (0, 0, 0, 0))
    src = source.load()
    dst = output.load()
    for y in range(source.height):
        for x in range(source.width):
            dst[x, y] = transform_pixel(
                src[x, y],
                float(variant["hue_shift"]),
                float(variant["saturation"]),
                float(variant["value"]),
            )
    return output


def color_count(image: Image.Image) -> int:
    colors = image.convert("RGBA").getcolors(maxcolors=image.width * image.height)
    if colors is None:
        return image.width * image.height
    return len({color for _, color in colors if color[3] > 0})


def alpha_coverage(image: Image.Image) -> float:
    alpha = image.convert("RGBA").getchannel("A")
    histogram = alpha.histogram()
    return round((image.width * image.height - histogram[0]) / (image.width * image.height), 4)


def draw_checker(size: tuple[int, int]) -> Image.Image:
    checker = Image.new("RGBA", size, (24, 27, 30, 255))
    draw = ImageDraw.Draw(checker)
    for y in range(0, size[1], 8):
        for x in range(0, size[0], 8):
            if (x // 8 + y // 8) % 2 == 0:
                draw.rectangle([x, y, x + 7, y + 7], fill=(34, 38, 42, 255))
    return checker


def draw_contact_sheet(project_root: Path, rows: list[dict[str, Any]], output_path: Path) -> None:
    cell_w = 142
    row_h = 118
    label_w = 150
    header_h = 68
    width = label_w + cell_w * len(VARIANTS)
    height = header_h + row_h * len(rows)
    sheet = Image.new("RGBA", (width, height), (16, 18, 20, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((12, 10), "Reference32 style-locked variants v1", fill=(238, 242, 238, 255))
    draw.text((12, 30), "Local deterministic palette variants. Review/training-prep only.", fill=(166, 176, 170, 255))
    for index, variant in enumerate(VARIANTS):
        draw.text((label_w + index * cell_w + 8, header_h - 22), variant["label"], fill=(212, 218, 212, 255))

    for row_index, row in enumerate(rows):
        y = header_h + row_index * row_h
        draw.rectangle([0, y, width - 1, y + row_h - 1], outline=(64, 72, 76, 255))
        draw.text((10, y + 20), row["id"], fill=(230, 236, 230, 255))
        draw.text((10, y + 38), row["source_tile"], fill=(150, 162, 156, 255))
        for col_index, candidate in enumerate(row["variants"]):
            image_path = project_root / candidate["path"].replace("/", "\\")
            image = Image.open(image_path).convert("RGBA")
            preview = image.resize((96, 96), Image.Resampling.NEAREST)
            x = label_w + col_index * cell_w + 12
            checker = draw_checker(preview.size)
            checker.alpha_composite(preview)
            sheet.alpha_composite(checker, (x, y + 8))
            draw.text((x, y + 102), f"c{candidate['colors']} cov{candidate['alpha_coverage']}", fill=(156, 168, 162, 255))

    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)


def find_variant(rows: list[dict[str, Any]], tile_id: str, variant_id: str) -> dict[str, Any]:
    row = next(item for item in rows if item["id"] == tile_id)
    return next(item for item in row["variants"] if item["variant_id"] == variant_id)


def draw_map_preview(project_root: Path, rows: list[dict[str, Any]], output_path: Path) -> None:
    cell_w = 210
    cell_h = 150
    width = cell_w * len(VARIANTS)
    height = cell_h + 34
    sheet = Image.new("RGBA", (width, height), (13, 16, 18, 255))
    draw = ImageDraw.Draw(sheet)

    layout = [
        ["grass_flat", "grass_flat", "grass_flat", "stone_flat", "grass_flat"],
        ["grass_flat", "dirt_flat", "dirt_flat", "grass_flat", "grass_flat"],
        ["grass_cliff_edge", "grass_flat", "water_shore_stone", "water_flat", "water_flat"],
        ["grass_flat", "grass_flat", "water_flat", "water_flat", "water_flat"],
        ["grass_flat", "stone_flat", "grass_flat", "dirt_flat", "grass_flat"],
    ]

    for variant_index, variant in enumerate(VARIANTS):
        origin_x = variant_index * cell_w + cell_w // 2 - 16
        origin_y = 38
        draw.text((variant_index * cell_w + 10, 10), variant["label"], fill=(218, 224, 218, 255))
        for gy, row in enumerate(layout):
            for gx, tile_id in enumerate(row):
                candidate = find_variant(rows, tile_id, variant["id"])
                image_path = project_root / candidate["path"].replace("/", "\\")
                tile = Image.open(image_path).convert("RGBA")
                x = origin_x + (gx - gy) * 16
                y = origin_y + (gx + gy) * 8
                sheet.alpha_composite(tile, (x, y))

    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)


def main() -> int:
    parser = argparse.ArgumentParser(description="Build deterministic Reference32 style-locked tile variants.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument(
        "--reference-manifest",
        default="Assets/Generated/_Review/reference32_geometry_locked_tile_family_v1/reference32_tile_family_manifest.json",
    )
    parser.add_argument(
        "--output-root",
        default="Assets/Generated/_Review/reference32_style_locked_variants_v1",
    )
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    reference_manifest = read_json(project_root / args.reference_manifest)
    output_root = project_root / args.output_root
    png_root = output_root / "png"
    png_root.mkdir(parents=True, exist_ok=True)

    rows: list[dict[str, Any]] = []
    for item in reference_manifest["items"]:
        source_path = project_root / item["path"].replace("/", "\\")
        source_image = Image.open(source_path).convert("RGBA")
        variants: list[dict[str, Any]] = []
        for variant in VARIANTS:
            output = transform_tile(source_image, variant)
            file_name = f"{item['id']}__{variant['id']}.png"
            out_path = png_root / file_name
            output.save(out_path)
            variants.append(
                {
                    "variant_id": variant["id"],
                    "label": variant["label"],
                    "path": rel(project_root, out_path),
                    "width": output.width,
                    "height": output.height,
                    "colors": color_count(output),
                    "alpha_coverage": alpha_coverage(output),
                    "note": variant["note"],
                }
            )
        rows.append(
            {
                "id": item["id"],
                "source_tile": item.get("source_tile", ""),
                "source_path": item["path"],
                "variants": variants,
            }
        )

    contact_sheet = output_root / "reference32_style_locked_variants_contact_sheet.png"
    draw_contact_sheet(project_root, rows, contact_sheet)
    map_preview = output_root / "reference32_style_locked_variants_map_preview.png"
    draw_map_preview(project_root, rows, map_preview)
    manifest = {
        "schema": "lit_iso.asset_forge.reference32_style_locked_variants.v1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "status": "review_only_not_unity_ready",
        "source_manifest": args.reference_manifest,
        "contact_sheet": rel(project_root, contact_sheet),
        "map_preview": rel(project_root, map_preview),
        "variant_ids": [variant["id"] for variant in VARIANTS],
        "rows": rows,
        "notes": [
            "This pack is deterministic and uses the locked Reference32 source pixels as style anchors.",
            "Use it for art-direction review and dataset augmentation planning.",
            "Do not promote to Unity runtime assets until licensing and art approval are confirmed.",
        ],
    }
    manifest_path = output_root / "reference32_style_locked_variants_manifest.json"
    write_json(manifest_path, manifest)
    print(
        json.dumps(
            {
                "manifest": rel(project_root, manifest_path),
                "contact_sheet": rel(project_root, contact_sheet),
                "map_preview": rel(project_root, map_preview),
                "rows": len(rows),
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
