#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
import shutil
import sys
from datetime import datetime, timezone
from pathlib import Path

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")

SOURCE_ROOT = Path(r"C:\Projects\Pixel Pipeline\sources\opengameart\400-items-basehumanmale-orc-skeleton\extracted\part-1\BaseHumanMale")
FILENAME_RE = re.compile(r"^(?P<index>\d+)_(?P<action>[^_]+)_CAM(?P<cam>\d+)_(?P<frame>\d+)\.png$", re.IGNORECASE)
CANONICAL_DIRECTIONS = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"]
LITISO_TO_CAM = {"S": 7, "SE": 6, "E": 5, "NE": 4, "N": 3, "NW": 2, "W": 1, "SW": 0}
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


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def safe_name(value: str) -> str:
    return re.sub(r"[^A-Za-z0-9_.-]+", "_", value).strip("._") or "oga_template_job"


def parse(path: Path) -> dict | None:
    match = FILENAME_RE.match(path.name)
    if not match:
        return None
    return {
        "index": int(match.group("index")),
        "action": match.group("action"),
        "cam": int(match.group("cam")),
        "frame": int(match.group("frame")),
    }


def choose_template(action: str, direction: str) -> Path:
    cam = LITISO_TO_CAM[direction]
    candidates = []
    for path in SOURCE_ROOT.glob("*.png"):
        record = parse(path)
        if not record:
            continue
        if record["action"].lower() == action.lower() and record["cam"] == cam:
            candidates.append((record["frame"], path))
    if not candidates:
        raise FileNotFoundError(f"No OGA template found for action={action} direction={direction} cam={cam}")
    candidates.sort(key=lambda item: item[0])
    return candidates[len(candidates) // 2][1]


def request_payload(job_name: str, action: str, direction: str, template_path: str, denoise: float, seed: int, style_reference: str, style_weight: float) -> dict:
    direction_word = DIRECTION_WORD[direction]
    prompt = (
        "LIT-ISO pixel sprite, full body cyan rune knight adventurer, dark hooded armor, "
        "cyan energy trim, amber rune details, readable helmet face, strong silhouette, "
        f"{action} animation pose, facing {direction_word}, direction {direction}, "
        "crisp pixel art, transparent background, no floor"
    )
    negative = (
        "concept art, painterly, realistic, 3d render, background, forest, room, floor, base, "
        "platform, shadow blob, duplicate character, cropped body, portrait, text, watermark"
    )
    return {
        "schema": "lit_iso.asset_forge.generation_request.v1",
        "generated_utc": utc_now(),
        "pack_name": "OGA_TemplateGuidedSmoke",
        "job_name": job_name,
        "asset_mode": "character",
        "provider": "comfyui",
        "status": "queued_from_oga_template_guided_builder",
        "prompt": prompt,
        "negative_prompt": negative,
        "user_prompt": prompt,
        "user_negative_prompt": negative,
        "reference_image": template_path,
        "reference_image_url": "",
        "style_reference_image": style_reference,
        "template_guidance": {
            "enabled": True,
            "source": "OpenGameArt 8D BaseHumanMale oracle",
            "action": action,
            "direction": direction,
            "denoise": denoise,
            "policy": "pose_orientation_silhouette_only_with_cc_by_attribution",
        },
        "style_guidance": {
            "enabled": bool(style_reference),
            "source": "LIT-ISO approved/reference style sprite",
            "weight": style_weight,
            "start_at": 0.0,
            "end_at": 0.72,
            "weight_type": "style transfer",
            "ipadapter_model": "ip-adapter_sd15.safetensors",
            "preset": "STANDARD (medium strength)",
        },
        "comfy_settings": {
            "checkpoint": "DreamShaper_8_pruned.safetensors",
            "lora": "PixelArtRedmond-Lite64.safetensors",
            "lora_strength": 0.35,
            "steps": 24,
            "cfg": 5.8,
            "sampler": "dpmpp_2m",
            "scheduler": "karras",
            "width": 512,
            "height": 512,
            "denoise": denoise,
            "timeout_seconds": 600,
        },
        "seed": str(seed),
        "directions": "single",
        "canonical_direction_order": CANONICAL_DIRECTIONS,
        "animation": {"name": action, "frame_count": 1, "fps": 0, "loop": False, "clips": []},
        "clips": [],
        "batch_count": 1,
        "asset_spec": {
            "subtype": "cyan_rune_knight_template_guided",
            "biome": "shared",
            "variant": direction.lower(),
            "footprint": "1x1",
            "background_policy": "transparent",
            "shadow_policy": "none",
            "palette_policy": "reference_locked",
            "quality_gate": "strict",
            "tile_overlay_policy": "not_applicable",
        },
        "canvas": {
            "width": 128,
            "height": 128,
            "cell_size": 128,
            "ppu": 128,
            "pivot": {"x": 0.5, "y": 0.0},
            "anchor": "bottom_center",
        },
        "unity_import": {
            "category": "Characters",
            "target_folder": f"Assets/Generated/Characters/{job_name}",
            "texture_type": "Sprite",
            "sprite_mode": "Single",
            "filter_mode": "Point",
            "mip_maps": False,
            "compression": "None",
            "ppu": 128,
            "pivot": {"x": 0.5, "y": 0.0},
        },
        "output_intent": {
            "review_folder": f"Assets/Generated/_Review/{job_name}",
            "unity_category": "Characters",
            "transparent_background": True,
            "pixel_perfect": True,
            "point_filtering": True,
            "no_mipmaps": True,
            "bottom_center_anchor": True,
        },
        "post_process": [
            "background_remove",
            "sprite_fusion_snap",
            "palette_cap",
            "nearest_neighbor_resize",
            "fixed_canvas_normalize",
            "anchor_lock",
            "qa_report",
        ],
        "acceptance_checks": [
            "transparent_png",
            "pixel_perfect_edges",
            "manifest_ready_for_unity",
            "strict_qa_report",
            "template_pose_preserved",
            "no_floor_for_sprites",
            "bottom_center_anchor",
        ],
        "clean_room_note": "Template-guided generation using CC-BY OGA source as motion/direction conditioning; final pixels must be reviewed and attributed/provenanced before training or runtime use.",
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--out-root", type=Path, default=Path(r"Assets\Generated\_Review\_Requests"))
    parser.add_argument("--action", default="Walk")
    parser.add_argument("--directions", nargs="+", default=["S", "E", "N", "W"])
    parser.add_argument("--job-prefix", default="oga_template_cyan_knight")
    parser.add_argument("--template-path", default="")
    parser.add_argument("--denoise", type=float, default=0.42)
    parser.add_argument("--seed", type=int, default=91000)
    parser.add_argument("--style-reference", default="")
    parser.add_argument("--style-weight", type=float, default=0.58)
    parser.add_argument("--replace", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    out_root = (project_root / args.out_root).resolve() if not args.out_root.is_absolute() else args.out_root.resolve()
    out_root.mkdir(parents=True, exist_ok=True)
    created = []
    for index, direction in enumerate(args.directions):
        if direction not in LITISO_TO_CAM:
            raise ValueError(f"Unsupported direction: {direction}")
        template = Path(args.template_path).resolve() if args.template_path else choose_template(args.action, direction)
        if not template.exists():
            raise FileNotFoundError(f"Template path does not exist: {template}")
        job_name = safe_name(f"{args.job_prefix}_{args.action.lower()}_{direction.lower()}")
        request_root = out_root / job_name
        if request_root.exists() and args.replace:
            shutil.rmtree(request_root)
        elif request_root.exists():
            created.append({"job_name": job_name, "status": "skipped_exists", "request_root": str(request_root)})
            continue
        (request_root / "Inputs").mkdir(parents=True, exist_ok=True)
        (request_root / "Outputs").mkdir(parents=True, exist_ok=True)
        (request_root / "Review").mkdir(parents=True, exist_ok=True)
        payload = request_payload(job_name, args.action, direction, str(template), args.denoise, args.seed + index, args.style_reference, args.style_weight)
        request_path = request_root / "generation_request.json"
        status_path = request_root / "request_status.json"
        worker_path = request_root / "worker_queue_item.json"
        request_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
        status_path.write_text(
            json.dumps(
                {
                    "ok": True,
                    "status": "queued_template_guided",
                    "saved_utc": utc_now(),
                    "job_name": job_name,
                    "asset_mode": "character",
                    "provider": "comfyui",
                    "template": str(template),
                    "next_step": f"Run process_generation_request_comfy.ps1 -JobName {job_name} -DryRun first.",
                },
                indent=2,
            ),
            encoding="utf-8",
        )
        worker_path.write_text(
            json.dumps(
                {
                    "schema": "lit_iso.asset_forge.worker_queue_item.v1",
                    "status": "queued_template_guided",
                    "saved_utc": utc_now(),
                    "job_name": job_name,
                    "asset_mode": "character",
                    "provider": "comfyui",
                    "request_path": str(request_path),
                    "template": str(template),
                },
                indent=2,
            ),
            encoding="utf-8",
        )
        (request_root / "README.md").write_text(
            f"# {job_name}\n\nTemplate-guided OGA direction smoke request.\n\nTemplate: `{template}`\n",
            encoding="utf-8",
        )
        created.append({"job_name": job_name, "status": "queued", "direction": direction, "template": str(template), "request_path": str(request_path)})
    print(json.dumps({"ok": True, "created": created}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
