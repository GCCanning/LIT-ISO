#!/usr/bin/env python3
"""
Apply Asset Forge deterministic character cleanup to LoRA eval outputs.

This is a review helper, not a generator. It lets us inspect whether a LoRA has
learned useful direction/body cues after the same local cleanup that production
character jobs use: edge background removal, foreground recovery, floor artifact
removal, nearest-neighbor normalization, and palette snap.
"""

from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageDraw

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from comfy_generation_worker import postprocess  # noqa: E402


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_contact_sheet(out_dir: Path, records: list[dict]) -> str | None:
    if not records:
        return None
    columns = 4
    cell = 160
    label_h = 30
    rows = (len(records) + columns - 1) // columns
    sheet = Image.new("RGBA", (columns * cell, rows * (cell + label_h)), (18, 21, 28, 255))
    draw = ImageDraw.Draw(sheet)
    for index, record in enumerate(records):
        image = Image.open(record["cleaned_output"]).convert("RGBA")
        preview = image.resize((128, 128), Image.Resampling.NEAREST)
        x = (index % columns) * cell
        y = (index // columns) * (cell + label_h)
        bg = Image.new("RGBA", (128, 128), (245, 246, 248, 255))
        bg.alpha_composite(preview)
        sheet.alpha_composite(bg, (x + 16, y + 10))
        draw.rectangle((x + 16, y + 10, x + 143, y + 137), outline=(65, 72, 84, 255))
        label = f"{record.get('direction', '?')} {Path(record['cleaned_output']).stem[:20]}"
        draw.text((x + 10, y + cell - 10), label, fill=(230, 235, 245, 255))
    path = out_dir / "cleaned_eval_contact.png"
    sheet.save(path)
    return str(path)


def main() -> int:
    parser = argparse.ArgumentParser(description="Clean LoRA eval PNGs with Asset Forge post-processing.")
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--out-dir", type=Path, default=None)
    parser.add_argument("--mode", default="character", choices=["character", "npc", "mob", "prop", "item", "tile"])
    args = parser.parse_args()

    manifest_path = args.manifest.resolve()
    manifest = load_json(manifest_path)
    out_dir = args.out_dir.resolve() if args.out_dir else manifest_path.parent / "cleaned"
    out_dir.mkdir(parents=True, exist_ok=True)

    cleaned_records = []
    for result in manifest.get("results", []):
        outputs = result.get("outputs", [])
        if not outputs:
            continue
        raw = Path(outputs[0])
        cleaned = out_dir / f"{raw.stem}_cleaned.png"
        size = postprocess(args.mode, raw, cleaned)
        cleaned_records.append(
            {
                "name": result.get("name", raw.stem),
                "direction": result.get("direction", ""),
                "raw_output": str(raw),
                "cleaned_output": str(cleaned),
                "size": list(size),
            }
        )

    cleaned_manifest = {
        "schema": "lit_iso.asset_forge.cleaned_lora_eval.v1",
        "created_utc": utc_now(),
        "source_manifest": str(manifest_path),
        "mode": args.mode,
        "records": cleaned_records,
        "contact_sheet": write_contact_sheet(out_dir, cleaned_records),
    }
    output_manifest = out_dir / "cleaned_manifest.json"
    output_manifest.write_text(json.dumps(cleaned_manifest, indent=2), encoding="utf-8")
    print(json.dumps({"cleaned": len(cleaned_records), "manifest": str(output_manifest), "contact_sheet": cleaned_manifest["contact_sheet"]}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
