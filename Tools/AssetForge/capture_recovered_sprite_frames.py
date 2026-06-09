#!/usr/bin/env python3
"""
Capture approved recovered sprite frames into a LoRA-ready dataset.

Input is the output of recover_sprite_frames_from_sheet.py:
  manifest.json
  frames/*.png
  captions/*.txt
  raw_crops/*.png

By default every recovered frame is treated as approved. A decision JSON may be
supplied later to approve/reject individual normalized frame filenames.
"""

from __future__ import annotations

import argparse
import json
import shutil
import sys
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageDraw

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def safe_name(path: str | Path) -> str:
    return Path(path).name.replace(" ", "_")


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def load_decisions(path: Path | None) -> dict[str, str]:
    if not path or not path.exists():
        return {}
    payload = load_json(path)
    decisions: dict[str, str] = {}
    for item in payload.get("decisions", []):
        frame = item.get("frame") or item.get("normalized_frame") or item.get("id")
        decision = item.get("decision", "pending")
        if frame:
            decisions[Path(frame).name] = str(decision)
    return decisions


def image_stats(path: Path) -> dict:
    image = Image.open(path).convert("RGBA")
    w, h = image.size
    alpha = image.getchannel("A")
    bbox = alpha.getbbox()
    alpha_data = alpha.get_flattened_data() if hasattr(alpha, "get_flattened_data") else alpha.getdata()
    opaque = sum(1 for value in alpha_data if value > 0)
    coverage = opaque / float(w * h) if w * h else 0
    return {
        "width": w,
        "height": h,
        "alpha_coverage": round(coverage, 4),
        "bbox": list(bbox) if bbox else None,
        "has_alpha": image.mode == "RGBA",
    }


def write_contact_sheet(out_dir: Path, records: list[dict], columns: int = 8) -> str | None:
    if not records:
        return None
    cell = 96
    label_h = 30
    rows = (len(records) + columns - 1) // columns
    sheet = Image.new("RGBA", (columns * cell, rows * (cell + label_h)), (16, 18, 24, 255))
    draw = ImageDraw.Draw(sheet)
    for index, record in enumerate(records):
        image = Image.open(record["absolute_image"]).convert("RGBA")
        preview = image.resize((cell, cell), Image.Resampling.NEAREST)
        x = (index % columns) * cell
        y = (index // columns) * (cell + label_h)
        background = Image.new("RGBA", (cell, cell), (238, 238, 238, 255))
        background.alpha_composite(preview)
        sheet.alpha_composite(background, (x, y))
        draw.text((x + 3, y + cell + 4), Path(record["file_name"]).stem[-18:], fill=(230, 235, 245, 255))
    path = out_dir / "approved_recovered_frames_contact_sheet.png"
    sheet.save(path)
    return str(path)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Capture recovered sprite frames into a dataset.")
    parser.add_argument("--recovered-pack", type=Path, required=True)
    parser.add_argument("--out-dataset", type=Path, required=True)
    parser.add_argument("--decisions", type=Path)
    parser.add_argument("--validation-every", type=int, default=8)
    parser.add_argument("--include-rejected", action="store_true")
    parser.add_argument("--replace", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    pack = args.recovered_pack
    manifest_path = pack / "manifest.json"
    if not manifest_path.exists():
        raise FileNotFoundError(f"Missing recovered pack manifest: {manifest_path}")

    manifest = load_json(manifest_path)
    decisions = load_decisions(args.decisions)

    if args.out_dataset.exists() and args.replace:
        # Guard against accidental broad deletion.
        if "recovered_motion_candidates" not in str(args.out_dataset).replace("\\", "/"):
            raise RuntimeError(f"Refusing to replace unexpected dataset path: {args.out_dataset}")
        shutil.rmtree(args.out_dataset)

    images_dir = args.out_dataset / "images"
    captions_dir = args.out_dataset / "captions"
    raw_dir = args.out_dataset / "raw_crops"
    provenance_dir = args.out_dataset / "provenance"
    for directory in (images_dir, captions_dir, raw_dir, provenance_dir):
        directory.mkdir(parents=True, exist_ok=True)

    records = []
    rejected = []
    warnings = []

    for index, record in enumerate(manifest.get("records", [])):
        frame_path = Path(record["normalized_frame"])
        caption_path = Path(record["caption_file"]) if record.get("caption_file") else None
        raw_path = Path(record["raw_crop"]) if record.get("raw_crop") else None
        frame_name = frame_path.name
        decision = decisions.get(frame_name, "approved")
        if decision != "approved" and not args.include_rejected:
            rejected.append({"frame": frame_name, "decision": decision})
            continue

        if not frame_path.exists():
            warnings.append({"frame": frame_name, "warning": "missing normalized frame"})
            continue
        if not caption_path or not caption_path.exists():
            warnings.append({"frame": frame_name, "warning": "missing caption sidecar"})
            continue

        destination_name = safe_name(frame_name)
        image_dest = images_dir / destination_name
        caption_dest = captions_dir / Path(destination_name).with_suffix(".txt").name
        shutil.copy2(frame_path, image_dest)
        shutil.copy2(caption_path, caption_dest)

        raw_rel = None
        if raw_path and raw_path.exists():
            raw_dest = raw_dir / safe_name(raw_path)
            shutil.copy2(raw_path, raw_dest)
            raw_rel = f"raw_crops/{raw_dest.name}"

        split = "validation" if args.validation_every > 0 and ((len(records) + 1) % args.validation_every == 0) else "train"
        stats = image_stats(image_dest)
        payload = {
            "file_name": f"images/{image_dest.name}",
            "caption_file": f"captions/{caption_dest.name}",
            "text": caption_dest.read_text(encoding="utf-8").strip(),
            "split": split,
            "decision": decision,
            "category": "recovered_motion_candidate",
            "source_generated_sheet": record.get("source"),
            "source_generated_sheet_name": record.get("source_name"),
            "source_bbox": record.get("bbox"),
            "raw_crop": raw_rel,
            "source_caption": record.get("caption"),
            "image_stats": stats,
            "provenance": {
                "recovered_pack": str(pack),
                "recovered_manifest": str(manifest_path),
                "source_manifest": manifest.get("source_manifest"),
                "source_checkpoint": None,
                "source_lora": None,
            },
            "absolute_image": str(image_dest),
        }
        records.append(payload)

    source_manifest_path = manifest.get("source_manifest")
    source_manifest = {}
    if source_manifest_path and Path(source_manifest_path).exists():
        source_manifest = load_json(Path(source_manifest_path))
        shutil.copy2(source_manifest_path, provenance_dir / "source_generation_manifest.json")
        for rec in records:
            rec["provenance"]["source_checkpoint"] = source_manifest.get("checkpoint")
            rec["provenance"]["source_lora"] = source_manifest.get("lora")

    shutil.copy2(manifest_path, provenance_dir / "recovered_pack_manifest.json")
    if args.decisions and args.decisions.exists():
        shutil.copy2(args.decisions, provenance_dir / "capture_decisions.json")

    metadata_path = args.out_dataset / "metadata.jsonl"
    train_path = args.out_dataset / "train.txt"
    val_path = args.out_dataset / "val.txt"
    qa_path = args.out_dataset / "qa_report.json"
    dataset_manifest_path = args.out_dataset / "dataset_manifest.json"

    with metadata_path.open("w", encoding="utf-8") as handle:
        for rec in records:
            serializable = dict(rec)
            serializable.pop("absolute_image", None)
            handle.write(json.dumps(serializable, separators=(",", ":")) + "\n")

    train = [rec["file_name"] for rec in records if rec["split"] == "train"]
    val = [rec["file_name"] for rec in records if rec["split"] == "validation"]
    train_path.write_text("\n".join(train) + ("\n" if train else ""), encoding="utf-8")
    val_path.write_text("\n".join(val) + ("\n" if val else ""), encoding="utf-8")

    qa_items = []
    for rec in records:
        issues = []
        stats = rec["image_stats"]
        if stats["width"] != 64 or stats["height"] != 64:
            issues.append("not_64x64")
        if stats["alpha_coverage"] <= 0.01:
            issues.append("blank_or_nearly_blank")
        if stats["alpha_coverage"] >= 0.9:
            issues.append("background_not_removed")
        qa_items.append({"file_name": rec["file_name"], "status": "pass" if not issues else "needs_review", "issues": issues, "stats": stats})

    qa = {
        "schemaVersion": 1,
        "generated_utc": utc_now(),
        "status": "pass" if all(item["status"] == "pass" for item in qa_items) else "needs_review",
        "items": qa_items,
        "warnings": warnings,
    }
    qa_path.write_text(json.dumps(qa, indent=2), encoding="utf-8")

    contact = write_contact_sheet(args.out_dataset, records)
    dataset_manifest = {
        "schemaVersion": 1,
        "generated_utc": utc_now(),
        "status": "dataset_ready" if records else "empty",
        "dataset": str(args.out_dataset),
        "source_recovered_pack": str(pack),
        "source_generation_manifest": source_manifest_path,
        "source_checkpoint": source_manifest.get("checkpoint"),
        "source_lora": source_manifest.get("lora"),
        "record_count": len(records),
        "train_count": len(train),
        "validation_count": len(val),
        "rejected_or_skipped": rejected,
        "warnings": warnings,
        "metadata_jsonl": str(metadata_path),
        "train_txt": str(train_path),
        "val_txt": str(val_path),
        "qa_report": str(qa_path),
        "contact_sheet": contact,
        "next_step": "Human-review these candidates before mixing them into the final LIT-ISO style LoRA dataset.",
    }
    dataset_manifest_path.write_text(json.dumps(dataset_manifest, indent=2), encoding="utf-8")

    print(json.dumps({"dataset": str(args.out_dataset), "records": len(records), "qa": qa["status"], "manifest": str(dataset_manifest_path)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
