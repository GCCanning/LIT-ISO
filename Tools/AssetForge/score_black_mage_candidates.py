#!/usr/bin/env python3
"""Score black mage isometric candidates against stricter review heuristics.

This is not a semantic vision model. It is a hard gate for obvious failures:
floor/background blobs, spell-effect arcs, over-wide silhouettes, tiny broken
sprites, and poor alpha coverage relative to the supplied style reference.
"""

from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw


DEFAULT_MANIFEST = Path("Assets/Generated/_Review/black_mage_iso_renders_v6/_v6_candidate_manifest.json")
DEFAULT_OUTPUT = Path("Assets/Generated/_Review/black_mage_iso_renders_v6/_v6_strict_qc_report.json")
DEFAULT_SHEET = Path("Assets/Generated/_Review/black_mage_iso_renders_v6/_v6_strict_qc_sheet.png")
CANONICAL_DIRECTIONS = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"]


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


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int] | None:
    return image.convert("RGBA").getchannel("A").getbbox()


def alpha_coverage(image: Image.Image) -> float:
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A")
    histogram = alpha.histogram()
    return (rgba.width * rgba.height - histogram[0]) / (rgba.width * rgba.height)


def bbox_metrics(image: Image.Image) -> dict[str, Any]:
    bbox = alpha_bbox(image)
    if not bbox:
        return {
            "bbox": None,
            "bbox_width": 0,
            "bbox_height": 0,
            "center_x": image.width / 2,
            "center_y": image.height / 2,
            "alpha_coverage": 0.0,
        }
    left, top, right, bottom = bbox
    return {
        "bbox": [left, top, right, bottom],
        "bbox_width": right - left,
        "bbox_height": bottom - top,
        "center_x": (left + right) / 2,
        "center_y": (top + bottom) / 2,
        "alpha_coverage": alpha_coverage(image),
    }


def score_candidate(candidate: dict[str, Any], metrics: dict[str, Any], ref_metrics: dict[str, Any]) -> dict[str, Any]:
    issues: list[str] = []
    warnings: list[str] = []

    coverage = float(metrics["alpha_coverage"])
    bbox_width = float(metrics["bbox_width"])
    bbox_height = float(metrics["bbox_height"])
    ref_coverage = max(0.001, float(ref_metrics["alpha_coverage"]))
    ref_width = max(1.0, float(ref_metrics["bbox_width"]))
    ref_height = max(1.0, float(ref_metrics["bbox_height"]))

    coverage_delta = abs(coverage - ref_coverage)
    width_ratio = bbox_width / ref_width
    height_ratio = bbox_height / ref_height
    center_delta = abs(float(metrics["center_x"]) - 64.0)

    if coverage > ref_coverage * 1.65:
        issues.append("excess_alpha_likely_floor_background_or_spell_effect")
    if coverage < ref_coverage * 0.45:
        issues.append("too_sparse_or_broken_silhouette")
    if width_ratio > 1.55:
        issues.append("too_wide_likely_spell_arc_or_extra_object")
    if height_ratio < 0.55:
        issues.append("too_short_or_missing_body")
    if center_delta > 22:
        warnings.append("off_center_anchor")
    if bbox_width >= 104 and coverage > 0.42:
        issues.append("large_background_or_effect_blob")

    score = (
        coverage_delta * 160.0
        + abs(width_ratio - 1.0) * 28.0
        + abs(height_ratio - 1.0) * 18.0
        + center_delta * 0.4
        + len(issues) * 100.0
        + len(warnings) * 15.0
    )

    status = "reject" if issues else "review_candidate"
    return {
        "path": candidate["path"],
        "direction": candidate["direction"],
        "seed": candidate.get("seed", ""),
        "status": status,
        "score": round(score, 3),
        "issues": issues,
        "warnings": warnings,
        "metrics": {
            "alpha_coverage": round(coverage, 4),
            "bbox": metrics["bbox"],
            "bbox_width": round(bbox_width, 2),
            "bbox_height": round(bbox_height, 2),
            "width_ratio_to_ref": round(width_ratio, 3),
            "height_ratio_to_ref": round(height_ratio, 3),
            "center_delta": round(center_delta, 3),
        },
    }


def draw_checker(size: tuple[int, int]) -> Image.Image:
    image = Image.new("RGBA", size, (28, 32, 36, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], 8):
        for x in range(0, size[0], 8):
            if (x // 8 + y // 8) % 2 == 0:
                draw.rectangle([x, y, x + 7, y + 7], fill=(38, 43, 48, 255))
    return image


def draw_qc_sheet(project_root: Path, ref_path: Path, results: list[dict[str, Any]], output_path: Path, variant: str, directions: list[str]) -> None:
    grouped = {direction: [item for item in results if item["direction"] == direction] for direction in directions}
    columns = 5
    cell_w = 172
    row_h = 170
    width = columns * cell_w
    height = 58 + len(directions) * row_h
    sheet = Image.new("RGBA", (width, height), (16, 18, 20, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((12, 10), f"Black mage {variant} strict QC", fill=(238, 242, 238, 255))
    draw.text((12, 30), "Structural art-direction gate. Rejects are not training-ready.", fill=(158, 170, 164, 255))

    ref = Image.open(ref_path).convert("RGBA")
    ref_preview = draw_checker((128, 128))
    ref_preview.alpha_composite(ref.resize((128, 128), Image.Resampling.NEAREST))

    for row_index, direction in enumerate(directions):
        y = 58 + row_index * row_h
        draw.rectangle([0, y, width - 1, y + row_h - 1], outline=(58, 66, 72, 255))
        draw.text((12, y + 64), direction, fill=(232, 238, 232, 255))
        sheet.alpha_composite(ref_preview, (cell_w - 142, y + 10))
        draw.text((cell_w - 134, y + 140), "style ref", fill=(226, 212, 120, 255))
        for index, result in enumerate(grouped[direction][:4], start=1):
            x = index * cell_w + 14
            image = Image.open(project_root / result["path"].replace("/", "\\")).convert("RGBA")
            preview = draw_checker((128, 128))
            preview.alpha_composite(image.resize((128, 128), Image.Resampling.NEAREST))
            sheet.alpha_composite(preview, (x, y + 10))
            status_color = (232, 92, 78, 255) if result["status"] == "reject" else (116, 220, 145, 255)
            draw.text((x, y + 140), f"c{index} {result['status']} {result['score']}", fill=status_color)
            issue = result["issues"][0] if result["issues"] else "manual review"
            draw.text((x, y + 154), issue[:25], fill=(166, 176, 170, 255))

    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)


def main() -> int:
    parser = argparse.ArgumentParser(description="Score black mage candidates with strict heuristics.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--manifest", default=str(DEFAULT_MANIFEST))
    parser.add_argument("--out-report", default=str(DEFAULT_OUTPUT))
    parser.add_argument("--out-sheet", default=str(DEFAULT_SHEET))
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    manifest = read_json(project_root / args.manifest)
    variant = str(manifest.get("variant", "candidates"))
    requested_directions = [str(direction).upper() for direction in manifest.get("directions", [])]
    directions = [direction for direction in CANONICAL_DIRECTIONS if direction in requested_directions]
    if not directions:
        directions = [direction for direction in CANONICAL_DIRECTIONS if any(item.get("direction") == direction for item in manifest.get("candidates", []))]
    if not directions:
        directions = ["NE", "NW", "SE", "SW"]
    ref_path = project_root / manifest["style_reference"].replace("/", "\\")
    ref_metrics = bbox_metrics(Image.open(ref_path).convert("RGBA"))
    scored: list[dict[str, Any]] = []
    for candidate in manifest.get("candidates", []):
        image_path = project_root / candidate["path"].replace("/", "\\")
        metrics = bbox_metrics(Image.open(image_path).convert("RGBA"))
        scored.append(score_candidate(candidate, metrics, ref_metrics))

    grouped_best = {}
    for direction in directions:
        candidates = [item for item in scored if item["direction"] == direction]
        grouped_best[direction] = sorted(candidates, key=lambda item: item["score"])[:2]

    report = {
        "schema": "lit_iso.asset_forge.black_mage_strict_qc.v1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "source_manifest": args.manifest,
        "style_reference": manifest["style_reference"],
        "directions": directions,
        "reference_metrics": {
            **ref_metrics,
            "alpha_coverage": round(float(ref_metrics["alpha_coverage"]), 4),
        },
        "candidate_count": len(scored),
        "reject_count": len([item for item in scored if item["status"] == "reject"]),
        "review_candidate_count": len([item for item in scored if item["status"] == "review_candidate"]),
        "best_by_direction": grouped_best,
        "candidates": scored,
        "recommendation": [
            f"Do not train from {variant} wholesale without manual visual approval.",
            "Reject spell/floor/effect artifacts, wrong silhouette width, and direction mismatches before training capture.",
            "Prefer review_candidate status only as rough direction evidence until the user approves one candidate per direction.",
        ],
    }
    out_report = project_root / args.out_report
    out_sheet = project_root / args.out_sheet
    write_json(out_report, report)
    draw_qc_sheet(project_root, ref_path, scored, out_sheet, variant, directions)
    print(
        json.dumps(
            {
                "report": rel(project_root, out_report),
                "sheet": rel(project_root, out_sheet),
                "candidate_count": report["candidate_count"],
                "reject_count": report["reject_count"],
                "review_candidate_count": report["review_candidate_count"],
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
