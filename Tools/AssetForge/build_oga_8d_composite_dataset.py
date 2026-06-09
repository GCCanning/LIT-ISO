#!/usr/bin/env python3
"""
Build composited 8D character examples from the approved OGA layer pack.

The body-only LoRA pilot is useful as a smoke test, but it does not give the
model enough full-character examples. This script composites curated layer
presets into fully dressed/equipped 8-direction animation frames.
"""

from __future__ import annotations

import argparse
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
FILENAME_RE = re.compile(r"^(?P<index>\d+)_(?P<action>[^_]+)_CAM(?P<cam>\d+)_(?P<frame>\d+)\.png$", re.IGNORECASE)
CANONICAL_LITISO_8D = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"]
CAM_TO_LITISO = {7: "S", 6: "SE", 5: "E", 4: "NE", 3: "N", 2: "NW", 1: "W", 0: "SW"}

PRESETS = [
    {
        "id": "forest_guard",
        "description": "human forest guard with vest, light armor, sword and buckler",
        "layers": ["BaseHumanMale", "BrownPants", "BlackBoots", "BlueVest", "BreastPlate1", "DrkGluv", "BroadSword", "BucklerWood"],
    },
    {
        "id": "iron_knight",
        "description": "human iron knight with plate armor, helm, sword and shield",
        "layers": ["BaseHumanMale", "BrownPants", "BlackBoots", "BreastPlateGold", "DrkArmPlat", "DrkGluv", "DrkHelm", "BastardSword", "BucklerNisos"],
    },
    {
        "id": "arcane_mage",
        "description": "human arcane mage with dark hood, robe pieces and wand",
        "layers": ["BaseHumanMale", "BrownPants", "DrkArcaneBoot", "ArcaneMystic", "DrkArcaneGluv", "DrkHood", "DaggerWand"],
    },
    {
        "id": "cloak_rogue",
        "description": "human rogue with cloak, tunic, dark gloves and dagger",
        "layers": ["DrkCloakBACK", "BaseHumanMale", "BrownPants", "BlackBoots", "BrvHrtTunic", "DrkGluv", "Dagger", "DrkCloakFRONT"],
    },
    {
        "id": "skeleton_archer",
        "description": "skeleton archer with bone bow and quiver belt",
        "layers": ["BaseSkeleton", "BoneQuiverBelt", "BoneBow"],
    },
]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def safe_stem(value: str) -> str:
    text = re.sub(r"[^A-Za-z0-9]+", "_", value).strip("_").lower()
    return text or "unnamed"


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


def parse_filename(path: Path) -> dict | None:
    match = FILENAME_RE.match(path.name)
    if not match:
        return None
    cam = int(match.group("cam"))
    if cam not in CAM_TO_LITISO:
        return None
    return {
        "source_action_index": int(match.group("index")),
        "action": match.group("action"),
        "cam": cam,
        "direction": CAM_TO_LITISO[cam],
        "source_frame_number": int(match.group("frame")),
    }


def layer_file_map(part_root: Path, layer_name: str) -> dict[str, Path]:
    folder = part_root / layer_name
    if not folder.exists():
        return {}
    return {path.name: path for path in folder.glob("*.png") if parse_filename(path)}


def sequence_indexes(base_paths: list[Path]) -> dict[str, tuple[int, int]]:
    grouped: dict[tuple[str, int], list[Path]] = defaultdict(list)
    for path in base_paths:
        parsed = parse_filename(path)
        if not parsed:
            continue
        grouped[(parsed["action"].lower(), parsed["cam"])].append(path)
    result: dict[str, tuple[int, int]] = {}
    for paths in grouped.values():
        paths.sort(key=lambda path: parse_filename(path)["source_frame_number"])  # type: ignore[index]
        for index, path in enumerate(paths):
            result[path.name] = (index, len(paths))
    return result


def make_caption(record: dict, preset: dict) -> str:
    action = record["action"].lower()
    direction = direction_phrase(record["direction"])
    return (
        "litiso_oga8d_composite_motion_reference, cc_by_oga_source, "
        f"character preset {preset['id']}, {preset['description']}, "
        f"{action} animation, facing {direction}, direction {record['direction']}, "
        f"frame {record['sequence_frame_index'] + 1} of {record['sequence_frame_count']}, "
        "8-direction RPG character motion, full body, centered, transparent background, "
        "game sprite frame, no scene, no floor, no text"
    )


def image_stats(path: Path) -> dict:
    image = Image.open(path).convert("RGBA")
    width, height = image.size
    alpha = image.getchannel("A")
    values = alpha.get_flattened_data() if hasattr(alpha, "get_flattened_data") else alpha.getdata()
    bbox = alpha.getbbox()
    opaque = sum(1 for value in values if value > 0)
    return {
        "width": width,
        "height": height,
        "alpha_coverage": round(opaque / float(max(1, width * height)), 5),
        "bbox": list(bbox) if bbox else None,
    }


def build_preset(part_root: Path, preset: dict, images_dir: Path, captions_dir: Path, validation_every: int, start_index: int) -> tuple[list[dict], list[str]]:
    layer_maps = {layer: layer_file_map(part_root, layer) for layer in preset["layers"]}
    warnings: list[str] = []
    for layer, mapping in layer_maps.items():
        if not mapping:
            warnings.append(f"{preset['id']}: missing or empty layer {layer}")

    base_layer = next((layer for layer in preset["layers"] if layer.startswith("Base")), preset["layers"][0])
    base_map = layer_maps.get(base_layer, {})
    base_paths = sorted(base_map.values(), key=lambda path: (parse_filename(path)["source_action_index"], parse_filename(path)["cam"], parse_filename(path)["source_frame_number"]))  # type: ignore[index]
    seq = sequence_indexes(base_paths)
    records: list[dict] = []

    for local_index, base_path in enumerate(base_paths, start=1):
        parsed = parse_filename(base_path)
        if not parsed:
            continue
        canvas = Image.new("RGBA", (148, 130), (0, 0, 0, 0))
        used_layers: list[str] = []
        missing_layers: list[str] = []
        for layer in preset["layers"]:
            source = layer_maps.get(layer, {}).get(base_path.name)
            if not source:
                missing_layers.append(layer)
                continue
            canvas.alpha_composite(Image.open(source).convert("RGBA"))
            used_layers.append(layer)

        seq_index, seq_count = seq.get(base_path.name, (0, 1))
        parsed["sequence_frame_index"] = seq_index
        parsed["sequence_frame_count"] = seq_count
        stem = "_".join(
            [
                "oga8dcomp",
                safe_stem(preset["id"]),
                safe_stem(parsed["action"]),
                parsed["direction"].lower(),
                f"{seq_index:03d}",
            ]
        )
        out_image = images_dir / f"{stem}.png"
        out_caption = captions_dir / f"{stem}.txt"
        canvas.save(out_image)
        caption = make_caption(parsed, preset)
        out_caption.write_text(caption + "\n", encoding="utf-8")
        global_index = start_index + len(records)
        split = "validation" if validation_every > 0 and ((global_index + 1) % validation_every == 0) else "train"
        record = {
            "file_name": f"images/{out_image.name}",
            "caption_file": f"captions/{out_caption.name}",
            "text": caption,
            "split": split,
            "category": "oga_8d_composite_motion",
            "license": LICENSE,
            "attribution": ATTRIBUTION,
            "source_page": SOURCE_PAGE,
            "preset_id": preset["id"],
            "preset_description": preset["description"],
            "layers": used_layers,
            "missing_layers": missing_layers,
            "source_frame_name": base_path.name,
            "action": parsed["action"],
            "direction": parsed["direction"],
            "direction_phrase": direction_phrase(parsed["direction"]),
            "cam": f"CAM{parsed['cam']}",
            "source_frame_number": parsed["source_frame_number"],
            "sequence_frame_index": parsed["sequence_frame_index"],
            "sequence_frame_count": parsed["sequence_frame_count"],
            "training_approval": "approved_2026-06-08",
            "image_stats": image_stats(out_image),
        }
        records.append(record)

    return records, warnings


def write_contact_sheet(out_dir: Path, records: list[dict]) -> str | None:
    if not records:
        return None
    by_preset_action_direction: dict[tuple[str, str, str], list[dict]] = defaultdict(list)
    for record in records:
        by_preset_action_direction[(record["preset_id"], record["action"], record["direction"])].append(record)

    presets = sorted({record["preset_id"] for record in records})
    actions = ["Idle", "Walk", "Attack", "Cast", "Run", "Death"]
    cell_w, cell_h = 148, 130
    scale = 2
    label_w = 170
    header_h = 70
    label_h = 26
    row_keys = [(preset, action) for preset in presets for action in actions if any(r["preset_id"] == preset and r["action"] == action for r in records)]
    width = label_w + len(CANONICAL_LITISO_8D) * cell_w * scale
    height = header_h + len(row_keys) * (cell_h * scale + label_h)
    sheet = Image.new("RGBA", (width, height), (16, 18, 24, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((10, 10), "OGA 8D composited motion dataset", fill=(235, 240, 245, 255))
    draw.text((10, 34), "Mid-frame samples, LIT-ISO order: S, SE, E, NE, N, NW, W, SW", fill=(190, 200, 215, 255))
    for col, direction in enumerate(CANONICAL_LITISO_8D):
        x = label_w + col * cell_w * scale
        draw.text((x + 8, 48), direction, fill=(235, 240, 245, 255))

    for row, (preset, action) in enumerate(row_keys):
        y = header_h + row * (cell_h * scale + label_h)
        draw.text((10, y + 8), f"{preset}\n{action}", fill=(235, 240, 245, 255))
        for col, direction in enumerate(CANONICAL_LITISO_8D):
            x = label_w + col * cell_w * scale
            candidates = sorted(
                by_preset_action_direction.get((preset, action, direction), []),
                key=lambda item: item["sequence_frame_index"],
            )
            if not candidates:
                draw.rectangle((x, y, x + cell_w * scale - 1, y + cell_h * scale - 1), outline=(150, 60, 60, 255))
                continue
            record = candidates[len(candidates) // 2]
            image = Image.open(out_dir / record["file_name"]).convert("RGBA")
            preview = image.resize((cell_w * scale, cell_h * scale), Image.Resampling.NEAREST)
            bg = Image.new("RGBA", preview.size, (238, 238, 238, 255))
            bg.alpha_composite(preview)
            sheet.alpha_composite(bg, (x, y))
            draw.rectangle((x, y, x + cell_w * scale - 1, y + cell_h * scale - 1), outline=(66, 74, 90, 255))
    path = out_dir / "composite_contact_sheet.png"
    sheet.save(path)
    return str(path)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", type=Path, default=Path(r"C:\Projects\Pixel Pipeline\sources\opengameart\400-items-basehumanmale-orc-skeleton\extracted\part-1"))
    parser.add_argument("--out-dataset", type=Path, default=Path(r"C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\oga_8d_composite_motion_v1"))
    parser.add_argument("--validation-every", type=int, default=10)
    parser.add_argument("--replace", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if not args.source_root.exists():
        raise FileNotFoundError(f"Missing source root: {args.source_root}")
    if args.out_dataset.exists() and args.replace:
        normalized = str(args.out_dataset).replace("\\", "/").lower()
        if "oga_8d_composite_motion" not in normalized:
            raise RuntimeError(f"Refusing to replace unexpected dataset path: {args.out_dataset}")
        shutil.rmtree(args.out_dataset)

    images_dir = args.out_dataset / "images"
    captions_dir = args.out_dataset / "captions"
    provenance_dir = args.out_dataset / "provenance"
    for directory in (images_dir, captions_dir, provenance_dir):
        directory.mkdir(parents=True, exist_ok=True)

    all_records: list[dict] = []
    warnings: list[str] = []
    for preset in PRESETS:
        records, preset_warnings = build_preset(args.source_root, preset, images_dir, captions_dir, args.validation_every, len(all_records))
        all_records.extend(records)
        warnings.extend(preset_warnings)

    metadata_path = args.out_dataset / "metadata.jsonl"
    train_path = args.out_dataset / "train.txt"
    val_path = args.out_dataset / "val.txt"
    qa_path = args.out_dataset / "qa_report.json"
    with metadata_path.open("w", encoding="utf-8") as handle:
        for record in all_records:
            handle.write(json.dumps(record, separators=(",", ":")) + "\n")

    train = [record["file_name"] for record in all_records if record["split"] == "train"]
    val = [record["file_name"] for record in all_records if record["split"] == "validation"]
    train_path.write_text("\n".join(train) + ("\n" if train else ""), encoding="utf-8")
    val_path.write_text("\n".join(val) + ("\n" if val else ""), encoding="utf-8")

    qa_items = []
    for record in all_records:
        issues = []
        stats = record["image_stats"]
        if stats["width"] != 148 or stats["height"] != 130:
            issues.append("unexpected_dimensions")
        if stats["alpha_coverage"] <= 0.01:
            issues.append("blank_or_nearly_blank")
        if record["missing_layers"]:
            issues.append("missing_layers")
        qa_items.append({"file_name": record["file_name"], "status": "pass" if not issues else "needs_review", "issues": issues, "stats": stats})
    qa_status = "pass" if all(item["status"] == "pass" for item in qa_items) else "needs_review"
    qa_path.write_text(json.dumps({"schemaVersion": 1, "generated_utc": utc_now(), "status": qa_status, "items": qa_items, "warnings": warnings}, indent=2), encoding="utf-8")

    contact = write_contact_sheet(args.out_dataset, all_records)
    notice = (
        "# Attribution Notice\n\n"
        f"This dataset uses composited frames from {SOURCE_PAGE}.\n\n"
        f"License: {LICENSE}\n\n"
        f"Attribution recorded locally: {ATTRIBUTION}\n\n"
        "Owner approved local training use on 2026-06-08. Derived models must record that CC-BY 4.0 source material was included.\n"
    )
    (args.out_dataset / "NOTICE_ATTRIBUTION.md").write_text(notice, encoding="utf-8")

    by_preset = Counter(record["preset_id"] for record in all_records)
    by_action = Counter(record["action"] for record in all_records)
    by_direction = Counter(record["direction"] for record in all_records)
    manifest = {
        "schemaVersion": 1,
        "generated_utc": utc_now(),
        "status": "composite_dataset_ready" if qa_status == "pass" else "composite_dataset_needs_review",
        "source_page": SOURCE_PAGE,
        "source_root": str(args.source_root),
        "license": LICENSE,
        "attribution": ATTRIBUTION,
        "owner_training_approval": "approved_2026-06-08",
        "dataset": str(args.out_dataset),
        "record_count": len(all_records),
        "train_count": len(train),
        "validation_count": len(val),
        "presets": PRESETS,
        "by_preset": dict(sorted(by_preset.items())),
        "by_action": dict(by_action.most_common()),
        "by_direction": {direction: by_direction.get(direction, 0) for direction in CANONICAL_LITISO_8D},
        "metadata_jsonl": str(metadata_path),
        "train_txt": str(train_path),
        "val_txt": str(val_path),
        "qa_report": str(qa_path),
        "qa_status": qa_status,
        "contact_sheet": contact,
        "warnings": warnings,
        "next_step": "Train a second pilot on this composited dataset; compare against the body-only LoRA before keeping either.",
    }
    (args.out_dataset / "dataset_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps({"dataset": str(args.out_dataset), "records": len(all_records), "qa": qa_status, "contact_sheet": contact}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
