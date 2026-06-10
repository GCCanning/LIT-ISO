#!/usr/bin/env python3
"""Validate one SpriteForge lane-A rendered output at the P2 gate."""
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


def validate_output(root: Path, target_size: int) -> dict[str, Any]:
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
    if not sheet_json:
        issues.append(f"missing {sheet_json_path}")
    else:
        if sheet_json.get("loop_start") != 1:
            issues.append(f"sheet.json loop_start expected 1 for walk anchor skip, got {sheet_json.get('loop_start')}")
        if sheet_json.get("loop_range") != [1, 5]:
            issues.append(f"sheet.json loop_range expected [1, 5], got {sheet_json.get('loop_range')}")
        if sheet_json.get("frames") != 6:
            issues.append(f"sheet.json frames expected 6, got {sheet_json.get('frames')}")

    frames_dir = root / "frames"
    for frame_index in range(6):
        issues.extend(validate_frame(frames_dir / f"frame_{frame_index:03d}.png", target_size))

    for name in ["sheet.png", "preview.png", "preview_x4.png"]:
        if not (root / name).exists():
            issues.append(f"missing {name}")

    if sheet_json and "cell" in sheet_json:
        cell = sheet_json["cell"]
        if cell[0] > target_size or cell[1] > target_size:
            warnings.append(f"packed cell {cell} exceeds target frame size; this may be okay if content is tightly cropped")

    return {
        "schema": "lit-iso.spriteforge.p2-gate-report.v1",
        "checked_utc": now_utc(),
        "root": str(root),
        "status": "pass" if not issues else "fail",
        "issues": issues,
        "warnings": warnings,
        "manifest": str(manifest_path),
        "sheet_json": str(sheet_json_path),
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
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    root = args.root or (project_root / "Tools" / "SpriteForge" / "out" / args.character / args.action / args.direction)
    report = validate_output(root.resolve(), args.target_size)
    report_path = root / "p2_gate_report.json"
    report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({"ok": report["status"] == "pass", "status": report["status"], "report": str(report_path), "issues": report["issues"], "warnings": report["warnings"]}, indent=2))
    return 0 if report["status"] == "pass" else 1


if __name__ == "__main__":
    raise SystemExit(main())
