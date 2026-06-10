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
    return "".join(ch if ch.isalnum() or ch in "_.-" else "_" for ch in value).strip("._")


CONTROL_TEMPLATE_BY_SHAPE = {
    "flat_top": "flat_grid",
    "raised_block": "raised_block_h1",
    "north_edge": "edge_north",
    "south_edge": "edge_south",
    "east_edge": "edge_east",
    "west_edge": "edge_west",
    "outer_corner": "corner_ne",
    "inner_corner": "corner_sw",
}

CONTROL_STRENGTH_BY_SHAPE = {
    "flat_top": 0.58,
}

CONTROL_END_BY_SHAPE = {
    "flat_top": 0.78,
}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--spec", required=True)
    parser.add_argument("--batch", default="first_generation_batch")
    parser.add_argument("--provider", default="sprixen")
    parser.add_argument("--variants", type=int, default=2)
    parser.add_argument("--control-template-root", default=r"C:\Projects\Pixel Pipeline\datasets\lit_iso\controlnet_templates\tile_geometry_v1")
    parser.add_argument("--control-net", default="control_v11p_sd15_canny_fp16.safetensors")
    parser.add_argument("--control-strength", type=float, default=0.72)
    parser.add_argument("--checkpoint", default="DreamShaper_8_pruned.safetensors")
    parser.add_argument("--lora", default="")
    parser.add_argument("--lora-strength", type=float, default=0.0)
    parser.add_argument("--steps", type=int, default=18)
    parser.add_argument("--cfg", type=float, default=6.8)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    root = Path(args.project_root).resolve()
    spec_path = (root / args.spec).resolve()
    spec = read_json(spec_path)
    request_root = root / "Assets/Generated/_Review/_Requests"
    materials = spec["materials"]
    shape_ids = spec.get(args.batch, [])
    shapes_by_id = {shape["id"]: shape for shape in spec["required_shapes"]}
    negative = spec.get("negative_prompt", "")
    created = []

    for material in materials:
        for shape_id in shape_ids:
            shape = shapes_by_id[shape_id]
            if shape_id == "raised_block":
                # Already generated and curated as the selected master set.
                continue
            job_name = safe_name(f"greenwake_{material}_{shape_id}_v1")
            prompt = (
                "Original cozy 2D isometric game tile asset, Unity tilemap ready, "
                f"Greenwake {material.replace('_', ' ')} material, {shape['prompt_shape']}, "
                "match the approved Greenwake height material master palette and side-face lighting, "
                "transparent background, no props on top, clean-room original terrain tile."
            )
            terrain_profile = "flat" if shape_id == "flat_top" else "raised_block"
            control_template_id = CONTROL_TEMPLATE_BY_SHAPE.get(shape_id, "flat_diamond")
            control_template_path = Path(args.control_template_root) / f"{control_template_id}.png"
            control_strength = CONTROL_STRENGTH_BY_SHAPE.get(shape_id, args.control_strength)
            control_end = CONTROL_END_BY_SHAPE.get(shape_id, 0.85)
            request = {
                "schema": "lit_iso.asset_forge.generation_request.v1",
                "job_name": job_name,
                "asset_mode": "tile",
                "provider": args.provider,
                "batch_count": max(1, min(3, args.variants)),
                "seed": f"{job_name}-seed",
                "prompt": prompt,
                "negative_prompt": negative,
                "asset_spec": {
                    "asset_id": job_name,
                    "biome": spec["biome"],
                    "material": material,
                    "shape": shape_id,
                    "subtype": f"{material}_{shape_id}",
                    "tile_shape": "flat_top" if terrain_profile == "flat" else "raised_height_block",
                    "terrain_profile": terrain_profile,
                    "height": spec["canvas"]["height"],
                    "footprint": spec["canvas"]["footprint"],
                    "source_canvas": f"{spec['canvas']['source_px']}x{spec['canvas']['source_px']}",
                    "style_anchor_pack": spec["style_anchor_pack"],
                    "tile_overlay_policy": "terrain_only_no_props_or_trees",
                    "unity_category": "Tiles",
                    "ppu": spec["canvas"]["ppu"],
                    "pivot": spec["canvas"]["pivot"],
                    "anchor": "center" if terrain_profile == "flat" else "bottom_center",
                    "family_spec": spec["name"],
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
                    "denoise": 1.0,
                    "timeout_seconds": 600,
                },
                "acceptance_checks": [
                    "matches selected master material",
                    "transparent background",
                    "no props or objects on top",
                    "tile footprint discipline",
                    "readable at 32px after downsample",
                ],
                "control_guidance": {
                    "enabled": True,
                    "type": "canny",
                    "control_image_path": str(control_template_path),
                    "control_template": control_template_id,
                    "control_net": args.control_net,
                    "strength": control_strength,
                    "start_percent": 0.0,
                    "end_percent": control_end,
                },
            }
            path = request_root / job_name / "generation_request.json"
            if not args.dry_run:
                write_json(path, request)
            created.append({"job_name": job_name, "request_path": str(path.relative_to(root)).replace("\\", "/"), "material": material, "shape": shape_id})

    print(json.dumps({"schema": "lit_iso.asset_forge.tile_family_queue.v1", "spec": str(spec_path.relative_to(root)).replace("\\", "/"), "created": created}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
