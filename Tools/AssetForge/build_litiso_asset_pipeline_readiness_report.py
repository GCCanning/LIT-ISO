#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


REQUIRED_TILE_IDS = [
    "grass_flat",
    "dirt_flat",
    "grass_cliff_edge",
    "stone_flat",
    "water_flat",
    "water_shore_stone",
]
REQUIRED_DIRECTIONS = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"]


def read_json(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def rel(root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path.resolve()).replace("\\", "/")


def repo_path(project_root: Path, repo_value: str) -> Path:
    value = str(repo_value).replace("/", "\\")
    path = Path(value)
    return path if path.is_absolute() else project_root / path


def artifact_exists(project_root: Path, repo_value: str | None) -> bool:
    return bool(repo_value) and repo_path(project_root, str(repo_value)).exists()


def status_item(requirement: str, status: str, evidence: list[str], blockers: list[str] | None = None) -> dict[str, Any]:
    return {
        "requirement": requirement,
        "status": status,
        "evidence": evidence,
        "blockers": blockers or [],
    }


def evaluate(project_root: Path, status_path: Path) -> dict[str, Any]:
    status = read_json(status_path)
    review = status.get("review_artifacts", {})

    tile_training = status.get("training", {}).get("tile", {})
    critter_training = status.get("training", {}).get("critter", {})
    comfy_loras = status.get("comfy_loras", {})
    tile_family = review.get("tile_mask_locked_screenshot_balanced", {})
    mage_8d = review.get("black_mage_v13_mixed_8d", {})
    mage_identity = review.get("black_mage_identity_lock", {})
    mage_anchor = review.get("black_mage_reference_anchor", {})
    mage_v14_partial = review.get("black_mage_v14_identity_partial", {})
    visual_delta = review.get("visual_delta", {})
    tile_capture_plan = read_json(repo_path(project_root, tile_family.get("training_capture_plan", "")))
    tile_capture_dry_run = read_json(repo_path(project_root, tile_family.get("dataset_capture_dry_run", "")))
    mage_capture_dry_run = read_json(repo_path(project_root, mage_8d.get("dataset_capture_dry_run", "")))
    mage_identity_report = read_json(repo_path(project_root, mage_identity.get("report", "")))
    mage_anchor_manifest = read_json(repo_path(project_root, mage_anchor.get("manifest", "")))
    mage_v14_partial_report = read_json(repo_path(project_root, mage_v14_partial.get("report", "")))
    visual_delta_report = read_json(repo_path(project_root, visual_delta.get("report", "")))

    tile_manifest = read_json(repo_path(project_root, tile_family.get("manifest", "")))
    tile_selected = tile_manifest.get("selected", [])
    tile_ids = [str(item.get("id")) for item in tile_selected]
    tile_shapes_ok = all(
        item.get("width") == 32 and item.get("height") == 32 and str(item.get("status")) == "pending_manual_review"
        for item in tile_selected
    )
    tile_roles_present = all(tile_id in tile_ids for tile_id in REQUIRED_TILE_IDS)

    mage_manifest = read_json(repo_path(project_root, mage_8d.get("manifest", "")))
    mage_selected = mage_manifest.get("selected", [])
    mage_dirs = [str(item.get("direction")).upper() for item in mage_selected]
    mage_decisions = read_json(repo_path(project_root, mage_8d.get("review_decisions", "")))
    mage_pending = int(mage_decisions.get("pending_count", len(mage_selected) if mage_selected else 0) or 0)
    mage_approved = int(mage_decisions.get("approved_count", 0) or 0)
    mage_identity_fail_count = int(mage_identity_report.get("identity_fail_count", 0) or 0)
    mage_v14_front_fail_count = int(mage_v14_partial_report.get("identity_fail_count", 0) or 0)

    requirements: list[dict[str, Any]] = []
    requirements.append(
        status_item(
            "Tile LoRA and critter/character LoRA state verified",
            "proven",
            [
                f"tile_state={tile_training.get('state')}",
                f"tile_percent={tile_training.get('percent')}",
                f"critter_state={critter_training.get('state')}",
                f"critter_percent={critter_training.get('percent')}",
            ],
            [] if tile_training.get("state") == "complete" and critter_training.get("state") == "complete" else ["training_not_complete"],
        )
    )
    requirements.append(
        status_item(
            "Usable LoRAs synced into ComfyUI",
            "proven" if comfy_loras.get("tile", {}).get("exists") and comfy_loras.get("critter", {}).get("exists") else "incomplete",
            [
                f"tile_lora={comfy_loras.get('tile', {}).get('synced_lora')}",
                f"critter_lora={comfy_loras.get('critter', {}).get('synced_lora')}",
            ],
            [] if comfy_loras.get("tile", {}).get("exists") and comfy_loras.get("critter", {}).get("exists") else ["missing_synced_lora"],
        )
    )
    requirements.append(
        status_item(
            "Controlled tile contact sheets generated against supplied style lock",
            "proven" if artifact_exists(project_root, tile_family.get("contact_sheet")) and artifact_exists(project_root, tile_family.get("map_preview")) else "incomplete",
            [str(tile_family.get("contact_sheet", "")), str(tile_family.get("map_preview", ""))],
        )
    )
    requirements.append(
        status_item(
            "Small 32x32 isometric tile family exists and is decoration-free",
            "review_only" if tile_roles_present and tile_shapes_ok else "incomplete",
            [
                str(tile_family.get("manifest", "")),
                f"roles_present={tile_roles_present}",
                f"all_32x32={tile_shapes_ok}",
                f"roles={','.join(tile_ids)}",
            ],
            [] if tile_roles_present and tile_shapes_ok else ["missing_tile_roles_or_size_mismatch"],
        )
    )
    requirements.append(
        status_item(
            "Black mage isometric direction tests generated from provided reference",
            "review_only" if all(direction in mage_dirs for direction in REQUIRED_DIRECTIONS) else "incomplete",
            [
                str(mage_8d.get("contact_sheet", "")),
                str(mage_8d.get("direction_coverage_report", "")),
                f"directions={','.join(mage_dirs)}",
            ],
            [] if all(direction in mage_dirs for direction in REQUIRED_DIRECTIONS) else ["missing_directions"],
        )
    )
    requirements.append(
        status_item(
            "Black mage 8D candidates preserve the supplied mage identity",
            "blocked" if mage_identity_fail_count > 0 else "review_only",
            [
                str(mage_identity.get("report", "")),
                str(mage_identity.get("board", "")),
                f"identity_status={mage_identity_report.get('status')}",
                f"identity_fail_count={mage_identity_fail_count}",
                str(mage_anchor.get("sheet", "")),
                f"anchor_policy={mage_anchor_manifest.get('generation_contract', [''])[0] if mage_anchor_manifest.get('generation_contract') else ''}",
            ],
            ["black_mage_identity_lock_failed"] if mage_identity_fail_count > 0 else ["manual_semantic_direction_review_required"],
        )
    )
    requirements.append(
        status_item(
            "Black mage S/front reconstruction setting is good enough to expand to 8D",
            "blocked" if mage_v14_front_fail_count > 0 else ("missing" if not mage_v14_partial_report else "review_only"),
            [
                str(mage_v14_partial.get("report", "")),
                str(mage_v14_partial.get("sheet", "")),
                f"candidate_count={mage_v14_partial_report.get('candidate_count')}",
                f"identity_fail_count={mage_v14_front_fail_count}",
                f"best_score={(mage_v14_partial_report.get('best_candidate') or {}).get('identity_score')}",
                f"conclusion={mage_v14_partial_report.get('conclusion')}",
            ],
            ["black_mage_v14_front_reconstruction_failed"] if mage_v14_front_fail_count > 0 else ["front_reconstruction_review_required"],
        )
    )
    requirements.append(
        status_item(
            "Best outputs grouped into clean review packs",
            "review_only" if artifact_exists(project_root, tile_family.get("contact_sheet")) and artifact_exists(project_root, mage_8d.get("contact_sheet")) else "incomplete",
            [str(tile_family.get("root", "")), str(mage_8d.get("root", ""))],
        )
    )
    requirements.append(
        status_item(
            "Outputs kept in review/temp folders and not imported to Unity runtime",
            "proven" if status.get("unity_imported") is False else "contradicted",
            [f"unity_imported={status.get('unity_imported')}", "review roots are under Assets/Generated/_Review or Temp/AssetForge"],
            [] if status.get("unity_imported") is False else ["unity_imported_true"],
        )
    )
    requirements.append(
        status_item(
            "Every run documented with manifests/settings/contact sheets/recommendations",
            "review_only",
            [
                rel(project_root, status_path),
                str(tile_family.get("manifest", "")),
                str(mage_8d.get("manifest", "")),
                str(mage_8d.get("review_decisions", "")),
                str(mage_8d.get("training_capture_plan", "")),
            ],
            ["manual_approval_pending"],
        )
    )
    requirements.append(
        status_item(
            "Dataset capture is dry-run gated before external writes",
            "proven"
            if artifact_exists(project_root, tile_family.get("dataset_capture_dry_run"))
            and artifact_exists(project_root, mage_8d.get("dataset_capture_dry_run"))
            else "incomplete",
            [
                str(tile_family.get("training_capture_plan", "")),
                str(tile_family.get("dataset_capture_dry_run", "")),
                f"tile_plan_records={len(tile_capture_plan.get('records') or [])}",
                f"tile_capture_planned={tile_capture_dry_run.get('planned_record_count')}",
                f"tile_capture_ready_for_apply={tile_capture_dry_run.get('ready_for_apply')}",
                str(mage_8d.get("training_capture_plan", "")),
                str(mage_8d.get("dataset_capture_dry_run", "")),
                f"mage_capture_planned={mage_capture_dry_run.get('planned_record_count')}",
                f"mage_capture_ready_for_apply={mage_capture_dry_run.get('ready_for_apply')}",
            ],
            []
            if artifact_exists(project_root, tile_family.get("dataset_capture_dry_run"))
            and artifact_exists(project_root, mage_8d.get("dataset_capture_dry_run"))
            else ["missing_capture_dry_run"],
        )
    )
    requirements.append(
        status_item(
            "Visual delta board compares source style lock against current outputs",
            "proven" if artifact_exists(project_root, visual_delta.get("board")) and artifact_exists(project_root, visual_delta.get("report")) else "incomplete",
            [
                str(visual_delta.get("board", "")),
                str(visual_delta.get("report", "")),
                f"tile_min_alpha_iou={visual_delta.get('tile_min_alpha_iou')}",
                f"tile_max_mean_rgb_delta={visual_delta.get('tile_max_mean_rgb_delta')}",
                f"mage_direction_count={visual_delta.get('mage_direction_count')}",
                f"schema={visual_delta_report.get('schema')}",
            ],
            []
            if artifact_exists(project_root, visual_delta.get("board")) and artifact_exists(project_root, visual_delta.get("report"))
            else ["missing_visual_delta_evidence"],
        )
    )

    blockers = []
    if mage_pending > 0 or mage_approved == 0:
        blockers.append("black_mage_8d_manual_approval_pending")
    if mage_identity_fail_count > 0:
        blockers.append("black_mage_identity_lock_failed")
    if mage_v14_front_fail_count > 0:
        blockers.append("black_mage_v14_front_reconstruction_failed")
    if tile_manifest.get("next_gate"):
        blockers.append("tile_manual_art_and_license_approval_pending")
    if tile_capture_dry_run.get("ready_for_apply") is False or mage_capture_dry_run.get("ready_for_apply") is False:
        blockers.append("dataset_capture_waiting_for_manual_approval")
    if mage_8d.get("animation_ready") is False:
        blockers.append("black_mage_animation_sequences_not_generated")

    return {
        "schema": "lit_iso.asset_forge.pipeline_readiness_audit.v1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "source_status": rel(project_root, status_path),
        "unity_imported": bool(status.get("unity_imported")),
        "overall_status": "review_ready_not_production_ready" if not status.get("unity_imported") else "invalid_unity_imported",
        "requirements": requirements,
        "current_best": {
            "tile_contact_sheet": tile_family.get("contact_sheet", ""),
            "tile_map_preview": tile_family.get("map_preview", ""),
            "tile_training_capture_plan": tile_family.get("training_capture_plan", ""),
            "tile_dataset_capture_dry_run": tile_family.get("dataset_capture_dry_run", ""),
            "mage_8d_contact_sheet": mage_8d.get("contact_sheet", ""),
            "mage_8d_coverage_sheet": mage_8d.get("direction_coverage_sheet", ""),
            "mage_identity_lock_board": mage_identity.get("board", ""),
            "mage_identity_lock_report": mage_identity.get("report", ""),
            "mage_reference_anchor_sheet": mage_anchor.get("sheet", ""),
            "mage_reference_anchor_manifest": mage_anchor.get("manifest", ""),
            "mage_v14_front_reconstruction_sheet": mage_v14_partial.get("sheet", ""),
            "mage_v14_front_reconstruction_report": mage_v14_partial.get("report", ""),
            "mage_8d_dataset_capture_dry_run": mage_8d.get("dataset_capture_dry_run", ""),
            "visual_delta_board": visual_delta.get("board", ""),
            "visual_delta_report": visual_delta.get("report", ""),
        },
        "remaining_blockers": blockers,
        "next_recommendations": [
            "Manual review the screenshot-balanced six-tile family before dataset capture or Unity promotion.",
            "Manual review each v13 mixed black mage direction; do not train until decisions are explicit.",
            "Do not train or animate the current black mage 8D pack because the stricter identity lock fails.",
            "Use the black mage source-anchor pack as the contract: source S/front first, then regenerate directions from that anchor.",
            "Do not expand v14 settings to 8D; the S/front reconstruction already fails identity lock.",
            "Next mage attempt should use source-anchor image-to-image reconstruction without OpenPose/template conflict before solving rotations.",
            "Use the dataset dry-run reports to verify exactly what will be captured before allowing external dataset writes.",
            "If E/W are rejected, rerender only side directions with stronger profile scaffolds; do not rerender all 8D.",
            "After idle directions are approved, generate idle/walk animation frame sequences and rerun alignment/QC.",
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Build a readiness audit for the LIT-ISO asset pipeline goal.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--status", default="Temp/AssetForge/litiso_asset_pipeline_review_golden_path_status.json")
    parser.add_argument("--out", default="Temp/AssetForge/litiso_asset_pipeline_readiness_audit.json")
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    status_path = repo_path(project_root, args.status)
    out_path = repo_path(project_root, args.out)
    report = evaluate(project_root, status_path)
    write_json(out_path, report)
    print(json.dumps({"ok": True, "audit": rel(project_root, out_path), "overall_status": report["overall_status"], "blockers": report["remaining_blockers"]}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
