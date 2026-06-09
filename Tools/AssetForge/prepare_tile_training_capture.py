#!/usr/bin/env python3
"""
Prepare a dry-run-first training capture manifest for approved derived tiles.

By default this script writes only a repo-local report. Use --apply to create or
update files in the external dataset root.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import sys
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


DEFAULT_MANIFEST = Path("Assets/Generated/_Review/greenwake_geometry_derived_v7/derived_geometry_manifest.json")
DEFAULT_DATASET_ROOT = Path(r"C:\Projects\Pixel Pipeline\datasets\lit_iso\tiles\greenwake_geometry_v1")
DEFAULT_REPORT = Path("Temp/AssetForge/greenwake_geometry_v1_training_capture_manifest.json")
APPROVED_QA_STATUSES = {"pass"}


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def read_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, payload: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def rel_path(root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path)


def repo_path(project_root: Path, path: Path) -> str:
    return rel_path(project_root, path)


def safe_stem(value: str) -> str:
    return "".join(char if char.isalnum() or char in "._-" else "_" for char in value).strip("_")


def resolve_input(project_root: Path, value: str | Path) -> Path:
    path = Path(value)
    return path if path.is_absolute() else project_root / path


def load_strict_report(manifest_path: Path, project_root: Path) -> dict[str, Any] | None:
    candidate = manifest_path.parent / "strict_asset_quality_report.json"
    if candidate.exists():
        return read_json(candidate)
    fallback = project_root / "Assets/Generated/_Review/greenwake_geometry_derived_v7/strict_asset_quality_report.json"
    if fallback.exists():
        return read_json(fallback)
    return None


def strict_items_by_name(strict_report: dict[str, Any] | None) -> dict[str, dict[str, Any]]:
    if not strict_report:
        return {}
    items: dict[str, dict[str, Any]] = {}
    for item in strict_report.get("items") or []:
        path_value = item.get("path")
        if not path_value:
            continue
        items[Path(str(path_value)).name] = item
    return items


def caption_for(item: dict[str, Any]) -> str:
    biome = str(item.get("biome") or "Greenwake")
    material = str(item.get("material") or "unknown material").replace("_", " ")
    shape = str(item.get("shape") or "tile").replace("_", " ")
    category = str(item.get("category") or "terrain").replace("_", " ")
    if "transition" in str(item.get("shape") or ""):
        material_text = material.replace(" to ", " to ")
        shape_text = "transition terrain tile"
    elif "edge" in str(item.get("shape") or ""):
        material_text = material
        shape_text = "raised edge terrain tile"
    else:
        material_text = material
        shape_text = "flat terrain tile"
    return (
        f"LIT-ISO original clean-room {biome} {category}, {material_text}, {shape}, "
        f"{shape_text}, 2:1 isometric pixel art, transparent background, "
        "strict terrain geometry, no props, no characters, no baked object decoration"
    )


def provenance_for(
    project_root: Path,
    manifest_path: Path,
    manifest: dict[str, Any],
    item: dict[str, Any],
    source_path: Path,
    qa_item: dict[str, Any] | None,
) -> dict[str, Any]:
    return {
        "schema": "lit_iso.asset_forge.tile_training_provenance.v1",
        "source_image": repo_path(project_root, source_path),
        "source_sha256": sha256(source_path),
        "source_manifest": repo_path(project_root, manifest_path),
        "source_manifest_schema": manifest.get("schema"),
        "source_status": item.get("status"),
        "source_master": item.get("source_master"),
        "category": item.get("category"),
        "biome": item.get("biome"),
        "material": item.get("material"),
        "shape": item.get("shape"),
        "dimensions": {"width": item.get("width"), "height": item.get("height")},
        "unity": item.get("unity"),
        "strict_qa": {
            "status": qa_item.get("status") if qa_item else "missing",
            "issues": qa_item.get("issues") if qa_item else ["missing_strict_qa_item"],
            "warnings": qa_item.get("warnings") if qa_item else [],
            "terrain_profile": qa_item.get("terrain_profile") if qa_item else None,
        },
        "licensing": {
            "source": "project-authored deterministic geometry derived from LIT-ISO review outputs",
            "training_use": "local_lit_iso_dataset_only",
            "clean_room_note": "Do not include third-party reference pack pixels or AI-training-prohibited sources.",
        },
    }


def build_capture(project_root: Path, manifest_path: Path, dataset_root: Path) -> dict[str, Any]:
    manifest = read_json(manifest_path)
    strict_report = load_strict_report(manifest_path, project_root)
    strict_by_name = strict_items_by_name(strict_report)
    records: list[dict[str, Any]] = []
    skipped: list[dict[str, Any]] = []

    for index, item in enumerate(manifest.get("items") or []):
        source_path = resolve_input(project_root, item.get("path", ""))
        if not source_path.exists():
            skipped.append({"name": item.get("name"), "reason": "missing_source_image", "path": str(source_path)})
            continue
        qa_item = strict_by_name.get(source_path.name)
        qa_status = qa_item.get("status") if qa_item else "missing"
        if qa_status not in APPROVED_QA_STATUSES:
            skipped.append({"name": source_path.name, "reason": "strict_qa_not_passed", "qa_status": qa_status})
            continue

        stem = safe_stem(Path(str(item.get("name") or source_path.name)).stem)
        image_dest = dataset_root / "images" / f"{stem}.png"
        caption_dest = dataset_root / "captions" / f"{stem}.txt"
        provenance_dest = dataset_root / "provenance" / f"{stem}.json"
        caption = caption_for(item)
        provenance = provenance_for(project_root, manifest_path, manifest, item, source_path, qa_item)
        records.append(
            {
                "id": stem,
                "index": index,
                "file_name": f"images/{image_dest.name}",
                "caption_file": f"captions/{caption_dest.name}",
                "provenance_file": f"provenance/{provenance_dest.name}",
                "text": caption,
                "asset_mode": "tile",
                "category": item.get("category"),
                "biome": item.get("biome"),
                "material": item.get("material"),
                "shape": item.get("shape"),
                "width": item.get("width"),
                "height": item.get("height"),
                "source_image": repo_path(project_root, source_path),
                "source_sha256": provenance["source_sha256"],
                "strict_qa_status": qa_status,
                "strict_qa_warnings": qa_item.get("warnings") or [],
                "copy": {
                    "source": str(source_path),
                    "image": str(image_dest),
                    "caption": str(caption_dest),
                    "provenance": str(provenance_dest),
                },
                "provenance": provenance,
            }
        )

    counts = {
        "by_material": dict(Counter(str(record.get("material")) for record in records)),
        "by_shape": dict(Counter(str(record.get("shape")) for record in records)),
        "by_category": dict(Counter(str(record.get("category")) for record in records)),
    }
    return {
        "schema": "lit_iso.asset_forge.tile_training_capture_plan.v1",
        "generated_utc": utc_now(),
        "project_root": str(project_root),
        "source_manifest": repo_path(project_root, manifest_path),
        "source_manifest_schema": manifest.get("schema"),
        "source_total": manifest.get("total"),
        "strict_qa": {
            "report": repo_path(project_root, manifest_path.parent / "strict_asset_quality_report.json"),
            "dataset_ready": strict_report.get("dataset_ready") if strict_report else False,
            "pass_count": strict_report.get("pass_count") if strict_report else 0,
            "review_count": strict_report.get("review_count") if strict_report else None,
            "warning_count": strict_report.get("warning_count") if strict_report else None,
        },
        "dataset_root": str(dataset_root),
        "dry_run_default": True,
        "record_count": len(records),
        "skipped_count": len(skipped),
        "counts": counts,
        "planned_outputs": {
            "metadata_jsonl": str(dataset_root / "metadata.jsonl"),
            "dataset_manifest": str(dataset_root / "dataset_manifest.json"),
            "images_dir": str(dataset_root / "images"),
            "captions_dir": str(dataset_root / "captions"),
            "provenance_dir": str(dataset_root / "provenance"),
        },
        "records": records,
        "skipped": skipped,
        "notes": [
            "Dry-run report only unless --apply is passed.",
            "Only strict QA pass records are eligible.",
            "Captions keep terrain-only constraints explicit for tile LoRA training.",
        ],
    }


def apply_capture(capture: dict[str, Any], dataset_root: Path) -> None:
    for folder in ("images", "captions", "provenance"):
        (dataset_root / folder).mkdir(parents=True, exist_ok=True)

    metadata_rows = []
    for record in capture["records"]:
        copy = record["copy"]
        shutil.copy2(copy["source"], copy["image"])
        Path(copy["caption"]).write_text(record["text"] + "\n", encoding="utf-8")
        write_json(Path(copy["provenance"]), record["provenance"])
        metadata_rows.append(
            {
                key: value
                for key, value in record.items()
                if key not in {"copy", "provenance"}
            }
        )

    metadata_path = dataset_root / "metadata.jsonl"
    metadata_path.write_text(
        "\n".join(json.dumps(row, separators=(",", ":")) for row in metadata_rows) + ("\n" if metadata_rows else ""),
        encoding="utf-8",
    )
    manifest_payload = {
        key: value
        for key, value in capture.items()
        if key not in {"records"}
    }
    manifest_payload["schema"] = "lit_iso.asset_forge.tile_training_dataset.v1"
    manifest_payload["applied_utc"] = utc_now()
    manifest_payload["records"] = metadata_rows
    write_json(dataset_root / "dataset_manifest.json", manifest_payload)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Prepare approved Greenwake derived tiles for a local training dataset.")
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--dataset-root", type=Path, default=DEFAULT_DATASET_ROOT)
    parser.add_argument("--output-report", type=Path, default=DEFAULT_REPORT)
    parser.add_argument("--apply", action="store_true", help="Actually write/copy files into --dataset-root.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    manifest_path = resolve_input(project_root, args.manifest).resolve()
    output_report = resolve_input(project_root, args.output_report).resolve()
    dataset_root = args.dataset_root.resolve()

    if not manifest_path.exists():
        raise FileNotFoundError(f"Missing derived tile manifest: {manifest_path}")

    capture = build_capture(project_root, manifest_path, dataset_root)
    capture["mode"] = "apply" if args.apply else "dry_run"
    capture["output_report"] = repo_path(project_root, output_report)

    if args.apply:
        apply_capture(capture, dataset_root)
        capture["applied"] = True
    else:
        capture["applied"] = False

    write_json(output_report, capture)
    print(
        json.dumps(
            {
                "mode": capture["mode"],
                "report": str(output_report),
                "dataset_root": str(dataset_root),
                "records": capture["record_count"],
                "skipped": capture["skipped_count"],
                "applied": capture["applied"],
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
