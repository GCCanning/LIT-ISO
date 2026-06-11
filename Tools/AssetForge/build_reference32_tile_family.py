#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter


FAMILY = [
    {
        "id": "grass_flat",
        "label": "Grass flat",
        "material": "grass",
        "role": "terrain_top",
        "source_tile": "tile_022.png",
        "prompt": "single 32x32 2:1 isometric grass diamond terrain tile, bright moss green top, tiny darker pixel flecks, transparent corners, no props",
    },
    {
        "id": "dirt_flat",
        "label": "Dirt flat",
        "material": "dirt",
        "role": "terrain_top",
        "source_tile": "tile_000.png",
        "prompt": "single 32x32 2:1 isometric warm dirt terrain tile, brown top, sparse darker pixel flecks, transparent corners, no props",
    },
    {
        "id": "grass_cliff_edge",
        "label": "Grass cliff",
        "material": "grass_dirt",
        "role": "height_edge",
        "source_tile": "tile_040.png",
        "prompt": "single 32x32 isometric raised grass edge tile, bright green top face, exposed warm brown dirt side face, clean cliff edge, transparent corners",
    },
    {
        "id": "stone_flat",
        "label": "Stone flat",
        "material": "stone",
        "role": "terrain_top",
        "source_tile": "tile_061.png",
        "prompt": "single 32x32 isometric stone terrain tile, cool grey cobblestone cluster, clean pixel outline, transparent corners, no props",
    },
    {
        "id": "water_flat",
        "label": "Water flat",
        "material": "water",
        "role": "terrain_top",
        "source_tile": "tile_086.png",
        "prompt": "single 32x32 isometric dark blue water diamond tile, subtle pixel ripples, transparent corners, no shore objects",
    },
    {
        "id": "water_shore_stone",
        "label": "Water shore",
        "material": "stone_water",
        "role": "shore_transition",
        "source_tile": "tile_066.png",
        "prompt": "single 32x32 isometric shoreline tile, grey stones meeting dark blue water, crisp pixel edge, transparent corners, no props",
    },
]


NEGATIVE_PROMPT = (
    "tree, bush, flower, log, stump, item, character, creature, building, complete map, "
    "large scene, floor, text, watermark, realistic render, blurry, anti aliasing, "
    "smooth gradients, props on top, oversized border"
)


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def rel(root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path)


def ensure_rgba_32(path: Path) -> Image.Image:
    image = Image.open(path).convert("RGBA")
    if image.size != (32, 32):
        image = image.resize((32, 32), Image.Resampling.NEAREST)
    return image


def alpha_bbox(image: Image.Image) -> list[int] | None:
    box = image.getchannel("A").getbbox()
    return list(box) if box else None


def count_colors(image: Image.Image) -> int:
    colors = image.getcolors(maxcolors=32 * 32)
    if colors is None:
        return 32 * 32
    return len({color for _, color in colors if len(color) == 4 and color[3] > 0})


def make_control_images(image: Image.Image, out_dir: Path, tile_id: str) -> dict[str, str]:
    alpha = image.getchannel("A")
    big_alpha = alpha.resize((512, 512), Image.Resampling.NEAREST)
    mask_path = out_dir / f"{tile_id}_alpha_mask_512.png"
    big_alpha.save(mask_path)

    edge = big_alpha.filter(ImageFilter.FIND_EDGES).point(lambda value: 255 if value >= 16 else 0)
    edge_path = out_dir / f"{tile_id}_edge_hint_512.png"
    edge.save(edge_path)

    rgb = Image.new("RGB", image.size, (0, 0, 0))
    rgb.paste(image.convert("RGB"), mask=alpha)
    color_big = rgb.resize((512, 512), Image.Resampling.NEAREST)
    color_hint_path = out_dir / f"{tile_id}_nearest_color_hint_512.png"
    color_big.save(color_hint_path)

    return {
        "alpha_mask_512": str(mask_path),
        "edge_hint_512": str(edge_path),
        "nearest_color_hint_512": str(color_hint_path),
    }


def draw_checker(size: tuple[int, int], cell: int = 8) -> Image.Image:
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], cell):
        for x in range(0, size[0], cell):
            fill = (42, 47, 52, 255) if ((x // cell) + (y // cell)) % 2 == 0 else (30, 34, 38, 255)
            draw.rectangle([x, y, x + cell - 1, y + cell - 1], fill=fill)
    return image


def make_contact(root: Path, records: list[dict], output_path: Path) -> None:
    scale = 4
    cell_w = 180
    cell_h = 168
    sheet = Image.new("RGBA", (cell_w * len(records), cell_h), (18, 21, 24, 255))
    draw = ImageDraw.Draw(sheet)
    for index, record in enumerate(records):
        x = index * cell_w
        draw.rectangle([x, 0, x + cell_w - 1, cell_h - 1], outline=(74, 84, 92, 255))
        checker = draw_checker((128, 128), 8)
        image = Image.open(root / record["path"]).convert("RGBA").resize((128, 128), Image.Resampling.NEAREST)
        checker.alpha_composite(image, (0, 0))
        sheet.alpha_composite(checker, (x + 26, 10))
        draw.text((x + 10, 142), record["id"][:24], fill=(235, 240, 245, 255))
        draw.text((x + 10, 154), f"{record['source_tile']} | colors {record['color_count']}", fill=(170, 185, 190, 255))
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)


def make_map_preview(root: Path, records: list[dict], output_path: Path) -> None:
    lookup = {record["id"]: Image.open(root / record["path"]).convert("RGBA") for record in records}
    tile_scale = 3
    cell = 32 * tile_scale
    canvas = Image.new("RGBA", (cell * 6, cell * 5), (22, 26, 30, 255))

    def place(tile_id: str, gx: int, gy: int) -> None:
        tile = lookup[tile_id].resize((cell, cell), Image.Resampling.NEAREST)
        x = (gx - gy) * 48 + 220
        y = (gx + gy) * 24 + 18
        canvas.alpha_composite(tile, (x, y))

    for gy in range(5):
        for gx in range(5):
            place("grass_flat", gx, gy)
    for pos in [(1, 1), (2, 1), (3, 2)]:
        place("dirt_flat", *pos)
    for pos in [(0, 3), (1, 3), (2, 3)]:
        place("water_flat", *pos)
    place("water_shore_stone", 2, 2)
    place("stone_flat", 4, 1)
    place("grass_cliff_edge", 4, 3)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output_path)


def main() -> int:
    parser = argparse.ArgumentParser(description="Build a review-only 32x32 source-locked tile family and ControlNet hints.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument(
        "--source-root",
        default="Assets/Generated/_Review/style_lock_sources/isometric tileset/separated images",
    )
    parser.add_argument(
        "--out-root",
        default="Assets/Generated/_Review/reference32_geometry_locked_tile_family_v1",
    )
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    source_root = (project_root / args.source_root).resolve()
    out_root = (project_root / args.out_root).resolve()
    png_dir = out_root / "png"
    control_dir = out_root / "control"
    png_dir.mkdir(parents=True, exist_ok=True)
    control_dir.mkdir(parents=True, exist_ok=True)

    if not source_root.exists():
        raise FileNotFoundError(f"Missing source tile root: {source_root}")

    records: list[dict] = []
    for item in FAMILY:
        source_path = source_root / item["source_tile"]
        if not source_path.exists():
            raise FileNotFoundError(f"Missing source tile for {item['id']}: {source_path}")
        image = ensure_rgba_32(source_path)
        out_path = png_dir / f"{item['id']}.png"
        image.save(out_path)
        controls = make_control_images(image, control_dir, item["id"])
        record = {
            "id": item["id"],
            "label": item["label"],
            "material": item["material"],
            "role": item["role"],
            "source_tile": item["source_tile"],
            "source_path": rel(project_root, source_path),
            "path": rel(project_root, out_path),
            "width": image.width,
            "height": image.height,
            "alpha_bbox": alpha_bbox(image),
            "alpha_coverage": round((32 * 32 - image.getchannel("A").histogram()[0]) / (32 * 32), 4),
            "color_count": count_colors(image),
            "prompt": item["prompt"],
            "negative_prompt": NEGATIVE_PROMPT,
            "control": {key: rel(project_root, Path(value)) for key, value in controls.items()},
            "status": "review_only_source_locked_reference",
        }
        records.append(record)

    contact_path = out_root / "reference32_tile_family_contact_sheet.png"
    make_contact(project_root, records, contact_path)
    map_preview_path = out_root / "reference32_tile_family_map_preview.png"
    make_map_preview(project_root, records, map_preview_path)

    manifest = {
        "schema": "lit_iso.asset_forge.reference32_geometry_locked_tile_family.v1",
        "generated_utc": utc_now(),
        "status": "review_only_not_unity_imported",
        "source_root": rel(project_root, source_root),
        "out_root": rel(project_root, out_root),
        "contact_sheet": rel(project_root, contact_path),
        "map_preview": rel(project_root, map_preview_path),
        "tile_count": len(records),
        "family_requirements": [
            "grass",
            "dirt",
            "edge/cliff",
            "stone",
            "water/shore",
            "decoration-free",
        ],
        "generation_recommendation": {
            "purpose": "Use these exact 32x32 tiles as geometry/style locks for the next AI pass.",
            "model": "SD1.5 pixel checkpoint through ComfyUI",
            "lora": "litiso_iso_reference_tile_style_v1_final.safetensors",
            "lora_strength_start": 0.35,
            "controlnet": "lineart/canny from edge_hint_512, optional img2img from nearest_color_hint_512",
            "denoise_range": [0.22, 0.42],
            "cfg_range": [5.0, 7.0],
            "reason": "The prompt-only LoRA matrix produced object-like blocks. Geometry must be locked before style variation.",
        },
        "items": records,
    }
    manifest_path = out_root / "reference32_tile_family_manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")

    print(json.dumps({
        "ok": True,
        "manifest": rel(project_root, manifest_path),
        "contact_sheet": rel(project_root, contact_path),
        "map_preview": rel(project_root, map_preview_path),
        "tiles": len(records),
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
