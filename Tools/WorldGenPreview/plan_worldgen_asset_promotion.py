#!/usr/bin/env python3
"""
Build a dry-run promotion plan for worldgen tile/prop assets.

The worldgen JSON already references the desired ids, but the live Unity
Resources set may not contain all of them. This script consumes the audit report
from audit_worldgen_rules.py and writes an explicit plan:

- BiomeSketch-only ids that can be copied into Resources when the art lane says go.
- Missing ids that need generation, aliasing, or removal from the rule pack.
- Existing live ids that require no action.

It never copies files unless --apply is passed. Even then, it only writes to the
configured destination folders and should be used by the owner/Claude art lane.
"""

from __future__ import annotations

import argparse
import json
import shutil
from dataclasses import dataclass
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
AUDIT = ROOT / "Temp" / "WorldGen" / "worldgen_rule_audit.json"
BIOMESKETCH = ROOT / "Tools" / "BiomeSketch"
DEST_TILES = ROOT / "Assets" / "Resources" / "Tiles"
DEST_DECOR = ROOT / "Assets" / "Resources" / "Decorations"
DEFAULT_OUT = ROOT / "Temp" / "WorldGen" / "worldgen_asset_promotion_plan.json"


FALLBACK_ALIASES = {
    # These are semantic fallbacks only. They preserve gameplay behavior while
    # art is missing, but should be replaced by authored assets before ship.
    "forest_log": "log",
    "forest_pine": "pine",
    "plains_bush_v2_0_big": "plains_bush_v2_0",
    "plains_bush_v2_1_big": "plains_bush_v2_1",
    "plains_bush_v2_2_big": "plains_bush_v2_2",
    "plains_bush_v2_3_big": "plains_bush_v2_3",
    "plains_rock_v2_0_big": "plains_rock_v2_0",
    "plains_rock_v2_1_big": "plains_rock_v2_1",
    "plains_rock_v2_2_big": "plains_rock_v2_2",
    "plains_rock_v2_3_big": "plains_rock_v2_3",
    "plains_tree_v2_0_young": "plains_tree_v2_0",
    "plains_tree_v2_1_young": "plains_tree_v2_1",
    "plains_tree_v2_3_young": "plains_tree_v2_3",
    "plains_willow": "plains_tree",
}


@dataclass
class PlannedCopy:
    kind: str
    asset_id: str
    source: str | None
    destination: str | None
    status: str
    reason: str
    alias_of: str | None = None


def rel(path: Path | None) -> str | None:
    if path is None:
        return None
    try:
        return path.relative_to(ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def load(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def source_path(item: dict[str, Any]) -> Path | None:
    generated = item.get("generated_path")
    if generated:
        return ROOT / generated
    p = item.get("biomesketch_path")
    if not p:
        return None
    return BIOMESKETCH / p


def plan_item(kind: str, item: dict[str, Any]) -> PlannedCopy:
    asset_id = item["id"]
    status = item["status"]
    destination_root = DEST_TILES if kind == "tile" else DEST_DECOR
    destination = destination_root / f"{asset_id}.png"

    if status == "unity-live":
        return PlannedCopy(kind, asset_id, None, rel(destination), "no-op",
                           "already present in Unity Resources")

    src = source_path(item)
    if status in {"generated-only", "biomesketch-only"} and src and src.exists():
        source_kind = "Assets/Generated" if status == "generated-only" else "BiomeSketch registry"
        return PlannedCopy(kind, asset_id, rel(src), rel(destination), "copy-ready",
                           f"available in {source_kind}; pending art-lane promotion")

    alias = FALLBACK_ALIASES.get(asset_id)
    if alias:
        alias_dest = destination_root / f"{alias}.png"
        if alias_dest.exists():
            return PlannedCopy(kind, asset_id, rel(alias_dest), rel(destination), "alias-ready",
                               "missing requested id but fallback alias exists in Unity Resources",
                               alias_of=alias)

    return PlannedCopy(kind, asset_id, rel(src) if src else None, rel(destination), "blocked",
                       "not found in Unity Resources or BiomeSketch; generate or remove rule reference",
                       alias_of=alias)


def write_markdown(plan: dict[str, Any], path: Path) -> None:
    lines = [
        "# Worldgen Asset Promotion Plan",
        "",
        "Dry-run output. No Unity assets are copied by this report.",
        "",
        "## Summary",
        "",
    ]
    for k, v in plan["summary"].items():
        lines.append(f"- {k}: {v}")
    for kind in ("tiles", "props"):
        lines.extend(["", f"## {kind.title()}", ""])
        for row in plan[kind]:
            alias = f" alias `{row['alias_of']}`" if row.get("alias_of") else ""
            lines.append(
                f"- `{row['id']}`: {row['status']}{alias} | "
                f"{row['source'] or '-'} -> {row['destination'] or '-'}"
            )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def apply_plan(plan: dict[str, Any]) -> list[str]:
    copied: list[str] = []
    for kind in ("tiles", "props"):
        for row in plan[kind]:
            if row["status"] not in {"copy-ready", "alias-ready"}:
                continue
            src = ROOT / row["source"]
            dst = ROOT / row["destination"]
            if not src.exists():
                raise FileNotFoundError(src)
            dst.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(src, dst)
            copied.append(rel(dst) or str(dst))
    return copied


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--audit", type=Path, default=AUDIT)
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    parser.add_argument("--markdown", type=Path, default=None)
    parser.add_argument("--apply", action="store_true",
                        help="Copy copy-ready/alias-ready files into Resources. Do not use without art-lane approval.")
    args = parser.parse_args()

    audit = load(args.audit)
    tile_plan = [plan_item("tile", item).__dict__ | {"id": item["id"]}
                 for item in audit["tiles"]["items"]]
    prop_plan = [plan_item("prop", item).__dict__ | {"id": item["id"]}
                 for item in audit["props"]["items"]]

    counts: dict[str, int] = {}
    for row in tile_plan + prop_plan:
        counts[row["status"]] = counts.get(row["status"], 0) + 1

    plan = {
        "audit": rel(args.audit),
        "summary": {
            "tile_items": len(tile_plan),
            "prop_items": len(prop_plan),
            **counts,
        },
        "tiles": tile_plan,
        "props": prop_plan,
        "apply_policy": "Do not apply from Codex while Claude owns Assets/Resources. Use as handoff unless owner approves.",
    }

    copied: list[str] = []
    if args.apply:
        copied = apply_plan(plan)
        plan["applied"] = copied

    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(plan, indent=2), encoding="utf-8")
    md = args.markdown or args.out.with_suffix(".md")
    write_markdown(plan, md)

    print(json.dumps({
        "out": str(args.out),
        "markdown": str(md),
        "summary": plan["summary"],
        "applied": len(copied),
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
