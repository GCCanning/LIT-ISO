#!/usr/bin/env python3
from __future__ import annotations

import argparse
import csv
import json
import math
import sys
from collections import Counter
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


def repo_rel(project_root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(project_root.resolve()).as_posix()
    except ValueError:
        return str(path.resolve()).replace("\\", "/")


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int] | None:
    return image.convert("RGBA").split()[-1].getbbox()


def alpha_coverage(image: Image.Image) -> float:
    rgba = image.convert("RGBA")
    alpha = rgba.split()[-1]
    total = alpha.width * alpha.height
    if total <= 0:
        return 0.0
    return sum(alpha.histogram()[9:]) / total


def visible_pixels(image: Image.Image, stride: int = 1) -> list[tuple[int, int, int]]:
    rgba = image.convert("RGBA")
    pixels: list[tuple[int, int, int]] = []
    for y in range(0, rgba.height, max(1, stride)):
        for x in range(0, rgba.width, max(1, stride)):
            r, g, b, a = rgba.getpixel((x, y))
            if a > 8:
                pixels.append((r, g, b))
    return pixels


def top_palette(paths: list[Path], limit: int = 32) -> list[tuple[int, int, int]]:
    counter: Counter[tuple[int, int, int]] = Counter()
    for path in paths:
        if not path.exists():
            continue
        with Image.open(path) as source:
            for color in visible_pixels(source):
                counter[color] += 1
    return [color for color, _count in counter.most_common(limit)]


def nearest_distance(color: tuple[int, int, int], palette: list[tuple[int, int, int]]) -> float:
    if not palette:
        return 255.0
    cr, cg, cb = color
    best = 255.0
    for pr, pg, pb in palette:
        dist = math.sqrt((cr - pr) ** 2 + (cg - pg) ** 2 + (cb - pb) ** 2)
        if dist < best:
            best = dist
    return best


def mean_palette_distance(image: Image.Image, palette: list[tuple[int, int, int]]) -> float:
    pixels = visible_pixels(image, stride=max(1, max(image.width, image.height) // 96))
    if not pixels:
        return 255.0
    return sum(nearest_distance(color, palette) for color in pixels) / len(pixels)


def load_reference_families(project_root: Path, reference_manifest: Path) -> dict[str, dict[str, Any]]:
    manifest = json.loads(reference_manifest.read_text(encoding="utf-8-sig"))
    families: dict[str, dict[str, Any]] = {}
    for family in manifest.get("families", []):
        tile_paths = []
        for tile in family.get("tiles", []):
            path_value = tile.get("path")
            if not path_value:
                continue
            path = Path(path_value)
            if not path.is_absolute():
                path = project_root / path
            if path.exists():
                tile_paths.append(path)
        palette = top_palette(tile_paths)
        families[family.get("family", "unknown")] = {
            "tiles": [repo_rel(project_root, path) for path in tile_paths],
            "palette": palette,
        }
    return families


def load_matrix_entries(project_root: Path, matrix_plan: Path) -> list[dict[str, Any]]:
    plan = json.loads(matrix_plan.read_text(encoding="utf-8-sig"))
    entries: list[dict[str, Any]] = []
    for matrix_item in plan.get("matrix", []):
        manifest_path = Path(matrix_item.get("manifest", ""))
        if not manifest_path.is_absolute():
            manifest_path = project_root / manifest_path
        if not manifest_path.exists():
            entries.append(
                {
                    "strength": matrix_item.get("strength"),
                    "output_dir": matrix_item.get("output_dir"),
                    "manifest": manifest_path,
                    "status": "missing_manifest",
                }
            )
            continue
        manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
        for result in manifest.get("results", []):
            outputs = result.get("outputs") or []
            if not outputs:
                entries.append(
                    {
                        "strength": matrix_item.get("strength"),
                        "output_dir": matrix_item.get("output_dir"),
                        "manifest": manifest_path,
                        "status": result.get("status", "no_outputs"),
                        "name": result.get("name"),
                        "style_target_family": result.get("style_target_family", ""),
                        "asset_kind": result.get("asset_kind", ""),
                    }
                )
                continue
            for output in outputs:
                output_path = Path(output)
                if not output_path.is_absolute():
                    output_path = manifest_path.parent / output_path
                entries.append(
                    {
                        "strength": matrix_item.get("strength"),
                        "output_dir": matrix_item.get("output_dir"),
                        "manifest": manifest_path,
                        "status": result.get("status", "review"),
                        "name": result.get("name") or output_path.stem,
                        "style_target_family": result.get("style_target_family", ""),
                        "asset_kind": result.get("asset_kind", ""),
                        "path": output_path,
                    }
                )
    return entries


def score_entry(project_root: Path, entry: dict[str, Any], reference: dict[str, dict[str, Any]]) -> dict[str, Any]:
    path = entry.get("path")
    base = {
        "strength": entry.get("strength"),
        "name": entry.get("name", ""),
        "style_target_family": entry.get("style_target_family", ""),
        "asset_kind": entry.get("asset_kind", ""),
        "status": entry.get("status", "review"),
        "path": repo_rel(project_root, path) if isinstance(path, Path) else "",
        "exists": isinstance(path, Path) and path.exists(),
    }
    if not isinstance(path, Path) or not path.exists():
        return {**base, "score": 9999.0, "palette_distance": None, "notes": ["missing output image"]}

    with Image.open(path) as source:
        image = source.convert("RGBA")
        family = str(entry.get("style_target_family", ""))
        palette = reference.get(family, {}).get("palette", [])
        coverage = alpha_coverage(image)
        bbox = alpha_bbox(image)
        color_count = len(set(visible_pixels(image, stride=max(1, max(image.width, image.height) // 96))))
        palette_distance = mean_palette_distance(image, palette)
        notes: list[str] = []
        if image.width != 32 or image.height != 32:
            notes.append("not_32x32_raw_or_postprocess_needed")
        if coverage > 0.95:
            notes.append("opaque_background_or_full_canvas")
        if not bbox:
            notes.append("blank")
        if color_count > 96:
            notes.append("too_many_colors_before_cleanup")
        score = palette_distance
        if image.width != 32 or image.height != 32:
            score += 18.0
        if coverage > 0.95:
            score += 12.0
        if color_count > 96:
            score += 8.0
        return {
            **base,
            "width": image.width,
            "height": image.height,
            "alpha_coverage": round(coverage, 4),
            "alpha_bbox": list(bbox) if bbox else None,
            "sampled_color_count": color_count,
            "palette_distance": round(palette_distance, 3),
            "score": round(score, 3),
            "notes": notes,
        }


def checkerboard(size: tuple[int, int], cell: int = 8) -> Image.Image:
    image = Image.new("RGBA", size, (31, 33, 37, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], cell):
        for x in range(0, size[0], cell):
            if (x // cell + y // cell) % 2 == 0:
                draw.rectangle((x, y, x + cell - 1, y + cell - 1), fill=(42, 46, 51, 255))
    return image


def fit_image(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    canvas = checkerboard(size)
    source = image.convert("RGBA")
    source.thumbnail((size[0] - 12, size[1] - 12), Image.Resampling.NEAREST)
    canvas.alpha_composite(source, ((size[0] - source.width) // 2, (size[1] - source.height) // 2))
    return canvas


def build_rank_sheet(project_root: Path, scores: list[dict[str, Any]], output: Path) -> None:
    shown = [item for item in scores if item.get("exists")][:24]
    columns = 6
    cell_w = 178
    cell_h = 194
    header_h = 58
    rows = max(1, (len(shown) + columns - 1) // columns)
    sheet = Image.new("RGBA", (columns * cell_w, header_h + rows * cell_h), (18, 20, 23, 255))
    draw = ImageDraw.Draw(sheet)
    title_font = load_font(17)
    font = load_font(11)
    draw.text((12, 10), "LIT-ISO tile style eval ranking", fill=(238, 241, 234, 255), font=title_font)
    draw.text((12, 34), "Lower score is better. Raw outputs still require cleanup/approval before Unity.", fill=(176, 186, 176, 255), font=font)

    for index, item in enumerate(shown):
        col = index % columns
        row = index // columns
        x = col * cell_w
        y = header_h + row * cell_h
        draw.rectangle((x, y, x + cell_w - 1, y + cell_h - 1), outline=(68, 76, 84, 255), fill=(27, 30, 34, 255))
        path = project_root / item["path"] if item.get("path") and not Path(item["path"]).is_absolute() else Path(item.get("path", ""))
        if path.exists():
            with Image.open(path) as source:
                sheet.alpha_composite(fit_image(source, (cell_w, 128)), (x, y))
        label = [
            f"#{index + 1} s{item.get('strength')} score {item.get('score')}",
            str(item.get("name", ""))[:28],
            str(item.get("style_target_family", ""))[:28],
            ",".join(item.get("notes", [])[:2])[:28],
        ]
        text_y = y + 132
        for line in label:
            draw.text((x + 8, text_y), line, fill=(229, 234, 226, 255), font=font)
            text_y += 14

    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output)


def write_csv(path: Path, rows: list[dict[str, Any]]) -> None:
    fields = [
        "strength",
        "name",
        "style_target_family",
        "asset_kind",
        "score",
        "palette_distance",
        "width",
        "height",
        "alpha_coverage",
        "sampled_color_count",
        "path",
        "notes",
    ]
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields)
        writer.writeheader()
        for row in rows:
            writer.writerow({field: row.get(field, "") for field in fields})


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Score tile LoRA matrix outputs against supplied style-lock tile families.")
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--matrix-plan", type=Path, default=Path("Temp/LoRA/litiso_iso_reference_tile_style_v1.eval_matrix_plan.json"))
    parser.add_argument("--reference-manifest", type=Path, default=Path("Temp/LoRA/Evals/stylelock_tile_reference_targets.json"))
    parser.add_argument("--output", type=Path, default=Path("Temp/LoRA/Evals/tile_style_eval_scores.json"))
    parser.add_argument("--csv", type=Path, default=Path("Temp/LoRA/Evals/tile_style_eval_scores.csv"))
    parser.add_argument("--sheet", type=Path, default=Path("Temp/LoRA/Evals/tile_style_eval_ranked_sheet.png"))
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    matrix_plan = args.matrix_plan if args.matrix_plan.is_absolute() else project_root / args.matrix_plan
    reference_manifest = args.reference_manifest if args.reference_manifest.is_absolute() else project_root / args.reference_manifest
    output = args.output if args.output.is_absolute() else project_root / args.output
    csv_path = args.csv if args.csv.is_absolute() else project_root / args.csv
    sheet = args.sheet if args.sheet.is_absolute() else project_root / args.sheet

    reference = load_reference_families(project_root, reference_manifest)
    entries = load_matrix_entries(project_root, matrix_plan)
    scores = [score_entry(project_root, entry, reference) for entry in entries if entry.get("status") != "missing_manifest"]
    scores.sort(key=lambda item: float(item.get("score", 9999.0)))
    payload = {
        "schema": "lit_iso.lora.tile_style_eval_scores.v1",
        "generated_utc": utc_now(),
        "matrix_plan": repo_rel(project_root, matrix_plan),
        "reference_manifest": repo_rel(project_root, reference_manifest),
        "reference_families": {key: {"tiles": value["tiles"], "palette_size": len(value["palette"])} for key, value in reference.items()},
        "score_notes": [
            "Lower score is better.",
            "Scores are ranking aids, not art approval.",
            "Non-32x32, opaque, or high-color outputs are penalized because they require cleanup before Unity.",
        ],
        "items": scores,
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    write_csv(csv_path, scores)
    if any(item.get("exists") for item in scores):
        build_rank_sheet(project_root, scores, sheet)
    print(
        json.dumps(
            {
                "ok": True,
                "items": len(scores),
                "images": sum(1 for item in scores if item.get("exists")),
                "report": str(output),
                "csv": str(csv_path),
                "sheet": str(sheet) if sheet.exists() else "",
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
