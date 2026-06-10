#!/usr/bin/env python3
"""Run SpriteForge lane A for one action/direction.

Lane A is the frame-by-frame path:

  reference + style/IP-Adapter + pose ControlNet -> AssetForge worker
  -> SpriteForge 64px normalization -> spriteforge_pack.py

Generated outputs stay under Tools/SpriteForge/out and are review artifacts.
Nothing is imported into Unity by this script.
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


DIRECTION_WORD = {
    "S": "south",
    "SE": "south-east",
    "E": "east",
    "NE": "north-east",
    "N": "north",
    "NW": "north-west",
    "W": "west",
    "SW": "south-west",
}


def now_utc() -> str:
    return datetime.now(timezone.utc).isoformat()


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, data: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2), encoding="utf-8")


def rel(project_root: Path, path: Path) -> str:
    path = path.resolve()
    try:
        return path.relative_to(project_root).as_posix()
    except ValueError:
        return path.as_posix()


def default_python(project_root: Path) -> str:
    comfy_python = Path("C:/Projects/ComfyUI/.venv/Scripts/python.exe")
    if comfy_python.exists():
        return str(comfy_python)
    return sys.executable


def load_worker_defaults(project_root: Path, mode: str) -> dict[str, Any]:
    cfg_path = project_root / "Tools" / "AssetForge" / "asset_forge.local.json"
    if not cfg_path.exists():
        cfg_path = project_root / "Tools" / "AssetForge" / "asset_forge.local.example.json"
    cfg = read_json(cfg_path) if cfg_path.exists() else {}
    comfy = cfg.get("comfyui", {})
    worker = comfy.get("worker_defaults", {})
    mode_defaults = worker.get("mode_defaults", {}).get(mode, {})
    merged: dict[str, Any] = {}
    for key, value in worker.items():
        if key != "mode_defaults" and not isinstance(value, (dict, list)):
            merged[key] = value
    merged.update(mode_defaults)
    merged.setdefault("comfy_url", comfy.get("url", "http://127.0.0.1:8188"))
    merged.setdefault("checkpoint", "DreamShaper_8_pruned.safetensors")
    merged.setdefault("lora", "litiso_style_directional_v1.safetensors")
    merged.setdefault("lora_strength", 0.5)
    merged.setdefault("steps", 26)
    merged.setdefault("cfg", 6.0)
    merged.setdefault("sampler", "dpmpp_2m")
    merged.setdefault("scheduler", "karras")
    merged.setdefault("width", 512)
    merged.setdefault("height", 512)
    merged.setdefault("denoise", 0.55)
    merged.setdefault("timeout_seconds", 600)
    return merged


def action_frame_records(action_json: dict[str, Any], direction: str) -> list[dict[str, Any]]:
    records = [
        record for record in action_json.get("frames_meta", [])
        if record.get("direction") == direction
    ]
    records.sort(key=lambda record: int(record.get("frame_index", 0)))
    expected = int(action_json.get("frames", len(records)))
    if len(records) != expected:
        raise RuntimeError(f"{action_json.get('action')} {direction}: expected {expected} pose records, found {len(records)}")
    return records


def prompt_for(character: str, action: str, direction: str, frame_index: int, frame_count: int) -> str:
    direction_word = DIRECTION_WORD.get(direction, direction.lower())
    readable = character.replace("_", " ")
    return (
        f"LIT-ISO pixel sprite, {readable} adventurer, {action} cycle, {direction_word} facing, "
        f"frame {frame_index + 1} of {frame_count}, one isolated full-body character, "
        "cozy fantasy survival RPG, readable chibi proportions, pointed witch hat, robe, boots, "
        "clean hard pixel edges, limited palette, transparent or chroma background, no floor, no base"
    )


def negative_prompt() -> str:
    return (
        "floor, ground, base, pedestal, shadow blob, duplicate character, multiple characters, sprite sheet, "
        "turnaround grid, cropped body, missing feet, huge head, realistic render, painterly blur, antialias haze, "
        "text, watermark, frame, border, UI panel"
    )


def hard_alpha(image: Image.Image, threshold: int = 80) -> Image.Image:
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A").point(lambda value: 255 if value >= threshold else 0)
    rgba.putalpha(alpha)
    return rgba


def normalize_frame(source: Path, target: Path, target_size: int, max_fill: float = 0.86) -> dict[str, Any]:
    image = hard_alpha(Image.open(source))
    bbox = image.getbbox()
    if bbox is None:
        raise RuntimeError(f"empty source frame: {source}")
    crop = image.crop(bbox)
    max_w = max(1, int(target_size * max_fill))
    max_h = max(1, int(target_size * max_fill))
    scale = min(max_w / crop.width, max_h / crop.height)
    new_size = (max(1, round(crop.width * scale)), max(1, round(crop.height * scale)))
    resized = crop.resize(new_size, Image.Resampling.NEAREST)
    canvas = Image.new("RGBA", (target_size, target_size), (0, 0, 0, 0))
    x = (target_size - resized.width) // 2
    y = target_size - resized.height - max(2, target_size // 16)
    canvas.alpha_composite(resized, (x, max(0, y)))
    canvas = hard_alpha(canvas)
    target.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(target)
    return {
        "source": str(source),
        "target": str(target),
        "source_bbox": list(bbox),
        "target_size": [target_size, target_size],
        "content_bbox": list(canvas.getbbox() or (0, 0, 0, 0)),
    }


def make_review_scale(source: Path, target: Path, scale: int = 4) -> None:
    image = Image.open(source).convert("RGBA")
    target.parent.mkdir(parents=True, exist_ok=True)
    image.resize((image.width * scale, image.height * scale), Image.Resampling.NEAREST).save(target)


def worker_command(
    python_exe: str,
    project_root: Path,
    request_path: Path,
    raw_root: Path,
    clean_root: Path,
    manifest_path: Path,
    defaults: dict[str, Any],
    dry_run: bool,
) -> list[str]:
    cmd = [
        python_exe,
        str(project_root / "Tools" / "AssetForge" / "comfy_generation_worker.py"),
        "--project-root", str(project_root),
        "--request-path", str(request_path),
        "--raw-output-root", str(raw_root),
        "--clean-output-root", str(clean_root),
        "--manifest-path", str(manifest_path),
        "--comfy-url", str(defaults.get("comfy_url", "http://127.0.0.1:8188")),
        "--checkpoint", str(defaults.get("checkpoint", "DreamShaper_8_pruned.safetensors")),
        "--lora", str(defaults.get("lora", "")),
        "--lora-strength", str(defaults.get("lora_strength", 0.0)),
        "--steps", str(defaults.get("steps", 26)),
        "--cfg", str(defaults.get("cfg", 6.0)),
        "--sampler", str(defaults.get("sampler", "dpmpp_2m")),
        "--scheduler", str(defaults.get("scheduler", "karras")),
        "--width", str(defaults.get("width", 512)),
        "--height", str(defaults.get("height", 512)),
        "--denoise", str(defaults.get("denoise", 0.55)),
        "--timeout-seconds", str(defaults.get("timeout_seconds", 600)),
    ]
    if dry_run:
        cmd.append("--dry-run")
    return cmd


def run_command(cmd: list[str], cwd: Path) -> subprocess.CompletedProcess[str]:
    return subprocess.run(cmd, cwd=str(cwd), text=True, capture_output=True)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run SpriteForge lane A for one action/direction.")
    parser.add_argument("--project-root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--character", default="witch")
    parser.add_argument("--character-ref", type=Path, default=Path("Assets/Characters/Witch/AnimationSprites/Static/witch static00.png"))
    parser.add_argument("--action", default="walk")
    parser.add_argument("--direction", default="S")
    parser.add_argument("--target-size", type=int, default=64)
    parser.add_argument("--seed", default="1207")
    parser.add_argument("--out-root", type=Path, default=Path(__file__).resolve().parent / "out")
    parser.add_argument("--python", default="")
    parser.add_argument("--checkpoint", default="")
    parser.add_argument("--lora", default="")
    parser.add_argument("--lora-strength", type=float, default=None)
    parser.add_argument("--template-denoise", type=float, default=0.55)
    parser.add_argument("--style-weight", type=float, default=0.58)
    parser.add_argument("--control-strength", type=float, default=0.86)
    parser.add_argument("--control-net", default="control_v11p_sd15_openpose.pth")
    parser.add_argument("--dry-run", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    character_ref = args.character_ref if args.character_ref.is_absolute() else project_root / args.character_ref
    if not character_ref.exists():
        raise FileNotFoundError(f"character reference not found: {character_ref}")

    action_root = project_root / "Tools" / "SpriteForge" / "poses" / args.action
    action_json_path = action_root / "action.json"
    action_json = read_json(action_json_path)
    records = action_frame_records(action_json, args.direction)
    out_dir = (args.out_root if args.out_root.is_absolute() else project_root / args.out_root) / args.character / args.action / args.direction
    request_dir = out_dir / "_requests"
    raw_root = out_dir / "raw"
    worker_clean_root = out_dir / "worker_clean"
    worker_manifest_dir = out_dir / "_worker_manifests"
    frames_dir = out_dir / "frames"
    out_dir.mkdir(parents=True, exist_ok=True)

    defaults = load_worker_defaults(project_root, "character")
    defaults["denoise"] = args.template_denoise
    if args.checkpoint:
        defaults["checkpoint"] = args.checkpoint
    if args.lora:
        defaults["lora"] = args.lora
    if args.lora_strength is not None:
        defaults["lora_strength"] = args.lora_strength

    python_exe = args.python or default_python(project_root)
    frame_results: list[dict[str, Any]] = []
    failures: list[str] = []
    frame_count = len(records)
    for record in records:
        frame_index = int(record["frame_index"])
        pose_png = project_root / "Tools" / "SpriteForge" / "poses" / record["pose_png"]
        job_name = f"spriteforge_{args.character}_{args.action}_{args.direction.lower()}_f{frame_index:03d}"
        request_path = request_dir / f"frame_{frame_index:03d}.json"
        request = {
            "schema": "lit-iso.spriteforge.lane-a-frame-request.v1",
            "job_name": job_name,
            "asset_mode": "character",
            "asset_spec": {
                "character": args.character,
                "action": args.action,
                "direction": args.direction,
                "frame_index": frame_index,
            },
            "prompt": prompt_for(args.character, args.action, args.direction, frame_index, frame_count),
            "negative_prompt": negative_prompt(),
            "seed": f"{args.seed}_{frame_index:03d}",
            "batch_count": 1,
            "reference_image": rel(project_root, character_ref),
            "style_reference_image": rel(project_root, character_ref),
            "template_guidance": {"enabled": True, "denoise": args.template_denoise},
            "style_guidance": {
                "enabled": True,
                "weight": args.style_weight,
                "start_at": 0.0,
                "end_at": 0.72,
                "weight_type": "style transfer",
            },
            "control_guidance": {
                "enabled": True,
                "type": "openpose",
                "control_image_path": rel(project_root, pose_png),
                "control_net": args.control_net,
                "strength": args.control_strength,
                "start_percent": 0.0,
                "end_percent": 0.86,
            },
        }
        write_json(request_path, request)
        worker_manifest = worker_manifest_dir / f"frame_{frame_index:03d}.json"
        cmd = worker_command(python_exe, project_root, request_path, raw_root, worker_clean_root, worker_manifest, defaults, args.dry_run)
        completed = run_command(cmd, project_root)
        frame_result: dict[str, Any] = {
            "frame_index": frame_index,
            "phase": record.get("phase", ""),
            "pose_png": rel(project_root, pose_png),
            "request_path": rel(project_root, request_path),
            "worker_manifest": rel(project_root, worker_manifest),
            "worker_returncode": completed.returncode,
        }
        if completed.returncode != 0:
            error = completed.stderr.strip() or completed.stdout.strip() or f"worker failed for frame {frame_index}"
            frame_result["error"] = error
            failures.append(error)
        elif not args.dry_run:
            worker_data = read_json(worker_manifest)
            output = (worker_data.get("outputs") or [{}])[0]
            if output.get("status") != "ok" or not output.get("cleaned_path"):
                error = output.get("error") or f"worker did not produce cleaned frame {frame_index}"
                frame_result["error"] = error
                failures.append(error)
            else:
                cleaned_path = project_root / output["cleaned_path"]
                frame_path = frames_dir / f"frame_{frame_index:03d}.png"
                frame_result.update(normalize_frame(cleaned_path, frame_path, args.target_size))
        frame_results.append(frame_result)

    pack_result: dict[str, Any] = {}
    if not args.dry_run and not failures:
        pack_cmd = [
            python_exe,
            str(project_root / "Tools" / "SpriteForge" / "spriteforge_pack.py"),
            "--frames", str(frames_dir),
            "--out", str(out_dir),
            "--action-json", str(action_json_path),
            "--character", args.character,
            "--action", args.action,
            "--direction", args.direction,
        ]
        packed = run_command(pack_cmd, project_root)
        if packed.returncode != 0:
            failures.append(packed.stderr.strip() or packed.stdout.strip() or "spriteforge_pack failed")
            pack_result = {"returncode": packed.returncode, "stdout": packed.stdout, "stderr": packed.stderr}
        else:
            pack_result = json.loads(packed.stdout)
            make_review_scale(out_dir / "preview.png", out_dir / "preview_x4.png", 4)
            make_review_scale(out_dir / "sheet.png", out_dir / "sheet_x4.png", 4)

    manifest = {
        "schema": "lit-iso.spriteforge.lane-a-run.v1",
        "status": "planned" if args.dry_run else ("complete" if not failures else "failed"),
        "created_utc": now_utc(),
        "character": args.character,
        "action": args.action,
        "direction": args.direction,
        "target_size": args.target_size,
        "reference_image": rel(project_root, character_ref),
        "action_json": rel(project_root, action_json_path),
        "loop_start": action_json.get("loop_start", 0),
        "loop_end": action_json.get("loop_end", action_json.get("frames", 1) - 1),
        "loop_range": action_json.get("loop_range", [action_json.get("loop_start", 0), action_json.get("loop_end", action_json.get("frames", 1) - 1)]),
        "worker_settings": {
            "checkpoint": defaults.get("checkpoint"),
            "lora": defaults.get("lora"),
            "lora_strength": defaults.get("lora_strength"),
            "steps": defaults.get("steps"),
            "cfg": defaults.get("cfg"),
            "sampler": defaults.get("sampler"),
            "scheduler": defaults.get("scheduler"),
            "width": defaults.get("width"),
            "height": defaults.get("height"),
            "template_denoise": args.template_denoise,
            "style_weight": args.style_weight,
            "control_strength": args.control_strength,
            "control_net": args.control_net,
        },
        "frames": frame_results,
        "pack": pack_result,
        "review": {
            "frames_dir": rel(project_root, frames_dir),
            "sheet": rel(project_root, out_dir / "sheet.png"),
            "sheet_x4": rel(project_root, out_dir / "sheet_x4.png"),
            "preview": rel(project_root, out_dir / "preview.png"),
            "preview_x4": rel(project_root, out_dir / "preview_x4.png"),
            "sheet_json": rel(project_root, out_dir / "sheet.json"),
        },
        "errors": failures,
    }
    manifest_path = out_dir / "lane_a_manifest.json"
    write_json(manifest_path, manifest)
    print(json.dumps({"ok": manifest["status"] in {"complete", "planned"}, "status": manifest["status"], "manifest": str(manifest_path)}, indent=2))
    return 0 if manifest["status"] in {"complete", "planned"} else 1


if __name__ == "__main__":
    raise SystemExit(main())
