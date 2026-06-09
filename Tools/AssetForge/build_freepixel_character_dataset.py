#!/usr/bin/env python3
"""
Build a LoRA-ready character/mob/NPC dataset from the local FreePixel character scrape.

This keeps character sprite data out of Unity's Assets folder. It intentionally
does not mix character sheets into tile/material datasets.
"""

from __future__ import annotations

import argparse
import json
import re
import shutil
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw


DIRECTION_WORDS = {
    "south",
    "south-east",
    "east",
    "north-east",
    "north",
    "north-west",
    "west",
    "south-west",
}


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def slug(value: str) -> str:
    value = value.lower().replace("&", " and ")
    value = re.sub(r"[^a-z0-9]+", "_", value)
    return re.sub(r"_+", "_", value).strip("_") or "asset"


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int] | None:
    return image.convert("RGBA").getchannel("A").getbbox()


def visible_alpha_count(image: Image.Image) -> int:
    alpha = image.convert("RGBA").getchannel("A")
    return sum(1 for value in alpha.getdata() if value > 16)


def classify_asset_mode(type_group: str) -> str:
    group = type_group.lower()
    if "creatures" in group or "monsters" in group:
        return "mob"
    if "npc" in group or "townsfolk" in group or "royalty" in group:
        return "npc"
    if "portrait" in group or "page" in group:
        return "reference"
    return "character"


def split_frames(image: Image.Image, cell_size: int, split_sheets: bool) -> list[tuple[Image.Image, dict[str, Any]]]:
    rgba = image.convert("RGBA")
    if not split_sheets or rgba.width < cell_size or rgba.height < cell_size:
        return [(rgba, {"source_kind": "image", "row": 0, "column": 0, "rows": 1, "columns": 1})]
    if rgba.width % cell_size != 0 or rgba.height % cell_size != 0:
        return [(rgba, {"source_kind": "image", "row": 0, "column": 0, "rows": 1, "columns": 1})]

    columns = rgba.width // cell_size
    rows = rgba.height // cell_size
    frames: list[tuple[Image.Image, dict[str, Any]]] = []
    for row in range(rows):
        for column in range(columns):
            left = column * cell_size
            top = row * cell_size
            frame = rgba.crop((left, top, left + cell_size, top + cell_size))
            frames.append((frame, {"source_kind": "sheet_cell", "row": row, "column": column, "rows": rows, "columns": columns}))
    return frames


def normalize_image(image: Image.Image, target: Path, max_size: int, content_size: int, trim: bool) -> dict[str, Any]:
    rgba = image.convert("RGBA")
    original_size = rgba.size
    bbox = alpha_bbox(rgba) if trim else None
    if bbox:
        rgba = rgba.crop(bbox)
    pre_scale_size = rgba.size
    content_size = max(1, min(max_size, content_size))
    scale = min(content_size / max(1, rgba.width), content_size / max(1, rgba.height))
    next_width = max(1, int(round(rgba.width * scale)))
    next_height = max(1, int(round(rgba.height * scale)))
    rgba = rgba.resize((next_width, next_height), Image.Resampling.NEAREST)
    canvas = Image.new("RGBA", (max_size, max_size), (0, 0, 0, 0))
    canvas.alpha_composite(rgba, ((max_size - rgba.width) // 2, (max_size - rgba.height) // 2))
    target.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(target)
    return {
        "source_width": original_size[0],
        "source_height": original_size[1],
        "cropped_width": pre_scale_size[0],
        "cropped_height": pre_scale_size[1],
        "content_width": rgba.width,
        "content_height": rgba.height,
        "normalized_width": max_size,
        "normalized_height": max_size,
        "trimmed": bool(bbox),
    }


def make_caption(record: dict[str, Any], mode: str, frame_info: dict[str, Any], prefix: str) -> str:
    display = str(record.get("DisplayName") or record.get("CharacterSlug") or "character").replace("-", " ")
    group = str(record.get("TypeGroup") or "").replace("_", " ")
    animation = str(record.get("Animation") or "static").replace("_", " ").replace("-", " ")
    source_kind = str(record.get("AssetKind") or "SpriteSheet")
    parts = [
        prefix,
        mode,
        display,
        group,
        f"{animation} animation reference",
        "isometric pixel sprite sheet source" if source_kind.lower() == "spritesheet" else "pixel sprite reference",
        f"cell row {frame_info['row'] + 1} column {frame_info['column'] + 1}",
        "transparent background",
        "hard pixel edges",
    ]
    if animation in DIRECTION_WORDS:
        parts.append(f"facing {animation}")
    return ", ".join(part for part in parts if part)


def build_contact_sheet(records: list[dict[str, Any]], out_path: Path) -> str | None:
    if not records:
        return None
    columns = 8
    cell_w = 168
    cell_h = 168
    rows = (min(len(records), 256) + columns - 1) // columns
    sheet = Image.new("RGBA", (columns * cell_w, rows * cell_h), (18, 21, 28, 255))
    draw = ImageDraw.Draw(sheet)
    for index, record in enumerate(records[:256]):
        image = Image.open(record["image_path"]).convert("RGBA")
        preview = image.copy()
        preview.thumbnail((120, 120), Image.Resampling.NEAREST)
        x = (index % columns) * cell_w
        y = (index // columns) * cell_h
        bg = Image.new("RGBA", (120, 120), (238, 238, 238, 255))
        bg.alpha_composite(preview, ((120 - preview.width) // 2, (120 - preview.height) // 2))
        sheet.alpha_composite(bg, (x + 24, y + 8))
        label = f"{record['asset_mode']} {record['character_slug']} {record['animation']}"[:30]
        draw.text((x + 6, y + 132), label, fill=(235, 240, 245, 255))
        draw.text((x + 6, y + 148), f"r{record['row'] + 1} c{record['column'] + 1}", fill=(165, 176, 194, 255))
    out_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out_path)
    return str(out_path)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a dataset from FreePixel character scrape metadata.")
    parser.add_argument("--source", type=Path, default=Path(r"C:\Users\garyc\OneDrive\Desktop\PixelArt\FreePixel_Characters"))
    parser.add_argument("--out-dataset", type=Path, default=Path(r"C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\freepixel_characters_v1"))
    parser.add_argument("--metadata", type=Path, default=None)
    parser.add_argument("--prompt-prefix", default="LIT-ISO character style reference, cozy isometric pixel art")
    parser.add_argument("--license", default="freepixel_reference_pending_license_review")
    parser.add_argument("--author", default="FreePixel")
    parser.add_argument("--cell-size", type=int, default=128)
    parser.add_argument("--max-size", type=int, default=512)
    parser.add_argument("--content-size", type=int, default=448)
    parser.add_argument("--min-alpha-pixels", type=int, default=24)
    parser.add_argument("--include-reference", action="store_true")
    parser.add_argument("--no-split-sheets", action="store_true")
    parser.add_argument("--replace", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    source = args.source.resolve()
    metadata_path = args.metadata.resolve() if args.metadata else source / "_freepixel_character_metadata.json"
    out = args.out_dataset.resolve()
    if not metadata_path.exists():
        raise FileNotFoundError(f"Missing FreePixel metadata: {metadata_path}")
    if out.exists() and args.replace:
        shutil.rmtree(out)
    out.mkdir(parents=True, exist_ok=True)
    image_root = out / "images"
    caption_root = out / "captions"
    image_root.mkdir(parents=True, exist_ok=True)
    caption_root.mkdir(parents=True, exist_ok=True)

    metadata = json.loads(metadata_path.read_text(encoding="utf-8-sig"))
    records: list[dict[str, Any]] = []
    skipped: list[dict[str, Any]] = []
    counts = Counter()
    action_counts = Counter()
    character_counts = defaultdict(int)

    for entry in metadata:
        if str(entry.get("Status", "")).upper() != "OK":
            skipped.append({"entry": entry, "reason": "status not OK"})
            continue
        mode = classify_asset_mode(str(entry.get("TypeGroup", "")))
        if mode == "reference" and not args.include_reference:
            skipped.append({"entry": entry, "reason": "reference/portrait excluded"})
            continue
        path = Path(str(entry.get("LocalPath", "")))
        if not path.exists():
            skipped.append({"entry": entry, "reason": f"missing local path: {path}"})
            continue
        try:
            image = Image.open(path).convert("RGBA")
            frames = split_frames(image, args.cell_size, split_sheets=not args.no_split_sheets)
        except Exception as exc:
            skipped.append({"entry": entry, "reason": str(exc)})
            continue

        character_slug = slug(str(entry.get("CharacterSlug") or path.parent.name))
        animation = slug(str(entry.get("Animation") or path.stem))
        for frame, frame_info in frames:
            if visible_alpha_count(frame) < args.min_alpha_pixels:
                continue
            character_counts[(character_slug, animation)] += 1
            suffix = f"{character_counts[(character_slug, animation)]:03d}"
            target = image_root / f"{mode}_{character_slug}_{animation}_{suffix}.png"
            try:
                info = normalize_image(frame, target, args.max_size, args.content_size, trim=True)
            except Exception as exc:
                skipped.append({"entry": entry, "reason": str(exc), "frame": frame_info})
                continue
            caption = make_caption(entry, mode, frame_info, args.prompt_prefix)
            caption_path = caption_root / f"{target.stem}.txt"
            caption_path.write_text(caption, encoding="utf-8")
            record = {
                "file_name": f"images/{target.name}",
                "text": caption,
                "asset_mode": mode,
                "category": mode,
                "type_group": entry.get("TypeGroup"),
                "character_slug": character_slug,
                "display_name": entry.get("DisplayName"),
                "animation": animation,
                "asset_kind": entry.get("AssetKind"),
                "row": frame_info["row"],
                "column": frame_info["column"],
                "rows": frame_info["rows"],
                "columns": frame_info["columns"],
                "source_path": str(path),
                "source_url": entry.get("SourceUrl"),
                "source_page": entry.get("SourcePage"),
                "source_relative_path": entry.get("SourceRelativePath"),
                "image_path": str(target),
                "caption_path": f"captions/{caption_path.name}",
                "license": args.license,
                "author": args.author,
                **info,
            }
            records.append(record)
            counts[mode] += 1
            action_counts[animation] += 1

    metadata_jsonl = out / "metadata.jsonl"
    metadata_jsonl.write_text("\n".join(json.dumps(record, ensure_ascii=False) for record in records) + ("\n" if records else ""), encoding="utf-8")
    contact = build_contact_sheet(records, out / "contact_sheet.png")
    manifest = {
        "schema": "lit_iso.asset_forge.freepixel_character_dataset.v1",
        "created_utc": utc_now(),
        "source": str(source),
        "metadata": str(metadata_path),
        "out_dataset": str(out),
        "record_count": len(records),
        "by_asset_mode": dict(sorted(counts.items())),
        "by_animation": dict(sorted(action_counts.items())),
        "contact_sheet": contact,
        "metadata_jsonl": str(metadata_jsonl),
        "license": args.license,
        "author": args.author,
        "training_note": "Use as character/mob/NPC sprite style and motion data only. Do not mix into terrain/tile LoRAs.",
        "skipped_count": len(skipped),
        "skipped": skipped[:200],
    }
    (out / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps({"dataset": str(out), "records": len(records), "by_asset_mode": manifest["by_asset_mode"], "contact_sheet": contact}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
