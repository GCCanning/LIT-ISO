#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import json
import shutil
from pathlib import Path

from PIL import Image, ImageDraw


KIT_NAME = "GreenwakeTerrainStarterKit_v1"


ASSETS = [
    {
        "id": "grass_flat",
        "source": "Assets/Generated/_Review/greenwake_grass_flat_top_ref_v1/Greenwake/greenwake_grass_flat_top_ref_v1_v1.png",
        "output": "Greenwake/greenwake_grass_flat.png",
        "role": "flat_ground",
        "material": "grass",
        "height": 0,
        "pivot": {"x": 0.5, "y": 0.5},
    },
    {
        "id": "dirt_flat",
        "source": "Assets/Generated/_Review/greenwake_dirt_flat_top_v1/Greenwake/greenwake_dirt_flat_top_v1_v1.png",
        "output": "Greenwake/greenwake_dirt_flat.png",
        "role": "flat_ground",
        "material": "dirt",
        "height": 0,
        "pivot": {"x": 0.5, "y": 0.5},
    },
    {
        "id": "grass_to_dirt_north",
        "source": "Assets/Generated/_Review/greenwake_grass_to_dirt_north_stylelock_v1/Greenwake/greenwake_grass_to_dirt_north_stylelock_v1_v2.png",
        "output": "Greenwake/greenwake_grass_to_dirt_north.png",
        "role": "transition_cardinal",
        "material": "grass_to_dirt",
        "direction": "north",
        "height": 0,
        "pivot": {"x": 0.5, "y": 0.5},
    },
    {
        "id": "grass_to_dirt_south",
        "source": "Assets/Generated/_Review/greenwake_grass_to_dirt_south_stylelock_v1/Greenwake/greenwake_grass_to_dirt_south_stylelock_v1_v1.png",
        "output": "Greenwake/greenwake_grass_to_dirt_south.png",
        "role": "transition_cardinal",
        "material": "grass_to_dirt",
        "direction": "south",
        "height": 0,
        "pivot": {"x": 0.5, "y": 0.5},
    },
    {
        "id": "grass_to_dirt_east",
        "source": "Assets/Generated/_Review/greenwake_grass_to_dirt_east_stylelock_v1/Greenwake/greenwake_grass_to_dirt_east_stylelock_v1_v2.png",
        "output": "Greenwake/greenwake_grass_to_dirt_east.png",
        "role": "transition_cardinal",
        "material": "grass_to_dirt",
        "direction": "east",
        "height": 0,
        "pivot": {"x": 0.5, "y": 0.5},
    },
    {
        "id": "grass_to_dirt_west",
        "source": "Assets/Generated/_Review/greenwake_grass_to_dirt_west_stylelock_v1/Greenwake/greenwake_grass_to_dirt_west_stylelock_v1_v1.png",
        "output": "Greenwake/greenwake_grass_to_dirt_west.png",
        "role": "transition_cardinal",
        "material": "grass_to_dirt",
        "direction": "west",
        "height": 0,
        "pivot": {"x": 0.5, "y": 0.5},
    },
    {
        "id": "grass_raised_block",
        "source": "Assets/Generated/_Review/greenwake_height_material_masters_selected_v1/Greenwake/greenwake_height_master_grass.png",
        "output": "Greenwake/greenwake_grass_raised_block.png",
        "role": "raised_block",
        "material": "grass",
        "height": 1,
        "pivot": {"x": 0.5, "y": 0.25},
    },
    {
        "id": "dirt_raised_block",
        "source": "Assets/Generated/_Review/greenwake_height_material_masters_selected_v1/Greenwake/greenwake_height_master_dirt.png",
        "output": "Greenwake/greenwake_dirt_raised_block.png",
        "role": "raised_block",
        "material": "dirt",
        "height": 1,
        "pivot": {"x": 0.5, "y": 0.25},
    },
]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            digest.update(chunk)
    return digest.hexdigest()


def repo_path(root: Path, path: Path) -> str:
    return path.resolve().relative_to(root.resolve()).as_posix()


def draw_iso_patch(root: Path, output_root: Path, items: list[dict]) -> Path:
    lookup = {item["id"]: output_root / item["path"] for item in items}
    tile = 64
    canvas = Image.new("RGBA", (tile * 10, tile * 8), (29, 34, 33, 255))
    placements = [
        ("grass_flat", 1, 0), ("grass_flat", 2, 0), ("grass_to_dirt_east", 3, 0), ("dirt_flat", 4, 0), ("dirt_flat", 5, 0),
        ("grass_flat", 1, 1), ("grass_to_dirt_south", 2, 1), ("grass_to_dirt_east", 3, 1), ("dirt_flat", 4, 1), ("dirt_flat", 5, 1),
        ("grass_flat", 0, 2), ("grass_flat", 1, 2), ("grass_to_dirt_north", 2, 2), ("grass_to_dirt_west", 3, 2), ("dirt_flat", 4, 2),
        ("grass_raised_block", 1, 3), ("grass_flat", 2, 3), ("grass_flat", 3, 3), ("dirt_raised_block", 4, 3),
        ("grass_flat", 2, 4), ("grass_flat", 3, 4), ("grass_to_dirt_west", 4, 4), ("dirt_flat", 5, 4),
    ]
    for name, gx, gy in placements:
        image = Image.open(lookup[name]).convert("RGBA")
        x = (gx - gy) * tile // 2 + 250
        y = (gx + gy) * tile // 4 + 30
        canvas.alpha_composite(image, (x, y))
    output = output_root / "greenwake_starter_9x9_preview.png"
    canvas.save(output)
    return output


def make_contact(output_root: Path, items: list[dict]) -> Path:
    cell_w, cell_h = 176, 136
    cols = 4
    rows = (len(items) + cols - 1) // cols
    sheet = Image.new("RGBA", (cell_w * cols, cell_h * rows), (28, 32, 34, 255))
    draw = ImageDraw.Draw(sheet)
    for index, item in enumerate(items):
        x = (index % cols) * cell_w
        y = (index // cols) * cell_h
        draw.rectangle([x, y, x + cell_w - 1, y + cell_h - 1], fill=(32, 37, 40, 255), outline=(78, 88, 90, 255))
        image = Image.open(output_root / item["path"]).convert("RGBA").resize((96, 96), Image.Resampling.NEAREST)
        sheet.alpha_composite(image, (x + 40, y + 6))
        draw.text((x + 6, y + 106), item["id"][:28], fill=(224, 228, 218, 255))
        draw.text((x + 6, y + 122), item["role"], fill=(164, 176, 170, 255))
    output = output_root / "greenwake_starter_contact_sheet.png"
    sheet.save(output)
    return output


def main() -> int:
    root = Path.cwd()
    output_root = root / "Assets/Generated/_Review" / KIT_NAME
    if output_root.exists():
        shutil.rmtree(output_root)
    (output_root / "Greenwake").mkdir(parents=True)

    manifest_items = []
    for asset in ASSETS:
        source = root / asset["source"]
        if not source.exists():
            raise FileNotFoundError(source)
        destination = output_root / asset["output"]
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, destination)
        width, height = Image.open(destination).size
        item = {
            "id": asset["id"],
            "name": destination.name,
            "path": repo_path(output_root, destination),
            "source": asset["source"],
            "category": "terrain",
            "biome": "Greenwake",
            "material": asset["material"],
            "role": asset["role"],
            "width": width,
            "height": height,
            "sha256": sha256(destination),
            "status": "starter_kit_candidate",
            "unity": {
                "category": "Tiles",
                "ppu": 64,
                "pivot": asset["pivot"],
                "height": asset["height"],
                "footprint": "diamond_1x1",
            },
        }
        if "direction" in asset:
            item["direction"] = asset["direction"]
        manifest_items.append(item)

    contact = make_contact(output_root, manifest_items)
    preview = draw_iso_patch(root, output_root, manifest_items)
    manifest = {
        "schema": "lit_iso.asset_forge.greenwake_terrain_starter_kit.v1",
        "pack_name": KIT_NAME,
        "status": "review_ready",
        "source_kind": "curated_from_existing_review_outputs",
        "generated_utc_note": "Generated locally from already-created review assets; no new AI generation.",
        "scope_lock": [
            "1 grass flat tile",
            "1 dirt flat tile",
            "4 grass-to-dirt cardinal transitions",
            "1 raised grass block",
            "1 raised dirt block",
        ],
        "contact_sheet": repo_path(root, contact),
        "preview": repo_path(root, preview),
        "items": manifest_items,
        "not_in_scope": [
            "forest-floor transitions",
            "path transitions",
            "stone transitions",
            "diagonal/corner transitions",
            "runtime promotion",
        ],
    }
    (output_root / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps({"pack": repo_path(root, output_root), "items": len(manifest_items), "contact": manifest["contact_sheet"], "preview": manifest["preview"]}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
