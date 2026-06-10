#!/usr/bin/env python3
"""Run SpriteForge lane B review path.

Lane B is designed for pose-driven video generation. The live ComfyUI submit is
intentionally gated until the WanVideoWrapper workflow and model files are
actually present. P3 still proves the critical downstream path:

  video or extracted frames -> deterministic cleanup/normalization
  -> SpriteForge packer -> review artifacts

This keeps the gate honest while making the future live video render a narrow
swap at the "source frames" boundary.
"""
from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Any

from PIL import Image

from check_lane_b_stack import build_report, now_utc
from run_lane_a_animation import (
    hard_alpha,
    lock_to_palette,
    make_review_scale,
    normalize_frame,
    palette_from_frame,
    read_json,
    rel,
    run_command,
    write_json,
)

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


def find_frame_paths(frames_dir: Path) -> list[Path]:
    paths = sorted(frames_dir.glob("frame_*.png"))
    if not paths:
        paths = sorted(frames_dir.glob("*.png"))
    return paths


def extract_video_frames(video_path: Path, target_dir: Path, frame_count: int) -> list[Path]:
    try:
        import cv2  # type: ignore
    except Exception as exc:  # pragma: no cover - environment dependent
        raise RuntimeError("opencv-python/cv2 is required for --input-video extraction") from exc

    cap = cv2.VideoCapture(str(video_path))
    if not cap.isOpened():
        raise RuntimeError(f"could not open video: {video_path}")
    total = int(cap.get(cv2.CAP_PROP_FRAME_COUNT)) or frame_count
    indices = [round(i * max(0, total - 1) / max(1, frame_count - 1)) for i in range(frame_count)]
    target_dir.mkdir(parents=True, exist_ok=True)
    written: list[Path] = []
    for out_index, source_index in enumerate(indices):
        cap.set(cv2.CAP_PROP_POS_FRAMES, source_index)
        ok, frame = cap.read()
        if not ok:
            break
        rgba = cv2.cvtColor(frame, cv2.COLOR_BGR2RGBA)
        image = Image.fromarray(rgba)
        target = target_dir / f"frame_{out_index:03d}.png"
        image.save(target)
        written.append(target)
    cap.release()
    if len(written) != frame_count:
        raise RuntimeError(f"expected {frame_count} extracted frames, got {len(written)}")
    return written


def copy_or_extract_source_frames(args: argparse.Namespace, out_dir: Path, frame_count: int) -> tuple[list[Path], str]:
    source_dir = out_dir / "source_frames"
    if source_dir.exists():
        shutil.rmtree(source_dir)
    source_dir.mkdir(parents=True, exist_ok=True)

    if args.input_frames:
        source = args.input_frames if args.input_frames.is_absolute() else args.project_root / args.input_frames
        paths = find_frame_paths(source)
        if len(paths) < frame_count:
            raise RuntimeError(f"{source}: expected at least {frame_count} frames, found {len(paths)}")
        copied: list[Path] = []
        for index, path in enumerate(paths[:frame_count]):
            target = source_dir / f"frame_{index:03d}.png"
            shutil.copy2(path, target)
            copied.append(target)
        return copied, "input_frames"

    if args.input_video:
        source = args.input_video if args.input_video.is_absolute() else args.project_root / args.input_video
        return extract_video_frames(source, source_dir, frame_count), "input_video"

    return [], "live_comfy_video"


def process_frames(source_frames: list[Path], frames_dir: Path, target_size: int, palette_lock: bool) -> list[dict[str, Any]]:
    if frames_dir.exists():
        shutil.rmtree(frames_dir)
    frames_dir.mkdir(parents=True, exist_ok=True)
    palette = None
    results: list[dict[str, Any]] = []
    for index, source in enumerate(source_frames):
        target = frames_dir / f"frame_{index:03d}.png"
        if index == 0:
            result = normalize_frame(source, target, target_size)
            if palette_lock:
                palette = palette_from_frame(Image.open(target).convert("RGBA"))
        else:
            result = normalize_frame(source, target, target_size, palette=palette if palette_lock else None)
        if palette_lock and palette is not None:
            image = hard_alpha(Image.open(target).convert("RGBA"))
            locked = lock_to_palette(image, palette)
            locked.save(target)
        result["frame_index"] = index
        results.append(result)
    return results


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run SpriteForge lane B video-frame cleanup/packing path.")
    parser.add_argument("--project-root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--comfy-root", type=Path, default=Path("C:/Projects/ComfyUI"))
    parser.add_argument("--comfy-url", default="http://127.0.0.1:8188")
    parser.add_argument("--character", default="witch")
    parser.add_argument("--action", default="walk")
    parser.add_argument("--direction", default="S")
    parser.add_argument("--target-size", type=int, default=64)
    parser.add_argument("--out-root", type=Path, default=Path(__file__).resolve().parent / "out" / "lane_b")
    parser.add_argument("--input-frames", type=Path, default=None)
    parser.add_argument("--input-video", type=Path, default=None)
    parser.add_argument("--palette-lock", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--dry-run", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    args.project_root = args.project_root.resolve()
    out_root = args.out_root if args.out_root.is_absolute() else args.project_root / args.out_root
    out_dir = out_root / args.character / args.action / args.direction
    out_dir.mkdir(parents=True, exist_ok=True)

    stack_report_path = out_root / "p3_stack_report.json"
    stack_report = build_report(args.project_root, args.comfy_root.resolve(), args.comfy_url, stack_report_path)

    action_json_path = args.project_root / "Tools" / "SpriteForge" / "poses" / args.action / "action.json"
    action_json = read_json(action_json_path)
    frame_count = int(action_json.get("frames", 0))
    if frame_count <= 0:
        raise RuntimeError(f"invalid frame count in {action_json_path}")

    source_kind = "none"
    frame_results: list[dict[str, Any]] = []
    pack_result: dict[str, Any] = {}
    errors: list[str] = []
    status = "planned" if args.dry_run else "blocked_live_generation"

    try:
        source_frames, source_kind = copy_or_extract_source_frames(args, out_dir, frame_count)
        if source_frames:
            frame_results = process_frames(source_frames, out_dir / "frames", args.target_size, args.palette_lock)
            pack_cmd = [
                sys.executable,
                str(args.project_root / "Tools" / "SpriteForge" / "spriteforge_pack.py"),
                "--frames", str(out_dir / "frames"),
                "--out", str(out_dir),
                "--action-json", str(action_json_path),
                "--character", args.character,
                "--action", args.action,
                "--direction", args.direction,
            ]
            packed = run_command(pack_cmd, args.project_root)
            if packed.returncode != 0:
                errors.append(packed.stderr.strip() or packed.stdout.strip() or "spriteforge_pack failed")
                pack_result = {"returncode": packed.returncode, "stdout": packed.stdout, "stderr": packed.stderr}
                status = "failed"
            else:
                pack_result = json.loads(packed.stdout)
                make_review_scale(out_dir / "preview.png", out_dir / "preview_x4.png", 4)
                make_review_scale(out_dir / "sheet.png", out_dir / "sheet_x4.png", 4)
                status = "complete_from_source_frames"
        elif not args.dry_run:
            errors.append(
                "live ComfyUI Lane B generation is not submitted yet; provide --input-video or --input-frames, "
                "or install/restart/models until p3_stack_report is ready and bind the workflow template."
            )
    except Exception as exc:
        errors.append(str(exc))
        status = "failed"

    manifest = {
        "schema": "lit-iso.spriteforge.lane-b-run.v1",
        "created_utc": now_utc(),
        "status": status,
        "character": args.character,
        "action": args.action,
        "direction": args.direction,
        "target_size": args.target_size,
        "source_kind": source_kind,
        "action_json": rel(args.project_root, action_json_path),
        "stack_report": rel(args.project_root, stack_report_path),
        "workflow": rel(args.project_root, args.project_root / "Tools" / "SpriteForge" / "workflows" / "one_to_all_pose_i2v.json"),
        "fallback_workflow": rel(args.project_root, args.project_root / "Tools" / "SpriteForge" / "workflows" / "wan22_i2v_pose.json"),
        "stack_status": stack_report["status"],
        "loop_start": action_json.get("loop_start", 0),
        "loop_end": action_json.get("loop_end", frame_count - 1),
        "loop_range": action_json.get("loop_range", [action_json.get("loop_start", 0), action_json.get("loop_end", frame_count - 1)]),
        "palette_lock": args.palette_lock,
        "frames": frame_results,
        "pack": pack_result,
        "review": {
            "frames_dir": rel(args.project_root, out_dir / "frames"),
            "sheet": rel(args.project_root, out_dir / "sheet.png"),
            "sheet_x4": rel(args.project_root, out_dir / "sheet_x4.png"),
            "preview": rel(args.project_root, out_dir / "preview.png"),
            "preview_x4": rel(args.project_root, out_dir / "preview_x4.png"),
            "sheet_json": rel(args.project_root, out_dir / "sheet.json"),
        },
        "errors": errors,
    }
    manifest_path = out_dir / "lane_b_manifest.json"
    write_json(manifest_path, manifest)
    print(json.dumps({"ok": not errors, "status": status, "manifest": str(manifest_path)}, indent=2))
    return 0 if not errors else 1


if __name__ == "__main__":
    raise SystemExit(main())
