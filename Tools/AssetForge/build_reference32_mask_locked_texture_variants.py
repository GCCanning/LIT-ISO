#!/usr/bin/env python3
"""Build local mask-locked Reference32 tile texture variants.

This is a no-credit generator path for tiles. It preserves the exact 32x32
alpha footprint and pixel mass of the supplied style-lock tiles, then generates
controlled material/palette/detail variation inside that locked mask.
"""

from __future__ import annotations

import argparse
import colorsys
import json
import math
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw


VARIANTS: list[dict[str, Any]] = [
    {
        "id": "source",
        "label": "source",
        "seed": 0,
        "note": "Unmodified locked reference tile.",
        "ramps": {},
    },
    {
        "id": "forest_moss",
        "label": "forest moss",
        "seed": 131,
        "note": "Cool saturated forest biome texture, silhouette locked.",
        "ramps": {
            "grass": ["#1f5b32", "#28723a", "#3e9a43", "#6dcc58", "#a7df6d"],
            "dirt": ["#351f24", "#55302b", "#754536", "#9a6242", "#c38a5b"],
            "stone": ["#26383a", "#40585a", "#627475", "#91a0a0", "#c1cbca"],
            "water": ["#10294a", "#164676", "#2377a0", "#4ab1ca", "#8dddeb"],
            "outline": ["#141921", "#1d242b", "#2b3135"],
        },
    },
    {
        "id": "plains_sun",
        "label": "plains sun",
        "seed": 293,
        "note": "Warmer plains palette with brighter grass and dry soil detail.",
        "ramps": {
            "grass": ["#315e2b", "#4b8a34", "#78b845", "#aad45b", "#d3ec7f"],
            "dirt": ["#3a2320", "#674033", "#945d3e", "#bd8151", "#e0ad70"],
            "stone": ["#394649", "#627073", "#8a9291", "#b5b8ac", "#dfdccb"],
            "water": ["#123a65", "#1f6395", "#2e91bd", "#5ec7df", "#a1edf4"],
            "outline": ["#171719", "#242322", "#35322f"],
        },
    },
    {
        "id": "screenshot_forest",
        "label": "screenshot forest",
        "seed": 347,
        "note": "Tuned from local LIT-ISO screenshot palette: strong but darker readable forest terrain.",
        "ramps": {
            "grass": ["#1b3d2d", "#285f3f", "#407a3c", "#50a84a", "#72cf5d"],
            "dirt": ["#352625", "#4f332c", "#6f4838", "#95634a", "#c08b5d"],
            "stone": ["#565e65", "#777f86", "#90969b", "#aeb0b7", "#c9c9d1"],
            "water": ["#102735", "#183d48", "#205963", "#2e8295", "#58bed0"],
            "outline": ["#11191d", "#1d2829", "#2b3533"],
        },
    },
    {
        "id": "screenshot_plains",
        "label": "screenshot plains",
        "seed": 419,
        "note": "Tuned from local LIT-ISO screenshot palette: brighter meadow terrain with warm dirt.",
        "ramps": {
            "grass": ["#24552f", "#38883a", "#50aa45", "#63c84d", "#8bdf62"],
            "dirt": ["#3b2723", "#604030", "#85533d", "#ad7148", "#d39c64"],
            "stone": ["#62686c", "#83888f", "#9a9ca3", "#b6b4bd", "#d5d1d8"],
            "water": ["#123040", "#184a5a", "#247083", "#35a4bb", "#70d6e3"],
            "outline": ["#141a1d", "#222927", "#333632"],
        },
    },
    {
        "id": "screenshot_balanced",
        "label": "screenshot balanced",
        "seed": 463,
        "note": "Screenshot grass/dirt with less cyan water and less lavender stone.",
        "ramps": {
            "grass": ["#1b3d2d", "#285f3f", "#407a3c", "#50a84a", "#72cf5d"],
            "dirt": ["#352625", "#4f332c", "#6f4838", "#95634a", "#c08b5d"],
            "stone": ["#35474a", "#5c7071", "#7f8e8d", "#a7b4ad", "#ccd2c8"],
            "water": ["#0e2442", "#143b64", "#1f5f86", "#328fb0", "#67c7d9"],
            "outline": ["#11191d", "#1d2829", "#2b3533"],
        },
    },
    {
        "id": "wetland_cool",
        "label": "wetland cool",
        "seed": 571,
        "note": "Damp low-saturation wetlands with cooler shadows.",
        "ramps": {
            "grass": ["#1f4738", "#2d6848", "#49875a", "#77a86d", "#abc982"],
            "dirt": ["#2b2428", "#493737", "#67504a", "#86705e", "#b49a7a"],
            "stone": ["#263440", "#40515e", "#65707a", "#8e9aa0", "#c2c9c9"],
            "water": ["#0d2944", "#164565", "#256f8a", "#4da5b1", "#91d9d9"],
            "outline": ["#131a20", "#1f282f", "#30383d"],
        },
    },
    {
        "id": "autumn_earth",
        "label": "autumn earth",
        "seed": 811,
        "note": "Warm autumn grass and earth tones, silhouette locked.",
        "ramps": {
            "grass": ["#4d4d25", "#77732d", "#a59638", "#d1b642", "#efd66a"],
            "dirt": ["#40201f", "#6b3328", "#934b31", "#bf7141", "#e59e61"],
            "stone": ["#384048", "#5c6670", "#858c90", "#b4b3a8", "#ddd4c3"],
            "water": ["#143055", "#1f5687", "#2c82ad", "#59bad4", "#a1e3ee"],
            "outline": ["#1a161b", "#282026", "#393030"],
        },
    },
]


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def repo_path(root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path)


def parse_hex(value: str) -> tuple[int, int, int]:
    value = value.lstrip("#")
    return (int(value[0:2], 16), int(value[2:4], 16), int(value[4:6], 16))


def luminance(rgb: tuple[int, int, int]) -> float:
    return 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2]


def mix(a: tuple[int, int, int], b: tuple[int, int, int], t: float) -> tuple[int, int, int]:
    t = max(0.0, min(1.0, t))
    return tuple(int(round(a[i] * (1.0 - t) + b[i] * t)) for i in range(3))


def color_distance(a: tuple[int, int, int], b: tuple[int, int, int]) -> float:
    return math.sqrt(sum((float(a[i]) - float(b[i])) ** 2 for i in range(3)))


def material_for(rgb: tuple[int, int, int], fallback: str) -> str:
    r, g, b = rgb
    h, s, v = colorsys.rgb_to_hsv(r / 255.0, g / 255.0, b / 255.0)
    if v < 0.16:
        return "outline"
    if s < 0.26:
        return "stone"
    if 0.48 <= h <= 0.72 and b > r + 25 and b > g + 5:
        return "water"
    if g > r + 8 and g >= b:
        return "grass"
    if r >= g - 5 and r > b + 10:
        return "dirt"
    return fallback


def source_stats(image: Image.Image) -> dict[str, float]:
    values = []
    rgba_image = image.convert("RGBA")
    data = rgba_image.get_flattened_data() if hasattr(rgba_image, "get_flattened_data") else rgba_image.getdata()
    for rgba in data:
        if rgba[3] > 0:
            values.append(luminance(rgba[:3]))
    if not values:
        return {"min": 0.0, "max": 255.0}
    return {"min": min(values), "max": max(values)}


def quantize_to_palette(rgb: tuple[int, int, int], palette: list[tuple[int, int, int]]) -> tuple[int, int, int]:
    return min(palette, key=lambda candidate: color_distance(rgb, candidate))


def deterministic_noise(x: int, y: int, seed: int, salt: int) -> float:
    value = (x * 73856093) ^ (y * 19349663) ^ (seed * 83492791) ^ (salt * 2654435761)
    value &= 0xFFFFFFFF
    return ((value >> 8) & 255) / 255.0


def ramp_color(
    ramp: list[tuple[int, int, int]],
    shade: float,
    x: int,
    y: int,
    seed: int,
    salt: int,
) -> tuple[int, int, int]:
    noise = deterministic_noise(x, y, seed, salt)
    if noise > 0.86:
        shade += 0.08
    elif noise < 0.14:
        shade -= 0.07
    shade = max(0.0, min(1.0, shade))
    scaled = shade * (len(ramp) - 1)
    index = int(math.floor(scaled))
    next_index = min(len(ramp) - 1, index + 1)
    return mix(ramp[index], ramp[next_index], scaled - index)


def build_variant(image: Image.Image, item: dict[str, Any], variant: dict[str, Any]) -> Image.Image:
    source = image.convert("RGBA")
    if variant["id"] == "source":
        return source.copy()

    material_fallback = str(item.get("material", "grass")).split("_")[0]
    ramps = {
        name: [parse_hex(color) for color in colors]
        for name, colors in variant["ramps"].items()
    }
    finite_palette = sorted({color for ramp in ramps.values() for color in ramp})
    stats = source_stats(source)
    lum_min = stats["min"]
    lum_span = max(1.0, stats["max"] - stats["min"])

    output = Image.new("RGBA", source.size, (0, 0, 0, 0))
    src = source.load()
    dst = output.load()
    salt = sum(ord(ch) for ch in str(item.get("id", "")))

    for y in range(source.height):
        for x in range(source.width):
            r, g, b, a = src[x, y]
            if a == 0:
                continue
            material = material_for((r, g, b), material_fallback)
            ramp = ramps.get(material) or ramps.get(material_fallback) or ramps["grass"]
            shade = (luminance((r, g, b)) - lum_min) / lum_span
            rgb = ramp_color(ramp, shade, x, y, int(variant["seed"]), salt)
            rgb = quantize_to_palette(rgb, finite_palette)
            dst[x, y] = (rgb[0], rgb[1], rgb[2], a)
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
            fill = (34, 38, 42, 255) if ((x // 8) + (y // 8)) % 2 == 0 else (24, 27, 30, 255)
            draw.rectangle([x, y, x + 7, y + 7], fill=fill)
    return checker


def draw_contact_sheet(project_root: Path, rows: list[dict[str, Any]], output_path: Path) -> None:
    cell_w = 150
    row_h = 120
    label_w = 150
    header_h = 70
    width = label_w + cell_w * len(VARIANTS)
    height = header_h + row_h * len(rows)
    sheet = Image.new("RGBA", (width, height), (16, 18, 20, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((12, 10), "Reference32 mask-locked texture variants", fill=(238, 242, 238, 255))
    draw.text((12, 30), "Local no-credit generator: exact alpha/mass lock, material-ramp pixel texture variants.", fill=(166, 176, 170, 255))
    for index, variant in enumerate(VARIANTS):
        draw.text((label_w + index * cell_w + 8, header_h - 24), variant["label"], fill=(212, 218, 212, 255))

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
            draw.text((x, y + 104), f"c{candidate['colors']} cov{candidate['alpha_coverage']}", fill=(156, 168, 162, 255))

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
                tile = Image.open(project_root / candidate["path"].replace("/", "\\")).convert("RGBA")
                x = origin_x + (gx - gy) * 16
                y = origin_y + (gx + gy) * 8
                sheet.alpha_composite(tile, (x, y))
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)


def main() -> int:
    parser = argparse.ArgumentParser(description="Build mask-locked local Reference32 texture variants.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument(
        "--reference-manifest",
        default="Assets/Generated/_Review/reference32_geometry_locked_tile_family_v1/reference32_tile_family_manifest.json",
    )
    parser.add_argument(
        "--output-root",
        default="Assets/Generated/_Review/reference32_mask_locked_texture_variants_v1",
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
            output = build_variant(source_image, item, variant)
            out_path = png_root / f"{item['id']}__{variant['id']}.png"
            output.save(out_path)
            variants.append(
                {
                    "variant_id": variant["id"],
                    "label": variant["label"],
                    "path": repo_path(project_root, out_path),
                    "width": output.width,
                    "height": output.height,
                    "colors": color_count(output),
                    "alpha_coverage": alpha_coverage(output),
                    "note": variant["note"],
                    "generator": "mask_locked_material_texture",
                    "alpha_locked_to": item["path"],
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

    contact_sheet = output_root / "reference32_mask_locked_texture_variants_contact_sheet.png"
    map_preview = output_root / "reference32_mask_locked_texture_variants_map_preview.png"
    draw_contact_sheet(project_root, rows, contact_sheet)
    draw_map_preview(project_root, rows, map_preview)
    manifest_path = output_root / "reference32_mask_locked_texture_variants_manifest.json"
    manifest = {
        "schema": "lit_iso.asset_forge.reference32_mask_locked_texture_variants.v1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "status": "review_only_not_unity_ready",
        "source_manifest": args.reference_manifest,
        "contact_sheet": repo_path(project_root, contact_sheet),
        "map_preview": repo_path(project_root, map_preview),
        "variant_ids": [variant["id"] for variant in VARIANTS],
        "rows": rows,
        "generator_contract": {
            "local_no_credit": True,
            "alpha_silhouette_locked": True,
            "changes_allowed": ["material palette", "shade ramp", "single-pixel texture detail"],
            "changes_forbidden": ["alpha mask edits", "tile footprint changes", "props", "characters", "scene composition"],
        },
        "notes": [
            "Use this as the current practical tile-generation path until diffusion can preserve 32x32 alpha geometry.",
            "Manual art approval and source/license verification are still required before dataset capture or Unity import.",
        ],
    }
    write_json(manifest_path, manifest)
    print(
        json.dumps(
            {
                "ok": True,
                "manifest": repo_path(project_root, manifest_path),
                "contact_sheet": repo_path(project_root, contact_sheet),
                "map_preview": repo_path(project_root, map_preview),
                "rows": len(rows),
                "variants": len(VARIANTS),
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
