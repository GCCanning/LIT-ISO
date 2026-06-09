#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageDraw

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


IMAGE_EXTS = {".png", ".jpg", ".jpeg", ".webp"}


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def classify_pack(pack_name: str, records: list[dict]) -> dict:
    lower = pack_name.lower()
    dimensions = Counter(f"{r['width']}x{r['height']}" for r in records)
    square_ratio = sum(1 for r in records if r["width"] == r["height"]) / len(records) if records else 0
    wide_ratio = sum(1 for r in records if r["width"] > r["height"] * 1.5) / len(records) if records else 0
    transparent_ratio = sum(1 for r in records if r["has_alpha"]) / len(records) if records else 0

    if "tileset" in lower:
        lane = "tile_reference"
        iso = "needs_manual_verification"
    elif "animation" in lower or "characters" in lower or "enemies" in lower:
        lane = "character_or_mob_reference"
        iso = "useful_for_sprite_reference_not_confirmed_8d"
    elif "environment" in lower:
        lane = "prop_reference"
        iso = "medium"
    elif "items" in lower or "weapons" in lower or "armor" in lower:
        lane = "item_icon_reference"
        iso = "low_to_medium"
    elif "ui" in lower:
        lane = "ui_reference"
        iso = "not_applicable"
    else:
        lane = "mixed_reference"
        iso = "needs_manual_verification"

    return {
        "lane": lane,
        "isometric_relevance": iso,
        "image_count": len(records),
        "transparent_ratio": round(transparent_ratio, 3),
        "square_ratio": round(square_ratio, 3),
        "wide_sheet_ratio": round(wide_ratio, 3),
        "top_dimensions": dimensions.most_common(12),
        "training_use": "blocked_until_explicit_ai_training_permission",
        "runtime_use": "blocked_until_license_and_art_direction_review",
    }


def inspect_image(path: Path, root: Path) -> dict | None:
    try:
        image = Image.open(path)
        image.load()
    except Exception:
        return None
    has_alpha = image.mode in ("RGBA", "LA") or ("transparency" in image.info)
    return {
        "path": str(path),
        "relative_path": str(path.relative_to(root)).replace("\\", "/"),
        "width": image.width,
        "height": image.height,
        "mode": image.mode,
        "has_alpha": has_alpha,
    }


def make_contact_sheet(pack_name: str, records: list[dict], out_dir: Path, max_images: int = 80) -> str | None:
    if not records:
        return None
    images = records[:max_images]
    columns = 10
    cell = 96
    label_h = 26
    title_h = 34
    rows = (len(images) + columns - 1) // columns
    sheet = Image.new("RGBA", (columns * cell, title_h + rows * (cell + label_h)), (16, 18, 24, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((8, 8), pack_name[:100], fill=(235, 240, 245, 255))
    for index, record in enumerate(images):
        path = Path(record["path"])
        image = Image.open(path).convert("RGBA")
        image.thumbnail((cell - 8, cell - 8), Image.Resampling.NEAREST)
        x = (index % columns) * cell
        y = title_h + (index // columns) * (cell + label_h)
        bg = Image.new("RGBA", (cell, cell), (238, 238, 238, 255))
        bg.alpha_composite(image, ((cell - image.width) // 2, (cell - image.height) // 2))
        sheet.alpha_composite(bg, (x, y))
        draw.text((x + 3, y + cell + 3), Path(record["relative_path"]).stem[:16], fill=(230, 235, 245, 255))
    out_dir.mkdir(parents=True, exist_ok=True)
    out = out_dir / f"{pack_name}_contact_sheet.png"
    sheet.save(out)
    return str(out)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", type=Path, default=Path(r"C:\Projects\Pixel Pipeline\sources\freepixel\extracted"))
    parser.add_argument("--out-root", type=Path, default=Path(r"C:\Projects\Pixel Pipeline\generated\freepixel_inventory"))
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    args.out_root.mkdir(parents=True, exist_ok=True)
    pack_summaries = []
    for pack_dir in sorted([p for p in args.source_root.iterdir() if p.is_dir()]):
        records = []
        for path in sorted(pack_dir.rglob("*")):
            if path.suffix.lower() not in IMAGE_EXTS:
                continue
            rec = inspect_image(path, pack_dir)
            if rec:
                records.append(rec)
        pack_out = args.out_root / pack_dir.name
        pack_out.mkdir(parents=True, exist_ok=True)
        classification = classify_pack(pack_dir.name, records)
        contact = make_contact_sheet(pack_dir.name, records, pack_out)
        manifest = {
            "schemaVersion": 1,
            "generated_utc": utc_now(),
            "pack": pack_dir.name,
            "source_dir": str(pack_dir),
            "classification": classification,
            "contact_sheet": contact,
            "records": records,
        }
        (pack_out / "inventory_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        pack_summaries.append({
            "pack": pack_dir.name,
            "source_dir": str(pack_dir),
            "contact_sheet": contact,
            **classification,
        })

    summary = {
        "schemaVersion": 1,
        "generated_utc": utc_now(),
        "source_root": str(args.source_root),
        "out_root": str(args.out_root),
        "pack_count": len(pack_summaries),
        "packs": pack_summaries,
    }
    (args.out_root / "freepixel_inventory_summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps({"out_root": str(args.out_root), "packs": len(pack_summaries), "summary": str(args.out_root / "freepixel_inventory_summary.json")}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
