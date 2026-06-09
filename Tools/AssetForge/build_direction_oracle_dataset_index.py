#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def rel_path(root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(root).as_posix()
    except ValueError:
        return str(path)


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a combined index for direction-oracle dataset packs.")
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--dataset-root", type=Path, default=Path(r"C:\Projects\Pixel Pipeline\datasets\lit_iso\direction_oracles"))
    parser.add_argument("--output-root", type=Path, default=Path(r"C:\Projects\Pixel Pipeline\datasets\lit_iso\direction_oracles\_index"))
    parser.add_argument("--include", action="append", default=[], help="Specific pack name to include. Default: all packs with metadata.jsonl.")
    parser.add_argument("--exclude", action="append", default=[], help="Pack name to exclude.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    dataset_root = args.dataset_root if args.dataset_root.is_absolute() else project_root / args.dataset_root
    output_root = args.output_root if args.output_root.is_absolute() else project_root / args.output_root
    output_root.mkdir(parents=True, exist_ok=True)

    include = set(args.include)
    exclude = set(args.exclude)
    pack_dirs = []
    for child in sorted(dataset_root.iterdir() if dataset_root.exists() else [], key=lambda item: item.name):
        if not child.is_dir() or child.name.startswith("_"):
            continue
        if include and child.name not in include:
            continue
        if child.name in exclude:
            continue
        if (child / "metadata.jsonl").exists():
            pack_dirs.append(child)

    records: list[dict[str, Any]] = []
    packs = []
    for pack_dir in pack_dirs:
        capture_path = pack_dir / "metadata" / "capture_manifest.json"
        capture = load_json(capture_path) if capture_path.exists() else {}
        pack_records = []
        with (pack_dir / "metadata.jsonl").open("r", encoding="utf-8-sig") as handle:
            for line in handle:
                line = line.strip()
                if not line:
                    continue
                record = json.loads(line)
                source_file_name = record["file_name"]
                image_path = (pack_dir / source_file_name).resolve()
                record["pack_name"] = pack_dir.name
                record["pack_root"] = rel_path(project_root, pack_dir)
                record["source_pack_file_name"] = source_file_name
                record["file_name"] = rel_path(output_root, image_path)
                record["absolute_image_path"] = str(image_path)
                records.append(record)
                pack_records.append(record)
        packs.append(
            {
                "pack_name": pack_dir.name,
                "pack_root": rel_path(project_root, pack_dir),
                "records": len(pack_records),
                "directions": sorted({record.get("direction") for record in pack_records if record.get("direction")}),
                "license": capture.get("license"),
                "author": capture.get("author"),
                "capture_manifest": rel_path(project_root, capture_path) if capture_path.exists() else None,
            }
        )

    metadata_jsonl = output_root / "metadata.jsonl"
    metadata_jsonl.write_text("\n".join(json.dumps(record, separators=(",", ":")) for record in records) + ("\n" if records else ""), encoding="utf-8")

    by_direction: dict[str, int] = {}
    by_pack: dict[str, int] = {}
    for record in records:
        by_direction[record.get("direction", "unknown")] = by_direction.get(record.get("direction", "unknown"), 0) + 1
        by_pack[record["pack_name"]] = by_pack.get(record["pack_name"], 0) + 1

    manifest = {
        "schema": "lit_iso.asset_forge.direction_oracle_dataset_index.v1",
        "generated_utc": utc_now(),
        "dataset_root": rel_path(project_root, dataset_root),
        "output_root": rel_path(project_root, output_root),
        "metadata_jsonl": rel_path(project_root, metadata_jsonl),
        "pack_count": len(packs),
        "record_count": len(records),
        "by_direction": by_direction,
        "by_pack": by_pack,
        "packs": packs,
        "training_readiness": {
            "ready_for_lora": len(records) >= 50,
            "current_gap": max(0, 50 - len(records)),
            "note": "Use as evaluation/anchor data until at least 50 approved direction/style records exist.",
        },
    }
    (output_root / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps({"ok": True, "packs": len(packs), "records": len(records), "output": rel_path(project_root, output_root)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
