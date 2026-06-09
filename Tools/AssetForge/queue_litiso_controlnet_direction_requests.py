#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import shutil
import sys
from datetime import datetime, timezone
from pathlib import Path

from queue_oga_template_guided_requests import CANONICAL_DIRECTIONS, DIRECTION_WORD, safe_name

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def pose_manifest_path(project_root: Path, pose_root: Path, action: str) -> Path:
    root = pose_root if pose_root.is_absolute() else project_root / pose_root
    return root / f"{action.lower()}_manifest.json"


def load_pose_map(manifest_path: Path) -> dict[str, dict]:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    return {entry["direction"]: entry for entry in manifest.get("entries", [])}


def load_oracle_map(manifest_path: Path | None) -> dict[str, dict]:
    if not manifest_path:
        return {}
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    return {entry["direction"]: entry for entry in manifest.get("frames", [])}


def direction_style_reference(style_reference: str, direction: str) -> str:
    path = Path(style_reference) if style_reference else None
    if not path:
        return ""
    direction_path = path.with_name(f"reference_knight_{direction.lower()}_cell{path.suffix}")
    if direction_path.exists():
        return str(direction_path)
    return style_reference


def request_payload(
    job_name: str,
    action: str,
    direction: str,
    pose_json: str,
    seed: int,
    style_reference: str,
    style_weight: float,
    control_strength: float,
    oracle_reference: str = "",
    template_denoise: float = 0.64,
) -> dict:
    direction_word = DIRECTION_WORD[direction]
    direction_contracts = {
        "S": "front view, visible masked helmet face, chest armor visible",
        "E": "right side view, profile silhouette, one side of helmet visible",
        "W": "left side view, profile silhouette, one side of helmet visible",
        "N": "back view only, facing away from camera, back of hood and cloak visible, back armor visible, no face visible, no visor visible",
    }
    direction_negatives = {
        "S": "back-only view",
        "E": "front-facing, back-facing, symmetrical front pose",
        "W": "front-facing, back-facing, symmetrical front pose",
        "N": "front-facing, face, eyes, visor, mask front, chest plate front, looking at camera, portrait face",
    }
    face_clause = "readable masked helmet face" if direction != "N" else "no visible face, back of hood readable"
    camera_contract = (
        "same camera as the other direction frames, orthographic isometric pixel-game camera, "
        "slight top-down view, no perspective lens, no low-angle view, fixed full-body framing, "
        "feet near bottom-center anchor, character height fills about 82 percent of the sprite cell"
    )
    prompt = (
        "LIT-ISO pixel sprite, full body cyan rune knight adventurer, dark hooded armor, "
        f"cyan glowing trim, amber rune details, {face_clause}, strong silhouette, "
        f"{action} animation pose, facing {direction_word}, direction {direction}, "
        f"{direction_contracts.get(direction, '')}, "
        f"{camera_contract}, "
        "body orientation must match the OpenPose control, crisp pixel art, solid flat chroma green background for extraction, no floor, no shadow, no magic circle, no backdrop"
    )
    negative = (
        "concept art, painterly, realistic, 3d render, background, forest, room, floor, base, "
        "platform, shadow blob, duplicate character, cropped body, portrait, text, watermark, "
        "zoomed in, zoomed out, tiny character, giant character, low-angle camera, dramatic perspective, "
        "different camera angle, top-down flat icon, floor ellipse, floor shadow disk, circular backdrop, round backdrop, "
        "halo, moon disk, spotlight, vignette, magic circle, energy pool, puddle, ground splash, shadow puddle, aura background, "
        f"wrong direction, {direction_negatives.get(direction, 'front-facing when another direction is requested')}"
    )
    return {
        "schema": "lit_iso.asset_forge.generation_request.v1",
        "generated_utc": utc_now(),
        "pack_name": "LITISO_ControlNetDirectionSmoke",
        "job_name": job_name,
        "asset_mode": "character",
        "provider": "comfyui",
        "status": "queued_from_litiso_controlnet_direction_builder",
        "prompt": prompt,
        "negative_prompt": negative,
        "user_prompt": prompt,
        "user_negative_prompt": negative,
        "reference_image": oracle_reference,
        "style_reference_image": style_reference,
        "template_guidance": {
            "enabled": bool(oracle_reference),
            "source": "LIT-ISO canonical 4D camera/framing oracle",
            "direction": direction,
            "denoise": template_denoise,
            "policy": "camera_framing_silhouette_guidance",
        },
        "style_guidance": {
            "enabled": bool(style_reference),
            "source": "LIT-ISO approved/reference style sprite",
            "weight": style_weight,
            "start_at": 0.0,
            "end_at": 0.68,
            "weight_type": "style transfer",
            "ipadapter_model": "ip-adapter_sd15.safetensors",
            "preset": "STANDARD (medium strength)",
        },
        "control_guidance": {
            "enabled": True,
            "type": "openpose",
            "pose_json_path": pose_json,
            "control_net": "control_v11p_sd15_openpose.pth",
            "strength": control_strength,
            "start_percent": 0.0,
            "end_percent": 0.86,
            "action": action,
            "direction": direction,
            "source": "Assets/Generated/_Review/_PoseControls/litiso_openpose_v1",
        },
        "comfy_settings": {
            "checkpoint": "DreamShaper_8_pruned.safetensors",
            "lora": "PixelArtRedmond-Lite64.safetensors",
            "lora_strength": 0.35,
            "steps": 26,
            "cfg": 5.6,
            "sampler": "dpmpp_2m",
            "scheduler": "karras",
            "width": 512,
            "height": 512,
            "denoise": 1.0,
            "timeout_seconds": 600,
        },
        "seed": str(seed),
        "directions": "single",
        "canonical_direction_order": CANONICAL_DIRECTIONS,
        "animation": {"name": action, "frame_count": 1, "fps": 0, "loop": False, "clips": []},
        "clips": [],
        "batch_count": 1,
        "asset_spec": {
            "subtype": "cyan_rune_knight_controlnet_direction",
            "biome": "shared",
            "variant": direction.lower(),
            "footprint": "1x1",
            "background_policy": "transparent",
            "shadow_policy": "none",
            "palette_policy": "reference_locked",
            "quality_gate": "strict",
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
        "post_process": ["background_remove", "sprite_fusion_snap", "palette_cap", "nearest_neighbor_resize", "fixed_canvas_normalize", "anchor_lock", "qa_report"],
        "acceptance_checks": ["transparent_png", "pixel_perfect_edges", "strict_qa_report", "openpose_direction_preserved", "no_floor_for_sprites", "bottom_center_anchor"],
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--pose-root", type=Path, default=Path(r"Assets\Generated\_Review\_PoseControls\litiso_openpose_v1"))
    parser.add_argument("--out-root", type=Path, default=Path(r"Assets\Generated\_Review\_Requests"))
    parser.add_argument("--action", default="Idle")
    parser.add_argument("--directions", nargs="+", default=["S", "E", "N", "W"])
    parser.add_argument("--job-prefix", default="litiso_control_refknight")
    parser.add_argument("--seed", type=int, default=93400)
    parser.add_argument("--style-reference", default=r"Assets\Generated\_Review\_StyleRefs\reference_knight_front_cell.png")
    parser.add_argument("--style-weight", type=float, default=0.55)
    parser.add_argument("--control-strength", type=float, default=0.82)
    parser.add_argument("--oracle-manifest", type=Path, default=Path(""))
    parser.add_argument("--template-denoise", type=float, default=0.64)
    parser.add_argument("--replace", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    out_root = args.out_root if args.out_root.is_absolute() else project_root / args.out_root
    manifest_path = pose_manifest_path(project_root, args.pose_root, args.action)
    if not manifest_path.exists():
        raise FileNotFoundError(f"Missing pose manifest. Run build_litiso_openpose_direction_library.py first: {manifest_path}")
    pose_map = load_pose_map(manifest_path)
    oracle_manifest = None
    if str(args.oracle_manifest).strip():
        oracle_manifest = args.oracle_manifest if args.oracle_manifest.is_absolute() else project_root / args.oracle_manifest
        if not oracle_manifest.exists():
            raise FileNotFoundError(f"Missing oracle manifest: {oracle_manifest}")
    oracle_map = load_oracle_map(oracle_manifest)
    out_root.mkdir(parents=True, exist_ok=True)
    created = []
    for index, direction in enumerate(args.directions):
        if direction not in CANONICAL_DIRECTIONS:
            raise ValueError(f"Unsupported direction: {direction}")
        if direction not in pose_map:
            raise FileNotFoundError(f"Pose manifest has no entry for direction {direction}: {manifest_path}")
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
        payload = request_payload(
            job_name,
            args.action,
            direction,
            pose_map[direction]["pose_json"],
            args.seed + index,
            direction_style_reference(args.style_reference, direction),
            args.style_weight,
            args.control_strength,
            oracle_map.get(direction, {}).get("source_image", ""),
            args.template_denoise,
        )
        request_path = request_root / "generation_request.json"
        status_path = request_root / "request_status.json"
        request_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
        status_path.write_text(
            json.dumps(
                {
                    "ok": True,
                    "status": "queued_controlnet_direction",
                    "saved_utc": utc_now(),
                    "job_name": job_name,
                    "action": args.action,
                    "direction": direction,
                    "pose_json": pose_map[direction]["pose_json"],
                    "next_step": f"Run process_generation_request_comfy.ps1 -JobName {job_name} -DryRun first.",
                },
                indent=2,
            ),
            encoding="utf-8",
        )
        created.append({"job_name": job_name, "status": "queued", "direction": direction, "request_path": str(request_path)})
    print(json.dumps({"ok": True, "created": created, "pose_manifest": str(manifest_path)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
