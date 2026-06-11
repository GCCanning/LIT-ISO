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
    total = 0.0
    count = 0
    a_data = a_rgba.get_flattened_data() if hasattr(a_rgba, "get_flattened_data") else a_rgba.getdata()
    b_data = b_rgba.get_flattened_data() if hasattr(b_rgba, "get_flattened_data") else b_rgba.getdata()
    for pa, pb in zip(a_data, b_data):
        if pa[3] == 0 and pb[3] == 0:
            continue
        total += (abs(pa[0] - pb[0]) + abs(pa[1] - pb[1]) + abs(pa[2] - pb[2])) / 3
        count += 1
    return round(total / max(1, count), 3)


def normalized_candidates(project_root: Path, review_root: Path) -> list[Path]:
    report_path = review_root / "_ProperPixelArt" / "proper_pixel_art_report.json"
    if not report_path.exists():
        return []
    report = read_json(report_path)
    paths: list[Path] = []
    for item in report.get("items", []):
        normalized = item.get("normalized_path")
        if not normalized:
            continue
        path = project_root / normalized.replace("/", "\\")
        if path.exists():
            paths.append(path)
    return paths


def draw_checker(size: tuple[int, int]) -> Image.Image:
    checker = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(checker)
    for y in range(0, size[1], 8):
        for x in range(0, size[0], 8):
            fill = (43, 48, 54, 255) if ((x // 8) + (y // 8)) % 2 == 0 else (30, 34, 38, 255)
            draw.rectangle([x, y, x + 7, y + 7], fill=fill)
    return checker


def draw_preview(sheet: Image.Image, image_path: Path, x: int, y: int, scale: int = 3) -> None:
    image = Image.open(image_path).convert("RGBA")
    preview = image.resize((image.width * scale, image.height * scale), Image.Resampling.NEAREST)
    cell = draw_checker(preview.size)
    cell.alpha_composite(preview, (0, 0))
    sheet.alpha_composite(cell, (x, y))


def main() -> int:
    parser = argparse.ArgumentParser(description="Build a contact sheet for Reference32 ControlNet tile-family outputs.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--run-manifest", default="Temp/AssetForge/reference32_clean_tile_family_controlnet_v1.json")
    parser.add_argument("--reference-manifest", default="Assets/Generated/_Review/reference32_geometry_locked_tile_family_v1/reference32_tile_family_manifest.json")
    parser.add_argument("--out-sheet", default="Assets/Generated/_Review/reference32_clean_tile_family_controlnet_v1/reference32_clean_tile_family_contact_sheet.png")
    parser.add_argument("--out-report", default="Assets/Generated/_Review/reference32_clean_tile_family_controlnet_v1/reference32_clean_tile_family_report.json")
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    run_manifest = read_json(project_root / args.run_manifest)
    reference_manifest = read_json(project_root / args.reference_manifest)
    reference_by_id = {item["id"]: item for item in reference_manifest["items"]}

    rows: list[dict[str, Any]] = []
    for entry in run_manifest.get("jobs", []):
        tile_id = entry["tile_id"]
        reference = reference_by_id[tile_id]
        source_path = project_root / reference["path"].replace("/", "\\")
        source_image = Image.open(source_path).convert("RGBA")
        review_root = project_root / entry["review_root"].replace("/", "\\")
        candidates = []
        for candidate_path in normalized_candidates(project_root, review_root):
            candidate_image = Image.open(candidate_path).convert("RGBA")
            candidates.append({
                "path": rel(project_root, candidate_path),
                "width": candidate_image.width,
                "height": candidate_image.height,
                "colors": color_count(candidate_image),
                "alpha_coverage": alpha_coverage(candidate_image),
                "palette_distance": palette_distance(source_image, candidate_image),
            })
        rows.append({
            **entry,
            "source_path": rel(project_root, source_path),
            "source_tile": reference["source_tile"],
            "label": reference["label"],
            "candidates": candidates,
        })

    cell_w = 210
    row_h = 128
    columns = 4
    sheet_w = cell_w * columns
    sheet_h = 64 + row_h * max(1, len(rows))
    sheet = Image.new("RGBA", (sheet_w, sheet_h), (18, 21, 24, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((12, 10), "Reference32 ControlNet tile-family review", fill=(240, 245, 248, 255))
    draw.text((12, 30), "Review-only. Source tile, then generated normalized candidates.", fill=(172, 184, 190, 255))

    y = 64
    for row in rows:
        draw.rectangle([0, y, sheet_w - 1, y + row_h - 1], outline=(65, 74, 82, 255))
        draw.text((10, y + 8), row["label"], fill=(240, 245, 248, 255))
        draw.text((10, y + 24), row["source_tile"], fill=(172, 184, 190, 255))
        draw.text((10, y + 40), row.get("status", "unknown"), fill=(130, 220, 155, 255) if row.get("status") == "complete" else (235, 190, 120, 255))
        draw_preview(sheet, project_root / row["source_path"].replace("/", "\\"), 108, y + 14, scale=3)
        draw.text((108, y + 112), "source", fill=(172, 184, 190, 255))
        for index, candidate in enumerate(row["candidates"][:2]):
            x = cell_w * (index + 1) + 26
            draw_preview(sheet, project_root / candidate["path"].replace("/", "\\"), x, y + 14, scale=3)
            draw.text((x, y + 112), f"c{index + 1} d{candidate['palette_distance']}", fill=(235, 240, 245, 255))
        y += row_h

    out_sheet = project_root / args.out_sheet
    out_report = project_root / args.out_report
    out_sheet.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out_sheet)
    report = {
        "schema": "lit_iso.asset_forge.reference32_controlnet_family_report.v1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "run_manifest": args.run_manifest,
        "reference_manifest": args.reference_manifest,
        "contact_sheet": rel(project_root, out_sheet),
        "rows": rows,
        "selection_note": "Lower palette_distance is a ranking aid only; manual approval is required before Unity import.",
    }
    write_json(out_report, report)
    print(json.dumps({"ok": True, "sheet": rel(project_root, out_sheet), "report": rel(project_root, out_report), "rows": len(rows)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
