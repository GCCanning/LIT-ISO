#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")

SCHEMA = "lit_iso.asset_forge.proper_pixel_art_cleanup.v1"
EXCLUDED_PARTS = {"_ProperPixelArt", "_Preview", "__pycache__"}
EXCLUDED_NAME_PARTS = {"contact_sheet", "strict_asset_quality_report"}

MODE_DEFAULTS = {
    "tile": {"colors": 16, "target": (32, 32), "anchor": "center", "max_fill": 1.0, "pixel_width": 1},
    "prop": {"colors": 32, "target": (128, 128), "anchor": "bottom", "max_fill": 0.88, "pixel_width": 1},
    "item": {"colors": 24, "target": (64, 64), "anchor": "center", "max_fill": 0.82, "pixel_width": 1},
    "character": {"colors": 48, "target": (128, 128), "anchor": "bottom", "max_fill": 0.88, "pixel_width": 1},
    "npc": {"colors": 48, "target": (128, 128), "anchor": "bottom", "max_fill": 0.88, "pixel_width": 1},
    "mob": {"colors": 48, "target": (128, 128), "anchor": "bottom", "max_fill": 0.88, "pixel_width": 1},
    "auto": {"colors": 32, "target": None, "anchor": "center", "max_fill": 0.92, "pixel_width": 1},
}


@dataclass(frozen=True)
class CleanupSettings:
    mode: str
    colors: int
    scale_result: int
    initial_upscale: int
    pixel_width: int
    transparent: bool
    target_size: tuple[int, int] | None
    fit_target: bool
    save_intermediates: bool


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def repo_rel(project_root: Path | None, path: Path) -> str:
    if project_root:
        try:
            return path.resolve().relative_to(project_root.resolve()).as_posix()
        except ValueError:
            pass
    return str(path.resolve()).replace("\\", "/")


def import_pixelate(source_root: Path | None):
    if source_root:
        sys.path.insert(0, str(source_root.resolve()))
    try:
        from proper_pixel_art import pixelate  # type: ignore
    except Exception as exc:
        return None, exc
    return pixelate, None


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
    return sum(alpha.histogram()[1:]) / total


def color_count(image: Image.Image, max_scan: int = 512) -> int:
    rgba = image.convert("RGBA")
    if rgba.width > max_scan or rgba.height > max_scan:
        rgba.thumbnail((max_scan, max_scan), Image.Resampling.NEAREST)
    colors = set()
    data = rgba.get_flattened_data() if hasattr(rgba, "get_flattened_data") else rgba.getdata()
    for r, g, b, a in data:
        if a > 0:
            colors.add((r, g, b, a))
    return len(colors)


def parse_size(value: str, default: tuple[int, int] | None) -> tuple[int, int] | None:
    text = str(value or "").strip().lower()
    if text in {"", "auto"}:
        return default
    if text in {"none", "off", "false", "0"}:
        return None
    if "x" not in text:
        size = int(text)
        return (size, size)
    left, right = text.split("x", 1)
    return (int(left), int(right))


def input_files(input_path: Path) -> list[Path]:
    if input_path.is_file():
        return [input_path] if input_path.suffix.lower() == ".png" else []
    files = []
    for path in sorted(input_path.rglob("*.png")):
        parts = set(path.parts)
        if parts.intersection(EXCLUDED_PARTS):
            continue
        lowered = path.name.lower()
        if lowered.startswith("_"):
            continue
        if any(token in lowered for token in EXCLUDED_NAME_PARTS):
            continue
        files.append(path)
    return files


def relative_stem(input_path: Path, file_path: Path) -> Path:
    if input_path.is_file():
        return Path(file_path.stem)
    rel = file_path.relative_to(input_path)
    return rel.with_suffix("")


def fit_to_canvas(image: Image.Image, size: tuple[int, int], anchor: str, max_fill: float) -> Image.Image:
    rgba = image.convert("RGBA")
    bbox = alpha_bbox(rgba)
    if not bbox:
        return Image.new("RGBA", size, (0, 0, 0, 0))
    crop = rgba.crop(bbox)
    max_w = max(1, int(size[0] * max_fill))
    max_h = max(1, int(size[1] * max_fill))
    scale = min(max_w / max(1, crop.width), max_h / max(1, crop.height))
    resized = crop.resize((max(1, int(crop.width * scale)), max(1, int(crop.height * scale))), Image.Resampling.NEAREST)
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    x = (size[0] - resized.width) // 2
    y = size[1] - resized.height - max(1, size[1] // 16) if anchor == "bottom" else (size[1] - resized.height) // 2
    canvas.alpha_composite(resized, (x, max(0, y)))
    return canvas


def checkerboard(size: tuple[int, int], cell: int = 8) -> Image.Image:
    image = Image.new("RGBA", size, (28, 31, 34, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], cell):
        for x in range(0, size[0], cell):
            if (x // cell + y // cell) % 2 == 0:
                draw.rectangle((x, y, x + cell - 1, y + cell - 1), fill=(41, 45, 49, 255))
    return image


def fit_preview(path: Path, size: tuple[int, int]) -> Image.Image:
    canvas = checkerboard(size)
    if not path.exists():
        return canvas
    with Image.open(path) as image:
        source = image.convert("RGBA")
    source.thumbnail((size[0] - 14, size[1] - 14), Image.Resampling.NEAREST)
    canvas.alpha_composite(source, ((size[0] - source.width) // 2, (size[1] - source.height) // 2))
    return canvas


def draw_contact_sheet(project_root: Path | None, items: list[dict[str, Any]], output_path: Path) -> str:
    if not items:
        return ""
    cell_w = 166
    cell_h = 172
    columns = 3
    rows = len(items)
    header_h = 52
    sheet = Image.new("RGBA", (cell_w * columns, header_h + cell_h * rows), (17, 19, 22, 255))
    draw = ImageDraw.Draw(sheet)
    title_font = load_font(16)
    font = load_font(10)
    draw.text((12, 10), "Proper Pixel Art cleanup candidates", fill=(238, 242, 234, 255), font=title_font)
    draw.text((12, 32), "Original | true-resolution pixelate | normalized candidate", fill=(178, 186, 178, 255), font=font)
    for row, item in enumerate(items):
        y = header_h + row * cell_h
        paths = [
            Path(item["source_path"]),
            Path(item["pixelated_path"]),
            Path(item.get("normalized_path") or item["pixelated_path"]),
        ]
        labels = [
            "source",
            f"ppa {item['pixelated_width']}x{item['pixelated_height']}",
            f"norm {item.get('normalized_width', item['pixelated_width'])}x{item.get('normalized_height', item['pixelated_height'])}",
        ]
        for col, path in enumerate(paths):
            x = col * cell_w
            draw.rectangle((x, y, x + cell_w - 1, y + cell_h - 1), fill=(25, 28, 32, 255), outline=(64, 70, 78, 255))
            preview = fit_preview(path, (cell_w, 128))
            sheet.alpha_composite(preview, (x, y))
            draw.rectangle((x, y + 128, x + cell_w - 1, y + cell_h - 1), fill=(12, 14, 16, 240))
            draw.text((x + 8, y + 134), labels[col], fill=(229, 235, 226, 255), font=font)
            draw.text((x + 8, y + 149), item["name"][:26], fill=(177, 186, 176, 255), font=font)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)
    return repo_rel(project_root, output_path)


def process_file(pixelate, input_path: Path, file_path: Path, output_root: Path, settings: CleanupSettings) -> dict[str, Any]:
    defaults = MODE_DEFAULTS.get(settings.mode, MODE_DEFAULTS["auto"])
    rel_stem = relative_stem(input_path, file_path)
    pixelated_path = output_root / "pixelated" / rel_stem.parent / f"{rel_stem.name}__ppa.png"
    normalized_path = output_root / "normalized" / rel_stem.parent / f"{rel_stem.name}__ppa_normalized.png"
    intermediate_dir = None
    if settings.save_intermediates:
        intermediate_dir = output_root / "intermediate" / rel_stem.parent / rel_stem.name
        intermediate_dir.mkdir(parents=True, exist_ok=True)

    with Image.open(file_path) as source:
        source_rgba = source.convert("RGBA")
        result = pixelate(
            source_rgba,
            num_colors=settings.colors,
            initial_upscale_factor=settings.initial_upscale,
            scale_result=settings.scale_result,
            transparent_background=settings.transparent,
            intermediate_dir=intermediate_dir,
            pixel_width=settings.pixel_width,
        ).convert("RGBA")

    pixelated_path.parent.mkdir(parents=True, exist_ok=True)
    result.save(pixelated_path)
    normalized = None
    if settings.fit_target and settings.target_size:
        normalized = fit_to_canvas(result, settings.target_size, str(defaults["anchor"]), float(defaults["max_fill"]))
        normalized_path.parent.mkdir(parents=True, exist_ok=True)
        normalized.save(normalized_path)

    entry = {
        "name": file_path.stem,
        "source_path": str(file_path.resolve()),
        "pixelated_path": str(pixelated_path.resolve()),
        "normalized_path": str(normalized_path.resolve()) if normalized else "",
        "source_width": source_rgba.width,
        "source_height": source_rgba.height,
        "source_colors": color_count(source_rgba),
        "source_alpha_coverage": round(alpha_coverage(source_rgba), 4),
        "pixelated_width": result.width,
        "pixelated_height": result.height,
        "pixelated_colors": color_count(result),
        "pixelated_alpha_coverage": round(alpha_coverage(result), 4),
        "alpha_bbox": list(alpha_bbox(result) or ()),
        "status": "ok",
    }
    if normalized:
        entry.update(
            {
                "normalized_width": normalized.width,
                "normalized_height": normalized.height,
                "normalized_colors": color_count(normalized),
                "normalized_alpha_coverage": round(alpha_coverage(normalized), 4),
                "normalized_alpha_bbox": list(alpha_bbox(normalized) or ()),
            }
        )
    return entry


def write_missing_manifest(project_root: Path | None, output_root: Path, exc: BaseException, args: argparse.Namespace) -> int:
    manifest_path = output_root / "proper_pixel_art_report.json"
    payload = {
        "schema": SCHEMA,
        "status": "missing_dependency",
        "generated_utc": utc_now(),
        "error": str(exc),
        "input_path": str(args.input_path),
        "output_root": repo_rel(project_root, output_root),
        "dependency": {
            "package": "proper-pixel-art",
            "python_module": "proper_pixel_art",
            "install_command": "python -m pip install proper-pixel-art",
            "source": "https://github.com/KennethJAllen/proper-pixel-art",
        },
        "notes": [
            "Asset Forge keeps its normal cleanup path when this optional dependency is missing.",
            "Install into the Python environment passed to run_proper_pixel_art_cleanup.ps1, or pass --proper-pixel-art-root to a local checkout.",
        ],
    }
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    print(json.dumps({"ok": False, "status": "missing_dependency", "report": repo_rel(project_root, manifest_path)}, indent=2))
    return 2 if args.fail_missing else 0


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run optional proper-pixel-art cleanup over Asset Forge PNGs.")
    parser.add_argument("--project-root", type=Path)
    parser.add_argument("--input-path", type=Path, required=True)
    parser.add_argument("--output-root", type=Path)
    parser.add_argument("--mode", choices=sorted(MODE_DEFAULTS.keys()), default="auto")
    parser.add_argument("--colors", type=int)
    parser.add_argument("--scale-result", type=int, default=1)
    parser.add_argument("--initial-upscale", type=int, default=2)
    parser.add_argument("--pixel-width", type=int)
    parser.add_argument("--transparent", action="store_true")
    parser.add_argument("--target-size", default="auto")
    parser.add_argument("--no-fit-target", action="store_true")
    parser.add_argument("--save-intermediates", action="store_true")
    parser.add_argument("--proper-pixel-art-root", type=Path)
    parser.add_argument("--fail-missing", action="store_true")
    parser.add_argument("--max-files", type=int, default=0)
    parser.add_argument("--dry-run", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve() if args.project_root else None
    input_path = args.input_path.resolve()
    output_root = args.output_root.resolve() if args.output_root else (input_path.parent / "_ProperPixelArt" if input_path.is_file() else input_path / "_ProperPixelArt")
    defaults = MODE_DEFAULTS[args.mode]
    colors = int(args.colors if args.colors is not None else defaults["colors"])
    target_size = parse_size(args.target_size, defaults["target"])
    settings = CleanupSettings(
        mode=args.mode,
        colors=colors,
        scale_result=max(1, args.scale_result),
        initial_upscale=max(1, args.initial_upscale),
        pixel_width=max(1, int(args.pixel_width if args.pixel_width is not None else defaults["pixel_width"])),
        transparent=bool(args.transparent),
        target_size=target_size,
        fit_target=not args.no_fit_target,
        save_intermediates=bool(args.save_intermediates),
    )
    files = input_files(input_path)
    if args.max_files > 0:
        files = files[: args.max_files]

    pixelate, import_error = import_pixelate(args.proper_pixel_art_root)
    if pixelate is None:
        return write_missing_manifest(project_root, output_root, import_error or RuntimeError("proper_pixel_art unavailable"), args)

    manifest_path = output_root / "proper_pixel_art_report.json"
    if args.dry_run:
        payload = {
            "schema": SCHEMA,
            "status": "dry_run",
            "generated_utc": utc_now(),
            "input_path": repo_rel(project_root, input_path),
            "output_root": repo_rel(project_root, output_root),
            "settings": settings.__dict__,
            "planned_files": [repo_rel(project_root, path) for path in files],
        }
        manifest_path.parent.mkdir(parents=True, exist_ok=True)
        manifest_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
        print(json.dumps({"ok": True, "status": "dry_run", "files": len(files), "report": repo_rel(project_root, manifest_path)}, indent=2))
        return 0

    results: list[dict[str, Any]] = []
    errors: list[dict[str, str]] = []
    for file_path in files:
        try:
            results.append(process_file(pixelate, input_path, file_path, output_root, settings))
        except Exception as exc:
            errors.append({"path": str(file_path), "error": str(exc)})

    contact_sheet = draw_contact_sheet(project_root, results, output_root / "proper_pixel_art_contact_sheet.png")
    payload = {
        "schema": SCHEMA,
        "status": "complete" if not errors else "review",
        "generated_utc": utc_now(),
        "input_path": repo_rel(project_root, input_path),
        "output_root": repo_rel(project_root, output_root),
        "settings": settings.__dict__,
        "total": len(files),
        "processed": len(results),
        "errors": errors,
        "contact_sheet": contact_sheet,
        "items": [
            {
                **item,
                "source_path": repo_rel(project_root, Path(item["source_path"])),
                "pixelated_path": repo_rel(project_root, Path(item["pixelated_path"])),
                "normalized_path": repo_rel(project_root, Path(item["normalized_path"])) if item.get("normalized_path") else "",
            }
            for item in results
        ],
    }
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    print(json.dumps({"ok": not errors, "status": payload["status"], "processed": len(results), "errors": len(errors), "report": repo_rel(project_root, manifest_path), "contact_sheet": contact_sheet}, indent=2))
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
