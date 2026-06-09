#!/usr/bin/env python3
from __future__ import annotations

import argparse
import colorsys
import json
import statistics
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def rel(root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path)


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def alpha_bbox(image: Image.Image):
    return image.getchannel("A").getbbox()


def rgb_to_hsv255(rgb: tuple[int, int, int]) -> tuple[float, float, float]:
    h, s, v = colorsys.rgb_to_hsv(rgb[0] / 255, rgb[1] / 255, rgb[2] / 255)
    return h * 360, s, v


def sample_metrics(path: Path) -> dict:
    image = Image.open(path).convert("RGBA")
    box = alpha_bbox(image)
    pixels = image.load()
    if not box:
        return {"path": str(path), "blank": True}

    min_x, min_y, max_x, max_y = box
    split_y = min_y + int((max_y - min_y) * 0.47)
    regions = {"top": [], "side": [], "all": []}
    colors = set()
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = pixels[x, y]
            if a <= 0:
                continue
            rgb = (r, g, b)
            colors.add(rgb)
            regions["all"].append(rgb)
            regions["side" if y >= split_y else "top"].append(rgb)

    def summarize(values: list[tuple[int, int, int]]) -> dict:
        if not values:
            return {"count": 0}
        hsv = [rgb_to_hsv255(value) for value in values]
        return {
            "count": len(values),
            "mean_rgb": [round(statistics.fmean(channel), 2) for channel in zip(*values)],
            "mean_hue": round(statistics.fmean(item[0] for item in hsv), 2),
            "mean_saturation": round(statistics.fmean(item[1] for item in hsv), 4),
            "mean_value": round(statistics.fmean(item[2] for item in hsv), 4),
            "value_stdev": round(statistics.pstdev(item[2] for item in hsv), 4),
        }

    width = max_x - min_x + 1
    height = max_y - min_y + 1
    return {
        "path": str(path),
        "width": image.width,
        "height": image.height,
        "bbox": [min_x, min_y, max_x, max_y],
        "bbox_aspect": round(width / max(1, height), 4),
        "coverage": round(len(regions["all"]) / (image.width * image.height), 4),
        "color_count": len(colors),
        "top": summarize(regions["top"]),
        "side": summarize(regions["side"]),
        "all": summarize(regions["all"]),
    }


def build_preview(root: Path, items: list[dict], output_path: Path) -> None:
    cell_w, cell_h = 210, 132
    preview = Image.new("RGBA", (cell_w * len(items), cell_h), (28, 32, 34, 255))
    draw = ImageDraw.Draw(preview)
    try:
        font = ImageFont.truetype("arial.ttf", 12)
        small = ImageFont.truetype("arial.ttf", 10)
    except Exception:
        font = ImageFont.load_default()
        small = ImageFont.load_default()

    for i, item in enumerate(items):
        path = root / item["path"]
        image = Image.open(path).convert("RGBA")
        scaled = image.resize((96, 96), Image.Resampling.NEAREST)
        x = i * cell_w
        draw.rectangle([x, 0, x + cell_w - 1, cell_h - 1], fill=(32, 37, 40, 255), outline=(70, 78, 82, 255))
        preview.alpha_composite(scaled, (x + 8, 8))
        draw.text((x + 108, 10), item["material"], fill=(226, 233, 224, 255), font=font)
        draw.text((x + 108, 30), f"top V {item['top']['mean_value']:.2f}", fill=(181, 195, 186, 255), font=small)
        draw.text((x + 108, 46), f"side V {item['side']['mean_value']:.2f}", fill=(181, 195, 186, 255), font=small)
        draw.text((x + 108, 62), f"sat {item['all']['mean_saturation']:.2f}", fill=(181, 195, 186, 255), font=small)
        draw.text((x + 108, 78), f"colors {item['color_count']}", fill=(181, 195, 186, 255), font=small)
        draw.text((x + 8, 110), Path(item["path"]).name, fill=(146, 159, 151, 255), font=small)

    preview.save(output_path)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--review-report", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()

    root = Path(args.project_root).resolve()
    report_path = (root / args.review_report).resolve()
    output_path = (root / args.output).resolve()
    report = read_json(report_path)
    metrics = []
    for item in report.get("items", []):
        path = root / item["path"]
        measured = sample_metrics(path)
        measured["material"] = item.get("material", Path(item["path"]).stem)
        measured["source_candidate"] = item.get("source_candidate", "")
        measured["path"] = rel(root, path)
        metrics.append(measured)

    values = [item["all"]["mean_value"] for item in metrics if not item.get("blank")]
    saturations = [item["all"]["mean_saturation"] for item in metrics if not item.get("blank")]
    color_counts = [item["color_count"] for item in metrics if not item.get("blank")]
    summary = {
        "value_range": round(max(values) - min(values), 4) if values else 0,
        "saturation_range": round(max(saturations) - min(saturations), 4) if saturations else 0,
        "max_color_count": max(color_counts) if color_counts else 0,
        "recommendation": "Generate transition tiles before Unity promotion; material contrast is expected until blend tiles exist.",
    }
    if summary["value_range"] > 0.28:
        summary["recommendation"] = "High value contrast: prefer new material masters or transitions over destructive recolor."

    output_path.parent.mkdir(parents=True, exist_ok=True)
    preview_path = output_path.with_name(output_path.stem + "_preview.png")
    build_preview(root, metrics, preview_path)
    payload = {
        "schema": "lit_iso.asset_forge.terrain_pack_analysis.v1",
        "review_report": rel(root, report_path),
        "preview": rel(root, preview_path),
        "summary": summary,
        "items": metrics,
    }
    output_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    print(json.dumps(payload, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
