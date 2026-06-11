#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw


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


def color_count(image: Image.Image) -> int:
    colors = image.convert("RGBA").getcolors(maxcolors=image.width * image.height)
    if colors is None:
        return image.width * image.height
    return len({color for _, color in colors if len(color) == 4 and color[3] > 0})


def alpha_coverage(image: Image.Image) -> float:
    alpha = image.convert("RGBA").getchannel("A")
    histogram = alpha.histogram()
    return round((image.width * image.height - histogram[0]) / (image.width * image.height), 4)


def palette_distance(a: Image.Image, b: Image.Image) -> float:
    a_rgba = a.convert("RGBA")
    b_rgba = b.convert("RGBA").resize(a_rgba.size, Image.Resampling.NEAREST)
    a_pixels = a_rgba.load()
    b_pixels = b_rgba.load()
    total = 0.0
    count = 0
    for y in range(a_rgba.height):
        for x in range(a_rgba.width):
            pa = a_pixels[x, y]
            pb = b_pixels[x, y]
            if pa[3] == 0 and pb[3] == 0:
                continue
            total += (abs(pa[0] - pb[0]) + abs(pa[1] - pb[1]) + abs(pa[2] - pb[2])) / 3
            count += 1
    return round(total / max(1, count), 3)


def find_normalized_candidates(project_root: Path, review_root: Path) -> list[Path]:
    report_path = review_root / "_ProperPixelArt" / "proper_pixel_art_report.json"
    if not report_path.exists():
        return []
    report = read_json(report_path)
    paths = []
    for item in report.get("items", []):
        normalized = item.get("normalized_path")
        if normalized:
            path = project_root / normalized.replace("/", "\\")
            if path.exists():
                paths.append(path)
    return paths


def draw_image_cell(sheet: Image.Image, image_path: Path, x: int, y: int, scale: int = 3) -> None:
    image = Image.open(image_path).convert("RGBA")
    preview = image.resize((image.width * scale, image.height * scale), Image.Resampling.NEAREST)
    checker = Image.new("RGBA", preview.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(checker)
    for cy in range(0, preview.height, 8):
        for cx in range(0, preview.width, 8):
            fill = (43, 48, 54, 255) if ((cx // 8) + (cy // 8)) % 2 == 0 else (30, 34, 38, 255)
            draw.rectangle([cx, cy, cx + 7, cy + 7], fill=fill)
    checker.alpha_composite(preview, (0, 0))
    sheet.alpha_composite(checker, (x, y))


def main() -> int:
    parser = argparse.ArgumentParser(description="Build a contact sheet for reference32 grass matrix outputs.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--matrix-manifest", default="Temp/AssetForge/reference32_grass_matrix_v1.json")
    parser.add_argument("--reference-manifest", default="Assets/Generated/_Review/reference32_geometry_locked_tile_family_v1/reference32_tile_family_manifest.json")
    parser.add_argument("--out-sheet", default="Assets/Generated/_Review/reference32_grass_matrix_v1/reference32_grass_matrix_contact_sheet.png")
    parser.add_argument("--out-report", default="Assets/Generated/_Review/reference32_grass_matrix_v1/reference32_grass_matrix_report.json")
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    matrix_manifest = read_json(project_root / args.matrix_manifest)
    reference_manifest = read_json(project_root / args.reference_manifest)
    grass_record = next(item for item in reference_manifest["items"] if item["id"] == "grass_flat")
    source_path = project_root / grass_record["path"].replace("/", "\\")
    source_image = Image.open(source_path).convert("RGBA")

    rows: list[dict[str, Any]] = []
    for entry in matrix_manifest.get("matrix", []):
        review_root = project_root / entry["review_root"].replace("/", "\\")
        candidates = find_normalized_candidates(project_root, review_root)
        reviewed = []
        for candidate in candidates:
            candidate_image = Image.open(candidate).convert("RGBA")
            reviewed.append({
                "path": rel(project_root, candidate),
                "width": candidate_image.width,
                "height": candidate_image.height,
                "colors": color_count(candidate_image),
                "alpha_coverage": alpha_coverage(candidate_image),
                "palette_distance": palette_distance(source_image, candidate_image),
            })
        rows.append({**entry, "candidates": reviewed})

    cell_w = 196
    row_h = 142
    columns = 4
    sheet_w = cell_w * columns
    sheet_h = 78 + row_h * max(1, len(rows))
    sheet = Image.new("RGBA", (sheet_w, sheet_h), (18, 21, 24, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((12, 10), "Reference32 grass ControlNet/img2img matrix", fill=(240, 245, 248, 255))
    draw.text((12, 30), "Review-only. Source tile at left; candidates are normalized Proper Pixel Art outputs.", fill=(172, 184, 190, 255))
    draw_image_cell(sheet, source_path, 14, 58, scale=3)
    draw.text((116, 64), f"source {grass_record['source_tile']}", fill=(235, 240, 245, 255))
    draw.text((116, 80), f"colors {grass_record['color_count']} | cov {grass_record['alpha_coverage']}", fill=(172, 184, 190, 255))

    y = 78
    for row in rows:
        draw.rectangle([0, y, sheet_w - 1, y + row_h - 1], outline=(65, 74, 82, 255))
        label = f"d{row['denoise']:.2f} l{row['lora_strength']:.2f} c{row['control_strength']:.2f}"
        draw.text((10, y + 10), label, fill=(240, 245, 248, 255))
        draw.text((10, y + 28), row["job_name"][:30], fill=(165, 178, 186, 255))
        draw.text((10, y + 46), row.get("status", "unknown"), fill=(130, 220, 155, 255) if row.get("status") == "complete" else (235, 190, 120, 255))
        for index, candidate in enumerate(row["candidates"][:3]):
            x = cell_w * (index + 1) + 12
            draw_image_cell(sheet, project_root / candidate["path"].replace("/", "\\"), x, y + 12, scale=3)
            text_y = y + 110
            draw.text((x, text_y), f"c{index + 1} dist {candidate['palette_distance']}", fill=(235, 240, 245, 255))
            draw.text((x, text_y + 14), f"colors {candidate['colors']} cov {candidate['alpha_coverage']}", fill=(172, 184, 190, 255))
        y += row_h

    out_sheet = project_root / args.out_sheet
    out_report = project_root / args.out_report
    out_sheet.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out_sheet)
    report = {
        "schema": "lit_iso.asset_forge.reference32_grass_matrix_report.v1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "matrix_manifest": args.matrix_manifest,
        "reference_manifest": args.reference_manifest,
        "source_tile": rel(project_root, source_path),
        "contact_sheet": rel(project_root, out_sheet),
        "rows": rows,
        "selection_note": "Lower palette_distance is only a ranking aid. Manual review remains required for geometry and art direction.",
    }
    write_json(out_report, report)
    print(json.dumps({"ok": True, "sheet": rel(project_root, out_sheet), "report": rel(project_root, out_report), "rows": len(rows)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
