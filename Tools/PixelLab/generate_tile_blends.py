"""
Generate gradient blend tiles between two approved BiomeSketch tiles.

No AI calls. The default blend uses a directional threshold mask plus
clustered value noise, so the tile reads as an organic material transition
while keeping crisp source pixels.
"""

from __future__ import annotations

import argparse
import json
import math
import re
from pathlib import Path

try:
    from PIL import Image, ImageDraw
except ImportError as exc:
    raise SystemExit("Pillow is required. Use the ComfyUI venv Python on this machine.") from exc


ROOT = Path(__file__).resolve().parents[2]
BIOME = ROOT / "Tools" / "BiomeSketch"
ASSETS_JS = BIOME / "assets.js"
REVIEW_DIR = BIOME / "variant_review"

DIRECTIONS = {
    "n": (0.0, -1.0),
    "s": (0.0, 1.0),
    "e": (1.0, 0.0),
    "w": (-1.0, 0.0),
    "ne": (1.0, -1.0),
    "nw": (-1.0, -1.0),
    "se": (1.0, 1.0),
    "sw": (-1.0, 1.0),
}

BAYER_4 = (
    (0, 8, 2, 10),
    (12, 4, 14, 6),
    (3, 11, 1, 9),
    (15, 7, 13, 5),
)

DEFAULT_PAIRS = [
    ("plains2_00", "snow2_00"),
    ("plains2_00", "mountain_stone_00"),
    ("plains2_02", "mountain_stone_05"),
    ("snow2_00", "mountain_stone_03"),
    ("plains2_00", "plains_bare_dirt"),
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


def directional_weight(x: int, y: int, width: int, height: int, direction: str, softness: float) -> float:
    dx, dy = DIRECTIONS[direction]
    length = math.sqrt(dx * dx + dy * dy)
    dx, dy = dx / length, dy / length
    nx = ((x + 0.5) / width) * 2.0 - 1.0
    ny = ((y + 0.5) / height) * 2.0 - 1.0
    projection = (nx * dx + ny * dy + 1.0) * 0.5
    softness = max(0.05, min(1.0, softness))
    return max(0.0, min(1.0, (projection - (0.5 - softness / 2.0)) / softness))


def deterministic_seed(*parts: str) -> int:
    digest = 2166136261
    for part in parts:
        for char in part:
            digest ^= ord(char)
            digest = (digest * 16777619) & 0xFFFFFFFF
    return digest


def cluster_noise(width: int, height: int, seed: int, cell: int = 3) -> list[list[float]]:
    # Keep a local import-free PRNG so this script remains deterministic across
    # Python versions and does not depend on hash randomization.
    state = seed & 0xFFFFFFFF

    def rand() -> float:
        nonlocal state
        state = (state + 0x6D2B79F5) & 0xFFFFFFFF
        t = state
        t = (t ^ (t >> 15)) * (t | 1)
        t ^= t + ((t ^ (t >> 7)) * (t | 61))
        return ((t ^ (t >> 14)) & 0xFFFFFFFF) / 4294967296.0

    def smoothstep(value: float) -> float:
        return value * value * (3.0 - 2.0 * value)

    grid_w = width // cell + 2
    grid_h = height // cell + 2
    grid = [[rand() for _ in range(grid_w)] for _ in range(grid_h)]
    out = [[0.0] * width for _ in range(height)]
    for y in range(height):
        for x in range(width):
            fx = x / cell
            fy = y / cell
            x0 = int(fx)
            y0 = int(fy)
            tx = smoothstep(fx - x0)
            ty = smoothstep(fy - y0)
            a = grid[y0][x0] * (1.0 - tx) + grid[y0][x0 + 1] * tx
            b = grid[y0 + 1][x0] * (1.0 - tx) + grid[y0 + 1][x0 + 1] * tx
            out[y][x] = a * (1.0 - ty) + b * ty
    return out


def blend_images(a_path: Path, b_path: Path, direction: str, softness: float, pattern: str, seed_text: str) -> Image.Image:
    a = Image.open(a_path).convert("RGBA")
    b = Image.open(b_path).convert("RGBA")
    if a.size != b.size:
        b = b.resize(a.size, Image.Resampling.NEAREST)
    out = Image.new("RGBA", a.size, (0, 0, 0, 0))
    apx, bpx, opx = a.load(), b.load(), out.load()
    noise = cluster_noise(a.width, a.height, deterministic_seed(seed_text, direction)) if pattern == "organic" else None
    for y in range(a.height):
        for x in range(a.width):
            pa = apx[x, y]
            pb = bpx[x, y]
            if pa[3] == 0 and pb[3] == 0:
                continue
            if pa[3] == 0:
                opx[x, y] = pb
                continue
            if pb[3] == 0:
                opx[x, y] = pa
                continue
            weight = directional_weight(x, y, a.width, a.height, direction, softness)
            if pattern == "organic":
                threshold = 0.5 + ((noise[y][x] if noise else 0.5) - 0.5) * 0.55
            else:
                threshold = (BAYER_4[y % 4][x % 4] + 0.5) / 16.0
            opx[x, y] = pb if weight > threshold else pa
    return out


def contact_sheet(paths: list[Path], out_path: Path) -> None:
    if not paths:
        return
    cells = [(p, Image.open(p).convert("RGBA")) for p in paths]
    max_w = max(img.width for _, img in cells)
    max_h = max(img.height for _, img in cells)
    cell_w = max(max_w + 18, 148)
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


def parse_pairs(raw_pairs: list[str]) -> list[tuple[str, str]]:
    pairs: list[tuple[str, str]] = []
    for raw in raw_pairs:
        for part in raw.split(","):
            if not part.strip():
                continue
            if ":" not in part:
                raise SystemExit(f"Bad pair '{part}'. Use asset_a:asset_b")
            a, b = part.split(":", 1)
            pairs.append((a.strip(), b.strip()))
    return pairs


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--default-pairs", action="store_true")
    parser.add_argument("--pairs", nargs="*", default=[])
    parser.add_argument("--directions", type=lambda v: [d.strip() for d in v.split(",") if d.strip()],
                        default=list(DIRECTIONS))
    parser.add_argument("--softness", type=float, default=0.68)
    parser.add_argument("--pattern", choices=("organic", "dither"), default="organic")
    parser.add_argument("--count", type=int, default=1,
                        help="Number of organic variants per pair and direction.")
    parser.add_argument("--seed", default="litiso",
                        help="Stable batch seed. Change this to create a different deterministic blend family.")
    parser.add_argument("--register", action="store_true")
    parser.add_argument("--contact-sheet", action="store_true")
    parser.add_argument("--overwrite", action="store_true")
    args = parser.parse_args()

    pairs = DEFAULT_PAIRS if args.default_pairs else parse_pairs(args.pairs)
    if not pairs:
        raise SystemExit("Provide --pairs a:b or --default-pairs")
    for direction in args.directions:
        if direction not in DIRECTIONS:
            raise SystemExit(f"Unknown direction {direction}. Options: {sorted(DIRECTIONS)}")

    assets = load_assets()
    by_name = {a["name"]: a for a in assets}
    existing = {(a.get("cat"), a.get("name")) for a in assets}
    records: list[dict] = []
    paths: list[Path] = []

    for left, right in pairs:
        if left not in by_name:
            print(f"missing asset: {left}")
            continue
        if right not in by_name:
            print(f"missing asset: {right}")
            continue
        left_asset = by_name[left]
        right_asset = by_name[right]
        if left_asset.get("cat") != "tile" or right_asset.get("cat") != "tile":
            print(f"skipping non-tile pair: {left}:{right}")
            continue
        for direction in args.directions:
            for index in range(1, max(1, args.count) + 1):
                suffix = "" if args.count == 1 else f"_v{index:02d}"
                name = f"blend_{safe_name(left)}_to_{safe_name(right)}_{direction}{suffix}"
                if ("tile", name) in existing and not args.overwrite:
                    continue
                out_rel = Path("assets") / "tile" / "gradient_blends" / f"{name}.png"
                out_path = BIOME / out_rel
                out_path.parent.mkdir(parents=True, exist_ok=True)
                image = blend_images(
                    BIOME / left_asset["path"],
                    BIOME / right_asset["path"],
                    direction,
                    args.softness,
                    args.pattern,
                    f"{args.seed}:{left}:{right}:{index}",
                )
                image.save(out_path)
                bx, by, bw, bh = alpha_bounds(image)
                record = {
                    "cat": "tile",
                    "name": name,
                    "path": out_rel.as_posix(),
                    "w": image.width,
                    "h": image.height,
                    "ppu": left_asset.get("ppu", 32),
                    "bx": bx,
                    "by": by,
                    "bw": bw,
                    "bh": bh,
                    "group": "gradient blends - tiles",
                    "variant_kind": f"{args.pattern}_gradient_blend",
                    "blend_from": left,
                    "blend_to": right,
                    "blend_direction": direction,
                    "blend_softness": args.softness,
                    "blend_pattern": args.pattern,
                    "blend_variant_index": index,
                    "blend_seed": args.seed,
                }
                records.append(record)
                paths.append(out_path)
                existing.add(("tile", name))

    if args.contact_sheet:
        contact_sheet(paths, REVIEW_DIR / "gradient_blends_tiles_contact_sheet.png")

    if args.register and records:
        replace = {(r["cat"], r["name"]) for r in records}
        assets = [a for a in assets if (a.get("cat"), a.get("name")) not in replace]
        assets.extend(records)
        write_assets(assets)

    REVIEW_DIR.mkdir(parents=True, exist_ok=True)
    (REVIEW_DIR / "gradient_blend_manifest.json").write_text(json.dumps({
        "pairs": pairs,
        "directions": args.directions,
        "pattern": args.pattern,
        "count_per_direction": args.count,
        "seed": args.seed,
        "registered": args.register,
        "generated_count": len(records),
        "records": records,
    }, indent=2), encoding="utf-8")
    print(json.dumps({
        "generated": len(records),
        "registered": args.register,
        "manifest": str(REVIEW_DIR / "gradient_blend_manifest.json"),
    }, indent=2))


if __name__ == "__main__":
    main()
