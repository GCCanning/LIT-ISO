#!/usr/bin/env python3
"""Extract coarse material palettes from local LIT-ISO visual target screenshots.

This is a review aid, not a style copier. It clusters screenshot colors into
material buckets so local generators can tune ramps toward the desired scene
readability without running image generation.
"""

from __future__ import annotations

import argparse
import colorsys
import json
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw


DEFAULT_SCREENSHOTS = [
    r"C:\Users\garyc\OneDrive\Pictures\Screenshots\Screenshot 2026-05-18 125733.png",
    r"C:\Users\garyc\OneDrive\Pictures\Screenshots\Screenshot 2026-05-18 125701.png",
    r"C:\Users\garyc\OneDrive\Pictures\Screenshots\Screenshot 2026-05-15 102234.png",
    r"C:\Users\garyc\OneDrive\Pictures\Screenshots\Screenshot 2026-05-15 100522.png",
]


def repo_path(root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path)


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def quantize(rgb: tuple[int, int, int], step: int = 8) -> tuple[int, int, int]:
    return tuple(max(0, min(255, int(round(channel / step) * step))) for channel in rgb)


def luminance(rgb: tuple[int, int, int]) -> float:
    return 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2]


def classify(rgb: tuple[int, int, int]) -> str | None:
    r, g, b = rgb
    h, s, v = colorsys.rgb_to_hsv(r / 255.0, g / 255.0, b / 255.0)
    lum = luminance(rgb)

    # Drop UI text/black overlays/near-white snow and highlights for terrain tuning.
    if lum < 28 or lum > 235:
        return None
    if s < 0.08 and lum > 190:
        return None

    if 0.22 <= h <= 0.43 and s >= 0.24:
        return "grass"
    if 0.47 <= h <= 0.62 and s >= 0.28 and b > r + 12:
        return "water"
    if (0.02 <= h <= 0.16 and s >= 0.16 and r >= b + 8) or (r > g + 8 and g > b + 4):
        return "dirt"
    if s <= 0.24 and 55 <= lum <= 210:
        return "stone"
    if v < 0.28:
        return "shadow"
    return None


def sorted_palette(counter: Counter[tuple[int, int, int]], limit: int) -> list[dict[str, Any]]:
    colors = sorted(counter.items(), key=lambda item: (-item[1], luminance(item[0]), item[0]))[:limit]
    return [
        {
            "hex": f"#{rgb[0]:02x}{rgb[1]:02x}{rgb[2]:02x}",
            "rgb": list(rgb),
            "count": count,
            "luminance": round(luminance(rgb), 2),
        }
        for rgb, count in colors
    ]


def draw_swatch(path: Path, palettes: dict[str, list[dict[str, Any]]]) -> None:
    row_h = 54
    swatch = 34
    width = 780
    height = 28 + row_h * max(1, len(palettes))
    image = Image.new("RGBA", (width, height), (18, 21, 24, 255))
    draw = ImageDraw.Draw(image)
    draw.text((12, 8), "LIT-ISO screenshot material palette sample", fill=(238, 242, 238, 255))
    for row_index, (name, entries) in enumerate(palettes.items()):
        y = 28 + row_index * row_h
        draw.text((12, y + 13), name, fill=(218, 224, 218, 255))
        for index, entry in enumerate(entries[:14]):
            x = 108 + index * 46
            rgb = tuple(entry["rgb"])
            draw.rectangle([x, y + 4, x + swatch - 1, y + swatch - 1], fill=rgb + (255,))
            draw.rectangle([x, y + 4, x + swatch - 1, y + swatch - 1], outline=(5, 8, 10, 255))
            draw.text((x, y + 40), entry["hex"][1:4], fill=(154, 164, 164, 255))
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path)


def main() -> int:
    parser = argparse.ArgumentParser(description="Analyze local LIT-ISO screenshot material palettes.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--screenshots", nargs="*", default=DEFAULT_SCREENSHOTS)
    parser.add_argument("--output-root", default="Assets/Generated/_Review/litiso_screenshot_palette_v1")
    parser.add_argument("--sample-step", type=int, default=3)
    parser.add_argument("--limit", type=int, default=16)
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    output_root = project_root / args.output_root
    buckets: dict[str, Counter[tuple[int, int, int]]] = defaultdict(Counter)
    inputs: list[dict[str, Any]] = []
    missing: list[str] = []

    for raw_path in args.screenshots:
        path = Path(raw_path)
        if not path.exists():
            missing.append(str(path))
            continue
        image = Image.open(path).convert("RGB")
        width, height = image.size
        pixels = image.load()
        sampled = 0
        for y in range(0, height, max(1, args.sample_step)):
            for x in range(0, width, max(1, args.sample_step)):
                rgb = quantize(pixels[x, y])
                bucket = classify(rgb)
                if bucket:
                    buckets[bucket][rgb] += 1
                    sampled += 1
        inputs.append({"path": str(path), "width": width, "height": height, "classified_samples": sampled})

    ordered_names = ["grass", "dirt", "stone", "water", "shadow"]
    palettes = {
        name: sorted_palette(buckets[name], args.limit)
        for name in ordered_names
        if buckets.get(name)
    }
    swatch_path = output_root / "litiso_screenshot_material_palette.png"
    draw_swatch(swatch_path, palettes)

    manifest_path = output_root / "litiso_screenshot_material_palette.json"
    payload = {
        "schema": "lit_iso.asset_forge.screenshot_material_palette.v1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "status": "review_only_palette_reference",
        "inputs": inputs,
        "missing": missing,
        "sample_step": args.sample_step,
        "palettes": palettes,
        "swatch": repo_path(project_root, swatch_path),
        "notes": [
            "Palette is sampled from screenshots and includes scene lighting/compression/noise.",
            "Use as ramp guidance only; do not treat as exact runtime art source.",
        ],
    }
    write_json(manifest_path, payload)
    print(
        json.dumps(
            {
                "ok": True,
                "manifest": repo_path(project_root, manifest_path),
                "swatch": repo_path(project_root, swatch_path),
                "inputs": len(inputs),
                "missing": len(missing),
                "buckets": {name: len(entries) for name, entries in palettes.items()},
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
