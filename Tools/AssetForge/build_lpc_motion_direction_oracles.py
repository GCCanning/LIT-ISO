#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any

from PIL import Image

from build_direction_oracle_factory import (
    DIRECTION_NAME,
    draw_contact,
    image_stats,
    normalize_cell,
    rel_path,
    safe_name,
    utc_now,
    warnings_for,
    write_dataset_pack,
)

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


LPC_TO_LITISO_DIRECTION = {
    "south": "S",
    "east": "E",
    "north": "N",
    "west": "W",
}
DEFAULT_SPECS = [
    "male_leather_adventurer:none:walk:4:male leather adventurer with forest pants, brown leather armor, dark brown hair",
    "female_forest_scout:none:walk:4:female forest scout with green leather armor, forest pants, carrot orange hair",
    "male_leather_adventurer:axe:walk:4:male leather adventurer carrying an axe",
    "female_forest_scout:axe:walk:4:female forest scout carrying an axe",
    "male_leather_adventurer:hammer:walk:4:male leather adventurer carrying a hammer",
    "female_forest_scout:hammer:walk:4:female forest scout carrying a hammer",
    "male_leather_adventurer:pickaxe:walk:4:male leather adventurer carrying a pickaxe",
    "female_forest_scout:pickaxe:walk:4:female forest scout carrying a pickaxe",
    "male_leather_adventurer:none:spellcast:3:male leather adventurer casting a simple spell",
]


def load_rows(dataset_root: Path) -> list[dict[str, Any]]:
    metadata = dataset_root / "metadata.jsonl"
    if not metadata.exists():
        raise FileNotFoundError(f"Missing metadata.jsonl: {metadata}")
    rows = []
    with metadata.open("r", encoding="utf-8-sig") as handle:
        for line in handle:
            line = line.strip()
            if line:
                rows.append(json.loads(line))
    return rows


def parse_spec(value: str) -> dict[str, Any]:
    parts = value.split(":", 4)
    if len(parts) != 5:
        raise argparse.ArgumentTypeError("--spec must be character:tool:action:frame_index:description")
    character, tool, action, frame_index, description = parts
    return {
        "character": character.strip(),
        "tool": tool.strip(),
        "action": action.strip(),
        "frame_index": int(frame_index.strip()),
        "description": description.strip(),
    }


def select_record(rows: list[dict[str, Any]], spec: dict[str, Any], direction: str) -> dict[str, Any]:
    matches = [
        row
        for row in rows
        if row.get("character") == spec["character"]
        and row.get("tool") == spec["tool"]
        and row.get("action") == spec["action"]
        and row.get("direction") == direction
    ]
    if not matches:
        raise FileNotFoundError(f"No LPC record for {spec['character']} {spec['tool']} {spec['action']} {direction}")
    return sorted(matches, key=lambda row: abs(int(row.get("frame_index", 0)) - spec["frame_index"]))[0]


def build_pack(
    project_root: Path,
    source_dataset: Path,
    out_root: Path,
    dataset_root: Path,
    rows: list[dict[str, Any]],
    spec: dict[str, Any],
    cell_size: int,
    output_size: int,
    max_fill: float,
    capture_dataset: bool,
) -> dict[str, Any]:
    pack_name = safe_name(f"lpc_{spec['character']}_{spec['tool']}_{spec['action']}_f{spec['frame_index']:02d}_4d_oracle")
    oracle_root = out_root / pack_name
    frames_root = oracle_root / "frames"
    frames_root.mkdir(parents=True, exist_ok=True)
    packed = Image.new("RGBA", (output_size * 4, output_size), (0, 0, 0, 0))
    frame_entries = []
    issues = []
    order = ["S", "E", "N", "W"]
    reverse_direction = {value: key for key, value in LPC_TO_LITISO_DIRECTION.items()}
    for index, direction in enumerate(order):
        lpc_direction = reverse_direction[direction]
        record = select_record(rows, spec, lpc_direction)
        source_path = source_dataset / record["file_name"]
        if not source_path.exists():
            raise FileNotFoundError(f"Missing source frame: {source_path}")
        with Image.open(source_path) as source:
            normalized, normalization = normalize_cell(
                source.convert("RGBA"),
                output_size,
                output_size,
                max_fill,
                allow_upscale=True,
            )
        frame_path = frames_root / f"{pack_name}_{direction.lower()}.png"
        normalized.save(frame_path)
        x = index * output_size
        packed.alpha_composite(normalized, (x, 0))
        stats = image_stats(normalized)
        issues.extend(warnings_for(direction, stats, output_size, output_size))
        frame_entries.append(
            {
                "index": index,
                "direction": direction,
                "source_image": rel_path(project_root, frame_path),
                "source_image_abs": str(frame_path),
                "source_dataset": str(source_dataset),
                "source_record": record,
                "normalization": normalization,
                "metrics": stats,
                "rect": {"x": x, "y": 0, "width": output_size, "height": output_size},
                "pivot": {"x": 0.5, "y": 0.0},
            }
        )

    sheet_output = oracle_root / f"{pack_name}_sheet.png"
    contact_output = oracle_root / f"{pack_name}_contact.png"
    manifest_output = oracle_root / f"{pack_name}_manifest.json"
    validation_output = oracle_root / f"{pack_name}_validation.json"
    packed.save(sheet_output)
    draw_contact(frame_entries, contact_output, output_size, output_size)
    manifest = {
        "schema": "lit_iso.asset_forge.reference_direction_sheet.v1",
        "generated_utc": utc_now(),
        "pack_name": pack_name,
        "source_dataset": str(source_dataset),
        "sheet": rel_path(project_root, sheet_output),
        "contact_sheet": rel_path(project_root, contact_output),
        "cell": {"width": output_size, "height": output_size},
        "directions": [frame["direction"] for frame in frame_entries],
        "frames": [{key: value for key, value in frame.items() if key != "source_image_abs"} for frame in frame_entries],
        "use": "LPC motion direction/body oracle for evaluation and pretraining anchors; not final LIT-ISO art style",
    }
    manifest_output.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    validation = {
        "schema": "lit_iso.asset_forge.direction_oracle_validation.v1",
        "generated_utc": utc_now(),
        "pack_name": pack_name,
        "manifest": rel_path(project_root, manifest_output),
        "summary": {
            "frame_count": len(frame_entries),
            "warning_count": sum(1 for issue in issues if issue["severity"] == "warning"),
            "error_count": sum(1 for issue in issues if issue["severity"] == "error"),
            "info_count": sum(1 for issue in issues if issue["severity"] == "info"),
        },
        "issues": issues,
        "directions": [frame["direction"] for frame in frame_entries],
        "metrics": {frame["direction"]: frame["metrics"] for frame in frame_entries},
    }
    validation_output.write_text(json.dumps(validation, indent=2), encoding="utf-8")
    dataset_pack = None
    if capture_dataset:
        dataset_pack = write_dataset_pack(
            project_root,
            dataset_root,
            pack_name,
            frame_entries,
            sheet_output,
            contact_output,
            manifest_output,
            spec["description"],
            f"{spec['action']} reference pose",
            "CC-BY-SA-LPC-training-reference",
            "Universal LPC community, adapted for LIT-ISO review",
        )
    return {
        "pack_name": pack_name,
        "manifest": rel_path(project_root, manifest_output),
        "validation": rel_path(project_root, validation_output),
        "contact_sheet": rel_path(project_root, contact_output),
        "dataset_pack": rel_path(project_root, dataset_pack) if dataset_pack else None,
        "summary": validation["summary"],
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build multiple 4D direction oracles from the LPC motion metadata dataset.")
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--source-dataset", type=Path, default=Path(r"C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\lpc_motion_male_female_v1"))
    parser.add_argument("--out-root", type=Path, default=Path("Assets/Generated/_Review/_DirectionOracles"))
    parser.add_argument("--dataset-root", type=Path, default=Path(r"C:\Projects\Pixel Pipeline\datasets\lit_iso"))
    parser.add_argument("--spec", action="append", type=parse_spec, default=[])
    parser.add_argument("--cell-size", type=int, default=64)
    parser.add_argument("--output-size", type=int, default=128)
    parser.add_argument("--max-fill", type=float, default=0.76)
    parser.add_argument("--capture-dataset", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    source_dataset = args.source_dataset if args.source_dataset.is_absolute() else project_root / args.source_dataset
    out_root = args.out_root if args.out_root.is_absolute() else project_root / args.out_root
    dataset_root = args.dataset_root if args.dataset_root.is_absolute() else project_root / args.dataset_root
    specs = args.spec or [parse_spec(value) for value in DEFAULT_SPECS]
    rows = load_rows(source_dataset)
    packs = [
        build_pack(
            project_root,
            source_dataset,
            out_root,
            dataset_root,
            rows,
            spec,
            args.cell_size,
            args.output_size,
            args.max_fill,
            args.capture_dataset,
        )
        for spec in specs
    ]
    summary = {
        "ok": True,
        "pack_count": len(packs),
        "record_count": len(packs) * 4,
        "warning_count": sum(pack["summary"]["warning_count"] for pack in packs),
        "error_count": sum(pack["summary"]["error_count"] for pack in packs),
        "packs": packs,
    }
    print(json.dumps(summary, indent=2))
    return 1 if summary["error_count"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
