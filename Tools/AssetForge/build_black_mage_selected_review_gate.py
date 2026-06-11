#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


DIRECTION_LABELS = {
    "N": "north, back facing",
    "NE": "north-east, back three-quarter facing",
    "E": "east, side facing",
    "SE": "south-east, front three-quarter facing",
    "S": "south, front facing",
    "SW": "south-west, front three-quarter facing",
    "W": "west, side facing",
    "NW": "north-west, back three-quarter facing",
}


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def repo_path(root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path)


def caption_for(direction: str, frame_index: int, total_frames: int) -> str:
    direction_text = DIRECTION_LABELS.get(direction.upper(), direction.lower())
    return (
        "LIT-ISO pixel character sprite, black mage with wide dark hat, black coat, "
        "small staff, warm face pixels, idle pose, "
        f"{direction_text}, frame {frame_index} of {total_frames}, "
        "isometric view, transparent background, bottom-center anchor"
    )


def dataset_target_for(pack_name: str) -> str:
    return f"C:/Projects/Pixel Pipeline/datasets/lit_iso/review_packs/{pack_name}"


def main() -> int:
    parser = argparse.ArgumentParser(description="Build review-gate metadata for selected black mage candidates.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--selected-manifest", default="Assets/Generated/_Review/black_mage_iso_selected_v11/black_mage_selected_v11_manifest.json")
    parser.add_argument("--pack-name", default="")
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    selected_manifest_path = project_root / args.selected_manifest
    selected_manifest = read_json(selected_manifest_path)
    out_root = project_root / selected_manifest.get("out_root", str(selected_manifest_path.parent))
    pack_name = args.pack_name or out_root.name
    generated_utc = datetime.now(timezone.utc).isoformat()

    selected_items = []
    decisions = []
    capture_records = []
    selected = list(selected_manifest.get("selected", []))
    for index, item in enumerate(selected, start=1):
        direction = str(item.get("direction", "")).upper()
        source_path = str(item["path"]).replace("\\", "/")
        name = Path(source_path).name
        record_id = name
        caption = caption_for(direction, index, len(selected))
        destination = f"Assets/Generated/Characters/BlackMage/idle/{direction.lower()}/{name}"
        metrics = item.get("metrics") or {}
        item_warnings = ["manual_art_approval_required", "not_runtime_imported"]
        source_variant = str(item.get("source_variant", ""))
        manual_quality = str(item.get("manual_direction_quality", ""))
        if source_variant:
            item_warnings.append(f"source_variant:{source_variant}")
        if manual_quality:
            item_warnings.append(f"manual_direction_quality:{manual_quality}")
        selected_items.append({
            "id": record_id,
            "name": name,
            "path": source_path,
            "category": "character",
            "asset_mode": "character",
            "biome": "Character",
            "character_id": "black_mage",
            "action": "idle",
            "direction": direction,
            "direction_label": DIRECTION_LABELS.get(direction, direction),
            "width": 128,
            "height": 128,
            "status": "review",
            "issues": [],
            "warnings": item_warnings,
            "metrics": metrics,
            "generation": {
                "source_manifest": repo_path(project_root, selected_manifest_path),
                "source_path": item.get("source_path", ""),
                "source_variant": source_variant,
                "seed": item.get("seed", ""),
                "structural_score": item.get("score"),
                "variant": selected_manifest.get("variant", ""),
            },
            "caption": caption,
            "unity": {"category": "Characters", "ppu": 128, "pivot": {"x": 0.5, "y": 0.08}},
        })
        decisions.append({
            "id": record_id,
            "name": name,
            "decision": "pending",
            "source_path": source_path,
            "destination_path": destination,
            "category": "character",
            "asset_mode": "character",
            "biome": "Character",
            "character_id": "black_mage",
            "action": "idle",
            "direction": direction,
            "unity": {"category": "Characters", "ppu": 128, "pivot": {"x": 0.5, "y": 0.08}},
            "notes": "Pending manual direction/style approval before dataset capture or Unity promotion.",
            "source_variant": source_variant,
            "manual_direction_quality": manual_quality,
        })
        capture_records.append({
            "file_name": source_path,
            "caption": caption,
            "direction": direction,
            "action": "idle",
            "frame_index": index,
            "frame_count": len(selected),
            "approved": False,
            "source_variant": source_variant,
            "manual_direction_quality": manual_quality,
            "source_seed": item.get("seed", ""),
            "source_score": item.get("score"),
        })

    review_report = {
        "schema": "lit_iso.asset_forge.review_report.v1",
        "pack_name": pack_name,
        "generated_utc": generated_utc,
        "provider": "comfyui_review_selection",
        "asset_mode": "character",
        "status": "review_only_not_unity_imported",
        "source_manifest": repo_path(project_root, selected_manifest_path),
        "contact_sheet": selected_manifest.get("contact_sheet", ""),
        "total": len(selected_items),
        "pass_count": 0,
        "review_count": len(selected_items),
        "items": selected_items,
    }
    review_decisions = {
        "schema": "lit_iso.asset_forge.review_decisions.v1",
        "pack_name": pack_name,
        "generated_utc": generated_utc,
        "source_report": repo_path(project_root, out_root / "review_report.json"),
        "decision_policy": "manual_review",
        "total": len(decisions),
        "approved_count": 0,
        "pending_count": len(decisions),
        "decisions": decisions,
    }
    capture_plan = {
        "schema": "lit_iso.asset_forge.black_mage_training_capture_plan.v1",
        "generated_utc": generated_utc,
        "status": "pending_manual_approval",
        "source_manifest": repo_path(project_root, selected_manifest_path),
        "dataset_target": dataset_target_for(pack_name),
        "records": capture_records,
        "next_gate": "Mark review_decisions.json entries approved only after visual approval; then run capture_dataset_from_review.ps1.",
    }

    write_json(out_root / "review_report.json", review_report)
    write_json(out_root / "review_decisions.json", review_decisions)
    write_json(out_root / "training_capture_plan.json", capture_plan)

    print(json.dumps({
        "ok": True,
        "review_report": repo_path(project_root, out_root / "review_report.json"),
        "review_decisions": repo_path(project_root, out_root / "review_decisions.json"),
        "training_capture_plan": repo_path(project_root, out_root / "training_capture_plan.json"),
        "items": len(selected_items),
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
