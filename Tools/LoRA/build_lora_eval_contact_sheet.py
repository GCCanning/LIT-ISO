#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def load_font(size: int) -> ImageFont.ImageFont:
    for name in ("segoeui.ttf", "arial.ttf", "calibri.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int] | None:
    return image.convert("RGBA").split()[-1].getbbox()


def alpha_coverage(image: Image.Image) -> float:
    alpha = image.convert("RGBA").split()[-1]
    total = alpha.width * alpha.height
    if total <= 0:
        return 0.0
    return sum(alpha.histogram()[9:]) / total


def repo_rel(project_root: Path | None, path: Path) -> str:
    if project_root:
        try:
            return path.resolve().relative_to(project_root.resolve()).as_posix()
        except ValueError:
            pass
    return str(path.resolve()).replace("\\", "/")


def collect_from_manifest(eval_dir: Path, manifest_path: Path) -> list[dict[str, Any]]:
    if not manifest_path.exists():
        return []
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    entries: list[dict[str, Any]] = []
    for result in manifest.get("results", []):
        outputs = result.get("outputs") or []
        for output in outputs:
            path = Path(output)
            if not path.is_absolute():
                path = eval_dir / path
            entries.append(
                {
                    "path": path,
                    "name": result.get("name") or path.stem,
                    "status": result.get("status", "review"),
                    "category": result.get("category", ""),
                    "asset_kind": result.get("asset_kind", ""),
                    "style_target_family": result.get("style_target_family", ""),
                    "seed": str(result.get("seed", "")),
                    "review_focus": result.get("review_focus", []),
                }
            )
    return entries


def collect_from_folder(eval_dir: Path) -> list[dict[str, Any]]:
    entries = []
    for path in sorted(eval_dir.glob("*.png")):
        if path.name.startswith("_") or path.name.lower() == "contact_sheet.png":
            continue
        entries.append(
            {
                "path": path,
                "name": path.stem,
                "status": "review",
                "category": "",
                "asset_kind": "",
                "style_target_family": "",
                "seed": "",
                "review_focus": [],
            }
        )
    return entries


def checkerboard(size: tuple[int, int], cell: int = 8) -> Image.Image:
    image = Image.new("RGBA", size, (32, 34, 38, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], cell):
        for x in range(0, size[0], cell):
            if (x // cell + y // cell) % 2 == 0:
                draw.rectangle((x, y, x + cell - 1, y + cell - 1), fill=(43, 47, 52, 255))
    return image


def fit_image(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    canvas = checkerboard(size)
    source = image.convert("RGBA")
    source.thumbnail((size[0] - 18, size[1] - 18), Image.Resampling.NEAREST)
    x = (size[0] - source.width) // 2
    y = (size[1] - source.height) // 2
    canvas.alpha_composite(source, (x, y))
    return canvas


def draw_label(draw: ImageDraw.ImageDraw, x: int, y: int, width: int, lines: list[str], font: ImageFont.ImageFont) -> None:
    draw.rectangle((x, y, x + width - 1, y + 58), fill=(14, 16, 19, 238))
    text_y = y + 6
    for line in lines[:3]:
        draw.text((x + 8, text_y), line[:31], fill=(229, 234, 226, 255), font=font)
        text_y += 16


def build_sheet(project_root: Path | None, entries: list[dict[str, Any]], output_path: Path, manifest_output: Path, title: str, columns: int) -> dict[str, Any]:
    columns = max(1, columns)
    cell_w = 176
    cell_h = 210
    header_h = 58
    rows = max(1, (len(entries) + columns - 1) // columns)
    sheet = Image.new("RGBA", (columns * cell_w, header_h + rows * cell_h), (18, 20, 23, 255))
    draw = ImageDraw.Draw(sheet)
    title_font = load_font(17)
    font = load_font(11)
    draw.text((12, 10), title, fill=(239, 242, 235, 255), font=title_font)
    draw.text((12, 34), "Generated eval candidates. Review manually before keeping or importing.", fill=(175, 184, 176, 255), font=font)

    metrics: list[dict[str, Any]] = []
    for index, entry in enumerate(entries):
        path = Path(entry["path"])
        col = index % columns
        row = index // columns
        x = col * cell_w
        y = header_h + row * cell_h
        draw.rectangle((x, y, x + cell_w - 1, y + cell_h - 1), outline=(68, 76, 84, 255), fill=(27, 30, 34, 255))
        metric: dict[str, Any] = {
            "path": repo_rel(project_root, path),
            "name": entry.get("name", path.stem),
            "status": entry.get("status", "review"),
            "category": entry.get("category", ""),
            "asset_kind": entry.get("asset_kind", ""),
            "style_target_family": entry.get("style_target_family", ""),
            "seed": entry.get("seed", ""),
            "width": 0,
            "height": 0,
            "alpha_bbox": None,
            "alpha_coverage": 0.0,
            "exists": path.exists(),
        }
        if path.exists():
            with Image.open(path) as source:
                image = source.convert("RGBA")
                metric["width"] = image.width
                metric["height"] = image.height
                bbox = alpha_bbox(image)
                metric["alpha_bbox"] = list(bbox) if bbox else None
                metric["alpha_coverage"] = round(alpha_coverage(image), 4)
                fitted = fit_image(image, (cell_w, 150))
                sheet.alpha_composite(fitted, (x, y))
        else:
            draw.text((x + 12, y + 66), "missing image", fill=(236, 116, 105, 255), font=font)
        label = [
            str(metric["name"]),
            f"{metric['width']}x{metric['height']} cov {metric['alpha_coverage']:.2f}",
            f"{metric['style_target_family'] or metric['asset_kind']} {metric['status']}".strip(),
        ]
        draw_label(draw, x, y + 151, cell_w, label, font)
        metrics.append(metric)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)
    payload = {
        "schema": "lit_iso.lora.eval_contact_sheet.v1",
        "generated_utc": utc_now(),
        "title": title,
        "image_count": len(entries),
        "contact_sheet": repo_rel(project_root, output_path),
        "items": metrics,
    }
    manifest_output.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    return payload


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a contact sheet for local LoRA eval outputs.")
    parser.add_argument("--project-root", type=Path)
    parser.add_argument("--eval-dir", type=Path, required=True)
    parser.add_argument("--manifest", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--report", type=Path)
    parser.add_argument("--title", default="LoRA evaluation contact sheet")
    parser.add_argument("--columns", type=int, default=5)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve() if args.project_root else None
    eval_dir = args.eval_dir.resolve()
    manifest = args.manifest.resolve() if args.manifest else eval_dir / "manifest.json"
    output = args.output.resolve() if args.output else eval_dir / "contact_sheet.png"
    report = args.report.resolve() if args.report else eval_dir / "contact_sheet_manifest.json"
    entries = collect_from_manifest(eval_dir, manifest)
    if not entries:
        entries = collect_from_folder(eval_dir)
    payload = build_sheet(project_root, entries, output, report, args.title, args.columns)
    print(json.dumps({"ok": True, "images": payload["image_count"], "contact_sheet": str(output), "report": str(report)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
