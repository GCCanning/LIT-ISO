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


DEFAULT_REFERENCE = "Assets/Generated/_Review/black_mage_iso_style_lock_v1/black_mage_normalized_128.png"
DEFAULT_SELECTED_MANIFEST = (
    "Assets/Generated/_Review/black_mage_iso_selected_v13_mixed_8d/"
    "black_mage_selected_v13_mixed_8d_manifest.json"
)
DEFAULT_OUT_ROOT = "Assets/Generated/_Review/black_mage_identity_lock_v1"
DIRECTION_ORDER = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"]


ACCENT_PROFILES = {
    "robe_dark": {"target": (27, 23, 31), "radius": 58, "min_ratio": 0.24},
    "hat_tan": {"target": (220, 184, 112), "radius": 70, "min_ratio": 0.018},
    "orange_accent": {"target": (230, 106, 28), "radius": 72, "min_ratio": 0.012},
    "brown_hair_staff": {"target": (94, 59, 44), "radius": 72, "min_ratio": 0.025},
}


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


def font(size: int = 13) -> ImageFont.ImageFont:
    try:
        return ImageFont.truetype("arial.ttf", size)
    except OSError:
        return ImageFont.load_default()


def color_distance(a: tuple[int, int, int], b: tuple[int, int, int]) -> float:
    return math.sqrt(sum((float(a[index]) - float(b[index])) ** 2 for index in range(3)))


def visible_pixels(image: Image.Image) -> list[tuple[int, int, int]]:
    pixels = image.load()
    values: list[tuple[int, int, int]] = []
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = pixels[x, y]
            if a > 16:
                values.append((r, g, b))
    return values


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int] | None:
    return image.getchannel("A").getbbox()


def alpha_coverage(image: Image.Image) -> float:
    alpha = image.getchannel("A")
    visible = 0
    for y in range(image.height):
        for x in range(image.width):
            if alpha.getpixel((x, y)) > 16:
                visible += 1
    return round(visible / float(max(1, image.width * image.height)), 4)


def alpha_iou(reference: Image.Image, candidate: Image.Image) -> float:
    width = max(reference.width, candidate.width)
    height = max(reference.height, candidate.height)
    ref_alpha = Image.new("L", (width, height), 0)
    cand_alpha = Image.new("L", (width, height), 0)
    ref_alpha.paste(reference.getchannel("A"), (0, 0))
    cand_alpha.paste(candidate.getchannel("A"), (0, 0))
    intersection = 0
    union = 0
    for y in range(height):
        for x in range(width):
            rv = ref_alpha.getpixel((x, y)) > 16
            cv = cand_alpha.getpixel((x, y)) > 16
            if rv and cv:
                intersection += 1
            if rv or cv:
                union += 1
    return round(intersection / union, 4) if union else 1.0


def mean_rgb(image: Image.Image) -> tuple[float, float, float]:
    values = visible_pixels(image)
    if not values:
        return (0.0, 0.0, 0.0)
    return tuple(sum(value[index] for value in values) / len(values) for index in range(3))  # type: ignore[return-value]


def palette_distance(reference: Image.Image, candidate: Image.Image, limit: int = 24) -> float:
    ref_counts = Counter(visible_pixels(reference)).most_common(limit)
    cand_counts = Counter(visible_pixels(candidate)).most_common(limit)
    if not ref_counts or not cand_counts:
        return 999.0
    total = sum(count for _, count in cand_counts)
    weighted = 0.0
    for color, count in cand_counts:
        weighted += min(color_distance(color, ref_color) for ref_color, _ in ref_counts) * count
    return round(weighted / float(max(1, total)), 3)


def accent_ratios(image: Image.Image) -> dict[str, float]:
    values = visible_pixels(image)
    total = float(max(1, len(values)))
    ratios: dict[str, float] = {}
    for name, profile in ACCENT_PROFILES.items():
        target = profile["target"]
        radius = float(profile["radius"])
        ratios[name] = round(sum(1 for color in values if color_distance(color, target) <= radius) / total, 4)
    return ratios


def profile_mask(image: Image.Image, profile: dict[str, Any]) -> Image.Image:
    target = profile["target"]
    radius = float(profile["radius"])
    mask = Image.new("1", image.size, 0)
    source = image.load()
    target_pixels = mask.load()
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = source[x, y]
            if a > 16 and color_distance((r, g, b), target) <= radius:
                target_pixels[x, y] = 1
    return mask


def mask_iou(a: Image.Image, b: Image.Image) -> float:
    width = max(a.width, b.width)
    height = max(a.height, b.height)
    a_canvas = Image.new("1", (width, height), 0)
    b_canvas = Image.new("1", (width, height), 0)
    a_canvas.paste(a, (0, 0))
    b_canvas.paste(b, (0, 0))
    intersection = 0
    union = 0
    for y in range(height):
        for x in range(width):
            av = bool(a_canvas.getpixel((x, y)))
            bv = bool(b_canvas.getpixel((x, y)))
            if av and bv:
                intersection += 1
            if av or bv:
                union += 1
    return round(intersection / union, 4) if union else 1.0


def feature_spatial_iou(reference: Image.Image, candidate: Image.Image) -> dict[str, float]:
    values: dict[str, float] = {"alpha": alpha_iou(reference, candidate)}
    for name, profile in ACCENT_PROFILES.items():
        values[name] = mask_iou(profile_mask(reference, profile), profile_mask(candidate, profile))
    return values


def bbox_metrics(image: Image.Image) -> dict[str, Any]:
    bbox = alpha_bbox(image)
    if not bbox:
        return {"bbox": None, "bbox_width": 0, "bbox_height": 0, "center_x": image.width / 2, "center_y": image.height / 2}
    left, top, right, bottom = bbox
    return {
        "bbox": [left, top, right, bottom],
        "bbox_width": right - left,
        "bbox_height": bottom - top,
        "center_x": round((left + right) / 2, 3),
        "center_y": round((top + bottom) / 2, 3),
    }


def score_identity(reference: Image.Image, candidate: Image.Image, direction: str) -> dict[str, Any]:
    ref_bbox = bbox_metrics(reference)
    cand_bbox = bbox_metrics(candidate)
    ref_ratios = accent_ratios(reference)
    cand_ratios = accent_ratios(candidate)
    pal_dist = palette_distance(reference, candidate)
    spatial_iou = feature_spatial_iou(reference, candidate)
    ref_mean = mean_rgb(reference)
    cand_mean = mean_rgb(candidate)
    mean_delta = round(color_distance(ref_mean, cand_mean), 3)
    coverage = alpha_coverage(candidate)
    ref_coverage = alpha_coverage(reference)
    width_ratio = round(cand_bbox["bbox_width"] / max(1, ref_bbox["bbox_width"]), 3)
    height_ratio = round(cand_bbox["bbox_height"] / max(1, ref_bbox["bbox_height"]), 3)
    missing_accents: list[str] = []
    weak_accents: list[str] = []
    front_identity_directions = {"S", "SE", "SW"}
    for name, profile in ACCENT_PROFILES.items():
        if direction in front_identity_directions:
            required = max(float(profile["min_ratio"]), ref_ratios.get(name, 0.0) * 0.48)
        else:
            required = max(float(profile["min_ratio"]), ref_ratios.get(name, 0.0) * 0.28)
        if cand_ratios.get(name, 0.0) <= 0.001:
            missing_accents.append(name)
        elif cand_ratios.get(name, 0.0) < required:
            weak_accents.append(name)

    issues: list[str] = []
    warnings: list[str] = []
    if pal_dist > 42:
        issues.append("palette_too_far_from_reference")
    elif pal_dist > 30:
        warnings.append("palette_drift")
    if mean_delta > 48:
        issues.append("mean_color_too_far_from_reference")
    elif mean_delta > 34:
        warnings.append("mean_color_drift")
    if missing_accents:
        issues.append("missing_key_accents:" + ",".join(missing_accents))
    if weak_accents:
        warnings.append("weak_key_accents:" + ",".join(weak_accents))
    if width_ratio < 0.72 or width_ratio > 1.18:
        issues.append("silhouette_width_not_reference_like")
    if height_ratio < 0.85 or height_ratio > 1.18:
        issues.append("silhouette_height_not_reference_like")
    if abs(coverage - ref_coverage) > 0.09:
        warnings.append("alpha_coverage_drift")
    front_spatial_score = None
    if direction == "S":
        front_spatial_score = round(
            spatial_iou["alpha"] * 0.35
            + spatial_iou["hat_tan"] * 0.25
            + spatial_iou["orange_accent"] * 0.2
            + spatial_iou["brown_hair_staff"] * 0.2,
            4,
        )
        if spatial_iou["alpha"] < 0.66:
            issues.append("front_silhouette_not_reference_locked")
        if front_spatial_score < 0.32:
            issues.append("front_feature_positions_not_reference_locked")

    semantic_notes: list[str] = []
    if direction in {"N", "NE", "NW"}:
        semantic_notes.append("manual_back_view_check_required")
    if direction in {"E", "W"}:
        semantic_notes.append("manual_side_profile_check_required")

    score = round(100.0 - pal_dist - mean_delta * 0.45 - len(issues) * 18.0 - len(warnings) * 6.0, 3)
    if score < 70.0 and "identity_score_below_gate" not in issues:
        issues.append("identity_score_below_gate")
    status = "identity_fail" if issues else "identity_review"
    return {
        "status": status,
        "identity_score": score,
        "identity_gate": {
            "minimum_score": 70.0,
            "front_min_alpha_iou": 0.66,
            "front_min_feature_spatial_score": 0.32,
            "front_spatial_score": front_spatial_score,
            "strict_reference_lock": direction == "S",
        },
        "palette_distance": pal_dist,
        "mean_rgb_delta": mean_delta,
        "feature_spatial_iou": spatial_iou,
        "reference_alpha_coverage": ref_coverage,
        "candidate_alpha_coverage": coverage,
        "width_ratio_to_reference": width_ratio,
        "height_ratio_to_reference": height_ratio,
        "reference_bbox": ref_bbox,
        "candidate_bbox": cand_bbox,
        "reference_accent_ratios": ref_ratios,
        "candidate_accent_ratios": cand_ratios,
        "issues": issues,
        "warnings": warnings,
        "semantic_notes": semantic_notes,
    }


def checker(size: tuple[int, int]) -> Image.Image:
    image = Image.new("RGBA", size, (35, 39, 45, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], 8):
        for x in range(0, size[0], 8):
            fill = (45, 50, 57, 255) if ((x // 8) + (y // 8)) % 2 == 0 else (35, 39, 45, 255)
            draw.rectangle((x, y, x + 7, y + 7), fill=fill)
    return image


def paste_sprite(board: Image.Image, sprite: Image.Image, x: int, y: int, scale: int = 2) -> None:
    preview = sprite.resize((sprite.width * scale, sprite.height * scale), Image.Resampling.NEAREST)
    cell = checker(preview.size)
    cell.alpha_composite(preview)
    board.alpha_composite(cell, (x, y))


def draw_board(project_root: Path, reference: Image.Image, records: list[dict[str, Any]], out_path: Path) -> None:
    title_font = font(14)
    body_font = font(11)
    cell_w = 284
    cell_h = 370
    cols = 3
    rows = 1 + math.ceil(len(records) / cols)
    width = cols * cell_w
    height = 76 + rows * cell_h
    board = Image.new("RGBA", (width, height), (18, 21, 26, 255))
    draw = ImageDraw.Draw(board)
    draw.text((16, 12), "Black Mage Identity Lock Gate", fill=(244, 247, 250, 255), font=title_font)
    draw.text(
        (16, 34),
        "Goal: preserve the supplied reference first; direction generation is invalid if identity fails.",
        fill=(175, 186, 199, 255),
        font=body_font,
    )
    paste_sprite(board, reference, 16, 76, 2)
    draw.text((16, 76 + 260), "REFERENCE", fill=(226, 214, 120, 255), font=title_font)
    ref_ratios = accent_ratios(reference)
    ref_lines = [
        f"coverage {alpha_coverage(reference)}",
        f"dark {ref_ratios['robe_dark']} tan {ref_ratios['hat_tan']}",
        f"orange {ref_ratios['orange_accent']} brown {ref_ratios['brown_hair_staff']}",
    ]
    for idx, line in enumerate(ref_lines):
        draw.text((16, 76 + 282 + idx * 15), line, fill=(184, 196, 208, 255), font=body_font)

    for index, record in enumerate(records):
        col = (index + 1) % cols
        row = (index + 1) // cols
        x = col * cell_w + 12
        y = 76 + row * cell_h
        image = load_rgba(resolve_path(project_root, record["path"]))
        paste_sprite(board, image, x, y, 2)
        color = (239, 106, 92, 255) if record["identity"]["status"] == "identity_fail" else (116, 220, 145, 255)
        draw.text((x, y + 260), f"{record['direction']} {record['identity']['status']}", fill=color, font=title_font)
        lines = [
            f"score {record['identity']['identity_score']} palD {record['identity']['palette_distance']}",
            f"meanD {record['identity']['mean_rgb_delta']} w/h {record['identity']['width_ratio_to_reference']}/{record['identity']['height_ratio_to_reference']}",
            f"alphaIoU {record['identity']['feature_spatial_iou']['alpha']} frontSpatial {record['identity']['identity_gate']['front_spatial_score']}",
            ", ".join(record["identity"]["issues"] or record["identity"]["warnings"] or record["identity"]["semantic_notes"] or ["manual review"]),
        ]
        for idx, line in enumerate(lines):
            draw.text((x, y + 282 + idx * 15), line[:42], fill=(184, 196, 208, 255), font=body_font)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    board.save(out_path)


def main() -> int:
    parser = argparse.ArgumentParser(description="Build an identity-lock report for black mage candidates against the original normalized reference.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--reference", default=DEFAULT_REFERENCE)
    parser.add_argument("--selected-manifest", default=DEFAULT_SELECTED_MANIFEST)
    parser.add_argument("--output-root", default=DEFAULT_OUT_ROOT)
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    reference_path = resolve_path(project_root, args.reference)
    selected_manifest_path = resolve_path(project_root, args.selected_manifest)
    out_root = resolve_path(project_root, args.output_root)
    selected_manifest = read_json(selected_manifest_path)
    reference = load_rgba(reference_path)
    by_direction = {str(item.get("direction")).upper(): item for item in selected_manifest.get("selected") or []}
    records: list[dict[str, Any]] = []

    for direction in DIRECTION_ORDER:
        item = by_direction.get(direction)
        if not item:
            continue
        image_path = resolve_path(project_root, item["path"])
        candidate = load_rgba(image_path)
        records.append(
            {
                "direction": direction,
                "path": repo_path(project_root, image_path),
                "source_variant": item.get("source_variant"),
                "seed": item.get("seed"),
                "structural_score": item.get("score"),
                "identity": score_identity(reference, candidate, direction),
            }
        )

    board_path = out_root / "black_mage_identity_lock_board.png"
    draw_board(project_root, reference, records, board_path)
    fail_count = len([record for record in records if record["identity"]["status"] == "identity_fail"])
    report = {
        "schema": "lit_iso.asset_forge.black_mage_identity_lock_report.v1",
        "generated_utc": utc_now(),
        "status": "identity_gate_failed" if fail_count else "identity_review_only_not_unity_imported",
        "reference": repo_path(project_root, reference_path),
        "selected_manifest": repo_path(project_root, selected_manifest_path),
        "board": repo_path(project_root, board_path),
        "direction_count": len(records),
        "identity_fail_count": fail_count,
        "identity_review_count": len(records) - fail_count,
        "records": records,
        "conclusion": (
            "Current generated 8D pack is direction evidence only, not identity-locked production art."
            if fail_count
            else "Current generated 8D pack passes automated identity heuristics but still requires manual semantic direction review."
        ),
        "gate_policy": {
            "front_s_direction": "must reconstruct the supplied mage reference or use the source anchor directly",
            "score_minimum": 70.0,
            "purpose": "prevent structurally valid 8D sheets from being accepted when they drift from the original character",
        },
        "next_recommendations": [
            "Before producing animation frames, run a front reconstruction test and require it to pass identity lock.",
            "For 8D, use reference-locked generation settings; reject candidates that fail this report even if structural QC passes.",
            "If IP-Adapter cannot preserve the source, use source/hand-authored 8D sheets as training targets rather than prompt-only rerolls.",
        ],
    }
    write_json(out_root / "black_mage_identity_lock_report.json", report)
    print(
        json.dumps(
            {
                "ok": True,
                "report": repo_path(project_root, out_root / "black_mage_identity_lock_report.json"),
                "board": repo_path(project_root, board_path),
                "direction_count": len(records),
                "identity_fail_count": fail_count,
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
