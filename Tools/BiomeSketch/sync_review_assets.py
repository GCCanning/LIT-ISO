#!/usr/bin/env python3
"""
Sync reviewed worldgen art candidates into the BiomeSketch palette.

This is a review-space operation only. It copies selected PNGs into
Tools/BiomeSketch/assets/{tile,prop}/review_sync and appends missing rows to
assets.js so designers can sketch with the full approved/reviewed pool before
anything is promoted into Unity runtime Resources folders.
"""

from __future__ import annotations

import argparse
import json
import math
import re
import shutil
from collections import Counter
from pathlib import Path
from typing import Any

try:
    from PIL import Image, ImageDraw
except ImportError as exc:
    raise SystemExit("Pillow is required. Use the ComfyUI venv Python on this machine.") from exc


ROOT = Path(__file__).resolve().parents[2]
BIOMESKETCH = ROOT / "Tools" / "BiomeSketch"
ASSETS_JS = BIOMESKETCH / "assets.js"
DEFAULT_MANIFEST = Path(r"C:\tmp\LitIsoWorldGen\art_import_ready_subset\art_import_ready_subset_manifest.json")
DEFAULT_REVIEW_MANIFEST = Path(r"C:\tmp\LitIsoWorldGen\art_review_pack\art_review_manifest.json")
SYNC_ROOT = BIOMESKETCH / "assets"
REPORT_ROOT = BIOMESKETCH / "review_sync"

BUCKET_GROUPS = {
    "ready_no_overwrite": {
        "tile": "synced ready tiles",
        "prop": "synced ready props",
    },
    "ready_existing_runtime_conflict": {
        "tile": "synced conflict tiles",
        "prop": "synced conflict props",
    },
    "held": {
        "tile": "synced held tiles",
        "prop": "synced held props",
    },
}

BUCKET_KEYS = {
    "ready_no_overwrite": "ready_no_overwrite",
    "ready_existing_runtime_conflict": "ready_existing_runtime_conflicts",
    "held": "held",
}

EXCLUDED_TILE_TOKENS = ("blend", "gradient", "transition")

SOURCE_LABELS = {
    "biomesketch": "biomesketch existing",
    "pixellab_props": "pixellab props",
    "pixellab_tilesets": "pixellab tiles",
    "unity_generated_props": "unity generated props",
    "unity_generated_tiles": "unity generated tiles",
    "biomesketch_assets": "biomesketch existing",
}


def rel(path: Path) -> str:
    try:
        return path.relative_to(ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def as_biomesketch_rel(path: Path) -> str:
    return path.relative_to(BIOMESKETCH).as_posix()


def resolve_path(value: str | None) -> Path | None:
    if not value:
        return None
    path = Path(value)
    return path if path.is_absolute() else ROOT / path


def slug(value: str) -> str:
    lowered = value.lower().strip()
    safe = re.sub(r"[^a-z0-9._-]+", "_", lowered)
    safe = re.sub(r"_+", "_", safe).strip("._-")
    return safe or "asset"


def display_family(value: Any) -> str:
    family = str(value or "misc").strip()
    return family or "misc"


def source_label(source_type: Any, kind: str) -> str:
    raw = str(source_type or "").strip()
    if raw in SOURCE_LABELS:
        label = SOURCE_LABELS[raw]
        if label == "biomesketch existing":
            return f"{label} {kind}s"
        return label
    return f"candidate {kind}s"


def grouped_label(kind: str, source_type: Any, family: Any, bucket: Any) -> str:
    family_label = display_family(family)
    if str(bucket or "") == "all_candidate":
        return f"{source_label(source_type, kind)} - {family_label}"
    base = BUCKET_GROUPS.get(str(bucket or ""), {}).get(kind, f"synced {kind}s")
    return f"{base} - {family_label}"


def tile_exclusion_reason(values: list[Any]) -> str | None:
    haystack = " ".join(str(v or "").lower().replace("\\", "/") for v in values)
    for token in EXCLUDED_TILE_TOKENS:
        if token in haystack:
            return token
    return None


def excluded_tile_row(row: dict[str, Any]) -> str | None:
    if str(row.get("kind")) != "tile":
        return None
    return tile_exclusion_reason([
        row.get("stable_id"),
        row.get("family"),
        row.get("source_type"),
        row.get("source_path"),
        row.get("staged_path"),
    ])


def excluded_tile_asset(asset: dict[str, Any]) -> str | None:
    if str(asset.get("cat")) != "tile":
        return None
    return tile_exclusion_reason([
        asset.get("name"),
        asset.get("group"),
        asset.get("source_family"),
        asset.get("source_type"),
        asset.get("path"),
        asset.get("sync_source"),
    ])


def normalize_synced_group(asset: dict[str, Any]) -> None:
    path = str(asset.get("path", ""))
    if not (
        path.startswith(f"assets/{asset.get('cat')}/review_sync/")
        or path.startswith(f"assets/{asset.get('cat')}/review_candidates/")
    ):
        return
    kind = str(asset.get("cat") or "")
    if kind not in ("tile", "prop"):
        return
    asset["group"] = grouped_label(
        kind,
        asset.get("source_type"),
        asset.get("source_family"),
        asset.get("import_bucket"),
    )


def load_assets(path: Path) -> list[dict[str, Any]]:
    text = path.read_text(encoding="utf-8-sig")
    match = re.search(r"const\s+ASSETS\s*=\s*(\[.*\])\s*;", text, re.S)
    if not match:
        raise SystemExit(f"Could not parse ASSETS array from {path}")
    return json.loads(match.group(1))


def write_assets(path: Path, assets: list[dict[str, Any]]) -> None:
    packed = json.dumps(assets, ensure_ascii=True, separators=(",", ":"))
    path.write_text(f"const ASSETS = {packed};\n", encoding="utf-8")


def alpha_bbox(path: Path) -> tuple[int, int, int, int, int, int]:
    with Image.open(path).convert("RGBA") as image:
        alpha = image.getchannel("A")
        box = alpha.getbbox()
        if not box:
            return image.width, image.height, 0, 0, image.width, image.height
        left, top, right, bottom = box
        return image.width, image.height, left, top, right - left, bottom - top


def load_manifest(path: Path) -> dict[str, Any]:
    if not path.exists():
        raise SystemExit(f"Missing review manifest: {path}")
    return json.loads(path.read_text(encoding="utf-8-sig"))


def manifest_rows(manifest: dict[str, Any], include_conflicts: bool, include_held: bool) -> list[dict[str, Any]]:
    wanted = ["ready_no_overwrite"]
    if include_conflicts:
        wanted.append("ready_existing_runtime_conflict")
    if include_held:
        wanted.append("held")

    rows: list[dict[str, Any]] = []
    for bucket in wanted:
        for row in manifest.get(BUCKET_KEYS[bucket], []):
            if row.get("kind") not in ("tile", "prop"):
                continue
            item = dict(row)
            item["import_bucket"] = bucket
            rows.append(item)
    return rows


def review_candidate_rows(manifest: dict[str, Any]) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for row in manifest.get("candidates", []):
        kind = row.get("kind")
        if kind not in ("tile", "prop"):
            continue
        scale = row.get("scale") or {}
        metrics = row.get("metrics") or {}
        source_path = row.get("source_path")
        if not source_path:
            continue
        rows.append({
            "candidate_id": row.get("id"),
            "stable_id": row.get("stable_id"),
            "kind": kind,
            "source_type": row.get("source_type"),
            "family": row.get("family"),
            "source_path": source_path,
            "staged_path": source_path,
            "ppu": scale.get("ppu"),
            "footprint": scale.get("footprint", "1x1"),
            "height_class": scale.get("height_class"),
            "blocks_movement": scale.get("blocks_movement"),
            "anchor": scale.get("anchor"),
            "review_status": row.get("review_status"),
            "flags": row.get("flags", []),
            "metrics": metrics,
            "import_bucket": "all_candidate",
        })
    return rows


def unique_name(base: str, cat: str, existing_keys: set[tuple[str, str]], row: dict[str, Any]) -> tuple[str, bool]:
    if (cat, base) not in existing_keys:
        return base, False

    source_type = slug(str(row.get("source_type") or "source"))
    family = slug(str(row.get("family") or "family"))
    bucket = slug(str(row.get("import_bucket") or "bucket"))
    candidate = f"sync_{bucket}_{source_type}_{family}_{base}"
    if (cat, candidate) not in existing_keys:
        return candidate, True

    index = 2
    while (cat, f"{candidate}_{index}") in existing_keys:
        index += 1
    return f"{candidate}_{index}", True


def destination_for(row: dict[str, Any], name: str) -> Path:
    kind = str(row["kind"])
    bucket = slug(str(row.get("import_bucket") or "ready"))
    family = slug(str(row.get("family") or "misc"))
    if bucket == "all_candidate":
        source_type = slug(str(row.get("source_type") or "source"))
        return SYNC_ROOT / kind / "review_candidates" / source_type / family / f"{name}.png"
    return SYNC_ROOT / kind / "review_sync" / bucket / family / f"{name}.png"


def make_asset(row: dict[str, Any], name: str, out_path: Path) -> dict[str, Any]:
    kind = str(row["kind"])
    group = grouped_label(kind, row.get("source_type"), row.get("family"), row.get("import_bucket"))
    width, height, bx, by, bw, bh = alpha_bbox(out_path)
    asset: dict[str, Any] = {
        "cat": kind,
        "name": name,
        "path": as_biomesketch_rel(out_path),
        "w": width,
        "h": height,
        "ppu": int(row.get("ppu") or (32 if kind == "tile" else 100)),
        "bx": bx,
        "by": by,
        "bw": bw,
        "bh": bh,
        "group": group,
        "stable_id": str(row.get("stable_id") or name),
        "source_type": row.get("source_type"),
        "source_family": row.get("family"),
        "selection_tier": row.get("selection_tier"),
        "import_bucket": row.get("import_bucket"),
        "review_status": row.get("review_status"),
        "sync_source": row.get("staged_path") or row.get("source_path"),
    }
    if row.get("candidate_id"):
        asset["candidate_id"] = row["candidate_id"]
    if row.get("import_bucket") == "all_candidate":
        asset["candidate_status"] = "review_candidate"
    if row.get("footprint"):
        asset["fp"] = row["footprint"]
    if row.get("height_class"):
        asset["height_class"] = row["height_class"]
    if row.get("blocks_movement") is not None:
        asset["blocks_movement"] = bool(row["blocks_movement"])
    if row.get("runtime_destination"):
        asset["runtime_destination"] = row["runtime_destination"]
    warnings = row.get("warning_codes") or []
    if warnings:
        asset["warning_codes"] = warnings
    return asset


def make_contact_sheet(rows: list[dict[str, Any]], out_path: Path, title: str) -> None:
    if not rows:
        return
    thumb_w, thumb_h = 96, 96
    cell_w, cell_h = 190, 158
    cols = min(6, len(rows))
    grid_rows = math.ceil(len(rows) / cols)
    sheet = Image.new("RGBA", (cols * cell_w, grid_rows * cell_h + 30), (34, 37, 46, 255))
    draw = ImageDraw.Draw(sheet)
    draw.text((8, 8), title, fill=(245, 229, 190, 255))
    for index, row in enumerate(rows):
        x = (index % cols) * cell_w
        y = (index // cols) * cell_h + 30
        path = BIOMESKETCH / row["path"]
        try:
            image = Image.open(path).convert("RGBA")
        except Exception:
            image = Image.new("RGBA", (32, 32), (180, 30, 60, 255))
        scale = min(thumb_w / image.width, thumb_h / image.height, 4.0)
        size = (max(1, round(image.width * scale)), max(1, round(image.height * scale)))
        resized = image.resize(size, Image.Resampling.NEAREST)
        draw.rectangle((x + 3, y + 3, x + cell_w - 4, y + cell_h - 4), outline=(72, 170, 100, 255), width=2)
        sheet.paste(resized, (x + (cell_w - size[0]) // 2, y + 8), resized)
        draw.text((x + 8, y + 108), str(row["name"])[:28], fill=(245, 229, 190, 255))
        draw.text((x + 8, y + 122), str(row.get("source_family") or "")[:30], fill=(190, 202, 220, 255))
        draw.text((x + 8, y + 136), f"ppu {row.get('ppu')} {row.get('fp', '')}"[:30], fill=(150, 160, 176, 255))
    out_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out_path)


def candidate_name(row: dict[str, Any]) -> str:
    stable_id = slug(str(row.get("stable_id") or "asset"))
    source = resolve_path(row.get("source_path"))
    frame = slug(source.stem if source else "")
    if frame and frame != stable_id:
        return f"{stable_id}_{frame}"
    return stable_id


def sync_assets(
    manifest_path: Path,
    include_conflicts: bool,
    include_held: bool,
    include_all_candidates: bool,
    review_manifest_path: Path,
) -> dict[str, Any]:
    existing = load_assets(ASSETS_JS)

    purged_excluded_assets: list[dict[str, Any]] = []
    normalized_groups = 0
    kept_existing: list[dict[str, Any]] = []
    for asset in existing:
        reason = excluded_tile_asset(asset)
        if reason and (
            str(asset.get("path", "")).startswith(f"assets/{asset.get('cat')}/review_sync/")
            or str(asset.get("path", "")).startswith(f"assets/{asset.get('cat')}/review_candidates/")
            or str(asset.get("group", "")).lower() in {"blends (new)", "gradient blends - tiles"}
        ):
            purged_excluded_assets.append({
                "name": asset.get("name"),
                "cat": asset.get("cat"),
                "group": asset.get("group"),
                "path": asset.get("path"),
                "reason": reason,
            })
            continue
        before = asset.get("group")
        normalize_synced_group(asset)
        if asset.get("group") != before:
            normalized_groups += 1
        kept_existing.append(asset)
    existing = kept_existing

    existing_keys = {(str(a.get("cat")), str(a.get("name"))) for a in existing}
    existing_paths = {str(a.get("path")) for a in existing}
    existing_sync_identities = {
        (
            str(a.get("cat")),
            str(a.get("stable_id")),
            str(a.get("import_bucket")),
            str(a.get("source_type")),
            str(a.get("source_family")),
        )
        for a in existing
        if str(a.get("path", "")).startswith(f"assets/{a.get('cat')}/review_sync/")
        and a.get("stable_id")
        and a.get("import_bucket")
    }
    existing_candidate_identities = {
        (
            str(a.get("cat")),
            str(a.get("candidate_id")),
            str(a.get("sync_source")),
        )
        for a in existing
        if str(a.get("path", "")).startswith(f"assets/{a.get('cat')}/review_candidates/")
        and a.get("candidate_id")
    }

    rows = manifest_rows(load_manifest(manifest_path), include_conflicts, include_held)
    if include_all_candidates:
        rows.extend(review_candidate_rows(load_manifest(review_manifest_path)))

    added: list[dict[str, Any]] = []
    missing_source: list[dict[str, Any]] = []
    excluded_rows: list[dict[str, Any]] = []
    copied = 0
    renamed = 0
    already_registered = 0

    for row in rows:
        cat = str(row["kind"])
        excluded_reason = excluded_tile_row(row)
        if excluded_reason:
            excluded_rows.append({
                "stable_id": row.get("stable_id"),
                "candidate_id": row.get("candidate_id"),
                "kind": cat,
                "family": row.get("family"),
                "source_type": row.get("source_type"),
                "source": row.get("staged_path") or row.get("source_path"),
                "reason": excluded_reason,
            })
            continue
        if row.get("import_bucket") == "all_candidate":
            sync_identity = (
                cat,
                str(row.get("candidate_id")),
                str(row.get("staged_path") or row.get("source_path")),
            )
            if sync_identity in existing_candidate_identities:
                already_registered += 1
                continue
            stable_id = candidate_name(row)
        else:
            stable_id = slug(str(row.get("stable_id") or "asset"))
            sync_identity = (
                cat,
                str(row.get("stable_id") or stable_id),
                str(row.get("import_bucket")),
                str(row.get("source_type")),
                str(row.get("family")),
            )
            if sync_identity in existing_sync_identities:
                already_registered += 1
                continue

        source = resolve_path(row.get("staged_path") or row.get("source_path"))
        if not source or not source.exists():
            missing_source.append({
                "stable_id": row.get("stable_id"),
                "kind": cat,
                "source": str(source) if source else None,
            })
            continue

        name, was_renamed = unique_name(stable_id, cat, existing_keys, row)
        out_path = destination_for(row, name)
        rel_out = as_biomesketch_rel(out_path)
        if rel_out in existing_paths:
            already_registered += 1
            continue

        out_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, out_path)
        copied += 1

        asset = make_asset(row, name, out_path)
        added.append(asset)
        existing.append(asset)
        existing_keys.add((cat, name))
        existing_paths.add(rel_out)
        if row.get("import_bucket") == "all_candidate":
            existing_candidate_identities.add(sync_identity)
        else:
            existing_sync_identities.add(sync_identity)
        if was_renamed:
            renamed += 1

    write_assets(ASSETS_JS, existing)

    synced_assets = [
        a
        for a in existing
        if (
            str(a.get("path", "")).startswith(f"assets/{a.get('cat')}/review_sync/")
            or str(a.get("path", "")).startswith(f"assets/{a.get('cat')}/review_candidates/")
        )
        and a.get("import_bucket")
    ]

    contact_sheets: list[str] = []
    by_group: dict[str, list[dict[str, Any]]] = {}
    for asset in synced_assets:
        by_group.setdefault(str(asset.get("group") or "synced"), []).append(asset)
    used_sheet_names: set[str] = set()
    for group, group_rows in sorted(by_group.items()):
        base_name = slug(group)
        sheet_name = base_name
        index = 2
        while sheet_name in used_sheet_names:
            sheet_name = f"{base_name}_{index}"
            index += 1
        used_sheet_names.add(sheet_name)
        out_path = REPORT_ROOT / "contact_sheets" / f"{sheet_name}.png"
        make_contact_sheet(group_rows, out_path, group)
        contact_sheets.append(rel(out_path))

    summary = {
        "manifest": rel(manifest_path),
        "assets_js": rel(ASSETS_JS),
        "rows_considered": len(rows),
        "rows_excluded": len(excluded_rows),
        "purged_excluded_assets": len(purged_excluded_assets),
        "normalized_existing_groups": normalized_groups,
        "added": len(added),
        "copied": copied,
        "renamed_for_palette_uniqueness": renamed,
        "already_registered": already_registered,
        "missing_source": len(missing_source),
        "added_by_cat": dict(sorted(Counter(a["cat"] for a in added).items())),
        "added_by_group": dict(sorted(Counter(a["group"] for a in added).items())),
        "total_synced": len(synced_assets),
        "synced_by_cat": dict(sorted(Counter(a["cat"] for a in synced_assets).items())),
        "synced_by_group": dict(sorted(Counter(a["group"] for a in synced_assets).items())),
        "include_all_candidates": include_all_candidates,
        "review_manifest": rel(review_manifest_path),
        "contact_sheets": contact_sheets,
        "missing_sources": missing_source,
        "excluded_rows": excluded_rows,
        "purged_excluded_asset_rows": purged_excluded_assets,
    }

    REPORT_ROOT.mkdir(parents=True, exist_ok=True)
    report_path = REPORT_ROOT / "biomesketch_review_sync_manifest.json"
    report_path.write_text(json.dumps(summary, indent=2), encoding="utf-8")
    summary["report"] = rel(report_path)
    return summary


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, default=DEFAULT_MANIFEST)
    parser.add_argument("--review-manifest", type=Path, default=DEFAULT_REVIEW_MANIFEST)
    parser.add_argument("--include-all-candidates", action="store_true")
    parser.add_argument("--skip-conflicts", action="store_true")
    parser.add_argument("--skip-held", action="store_true")
    args = parser.parse_args()

    summary = sync_assets(
        manifest_path=args.manifest,
        include_conflicts=not args.skip_conflicts,
        include_held=not args.skip_held,
        include_all_candidates=args.include_all_candidates,
        review_manifest_path=args.review_manifest,
    )
    print(json.dumps(summary, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
