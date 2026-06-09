#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from PIL import Image

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


def repo_path(project_root: Path, value: str) -> Path:
    path = Path(value.replace("/", "\\"))
    return path if path.is_absolute() else project_root / path


def load_review_image(project_root: Path, job_name: str) -> Path:
    manifest_path = project_root / "Assets" / "Generated" / "_Review" / job_name / "generation_manifest.json"
    if not manifest_path.exists():
        raise FileNotFoundError(f"Missing review manifest for {job_name}: {manifest_path}")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    generated = manifest.get("generated_files") or []
    if not generated:
        raise ValueError(f"No generated_files in {manifest_path}")
    return repo_path(project_root, generated[0])


def parse_job(value: str) -> tuple[str, str]:
    if "=" in value:
        direction, job_name = value.split("=", 1)
        return direction.strip(), job_name.strip()
    return "", value.strip()


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--job", action="append", required=True, help="Direction=JobName")
    parser.add_argument("--cell-width", type=int, default=128)
    parser.add_argument("--cell-height", type=int, default=128)
    parser.add_argument("--layout", default="row")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    entries = [parse_job(value) for value in args.job]
    width = args.cell_width * len(entries)
    height = args.cell_height
    sheet = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    frames = []
    for index, (direction, job_name) in enumerate(entries):
        image_path = load_review_image(project_root, job_name)
        with Image.open(image_path) as source:
            image = source.convert("RGBA")
        if image.size != (args.cell_width, args.cell_height):
            cell = Image.new("RGBA", (args.cell_width, args.cell_height), (0, 0, 0, 0))
            image.thumbnail((args.cell_width, args.cell_height), Image.Resampling.NEAREST)
            cell.alpha_composite(image, ((args.cell_width - image.width) // 2, args.cell_height - image.height))
            image = cell
        x = index * args.cell_width
        sheet.alpha_composite(image, (x, 0))
        frames.append(
            {
                "index": index,
                "direction": direction,
                "job_name": job_name,
                "source_image": str(image_path),
                "rect": {"x": x, "y": 0, "width": args.cell_width, "height": args.cell_height},
                "pivot": {"x": 0.5, "y": 0.0},
            }
        )

    output = args.output if args.output.is_absolute() else project_root / args.output
    manifest_path = args.manifest if args.manifest.is_absolute() else project_root / args.manifest
    output.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output)
    payload = {
        "schema": "lit_iso.asset_forge.packed_spritesheet.v1",
        "sheet": str(output),
        "cell": {"width": args.cell_width, "height": args.cell_height},
        "directions": [entry[0] for entry in entries],
        "frames": frames,
        "unity": {
            "texture_type": "Sprite",
            "sprite_mode": "Multiple",
            "filter_mode": "Point",
            "mip_maps": False,
            "compression": "None",
            "ppu": 128,
        },
    }
    manifest_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    print(json.dumps({"ok": True, "sheet": str(output), "manifest": str(manifest_path), "frames": len(frames)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
