#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import math
import shutil
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont


DEFAULT_REFERENCE = "Assets/Generated/_Review/black_mage_iso_style_lock_v1/black_mage_normalized_128.png"
DEFAULT_SELECTED_MANIFEST = (
    "Assets/Generated/_Review/black_mage_iso_selected_v13_mixed_8d/"
    "black_mage_selected_v13_mixed_8d_manifest.json"
)
DEFAULT_IDENTITY_REPORT = (
    "Assets/Generated/_Review/black_mage_identity_lock_v1/"
    "black_mage_identity_lock_report.json"
)
DEFAULT_OUT_ROOT = "Assets/Generated/_Review/black_mage_reference_anchor_v1"
DIRECTION_ORDER = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def read_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")


def resolve_path(root: Path, value: str | Path) -> Path:
    path = Path(value)
    return path if path.is_absolute() else root / path


def repo_path(root: Path, path: Path | str) -> str:
    path = Path(path)
    if not path.is_absolute():
        path = root / path
    try:
        return path.resolve().relative_to(root.resolve()).as_posix()
    except ValueError:
        return str(path.resolve()).replace("\\", "/")


def load_rgba(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def font(size: int = 13) -> ImageFont.ImageFont:
    try:
        return ImageFont.truetype("arial.ttf", size)
    except OSError:
        return ImageFont.load_default()


def checker(size: tuple[int, int]) -> Image.Image:
    image = Image.new("RGBA", size, (32, 36, 42, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], 8):
        for x in range(0, size[0], 8):
            fill = (42, 47, 54, 255) if ((x // 8) + (y // 8)) % 2 == 0 else (32, 36, 42, 255)
            draw.rectangle((x, y, x + 7, y + 7), fill=fill)
    return image


def paste_preview(board: Image.Image, image: Image.Image, x: int, y: int, scale: int = 2) -> None:
    preview = image.resize((image.width * scale, image.height * scale), Image.Resampling.NEAREST)
    cell = checker(preview.size)
    cell.alpha_composite(preview)
    board.alpha_composite(cell, (x, y))


def selected_by_direction(manifest: dict[str, Any]) -> dict[str, dict[str, Any]]:
    return {str(item.get("direction", "")).upper(): item for item in manifest.get("selected", [])}


def identity_by_direction(report: dict[str, Any]) -> dict[str, dict[str, Any]]:
    return {str(item.get("direction", "")).upper(): item for item in report.get("records", [])}


def build_sheet(
    project_root: Path,
    reference_path: Path,
    selected_manifest: dict[str, Any],
    identity_report: dict[str, Any],
    out_path: Path,
) -> None:
    title_font = font(14)
    body_font = font(11)
    card_w = 266
    card_h = 344
    cols = 3
    rows = 1 + math.ceil(len(DIRECTION_ORDER) / cols)
    board = Image.new("RGBA", (cols * card_w, 74 + rows * card_h), (16, 19, 24, 255))
    draw = ImageDraw.Draw(board)
    draw.text((14, 12), "Black Mage Reference Anchor Pack", fill=(246, 248, 252, 255), font=title_font)
    draw.text(
        (14, 34),
        "Contract: S/front is the supplied source anchor unless a generated S passes strict identity lock.",
        fill=(172, 183, 196, 255),
        font=body_font,
    )

    reference = load_rgba(reference_path)
    paste_preview(board, reference, 14, 74, 2)
    draw.text((14, 74 + 260), "S SOURCE ANCHOR", fill=(237, 220, 115, 255), font=title_font)
    draw.text((14, 74 + 282), "Use this as the front frame baseline.", fill=(184, 196, 208, 255), font=body_font)
    draw.text((14, 74 + 298), "Generated directions must match it.", fill=(184, 196, 208, 255), font=body_font)

    by_dir = selected_by_direction(selected_manifest)
    identity_dir = identity_by_direction(identity_report)
    for index, direction in enumerate(DIRECTION_ORDER):
        item = by_dir.get(direction)
        col = (index + 1) % cols
        row = (index + 1) // cols
        x = col * card_w + 10
        y = 74 + row * card_h
        if not item:
            draw.text((x, y + 128), f"{direction} MISSING", fill=(240, 100, 86, 255), font=title_font)
            continue
        image_path = resolve_path(project_root, item["path"])
        image = load_rgba(image_path)
        paste_preview(board, image, x, y, 2)
        identity = identity_dir.get(direction, {}).get("identity", {})
        status = str(identity.get("status", "needs_review"))
        color = (239, 106, 92, 255) if status == "identity_fail" else (116, 220, 145, 255)
        draw.text((x, y + 260), f"{direction} {status}", fill=color, font=title_font)
        draw.text((x, y + 282), f"source {item.get('source_variant', '')}", fill=(184, 196, 208, 255), font=body_font)
        draw.text((x, y + 298), f"score {identity.get('identity_score', 'n/a')}", fill=(184, 196, 208, 255), font=body_font)
        issues = identity.get("issues") or identity.get("warnings") or ["manual review"]
        draw.text((x, y + 314), ", ".join(issues)[:38], fill=(184, 196, 208, 255), font=body_font)

    out_path.parent.mkdir(parents=True, exist_ok=True)
    board.save(out_path)


def main() -> int:
    parser = argparse.ArgumentParser(description="Create a source-anchor review pack for black mage 8D work.")
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--reference", default=DEFAULT_REFERENCE)
    parser.add_argument("--selected-manifest", default=DEFAULT_SELECTED_MANIFEST)
    parser.add_argument("--identity-report", default=DEFAULT_IDENTITY_REPORT)
    parser.add_argument("--output-root", default=DEFAULT_OUT_ROOT)
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    reference_path = resolve_path(project_root, args.reference)
    selected_manifest_path = resolve_path(project_root, args.selected_manifest)
    identity_report_path = resolve_path(project_root, args.identity_report)
    out_root = resolve_path(project_root, args.output_root)
    out_root.mkdir(parents=True, exist_ok=True)

    source_anchor_path = out_root / "black_mage_s_source_anchor.png"
    shutil.copy2(reference_path, source_anchor_path)
    selected_manifest = read_json(selected_manifest_path)
    identity_report = read_json(identity_report_path)
    sheet_path = out_root / "black_mage_reference_anchor_sheet.png"
    build_sheet(project_root, reference_path, selected_manifest, identity_report, sheet_path)

    generated_dirs = selected_by_direction(selected_manifest)
    identity_dirs = identity_by_direction(identity_report)
    entries = []
    for direction in DIRECTION_ORDER:
        item = generated_dirs.get(direction)
        identity = identity_dirs.get(direction, {}).get("identity", {})
        entries.append(
            {
                "direction": direction,
                "role": "source_anchor" if direction == "S" else "generated_direction_candidate",
                "path": repo_path(project_root, source_anchor_path) if direction == "S" else (item.get("path") if item else None),
                "candidate_path": item.get("path") if item else None,
                "identity_status": identity.get("status", "missing"),
                "identity_score": identity.get("identity_score"),
                "issues": identity.get("issues", []),
            }
        )

    manifest = {
        "schema": "lit_iso.asset_forge.black_mage_reference_anchor_pack.v1",
        "generated_utc": utc_now(),
        "status": "review_only_not_unity_imported",
        "source_anchor": repo_path(project_root, source_anchor_path),
        "selected_manifest": repo_path(project_root, selected_manifest_path),
        "identity_report": repo_path(project_root, identity_report_path),
        "sheet": repo_path(project_root, sheet_path),
        "entries": entries,
        "generation_contract": [
            "Do not accept an 8D mage pack until S/front either uses source_anchor or generated S passes the identity lock.",
            "Do not train on generated direction candidates that fail identity lock.",
            "Use current v13 mixed 8D only as directional evidence, not as production art.",
        ],
        "next_recommendation": "Queue v14 S/front reconstruction first; expand to SE/E/NE/N/NW/W/SW only after S passes.",
    }
    write_json(out_root / "black_mage_reference_anchor_manifest.json", manifest)
    print(
        json.dumps(
            {
                "ok": True,
                "manifest": repo_path(project_root, out_root / "black_mage_reference_anchor_manifest.json"),
                "sheet": repo_path(project_root, sheet_path),
                "source_anchor": repo_path(project_root, source_anchor_path),
            },
            indent=2,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
