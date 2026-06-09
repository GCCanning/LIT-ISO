#!/usr/bin/env python3
"""
Build a training-ready index from the approved OpenGameArt 8D character pack.

The output has two layers:
  1. A full JSONL index over every extracted PNG, without duplicating all source
     images.
  2. A compact ready-to-train copied subset for selected base-body folders.

Default output is outside Unity's Assets tree so Unity does not import hundreds
of thousands of source frames.
"""

from __future__ import annotations

import argparse
import csv
import json
import re
import shutil
import sys
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageDraw

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")

SOURCE_PAGE = "https://opengameart.org/content/400-items-basehumanmale-orc-skeleton"
LICENSE = "CC-BY 4.0"
ATTRIBUTION = "LinksDream / OpenGameArt page credits"
DATASET_STATUS = "training_index_ready"

FILENAME_RE = re.compile(r"^(?P<index>\d+)_(?P<action>[^_]+)_CAM(?P<cam>\d+)_(?P<frame>\d+)\.png$", re.IGNORECASE)

CANONICAL_LITISO_8D = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"]
CAM_TO_LITISO = {
    7: "S",
    6: "SE",
    5: "E",
    4: "NE",
    3: "N",
    2: "NW",
    1: "W",
    0: "SW",
}
LITISO_TO_CAM = {direction: cam for cam, direction in CAM_TO_LITISO.items()}

LAYER_HINTS: list[tuple[str, str]] = [
    ("base", "base_body"),
    ("head", "head"),
    ("hair", "hair"),
    ("beard", "hair"),
    ("helm", "headgear"),
    ("hat", "headgear"),
    ("hood", "headgear"),
    ("chest", "torso"),
    ("breast", "torso"),
    ("shirt", "torso"),
    ("robe", "torso"),
    ("armor", "armor"),
    ("armour", "armor"),
    ("shoulder", "shoulder"),
    ("armguard", "arms"),
    ("arm guard", "arms"),
    ("glove", "hands"),
    ("gauntlet", "hands"),
    ("hand", "hands"),
    ("belt", "belt"),
    ("kilt", "legs"),
    ("pants", "legs"),
    ("leg", "legs"),
    ("kneepad", "legs"),
    ("boot", "feet"),
    ("shoe", "feet"),
    ("sword", "weapon"),
    ("axe", "weapon"),
    ("bow", "weapon"),
    ("dagger", "weapon"),
    ("spear", "weapon"),
    ("staff", "weapon"),
    ("shield", "shield"),
    ("tattoo", "body_marking"),
    ("scale", "body_marking"),
]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def json_dump(path: Path, payload: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def safe_stem(value: str) -> str:
    text = re.sub(r"[^A-Za-z0-9]+", "_", value).strip("_").lower()
    return text or "unnamed"


def infer_layer_kind(layer_name: str) -> str:
    lowered = re.sub(r"([a-z])([A-Z])", r"\1 \2", layer_name).lower()
    for needle, category in LAYER_HINTS:
        if needle in lowered:
            return category
    return "item_layer"


def readable_layer_name(layer_name: str) -> str:
    spaced = re.sub(r"([a-z])([A-Z])", r"\1 \2", layer_name)
    spaced = re.sub(r"[_-]+", " ", spaced)
    return re.sub(r"\s+", " ", spaced).strip()


def direction_phrase(direction: str) -> str:
    return {
        "S": "south",
        "SE": "south-east",
        "E": "east",
        "NE": "north-east",
        "N": "north",
        "NW": "north-west",
        "W": "west",
        "SW": "south-west",
    }[direction]


def make_caption(record: dict) -> str:
    layer = readable_layer_name(record["layer_name"])
    action = record["action"].lower()
    direction = direction_phrase(record["direction"])
    return (
        "litiso_oga8d_motion_reference, "
        f"cc_by_oga_source, layer {layer}, layer kind {record['layer_kind']}, "
        f"{action} animation, facing {direction}, direction {record['direction']}, "
        f"frame {record['sequence_frame_index'] + 1} of {record['sequence_frame_count']}, "
        "8-direction RPG character motion, transparent game sprite frame, "
        "full body, centered, no scene, no floor, no text"
    )


def image_stats(path: Path) -> dict:
    image = Image.open(path).convert("RGBA")
    width, height = image.size
    alpha = image.getchannel("A")
    bbox = alpha.getbbox()
    alpha_values = alpha.get_flattened_data() if hasattr(alpha, "get_flattened_data") else alpha.getdata()
    opaque = sum(1 for value in alpha_values if value > 0)
    coverage = opaque / float(max(1, width * height))
    return {
        "width": width,
        "height": height,
        "alpha_coverage": round(coverage, 5),
        "bbox": list(bbox) if bbox else None,
    }


def iter_parseable_frames(source_root: Path) -> list[dict]:
    raw_records: list[dict] = []
    for folder in sorted(source_root.glob("part-*/*")):
        if not folder.is_dir():
            continue
        layer_name = folder.name
        layer_kind = infer_layer_kind(layer_name)
        for path in sorted(folder.glob("*.png")):
            match = FILENAME_RE.match(path.name)
            if not match:
                continue
            cam = int(match.group("cam"))
            if cam not in CAM_TO_LITISO:
                continue
            raw_records.append(
                {
                    "source_path": path,
                    "source_relative_path": path.relative_to(source_root).as_posix(),
                    "part": folder.parent.name,
                    "layer_name": layer_name,
                    "layer_kind": layer_kind,
                    "source_action_index": int(match.group("index")),
                    "action": match.group("action"),
                    "cam": cam,
                    "direction": CAM_TO_LITISO[cam],
                    "source_frame_number": int(match.group("frame")),
                }
            )

    grouped: dict[tuple[str, str, str, int], list[dict]] = defaultdict(list)
    for record in raw_records:
        grouped[(record["part"], record["layer_name"], record["action"].lower(), record["cam"])].append(record)

    for group_records in grouped.values():
        group_records.sort(key=lambda item: item["source_frame_number"])
        count = len(group_records)
        for index, record in enumerate(group_records):
            record["sequence_frame_index"] = index
            record["sequence_frame_count"] = count
            record["caption"] = make_caption(record)

    raw_records.sort(
        key=lambda item: (
            item["part"],
            item["layer_name"].lower(),
            item["source_action_index"],
            item["cam"],
            item["source_frame_number"],
        )
    )
    return raw_records


def write_full_index(out_dir: Path, source_root: Path, records: list[dict]) -> dict:
    index_dir = out_dir / "index"
    index_dir.mkdir(parents=True, exist_ok=True)

    metadata_path = index_dir / "frames_index.jsonl"
    with metadata_path.open("w", encoding="utf-8") as handle:
        for record in records:
            payload = dict(record)
            payload["source_path"] = str(record["source_path"])
            handle.write(json.dumps(payload, separators=(",", ":")) + "\n")

    by_layer_kind = Counter(record["layer_kind"] for record in records)
    by_action = Counter(record["action"] for record in records)
    by_direction = Counter(record["direction"] for record in records)
    by_part = Counter(record["part"] for record in records)
    by_layer = Counter(record["layer_name"] for record in records)

    layer_summary = {
        "schemaVersion": 1,
        "generated_utc": utc_now(),
        "source_root": str(source_root),
        "record_count": len(records),
        "layer_count": len(by_layer),
        "by_part": dict(sorted(by_part.items())),
        "by_layer_kind": dict(by_layer_kind.most_common()),
        "by_action": dict(by_action.most_common()),
        "by_direction": {direction: by_direction.get(direction, 0) for direction in CANONICAL_LITISO_8D},
        "top_layers": dict(by_layer.most_common(80)),
    }
    json_dump(index_dir / "layer_summary.json", layer_summary)

    csv_path = index_dir / "action_direction_counts.csv"
    action_direction = Counter((record["action"], record["direction"]) for record in records)
    with csv_path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(["action", *CANONICAL_LITISO_8D])
        for action in sorted(by_action):
            writer.writerow([action, *[action_direction.get((action, direction), 0) for direction in CANONICAL_LITISO_8D]])

    return {
        "frames_index_jsonl": str(metadata_path),
        "layer_summary": str(index_dir / "layer_summary.json"),
        "action_direction_counts_csv": str(csv_path),
        "record_count": len(records),
        "layer_count": len(by_layer),
    }


def split_for_record(sequence_number: int, validation_every: int) -> str:
    if validation_every > 0 and sequence_number % validation_every == 0:
        return "validation"
    return "train"


def copy_ready_subset(out_dir: Path, records: list[dict], ready_folders: set[str], validation_every: int) -> dict:
    ready_dir = out_dir / "ready_training_subset"
    images_dir = ready_dir / "images"
    captions_dir = ready_dir / "captions"
    provenance_dir = ready_dir / "provenance"
    for directory in (images_dir, captions_dir, provenance_dir):
        directory.mkdir(parents=True, exist_ok=True)

    selected = [record for record in records if record["layer_name"] in ready_folders]
    metadata_path = ready_dir / "metadata.jsonl"
    train_path = ready_dir / "train.txt"
    val_path = ready_dir / "val.txt"
    qa_path = ready_dir / "qa_report.json"
    copied_records: list[dict] = []
    train: list[str] = []
    val: list[str] = []
    qa_items: list[dict] = []

    for index, record in enumerate(selected, start=1):
        stem = "_".join(
            [
                "oga8d",
                safe_stem(record["layer_name"]),
                safe_stem(record["action"]),
                record["direction"].lower(),
                f"{record['sequence_frame_index']:03d}",
            ]
        )
        image_dest = images_dir / f"{stem}.png"
        caption_dest = captions_dir / f"{stem}.txt"
        shutil.copy2(record["source_path"], image_dest)
        caption_dest.write_text(record["caption"] + "\n", encoding="utf-8")
        split = split_for_record(index, validation_every)
        rel_image = f"images/{image_dest.name}"
        if split == "validation":
            val.append(rel_image)
        else:
            train.append(rel_image)

        stats = image_stats(image_dest)
        issues = []
        if stats["width"] != 148 or stats["height"] != 130:
            issues.append("unexpected_source_dimensions")
        if stats["alpha_coverage"] <= 0.005:
            issues.append("blank_or_nearly_blank")

        payload = {
            "file_name": rel_image,
            "caption_file": f"captions/{caption_dest.name}",
            "text": record["caption"],
            "split": split,
            "category": "oga_8d_motion_direction",
            "license": LICENSE,
            "attribution": ATTRIBUTION,
            "source_page": SOURCE_PAGE,
            "source_image": str(record["source_path"]),
            "source_relative_path": record["source_relative_path"],
            "part": record["part"],
            "layer_name": record["layer_name"],
            "layer_kind": record["layer_kind"],
            "action": record["action"],
            "direction": record["direction"],
            "direction_phrase": direction_phrase(record["direction"]),
            "cam": f"CAM{record['cam']}",
            "source_frame_number": record["source_frame_number"],
            "sequence_frame_index": record["sequence_frame_index"],
            "sequence_frame_count": record["sequence_frame_count"],
            "training_approval": "approved_2026-06-08",
            "image_stats": stats,
        }
        copied_records.append(payload)
        qa_items.append({"file_name": rel_image, "status": "pass" if not issues else "needs_review", "issues": issues, "stats": stats})

    with metadata_path.open("w", encoding="utf-8") as handle:
        for record in copied_records:
            handle.write(json.dumps(record, separators=(",", ":")) + "\n")
    train_path.write_text("\n".join(train) + ("\n" if train else ""), encoding="utf-8")
    val_path.write_text("\n".join(val) + ("\n" if val else ""), encoding="utf-8")
    json_dump(
        qa_path,
        {
            "schemaVersion": 1,
            "generated_utc": utc_now(),
            "status": "pass" if all(item["status"] == "pass" for item in qa_items) else "needs_review",
            "items": qa_items,
        },
    )

    notice = (
        "# Attribution Notice\n\n"
        f"This dataset subset uses frames from {SOURCE_PAGE}.\n\n"
        f"License: {LICENSE}\n\n"
        f"Attribution recorded locally: {ATTRIBUTION}\n\n"
        "Owner approved local training use on 2026-06-08. Derived models must record that CC-BY 4.0 source material was included.\n"
    )
    (ready_dir / "NOTICE_ATTRIBUTION.md").write_text(notice, encoding="utf-8")

    contact = write_ready_contact_sheet(ready_dir, copied_records)

    return {
        "ready_training_subset": str(ready_dir),
        "records": len(copied_records),
        "train": len(train),
        "validation": len(val),
        "metadata_jsonl": str(metadata_path),
        "train_txt": str(train_path),
        "val_txt": str(val_path),
        "qa_report": str(qa_path),
        "contact_sheet": contact,
        "folders": sorted(ready_folders),
    }


def write_ready_contact_sheet(ready_dir: Path, records: list[dict]) -> str | None:
    if not records:
        return None

    by_key: dict[tuple[str, str], list[dict]] = defaultdict(list)
    for record in records:
        by_key[(record["action"], record["direction"])].append(record)

    actions = sorted({record["action"] for record in records}, key=lambda value: ["Idle", "Attack", "Bow", "Cast", "Walk", "Run", "Death"].index(value) if value in ["Idle", "Attack", "Bow", "Cast", "Walk", "Run", "Death"] else value)
    cell_w, cell_h = 148, 130
    scale = 2
    label_w = 150
    header_h = 70
    label_h = 28
    width = label_w + len(CANONICAL_LITISO_8D) * cell_w * scale
    height = header_h + len(actions) * (cell_h * scale + label_h)
    sheet = Image.new("RGBA", (width, height), (16, 18, 24, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((10, 10), "OGA 8D ready training subset", fill=(235, 240, 245, 255))
    draw.text((10, 34), "Mid-frame samples, LIT-ISO order: S, SE, E, NE, N, NW, W, SW", fill=(190, 200, 215, 255))

    for col, direction in enumerate(CANONICAL_LITISO_8D):
        x = label_w + col * cell_w * scale
        draw.text((x + 8, 48), direction, fill=(235, 240, 245, 255))

    for row, action in enumerate(actions):
        y = header_h + row * (cell_h * scale + label_h)
        draw.text((10, y + 10), action, fill=(235, 240, 245, 255))
        for col, direction in enumerate(CANONICAL_LITISO_8D):
            x = label_w + col * cell_w * scale
            candidates = sorted(by_key.get((action, direction), []), key=lambda item: item["sequence_frame_index"])
            if not candidates:
                draw.rectangle((x, y, x + cell_w * scale - 1, y + cell_h * scale - 1), outline=(150, 60, 60, 255))
                draw.text((x + 10, y + 110), "missing", fill=(255, 170, 170, 255))
                continue
            record = candidates[len(candidates) // 2]
            image = Image.open(ready_dir / record["file_name"]).convert("RGBA")
            preview = image.resize((cell_w * scale, cell_h * scale), Image.Resampling.NEAREST)
            bg = Image.new("RGBA", preview.size, (238, 238, 238, 255))
            bg.alpha_composite(preview)
            sheet.alpha_composite(bg, (x, y))
            draw.rectangle((x, y, x + cell_w * scale - 1, y + cell_h * scale - 1), outline=(66, 74, 90, 255))
            draw.text((x + 6, y + cell_h * scale + 5), Path(record["file_name"]).stem[-22:], fill=(210, 220, 230, 255))

    path = ready_dir / "ready_subset_contact_sheet.png"
    sheet.save(path)
    return str(path)


def write_manifest(out_dir: Path, source_root: Path, index_result: dict, subset_result: dict | None, records: list[dict], args: argparse.Namespace) -> Path:
    manifest = {
        "schemaVersion": 1,
        "generated_utc": utc_now(),
        "status": DATASET_STATUS,
        "dataset_name": "oga_8d_motion_direction_v1",
        "source_page": SOURCE_PAGE,
        "source_root": str(source_root),
        "license": LICENSE,
        "attribution": ATTRIBUTION,
        "owner_training_approval": {
            "approved": True,
            "approved_date": "2026-06-08",
            "allowed_uses": [
                "local LoRA training",
                "local adapter training",
                "direction QA oracle",
                "sprite-sheet packing reference",
            ],
            "conditions": [
                "Preserve CC-BY 4.0 attribution in dataset and model manifests",
                "Label derived models as trained with CC-BY 4.0 source material",
                "Do not ship raw source pixels without final game attribution",
                "Keep distinct from original-only LIT-ISO datasets",
            ],
        },
        "canonical_litiso_8d": CANONICAL_LITISO_8D,
        "direction_mapping": {f"CAM{cam}": direction for cam, direction in sorted(CAM_TO_LITISO.items(), reverse=True)},
        "record_count": len(records),
        "index": index_result,
        "ready_training_subset": subset_result,
        "replace_used": args.replace,
        "next_step": "Train a small motion/direction LoRA from ready_training_subset, then evaluate direction correctness before adding item-layer composites.",
    }
    path = out_dir / "dataset_manifest.json"
    json_dump(path, manifest)
    return path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build an OGA 8D motion/direction training index.")
    parser.add_argument("--source-root", type=Path, default=Path(r"C:\Projects\Pixel Pipeline\sources\opengameart\400-items-basehumanmale-orc-skeleton\extracted"))
    parser.add_argument("--out-dataset", type=Path, default=Path(r"C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\oga_8d_motion_direction_v1"))
    parser.add_argument("--ready-folder", action="append", default=["BaseHumanMale"], help="Folder name to copy into the ready-to-train subset. Repeatable.")
    parser.add_argument("--validation-every", type=int, default=10)
    parser.add_argument("--replace", action="store_true")
    parser.add_argument("--index-only", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    source_root = args.source_root
    if not source_root.exists():
        raise FileNotFoundError(f"Missing source root: {source_root}")

    out_dir = args.out_dataset
    if out_dir.exists() and args.replace:
        normalized = str(out_dir).replace("\\", "/").lower()
        if "oga_8d_motion_direction" not in normalized:
            raise RuntimeError(f"Refusing to replace unexpected dataset path: {out_dir}")
        shutil.rmtree(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)

    records = iter_parseable_frames(source_root)
    if not records:
        raise RuntimeError(f"No parseable OGA frames found under {source_root}")

    index_result = write_full_index(out_dir, source_root, records)
    subset_result = None
    if not args.index_only:
        subset_result = copy_ready_subset(out_dir, records, set(args.ready_folder), args.validation_every)
    manifest_path = write_manifest(out_dir, source_root, index_result, subset_result, records, args)

    print(
        json.dumps(
            {
                "dataset": str(out_dir),
                "status": DATASET_STATUS,
                "records": len(records),
                "full_index": index_result["frames_index_jsonl"],
                "ready_subset_records": subset_result["records"] if subset_result else 0,
                "manifest": str(manifest_path),
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
