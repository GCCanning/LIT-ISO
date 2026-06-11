#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import math
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont


DEFAULT_TILE_MANIFEST = (
    "Assets/Generated/_Review/reference32_mask_locked_texture_family_screenshot_balanced_v1/"
    "selected_tile_family_manifest.json"
)
DEFAULT_MAGE_MANIFEST = (
    "Assets/Generated/_Review/black_mage_iso_selected_v13_mixed_8d/"
    "black_mage_selected_v13_mixed_8d_manifest.json"
)
DEFAULT_MAGE_REFERENCE = r"C:\Users\garyc\OneDrive\Desktop\PixelArt\BlackMage1.png"
DEFAULT_OUTPUT_ROOT = "Assets/Generated/_Review/litiso_pipeline_visual_delta_v1"
DIRECTION_ORDER = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def repo_path(root: Path, path: Path | str) -> str:
    path = Path(path)
    if not path.is_absolute():
        path = root / path
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path.resolve()).replace("\\", "/")


def resolve_path(root: Path, value: str | Path) -> Path:
    path = Path(value)
    return path if path.is_absolute() else root / path


def load_rgba(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int] | None:
    alpha = image.getchannel("A")
    return alpha.getbbox()


def crop_to_alpha(image: Image.Image, padding: int = 0) -> Image.Image:
    bbox = alpha_bbox(image)
    if not bbox:
        return image.copy()
    left, top, right, bottom = bbox
    left = max(0, left - padding)
    top = max(0, top - padding)
    right = min(image.width, right + padding)
    bottom = min(image.height, bottom + padding)
    return image.crop((left, top, right, bottom))


def scaled(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    src = crop_to_alpha(image, 1)
    scale = min(size[0] / max(1, src.width), size[1] / max(1, src.height))
    new_size = (max(1, int(src.width * scale)), max(1, int(src.height * scale)))
    resized = src.resize(new_size, Image.Resampling.NEAREST)
    canvas.alpha_composite(resized, ((size[0] - new_size[0]) // 2, (size[1] - new_size[1]) // 2))
    return canvas


def colors(image: Image.Image) -> list[tuple[int, int, int]]:
    values: list[tuple[int, int, int]] = []
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = pixels[x, y]
            if a > 16:
                values.append((r, g, b))
    return values


def mean_rgb(image: Image.Image) -> tuple[float, float, float]:
    values = colors(image)
    if not values:
        return (0.0, 0.0, 0.0)
    count = len(values)
    return (
        sum(value[0] for value in values) / count,
        sum(value[1] for value in values) / count,
        sum(value[2] for value in values) / count,
    )


def mean_delta(a: tuple[float, float, float], b: tuple[float, float, float]) -> float:
    return math.sqrt(sum((a[index] - b[index]) ** 2 for index in range(3)))


def color_count(image: Image.Image) -> int:
    return len(set(colors(image)))


def dark_outline_ratio(image: Image.Image) -> float:
    alpha = image.getchannel("A")
    pixels = image.load()
    outline = 0
    dark = 0
    for y in range(image.height):
        for x in range(image.width):
            if alpha.getpixel((x, y)) <= 16:
                continue
            neighbor_transparent = False
            for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if nx < 0 or ny < 0 or nx >= image.width or ny >= image.height or alpha.getpixel((nx, ny)) <= 16:
                    neighbor_transparent = True
                    break
            if not neighbor_transparent:
                continue
            outline += 1
            r, g, b, _ = pixels[x, y]
            if max(r, g, b) <= 78:
                dark += 1
    return round(dark / outline, 4) if outline else 0.0


def alpha_iou(a: Image.Image, b: Image.Image) -> float:
    width = max(a.width, b.width)
    height = max(a.height, b.height)
    a_canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    b_canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    a_canvas.alpha_composite(a, (0, 0))
    b_canvas.alpha_composite(b, (0, 0))
    intersection = 0
    union = 0
    a_alpha = a_canvas.getchannel("A")
    b_alpha = b_canvas.getchannel("A")
    for y in range(height):
        for x in range(width):
            av = a_alpha.getpixel((x, y)) > 16
            bv = b_alpha.getpixel((x, y)) > 16
            if av and bv:
                intersection += 1
            if av or bv:
                union += 1
    return round(intersection / union, 4) if union else 1.0


def alpha_delta_image(source: Image.Image, current: Image.Image, scale: int = 4) -> Image.Image:
    width = max(source.width, current.width)
    height = max(source.height, current.height)
    out = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    src_alpha = source.getchannel("A")
    cur_alpha = current.getchannel("A")
    pixels = out.load()
    for y in range(height):
        for x in range(width):
            sv = x < source.width and y < source.height and src_alpha.getpixel((x, y)) > 16
            cv = x < current.width and y < current.height and cur_alpha.getpixel((x, y)) > 16
            if sv and cv:
                pixels[x, y] = (210, 214, 220, 255)
            elif sv:
                pixels[x, y] = (65, 130, 245, 255)
            elif cv:
                pixels[x, y] = (65, 220, 120, 255)
    return out.resize((width * scale, height * scale), Image.Resampling.NEAREST)


def palette_swatch(image: Image.Image, size: tuple[int, int] = (112, 24), limit: int = 8) -> Image.Image:
    counter = Counter(colors(image))
    swatch = Image.new("RGBA", size, (28, 30, 36, 255))
    draw = ImageDraw.Draw(swatch)
    if not counter:
        return swatch
    top = counter.most_common(limit)
    cell = max(1, size[0] // len(top))
    for index, (color, _) in enumerate(top):
        draw.rectangle((index * cell, 0, (index + 1) * cell - 1, size[1]), fill=color + (255,))
    return swatch


def font() -> ImageFont.ImageFont:
    try:
        return ImageFont.truetype("arial.ttf", 13)
    except OSError:
        return ImageFont.load_default()


def small_font() -> ImageFont.ImageFont:
    try:
        return ImageFont.truetype("arial.ttf", 11)
    except OSError:
        return ImageFont.load_default()


def tile_metrics(project_root: Path, item: dict[str, Any]) -> dict[str, Any]:
    source_path = resolve_path(project_root, item["source_path"])
    current_path = resolve_path(project_root, item["path"])
    source = load_rgba(source_path)
    current = load_rgba(current_path)
    source_mean = mean_rgb(source)
    current_mean = mean_rgb(current)
    return {
        "id": item.get("id"),
        "source_path": repo_path(project_root, source_path),
        "current_path": repo_path(project_root, current_path),
        "alpha_iou": alpha_iou(source, current),
        "source_color_count": color_count(source),
        "current_color_count": color_count(current),
        "source_mean_rgb": [round(value, 2) for value in source_mean],
        "current_mean_rgb": [round(value, 2) for value in current_mean],
        "mean_rgb_delta": round(mean_delta(source_mean, current_mean), 3),
        "source_outline_ratio": dark_outline_ratio(source),
        "current_outline_ratio": dark_outline_ratio(current),
        "width": current.width,
        "height": current.height,
        "status": item.get("status"),
    }


def mage_metrics(project_root: Path, item: dict[str, Any], reference: Image.Image | None) -> dict[str, Any]:
    current_path = resolve_path(project_root, item["path"])
    current = load_rgba(current_path)
    current_mean = mean_rgb(current)
    ref_mean = mean_rgb(reference) if reference else (0.0, 0.0, 0.0)
    bbox = alpha_bbox(current)
    return {
        "direction": item.get("direction"),
        "path": repo_path(project_root, current_path),
        "source_variant": item.get("source_variant"),
        "seed": item.get("seed"),
        "score": item.get("score"),
        "alpha_coverage": item.get("metrics", {}).get("alpha_coverage"),
        "bbox": item.get("metrics", {}).get("bbox") or bbox,
        "bbox_width": item.get("metrics", {}).get("bbox_width"),
        "bbox_height": item.get("metrics", {}).get("bbox_height"),
        "width_ratio_to_ref": item.get("metrics", {}).get("width_ratio_to_ref"),
        "height_ratio_to_ref": item.get("metrics", {}).get("height_ratio_to_ref"),
        "mean_rgb_delta_from_reference": round(mean_delta(ref_mean, current_mean), 3) if reference else None,
        "current_color_count": color_count(current),
        "current_outline_ratio": dark_outline_ratio(current),
        "manual_direction_quality": item.get("manual_direction_quality"),
    }


def recommendation_for_tile(metric: dict[str, Any]) -> list[str]:
    notes: list[str] = []
    if metric["alpha_iou"] < 0.98:
        notes.append("reject_or_regenerate_geometry_alpha_drift")
    if metric["mean_rgb_delta"] > 85:
        notes.append("palette_shift_large_tune_material_ramp")
    if metric["current_outline_ratio"] < metric["source_outline_ratio"] * 0.65:
        notes.append("outline_too_weak_strengthen_dark_edge_pixels")
    if metric["current_color_count"] > 16:
        notes.append("palette_too_large_quantize_before_training")
    if not notes:
        notes.append("geometry_ready_manual_art_review")
    return notes


def recommendation_for_mage(metric: dict[str, Any]) -> list[str]:
    notes: list[str] = []
    direction = str(metric.get("direction") or "")
    if direction in {"E", "W"} and metric.get("width_ratio_to_ref") and metric["width_ratio_to_ref"] > 0.92:
        notes.append("side_view_may_still_be_too_front_facing")
    if direction in {"N", "NE", "NW"} and metric.get("mean_rgb_delta_from_reference") is not None:
        notes.append("manual_check_back_facing_identity_vs_reference")
    if metric.get("height_ratio_to_ref") and metric["height_ratio_to_ref"] > 1.12:
        notes.append("sprite_too_tall_reduce_vertical_scale")
    if metric.get("current_outline_ratio", 0) < 0.2:
        notes.append("outline_too_weak")
    if not notes:
        notes.append("manual_direction_review_required")
    return notes


def paste_card(
    board: Image.Image,
    draw: ImageDraw.ImageDraw,
    image: Image.Image,
    x: int,
    y: int,
    label: str,
    caption: list[str],
    card_size: tuple[int, int],
    image_size: tuple[int, int],
    title_font: ImageFont.ImageFont,
    body_font: ImageFont.ImageFont,
) -> None:
    draw.rectangle((x, y, x + card_size[0], y + card_size[1]), fill=(38, 42, 49, 255), outline=(74, 82, 96, 255))
    draw.text((x + 8, y + 7), label, fill=(234, 238, 244, 255), font=title_font)
    img = scaled(image, image_size)
    board.alpha_composite(img, (x + (card_size[0] - image_size[0]) // 2, y + 28))
    text_y = y + 34 + image_size[1]
    for line in caption[:4]:
        draw.text((x + 8, text_y), clamp_text(line, 34), fill=(184, 193, 207, 255), font=body_font)
        text_y += 14


def clamp_text(value: str, limit: int) -> str:
    if len(value) <= limit:
        return value
    return value[: max(0, limit - 3)] + "..."


def build_board(
    project_root: Path,
    tile_manifest: dict[str, Any],
    mage_manifest: dict[str, Any],
    mage_reference_path: Path,
    out_root: Path,
    tile_metrics_list: list[dict[str, Any]],
    mage_metrics_list: list[dict[str, Any]],
) -> dict[str, str]:
    title_font = font()
    body_font = small_font()
    board_width = 1180
    tile_row_height = 132
    mage_row_height = 190
    board_height = 158 + len(tile_metrics_list) * tile_row_height + 24 + mage_row_height * 2
    board = Image.new("RGBA", (board_width, board_height), (22, 24, 29, 255))
    draw = ImageDraw.Draw(board)
    draw.text((24, 20), "LIT-ISO Asset Pipeline Visual Delta", fill=(244, 247, 251, 255), font=title_font)
    draw.text(
        (24, 42),
        "Blue = source-only alpha, green = generated-only alpha, gray = overlap. Review-only; no Unity import.",
        fill=(178, 188, 202, 255),
        font=body_font,
    )
    draw.text((24, 68), "Tiles: source vs current screenshot-balanced mask-locked family", fill=(218, 228, 236, 255), font=title_font)
    draw.text((206, 88), "source", fill=(160, 172, 188, 255), font=body_font)
    draw.text((356, 88), "current", fill=(160, 172, 188, 255), font=body_font)
    draw.text((512, 88), "alpha delta", fill=(160, 172, 188, 255), font=body_font)
    draw.text((674, 88), "metrics", fill=(160, 172, 188, 255), font=body_font)

    y = 112
    for metric in tile_metrics_list:
        source = load_rgba(resolve_path(project_root, metric["source_path"]))
        current = load_rgba(resolve_path(project_root, metric["current_path"]))
        delta = alpha_delta_image(source, current)
        draw.text((24, y + 10), str(metric["id"]), fill=(238, 241, 246, 255), font=title_font)
        board.alpha_composite(source.resize((128, 128), Image.Resampling.NEAREST), (200, y))
        board.alpha_composite(current.resize((128, 128), Image.Resampling.NEAREST), (356, y))
        board.alpha_composite(delta, (512, y))
        board.alpha_composite(palette_swatch(current), (674, y + 14))
        lines = [
            f"alpha IoU {metric['alpha_iou']}  mean RGB delta {metric['mean_rgb_delta']}",
            f"colors {metric['current_color_count']}  outline {metric['current_outline_ratio']}",
            ", ".join(metric["recommendations"]),
        ]
        for index, line in enumerate(lines):
            draw.text((674, y + 48 + index * 17), line, fill=(186, 196, 210, 255), font=body_font)
        y += tile_row_height

    y += 10
    draw.text((24, y), "Black mage: reference vs current selected 8D evidence", fill=(218, 228, 236, 255), font=title_font)
    y += 28
    mage_reference = load_rgba(mage_reference_path) if mage_reference_path.exists() else None
    if mage_reference:
        paste_card(
            board,
            draw,
            mage_reference,
            24,
            y,
            "Reference",
            [mage_reference_path.name, "style/identity target"],
            (170, 178),
            (128, 128),
            title_font,
            body_font,
        )

    by_direction = {str(item.get("direction")).upper(): item for item in mage_manifest.get("selected") or []}
    by_metric = {str(item.get("direction")).upper(): item for item in mage_metrics_list}
    x = 210
    for direction in DIRECTION_ORDER[:4]:
        item = by_direction.get(direction)
        metric = by_metric.get(direction, {})
        if not item:
            continue
        image = load_rgba(resolve_path(project_root, item["path"]))
        paste_card(
            board,
            draw,
            image,
            x,
            y,
            direction,
            [
                f"{metric.get('source_variant')} seed {metric.get('seed')}",
                f"score {metric.get('score')} rgbΔ {metric.get('mean_rgb_delta_from_reference')}",
                ", ".join(metric.get("recommendations", [])[:2]),
            ],
            (220, 178),
            (128, 128),
            title_font,
            body_font,
        )
        x += 232

    y += mage_row_height
    x = 210
    for direction in DIRECTION_ORDER[4:]:
        item = by_direction.get(direction)
        metric = by_metric.get(direction, {})
        if not item:
            continue
        image = load_rgba(resolve_path(project_root, item["path"]))
        paste_card(
            board,
            draw,
            image,
            x,
            y,
            direction,
            [
                f"{metric.get('source_variant')} seed {metric.get('seed')}",
                f"score {metric.get('score')} rgbΔ {metric.get('mean_rgb_delta_from_reference')}",
                ", ".join(metric.get("recommendations", [])[:2]),
            ],
            (220, 178),
            (128, 128),
            title_font,
            body_font,
        )
        x += 232

    out_root.mkdir(parents=True, exist_ok=True)
    board_path = out_root / "litiso_pipeline_visual_delta_board.png"
    board.save(board_path)
    return {"visual_delta_board": repo_path(project_root, board_path)}


def main() -> int:
    parser = argparse.ArgumentParser(description="Build visual delta board and metrics for current best LIT-ISO asset pipeline packs.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--tile-manifest", default=DEFAULT_TILE_MANIFEST)
    parser.add_argument("--mage-manifest", default=DEFAULT_MAGE_MANIFEST)
    parser.add_argument("--mage-reference", default=DEFAULT_MAGE_REFERENCE)
    parser.add_argument("--output-root", default=DEFAULT_OUTPUT_ROOT)
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    tile_manifest_path = resolve_path(project_root, args.tile_manifest).resolve()
    mage_manifest_path = resolve_path(project_root, args.mage_manifest).resolve()
    mage_reference_path = resolve_path(project_root, args.mage_reference).resolve()
    out_root = resolve_path(project_root, args.output_root).resolve()
    tile_manifest = read_json(tile_manifest_path)
    mage_manifest = read_json(mage_manifest_path)
    mage_reference = load_rgba(mage_reference_path) if mage_reference_path.exists() else None

    tile_metrics_list = [tile_metrics(project_root, item) for item in tile_manifest.get("selected") or []]
    for metric in tile_metrics_list:
        metric["recommendations"] = recommendation_for_tile(metric)

    mage_metrics_list = [mage_metrics(project_root, item, mage_reference) for item in mage_manifest.get("selected") or []]
    for metric in mage_metrics_list:
        metric["recommendations"] = recommendation_for_mage(metric)

    board_paths = build_board(
        project_root,
        tile_manifest,
        mage_manifest,
        mage_reference_path,
        out_root,
        tile_metrics_list,
        mage_metrics_list,
    )

    report = {
        "schema": "lit_iso.asset_forge.pipeline_visual_delta_report.v1",
        "generated_utc": utc_now(),
        "status": "review_only_not_unity_imported",
        "tile_manifest": repo_path(project_root, tile_manifest_path),
        "mage_manifest": repo_path(project_root, mage_manifest_path),
        "mage_reference": repo_path(project_root, mage_reference_path) if mage_reference_path.exists() else None,
        "outputs": board_paths,
        "tile_metrics": tile_metrics_list,
        "mage_metrics": mage_metrics_list,
        "summary": {
            "tile_count": len(tile_metrics_list),
            "tile_min_alpha_iou": min((item["alpha_iou"] for item in tile_metrics_list), default=None),
            "tile_max_mean_rgb_delta": max((item["mean_rgb_delta"] for item in tile_metrics_list), default=None),
            "mage_direction_count": len(mage_metrics_list),
            "mage_directions": [item["direction"] for item in mage_metrics_list],
        },
        "next_recommendations": [
            "If tile alpha IoU is high, keep mask-locked geometry and tune color ramps/details only.",
            "If any tile palette shift is too large, adjust the deterministic material palette rather than rerunning diffusion.",
            "Review mage side/back cards manually; current metrics catch scale/palette, not semantic direction correctness.",
            "Only after manual approval should capture_approved_review_pack.py be applied to external datasets.",
        ],
    }
    report_path = out_root / "litiso_pipeline_visual_delta_report.json"
    write_json(report_path, report)
    print(
        json.dumps(
            {
                "ok": True,
                "report": repo_path(project_root, report_path),
                "board": board_paths["visual_delta_board"],
                "tile_count": len(tile_metrics_list),
                "mage_direction_count": len(mage_metrics_list),
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
