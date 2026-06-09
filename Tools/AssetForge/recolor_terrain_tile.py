#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw


PALETTES = {
    "greenwake": {
        "top": [(72, 103, 57), (98, 132, 66), (132, 151, 73), (165, 166, 87)],
        "side": [(86, 50, 38), (118, 72, 48), (149, 96, 62), (182, 129, 82)],
    },
    "deep_forest": {
        "top": [(45, 79, 51), (61, 105, 60), (83, 127, 67), (117, 143, 78)],
        "side": [(58, 42, 35), (83, 56, 41), (112, 74, 49), (145, 97, 61)],
    },
    "plains": {
        "top": [(96, 119, 56), (132, 145, 66), (165, 162, 80), (191, 181, 102)],
        "side": [(95, 58, 39), (131, 83, 50), (166, 111, 63), (198, 145, 86)],
    },
    "autumn": {
        "top": [(100, 88, 47), (137, 110, 54), (173, 132, 65), (199, 157, 82)],
        "side": [(82, 48, 37), (121, 69, 43), (158, 93, 50), (190, 125, 67)],
    },
    "mud": {
        "top": [(76, 69, 48), (103, 88, 57), (129, 104, 66), (156, 127, 84)],
        "side": [(55, 43, 36), (78, 57, 43), (104, 74, 51), (133, 96, 64)],
    },
}


def alpha_bbox(image: Image.Image):
    return image.getchannel("A").getbbox()


def luminance(rgb: tuple[int, int, int]) -> float:
    r, g, b = rgb
    return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0


def pick_ramp_color(ramp, t: float):
    idx = max(0, min(len(ramp) - 1, int(t * len(ramp))))
    return ramp[idx]


def recolor_tile(image: Image.Image, palette: dict, shade_strength: float = 0.28) -> Image.Image:
    rgba = image.convert("RGBA")
    box = alpha_bbox(rgba)
    if not box:
        return rgba

    min_x, min_y, max_x, max_y = box
    height = max(1, max_y - min_y)
    split_y = min_y + int(height * 0.47)

    output = Image.new("RGBA", rgba.size, (0, 0, 0, 0))
    source = rgba.load()
    dest = output.load()

    for y in range(rgba.height):
        for x in range(rgba.width):
            r, g, b, a = source[x, y]
            if a == 0:
                continue
            is_side = y >= split_y
            ramp = palette["side"] if is_side else palette["top"]
            lum = luminance((r, g, b))
            base = pick_ramp_color(ramp, lum)
            vertical = (y - min_y) / height
            shade = 1.0 - (vertical * shade_strength if is_side else vertical * shade_strength * 0.35)
            nr = max(0, min(255, int(base[0] * shade)))
            ng = max(0, min(255, int(base[1] * shade)))
            nb = max(0, min(255, int(base[2] * shade)))
            dest[x, y] = (nr, ng, nb, 255)

    return output


def make_preview(images: list[tuple[str, Image.Image]], output_path: Path) -> None:
    scale = 3
    cell = 96
    width = cell * len(images)
    height = 130
    preview = Image.new("RGBA", (width, height), (28, 32, 34, 255))
    draw = ImageDraw.Draw(preview)
    for i, (name, image) in enumerate(images):
        scaled = image.resize((image.width * scale, image.height * scale), Image.Resampling.NEAREST)
        x = i * cell + (cell - scaled.width) // 2
        preview.alpha_composite(scaled, (x, 8))
        draw.text((i * cell + 8, 106), name, fill=(220, 226, 218, 255))
    preview.save(output_path)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--output-root", required=True)
    parser.add_argument("--prefix", default="terrain_recolor")
    parser.add_argument("--palettes", default="greenwake,deep_forest,plains,autumn,mud")
    args = parser.parse_args()

    source_path = Path(args.input)
    output_root = Path(args.output_root)
    output_root.mkdir(parents=True, exist_ok=True)
    source = Image.open(source_path).convert("RGBA")

    written = []
    preview_items = []
    for palette_name in [part.strip() for part in args.palettes.split(",") if part.strip()]:
        if palette_name not in PALETTES:
            raise ValueError(f"Unknown palette '{palette_name}'. Options: {', '.join(PALETTES)}")
        image = recolor_tile(source, PALETTES[palette_name])
        output_path = output_root / f"{args.prefix}_{palette_name}.png"
        image.save(output_path)
        written.append({"palette": palette_name, "path": str(output_path), "width": image.width, "height": image.height})
        preview_items.append((palette_name, image))

    preview_path = output_root / f"{args.prefix}_palette_preview.png"
    make_preview(preview_items, preview_path)
    manifest = {
        "schema": "lit_iso.asset_forge.terrain_recolor.v1",
        "source": str(source_path),
        "preview": str(preview_path),
        "outputs": written,
        "note": "Local recolor pass from one approved geometry master; no generation credits spent.",
    }
    manifest_path = output_root / f"{args.prefix}_recolor_manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps(manifest, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
