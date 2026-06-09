#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path


REFERENCE_URLS = {
    "grass": "https://api.sprixen.com/api/public/uploads/generations/cmp0ixf2f00lh14e7rtkkecyg/cmq3hkq6400mii22icqwpah58_0.png",
    "dirt": "https://api.sprixen.com/api/public/uploads/generations/cmp0ixf2f00lg14e7b8niqk0u/cmq3ho8xz00mri22i6r1etga2_0.png",
    "forest_floor": "https://api.sprixen.com/api/public/uploads/generations/cmp0ixf2f00lh14e7rtkkecyg/cmq3hx1uh00n0i22ix5fk3ymq_0.png",
    "stone": "https://api.sprixen.com/api/public/uploads/generations/cmp0ixf2f00lg14e7b8niqk0u/cmq3i283f00n9i22i0830ee2o_1.png",
    "path": "https://api.sprixen.com/api/public/uploads/generations/cmp0ixf2f00lh14e7rtkkecyg/cmq3ibcz000nii22ii1w94ysp_1.png",
}


TRANSITIONS = [
    ("grass", "dirt"),
    ("grass", "forest_floor"),
    ("grass", "stone"),
    ("grass", "path"),
]


DIRECTIONS = ["north", "south", "east", "west", "north_east", "north_west", "south_east", "south_west"]


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def safe_name(value: str) -> str:
    return "".join(ch if ch.isalnum() or ch in "_.-" else "_" for ch in value).strip("._")


def direction_words(direction: str) -> str:
    return direction.replace("_", "-")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--variants", type=int, default=2)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    root = Path(args.project_root).resolve()
    request_root = root / "Assets/Generated/_Review/_Requests"
    created = []
    for base, overlay in TRANSITIONS:
        for direction in DIRECTIONS:
            job_name = safe_name(f"greenwake_{base}_to_{overlay}_{direction}_stylelock_v1")
            base_label = base.replace("_", " ")
            overlay_label = overlay.replace("_", " ")
            direction_label = direction_words(direction)
            prompt = (
                "Original cozy 2D isometric terrain transition tile for Unity, single flat 2:1 diamond tile, "
                f"Greenwake {base_label} base with a subtle organic {overlay_label} blend entering from the {direction_label} edge, "
                "natural hand-painted pixel texture, no hard straight split, no side walls, transparent corners, "
                "terrain only, no props, no trees, no rocks, no flowers, no characters, no labels, clean-room original art. "
                "Match the approved Greenwake material-master palette, pixel density, soft outline weight, and painterly tile style."
            )
            request = {
                "schema": "lit_iso.asset_forge.generation_request.v1",
                "job_name": job_name,
                "asset_mode": "tile",
                "provider": "sprixen",
                "batch_count": max(1, min(3, args.variants)),
                "seed": f"{job_name}-seed",
                "prompt": prompt,
                "negative_prompt": (
                    "tree, bush, rock, flower, log, prop, item, character, creature, building, wall, raised block, "
                    "cliff side, complete map, background scene, UI frame, text, watermark, realistic render, "
                    "hard diagonal split, two separate tiles, object on top"
                ),
                "reference_image_url": REFERENCE_URLS[base],
                "style_reference": {
                    "mode": "sprixen_reference_image_url_plus_project_style_lock",
                    "base_material": base,
                    "overlay_material": overlay,
                    "reference_image_url": REFERENCE_URLS[base],
                    "secondary_reference_image_url": REFERENCE_URLS[overlay],
                    "sprixen_style_lock_required": True,
                    "note": "Use the base material master as the primary style reference. Use Sprixen tiles_project_id from local config when running.",
                },
                "asset_spec": {
                    "asset_id": job_name,
                    "biome": "Greenwake",
                    "material": f"{base}_to_{overlay}",
                    "materials": [base, overlay],
                    "shape": "organic_transition",
                    "direction": direction,
                    "subtype": f"{base}_to_{overlay}_{direction}_stylelock",
                    "tile_shape": "flat_top",
                    "terrain_profile": "flat",
                    "height": 0,
                    "footprint": "diamond_1x1",
                    "source_canvas": "64x64",
                    "style_anchor_pack": "Assets/Generated/_Review/greenwake_height_material_masters_selected_v1",
                    "tile_overlay_policy": "terrain_only_no_props_or_trees",
                    "unity_category": "Tiles",
                    "ppu": 64,
                    "pivot": {"x": 0.5, "y": 0.5},
                    "anchor": "center",
                    "family_spec": "greenwake_height_tile_family_v1",
                    "generated_reason": "final-art candidate for local-mask replacement",
                },
                "post_process": [
                    "edge_background_removal",
                    "sprite_fusion_snap",
                    "palette_cap",
                    "nearest_neighbor_resize",
                    "terrain_profile_qa",
                    "qa_report",
                ],
                "acceptance_checks": [
                    "organic blend, not a hard split",
                    "matches selected Greenwake material palette",
                    "transparent corners",
                    "flat diamond footprint",
                    "no props or objects on top",
                    "readable at 32px after downsample",
                ],
            }
            path = request_root / job_name / "generation_request.json"
            if not args.dry_run:
                write_json(path, request)
            created.append({"job_name": job_name, "path": str(path.relative_to(root)).replace("\\", "/")})

    print(json.dumps({"schema": "lit_iso.asset_forge.greenwake_style_lock_queue.v1", "dry_run": args.dry_run, "created": created}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
