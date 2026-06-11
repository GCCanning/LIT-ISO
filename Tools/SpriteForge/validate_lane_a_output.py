#!/usr/bin/env python3
"""Validate one SpriteForge lane-A rendered output.

P4 adds a temporal stability metric: each frame is compared against frame 0
inside a fixed head/band crop, so face/hat-band drift is reported numerically.
"""
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


DEFAULT_HEAD_BAND_CROP = [18, 4, 28, 18]
DEFAULT_WARN_DELTA = 0.10
DEFAULT_FAIL_DELTA = 0.16


def now_utc() -> str:
    return datetime.now(timezone.utc).isoformat()


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def validate_frame(path: Path, target_size: int) -> list[str]:
    issues: list[str] = []
    if not path.exists():
        return [f"missing frame {path}"]
    with Image.open(path) as image:
        if image.size != (target_size, target_size):
            issues.append(f"{path}: expected {target_size}x{target_size}, got {image.width}x{image.height}")
        rgba = image.convert("RGBA")
        alpha = rgba.getchannel("A")
        if alpha.getbbox() is None:
            issues.append(f"{path}: empty transparent frame")
        for xy in [(0, 0), (image.width - 1, 0), (0, image.height - 1), (image.width - 1, image.height - 1)]:
            if alpha.getpixel(xy) != 0:
                issues.append(f"{path}: non-transparent corner at {xy}")
                break
    return issues


def mean_abs_delta(a: Image.Image, b: Image.Image) -> float:
    a_rgba = a.convert("RGBA")
    b_rgba = b.convert("RGBA")
    if a_rgba.size != b_rgba.size:
        raise ValueError(f"image size mismatch {a_rgba.size} vs {b_rgba.size}")
    ap = list(a_rgba.getdata())
    bp = list(b_rgba.getdata())
    total = 0
    for left, right in zip(ap, bp):
        total += sum(abs(int(left[channel]) - int(right[channel])) for channel in range(4))
    return total / float(len(ap) * 4 * 255)


def temporal_stability(frames_dir: Path, frame_count: int, crop: list[int], warn_delta: float, fail_delta: float) -> dict[str, Any]:
    x, y, w, h = crop
    frame0_path = frames_dir / "frame_000.png"
    if not frame0_path.exists():
        return {"status": "fail", "issues": ["missing frame_000.png for temporal stability"], "frames": []}
    with Image.open(frame0_path) as base_image:
        base = base_image.convert("RGBA").crop((x, y, x + w, y + h))
    frame_metrics: list[dict[str, Any]] = []
    issues: list[str] = []
    warnings: list[str] = []
    max_delta = 0.0
    for frame_index in range(frame_count):
        path = frames_dir / f"frame_{frame_index:03d}.png"
        if not path.exists():
            issues.append(f"missing frame_{frame_index:03d}.png for temporal stability")
            continue
        with Image.open(path) as frame_image:
            crop_image = frame_image.convert("RGBA").crop((x, y, x + w, y + h))
        delta = mean_abs_delta(base, crop_image)
        max_delta = max(max_delta, delta)
        status = "ok"
        if delta > fail_delta:
            status = "fail"
            issues.append(f"frame {frame_index} head/band delta {delta:.4f} exceeds fail {fail_delta:.4f}")
        elif delta > warn_delta:
            status = "warn"
            warnings.append(f"frame {frame_index} head/band delta {delta:.4f} exceeds warn {warn_delta:.4f}")
        frame_metrics.append({"frame_index": frame_index, "delta_vs_frame0": round(delta, 5), "status": status})
    return {
        "metric": "mean_abs_rgba_delta_vs_frame0",
        "crop_xywh": crop,
        "warn_delta": warn_delta,
        "fail_delta": fail_delta,
        "max_delta": round(max_delta, 5),
        "status": "fail" if issues else ("warn" if warnings else "pass"),
        "frames": frame_metrics,
        "issues": issues,
        "warnings": warnings,
    }


def resolve_action_json(project_root: Path, root: Path, manifest: dict[str, Any]) -> tuple[Path | None, dict[str, Any]]:
    rel_path = manifest.get("action_json")
    candidates: list[Path] = []
    if rel_path:
        rel_candidate = Path(str(rel_path))
        candidates.append(rel_candidate if rel_candidate.is_absolute() else project_root / rel_candidate)
    action = manifest.get("action")
    if action:
        candidates.append(project_root / "Tools" / "SpriteForge" / "poses" / str(action) / "action.json")
    candidates.append(root.parents[1] / "action.json")
    for candidate in candidates:
        if candidate.exists():
            return candidate, read_json(candidate)
    return None, {}


def validate_output(
    root: Path,
    project_root: Path,
    target_size: int,
    crop: list[int],
    warn_delta: float,
    fail_delta: float,
) -> dict[str, Any]:
    issues: list[str] = []
    warnings: list[str] = []
    manifest_path = root / "lane_a_manifest.json"
    sheet_json_path = root / "sheet.json"
    manifest = read_json(manifest_path) if manifest_path.exists() else {}
    if not manifest:
        issues.append(f"missing {manifest_path}")
    elif manifest.get("status") != "complete":
        issues.append(f"lane_a_manifest status is {manifest.get('status')}, expected complete")

    sheet_json = read_json(sheet_json_path) if sheet_json_path.exists() else {}
    action_json_path, action_json = resolve_action_json(project_root, root, manifest)
    expected_frames = int(action_json.get("frames", sheet_json.get("frames", 0) or 0))
    expected_loop_start = int(action_json.get("loop_start", sheet_json.get("loop_start", 0) or 0))
    expected_loop_end = int(action_json.get("loop_end", sheet_json.get("loop_end", max(0, expected_frames - 1)) or max(0, expected_frames - 1)))
    expected_loop_range = action_json.get("loop_range", [expected_loop_start, expected_loop_end])
    if not sheet_json:
        issues.append(f"missing {sheet_json_path}")
    else:
        if sheet_json.get("loop_start") != expected_loop_start:
            issues.append(f"sheet.json loop_start expected {expected_loop_start}, got {sheet_json.get('loop_start')}")
        if sheet_json.get("loop_range") != expected_loop_range:
            issues.append(f"sheet.json loop_range expected {expected_loop_range}, got {sheet_json.get('loop_range')}")
        if sheet_json.get("frames") != expected_frames:
            issues.append(f"sheet.json frames expected {expected_frames}, got {sheet_json.get('frames')}")

    frames_dir = root / "frames"
    for frame_index in range(expected_frames):
        issues.extend(validate_frame(frames_dir / f"frame_{frame_index:03d}.png", target_size))

    for name in ["sheet.png", "preview.png", "preview_x4.png"]:
        if not (root / name).exists():
            issues.append(f"missing {name}")

    if sheet_json and "cell" in sheet_json:
        cell = sheet_json["cell"]
        if cell[0] > target_size or cell[1] > target_size:
            warnings.append(f"packed cell {cell} exceeds target frame size; this may be okay if content is tightly cropped")
    stability = temporal_stability(frames_dir, expected_frames, crop, warn_delta, fail_delta) if expected_frames else {}
    issues.extend(stability.get("issues", []))
    warnings.extend(stability.get("warnings", []))

    return {
        "schema": "lit-iso.spriteforge.lane-a-gate-report.v2",
        "checked_utc": now_utc(),
        "root": str(root),
        "status": "pass" if not issues else "fail",
        "issues": issues,
        "warnings": warnings,
        "manifest": str(manifest_path),
        "sheet_json": str(sheet_json_path),
        "action_json": str(action_json_path) if action_json_path else "",
        "expected_frames": expected_frames,
        "temporal_stability": stability,
        "review_preview": str(root / "preview_x4.png"),
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Validate one SpriteForge lane-A rendered output.")
    parser.add_argument("--project-root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--character", default="witch")
    parser.add_argument("--action", default="walk")
    parser.add_argument("--direction", default="S")
    parser.add_argument("--target-size", type=int, default=64)
    parser.add_argument("--root", type=Path, default=None)
    parser.add_argument("--head-band-crop", default=",".join(str(value) for value in DEFAULT_HEAD_BAND_CROP))
    parser.add_argument("--warn-delta", type=float, default=DEFAULT_WARN_DELTA)
    parser.add_argument("--fail-delta", type=float, default=DEFAULT_FAIL_DELTA)
    parser.add_argument("--report-name", default="p2_gate_report.json")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    root = args.root or (project_root / "Tools" / "SpriteForge" / "out" / args.character / args.action / args.direction)
    crop = [int(part.strip()) for part in args.head_band_crop.split(",")]
    if len(crop) != 4:
        raise ValueError("--head-band-crop must be x,y,w,h")
    report = validate_output(root.resolve(), project_root, args.target_size, crop, args.warn_delta, args.fail_delta)
    report_path = root / args.report_name
    report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({"ok": report["status"] == "pass", "status": report["status"], "report": str(report_path), "issues": report["issues"], "warnings": report["warnings"]}, indent=2))
    return 0 if report["status"] == "pass" else 1


if __name__ == "__main__":
    raise SystemExit(main())
