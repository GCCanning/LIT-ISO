#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import shutil
import sys
from pathlib import Path

from queue_oga_template_guided_requests import (
    CANONICAL_DIRECTIONS,
    request_payload,
    safe_name,
    utc_now,
)

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")

DEFAULT_DATASET = Path(r"C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\oga_8d_composite_motion_v1")


def load_records(dataset: Path) -> list[dict]:
    metadata = dataset / "metadata.jsonl"
    if not metadata.exists():
        raise FileNotFoundError(metadata)
    return [json.loads(line) for line in metadata.read_text(encoding="utf-8").splitlines() if line.strip()]


def choose_template(dataset: Path, records: list[dict], preset: str, action: str, direction: str) -> Path:
    matches = [
        record
        for record in records
        if record.get("preset_id") == preset
        and str(record.get("action", "")).lower() == action.lower()
        and record.get("direction") == direction
    ]
    if not matches:
        raise FileNotFoundError(f"No composite template for preset={preset} action={action} direction={direction}")
    matches.sort(key=lambda record: int(record.get("sequence_frame_index", 0)))
    selected = matches[len(matches) // 2]
    return dataset / selected["file_name"]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--dataset", type=Path, default=DEFAULT_DATASET)
    parser.add_argument("--out-root", type=Path, default=Path(r"Assets\Generated\_Review\_Requests"))
    parser.add_argument("--preset", default="iron_knight")
    parser.add_argument("--action", default="Walk")
    parser.add_argument("--directions", nargs="+", default=["S", "E", "N", "W"])
    parser.add_argument("--job-prefix", default="oga_composite_refknight_style")
    parser.add_argument("--denoise", type=float, default=0.70)
    parser.add_argument("--seed", type=int, default=91600)
    parser.add_argument("--style-reference", default="")
    parser.add_argument("--style-weight", type=float, default=0.62)
    parser.add_argument("--replace", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    dataset = args.dataset.resolve()
    out_root = (project_root / args.out_root).resolve() if not args.out_root.is_absolute() else args.out_root.resolve()
    records = load_records(dataset)
    out_root.mkdir(parents=True, exist_ok=True)
    created = []
    for index, direction in enumerate(args.directions):
        if direction not in CANONICAL_DIRECTIONS:
            raise ValueError(f"Unsupported direction: {direction}")
        template = choose_template(dataset, records, args.preset, args.action, direction)
        job_name = safe_name(f"{args.job_prefix}_{args.preset}_{args.action.lower()}_{direction.lower()}")
        request_root = out_root / job_name
        if request_root.exists() and args.replace:
            shutil.rmtree(request_root)
        elif request_root.exists():
            created.append({"job_name": job_name, "status": "skipped_exists", "request_root": str(request_root)})
            continue
        (request_root / "Inputs").mkdir(parents=True, exist_ok=True)
        (request_root / "Outputs").mkdir(parents=True, exist_ok=True)
        (request_root / "Review").mkdir(parents=True, exist_ok=True)
        payload = request_payload(
            job_name,
            args.action,
            direction,
            str(template),
            args.denoise,
            args.seed + index,
            args.style_reference,
            args.style_weight,
        )
        payload["pack_name"] = "OGA_CompositeTemplateGuided"
        payload["template_guidance"]["source"] = f"OpenGameArt 8D composite preset {args.preset}"
        payload["template_guidance"]["preset"] = args.preset
        payload["template_guidance"]["dataset"] = str(dataset)
        request_path = request_root / "generation_request.json"
        status_path = request_root / "request_status.json"
        request_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
        status_path.write_text(
            json.dumps(
                {
                    "ok": True,
                    "status": "queued_composite_template_guided",
                    "saved_utc": utc_now(),
                    "job_name": job_name,
                    "preset": args.preset,
                    "action": args.action,
                    "direction": direction,
                    "template": str(template),
                    "next_step": f"Run process_generation_request_comfy.ps1 -JobName {job_name} -DryRun first.",
                },
                indent=2,
            ),
            encoding="utf-8",
        )
        created.append({"job_name": job_name, "status": "queued", "direction": direction, "template": str(template), "request_path": str(request_path)})
    print(json.dumps({"ok": True, "created": created}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
