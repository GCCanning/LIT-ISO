#!/usr/bin/env python3
"""
Build a review/cull/scale pack for LIT-ISO generated world art.

This is intentionally non-destructive. It scans Unity-generated review assets,
BiomeSketch registered assets, and the external PixelLab harvest folders, then
writes review manifests and contact sheets under TempEvalDryRun/WorldGen/art_review_pack.

The output is a staging gate before anything moves into Claude-owned runtime art
folders such as Assets/Resources.
"""

from __future__ import annotations

import argparse
import os
import json
import math
import re
from collections import Counter, defaultdict
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Any

try:
    from PIL import Image, ImageDraw
except ImportError as exc:
    raise SystemExit("Pillow is required. Use the ComfyUI venv Python on this machine.") from exc


ROOT = Path(__file__).resolve().parents[2]
BIOMESKETCH = ROOT / "Tools" / "BiomeSketch"
BIOMESKETCH_ASSETS = BIOMESKETCH / "assets.js"
RUNTIME_CONTRACT = ROOT / "Assets" / "StreamingAssets" / "worldgen" / "runtime_contract.json"
UNITY_PROPS = ROOT / "Assets" / "Generated" / "Props"
UNITY_TILES = ROOT / "Assets" / "Generated" / "Tiles"
DEFAULT_PIXELART_PROPS = Path(r"C:\Users\garyc\OneDrive\Desktop\PixelArt\Props")
DEFAULT_PIXELART_TILESETS = Path(r"C:\Users\garyc\OneDrive\Desktop\PixelArt\Tilesets")
REVIEW_ROOT = Path(os.environ.get("LITISO_WORLDGEN_REVIEW_ROOT", r"C:\tmp\LitIsoWorldGen"))
DEFAULT_OUT = REVIEW_ROOT / "art_review_pack"


@dataclass
class ImageMetrics:
    width: int
    height: int
    mode: str
    has_alpha: bool
    alpha_pixels: int
    alpha_coverage: float
    bbox: list[int]
    bbox_coverage: float
    corner_white_opaque_ratio: float
    corner_dark_opaque_ratio: float
    unique_rgb_sample: int


@dataclass
class ScalePlan:
    ppu: int
    footprint: str
    height_class: str
    blocks_movement: bool
    anchor: str
    notes: list[str] = field(default_factory=list)


@dataclass
class Candidate:
    id: str
    stable_id: str
    kind: str
    source_type: str
    family: str
    source_path: str
    unity_rel_path: str | None
    worldgen_status: str | None
    worldgen_role: str | None
    flags: list[str]
    review_status: str
    metrics: ImageMetrics
    scale: ScalePlan


def rel(path: Path) -> str:
    try:
        return path.relative_to(ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def safe_name(value: str) -> str:
    value = value.replace("\\", "/")
    value = re.sub(r"[^a-zA-Z0-9_./-]+", "_", value)
    return value.strip("_").lower()


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def load_biomesketch_assets() -> list[dict[str, Any]]:
    if not BIOMESKETCH_ASSETS.exists():
        return []
    text = BIOMESKETCH_ASSETS.read_text(encoding="utf-8-sig")
    match = re.search(r"const\s+ASSETS\s*=\s*(\[.*\]);?\s*$", text, re.S)
    if not match:
        raise SystemExit(f"Could not parse {BIOMESKETCH_ASSETS}")
    return json.loads(match.group(1))


def load_contract_index() -> tuple[dict[str, dict[str, Any]], dict[str, dict[str, Any]]]:
    if not RUNTIME_CONTRACT.exists():
        return {}, {}
    contract = load_json(RUNTIME_CONTRACT)
    tiles = {row["tileId"]: row for row in contract.get("tiles", [])}
    features = {row["featureId"]: row for row in contract.get("features", [])}
    return tiles, features


def image_metrics(path: Path) -> ImageMetrics:
    image = Image.open(path)
    mode = image.mode
    rgba = image.convert("RGBA")
    px = rgba.load()
    width, height = rgba.size
    alpha_pixels = 0
    min_x, min_y = width, height
    max_x, max_y = -1, -1
    rgb_sample: set[tuple[int, int, int]] = set()
    for y in range(height):
        for x in range(width):
            r, g, b, a = px[x, y]
            if (x + y) % 3 == 0:
                rgb_sample.add((r, g, b))
            if a > 0:
                alpha_pixels += 1
                min_x = min(min_x, x)
                min_y = min(min_y, y)
                max_x = max(max_x, x)
                max_y = max(max_y, y)

    if alpha_pixels:
        bbox = [min_x, min_y, max_x - min_x + 1, max_y - min_y + 1]
    else:
        bbox = [0, 0, 0, 0]

    corner_pixels: list[tuple[int, int, int, int]] = []
    sample = max(2, min(8, width // 4, height // 4))
    for sx, sy in ((0, 0), (width - sample, 0), (0, height - sample), (width - sample, height - sample)):
        for y in range(sy, sy + sample):
            for x in range(sx, sx + sample):
                corner_pixels.append(px[x, y])
    opaque_corners = [p for p in corner_pixels if p[3] > 240]
    white = [p for p in opaque_corners if p[0] > 235 and p[1] > 235 and p[2] > 235]
    dark = [p for p in opaque_corners if p[0] < 18 and p[1] < 18 and p[2] < 18]

    total = width * height
    return ImageMetrics(
        width=width,
        height=height,
        mode=mode,
        has_alpha=(mode in {"RGBA", "LA"} or "transparency" in image.info),
        alpha_pixels=alpha_pixels,
        alpha_coverage=round(alpha_pixels / total, 4) if total else 0.0,
        bbox=bbox,
        bbox_coverage=round((bbox[2] * bbox[3]) / total, 4) if total else 0.0,
        corner_white_opaque_ratio=round(len(white) / max(1, len(corner_pixels)), 4),
        corner_dark_opaque_ratio=round(len(dark) / max(1, len(corner_pixels)), 4),
        unique_rgb_sample=len(rgb_sample),
    )


def infer_kind_from_path(path: Path, source_type: str, registry_kind: str | None = None) -> str:
    if registry_kind in {"tile", "prop", "decor"}:
        return "prop" if registry_kind == "decor" else registry_kind
    text = path.as_posix().lower()
    if "/tile/" in text or "tilesets" in text or "tiles" in text:
        return "tile"
    return "prop"


def infer_scale(kind: str, stable_id: str, family: str, metrics: ImageMetrics, registry: dict[str, Any] | None) -> ScalePlan:
    if registry:
        ppu = int(registry.get("ppu") or 32)
        fp = str(registry.get("fp") or "1x1")
        if kind == "tile":
            return ScalePlan(ppu=ppu, footprint="1x1", height_class="terrain", blocks_movement=False,
                             anchor="tilemap cell", notes=["BiomeSketch metadata"])
        return ScalePlan(ppu=ppu, footprint=fp, height_class=infer_height_class(stable_id, family),
                         blocks_movement=should_block(stable_id, family), anchor="bottom-center",
                         notes=["BiomeSketch metadata"])

    name = stable_id.lower()
    fam = family.lower()
    bbox_h = max(1, metrics.bbox[3])

    if kind == "tile":
        notes = ["terrain tile", "promote only after visual approval"]
        if metrics.width == 256 and metrics.height >= 256:
            notes.append("large authored dungeon/interior tile canvas")
        return ScalePlan(ppu=32, footprint="1x1", height_class="terrain", blocks_movement=False,
                         anchor="tilemap cell", notes=notes)

    desired_units = 1.0
    footprint = "1x1"
    blocks = True
    notes: list[str] = []
    height_class = infer_height_class(name, fam)

    if re.search(r"_r([123])$", name):
        rank = int(name[-1])
        desired_units = {1: 2.3, 2: 3.0, 3: 3.8}[rank]
        footprint = {1: "2x2", 2: "3x3", 3: "4x4"}[rank]
        notes.extend(["building rank footprint", "manual door-cell alignment required"])
    elif "tree" in name or "pine" in name or "oak" in name or "willow" in name:
        desired_units = 2.8
        footprint = "2x1" if any(k in name for k in ("large", "deep", "willow", "v2")) else "1x1"
    elif "tent" in name:
        desired_units = 1.8
        footprint = "2x2"
    elif any(k in name for k in ("station", "table", "furnace", "anvil", "loom", "bench", "rack", "bar_counter")):
        desired_units = 1.25
        footprint = "2x1"
    elif any(k in name for k in ("torch", "lantern", "brazier", "candelabra")):
        desired_units = 1.45
        footprint = "1x1"
        notes.append("light-source candidate")
    elif any(k in name for k in ("campfire", "glowbug", "wisp")):
        desired_units = 0.8
        footprint = "1x1"
        blocks = "campfire" in name
        notes.append("ambient/light-source candidate")
    elif any(k in name for k in ("rock", "ore", "vein", "boulder")):
        desired_units = 0.75
    elif any(k in name for k in ("bush", "flower", "grass", "mushroom", "tuft")):
        desired_units = 0.65
        blocks = False
    elif any(k in name for k in ("bedroll", "rug", "floor")):
        desired_units = 0.45
        blocks = False

    ppu = max(24, min(240, round(bbox_h / desired_units)))
    return ScalePlan(ppu=ppu, footprint=footprint, height_class=height_class,
                     blocks_movement=blocks, anchor="bottom-center", notes=notes)


def infer_height_class(stable_id: str, family: str) -> str:
    text = f"{stable_id} {family}".lower()
    if any(k in text for k in ("building", "tree", "pine", "oak", "willow")):
        return "tall"
    if any(k in text for k in ("torch", "lantern", "brazier", "station", "table", "furnace", "anvil", "tent")):
        return "mid"
    if any(k in text for k in ("bush", "rock", "ore", "stump", "log", "chest")):
        return "low"
    if any(k in text for k in ("glowbug", "wisp", "flower", "grass", "tuft")):
        return "ambient"
    return "mid"


def should_block(stable_id: str, family: str) -> bool:
    text = f"{stable_id} {family}".lower()
    if any(k in text for k in ("flower", "grass", "tuft", "glowbug", "wisp", "rug", "bedroll")):
        return False
    return True


def flags_for(kind: str, metrics: ImageMetrics, path: Path) -> list[str]:
    flags: list[str] = []
    name = path.name.lower()
    if metrics.alpha_pixels == 0:
        flags.append("blank_alpha")
    if not metrics.has_alpha and kind == "prop":
        flags.append("missing_alpha")
    if metrics.alpha_coverage > 0.985 and kind == "prop":
        flags.append("opaque_prop_canvas")
    if metrics.corner_white_opaque_ratio > 0.4 and kind == "prop":
        flags.append("white_corner_background")
    if metrics.corner_dark_opaque_ratio > 0.4 and kind == "prop":
        flags.append("black_corner_background")
    if metrics.bbox_coverage < 0.015:
        flags.append("tiny_foreground")
    if kind == "tile":
        allowed = {(32, 32), (64, 64), (128, 64), (128, 128), (256, 512)}
        if (metrics.width, metrics.height) not in allowed:
            flags.append("tile_size_review")
        if metrics.width >= 256 or metrics.height >= 256:
            flags.append("large_tile_canvas")
    else:
        if metrics.width > 512 or metrics.height > 512:
            flags.append("large_prop_canvas")
        if metrics.width < 24 or metrics.height < 24:
            flags.append("small_prop_canvas")
    if name.startswith("_contact"):
        flags.append("contact_sheet_not_candidate")
    return flags


def review_status(flags: list[str]) -> str:
    hard = {"blank_alpha", "white_corner_background", "black_corner_background", "contact_sheet_not_candidate"}
    if hard.intersection(flags):
        return "reject"
    if flags:
        return "review"
    return "candidate"


def candidate_from_path(
    path: Path,
    source_type: str,
    family: str,
    stable_id: str,
    kind: str,
    registry: dict[str, Any] | None,
    contract_tiles: dict[str, dict[str, Any]],
    contract_features: dict[str, dict[str, Any]],
) -> Candidate | None:
    try:
        metrics = image_metrics(path)
    except Exception as exc:  # pragma: no cover - reported in caller manifest.
        print(f"skipping unreadable image {path}: {exc}")
        return None

    flags = flags_for(kind, metrics, path)
    scale = infer_scale(kind, stable_id, family, metrics, registry)
    if kind == "tile":
        contract = contract_tiles.get(stable_id)
    else:
        contract = contract_features.get(stable_id)
    worldgen_status = contract.get("status") if contract else None
    worldgen_role = contract.get("role") if contract else None

    unity_rel: str | None = None
    if source_type.startswith("unity_generated"):
        unity_rel = rel(path)
    elif registry:
        unity_rel = registry.get("path")

    return Candidate(
        id=safe_name(f"{source_type}/{family}/{stable_id}/{path.stem}"),
        stable_id=stable_id,
        kind=kind,
        source_type=source_type,
        family=family,
        source_path=rel(path),
        unity_rel_path=unity_rel,
        worldgen_status=worldgen_status,
        worldgen_role=worldgen_role,
        flags=flags,
        review_status=review_status(flags),
        metrics=metrics,
        scale=scale,
    )


def scan_unity_generated(folder: Path, kind: str, source_type: str, contract_tiles: dict[str, dict[str, Any]],
                         contract_features: dict[str, dict[str, Any]]) -> list[Candidate]:
    rows: list[Candidate] = []
    if not folder.exists():
        return rows
    for path in sorted(folder.rglob("*.png")):
        family = path.parent.name
        stable_id = path.stem
        candidate = candidate_from_path(path, source_type, family, stable_id, kind, None, contract_tiles, contract_features)
        if candidate:
            rows.append(candidate)
    return rows


def scan_biomesketch(contract_tiles: dict[str, dict[str, Any]], contract_features: dict[str, dict[str, Any]]) -> list[Candidate]:
    rows: list[Candidate] = []
    for asset in load_biomesketch_assets():
        registry_kind = asset.get("cat")
        if registry_kind not in {"tile", "prop", "decor"}:
            continue
        kind = infer_kind_from_path(Path(asset.get("path", "")), "biomesketch", registry_kind)
        source = BIOMESKETCH / asset["path"]
        if not source.exists():
            continue
        family = asset.get("group") or f"biomesketch_{kind}"
        stable_id = asset["name"]
        candidate = candidate_from_path(source, "biomesketch", family, stable_id, kind, asset, contract_tiles, contract_features)
        if candidate:
            rows.append(candidate)
    return rows


def scan_pixelart_props(root: Path, contract_tiles: dict[str, dict[str, Any]], contract_features: dict[str, dict[str, Any]]) -> list[Candidate]:
    rows: list[Candidate] = []
    if not root.exists():
        return rows
    for path in sorted(root.rglob("*.png")):
        if path.name.startswith("_"):
            continue
        if ".orig" in path.name.lower() or path.name.lower().endswith(".bak.png"):
            continue
        try:
            rel_parts = path.relative_to(root).parts
        except ValueError:
            continue
        if len(rel_parts) < 3:
            continue
        family, stable_id = rel_parts[0], rel_parts[1]
        candidate = candidate_from_path(path, "pixellab_props", family, stable_id, "prop", None, contract_tiles, contract_features)
        if candidate:
            rows.append(candidate)
    return rows


def scan_pixelart_tilesets(root: Path, contract_tiles: dict[str, dict[str, Any]],
                           contract_features: dict[str, dict[str, Any]]) -> list[Candidate]:
    rows: list[Candidate] = []
    if not root.exists():
        return rows
    for path in sorted(root.rglob("*.png")):
        if path.name.startswith("_"):
            continue
        try:
            rel_parts = path.relative_to(root).parts
        except ValueError:
            continue
        if len(rel_parts) < 2:
            continue
        family = rel_parts[0]
        tile_number = re.sub(r"^tile_", "", path.stem)
        stable_id = f"{safe_name(family)}_{int(tile_number):02d}" if tile_number.isdigit() else f"{safe_name(family)}_{safe_name(path.stem)}"
        candidate = candidate_from_path(path, "pixellab_tilesets", family, stable_id, "tile", None, contract_tiles, contract_features)
        if candidate:
            rows.append(candidate)
    return rows


def summarize(candidates: list[Candidate]) -> dict[str, Any]:
    by_source = Counter(c.source_type for c in candidates)
    by_kind = Counter(c.kind for c in candidates)
    by_status = Counter(c.review_status for c in candidates)
    by_family = Counter(f"{c.source_type}/{c.family}" for c in candidates)
    by_flag: Counter[str] = Counter()
    for candidate in candidates:
        by_flag.update(candidate.flags or ["no_flags"])
    return {
        "total": len(candidates),
        "by_source": dict(sorted(by_source.items())),
        "by_kind": dict(sorted(by_kind.items())),
        "by_review_status": dict(sorted(by_status.items())),
        "top_families": dict(by_family.most_common(40)),
        "flags": dict(sorted(by_flag.items())),
    }


def write_contact_sheets(candidates: list[Candidate], out_dir: Path, max_per_sheet: int) -> list[str]:
    sheets: list[str] = []
    grouped: dict[str, list[Candidate]] = defaultdict(list)
    for candidate in candidates:
        if "contact_sheet_not_candidate" in candidate.flags:
            continue
        grouped[f"{candidate.source_type}__{safe_name(candidate.family)}"].append(candidate)

    out_dir.mkdir(parents=True, exist_ok=True)
    for group, rows in sorted(grouped.items()):
        rows = rows[:max_per_sheet]
        if not rows:
            continue
        sheet_path = out_dir / f"{safe_name(group)}.png"
        make_contact_sheet(rows, sheet_path)
        sheets.append(rel(sheet_path))
    return sheets


def make_contact_sheet(rows: list[Candidate], out_path: Path) -> None:
    thumbs: list[tuple[Candidate, Image.Image]] = []
    for row in rows:
        image = Image.open(ROOT / row.source_path if not Path(row.source_path).is_absolute() else Path(row.source_path)).convert("RGBA")
        thumbs.append((row, image))

    thumb_w, thumb_h = 96, 96
    cell_w, cell_h = 172, 142
    cols = min(6, len(thumbs))
    rows_count = math.ceil(len(thumbs) / cols)
    sheet = Image.new("RGBA", (cols * cell_w, rows_count * cell_h), (34, 37, 46, 255))
    draw = ImageDraw.Draw(sheet)
    border = {
        "candidate": (72, 170, 100, 255),
        "review": (225, 170, 60, 255),
        "reject": (215, 70, 70, 255),
    }
    for index, (candidate, image) in enumerate(thumbs):
        x = (index % cols) * cell_w
        y = (index // cols) * cell_h
        color = border.get(candidate.review_status, (120, 120, 120, 255))
        draw.rectangle((x + 3, y + 3, x + cell_w - 4, y + cell_h - 4), outline=color, width=2)
        scale = min(thumb_w / image.width, thumb_h / image.height, 4.0)
        size = (max(1, round(image.width * scale)), max(1, round(image.height * scale)))
        resized = image.resize(size, Image.Resampling.NEAREST)
        sheet.paste(resized, (x + (cell_w - size[0]) // 2, y + 8), resized)
        label = candidate.stable_id[:24]
        sub = f"{candidate.review_status} ppu {candidate.scale.ppu} {candidate.scale.footprint}"
        flag = ",".join(candidate.flags[:2]) if candidate.flags else "ok"
        draw.text((x + 8, y + 106), label, fill=(245, 229, 190, 255))
        draw.text((x + 8, y + 119), sub[:28], fill=(190, 202, 220, 255))
        draw.text((x + 8, y + 132), flag[:28], fill=(150, 160, 176, 255))
    out_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out_path)


def write_markdown(report: dict[str, Any], path: Path) -> None:
    summary = report["summary"]
    lines = [
        "# LIT-ISO Art Review Pack",
        "",
        "Non-destructive review output. Nothing here imports art into runtime Resources.",
        "",
        "## Summary",
        "",
        f"- Total candidates: {summary['total']}",
        f"- By source: `{summary['by_source']}`",
        f"- By kind: `{summary['by_kind']}`",
        f"- By review status: `{summary['by_review_status']}`",
        f"- Flags: `{summary['flags']}`",
        "",
        "## Promotion Rules",
        "",
        "- `candidate`: structurally clean enough for human art review.",
        "- `review`: usable only after manual inspection; usually scale/size/alpha needs attention.",
        "- `reject`: do not promote without repair; likely blank, contact sheet, or visible baked background.",
        "",
        "## Scale Rules Used",
        "",
        "- Tiles: `ppu=32`, footprint `1x1`, tilemap-cell anchor.",
        "- Trees: tall, usually `1x1` or `2x1`, bottom-center anchor.",
        "- Building ranks: r1=`2x2`, r2=`3x3`, r3=`4x4`; door-cell alignment must be manually verified.",
        "- Stations/workbenches: mostly `2x1`, mid-height.",
        "- Bushes/flowers/grass: usually non-blocking `1x1` low/ambient props.",
        "- Lights/ambient effects: tagged by notes for future glow/particle hooks.",
        "",
        "## Top Families",
        "",
    ]
    for family, count in summary["top_families"].items():
        lines.append(f"- {family}: {count}")

    lines.extend([
        "",
        "## High-Risk Items",
        "",
    ])
    risky = [c for c in report["candidates"] if c["review_status"] != "candidate"][:120]
    if not risky:
        lines.append("- None")
    for item in risky:
        lines.append(
            f"- `{item['stable_id']}` [{item['source_type']}/{item['family']}]: "
            f"{item['review_status']} | flags `{', '.join(item['flags']) or 'none'}`"
        )

    lines.extend([
        "",
        "## Contact Sheets",
        "",
    ])
    for sheet in report["contact_sheets"][:120]:
        lines.append(f"- `{sheet}`")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    parser.add_argument("--pixelart-props-root", type=Path, default=DEFAULT_PIXELART_PROPS)
    parser.add_argument("--pixelart-tilesets-root", type=Path, default=DEFAULT_PIXELART_TILESETS)
    parser.add_argument("--max-per-family", type=int, default=80)
    args = parser.parse_args()

    contract_tiles, contract_features = load_contract_index()
    candidates: list[Candidate] = []
    candidates.extend(scan_unity_generated(UNITY_PROPS, "prop", "unity_generated_props", contract_tiles, contract_features))
    candidates.extend(scan_unity_generated(UNITY_TILES, "tile", "unity_generated_tiles", contract_tiles, contract_features))
    candidates.extend(scan_biomesketch(contract_tiles, contract_features))
    candidates.extend(scan_pixelart_props(args.pixelart_props_root, contract_tiles, contract_features))
    candidates.extend(scan_pixelart_tilesets(args.pixelart_tilesets_root, contract_tiles, contract_features))

    args.out.mkdir(parents=True, exist_ok=True)
    contact_sheets = write_contact_sheets(candidates, args.out / "contact_sheets", args.max_per_family)
    report = {
        "schema": "litiso.art_review_pack.v1",
        "sources": {
            "unity_generated_props": rel(UNITY_PROPS),
            "unity_generated_tiles": rel(UNITY_TILES),
            "biomesketch_assets": rel(BIOMESKETCH_ASSETS),
            "pixellab_props": args.pixelart_props_root.as_posix(),
            "pixellab_tilesets": args.pixelart_tilesets_root.as_posix(),
        },
        "summary": summarize(candidates),
        "contact_sheets": contact_sheets,
        "candidates": [asdict(candidate) for candidate in candidates],
    }
    manifest = args.out / "art_review_manifest.json"
    markdown = args.out / "art_review_summary.md"
    manifest.write_text(json.dumps(report, indent=2), encoding="utf-8")
    write_markdown(report, markdown)
    print(json.dumps({
        "manifest": rel(manifest),
        "summary": rel(markdown),
        "contact_sheets": len(contact_sheets),
        "candidates": len(candidates),
        "status": report["summary"]["by_review_status"],
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
