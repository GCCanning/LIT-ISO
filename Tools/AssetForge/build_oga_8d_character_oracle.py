#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageDraw

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")

FILENAME_RE = re.compile(r"^(?P<index>\d+)_(?P<action>[^_]+)_CAM(?P<cam>\d+)_(?P<frame>\d+)\.png$", re.IGNORECASE)
CANONICAL_LITISO_8D = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"]
CAM_TO_LITISO = {
    7: "S",
    6: "SE",
    5: "E",
    4: "NE",
    3: "N",
    2: "NW",
    1: "W",
    0: "SW",
}
LITISO_TO_CAM = {direction: cam for cam, direction in CAM_TO_LITISO.items()}


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def parse_frames(folder: Path) -> list[dict]:
    frames = []
    for path in sorted(folder.glob("*.png")):
        match = FILENAME_RE.match(path.name)
        if not match:
            continue
        frames.append(
            {
                "path": path,
                "index": int(match.group("index")),
                "action": match.group("action"),
                "cam": int(match.group("cam")),
                "frame": int(match.group("frame")),
            }
        )
    return frames


def choose_frame(frames: list[dict], action: str, cam: int) -> dict | None:
    candidates = [f for f in frames if f["action"].lower() == action.lower() and f["cam"] == cam]
    if not candidates:
        return None
    candidates = sorted(candidates, key=lambda f: f["frame"])
    return candidates[len(candidates) // 2]


def draw_cam_oracle(folder: Path, frames: list[dict], out_dir: Path, actions: list[str]) -> Path:
    scale = 2
    source_cell_w = 148
    source_cell_h = 130
    cell_w = source_cell_w * scale
    cell_h = source_cell_h * scale
    row_label_w = 160
    header_h = 72
    label_h = 28
    width = row_label_w + 8 * cell_w
    height = header_h + len(actions) * (cell_h + label_h)
    sheet = Image.new("RGBA", (width, height), (16, 18, 24, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((10, 10), f"OGA 8D camera oracle: {folder.name}", fill=(235, 240, 245, 255))
    draw.text((10, 34), "Map CAM labels to LIT-ISO directions by visual QC; do not assume yet.", fill=(190, 200, 215, 255))

    for cam in range(8):
        x = row_label_w + cam * cell_w
        draw.text((x + 8, 48), f"CAM{cam}", fill=(235, 240, 245, 255))

    for row, action in enumerate(actions):
        y = header_h + row * (cell_h + label_h)
        draw.text((10, y + 10), action, fill=(235, 240, 245, 255))
        for cam in range(8):
            x = row_label_w + cam * cell_w
            record = choose_frame(frames, action, cam)
            if not record:
                draw.rectangle((x, y, x + cell_w - 1, y + cell_h - 1), outline=(150, 60, 60, 255))
                draw.text((x + 10, y + 110), "missing", fill=(255, 170, 170, 255))
                continue
            image = Image.open(record["path"]).convert("RGBA")
            preview = image.resize((source_cell_w * scale, source_cell_h * scale), Image.Resampling.NEAREST)
            bg = Image.new("RGBA", preview.size, (238, 238, 238, 255))
            bg.alpha_composite(preview)
            sheet.alpha_composite(bg, (x, y))
            draw.rectangle((x, y, x + cell_w - 1, y + cell_h - 1), outline=(66, 74, 90, 255))
            draw.text((x + 6, y + cell_h + 5), record["path"].stem[-22:], fill=(210, 220, 230, 255))

    out_dir.mkdir(parents=True, exist_ok=True)
    out = out_dir / f"{folder.name}_cam_oracle.png"
    sheet.save(out)
    return out


def draw_litiso_oracle(folder: Path, frames: list[dict], out_dir: Path, actions: list[str]) -> Path:
    scale = 2
    source_cell_w = 148
    source_cell_h = 130
    cell_w = source_cell_w * scale
    cell_h = source_cell_h * scale
    row_label_w = 160
    header_h = 72
    label_h = 28
    width = row_label_w + len(CANONICAL_LITISO_8D) * cell_w
    height = header_h + len(actions) * (cell_h + label_h)
    sheet = Image.new("RGBA", (width, height), (16, 18, 24, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((10, 10), f"OGA 8D LIT-ISO ordered oracle: {folder.name}", fill=(235, 240, 245, 255))
    draw.text((10, 34), "Canonical order: S, SE, E, NE, N, NW, W, SW", fill=(190, 200, 215, 255))

    for col, direction in enumerate(CANONICAL_LITISO_8D):
        x = row_label_w + col * cell_w
        cam = LITISO_TO_CAM[direction]
        draw.text((x + 8, 48), f"{direction} / CAM{cam}", fill=(235, 240, 245, 255))

    for row, action in enumerate(actions):
        y = header_h + row * (cell_h + label_h)
        draw.text((10, y + 10), action, fill=(235, 240, 245, 255))
        for col, direction in enumerate(CANONICAL_LITISO_8D):
            cam = LITISO_TO_CAM[direction]
            x = row_label_w + col * cell_w
            record = choose_frame(frames, action, cam)
            if not record:
                draw.rectangle((x, y, x + cell_w - 1, y + cell_h - 1), outline=(150, 60, 60, 255))
                draw.text((x + 10, y + 110), "missing", fill=(255, 170, 170, 255))
                continue
            image = Image.open(record["path"]).convert("RGBA")
            preview = image.resize((source_cell_w * scale, source_cell_h * scale), Image.Resampling.NEAREST)
            bg = Image.new("RGBA", preview.size, (238, 238, 238, 255))
            bg.alpha_composite(preview)
            sheet.alpha_composite(bg, (x, y))
            draw.rectangle((x, y, x + cell_w - 1, y + cell_h - 1), outline=(66, 74, 90, 255))
            draw.text((x + 6, y + cell_h + 5), record["path"].stem[-22:], fill=(210, 220, 230, 255))

    out_dir.mkdir(parents=True, exist_ok=True)
    out = out_dir / f"{folder.name}_litiso_8d_oracle.png"
    sheet.save(out)
    return out


def summarize(root: Path) -> dict:
    folder_count = 0
    png_count = 0
    action_counts: Counter[str] = Counter()
    cam_counts: Counter[int] = Counter()
    folders_by_part: dict[str, int] = defaultdict(int)
    sample_folders = []
    for folder in root.glob("part-*/*"):
        if not folder.is_dir():
            continue
        folder_count += 1
        folders_by_part[folder.parent.name] += 1
        if len(sample_folders) < 40:
            sample_folders.append(str(folder))
        for path in folder.glob("*.png"):
            png_count += 1
            match = FILENAME_RE.match(path.name)
            if match:
                action_counts[match.group("action")] += 1
                cam_counts[int(match.group("cam"))] += 1
    return {
        "folder_count": folder_count,
        "png_count": png_count,
        "folders_by_part": dict(sorted(folders_by_part.items())),
        "action_counts": dict(action_counts.most_common()),
        "cam_counts": dict(sorted(cam_counts.items())),
        "sample_folders": sample_folders,
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path(r"C:\Projects\Pixel Pipeline\sources\opengameart\400-items-basehumanmale-orc-skeleton\extracted"))
    parser.add_argument("--folder", default="part-1/BaseHumanMale")
    parser.add_argument("--out-dir", type=Path, default=Path(r"C:\Projects\Pixel Pipeline\generated\oga_8d_character_oracle_v1"))
    parser.add_argument("--actions", nargs="+", default=["Idle", "Attack", "Bow", "Cast", "Walk", "Run", "Death"])
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    folder = args.root / args.folder
    if not folder.exists():
        raise FileNotFoundError(f"Missing oracle folder: {folder}")
    frames = parse_frames(folder)
    if not frames:
        raise RuntimeError(f"No parseable frames found in {folder}")
    contact = draw_cam_oracle(folder, frames, args.out_dir, args.actions)
    litiso_contact = draw_litiso_oracle(folder, frames, args.out_dir, args.actions)
    manifest = {
        "schemaVersion": 1,
        "generated_utc": utc_now(),
        "status": "litiso_8d_oracle_ready",
        "source_page": "https://opengameart.org/content/400-items-basehumanmale-orc-skeleton",
        "license": "CC-BY 4.0",
        "root": str(args.root),
        "oracle_folder": str(folder),
        "contact_sheet": str(contact),
        "litiso_ordered_contact_sheet": str(litiso_contact),
        "canonical_litiso_8d": CANONICAL_LITISO_8D,
        "camera_labels": [f"CAM{i}" for i in range(8)],
        "direction_mapping": {
            "CAM7": "S",
            "CAM6": "SE",
            "CAM5": "E",
            "CAM4": "NE",
            "CAM3": "N",
            "CAM2": "NW",
            "CAM1": "W",
            "CAM0": "SW"
        },
        "actions_in_oracle": args.actions,
        "verified_action_names": ["Idle", "Attack", "Bow", "Cast", "Walk", "Run", "Death"],
        "missing_expected_actions": ["Hurt/Pain"],
        "summary": summarize(args.root),
    }
    (args.out_dir / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps({"out_dir": str(args.out_dir), "contact_sheet": str(contact), "litiso_ordered_contact_sheet": str(litiso_contact), "status": manifest["status"]}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
