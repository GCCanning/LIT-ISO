#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont


DEFAULT_MANIFEST = Path("Assets/Generated/_Review/greenwake_geometry_derived_v7/derived_geometry_manifest.json")
DEFAULT_OUTPUT_NAME = "derived_geometry_showcase_13x13.png"
DEFAULT_LABEL_NAME = "derived_geometry_showcase_manifest.json"


if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


def repo_path(project_root: Path, value: str | Path) -> Path:
    path = Path(str(value).replace("/", "\\"))
    if path.is_absolute():
        return path
    return project_root / path


def repo_rel(project_root: Path, path: Path) -> str:
    return path.resolve().relative_to(project_root.resolve()).as_posix()


def load_font(size: int) -> ImageFont.ImageFont:
    for name in ("segoeui.ttf", "arial.ttf", "calibri.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def item_key(material: str, shape: str) -> tuple[str, str]:
    return material.lower(), shape.lower()


def load_tiles(project_root: Path, manifest_path: Path) -> tuple[dict[str, Any], dict[tuple[str, str], dict[str, Any]]]:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    tiles: dict[tuple[str, str], dict[str, Any]] = {}
    for item in manifest.get("items", []):
        material = str(item.get("material", "")).lower()
        shape = str(item.get("shape", "")).lower()
        if not material or not shape:
            continue
        path = repo_path(project_root, item["path"])
        if not path.exists():
            raise FileNotFoundError(path)
        tiles[item_key(material, shape)] = item
    return manifest, tiles


def pick(tiles: dict[tuple[str, str], dict[str, Any]], material: str, shape: str) -> dict[str, Any]:
    key = item_key(material, shape)
    if key not in tiles:
        raise KeyError(f"Missing tile material={material!r} shape={shape!r}")
    return tiles[key]


def classify_cell(x: int, y: int, size: int) -> tuple[str, str, str]:
    center = size // 2
    dx = x - center
    dy = y - center

    if abs(dx) <= 1 and abs(dy) <= 1:
        return "stone", "flat_top", "stone plaza"
    if (abs(dx) == 2 and abs(dy) <= 1) or (abs(dy) == 2 and abs(dx) <= 1):
        shape = transition_shape(dx, dy)
        return "grass_to_stone", shape, "grass-to-stone transition"

    if abs(dx - dy) <= 1 and -5 <= dx + dy <= 5:
        if abs(dx - dy) == 1:
            shape = "east_transition" if dx > dy else "west_transition"
            return "grass_to_path", shape, "path shoulder transition"
        return "path", "flat_top", "footpath"

    if (x <= 3 and y >= 7) or (x <= 2 and y >= 5):
        if x == 3 or y == 7:
            shape = transition_shape(dx, dy)
            return "grass_to_dirt", shape, "dirt edge transition"
        return "dirt", "flat_top", "tilled dirt patch"

    if (x >= 8 and y <= 4) or (x >= 10 and y <= 6):
        if x == 8 or y == 4:
            shape = transition_shape(dx, dy)
            return "grass_to_forest_floor", shape, "forest edge transition"
        return "forest_floor", "flat_top", "forest floor pocket"

    return "grass", "flat_top", "filled grass base"


def transition_shape(dx: int, dy: int) -> str:
    if dx >= 2 and dy <= -2:
        return "north_east_transition"
    if dx <= -2 and dy <= -1:
        return "north_west_transition"
    if dx >= 1 and dy >= 2:
        return "south_east_transition"
    if dx <= -2 and dy >= 1:
        return "south_west_transition"
    if dy < 0:
        return "north_transition"
    if dy > 0:
        return "south_transition"
    if dx > 0:
        return "east_transition"
    return "west_transition"


def edge_shape_for_cell(x: int, y: int, size: int) -> str:
    if x == 1:
        return "west_edge"
    if x == size - 2:
        return "east_edge"
    if y == 1:
        return "north_edge"
    if y == size - 2:
        return "south_edge"
    if x + y < size - 2:
        return "north_edge"
    return "south_edge"


def build_layout(size: int) -> list[dict[str, Any]]:
    placements: list[dict[str, Any]] = []
    raised_cells = {
        (1, 1), (2, 1), (3, 1), (9, 1), (10, 1), (11, 1),
        (1, 2), (11, 2), (1, 3), (11, 3),
        (1, 9), (1, 10), (1, 11), (2, 11), (3, 11),
        (9, 11), (10, 11), (11, 11),
    }
    for y in range(size):
        for x in range(size):
            material, shape, note = classify_cell(x, y, size)
            if (x, y) in raised_cells and material == "grass":
                shape = edge_shape_for_cell(x, y, size)
                note = "raised grass edge"
            placements.append({"grid": {"x": x, "y": y}, "material": material, "shape": shape, "note": note})
    return placements


def draw_label(
    draw: ImageDraw.ImageDraw,
    xy: tuple[int, int],
    text: str,
    font: ImageFont.ImageFont,
    fill: tuple[int, int, int, int] = (236, 241, 228, 255),
) -> None:
    x, y = xy
    bbox = draw.textbbox((x, y), text, font=font)
    pad = 6
    draw.rounded_rectangle(
        (bbox[0] - pad, bbox[1] - 4, bbox[2] + pad, bbox[3] + 5),
        radius=4,
        fill=(19, 24, 22, 224),
        outline=(87, 104, 80, 255),
    )
    draw.text((x, y), text, font=font, fill=fill)


def make_showcase(
    project_root: Path,
    manifest_path: Path,
    output_path: Path,
    label_manifest_path: Path,
    size: int,
) -> dict[str, Any]:
    source_manifest, tiles = load_tiles(project_root, manifest_path)
    placements = build_layout(size)
    tile_w = 64
    tile_h = 32
    sprite_h = max(int(item.get("height", 64)) for item in tiles.values())
    margin_x = 220
    margin_top = 116
    margin_bottom = 110
    width = (size + size) * tile_w // 2 + margin_x * 2
    height = (size + size) * tile_h // 2 + sprite_h + margin_top + margin_bottom
    origin_x = width // 2 - tile_w // 2
    origin_y = margin_top

    canvas = Image.new("RGBA", (width, height), (24, 31, 30, 255))
    draw = ImageDraw.Draw(canvas)
    title_font = load_font(24)
    label_font = load_font(15)
    small_font = load_font(12)

    draw.rectangle((0, 0, width, 64), fill=(17, 23, 22, 255))
    draw.text((24, 18), f"Greenwake derived tile showcase - {size}x{size} layout", fill=(238, 244, 229, 255), font=title_font)
    draw.text((24, 48), "Filled grass base with raised edges, footpath, dirt field, forest pocket, and stone plaza transitions.", fill=(170, 184, 166, 255), font=small_font)

    resolved: list[dict[str, Any]] = []
    for placement in placements:
        x = placement["grid"]["x"]
        y = placement["grid"]["y"]
        item = pick(tiles, placement["material"], placement["shape"])
        image_path = repo_path(project_root, item["path"])
        with Image.open(image_path) as image:
            sprite = image.convert("RGBA")
        px = origin_x + (x - y) * tile_w // 2
        py = origin_y + (x + y) * tile_h // 2 - (sprite.height - tile_h)
        canvas.alpha_composite(sprite, (px, py))
        resolved.append({
            **placement,
            "tile": item["name"],
            "path": item["path"],
            "pixel": {"x": px, "y": py},
        })

    label_specs = [
        {"id": "raised_edges", "text": "raised grass edges", "grid": {"x": 1, "y": 1}, "offset": {"x": -166, "y": -48}},
        {"id": "path", "text": "path corridor", "grid": {"x": 5, "y": 6}, "offset": {"x": -104, "y": -58}},
        {"id": "stone", "text": "stone plaza", "grid": {"x": 7, "y": 5}, "offset": {"x": 94, "y": -38}},
        {"id": "dirt", "text": "dirt field", "grid": {"x": 2, "y": 9}, "offset": {"x": -158, "y": 18}},
        {"id": "forest", "text": "forest floor", "grid": {"x": 10, "y": 3}, "offset": {"x": 48, "y": -22}},
    ]
    for label in label_specs:
        gx = label["grid"]["x"]
        gy = label["grid"]["y"]
        x = origin_x + (gx - gy) * tile_w // 2 + label["offset"]["x"]
        y = origin_y + (gx + gy) * tile_h // 2 + label["offset"]["y"]
        draw_label(draw, (x, y), label["text"], label_font)
        label["pixel"] = {"x": x, "y": y}

    legend_x = width - 230
    legend_y = height - 96
    draw.rounded_rectangle((legend_x, legend_y, width - 24, height - 24), radius=6, fill=(17, 23, 22, 230), outline=(83, 98, 78, 255))
    legend = [("grass", "base"), ("path", "walk route"), ("dirt", "field"), ("stone", "plaza"), ("forest", "shade")]
    for index, (name, detail) in enumerate(legend):
        draw.text((legend_x + 12, legend_y + 10 + index * 13), f"{name}: {detail}", fill=(214, 223, 205, 255), font=small_font)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output_path)

    label_manifest = {
        "schema": "lit_iso.asset_forge.tile_showcase_preview.v1",
        "source_manifest": repo_rel(project_root, manifest_path),
        "source_schema": source_manifest.get("schema"),
        "output": repo_rel(project_root, output_path),
        "grid": {"width": size, "height": size, "projection": "isometric_2_to_1", "tile_width": tile_w, "tile_height": tile_h},
        "labels": label_specs,
        "placements": resolved,
    }
    label_manifest_path.write_text(json.dumps(label_manifest, indent=2) + "\n", encoding="utf-8")
    return label_manifest


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a large isometric review showcase from a derived tile geometry manifest.")
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--label-manifest", type=Path)
    parser.add_argument("--size", type=int, default=13, help="Grid size. Odd sizes work best; default 13.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    manifest_path = repo_path(project_root, args.manifest)
    if not manifest_path.exists():
        raise FileNotFoundError(manifest_path)
    output_path = repo_path(project_root, args.output) if args.output else manifest_path.parent / DEFAULT_OUTPUT_NAME
    label_manifest_path = repo_path(project_root, args.label_manifest) if args.label_manifest else manifest_path.parent / DEFAULT_LABEL_NAME

    result = make_showcase(project_root, manifest_path, output_path, label_manifest_path, args.size)
    print(json.dumps({
        "ok": True,
        "output": result["output"],
        "label_manifest": repo_rel(project_root, label_manifest_path),
        "placements": len(result["placements"]),
        "labels": len(result["labels"]),
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
