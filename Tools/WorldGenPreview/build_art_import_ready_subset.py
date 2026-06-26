#!/usr/bin/env python3
"""
Build the art-lane import-ready subset from the Unity dry-run report.

This is still non-destructive: it does not copy anything into Assets/Resources.
It extracts the rows that passed the Unity dry-run gate, separates no-overwrite
imports from existing-runtime conflicts, and writes contact sheets/manifests for
Claude/art-lane review.
"""

from __future__ import annotations

import argparse
import json
import math
import os
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any

try:
    from PIL import Image, ImageDraw
except ImportError as exc:
    raise SystemExit("Pillow is required. Use the ComfyUI venv Python on this machine.") from exc


ROOT = Path(__file__).resolve().parents[2]
REVIEW_ROOT = Path(os.environ.get("LITISO_WORLDGEN_REVIEW_ROOT", r"C:\tmp\LitIsoWorldGen"))
DEFAULT_SELECTION = REVIEW_ROOT / "art_promotion_selection" / "art_promotion_selection_manifest.json"
DEFAULT_DRY_RUN = REVIEW_ROOT / "art_promotion_selection" / "unity_art_promotion_dry_run_report.json"
DEFAULT_OUT = REVIEW_ROOT / "art_import_ready_subset"


def rel(path: Path) -> str:
    try:
        return path.relative_to(ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def resolve_path(path: str) -> Path:
    p = Path(path)
    return p if p.is_absolute() else ROOT / p


def warning_lookup(report: dict[str, Any]) -> dict[str, list[dict[str, str]]]:
    by_id: dict[str, list[dict[str, str]]] = defaultdict(list)
    for warning in report.get("warnings", []):
        by_id[warning.get("stable_id", "")].append(warning)
    return by_id


def candidate_lookup(report: dict[str, Any]) -> dict[str, dict[str, Any]]:
    return {row["stable_id"]: row for row in report.get("candidates", []) if row.get("stable_id")}


def has_warning(row: dict[str, Any], warnings: dict[str, list[dict[str, str]]], code: str) -> bool:
    return any(w.get("code") == code for w in warnings.get(row["stable_id"], []))


def combine_rows(selection: dict[str, Any], report: dict[str, Any]) -> list[dict[str, Any]]:
    candidates = candidate_lookup(report)
    warnings = warning_lookup(report)
    rows: list[dict[str, Any]] = []
    for selected in selection.get("selected", []):
        stable_id = selected.get("stable_id", "")
        candidate = candidates.get(stable_id, {})
        action = candidate.get("action", "missing_from_dry_run_report")
        row = {
            "stable_id": stable_id,
            "kind": selected.get("kind"),
            "source_type": selected.get("source_type"),
            "family": selected.get("family"),
            "selection_tier": selected.get("selection_tier"),
            "action": action,
            "source_path": selected.get("source_path"),
            "staged_path": selected.get("staged_path"),
            "runtime_destination": selected.get("destinations", {}).get("runtime"),
            "generated_review_destination": selected.get("destinations", {}).get("generated_review"),
            "ppu": selected.get("scale", {}).get("ppu"),
            "footprint": selected.get("scale", {}).get("footprint", "1x1"),
            "height_class": selected.get("scale", {}).get("height_class"),
            "blocks_movement": selected.get("scale", {}).get("blocks_movement"),
            "anchor": selected.get("scale", {}).get("anchor"),
            "review_status": selected.get("review_status"),
            "manual_review_required": selected.get("manual_review_required", False),
            "flags": selected.get("flags", []),
            "warning_codes": sorted({w.get("code", "") for w in warnings.get(stable_id, []) if w.get("code")}),
        }
        if action == "ready_for_art_lane_import" and not has_warning(row, warnings, "runtime_destination_exists"):
            row["import_bucket"] = "ready_no_overwrite"
        elif action == "ready_for_art_lane_import":
            row["import_bucket"] = "ready_existing_runtime_conflict"
        else:
            row["import_bucket"] = "held"
        rows.append(row)
    return rows


def make_contact_sheet(rows: list[dict[str, Any]], out_path: Path, title: str) -> None:
    if not rows:
        return
    thumb_w, thumb_h = 96, 96
    cell_w, cell_h = 188, 154
    cols = min(6, len(rows))
    grid_rows = math.ceil(len(rows) / cols)
    sheet = Image.new("RGBA", (cols * cell_w, grid_rows * cell_h + 30), (34, 37, 46, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((8, 8), title, fill=(245, 229, 190, 255))
    for index, row in enumerate(rows):
        x = (index % cols) * cell_w
        y = (index // cols) * cell_h + 30
        staged = resolve_path(row["staged_path"] or row["source_path"])
        try:
            image = Image.open(staged).convert("RGBA")
        except Exception:
            image = Image.new("RGBA", (32, 32), (180, 30, 60, 255))
        scale = min(thumb_w / image.width, thumb_h / image.height, 4.0)
        size = (max(1, round(image.width * scale)), max(1, round(image.height * scale)))
        resized = image.resize(size, Image.Resampling.NEAREST)
        draw.rectangle((x + 3, y + 3, x + cell_w - 4, y + cell_h - 4), outline=(72, 170, 100, 255), width=2)
        sheet.paste(resized, (x + (cell_w - size[0]) // 2, y + 8), resized)
        draw.text((x + 8, y + 108), row["stable_id"][:26], fill=(245, 229, 190, 255))
        draw.text((x + 8, y + 121), f"{row['kind']} {row['selection_tier']}"[:32], fill=(190, 202, 220, 255))
        draw.text((x + 8, y + 134), f"ppu {row['ppu']} {row['footprint']}"[:32], fill=(150, 160, 176, 255))
    out_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out_path)


def write_markdown(manifest: dict[str, Any], path: Path) -> None:
    summary = manifest["summary"]
    lines = [
        "# Art Import-Ready Subset",
        "",
        "Dry-run only. This is an art-lane approval manifest, not an import operation.",
        "",
        "## Summary",
        "",
        f"- Dry-run passed: `{summary['dry_run_passed']}`",
        f"- Selected rows: `{summary['selected_count']}`",
        f"- Ready, no overwrite: `{summary['ready_no_overwrite']}`",
        f"- Ready but existing runtime destination: `{summary['ready_existing_runtime_conflict']}`",
        f"- Held/skipped: `{summary['held']}`",
        f"- By kind: `{summary['ready_no_overwrite_by_kind']}`",
        f"- By tier: `{summary['ready_no_overwrite_by_tier']}`",
        "",
        "## Contact Sheets",
        "",
    ]
    for sheet in manifest["contact_sheets"]:
        lines.append(f"- `{sheet}`")
    lines.extend([
        "",
        "## Approval Rules",
        "",
        "- `ready_no_overwrite` rows are the first safe art-lane import candidates.",
        "- `ready_existing_runtime_conflict` rows require explicit overwrite/versioning approval.",
        "- `held` rows are not importable until their warning reason is resolved.",
        "- This file does not grant approval to copy into `Assets/Resources`.",
        "",
        "## First Ready Candidates",
        "",
    ])
    for row in manifest["ready_no_overwrite"][:120]:
        lines.append(
            f"- `{row['stable_id']}` [{row['kind']}/{row['selection_tier']}] -> "
            f"`{row['runtime_destination']}` from `{row['staged_path']}`"
        )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--selection", type=Path, default=DEFAULT_SELECTION)
    parser.add_argument("--dry-run", type=Path, default=DEFAULT_DRY_RUN)
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    args = parser.parse_args()

    selection = load_json(args.selection)
    report = load_json(args.dry_run)
    rows = combine_rows(selection, report)

    ready_no_overwrite = [r for r in rows if r["import_bucket"] == "ready_no_overwrite"]
    ready_conflicts = [r for r in rows if r["import_bucket"] == "ready_existing_runtime_conflict"]
    held = [r for r in rows if r["import_bucket"] == "held"]

    args.out.mkdir(parents=True, exist_ok=True)
    contact_sheets: list[str] = []
    sheet_sets = {
        "ready_no_overwrite": ready_no_overwrite,
        "ready_tiles_no_overwrite": [r for r in ready_no_overwrite if r["kind"] == "tile"],
        "ready_props_no_overwrite": [r for r in ready_no_overwrite if r["kind"] == "prop"],
        "ready_existing_runtime_conflicts": ready_conflicts,
        "held": held,
    }
    for name, sheet_rows in sheet_sets.items():
        out_path = args.out / "contact_sheets" / f"{name}.png"
        make_contact_sheet(sheet_rows, out_path, name.replace("_", " ").title())
        if sheet_rows:
            contact_sheets.append(rel(out_path))

    summary = {
        "dry_run_passed": bool(report.get("passed")),
        "selected_count": len(rows),
        "ready_no_overwrite": len(ready_no_overwrite),
        "ready_existing_runtime_conflict": len(ready_conflicts),
        "held": len(held),
        "ready_no_overwrite_by_kind": dict(sorted(Counter(r["kind"] for r in ready_no_overwrite).items())),
        "ready_no_overwrite_by_tier": dict(sorted(Counter(r["selection_tier"] for r in ready_no_overwrite).items())),
        "held_by_action": dict(sorted(Counter(r["action"] for r in held).items())),
        "held_warning_codes": dict(sorted(Counter(code for r in held for code in r["warning_codes"]).items())),
    }

    manifest = {
        "schema": "litiso.art_import_ready_subset.v1",
        "selection_manifest": rel(args.selection),
        "unity_dry_run_report": rel(args.dry_run),
        "summary": summary,
        "contact_sheets": contact_sheets,
        "ready_no_overwrite": ready_no_overwrite,
        "ready_existing_runtime_conflicts": ready_conflicts,
        "held": held,
    }
    manifest_path = args.out / "art_import_ready_subset_manifest.json"
    summary_path = args.out / "art_import_ready_subset_summary.md"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    write_markdown(manifest, summary_path)
    print(json.dumps({
        "manifest": rel(manifest_path),
        "summary": rel(summary_path),
        **summary,
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
