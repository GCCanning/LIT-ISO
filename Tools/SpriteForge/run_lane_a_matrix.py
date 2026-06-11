#!/usr/bin/env python3
"""Run SpriteForge lane A across an action/direction matrix.

P4 uses this as the review-gate runner. It is deliberately conservative:

- outputs stay under Tools/SpriteForge/out/p4_matrix by default;
- mirrorable directions are honored from action.json unless --no-mirror is set;
- every generated or mirrored job gets validate_lane_a_output.py run against it;
- action-level contact sheets and a p4_gate_report.json are written for review.
"""
from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


DIRECTIONS = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"]
DEFAULT_ACTIONS = ["idle", "walk", "run", "attack_swing", "cast", "hurt", "death"]


def now_utc() -> str:
    return datetime.now(timezone.utc).isoformat()


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, data: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2), encoding="utf-8")


def default_python() -> str:
    comfy_python = Path("C:/Projects/ComfyUI/.venv/Scripts/python.exe")
    return str(comfy_python) if comfy_python.exists() else sys.executable


def run(cmd: list[str], cwd: Path) -> subprocess.CompletedProcess[str]:
    return subprocess.run(cmd, cwd=str(cwd), text=True, capture_output=True)


def action_json_path(project_root: Path, action: str) -> Path:
    return project_root / "Tools" / "SpriteForge" / "poses" / action / "action.json"


def mirror_frame(source: Path, target: Path) -> None:
    with Image.open(source) as image:
        mirrored = image.convert("RGBA").transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    target.parent.mkdir(parents=True, exist_ok=True)
    mirrored.save(target)


def make_review_scale(source: Path, target: Path, scale: int = 4) -> None:
    with Image.open(source) as image:
        upscaled = image.convert("RGBA").resize((image.width * scale, image.height * scale), Image.Resampling.NEAREST)
    target.parent.mkdir(parents=True, exist_ok=True)
    upscaled.save(target)


def mirror_job(project_root: Path, python_exe: str, source_root: Path, target_root: Path, character: str, action: str, direction: str, source_direction: str) -> dict[str, Any]:
    frames_dir = target_root / "frames"
    if target_root.exists():
        shutil.rmtree(target_root)
    source_frames = sorted((source_root / "frames").glob("frame_*.png"))
    if not source_frames:
        raise RuntimeError(f"missing source frames for mirror: {source_root / 'frames'}")
    for source in source_frames:
        mirror_frame(source, frames_dir / source.name)
    pack_cmd = [
        python_exe,
        str(project_root / "Tools" / "SpriteForge" / "spriteforge_pack.py"),
        "--frames", str(frames_dir),
        "--out", str(target_root),
        "--action-json", str(action_json_path(project_root, action)),
        "--character", character,
        "--action", action,
        "--direction", direction,
    ]
    packed = run(pack_cmd, project_root)
    if packed.returncode != 0:
        raise RuntimeError(packed.stderr.strip() or packed.stdout.strip() or f"pack failed for mirrored {action}/{direction}")
    make_review_scale(target_root / "preview.png", target_root / "preview_x4.png", 4)
    make_review_scale(target_root / "sheet.png", target_root / "sheet_x4.png", 4)
    source_manifest_path = source_root / "lane_a_manifest.json"
    source_manifest = read_json(source_manifest_path) if source_manifest_path.exists() else {}
    manifest = {
        **source_manifest,
        "schema": "lit-iso.spriteforge.lane-a-run.v1",
        "status": "complete",
        "created_utc": now_utc(),
        "action": action,
        "direction": direction,
        "generation_method": "mirrored_from_lane_a",
        "mirrored_from": source_direction,
        "frames": [
            {**frame, "mirrored_from": source_direction, "source_direction": source_direction}
            for frame in source_manifest.get("frames", [])
        ],
        "review": {
            "frames_dir": str(frames_dir),
            "sheet": str(target_root / "sheet.png"),
            "sheet_x4": str(target_root / "sheet_x4.png"),
            "preview": str(target_root / "preview.png"),
            "preview_x4": str(target_root / "preview_x4.png"),
            "sheet_json": str(target_root / "sheet.json"),
        },
        "errors": [],
    }
    write_json(target_root / "lane_a_manifest.json", manifest)
    return {"status": "complete", "mirrored_from": source_direction, "manifest": str(target_root / "lane_a_manifest.json")}


def validate_job(project_root: Path, python_exe: str, root: Path, action: str, direction: str, target_size: int) -> dict[str, Any]:
    cmd = [
        python_exe,
        str(project_root / "Tools" / "SpriteForge" / "validate_lane_a_output.py"),
        "--project-root", str(project_root),
        "--action", action,
        "--direction", direction,
        "--target-size", str(target_size),
        "--root", str(root),
        "--report-name", "p4_gate_report.json",
    ]
    completed = run(cmd, project_root)
    report_path = root / "p4_gate_report.json"
    report = read_json(report_path) if report_path.exists() else {}
    return {
        "returncode": completed.returncode,
        "stdout": completed.stdout,
        "stderr": completed.stderr,
        "report": str(report_path),
        "status": report.get("status", "fail"),
        "issues": report.get("issues", []),
        "warnings": report.get("warnings", []),
        "temporal_stability": report.get("temporal_stability", {}),
    }


def existing_pass(root: Path) -> bool:
    manifest_path = root / "lane_a_manifest.json"
    report_path = root / "p4_gate_report.json"
    if not manifest_path.exists() or not report_path.exists():
        return False
    try:
        manifest = read_json(manifest_path)
        report = read_json(report_path)
    except (OSError, json.JSONDecodeError):
        return False
    return manifest.get("status") == "complete" and report.get("status") == "pass"


def build_action_contact_sheet(action_root: Path, action: str, directions: list[str], output: Path) -> None:
    thumbs: list[tuple[str, Image.Image]] = []
    for direction in directions:
        preview = action_root / direction / "preview_x4.png"
        if not preview.exists():
            preview = action_root / direction / "preview.png"
        if preview.exists():
            with Image.open(preview) as image:
                thumb = image.convert("RGBA")
                thumb.thumbnail((420, 96), Image.Resampling.NEAREST)
            thumbs.append((direction, thumb))
    if not thumbs:
        return
    label_w = 58
    row_h = 116
    sheet_w = label_w + max(thumb.width for _, thumb in thumbs) + 18
    sheet_h = row_h * len(thumbs) + 34
    sheet = Image.new("RGBA", (sheet_w, sheet_h), (16, 18, 24, 255))
    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("arial.ttf", 14)
        title_font = ImageFont.truetype("arial.ttf", 16)
    except OSError:
        font = ImageFont.load_default()
        title_font = ImageFont.load_default()
    draw.text((10, 8), action, fill=(238, 242, 248, 255), font=title_font)
    for row, (direction, thumb) in enumerate(thumbs):
        y = 30 + row * row_h
        draw.rectangle((0, y, sheet_w - 1, y + row_h - 1), outline=(59, 66, 84, 255), width=1)
        draw.text((12, y + 42), direction, fill=(230, 235, 242, 255), font=font)
        sheet.alpha_composite(thumb, (label_w, y + (row_h - thumb.height) // 2))
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output)


def parse_csv(value: str) -> list[str]:
    return [part.strip() for part in value.split(",") if part.strip()]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run SpriteForge lane-A P4 action/direction matrix.")
    parser.add_argument("--project-root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--character", default="witch")
    parser.add_argument("--character-ref", type=Path, default=Path("Assets/Characters/Witch/AnimationSprites/Static/witch static00.png"))
    parser.add_argument("--actions", default=",".join(DEFAULT_ACTIONS))
    parser.add_argument("--directions", default=",".join(DIRECTIONS))
    parser.add_argument("--target-size", type=int, default=64)
    parser.add_argument("--seed", default="1207")
    parser.add_argument("--out-root", type=Path, default=Path("Tools/SpriteForge/out/p4_matrix"))
    parser.add_argument("--python", default="")
    parser.add_argument("--template-denoise", type=float, default=0.38)
    parser.add_argument("--style-weight", type=float, default=0.72)
    parser.add_argument("--control-strength", type=float, default=0.62)
    parser.add_argument("--no-mirror", action="store_true")
    parser.add_argument("--clean", action="store_true", help="Delete the character matrix output root before running.")
    parser.add_argument("--dry-run", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    python_exe = args.python or default_python()
    actions = parse_csv(args.actions)
    directions = parse_csv(args.directions)
    out_root = args.out_root if args.out_root.is_absolute() else project_root / args.out_root
    matrix_root = out_root / args.character
    if args.clean and matrix_root.exists():
        shutil.rmtree(matrix_root)
    jobs: list[dict[str, Any]] = []
    failures: list[str] = []

    for action in actions:
        action_data = read_json(action_json_path(project_root, action))
        mirrorable = {} if args.no_mirror else dict(action_data.get("mirrorable", {}))
        generated_roots: dict[str, Path] = {
            direction: matrix_root / action / direction
            for direction in DIRECTIONS
            if existing_pass(matrix_root / action / direction)
        }
        for direction in directions:
            root = matrix_root / action / direction
            source_direction = mirrorable.get(direction)
            job: dict[str, Any] = {"action": action, "direction": direction, "root": str(root)}
            try:
                if not args.clean and existing_pass(root):
                    job.update({"status": "skipped_existing", "returncode": 0})
                    generated_roots[direction] = root
                elif source_direction and source_direction in generated_roots:
                    if args.dry_run:
                        job.update({"status": "planned_mirror", "mirrored_from": source_direction})
                    else:
                        job.update(mirror_job(project_root, python_exe, generated_roots[source_direction], root, args.character, action, direction, source_direction))
                else:
                    cmd = [
                        python_exe,
                        str(project_root / "Tools" / "SpriteForge" / "run_lane_a_animation.py"),
                        "--project-root", str(project_root),
                        "--character", args.character,
                        "--character-ref", str(args.character_ref),
                        "--action", action,
                        "--direction", direction,
                        "--target-size", str(args.target_size),
                        "--seed", args.seed,
                        "--out-root", str(out_root),
                        "--template-denoise", str(args.template_denoise),
                        "--style-weight", str(args.style_weight),
                        "--control-strength", str(args.control_strength),
                    ]
                    if args.dry_run:
                        cmd.append("--dry-run")
                    completed = run(cmd, project_root)
                    job.update({"status": "planned" if args.dry_run else "complete", "returncode": completed.returncode})
                    if completed.returncode != 0:
                        raise RuntimeError(completed.stderr.strip() or completed.stdout.strip() or f"lane A failed for {action}/{direction}")
                    generated_roots[direction] = root
                if not args.dry_run:
                    validation = validate_job(project_root, python_exe, root, action, direction, args.target_size)
                    job["validation"] = validation
                    if validation["status"] != "pass":
                        failures.append(f"{action}/{direction}: validation {validation['status']}")
            except Exception as exc:
                job["status"] = "failed"
                job["error"] = str(exc)
                failures.append(f"{action}/{direction}: {exc}")
            jobs.append(job)
        if not args.dry_run:
            build_action_contact_sheet(matrix_root / action, action, directions, matrix_root / action / f"{action}_directions_contact_sheet.png")

    report = {
        "schema": "lit-iso.spriteforge.p4-gate-report.v1",
        "created_utc": now_utc(),
        "status": "planned" if args.dry_run else ("pass" if not failures else "fail"),
        "character": args.character,
        "actions": actions,
        "directions": directions,
        "target_size": args.target_size,
        "defaults": {
            "lane_a_default": "d038_c062_bob",
            "template_denoise": args.template_denoise,
            "style_weight": args.style_weight,
            "control_strength": args.control_strength,
            "mirrorable_honored": not args.no_mirror,
        },
        "jobs": jobs,
        "failures": failures,
        "contact_sheets": [
            str(matrix_root / action / f"{action}_directions_contact_sheet.png")
            for action in actions
            if (matrix_root / action / f"{action}_directions_contact_sheet.png").exists()
        ],
    }
    report_path = matrix_root / "p4_gate_report.json"
    write_json(report_path, report)
    print(json.dumps({"ok": report["status"] in {"pass", "planned"}, "status": report["status"], "report": str(report_path), "failures": failures}, indent=2))
    return 0 if report["status"] in {"pass", "planned"} else 1


if __name__ == "__main__":
    raise SystemExit(main())
