#!/usr/bin/env python3
"""Build a compact review sheet from multiple SpriteForge lane-A candidates."""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


def load_label(candidate_root: Path) -> str:
    manifest_path = candidate_root / "witch" / "walk" / "S" / "lane_a_manifest.json"
    if not manifest_path.exists():
        return candidate_root.name
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    settings = manifest.get("worker_settings", {})
    return (
        f"{candidate_root.name}\n"
        f"d={settings.get('template_denoise')} c={settings.get('control_strength')}\n"
        f"pal={settings.get('palette_lock')}"
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a SpriteForge lane-A sweep review sheet.")
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--out", type=Path, default=None)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    root = args.root.resolve()
    candidates = []
    for candidate in sorted(root.iterdir()):
        preview = candidate / "witch" / "walk" / "S" / "preview_x4.png"
        report = candidate / "witch" / "walk" / "S" / "p2_gate_report.json"
        if preview.exists() and report.exists():
            candidates.append((candidate, preview))
    if not candidates:
        raise SystemExit(f"no candidate previews found under {root}")

    thumbs = []
    for candidate, preview_path in candidates:
        image = Image.open(preview_path).convert("RGBA")
        thumbs.append((candidate, image))

    max_w = max(image.width for _, image in thumbs)
    max_h = max(image.height for _, image in thumbs)
    label_h = 54
    cols = 1
    rows = len(thumbs)
    sheet = Image.new("RGBA", (cols * max_w, rows * (max_h + label_h)), (14, 14, 18, 255))
    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("arial.ttf", 14)
    except OSError:
        font = ImageFont.load_default()

    for row, (candidate, image) in enumerate(thumbs):
        y = row * (max_h + label_h)
        draw.rectangle((0, y, max_w - 1, y + max_h + label_h - 1), outline=(74, 80, 96, 255), width=1)
        sheet.alpha_composite(image, ((max_w - image.width) // 2, y))
        draw.text((8, y + max_h + 6), load_label(candidate), fill=(232, 236, 245, 255), font=font)

    out = args.out or root / "lane_a_sweep_contact_sheet.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out)
    print(json.dumps({"ok": True, "out": str(out), "candidates": len(candidates)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
