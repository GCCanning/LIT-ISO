#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def load_font(size: int) -> ImageFont.ImageFont:
    for name in ("segoeui.ttf", "arial.ttf", "calibri.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def repo_rel(project_root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(project_root.resolve()).as_posix()
    except ValueError:
        return str(path.resolve()).replace("\\", "/")


def safe_name(value: str) -> str:
    safe = "".join(ch if ch.isalnum() or ch in "._-" else "_" for ch in value).strip("._")
    return safe or "tile_candidate"


def normalize_to_32(path: Path, output_path: Path) -> dict[str, Any]:
    with Image.open(path) as source:
        image = source.convert("RGBA")
    alpha = image.split()[-1]
    bbox = alpha.getbbox()
    if bbox:
        cropped = image.crop(bbox)
    else:
        cropped = image
    cropped.thumbnail((32, 32), Image.Resampling.NEAREST)
    canvas = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    canvas.alpha_composite(cropped, ((32 - cropped.width) // 2, (32 - cropped.height) // 2))
    output_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output_path)
    return {
        "source_size": [image.width, image.height],
        "source_alpha_bbox": list(bbox) if bbox else None,
        "normalized_size": [32, 32],
    }


def checkerboard(size: tuple[int, int], cell: int = 8) -> Image.Image:
    image = Image.new("RGBA", size, (31, 33, 37, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], cell):
        for x in range(0, size[0], cell):
            if (x // cell + y // cell) % 2 == 0:
                draw.rectangle((x, y, x + cell - 1, y + cell - 1), fill=(42, 46, 51, 255))
    return image


def build_sheet(project_root: Path, selected: list[dict[str, Any]], output_path: Path) -> None:
    columns = max(1, len(selected))
    cell_w = 156
    cell_h = 142
    header_h = 58
    sheet = Image.new("RGBA", (columns * cell_w, header_h + cell_h), (18, 20, 23, 255))
    draw = ImageDraw.Draw(sheet)
    title_font = load_font(17)
    font = load_font(11)
    draw.text((12, 10), "Selected LIT-ISO 32x32 tile family candidates", fill=(238, 241, 234, 255), font=title_font)
    draw.text((12, 34), "Review-only. These are not approved Unity runtime assets.", fill=(176, 186, 176, 255), font=font)
    for index, item in enumerate(selected):
        x = index * cell_w
        y = header_h
        draw.rectangle((x, y, x + cell_w - 1, y + cell_h - 1), outline=(68, 76, 84, 255), fill=(27, 30, 34, 255))
        backing = checkerboard((96, 70), 8)
        sheet.alpha_composite(backing, (x + 30, y + 8))
        output = project_root / item["normalized_path"]
        if output.exists():
            with Image.open(output) as source:
                preview = source.convert("RGBA").resize((64, 64), Image.Resampling.NEAREST)
                sheet.alpha_composite(preview, (x + 46, y + 11))
        draw.text((x + 8, y + 82), str(item["family"])[:24], fill=(229, 234, 226, 255), font=font)
        draw.text((x + 8, y + 98), f"s{item['strength']} score {item['score']}", fill=(197, 205, 196, 255), font=font)
        draw.text((x + 8, y + 114), str(item["source_name"])[:24], fill=(155, 164, 160, 255), font=font)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)


def select_best(scores: list[dict[str, Any]], families: list[str]) -> list[dict[str, Any]]:
    selected = []
    for family in families:
        family_scores = [
            item for item in scores
            if item.get("exists") and item.get("style_target_family") == family and item.get("path")
        ]
        family_scores.sort(key=lambda item: float(item.get("score", 9999.0)))
        if family_scores:
            selected.append(family_scores[0])
    return selected


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Select one best 32x32 candidate per tile family from tile style eval scores.")
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--scores", type=Path, default=Path("Temp/LoRA/Evals/tile_style_eval_scores.json"))
    parser.add_argument("--out-root", type=Path, default=Path("Assets/Generated/_Review/tile_style_eval_selected_family_v1"))
    parser.add_argument("--families", nargs="+", default=["earth_block", "grass_top", "stone_water", "dark_water", "ice"])
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    scores_path = args.scores if args.scores.is_absolute() else project_root / args.scores
    out_root = args.out_root if args.out_root.is_absolute() else project_root / args.out_root
    payload = json.loads(scores_path.read_text(encoding="utf-8-sig"))
    selected_scores = select_best(payload.get("items", []), args.families)
    selected: list[dict[str, Any]] = []
    for item in selected_scores:
        source = Path(item["path"])
        if not source.is_absolute():
            source = project_root / source
        family = str(item.get("style_target_family", "tile"))
        source_name = str(item.get("name") or source.stem)
        filename = f"{safe_name(family)}__{safe_name(source_name)}__s{str(item.get('strength')).replace('.', 'p')}.png"
        normalized_path = out_root / "png" / filename
        facts = normalize_to_32(source, normalized_path)
        selected.append(
            {
                "family": family,
                "source_name": source_name,
                "strength": item.get("strength"),
                "score": item.get("score"),
                "palette_distance": item.get("palette_distance"),
                "source_path": repo_rel(project_root, source),
                "normalized_path": repo_rel(project_root, normalized_path),
                "notes": item.get("notes", []),
                **facts,
            }
        )
    sheet_path = out_root / "selected_tile_family_contact_sheet.png"
    if selected:
        build_sheet(project_root, selected, sheet_path)
    manifest = {
        "schema": "lit_iso.lora.selected_tile_family_review.v1",
        "generated_utc": utc_now(),
        "scores": repo_rel(project_root, scores_path),
        "out_root": repo_rel(project_root, out_root),
        "contact_sheet": repo_rel(project_root, sheet_path) if sheet_path.exists() else "",
        "status": "review_only_not_unity_approved",
        "selection_rule": "lowest score per requested family",
        "requested_families": args.families,
        "selected": selected,
        "missing_families": [family for family in args.families if family not in {item["family"] for item in selected}],
    }
    out_root.mkdir(parents=True, exist_ok=True)
    manifest_path = out_root / "selected_tile_family_manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps({"ok": True, "selected": len(selected), "manifest": str(manifest_path), "contact_sheet": str(sheet_path) if sheet_path.exists() else ""}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
