#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw


CANONICAL_DIRECTION_ORDER = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"]

DIRECTION_LABELS = {
    "S": "south / front",
    "SE": "south-east / front three-quarter",
    "E": "east / side",
    "NE": "north-east / back three-quarter",
    "N": "north / back",
    "NW": "north-west / back three-quarter",
    "W": "west / side",
    "SW": "south-west / front three-quarter",
}


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def repo_path(root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path)


def make_checker(size: tuple[int, int]) -> Image.Image:
    image = Image.new("RGBA", size, (28, 31, 35, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], 8):
        for x in range(0, size[0], 8):
            color = (38, 43, 48, 255) if ((x // 8) + (y // 8)) % 2 == 0 else (24, 27, 31, 255)
            draw.rectangle([x, y, x + 7, y + 7], fill=color)
    return image


def draw_wrapped_text(draw: ImageDraw.ImageDraw, xy: tuple[int, int], text: str, fill: tuple[int, int, int, int], max_chars: int) -> None:
    x, y = xy
    line = ""
    for word in text.split():
        candidate = f"{line} {word}".strip()
        if len(candidate) > max_chars and line:
            draw.text((x, y), line, fill=fill)
            y += 12
            line = word
        else:
            line = candidate
    if line:
        draw.text((x, y), line, fill=fill)


def make_sheet(project_root: Path, selected_by_direction: dict[str, dict[str, Any]], output_path: Path, title: str) -> None:
    cell_w = 152
    cell_h = 206
    margin_top = 58
    sheet = Image.new("RGBA", (cell_w * len(CANONICAL_DIRECTION_ORDER), margin_top + cell_h), (16, 18, 21, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((12, 10), title, fill=(238, 244, 247, 255))
    draw.text((12, 30), "Canonical order: S, SE, E, NE, N, NW, W, SW. Missing cells must be generated before animation work.", fill=(171, 184, 192, 255))

    for index, direction in enumerate(CANONICAL_DIRECTION_ORDER):
        x = index * cell_w
        y = margin_top
        present = direction in selected_by_direction
        outline = (88, 170, 112, 255) if present else (177, 92, 84, 255)
        draw.rectangle([x, y, x + cell_w - 1, y + cell_h - 1], outline=outline)
        draw.text((x + 8, y + 8), direction, fill=(244, 247, 250, 255))
        draw.text((x + 8, y + 22), DIRECTION_LABELS[direction], fill=(164, 177, 185, 255))

        preview_box = (x + 12, y + 42)
        if present:
            item = selected_by_direction[direction]
            image_path = project_root / str(item["path"]).replace("/", "\\")
            if image_path.exists():
                sprite = Image.open(image_path).convert("RGBA")
                scale = max(1, min(1, 112 // max(sprite.width, sprite.height)))
                preview = make_checker((128, 128))
                left = (128 - sprite.width * scale) // 2
                top = (128 - sprite.height * scale) // 2
                if scale != 1:
                    sprite = sprite.resize((sprite.width * scale, sprite.height * scale), Image.Resampling.NEAREST)
                preview.alpha_composite(sprite, (left, top))
                sheet.alpha_composite(preview, preview_box)
            score = item.get("score")
            seed = item.get("seed")
            draw.text((x + 8, y + 176), f"present score={score}", fill=(142, 226, 159, 255))
            draw.text((x + 8, y + 190), f"seed={seed}", fill=(164, 177, 185, 255))
        else:
            draw.rectangle([preview_box[0], preview_box[1], preview_box[0] + 127, preview_box[1] + 127], fill=(35, 24, 24, 255), outline=(177, 92, 84, 255))
            draw.line([preview_box[0] + 12, preview_box[1] + 12, preview_box[0] + 116, preview_box[1] + 116], fill=(177, 92, 84, 255), width=2)
            draw.line([preview_box[0] + 116, preview_box[1] + 12, preview_box[0] + 12, preview_box[1] + 116], fill=(177, 92, 84, 255), width=2)
            draw_wrapped_text(draw, (x + 8, y + 176), "missing; generate from true template", (235, 151, 145, 255), 22)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)


def build_report(project_root: Path, selected_manifest_path: Path, output_root: Path) -> dict[str, Any]:
    selected_manifest = read_json(selected_manifest_path)
    generated_utc = datetime.now(timezone.utc).isoformat()

    selected_items = list(selected_manifest.get("selected", []))
    selected_by_direction: dict[str, dict[str, Any]] = {}
    duplicates: list[str] = []
    for item in selected_items:
        direction = str(item.get("direction", "")).upper()
        if not direction:
            continue
        if direction in selected_by_direction:
            duplicates.append(direction)
            continue
        selected_by_direction[direction] = item

    present = [direction for direction in CANONICAL_DIRECTION_ORDER if direction in selected_by_direction]
    missing = [direction for direction in CANONICAL_DIRECTION_ORDER if direction not in selected_by_direction]
    cardinal = ["S", "E", "N", "W"]
    diagonals = ["SE", "NE", "NW", "SW"]
    present_cardinals = [direction for direction in cardinal if direction in selected_by_direction]
    missing_cardinals = [direction for direction in cardinal if direction not in selected_by_direction]
    present_diagonals = [direction for direction in diagonals if direction in selected_by_direction]
    missing_diagonals = [direction for direction in diagonals if direction not in selected_by_direction]

    sheet_path = output_root / "black_mage_direction_coverage_sheet.png"
    make_sheet(project_root, selected_by_direction, sheet_path, "Black Mage v11 Direction Coverage")

    complete_4d_cardinal = len(missing_cardinals) == 0
    complete_8d = len(missing) == 0
    report = {
        "schema": "lit_iso.asset_forge.black_mage_direction_coverage.v1",
        "generated_utc": generated_utc,
        "status": "review_only_not_unity_imported",
        "source_manifest": repo_path(project_root, selected_manifest_path),
        "coverage_sheet": repo_path(project_root, sheet_path),
        "canonical_direction_order": CANONICAL_DIRECTION_ORDER,
        "present_count": len(present),
        "missing_count": len(missing),
        "present_directions": present,
        "missing_directions": missing,
        "present_cardinal_directions": present_cardinals,
        "missing_cardinal_directions": missing_cardinals,
        "present_diagonal_directions": present_diagonals,
        "missing_diagonal_directions": missing_diagonals,
        "duplicate_directions": duplicates,
        "complete_4d_cardinal_set": complete_4d_cardinal,
        "complete_8d_set": complete_8d,
        "animation_ready": False,
        "training_ready": False,
        "training_blockers": [
            "manual_art_approval_pending",
            "missing_cardinal_directions" if missing_cardinals else "",
            "missing_8d_directions" if missing else "",
            "no_animation_frame_sequences",
        ],
        "selected": [
            {
                "direction": direction,
                "label": DIRECTION_LABELS[direction],
                "path": str(selected_by_direction[direction].get("path", "")),
                "seed": selected_by_direction[direction].get("seed", ""),
                "score": selected_by_direction[direction].get("score"),
                "metrics": selected_by_direction[direction].get("metrics", {}),
            }
            for direction in present
        ],
        "next_required_generation": [
            {
                "direction": direction,
                "label": DIRECTION_LABELS[direction],
                "reason": "cardinal direction missing; required before 4D sheet, walk cycles, or LoRA capture",
            }
            for direction in missing_cardinals
        ],
        "recommendations": [
            "Do not treat v11 as a complete character sheet.",
            "Use v11 as diagonal style/direction evidence only until manual approval.",
            "Generate true S/E/N/W templates next; north must show the character back, not the face.",
            "After S/E/N/W pass, rerun the same coverage report before animation frame generation.",
        ],
    }
    report["training_blockers"] = [item for item in report["training_blockers"] if item]
    return report


def main() -> int:
    parser = argparse.ArgumentParser(description="Build black mage direction coverage report from the selected candidate manifest.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--selected-manifest", default="Assets/Generated/_Review/black_mage_iso_selected_v11/black_mage_selected_v11_manifest.json")
    parser.add_argument("--output-root", default="")
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    selected_manifest_path = project_root / args.selected_manifest
    if args.output_root:
        output_root = project_root / args.output_root
    else:
        selected_manifest = read_json(selected_manifest_path)
        output_root = project_root / selected_manifest.get("out_root", str(selected_manifest_path.parent))

    report = build_report(project_root, selected_manifest_path, output_root)
    report_path = output_root / "direction_coverage_report.json"
    write_json(report_path, report)

    print(json.dumps({
        "ok": True,
        "report": repo_path(project_root, report_path),
        "coverage_sheet": report["coverage_sheet"],
        "present": report["present_directions"],
        "missing": report["missing_directions"],
        "complete_8d_set": report["complete_8d_set"],
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
