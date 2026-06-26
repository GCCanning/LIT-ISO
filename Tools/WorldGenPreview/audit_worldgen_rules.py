#!/usr/bin/env python3
"""
Audit the LIT-ISO worldgen datapack against Unity-facing asset ids.

This intentionally does not import art or touch Unity assets. It reads the
authored JSON rules under Assets/StreamingAssets/worldgen, the current
Resources/Tiles and Resources/Decorations folders, and the BiomeSketch asset
registry. The output tells us which ids are live in Unity, which are available
only in review/sketch tooling, and which are missing.
"""

from __future__ import annotations

import argparse
import json
import re
from collections import Counter, defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
WORLDGEN = ROOT / "Assets" / "StreamingAssets" / "worldgen"
BIOMES = WORLDGEN / "biomes"
FEATURES = WORLDGEN / "features"
BIOME_SUITE = WORLDGEN / "biome_suite.json"
DUNGEON_THEMES = WORLDGEN / "dungeon_themes.json"
PROP_INTERACTIONS = WORLDGEN / "prop_interactions.json"
SETTLEMENTS = WORLDGEN / "settlements.json"
RES_TILES = ROOT / "Assets" / "Resources" / "Tiles"
RES_DECORATIONS = ROOT / "Assets" / "Resources" / "Decorations"
GENERATED_TILES = ROOT / "Assets" / "Generated" / "Tiles"
GENERATED_PROPS = ROOT / "Assets" / "Generated" / "Props"
BIOMESKETCH_ASSETS = ROOT / "Tools" / "BiomeSketch" / "assets.js"
DEFAULT_OUT = ROOT / "Temp" / "WorldGen" / "worldgen_rule_audit.json"


@dataclass
class Ref:
    ref_type: str
    ref_id: str
    source: str
    context: str


@dataclass
class Audit:
    tile_refs: list[Ref] = field(default_factory=list)
    prop_refs: list[Ref] = field(default_factory=list)
    biome_files: list[str] = field(default_factory=list)
    feature_files: list[str] = field(default_factory=list)
    support_files: list[str] = field(default_factory=list)


def load_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8-sig") as f:
        return json.load(f)


def unity_resource_ids(folder: Path) -> set[str]:
    if not folder.exists():
        return set()
    return {p.stem for p in folder.glob("*.png")}


def recursive_png_ids(folder: Path) -> dict[str, str]:
    if not folder.exists():
        return {}
    ids: dict[str, str] = {}
    for p in sorted(folder.rglob("*.png")):
        ids.setdefault(p.stem, rel(p))
    return ids


def load_biomesketch_registry(path: Path) -> dict[str, dict[str, Any]]:
    if not path.exists():
        return {}
    text = path.read_text(encoding="utf-8-sig")
    m = re.search(r"const\s+ASSETS\s*=\s*(\[.*?\]);?\s*$", text, re.S)
    if not m:
        raise RuntimeError(f"Could not parse ASSETS array from {path}")
    assets = json.loads(m.group(1))
    return {a.get("name", ""): a for a in assets if a.get("name")}


def tile_ref(audit: Audit, ref_id: str | None, source: Path, context: str) -> None:
    if ref_id and not ref_id.startswith("$"):
        audit.tile_refs.append(Ref("tile", ref_id, rel(source), context))


def prop_ref(audit: Audit, ref_id: str | None, source: Path, context: str) -> None:
    if ref_id:
        audit.prop_refs.append(Ref("prop", ref_id, rel(source), context))


def rel(path: Path) -> str:
    try:
        return path.relative_to(ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def collect_surface_rules(audit: Audit) -> None:
    path = WORLDGEN / "surface_rules.json"
    if not path.exists():
        return
    data = load_json(path)
    for i, rule in enumerate(data):
        ctx = f"surface_rules[{i}]"
        tile_ref(audit, rule.get("tile"), path, ctx)
        for key in ("variants", "accents", "shoreContact"):
            for entry in rule.get(key, []) or []:
                tile_ref(audit, entry.get("tile"), path, f"{ctx}.{key}")


def collect_biomes(audit: Audit) -> None:
    if not BIOMES.exists():
        return
    for path in sorted(BIOMES.glob("*.json")):
        audit.biome_files.append(rel(path))
        data = load_json(path)
        tile_ref(audit, data.get("surfaceBase"), path, "surfaceBase")
        for entry in data.get("surfaceBasePool", []) or []:
            tile_ref(audit, entry.get("tile"), path, "surfaceBasePool")
        for entry in data.get("surfaceAccents", []) or []:
            tile_ref(audit, entry.get("tile"), path, "surfaceAccents")
        for entry in data.get("stratification", []) or []:
            tile_ref(audit, entry.get("tile"), path, "stratification")
            for variant in entry.get("variants", []) or []:
                tile_ref(audit, variant.get("tile"), path, "stratification.variants")


def collect_dungeon_themes(audit: Audit) -> None:
    path = DUNGEON_THEMES
    if not path.exists():
        return
    audit.support_files.append(rel(path))
    data = load_json(path)
    for tier, theme in (data.get("tiers", {}) or {}).items():
        for entry in theme.get("floors", []) or []:
            tile_ref(audit, entry.get("tile"), path, f"tiers.{tier}.floors")
        for entry in theme.get("accents", []) or []:
            tile_ref(audit, entry.get("tile"), path, f"tiers.{tier}.accents")


def collect_prop_interactions(audit: Audit) -> None:
    path = PROP_INTERACTIONS
    if not path.exists():
        return
    audit.support_files.append(rel(path))
    data = load_json(path)
    list_keys = (
        "craftingStations",
        "resourceNodes",
        "containers",
        "townServices",
        "lights",
        "ambient",
    )
    for key in list_keys:
        for entry in data.get(key, []) or []:
            prop_ref(audit, entry.get("id"), path, key)
    for key in ("blockingDecorations", "floorOverlays"):
        for value in data.get(key, []) or []:
            prop_ref(audit, value, path, key)


def collect_settlements(audit: Audit) -> None:
    path = SETTLEMENTS
    if not path.exists():
        return
    audit.support_files.append(rel(path))
    data = load_json(path)
    for entry in data.get("buildingPool", []) or []:
        prop_ref(audit, entry.get("id"), path, "buildingPool")
    for value in data.get("outdoorProps", []) or []:
        prop_ref(audit, value, path, "outdoorProps")


def collect_biome_suite(audit: Audit) -> None:
    path = BIOME_SUITE
    if path.exists():
        audit.support_files.append(rel(path))


def collect_features(audit: Audit) -> None:
    if not FEATURES.exists():
        return
    for path in sorted(FEATURES.glob("*.json")):
        audit.feature_files.append(rel(path))
        data = load_json(path)
        configured = data.get("configured", {}) or {}
        for entry in configured.get("entries", []) or []:
            prop_ref(audit, entry.get("id"), path, "configured.entries")
        center = configured.get("center", {}) or {}
        for entry in center.get("entries", []) or []:
            prop_ref(audit, entry.get("id"), path, "configured.center.entries")


def classify(
    refs: list[Ref],
    unity_ids: set[str],
    sketch: dict[str, dict[str, Any]],
    generated: dict[str, str],
) -> dict[str, Any]:
    by_id: dict[str, list[Ref]] = defaultdict(list)
    for ref in refs:
        by_id[ref.ref_id].append(ref)

    rows = []
    counts = Counter()
    for ref_id in sorted(by_id):
        live = ref_id in unity_ids
        sketch_asset = sketch.get(ref_id)
        generated_path = generated.get(ref_id)
        if live:
            status = "unity-live"
        elif generated_path:
            status = "generated-only"
        elif sketch_asset:
            status = "biomesketch-only"
        else:
            status = "missing"
        counts[status] += 1
        rows.append({
            "id": ref_id,
            "status": status,
            "biomesketch_group": sketch_asset.get("group") if sketch_asset else None,
            "biomesketch_path": sketch_asset.get("path") if sketch_asset else None,
            "generated_path": generated_path,
            "references": [
                {"source": r.source, "context": r.context}
                for r in by_id[ref_id]
            ],
        })

    return {"counts": dict(counts), "items": rows}


def write_markdown(report: dict[str, Any], path: Path) -> None:
    lines = [
        "# Worldgen Rule Audit",
        "",
        f"Generated from `{rel(WORLDGEN)}`.",
        "",
        "## Summary",
        "",
        f"- Biomes: {report['summary']['biome_count']}",
        f"- Features: {report['summary']['feature_count']}",
        f"- Support files: {report['summary']['support_file_count']}",
        f"- Tile ids referenced: {report['summary']['tile_ids']}",
        f"- Prop ids referenced: {report['summary']['prop_ids']}",
        "",
        "## Tile Status",
        "",
    ]
    for status, count in sorted(report["tiles"]["counts"].items()):
        lines.append(f"- {status}: {count}")
    lines.extend(["", "## Prop Status", ""])
    for status, count in sorted(report["props"]["counts"].items()):
        lines.append(f"- {status}: {count}")

    for section in ("tiles", "props"):
        missing = [i for i in report[section]["items"] if i["status"] != "unity-live"]
        lines.extend(["", f"## {section.title()} Not Live In Unity", ""])
        if not missing:
            lines.append("- None")
            continue
        for item in missing:
            src = item["references"][0]["source"] if item["references"] else "?"
            group = item.get("biomesketch_group") or "-"
            lines.append(f"- `{item['id']}`: {item['status']} | group `{group}` | first ref `{src}`")

    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    parser.add_argument("--markdown", type=Path, default=None)
    args = parser.parse_args()

    audit = Audit()
    collect_surface_rules(audit)
    collect_biomes(audit)
    collect_dungeon_themes(audit)
    collect_features(audit)
    collect_prop_interactions(audit)
    collect_settlements(audit)
    collect_biome_suite(audit)

    sketch = load_biomesketch_registry(BIOMESKETCH_ASSETS)
    tiles = classify(audit.tile_refs, unity_resource_ids(RES_TILES), sketch, recursive_png_ids(GENERATED_TILES))
    props = classify(audit.prop_refs, unity_resource_ids(RES_DECORATIONS), sketch, recursive_png_ids(GENERATED_PROPS))

    report = {
        "project_root": str(ROOT),
        "worldgen_root": rel(WORLDGEN),
        "summary": {
            "biome_count": len(audit.biome_files),
            "feature_count": len(audit.feature_files),
            "support_file_count": len(audit.support_files),
            "tile_ids": len(tiles["items"]),
            "prop_ids": len(props["items"]),
        },
        "biome_files": audit.biome_files,
        "feature_files": audit.feature_files,
        "support_files": audit.support_files,
        "tiles": tiles,
        "props": props,
        "policy": {
            "unity_live": "id exists as Assets/Resources/Tiles or Assets/Resources/Decorations png",
            "generated_only": "id exists under Assets/Generated but is not runtime-live until promoted to Resources",
            "biomesketch_only": "id exists in Tools/BiomeSketch/assets.js but is not promoted to Resources",
            "missing": "id is referenced by worldgen JSON but not found in Unity Resources, Assets/Generated, or BiomeSketch",
        },
    }

    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(report, indent=2), encoding="utf-8")
    md = args.markdown or args.out.with_suffix(".md")
    write_markdown(report, md)

    print(json.dumps({
        "out": str(args.out),
        "markdown": str(md),
        "summary": report["summary"],
        "tile_counts": tiles["counts"],
        "prop_counts": props["counts"],
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
