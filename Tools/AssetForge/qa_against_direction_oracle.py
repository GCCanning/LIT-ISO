#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


ALPHA_THRESHOLD = 8
BBOX_SIZE_WARNING_PX = 12
CENTROID_WARNING_PX = 10.0
COVERAGE_WARNING_ABS = 0.08
RGB_DISTANCE_WARNING = 48.0
REVIEW_IMAGE_NAMES = ("cleaned.png", "snapped.png", "generated.png")

try:
    RESAMPLE_BICUBIC = Image.Resampling.BICUBIC
except AttributeError:
    RESAMPLE_BICUBIC = Image.BICUBIC


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def repo_path(project_root: Path, value: str | None) -> Path | None:
    if not value:
        return None
    path = Path(value.replace("/", "\\"))
    return path if path.is_absolute() else project_root / path


def rel_path(project_root: Path, path: Path | None) -> str | None:
    if path is None:
        return None
    try:
        return path.resolve().relative_to(project_root).as_posix()
    except ValueError:
        return str(path)


def parse_job(value: str) -> tuple[str, str]:
    if "=" not in value:
        raise argparse.ArgumentTypeError("--job must use Direction=JobName")
    direction, job_name = value.split("=", 1)
    direction = direction.strip()
    job_name = job_name.strip()
    if not direction or not job_name:
        raise argparse.ArgumentTypeError("--job must use non-empty Direction=JobName")
    return direction, job_name


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def find_review_image(project_root: Path, job_name: str) -> Path | None:
    job_dir = project_root / "Assets" / "Generated" / "_Review" / job_name
    for name in REVIEW_IMAGE_NAMES:
        candidate = job_dir / name
        if candidate.exists():
            return candidate
    manifest_path = job_dir / "generation_manifest.json"
    if manifest_path.exists():
        manifest = load_json(manifest_path)
        for generated in manifest.get("generated_files") or []:
            candidate = repo_path(project_root, generated)
            if candidate is not None and candidate.exists():
                return candidate
    return None


def image_stats(image: Image.Image) -> dict[str, Any]:
    rgba = image.convert("RGBA")
    width, height = rgba.size
    pixels = rgba.load()
    min_x = width
    min_y = height
    max_x = -1
    max_y = -1
    alpha_sum = 0
    weighted_x = 0.0
    weighted_y = 0.0
    opaque_pixels = 0

    for y in range(height):
        for x in range(width):
            alpha = pixels[x, y][3]
            if alpha <= ALPHA_THRESHOLD:
                continue
            opaque_pixels += 1
            alpha_sum += alpha
            weighted_x += x * alpha
            weighted_y += y * alpha
            min_x = min(min_x, x)
            min_y = min(min_y, y)
            max_x = max(max_x, x)
            max_y = max(max_y, y)

    if opaque_pixels == 0:
        return {
            "image_size": {"width": width, "height": height},
            "alpha_bbox": None,
            "centroid": None,
            "foreground_coverage": 0.0,
            "size": {"width": 0, "height": 0},
        }

    bbox_width = max_x - min_x + 1
    bbox_height = max_y - min_y + 1
    return {
        "image_size": {"width": width, "height": height},
        "alpha_bbox": {"x": min_x, "y": min_y, "width": bbox_width, "height": bbox_height},
        "centroid": {"x": round(weighted_x / alpha_sum, 3), "y": round(weighted_y / alpha_sum, 3)},
        "foreground_coverage": round(opaque_pixels / float(width * height), 6),
        "size": {"width": bbox_width, "height": bbox_height},
    }


def image_stats_from_path(image_path: Path) -> dict[str, Any]:
    if not image_path.exists():
        raise FileNotFoundError(f"Image does not exist: {image_path}")
    with Image.open(image_path) as image:
        return image_stats(image)


def normalize_to_cell(image: Image.Image, width: int, height: int) -> Image.Image:
    source = image.convert("RGBA")
    if source.size == (width, height):
        return source.copy()
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    source.thumbnail((width, height), Image.Resampling.NEAREST)
    canvas.alpha_composite(source, ((width - source.width) // 2, height - source.height))
    return canvas


def image_stats_from_oracle_frame(image_path: Path, frame: dict[str, Any]) -> dict[str, Any]:
    rect = frame.get("rect") or {}
    width = int(rect.get("width") or 0)
    height = int(rect.get("height") or 0)
    if width <= 0 or height <= 0:
        return image_stats_from_path(image_path)
    with Image.open(image_path) as image:
        return image_stats(normalize_to_cell(image, width, height))


def average_rgb_distance(generated_path: Path, oracle_path: Path, oracle_frame: dict[str, Any] | None = None) -> float:
    with Image.open(generated_path) as generated_source, Image.open(oracle_path) as oracle_source:
        generated = generated_source.convert("RGBA")
        if oracle_frame is not None:
            rect = oracle_frame.get("rect") or {}
            width = int(rect.get("width") or 0)
            height = int(rect.get("height") or 0)
            oracle = normalize_to_cell(oracle_source, width, height) if width > 0 and height > 0 else oracle_source.convert("RGBA")
        else:
            oracle = oracle_source.convert("RGBA")
        if generated.size != oracle.size:
            generated = generated.resize(oracle.size, RESAMPLE_BICUBIC)

        generated_pixels = generated.load()
        oracle_pixels = oracle.load()
        width, height = oracle.size
        total = 0.0
        count = 0
        for y in range(height):
            for x in range(width):
                gr, gg, gb, ga = generated_pixels[x, y]
                or_, og, ob, oa = oracle_pixels[x, y]
                if ga <= ALPHA_THRESHOLD and oa <= ALPHA_THRESHOLD:
                    continue
                total += ((gr - or_) ** 2 + (gg - og) ** 2 + (gb - ob) ** 2) ** 0.5
                count += 1
        return round(total / count, 3) if count else 0.0


def load_oracle_frames(project_root: Path, oracle_manifest_path: Path) -> tuple[dict[str, Any], dict[str, dict[str, Any]]]:
    manifest = load_json(oracle_manifest_path)
    frames_by_direction: dict[str, dict[str, Any]] = {}

    for frame in manifest.get("frames", []):
        direction = str(frame.get("direction", "")).strip()
        if not direction:
            continue
        source_path = repo_path(project_root, frame.get("source_image"))
        if source_path is None or not source_path.exists():
            raise FileNotFoundError(f"Oracle frame {direction} has no readable source_image: {source_path}")
        frames_by_direction[direction] = {
            "index": frame.get("index"),
            "direction": direction,
            "source_image": rel_path(project_root, source_path),
            "rect": frame.get("rect"),
            "metrics": image_stats_from_oracle_frame(source_path, frame),
        }

    return manifest, frames_by_direction


def number(metrics: dict[str, Any], *path: str) -> float | None:
    value: Any = metrics
    for key in path:
        if value is None:
            return None
        value = value.get(key)
    return float(value) if value is not None else None


def rounded(value: float | None) -> float | None:
    return round(value, 3) if value is not None else None


def metric_delta(generated: dict[str, Any], oracle: dict[str, Any], rgb_distance: float) -> dict[str, Any]:
    gen_cx = number(generated, "centroid", "x")
    gen_cy = number(generated, "centroid", "y")
    oracle_cx = number(oracle, "centroid", "x")
    oracle_cy = number(oracle, "centroid", "y")
    centroid_distance = None
    if None not in (gen_cx, gen_cy, oracle_cx, oracle_cy):
        centroid_distance = ((gen_cx - oracle_cx) ** 2 + (gen_cy - oracle_cy) ** 2) ** 0.5

    return {
        "image_width": int(number(generated, "image_size", "width") or 0)
        - int(number(oracle, "image_size", "width") or 0),
        "image_height": int(number(generated, "image_size", "height") or 0)
        - int(number(oracle, "image_size", "height") or 0),
        "bbox_width": int(number(generated, "size", "width") or 0) - int(number(oracle, "size", "width") or 0),
        "bbox_height": int(number(generated, "size", "height") or 0) - int(number(oracle, "size", "height") or 0),
        "centroid": {
            "x": rounded(None if gen_cx is None or oracle_cx is None else gen_cx - oracle_cx),
            "y": rounded(None if gen_cy is None or oracle_cy is None else gen_cy - oracle_cy),
            "distance": rounded(centroid_distance),
        },
        "foreground_coverage": rounded(
            float(generated.get("foreground_coverage", 0.0)) - float(oracle.get("foreground_coverage", 0.0))
        ),
        "average_rgb_distance": rgb_distance,
    }


def warning_notes(direction: str, job_name: str, image: str | None, delta: dict[str, Any]) -> list[dict[str, Any]]:
    notes = []
    if abs(delta["bbox_width"]) > BBOX_SIZE_WARNING_PX:
        notes.append(
            {
                "severity": "warning",
                "kind": "bbox_width_delta",
                "direction": direction,
                "job_name": job_name,
                "image": image,
                "delta_px": delta["bbox_width"],
                "threshold_px": BBOX_SIZE_WARNING_PX,
            }
        )
    if abs(delta["bbox_height"]) > BBOX_SIZE_WARNING_PX:
        notes.append(
            {
                "severity": "warning",
                "kind": "bbox_height_delta",
                "direction": direction,
                "job_name": job_name,
                "image": image,
                "delta_px": delta["bbox_height"],
                "threshold_px": BBOX_SIZE_WARNING_PX,
            }
        )
    centroid_distance = delta["centroid"]["distance"]
    if centroid_distance is not None and centroid_distance > CENTROID_WARNING_PX:
        notes.append(
            {
                "severity": "warning",
                "kind": "centroid_delta",
                "direction": direction,
                "job_name": job_name,
                "image": image,
                "distance_px": centroid_distance,
                "threshold_px": CENTROID_WARNING_PX,
            }
        )
    coverage_delta = delta["foreground_coverage"]
    if coverage_delta is not None and abs(coverage_delta) > COVERAGE_WARNING_ABS:
        notes.append(
            {
                "severity": "warning",
                "kind": "coverage_delta",
                "direction": direction,
                "job_name": job_name,
                "image": image,
                "delta": coverage_delta,
                "threshold_abs": COVERAGE_WARNING_ABS,
            }
        )
    rgb_distance = delta["average_rgb_distance"]
    if rgb_distance > RGB_DISTANCE_WARNING:
        notes.append(
            {
                "severity": "warning",
                "kind": "average_rgb_distance",
                "direction": direction,
                "job_name": job_name,
                "image": image,
                "distance": rgb_distance,
                "threshold": RGB_DISTANCE_WARNING,
            }
        )
    return notes


def candidate_score(delta: dict[str, Any]) -> float:
    centroid_distance = delta.get("centroid", {}).get("distance") or 0.0
    bbox_width = abs(float(delta.get("bbox_width") or 0.0))
    bbox_height = abs(float(delta.get("bbox_height") or 0.0))
    coverage = abs(float(delta.get("foreground_coverage") or 0.0)) * 100.0
    rgb_distance = float(delta.get("average_rgb_distance") or 0.0)
    return round(rgb_distance + centroid_distance * 2.0 + bbox_width + bbox_height + coverage, 3)


def ranking_for(comparisons: list[dict[str, Any]]) -> list[dict[str, Any]]:
    ranked = []
    for comparison in comparisons:
        delta = comparison["metrics"]["delta"]
        ranked.append(
            {
                "direction": comparison["direction"],
                "job_name": comparison["job_name"],
                "generated_image": comparison["generated_image"],
                "score": candidate_score(delta),
                "average_rgb_distance": delta["average_rgb_distance"],
                "centroid_distance": delta["centroid"]["distance"],
                "warning_count": len(comparison["warnings"]),
            }
        )
    return sorted(ranked, key=lambda item: (item["direction"], item["score"]))


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Compare generated 4D review jobs against a direction oracle sheet manifest."
    )
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--oracle-manifest", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--job", action="append", required=True, type=parse_job, help="Direction=JobName")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    oracle_manifest_path = args.oracle_manifest
    if not oracle_manifest_path.is_absolute():
        oracle_manifest_path = project_root / oracle_manifest_path
    output = args.output if args.output.is_absolute() else project_root / args.output

    oracle_manifest, oracle_frames = load_oracle_frames(project_root, oracle_manifest_path)
    directions = [direction for direction, _ in args.job]
    issues: list[dict[str, Any]] = []
    comparisons: list[dict[str, Any]] = []

    for direction, job_name in args.job:
        oracle = oracle_frames.get(direction)
        if oracle is None:
            issues.append(
                {
                    "severity": "error",
                    "kind": "missing_oracle_direction",
                    "direction": direction,
                    "job_name": job_name,
                }
            )
            continue

        image_path = find_review_image(project_root, job_name)
        if image_path is None:
            issues.append(
                {
                    "severity": "error",
                    "kind": "missing_generated_images",
                    "direction": direction,
                    "job_name": job_name,
                    "searched": [f"Assets/Generated/_Review/{job_name}/{name}" for name in REVIEW_IMAGE_NAMES]
                    + [f"Assets/Generated/_Review/{job_name}/generation_manifest.json generated_files"],
                }
            )
            continue

        generated_metrics = image_stats_from_path(image_path)
        oracle_path = repo_path(project_root, oracle["source_image"])
        if oracle_path is None:
            raise FileNotFoundError(f"Oracle source image is not resolvable for {direction}")
        delta = metric_delta(
            generated_metrics,
            oracle["metrics"],
            average_rgb_distance(image_path, oracle_path, oracle),
        )
        image_rel = rel_path(project_root, image_path)
        warnings = warning_notes(direction, job_name, image_rel, delta)
        issues.extend(warnings)
        if generated_metrics["alpha_bbox"] is None:
            issues.append(
                {
                    "severity": "error",
                    "kind": "blank_alpha",
                    "direction": direction,
                    "job_name": job_name,
                    "image": image_rel,
                }
            )

        comparisons.append(
            {
                "direction": direction,
                "job_name": job_name,
                "generated_image": image_rel,
                "oracle_source_image": oracle["source_image"],
                "metrics": {
                    "generated": generated_metrics,
                    "oracle": oracle["metrics"],
                    "delta": delta,
                },
                "warnings": warnings,
            }
        )

    ranking = ranking_for(comparisons)
    best_by_direction = {}
    for candidate in ranking:
        best_by_direction.setdefault(candidate["direction"], candidate)

    payload = {
        "schema": "lit_iso.asset_forge.direction_oracle_qa.v1",
        "generated_utc": utc_now(),
        "project_root": str(project_root),
        "oracle_manifest": rel_path(project_root, oracle_manifest_path),
        "oracle_schema": oracle_manifest.get("schema"),
        "directions": directions,
        "thresholds": {
            "alpha_threshold": ALPHA_THRESHOLD,
            "bbox_size_warning_px": BBOX_SIZE_WARNING_PX,
            "centroid_warning_px": CENTROID_WARNING_PX,
            "coverage_warning_abs": COVERAGE_WARNING_ABS,
            "average_rgb_distance_warning": RGB_DISTANCE_WARNING,
            "review_image_preference": REVIEW_IMAGE_NAMES,
        },
        "summary": {
            "direction_count": len(comparisons),
            "warning_count": sum(1 for issue in issues if issue["severity"] == "warning"),
            "error_count": sum(1 for issue in issues if issue["severity"] == "error"),
        },
        "ranking": ranking,
        "best_by_direction": best_by_direction,
        "per_direction": comparisons,
        "issues": issues,
    }

    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    print(json.dumps({"ok": True, "output": rel_path(project_root, output), **payload["summary"]}, indent=2))
    return 1 if payload["summary"]["error_count"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
