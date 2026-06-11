"""
Deterministic asset variant generator for BiomeSketch.

This does not call any AI provider. It recolours approved tiles/props while
preserving the exact alpha, dimensions, silhouette, and pixel grid.

Examples:
  py -3 generate_palette_variants.py --list-groups
  py -3 generate_palette_variants.py --default-set --presets lush,dry,frost --register
  py -3 generate_palette_variants.py --groups "plains v2 (new)" --presets autumn --contact-sheet
"""

from __future__ import annotations

import argparse
import colorsys
import json
import math
import os
import re
from collections import Counter
from pathlib import Path

try:
    from PIL import Image, ImageDraw
except ImportError as exc:
    raise SystemExit("Pillow is required. Run with the ComfyUI venv Python on this machine.") from exc


ROOT = Path(__file__).resolve().parents[2]
BIOME = ROOT / "Tools" / "BiomeSketch"
ASSETS_JS = BIOME / "assets.js"
REVIEW_DIR = BIOME / "variant_review"

DEFAULT_GROUPS = [
    "plains v2 (new)",
    "snow (new)",
    "mountain stone (new)",
    "transitions (new)",
    "plains props v2 (new)",
]

# Conservative transforms. These are intentionally moderate because the goal is
# "same asset, different biome/mood", not a new drawing.
PRESETS = {
    "lush": {
        "hue": 8,
        "sat": 1.15,
        "val": 1.06,
        "contrast": 1.04,
        "rgb": (0, 5, -2),
        "group": "variants - lush",
    },
    "dry": {
        "hue": 24,
        "sat": 0.86,
        "val": 1.03,
        "contrast": 1.02,
        "rgb": (12, 3, -8),
        "group": "variants - dry",
    },
    "autumn": {
        "hue": 36,
        "sat": 1.10,
        "val": 1.04,
        "contrast": 1.04,
        "rgb": (18, 0, -12),
        "group": "variants - autumn",
    },
    "frost": {
        "hue": -28,
        "sat": 0.74,
        "val": 1.12,
        "contrast": 0.98,
        "rgb": (-8, 8, 22),
        "group": "variants - frost",
    },
    "moss": {
        "hue": -16,
        "sat": 1.08,
        "val": 0.96,
        "contrast": 1.06,
        "rgb": (-10, 10, -8),
        "group": "variants - moss",
    },
    "dusk": {
        "hue": -14,
        "sat": 0.82,
        "val": 0.82,
        "contrast": 1.08,
        "rgb": (-12, -4, 18),
        "group": "variants - dusk",
    },
}


def load_assets() -> list[dict]:
    text = ASSETS_JS.read_text(encoding="utf-8-sig").strip()
    match = re.match(r"const\s+ASSETS\s*=\s*(.*);\s*$", text, re.S)
    if not match:
        raise SystemExit(f"Could not parse {ASSETS_JS}")
    return json.loads(match.group(1))


def write_assets(assets: list[dict]) -> None:
    ASSETS_JS.write_text("const ASSETS = " + json.dumps(assets, separators=(",", ":")) + ";\n", encoding="utf-8")


def clamp(v: float) -> int:
    return max(0, min(255, int(round(v))))


def luminance(r: int, g: int, b: int) -> float:
    return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0


def transform_rgb(r: int, g: int, b: int, preset: dict) -> tuple[int, int, int]:
    lum = luminance(r, g, b)

    # Preserve dark outlines and near-black contact pixels. This keeps the
    # original pixel-art linework from drifting across variants.
    if lum < 0.13:
        contrast = preset["contrast"]
        return tuple(clamp((c - 128) * contrast + 128) for c in (r, g, b))

    rf, gf, bf = r / 255.0, g / 255.0, b / 255.0
    h, s, v = colorsys.rgb_to_hsv(rf, gf, bf)

    # Low-saturation stone/snow pixels should shift less than vegetation.
    sat_weight = min(1.0, 0.35 + s * 1.35)
    h = (h + (preset["hue"] / 360.0) * sat_weight) % 1.0
    s = max(0.0, min(1.0, s * (1.0 + (preset["sat"] - 1.0) * sat_weight)))
    v = max(0.0, min(1.0, v * preset["val"]))

    nr, ng, nb = colorsys.hsv_to_rgb(h, s, v)
    rr, gg, bb = nr * 255.0, ng * 255.0, nb * 255.0

    contrast = preset["contrast"]
    rr = (rr - 128) * contrast + 128 + preset["rgb"][0]
    gg = (gg - 128) * contrast + 128 + preset["rgb"][1]
    bb = (bb - 128) * contrast + 128 + preset["rgb"][2]
    return clamp(rr), clamp(gg), clamp(bb)


def alpha_bounds(image: Image.Image) -> tuple[int, int, int, int]:
    if image.mode != "RGBA":
        image = image.convert("RGBA")
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


def variant_image(src: Path, preset: dict) -> Image.Image:
    image = Image.open(src).convert("RGBA")
    out = Image.new("RGBA", image.size, (0, 0, 0, 0))
    src_px, out_px = image.load(), out.load()
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = src_px[x, y]
            if a == 0:
                continue
            nr, ng, nb = transform_rgb(r, g, b, preset)
            out_px[x, y] = (nr, ng, nb, a)
    return out


def safe_name(name: str) -> str:
    return re.sub(r"[^a-zA-Z0-9_]+", "_", name).strip("_").lower()


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


def build_variants(args: argparse.Namespace) -> list[dict]:
    assets = load_assets()
    wanted_groups = DEFAULT_GROUPS if args.default_set else args.groups
    wanted_presets = args.presets
    if not wanted_groups:
        raise SystemExit("Provide --groups or --default-set")
    if not wanted_presets:
        raise SystemExit("Provide --presets")

    selected = [
        a for a in assets
        if a.get("group") in wanted_groups and a.get("cat") in ("tile", "prop")
    ]
    if args.max:
        selected = selected[: args.max]
    if not selected:
        raise SystemExit("No matching tile/prop assets found.")

    existing_keys = {(a.get("cat"), a.get("name")) for a in assets}
    new_records: list[dict] = []
    generated_by_preset: dict[str, list[Path]] = {p: [] for p in wanted_presets}

    for preset_name in wanted_presets:
        if preset_name not in PRESETS:
            raise SystemExit(f"Unknown preset {preset_name}. Options: {sorted(PRESETS)}")
        preset = PRESETS[preset_name]
        for asset in selected:
            src_path = BIOME / asset["path"]
            if not src_path.exists():
                print(f"missing source: {asset['path']}")
                continue
            category = asset["cat"]
            variant_name = f"{asset['name']}_{safe_name(preset_name)}"
            if (category, variant_name) in existing_keys and not args.overwrite:
                continue

            out_rel = Path("assets") / category / "variants" / safe_name(preset_name) / f"{variant_name}.png"
            out_path = BIOME / out_rel
            out_path.parent.mkdir(parents=True, exist_ok=True)

            image = variant_image(src_path, preset)
            image.save(out_path)
            bx, by, bw, bh = alpha_bounds(image)
            generated_by_preset[preset_name].append(out_path)

            record = {
                "cat": category,
                "name": variant_name,
                "path": out_rel.as_posix(),
                "w": image.width,
                "h": image.height,
                "ppu": asset.get("ppu", 32 if category == "tile" else 128),
                "bx": bx,
                "by": by,
                "bw": bw,
                "bh": bh,
                "group": preset["group"],
                "variant_preset": preset_name,
                "source_asset": asset["name"],
                "source_group": asset.get("group", ""),
            }
            new_records.append(record)
            existing_keys.add((category, variant_name))

    if args.contact_sheet:
        for preset_name, paths in generated_by_preset.items():
            contact_sheet(paths, REVIEW_DIR / f"variants_{safe_name(preset_name)}_contact_sheet.png")

    if args.register and new_records:
        replace_names = {(r["cat"], r["name"]) for r in new_records}
        assets = [a for a in assets if (a.get("cat"), a.get("name")) not in replace_names]
        assets.extend(new_records)
        write_assets(assets)

    manifest = {
        "source_groups": wanted_groups,
        "presets": wanted_presets,
        "registered": bool(args.register),
        "generated_count": len(new_records),
        "records": new_records,
    }
    REVIEW_DIR.mkdir(parents=True, exist_ok=True)
    (REVIEW_DIR / "variant_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    return new_records


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--list-groups", action="store_true")
    parser.add_argument("--groups", nargs="*", default=[])
    parser.add_argument("--default-set", action="store_true")
    parser.add_argument("--presets", type=lambda v: [p.strip() for p in v.split(",") if p.strip()], default=[])
    parser.add_argument("--register", action="store_true")
    parser.add_argument("--contact-sheet", action="store_true")
    parser.add_argument("--overwrite", action="store_true")
    parser.add_argument("--max", type=int, default=0)
    args = parser.parse_args()

    if args.list_groups:
        counts = Counter(a.get("group", "misc") for a in load_assets())
        for group, count in sorted(counts.items()):
            print(f"{group}: {count}")
        return

    records = build_variants(args)
    print(json.dumps({
        "generated": len(records),
        "registered": args.register,
        "manifest": str(REVIEW_DIR / "variant_manifest.json"),
        "review_dir": str(REVIEW_DIR),
    }, indent=2))


if __name__ == "__main__":
    main()
