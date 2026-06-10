#!/usr/bin/env python3
"""Plan or materialize the LIT-ISO isometric reference LoRA dataset."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import struct
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Iterable

try:
    from PIL import Image
except Exception as exc:  # pragma: no cover - surfaced for --apply users.
    Image = None
    PIL_IMPORT_ERROR = exc
else:
    PIL_IMPORT_ERROR = None


REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_SOURCE = REPO_ROOT / "Assets" / "Generated" / "_Review" / "style_lock_sources"
DEFAULT_REPORT = REPO_ROOT / "Temp" / "AssetForge" / "iso_reference_lora_dataset_plan.json"
DEFAULT_OUTPUT = Path(r"C:\Projects\Pixel Pipeline\datasets\lit_iso\style_lock\iso_reference_v1")
LICENSE_STATUS = "user_supplied_reference_pending_license_confirmation"
DATASET_RESOLUTION = 512
IMAGE_EXTENSIONS = {".png", ".jpg", ".jpeg", ".webp"}
OMIT_NAME_TOKENS = {"sheet", "strip", "spritesheet", "preview", "contact", "aseprite"}
DIRECTION_TOKENS = {"N", "S", "E", "W", "NE", "NW", "SE", "SW"}


@dataclass(frozen=True)
class DatasetRecord:
    id: str
    source_path: str
    file_name: str
    text: str
    dataset_image_path: str
    dataset_caption_path: str
    category: str
    asset_family: str
    asset_name: str
    direction: str | None
    motion: str | None
    frame_index: int | None
    width: int | None
    height: int | None
    sha256: str
    caption: str
    license_status: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Inventory Assets/Generated/_Review/style_lock_sources and build a "
            "dry-run or applied LoRA dataset plan."
        )
    )
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE, help="Source review folder.")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT, help="External dataset output folder.")
    parser.add_argument("--report", type=Path, default=DEFAULT_REPORT, help="Dry-run/apply plan report path.")
    parser.add_argument("--apply", action="store_true", help="Copy images and captions into --output.")
    parser.add_argument(
        "--include-sheets",
        action="store_true",
        help="Include sheet/strip/preview images; defaults to extracted frame/tile images only.",
    )
    return parser.parse_args()


def repo_relative(path: Path) -> str:
    try:
        return path.resolve().relative_to(REPO_ROOT).as_posix()
    except ValueError:
        return str(path.resolve())


def stable_slug(text: str) -> str:
    slug = re.sub(r"[^a-zA-Z0-9]+", "_", text).strip("_").lower()
    return slug or "asset"


def read_png_size(path: Path) -> tuple[int | None, int | None]:
    if path.suffix.lower() != ".png":
        return None, None
    with path.open("rb") as handle:
        header = handle.read(24)
    if len(header) < 24 or header[:8] != b"\x89PNG\r\n\x1a\n" or header[12:16] != b"IHDR":
        return None, None
    return struct.unpack(">II", header[16:24])


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def source_images(source: Path, include_sheets: bool) -> Iterable[Path]:
    for path in sorted(source.rglob("*")):
        if not path.is_file() or path.suffix.lower() not in IMAGE_EXTENSIONS:
            continue
        name_tokens = set(re.split(r"[_\-\s]+", path.stem.lower()))
        if not include_sheets and name_tokens & OMIT_NAME_TOKENS:
            continue
        yield path


def classify(path: Path, source: Path) -> tuple[str, str]:
    relative_parts = [part.lower() for part in path.relative_to(source).parts]
    if "critters" in relative_parts:
        family = path.parent.name if path.parent.name.lower() != "aseprite files" else "critters"
        return "critter", family
    if any("tile" in part for part in relative_parts):
        return "tile", path.parent.name
    return "reference", path.parent.name


def parse_name(path: Path) -> tuple[str, str | None, str | None, int | None]:
    tokens = [token for token in re.split(r"[_\-\s]+", path.stem) if token]
    direction = next((token.upper() for token in tokens if token.upper() in DIRECTION_TOKENS), None)
    frame_index = None
    if tokens and tokens[-1].isdigit():
        frame_index = int(tokens[-1])
    motion = None
    if direction:
        direction_index = next(i for i, token in enumerate(tokens) if token.upper() == direction)
        after_direction = tokens[direction_index + 1 :]
        if after_direction and not after_direction[0].isdigit():
            motion = after_direction[0].lower()
    asset_tokens = [
        token.lower()
        for token in tokens
        if token.upper() not in DIRECTION_TOKENS and not token.isdigit() and token.lower() not in {"critter", "tile"}
    ]
    if motion in asset_tokens:
        asset_tokens.remove(motion)
    return "_".join(asset_tokens) or path.stem.lower(), direction, motion, frame_index


def build_caption(record_bits: dict[str, object]) -> str:
    pieces = [
        "LIT-ISO style lock reference",
        "original cozy isometric pixel art",
        "clean readable silhouette",
        "2:1 isometric game asset",
        str(record_bits["category"]).replace("_", " "),
        str(record_bits["asset_name"]).replace("_", " "),
    ]
    if record_bits.get("direction"):
        pieces.append(f"{record_bits['direction']} facing")
    if record_bits.get("motion"):
        pieces.append(f"{record_bits['motion']} animation frame")
    return ", ".join(pieces)


def build_records(source: Path, output: Path, include_sheets: bool) -> list[DatasetRecord]:
    records: list[DatasetRecord] = []
    seen_ids: set[str] = set()
    for image_path in source_images(source, include_sheets):
        category, family = classify(image_path, source)
        asset_name, direction, motion, frame_index = parse_name(image_path)
        width, height = read_png_size(image_path)
        digest = sha256_file(image_path)
        base_id = stable_slug(f"{category}_{family}_{image_path.stem}")
        record_id = base_id
        suffix = 2
        while record_id in seen_ids:
            record_id = f"{base_id}_{suffix}"
            suffix += 1
        seen_ids.add(record_id)
        image_name = f"{record_id}.png"
        caption_name = f"{record_id}.txt"
        file_name = f"images/{image_name}"
        bits = {
            "category": category,
            "asset_name": asset_name,
            "direction": direction,
            "motion": motion,
        }
        records.append(
            DatasetRecord(
                id=record_id,
                source_path=repo_relative(image_path),
                file_name=file_name,
                text=build_caption(bits),
                dataset_image_path=str((output / "images" / image_name).resolve()),
                dataset_caption_path=str((output / "captions" / caption_name).resolve()),
                category=category,
                asset_family=family,
                asset_name=asset_name,
                direction=direction,
                motion=motion,
                frame_index=frame_index,
                width=width,
                height=height,
                sha256=digest,
                caption=build_caption(bits),
                license_status=LICENSE_STATUS,
            )
        )
    return records


def summarize(records: list[DatasetRecord]) -> dict[str, object]:
    by_category: dict[str, int] = {}
    by_family: dict[str, int] = {}
    for record in records:
        by_category[record.category] = by_category.get(record.category, 0) + 1
        family_key = f"{record.category}/{record.asset_family}"
        by_family[family_key] = by_family.get(family_key, 0) + 1
    return {
        "record_count": len(records),
        "by_category": dict(sorted(by_category.items())),
        "by_family": dict(sorted(by_family.items())),
    }


def write_report(report_path: Path, source: Path, output: Path, records: list[DatasetRecord], applied: bool) -> None:
    report_path.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "mode": "apply" if applied else "dry-run",
        "source": str(source.resolve()),
        "external_output": str(output.resolve()),
        "license_status": LICENSE_STATUS,
        "summary": summarize(records),
        "records": [asdict(record) for record in records],
    }
    report_path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def normalize_training_image(source_path: Path, output_path: Path, category: str) -> None:
    if Image is None:
        raise RuntimeError(f"Pillow is required for --apply dataset normalization: {PIL_IMPORT_ERROR}")
    image = Image.open(source_path).convert("RGBA")
    bbox = image.getchannel("A").getbbox()
    cropped = image.crop(bbox) if bbox else image
    canvas = Image.new("RGBA", (DATASET_RESOLUTION, DATASET_RESOLUTION), (0, 0, 0, 0))
    if category == "tile":
        max_w = DATASET_RESOLUTION
        max_h = DATASET_RESOLUTION
        anchor_y = (DATASET_RESOLUTION - cropped.height) // 2
    else:
        max_w = int(DATASET_RESOLUTION * 0.78)
        max_h = int(DATASET_RESOLUTION * 0.78)
        anchor_y = DATASET_RESOLUTION - max(34, DATASET_RESOLUTION // 14)
    scale = min(max_w / max(1, cropped.width), max_h / max(1, cropped.height))
    resized_size = (max(1, round(cropped.width * scale)), max(1, round(cropped.height * scale)))
    resized = cropped.resize(resized_size, Image.Resampling.NEAREST)
    x = (DATASET_RESOLUTION - resized.width) // 2
    if category == "tile":
        y = (DATASET_RESOLUTION - resized.height) // 2
    else:
        y = max(0, anchor_y - resized.height)
    canvas.alpha_composite(resized, (x, y))
    output_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output_path)


def apply_dataset(output: Path, records: list[DatasetRecord]) -> None:
    images_dir = output / "images"
    captions_dir = output / "captions"
    images_dir.mkdir(parents=True, exist_ok=True)
    captions_dir.mkdir(parents=True, exist_ok=True)
    metadata_path = output / "metadata.jsonl"
    manifest_path = output / "dataset_manifest.json"

    metadata_lines: list[str] = []
    for record in records:
        source_path = (REPO_ROOT / record.source_path).resolve()
        image_path = Path(record.dataset_image_path)
        caption_path = Path(record.dataset_caption_path)
        normalize_training_image(source_path, image_path, record.category)
        caption_path.write_text(record.caption + "\n", encoding="utf-8")
        metadata = asdict(record)
        metadata["file_name"] = record.file_name
        metadata["text"] = record.text
        metadata_lines.append(json.dumps(metadata, sort_keys=True))

    metadata_path.write_text("\n".join(metadata_lines) + ("\n" if metadata_lines else ""), encoding="utf-8")
    manifest = {
        "dataset": "lit_iso_style_lock_iso_reference_v1",
        "record_count": len(records),
        "license_status": LICENSE_STATUS,
        "metadata": str(metadata_path.resolve()),
        "images": str(images_dir.resolve()),
        "captions": str(captions_dir.resolve()),
    }
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


def main() -> int:
    args = parse_args()
    source = args.source.resolve()
    output = args.output.resolve()
    report = args.report.resolve()
    if not source.exists():
        raise SystemExit(f"Source folder does not exist: {source}")

    records = build_records(source, output, args.include_sheets)
    if args.apply:
        apply_dataset(output, records)
    write_report(report, source, output, records, args.apply)

    mode = "Applied" if args.apply else "Planned"
    print(f"{mode} {len(records)} LoRA dataset records.")
    print(f"Report: {report}")
    if args.apply:
        print(f"Dataset: {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
