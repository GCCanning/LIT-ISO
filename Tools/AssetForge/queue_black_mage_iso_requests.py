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

CANONICAL_DIRECTIONS = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"]
DEFAULT_DIRECTIONS = ["NE", "NW", "SE", "SW"]
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
DIRECTION_CONTRACT = {
    "NE": "character facing away-right, back and right side visible, no front-facing face",
    "NW": "character facing away-left, back and left side visible, no front-facing face",
    "SE": "character facing toward-right, front and right side visible",
    "SW": "character facing toward-left, front and left side visible",
    "S": "front view, both eyes and front robe readable",
    "N": "back view only, no face visible, back of hat and robe readable",
    "E": "right side profile, one side of hat and robe readable",
    "W": "left side profile, one side of hat and robe readable",
}
DIRECTION_NEGATIVE = {
    "NE": "front-facing, looking at camera, full face visible, south view, west side only",
    "NW": "front-facing, looking at camera, full face visible, south view, east side only",
    "SE": "back-only view, north view, left-facing only",
    "SW": "back-only view, north view, right-facing only",
    "S": "back-only view",
    "N": "front-facing, face, eyes, looking at camera, chest-front view",
    "E": "front-facing, back-facing, left-facing",
    "W": "front-facing, back-facing, right-facing",
}


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def safe_name(value: str) -> str:
    return re.sub(r"[^A-Za-z0-9_.-]+", "_", value).strip("._") or "black_mage_iso"


def repo_path(project_root: Path, path: str | Path) -> str:
    candidate = Path(path)
    try:
        return candidate.resolve().relative_to(project_root.resolve()).as_posix()
    except ValueError:
        return str(candidate)


def load_pose_map(manifest_path: Path) -> dict[str, dict]:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    return {entry["direction"]: entry for entry in manifest.get("entries", [])}


def load_mage_prompt_map(mage_manifest_path: Path) -> dict[str, dict]:
    if not mage_manifest_path.exists():
        return {}
    manifest = json.loads(mage_manifest_path.read_text(encoding="utf-8-sig"))
    return {entry["direction"]: entry for entry in manifest.get("generation_prompts", [])}


def request_payload(
    project_root: Path,
    job_name: str,
    direction: str,
    action: str,
    pose_entry: dict,
    prompt_entry: dict | None,
    reference_image: str,
    style_reference_image: str,
    style_weight: float,
    control_strength: float,
    seed: int,
    batch_count: int,
    strict_sprite_contract: bool,
    template_enabled: bool,
    template_denoise: float,
    control_enabled: bool,
    pose_source: str,
    style_end_at: float,
    checkpoint: str,
    lora: str,
    lora_strength: float,
    steps: int,
    cfg: float,
) -> dict:
    direction_word = DIRECTION_WORD[direction]
    prompt = (
        "LIT-ISO pixel character sprite, black mage wizard, compact isometric RPG sprite, "
        f"{direction_word} isometric view, {DIRECTION_CONTRACT[direction]}, "
        "preserve tall black hat with tan brim patch, brown hair, black robe with orange belt accents, "
        "dark gloves, one crooked staff with orange crystal, solid flat chroma green background for extraction, "
        "no floor, no shadow, no extra objects, crisp compact pixel art, bottom-center anchor"
    )
    if strict_sprite_contract:
        prompt += (
            ", single isolated full-body sprite only, staff close to body, compact 1x1 actor silhouette, "
            "plain chroma background only, no spell casting, no visual effects, no environment, no scenic prop"
        )
    negative = (
        "side-scroller sprite, chibi front portrait, floor, base, shadow, text, duplicate, blurry, "
        "antialiased, smooth painting, 3D render"
    )
    negative = (
        f"{negative}, wrong direction, {DIRECTION_NEGATIVE[direction]}, "
        "books, table, pumpkin, tile, terrain, circle, ring, halo, portal, ornament, extra staff, extra prop, backdrop"
    )
    if strict_sprite_contract:
        negative += (
            ", spell effect, magic circle, energy ring, aura, flame trail, glowing path, ground patch, "
            "gray card, background square, scene, environmental prop, floating duplicate, cropped body, "
            "summoning circle, light beam, oversized weapon effect, floor shadow, cast shadow"
        )
    pose_json_path = repo_path(project_root, pose_entry["pose_json"])
    reference_repo_path = repo_path(project_root, reference_image)
    style_reference_repo_path = repo_path(project_root, style_reference_image)
    return {
        "schema": "lit_iso.asset_forge.generation_request.v1",
        "generated_utc": utc_now(),
        "pack_name": "BlackMageIsoStyleLock",
        "job_name": job_name,
        "asset_mode": "character",
        "provider": "comfyui",
        "status": "queued_black_mage_isometric_controlnet_style_lock",
        "prompt": prompt,
        "negative_prompt": negative,
        "user_prompt": prompt,
        "user_negative_prompt": negative,
        "reference_image": reference_repo_path,
        "style_reference_image": style_reference_repo_path,
        "template_guidance": {
            "enabled": template_enabled,
            "source": "Direction scaffold template" if template_enabled else "BlackMage1 normalized reference is used for style only; template pose is disabled so OpenPose controls orientation.",
            "direction": direction,
            "policy": "preserve_direction_scaffold_silhouette" if template_enabled else "avoid_front_reference_pose_lock",
            "denoise": template_denoise,
        },
        "style_guidance": {
            "enabled": True,
            "source": "BlackMage1 normalized style lock reference",
            "weight": style_weight,
            "start_at": 0.0,
            "end_at": style_end_at,
            "weight_type": "style transfer",
            "ipadapter_model": "ip-adapter_sd15.safetensors",
            "preset": "STANDARD (medium strength)",
        },
        "control_guidance": {
            "enabled": control_enabled,
            "type": "openpose",
            "pose_json_path": pose_json_path,
            "control_net": "control_v11p_sd15_openpose.pth",
            "strength": control_strength,
            "start_percent": 0.0,
            "end_percent": 0.86,
            "action": action,
            "direction": direction,
            "source": pose_source,
        },
        "comfy_settings": {
            "checkpoint": checkpoint,
            "lora": lora,
            "lora_strength": lora_strength,
            "steps": steps,
            "cfg": cfg,
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
        "batch_count": batch_count,
        "asset_spec": {
            "subtype": "black_mage_isometric_style_lock",
            "biome": "shared",
            "variant": direction.lower(),
            "footprint": "1x1",
            "background_policy": "transparent",
            "shadow_policy": "none",
            "palette_policy": "reference_locked",
            "quality_gate": "strict_v7_no_floor_no_effects" if strict_sprite_contract else "strict",
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
        "post_process": [
            "background_remove",
            "sprite_fusion_snap",
            "palette_cap",
            "nearest_neighbor_resize",
            "fixed_canvas_normalize",
            "anchor_lock",
            "proper_pixel_art",
            "qa_report",
        ],
        "acceptance_checks": [
            "transparent_png",
            "pixel_perfect_edges",
            "openpose_direction_preserved",
            "black_mage_identity_preserved",
            "no_floor_for_sprites",
            "bottom_center_anchor",
        ],
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--pose-manifest", type=Path, default=Path(r"Assets\Generated\_Review\_PoseControls\litiso_openpose_diagonal_v1\idle_manifest.json"))
    parser.add_argument("--mage-manifest", type=Path, default=Path(r"Assets\Generated\_Review\black_mage_iso_style_lock_v1\black_mage_iso_review_manifest.json"))
    parser.add_argument("--reference-image", default=r"Assets\Generated\_Review\black_mage_iso_style_lock_v1\black_mage_normalized_128.png")
    parser.add_argument("--style-reference-image", default=r"Assets\Generated\_Review\black_mage_iso_style_lock_v1\black_mage_normalized_128.png")
    parser.add_argument("--scaffold-manifest", type=Path, default=Path(r"Assets\Generated\_Review\black_mage_iso_style_lock_v1\black_mage_iso_review_manifest.json"))
    parser.add_argument("--use-scaffold-template", action="store_true")
    parser.add_argument("--use-reference-template", action="store_true")
    parser.add_argument("--template-denoise", type=float, default=0.38)
    parser.add_argument("--out-root", type=Path, default=Path(r"Temp\AssetForge\black_mage_requests"))
    parser.add_argument("--directions", nargs="+", default=DEFAULT_DIRECTIONS)
    parser.add_argument("--action", default="Idle")
    parser.add_argument("--job-prefix", default="black_mage_iso")
    parser.add_argument("--variant-suffix", default="v1")
    parser.add_argument("--batch-count", type=int, default=1)
    parser.add_argument("--seed", type=int, default=118300)
    parser.add_argument("--style-weight", type=float, default=0.56)
    parser.add_argument("--style-end-at", type=float, default=0.68)
    parser.add_argument("--control-strength", type=float, default=0.84)
    parser.add_argument("--disable-control", action="store_true")
    parser.add_argument("--checkpoint", default="DreamShaper_8_pruned.safetensors")
    parser.add_argument("--lora", default="PixelArtRedmond-Lite64.safetensors")
    parser.add_argument("--lora-strength", type=float, default=0.32)
    parser.add_argument("--steps", type=int, default=28)
    parser.add_argument("--cfg", type=float, default=5.6)
    parser.add_argument("--strict-sprite-contract", action="store_true")
    parser.add_argument("--replace", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    pose_manifest = args.pose_manifest if args.pose_manifest.is_absolute() else project_root / args.pose_manifest
    mage_manifest = args.mage_manifest if args.mage_manifest.is_absolute() else project_root / args.mage_manifest
    reference_image = Path(args.reference_image)
    if not reference_image.is_absolute():
        reference_image = project_root / reference_image
    style_reference_image = Path(args.style_reference_image)
    if not style_reference_image.is_absolute():
        style_reference_image = project_root / style_reference_image
    scaffold_manifest_path = args.scaffold_manifest if args.scaffold_manifest.is_absolute() else project_root / args.scaffold_manifest
    if not pose_manifest.exists():
        raise FileNotFoundError(f"Missing diagonal pose manifest: {pose_manifest}")
    if not reference_image.exists():
        raise FileNotFoundError(f"Missing black mage normalized reference image: {reference_image}")
    if not style_reference_image.exists():
        raise FileNotFoundError(f"Missing black mage style reference image: {style_reference_image}")
    pose_map = load_pose_map(pose_manifest)
    prompt_map = load_mage_prompt_map(mage_manifest)
    scaffold_map: dict[str, str] = {}
    if args.use_scaffold_template:
        if not scaffold_manifest_path.exists():
            raise FileNotFoundError(f"Missing black mage scaffold manifest: {scaffold_manifest_path}")
        scaffold_manifest = json.loads(scaffold_manifest_path.read_text(encoding="utf-8-sig"))
        scaffold_map = {str(key).upper(): str(value) for key, value in scaffold_manifest.get("scaffolds", {}).items()}
    out_root = args.out_root if args.out_root.is_absolute() else project_root / args.out_root
    plan = []
    for index, direction in enumerate(args.directions):
        direction = direction.upper()
        if direction not in CANONICAL_DIRECTIONS:
            raise ValueError(f"Unsupported direction: {direction}")
        if direction not in pose_map:
            raise ValueError(f"Pose manifest has no {direction} entry: {pose_manifest}")
        template_reference = reference_image
        if args.use_scaffold_template:
            scaffold = scaffold_map.get(direction, "")
            if not scaffold:
                raise ValueError(f"Scaffold manifest has no {direction} entry: {scaffold_manifest_path}")
            template_reference = Path(scaffold)
            if not template_reference.is_absolute():
                template_reference = project_root / str(template_reference).replace("/", "\\")
            if not template_reference.exists():
                raise FileNotFoundError(f"Missing black mage {direction} scaffold image: {template_reference}")
        job_name = safe_name(f"{args.job_prefix}_{args.action.lower()}_{direction.lower()}_{args.variant_suffix}")
        request_root = out_root / job_name
        payload = request_payload(
            project_root,
            job_name,
            direction,
            args.action,
            pose_map[direction],
            prompt_map.get(direction),
            str(template_reference),
            str(style_reference_image),
            args.style_weight,
            args.control_strength,
            args.seed + index,
            max(1, min(8, args.batch_count)),
            args.strict_sprite_contract,
            args.use_scaffold_template or args.use_reference_template,
            args.template_denoise,
            not args.disable_control,
            repo_path(project_root, pose_manifest),
            args.style_end_at,
            args.checkpoint,
            args.lora,
            args.lora_strength,
            args.steps,
            args.cfg,
        )
        plan.append({"job_name": job_name, "direction": direction, "request_path": str(request_root / "generation_request.json"), "payload": payload})

    if args.dry_run:
        print(json.dumps({"ok": True, "status": "dry_run", "planned": len(plan), "requests": plan}, indent=2))
        return 0

    out_root.mkdir(parents=True, exist_ok=True)
    created = []
    for item in plan:
        request_root = Path(item["request_path"]).parent
        if request_root.exists() and args.replace:
            shutil.rmtree(request_root)
        elif request_root.exists():
            created.append({"job_name": item["job_name"], "direction": item["direction"], "status": "skipped_exists", "request_path": item["request_path"]})
            continue
        (request_root / "Inputs").mkdir(parents=True, exist_ok=True)
        (request_root / "Outputs").mkdir(parents=True, exist_ok=True)
        (request_root / "Review").mkdir(parents=True, exist_ok=True)
        request_path = request_root / "generation_request.json"
        status_path = request_root / "request_status.json"
        request_path.write_text(json.dumps(item["payload"], indent=2), encoding="utf-8")
        status_path.write_text(
            json.dumps(
                {
                    "ok": True,
                    "status": "queued_black_mage_isometric_controlnet_style_lock",
                    "saved_utc": utc_now(),
                    "job_name": item["job_name"],
                    "direction": item["direction"],
                    "next_step": f"Run Tools\\AssetForge\\process_generation_request_comfy.ps1 -JobName {item['job_name']} -DryRun first.",
                },
                indent=2,
            ),
            encoding="utf-8",
        )
        created.append({"job_name": item["job_name"], "direction": item["direction"], "status": "queued", "request_path": str(request_path)})
    print(json.dumps({"ok": True, "status": "queued", "created": created, "pose_manifest": str(pose_manifest), "reference_image": str(reference_image), "style_reference_image": str(style_reference_image), "use_scaffold_template": args.use_scaffold_template, "out_root": str(out_root)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
