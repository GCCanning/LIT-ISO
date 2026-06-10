#!/usr/bin/env python3
"""Build the SpriteForge P3 lane A/B comparison sheet."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

from PIL import Image, ImageDraw, ImageFont


def load_json(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {}
    return json.loads(path.read_text(encoding="utf-8-sig"))


def placeholder(size: tuple[int, int], title: str, lines: list[str]) -> Image.Image:
    image = Image.new("RGBA", size, (24, 24, 30, 255))
    draw = ImageDraw.Draw(image)
    font = ImageFont.load_default()
    draw.text((12, 10), title, fill=(245, 245, 245, 255), font=font)
    y = 30
    for line in lines:
        draw.text((12, y), line[:120], fill=(190, 190, 205, 255), font=font)
        y += 14
    return image


def load_panel(path: Path, fallback_size: tuple[int, int], title: str, lines: list[str]) -> Image.Image:
    if path.exists():
        return Image.open(path).convert("RGBA")
    return placeholder(fallback_size, title, lines)


def label_panel(panel: Image.Image, label: str) -> Image.Image:
    label_h = 22
    out = Image.new("RGBA", (panel.width, panel.height + label_h), (12, 12, 16, 255))
    out.alpha_composite(panel, (0, label_h))
    draw = ImageDraw.Draw(out)
    draw.text((6, 5), label, fill=(240, 240, 240, 255), font=ImageFont.load_default())
    return out


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build P3 A/B comparison sheet.")
    parser.add_argument("--project-root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--lane-a-preview", type=Path, default=Path("Tools/SpriteForge/out/p2_fix_sweep/d038_c062_bob/witch/walk/S/preview_x4.png"))
    parser.add_argument("--lane-b-preview", type=Path, default=Path("Tools/SpriteForge/out/lane_b/witch/walk/S/preview_x4.png"))
    parser.add_argument("--lane-b-manifest", type=Path, default=Path("Tools/SpriteForge/out/lane_b/witch/walk/S/lane_b_manifest.json"))
    parser.add_argument("--stack-report", type=Path, default=Path("Tools/SpriteForge/out/lane_b/p3_stack_report.json"))
    parser.add_argument("--out", type=Path, default=Path("Tools/SpriteForge/out/lane_b/p3_ab_comparison.png"))
    parser.add_argument("--manifest", type=Path, default=Path("Tools/SpriteForge/out/lane_b/p3_ab_comparison.json"))
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    root = args.project_root.resolve()
    lane_a = args.lane_a_preview if args.lane_a_preview.is_absolute() else root / args.lane_a_preview
    lane_b = args.lane_b_preview if args.lane_b_preview.is_absolute() else root / args.lane_b_preview
    stack_path = args.stack_report if args.stack_report.is_absolute() else root / args.stack_report
    lane_b_manifest_path = args.lane_b_manifest if args.lane_b_manifest.is_absolute() else root / args.lane_b_manifest
    out_path = args.out if args.out.is_absolute() else root / args.out
    manifest_path = args.manifest if args.manifest.is_absolute() else root / args.manifest

    stack = load_json(stack_path)
    lane_b_manifest = load_json(lane_b_manifest_path)
    a_panel = load_panel(lane_a, (1536, 300), "Lane A missing", [str(lane_a)])
    fallback_size = a_panel.size
    b_panel = load_panel(
        lane_b,
        fallback_size,
        "Lane B preview unavailable",
        [
            f"stack_status: {stack.get('status', 'unknown')}",
            f"lane_b_status: {lane_b_manifest.get('status', 'not-run')}",
            "Live Wan render requires node load + model files.",
        ],
    )
    width = max(a_panel.width, b_panel.width)
    a_labeled = label_panel(a_panel, "LANE A default: d038_c062_bob, frame-by-frame ControlNet/IP-Adapter")
    b_labeled = label_panel(b_panel, f"LANE B: video-frame cleanup/pack path, status={lane_b_manifest.get('status', 'not-run')}")
    summary_lines = [
        f"stack_status: {stack.get('status', 'unknown')}",
        f"missing_nodes: {', '.join(stack.get('comfy', {}).get('missing_required_nodes', [])[:5]) or 'none'}",
        f"missing_model_buckets: {', '.join(stack.get('missing_model_buckets', [])) or 'none'}",
        "Decision: Lane A remains default below 96px until a real Wan model render beats it.",
    ]
    summary = placeholder((width, 74), "P3 Lane A/B Gate", summary_lines)

    total_h = summary.height + a_labeled.height + b_labeled.height
    sheet = Image.new("RGBA", (width, total_h), (8, 8, 12, 255))
    y = 0
    sheet.alpha_composite(summary, (0, y))
    y += summary.height
    sheet.alpha_composite(a_labeled, (0, y))
    y += a_labeled.height
    sheet.alpha_composite(b_labeled, (0, y))
    out_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out_path)

    data = {
        "schema": "lit-iso.spriteforge.p3-ab-comparison.v1",
        "out": str(out_path),
        "lane_a_preview": str(lane_a),
        "lane_b_preview": str(lane_b),
        "lane_b_manifest": str(lane_b_manifest_path),
        "stack_report": str(stack_path),
        "stack_status": stack.get("status", "unknown"),
        "lane_b_status": lane_b_manifest.get("status", "not-run"),
        "decision": "lane_a_default_below_96px_pending_real_lane_b_model_render",
    }
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(json.dumps(data, indent=2), encoding="utf-8")
    print(json.dumps({"comparison": str(out_path), "manifest": str(manifest_path)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
