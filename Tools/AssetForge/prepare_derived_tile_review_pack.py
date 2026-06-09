#!/usr/bin/env python3
"""Build review/decision files for deterministic derived tile packs.

This bridges geometry-derived tile manifests into the existing Asset Forge
approval flow without requiring another generation pass.
"""

from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8-sig") as handle:
        return json.load(handle)


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as handle:
        json.dump(payload, handle, indent=2)
        handle.write("\n")


def normalize_rel(path: str) -> str:
    return path.replace("\\", "/")


def strip_pack_prefix(path: str, pack_name: str, project_root: Path) -> str:
    normalized = normalize_rel(path)
    project_prefix = normalize_rel(str(project_root.resolve())) + "/"
    if normalized.lower().startswith(project_prefix.lower()):
        normalized = normalized[len(project_prefix) :]
    review_prefix = f"Assets/Generated/_Review/{pack_name}/"
    if normalized.startswith(review_prefix):
        return normalized[len(review_prefix) :]
    return normalized


def build_strict_issue_map(
    strict_report: dict[str, Any], pack_name: str, project_root: Path
) -> dict[str, dict[str, list[str]]]:
    issue_map: dict[str, dict[str, list[str]]] = {}
    for item in strict_report.get("items", []):
        key = strip_pack_prefix(str(item.get("path", "")), pack_name, project_root)
        issue_map[key] = {
            "issues": list(item.get("issues", []) or []),
            "warnings": list(item.get("warnings", []) or []),
        }
    return issue_map


def destination_for(item: dict[str, Any], fallback_biome: str) -> str:
    biome = str(item.get("biome") or fallback_biome)
    category = str(item.get("category") or "terrain")
    unity_category = ((item.get("unity") or {}).get("category") or "").lower()
    if category == "decoration" or unity_category == "props":
        root = "Assets/Generated/Props"
    else:
        root = "Assets/Generated/Tiles"
    return f"{root}/{biome}/{item['name']}"


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Convert a derived tile manifest into review_report.json and review_decisions.json."
    )
    parser.add_argument("--project-root", default=".", help="Unity project root.")
    parser.add_argument(
        "--pack-name",
        default="greenwake_geometry_derived_v7",
        help="Pack folder under Assets/Generated/_Review.",
    )
    parser.add_argument(
        "--manifest",
        help="Derived geometry manifest path. Defaults to the pack derived_geometry_manifest.json.",
    )
    parser.add_argument(
        "--strict-report",
        help="Strict QA report path. Defaults to the pack strict_asset_quality_report.json.",
    )
    parser.add_argument(
        "--approve-passing",
        action="store_true",
        help="Mark strict-QA-passing items as approved. Others remain pending.",
    )
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    pack_root = project_root / "Assets" / "Generated" / "_Review" / args.pack_name
    manifest_path = Path(args.manifest) if args.manifest else pack_root / "derived_geometry_manifest.json"
    strict_report_path = (
        Path(args.strict_report) if args.strict_report else pack_root / "strict_asset_quality_report.json"
    )

    manifest = load_json(manifest_path)
    strict_report = load_json(strict_report_path)
    strict_map = build_strict_issue_map(strict_report, args.pack_name, project_root)

    fallback_biome = "Greenwake"
    report_items: list[dict[str, Any]] = []
    decisions: list[dict[str, Any]] = []
    pass_count = 0
    review_count = 0
    approved_count = 0
    pending_count = 0

    for item in manifest.get("items", []):
        source_path = normalize_rel(str(item["path"]))
        item_id = strip_pack_prefix(source_path, args.pack_name, project_root)
        strict_entry = strict_map.get(item_id, {"issues": [], "warnings": []})
        issues = list(strict_entry.get("issues", []))
        warnings = list(strict_entry.get("warnings", []))
        status = "pass" if not issues else "review"
        if status == "pass":
            pass_count += 1
        else:
            review_count += 1

        unity = item.get("unity") or {}
        report_items.append(
            {
                "id": item_id,
                "name": item.get("name"),
                "path": source_path,
                "category": item.get("category", "terrain"),
                "biome": item.get("biome", fallback_biome),
                "material": item.get("material"),
                "shape": item.get("shape"),
                "width": item.get("width"),
                "height": item.get("height"),
                "status": status,
                "issues": issues,
                "warnings": warnings,
                "unity": unity,
            }
        )

        decision = "approved" if args.approve_passing and not issues else "pending"
        if decision == "approved":
            approved_count += 1
        else:
            pending_count += 1

        decisions.append(
            {
                "id": item_id,
                "name": item.get("name"),
                "decision": decision,
                "source_path": source_path,
                "destination_path": destination_for(item, fallback_biome),
                "category": item.get("category", "terrain"),
                "biome": item.get("biome", fallback_biome),
                "material": item.get("material"),
                "shape": item.get("shape"),
                "unity": unity,
                "notes": "Auto-approved from strict QA pass." if decision == "approved" else "",
            }
        )

    generated_utc = datetime.now(timezone.utc).isoformat()
    report_payload = {
        "schema": "lit_iso.asset_forge.review_report.v1",
        "pack_name": args.pack_name,
        "generated_utc": generated_utc,
        "provider": "deterministic_geometry",
        "source_kind": "derived_tile_geometry",
        "asset_mode": "tile",
        "source_manifest": normalize_rel(str(manifest_path.relative_to(project_root))),
        "strict_report": normalize_rel(str(strict_report_path.relative_to(project_root))),
        "contact_sheet": manifest.get("contact_sheet"),
        "preview": manifest.get("map_preview"),
        "showcase": f"Assets/Generated/_Review/{args.pack_name}/derived_geometry_showcase_13x13.png",
        "total": len(report_items),
        "terrain_count": len([item for item in report_items if item.get("category") == "terrain"]),
        "pass_count": pass_count,
        "review_count": review_count,
        "items": report_items,
    }
    decisions_payload = {
        "schema": "lit_iso.asset_forge.review_decisions.v1",
        "pack_name": args.pack_name,
        "generated_utc": generated_utc,
        "source_report": f"Assets/Generated/_Review/{args.pack_name}/review_report.json",
        "decision_policy": "approve_passing" if args.approve_passing else "manual_review",
        "total": len(decisions),
        "approved_count": approved_count,
        "pending_count": pending_count,
        "decisions": decisions,
    }

    review_report_path = pack_root / "review_report.json"
    review_decisions_path = pack_root / "review_decisions.json"
    write_json(review_report_path, report_payload)
    write_json(review_decisions_path, decisions_payload)

    print(
        json.dumps(
            {
                "review_report": normalize_rel(str(review_report_path)),
                "review_decisions": normalize_rel(str(review_decisions_path)),
                "total": len(decisions),
                "approved": approved_count,
                "pending": pending_count,
                "review": review_count,
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
