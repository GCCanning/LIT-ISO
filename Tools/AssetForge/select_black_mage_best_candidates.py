#!/usr/bin/env python3
"""Stage the best black mage candidates per direction into a clean review pack."""

from __future__ import annotations

import argparse
import json
import shutil
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw


DEFAULT_DIRECTIONS = ["NE", "NW", "SE", "SW"]


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def rel(root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path.resolve()).replace("\\", "/")


def draw_checker(size: tuple[int, int]) -> Image.Image:
    image = Image.new("RGBA", size, (27, 30, 34, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], 8):
        for x in range(0, size[0], 8):
            if (x // 8 + y // 8) % 2 == 0:
                draw.rectangle([x, y, x + 7, y + 7], fill=(38, 42, 48, 255))
    return image


def draw_contact_sheet(project_root: Path, records: list[dict[str, Any]], output_path: Path, style_reference: Path | None, variant: str) -> None:
    cell_w = 176
    cell_h = 174
    header_h = 56
    columns = len(records) + (1 if style_reference else 0)
    sheet = Image.new("RGBA", (max(1, columns) * cell_w, header_h + cell_h), (17, 19, 22, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((12, 10), f"Black mage selected {variant} candidates", fill=(238, 242, 238, 255))
    draw.text((12, 30), "Review only. Manually approve before training capture or Unity import.", fill=(160, 170, 164, 255))

    x = 0
    if style_reference and style_reference.exists():
        paste_cell(sheet, draw, style_reference, "STYLE", "source", "", x, header_h, cell_w, cell_h)
        x += cell_w
    for record in records:
        paste_cell(
            sheet,
            draw,
            project_root / record["path"].replace("/", "\\"),
            record["direction"],
            f"seed {record['seed']} score {record['score']}",
            record["status"],
            x,
            header_h,
            cell_w,
            cell_h,
        )
        x += cell_w

    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)


def paste_cell(
    sheet: Image.Image,
    draw: ImageDraw.ImageDraw,
    source_path: Path,
    title: str,
    subtitle: str,
    status: str,
    x: int,
    y: int,
    cell_w: int,
    cell_h: int,
) -> None:
    draw.rectangle([x, y, x + cell_w - 1, y + cell_h - 1], fill=(24, 27, 31, 255), outline=(66, 74, 82, 255))
    image = Image.open(source_path).convert("RGBA")
    preview = draw_checker((128, 128))
    preview.alpha_composite(image.resize((128, 128), Image.Resampling.NEAREST))
    sheet.alpha_composite(preview, (x + 24, y + 8))
    draw.text((x + 10, y + 138), title, fill=(236, 241, 232, 255))
    draw.text((x + 10, y + 152), subtitle[:32], fill=(168, 180, 170, 255))
    if status:
        draw.text((x + 118, y + 138), status[:18], fill=(126, 220, 151, 255))


def main() -> int:
    parser = argparse.ArgumentParser(description="Select best black mage candidates from strict QC report.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--variant", default="v8")
    parser.add_argument("--qc-report", default="Assets/Generated/_Review/black_mage_iso_renders_v8/_v8_strict_qc_report.json")
    parser.add_argument("--out-root", default="Assets/Generated/_Review/black_mage_iso_selected_v8")
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    report_path = project_root / args.qc_report
    out_root = project_root / args.out_root
    report = read_json(report_path)
    directions = [str(direction).upper() for direction in report.get("directions", [])]
    if not directions:
        directions = DEFAULT_DIRECTIONS
    selected: list[dict[str, Any]] = []

    for direction in directions:
        best = report.get("best_by_direction", {}).get(direction, [])
        if not best:
            continue
        candidate = best[0]
        source = project_root / candidate["path"].replace("/", "\\")
        dest = out_root / f"black_mage_{direction.lower()}_selected_{args.variant}.png"
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, dest)
        selected.append(
            {
                "direction": direction,
                "path": rel(project_root, dest),
                "source_path": candidate["path"],
                "seed": candidate.get("seed", ""),
                "score": candidate.get("score"),
                "status": candidate.get("status"),
                "issues": candidate.get("issues", []),
                "warnings": candidate.get("warnings", []),
                "metrics": candidate.get("metrics", {}),
            }
        )

    style_reference = project_root / str(report.get("style_reference", "")).replace("/", "\\")
    contact_sheet = out_root / f"black_mage_selected_{args.variant}_contact_sheet.png"
    draw_contact_sheet(project_root, selected, contact_sheet, style_reference if style_reference.exists() else None, args.variant)

    manifest = {
        "schema": "lit_iso.asset_forge.black_mage_selected_candidates.v1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "variant": args.variant,
        "source_qc_report": rel(project_root, report_path),
        "out_root": rel(project_root, out_root),
        "contact_sheet": rel(project_root, contact_sheet),
        "selected_count": len(selected),
        "selected": selected,
        "status": "review_only_manual_approval_required",
        "manual_review_note": "Selected automatically by structural score. Manual art review still decides whether these are training-ready.",
        "next_gate": "Approve one candidate per direction or adjust templates/settings and rerun.",
    }
    manifest_path = out_root / f"black_mage_selected_{args.variant}_manifest.json"
    write_json(manifest_path, manifest)
    print(
        json.dumps(
            {
                "manifest": rel(project_root, manifest_path),
                "contact_sheet": rel(project_root, contact_sheet),
                "selected_count": len(selected),
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
