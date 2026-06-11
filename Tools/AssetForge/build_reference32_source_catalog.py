#!/usr/bin/env python3
"""Build a categorized catalog from the supplied Reference32 tileset.

The supplied style-lock tileset is useful, but training/generation quality gets
worse if terrain, props, shore rocks, water, and decorative sprites are mixed
under one prompt. This script creates a review-only source catalog with role
metadata, contact sheets, and starter subsets so later LoRA/ControlNet work can
target the correct asset mode.
"""

from __future__ import annotations

import argparse
import json
import re
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw


SOURCE_ROOT = Path("Assets/Generated/_Review/style_lock_sources/isometric tileset/separated images")
OUTPUT_ROOT = Path("Assets/Generated/_Review/reference32_source_catalog_v1")


ROLE_RANGES = [
    {
        "role": "dirt_height_block",
        "asset_mode": "terrain_tile",
        "range": range(0, 11),
        "training_bucket": "tile_geometry",
        "description": "Brown dirt height blocks and compact raised terrain forms.",
    },
    {
        "role": "dirt_surface_detail",
        "asset_mode": "terrain_tile",
        "range": range(11, 22),
        "training_bucket": "tile_geometry",
        "description": "Dirt surface variants, plank-like ridges, and small dirt-to-growth transitions.",
    },
    {
        "role": "grass_height_block",
        "asset_mode": "terrain_tile",
        "range": list(range(22, 25)) + list(range(37, 41)),
        "training_bucket": "tile_geometry",
        "description": "Grass-top blocks, flat grass, and grass cliff/edge forms.",
    },
    {
        "role": "wood_log_asset",
        "asset_mode": "prop",
        "range": list(range(25, 29)) + list(range(48, 53)),
        "training_bucket": "prop_asset",
        "description": "Logs, cut wood, and stump-style prop references.",
    },
    {
        "role": "green_groundcover_asset",
        "asset_mode": "prop",
        "range": list(range(29, 37)) + list(range(41, 48)),
        "training_bucket": "prop_asset",
        "description": "Bushes, grass clumps, flowers, and groundcover sprites.",
    },
    {
        "role": "brown_rock_asset",
        "asset_mode": "prop",
        "range": range(53, 61),
        "training_bucket": "prop_asset",
        "description": "Brown rocks and small stacked cliff/stone props.",
    },
    {
        "role": "grey_stone_shore_tile",
        "asset_mode": "terrain_tile",
        "range": range(61, 82),
        "training_bucket": "tile_geometry",
        "description": "Grey stones, shore stones, and water-edge terrain tiles.",
    },
    {
        "role": "water_sparkle_detail",
        "asset_mode": "effect_or_overlay",
        "range": range(82, 86),
        "training_bucket": "overlay_effect",
        "description": "Tiny water sparkle/detail overlay pixels.",
    },
    {
        "role": "dark_water_tile",
        "asset_mode": "terrain_tile",
        "range": range(86, 104),
        "training_bucket": "tile_geometry",
        "description": "Dark blue isometric water tiles and water transition variants.",
    },
    {
        "role": "ice_water_tile",
        "asset_mode": "terrain_tile",
        "range": range(104, 115),
        "training_bucket": "tile_geometry",
        "description": "Bright ice/snow/water top tiles.",
    },
]

STARTER_SUBSETS = {
    "forest_plains_terrain_clean_core": {
        "description": "Decoration-free terrain candidates for grass, dirt, cliff, stone, and water/shore.",
        "tile_ids": [
            "tile_000",
            "tile_001",
            "tile_002",
            "tile_022",
            "tile_023",
            "tile_024",
            "tile_037",
            "tile_038",
            "tile_039",
            "tile_040",
            "tile_061",
            "tile_062",
            "tile_063",
            "tile_066",
            "tile_070",
            "tile_071",
            "tile_086",
            "tile_087",
            "tile_088",
            "tile_095",
            "tile_096",
        ],
    },
    "forest_plains_terrain_detail_secondary": {
        "description": "Secondary terrain details/transitions; useful later, but keep out of the first clean tile LoRA pass.",
        "tile_ids": [
            "tile_011",
            "tile_012",
            "tile_013",
            "tile_014",
            "tile_015",
            "tile_016",
            "tile_017",
            "tile_018",
            "tile_019",
            "tile_020",
            "tile_021",
            "tile_067",
            "tile_068",
            "tile_069",
            "tile_072",
            "tile_073",
            "tile_074",
            "tile_075",
            "tile_076",
            "tile_077",
            "tile_078",
            "tile_079",
            "tile_080",
            "tile_081",
        ],
    },
    "forest_plains_prop_core": {
        "description": "Separate prop/deco style references; do not mix into terrain tile prompts.",
        "tile_ids": [
            "tile_029",
            "tile_030",
            "tile_031",
            "tile_032",
            "tile_033",
            "tile_036",
            "tile_041",
            "tile_042",
            "tile_043",
            "tile_044",
            "tile_048",
            "tile_049",
            "tile_050",
            "tile_052",
            "tile_053",
            "tile_054",
            "tile_055",
            "tile_056",
            "tile_060",
        ],
    },
}

CLEAN_TERRAIN_IDS = set(STARTER_SUBSETS["forest_plains_terrain_clean_core"]["tile_ids"])
SECONDARY_TERRAIN_IDS = set(STARTER_SUBSETS["forest_plains_terrain_detail_secondary"]["tile_ids"])
PROP_CORE_IDS = set(STARTER_SUBSETS["forest_plains_prop_core"]["tile_ids"])


def tile_number(path: Path) -> int:
    match = re.search(r"tile_(\d+)\.png$", path.name)
    if not match:
        raise ValueError(f"Not a tile_NNN.png path: {path}")
    return int(match.group(1))


def tile_id(number: int) -> str:
    return f"tile_{number:03d}"


def rel(root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path)


def color_count(image: Image.Image) -> int:
    colors = image.convert("RGBA").getcolors(maxcolors=image.width * image.height)
    if colors is None:
        return image.width * image.height
    return len({color for _, color in colors if color[3] > 0})


def alpha_coverage(image: Image.Image) -> float:
    alpha = image.convert("RGBA").getchannel("A")
    histogram = alpha.histogram()
    return round((image.width * image.height - histogram[0]) / (image.width * image.height), 4)


def visible_bbox(image: Image.Image) -> list[int] | None:
    bbox = image.convert("RGBA").getchannel("A").getbbox()
    return list(bbox) if bbox else None


def role_for(number: int) -> dict[str, Any]:
    for role in ROLE_RANGES:
        if number in role["range"]:
            return role
    return {
        "role": "uncategorized",
        "asset_mode": "unknown",
        "training_bucket": "review_only",
        "description": "Uncategorized source tile.",
    }


def recommended_use(tile_id_value: str, role: str, asset_mode: str) -> dict[str, Any]:
    if tile_id_value in CLEAN_TERRAIN_IDS:
        return {
            "training_split": "tile_lora_core",
            "train_priority": 100,
            "include_in_first_tile_lora": True,
            "include_in_prop_lora": False,
            "reason": "Clean terrain geometry/style source with no prop object on top.",
        }
    if tile_id_value in SECONDARY_TERRAIN_IDS:
        return {
            "training_split": "tile_lora_secondary",
            "train_priority": 60,
            "include_in_first_tile_lora": False,
            "include_in_prop_lora": False,
            "reason": "Terrain detail/transition source; keep for second pass after clean geometry is stable.",
        }
    if tile_id_value in PROP_CORE_IDS or asset_mode == "prop":
        return {
            "training_split": "prop_lora_core",
            "train_priority": 80,
            "include_in_first_tile_lora": False,
            "include_in_prop_lora": True,
            "reason": "Prop/deco asset. Exclude from terrain tile LoRA.",
        }
    if asset_mode == "terrain_tile":
        return {
            "training_split": "tile_lora_later",
            "train_priority": 40,
            "include_in_first_tile_lora": False,
            "include_in_prop_lora": False,
            "reason": "Terrain candidate, but not in the clean starter selection.",
        }
    return {
        "training_split": "review_only",
        "train_priority": 0,
        "include_in_first_tile_lora": False,
        "include_in_prop_lora": False,
        "reason": f"{role} is not a first-pass terrain or prop training target.",
    }


def caption_for(item: dict[str, Any]) -> str:
    base = f"LIT-ISO Reference32 pixel art, {item['role'].replace('_', ' ')}, 32x32 isometric"
    if item["asset_mode"] == "terrain_tile":
        return f"{base} terrain tile, transparent background, no props on top"
    if item["asset_mode"] == "prop":
        return f"{base} prop asset, transparent background, bottom anchored"
    if item["asset_mode"] == "effect_or_overlay":
        return f"{base} water sparkle overlay, transparent background"
    return f"{base} source reference, transparent background"


def draw_checker(width: int, height: int) -> Image.Image:
    image = Image.new("RGBA", (width, height), (22, 25, 28, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, height, 8):
        for x in range(0, width, 8):
            if (x // 8 + y // 8) % 2 == 0:
                draw.rectangle([x, y, x + 7, y + 7], fill=(32, 36, 40, 255))
    return image


def draw_tile_cell(sheet: Image.Image, image_path: Path, x: int, y: int, scale: int = 2) -> None:
    image = Image.open(image_path).convert("RGBA")
    preview = image.resize((image.width * scale, image.height * scale), Image.Resampling.NEAREST)
    checker = draw_checker(preview.width, preview.height)
    checker.alpha_composite(preview)
    sheet.alpha_composite(checker, (x, y))


def make_role_sheet(project_root: Path, output_path: Path, role: str, items: list[dict[str, Any]]) -> None:
    columns = 8
    cell_w = 112
    cell_h = 104
    header_h = 50
    rows = max(1, (len(items) + columns - 1) // columns)
    sheet = Image.new("RGBA", (columns * cell_w, header_h + rows * cell_h), (16, 18, 20, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((10, 10), role, fill=(238, 242, 238, 255))
    draw.text((10, 28), f"{len(items)} source tiles. Review-only catalog.", fill=(158, 170, 164, 255))
    for index, item in enumerate(items):
        x = (index % columns) * cell_w + 14
        y = header_h + (index // columns) * cell_h + 8
        draw_tile_cell(sheet, project_root / item["path"].replace("/", "\\"), x, y, scale=2)
        draw.text((x, y + 68), item["id"], fill=(222, 228, 222, 255))
        draw.text((x, y + 82), f"c{item['color_count']} cov{item['alpha_coverage']}", fill=(148, 160, 154, 255))
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)


def make_subset_sheet(project_root: Path, output_path: Path, subset_name: str, items: list[dict[str, Any]]) -> None:
    columns = 7
    cell_w = 120
    cell_h = 108
    header_h = 54
    rows = max(1, (len(items) + columns - 1) // columns)
    sheet = Image.new("RGBA", (columns * cell_w, header_h + rows * cell_h), (15, 18, 20, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((10, 10), subset_name, fill=(238, 242, 238, 255))
    draw.text((10, 30), "Selected source-lock candidates for controlled training/review.", fill=(158, 170, 164, 255))
    for index, item in enumerate(items):
        x = (index % columns) * cell_w + 16
        y = header_h + (index // columns) * cell_h + 8
        draw_tile_cell(sheet, project_root / item["path"].replace("/", "\\"), x, y, scale=2)
        draw.text((x, y + 68), f"{item['id']} {item['role'][:18]}", fill=(222, 228, 222, 255))
        draw.text((x, y + 82), item["asset_mode"], fill=(148, 160, 154, 255))
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)


def main() -> int:
    parser = argparse.ArgumentParser(description="Build Reference32 source catalog review sheets.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--source-root", default=str(SOURCE_ROOT))
    parser.add_argument("--output-root", default=str(OUTPUT_ROOT))
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    source_root = project_root / args.source_root
    output_root = project_root / args.output_root
    sheets_root = output_root / "contact_sheets"
    output_root.mkdir(parents=True, exist_ok=True)

    source_paths = sorted(source_root.glob("tile_*.png"), key=tile_number)
    items: list[dict[str, Any]] = []
    by_role: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for path in source_paths:
        number = tile_number(path)
        image = Image.open(path).convert("RGBA")
        role = role_for(number)
        id_value = tile_id(number)
        recommendation = recommended_use(id_value, role["role"], role["asset_mode"])
        item = {
            "id": id_value,
            "source_file": path.name,
            "path": rel(project_root, path),
            "width": image.width,
            "height": image.height,
            "visible_bbox": visible_bbox(image),
            "color_count": color_count(image),
            "alpha_coverage": alpha_coverage(image),
            "role": role["role"],
            "asset_mode": role["asset_mode"],
            "training_bucket": role["training_bucket"],
            "description": role["description"],
            "caption": "",
            "recommendation": recommendation,
            "status": "source_lock_reference_review_only",
        }
        item["caption"] = caption_for(item)
        items.append(item)
        by_role[item["role"]].append(item)

    role_sheets: dict[str, str] = {}
    for role, role_items in sorted(by_role.items()):
        sheet_path = sheets_root / f"{role}.png"
        make_role_sheet(project_root, sheet_path, role, role_items)
        role_sheets[role] = rel(project_root, sheet_path)

    subsets: dict[str, Any] = {}
    item_by_id = {item["id"]: item for item in items}
    for subset_name, subset in STARTER_SUBSETS.items():
        selected = [item_by_id[item_id] for item_id in subset["tile_ids"] if item_id in item_by_id]
        sheet_path = output_root / f"{subset_name}_contact_sheet.png"
        make_subset_sheet(project_root, sheet_path, subset_name, selected)
        subsets[subset_name] = {
            "description": subset["description"],
            "contact_sheet": rel(project_root, sheet_path),
            "count": len(selected),
            "items": selected,
        }

    manifest = {
        "schema": "lit_iso.asset_forge.reference32_source_catalog.v1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "status": "review_only_not_unity_ready",
        "source_root": rel(project_root, source_root),
        "output_root": rel(project_root, output_root),
        "total_tiles": len(items),
        "role_counts": {role: len(role_items) for role, role_items in sorted(by_role.items())},
        "role_sheets": role_sheets,
        "starter_subsets": subsets,
        "training_plan": {
            "tile_lora_core_count": len([item for item in items if item["recommendation"]["training_split"] == "tile_lora_core"]),
            "tile_lora_secondary_count": len([item for item in items if item["recommendation"]["training_split"] == "tile_lora_secondary"]),
            "prop_lora_core_count": len([item for item in items if item["recommendation"]["training_split"] == "prop_lora_core"]),
            "records": [
                {
                    "id": item["id"],
                    "path": item["path"],
                    "asset_mode": item["asset_mode"],
                    "role": item["role"],
                    "caption": item["caption"],
                    "training_split": item["recommendation"]["training_split"],
                    "train_priority": item["recommendation"]["train_priority"],
                    "include_in_first_tile_lora": item["recommendation"]["include_in_first_tile_lora"],
                    "include_in_prop_lora": item["recommendation"]["include_in_prop_lora"],
                }
                for item in items
            ],
        },
        "items": items,
        "notes": [
            "Terrain tiles and props are intentionally separated to avoid poisoning tile LoRA training with decorations.",
            "Use forest_plains_terrain_clean_core for first-pass tile geometry/style training and ControlNet references.",
            "Use forest_plains_terrain_detail_secondary only after clean terrain geometry is stable.",
            "Use forest_plains_prop_core for props/decorations only.",
            "Review-only: do not promote into Unity runtime until art rights and final style approval are explicit.",
        ],
    }
    manifest_path = output_root / "reference32_source_catalog_manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps({"manifest": rel(project_root, manifest_path), "total_tiles": len(items), "roles": manifest["role_counts"]}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
