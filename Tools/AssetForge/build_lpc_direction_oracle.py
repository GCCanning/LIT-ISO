#!/usr/bin/env python3
"""
Build a known-good LPC cardinal direction reference sheet.

This is a direction oracle, not final art. It exists to prevent bad generated
frames from poisoning direction training when the model fails to face N/E/S/W.
"""

from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageDraw

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")

CARDINALS = ["south", "east", "north", "west"]
CANONICAL_LITISO_8D = ["south", "south-east", "east", "north-east", "north", "north-west", "west", "south-west"]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def load_metadata(dataset: Path) -> list[dict]:
    metadata_path = dataset / "metadata.jsonl"
    if not metadata_path.exists():
        raise FileNotFoundError(f"Missing metadata: {metadata_path}")
    rows = []
    with metadata_path.open("r", encoding="utf-8-sig") as handle:
        for line in handle:
            line = line.strip()
            if line:
                rows.append(json.loads(line))
    return rows


def select_record(rows: list[dict], character: str, action: str, tool: str, direction: str, frame_index: int) -> dict | None:
    exact = [
        row
        for row in rows
        if row.get("character") == character
        and row.get("action") == action
        and row.get("tool") == tool
        and row.get("direction") == direction
        and int(row.get("frame_index", -1)) == frame_index
    ]
    if exact:
        return exact[0]
    fallback = [
        row
        for row in rows
        if row.get("character") == character
        and row.get("action") == action
        and row.get("tool") == tool
        and row.get("direction") == direction
    ]
    if not fallback:
        return None
    return sorted(fallback, key=lambda row: abs(int(row.get("frame_index", 0)) - frame_index))[0]


def draw_oracle(dataset: Path, out_dir: Path, selections: list[dict]) -> Path:
    scale = 3
    cell = 64 * scale
    label_h = 36
    row_label_w = 220
    title_h = 34
    header_h = 76
    rows = sorted(set(item["row_label"] for item in selections))
    width = row_label_w + len(CARDINALS) * cell
    height = header_h + len(rows) * (cell + label_h)
    sheet = Image.new("RGBA", (width, height), (18, 21, 28, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((10, 10), "Known-good LPC cardinal direction oracle (not final LIT-ISO art)", fill=(235, 240, 245, 255))
    for col, direction in enumerate(CARDINALS):
        x = row_label_w + col * cell
        draw.text((x + 12, title_h + 12), direction.upper(), fill=(235, 240, 245, 255))

    for row_index, row_label in enumerate(rows):
        y = header_h + row_index * (cell + label_h)
        draw.text((10, y + 12), row_label, fill=(235, 240, 245, 255))
        for col, direction in enumerate(CARDINALS):
            match = next((item for item in selections if item["row_label"] == row_label and item["direction"] == direction), None)
            x = row_label_w + col * cell
            if not match:
                draw.rectangle((x, y, x + cell - 1, y + cell - 1), outline=(160, 64, 64, 255))
                draw.text((x + 10, y + 84), "missing", fill=(255, 170, 170, 255))
                continue
            image = Image.open(dataset / match["file_name"]).convert("RGBA")
            preview = image.resize((cell, cell), Image.Resampling.NEAREST)
            bg = Image.new("RGBA", (cell, cell), (238, 238, 238, 255))
            bg.alpha_composite(preview)
            sheet.alpha_composite(bg, (x, y))
            draw.rectangle((x, y, x + cell - 1, y + cell - 1), outline=(65, 70, 80, 255))
            draw.text((x + 6, y + cell + 6), Path(match["file_name"]).stem[-28:], fill=(210, 220, 230, 255))

    out = out_dir / "lpc_cardinal_direction_oracle.png"
    sheet.save(out)
    return out


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a known-good LPC direction reference sheet.")
    parser.add_argument("--dataset", type=Path, default=Path(r"C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\lpc_motion_male_female_v1"))
    parser.add_argument("--out-dir", type=Path, default=Path(r"C:\Projects\Pixel Pipeline\generated\lpc_direction_oracle_v1"))
    parser.add_argument("--frame-index", type=int, default=3)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    args.out_dir.mkdir(parents=True, exist_ok=True)
    rows = load_metadata(args.dataset)
    specs = [
        ("male_leather_adventurer", "walk", "none", "male walk none"),
        ("female_forest_scout", "walk", "none", "female walk none"),
        ("male_leather_adventurer", "slash", "longsword_slash", "male slash longsword"),
        ("female_forest_scout", "thrust", "axe", "female thrust axe"),
    ]
    selections = []
    missing = []
    for character, action, tool, row_label in specs:
        for direction in CARDINALS:
            record = select_record(rows, character, action, tool, direction, args.frame_index)
            if not record:
                missing.append({"character": character, "action": action, "tool": tool, "direction": direction})
                continue
            selected = dict(record)
            selected["row_label"] = row_label
            selections.append(selected)

    contact = draw_oracle(args.dataset, args.out_dir, selections)
    manifest = {
        "schemaVersion": 1,
        "generated_utc": utc_now(),
        "status": "cardinal_oracle_ready",
        "purpose": "Known-good N/E/S/W direction reference for evaluating generated character direction control.",
        "dataset": str(args.dataset),
        "out_dir": str(args.out_dir),
        "contact_sheet": str(contact),
        "canonical_litiso_direction_order_8d": CANONICAL_LITISO_8D,
        "available_directions": CARDINALS,
        "missing_directions": ["south-east", "north-east", "north-west", "south-west"],
        "missing_direction_policy": "Do not train or approve diagonal labels until actual diagonal source frames exist or are manually authored and reviewed.",
        "selected_records": selections,
        "missing_records": missing,
        "direction_contract": {
            "south": "front-facing / body and face toward camera",
            "north": "back-facing / face hidden or mostly hidden",
            "east": "profile facing screen-right",
            "west": "profile facing screen-left",
            "diagonals": "must be distinct 3/4 facings, not mirrored/cardinal relabels unless explicitly approved",
        },
    }
    (args.out_dir / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps({"out_dir": str(args.out_dir), "contact_sheet": str(contact), "missing": len(missing)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
