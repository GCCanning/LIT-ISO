#!/usr/bin/env python3
"""
Select one preferred art candidate per stable tile/prop id.

Input is the non-destructive review pack produced by build_art_review_pack.py.
Output is another non-destructive staging pack under TempEvalDryRun/WorldGen with:

- one selected source per stable id where possible
- proposed Unity/runtime destinations
- import metadata from the review scale pass
- selected PNG copies in Temp only
- contact sheets for selected tiles/props

This does not write to Assets/Resources and does not modify Unity import
settings. It is a promotion plan for art-lane review.
"""

from __future__ import annotations

import argparse
import os
import json
import math
import re
import shutil
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any

try:
    from PIL import Image, ImageDraw
except ImportError as exc:
    raise SystemExit("Pillow is required. Use the ComfyUI venv Python on this machine.") from exc


ROOT = Path(__file__).resolve().parents[2]
REVIEW_ROOT = Path(os.environ.get("LITISO_WORLDGEN_REVIEW_ROOT", r"C:\tmp\LitIsoWorldGen"))
DEFAULT_REVIEW = REVIEW_ROOT / "art_review_pack" / "art_review_manifest.json"
DEFAULT_OUT = REVIEW_ROOT / "art_promotion_selection"

SKIP_GROUP_MARKERS = {
    "v2 size compare",
}


def rel(path: Path) -> str:
    try:
        return path.relative_to(ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def safe_name(value: str) -> str:
    return re.sub(r"[^a-zA-Z0-9_./-]+", "_", value.replace("\\", "/")).strip("_").lower()


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def source_path(row: dict[str, Any]) -> Path:
    path = Path(row["source_path"])
    return path if path.is_absolute() else ROOT / path


def should_skip(row: dict[str, Any]) -> bool:
    if row["review_status"] == "reject":
        return True
    family = row.get("family", "").lower()
    stable_id = row.get("stable_id", "").lower()
    if any(marker in family for marker in SKIP_GROUP_MARKERS):
        return True
    if stable_id.endswith("_sm") or stable_id.endswith("_lg"):
        return True
    if "contact_sheet_not_candidate" in row.get("flags", []):
        return True
    return False


def source_priority(row: dict[str, Any]) -> int:
    source_type = row["source_type"]
    kind = row["kind"]
    worldgen_status = row.get("worldgen_status")
    if worldgen_status == "unity-live":
        return 90
    if kind == "tile":
        order = {
            "biomesketch": 80,
            "pixellab_tilesets": 70,
            "unity_generated_tiles": 65,
        }
    else:
        order = {
            "unity_generated_props": 80,
            "biomesketch": 72,
            "pixellab_props": 68,
        }
    return order.get(source_type, 40)


def quality_score(row: dict[str, Any]) -> float:
    metrics = row["metrics"]
    flags = row.get("flags", [])
    score = 0.0
    score += source_priority(row)
    score += 100 if row["review_status"] == "candidate" else 25
    score += 30 if not flags else -20 * len(flags)

    unique = metrics.get("unique_rgb_sample", 0)
    score += min(28, unique / 140)

    coverage = metrics.get("alpha_coverage", 0)
    if row["kind"] == "tile":
        score += 12 if 0.25 <= coverage <= 1.0 else -10
    else:
        # Props should have meaningful transparent space, but buildings are
        # allowed to occupy more canvas than small decor.
        if "building" in row.get("family", "").lower():
            target = 0.42
        else:
            target = 0.30
        score -= abs(coverage - target) * 18

    stem = Path(row["source_path"]).stem
    frame_match = re.fullmatch(r"frame_(\d+)", stem)
    if frame_match:
        # Deterministic tie-break only; do not make frame_0 automatically win.
        score -= int(frame_match.group(1)) * 0.01
    return round(score, 4)


def destination_for(row: dict[str, Any]) -> dict[str, str]:
    stable_id = row["stable_id"]
    family = safe_name(row.get("family") or "misc")
    if row["kind"] == "tile":
        generated = f"Assets/Generated/Tiles/Worldgen/{family}/{stable_id}.png"
        runtime = f"Assets/Resources/Tiles/{stable_id}.png"
    else:
        generated = f"Assets/Generated/Props/Worldgen/{family}/{stable_id}.png"
        runtime = f"Assets/Resources/Decorations/{stable_id}.png"
    return {"generated_review": generated, "runtime": runtime}


def selection_tier(row: dict[str, Any]) -> str:
    stable_id = row["stable_id"]
    family = row.get("family", "").lower()
    source_type = row["source_type"]
    if row.get("worldgen_status"):
        return "runtime_core"
    if source_type.startswith("unity_generated"):
        return "runtime_core"
    if family in {
        "plains v2 (new)",
        "snow (new)",
        "mountain stone (new)",
        "dungeon (new)",
        "farming (new)",
        "blends (new)",
        "gradient blends - tiles",
        "new variants - tiles",
    }:
        return "runtime_core"
    if source_type == "pixellab_tilesets" and family in {"plains", "snow", "mountain_stone", "dungeon_stone", "beach"}:
        return "runtime_core"
    if source_type == "pixellab_props" and family in {"forest", "plains", "ores", "ambient", "lights"}:
        return "runtime_core"
    if re.search(r"^(tavern|guild_hall|library|shop)_r[123]$", stable_id):
        return "building_registry"
    if source_type == "pixellab_props" and family in {"stations", "camp", "town", "guild", "library", "tavern", "chest"}:
        return "extended_catalog"
    return "review_only"


def stage_copy(row: dict[str, Any], out_dir: Path) -> str:
    family = safe_name(row.get("family") or "misc")
    stable_id = safe_name(row["stable_id"])
    target = out_dir / "selected_pngs" / row["kind"] / family / f"{stable_id}.png"
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source_path(row), target)
    return rel(target)


def select_rows(candidates: list[dict[str, Any]]) -> tuple[list[dict[str, Any]], list[dict[str, Any]]]:
    grouped: dict[tuple[str, str, str], list[dict[str, Any]]] = defaultdict(list)
    skipped: list[dict[str, Any]] = []
    for row in candidates:
        if should_skip(row):
            skipped.append(row)
            continue
        group_key = row["family"] if row["source_type"] == "pixellab_props" else ""
        grouped[(row["kind"], row["stable_id"], group_key)].append(row)

    selected: list[dict[str, Any]] = []
    for (kind, stable_id, family_key), rows in sorted(grouped.items()):
        scored = sorted(((quality_score(row), row) for row in rows), key=lambda pair: pair[0], reverse=True)
        best_score, best = scored[0]
        selected.append({
            "kind": kind,
            "stable_id": stable_id,
            "source_type": best["source_type"],
            "family": best["family"],
            "source_path": best["source_path"],
            "score": best_score,
            "alternative_count": len(rows),
            "review_status": best["review_status"],
            "manual_review_required": best["review_status"] != "candidate" or bool(best.get("flags")),
            "flags": best.get("flags", []),
            "worldgen_status": best.get("worldgen_status"),
            "worldgen_role": best.get("worldgen_role"),
            "metrics": best["metrics"],
            "scale": best["scale"],
            "destinations": destination_for(best),
            "selection_tier": selection_tier(best),
            "alternatives": [
                {
                    "source_path": row["source_path"],
                    "source_type": row["source_type"],
                    "family": row["family"],
                    "score": score,
                    "review_status": row["review_status"],
                    "flags": row.get("flags", []),
                }
                for score, row in scored
            ],
        })
    return selected, skipped


def make_contact_sheet(rows: list[dict[str, Any]], out_path: Path, title: str) -> None:
    if not rows:
        return
    thumb_w, thumb_h = 96, 96
    cell_w, cell_h = 178, 146
    cols = min(6, len(rows))
    grid_rows = math.ceil(len(rows) / cols)
    sheet = Image.new("RGBA", (cols * cell_w, grid_rows * cell_h + 28), (34, 37, 46, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((8, 7), title, fill=(245, 229, 190, 255))
    border = {
        False: (72, 170, 100, 255),
        True: (225, 170, 60, 255),
    }
    for index, row in enumerate(rows):
        image = Image.open(source_path(row)).convert("RGBA")
        x = (index % cols) * cell_w
        y = (index // cols) * cell_h + 28
        draw.rectangle((x + 3, y + 3, x + cell_w - 4, y + cell_h - 4),
                       outline=border[row["manual_review_required"]], width=2)
        scale = min(thumb_w / image.width, thumb_h / image.height, 4.0)
        size = (max(1, round(image.width * scale)), max(1, round(image.height * scale)))
        resized = image.resize(size, Image.Resampling.NEAREST)
        sheet.paste(resized, (x + (cell_w - size[0]) // 2, y + 8), resized)
        label = row["stable_id"][:24]
        sub = f"{row['source_type']} alt {row['alternative_count']}"
        ppu = row["scale"].get("ppu")
        fp = row["scale"].get("footprint")
        draw.text((x + 8, y + 106), label, fill=(245, 229, 190, 255))
        draw.text((x + 8, y + 119), sub[:30], fill=(190, 202, 220, 255))
        draw.text((x + 8, y + 132), f"ppu {ppu} {fp}"[:30], fill=(150, 160, 176, 255))
    out_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out_path)


def write_markdown(plan: dict[str, Any], path: Path) -> None:
    summary = plan["summary"]
    lines = [
        "# Art Promotion Selection Plan",
        "",
        "Dry-run only. Selected PNGs are staged under the configured worldgen review root; no runtime art paths were modified.",
        "",
        "## Summary",
        "",
        f"- Selected: {summary['selected_total']}",
        f"- Tiles: {summary['selected_tiles']}",
        f"- Props: {summary['selected_props']}",
        f"- Manual review required: {summary['manual_review_required']}",
        f"- Skipped: {summary['skipped']}",
        f"- By source: `{summary['by_source']}`",
        f"- By tier: `{summary['by_tier']}`",
        "",
        "## Rules",
        "",
        "- One selected row per stable id/family candidate group.",
        "- PixelLab `frame_0..3` alternatives remain alternatives; only one is staged per stable id.",
        "- `v2 size compare` aliases and `_sm`/`_lg` review ids are skipped.",
        "- Runtime destinations are proposals only until art-lane promotion happens.",
        "",
        "## Manual Review Required",
        "",
    ]
    manual = [r for r in plan["selected"] if r["manual_review_required"]]
    if not manual:
        lines.append("- None")
    for row in manual:
        lines.append(f"- `{row['stable_id']}` [{row['kind']}/{row['family']}]: flags `{', '.join(row['flags'])}`")

    lines.extend(["", "## Selected Contact Sheets", ""])
    for sheet in plan["contact_sheets"]:
        lines.append(f"- `{sheet}`")

    lines.extend(["", "## Selected Runtime Proposals", ""])
    for row in plan["selected"][:260]:
        lines.append(
            f"- `{row['stable_id']}` [{row['selection_tier']}] -> `{row['destinations']['runtime']}` "
            f"from `{row['source_path']}`"
        )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--review", type=Path, default=DEFAULT_REVIEW)
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    parser.add_argument("--stage-copies", action="store_true", default=True)
    args = parser.parse_args()

    review = load_json(args.review)
    selected, skipped = select_rows(review["candidates"])
    args.out.mkdir(parents=True, exist_ok=True)

    for row in selected:
        row["staged_path"] = stage_copy(row, args.out) if args.stage_copies else None

    contact_sheets: list[str] = []
    by_kind = defaultdict(list)
    by_tier = defaultdict(list)
    for row in selected:
        by_kind[row["kind"]].append(row)
        by_tier[row["selection_tier"]].append(row)
    for kind, rows in sorted(by_kind.items()):
        out_path = args.out / "contact_sheets" / f"selected_{kind}s.png"
        make_contact_sheet(rows, out_path, f"Selected {kind}s")
        contact_sheets.append(rel(out_path))
    for tier, rows in sorted(by_tier.items()):
        out_path = args.out / "contact_sheets" / f"selected_tier_{safe_name(tier)}.png"
        make_contact_sheet(rows, out_path, f"Selected {tier}")
        contact_sheets.append(rel(out_path))

    by_source = Counter(row["source_type"] for row in selected)
    by_tier = Counter(row["selection_tier"] for row in selected)
    plan = {
        "schema": "litiso.art_promotion_selection.v1",
        "review_manifest": rel(args.review),
        "summary": {
            "selected_total": len(selected),
            "selected_tiles": sum(1 for row in selected if row["kind"] == "tile"),
            "selected_props": sum(1 for row in selected if row["kind"] == "prop"),
            "manual_review_required": sum(1 for row in selected if row["manual_review_required"]),
            "skipped": len(skipped),
            "by_source": dict(sorted(by_source.items())),
            "by_tier": dict(sorted(by_tier.items())),
        },
        "contact_sheets": contact_sheets,
        "selected": selected,
        "skipped": [
            {
                "kind": row["kind"],
                "stable_id": row["stable_id"],
                "source_type": row["source_type"],
                "family": row["family"],
                "source_path": row["source_path"],
                "review_status": row["review_status"],
                "flags": row.get("flags", []),
            }
            for row in skipped
        ],
        "apply_policy": "review_only",
        "apply_policy_note": "Do not copy into Assets/Resources without owner/art-lane approval.",
    }
    manifest = args.out / "art_promotion_selection_manifest.json"
    markdown = args.out / "art_promotion_selection_summary.md"
    manifest.write_text(json.dumps(plan, indent=2), encoding="utf-8")
    write_markdown(plan, markdown)
    print(json.dumps({
        "manifest": rel(manifest),
        "summary": rel(markdown),
        "selected": plan["summary"]["selected_total"],
        "manual_review_required": plan["summary"]["manual_review_required"],
        "contact_sheets": contact_sheets,
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
