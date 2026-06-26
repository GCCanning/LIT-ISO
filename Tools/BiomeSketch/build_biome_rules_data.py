#!/usr/bin/env python3
"""Builds biome_rules_data.js for rules.html (the biome rules editor page).

Seeds the editor with the CURRENT biome -> tile/prop assignments by reading:
  - Assets/StreamingAssets/worldgen/biomes/*.json   (base/accent tile pools)
  - Assets/StreamingAssets/worldgen/features/*.json (prop entries per feature)
  - Assets/StreamingAssets/worldgen/runtime_contract.json (active tile ids)
  - Assets/Resources/Decorations/*.png              (canonical prop superset)
  - Tools/BiomeSketch/assets.js                     (thumbnail paths)

Re-run after rule changes to refresh the seed:
  python3 Tools/BiomeSketch/build_biome_rules_data.py
"""
import json
import os
import re
import sys
import glob
import datetime

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
WG = os.path.join(ROOT, "LIT-ISO", "Assets", "StreamingAssets", "worldgen")
if not os.path.isdir(WG):  # script lives in <repo>/Tools/BiomeSketch
    WG = os.path.join(os.path.dirname(HERE), "..", "Assets", "StreamingAssets", "worldgen")
    WG = os.path.abspath(WG)
DECOR_DIR = os.path.abspath(os.path.join(WG, "..", "..", "Resources", "Decorations"))
TILES_DIR = os.path.abspath(os.path.join(WG, "..", "..", "Resources", "Tiles"))


def load(p):
    with open(p, encoding="utf-8") as f:
        return json.load(f)


def pool_ids(pool):
    return [e["tile"] for e in pool or [] if isinstance(e, dict) and "tile" in e]


def main():
    biomes_dir = os.path.join(WG, "biomes")
    features_dir = os.path.join(WG, "features")

    # feature id -> prop entry ids
    feature_props = {}
    for fp in sorted(glob.glob(os.path.join(features_dir, "*.json"))):
        fid = os.path.splitext(os.path.basename(fp))[0]
        d = load(fp)
        entries = (d.get("configured") or {}).get("entries") or d.get("entries") or []
        feature_props[fid] = [e["id"] for e in entries if isinstance(e, dict) and "id" in e]

    assignments = {}

    for bp in sorted(glob.glob(os.path.join(biomes_dir, "*.json"))):
        bid = os.path.splitext(os.path.basename(bp))[0]
        b = load(bp)
        base, accent, props = [], [], []

        if "surfaceBase" in b:
            base.append(b["surfaceBase"])
        base += pool_ids(b.get("surfaceBasePool"))
        accent += pool_ids(b.get("surfaceAccents"))
        accent += [t for t in (b.get("iceTiles") or [])]

        # mountain stratification bands
        for band in b.get("stratification") or []:
            if band.get("surface"):
                base.append(band["surface"])
            accent += pool_ids(band.get("accents"))

        # legacy beach/coast schema: known band/decor ids
        if "bandOrder" in b:
            for token in b["bandOrder"]:
                m = re.match(r"([a-z0-9_]+)", token)
                if m and m.group(1) not in ("grass",):
                    base.append(m.group(1))
            for d in (b.get("decor") or {}).values():
                if isinstance(d, dict) and d.get("id"):
                    props.append(d["id"])

        feats = b.get("features") or []
        for fid in feats:
            props += feature_props.get(fid, [])

        def dedupe(xs):
            seen, out = set(), []
            for x in xs:
                if x not in seen:
                    seen.add(x)
                    out.append(x)
            return out

        base = dedupe(base)
        assignments[bid] = {
            "base": base,
            "accent": [t for t in dedupe(accent) if t not in base],
            "props": dedupe(props),
            "features": feats,
            "notes": "",
        }

    # water + dungeon pseudo-buckets so ocean/river and dungeon art can be triaged
    contract = load(os.path.join(WG, "runtime_contract.json"))
    active_tiles = [t["tileId"] for t in contract.get("tiles") or []]
    water_ids = [t for t in active_tiles if t.startswith("water")]
    dungeon_ids = [t for t in active_tiles if t.startswith("dungeon")]
    assignments.setdefault("water", {"base": water_ids, "accent": [], "props": ["shore_stone"], "features": [], "notes": ""})
    assignments.setdefault("dungeon", {"base": dungeon_ids, "accent": [], "props": [], "features": [], "notes": ""})

    # canonical pools
    decor_props = sorted(os.path.splitext(os.path.basename(p))[0]
                         for p in glob.glob(os.path.join(DECOR_DIR, "*.png")))
    res_tiles = sorted(os.path.splitext(os.path.basename(p))[0]
                       for p in glob.glob(os.path.join(TILES_DIR, "*.png")))
    all_tiles = sorted(set(active_tiles) | set(res_tiles))

    assigned_tiles = {t for a in assignments.values() for t in a["base"] + a["accent"]}
    assigned_props = {p for a in assignments.values() for p in a["props"]}
    unassigned = {
        "tiles": [t for t in all_tiles if t not in assigned_tiles],
        "props": [p for p in decor_props if p not in assigned_props],
    }

    # thumbnails from the BiomeSketch registry
    s = open(os.path.join(HERE, "assets.js"), encoding="utf-8").read()
    registry = json.loads(s[s.index("["):s.rindex("]") + 1])
    asset_index = {}
    for a in registry:
        # first hit wins (top-level catalog entries come before variant subfolders)
        asset_index.setdefault(a["name"], a["path"])

    out = {
        "schema": "litiso.biome_rules_seed.v1",
        "generatedAt": datetime.datetime.now().isoformat(timespec="seconds"),
        "biomeOrder": ["meadow", "forest", "beach", "coast", "mountain", "snow", "water", "dungeon"],
        "assignments": assignments,
        "unassigned": unassigned,
        "assetIndex": asset_index,
        "featureProps": feature_props,
    }

    out_path = os.path.join(HERE, "biome_rules_data.js")
    with open(out_path, "w", encoding="utf-8") as f:
        f.write("// generated by build_biome_rules_data.py — do not hand-edit\n")
        f.write("const BIOME_RULES_SEED = ")
        json.dump(out, f, separators=(",", ":"))
        f.write(";\n")

    n_assigned = sum(len(a["base"]) + len(a["accent"]) + len(a["props"]) for a in assignments.values())
    print(f"wrote {out_path}")
    print(f"biomes: {list(assignments)}  assigned ids: {n_assigned}  "
          f"unassigned tiles: {len(unassigned['tiles'])}  unassigned props: {len(unassigned['props'])}")


if __name__ == "__main__":
    sys.exit(main())
