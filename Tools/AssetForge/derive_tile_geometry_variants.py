#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import random
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def rel(root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path)


def alpha_bbox(image: Image.Image):
    return image.getchannel("A").getbbox()


def crop_foreground(image: Image.Image) -> Image.Image:
    box = alpha_bbox(image)
    return image.crop(box) if box else image


def quantize(image: Image.Image, colors: int = 28) -> Image.Image:
    alpha = image.getchannel("A").point(lambda value: 255 if value >= 64 else 0)
    rgb = image.convert("RGBA")
    rgb.putalpha(alpha)
    rgb = rgb.quantize(colors=colors, method=Image.Quantize.FASTOCTREE).convert("RGBA")
    rgb.putalpha(alpha)
    return rgb


def luminance(color: tuple[int, int, int]) -> float:
    return color[0] * 0.2126 + color[1] * 0.7152 + color[2] * 0.0722


MATERIAL_PALETTES: dict[str, list[tuple[int, int, int]]] = {
    # Bright readable isometric palette, tuned from open CC0/CC-BY tile references
    # and the current LIT-ISO target screenshots. Keep the material families
    # separated so tile recolors stay deterministic and training-safe.
    "grass": [(34, 103, 42), (47, 133, 50), (68, 164, 55), (93, 195, 69), (132, 221, 88)],
    "forest_floor": [(25, 72, 45), (34, 95, 53), (48, 122, 61), (69, 150, 71), (101, 179, 87)],
    "dirt": [(86, 48, 36), (122, 68, 44), (158, 91, 54), (194, 125, 72), (224, 166, 98)],
    "stone": [(72, 75, 82), (101, 105, 113), (132, 137, 146), (165, 171, 180), (204, 209, 216)],
    "path": [(122, 86, 47), (158, 113, 58), (194, 146, 76), (222, 180, 105), (240, 210, 146)],
}

SIDE_MATERIAL_FOR_TOP: dict[str, str] = {
    "grass": "dirt",
    "forest_floor": "dirt",
    "path": "dirt",
}


def extract_palette(image: Image.Image, colors: int = 10, material: str | None = None) -> list[tuple[int, int, int]]:
    if material in MATERIAL_PALETTES:
        return MATERIAL_PALETTES[material]
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A")
    bbox = alpha.getbbox()
    cropped = rgba.crop(bbox) if bbox else rgba
    quantized = cropped.quantize(colors=colors, method=Image.Quantize.FASTOCTREE).convert("RGBA")
    counts: dict[tuple[int, int, int], int] = {}
    for r, g, b, a in quantized.getdata():
        if a < 64:
            continue
        counts[(r, g, b)] = counts.get((r, g, b), 0) + 1
    ranked = sorted(counts, key=counts.get, reverse=True)
    ranked = sorted(ranked[:colors], key=luminance)
    if not ranked:
        return [(130, 130, 90), (160, 160, 110), (88, 84, 64)]
    while len(ranked) < 4:
        ranked.append(ranked[-1])
    return ranked


def choose_palette_roles(palette: list[tuple[int, int, int]]) -> dict[str, tuple[int, int, int]]:
    ordered = sorted(palette, key=luminance)
    return {
        "dark": ordered[max(0, len(ordered) // 5)],
        "shadow": ordered[max(0, len(ordered) // 3)],
        "base": ordered[len(ordered) // 2],
        "light": ordered[min(len(ordered) - 1, len(ordered) * 3 // 4)],
        "bright": ordered[-1],
    }


def mix_color(a: tuple[int, int, int], b: tuple[int, int, int], t: float) -> tuple[int, int, int]:
    return (
        int(a[0] + (b[0] - a[0]) * t),
        int(a[1] + (b[1] - a[1]) * t),
        int(a[2] + (b[2] - a[2]) * t),
    )


def inside_mask(mask: Image.Image, x: int, y: int) -> bool:
    return 0 <= x < mask.width and 0 <= y < mask.height and mask.getpixel((x, y)) > 0


def paint_cluster(draw: ImageDraw.ImageDraw, mask: Image.Image, x: int, y: int, color: tuple[int, int, int], rng: random.Random) -> None:
    width = rng.choice([1, 1, 2, 2, 3])
    height = rng.choice([1, 1, 2])
    for oy in range(height):
        for ox in range(width):
            px = x + ox
            py = y + oy
            if inside_mask(mask, px, py) and rng.random() > 0.18:
                draw.point((px, py), fill=color + (255,))


def material_detail_count(material: str) -> int:
    return {
        "grass": 46,
        "forest_floor": 58,
        "dirt": 38,
        "stone": 32,
        "path": 34,
    }.get(material, 34)


def procedural_top(master: Image.Image, material: str, size: int, seed: str) -> Image.Image:
    palette = choose_palette_roles(extract_palette(master, material=material))
    mask = diamond_mask(size, vertical_scale=0.48, y_offset=-2)
    output = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(output)
    draw.bitmap((0, 0), mask, fill=palette["base"] + (255,))
    rng = random.Random(seed)
    # Pixel-detail clusters, biased to horizontal/isometric reads.
    for _ in range(material_detail_count(material)):
        x = rng.randint(6, size - 7)
        y = rng.randint(17, size - 18)
        if not inside_mask(mask, x, y):
            continue
        roll = rng.random()
        if material == "grass":
            color = palette["light"] if roll < 0.46 else palette["shadow"] if roll < 0.78 else palette["bright"]
        elif material == "forest_floor":
            color = palette["shadow"] if roll < 0.48 else palette["light"] if roll < 0.78 else palette["dark"]
        elif material == "stone":
            color = palette["light"] if roll < 0.35 else palette["shadow"] if roll < 0.75 else palette["bright"]
        else:
            color = palette["shadow"] if roll < 0.45 else palette["light"] if roll < 0.82 else palette["dark"]
        paint_cluster(draw, mask, x, y, color, rng)
    # Subtle rim pixels keep the diamond readable without a heavy black outline.
    outline = mask.filter(ImageFilter.FIND_EDGES).point(lambda value: 28 if value else 0)
    rim_color = mix_color(palette["shadow"], palette["base"], 0.38)
    rim = Image.new("RGBA", (size, size), rim_color + (0,))
    rim.putalpha(outline)
    output.alpha_composite(rim)
    return quantize(output, colors=18)


def procedural_side(master: Image.Image, material: str, size: int, direction: str) -> Image.Image:
    side_material = SIDE_MATERIAL_FOR_TOP.get(material, material)
    palette = choose_palette_roles(extract_palette(master, material=side_material))
    mask = side_mask(size, direction)
    output = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(output)
    side_base = mix_color(palette["shadow"], palette["base"], 0.18)
    draw.bitmap((0, 0), mask, fill=side_base + (255,))
    rng = random.Random(f"{material}:side:{direction}")
    for _ in range(26):
        x = rng.randint(4, size - 5)
        y = rng.randint(22, size - 8)
        if not inside_mask(mask, x, y):
            continue
        color = palette["dark"] if rng.random() < 0.55 else palette["base"]
        paint_cluster(draw, mask, x, y, color, rng)
    return quantize(output, colors=12)


def diamond_mask(size: int, vertical_scale: float = 0.5, y_offset: int = 0) -> Image.Image:
    mask = Image.new("L", (size, size), 0)
    draw = ImageDraw.Draw(mask)
    cx = (size - 1) / 2
    half_w = size / 2 - 1
    half_h = size * vertical_scale / 2
    cy = size / 2 + y_offset
    points = [(cx, cy - half_h), (cx + half_w, cy), (cx, cy + half_h), (cx - half_w, cy)]
    draw.polygon(points, fill=255)
    return mask


def fit_texture(texture: Image.Image, size: int, fill: float = 1.25) -> Image.Image:
    cropped = crop_foreground(texture.convert("RGBA"))
    scale = max(size * fill / max(1, cropped.width), size * fill / max(1, cropped.height))
    resized = cropped.resize((max(1, int(cropped.width * scale)), max(1, int(cropped.height * scale))), Image.Resampling.NEAREST)
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    canvas.alpha_composite(resized, ((size - resized.width) // 2, (size - resized.height) // 2))
    return canvas


def masked(texture: Image.Image, mask: Image.Image) -> Image.Image:
    output = texture.copy()
    output.putalpha(mask)
    return quantize(output)


def tint(image: Image.Image, brightness: float = 1.0, contrast: float = 1.0) -> Image.Image:
    rgb = image.convert("RGBA")
    alpha = rgb.getchannel("A")
    rgb = ImageEnhance.Brightness(rgb).enhance(brightness)
    rgb = ImageEnhance.Contrast(rgb).enhance(contrast)
    rgb.putalpha(alpha)
    return rgb


def derive_flat_top(master: Image.Image, material: str, size: int) -> Image.Image:
    return procedural_top(master, material, size, seed=f"{material}:flat")


def side_mask(size: int, direction: str) -> Image.Image:
    mask = Image.new("L", (size, size), 0)
    draw = ImageDraw.Draw(mask)
    cx = (size - 1) / 2
    # Match the top-mask geometry used by derive_edge so side faces share real
    # pixel boundaries with the lower diamond edges.
    cy = size / 2 - 8
    top_half_w = size / 2 - 1
    top_half_h = size * 0.42 / 2
    mid_half_w = size * 0.31
    side_h = size * 0.34
    top = (cx, cy - top_half_h)
    right = (cx + top_half_w, cy)
    bottom = (cx, cy + top_half_h)
    left = (cx - top_half_w, cy)
    if direction == "south":
        points = [
            left,
            bottom,
            right,
            (cx + mid_half_w, cy + side_h),
            (cx, cy + side_h + 8),
            (cx - mid_half_w, cy + side_h),
        ]
    elif direction == "north":
        points = [
            left,
            top,
            right,
            (cx + mid_half_w, cy + side_h * 0.55),
            (cx - mid_half_w, cy + side_h * 0.55),
        ]
    elif direction == "east":
        points = [
            right,
            bottom,
            (cx, cy + top_half_h + side_h),
            (cx + mid_half_w, cy + side_h),
        ]
    elif direction == "west":
        points = [
            left,
            bottom,
            (cx, cy + top_half_h + side_h),
            (cx - mid_half_w, cy + side_h),
        ]
    else:
        points = []
    if points:
        draw.polygon(points, fill=255)
    return mask.filter(ImageFilter.ModeFilter(3))


def derive_edge(master: Image.Image, material: str, size: int, direction: str) -> Image.Image:
    texture = procedural_top(master, material, size, seed=f"{material}:edge:{direction}")
    top_mask = diamond_mask(size, vertical_scale=0.42, y_offset=-8)
    top = masked(texture, top_mask)
    side = procedural_side(master, material, size, direction)
    output = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    output.alpha_composite(side)
    output.alpha_composite(top)
    return quantize(output)


def transition_mask(size: int, direction: str) -> Image.Image:
    return organic_transition_mask(size, direction, seed=f"transition:{direction}")


def direction_score(x: int, y: int, size: int, direction: str) -> float:
    cx = size / 2
    cy = size / 2 - 2
    if direction == "north":
        return (cy - y) / size
    if direction == "south":
        return (y - cy) / size
    if direction == "east":
        return (x - cx) / size
    if direction == "west":
        return (cx - x) / size
    if direction == "north_east":
        return ((cy - y) + (x - cx)) / (size * 1.25)
    if direction == "north_west":
        return ((cy - y) + (cx - x)) / (size * 1.25)
    if direction == "south_east":
        return ((y - cy) + (x - cx)) / (size * 1.25)
    if direction == "south_west":
        return ((y - cy) + (cx - x)) / (size * 1.25)
    return 0.0


def organic_transition_mask(size: int, direction: str, seed: str) -> Image.Image:
    diamond = diamond_mask(size, vertical_scale=0.48, y_offset=-2)
    noise_rng = random.Random(seed)
    low_noise = Image.new("L", (size, size), 0)
    low = ImageDraw.Draw(low_noise)
    for y in range(0, size, 4):
        for x in range(0, size, 4):
            low.rectangle([x, y, x + 3, y + 3], fill=noise_rng.randint(0, 255))
    noise = low_noise.filter(ImageFilter.GaussianBlur(3.5))
    mask = Image.new("L", (size, size), 0)
    pixels = mask.load()
    diamond_px = diamond.load()
    noise_px = noise.load()
    corner = "_" in direction
    # Keep the base material dominant. Transitions are edge-biased patches,
    # not a half-tile replacement.
    threshold = 0.23 if corner else 0.21
    fringe = 0.09 if corner else 0.11
    for y in range(size):
        for x in range(size):
            if diamond_px[x, y] == 0:
                continue
            wobble = (noise_px[x, y] - 128) / 255.0 * 0.15
            score = direction_score(x, y, size, direction) + wobble
            near_edge = score > threshold
            broken_fringe = score > threshold - fringe and noise_px[x, y] > 166
            accent_chip = score > threshold - fringe * 1.65 and noise_px[x, y] > 230
            if near_edge or broken_fringe or accent_chip:
                value = 255
            else:
                value = 0
            pixels[x, y] = value
    # Remove isolated pinholes but keep a stepped pixel-art edge.
    mask = mask.filter(ImageFilter.ModeFilter(3))
    alpha = ImageChops.multiply(mask, diamond)
    return alpha.point(lambda value: 255 if value >= 96 else 0)


def geometric_transition_mask(size: int, direction: str) -> Image.Image:
    mask = diamond_mask(size, vertical_scale=0.48, y_offset=-2)
    draw = ImageDraw.Draw(mask)
    cx = size / 2
    cy = size / 2 - 2
    if direction == "north":
        blocker = [(0, cy), (size, cy), (size, size), (0, size)]
    elif direction == "south":
        blocker = [(0, 0), (size, 0), (size, cy), (0, cy)]
    elif direction == "east":
        blocker = [(0, 0), (cx, 0), (cx, size), (0, size)]
    elif direction == "west":
        blocker = [(cx, 0), (size, 0), (size, size), (cx, size)]
    else:
        blocker = []
    if blocker:
        draw.polygon(blocker, fill=0)
    feather = mask.filter(ImageFilter.GaussianBlur(1.25))
    return feather.point(lambda value: 255 if value >= 96 else 0)


def derive_transition(base_master: Image.Image, overlay_master: Image.Image, size: int, direction: str, base: str, overlay: str) -> Image.Image:
    base_image = procedural_top(base_master, base, size, seed=f"{base}:transition-base:{overlay}:{direction}")
    overlay_image = procedural_top(overlay_master, overlay, size, seed=f"{overlay}:transition-overlay:{base}:{direction}")
    diamond = diamond_mask(size, vertical_scale=0.48, y_offset=-2)
    edge = transition_mask(size, direction)
    base_image.putalpha(diamond)
    overlay_image.putalpha(edge)
    output = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    output.alpha_composite(quantize(base_image, colors=18))
    output.alpha_composite(quantize(overlay_image, colors=18))
    return quantize(output)


def make_contact(items: list[dict], root: Path, output_path: Path) -> None:
    cell = 126
    preview = Image.new("RGBA", (cell * 5, cell * max(1, ((len(items) + 4) // 5))), (28, 32, 34, 255))
    draw = ImageDraw.Draw(preview)
    for i, item in enumerate(items):
        x = (i % 5) * cell
        y = (i // 5) * cell
        draw.rectangle([x, y, x + cell - 1, y + cell - 1], fill=(32, 37, 40, 255), outline=(70, 78, 82, 255))
        image = Image.open(root / item["path"]).convert("RGBA").resize((96, 96), Image.Resampling.NEAREST)
        preview.alpha_composite(image, (x + 15, y + 5))
        draw.text((x + 6, y + 104), item["name"][:20], fill=(220, 226, 218, 255))
    preview.save(output_path)


def make_map_preview(items: list[dict], root: Path, output_path: Path) -> None:
    lookup = {item["name"]: root / item["path"] for item in items}

    def load(name: str) -> Image.Image:
        return Image.open(lookup[name]).convert("RGBA")

    tile = 64
    canvas = Image.new("RGBA", (tile * 9, tile * 7), (33, 45, 34, 255))

    if "greenwake_grass_flat_top_derived.png" in lookup:
        grass = load("greenwake_grass_flat_top_derived.png")
        for gy in range(6):
            for gx in range(8):
                x = (gx - gy) * tile // 2 + 220
                y = (gx + gy) * tile // 4 + 28
                canvas.alpha_composite(grass, (x, y))

    placements = [
        ("greenwake_grass_to_dirt_east_transition_derived.png", 4, 0),
        ("greenwake_dirt_flat_top_derived.png", 5, 0),
        ("greenwake_grass_south_edge_derived.png", 2, 1),
        ("greenwake_grass_south_edge_derived.png", 3, 1),
        ("greenwake_grass_to_dirt_south_transition_derived.png", 4, 1),
        ("greenwake_dirt_south_edge_derived.png", 5, 1),
        ("greenwake_forest_floor_flat_top_derived.png", 1, 2),
        ("greenwake_grass_to_forest_floor_west_transition_derived.png", 2, 2),
        ("greenwake_grass_flat_top_derived.png", 3, 2),
        ("greenwake_grass_to_path_south_east_transition_derived.png", 4, 2),
        ("greenwake_grass_to_path_north_transition_derived.png", 5, 2),
        ("greenwake_path_flat_top_derived.png", 6, 2),
        ("greenwake_stone_flat_top_derived.png", 3, 3),
        ("greenwake_grass_to_stone_south_transition_derived.png", 4, 3),
        ("greenwake_grass_flat_top_derived.png", 5, 3),
        ("greenwake_path_south_edge_derived.png", 6, 3),
    ]
    for name, gx, gy in placements:
        if name in lookup:
            x = (gx - gy) * tile // 2 + 220
            y = (gx + gy) * tile // 4 + 28
            canvas.alpha_composite(load(name), (x, y))
    canvas.save(output_path)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--selected-report", default="Assets/Generated/_Review/greenwake_height_material_masters_selected_v1/review_report.json")
    parser.add_argument("--output-root", default="Assets/Generated/_Review/greenwake_geometry_derived_v1")
    args = parser.parse_args()

    root = Path(args.project_root).resolve()
    report = read_json(root / args.selected_report)
    output_root = (root / args.output_root).resolve()
    biome_dir = output_root / "Greenwake"
    biome_dir.mkdir(parents=True, exist_ok=True)

    masters = {}
    items = []
    for source_item in report.get("items", []):
        material = source_item["material"]
        master = Image.open(root / source_item["path"]).convert("RGBA")
        masters[material] = {"image": master, "path": source_item["path"]}
        variants = {
            "flat_top": derive_flat_top(master, material, 64),
            "south_edge": derive_edge(master, material, 64, "south"),
            "north_edge": derive_edge(master, material, 64, "north"),
            "east_edge": derive_edge(master, material, 64, "east"),
            "west_edge": derive_edge(master, material, 64, "west"),
        }
        for shape, image in variants.items():
            name = f"greenwake_{material}_{shape}_derived.png"
            path = biome_dir / name
            image.save(path)
            items.append({
                "name": name,
                "path": rel(root, path),
                "category": "terrain",
                "biome": "Greenwake",
                "material": material,
                "shape": shape,
                "source_master": source_item["path"],
                "width": image.width,
                "height": image.height,
                "status": "derived_review",
                "unity": {"category": "Tiles", "ppu": 64, "pivot": {"x": 0.5, "y": 0.25 if shape != "flat_top" else 0.5}},
            })

    transition_pairs = [
        ("grass", "dirt"),
        ("grass", "forest_floor"),
        ("grass", "stone"),
        ("grass", "path"),
    ]
    for base, overlay in transition_pairs:
        if base not in masters or overlay not in masters:
            continue
        for direction in ["north", "south", "east", "west", "north_east", "north_west", "south_east", "south_west"]:
            image = derive_transition(masters[base]["image"], masters[overlay]["image"], 64, direction, base, overlay)
            shape = f"{direction}_transition"
            name = f"greenwake_{base}_to_{overlay}_{shape}_derived.png"
            path = biome_dir / name
            image.save(path)
            items.append({
                "name": name,
                "path": rel(root, path),
                "category": "terrain",
                "biome": "Greenwake",
                "material": f"{base}_to_{overlay}",
                "shape": shape,
                "source_master": [masters[base]["path"], masters[overlay]["path"]],
                "width": image.width,
                "height": image.height,
                "status": "derived_review",
                "unity": {"category": "Tiles", "ppu": 64, "pivot": {"x": 0.5, "y": 0.5}},
            })

    contact = output_root / "derived_geometry_contact_sheet.png"
    make_contact(items, root, contact)
    map_preview = output_root / "derived_geometry_map_preview.png"
    make_map_preview(items, root, map_preview)
    manifest = {
        "schema": "lit_iso.asset_forge.derived_tile_geometry.v1",
        "source_report": args.selected_report,
        "contact_sheet": rel(root, contact),
        "map_preview": rel(root, map_preview),
        "total": len(items),
        "items": items,
        "note": "Local deterministic geometry variants from selected Sprixen material masters. Review visually before promotion or training.",
    }
    manifest_path = output_root / "derived_geometry_manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps(manifest, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
