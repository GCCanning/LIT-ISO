#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


DEFAULT_SELECTED_MANIFEST = (
    "Assets/Generated/_Review/reference32_mask_locked_texture_family_screenshot_balanced_v1/"
    "selected_tile_family_manifest.json"
)


TILE_LABELS = {
    "grass_flat": "grass terrain tile",
    "dirt_flat": "dirt terrain tile",
    "grass_cliff_edge": "grass raised cliff edge tile",
    "stone_flat": "stone terrain tile",
    "water_flat": "water terrain tile",
    "water_shore_stone": "water-to-stone shore transition tile",
}


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def repo_path(root: Path, path: Path | str) -> str:
    path = Path(path)
    if not path.is_absolute():
        path = root / path
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path.resolve()).replace("\\", "/")


def resolve_path(root: Path, value: str | Path) -> Path:
    path = Path(value)
    return path if path.is_absolute() else root / path


def dataset_target_for(pack_name: str) -> str:
    return f"C:/Projects/Pixel Pipeline/datasets/lit_iso/review_packs/{pack_name}"


def caption_for(item: dict[str, Any], pack_name: str) -> str:
    tile_id = str(item.get("id") or "tile")
    label = TILE_LABELS.get(tile_id, tile_id.replace("_", " ") + " tile")
    variant = str(item.get("variant_id") or item.get("label") or "style locked").replace("_", " ")
    return (
        "LIT-ISO isometric tile, "
        f"{label}, {variant} palette, 32x32 source tile, 2:1 isometric pixel art, "
        "transparent background, strict terrain geometry, no props, no characters, "
        "no trees, no buildings, no floor clutter, training source "
        f"{pack_name}"
    )


def build_plan(project_root: Path, selected_manifest_path: Path, pack_name: str | None) -> dict[str, Any]:
    selected_manifest = read_json(selected_manifest_path)
    output_root_value = selected_manifest.get("output_root") or str(selected_manifest_path.parent)
    output_root = resolve_path(project_root, output_root_value)
    resolved_pack_name = pack_name or output_root.name
    generated_utc = utc_now()
    records: list[dict[str, Any]] = []

    for index, item in enumerate(selected_manifest.get("selected") or [], start=1):
        file_path = str(item.get("path") or "").replace("\\", "/")
        source_path = str(item.get("source_path") or "").replace("\\", "/")
        tile_id = str(item.get("id") or Path(file_path).stem)
        records.append(
            {
                "id": tile_id,
                "file_name": file_path,
                "caption": caption_for(item, resolved_pack_name),
                "asset_mode": "tile",
                "category": "terrain",
                "biome": "Reference32",
                "material": tile_id,
                "shape": str(item.get("variant_id") or "style_locked"),
                "frame_index": index,
                "frame_count": len(selected_manifest.get("selected") or []),
                "width": item.get("width"),
                "height": item.get("height"),
                "colors": item.get("colors"),
                "alpha_coverage": item.get("alpha_coverage"),
                "approved": False,
                "source_tile": item.get("source_tile"),
                "source_path": source_path,
                "source_variant": item.get("variant_id"),
                "source_status": item.get("status"),
                "unity_destination": item.get("unity_destination"),
                "manual_art_quality": "needs_review",
            }
        )

    return {
        "schema": "lit_iso.asset_forge.tile_training_capture_plan.v1",
        "generated_utc": generated_utc,
        "status": "pending_manual_approval",
        "source_manifest": repo_path(project_root, selected_manifest_path),
        "source_manifest_schema": selected_manifest.get("schema"),
        "pack_name": resolved_pack_name,
        "dataset_target": dataset_target_for(resolved_pack_name),
        "records": records,
        "next_gate": (
            "Mark review_decisions.json entries approved only after visual/license approval; "
            "then run capture_approved_review_pack.py --apply outside Codex sandbox if external dataset writes are approved."
        ),
        "notes": [
            "This is a plan only. It does not approve or copy training examples.",
            "Tiles are terrain-only captions so the tile LoRA does not learn trees, props, floors, or characters on top of tiles.",
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Build a training capture plan for the selected LIT-ISO tile review pack.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--selected-manifest", default=DEFAULT_SELECTED_MANIFEST)
    parser.add_argument("--pack-name", default="")
    parser.add_argument("--out", default="")
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    selected_manifest_path = resolve_path(project_root, args.selected_manifest).resolve()
    if not selected_manifest_path.exists():
        raise FileNotFoundError(f"Missing selected tile manifest: {selected_manifest_path}")

    selected_manifest = read_json(selected_manifest_path)
    output_root = resolve_path(project_root, selected_manifest.get("output_root") or selected_manifest_path.parent)
    out_path = resolve_path(project_root, args.out) if args.out else output_root / "training_capture_plan.json"
    plan = build_plan(project_root, selected_manifest_path, args.pack_name or None)
    write_json(out_path, plan)
    print(
        json.dumps(
            {
                "ok": True,
                "training_capture_plan": repo_path(project_root, out_path),
                "records": len(plan["records"]),
                "status": plan["status"],
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
