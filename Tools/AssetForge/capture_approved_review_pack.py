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


DEFAULT_DATASET_ROOT = Path(r"C:\Projects\Pixel Pipeline\datasets\lit_iso\review_packs")
DEFAULT_REPORT_ROOT = Path("Temp/AssetForge/dataset_capture_plans")
CAPTURE_DECISIONS = {"approved"}


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def read_json(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {}
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


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def safe_name(value: str) -> str:
    return "".join(char if char.isalnum() or char in "._-" else "_" for char in value).strip("_")


def assert_under(root: Path, target: Path, label: str) -> None:
    root_full = root.resolve()
    target_full = target.resolve()
    if target_full != root_full and root_full not in target_full.parents:
        raise ValueError(f"{label} must stay under {root_full}; got {target_full}")


def report_items_by_id(report: dict[str, Any]) -> dict[str, dict[str, Any]]:
    items: dict[str, dict[str, Any]] = {}
    for item in report.get("items") or []:
        item_id = str(item.get("id") or Path(str(item.get("path", ""))).name)
        items[item_id] = item
        if item.get("path"):
            items[str(item["path"]).replace("\\", "/")] = item
            items[Path(str(item["path"])).name] = item
    return items


def capture_records_by_source(plan: dict[str, Any]) -> dict[str, dict[str, Any]]:
    records: dict[str, dict[str, Any]] = {}
    for record in plan.get("records") or []:
        for key in ("file_name", "source_path", "id"):
            value = record.get(key)
            if value:
                records[str(value).replace("\\", "/")] = record
                records[Path(str(value)).name] = record
    return records


def fallback_caption(decision: dict[str, Any], report_item: dict[str, Any] | None) -> str:
    category = str(decision.get("asset_mode") or decision.get("category") or "asset")
    name = Path(str(decision.get("name") or decision.get("source_path") or "asset")).stem.replace("_", " ")
    if category == "tile" or decision.get("category") == "terrain":
        return (
            f"LIT-ISO isometric tile, {name}, 2:1 isometric pixel art, transparent background, "
            "strict terrain geometry, no props, no characters, no trees"
        )
    if category == "character":
        direction = str(decision.get("direction") or "")
        action = str(decision.get("action") or "idle")
        return (
            f"LIT-ISO pixel character sprite, {name}, {action}, {direction}, "
            "isometric view, transparent background, bottom-center anchor"
        )
    if report_item and report_item.get("caption"):
        return str(report_item["caption"])
    return f"LIT-ISO pixel art asset, {name}, transparent background"


def planned_subdir(decision: dict[str, Any]) -> str:
    mode = str(decision.get("asset_mode") or decision.get("category") or "").lower()
    if mode in {"tile", "terrain"}:
        return "output/tiles"
    if mode == "character":
        return "output/characters"
    if mode in {"mob", "npc"}:
        return "output/actors"
    if mode in {"prop", "decoration"}:
        return "output/props"
    if mode == "item":
        return "output/items"
    return "output/assets"


def build_capture_plan(
    project_root: Path,
    pack_root: Path,
    dataset_root: Path,
    include_rejected: bool,
) -> dict[str, Any]:
    decisions_path = pack_root / "review_decisions.json"
    report_path = pack_root / "review_report.json"
    training_plan_path = pack_root / "training_capture_plan.json"
    decisions = read_json(decisions_path)
    report = read_json(report_path)
    training_plan = read_json(training_plan_path)
    pack_name = str(decisions.get("pack_name") or report.get("pack_name") or pack_root.name)
    dataset_pack_root = dataset_root / pack_name
    report_items = report_items_by_id(report)
    planned_sources = capture_records_by_source(training_plan)
    blockers: list[dict[str, Any]] = []
    planned: list[dict[str, Any]] = []
    skipped: list[dict[str, Any]] = []
    decision_counts = {"approved": 0, "pending": 0, "rejected": 0, "needs_edit": 0, "other": 0}

    for decision in decisions.get("decisions") or []:
        decision_value = str(decision.get("decision") or "pending")
        if decision_value in decision_counts:
            decision_counts[decision_value] += 1
        else:
            decision_counts["other"] += 1

        include = decision_value in CAPTURE_DECISIONS or (include_rejected and decision_value == "rejected")
        if not include:
            skipped.append(
                {
                    "id": decision.get("id"),
                    "decision": decision_value,
                    "reason": "not_selected_for_capture",
                }
            )
            continue

        source_value = str(decision.get("source_path") or "")
        source_path = resolve_path(project_root, source_value)
        if not source_value or not source_path.exists():
            blockers.append(
                {
                    "id": decision.get("id"),
                    "decision": decision_value,
                    "reason": "missing_source_image",
                    "source_path": source_value,
                }
            )
            continue
        assert_under(project_root, source_path, "Source image")

        report_item = (
            report_items.get(str(decision.get("id") or ""))
            or report_items.get(source_value.replace("\\", "/"))
            or report_items.get(Path(source_value).name)
        )
        issues = list((report_item or {}).get("issues") or [])
        if decision_value == "approved" and issues:
            blockers.append(
                {
                    "id": decision.get("id"),
                    "decision": decision_value,
                    "reason": "approved_item_has_unresolved_review_issues",
                    "issues": issues,
                }
            )
            continue

        plan_record = (
            planned_sources.get(source_value.replace("\\", "/"))
            or planned_sources.get(Path(source_value).name)
            or planned_sources.get(str(decision.get("id") or ""))
        )
        image_name = safe_name(Path(source_value).name)
        caption = str((plan_record or {}).get("caption") or fallback_caption(decision, report_item))
        output_rel = f"{planned_subdir(decision)}/{image_name}"
        caption_rel = f"captions/{Path(image_name).stem}.txt"
        provenance_rel = f"provenance/{Path(image_name).stem}.json"
        planned.append(
            {
                "id": decision.get("id"),
                "decision": decision_value,
                "source_path": repo_path(project_root, source_path),
                "source_sha256": sha256(source_path),
                "caption": caption,
                "asset_mode": decision.get("asset_mode") or decision.get("category"),
                "category": decision.get("category"),
                "biome": decision.get("biome"),
                "direction": decision.get("direction"),
                "action": decision.get("action"),
                "copy": {
                    "source": str(source_path),
                    "image": str(dataset_pack_root / output_rel),
                    "caption": str(dataset_pack_root / caption_rel),
                    "provenance": str(dataset_pack_root / provenance_rel),
                },
                "dataset_paths": {
                    "image": output_rel,
                    "caption": caption_rel,
                    "provenance": provenance_rel,
                },
                "training_plan_record": plan_record or None,
                "review_item_status": (report_item or {}).get("status"),
            }
        )

    sheet_copies = []
    for key in ("contact_sheet", "preview"):
        value = report.get(key)
        if value:
            source = resolve_path(project_root, value)
            if source.exists():
                sheet_copies.append(
                    {
                        "source": repo_path(project_root, source),
                        "destination": str(dataset_pack_root / "sheet" / source.name),
                    }
                )

    ready_for_apply = len(planned) > 0 and not blockers
    return {
        "schema": "lit_iso.asset_forge.approved_review_pack_capture_dry_run.v1",
        "generated_utc": utc_now(),
        "mode": "dry_run",
        "pack_name": pack_name,
        "pack_root": repo_path(project_root, pack_root),
        "dataset_pack_root": str(dataset_pack_root),
        "decision_source": repo_path(project_root, decisions_path),
        "review_report": repo_path(project_root, report_path),
        "training_capture_plan": repo_path(project_root, training_plan_path) if training_plan_path.exists() else None,
        "include_rejected": include_rejected,
        "decision_counts": decision_counts,
        "planned_record_count": len(planned),
        "skipped_count": len(skipped),
        "blocker_count": len(blockers),
        "ready_for_apply": ready_for_apply,
        "planned_records": planned,
        "planned_sidecar_copies": sheet_copies
        + [
            {
                "source": repo_path(project_root, decisions_path),
                "destination": str(dataset_pack_root / "metadata" / "review_decisions.json"),
            },
            {
                "source": repo_path(project_root, report_path),
                "destination": str(dataset_pack_root / "metadata" / "review_report.json"),
            },
        ],
        "skipped": skipped,
        "blockers": blockers,
        "next_gate": (
            "Approve at least one QA-clean review_decisions.json entry before applying capture."
            if not ready_for_apply
            else "Run with --apply only after external dataset writes are explicitly approved."
        ),
    }


def apply_capture(capture: dict[str, Any], project_root: Path, dataset_root: Path) -> None:
    assert_under(dataset_root, Path(capture["dataset_pack_root"]), "Dataset pack root")
    metadata_rows = []
    for record in capture["planned_records"]:
        copy = record["copy"]
        for key in ("image", "caption", "provenance"):
            assert_under(dataset_root, Path(copy[key]), f"Dataset {key}")
        Path(copy["image"]).parent.mkdir(parents=True, exist_ok=True)
        Path(copy["caption"]).parent.mkdir(parents=True, exist_ok=True)
        Path(copy["provenance"]).parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(copy["source"], copy["image"])
        Path(copy["caption"]).write_text(record["caption"] + "\n", encoding="utf-8")
        provenance = {
            "schema": "lit_iso.asset_forge.review_pack_capture_provenance.v1",
            "captured_utc": utc_now(),
            "record": {key: value for key, value in record.items() if key != "copy"},
            "source_copy": copy,
        }
        write_json(Path(copy["provenance"]), provenance)
        metadata_rows.append({key: value for key, value in record.items() if key not in {"copy", "training_plan_record"}})

    for sidecar in capture["planned_sidecar_copies"]:
        source = resolve_path(project_root, sidecar["source"])
        destination = Path(sidecar["destination"])
        assert_under(dataset_root, destination, "Dataset sidecar")
        if source.exists():
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, destination)

    metadata_path = Path(capture["dataset_pack_root"]) / "metadata.jsonl"
    metadata_path.write_text(
        "\n".join(json.dumps(row, separators=(",", ":")) for row in metadata_rows) + ("\n" if metadata_rows else ""),
        encoding="utf-8",
    )
    write_json(Path(capture["dataset_pack_root"]) / "capture_manifest.json", capture)


def main() -> int:
    parser = argparse.ArgumentParser(description="Dry-run or apply approved Asset Forge review-pack dataset capture.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--pack-name", default="")
    parser.add_argument("--pack-root", default="")
    parser.add_argument("--dataset-root", type=Path, default=DEFAULT_DATASET_ROOT)
    parser.add_argument("--output-report", default="")
    parser.add_argument("--include-rejected", action="store_true")
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    if args.pack_root:
        pack_root = resolve_path(project_root, args.pack_root).resolve()
    elif args.pack_name:
        pack_root = (project_root / "Assets/Generated/_Review" / args.pack_name).resolve()
    else:
        raise ValueError("Pass --pack-name or --pack-root.")
    if not pack_root.exists():
        raise FileNotFoundError(f"Missing review pack root: {pack_root}")

    dataset_root = args.dataset_root.resolve()
    capture = build_capture_plan(project_root, pack_root, dataset_root, args.include_rejected)
    capture["mode"] = "apply" if args.apply else "dry_run"
    if args.apply:
        if not capture["ready_for_apply"]:
            raise RuntimeError(f"Capture is not ready for apply: {capture['next_gate']}")
        apply_capture(capture, project_root, dataset_root)
        capture["applied"] = True
    else:
        capture["applied"] = False

    output_report = (
        resolve_path(project_root, args.output_report)
        if args.output_report
        else project_root / DEFAULT_REPORT_ROOT / f"{capture['pack_name']}_capture_dry_run.json"
    )
    capture["output_report"] = repo_path(project_root, output_report)
    write_json(output_report, capture)
    print(
        json.dumps(
            {
                "ok": True,
                "mode": capture["mode"],
                "pack_name": capture["pack_name"],
                "report": repo_path(project_root, output_report),
                "planned_records": capture["planned_record_count"],
                "blockers": capture["blocker_count"],
                "ready_for_apply": capture["ready_for_apply"],
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
