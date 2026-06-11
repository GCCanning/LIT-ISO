#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import shutil
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw

from score_reference32_style_lock import (
    alpha_coverage,
    alpha_iou,
    bbox,
    bbox_delta,
    color_count,
    load_rgba,
    opaque_mask,
    palette_distance,
    repo_path,
)


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def resolve_repo_path(project_root: Path, value: str) -> Path:
    return project_root / value.replace("/", "\\")


def score_pair(project_root: Path, tile_id: str, source_path: str, candidate_path: str, metadata: dict[str, Any]) -> dict[str, Any]:
    source_full = resolve_repo_path(project_root, source_path)
    candidate_full = resolve_repo_path(project_root, candidate_path)
    source = load_rgba(source_full)
    candidate = load_rgba(candidate_full)
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

    status = "pass" if not issues else "reject"
    return {
        "tile_id": tile_id,
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
        "source_path": repo_path(project_root, source_full),
        "candidate_path": repo_path(project_root, candidate_full),
        "issues": issues,
        "warnings": warnings,
        "metadata": metadata,
    }


def load_controlnet_report(project_root: Path, report_path: Path) -> list[dict[str, Any]]:
    report = read_json(report_path)
    scored: list[dict[str, Any]] = []
    for row in report.get("rows", []):
        tile_id = str(row.get("tile_id"))
        source_path = str(row.get("source_path"))
        for index, candidate in enumerate(row.get("candidates", [])):
            candidate_path = str(candidate.get("path"))
            if not candidate_path:
                continue
            metadata = {
                "report": repo_path(project_root, report_path),
                "report_schema": report.get("schema"),
                "candidate_index": index,
                "job_name": row.get("job_name"),
                "template_denoise": row.get("template_denoise"),
                "lora_name": row.get("lora_name"),
                "lora_strength": row.get("lora_strength"),
                "control_strength": row.get("control_strength"),
                "previous_palette_distance": candidate.get("palette_distance"),
                "previous_alpha_coverage": candidate.get("alpha_coverage"),
            }
            scored.append(score_pair(project_root, tile_id, source_path, candidate_path, metadata))
    return scored


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


def draw_review_sheet(project_root: Path, selected: list[dict[str, Any]], output: Path) -> None:
    cell_h = 132
    sheet = Image.new("RGBA", (720, 54 + max(1, len(selected)) * cell_h), (18, 21, 24, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((12, 10), "Reference32 AI candidate gate", fill=(240, 245, 248, 255))
    draw.text((12, 30), "Best AI attempt per tile. Left: source-lock target. Right: AI candidate.", fill=(172, 184, 190, 255))
    for index, record in enumerate(selected):
        y = 54 + index * cell_h
        draw.rectangle([0, y, sheet.width - 1, y + cell_h - 1], outline=(67, 76, 84, 255))
        paste_tile(sheet, resolve_repo_path(project_root, record["source_path"]), 18, y + 18, 3)
        paste_tile(sheet, resolve_repo_path(project_root, record["candidate_path"]), 148, y + 18, 3)
        status = str(record["status"])
        color = (119, 226, 151, 255) if status == "pass" else (245, 96, 96, 255)
        draw.text((268, y + 14), f"{record['tile_id']} | {status} | {record['score']}", fill=color)
        draw.text((268, y + 34), f"geom {record['geometry_score']} pal {record['palette_score']}", fill=(230, 236, 230, 255))
        draw.text((268, y + 54), f"iou {record['alpha_iou']} covD {record['alpha_coverage_delta']}", fill=(172, 184, 190, 255))
        draw.text((268, y + 74), f"colors {record['candidate_colors']}/{record['source_colors']} palD {record['palette_distance']}", fill=(172, 184, 190, 255))
        settings = record.get("metadata", {})
        draw.text(
            (268, y + 94),
            f"d{settings.get('template_denoise')} l{settings.get('lora_strength')} c{settings.get('control_strength')}",
            fill=(172, 184, 190, 255),
        )
        issue_text = ", ".join(record.get("issues") or record.get("warnings") or ["ok"])
        draw.text((488, y + 94), issue_text[:30], fill=(172, 184, 190, 255))
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output)


def copy_selected(project_root: Path, output_root: Path, records: list[dict[str, Any]]) -> list[dict[str, Any]]:
    png_root = output_root / "png"
    png_root.mkdir(parents=True, exist_ok=True)
    copied: list[dict[str, Any]] = []
    for record in records:
        source = resolve_repo_path(project_root, record["candidate_path"])
        suffix = "pass" if record["status"] == "pass" else "best_failed"
        dest = png_root / f"{record['tile_id']}__ai_{suffix}.png"
        shutil.copy2(source, dest)
        copied_record = dict(record)
        copied_record["staged_path"] = repo_path(project_root, dest)
        copied.append(copied_record)
    return copied


def main() -> int:
    parser = argparse.ArgumentParser(description="Gate AI-generated Reference32 tile candidates against source-lock targets.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--reports", nargs="+", required=True)
    parser.add_argument("--output-root", default="Assets/Generated/_Review/reference32_ai_candidate_gate_v1")
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    output_root = project_root / args.output_root
    all_scored: list[dict[str, Any]] = []
    for report in args.reports:
        all_scored.extend(load_controlnet_report(project_root, project_root / report))

    by_tile: dict[str, list[dict[str, Any]]] = {}
    for record in all_scored:
        by_tile.setdefault(record["tile_id"], []).append(record)

    selected = [
        sorted(records, key=lambda item: (-float(item["score"]), str(item["candidate_path"])))[0]
        for _, records in sorted(by_tile.items())
        if records
    ]
    selected = copy_selected(project_root, output_root, selected)
    accepted = [record for record in selected if record["status"] == "pass"]
    rejected_best = [record for record in selected if record["status"] != "pass"]

    review_sheet = output_root / "reference32_ai_candidate_gate_sheet.png"
    draw_review_sheet(project_root, selected, review_sheet)

    generated_utc = datetime.now(timezone.utc).isoformat()
    report_payload = {
        "schema": "lit_iso.asset_forge.reference32_ai_candidate_gate.v1",
        "generated_utc": generated_utc,
        "status": "review_only_not_unity_imported",
        "reports": args.reports,
        "output_root": repo_path(project_root, output_root),
        "review_sheet": repo_path(project_root, review_sheet),
        "candidate_count": len(all_scored),
        "tile_count": len(selected),
        "accepted_count": len(accepted),
        "rejected_best_count": len(rejected_best),
        "selected": selected,
        "all_scores": sorted(all_scored, key=lambda item: (str(item["tile_id"]), -float(item["score"]))),
        "acceptance_policy": {
            "required_status": "pass",
            "meaning": "Candidate must preserve source alpha footprint, coverage, and bbox. Palette shifts may warn but do not fail alone.",
        },
        "next_recommendation": (
            "No AI tile should advance to training or Unity unless accepted_count covers the required tile family. "
            "If rejected_best_count is nonzero, tune generation/template conditioning before adding more training data."
        ),
    }
    write_json(output_root / "candidate_gate_report.json", report_payload)

    decisions = {
        "schema": "lit_iso.asset_forge.review_decisions.v1",
        "pack_name": output_root.name,
        "generated_utc": generated_utc,
        "source_report": repo_path(project_root, output_root / "candidate_gate_report.json"),
        "decision_policy": "auto_reject_failed_style_lock_then_manual_review",
        "total": len(selected),
        "approved_count": 0,
        "pending_count": len(accepted),
        "rejected_count": len(rejected_best),
        "decisions": [
            {
                "id": record["tile_id"],
                "decision": "pending" if record["status"] == "pass" else "rejected",
                "reason": "passes_style_lock_gate" if record["status"] == "pass" else ",".join(record["issues"]),
                "source_path": record["staged_path"],
                "score": record["score"],
                "metadata": record["metadata"],
            }
            for record in selected
        ],
    }
    write_json(output_root / "review_decisions.json", decisions)

    print(json.dumps({
        "ok": True,
        "report": repo_path(project_root, output_root / "candidate_gate_report.json"),
        "sheet": repo_path(project_root, review_sheet),
        "candidate_count": len(all_scored),
        "tile_count": len(selected),
        "accepted_count": len(accepted),
        "rejected_best_count": len(rejected_best),
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
