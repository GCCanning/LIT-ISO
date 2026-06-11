#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def safe_name(value: str) -> str:
    cleaned = "".join(ch if ch.isalnum() or ch in "_.-" else "_" for ch in value).strip("._")
    return cleaned or "asset"


def rel(root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path)


def request_payload(
    manifest_path: str,
    item: dict,
    args: argparse.Namespace,
    index: int,
) -> dict:
    tile_id = item["id"]
    job_name = safe_name(f"{args.job_prefix}_{tile_id}_v1")
    control_path = item["control"]["edge_hint_512" if args.control_hint == "edge" else "nearest_color_hint_512"]
    template_path = item["control"]["nearest_color_hint_512"]
    prompt = (
        f"{item['prompt']}, match the supplied LIT-ISO reference32 isometric pixel tile style, "
        "same compact silhouette, same transparent-corner footprint, crisp indexed-palette pixel art, "
        "terrain tile only"
    )
    return {
        "schema": "lit_iso.asset_forge.generation_request.v1",
        "job_name": job_name,
        "asset_mode": "tile",
        "provider": "comfyui",
        "batch_count": args.variants,
        "seed": args.seed + index * 100,
        "prompt": prompt,
        "negative_prompt": item["negative_prompt"],
        "reference_image": template_path,
        "template_guidance": {
            "enabled": True,
            "source": "reference32 nearest-color tile template",
            "denoise": args.template_denoise,
        },
        "asset_spec": {
            "asset_id": job_name,
            "biome": "Reference32",
            "material": item["material"],
            "role": item["role"],
            "source_tile": item["source_tile"],
            "source_lock_manifest": manifest_path,
            "source_lock_tile": item["path"],
            "source_template_image": template_path,
            "source_control_hint": control_path,
            "tile_shape": item["role"],
            "footprint": "diamond_1x1",
            "unity_category": "Tiles",
            "ppu": 32,
            "pivot": {"x": 0.5, "y": 0.25},
            "review_only": True,
        },
        "post_process": [
            "edge_background_removal",
            "sprite_fusion_snap",
            "palette_cap",
            "nearest_neighbor_resize",
            "proper_pixel_art",
            "terrain_profile_qa",
            "qa_report",
        ],
        "comfy_settings": {
            "checkpoint": args.checkpoint,
            "lora": args.lora,
            "lora_strength": args.lora_strength,
            "steps": args.steps,
            "cfg": args.cfg,
            "sampler": "dpmpp_2m",
            "scheduler": "karras",
            "width": 512,
            "height": 512,
            "denoise": args.template_denoise,
            "timeout_seconds": args.timeout_seconds,
        },
        "control_guidance": {
            "enabled": True,
            "type": "canny",
            "control_image_path": control_path,
            "control_net": args.control_net,
            "strength": args.control_strength,
            "start_percent": 0.0,
            "end_percent": args.control_end,
        },
        "acceptance_checks": [
            "matches reference32 source tile footprint",
            "stays terrain-only with no props or scene",
            "keeps transparent corners after cleanup",
            "readable after 32x32 nearest-neighbor normalization",
            "does not become a standalone cube/object diorama",
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Queue reference32 ControlNet tile requests for ComfyUI.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument(
        "--manifest",
        default="Assets/Generated/_Review/reference32_geometry_locked_tile_family_v1/reference32_tile_family_manifest.json",
    )
    parser.add_argument("--out-root", default="Temp/AssetForge/reference32_controlnet_tile_requests")
    parser.add_argument("--job-prefix", default="reference32_controlnet")
    parser.add_argument("--only", nargs="*", default=[])
    parser.add_argument("--variants", type=int, default=2)
    parser.add_argument("--seed", type=int, default=320100)
    parser.add_argument("--checkpoint", default="DreamShaper_8_pruned.safetensors")
    parser.add_argument("--lora", default="litiso_iso_reference_tile_style_v1_final.safetensors")
    parser.add_argument("--lora-strength", type=float, default=0.35)
    parser.add_argument("--control-net", default="control_v11p_sd15_canny_fp16.safetensors")
    parser.add_argument("--control-strength", type=float, default=0.92)
    parser.add_argument("--control-end", type=float, default=0.9)
    parser.add_argument("--control-hint", choices=["edge", "color"], default="edge")
    parser.add_argument("--template-denoise", type=float, default=0.32)
    parser.add_argument("--steps", type=int, default=20)
    parser.add_argument("--cfg", type=float, default=6.0)
    parser.add_argument("--timeout-seconds", type=int, default=600)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    if args.variants < 1 or args.variants > 4:
        raise ValueError("--variants must be between 1 and 4")

    project_root = Path(args.project_root).resolve()
    manifest_path = (project_root / args.manifest).resolve()
    manifest = read_json(manifest_path)
    out_root = (project_root / args.out_root).resolve()
    only = {item.lower() for item in args.only}

    created = []
    for index, item in enumerate(manifest["items"]):
        if only and item["id"].lower() not in only:
            continue
        payload = request_payload(rel(project_root, manifest_path), item, args, index)
        request_path = out_root / payload["job_name"] / "generation_request.json"
        if not args.dry_run:
            write_json(request_path, payload)
        created.append({
            "job_name": payload["job_name"],
            "tile_id": item["id"],
            "request_path": rel(project_root, request_path),
            "control_image_path": payload["control_guidance"]["control_image_path"],
            "lora": args.lora,
            "lora_strength": args.lora_strength,
            "variants": args.variants,
        })

    plan = {
        "schema": "lit_iso.asset_forge.reference32_controlnet_tile_queue.v1",
        "dry_run": bool(args.dry_run),
        "manifest": rel(project_root, manifest_path),
        "out_root": rel(project_root, out_root),
        "created_count": len(created),
        "created": created,
        "next_step": "Run process_generation_request_comfy.ps1 -DryRun for request validation, then run one tile smoke live before full batch.",
    }
    print(json.dumps(plan, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
