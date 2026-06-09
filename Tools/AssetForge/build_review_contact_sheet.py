#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


def repo_path(project_root: Path, value: str) -> Path:
    path = Path(value.replace("/", "\\"))
    if path.is_absolute():
        return path
    return project_root / path


def load_font(size: int) -> ImageFont.ImageFont:
    for name in ("arial.ttf", "segoeui.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def fit_image(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    canvas = Image.new("RGBA", size, (22, 24, 28, 255))
    source = image.convert("RGBA")
    source.thumbnail((size[0] - 16, size[1] - 16), Image.Resampling.NEAREST)
    x = (size[0] - source.width) // 2
    y = (size[1] - source.height) // 2
    canvas.alpha_composite(source, (x, y))
    return canvas


def load_review_image(project_root: Path, job_name: str) -> tuple[Path, str]:
    manifest_path = project_root / "Assets" / "Generated" / "_Review" / job_name / "generation_manifest.json"
    if not manifest_path.exists():
        raise FileNotFoundError(f"Missing review manifest for {job_name}: {manifest_path}")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    generated = manifest.get("generated_files") or []
    if not generated:
        raise ValueError(f"Review pack has no generated_files: {manifest_path}")
    return repo_path(project_root, generated[0]), job_name


def draw_label(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], text: str, font: ImageFont.ImageFont) -> None:
    x0, y0, x1, y1 = box
    draw.rectangle(box, fill=(12, 14, 18, 255))
    lines: list[str] = []
    for raw in text.split("|"):
        raw = raw.strip()
        if raw:
            lines.append(raw)
    y = y0 + 6
    for line in lines[:3]:
        draw.text((x0 + 8, y), line, fill=(230, 236, 244, 255), font=font)
        y += 18


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--template", type=Path)
    parser.add_argument("--template-label", default="OGA template")
    parser.add_argument("--job", action="append", default=[], help="JobName=Label or just JobName")
    parser.add_argument("--cell-width", type=int, default=192)
    parser.add_argument("--cell-height", type=int, default=224)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    entries: list[tuple[Path, str]] = []
    if args.template:
        entries.append((args.template.resolve(), args.template_label))
    for spec in args.job:
        if "=" in spec:
            job_name, label = spec.split("=", 1)
        else:
            job_name, label = spec, spec
        image_path, _ = load_review_image(project_root, job_name.strip())
        entries.append((image_path, label.strip()))
    if not entries:
        raise ValueError("Pass --template and/or at least one --job.")

    font = load_font(14)
    title_font = load_font(16)
    cell_w, cell_h = args.cell_width, args.cell_height
    image_h = cell_h - 54
    sheet = Image.new("RGBA", (cell_w * len(entries), cell_h), (34, 37, 43, 255))
    draw = ImageDraw.Draw(sheet)
    for index, (path, label) in enumerate(entries):
        if not path.exists():
            raise FileNotFoundError(path)
        x = index * cell_w
        with Image.open(path) as image:
            fitted = fit_image(image, (cell_w, image_h))
        sheet.alpha_composite(fitted, (x, 0))
        draw.rectangle((x, 0, x + cell_w - 1, cell_h - 1), outline=(72, 80, 96, 255), width=1)
        draw_label(draw, (x, image_h, x + cell_w, cell_h), label, title_font if index == 0 else font)

    output = args.output
    if not output.is_absolute():
        output = project_root / output
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output)
    print(json.dumps({"ok": True, "output": str(output), "entries": len(entries)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
