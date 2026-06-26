#!/usr/bin/env python3
"""
Audit PixelLab building rank candidates for scale/crop/door alignment risk.

This does not attempt computer vision door detection. It emits the fields needed
for human review and future annotated door metadata:

- frame count and missing frames
- bbox margins and canvas-edge contact
- proposed footprint/PPU/world size
- visual center offset from bottom-center anchor
- lowest opaque span as a rough ground-contact indicator
- rank-to-rank consistency warnings
"""

from __future__ import annotations

import argparse
import os
import json
import math
import re
from collections import defaultdict
from pathlib import Path
from typing import Any

try:
    from PIL import Image, ImageDraw
except ImportError as exc:
    raise SystemExit("Pillow is required. Use the ComfyUI venv Python on this machine.") from exc


ROOT = Path(__file__).resolve().parents[2]
REVIEW_ROOT = Path(os.environ.get("LITISO_WORLDGEN_REVIEW_ROOT", r"C:\tmp\LitIsoWorldGen"))
DEFAULT_REVIEW = REVIEW_ROOT / "art_review_pack" / "art_review_manifest.json"
DEFAULT_OUT = REVIEW_ROOT / "building_rank_alignment"
EXPECTED_FRAMES = 4


def rel(path: Path) -> str:
    try:
        return path.relative_to(ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def source_path(row: dict[str, Any]) -> Path:
    p = Path(row["source_path"])
    return p if p.is_absolute() else ROOT / p


def rank_for(stable_id: str) -> int | None:
    match = re.search(r"_r([123])$", stable_id)
    return int(match.group(1)) if match else None


def building_family(stable_id: str) -> str:
    return re.sub(r"_r[123]$", "", stable_id)


def lowest_opaque_span(path: Path) -> dict[str, int | None]:
    img = Image.open(path).convert("RGBA")
    px = img.load()
    for y in range(img.height - 1, -1, -1):
        xs = [x for x in range(img.width) if px[x, y][3] > 0]
        if xs:
            return {"y": y, "x_min": min(xs), "x_max": max(xs), "width": max(xs) - min(xs) + 1}
    return {"y": None, "x_min": None, "x_max": None, "width": None}


def frame_index(path: str) -> int | None:
    match = re.search(r"frame_(\d+)\.png$", path.replace("\\", "/"))
    return int(match.group(1)) if match else None


def audit_frame(row: dict[str, Any]) -> dict[str, Any]:
    metrics = row["metrics"]
    bbox = metrics["bbox"]
    width, height = metrics["width"], metrics["height"]
    left = bbox[0]
    top = bbox[1]
    right = width - (bbox[0] + bbox[2])
    bottom = height - (bbox[1] + bbox[3])
    center_x = bbox[0] + bbox[2] / 2.0
    anchor_x = width / 2.0
    anchor_y = height - 1
    span = lowest_opaque_span(source_path(row))
    rank = rank_for(row["stable_id"]) or 1
    footprint = {1: "2x2", 2: "3x3", 3: "4x4"}[rank]
    ppu = row["scale"]["ppu"]
    world_w = round(width / ppu, 3)
    world_h = round(height / ppu, 3)
    risks: list[str] = []
    if top <= 0:
        risks.append("touches_top_edge")
    if left <= 1 or right <= 1:
        risks.append("touches_side_edge")
    if bottom <= 0:
        risks.append("touches_bottom_edge")
    if abs(center_x - anchor_x) > width * 0.12:
        risks.append("visual_center_offset")
    if span["width"] and span["width"] > width * 0.70:
        risks.append("wide_ground_contact")
    return {
        "stable_id": row["stable_id"],
        "frame_index": frame_index(row["source_path"]),
        "source_path": row["source_path"],
        "canvas_size": [width, height],
        "bbox": bbox,
        "bbox_margin": {"left": left, "top": top, "right": right, "bottom": bottom},
        "touches_canvas_edge": top <= 0 or left <= 0 or right <= 0 or bottom <= 0,
        "inferred_footprint": footprint,
        "ppu": ppu,
        "world_size": [world_w, world_h],
        "anchor_pixel": [anchor_x, anchor_y],
        "visual_center_offset_from_anchor": round(center_x - anchor_x, 2),
        "ground_contact_pixels": span,
        "scale_risk": "review" if world_h > 4.2 or world_w > 3.8 else "ok",
        "crop_risk": "review" if any(r in risks for r in ("touches_top_edge", "touches_side_edge")) else "ok",
        "door_alignment_risk": "unknown_requires_manual_or_annotation",
        "risk_reasons": risks,
    }


def summarize_building(rows: list[dict[str, Any]]) -> dict[str, Any]:
    frames = [audit_frame(row) for row in rows]
    indexes = sorted(i for i in (f["frame_index"] for f in frames) if i is not None)
    missing = [i for i in range(EXPECTED_FRAMES) if i not in indexes]
    risks = sorted({reason for frame in frames for reason in frame["risk_reasons"]})
    selected = sorted(frames, key=lambda f: (
        len(f["risk_reasons"]),
        abs(float(f["visual_center_offset_from_anchor"])),
        f["frame_index"] if f["frame_index"] is not None else 99,
    ))[0] if frames else None
    return {
        "stable_id": rows[0]["stable_id"] if rows else "",
        "family": building_family(rows[0]["stable_id"]) if rows else "",
        "rank": rank_for(rows[0]["stable_id"]) if rows else None,
        "frame_count": len(frames),
        "expected_frame_count": EXPECTED_FRAMES,
        "missing_frames": missing,
        "risk_reasons": risks,
        "selected_lowest_risk_frame": selected["frame_index"] if selected else None,
        "selected_lowest_risk_source": selected["source_path"] if selected else None,
        "frames": frames,
    }


def make_contact_sheet(buildings: list[dict[str, Any]], out_path: Path) -> None:
    flat = [frame for building in buildings for frame in building["frames"]]
    if not flat:
        return
    cell_w, cell_h = 190, 152
    thumb_w, thumb_h = 104, 104
    cols = 4
    rows = math.ceil(len(flat) / cols)
    sheet = Image.new("RGBA", (cols * cell_w, rows * cell_h + 28), (34, 37, 46, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((8, 7), "Building rank alignment audit", fill=(245, 229, 190, 255))
    for index, frame in enumerate(flat):
        path = source_path(frame)
        img = Image.open(path).convert("RGBA")
        x = (index % cols) * cell_w
        y = (index // cols) * cell_h + 28
        risk = frame["crop_risk"] != "ok" or frame["scale_risk"] != "ok"
        color = (225, 170, 60, 255) if risk else (72, 170, 100, 255)
        draw.rectangle((x + 3, y + 3, x + cell_w - 4, y + cell_h - 4), outline=color, width=2)
        scale = min(thumb_w / img.width, thumb_h / img.height, 4.0)
        size = (max(1, round(img.width * scale)), max(1, round(img.height * scale)))
        resized = img.resize(size, Image.Resampling.NEAREST)
        sheet.paste(resized, (x + (cell_w - size[0]) // 2, y + 8), resized)
        label = f"{frame['stable_id']} f{frame['frame_index']}"
        sub = f"{frame['inferred_footprint']} ppu {frame['ppu']} off {frame['visual_center_offset_from_anchor']}"
        risks = ",".join(frame["risk_reasons"][:2]) or "ok"
        draw.text((x + 8, y + 112), label[:28], fill=(245, 229, 190, 255))
        draw.text((x + 8, y + 126), sub[:30], fill=(190, 202, 220, 255))
        draw.text((x + 8, y + 140), risks[:30], fill=(150, 160, 176, 255))
    out_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out_path)


def write_markdown(report: dict[str, Any], path: Path) -> None:
    lines = [
        "# Building Rank Alignment Audit",
        "",
        "Dry-run metadata for r1/r2/r3 building sets. Door detection is not automated yet.",
        "",
        "## Summary",
        "",
        f"- Building ids: {report['summary']['building_ids']}",
        f"- Complete ids: {report['summary']['complete_ids']}",
        f"- Incomplete ids: {report['summary']['incomplete_ids']}",
        f"- Risk ids: {report['summary']['risk_ids']}",
        f"- Contact sheet: `{report['contact_sheet']}`",
        "",
        "## Building Sets",
        "",
    ]
    for building in report["buildings"]:
        lines.append(
            f"- `{building['stable_id']}`: frames {building['frame_count']}/"
            f"{building['expected_frame_count']}, missing `{building['missing_frames']}`, "
            f"risk `{', '.join(building['risk_reasons']) or 'ok'}`, "
            f"lowest-risk frame `{building['selected_lowest_risk_frame']}`"
        )
    lines.extend([
        "",
        "## Required Manual Gate",
        "",
        "- Annotate or approve one entrance cell per rank.",
        "- Verify r1/r2/r3 door cell does not move for each building family.",
        "- Confirm footprint occupancy before runtime placement uses buildings.",
    ])
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--review", type=Path, default=DEFAULT_REVIEW)
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    args = parser.parse_args()
    review = load_json(args.review)
    grouped: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for row in review["candidates"]:
        if row.get("source_type") != "pixellab_props":
            continue
        if row.get("family") != "buildings":
            continue
        if rank_for(row.get("stable_id", "")) is None:
            continue
        grouped[row["stable_id"]].append(row)

    buildings = [summarize_building(rows) for _, rows in sorted(grouped.items())]
    contact = args.out / "building_rank_alignment_contact_sheet.png"
    make_contact_sheet(buildings, contact)
    summary = {
        "building_ids": len(buildings),
        "complete_ids": sum(1 for b in buildings if not b["missing_frames"] and b["frame_count"] == EXPECTED_FRAMES),
        "incomplete_ids": sum(1 for b in buildings if b["missing_frames"] or b["frame_count"] != EXPECTED_FRAMES),
        "risk_ids": sum(1 for b in buildings if b["risk_reasons"]),
    }
    report = {
        "schema": "litiso.building_rank_alignment_audit.v1",
        "review_manifest": rel(args.review),
        "contact_sheet": rel(contact),
        "summary": summary,
        "buildings": buildings,
    }
    args.out.mkdir(parents=True, exist_ok=True)
    manifest = args.out / "building_rank_alignment_manifest.json"
    markdown = args.out / "building_rank_alignment_summary.md"
    manifest.write_text(json.dumps(report, indent=2), encoding="utf-8")
    write_markdown(report, markdown)
    print(json.dumps({
        "manifest": rel(manifest),
        "summary": rel(markdown),
        "contact_sheet": rel(contact),
        "summary_counts": summary,
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
