#!/usr/bin/env python3
"""
Build a Unity-facing worldgen runtime contract.

The authored datapack describes visual tile/prop ids. Foundation runtime still
has gameplay archetypes such as tree, rock, bush, flower, log, and stump. This
contract bridges the two without importing art or changing sampler behavior:

- every referenced tile id gets a promotion status;
- every feature entry gets a visual id, gameplay archetype, footprint, and
  optional center/drop multiplier metadata;
- transition material aliases are explicit, so "grass" remains a material tag
  instead of accidentally becoming a missing tile id.
"""

from __future__ import annotations

import argparse
import json
import re
import uuid
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
WORLDGEN = ROOT / "Assets" / "StreamingAssets" / "worldgen"
FEATURES = WORLDGEN / "features"
TRANSITIONS = WORLDGEN / "transitions.json"
BIOME_SUITE = WORLDGEN / "biome_suite.json"
PROP_INTERACTIONS = WORLDGEN / "prop_interactions.json"
SETTLEMENTS = WORLDGEN / "settlements.json"
BIOMESKETCH_ASSETS = ROOT / "Tools" / "BiomeSketch" / "assets.js"
AUDIT = ROOT / "Temp" / "WorldGen" / "worldgen_rule_audit.json"
PROMOTION = ROOT / "Temp" / "WorldGen" / "worldgen_asset_promotion_plan.json"
DEFAULT_OUT = WORLDGEN / "runtime_contract.json"


def rel(path: Path) -> str:
    try:
        return path.relative_to(ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def load(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def load_optional(path: Path, fallback: Any) -> Any:
    return load(path) if path.exists() else fallback


def load_biomesketch_assets() -> list[dict[str, Any]]:
    if not BIOMESKETCH_ASSETS.exists():
        return []
    text = BIOMESKETCH_ASSETS.read_text(encoding="utf-8-sig")
    match = re.search(r"const\s+ASSETS\s*=\s*(\[.*?\]);?\s*$", text, re.S)
    if not match:
        raise RuntimeError(f"Could not parse ASSETS array from {BIOMESKETCH_ASSETS}")
    return json.loads(match.group(1))


def archetype(asset_id: str) -> str:
    s = asset_id.lower()
    if any(k in s for k in ("tree", "oak", "pine", "willow")):
        return "tree"
    if any(k in s for k in ("rock", "stone", "ore", "vein")):
        return "rock"
    if "bush" in s:
        return "bush"
    if "mushroom" in s:
        return "mushroom"
    if "flower" in s or "tulip" in s:
        return "flower"
    if "tuft" in s or "grass" in s:
        return "tuft"
    if "stump" in s:
        return "stump"
    if "log" in s:
        return "log"
    return "decoration"


def prototype_for(kind: str) -> str:
    return {
        "tree": "tree",
        "rock": "rock",
        "bush": "bush",
        "mushroom": "flower",
        "flower": "flower",
        "tuft": "tuft",
        "stump": "stump",
        "log": "log",
    }.get(kind, "bush")


def promotion_status(plan: dict[str, Any], kind: str, asset_id: str) -> dict[str, Any]:
    section = "tiles" if kind == "tile" else "props"
    for row in plan.get(section, []):
        if row.get("id") == asset_id:
            return {
                "status": row.get("status"),
                "source": row.get("source"),
                "destination": row.get("destination"),
                "aliasOf": row.get("alias_of"),
            }
    return {"status": "unknown", "source": None, "destination": None, "aliasOf": None}


def collect_feature_contract(plan: dict[str, Any]) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    if not FEATURES.exists():
        return result
    for path in sorted(FEATURES.glob("*.json")):
        feature_id = path.stem
        data = load(path)
        configured = data.get("configured", {}) or {}
        placement = data.get("placement", {}) or {}

        def add_entry(entry: dict[str, Any], role: str, drop_multiplier: float = 1.0) -> None:
            asset_id = entry.get("id")
            if not asset_id:
                return
            kind = archetype(asset_id)
            promo = promotion_status(plan, "prop", asset_id)
            result.append({
                "featureId": feature_id,
                "configuredType": configured.get("type"),
                "role": role,
                "visualId": asset_id,
                "gameplayPrototype": prototype_for(kind),
                "archetype": kind,
                "weight": entry.get("w", 1),
                "optional": bool(entry.get("optional", False)),
                "footprint": entry.get("footprint", "1x1"),
                "dropMultiplier": drop_multiplier,
                "placement": placement,
                "promotion": promo,
                "source": rel(path),
            })

        for entry in configured.get("entries", []) or []:
            add_entry(entry, "entry")

        center = configured.get("center", {}) or {}
        center_multiplier = float(center.get("dropMultiplier", 1.0) or 1.0)
        for entry in center.get("entries", []) or []:
            add_entry(entry, "cluster-center", center_multiplier)
    return result


def collect_tile_contract(audit: dict[str, Any], plan: dict[str, Any]) -> list[dict[str, Any]]:
    result = []
    for item in audit.get("tiles", {}).get("items", []):
        promo = promotion_status(plan, "tile", item["id"])
        result.append({
            "tileId": item["id"],
            "status": item.get("status"),
            "promotion": promo,
            "references": item.get("references", []),
        })
    return result


def collect_material_aliases() -> dict[str, list[str]]:
    aliases: dict[str, set[str]] = {
        "grass": {"grass_1", "grass_2", "grass_3", "forest_floor"},
        "sand": {"sand_1", "sand_2"},
        "water": {"water", "water_deep", "water_deep_2", "water_deep_3", "water_swell_1", "water_swell_2"},
        "dirt": {"dirt", "forest_mud_path"},
        "stone": {"stone_block", "stone_path"},
        "forest_floor": {"forest_floor", "canopy_1", "canopy_2", "canopy_3"},
    }
    if TRANSITIONS.exists():
        data = load(TRANSITIONS)
        for pair in data.get("pairs", []) or []:
            a = pair.get("a")
            b = pair.get("b")
            tiles = pair.get("tiles", {}) or {}
            if a:
                aliases.setdefault(a, set()).update(tiles.values())
            if b:
                aliases.setdefault(b, set()).update(tiles.values())
    return {k: sorted(v) for k, v in sorted(aliases.items())}


def collect_variant_groups(assets: list[dict[str, Any]]) -> list[dict[str, Any]]:
    groups: dict[tuple[str, str, str], list[dict[str, Any]]] = {}
    metadata: dict[tuple[str, str, str], dict[str, Any]] = {}
    for asset in assets:
        if asset.get("cat") != "tile":
            continue
        source = asset.get("source_asset")
        variant_kind = asset.get("variant_kind")
        if not source or not variant_kind:
            continue
        key = (source, asset.get("group", ""), variant_kind)
        metadata.setdefault(key, {
            "sourceTileId": source,
            "sourceGroup": asset.get("source_group", ""),
            "group": asset.get("group", ""),
            "variantKind": variant_kind,
            "mode": asset.get("variant_mode", ""),
            "status": "biomesketch-only",
        })
        groups.setdefault(key, []).append({
            "tileId": asset.get("name", ""),
            "path": asset.get("path", ""),
            "index": int(asset.get("variant_index", 0) or 0),
            "seed": asset.get("variant_seed", ""),
            "status": "biomesketch-only",
        })

    result: list[dict[str, Any]] = []
    for key in sorted(groups):
        entry = metadata[key].copy()
        entry["variants"] = sorted(groups[key], key=lambda item: (item["index"], item["tileId"]))
        result.append(entry)
    return result


def collect_blend_pairs(assets: list[dict[str, Any]]) -> list[dict[str, Any]]:
    pairs: dict[tuple[str, str, str, float], list[dict[str, Any]]] = {}
    metadata: dict[tuple[str, str, str, float], dict[str, Any]] = {}
    for asset in assets:
        if asset.get("cat") != "tile":
            continue
        left = asset.get("blend_from")
        right = asset.get("blend_to")
        if not left or not right:
            continue
        pattern = asset.get("blend_pattern", "")
        softness = float(asset.get("blend_softness", 0.0) or 0.0)
        key = (left, right, pattern, softness)
        metadata.setdefault(key, {
            "fromTileId": left,
            "toTileId": right,
            "pattern": pattern,
            "softness": softness,
            "status": "biomesketch-only",
        })
        pairs.setdefault(key, []).append({
            "tileId": asset.get("name", ""),
            "path": asset.get("path", ""),
            "direction": asset.get("blend_direction", ""),
            "index": int(asset.get("blend_variant_index", 0) or 0),
            "seed": asset.get("blend_seed", ""),
            "status": "biomesketch-only",
        })

    direction_order = {"n": 0, "s": 1, "e": 2, "w": 3, "ne": 4, "nw": 5, "se": 6, "sw": 7}
    result: list[dict[str, Any]] = []
    for key in sorted(pairs):
        entry = metadata[key].copy()
        entry["variants"] = sorted(
            pairs[key],
            key=lambda item: (direction_order.get(item["direction"], 99), item["index"], item["tileId"]),
        )
        result.append(entry)
    return result


def ensure_meta(path: Path) -> None:
    meta = path.with_suffix(path.suffix + ".meta")
    if meta.exists():
        return
    meta.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {uuid.uuid4().hex}\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n",
        encoding="utf-8",
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--audit", type=Path, default=AUDIT)
    parser.add_argument("--promotion", type=Path, default=PROMOTION)
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    args = parser.parse_args()

    audit = load(args.audit)
    plan = load(args.promotion)
    biomesketch_assets = load_biomesketch_assets()
    contract = {
        "schema": "litiso.worldgen.runtime_contract.v1",
        "generatedFrom": {
            "worldgenRoot": rel(WORLDGEN),
            "audit": rel(args.audit),
            "promotionPlan": rel(args.promotion),
            "biomeSketchAssets": rel(BIOMESKETCH_ASSETS),
        },
        "rules": {
            "materialTagsAreNotTileIds": ["grass", "sand", "stone", "water", "dirt"],
            "runtimeLiveMeans": "asset exists under Assets/Resources and Foundation content defines the id",
            "generatedOnlyMeans": "asset exists under Assets/Generated and needs promotion before Resources lookup works",
            "biomeSketchOnlyMeans": "asset exists only in the review/sketch tool and needs art-lane promotion",
        },
        "materialAliases": collect_material_aliases(),
        "tiles": collect_tile_contract(audit, plan),
        "features": collect_feature_contract(plan),
        "biomeSuite": load_optional(BIOME_SUITE, {}),
        "settlements": load_optional(SETTLEMENTS, {}),
        "propInteractions": load_optional(PROP_INTERACTIONS, {}),
        "variantGroups": collect_variant_groups(biomesketch_assets),
        "blendPairs": collect_blend_pairs(biomesketch_assets),
    }

    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(contract, indent=2), encoding="utf-8")
    if ROOT in args.out.parents:
        ensure_meta(args.out)

    print(json.dumps({
        "out": str(args.out),
        "tiles": len(contract["tiles"]),
        "features": len(contract["features"]),
        "variantGroups": len(contract["variantGroups"]),
        "blendPairs": len(contract["blendPairs"]),
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
