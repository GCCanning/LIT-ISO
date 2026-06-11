#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import shutil
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw


DEFAULT_SELECTION = {
    "grass_flat": "source",
    "dirt_flat": "source",
    "grass_cliff_edge": "source",
    "stone_flat": "source",
    "water_flat": "source",
    "water_shore_stone": "source",
}

TILE_DESTINATION = {
    "grass_flat": "Assets/Generated/Tiles/Reference32/grass_flat.png",
    "dirt_flat": "Assets/Generated/Tiles/Reference32/dirt_flat.png",
    "grass_cliff_edge": "Assets/Generated/Tiles/Reference32/grass_cliff_edge.png",
    "stone_flat": "Assets/Generated/Tiles/Reference32/stone_flat.png",
    "water_flat": "Assets/Generated/Tiles/Reference32/water_flat.png",
    "water_shore_stone": "Assets/Generated/Tiles/Reference32/water_shore_stone.png",
}


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


def safe_name(value: str) -> str:
    cleaned = "".join(ch if ch.isalnum() or ch in "_.-" else "_" for ch in value).strip("._")
    return cleaned or "asset"


def load_selection(path: Path | None, fallback_variant: str) -> dict[str, str]:
    if path is None:
        return {key: fallback_variant for key in DEFAULT_SELECTION}
    data = read_json(path)
    if "selection" in data:
        data = data["selection"]
    return {str(key): str(value) for key, value in data.items()}


def find_variant(row: dict[str, Any], variant_id: str) -> dict[str, Any]:
    for variant in row.get("variants", []):
        if variant.get("variant_id") == variant_id:
            return variant
    available = ", ".join(str(item.get("variant_id")) for item in row.get("variants", []))
    raise ValueError(f"Missing variant '{variant_id}' for {row.get('id')}. Available: {available}")


def draw_checker(size: tuple[int, int]) -> Image.Image:
    checker = Image.new("RGBA", size, (28, 32, 36, 255))
    draw = ImageDraw.Draw(checker)
    for y in range(0, size[1], 8):
        for x in range(0, size[0], 8):
            fill = (42, 47, 52, 255) if ((x // 8) + (y // 8)) % 2 == 0 else (30, 34, 38, 255)
            draw.rectangle([x, y, x + 7, y + 7], fill=fill)
    return checker


def paste_preview(sheet: Image.Image, path: Path, x: int, y: int, scale: int = 4) -> None:
    image = Image.open(path).convert("RGBA")
    preview = image.resize((image.width * scale, image.height * scale), Image.Resampling.NEAREST)
    cell = draw_checker(preview.size)
    cell.alpha_composite(preview)
    sheet.alpha_composite(cell, (x, y))


def make_contact_sheet(project_root: Path, selected: list[dict[str, Any]], output: Path, title: str) -> None:
    cell_w = 164
    cell_h = 188
    columns = 3
    rows = max(1, (len(selected) + columns - 1) // columns)
    sheet = Image.new("RGBA", (cell_w * columns, 64 + cell_h * rows), (18, 21, 24, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((12, 10), title, fill=(240, 245, 248, 255))
    draw.text((12, 30), "Review-only selected 32x32 tile family. Pending manual approval.", fill=(172, 184, 190, 255))
    for index, item in enumerate(selected):
        x = (index % columns) * cell_w
        y = 64 + (index // columns) * cell_h
        draw.rectangle([x, y, x + cell_w - 1, y + cell_h - 1], outline=(67, 76, 84, 255))
        paste_preview(sheet, project_root / item["path"].replace("/", "\\"), x + 18, y + 8, scale=4)
        draw.text((x + 8, y + 144), item["id"], fill=(240, 245, 248, 255))
        draw.text((x + 8, y + 160), f"variant {item['variant_id']}", fill=(172, 184, 190, 255))
        draw.text((x + 8, y + 174), f"colors {item['colors']} cov {item['alpha_coverage']}", fill=(172, 184, 190, 255))
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output)


def make_map_preview(project_root: Path, selected: list[dict[str, Any]], output: Path, title: str) -> None:
    by_id = {item["id"]: project_root / item["path"].replace("/", "\\") for item in selected}
    canvas = Image.new("RGBA", (360, 230), (13, 16, 18, 255))
    draw = ImageDraw.Draw(canvas)
    draw.text((12, 10), title, fill=(230, 236, 230, 255))
    layout = [
        ["grass_flat", "grass_flat", "grass_flat", "stone_flat", "grass_flat"],
        ["grass_flat", "dirt_flat", "dirt_flat", "grass_flat", "grass_flat"],
        ["grass_cliff_edge", "grass_flat", "water_shore_stone", "water_flat", "water_flat"],
        ["grass_flat", "grass_flat", "water_flat", "water_flat", "water_flat"],
        ["grass_flat", "stone_flat", "grass_flat", "dirt_flat", "grass_flat"],
    ]
    origin_x = 170
    origin_y = 48
    for gy, row in enumerate(layout):
        for gx, tile_id in enumerate(row):
            path = by_id.get(tile_id)
            if path is None:
                continue
            tile = Image.open(path).convert("RGBA")
            x = origin_x + (gx - gy) * 16
            y = origin_y + (gx + gy) * 8
            canvas.alpha_composite(tile, (x, y))
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output)


def main() -> int:
    parser = argparse.ArgumentParser(description="Select a clean review-only family from Reference32 style-locked variants.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--source-manifest", default="Assets/Generated/_Review/reference32_style_locked_variants_v2/reference32_style_locked_variants_manifest.json")
    parser.add_argument("--output-root", default="Assets/Generated/_Review/reference32_selected_tile_family_v1")
    parser.add_argument("--default-variant", default="source")
    parser.add_argument("--selection-json", type=Path)
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    source_manifest_path = project_root / args.source_manifest
    source_manifest = read_json(source_manifest_path)
    output_root = project_root / args.output_root
    png_root = output_root / "png"
    png_root.mkdir(parents=True, exist_ok=True)

    selection = load_selection(args.selection_json, args.default_variant)
    selected: list[dict[str, Any]] = []
    for row in source_manifest.get("rows", []):
        tile_id = str(row["id"])
        variant_id = selection.get(tile_id, args.default_variant)
        variant = find_variant(row, variant_id)
        source_path = project_root / variant["path"].replace("/", "\\")
        out_name = f"{safe_name(tile_id)}__{safe_name(variant_id)}.png"
        out_path = png_root / out_name
        shutil.copy2(source_path, out_path)
        selected.append({
            "id": tile_id,
            "source_tile": row.get("source_tile", ""),
            "source_path": row.get("source_path", ""),
            "variant_id": variant_id,
            "label": variant.get("label", variant_id),
            "path": repo_path(project_root, out_path),
            "width": variant.get("width"),
            "height": variant.get("height"),
            "colors": variant.get("colors"),
            "alpha_coverage": variant.get("alpha_coverage"),
            "status": "pending_manual_review",
            "unity_destination": TILE_DESTINATION.get(tile_id, f"Assets/Generated/Tiles/Reference32/{tile_id}.png"),
        })

    title = f"Reference32 selected tile family ({args.default_variant})"
    contact_sheet = output_root / "selected_tile_family_contact_sheet.png"
    map_preview = output_root / "selected_tile_family_map_preview.png"
    make_contact_sheet(project_root, selected, contact_sheet, title)
    make_map_preview(project_root, selected, map_preview, title)

    generated_utc = datetime.now(timezone.utc).isoformat()
    manifest = {
        "schema": "lit_iso.asset_forge.reference32_selected_tile_family.v1",
        "generated_utc": generated_utc,
        "status": "review_only_not_unity_imported",
        "source_manifest": repo_path(project_root, source_manifest_path),
        "output_root": repo_path(project_root, output_root),
        "contact_sheet": repo_path(project_root, contact_sheet),
        "map_preview": repo_path(project_root, map_preview),
        "default_variant": args.default_variant,
        "selection": selection,
        "selected": selected,
        "next_gate": "Manual art approval and license verification before Unity promotion or training capture.",
    }
    write_json(output_root / "selected_tile_family_manifest.json", manifest)

    review_report = {
        "schema": "lit_iso.asset_forge.review_report.v1",
        "pack_name": output_root.name,
        "generated_utc": generated_utc,
        "provider": "deterministic_style_locked_variant",
        "asset_mode": "tile",
        "status": "review_only_not_unity_imported",
        "source_manifest": repo_path(project_root, source_manifest_path),
        "contact_sheet": repo_path(project_root, contact_sheet),
        "preview": repo_path(project_root, map_preview),
        "total": len(selected),
        "pass_count": 0,
        "review_count": len(selected),
        "items": [
            {
                "id": f"png/{Path(item['path']).name}",
                "name": Path(item["path"]).name,
                "path": item["path"],
                "category": "terrain",
                "biome": "Reference32",
                "material": item["id"],
                "shape": item["variant_id"],
                "width": item["width"],
                "height": item["height"],
                "status": "review",
                "issues": ["manual_art_approval_required", "license_verification_required"],
                "warnings": [],
                "unity": {"category": "Tiles", "ppu": 32, "pivot": {"x": 0.5, "y": 0.25}},
            }
            for item in selected
        ],
    }
    write_json(output_root / "review_report.json", review_report)

    decisions = {
        "schema": "lit_iso.asset_forge.review_decisions.v1",
        "pack_name": output_root.name,
        "generated_utc": generated_utc,
        "source_report": repo_path(project_root, output_root / "review_report.json"),
        "decision_policy": "manual_review",
        "total": len(selected),
        "approved_count": 0,
        "pending_count": len(selected),
        "decisions": [
            {
                "id": f"png/{Path(item['path']).name}",
                "name": Path(item["path"]).name,
                "decision": "pending",
                "source_path": item["path"],
                "destination_path": item["unity_destination"],
                "category": "terrain",
                "biome": "Reference32",
                "material": item["id"],
                "shape": item["variant_id"],
                "unity": {"category": "Tiles", "ppu": 32, "pivot": {"x": 0.5, "y": 0.25}},
                "notes": "Pending manual art and license approval.",
            }
            for item in selected
        ],
    }
    write_json(output_root / "review_decisions.json", decisions)

    print(json.dumps({
        "ok": True,
        "manifest": repo_path(project_root, output_root / "selected_tile_family_manifest.json"),
        "contact_sheet": repo_path(project_root, contact_sheet),
        "map_preview": repo_path(project_root, map_preview),
        "review_report": repo_path(project_root, output_root / "review_report.json"),
        "review_decisions": repo_path(project_root, output_root / "review_decisions.json"),
        "selected": len(selected),
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
