#!/usr/bin/env python3
"""
Build a narrow Sprixen-style FreePixel frame dataset for LIT-ISO LoRA training.

This is intentionally stricter than freepixel_structured_dataset.py:
  - consumes already sliced directional FreePixel frames
  - keeps only trustworthy 8-direction rows
  - removes edge backgrounds again
  - normalizes each subject into a small centered transparent 128x128 cell
  - writes tighter captions focused on single-frame character sprites

Run:
  python Tools\\LoRA\\freepixel_sprixen_frame_dataset.py
"""

from __future__ import annotations

import argparse
import json
import math
import sys
from collections import Counter, deque
from pathlib import Path

from PIL import Image

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")

DEFAULT_SOURCE_DIR = Path(r"C:\Projects\Pixel Pipeline\style_examples\freepixel_web_download_structured")
DEFAULT_OUTPUT_DIR = Path(r"C:\Projects\Pixel Pipeline\style_examples\freepixel_sprixen_frame_only")

DIRECTION_ORDER = [
    "south",
    "south-east",
    "east",
    "north-east",
    "north",
    "north-west",
    "west",
    "south-west",
]

KEEP_ACTIONS = {
    "idle",
    "walk",
    "run",
    "attack",
    "hurt",
    "death",
    "die",
    "cast",
    "gather",
    "mine",
    "chop",
}

ACTION_ALIASES = {
    "die": "death",
}


def color_distance_sq(a: tuple[int, int, int, int], b: tuple[int, int, int, int]) -> int:
    return (a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2 + (a[2] - b[2]) ** 2


def edge_background_color(image: Image.Image) -> tuple[int, int, int, int]:
    rgba = image.convert("RGBA")
    width, height = rgba.size
    pixels = []
    for x in range(width):
        pixels.append(rgba.getpixel((x, 0)))
        pixels.append(rgba.getpixel((x, height - 1)))
    for y in range(height):
        pixels.append(rgba.getpixel((0, y)))
        pixels.append(rgba.getpixel((width - 1, y)))

    buckets: Counter[tuple[int, int, int]] = Counter()
    for r, g, b, a in pixels:
        if a < 16:
            continue
        buckets[(round(r / 16), round(g / 16), round(b / 16))] += 1
    if not buckets:
        return (0, 0, 0, 0)
    r, g, b = buckets.most_common(1)[0][0]
    return (r * 16, g * 16, b * 16, 255)


def remove_edge_bg(image: Image.Image, tolerance: int = 26) -> Image.Image:
    rgba = image.convert("RGBA")
    width, height = rgba.size
    data = rgba.load()
    bg = edge_background_color(rgba)
    tolerance_sq = tolerance * tolerance
    queue: deque[tuple[int, int]] = deque()
    visited: set[tuple[int, int]] = set()

    for x in range(width):
        queue.append((x, 0))
        queue.append((x, height - 1))
    for y in range(height):
        queue.append((0, y))
        queue.append((width - 1, y))

    while queue:
        x, y = queue.popleft()
        if x < 0 or y < 0 or x >= width or y >= height or (x, y) in visited:
            continue
        visited.add((x, y))
        px = data[x, y]
        if px[3] < 16 or color_distance_sq(px, bg) <= tolerance_sq:
            data[x, y] = (px[0], px[1], px[2], 0)
            queue.append((x + 1, y))
            queue.append((x - 1, y))
            queue.append((x, y + 1))
            queue.append((x, y - 1))

    return rgba


def foreground_bounds(image: Image.Image) -> tuple[int, int, int, int] | None:
    alpha = image.getchannel("A")
    box = alpha.getbbox()
    if not box:
        return None
    return box


def count_components(image: Image.Image, min_pixels: int = 12) -> int:
    alpha = image.getchannel("A")
    width, height = alpha.size
    data = alpha.load()
    visited = bytearray(width * height)
    components = 0
    for y in range(height):
        for x in range(width):
            idx = y * width + x
            if visited[idx] or data[x, y] <= 16:
                continue
            pixels = 0
            queue = deque([(x, y)])
            visited[idx] = 1
            while queue:
                cx, cy = queue.popleft()
                pixels += 1
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if nx < 0 or ny < 0 or nx >= width or ny >= height:
                        continue
                    nidx = ny * width + nx
                    if visited[nidx] or data[nx, ny] <= 16:
                        continue
                    visited[nidx] = 1
                    queue.append((nx, ny))
            if pixels >= min_pixels:
                components += 1
    return components


def normalize_sprite(
    image: Image.Image,
    canvas_size: int,
    target_height: int,
    foot_y: int,
) -> tuple[Image.Image, dict] | None:
    clean = remove_edge_bg(image)
    box = foreground_bounds(clean)
    if not box:
        return None
    x0, y0, x1, y1 = box
    width = x1 - x0
    height = y1 - y0
    if width < 8 or height < 12:
        return None

    scale = min(1.8, target_height / max(1, height))
    draw_w = max(1, round(width * scale))
    draw_h = max(1, round(height * scale))
    if draw_w > canvas_size * 0.82 or draw_h > canvas_size * 0.88:
        return None

    subject = clean.crop(box).resize((draw_w, draw_h), Image.Resampling.NEAREST)
    output = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))
    draw_x = round(canvas_size / 2 - draw_w / 2)
    draw_y = round(foot_y - draw_h)
    if draw_x < 0 or draw_y < 0 or draw_x + draw_w > canvas_size or draw_y + draw_h > canvas_size:
        return None
    output.alpha_composite(subject, (draw_x, draw_y))

    components = count_components(output)
    stats = {
        "source_bounds": [x0, y0, x1, y1],
        "source_size": [width, height],
        "draw_size": [draw_w, draw_h],
        "scale": round(scale, 4),
        "component_count": components,
        "foreground_pixels": sum(1 for value in output.getchannel("A").getdata() if value > 16),
    }
    return output, stats


def caption_for(item: dict, action: str, direction: str) -> str:
    character = item.get("character") or item.get("slug", "character")
    category = item.get("category", "characters")
    return (
        "litiso_sprixen, litiso_style, "
        f"fp_category {category}, fp_character {character}, fp_action {action}, fp_direction {direction}, "
        "FreePixel character sprite frame, single tiny character only, "
        f"{character} {action} frame, facing {direction}, "
        "small 64x64 RPG walk-cycle sprite proportions, full body including feet, "
        "centered transparent background, clean outline, hard pixel edges, limited palette, "
        "no environment, no scene, no floor, no UI, no text"
    )


def load_records(source_dir: Path) -> list[dict]:
    metadata = source_dir / "metadata.jsonl"
    if not metadata.exists():
        raise FileNotFoundError(metadata)
    return [json.loads(line) for line in metadata.read_text(encoding="utf-8").splitlines() if line.strip()]


def build_dataset(args: argparse.Namespace) -> None:
    source_dir: Path = args.source_dir
    output_dir: Path = args.output_dir
    image_dir = output_dir / "images"
    caption_dir = output_dir / "captions"
    image_dir.mkdir(parents=True, exist_ok=True)
    caption_dir.mkdir(parents=True, exist_ok=True)

    for path in image_dir.glob("*.png"):
        path.unlink()
    for path in caption_dir.glob("*.txt"):
        path.unlink()

    source_records = load_records(source_dir)
    output_records: list[dict] = []
    stats = Counter()

    for item in source_records:
        direction = item.get("direction", "unknown")
        action = ACTION_ALIASES.get(item.get("action", ""), item.get("action", ""))
        if direction not in DIRECTION_ORDER:
            stats["rejected_direction"] += 1
            continue
        if action not in KEEP_ACTIONS:
            stats["rejected_action"] += 1
            continue

        src_path = source_dir / item["file_name"]
        if not src_path.exists():
            stats["missing_source"] += 1
            continue

        try:
            frame = Image.open(src_path).convert("RGBA")
        except Exception:
            stats["bad_image"] += 1
            continue

        normalized = normalize_sprite(
            frame,
            canvas_size=args.canvas_size,
            target_height=args.target_height,
            foot_y=args.foot_y,
        )
        if normalized is None:
            stats["rejected_bounds"] += 1
            continue
        out_image, norm_stats = normalized
        if norm_stats["component_count"] > args.max_components:
            stats["rejected_components"] += 1
            continue

        index = len(output_records) + 1
        filename = f"sprixen_frame_{index:05d}.png"
        out_path = image_dir / filename
        out_image.save(out_path)
        caption = caption_for(item, action, direction)
        (caption_dir / f"{Path(filename).stem}.txt").write_text(caption, encoding="utf-8")

        output_records.append({
            "file_name": f"images/{filename}",
            "text": caption,
            "trigger": "litiso_sprixen",
            "source": item.get("source", "freepixel.art"),
            "source_file": item.get("file_name"),
            "category": item.get("category"),
            "character": item.get("character"),
            "slug": item.get("slug"),
            "action": action,
            "direction": direction,
            "frame_index": item.get("frame_index"),
            "canvas_size": args.canvas_size,
            "target_height": args.target_height,
            "foot_y": args.foot_y,
            "normalization": norm_stats,
        })
        stats["frames"] += 1
        stats[f"action_{action}"] += 1
        stats[f"direction_{direction}"] += 1

        if args.limit and len(output_records) >= args.limit:
            break

    (output_dir / "metadata.jsonl").write_text(
        "\n".join(json.dumps(record, ensure_ascii=False) for record in output_records) + "\n",
        encoding="utf-8",
    )
    manifest = {
        "source_dataset": str(source_dir),
        "trigger_word": "litiso_sprixen",
        "canvas_size": args.canvas_size,
        "target_height": args.target_height,
        "foot_y": args.foot_y,
        "direction_order": DIRECTION_ORDER,
        "total_frames": len(output_records),
        "stats": dict(stats),
        "caption_policy": "single tiny character frame, transparent centered canvas, no environment",
    }
    (output_dir / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"Built {len(output_records)} Sprixen-style frames -> {output_dir}")
    print(json.dumps(manifest["stats"], indent=2))


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-dir", type=Path, default=DEFAULT_SOURCE_DIR)
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT_DIR)
    parser.add_argument("--canvas-size", type=int, default=128)
    parser.add_argument("--target-height", type=int, default=74)
    parser.add_argument("--foot-y", type=int, default=116)
    parser.add_argument("--max-components", type=int, default=4)
    parser.add_argument("--limit", type=int, default=0)
    return parser.parse_args()


def main() -> None:
    build_dataset(parse_args())


if __name__ == "__main__":
    main()
