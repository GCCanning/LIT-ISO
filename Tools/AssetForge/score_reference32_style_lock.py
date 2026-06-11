#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import math
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw


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


def resolve_repo_path(root: Path, value: str) -> Path:
    return root / value.replace("/", "\\")


def load_rgba(path: Path) -> Image.Image:
    image = Image.open(path).convert("RGBA")
    if image.size != (32, 32):
        image = image.resize((32, 32), Image.Resampling.NEAREST)
    return image


def opaque_mask(image: Image.Image, threshold: int = 0) -> set[tuple[int, int]]:
    alpha = image.getchannel("A")
    pixels = alpha.load()
    return {
        (x, y)
        for y in range(image.height)
        for x in range(image.width)
        if pixels[x, y] > threshold
    }


def bbox(mask: set[tuple[int, int]]) -> list[int] | None:
    if not mask:
        return None
    xs = [x for x, _ in mask]
    ys = [y for _, y in mask]
    return [min(xs), min(ys), max(xs), max(ys)]


def alpha_iou(a: Image.Image, b: Image.Image) -> float:
    ma = opaque_mask(a)
    mb = opaque_mask(b)
    union = len(ma | mb)
    if union == 0:
        return 1.0
    return len(ma & mb) / union


def alpha_coverage(image: Image.Image) -> float:
    mask = opaque_mask(image)
    return len(mask) / float(max(1, image.width * image.height))


def color_count(image: Image.Image) -> int:
    colors = {
        rgba[:3]
        for rgba in iter_rgba(image)
        if rgba[3] > 0
    }
    return len(colors)


def iter_rgba(image: Image.Image):
    if hasattr(image, "get_flattened_data"):
        return image.get_flattened_data()
    return image.getdata()


def palette(image: Image.Image, max_colors: int = 16) -> list[tuple[tuple[int, int, int], int]]:
    counts: dict[tuple[int, int, int], int] = {}
    for rgba in iter_rgba(image):
        if rgba[3] <= 0:
            continue
        rgb = rgba[:3]
        counts[rgb] = counts.get(rgb, 0) + 1
    return sorted(counts.items(), key=lambda item: item[1], reverse=True)[:max_colors]


def rgb_distance(a: tuple[int, int, int], b: tuple[int, int, int]) -> float:
    return math.sqrt(sum((float(a[i]) - float(b[i])) ** 2 for i in range(3)))


def palette_distance(source: Image.Image, candidate: Image.Image) -> float:
    source_palette = palette(source, 24)
    candidate_palette = palette(candidate, 24)
    if not source_palette and not candidate_palette:
        return 0.0
    if not source_palette or not candidate_palette:
        return 999.0
    total_weight = sum(weight for _, weight in candidate_palette)
    weighted = 0.0
    for rgb, weight in candidate_palette:
        nearest = min(rgb_distance(rgb, source_rgb) for source_rgb, _ in source_palette)
        weighted += nearest * weight
    return weighted / float(max(1, total_weight))


def bbox_delta(a: list[int] | None, b: list[int] | None) -> int | None:
    if a is None or b is None:
        return None
    return sum(abs(a[index] - b[index]) for index in range(4))


def find_source_rows(source_manifest: dict[str, Any]) -> dict[str, dict[str, Any]]:
    rows: dict[str, dict[str, Any]] = {}
    for row in source_manifest.get("rows", []):
        tile_id = str(row.get("id"))
        for variant in row.get("variants", []):
            if variant.get("variant_id") == "source":
                rows[tile_id] = {
                    "row": row,
                    "variant": variant,
                    "path": variant.get("path"),
                }
                break
    return rows


def score_item(project_root: Path, source_rows: dict[str, dict[str, Any]], item: dict[str, Any]) -> dict[str, Any]:
    tile_id = str(item["id"])
    candidate_path = resolve_repo_path(project_root, str(item["path"]))
    source_record = source_rows.get(tile_id)
    if source_record is None:
        return {
            "id": tile_id,
            "candidate_path": item.get("path"),
            "status": "missing_source",
            "issues": ["missing_source_reference"],
        }

    source_path = resolve_repo_path(project_root, str(source_record["path"]))
    source = load_rgba(source_path)
    candidate = load_rgba(candidate_path)
    source_mask = opaque_mask(source)
    candidate_mask = opaque_mask(candidate)
    source_bbox = bbox(source_mask)
    candidate_bbox = bbox(candidate_mask)

    iou = alpha_iou(source, candidate)
    coverage_delta = abs(alpha_coverage(source) - alpha_coverage(candidate))
    colors_source = color_count(source)
    colors_candidate = color_count(candidate)
    color_delta = abs(colors_source - colors_candidate)
    pal_dist = palette_distance(source, candidate)
    bbox_diff = bbox_delta(source_bbox, candidate_bbox)

    geometry_score = max(0.0, 100.0 - ((1.0 - iou) * 130.0) - (coverage_delta * 120.0) - float(bbox_diff or 0) * 1.5)
    palette_score = max(0.0, 100.0 - pal_dist * 1.8 - color_delta * 2.5)
    total_score = round(geometry_score * 0.68 + palette_score * 0.32, 3)

    issues: list[str] = []
    warnings: list[str] = []
    if candidate.size != source.size:
        issues.append("size_changed")
    if iou < 0.96:
        issues.append("alpha_footprint_drift")
    elif iou < 0.99:
        warnings.append("minor_alpha_footprint_drift")
    if coverage_delta > 0.03:
        issues.append("alpha_coverage_drift")
    if bbox_diff is not None and bbox_diff > 2:
        issues.append("bbox_drift")
    if pal_dist > 28:
        warnings.append("large_palette_shift")
    if colors_candidate > max(16, colors_source + 8):
        warnings.append("too_many_colors_for_reference32")

    status = "pass" if not issues else "review"
    return {
        "id": tile_id,
        "candidate_path": repo_path(project_root, candidate_path),
        "source_path": repo_path(project_root, source_path),
        "variant_id": item.get("variant_id"),
        "status": status,
        "score": total_score,
        "geometry_score": round(geometry_score, 3),
        "palette_score": round(palette_score, 3),
        "alpha_iou": round(iou, 5),
        "alpha_coverage_delta": round(coverage_delta, 5),
        "bbox_delta": bbox_diff,
        "source_bbox": source_bbox,
        "candidate_bbox": candidate_bbox,
        "source_colors": colors_source,
        "candidate_colors": colors_candidate,
        "color_delta": color_delta,
        "palette_distance": round(pal_dist, 3),
        "issues": issues,
        "warnings": warnings,
    }


def draw_checker(size: tuple[int, int]) -> Image.Image:
    image = Image.new("RGBA", size, (28, 32, 36, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], 8):
        for x in range(0, size[0], 8):
            fill = (42, 47, 52, 255) if ((x // 8) + (y // 8)) % 2 == 0 else (30, 34, 38, 255)
            draw.rectangle([x, y, x + 7, y + 7], fill=fill)
    return image


def paste_tile(sheet: Image.Image, path: Path, x: int, y: int, scale: int = 3) -> None:
    image = load_rgba(path)
    preview = image.resize((image.width * scale, image.height * scale), Image.Resampling.NEAREST)
    cell = draw_checker(preview.size)
    cell.alpha_composite(preview)
    sheet.alpha_composite(cell, (x, y))


def draw_score_sheet(project_root: Path, records: list[dict[str, Any]], output: Path, title: str) -> None:
    cell_w = 250
    cell_h = 132
    rows = max(1, len(records))
    sheet = Image.new("RGBA", (cell_w * 2, 54 + cell_h * rows), (18, 21, 24, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((12, 10), title, fill=(240, 245, 248, 255))
    draw.text((12, 30), "Left: source target. Right: selected candidate. Review-only style-lock scorecard.", fill=(172, 184, 190, 255))
    for index, record in enumerate(records):
        y = 54 + index * cell_h
        draw.rectangle([0, y, sheet.width - 1, y + cell_h - 1], outline=(67, 76, 84, 255))
        source = project_root / str(record.get("source_path", "")).replace("/", "\\")
        candidate = project_root / str(record.get("candidate_path", "")).replace("/", "\\")
        if source.exists():
            paste_tile(sheet, source, 18, y + 18, 3)
        if candidate.exists():
            paste_tile(sheet, candidate, 148, y + 18, 3)
        status = str(record.get("status"))
        color = (119, 226, 151, 255) if status == "pass" else (245, 196, 96, 255)
        draw.text((268, y + 14), f"{record.get('id')} | {status} | {record.get('score')}", fill=color)
        draw.text((268, y + 34), f"geom {record.get('geometry_score')} pal {record.get('palette_score')}", fill=(230, 236, 230, 255))
        draw.text((268, y + 54), f"iou {record.get('alpha_iou')} covD {record.get('alpha_coverage_delta')}", fill=(172, 184, 190, 255))
        draw.text((268, y + 74), f"colors {record.get('candidate_colors')}/{record.get('source_colors')} palD {record.get('palette_distance')}", fill=(172, 184, 190, 255))
        issue_text = ", ".join(record.get("issues") or record.get("warnings") or ["ok"])
        draw.text((268, y + 94), issue_text[:54], fill=(172, 184, 190, 255))
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output)


def main() -> int:
    parser = argparse.ArgumentParser(description="Score selected Reference32 tiles against source style-lock targets.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--candidate-manifest", required=True)
    parser.add_argument("--source-manifest", default="Assets/Generated/_Review/reference32_style_locked_variants_v2/reference32_style_locked_variants_manifest.json")
    parser.add_argument("--output-json", default="")
    parser.add_argument("--output-sheet", default="")
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    candidate_manifest_path = project_root / args.candidate_manifest
    source_manifest_path = project_root / args.source_manifest
    candidate_manifest = read_json(candidate_manifest_path)
    source_manifest = read_json(source_manifest_path)
    source_rows = find_source_rows(source_manifest)

    records = [
        score_item(project_root, source_rows, item)
        for item in candidate_manifest.get("selected", [])
    ]
    pass_count = len([item for item in records if item.get("status") == "pass"])
    review_count = len(records) - pass_count
    score_mean = round(sum(float(item.get("score") or 0.0) for item in records) / float(max(1, len(records))), 3)

    output_root = candidate_manifest_path.parent
    output_json = project_root / args.output_json if args.output_json else output_root / "style_lock_scorecard.json"
    output_sheet = project_root / args.output_sheet if args.output_sheet else output_root / "style_lock_score_sheet.png"

    payload = {
        "schema": "lit_iso.asset_forge.reference32_style_lock_scorecard.v1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "candidate_manifest": repo_path(project_root, candidate_manifest_path),
        "source_manifest": repo_path(project_root, source_manifest_path),
        "output_sheet": repo_path(project_root, output_sheet),
        "total": len(records),
        "pass_count": pass_count,
        "review_count": review_count,
        "score_mean": score_mean,
        "acceptance_note": "Scores validate geometry/style similarity only. Manual art approval and license verification are still required.",
        "items": records,
    }
    write_json(output_json, payload)
    draw_score_sheet(project_root, records, output_sheet, f"Reference32 style-lock scorecard: {candidate_manifest_path.parent.name}")

    print(json.dumps({
        "ok": True,
        "scorecard": repo_path(project_root, output_json),
        "sheet": repo_path(project_root, output_sheet),
        "pass_count": pass_count,
        "review_count": review_count,
        "score_mean": score_mean,
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
