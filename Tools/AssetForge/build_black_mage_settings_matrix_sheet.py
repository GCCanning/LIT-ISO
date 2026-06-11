#!/usr/bin/env python3
"""Build a compact contact sheet for black mage settings matrix runs."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def rel(root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path.resolve()).replace("\\", "/")


def checker(size: tuple[int, int]) -> Image.Image:
    image = Image.new("RGBA", size, (27, 30, 34, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], 8):
        for x in range(0, size[0], 8):
            if (x // 8 + y // 8) % 2 == 0:
                draw.rectangle([x, y, x + 7, y + 7], fill=(38, 42, 48, 255))
    return image


def collect_job_outputs(project_root: Path, job_name: str) -> list[dict[str, Any]]:
    review_root = project_root / "Assets" / "Generated" / "_Review" / job_name
    report_path = review_root / "review_report.json"
    if not report_path.exists():
        return []
    report = read_json(report_path)
    return list(report.get("items", []))


def draw_cell(sheet: Image.Image, draw: ImageDraw.ImageDraw, image_path: Path, label: str, x: int, y: int) -> None:
    draw.rectangle([x, y, x + 139, y + 159], fill=(24, 27, 31, 255), outline=(66, 74, 82, 255))
    image = Image.open(image_path).convert("RGBA")
    preview = checker((128, 128))
    preview.alpha_composite(image.resize((128, 128), Image.Resampling.NEAREST))
    sheet.alpha_composite(preview, (x + 6, y + 6))
    draw.text((x + 8, y + 138), label[:23], fill=(222, 230, 220, 255))


def main() -> int:
    parser = argparse.ArgumentParser(description="Build black mage v10 settings matrix sheet.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--run-manifest", default="Assets/Generated/_Review/black_mage_v10_ne_matrix/black_mage_v10_ne_matrix_run_manifest.json")
    parser.add_argument("--out-root", default="Assets/Generated/_Review/black_mage_v10_ne_matrix")
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    run_manifest_path = project_root / args.run_manifest
    run_manifest = read_json(run_manifest_path)
    rows = []
    for entry in run_manifest.get("matrix", []):
        suffix = entry["suffix"]
        job_name = f"black_mage_iso_idle_ne_{suffix}"
        outputs = collect_job_outputs(project_root, job_name)
        rows.append({"entry": entry, "job_name": job_name, "outputs": outputs})

    cell_w = 146
    row_h = 180
    label_w = 230
    header_h = 58
    max_outputs = max((len(row["outputs"]) for row in rows), default=0)
    sheet = Image.new("RGBA", (label_w + max(1, max_outputs) * cell_w, header_h + max(1, len(rows)) * row_h), (16, 18, 20, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((12, 10), "Black mage v10 NE settings matrix", fill=(238, 242, 238, 255))
    draw.text((12, 30), "Goal: balance v8 style fidelity with v9 back-facing direction control.", fill=(160, 170, 164, 255))

    manifest_rows = []
    for row_index, row in enumerate(rows):
        y = header_h + row_index * row_h
        entry = row["entry"]
        label = f"{entry['suffix']}\nstyle {entry['style_weight']} control {entry['control_strength']}\ndenoise {entry['template_denoise']}"
        draw.rectangle([0, y, label_w - 1, y + row_h - 1], fill=(22, 25, 29, 255), outline=(60, 68, 75, 255))
        for line_index, line in enumerate(label.splitlines()):
            draw.text((12, y + 18 + line_index * 16), line, fill=(224, 232, 222, 255))
        output_records = []
        for output_index, item in enumerate(row["outputs"]):
            image_path = project_root / item["path"].replace("/", "\\")
            x = label_w + output_index * cell_w
            draw_cell(sheet, draw, image_path, f"c{output_index + 1} {item.get('status', '')}", x, y + 8)
            output_records.append({"path": item["path"], "status": item.get("status", ""), "name": item.get("name", "")})
        manifest_rows.append({**row, "outputs": output_records})

    out_root = project_root / args.out_root
    out_root.mkdir(parents=True, exist_ok=True)
    sheet_path = out_root / "black_mage_v10_ne_matrix_contact_sheet.png"
    sheet.save(sheet_path)
    manifest_path = out_root / "black_mage_v10_ne_matrix_manifest.json"
    manifest = {
        "schema": "lit_iso.asset_forge.black_mage_settings_matrix_sheet.v1",
        "run_manifest": rel(project_root, run_manifest_path),
        "contact_sheet": rel(project_root, sheet_path),
        "rows": manifest_rows,
        "status": "review_ready",
    }
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps({"manifest": rel(project_root, manifest_path), "contact_sheet": rel(project_root, sheet_path), "rows": len(rows)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
