#!/usr/bin/env python3
"""
Build a direction-aware FreePixel character dataset for LoRA training.

Why this exists:
  C:\\Projects\\Pixel Pipeline\\download_freepixel_sprites.py already downloaded
  and sliced the FreePixel character sheets, but its metadata flattened away
  row/column information. That means the existing 9,392-frame dataset knows
  character names and actions, but not "facing south/east/etc.".

This script reuses the original FreePixel catalogue and CDN URL, then writes a
new dataset where every frame has:
  - character slug / readable name
  - category
  - action
  - direction
  - sheet row
  - frame column
  - structured caption text

Default output intentionally points at the Pixel Pipeline folder because that is
where the existing training scripts expect datasets to live. Run from PowerShell:

  python Tools\\LoRA\\freepixel_structured_dataset.py --directional-only

Optional dry audit without downloading:

  python Tools\\LoRA\\freepixel_structured_dataset.py --audit-existing

Build only sheets that contain enough populated direction rows for direction
training:

  python Tools\\LoRA\\freepixel_structured_dataset.py --directional-only
"""

from __future__ import annotations

import argparse
import ast
import io
import json
import sys
import time
from collections import Counter
from collections import deque
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

from PIL import Image

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


DEFAULT_ORIGINAL_SCRIPT = Path(r"C:\Projects\Pixel Pipeline\download_freepixel_sprites.py")
DEFAULT_OUTPUT_DIR = Path(r"C:\Projects\Pixel Pipeline\style_examples\freepixel_web_download_structured")
DEFAULT_EXISTING_DATASET = Path(r"C:\Projects\Pixel Pipeline\style_examples\freepixel_web_download")

SPRITE_SIZE = 128
MIN_COVERAGE = 0.04
BG_TOLERANCE = 18

# FreePixel 8-direction sheets are treated as row-major. This matches the
# direction convention already used elsewhere in the local Pixel Pipeline.
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


def load_original_catalogue(original_script: Path) -> tuple[str, dict[str, list[tuple[str, list[str]]]]]:
    if not original_script.exists():
        raise FileNotFoundError(f"Original FreePixel downloader not found: {original_script}")

    tree = ast.parse(original_script.read_text(encoding="utf-8"))
    cdn = None
    characters = None
    for node in tree.body:
        if not isinstance(node, ast.Assign):
            continue
        for target in node.targets:
            if isinstance(target, ast.Name) and target.id == "CDN":
                cdn = ast.literal_eval(node.value)
            elif isinstance(target, ast.Name) and target.id == "CHARACTERS":
                characters = ast.literal_eval(node.value)

    if not cdn or not characters:
        raise RuntimeError(f"Could not read CDN/CHARACTERS from {original_script}")
    return cdn, characters


def color_distance(a: tuple[int, int, int, int], b: tuple[int, int, int, int]) -> int:
    return abs(a[0] - b[0]) + abs(a[1] - b[1]) + abs(a[2] - b[2])


def remove_edge_bg(img: Image.Image) -> Image.Image:
    rgba = img.convert("RGBA")
    data = rgba.load()
    width, height = rgba.size
    bg = data[0, 0]
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
        if px[3] == 0 or color_distance(px, bg) <= BG_TOLERANCE:
            data[x, y] = (px[0], px[1], px[2], 0)
            queue.append((x + 1, y))
            queue.append((x - 1, y))
            queue.append((x, y + 1))
            queue.append((x, y - 1))

    return rgba


def foreground_coverage(cell: Image.Image) -> float:
    rgba = remove_edge_bg(cell)
    alpha = rgba.getchannel("A")
    foreground = sum(1 for value in alpha.getdata() if value > 0)
    return foreground / max(1, cell.width * cell.height)


def readable_name(slug: str) -> str:
    return slug.replace("-", " ").strip()


def make_caption(category: str, slug: str, action: str, direction: str, frame_col: int) -> str:
    name = readable_name(slug)
    direction_bits = "" if direction == "unknown" else f"fp_direction {direction}, "
    facing_bits = "" if direction == "unknown" else f"facing {direction}, "
    return (
        "litiso_style, "
        f"fp_category {category}, fp_character {name}, fp_action {action}, {direction_bits}"
        f"pixel art RPG character sprite, {category} {name}, {action} animation frame {frame_col}, "
        f"{facing_bits}8-direction game sprite, full body, crisp pixel art, clean outline, "
        "transparent background, no text, no logo, no scene, no floor aura, game asset"
    )


def download_sheet(cdn: str, slug: str, action: str) -> Image.Image | None:
    url = f"{cdn}/{slug}/{action}.png"
    request = Request(url, headers={"User-Agent": "Mozilla/5.0"})
    try:
        with urlopen(request, timeout=20) as response:
            return Image.open(io.BytesIO(response.read())).convert("RGB")
    except HTTPError as exc:
        print(f"  skipped {exc.code}: {url}")
        return None
    except URLError as exc:
        print(f"  skipped network error {exc}: {url}")
        return None


def get_populated_rows(sheet: Image.Image) -> list[int]:
    rows = sheet.height // SPRITE_SIZE
    populated: list[int] = []
    for row in range(rows):
        cell_band = sheet.crop((0, row * SPRITE_SIZE, sheet.width, (row + 1) * SPRITE_SIZE))
        if foreground_coverage(cell_band) >= MIN_COVERAGE:
            populated.append(row)
    return populated


def slice_sheet(
    sheet: Image.Image,
    slug: str,
    action: str,
    category: str,
    image_dir: Path,
    records: list[dict],
    index_ref: list[int],
    args: argparse.Namespace,
) -> int:
    sheet_width, sheet_height = sheet.size
    cols = sheet_width // SPRITE_SIZE
    rows = sheet_height // SPRITE_SIZE
    saved = 0
    populated_rows = get_populated_rows(sheet)
    has_directional_layout = len(populated_rows) >= args.min_directional_rows
    if args.directional_only and not has_directional_layout:
        return 0

    for row in range(rows):
        if has_directional_layout and row < len(DIRECTION_ORDER):
            direction = DIRECTION_ORDER[row]
        else:
            direction = "unknown"

        for col in range(cols):
            box = (
                col * SPRITE_SIZE,
                row * SPRITE_SIZE,
                (col + 1) * SPRITE_SIZE,
                (row + 1) * SPRITE_SIZE,
            )
            cell = sheet.crop(box)
            coverage = foreground_coverage(cell)
            if coverage < MIN_COVERAGE:
                continue

            frame = remove_edge_bg(cell)
            file_name = f"fp_struct_{index_ref[0]:05d}.png"
            frame.save(image_dir / file_name)

            caption = make_caption(category, slug, action, direction, col)
            records.append({
                "file_name": f"images/{file_name}",
                "text": caption,
                "trigger": "litiso_style",
                "source": "freepixel.art",
                "source_url": f"sorted/characters/spritesheets/{slug}/{action}.png",
                "category": category,
                "character": readable_name(slug),
                "slug": slug,
                "action": action,
                "direction": direction,
                "direction_index": row,
                "frame_index": col,
                "coverage": round(coverage, 5),
                "sheet_rows": rows,
                "populated_rows": populated_rows,
                "directional_layout": has_directional_layout,
            })
            index_ref[0] += 1
            saved += 1

    return saved


def build_dataset(args: argparse.Namespace) -> None:
    cdn, characters = load_original_catalogue(args.original_script)
    output_dir: Path = args.output_dir
    image_dir = output_dir / "images"
    caption_dir = output_dir / "captions"
    image_dir.mkdir(parents=True, exist_ok=True)
    caption_dir.mkdir(parents=True, exist_ok=True)

    records: list[dict] = []
    index_ref = [1]
    stats = Counter()

    for category, entries in characters.items():
        print(f"\n{category.upper()} ({len(entries)} characters)")
        for slug, actions in entries:
            for action in actions:
                if args.limit_sheets and stats["sheets_seen"] >= args.limit_sheets:
                    break
                stats["sheets_seen"] += 1
                try:
                    sheet = download_sheet(cdn, slug, action)
                except Exception as exc:
                    print(f"  error {slug}/{action}: {exc}")
                    stats["sheet_errors"] += 1
                    continue
                if sheet is None:
                    stats["sheet_missing"] += 1
                    continue

                populated_rows = get_populated_rows(sheet)
                if args.directional_only and len(populated_rows) < args.min_directional_rows:
                    print(f"  {slug}/{action}: skipped partial rows={populated_rows}")
                    stats["partial_sheets_skipped"] += 1
                    continue

                count = slice_sheet(sheet, slug, action, category, image_dir, records, index_ref, args)
                print(f"  {slug}/{action}: {count} frames")
                stats["sheets_downloaded"] += 1
                stats["frames"] += count
                time.sleep(args.sleep)

    for record in records:
        caption_path = caption_dir / f"{Path(record['file_name']).stem}.txt"
        caption_path.write_text(record["text"], encoding="utf-8")

    (output_dir / "metadata.jsonl").write_text(
        "\n".join(json.dumps(record, ensure_ascii=False) for record in records) + "\n",
        encoding="utf-8",
    )
    manifest = {
        "source": "freepixel.art",
        "source_script": str(args.original_script),
        "trigger_word": "litiso_style",
        "sprite_size": SPRITE_SIZE,
        "direction_order": DIRECTION_ORDER,
        "directional_only": args.directional_only,
        "min_directional_rows": args.min_directional_rows,
        "total_frames": len(records),
        "stats": dict(stats),
        "categories": list(characters.keys()),
    }
    (output_dir / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"\nDone: {len(records)} frames -> {output_dir}")


def audit_existing(existing_dataset: Path) -> None:
    metadata = existing_dataset / "metadata.jsonl"
    if not metadata.exists():
        raise FileNotFoundError(metadata)

    total = 0
    actions = Counter()
    directions = Counter()
    categories = Counter()
    for line in metadata.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        item = json.loads(line)
        text = item.get("text", "").lower()
        total += 1
        for action in ("idle", "walk", "run", "attack", "hurt", "die", "death", "battlecry"):
            if action in text:
                actions[action] += 1
                break
        for direction in DIRECTION_ORDER:
            if f"facing {direction}" in text or f"fp_direction {direction}" in text:
                directions[direction] += 1
                break
        for category in ("mages", "warriors", "rogues", "npcs", "enemies"):
            if f" {category} " in f" {text} ":
                categories[category] += 1
                break

    print(f"Dataset: {existing_dataset}")
    print(f"Total records: {total}")
    print(f"Actions: {dict(actions)}")
    print(f"Directions: {dict(directions)}")
    print(f"Categories: {dict(categories)}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--original-script", type=Path, default=DEFAULT_ORIGINAL_SCRIPT)
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT_DIR)
    parser.add_argument("--existing-dataset", type=Path, default=DEFAULT_EXISTING_DATASET)
    parser.add_argument("--audit-existing", action="store_true")
    parser.add_argument("--limit-sheets", type=int, default=0)
    parser.add_argument("--sleep", type=float, default=0.12)
    parser.add_argument("--directional-only", action="store_true")
    parser.add_argument("--min-directional-rows", type=int, default=6)
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    if args.audit_existing:
        audit_existing(args.existing_dataset)
    else:
        build_dataset(args)


if __name__ == "__main__":
    main()
