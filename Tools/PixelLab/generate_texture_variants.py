"""
Generate new local tile texture variants from approved BiomeSketch tiles.

No AI calls. The algorithm preserves image size, alpha, and the source tile's
outer silhouette while creating deterministic internal texture changes:
small colour jitter, local neighbour swaps, and micro speckle clusters.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import random
import re
from collections import Counter
from pathlib import Path

try:
    from PIL import Image, ImageDraw
except ImportError as exc:
    raise SystemExit("Pillow is required. Use the ComfyUI venv Python on this machine.") from exc


ROOT = Path(__file__).resolve().parents[2]
BIOME = ROOT / "Tools" / "BiomeSketch"
ASSETS_JS = BIOME / "assets.js"
REVIEW_DIR = BIOME / "variant_review"

DEFAULT_TILE_GROUPS = [
    "plains v2 (new)",
    "snow (new)",
    "mountain stone (new)",
]


def load_assets() -> list[dict]:
    text = ASSETS_JS.read_text(encoding="utf-8-sig").strip()
    match = re.match(r"const\s+ASSETS\s*=\s*(.*);\s*$", text, re.S)
    if not match:
        raise SystemExit(f"Could not parse {ASSETS_JS}")
    return json.loads(match.group(1))


def write_assets(assets: list[dict]) -> None:
    ASSETS_JS.write_text("const ASSETS = " + json.dumps(assets, separators=(",", ":")) + ";\n", encoding="utf-8")


def safe_name(name: str) -> str:
    return re.sub(r"[^a-zA-Z0-9_]+", "_", name).strip("_").lower()


def clamp(v: float) -> int:
    return max(0, min(255, int(round(v))))


def alpha_bounds(image: Image.Image) -> tuple[int, int, int, int]:
    px = image.load()
    min_x, min_y = image.width, image.height
    max_x, max_y = -1, -1
    for y in range(image.height):
        for x in range(image.width):
            if px[x, y][3] > 0:
                min_x = min(min_x, x)
                min_y = min(min_y, y)
                max_x = max(max_x, x)
                max_y = max(max_y, y)
    if max_x < 0:
        return 0, 0, 0, 0
    return min_x, min_y, max_x - min_x + 1, max_y - min_y + 1


def deterministic_seed(*parts: str) -> int:
    digest = hashlib.sha256("|".join(parts).encode("utf-8")).hexdigest()
    return int(digest[:16], 16)


def luminance(pixel: tuple[int, int, int, int]) -> float:
    r, g, b, _ = pixel
    return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0


def is_edge(alpha: list[list[int]], x: int, y: int) -> bool:
    h = len(alpha)
    w = len(alpha[0])
    if alpha[y][x] == 0:
        return False
    for yy in range(max(0, y - 1), min(h, y + 2)):
        for xx in range(max(0, x - 1), min(w, x + 2)):
            if alpha[yy][xx] == 0:
                return True
    return False


def make_texture_variant(src: Path, seed: int, strength: float) -> Image.Image:
    image = Image.open(src).convert("RGBA")
    out = image.copy()
    px = out.load()
    source_px = image.load()
    rng = random.Random(seed)

    alpha = [[source_px[x, y][3] for x in range(image.width)] for y in range(image.height)]

    # Pass 1: subtle deterministic colour jitter on interior pixels.
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = source_px[x, y]
            if a == 0:
                continue
            lum = luminance((r, g, b, a))
            edge = is_edge(alpha, x, y)
            if lum < 0.13 or edge:
                px[x, y] = (r, g, b, a)
                continue

            local = rng.random()
            jitter = int((local - 0.5) * 18 * strength)
            sat_bias = int((0.5 - abs(lum - 0.5)) * 6 * strength)
            px[x, y] = (
                clamp(r + jitter + sat_bias),
                clamp(g + jitter),
                clamp(b + jitter - sat_bias),
                a,
            )

    # Pass 2: copy tiny internal pixels from neighbours to create new texture
    # flakes/grass blades without changing the silhouette.
    source_after_jitter = out.copy()
    src2 = source_after_jitter.load()
    swaps = int(image.width * image.height * 0.045 * strength)
    for _ in range(swaps):
        x = rng.randrange(1, max(2, image.width - 1))
        y = rng.randrange(1, max(2, image.height - 1))
        if alpha[y][x] == 0 or is_edge(alpha, x, y):
            continue
        dx, dy = rng.choice(((1, 0), (-1, 0), (0, 1), (0, -1), (1, -1), (-1, 1)))
        sx, sy = x + dx, y + dy
        if alpha[sy][sx] > 0 and not is_edge(alpha, sx, sy):
            px[x, y] = src2[sx, sy]

    # Pass 3: occasional 1-2 pixel highlights/shadows, kept subtle.
    clusters = int(7 * strength)
    for _ in range(clusters):
        x = rng.randrange(1, max(2, image.width - 1))
        y = rng.randrange(1, max(2, image.height - 1))
        if alpha[y][x] == 0 or is_edge(alpha, x, y):
            continue
        delta = rng.choice((-12, -8, 8, 11))
        for ox, oy in ((0, 0), (1, 0), (0, 1)):
            xx, yy = x + ox, y + oy
            if xx >= image.width or yy >= image.height or alpha[yy][xx] == 0 or is_edge(alpha, xx, yy):
                continue
            r, g, b, a = px[xx, yy]
            px[xx, yy] = (clamp(r + delta), clamp(g + delta), clamp(b + delta), a)

    return out


def contact_sheet(paths: list[Path], out_path: Path) -> None:
    if not paths:
        return
    cells = [(p, Image.open(p).convert("RGBA")) for p in paths]
    max_w = max(img.width for _, img in cells)
    max_h = max(img.height for _, img in cells)
    cell_w = max_w + 18
    cell_h = max_h + 34
    cols = min(8, len(cells))
    rows = math.ceil(len(cells) / cols)
    sheet = Image.new("RGBA", (cols * cell_w, rows * cell_h), (42, 44, 52, 255))
    draw = ImageDraw.Draw(sheet)
    for index, (path, img) in enumerate(cells):
        x = (index % cols) * cell_w
        y = (index // cols) * cell_h
        sheet.paste(img, (x + (cell_w - img.width) // 2, y + 18), img)
        draw.text((x + 5, y + 3), path.stem[-28:], fill=(245, 229, 190, 255))
    out_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out_path)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--list-groups", action="store_true")
    parser.add_argument("--groups", nargs="*", default=[])
    parser.add_argument("--default-set", action="store_true")
    parser.add_argument("--count", type=int, default=2)
    parser.add_argument("--strength", type=float, default=1.0)
    parser.add_argument("--register", action="store_true")
    parser.add_argument("--contact-sheet", action="store_true")
    parser.add_argument("--overwrite", action="store_true")
    parser.add_argument("--max", type=int, default=0)
    args = parser.parse_args()

    assets = load_assets()
    if args.list_groups:
        counts = Counter(a.get("group", "misc") for a in assets)
        for group, count in sorted(counts.items()):
            print(f"{group}: {count}")
        return

    groups = DEFAULT_TILE_GROUPS if args.default_set else args.groups
    if not groups:
        raise SystemExit("Provide --groups or --default-set")

    selected = [a for a in assets if a.get("cat") == "tile" and a.get("group") in groups]
    if args.max:
        selected = selected[: args.max]
    if not selected:
        raise SystemExit("No matching tile assets found.")

    existing_keys = {(a.get("cat"), a.get("name")) for a in assets}
    records: list[dict] = []
    paths: list[Path] = []
    for asset in selected:
        src = BIOME / asset["path"]
        if not src.exists():
            print(f"missing source: {asset['path']}")
            continue
        for index in range(1, args.count + 1):
            variant_name = f"{asset['name']}_tex{index:02d}"
            if ("tile", variant_name) in existing_keys and not args.overwrite:
                continue
            out_rel = Path("assets") / "tile" / "texture_variants" / f"{variant_name}.png"
            out_path = BIOME / out_rel
            out_path.parent.mkdir(parents=True, exist_ok=True)
            image = make_texture_variant(src, deterministic_seed(asset["name"], str(index)), args.strength)
            image.save(out_path)
            bx, by, bw, bh = alpha_bounds(image)
            record = {
                "cat": "tile",
                "name": variant_name,
                "path": out_rel.as_posix(),
                "w": image.width,
                "h": image.height,
                "ppu": asset.get("ppu", 32),
                "bx": bx,
                "by": by,
                "bw": bw,
                "bh": bh,
                "group": "texture variants - tiles",
                "variant_kind": "procedural_texture",
                "variant_index": index,
                "source_asset": asset["name"],
                "source_group": asset.get("group", ""),
            }
            records.append(record)
            paths.append(out_path)
            existing_keys.add(("tile", variant_name))

    if args.contact_sheet:
        contact_sheet(paths, REVIEW_DIR / "texture_variants_tiles_contact_sheet.png")

    if args.register and records:
        replace = {(r["cat"], r["name"]) for r in records}
        assets = [a for a in assets if (a.get("cat"), a.get("name")) not in replace]
        assets.extend(records)
        write_assets(assets)

    REVIEW_DIR.mkdir(parents=True, exist_ok=True)
    (REVIEW_DIR / "texture_variant_manifest.json").write_text(json.dumps({
        "source_groups": groups,
        "count_per_source": args.count,
        "registered": args.register,
        "generated_count": len(records),
        "records": records,
    }, indent=2), encoding="utf-8")
    print(json.dumps({
        "generated": len(records),
        "registered": args.register,
        "manifest": str(REVIEW_DIR / "texture_variant_manifest.json"),
    }, indent=2))


if __name__ == "__main__":
    main()
