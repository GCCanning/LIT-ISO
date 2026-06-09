#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


DIRECTION_NAME = {
    "S": "south",
    "SE": "south-east",
    "E": "east",
    "NE": "north-east",
    "N": "north",
    "NW": "north-west",
    "W": "west",
    "SW": "south-west",
}
CANONICAL_4D = ["S", "E", "N", "W"]
CANONICAL_8D = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"]
ALPHA_THRESHOLD = 8


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def safe_name(value: str) -> str:
    return "".join(char if char.isalnum() or char in "._-" else "_" for char in value).strip("_")


def rel_path(root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(root).as_posix()
    except ValueError:
        return str(path)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_font(size: int) -> ImageFont.ImageFont:
    for name in ("arial.ttf", "segoeui.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def parse_frame_spec(value: str) -> tuple[str, int, int]:
    if "=" not in value or "," not in value:
        raise argparse.ArgumentTypeError("--frame must use Direction=Column,Row")
    direction, coords = value.split("=", 1)
    column, row = coords.split(",", 1)
    direction = direction.strip().upper()
    if direction not in DIRECTION_NAME:
        raise argparse.ArgumentTypeError(f"Unsupported direction: {direction}")
    return direction, int(column.strip()), int(row.strip())


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int] | None:
    alpha = image.convert("RGBA").getchannel("A")
    raw = alpha.point(lambda value: 255 if value > ALPHA_THRESHOLD else 0)
    return raw.getbbox()


def image_stats(image: Image.Image) -> dict[str, Any]:
    rgba = image.convert("RGBA")
    width, height = rgba.size
    pixels = rgba.load()
    min_x = width
    min_y = height
    max_x = -1
    max_y = -1
    alpha_sum = 0
    weighted_x = 0.0
    weighted_y = 0.0
    opaque = 0
    corner_alpha = 0
    for x, y in ((0, 0), (width - 1, 0), (0, height - 1), (width - 1, height - 1)):
        corner_alpha += pixels[x, y][3]
    for y in range(height):
        for x in range(width):
            alpha = pixels[x, y][3]
            if alpha <= ALPHA_THRESHOLD:
                continue
            opaque += 1
            alpha_sum += alpha
            weighted_x += x * alpha
            weighted_y += y * alpha
            min_x = min(min_x, x)
            min_y = min(min_y, y)
            max_x = max(max_x, x)
            max_y = max(max_y, y)
    if opaque == 0:
        return {
            "image_size": {"width": width, "height": height},
            "alpha_bbox": None,
            "centroid": None,
            "foreground_coverage": 0.0,
            "corner_alpha_sum": corner_alpha,
        }
    return {
        "image_size": {"width": width, "height": height},
        "alpha_bbox": {"x": min_x, "y": min_y, "width": max_x - min_x + 1, "height": max_y - min_y + 1},
        "centroid": {"x": round(weighted_x / alpha_sum, 3), "y": round(weighted_y / alpha_sum, 3)},
        "foreground_coverage": round(opaque / float(width * height), 6),
        "corner_alpha_sum": corner_alpha,
    }


def normalize_cell(
    cell: Image.Image,
    output_width: int,
    output_height: int,
    max_fill: float,
    allow_upscale: bool,
) -> tuple[Image.Image, dict[str, Any]]:
    source = cell.convert("RGBA")
    bbox = alpha_bbox(source)
    output = Image.new("RGBA", (output_width, output_height), (0, 0, 0, 0))
    if bbox is None:
        return output, {"source_bbox": None, "scale": 1.0, "draw_rect": None}
    cropped = source.crop(bbox)
    max_w = max(1, int(output_width * max_fill))
    max_h = max(1, int(output_height * max_fill))
    scale = min(max_w / cropped.width, max_h / cropped.height)
    if not allow_upscale:
        scale = min(scale, 1.0)
    draw_w = max(1, int(round(cropped.width * scale)))
    draw_h = max(1, int(round(cropped.height * scale)))
    if (draw_w, draw_h) != cropped.size:
        cropped = cropped.resize((draw_w, draw_h), Image.Resampling.NEAREST)
    draw_x = int(round((output_width - draw_w) / 2))
    draw_y = output_height - draw_h
    output.alpha_composite(cropped, (draw_x, draw_y))
    return output, {
        "source_bbox": {"x": bbox[0], "y": bbox[1], "width": bbox[2] - bbox[0], "height": bbox[3] - bbox[1]},
        "scale": round(scale, 4),
        "draw_rect": {"x": draw_x, "y": draw_y, "width": draw_w, "height": draw_h},
    }


def warnings_for(direction: str, stats: dict[str, Any], output_width: int, output_height: int) -> list[dict[str, Any]]:
    warnings: list[dict[str, Any]] = []
    bbox = stats.get("alpha_bbox")
    if bbox is None:
        warnings.append({"severity": "error", "direction": direction, "kind": "blank_frame"})
        return warnings
    if stats.get("corner_alpha_sum", 0) > 0:
        warnings.append({"severity": "warning", "direction": direction, "kind": "opaque_corner_alpha", "corner_alpha_sum": stats["corner_alpha_sum"]})
    if bbox["height"] > output_height * 0.96:
        warnings.append({"severity": "warning", "direction": direction, "kind": "fills_cell_height", "height": bbox["height"]})
    if bbox["width"] > output_width * 0.86:
        warnings.append({"severity": "warning", "direction": direction, "kind": "fills_cell_width", "width": bbox["width"]})
    centroid = stats.get("centroid")
    if centroid and abs(float(centroid["x"]) - (output_width / 2.0)) > output_width * 0.16:
        warnings.append({"severity": "warning", "direction": direction, "kind": "off_center_x", "centroid_x": centroid["x"]})
    return warnings


def draw_contact(frames: list[dict[str, Any]], output: Path, cell_width: int, cell_height: int) -> None:
    label_h = 34
    scale = 2
    font = load_font(14)
    width = cell_width * scale * len(frames)
    height = cell_height * scale + label_h
    sheet = Image.new("RGBA", (width, height), (28, 31, 38, 255))
    draw = ImageDraw.Draw(sheet)
    for index, frame in enumerate(frames):
        x = index * cell_width * scale
        with Image.open(frame["source_image_abs"]) as source:
            preview = source.convert("RGBA").resize((cell_width * scale, cell_height * scale), Image.Resampling.NEAREST)
        tile_bg = Image.new("RGBA", preview.size, (20, 23, 29, 255))
        tile_bg.alpha_composite(preview)
        sheet.alpha_composite(tile_bg, (x, 0))
        draw.rectangle((x, 0, x + cell_width * scale - 1, height - 1), outline=(76, 84, 100, 255), width=1)
        draw.text((x + 8, cell_height * scale + 8), f"{frame['direction']} canonical ref", fill=(230, 236, 244, 255), font=font)
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output)


def write_dataset_pack(
    project_root: Path,
    dataset_root: Path,
    pack_name: str,
    frames: list[dict[str, Any]],
    sheet: Path,
    contact_sheet: Path,
    manifest: Path,
    character_description: str,
    action: str,
    license_name: str,
    author: str,
) -> Path:
    pack_root = dataset_root / "direction_oracles" / pack_name
    image_root = pack_root / "reference"
    sheet_root = pack_root / "sheet"
    caption_root = pack_root / "captions"
    metadata_root = pack_root / "metadata"
    for folder in (image_root, sheet_root, caption_root, metadata_root):
        folder.mkdir(parents=True, exist_ok=True)

    records = []
    for frame in frames:
        source = Path(frame["source_image_abs"])
        dest = image_root / f"{pack_name}_{frame['direction'].lower()}.png"
        shutil.copy2(source, dest)
        direction_text = DIRECTION_NAME[frame["direction"]]
        caption = (
            f"LIT-ISO pixel sprite, {character_description}, {action}, {direction_text}, "
            "frame 1 of 1, isometric view, pixel art, transparent background, "
            "consistent 4-direction camera, bottom-center anchor"
        )
        (caption_root / f"{dest.stem}.txt").write_text(caption, encoding="utf-8")
        records.append(
            {
                "file_name": rel_path(pack_root, dest),
                "text": caption,
                "asset_mode": "character",
                "character_description": character_description,
                "action": action,
                "direction": frame["direction"],
                "direction_name": direction_text,
                "frame_index": frame["index"],
                "frame_count": 1,
                "source_image": rel_path(project_root, Path(frame["source_image_abs"])),
                "oracle_manifest": rel_path(project_root, manifest),
                "rect": frame["rect"],
                "pivot": frame["pivot"],
                "camera": "consistent_2.5d_isometric_sprite",
                "anchor": "bottom_center",
                "license": license_name,
                "author": author,
                "sha256": sha256(dest),
                "usage": "approved_direction_oracle_training_and_evaluation",
            }
        )
    metadata_jsonl = pack_root / "metadata.jsonl"
    metadata_jsonl.write_text("\n".join(json.dumps(record, separators=(",", ":")) for record in records) + "\n", encoding="utf-8")
    sheet_dest = sheet_root / f"{pack_name}_sheet.png"
    contact_dest = sheet_root / f"{pack_name}_contact.png"
    shutil.copy2(sheet, sheet_dest)
    shutil.copy2(contact_sheet, contact_dest)
    capture_manifest = {
        "schema": "lit_iso.asset_forge.direction_oracle_dataset.v1",
        "generated_utc": utc_now(),
        "pack_name": pack_name,
        "oracle_manifest": rel_path(project_root, manifest),
        "dataset_root": rel_path(project_root, pack_root),
        "record_count": len(records),
        "directions": [frame["direction"] for frame in frames],
        "sheet": rel_path(pack_root, sheet_dest),
        "contact_sheet": rel_path(pack_root, contact_dest),
        "metadata_jsonl": "metadata.jsonl",
        "license": license_name,
        "author": author,
        "notes": [
            "Factory-built direction oracle pack.",
            "Use as camera/direction anchor data after human approval.",
            "Do not train on unlicensed or unapproved source frames.",
        ],
    }
    (metadata_root / "capture_manifest.json").write_text(json.dumps(capture_manifest, indent=2), encoding="utf-8")
    return pack_root


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a normalized LIT-ISO direction oracle from an approved sprite sheet.")
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--sheet", type=Path, required=True)
    parser.add_argument("--pack-name", required=True)
    parser.add_argument("--frame", action="append", required=True, type=parse_frame_spec, help="Direction=Column,Row")
    parser.add_argument("--cell-width", type=int, default=128)
    parser.add_argument("--cell-height", type=int, default=128)
    parser.add_argument("--output-width", type=int, default=128)
    parser.add_argument("--output-height", type=int, default=128)
    parser.add_argument("--max-fill", type=float, default=0.92)
    parser.add_argument("--allow-upscale", action="store_true")
    parser.add_argument("--out-root", type=Path, default=Path("Assets/Generated/_Review/_DirectionOracles"))
    parser.add_argument("--dataset-root", type=Path, default=Path(r"C:\Projects\Pixel Pipeline\datasets\lit_iso"))
    parser.add_argument("--capture-dataset", action="store_true")
    parser.add_argument("--character-description", default="armored knight with cyan energy trim, amber runes, dark hood, glowing sword")
    parser.add_argument("--action", default="idle pose")
    parser.add_argument("--license", default="project_internal_or_explicitly_licensed")
    parser.add_argument("--author", default="LIT-ISO")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    sheet_path = args.sheet if args.sheet.is_absolute() else project_root / args.sheet
    if not sheet_path.exists():
        raise FileNotFoundError(f"Missing source sheet: {sheet_path}")
    pack_name = safe_name(args.pack_name)
    out_root = args.out_root if args.out_root.is_absolute() else project_root / args.out_root
    oracle_root = out_root / pack_name
    frames_root = oracle_root / "frames"
    frames_root.mkdir(parents=True, exist_ok=True)

    direction_order = [direction for direction, _, _ in args.frame]
    canonical = CANONICAL_8D if len(direction_order) == 8 else CANONICAL_4D
    ordered_specs = sorted(args.frame, key=lambda item: canonical.index(item[0]) if item[0] in canonical else 999)

    with Image.open(sheet_path) as source_sheet:
        sheet = source_sheet.convert("RGBA")

    frame_entries: list[dict[str, Any]] = []
    issues: list[dict[str, Any]] = []
    packed = Image.new("RGBA", (args.output_width * len(ordered_specs), args.output_height), (0, 0, 0, 0))
    for index, (direction, column, row) in enumerate(ordered_specs):
        left = column * args.cell_width
        top = row * args.cell_height
        right = left + args.cell_width
        bottom = top + args.cell_height
        if left < 0 or top < 0 or right > sheet.width or bottom > sheet.height:
            raise ValueError(f"{direction} cell outside sheet bounds: {left},{top},{right},{bottom} sheet={sheet.size}")
        raw_cell = sheet.crop((left, top, right, bottom))
        normalized, normalization = normalize_cell(
            raw_cell,
            args.output_width,
            args.output_height,
            args.max_fill,
            args.allow_upscale,
        )
        frame_path = frames_root / f"{pack_name}_{direction.lower()}.png"
        normalized.save(frame_path)
        x = index * args.output_width
        packed.alpha_composite(normalized, (x, 0))
        stats = image_stats(normalized)
        issues.extend(warnings_for(direction, stats, args.output_width, args.output_height))
        frame_entries.append(
            {
                "index": index,
                "direction": direction,
                "source_image": rel_path(project_root, frame_path),
                "source_image_abs": str(frame_path),
                "source_sheet": rel_path(project_root, sheet_path),
                "source_cell": {"column": column, "row": row, "x": left, "y": top, "width": args.cell_width, "height": args.cell_height},
                "normalization": normalization,
                "metrics": stats,
                "rect": {"x": x, "y": 0, "width": args.output_width, "height": args.output_height},
                "pivot": {"x": 0.5, "y": 0.0},
            }
        )

    missing = [direction for direction in canonical if direction not in direction_order]
    for direction in missing:
        issues.append({"severity": "info", "direction": direction, "kind": "missing_direction_not_in_source"})

    sheet_output = oracle_root / f"{pack_name}_sheet.png"
    contact_output = oracle_root / f"{pack_name}_contact.png"
    manifest_output = oracle_root / f"{pack_name}_manifest.json"
    validation_output = oracle_root / f"{pack_name}_validation.json"
    packed.save(sheet_output)
    draw_contact(frame_entries, contact_output, args.output_width, args.output_height)

    manifest = {
        "schema": "lit_iso.asset_forge.reference_direction_sheet.v1",
        "generated_utc": utc_now(),
        "pack_name": pack_name,
        "source_sheet": rel_path(project_root, sheet_path),
        "sheet": rel_path(project_root, sheet_output),
        "contact_sheet": rel_path(project_root, contact_output),
        "cell": {"width": args.output_width, "height": args.output_height},
        "directions": [frame["direction"] for frame in frame_entries],
        "frames": [{key: value for key, value in frame.items() if key != "source_image_abs"} for frame in frame_entries],
        "use": "canonical camera/framing oracle for generation QA and direction/style training anchors",
    }
    manifest_output.write_text(json.dumps(manifest, indent=2), encoding="utf-8")

    validation = {
        "schema": "lit_iso.asset_forge.direction_oracle_validation.v1",
        "generated_utc": utc_now(),
        "pack_name": pack_name,
        "manifest": rel_path(project_root, manifest_output),
        "summary": {
            "frame_count": len(frame_entries),
            "warning_count": sum(1 for issue in issues if issue["severity"] == "warning"),
            "error_count": sum(1 for issue in issues if issue["severity"] == "error"),
            "info_count": sum(1 for issue in issues if issue["severity"] == "info"),
        },
        "issues": issues,
        "directions": [frame["direction"] for frame in frame_entries],
        "metrics": {frame["direction"]: frame["metrics"] for frame in frame_entries},
    }
    validation_output.write_text(json.dumps(validation, indent=2), encoding="utf-8")

    dataset_pack = None
    if args.capture_dataset:
        dataset_root = args.dataset_root if args.dataset_root.is_absolute() else project_root / args.dataset_root
        dataset_pack = write_dataset_pack(
            project_root,
            dataset_root,
            pack_name,
            frame_entries,
            sheet_output,
            contact_output,
            manifest_output,
            args.character_description,
            args.action,
            args.license,
            args.author,
        )

    print(
        json.dumps(
            {
                "ok": True,
                "pack_name": pack_name,
                "manifest": rel_path(project_root, manifest_output),
                "validation": rel_path(project_root, validation_output),
                "contact_sheet": rel_path(project_root, contact_output),
                "sheet": rel_path(project_root, sheet_output),
                "dataset_pack": rel_path(project_root, dataset_pack) if dataset_pack else None,
                "summary": validation["summary"],
            },
            indent=2,
        )
    )
    return 1 if validation["summary"]["error_count"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
