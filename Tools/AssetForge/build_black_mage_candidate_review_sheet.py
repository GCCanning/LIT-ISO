#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")

CANONICAL_DIRECTIONS = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"]
DEFAULT_DIRECTIONS = ["NE", "NW", "SE", "SW"]
SCRIPT_SCHEMA = "lit_iso.asset_forge.black_mage_candidate_review.v1"


@dataclass(frozen=True)
class Candidate:
    direction: str
    path: Path
    label: str
    source: str
    job_name: str
    seed: str
    status: str
    issues: tuple[str, ...]
    warnings: tuple[str, ...]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def repo_rel(project_root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(project_root.resolve()).as_posix()
    except ValueError:
        return str(path.resolve()).replace("\\", "/")


def repo_path(project_root: Path, value: str | Path) -> Path:
    path = Path(str(value).replace("/", "\\"))
    return path if path.is_absolute() else project_root / path


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
    opaque = sum(alpha.histogram()[9:])
    return opaque / total


def direction_from_name(name: str) -> str | None:
    lowered = name.lower()
    for direction in CANONICAL_DIRECTIONS:
        if re.search(rf"(^|[_\-.]){direction.lower()}([_\-.]|$)", lowered):
            return direction
    return None


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def qa_by_path(project_root: Path, review_root: Path) -> dict[str, dict[str, Any]]:
    report_path = review_root / "review_report.json"
    if not report_path.exists():
        return {}
    report = read_json(report_path)
    by_path: dict[str, dict[str, Any]] = {}
    for item in report.get("items", []):
        item_path = repo_path(project_root, item.get("path", ""))
        by_path[item_path.resolve().as_posix().lower()] = item
    return by_path


def generation_by_name(project_root: Path, review_root: Path) -> dict[str, dict[str, Any]]:
    manifest_path = review_root / "generation_manifest.json"
    if not manifest_path.exists():
        return {}
    manifest = read_json(manifest_path)
    comfy_manifest_path = manifest.get("comfy_generation_manifest")
    if not comfy_manifest_path:
        return {}
    comfy_path = repo_path(project_root, comfy_manifest_path)
    if not comfy_path.exists():
        return {}
    comfy = read_json(comfy_path)
    result: dict[str, dict[str, Any]] = {}
    for output in comfy.get("outputs", []):
        cleaned = output.get("cleaned_path") or output.get("name")
        if cleaned:
            result[Path(str(cleaned)).name.lower()] = output
    return result


def candidates_from_review_job(project_root: Path, job_name: str, direction: str) -> list[Candidate]:
    review_root = project_root / "Assets" / "Generated" / "_Review" / job_name
    manifest_path = review_root / "generation_manifest.json"
    if not manifest_path.exists():
        return []
    manifest = read_json(manifest_path)
    qa_map = qa_by_path(project_root, review_root)
    gen_map = generation_by_name(project_root, review_root)
    candidates: list[Candidate] = []
    for index, value in enumerate(manifest.get("generated_files", []), start=1):
        path = repo_path(project_root, value)
        if not path.exists() or path.suffix.lower() != ".png":
            continue
        key = path.resolve().as_posix().lower()
        qa = qa_map.get(key, {})
        gen = gen_map.get(path.name.lower(), {})
        issues = tuple(str(item) for item in qa.get("issues", []) if item)
        warnings = tuple(str(item) for item in qa.get("warnings", []) if item)
        seed = str(gen.get("seed", ""))
        label_bits = [f"{direction} c{index}"]
        if seed:
            label_bits.append(f"seed {seed}")
        if qa.get("status"):
            label_bits.append(f"qa {qa.get('status')}")
        candidates.append(
            Candidate(
                direction=direction,
                path=path,
                label=" | ".join(label_bits),
                source="review_job",
                job_name=job_name,
                seed=seed,
                status=str(qa.get("status", manifest.get("status", "review"))),
                issues=issues,
                warnings=warnings,
            )
        )
    return candidates


def candidates_from_flat_folder(project_root: Path, folder: Path) -> list[Candidate]:
    folder = folder if folder.is_absolute() else project_root / folder
    if not folder.exists():
        return []
    candidates: list[Candidate] = []
    counters = {direction: 0 for direction in DIRECTIONS}
    for path in sorted(folder.glob("*.png")):
        if path.name.startswith("_"):
            continue
        direction = direction_from_name(path.name)
        if direction is None:
            continue
        counters[direction] += 1
        candidates.append(
            Candidate(
                direction=direction,
                path=path,
                label=f"{direction} c{counters[direction]}",
                source="flat_folder",
                job_name=folder.name,
                seed="",
                status="legacy_review",
                issues=(),
                warnings=(),
            )
        )
    return candidates


def collect_candidates(args: argparse.Namespace, project_root: Path) -> dict[str, list[Candidate]]:
    grouped = {direction: [] for direction in args.directions}
    if args.flat_source:
        for candidate in candidates_from_flat_folder(project_root, args.flat_source):
            if candidate.direction in grouped:
                grouped[candidate.direction].append(candidate)
    for direction in args.directions:
        job_name = args.job_template.format(direction=direction.lower(), DIRECTION=direction, variant=args.variant)
        for candidate in candidates_from_review_job(project_root, job_name, direction):
            grouped[direction].append(candidate)
    return grouped


def checkerboard(size: tuple[int, int], cell: int = 8) -> Image.Image:
    image = Image.new("RGBA", size, (31, 34, 38, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], cell):
        for x in range(0, size[0], cell):
            if (x // cell + y // cell) % 2 == 0:
                draw.rectangle((x, y, x + cell - 1, y + cell - 1), fill=(42, 46, 52, 255))
    return image


def fit_sprite(source: Image.Image, size: tuple[int, int]) -> Image.Image:
    canvas = checkerboard(size)
    image = source.convert("RGBA")
    image.thumbnail((size[0] - 18, size[1] - 18), Image.Resampling.NEAREST)
    x = (size[0] - image.width) // 2
    y = size[1] - image.height - 8
    canvas.alpha_composite(image, (x, max(0, y)))
    return canvas


def draw_wrapped_text(draw: ImageDraw.ImageDraw, xy: tuple[int, int], text: str, fill: tuple[int, int, int, int], font: ImageFont.ImageFont, max_chars: int, max_lines: int) -> None:
    x, y = xy
    words = text.split()
    lines: list[str] = []
    current = ""
    for word in words:
        candidate = word if not current else f"{current} {word}"
        if len(candidate) <= max_chars:
            current = candidate
        else:
            if current:
                lines.append(current)
            current = word
    if current:
        lines.append(current)
    for line in lines[:max_lines]:
        draw.text((x, y), line, fill=fill, font=font)
        y += 14


def draw_sheet(project_root: Path, grouped: dict[str, list[Candidate]], output_path: Path, style_reference: Path | None, title: str) -> dict[str, Any]:
    max_candidates = max((len(items) for items in grouped.values()), default=0)
    columns = max(1, max_candidates) + (1 if style_reference else 0)
    label_w = 70
    cell_w = 170
    cell_h = 190
    header_h = 64
    width = label_w + columns * cell_w
    height = header_h + len(grouped) * cell_h
    sheet = Image.new("RGBA", (width, height), (18, 20, 23, 255))
    draw = ImageDraw.Draw(sheet)
    title_font = load_font(18)
    label_font = load_font(14)
    small_font = load_font(11)
    draw.text((14, 12), title, fill=(239, 242, 235, 255), font=title_font)
    draw.text((14, 38), "Review only. Pick candidates manually before training or Unity import.", fill=(175, 184, 176, 255), font=small_font)

    metrics: list[dict[str, Any]] = []
    for row, direction in enumerate(grouped.keys()):
        y = header_h + row * cell_h
        draw.rectangle((0, y, label_w - 1, y + cell_h - 1), fill=(26, 31, 29, 255), outline=(56, 66, 58, 255))
        draw.text((18, y + 76), direction, fill=(231, 238, 226, 255), font=label_font)

        column = 0
        if style_reference:
            draw_candidate_cell(sheet, draw, style_reference, "style ref", "source", (), (), label_w + column * cell_w, y, cell_w, cell_h, small_font)
            column += 1

        for candidate in grouped[direction]:
            metric = draw_candidate_cell(
                sheet,
                draw,
                candidate.path,
                candidate.label,
                candidate.status,
                candidate.issues,
                candidate.warnings,
                label_w + column * cell_w,
                y,
                cell_w,
                cell_h,
                small_font,
            )
            metric.update(
                {
                    "direction": candidate.direction,
                    "path": repo_rel(project_root, candidate.path),
                    "source": candidate.source,
                    "job_name": candidate.job_name,
                    "seed": candidate.seed,
                    "status": candidate.status,
                    "issues": list(candidate.issues),
                    "warnings": list(candidate.warnings),
                }
            )
            metrics.append(metric)
            column += 1

        while column < columns:
            x = label_w + column * cell_w
            draw.rectangle((x, y, x + cell_w - 1, y + cell_h - 1), fill=(21, 23, 26, 255), outline=(48, 52, 58, 255))
            draw.text((x + 14, y + 78), "missing", fill=(122, 128, 132, 255), font=small_font)
            column += 1

    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)
    return {"sheet": repo_rel(project_root, output_path), "metrics": metrics}


def draw_candidate_cell(
    sheet: Image.Image,
    draw: ImageDraw.ImageDraw,
    path: Path,
    label: str,
    status: str,
    issues: tuple[str, ...],
    warnings: tuple[str, ...],
    x: int,
    y: int,
    cell_w: int,
    cell_h: int,
    font: ImageFont.ImageFont,
) -> dict[str, Any]:
    draw.rectangle((x, y, x + cell_w - 1, y + cell_h - 1), fill=(28, 31, 35, 255), outline=(68, 76, 84, 255))
    metric: dict[str, Any] = {"width": 0, "height": 0, "alpha_bbox": None, "alpha_coverage": 0.0}
    if not path.exists():
        draw.text((x + 10, y + 70), "missing file", fill=(235, 104, 92, 255), font=font)
        return metric
    try:
        with Image.open(path) as source:
            image = source.convert("RGBA")
            metric["width"] = image.width
            metric["height"] = image.height
            bbox = alpha_bbox(image)
            metric["alpha_bbox"] = list(bbox) if bbox else None
            metric["alpha_coverage"] = round(alpha_coverage(image), 4)
            fitted = fit_sprite(image, (cell_w, 132))
    except OSError:
        draw.text((x + 10, y + 70), "bad image", fill=(235, 104, 92, 255), font=font)
        return metric
    sheet.alpha_composite(fitted, (x, y))
    label_y = y + 136
    draw.rectangle((x, label_y, x + cell_w - 1, y + cell_h - 1), fill=(13, 15, 18, 235))
    draw.text((x + 8, label_y + 6), label, fill=(231, 236, 229, 255), font=font)
    draw.text((x + 8, label_y + 22), f"{metric['width']}x{metric['height']} | cov {metric['alpha_coverage']:.2f}", fill=(179, 188, 180, 255), font=font)
    status_fill = (129, 216, 151, 255) if status == "pass" else (235, 204, 115, 255)
    draw.text((x + 8, label_y + 38), status[:24], fill=status_fill, font=font)
    if issues:
        draw_wrapped_text(draw, (x + 82, label_y + 38), issues[0], (239, 130, 118, 255), font, 12, 1)
    elif warnings:
        draw_wrapped_text(draw, (x + 82, label_y + 38), warnings[0], (232, 192, 107, 255), font, 12, 1)
    return metric


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a black mage candidate QC sheet from generated review outputs.")
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--variant", default="v6")
    parser.add_argument("--directions", nargs="+", default=DEFAULT_DIRECTIONS)
    parser.add_argument("--job-template", default="black_mage_iso_idle_{direction}_{variant}")
    parser.add_argument("--flat-source", type=Path, help="Optional legacy folder with flat direction PNGs.")
    parser.add_argument("--output-root", type=Path)
    parser.add_argument("--style-reference", type=Path, default=Path("Assets/Generated/_Review/black_mage_iso_style_lock_v1/black_mage_normalized_128.png"))
    parser.add_argument("--no-style-reference", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    args.directions = [direction.upper() for direction in args.directions]
    for direction in args.directions:
        if direction not in CANONICAL_DIRECTIONS:
            raise ValueError(f"Unsupported black mage review direction: {direction}")

    output_root = args.output_root
    if output_root is None:
        output_root = Path(f"Assets/Generated/_Review/black_mage_iso_renders_{args.variant}")
    output_root = output_root if output_root.is_absolute() else project_root / output_root
    style_reference = None
    if not args.no_style_reference and args.style_reference:
        style_reference = repo_path(project_root, args.style_reference)
        if not style_reference.exists():
            style_reference = None

    grouped = collect_candidates(args, project_root)
    total = sum(len(items) for items in grouped.values())
    sheet_path = output_root / f"_{args.variant}_candidate_review_sheet.png"
    manifest_path = output_root / f"_{args.variant}_candidate_manifest.json"
    result = draw_sheet(project_root, grouped, sheet_path, style_reference, f"Black Mage {args.variant} direction candidates")
    manifest = {
        "schema": SCRIPT_SCHEMA,
        "generated_utc": utc_now(),
        "variant": args.variant,
        "directions": args.directions,
        "candidate_count": total,
        "review_sheet": result["sheet"],
        "style_reference": repo_rel(project_root, style_reference) if style_reference else "",
        "job_template": args.job_template,
        "flat_source": repo_rel(project_root, repo_path(project_root, args.flat_source)) if args.flat_source else "",
        "status": "review_ready" if total else "no_candidates_found",
        "candidates": result["metrics"],
        "next_step": "Review sheet manually; approve one candidate per direction before animation, training capture, or Unity import.",
    }
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(
        json.dumps(
            {
                "ok": True,
                "status": manifest["status"],
                "candidate_count": total,
                "review_sheet": repo_rel(project_root, sheet_path),
                "manifest": repo_rel(project_root, manifest_path),
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
