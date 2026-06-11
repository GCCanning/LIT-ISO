#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import shutil
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw


CANONICAL_DIRECTIONS = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"]
DIRECTION_LABELS = {
    "S": "south/front",
    "SE": "south-east/front 3q",
    "E": "east/side",
    "NE": "north-east/back 3q",
    "N": "north/back",
    "NW": "north-west/back 3q",
    "W": "west/side",
    "SW": "south-west/front 3q",
}


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def rel(root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path.resolve()).replace("\\", "/")


def checker(size: tuple[int, int]) -> Image.Image:
    image = Image.new("RGBA", size, (27, 30, 34, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], 8):
        for x in range(0, size[0], 8):
            if (x // 8 + y // 8) % 2 == 0:
                draw.rectangle([x, y, x + 7, y + 7], fill=(38, 42, 48, 255))
    return image


def source_by_direction(manifests: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}
    for manifest in manifests:
        for item in manifest.get("selected", []):
            direction = str(item.get("direction", "")).upper()
            if direction and direction not in result:
                result[direction] = item
    return result


def paste_cell(sheet: Image.Image, draw: ImageDraw.ImageDraw, image_path: Path, x: int, y: int, title: str, subtitle: str, status: str) -> None:
    draw.rectangle([x, y, x + 151, y + 173], fill=(24, 27, 31, 255), outline=(66, 74, 82, 255))
    preview = checker((128, 128))
    image = Image.open(image_path).convert("RGBA").resize((128, 128), Image.Resampling.NEAREST)
    preview.alpha_composite(image)
    sheet.alpha_composite(preview, (x + 12, y + 8))
    draw.text((x + 8, y + 140), title, fill=(236, 241, 232, 255))
    draw.text((x + 8, y + 154), subtitle[:29], fill=(168, 180, 170, 255))
    draw.text((x + 94, y + 140), status[:16], fill=(126, 220, 151, 255))


def make_contact_sheet(project_root: Path, selected: list[dict[str, Any]], style_reference: Path, output_path: Path, variant: str) -> None:
    cell_w = 152
    cell_h = 174
    header_h = 58
    columns = 9
    sheet = Image.new("RGBA", (columns * cell_w, header_h + cell_h), (17, 19, 22, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((12, 10), f"Black mage {variant} 8D current best evidence", fill=(238, 242, 238, 255))
    draw.text((12, 30), "Review only. Mixed current-best directions; manual approval required before training or Unity.", fill=(160, 170, 164, 255))

    paste_cell(sheet, draw, style_reference, 0, header_h, "STYLE", "source", "")
    for index, item in enumerate(selected, start=1):
        direction = item["direction"]
        path = project_root / item["path"].replace("/", "\\")
        subtitle = f"{item.get('source_variant', '')} seed {item.get('seed', '')}".strip()
        paste_cell(sheet, draw, path, index * cell_w, header_h, direction, subtitle, str(item.get("status", "")))
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path)


def main() -> int:
    parser = argparse.ArgumentParser(description="Combine selected black mage cardinals/diagonals into one 8D review pack.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--diagonal-manifest", default="Assets/Generated/_Review/black_mage_iso_selected_v11/black_mage_selected_v11_manifest.json")
    parser.add_argument("--cardinal-manifest", default="Assets/Generated/_Review/black_mage_iso_selected_v12_cardinals/black_mage_selected_v12_cardinals_manifest.json")
    parser.add_argument("--side-manifest", default="")
    parser.add_argument("--style-reference", default="Assets/Generated/_Review/black_mage_iso_style_lock_v1/black_mage_normalized_128.png")
    parser.add_argument("--out-root", default="Assets/Generated/_Review/black_mage_iso_selected_v12_mixed_8d")
    parser.add_argument("--variant", default="v12_mixed_8d")
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    out_root = project_root / args.out_root
    out_root.mkdir(parents=True, exist_ok=True)
    diagonal_manifest_path = project_root / args.diagonal_manifest
    cardinal_manifest_path = project_root / args.cardinal_manifest
    manifests = []
    side_manifest_path = None
    if args.side_manifest:
        side_manifest_path = project_root / args.side_manifest
        manifests.append(read_json(side_manifest_path))
    manifests.append(read_json(cardinal_manifest_path))
    manifests.append(read_json(diagonal_manifest_path))
    by_direction = source_by_direction(manifests)

    selected: list[dict[str, Any]] = []
    missing: list[str] = []
    for direction in CANONICAL_DIRECTIONS:
        item = by_direction.get(direction)
        if item is None:
            missing.append(direction)
            continue
        source = project_root / str(item["path"]).replace("/", "\\")
        dest = out_root / f"black_mage_{direction.lower()}_selected_{args.variant}.png"
        shutil.copy2(source, dest)
        if side_manifest_path is not None and direction in {"E", "W"}:
            source_variant = "v13_side"
        elif direction in {"S", "E", "N", "W"}:
            source_variant = "v12_cardinals"
        else:
            source_variant = "v11_diagonal"
        selected.append(
            {
                "direction": direction,
                "direction_label": DIRECTION_LABELS[direction],
                "path": rel(project_root, dest),
                "source_path": item.get("path", ""),
                "source_variant": source_variant,
                "seed": item.get("seed", ""),
                "score": item.get("score"),
                "status": item.get("status", "review_candidate"),
                "issues": item.get("issues", []),
                "warnings": item.get("warnings", []),
                "metrics": item.get("metrics", {}),
                "manual_direction_quality": "needs_review",
            }
        )

    style_reference = project_root / args.style_reference.replace("/", "\\")
    contact_sheet = out_root / f"black_mage_selected_{args.variant}_contact_sheet.png"
    make_contact_sheet(project_root, selected, style_reference, contact_sheet, args.variant)
    manifest = {
        "schema": "lit_iso.asset_forge.black_mage_selected_candidates.v1",
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "variant": args.variant,
        "source_qc_report": "",
        "source_manifests": [rel(project_root, path) for path in ([side_manifest_path] if side_manifest_path else []) + [cardinal_manifest_path, diagonal_manifest_path]],
        "out_root": rel(project_root, out_root),
        "contact_sheet": rel(project_root, contact_sheet),
        "selected_count": len(selected),
        "missing_directions": missing,
        "selected": selected,
        "status": "review_only_manual_approval_required",
        "manual_review_note": "Mixed current-best direction evidence. Manual direction review is still required before training.",
        "next_gate": "Approve/reject each direction; rerun side-view E/W if they are too front-facing.",
    }
    manifest_path = out_root / f"black_mage_selected_{args.variant}_manifest.json"
    write_json(manifest_path, manifest)
    print(json.dumps({"manifest": rel(project_root, manifest_path), "contact_sheet": rel(project_root, contact_sheet), "selected_count": len(selected), "missing": missing}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
