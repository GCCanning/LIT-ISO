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


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def repo_path(project_root: Path, value: str | None) -> Path | None:
    if not value:
        return None
    path = Path(value.replace("/", "\\"))
    return path if path.is_absolute() else project_root / path


def rel_path(project_root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(project_root).as_posix()
    except ValueError:
        return str(path)


def safe_name(value: str) -> str:
    return "".join(char if char.isalnum() or char in "._-" else "_" for char in value).strip("_")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Capture an approved 4D/8D oracle sheet as LoRA/eval dataset records.")
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--oracle-manifest", type=Path, required=True)
    parser.add_argument("--dataset-root", type=Path, default=Path(r"C:\Projects\Pixel Pipeline\datasets\lit_iso"))
    parser.add_argument("--pack-name", default="reference_knight_idle_4d_oracle")
    parser.add_argument("--character-description", default="armored knight with cyan energy trim, amber runes, dark hood, glowing sword")
    parser.add_argument("--action", default="idle pose")
    parser.add_argument("--asset-mode", default="character")
    parser.add_argument("--license", default="project_internal_or_explicitly_licensed")
    parser.add_argument("--author", default="LIT-ISO")
    parser.add_argument("--qa-report", type=Path, default=Path(""))
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    manifest_path = args.oracle_manifest if args.oracle_manifest.is_absolute() else project_root / args.oracle_manifest
    if not manifest_path.exists():
        raise FileNotFoundError(f"Missing oracle manifest: {manifest_path}")

    dataset_root = args.dataset_root if args.dataset_root.is_absolute() else project_root / args.dataset_root
    pack_name = safe_name(args.pack_name)
    pack_root = dataset_root / "direction_oracles" / pack_name
    image_root = pack_root / "reference"
    sheet_root = pack_root / "sheet"
    caption_root = pack_root / "captions"
    metadata_root = pack_root / "metadata"
    for folder in (image_root, sheet_root, caption_root, metadata_root):
        folder.mkdir(parents=True, exist_ok=True)

    oracle_manifest = load_json(manifest_path)
    frames = oracle_manifest.get("frames") or []
    records: list[dict[str, Any]] = []

    for frame in frames:
        direction = str(frame.get("direction", "")).strip()
        source = repo_path(project_root, frame.get("source_image"))
        if not direction or source is None or not source.exists():
            raise FileNotFoundError(f"Bad oracle frame source for direction {direction}: {source}")

        direction_text = DIRECTION_NAME.get(direction, direction.lower())
        dest_name = f"{pack_name}_{direction.lower()}{source.suffix.lower()}"
        image_dest = image_root / dest_name
        shutil.copy2(source, image_dest)

        caption = (
            f"LIT-ISO pixel sprite, {args.character_description}, {args.action}, "
            f"{direction_text}, frame 1 of 1, isometric view, pixel art, transparent background, "
            "consistent 4-direction camera, bottom-center anchor"
        )
        caption_dest = caption_root / f"{Path(dest_name).stem}.txt"
        caption_dest.write_text(caption, encoding="utf-8")

        records.append(
            {
                "file_name": rel_path(pack_root, image_dest),
                "text": caption,
                "asset_mode": args.asset_mode,
                "character_description": args.character_description,
                "action": args.action,
                "direction": direction,
                "direction_name": direction_text,
                "frame_index": frame.get("index"),
                "frame_count": 1,
                "source_image": rel_path(project_root, source),
                "oracle_manifest": rel_path(project_root, manifest_path),
                "rect": frame.get("rect"),
                "pivot": frame.get("pivot", {"x": 0.5, "y": 0.0}),
                "camera": "consistent_2.5d_isometric_sprite",
                "anchor": "bottom_center",
                "license": args.license,
                "author": args.author,
                "sha256": sha256(image_dest),
                "usage": "approved_direction_oracle_training_and_evaluation",
            }
        )

    sheet_source = repo_path(project_root, oracle_manifest.get("sheet"))
    contact_source = repo_path(project_root, oracle_manifest.get("contact_sheet"))
    copied_sheet = None
    copied_contact = None
    if sheet_source and sheet_source.exists():
        copied_sheet = sheet_root / f"{pack_name}_sheet{sheet_source.suffix.lower()}"
        shutil.copy2(sheet_source, copied_sheet)
    if contact_source and contact_source.exists():
        copied_contact = sheet_root / f"{pack_name}_contact{contact_source.suffix.lower()}"
        shutil.copy2(contact_source, copied_contact)

    metadata_jsonl = pack_root / "metadata.jsonl"
    metadata_jsonl.write_text("\n".join(json.dumps(record, separators=(",", ":")) for record in records) + "\n", encoding="utf-8")

    qa_report = None
    if str(args.qa_report).strip():
        qa_source = args.qa_report if args.qa_report.is_absolute() else project_root / args.qa_report
        if qa_source.exists():
            qa_report = metadata_root / qa_source.name
            shutil.copy2(qa_source, qa_report)

    capture_manifest = {
        "schema": "lit_iso.asset_forge.direction_oracle_dataset.v1",
        "generated_utc": utc_now(),
        "pack_name": pack_name,
        "oracle_manifest": rel_path(project_root, manifest_path),
        "dataset_root": rel_path(project_root, pack_root),
        "record_count": len(records),
        "directions": [record["direction"] for record in records],
        "sheet": rel_path(pack_root, copied_sheet) if copied_sheet else None,
        "contact_sheet": rel_path(pack_root, copied_contact) if copied_contact else None,
        "metadata_jsonl": rel_path(pack_root, metadata_jsonl),
        "qa_report": rel_path(pack_root, qa_report) if qa_report else None,
        "license": args.license,
        "author": args.author,
        "notes": [
            "Use these records as approved direction/camera anchors.",
            "This pack is too small for standalone LoRA training; combine with many approved matching sheets.",
            "Generated sweep failures should remain evaluation evidence unless explicitly approved.",
        ],
    }
    (metadata_root / "capture_manifest.json").write_text(json.dumps(capture_manifest, indent=2), encoding="utf-8")
    print(json.dumps({"ok": True, "pack_root": rel_path(project_root, pack_root), "records": len(records)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
