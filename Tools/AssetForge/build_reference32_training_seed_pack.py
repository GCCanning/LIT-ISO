#!/usr/bin/env python3
"""Stage Reference32 clean terrain sources and variants for future LoRA intake.

The output is a review/training-prep pack, not a live dataset outside the Unity
workspace. It groups first-pass tile sources separately from deterministic
palette variants and writes caption sidecars using the catalog metadata.
"""

from __future__ import annotations

import argparse
import json
import shutil
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw


CATALOG_MANIFEST = Path("Assets/Generated/_Review/reference32_source_catalog_v1/reference32_source_catalog_manifest.json")
VARIANT_MANIFEST = Path("Assets/Generated/_Review/reference32_style_locked_variants_v1/reference32_style_locked_variants_manifest.json")
OUTPUT_ROOT = Path("Assets/Generated/_Review/reference32_training_seed_pack_v1")


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def rel(root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path)


def safe_caption(value: str) -> str:
    return " ".join(value.replace("\n", " ").split()).strip()


def copy_with_caption(
    project_root: Path,
    source_path: Path,
    output_path: Path,
    caption: str,
) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source_path, output_path)
    output_path.with_suffix(".txt").write_text(safe_caption(caption) + "\n", encoding="utf-8")


def draw_checker(size: tuple[int, int]) -> Image.Image:
    image = Image.new("RGBA", size, (22, 25, 28, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], 8):
        for x in range(0, size[0], 8):
            if (x // 8 + y // 8) % 2 == 0:
                draw.rectangle([x, y, x + 7, y + 7], fill=(32, 36, 40, 255))
    return image


def draw_contact_sheet(project_root: Path, records: list[dict[str, Any]], output_path: Path) -> None:
    columns = 6
    cell_w = 170
    cell_h = 124
    header_h = 58
    rows = max(1, (len(records) + columns - 1) // columns)
    sheet = Image.new("RGBA", (columns * cell_w, header_h + rows * cell_h), (15, 18, 20, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((10, 10), "Reference32 tile LoRA seed pack v1", fill=(238, 242, 238, 255))
    draw.text((10, 30), "Clean source-lock terrain + deterministic variants. Review/training-prep only.", fill=(158, 170, 164, 255))
    for index, record in enumerate(records):
        x = (index % columns) * cell_w + 12
        y = header_h + (index // columns) * cell_h + 8
        image = Image.open(project_root / record["path"].replace("/", "\\")).convert("RGBA")
        preview = image.resize((96, 96), Image.Resampling.NEAREST)
        checker = draw_checker(preview.size)
        checker.alpha_composite(preview)
        sheet.alpha_composite(checker, (x, y))
        draw.text((x, y + 98), record["id"][:27], fill=(222, 228, 222, 255))
        draw.text((x, y + 112), record["split"][:28], fill=(148, 160, 154, 255))
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)


def write_training_dataset(project_root: Path, output_root: Path, records: list[dict[str, Any]]) -> dict[str, Any]:
    dataset_root = output_root / "training_dataset"
    images_root = dataset_root / "images"
    captions_root = dataset_root / "captions"
    images_root.mkdir(parents=True, exist_ok=True)
    captions_root.mkdir(parents=True, exist_ok=True)

    metadata_lines: list[str] = []
    train_lines: list[str] = []
    for record in records:
        source = project_root / record["path"].replace("/", "\\")
        image_name = f"{record['id']}.png"
        caption_name = f"{record['id']}.txt"
        image_dest = images_root / image_name
        caption_dest = captions_root / caption_name
        shutil.copy2(source, image_dest)
        caption_dest.write_text(safe_caption(record["caption"]) + "\n", encoding="utf-8")
        metadata_lines.append(
            json.dumps(
                {
                    "file_name": f"images/{image_name}",
                    "caption_file": f"captions/{caption_name}",
                    "text": safe_caption(record["caption"]),
                    "category": "tile",
                    "asset_mode": "terrain_tile",
                    "source_record_id": record["id"],
                    "source_path": record["source_path"],
                    "role": record["role"],
                    "split": record["split"],
                    "train_priority": record["train_priority"],
                },
                ensure_ascii=True,
            )
        )
        train_lines.append(f"images/{image_name}")

    metadata_path = dataset_root / "metadata.jsonl"
    train_path = dataset_root / "train.txt"
    readiness_path = dataset_root / "dataset_readiness_summary.json"
    metadata_path.write_text("\n".join(metadata_lines) + "\n", encoding="utf-8")
    train_path.write_text("\n".join(train_lines) + "\n", encoding="utf-8")
    readiness = {
        "schema": "lit_iso.asset_forge.reference32_training_dataset_readiness.v1",
        "status": "ready_for_local_review_training",
        "record_count": len(records),
        "image_count": len(list(images_root.glob("*.png"))),
        "caption_count": len(list(captions_root.glob("*.txt"))),
        "category": "tile",
        "warning": "Review-only dataset staging. Confirm art rights before any distributable model use.",
    }
    write_json(readiness_path, readiness)
    return {
        "dataset_root": rel(project_root, dataset_root),
        "images": rel(project_root, images_root),
        "captions": rel(project_root, captions_root),
        "metadata_jsonl": rel(project_root, metadata_path),
        "train_txt": rel(project_root, train_path),
        "readiness_summary": rel(project_root, readiness_path),
        "record_count": len(records),
    }


def flatten_variant_records(variant_manifest: dict[str, Any]) -> dict[str, list[dict[str, Any]]]:
    variants_by_tile: dict[str, list[dict[str, Any]]] = {}
    for row in variant_manifest.get("rows", []):
        variants_by_tile[row["id"]] = row.get("variants", [])
        source_tile = str(row.get("source_tile", ""))
        if source_tile.lower().endswith(".png"):
            variants_by_tile[source_tile[:-4]] = row.get("variants", [])
    return variants_by_tile


def main() -> int:
    parser = argparse.ArgumentParser(description="Build Reference32 tile training seed pack.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--catalog-manifest", default=str(CATALOG_MANIFEST))
    parser.add_argument("--variant-manifest", default=str(VARIANT_MANIFEST))
    parser.add_argument("--output-root", default=str(OUTPUT_ROOT))
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    catalog = read_json(project_root / args.catalog_manifest)
    variant_manifest_path = project_root / args.variant_manifest
    variant_manifest = read_json(variant_manifest_path) if variant_manifest_path.exists() else {"rows": []}
    variants_by_tile = flatten_variant_records(variant_manifest)
    output_root = project_root / args.output_root

    records: list[dict[str, Any]] = []
    for item in catalog["items"]:
        recommendation = item["recommendation"]
        if not recommendation.get("include_in_first_tile_lora"):
            continue
        source_path = project_root / item["path"].replace("/", "\\")
        out_name = f"{item['id']}__source.png"
        out_path = output_root / "tile_lora_core" / "source" / out_name
        caption = item["caption"]
        copy_with_caption(project_root, source_path, out_path, caption)
        records.append(
            {
                "id": f"{item['id']}__source",
                "tile_id": item["id"],
                "split": "tile_lora_core_source",
                "path": rel(project_root, out_path),
                "caption_path": rel(project_root, out_path.with_suffix(".txt")),
                "caption": caption,
                "source_path": item["path"],
                "role": item["role"],
                "train_priority": recommendation["train_priority"],
            }
        )

        for variant in variants_by_tile.get(item["id"], []):
            variant_id = variant["variant_id"]
            if variant_id == "source":
                continue
            variant_path = project_root / variant["path"].replace("/", "\\")
            variant_out_name = f"{item['id']}__{variant_id}.png"
            variant_out = output_root / "tile_lora_core" / "deterministic_variants" / variant_out_name
            variant_caption = f"{caption}, {variant['label']} palette variant"
            copy_with_caption(project_root, variant_path, variant_out, variant_caption)
            records.append(
                {
                    "id": f"{item['id']}__{variant_id}",
                    "tile_id": item["id"],
                    "split": "tile_lora_core_deterministic_variant",
                    "path": rel(project_root, variant_out),
                    "caption_path": rel(project_root, variant_out.with_suffix(".txt")),
                    "caption": variant_caption,
                    "source_path": variant["path"],
                    "role": item["role"],
                    "variant_id": variant_id,
                    "train_priority": max(1, recommendation["train_priority"] - 10),
                }
            )

    contact_sheet = output_root / "reference32_training_seed_pack_contact_sheet.png"
    draw_contact_sheet(project_root, records, contact_sheet)
    training_dataset = write_training_dataset(project_root, output_root, records)
    manifest = {
        "schema": "lit_iso.asset_forge.reference32_training_seed_pack.v1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "status": "review_training_prep_only_not_live_dataset",
        "catalog_manifest": args.catalog_manifest,
        "variant_manifest": args.variant_manifest,
        "output_root": rel(project_root, output_root),
        "contact_sheet": rel(project_root, contact_sheet),
        "record_count": len(records),
        "source_record_count": len([record for record in records if record["split"] == "tile_lora_core_source"]),
        "variant_record_count": len([record for record in records if record["split"] == "tile_lora_core_deterministic_variant"]),
        "training_dataset": training_dataset,
        "records": records,
        "recommended_next_training": {
            "target": "litiso_reference32_clean_tile_geometry_v1",
            "base": "SD1.5 pixel-art checkpoint",
            "caption_style": "sidecar_txt",
            "warning": "Do not mix prop/deco records into this tile LoRA. Use prop_lora_core separately.",
        },
    }
    manifest_path = output_root / "reference32_training_seed_pack_manifest.json"
    write_json(manifest_path, manifest)
    print(
        json.dumps(
            {
                "manifest": rel(project_root, manifest_path),
                "contact_sheet": rel(project_root, contact_sheet),
                "training_dataset": training_dataset["dataset_root"],
                "records": len(records),
                "sources": manifest["source_record_count"],
                "variants": manifest["variant_record_count"],
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
