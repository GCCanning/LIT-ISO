#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


DIRECTIONS = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"]


def now_utc() -> str:
    return datetime.now(timezone.utc).isoformat()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            digest.update(chunk)
    return digest.hexdigest()


def validate_frame(path: Path) -> list[str]:
    issues = []
    if not path.exists():
        return [f"missing {path}"]
    with Image.open(path) as image:
        if image.size != (512, 512):
            issues.append(f"{path}: expected 512x512, got {image.size[0]}x{image.size[1]}")
        if image.mode != "RGBA":
            issues.append(f"{path}: expected RGBA, got {image.mode}")
        rgba = image.convert("RGBA")
        alpha = rgba.getchannel("A")
        bbox = alpha.getbbox()
        if bbox is None:
            issues.append(f"{path}: empty transparent frame")
        else:
            coverage = sum(alpha.histogram()[1:]) / float(512 * 512)
            if coverage < 0.001:
                issues.append(f"{path}: foreground coverage too low ({coverage:.5f})")
        for xy in [(0, 0), (511, 0), (0, 511), (511, 511)]:
            if alpha.getpixel(xy) != 0:
                issues.append(f"{path}: non-transparent corner at {xy}")
                break
    return issues


def validate_library(poses_root: Path) -> dict:
    issues: list[str] = []
    warnings: list[str] = []
    version_path = poses_root / "VERSION"
    version = version_path.read_text(encoding="utf-8").strip() if version_path.exists() else ""
    if not version:
        issues.append("missing poses/VERSION")
    if version == "0.1.0-scaffold":
        issues.append("poses/VERSION was not bumped from scaffold")

    action_reports = []
    idle_anchor_hashes: dict[str, str] = {}
    manifest_path = poses_root / "pose_library_manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8")) if manifest_path.exists() else {}
    manifest_actions = manifest.get("actions", []) if isinstance(manifest.get("actions", []), list) else []
    expected = {str(item.get("action")): int(item.get("frames", 0)) for item in manifest_actions if item.get("action")}
    if not expected:
        expected = {"idle": 4, "walk": 6}

    for action, frame_count in expected.items():
        action_root = poses_root / action
        action_json_path = action_root / "action.json"
        if not action_json_path.exists():
            issues.append(f"missing {action}/action.json")
            action_json = {}
        else:
            action_json = json.loads(action_json_path.read_text(encoding="utf-8"))
            if action_json.get("frames") != frame_count:
                issues.append(f"{action}/action.json frames expected {frame_count}, got {action_json.get('frames')}")
            if action_json.get("directions") != DIRECTIONS:
                issues.append(f"{action}/action.json directions are not canonical")
            if action_json.get("anchor_frame") != 0:
                issues.append(f"{action}/action.json anchor_frame must be 0")

        for direction in DIRECTIONS:
            direction_root = action_root / direction
            if not direction_root.exists():
                issues.append(f"missing {action}/{direction}")
                continue
            for index in range(frame_count):
                png_path = direction_root / f"frame_{index:03d}.png"
                issues.extend(validate_frame(png_path))
                if png_path.exists():
                    digest = sha256_file(png_path)
                    if action == "idle" and index == 0:
                        idle_anchor_hashes[direction] = digest
                    if action != "idle" and index == 0:
                        expected_digest = idle_anchor_hashes.get(direction)
                        if expected_digest and digest != expected_digest:
                            issues.append(f"{action}/{direction}/frame_{index:03d}.png does not match idle anchor")

            extras = sorted(direction_root.glob("frame_*.png"))
            if len(extras) != frame_count:
                issues.append(f"{action}/{direction}: expected {frame_count} png frames, found {len(extras)}")

        contact_sheet = action_root / f"{action}_pose_contact_sheet.png"
        if not contact_sheet.exists():
            issues.append(f"missing {action} contact sheet")
        action_reports.append(
            {
                "action": action,
                "expected_frames": frame_count,
                "directions": DIRECTIONS,
                "action_json": str(action_json_path),
                "contact_sheet": str(contact_sheet),
            }
        )

    if not manifest_path.exists():
        issues.append("missing pose_library_manifest.json")
    else:
        if manifest.get("version") != version:
            issues.append("pose_library_manifest.json version does not match VERSION")

    return {
        "schema": "lit-iso.spriteforge.p1-gate-report.v1",
        "checked_utc": now_utc(),
        "poses_root": str(poses_root),
        "version": version,
        "status": "pass" if not issues else "fail",
        "issues": issues,
        "warnings": warnings,
        "actions": action_reports,
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Validate SpriteForge P1 action pose library.")
    parser.add_argument("--poses-root", type=Path, default=Path(__file__).resolve().parent / "poses")
    parser.add_argument("--report", type=Path, default=None)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    poses_root = args.poses_root.resolve()
    report = validate_library(poses_root)
    if args.report:
        report_path = args.report if args.report.is_absolute() else Path.cwd() / args.report
    else:
        report_path = poses_root / "p1_gate_report.json"
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({"ok": report["status"] == "pass", "status": report["status"], "report": str(report_path), "issues": report["issues"]}, indent=2))
    return 0 if report["status"] == "pass" else 1


if __name__ == "__main__":
    raise SystemExit(main())
