#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
import shutil
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont

from build_black_mage_identity_lock_report import load_rgba, repo_path, score_identity


DEFAULT_REFERENCE = "Assets/Generated/_Review/black_mage_iso_style_lock_v1/black_mage_normalized_128.png"
DEFAULT_REQUEST_ROOT = "Temp/AssetForge/black_mage_requests/black_mage_iso_idle_s_v14_identity"
DEFAULT_OUTPUT_ROOT = "Assets/Generated/_Review/black_mage_v14_identity_partial"


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def resolve_path(root: Path, value: str | Path) -> Path:
    path = Path(value)
    return path if path.is_absolute() else root / path


def font(size: int) -> ImageFont.ImageFont:
    try:
        return ImageFont.truetype("arial.ttf", size)
    except OSError:
        return ImageFont.load_default()


def checker(size: tuple[int, int]) -> Image.Image:
    image = Image.new("RGBA", size, (32, 36, 42, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], 8):
        for x in range(0, size[0], 8):
            fill = (42, 47, 54, 255) if ((x // 8) + (y // 8)) % 2 == 0 else (32, 36, 42, 255)
            draw.rectangle((x, y, x + 7, y + 7), fill=fill)
    return image


def paste_sprite(board: Image.Image, sprite: Image.Image, x: int, y: int, scale: int = 2) -> None:
    preview = sprite.resize((sprite.width * scale, sprite.height * scale), Image.Resampling.NEAREST)
    cell = checker(preview.size)
    cell.alpha_composite(preview)
    board.alpha_composite(cell, (x, y))


def seed_for(request: dict[str, Any], file_name: str, index: int) -> str:
    base = str(request.get("seed", ""))
    if not base:
        return ""
    try:
        return str(int(base) + index - 1)
    except ValueError:
        return base


def candidate_index(path: Path) -> int:
    match = re.search(r"_v(\d+)\.png$", path.name)
    return int(match.group(1)) if match else 0


def draw_sheet(reference: Image.Image, records: list[dict[str, Any]], out_path: Path, title: str) -> None:
    title_font = font(14)
    body_font = font(11)
    cell_w = 286
    cell_h = 354
    cols = 3
    rows = 1 + ((len(records) + cols - 1) // cols)
    board = Image.new("RGBA", (cols * cell_w, 76 + rows * cell_h), (16, 19, 24, 255))
    draw = ImageDraw.Draw(board)
    draw.text((14, 12), title, fill=(246, 248, 252, 255), font=title_font)
    draw.text(
        (14, 34),
        "Goal: candidate must preserve the supplied S/front source before any 8D expansion.",
        fill=(172, 183, 196, 255),
        font=body_font,
    )
    paste_sprite(board, reference, 14, 76, 2)
    draw.text((14, 76 + 260), "REFERENCE", fill=(237, 220, 115, 255), font=title_font)
    draw.text((14, 76 + 282), "Required identity target", fill=(184, 196, 208, 255), font=body_font)

    for offset, record in enumerate(records, start=1):
        col = offset % cols
        row = offset // cols
        x = col * cell_w + 12
        y = 76 + row * cell_h
        image = load_rgba(Path(record["absolute_path"]))
        paste_sprite(board, image, x, y, 2)
        identity = record["identity"]
        color = (239, 106, 92, 255) if identity["status"] == "identity_fail" else (116, 220, 145, 255)
        draw.text((x, y + 260), f"candidate {record['index']} {identity['status']}", fill=color, font=title_font)
        lines = [
            f"score {identity['identity_score']} seed {record.get('seed', '')}",
            f"alphaIoU {identity['feature_spatial_iou']['alpha']} frontSpatial {identity['identity_gate']['front_spatial_score']}",
            f"meanD {identity['mean_rgb_delta']} palD {identity['palette_distance']}",
            ", ".join(identity["issues"] or identity["warnings"] or ["manual review"]),
        ]
        for line_index, line in enumerate(lines):
            draw.text((x, y + 282 + line_index * 15), line[:42], fill=(184, 196, 208, 255), font=body_font)

    out_path.parent.mkdir(parents=True, exist_ok=True)
    board.save(out_path)


def main() -> int:
    parser = argparse.ArgumentParser(description="Review partial request outputs against the black mage reference identity lock.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--request-root", default=DEFAULT_REQUEST_ROOT)
    parser.add_argument("--reference", default=DEFAULT_REFERENCE)
    parser.add_argument("--output-root", default=DEFAULT_OUTPUT_ROOT)
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    request_root = resolve_path(project_root, args.request_root)
    reference_path = resolve_path(project_root, args.reference)
    out_root = resolve_path(project_root, args.output_root)
    cleaned_root = request_root / "Outputs" / "cleaned"
    request_path = request_root / "generation_request.json"
    if not cleaned_root.exists():
        raise FileNotFoundError(f"Missing cleaned output folder: {cleaned_root}")
    if not request_path.exists():
        raise FileNotFoundError(f"Missing generation request: {request_path}")

    request = read_json(request_path)
    job_name = str(request.get("job_name") or request_root.name)
    safe_job_name = "".join(char if char.isalnum() or char in "_.-" else "_" for char in job_name).strip("._")
    reference = load_rgba(reference_path)
    out_root.mkdir(parents=True, exist_ok=True)
    records: list[dict[str, Any]] = []
    for path in sorted(cleaned_root.glob("*.png"), key=candidate_index):
        index = candidate_index(path) or len(records) + 1
        review_path = out_root / path.name
        shutil.copy2(path, review_path)
        candidate = load_rgba(review_path)
        identity = score_identity(reference, candidate, "S")
        records.append(
            {
                "index": index,
                "path": repo_path(project_root, review_path),
                "absolute_path": str(review_path),
                "source_path": repo_path(project_root, path),
                "seed": seed_for(request, path.name, index),
                "identity": identity,
            }
        )

    sheet_path = out_root / f"{safe_job_name}_identity_sheet.png"
    draw_sheet(reference, records, sheet_path, f"{job_name} Identity Review")
    fail_count = len([record for record in records if record["identity"]["status"] == "identity_fail"])
    best = max(records, key=lambda record: float(record["identity"]["identity_score"])) if records else None
    report = {
        "schema": "lit_iso.asset_forge.black_mage_request_identity_review.v1",
        "generated_utc": utc_now(),
        "status": "identity_gate_failed" if fail_count else "identity_review_only_not_unity_imported",
        "job_name": job_name,
        "request_root": repo_path(project_root, request_root),
        "generation_request": repo_path(project_root, request_path),
        "reference": repo_path(project_root, reference_path),
        "sheet": repo_path(project_root, sheet_path),
        "candidate_count": len(records),
        "identity_fail_count": fail_count,
        "best_candidate": {
            "path": best["path"],
            "identity_score": best["identity"]["identity_score"],
            "status": best["identity"]["status"],
            "issues": best["identity"]["issues"],
        }
        if best
        else None,
        "records": [{key: value for key, value in record.items() if key != "absolute_path"} for record in records],
        "conclusion": (
            "v14 S/front did not preserve the original mage identity; do not expand this setting to 8D."
            if fail_count
            else "v14 S/front passes the automated identity gate and needs manual visual approval."
        ),
        "next_recommendations": [
            "Stop rerolling this exact setting if all candidates fail; it is over-regularizing the mage into a generic wizard.",
            "Next attempt should use image-to-image reconstruction from the original S anchor with no OpenPose/template conflict, then separately solve rotations.",
            "If reconstruction still fails, build/train from paired source-anchor variations instead of relying on IP-Adapter alone.",
        ],
    }
    report_path = out_root / f"{safe_job_name}_identity_report.json"
    write_json(report_path, report)
    print(
        json.dumps(
            {
                "ok": True,
                "report": repo_path(project_root, report_path),
                "sheet": repo_path(project_root, sheet_path),
                "candidate_count": len(records),
                "identity_fail_count": fail_count,
                "best_score": report["best_candidate"]["identity_score"] if report["best_candidate"] else None,
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
