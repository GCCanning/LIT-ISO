#!/usr/bin/env python3
"""
Recover individual sprite frames from sheet-like AI outputs.

The PixelartSpritesheet checkpoint often generates several tiny repeated sprites
inside one 512x512 image. This tool turns those generated sheets into separate
transparent, bottom-center anchored cells so they can be reviewed or captured as
training material.
"""

from __future__ import annotations

import argparse
import json
import math
import statistics
import sys
from collections import deque
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageDraw

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def color_distance(a: tuple[int, int, int], b: tuple[int, int, int]) -> float:
    return math.sqrt(sum((int(a[i]) - int(b[i])) ** 2 for i in range(3)))


def estimate_background(image: Image.Image) -> tuple[int, int, int]:
    rgb = image.convert("RGB")
    w, h = rgb.size
    samples: list[tuple[int, int, int]] = []
    band = max(4, min(w, h) // 24)
    for x in range(w):
        for y in range(band):
            samples.append(rgb.getpixel((x, y)))
            samples.append(rgb.getpixel((x, h - 1 - y)))
    for y in range(h):
        for x in range(band):
            samples.append(rgb.getpixel((x, y)))
            samples.append(rgb.getpixel((w - 1 - x, y)))

    quantized: dict[tuple[int, int, int], int] = {}
    for r, g, b in samples:
        key = (round(r / 8) * 8, round(g / 8) * 8, round(b / 8) * 8)
        quantized[key] = quantized.get(key, 0) + 1
    return max(quantized.items(), key=lambda item: item[1])[0]


def build_mask(image: Image.Image, bg: tuple[int, int, int], threshold: float) -> list[list[bool]]:
    rgb = image.convert("RGB")
    w, h = rgb.size
    mask = [[False for _ in range(w)] for _ in range(h)]
    for y in range(h):
        for x in range(w):
            if color_distance(rgb.getpixel((x, y)), bg) >= threshold:
                mask[y][x] = True
    return mask


def close_mask(mask: list[list[bool]], radius: int = 1) -> list[list[bool]]:
    if radius <= 0:
        return mask
    h = len(mask)
    w = len(mask[0]) if h else 0
    dilated = [[False for _ in range(w)] for _ in range(h)]
    for y in range(h):
        for x in range(w):
            if not mask[y][x]:
                continue
            for yy in range(max(0, y - radius), min(h, y + radius + 1)):
                for xx in range(max(0, x - radius), min(w, x + radius + 1)):
                    dilated[yy][xx] = True
    eroded = [[False for _ in range(w)] for _ in range(h)]
    for y in range(h):
        for x in range(w):
            keep = True
            for yy in range(max(0, y - radius), min(h, y + radius + 1)):
                for xx in range(max(0, x - radius), min(w, x + radius + 1)):
                    if not dilated[yy][xx]:
                        keep = False
                        break
                if not keep:
                    break
            eroded[y][x] = keep
    return eroded


def connected_components(mask: list[list[bool]], min_area: int) -> list[tuple[int, int, int, int, int]]:
    h = len(mask)
    w = len(mask[0]) if h else 0
    seen = [[False for _ in range(w)] for _ in range(h)]
    components: list[tuple[int, int, int, int, int]] = []
    for y in range(h):
        for x in range(w):
            if seen[y][x] or not mask[y][x]:
                continue
            queue: deque[tuple[int, int]] = deque([(x, y)])
            seen[y][x] = True
            min_x = max_x = x
            min_y = max_y = y
            area = 0
            while queue:
                cx, cy = queue.popleft()
                area += 1
                min_x = min(min_x, cx)
                max_x = max(max_x, cx)
                min_y = min(min_y, cy)
                max_y = max(max_y, cy)
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if nx < 0 or ny < 0 or nx >= w or ny >= h:
                        continue
                    if seen[ny][nx] or not mask[ny][nx]:
                        continue
                    seen[ny][nx] = True
                    queue.append((nx, ny))
            if area >= min_area:
                components.append((min_x, min_y, max_x, max_y, area))
    return components


def group_columns(mask: list[list[bool]], min_pixels_per_column: int, gap: int) -> list[tuple[int, int]]:
    h = len(mask)
    w = len(mask[0]) if h else 0
    active = []
    for x in range(w):
        count = sum(1 for y in range(h) if mask[y][x])
        active.append(count >= min_pixels_per_column)

    groups: list[tuple[int, int]] = []
    start: int | None = None
    last = -1
    gap_count = 0
    for x, is_active in enumerate(active):
        if is_active:
            if start is None:
                start = x
            last = x
            gap_count = 0
        elif start is not None:
            gap_count += 1
            if gap_count > gap:
                groups.append((start, last))
                start = None
                gap_count = 0
    if start is not None:
        groups.append((start, last))
    return groups


def bbox_for_column_group(mask: list[list[bool]], x0: int, x1: int, pad: int) -> tuple[int, int, int, int] | None:
    h = len(mask)
    w = len(mask[0]) if h else 0
    ys = []
    xs = []
    for y in range(h):
        for x in range(max(0, x0), min(w, x1 + 1)):
            if mask[y][x]:
                xs.append(x)
                ys.append(y)
    if not xs:
        return None
    return (
        max(0, min(xs) - pad),
        max(0, min(ys) - pad),
        min(w - 1, max(xs) + pad),
        min(h - 1, max(ys) + pad),
    )


def choose_bboxes(mask: list[list[bool]], args: argparse.Namespace) -> list[tuple[int, int, int, int]]:
    groups = group_columns(mask, args.min_pixels_per_column, args.column_gap)
    bboxes = []
    for x0, x1 in groups:
        bbox = bbox_for_column_group(mask, x0, x1, args.pad)
        if not bbox:
            continue
        bw = bbox[2] - bbox[0] + 1
        bh = bbox[3] - bbox[1] + 1
        if bw >= args.min_frame_width and bh >= args.min_frame_height:
            bboxes.append(bbox)

    if len(bboxes) >= 2:
        return bboxes

    components = connected_components(mask, args.min_component_area)
    out = []
    for x0, y0, x1, y1, _area in components:
        bw = x1 - x0 + 1
        bh = y1 - y0 + 1
        if bw >= args.min_frame_width and bh >= args.min_frame_height:
            out.append((max(0, x0 - args.pad), max(0, y0 - args.pad), x1 + args.pad, y1 + args.pad))
    return sorted(out, key=lambda box: (box[0], box[1]))


def crop_transparent(image: Image.Image, mask: list[list[bool]], bbox: tuple[int, int, int, int]) -> Image.Image:
    rgb = image.convert("RGB")
    x0, y0, x1, y1 = bbox
    crop = Image.new("RGBA", (x1 - x0 + 1, y1 - y0 + 1), (0, 0, 0, 0))
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if mask[y][x]:
                crop.putpixel((x - x0, y - y0), (*rgb.getpixel((x, y)), 255))
    return crop


def normalize_cell(frame: Image.Image, cell_size: int, max_occupancy: float) -> Image.Image:
    bbox = frame.getbbox()
    if bbox is None:
        return Image.new("RGBA", (cell_size, cell_size), (0, 0, 0, 0))
    content = frame.crop(bbox)
    max_w = max(1, int(cell_size * max_occupancy))
    max_h = max(1, int(cell_size * max_occupancy))
    scale = min(max_w / content.width, max_h / content.height, 1.0)
    new_size = (max(1, round(content.width * scale)), max(1, round(content.height * scale)))
    if new_size != content.size:
        content = content.resize(new_size, Image.Resampling.NEAREST)
    cell = Image.new("RGBA", (cell_size, cell_size), (0, 0, 0, 0))
    x = (cell_size - content.width) // 2
    y = cell_size - content.height
    cell.alpha_composite(content, (x, y))
    return cell


def load_source_manifest(path: Path | None) -> dict:
    if not path or not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8"))


def caption_for(source_manifest: dict, image_name: str) -> str | None:
    for result in source_manifest.get("results", []):
        outputs = [Path(p).name for p in result.get("outputs", [])]
        if image_name in outputs:
            return result.get("caption")
    return None


def write_contact_sheet(out_dir: Path, frames: list[Path], columns: int = 8) -> Path | None:
    if not frames:
        return None
    cell = 96
    label_h = 28
    rows = (len(frames) + columns - 1) // columns
    sheet = Image.new("RGBA", (columns * cell, rows * (cell + label_h)), (16, 18, 24, 255))
    draw = ImageDraw.Draw(sheet)
    for index, path in enumerate(frames):
        image = Image.open(path).convert("RGBA")
        preview = image.resize((cell, cell), Image.Resampling.NEAREST)
        x = (index % columns) * cell
        y = (index // columns) * (cell + label_h)
        checker = Image.new("RGBA", (cell, cell), (238, 238, 238, 255))
        checker.alpha_composite(preview)
        sheet.alpha_composite(checker, (x, y))
        draw.text((x + 3, y + cell + 4), path.stem[-18:], fill=(230, 235, 245, 255))
    out = out_dir / "recovered_frame_contact_sheet.png"
    sheet.save(out)
    return out


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Recover sprite frames from sheet-like AI output PNGs.")
    parser.add_argument("--input-dir", type=Path, required=True)
    parser.add_argument("--out-dir", type=Path, required=True)
    parser.add_argument("--source-manifest", type=Path)
    parser.add_argument("--cell-size", type=int, default=64)
    parser.add_argument("--threshold", type=float, default=34.0)
    parser.add_argument("--min-component-area", type=int, default=80)
    parser.add_argument("--min-frame-width", type=int, default=18)
    parser.add_argument("--min-frame-height", type=int, default=28)
    parser.add_argument("--min-pixels-per-column", type=int, default=8)
    parser.add_argument("--column-gap", type=int, default=12)
    parser.add_argument("--pad", type=int, default=2)
    parser.add_argument("--max-occupancy", type=float, default=0.9)
    parser.add_argument("--include", default="*.png")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    args.out_dir.mkdir(parents=True, exist_ok=True)
    frame_dir = args.out_dir / "frames"
    caption_dir = args.out_dir / "captions"
    raw_crop_dir = args.out_dir / "raw_crops"
    frame_dir.mkdir(parents=True, exist_ok=True)
    caption_dir.mkdir(parents=True, exist_ok=True)
    raw_crop_dir.mkdir(parents=True, exist_ok=True)

    source_manifest = load_source_manifest(args.source_manifest)
    source_images = [
        path
        for path in sorted(args.input_dir.glob(args.include))
        if "contact" not in path.stem.lower() and path.is_file()
    ]

    records = []
    recovered_paths: list[Path] = []
    for source in source_images:
        image = Image.open(source).convert("RGB")
        bg = estimate_background(image)
        mask = close_mask(build_mask(image, bg, args.threshold), radius=1)
        bboxes = choose_bboxes(mask, args)
        caption = caption_for(source_manifest, source.name)
        for index, bbox in enumerate(bboxes):
            crop = crop_transparent(image, mask, bbox)
            cell = normalize_cell(crop, args.cell_size, args.max_occupancy)
            frame_name = f"{source.stem}__recovered_{index:02d}.png"
            raw_name = f"{source.stem}__rawcrop_{index:02d}.png"
            caption_name = f"{source.stem}__recovered_{index:02d}.txt"
            raw_path = raw_crop_dir / raw_name
            frame_path = frame_dir / frame_name
            caption_path = caption_dir / caption_name
            crop.save(raw_path)
            cell.save(frame_path)
            if caption:
                caption_path.write_text(f"{caption}, recovered sprite candidate {index + 1}", encoding="utf-8")
            recovered_paths.append(frame_path)
            records.append(
                {
                    "source": str(source),
                    "source_name": source.name,
                    "frame_index": index,
                    "bbox": list(bbox),
                    "background_rgb": list(bg),
                    "raw_crop": str(raw_path),
                    "normalized_frame": str(frame_path),
                    "caption_file": str(caption_path) if caption else None,
                    "caption": caption,
                }
            )

    contact = write_contact_sheet(args.out_dir, recovered_paths)
    manifest = {
        "schemaVersion": 1,
        "generated_utc": utc_now(),
        "status": "complete",
        "input_dir": str(args.input_dir),
        "source_manifest": str(args.source_manifest) if args.source_manifest else None,
        "out_dir": str(args.out_dir),
        "cell_size": args.cell_size,
        "threshold": args.threshold,
        "source_image_count": len(source_images),
        "recovered_frame_count": len(recovered_paths),
        "contact_sheet": str(contact) if contact else None,
        "records": records,
        "review_guidance": [
            "Approve only frames that are a single readable sprite with transparent background.",
            "Reject frames with merged duplicates, portraits, scenery, or unusable tool/body distortion.",
            "This is a salvage/review step; do not promote recovered frames to runtime art without approval.",
        ],
    }
    (args.out_dir / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps({"out_dir": str(args.out_dir), "frames": len(recovered_paths), "manifest": str(args.out_dir / "manifest.json")}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
