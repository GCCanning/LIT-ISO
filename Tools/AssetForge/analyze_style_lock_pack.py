#!/usr/bin/env python3
"""Analyze local style-lock reference packs for Asset Forge.

The output is intentionally review-only: inventory, palettes, contact sheets,
and a concise markdown summary. It does not promote source pixels into runtime
Unity folders.
"""

from __future__ import annotations

import argparse
import json
import math
import re
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont


IMAGE_EXTENSIONS = {".png"}
DEFAULT_SOURCE_ROOT = Path("Assets/Generated/_Review/style_lock_sources")
DEFAULT_OUTPUT_ROOT = Path("Assets/Generated/_Review/style_lock_sources/style_lock_analysis")


def normalize_rel(root: Path, path: Path) -> str:
    return path.resolve().relative_to(root.resolve()).as_posix()


def load_font(size: int) -> ImageFont.ImageFont:
    for name in ("segoeui.ttf", "arial.ttf", "calibri.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def safe_name(value: str) -> str:
    return re.sub(r"[^a-zA-Z0-9._-]+", "_", value).strip("_").lower()


def visible_bbox(image: Image.Image) -> tuple[int, int, int, int] | None:
    alpha = image.convert("RGBA").split()[-1]
    return alpha.getbbox()


def top_palette(records: list[dict[str, Any]], max_colors: int = 96) -> list[dict[str, Any]]:
    counter: Counter[tuple[int, int, int, int]] = Counter()
    for record in records:
        path = Path(record["absolute_path"])
        with Image.open(path) as image:
            rgba = image.convert("RGBA")
            for color, count in (rgba.getcolors(maxcolors=2_000_000) or []):
                r, g, b, a = count
                if a < 16:
                    continue
                counter[(r, g, b, a)] += color
    palette = []
    total = sum(counter.values()) or 1
    for (r, g, b, a), count in counter.most_common(max_colors):
        palette.append(
            {
                "hex": f"#{r:02X}{g:02X}{b:02X}",
                "rgba": [r, g, b, a],
                "pixels": count,
                "ratio": round(count / total, 5),
            }
        )
    return palette


def draw_palette(path: Path, palette: list[dict[str, Any]], title: str) -> None:
    swatch = 32
    cols = 12
    rows = math.ceil(len(palette) / cols)
    header_h = 54
    image = Image.new("RGBA", (cols * swatch, header_h + rows * swatch), (18, 20, 22, 255))
    draw = ImageDraw.Draw(image)
    font = load_font(16)
    small = load_font(10)
    draw.text((10, 10), title, fill=(238, 238, 230, 255), font=font)
    for index, entry in enumerate(palette):
        x = (index % cols) * swatch
        y = header_h + (index // cols) * swatch
        r, g, b, a = entry["rgba"]
        draw.rectangle((x, y, x + swatch - 1, y + swatch - 1), fill=(r, g, b, a))
        if index < 24:
            draw.text((x + 2, y + 18), str(index + 1), fill=(0, 0, 0, 220), font=small)
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path)


def make_contact_sheet(
    project_root: Path,
    records: list[dict[str, Any]],
    output_path: Path,
    title: str,
    max_items: int,
    cell_size: tuple[int, int],
) -> None:
    selected = records[:max_items]
    if not selected:
        return
    cols = 12
    rows = math.ceil(len(selected) / cols)
    label_h = 24
    header_h = 52
    cell_w, cell_h = cell_size
    sheet = Image.new("RGBA", (cols * cell_w, header_h + rows * (cell_h + label_h)), (16, 18, 20, 255))
    draw = ImageDraw.Draw(sheet)
    font = load_font(16)
    small = load_font(9)
    draw.text((10, 10), title, fill=(238, 238, 230, 255), font=font)
    for index, record in enumerate(selected):
        x = (index % cols) * cell_w
        y = header_h + (index // cols) * (cell_h + label_h)
        with Image.open(record["absolute_path"]) as image:
            sprite = image.convert("RGBA")
        scale = min((cell_w - 8) / sprite.width, (cell_h - 8) / sprite.height, 4)
        if scale < 1:
            scale = 1
        display = sprite.resize((max(1, int(sprite.width * scale)), max(1, int(sprite.height * scale))), Image.Resampling.NEAREST)
        px = x + (cell_w - display.width) // 2
        py = y + (cell_h - display.height) // 2
        sheet.alpha_composite(display, (px, py))
        label = Path(record["relative_path"]).stem[:22]
        draw.text((x + 4, y + cell_h + 4), label, fill=(198, 205, 195, 255), font=small)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)


def infer_critter_meta(path: Path) -> dict[str, str | None]:
    name = path.stem
    direction = None
    action = None
    for candidate in ("NE", "NW", "SE", "SW", "N", "S", "E", "W"):
        if f"_{candidate}_" in name:
            direction = candidate
            break
    parts = name.split("_")
    if direction and direction in parts:
        idx = parts.index(direction)
        if idx + 1 < len(parts):
            action = parts[idx + 1]
    return {"direction": direction, "action": action}


def scan_sources(project_root: Path, source_root: Path) -> dict[str, Any]:
    records: list[dict[str, Any]] = []
    for path in sorted(source_root.rglob("*")):
        if not path.is_file() or path.suffix.lower() not in IMAGE_EXTENSIONS:
            continue
        with Image.open(path) as image:
            rgba = image.convert("RGBA")
            bbox = visible_bbox(rgba)
            colors = rgba.getcolors(maxcolors=2_000_000) or []
            relative = normalize_rel(source_root, path)
            top_group = relative.split("/", 1)[0]
            record: dict[str, Any] = {
                "relative_path": relative,
                "project_path": normalize_rel(project_root, path),
                "absolute_path": str(path.resolve()),
                "top_group": top_group,
                "name": path.name,
                "width": image.width,
                "height": image.height,
                "mode": image.mode,
                "visible_bbox": list(bbox) if bbox else None,
                "color_count": len([entry for entry in colors if entry[1][3] >= 16]) if rgba.mode == "RGBA" else len(colors),
            }
            if top_group == "critters":
                record.update(infer_critter_meta(path))
            records.append(record)

    by_group = defaultdict(list)
    for record in records:
        by_group[record["top_group"]].append(record)

    inventory = {
        "schema": "lit_iso.asset_forge.style_lock_analysis.v1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "source_root": normalize_rel(project_root, source_root),
        "license_status": "unknown_user_supplied_reference_until_documented",
        "total_png": len(records),
        "groups": {},
        "records": records,
    }
    for group, group_records in by_group.items():
        dims = Counter((record["width"], record["height"]) for record in group_records)
        actions = Counter(str(record.get("action")) for record in group_records if record.get("action"))
        directions = Counter(str(record.get("direction")) for record in group_records if record.get("direction"))
        inventory["groups"][group] = {
            "count": len(group_records),
            "dimensions": [
                {"width": width, "height": height, "count": count}
                for (width, height), count in dims.most_common()
            ],
            "actions": dict(actions),
            "directions": dict(directions),
            "palette": top_palette(group_records, 96),
        }
    return inventory


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as handle:
        json.dump(payload, handle, indent=2)
        handle.write("\n")


def write_markdown(path: Path, inventory: dict[str, Any], outputs: dict[str, str]) -> None:
    lines = [
        "# Style Lock Analysis",
        "",
        f"Generated: `{inventory['generated_utc']}`",
        f"Source root: `{inventory['source_root']}`",
        f"License status: `{inventory['license_status']}`",
        "",
        "## Summary",
        "",
        f"- PNG records: {inventory['total_png']}",
        "- Runtime import: blocked until the art/source license is documented.",
        "- Use as style lock/reference first; only use for training if licensing explicitly allows it.",
        "",
        "## Groups",
        "",
    ]
    for group, data in inventory["groups"].items():
        lines.append(f"### {group}")
        lines.append(f"- Count: {data['count']}")
        dims = ", ".join(f"{entry['width']}x{entry['height']} ({entry['count']})" for entry in data["dimensions"][:8])
        lines.append(f"- Main dimensions: {dims}")
        if data["directions"]:
            lines.append(f"- Directions: {', '.join(f'{key}:{value}' for key, value in sorted(data['directions'].items()))}")
        if data["actions"]:
            lines.append(f"- Actions: {', '.join(f'{key}:{value}' for key, value in sorted(data['actions'].items()))}")
        lines.append("")
    lines.extend(
        [
            "## Generated Review Artifacts",
            "",
            f"- Inventory JSON: `{outputs['inventory']}`",
            f"- Tile contact sheet: `{outputs['tile_contact_sheet']}`",
            f"- Critter contact sheet: `{outputs['critter_contact_sheet']}`",
            f"- Tile palette: `{outputs['tile_palette']}`",
            f"- Critter palette: `{outputs['critter_palette']}`",
            "",
            "## Pipeline Correction",
            "",
            "New terrain output must match these references before Unity import:",
            "",
            "- 32x32 terrain cells for the supplied tileset scale.",
            "- Compact, high-contrast but not noisy palette.",
            "- Dark underside/shadow ramps with hand-authored dithering.",
            "- Organic edge variants and corner/transition pieces, not procedural diamond outlines.",
            "- Critter/mob output should use the extracted direction/action naming as the animation contract.",
        ]
    )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Analyze supplied style-lock packs.")
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--source-root", type=Path, default=DEFAULT_SOURCE_ROOT)
    parser.add_argument("--output-root", type=Path, default=DEFAULT_OUTPUT_ROOT)
    args = parser.parse_args()

    project_root = args.project_root.resolve()
    source_root = (project_root / args.source_root).resolve() if not args.source_root.is_absolute() else args.source_root.resolve()
    output_root = (project_root / args.output_root).resolve() if not args.output_root.is_absolute() else args.output_root.resolve()
    output_root.mkdir(parents=True, exist_ok=True)

    inventory = scan_sources(project_root, source_root)
    inventory_path = output_root / "style_lock_inventory.json"
    tile_contact = output_root / "tileset_contact_sheet.png"
    critter_contact = output_root / "critters_contact_sheet.png"
    tile_palette = output_root / "tileset_palette.png"
    critter_palette = output_root / "critters_palette.png"
    summary_path = output_root / "STYLE_LOCK_ANALYSIS.md"

    tile_records = [
        record
        for record in inventory["records"]
        if record["top_group"] == "isometric tileset" and record["width"] <= 64 and record["height"] <= 64
    ]
    critter_records = [
        record
        for record in inventory["records"]
        if record["top_group"] == "critters" and record["width"] <= 128 and record["height"] <= 64
    ]

    make_contact_sheet(project_root, tile_records, tile_contact, "Isometric tileset style-lock references", 144, (72, 72))
    make_contact_sheet(project_root, critter_records, critter_contact, "Critter style-lock animation frames", 144, (96, 64))
    draw_palette(tile_palette, inventory["groups"].get("isometric tileset", {}).get("palette", []), "Tileset palette")
    draw_palette(critter_palette, inventory["groups"].get("critters", {}).get("palette", []), "Critter palette")
    write_json(inventory_path, inventory)
    outputs = {
        "inventory": normalize_rel(project_root, inventory_path),
        "tile_contact_sheet": normalize_rel(project_root, tile_contact),
        "critter_contact_sheet": normalize_rel(project_root, critter_contact),
        "tile_palette": normalize_rel(project_root, tile_palette),
        "critter_palette": normalize_rel(project_root, critter_palette),
    }
    write_markdown(summary_path, inventory, outputs)

    print(
        json.dumps(
            {
                "inventory": outputs["inventory"],
                "summary": normalize_rel(project_root, summary_path),
                "total_png": inventory["total_png"],
                "groups": {key: value["count"] for key, value in inventory["groups"].items()},
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
