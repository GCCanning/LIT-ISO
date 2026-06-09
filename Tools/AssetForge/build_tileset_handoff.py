#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def rel(root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--review-report", required=True)
    parser.add_argument("--output-root", required=True)
    args = parser.parse_args()

    root = Path(args.project_root).resolve()
    report_path = (root / args.review_report).resolve()
    output_root = (root / args.output_root).resolve()
    output_root.mkdir(parents=True, exist_ok=True)
    report = read_json(report_path)
    items = report.get("items", [])
    if not items:
        raise SystemExit("No items in review report.")

    tile_size = 64
    atlas = Image.new("RGBA", (tile_size * len(items), tile_size), (0, 0, 0, 0))
    rows = []
    for i, item in enumerate(items):
        image = Image.open(root / item["path"]).convert("RGBA")
        atlas.alpha_composite(image, (i * tile_size, 0))
        rows.append({
            "name": Path(item["path"]).stem,
            "material": item.get("material", ""),
            "rect": {"x": i * tile_size, "y": 0, "w": tile_size, "h": tile_size},
            "ppu": item.get("unity", {}).get("ppu", 64),
            "pivot": item.get("unity", {}).get("pivot", {"x": 0.5, "y": 0.25}),
            "height": item.get("unity", {}).get("height", 1),
            "source": item["path"],
        })

    atlas_path = output_root / "greenwake_selected_height_masters_atlas.png"
    atlas.save(atlas_path)

    preview = Image.new("RGBA", (max(520, 112 * len(items)), 154), (28, 32, 34, 255))
    draw = ImageDraw.Draw(preview)
    try:
        font = ImageFont.truetype("arial.ttf", 12)
    except Exception:
        font = ImageFont.load_default()
    for i, item in enumerate(items):
        image = Image.open(root / item["path"]).convert("RGBA").resize((96, 96), Image.Resampling.NEAREST)
        x = 8 + i * 112
        preview.alpha_composite(image, (x, 8))
        draw.text((x, 110), item.get("material", ""), fill=(220, 226, 218, 255), font=font)
        draw.text((x, 128), Path(item["path"]).name[:16], fill=(145, 158, 150, 255), font=font)
    preview_path = output_root / "greenwake_selected_height_masters_handoff_preview.png"
    preview.save(preview_path)

    manifest = {
        "schema": "lit_iso.asset_forge.tileset_handoff.v1",
        "name": "greenwake_selected_height_masters",
        "source_review_report": rel(root, report_path),
        "atlas": rel(root, atlas_path),
        "preview": rel(root, preview_path),
        "unity_import": {
            "target_folder": "Assets/Generated/Tiles/Greenwake",
            "texture_type": "Sprite",
            "sprite_mode": "Multiple",
            "filter_mode": "Point",
            "mip_maps": False,
            "compression": "None",
            "pixels_per_unit": 64,
            "default_pivot": {"x": 0.5, "y": 0.25},
        },
        "sprites": rows,
        "status": "review_only_not_promoted",
    }
    manifest_path = output_root / "greenwake_selected_height_masters_handoff.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps(manifest, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
