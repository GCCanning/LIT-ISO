#!/usr/bin/env python3
"""
Build a LoRA-ready style dataset from an approved local PixelArt folder.

The output matches the simple dataset contract used by
Tools/LoRA/train_litiso_lora_resumable.py:

  metadata.jsonl
  images/*.png
  captions/*.txt

This script does not decide licensing. It records the provided license/author in
the manifest and captions only files from the explicitly supplied source folder.
"""

from __future__ import annotations

import argparse
import json
import re
import shutil
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageDraw

IMAGE_EXTS = {".png", ".jpg", ".jpeg", ".webp"}
ANIM_EXTS = {".gif"}

TILE_WORDS = {
    "acid",
    "block",
    "bridge",
    "grass",
    "ground",
    "lava",
    "water",
    "wall",
    "ladder",
    "trapdoor",
    "floor",
    "stairs",
    "boss",
    "edge",
    "centre",
    "center",
    "pattern",
}
PROP_WORDS = {
    "barrel",
    "box",
    "boxes",
    "chain",
    "chair",
    "door",
    "jail",
    "lever",
    "pilar",
    "pillar",
    "pipe",
    "torch",
    "table",
    "crate",
    "post",
    "lantern",
    "waterf",
    "spikef",
}
ITEM_WORDS = {"chest", "coin", "key", "potion", "treasure"}
CHARACTER_WORDS = {"blackmage", "mage", "ninja", "swordsman", "female", "sword"}
VFX_WORDS = {"spell", "summon", "acid", "lava", "water"}
TERRAIN_SURFACE_WORDS = {"acid", "dirt", "grass", "ground", "lava", "sand", "snow", "water"}
TERRAIN_STRUCTURE_WORDS = {"block", "bridge", "stairs", "ladder", "wall"}
HEIGHT_MATERIAL_WORDS = {
    "andesite",
    "basalt",
    "brick",
    "bricks",
    "clay",
    "cobblestone",
    "deepslate",
    "dirt",
    "grass",
    "granite",
    "ground",
    "lava",
    "moss",
    "sand",
    "snow",
    "stone",
    "water",
}
NON_TERRAIN_BLOCK_WORDS = {
    "acacia",
    "amethyst",
    "anvil",
    "azalea",
    "barrel",
    "beacon",
    "birch",
    "bookshelf",
    "brewing",
    "bridge",
    "cherry",
    "chest",
    "coal",
    "composter",
    "copper",
    "crafting",
    "crystal",
    "diamond",
    "emerald",
    "furnace",
    "gold",
    "iron",
    "jukebox",
    "ladder",
    "lapis",
    "leaves",
    "log",
    "netherite",
    "oak",
    "ore",
    "planks",
    "prismarine",
    "redstone",
    "smoke",
    "smoker",
    "soul",
    "spruce",
    "terracotta",
    "torch",
    "wall",
    "wood",
}
TRAP_WORDS = {"saw", "spike", "spikes"}


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def slug(text: str) -> str:
    text = text.lower().replace("&", " and ")
    text = re.sub(r"[^a-z0-9]+", "_", text)
    return re.sub(r"_+", "_", text).strip("_") or "asset"


def tokens_from_text(text: str) -> set[str]:
    text = re.sub(r"([a-z])([A-Z])", r"\1 \2", text)
    text = re.sub(r"([A-Za-z])(\d)", r"\1 \2", text)
    text = re.sub(r"(\d)([A-Za-z])", r"\1 \2", text)
    tokens = set(re.findall(r"[a-z0-9]+", text.lower()))
    tokens |= {re.sub(r"\d+$", "", token) for token in tokens if re.sub(r"\d+$", "", token)}
    return tokens


def words_for(path: Path) -> set[str]:
    return tokens_from_text(" ".join([path.stem, *path.parts]))


def lower_parts(path: Path) -> set[str]:
    return {part.lower() for part in path.parts}


def infer_category(path: Path) -> str:
    words = words_for(path)
    parts = lower_parts(path)
    stem_words = tokens_from_text(path.stem)
    if "floors" in parts:
        return "interior_tile"
    if "walls" in parts:
        return "interior_wall"
    if "doors" in parts:
        return "interior_door"
    if "misc props" in parts:
        return "interior_prop"
    if "freepixel_sorted" in parts:
        if "blocks" in parts:
            if stem_words & HEIGHT_MATERIAL_WORDS and not stem_words & NON_TERRAIN_BLOCK_WORDS:
                return "height_block"
            return "material_block"
        if "trees" in parts:
            return "tree_prop"
        if "foliage" in parts:
            return "foliage_prop"
        if "rocks" in parts:
            return "rock_prop"
        if "rpg_weapons_armor" in parts:
            return "item"
        if "rpg_environment" in parts:
            return "prop"
    if "isometric tiles" in parts:
        if stem_words & TRAP_WORDS:
            return "vfx"
        if stem_words & ITEM_WORDS:
            return "item"
        if stem_words & PROP_WORDS:
            return "prop"
        if stem_words & NON_TERRAIN_BLOCK_WORDS:
            return "material_block"
        if stem_words & TERRAIN_SURFACE_WORDS:
            return "terrain_tile"
        if stem_words & TERRAIN_STRUCTURE_WORDS:
            return "height_block"
    if "animated" in words or path.suffix.lower() in ANIM_EXTS:
        return "vfx"
    if words & CHARACTER_WORDS and "isometric" not in words:
        return "character"
    if words & ITEM_WORDS:
        return "item"
    if words & PROP_WORDS:
        return "prop"
    if words & TILE_WORDS or "tiles" in words or "isometric" in words:
        return "tile"
    if words & VFX_WORDS:
        return "vfx"
    return "reference"


def style_terms_for_category(category: str) -> str:
    if category == "terrain_tile":
        return "flat 2:1 isometric terrain surface, strict tile geometry, no object on top"
    if category == "height_block":
        return "isometric raised terrain block or height material, strict geometric faces, no prop on top"
    if category == "material_block":
        return "isometric material block reference, not terrain-safe unless explicitly selected"
    if category == "tile":
        return "2:1 isometric tile, strict tile geometry, no object on top"
    if category == "interior_tile":
        return "dungeon or building interior floor tile, modular strip frame, strict grid geometry"
    if category == "interior_wall":
        return "dungeon or building interior wall tile, modular strip frame, strict grid geometry"
    if category == "interior_door":
        return "dungeon or building interior door tile, modular strip frame, strict grid geometry"
    if category == "interior_prop":
        return "dungeon or building interior prop, modular strip frame, pixel art"
    if category == "prop":
        return "isometric prop, bottom anchored, transparent background"
    if category == "tree_prop":
        return "isometric tree prop, bottom anchored, transparent background"
    if category == "foliage_prop":
        return "isometric foliage prop, bottom anchored, transparent background"
    if category == "rock_prop":
        return "isometric rock prop, bottom anchored, transparent background"
    if category == "item":
        return "pixel item icon, centered, transparent background"
    if category == "character":
        return "pixel character sprite, animation reference, transparent background"
    if category == "vfx":
        return "pixel VFX or animated tile reference"
    return "pixel art reference"


def make_caption(path: Path, category: str, prompt_prefix: str) -> str:
    name = re.sub(r"_strip\d+", "", path.stem, flags=re.IGNORECASE)
    name = re.sub(r"[_\-]+", " ", name).strip()
    parts = [prompt_prefix.strip(), category, name, style_terms_for_category(category)]
    return ", ".join(part for part in parts if part)


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int] | None:
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A")
    return alpha.getbbox()


def strip_count_from_name(path: Path) -> int | None:
    match = re.search(r"_strip(\d+)", path.stem, re.IGNORECASE)
    if not match:
        return None
    value = int(match.group(1))
    return value if value > 0 else None


def extract_source_frames(source: Path, split_strips: bool) -> list[tuple[Image.Image, dict]]:
    image = Image.open(source)
    frame_count = getattr(image, "n_frames", 1)
    strip_count = strip_count_from_name(source) if split_strips else None
    if strip_count and image.width >= strip_count:
        frame_width = max(1, image.width // strip_count)
        rgba = image.convert("RGBA")
        frames = []
        for index in range(strip_count):
            left = index * frame_width
            right = image.width if index == strip_count - 1 else min(image.width, left + frame_width)
            frames.append(
                (
                    rgba.crop((left, 0, right, image.height)),
                    {
                        "source_kind": "strip",
                        "frame_index": index,
                        "frame_count": strip_count,
                        "source_width": image.width,
                        "source_height": image.height,
                    },
                )
            )
        return frames

    image.seek(0)
    return [
        (
            image.convert("RGBA"),
            {
                "source_kind": "image",
                "frame_index": 0,
                "frame_count": frame_count,
                "source_width": image.width,
                "source_height": image.height,
            },
        )
    ]


def normalize_image(image: Image.Image, target: Path, max_size: int, content_size: int, trim: bool, upscale_small: bool) -> dict:
    rgba = image.convert("RGBA")
    original_size = rgba.size
    bbox = alpha_bbox(rgba) if trim else None
    if bbox:
        rgba = rgba.crop(bbox)
    content_size = max(1, min(content_size, max_size))
    pre_scale_size = rgba.size
    scale = min(content_size / max(1, rgba.width), content_size / max(1, rgba.height))
    if scale < 1 or upscale_small:
        next_width = max(1, int(round(rgba.width * scale)))
        next_height = max(1, int(round(rgba.height * scale)))
        rgba = rgba.resize((next_width, next_height), Image.Resampling.NEAREST)
    canvas = Image.new("RGBA", (max_size, max_size), (0, 0, 0, 0))
    canvas.alpha_composite(rgba, ((max_size - rgba.width) // 2, (max_size - rgba.height) // 2))
    target.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(target)
    return {
        "original_width": original_size[0],
        "original_height": original_size[1],
        "trimmed": bool(bbox),
        "content_width": rgba.width,
        "content_height": rgba.height,
        "upscaled_small": bool(upscale_small and scale > 1 and rgba.size != pre_scale_size),
        "normalized_width": max_size,
        "normalized_height": max_size,
    }


def build_contact_sheet(records: list[dict], out_path: Path) -> str | None:
    if not records:
        return None
    columns = 8
    cell_w = 168
    cell_h = 164
    rows = (len(records) + columns - 1) // columns
    sheet = Image.new("RGBA", (columns * cell_w, rows * cell_h), (18, 21, 28, 255))
    draw = ImageDraw.Draw(sheet)
    for index, record in enumerate(records):
        image = Image.open(record["image_path"]).convert("RGBA")
        preview = image.copy()
        preview.thumbnail((120, 120), Image.Resampling.NEAREST)
        x = (index % columns) * cell_w
        y = (index // columns) * cell_h
        bg = Image.new("RGBA", (120, 120), (238, 238, 238, 255))
        bg.alpha_composite(preview, ((120 - preview.width) // 2, (120 - preview.height) // 2))
        sheet.alpha_composite(bg, (x + 24, y + 8))
        label = f"{record['category']} {record['source_name']}"[:28]
        draw.text((x + 6, y + 132), label, fill=(235, 240, 245, 255))
        draw.text((x + 6, y + 146), f"{record['source_width']}x{record['source_height']}", fill=(165, 176, 194, 255))
    out_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out_path)
    return str(out_path)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a trainer-ready dataset from a local PixelArt folder.")
    parser.add_argument("--source", type=Path, default=Path(r"C:\Users\garyc\OneDrive\Desktop\PixelArt"))
    parser.add_argument("--out-dataset", type=Path, default=Path(r"C:\Projects\Pixel Pipeline\datasets\lit_iso\style_lock\pixelart_local_v1"))
    parser.add_argument("--prompt-prefix", default="LIT-ISO style lock, cozy isometric pixel art")
    parser.add_argument("--license", default="user_supplied_pending_license_review")
    parser.add_argument("--author", default="user_supplied_reference_pack")
    parser.add_argument("--max-size", type=int, default=512)
    parser.add_argument("--content-size", type=int, default=448)
    parser.add_argument("--include-gif", action="store_true")
    parser.add_argument("--no-trim", action="store_true")
    parser.add_argument("--no-split-strips", action="store_true")
    parser.add_argument("--no-upscale-small", action="store_true")
    parser.add_argument("--category", action="append", default=[], help="Only include category. Repeatable.")
    parser.add_argument("--replace", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    source = args.source.resolve()
    out = args.out_dataset.resolve()
    categories = set(args.category)
    if not source.exists():
        raise FileNotFoundError(f"Source folder not found: {source}")
    if out.exists() and args.replace:
        shutil.rmtree(out)
    out.mkdir(parents=True, exist_ok=True)
    image_dir = out / "images"
    caption_dir = out / "captions"
    image_dir.mkdir(parents=True, exist_ok=True)
    caption_dir.mkdir(parents=True, exist_ok=True)

    records: list[dict] = []
    skipped = []
    counters = Counter()
    source_counts = defaultdict(int)
    allowed_exts = set(IMAGE_EXTS)
    if args.include_gif:
        allowed_exts |= ANIM_EXTS

    for path in sorted(source.rglob("*")):
        if not path.is_file() or path.suffix.lower() not in allowed_exts:
            continue
        category = infer_category(path)
        if categories and category not in categories:
            skipped.append({"path": str(path), "reason": f"category {category} not selected"})
            continue
        try:
            frames = extract_source_frames(path, split_strips=not args.no_split_strips)
        except Exception as exc:
            skipped.append({"path": str(path), "reason": str(exc)})
            continue

        for frame, frame_info in frames:
            source_counts[path.stem] += 1
            suffix = "" if source_counts[path.stem] == 1 else f"_{source_counts[path.stem]:02d}"
            name = f"{category}_{slug(path.stem)}{suffix}.png"
            target = image_dir / name
            try:
                info = normalize_image(
                    frame,
                    target,
                    args.max_size,
                    args.content_size,
                    trim=not args.no_trim,
                    upscale_small=not args.no_upscale_small,
                )
            except Exception as exc:
                skipped.append({"path": str(path), "reason": str(exc), "frame": frame_info.get("frame_index")})
                continue
            caption = make_caption(path, category, args.prompt_prefix)
            if frame_info["frame_count"] > 1:
                caption = f"{caption}, frame {frame_info['frame_index'] + 1} of {frame_info['frame_count']}"
            caption_path = caption_dir / f"{target.stem}.txt"
            caption_path.write_text(caption, encoding="utf-8")
            record = {
                "file_name": f"images/{target.name}",
                "text": caption,
                "category": category,
                "source_path": str(path),
                "source_name": path.name,
                "relative_source": str(path.relative_to(source)),
                "caption_path": f"captions/{caption_path.name}",
                "image_path": str(target),
                "license": args.license,
                "author": args.author,
                "source_width": frame_info["source_width"],
                "source_height": frame_info["source_height"],
                "source_frames": frame_info["frame_count"],
                "source_frame_index": frame_info["frame_index"],
                "source_kind": frame_info["source_kind"],
                "frame_width": info["original_width"],
                "frame_height": info["original_height"],
                "content_width": info["content_width"],
                "content_height": info["content_height"],
                "upscaled_small": info["upscaled_small"],
                "normalized_width": info["normalized_width"],
                "normalized_height": info["normalized_height"],
            }
            records.append(record)
            counters[category] += 1

    metadata_path = out / "metadata.jsonl"
    metadata_path.write_text("\n".join(json.dumps(record, ensure_ascii=False) for record in records) + ("\n" if records else ""), encoding="utf-8")
    manifest = {
        "schema": "lit_iso.asset_forge.pixelart_style_dataset.v1",
        "created_utc": utc_now(),
        "source": str(source),
        "out_dataset": str(out),
        "record_count": len(records),
        "by_category": dict(sorted(counters.items())),
        "license": args.license,
        "author": args.author,
        "prompt_prefix": args.prompt_prefix,
        "max_size": args.max_size,
        "content_size": args.content_size,
        "upscale_small": not args.no_upscale_small,
        "metadata_jsonl": str(metadata_path),
        "contact_sheet": build_contact_sheet(records, out / "contact_sheet.png"),
        "skipped": skipped[:200],
        "skipped_count": len(skipped),
        "training_note": "Use --category tile for tile-style LoRA experiments; verify license before shipping or sharing trained derivatives.",
    }
    (out / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps({"dataset": str(out), "records": len(records), "by_category": manifest["by_category"], "contact_sheet": manifest["contact_sheet"]}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
