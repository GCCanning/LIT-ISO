#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


TARGET_FAMILIES = [
    {"family": "earth_block", "reference_tiles": ["tile_000", "tile_001", "tile_004", "tile_008", "tile_017", "tile_018"]},
    {"family": "grass_top", "reference_tiles": ["tile_022", "tile_023", "tile_024", "tile_037", "tile_038", "tile_039", "tile_040"]},
    {"family": "stone_water", "reference_tiles": ["tile_061", "tile_066", "tile_070", "tile_071", "tile_077", "tile_078"]},
    {"family": "dark_water", "reference_tiles": ["tile_086", "tile_087", "tile_088", "tile_089", "tile_090", "tile_095"]},
    {"family": "ice", "reference_tiles": ["tile_104", "tile_105", "tile_106", "tile_107", "tile_108", "tile_109", "tile_114"]},
]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def load_font(size: int) -> ImageFont.ImageFont:
    for name in ("segoeui.ttf", "arial.ttf", "calibri.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def repo_rel(project_root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(project_root.resolve()).as_posix()
    except ValueError:
        return str(path.resolve()).replace("\\", "/")


def checkerboard(size: tuple[int, int], cell: int = 8) -> Image.Image:
    image = Image.new("RGBA", size, (22, 24, 28, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], cell):
        for x in range(0, size[0], cell):
            if (x // cell + y // cell) % 2 == 0:
                draw.rectangle((x, y, x + cell - 1, y + cell - 1), fill=(34, 37, 42, 255))
    return image


def index_inventory(project_root: Path, inventory_path: Path) -> dict[str, dict[str, Any]]:
    inventory = json.loads(inventory_path.read_text(encoding="utf-8-sig"))
    records = inventory.get("records", [])
    indexed: dict[str, dict[str, Any]] = {}
    for record in records:
        name = str(record.get("name", ""))
        stem = Path(name).stem
        if not stem.startswith("tile_"):
            continue
        path = Path(record.get("project_path", ""))
        if not path.is_absolute():
            path = project_root / path
        indexed[stem] = {**record, "path": path}
    return indexed


def draw_tile(canvas: Image.Image, tile_path: Path, x: int, y: int, scale: int) -> None:
    with Image.open(tile_path) as source:
        tile = source.convert("RGBA")
    tile = tile.resize((tile.width * scale, tile.height * scale), Image.Resampling.NEAREST)
    canvas.alpha_composite(tile, (x, y))


def build_sheet(project_root: Path, inventory_path: Path, output_path: Path, manifest_path: Path, scale: int) -> dict[str, Any]:
    indexed = index_inventory(project_root, inventory_path)
    row_h = 76
    label_w = 142
    tile_w = 42
    pad = 10
    width = label_w + (8 * tile_w) + pad
    height = 56 + len(TARGET_FAMILIES) * row_h
    sheet = Image.new("RGBA", (width, height), (17, 19, 23, 255))
    draw = ImageDraw.Draw(sheet)
    title_font = load_font(17)
    font = load_font(11)
    draw.text((12, 10), "LIT-ISO tile style-lock reference targets", fill=(238, 241, 234, 255), font=title_font)
    draw.text((12, 34), "These source families are the visual targets for the LoRA strength matrix.", fill=(176, 186, 176, 255), font=font)

    rows: list[dict[str, Any]] = []
    for row_index, family in enumerate(TARGET_FAMILIES):
        y = 56 + row_index * row_h
        draw.rectangle((0, y, width - 1, y + row_h - 1), outline=(56, 64, 70, 255), fill=(24, 27, 32, 255))
        draw.text((12, y + 12), family["family"], fill=(231, 236, 229, 255), font=title_font)
        row = {"family": family["family"], "tiles": []}
        for tile_index, tile_id in enumerate(family["reference_tiles"]):
            x = label_w + tile_index * tile_w
            backing = checkerboard((36, 36), 6)
            sheet.alpha_composite(backing, (x, y + 8))
            record = indexed.get(tile_id)
            if record and Path(record["path"]).exists():
                draw_tile(sheet, Path(record["path"]), x + 2, y + 10, scale)
                row["tiles"].append(
                    {
                        "tile_id": tile_id,
                        "path": repo_rel(project_root, Path(record["path"])),
                        "visible_bbox": record.get("visible_bbox"),
                        "color_count": record.get("color_count"),
                    }
                )
            else:
                draw.text((x + 2, y + 18), "missing", fill=(236, 116, 105, 255), font=font)
                row["tiles"].append({"tile_id": tile_id, "missing": True})
            draw.text((x + 1, y + 49), tile_id.replace("tile_", ""), fill=(173, 181, 177, 255), font=font)
        rows.append(row)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)
    manifest = {
        "schema": "lit_iso.lora.tile_style_reference_targets.v1",
        "generated_utc": utc_now(),
        "inventory": repo_rel(project_root, inventory_path),
        "contact_sheet": repo_rel(project_root, output_path),
        "families": rows,
    }
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    return manifest


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a reference sheet for supplied LIT-ISO tile style-lock families.")
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--inventory", type=Path, default=Path("Assets/Generated/_Review/style_lock_sources/style_lock_analysis/style_lock_inventory.json"))
    parser.add_argument("--output", type=Path, default=Path("Temp/LoRA/Evals/stylelock_tile_reference_targets.png"))
    parser.add_argument("--manifest", type=Path, default=Path("Temp/LoRA/Evals/stylelock_tile_reference_targets.json"))
    parser.add_argument("--scale", type=int, default=1)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    inventory = args.inventory if args.inventory.is_absolute() else project_root / args.inventory
    output = args.output if args.output.is_absolute() else project_root / args.output
    manifest = args.manifest if args.manifest.is_absolute() else project_root / args.manifest
    payload = build_sheet(project_root, inventory, output, manifest, max(1, args.scale))
    print(json.dumps({"ok": True, "families": len(payload["families"]), "contact_sheet": str(output), "manifest": str(manifest)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
