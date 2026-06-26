#!/usr/bin/env python3
"""Build a review contact sheet and manifest for the LIT-ISO character creator."""

from __future__ import annotations

import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


REPO = Path(__file__).resolve().parents[2]
CATALOG = REPO / "Assets" / "StreamingAssets" / "character_creator" / "appearance_catalog.json"
OUT = REPO / "Assets" / "Generated" / "_Review" / "character_creator_suite_v1"


def load_catalog() -> dict:
    return json.loads(CATALOG.read_text(encoding="utf-8"))


def source_png(resource: str) -> Path:
    return REPO / "Assets" / "Resources" / f"{resource}.png"


def first_row_preview(sheet: Image.Image, frames: int) -> Image.Image:
    cell_w = sheet.width // frames
    cell_h = sheet.height // 8
    preview = Image.new("RGBA", (cell_w * frames, cell_h), (0, 0, 0, 0))
    for frame in range(frames):
        src = sheet.crop((frame * cell_w, 0, (frame + 1) * cell_w, cell_h))
        preview.alpha_composite(src, (frame * cell_w, 0))
    return preview


def main() -> int:
    catalog = load_catalog()
    OUT.mkdir(parents=True, exist_ok=True)
    rows = []
    manifest = {
        "version": 1,
        "sourceCatalog": str(CATALOG.relative_to(REPO)).replace("\\", "/"),
        "output": str(OUT.relative_to(REPO)).replace("\\", "/"),
        "sheetContract": catalog["sheetContract"],
        "appearances": [],
    }

    for appearance in catalog["appearances"]:
        path = source_png(appearance["spriteResource"])
        if not path.exists():
            manifest["appearances"].append({**appearance, "status": "missing_source", "source": str(path)})
            continue
        sheet = Image.open(path).convert("RGBA")
        frames = int(appearance.get("framesPerRow", 4))
        preview = first_row_preview(sheet, frames)
        scale = 2
        preview = preview.resize((preview.width * scale, preview.height * scale), Image.Resampling.NEAREST)
        rows.append((appearance, preview))
        manifest["appearances"].append({
            **appearance,
            "status": "review_ready",
            "source": str(path.relative_to(REPO)).replace("\\", "/"),
            "width": sheet.width,
            "height": sheet.height,
        })

    if rows:
        label_w = 210
        row_h = max(96, max(img.height for _, img in rows) + 22)
        sheet_w = label_w + max(img.width for _, img in rows) + 24
        sheet_h = row_h * len(rows)
        contact = Image.new("RGBA", (sheet_w, sheet_h), (22, 24, 29, 255))
        draw = ImageDraw.Draw(contact)
        font = ImageFont.load_default()
        for idx, (appearance, preview) in enumerate(rows):
            y = idx * row_h
            draw.rectangle((0, y, sheet_w, y + row_h - 1), outline=(58, 67, 82, 255))
            draw.text((12, y + 14), appearance["displayName"], fill=(240, 199, 109, 255), font=font)
            draw.text((12, y + 32), appearance["id"], fill=(180, 188, 200, 255), font=font)
            if appearance.get("prototypeOnly"):
                draw.text((12, y + 50), "prototype-only", fill=(255, 160, 120, 255), font=font)
            contact.alpha_composite(preview, (label_w, y + 10))
        contact.save(OUT / "character_creator_suite_contact_sheet.png")

    (OUT / "character_creator_suite_manifest.json").write_text(
        json.dumps(manifest, indent=2),
        encoding="utf-8",
    )
    print(f"Wrote {OUT / 'character_creator_suite_manifest.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
