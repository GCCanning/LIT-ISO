#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw


DEFAULT_SOURCE_FAMILY = "Assets/Generated/_Review/reference32_mask_locked_texture_family_screenshot_balanced_v1/selected_tile_family_manifest.json"
DEFAULT_OUTPUT_ROOT = "Assets/Generated/_Review/reference32_outline_reinforced_texture_family_v1"


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def repo_path(root: Path, path: Path | str) -> str:
    path = Path(path)
    if not path.is_absolute():
        path = root / path
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path.resolve()).replace("\\", "/")


def resolve_path(root: Path, value: str | Path) -> Path:
    path = Path(value)
    return path if path.is_absolute() else root / path


def luminance(rgb: tuple[int, int, int]) -> float:
    return 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2]


def mix(a: tuple[int, int, int], b: tuple[int, int, int], t: float) -> tuple[int, int, int]:
    t = max(0.0, min(1.0, t))
    return tuple(int(round(a[index] * (1.0 - t) + b[index] * t)) for index in range(3))


def is_opaque(image: Image.Image, x: int, y: int) -> bool:
    if x < 0 or y < 0 or x >= image.width or y >= image.height:
        return False
    return image.getpixel((x, y))[3] > 16


def is_edge_pixel(image: Image.Image, x: int, y: int) -> bool:
    if not is_opaque(image, x, y):
        return False
    for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
        if not is_opaque(image, nx, ny):
            return True
    return False


def dark_outline_ratio(image: Image.Image) -> float:
    edge = 0
    dark = 0
    for y in range(image.height):
        for x in range(image.width):
            if not is_edge_pixel(image, x, y):
                continue
            edge += 1
            r, g, b, _ = image.getpixel((x, y))
            if max(r, g, b) <= 78:
                dark += 1
    return round(dark / edge, 4) if edge else 0.0


def color_count(image: Image.Image) -> int:
    values = set()
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = pixels[x, y]
            if a > 16:
                values.add((r, g, b))
    return len(values)


def alpha_coverage(image: Image.Image) -> float:
    alpha = image.getchannel("A")
    visible = 0
    for y in range(image.height):
        for x in range(image.width):
            if alpha.getpixel((x, y)) > 16:
                visible += 1
    return round(visible / float(max(1, image.width * image.height)), 4)


def reinforce_outline(current: Image.Image, source: Image.Image, strength: float, dark_anchor: tuple[int, int, int]) -> Image.Image:
    current = current.convert("RGBA")
    source = source.convert("RGBA")
    output = current.copy()
    for y in range(current.height):
        for x in range(current.width):
            if not is_edge_pixel(current, x, y):
                continue
            cr, cg, cb, ca = current.getpixel((x, y))
            sr, sg, sb, sa = source.getpixel((x, y)) if x < source.width and y < source.height else (0, 0, 0, 0)
            current_rgb = (cr, cg, cb)
            source_rgb = (sr, sg, sb) if sa > 16 else dark_anchor
            if luminance(source_rgb) >= luminance(current_rgb):
                target = mix(source_rgb, dark_anchor, 0.55)
                local_strength = strength * 0.55
            else:
                target = mix(source_rgb, dark_anchor, 0.25)
                local_strength = strength
            output.putpixel((x, y), (*mix(current_rgb, target, local_strength), ca))
    return output


def checker(size: tuple[int, int]) -> Image.Image:
    image = Image.new("RGBA", size, (26, 29, 34, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], 8):
        for x in range(0, size[0], 8):
            fill = (38, 42, 48, 255) if ((x // 8) + (y // 8)) % 2 == 0 else (26, 29, 34, 255)
            draw.rectangle((x, y, x + 7, y + 7), fill=fill)
    return image


def paste_scaled(sheet: Image.Image, image: Image.Image, x: int, y: int, scale: int = 4) -> None:
    preview = image.resize((image.width * scale, image.height * scale), Image.Resampling.NEAREST)
    cell = checker(preview.size)
    cell.alpha_composite(preview)
    sheet.alpha_composite(cell, (x, y))


def make_contact_sheet(project_root: Path, selected: list[dict[str, Any]], output: Path) -> None:
    cell_w = 210
    row_h = 156
    width = cell_w * 3
    height = 74 + row_h * len(selected)
    sheet = Image.new("RGBA", (width, height), (18, 21, 25, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((12, 10), "Reference32 outline-reinforced family", fill=(242, 246, 250, 255))
    draw.text((12, 30), "source / screenshot-balanced / outline-reinforced. Review-only; no Unity import.", fill=(172, 184, 194, 255))
    draw.text((34, 54), "source", fill=(166, 178, 190, 255))
    draw.text((244, 54), "before", fill=(166, 178, 190, 255))
    draw.text((454, 54), "after", fill=(166, 178, 190, 255))
    for index, item in enumerate(selected):
        y = 74 + index * row_h
        source = Image.open(resolve_path(project_root, item["source_path"])).convert("RGBA")
        before = Image.open(resolve_path(project_root, item["previous_path"])).convert("RGBA")
        after = Image.open(resolve_path(project_root, item["path"])).convert("RGBA")
        paste_scaled(sheet, source, 36, y + 8)
        paste_scaled(sheet, before, 246, y + 8)
        paste_scaled(sheet, after, 456, y + 8)
        draw.text((12, y + 132), f"{item['id']} outline {item['previous_outline_ratio']} -> {item['outline_ratio']}", fill=(196, 207, 218, 255))
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output)


def make_map_preview(project_root: Path, selected: list[dict[str, Any]], output: Path) -> None:
    by_id = {item["id"]: resolve_path(project_root, item["path"]) for item in selected}
    canvas = Image.new("RGBA", (360, 230), (13, 16, 18, 255))
    draw = ImageDraw.Draw(canvas)
    draw.text((12, 10), "outline reinforced map preview", fill=(230, 236, 230, 255))
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
            if not path:
                continue
            tile = Image.open(path).convert("RGBA")
            x = origin_x + (gx - gy) * 16
            y = origin_y + (gx + gy) * 8
            canvas.alpha_composite(tile, (x, y))
    output.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output)


def main() -> int:
    parser = argparse.ArgumentParser(description="Build an outline-reinforced variant of the selected Reference32 tile family.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--source-family", default=DEFAULT_SOURCE_FAMILY)
    parser.add_argument("--output-root", default=DEFAULT_OUTPUT_ROOT)
    parser.add_argument("--strength", type=float, default=0.62)
    parser.add_argument("--dark-anchor", default="#11191d")
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    source_family_path = resolve_path(project_root, args.source_family)
    source_family = read_json(source_family_path)
    output_root = resolve_path(project_root, args.output_root)
    png_root = output_root / "png"
    png_root.mkdir(parents=True, exist_ok=True)
    dark_anchor = tuple(int(args.dark_anchor.lstrip("#")[i:i + 2], 16) for i in (0, 2, 4))
    generated_utc = utc_now()
    selected: list[dict[str, Any]] = []

    for item in source_family.get("selected") or []:
        source_path = resolve_path(project_root, item["source_path"])
        current_path = resolve_path(project_root, item["path"])
        source = Image.open(source_path).convert("RGBA")
        current = Image.open(current_path).convert("RGBA")
        reinforced = reinforce_outline(current, source, args.strength, dark_anchor)
        out_path = png_root / f"{item['id']}__outline_reinforced.png"
        reinforced.save(out_path)
        selected.append(
            {
                "id": item["id"],
                "source_tile": item.get("source_tile", ""),
                "source_path": repo_path(project_root, source_path),
                "previous_path": repo_path(project_root, current_path),
                "variant_id": "outline_reinforced",
                "label": "outline reinforced",
                "path": repo_path(project_root, out_path),
                "width": reinforced.width,
                "height": reinforced.height,
                "colors": color_count(reinforced),
                "alpha_coverage": alpha_coverage(reinforced),
                "previous_outline_ratio": dark_outline_ratio(current),
                "outline_ratio": dark_outline_ratio(reinforced),
                "source_outline_ratio": dark_outline_ratio(source),
                "status": "pending_manual_review",
                "unity_destination": item.get("unity_destination"),
            }
        )

    contact_sheet = output_root / "outline_reinforced_contact_sheet.png"
    map_preview = output_root / "outline_reinforced_map_preview.png"
    make_contact_sheet(project_root, selected, contact_sheet)
    make_map_preview(project_root, selected, map_preview)

    manifest = {
        "schema": "lit_iso.asset_forge.reference32_outline_reinforced_family.v1",
        "generated_utc": generated_utc,
        "status": "review_only_not_unity_imported",
        "source_family": repo_path(project_root, source_family_path),
        "output_root": repo_path(project_root, output_root),
        "contact_sheet": repo_path(project_root, contact_sheet),
        "map_preview": repo_path(project_root, map_preview),
        "default_variant": "outline_reinforced",
        "strength": args.strength,
        "dark_anchor": args.dark_anchor,
        "selected": selected,
        "next_gate": "Manual art approval and license verification before dataset capture, training, or Unity promotion.",
        "notes": [
            "This pass changes only opaque edge colors; it preserves the existing alpha footprint exactly.",
            "Use it to evaluate whether the current screenshot-balanced palette needs stronger Reference32 dark outlines.",
        ],
    }
    write_json(output_root / "selected_tile_family_manifest.json", manifest)

    review_report = {
        "schema": "lit_iso.asset_forge.review_report.v1",
        "pack_name": output_root.name,
        "generated_utc": generated_utc,
        "provider": "deterministic_outline_reinforcement",
        "asset_mode": "tile",
        "status": "review_only_not_unity_imported",
        "source_manifest": repo_path(project_root, source_family_path),
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
    review_decisions = {
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
    write_json(output_root / "review_decisions.json", review_decisions)

    print(
        json.dumps(
            {
                "ok": True,
                "manifest": repo_path(project_root, output_root / "selected_tile_family_manifest.json"),
                "contact_sheet": repo_path(project_root, contact_sheet),
                "map_preview": repo_path(project_root, map_preview),
                "selected": len(selected),
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
