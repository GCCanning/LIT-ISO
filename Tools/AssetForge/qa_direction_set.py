#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import math
import statistics
import sys
from collections import Counter
from pathlib import Path
from typing import Any

from PIL import Image

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


ALPHA_THRESHOLD = 8


def repo_path(project_root: Path, value: str) -> Path:
    path = Path(value.replace("/", "\\"))
    return path if path.is_absolute() else project_root / path


def rel_path(project_root: Path, path: Path) -> str:
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


def load_review_image(project_root: Path, job_name: str) -> tuple[Path, Path, dict[str, Any]]:
    manifest_path = project_root / "Assets" / "Generated" / "_Review" / job_name / "generation_manifest.json"
    if not manifest_path.exists():
        raise FileNotFoundError(f"Missing review manifest for {job_name}: {manifest_path}")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    generated = manifest.get("generated_files") or manifest.get("copied_review_paths") or []
    if not generated:
        raise ValueError(f"No generated_files or copied_review_paths in {manifest_path}")
    return manifest_path, repo_path(project_root, generated[0]), manifest


def color_bucket(rgb: tuple[int, int, int]) -> str:
    return "#{:01x}{:01x}{:01x}".format(rgb[0] >> 4, rgb[1] >> 4, rgb[2] >> 4)


def image_metrics(image_path: Path) -> dict[str, Any]:
    with Image.open(image_path) as source:
        image = source.convert("RGBA")

    width, height = image.size
    pixels = image.load()
    min_x = width
    min_y = height
    max_x = -1
    max_y = -1
    alpha_sum = 0
    weighted_x = 0.0
    weighted_y = 0.0
    opaque_pixels = 0
    buckets: Counter[str] = Counter()

    for y in range(height):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            if a <= ALPHA_THRESHOLD:
                continue
            opaque_pixels += 1
            alpha_sum += a
            weighted_x += x * a
            weighted_y += y * a
            min_x = min(min_x, x)
            min_y = min(min_y, y)
            max_x = max(max_x, x)
            max_y = max(max_y, y)
            buckets[color_bucket((r, g, b))] += 1

    if opaque_pixels == 0:
        return {
            "image_size": {"width": width, "height": height},
            "alpha_bbox": None,
            "alpha_coverage": 0.0,
            "centroid": None,
            "size": {"width": 0, "height": 0},
            "color_count_estimate": 0,
            "histogram_buckets": {},
        }

    bbox_width = max_x - min_x + 1
    bbox_height = max_y - min_y + 1
    return {
        "image_size": {"width": width, "height": height},
        "alpha_bbox": {"x": min_x, "y": min_y, "width": bbox_width, "height": bbox_height},
        "alpha_coverage": round(opaque_pixels / float(width * height), 6),
        "centroid": {
            "x": round(weighted_x / alpha_sum, 3),
            "y": round(weighted_y / alpha_sum, 3),
        },
        "size": {"width": bbox_width, "height": bbox_height},
        "color_count_estimate": len(buckets),
        "histogram_buckets": dict(buckets.most_common(16)),
    }


def number(metric: dict[str, Any], *path: str) -> float | None:
    value: Any = metric
    for key in path:
        if value is None:
            return None
        value = value.get(key)
    return float(value) if value is not None else None


def distance(a: dict[str, Any], b: dict[str, Any], group: str) -> float | None:
    ax = number(a, group, "x")
    ay = number(a, group, "y")
    bx = number(b, group, "x")
    by = number(b, group, "y")
    if ax is None or ay is None or bx is None or by is None:
        return None
    return math.hypot(ax - bx, ay - by)


def compare_adjacent(frames: list[dict[str, Any]]) -> list[dict[str, Any]]:
    comparisons = []
    for left, right in zip(frames, frames[1:]):
        left_metrics = left["metrics"]
        right_metrics = right["metrics"]
        comparisons.append(
            {
                "from": left["direction"],
                "to": right["direction"],
                "centroid_distance": rounded(distance(left_metrics, right_metrics, "centroid")),
                "bbox_origin_distance": rounded(distance(left_metrics, right_metrics, "alpha_bbox")),
                "alpha_coverage_delta": rounded(
                    abs(left_metrics["alpha_coverage"] - right_metrics["alpha_coverage"])
                ),
                "width_delta": abs(left_metrics["size"]["width"] - right_metrics["size"]["width"]),
                "height_delta": abs(left_metrics["size"]["height"] - right_metrics["size"]["height"]),
                "color_count_delta": abs(
                    left_metrics["color_count_estimate"] - right_metrics["color_count_estimate"]
                ),
            }
        )
    return comparisons


def rounded(value: float | None) -> float | None:
    return round(value, 3) if value is not None else None


def outlier_notes(frames: list[dict[str, Any]]) -> list[dict[str, Any]]:
    tracked = [
        ("alpha_coverage", ("alpha_coverage",), 0.35),
        ("bbox_width", ("size", "width"), 0.25),
        ("bbox_height", ("size", "height"), 0.25),
        ("centroid_x", ("centroid", "x"), 0.18),
        ("centroid_y", ("centroid", "y"), 0.18),
        ("color_count_estimate", ("color_count_estimate",), 0.45),
    ]
    notes = []
    for label, path, threshold in tracked:
        values = [number(frame["metrics"], *path) for frame in frames]
        numeric = [value for value in values if value is not None]
        if len(numeric) < 3:
            continue
        median = statistics.median(numeric)
        if median == 0:
            continue
        for frame, value in zip(frames, values):
            if value is None:
                continue
            ratio = abs(value - median) / abs(median)
            if ratio > threshold:
                notes.append(
                    {
                        "severity": "warning",
                        "direction": frame["direction"],
                        "job_name": frame["job_name"],
                        "metric": label,
                        "value": rounded(value),
                        "median": rounded(median),
                        "relative_delta": rounded(ratio),
                    }
                )
    return notes


def drift_notes(comparisons: list[dict[str, Any]]) -> list[dict[str, Any]]:
    notes = []
    for comparison in comparisons:
        if comparison["centroid_distance"] is not None and comparison["centroid_distance"] > 18:
            notes.append({"severity": "warning", "kind": "centroid_drift", **comparison})
        if comparison["bbox_origin_distance"] is not None and comparison["bbox_origin_distance"] > 18:
            notes.append({"severity": "warning", "kind": "bbox_drift", **comparison})
        if comparison["alpha_coverage_delta"] is not None and comparison["alpha_coverage_delta"] > 0.12:
            notes.append({"severity": "warning", "kind": "coverage_drift", **comparison})
        if comparison["width_delta"] > 32 or comparison["height_delta"] > 32:
            notes.append({"severity": "warning", "kind": "size_drift", **comparison})
    return notes


def parse_expected_directions(value: str | None) -> list[str]:
    if not value:
        return []
    return [part.strip() for part in value.split(",") if part.strip()]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Measure generated direction-set review frames and report drift/outliers."
    )
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--job", action="append", required=True, type=parse_job, help="Direction=JobName")
    parser.add_argument("--expected-directions", help="Comma-separated direction order, for example S,E,N,W")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    output = args.output if args.output.is_absolute() else project_root / args.output
    expected = parse_expected_directions(args.expected_directions)
    frames = []
    issues = []

    directions = [direction for direction, _ in args.job]
    if expected and directions != expected:
        issues.append(
            {
                "severity": "warning",
                "kind": "direction_order_mismatch",
                "expected": expected,
                "actual": directions,
            }
        )

    for index, (direction, job_name) in enumerate(args.job):
        manifest_path, image_path, manifest = load_review_image(project_root, job_name)
        if not image_path.exists():
            raise FileNotFoundError(f"Generated image for {job_name} does not exist: {image_path}")
        metrics = image_metrics(image_path)
        if metrics["alpha_bbox"] is None:
            issues.append(
                {
                    "severity": "error",
                    "kind": "blank_alpha",
                    "direction": direction,
                    "job_name": job_name,
                    "image": rel_path(project_root, image_path),
                }
            )
        frames.append(
            {
                "index": index,
                "direction": direction,
                "job_name": job_name,
                "manifest": rel_path(project_root, manifest_path),
                "source_image": rel_path(project_root, image_path),
                "provider": manifest.get("provider"),
                "asset_mode": manifest.get("asset_mode"),
                "metrics": metrics,
            }
        )

    adjacent = compare_adjacent(frames)
    issues.extend(drift_notes(adjacent))
    issues.extend(outlier_notes(frames))

    payload = {
        "schema": "lit_iso.asset_forge.direction_set_qa.v1",
        "project_root": str(project_root),
        "expected_directions": expected,
        "directions": directions,
        "summary": {
            "frame_count": len(frames),
            "warning_count": sum(1 for issue in issues if issue["severity"] == "warning"),
            "error_count": sum(1 for issue in issues if issue["severity"] == "error"),
        },
        "frames": frames,
        "adjacent_comparisons": adjacent,
        "issues": issues,
    }

    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    print(json.dumps({"ok": True, "output": rel_path(project_root, output), **payload["summary"]}, indent=2))
    return 1 if payload["summary"]["error_count"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
