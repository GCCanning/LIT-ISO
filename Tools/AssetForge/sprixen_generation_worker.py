#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import os
import time
from datetime import datetime, timezone
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode, urlparse
from urllib.request import Request, urlopen

try:
    from PIL import Image
except Exception as exc:  # pragma: no cover - surfaced in manifest.
    Image = None
    PIL_IMPORT_ERROR = exc
else:
    PIL_IMPORT_ERROR = None


SUPPORTED_MODES = {"tile", "prop", "item", "character", "npc", "mob"}


def now_utc() -> str:
    return datetime.now(timezone.utc).isoformat()


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def safe_name(value: object, default: str = "asset") -> str:
    text = str(value or default).strip()
    keep = [char if char.isalnum() or char in "_.-" else "_" for char in text]
    cleaned = "".join(keep).strip("._")
    return cleaned or default


def get_value(data: dict, *names: str, default=None):
    for name in names:
        if isinstance(data, dict) and name in data and data[name] is not None:
            return data[name]
    return default


def rel_path(project_root: Path, path: Path) -> str:
    try:
        return path.resolve().relative_to(project_root.resolve()).as_posix()
    except ValueError:
        return str(path)


def seed_from_text(seed_value: object, job_name: str, index: int) -> int:
    text = str(seed_value or "random").strip()
    if text and text.lower() != "random":
        try:
            return int(text) + index
        except ValueError:
            pass
    digest = hashlib.sha256(f"{job_name}:{text}:{index}".encode("utf-8")).hexdigest()
    return int(digest[:12], 16) % 2147483647


def request_json(base_url: str, path: str, api_key: str, payload: dict | None = None, timeout: int = 30) -> dict:
    data = None
    headers = {"Authorization": f"Bearer {api_key}", "Accept": "application/json"}
    if payload is not None:
        data = json.dumps(payload).encode("utf-8")
        headers["Content-Type"] = "application/json"
    req = Request(f"{base_url.rstrip('/')}{path}", data=data, headers=headers)
    try:
        with urlopen(req, timeout=timeout) as response:
            return json.loads(response.read().decode("utf-8"))
    except HTTPError as exc:
        body = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"Sprixen HTTP {exc.code}: {body}") from exc
    except URLError as exc:
        raise RuntimeError(f"Sprixen request failed: {exc.reason}") from exc


def absolutize_url(base_url: str, url: str) -> str:
    if url.startswith("http://") or url.startswith("https://"):
        return url
    parsed = urlparse(base_url)
    if url.startswith("/") and parsed.scheme and parsed.netloc:
        return f"{parsed.scheme}://{parsed.netloc}{url}"
    return url


def request_bytes(url: str, api_key: str, base_url: str, timeout: int = 60) -> bytes:
    url = absolutize_url(base_url, url)
    headers = {"Accept": "image/png,*/*"}
    host = urlparse(url).netloc.lower()
    if "sprixen.com" in host:
        headers["Authorization"] = f"Bearer {api_key}"
    req = Request(url, headers=headers)
    with urlopen(req, timeout=timeout) as response:
        return response.read()


def wait_for_job(base_url: str, path: str, api_key: str, timeout_seconds: int, poll_seconds: float = 2.0) -> dict:
    start = time.time()
    while time.time() - start < timeout_seconds:
        payload = request_json(base_url, path, api_key, timeout=30)
        status = str(payload.get("status", "")).lower()
        if status in {"completed", "complete", "succeeded", "success"}:
            return payload
        if status in {"failed", "error", "cancelled", "canceled"}:
            raise RuntimeError(f"Sprixen job failed: {payload}")
        time.sleep(poll_seconds)
    raise TimeoutError(f"Timed out waiting for Sprixen job at {path}")


def category_for_mode(mode: str) -> str:
    if mode == "tile":
        return "terrain"
    if mode == "prop":
        return "decoration"
    if mode == "item":
        return "item"
    return mode


def sprixen_type_for_mode(mode: str) -> str:
    if mode in {"character", "npc", "mob"}:
        return "character"
    if mode == "item":
        return "sprite"
    if mode == "prop":
        return "sprite"
    if mode == "tile":
        return "sprite"
    return "sprite"


def mode_prompt(mode: str, prompt: str, spec: dict | None = None) -> str:
    requested = prompt.strip().replace('"', "'")
    shared = "original LIT-ISO cozy isometric pixel art, crisp square pixels, transparent background, no text, no watermark"
    spec = spec or {}
    tile_shape = str(get_value(spec, "tile_shape", "tileShape", default="flat_top") or "flat_top").lower()
    tile_contract = "single 2:1 isometric diamond terrain tile only, terrain surface only, no object on top, no tree, no bush, no rock, seamless edge-compatible, transparent corners, no hard border frame"
    if tile_shape in {"raised_height_block", "height_block", "raised", "height"}:
        tile_contract = "single 2:1 isometric raised terrain height tile block only, visible top material and side faces, no object on top, no tree, no bush, no rock, transparent background, no hard border frame, clean tile edges, height level 1"
    contracts = {
        "tile": tile_contract,
        "prop": "single standalone isometric map prop only, bottom-center anchor, no baked ground tile, no floor, no diorama, full object visible",
        "item": "single centered standalone inventory item sprite only, readable at 16px and 32px, no hand, no badge, no UI frame, no circular backing",
        "character": "single full-body isometric character sprite only, bottom-center foot anchor, full body visible, readable face, no floor, no scene, no duplicate",
        "npc": "single full-body isometric NPC sprite only, bottom-center foot anchor, full body visible, readable face, no floor, no scene, no duplicate",
        "mob": "single full-body isometric creature sprite only, bottom-center foot anchor, full body visible, readable silhouette, no floor, no scene, no duplicate",
    }
    return f"{shared}, {contracts.get(mode, '')}, subject: {requested}".strip(", ")


def mode_negative(mode: str, negative: str) -> str:
    base = negative.strip()
    extras = {
        "tile": "tree, bush, prop, rock, flower, log, character, wall, cube, raised block, square floor, scene, background, border, frame",
        "prop": "floor, ground tile, base plate, pedestal, room, diorama, miniature scene, multiple objects, collection, wall, background, badge, frame",
        "item": "character, hand, person, badge, emblem, UI slot, circular frame, square frame, label, text, room, floor, pedestal, background",
        "character": "floor, base, ground patch, portrait, bust, cropped body, duplicate character, background, scene, 3d render, realistic",
        "npc": "floor, base, ground patch, portrait, bust, cropped body, duplicate character, background, scene, 3d render, realistic",
        "mob": "floor, base, ground patch, portrait, bust, cropped body, duplicate creature, background, scene, 3d render, realistic",
    }
    extra = extras.get(mode, "")
    return f"{base}, {extra}" if base else extra


def tile_shape_from_spec(spec: dict | None) -> str:
    return str(get_value(spec or {}, "tile_shape", "tileShape", default="flat_top") or "flat_top").lower()


def parse_square_resolution(value: str, default: int = 64) -> int:
    text = str(value or "").lower().replace(" ", "")
    parts = text.split("x")
    if len(parts) == 2 and parts[0].isdigit() and parts[1].isdigit() and parts[0] == parts[1]:
        size = int(parts[0])
        if size in {32, 64, 96, 128}:
            return size
    return default


def color_distance(a: tuple[int, int, int], b: tuple[int, int, int]) -> float:
    return sum((int(a[i]) - int(b[i])) ** 2 for i in range(3)) ** 0.5


def remove_edge_background(image):
    rgba = image.convert("RGBA")
    width, height = rgba.size
    pixels = rgba.load()
    corners = [
        pixels[0, 0][:3],
        pixels[width - 1, 0][:3],
        pixels[0, height - 1][:3],
        pixels[width - 1, height - 1][:3],
    ]
    bg = tuple(sorted(c[i] for c in corners)[len(corners) // 2] for i in range(3))
    tolerance = 44
    seen = set()
    stack = []
    for x in range(width):
        stack.extend(((x, 0), (x, height - 1)))
    for y in range(height):
        stack.extend(((0, y), (width - 1, y)))
    while stack:
        x, y = stack.pop()
        if (x, y) in seen or x < 0 or y < 0 or x >= width or y >= height:
            continue
        seen.add((x, y))
        r, g, b, a = pixels[x, y]
        if a < 12 or color_distance((r, g, b), bg) <= tolerance:
            pixels[x, y] = (r, g, b, 0)
            stack.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    return rgba


def alpha_bbox(image) -> tuple[int, int, int, int] | None:
    return image.getchannel("A").getbbox()


def fit_foreground(image, size: tuple[int, int], max_fill: float, anchor: str):
    target_w, target_h = size
    cleaned = remove_edge_background(image)
    box = alpha_bbox(cleaned)
    if not box:
        return Image.new("RGBA", size, (0, 0, 0, 0))
    cropped = cleaned.crop(box)
    max_w = max(1, int(target_w * max_fill))
    max_h = max(1, int(target_h * max_fill))
    scale = min(max_w / cropped.width, max_h / cropped.height)
    resized = cropped.resize((max(1, int(cropped.width * scale)), max(1, int(cropped.height * scale))), Image.Resampling.NEAREST)
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    x = (target_w - resized.width) // 2
    y = target_h - resized.height - max(1, target_h // 16) if anchor == "bottom" else (target_h - resized.height) // 2
    canvas.alpha_composite(resized, (x, max(0, y)))
    return canvas


def snap_pixels(image, max_colors: int):
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A").point(lambda value: 255 if value >= 80 else 0)
    rgba.putalpha(alpha)
    if max_colors > 0:
        rgba = rgba.quantize(colors=max_colors, method=Image.Quantize.FASTOCTREE).convert("RGBA")
        rgba.putalpha(alpha)
    pixels = rgba.load()
    width, height = rgba.size
    for y in range(height):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            pixels[x, y] = (r, g, b, 255) if a >= 255 else (0, 0, 0, 0)
    return rgba


def make_tile(image, size: int = 64):
    fitted = fit_foreground(image, (size, size), 1.0, "center").resize((size, size), Image.Resampling.NEAREST)
    pixels = fitted.load()
    cx = (size - 1) / 2.0
    cy = (size - 1) / 2.0
    half_width = size / 2.0
    half_height = size / 4.0
    for y in range(size):
        for x in range(size):
            if abs(x - cx) / half_width + abs(y - cy) / half_height > 1.0:
                r, g, b, _ = pixels[x, y]
                pixels[x, y] = (r, g, b, 0)
    return fitted


def make_raised_tile(image, size: int = 64):
    return fit_foreground(image, (size, size), 0.92, "center").resize((size, size), Image.Resampling.NEAREST)


def postprocess(mode: str, raw_path: Path, cleaned_path: Path, tile_size: int = 64, tile_shape: str = "flat_top") -> tuple[int, int]:
    if Image is None:
        raise RuntimeError(f"Pillow is not available: {PIL_IMPORT_ERROR}")
    image = Image.open(raw_path).convert("RGBA")
    if mode == "tile":
        if tile_shape in {"raised_height_block", "height_block", "raised", "height"}:
            result = snap_pixels(make_raised_tile(image, tile_size), 28 if tile_size >= 64 else 20)
        else:
            result = snap_pixels(make_tile(image, tile_size), 24 if tile_size >= 64 else 16)
    elif mode == "prop":
        result = snap_pixels(fit_foreground(image, (128, 128), 0.88, "bottom"), 32)
    elif mode == "item":
        result = snap_pixels(fit_foreground(image, (64, 64), 0.82, "center"), 24)
    elif mode in {"character", "npc", "mob"}:
        result = snap_pixels(fit_foreground(image, (128, 128), 0.88, "bottom"), 48)
    else:
        raise ValueError(f"Unsupported Sprixen mode: {mode}")
    cleaned_path.parent.mkdir(parents=True, exist_ok=True)
    result.save(cleaned_path)
    return result.size


def generation_body(args, request: dict, mode: str, prompt: str, negative: str, variant_count: int) -> dict:
    style = get_value(request, "style", default={}) or {}
    project_id = args.project_id or get_value(style, "sprixen_project_id", "project_id", default="")
    body = {
        "prompt": prompt,
        "type": sprixen_type_for_mode(mode),
        "variantCount": variant_count,
        "resolution": args.resolution,
        "pixelPerfect": args.pixel_perfect,
        "noBase": True,
        "skipBgRemoval": False,
    }
    if project_id:
        body["projectId"] = project_id
    reference = str(get_value(request, "reference_image_url", "referenceImageUrl", default=args.reference_image_url or "") or "")
    if reference:
        body["referenceImageUrl"] = reference
    animation = get_value(request, "animation", default={}) or {}
    animation_name = str(get_value(animation, "name", default="none")).lower()
    frame_count = int(get_value(animation, "frame_count", "frameCount", default=1) or 1)
    if args.chain_animation and animation_name not in {"", "none", "full_set"}:
        body["chainAnimation"] = {
            "type": animation_name,
            "frameCount": max(2, min(12, frame_count)),
            "fps": int(get_value(animation, "fps", default=8) or 8),
        }
    return body


def reusable_result_urls(manifest_path: Path, job_name: str) -> tuple[str, list[str]] | None:
    if not manifest_path.exists():
        return None
    try:
        previous = read_json(manifest_path)
    except Exception:
        return None
    if previous.get("job_name") != job_name:
        return None
    generation_id = str(previous.get("sprixen_generation_id") or "")
    result_urls = []
    for output in previous.get("outputs") or []:
        result_url = str(output.get("result_url") or "")
        if result_url:
            result_urls.append(result_url)
    if generation_id and result_urls:
        return generation_id, result_urls
    return None


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", required=True)
    parser.add_argument("--request-path", required=True)
    parser.add_argument("--raw-output-root", required=True)
    parser.add_argument("--clean-output-root", required=True)
    parser.add_argument("--manifest-path", required=True)
    parser.add_argument("--api-key", default="")
    parser.add_argument("--base-url", default="https://api.sprixen.com/v1")
    parser.add_argument("--project-id", default="")
    parser.add_argument("--reference-image-url", default="")
    parser.add_argument("--resolution", default="64x64")
    parser.add_argument("--pixel-perfect", default="on")
    parser.add_argument("--timeout-seconds", type=int, default=180)
    parser.add_argument("--chain-animation", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()
    args.api_key = args.api_key or os.environ.get("SPRIXEN_API_KEY", "")

    project_root = Path(args.project_root).resolve()
    request_path = Path(args.request_path).resolve()
    raw_root = Path(args.raw_output_root).resolve()
    clean_root = Path(args.clean_output_root).resolve()
    manifest_path = Path(args.manifest_path).resolve()
    request = read_json(request_path)
    job_name = safe_name(get_value(request, "job_name", "jobName", default="asset_job"))
    mode = str(get_value(request, "asset_mode", "assetMode", default="tile")).lower()
    spec = get_value(request, "asset_spec", "assetSpec", default={}) or {}
    biome = safe_name(get_value(spec, "biome", default="shared"), "shared")
    prompt = str(get_value(request, "prompt", default="")).strip()
    negative = str(get_value(request, "negative_prompt", "negativePrompt", default="")).strip()
    batch_count = max(1, min(3, int(get_value(request, "batch_count", "batchCount", default=1))))
    tile_size = parse_square_resolution(args.resolution, 32 if mode == "tile" else 64)
    tile_shape = tile_shape_from_spec(spec)

    manifest = {
        "schema": "lit_iso.asset_forge.sprixen_generation_manifest.v1",
        "status": "planned" if args.dry_run else "running",
        "created_utc": now_utc(),
        "job_name": job_name,
        "asset_mode": mode,
        "request_path": rel_path(project_root, request_path),
        "source_kind": "sprixen_api",
        "settings": {
            "base_url": args.base_url.rstrip("/"),
            "project_id": args.project_id,
            "resolution": args.resolution,
            "pixel_perfect": args.pixel_perfect,
            "tile_postprocess_size": tile_size if mode == "tile" else None,
            "tile_shape": tile_shape if mode == "tile" else None,
            "variant_count": batch_count,
            "chain_animation": bool(args.chain_animation),
        },
        "outputs": [],
    }

    if mode not in SUPPORTED_MODES:
        manifest["status"] = "unsupported_mode"
        manifest["error"] = f"Sprixen worker currently supports only: {', '.join(sorted(SUPPORTED_MODES))}"
        write_json(manifest_path, manifest)
        print(manifest_path)
        return 2

    positive = mode_prompt(mode, prompt, spec)
    negative_text = mode_negative(mode, negative)
    body = generation_body(args, request, mode, positive, negative_text, batch_count)
    manifest["request_body_preview"] = {key: value for key, value in body.items() if key != "referenceImageUrl"}
    if "referenceImageUrl" in body:
        manifest["request_body_preview"]["referenceImageUrl"] = "[configured]"

    if args.dry_run:
        for index in range(batch_count):
            manifest["outputs"].append({
                "index": index + 1,
                "name": f"{job_name}_v{index + 1}.png",
                "category": category_for_mode(mode),
                "biome": biome,
                "seed": seed_from_text(get_value(request, "seed", default="random"), job_name, index),
                "prompt": positive,
                "negative_prompt": negative_text,
                "status": "planned",
            })
        write_json(manifest_path, manifest)
        print(manifest_path)
        return 0

    if not args.api_key:
        manifest["status"] = "missing_api_key"
        manifest["error"] = "Set SPRIXEN_API_KEY or sprixen.api_key in local config before running the Sprixen worker."
        write_json(manifest_path, manifest)
        print(manifest_path)
        return 3
    if Image is None:
        manifest["status"] = "failed"
        manifest["error"] = f"Pillow is not available: {PIL_IMPORT_ERROR}"
        write_json(manifest_path, manifest)
        print(manifest_path)
        return 1

    raw_root.mkdir(parents=True, exist_ok=True)
    clean_root.mkdir(parents=True, exist_ok=True)
    failures = []
    try:
        reusable = reusable_result_urls(manifest_path, job_name)
        if reusable:
            generation_id, result_urls = reusable
            manifest["reused_existing_results"] = True
        else:
            created = request_json(args.base_url, "/generations", args.api_key, payload=body, timeout=30)
            generation_id = created.get("id")
            if not generation_id:
                raise RuntimeError(f"Sprixen did not return a generation id: {created}")
            complete = wait_for_job(args.base_url, f"/generations/{generation_id}", args.api_key, args.timeout_seconds)
            result_urls = complete.get("resultUrls") or complete.get("result_urls") or []
            if not result_urls:
                raise RuntimeError(f"Sprixen completed without resultUrls: {complete}")
        manifest["sprixen_generation_id"] = generation_id
        for index, url in enumerate(result_urls[:batch_count]):
            candidate = f"{job_name}_v{index + 1}"
            raw_path = raw_root / f"{candidate}_raw.png"
            cleaned_path = clean_root / f"{candidate}.png"
            output_record = {
                "index": index + 1,
                "name": f"{candidate}.png",
                "category": category_for_mode(mode),
                "biome": biome,
                "seed": seed_from_text(get_value(request, "seed", default="random"), job_name, index),
                "prompt": positive,
                "negative_prompt": negative_text,
                "sprixen_generation_id": generation_id,
                "result_url": url,
            }
            try:
                raw_path.write_bytes(request_bytes(url, args.api_key, args.base_url, timeout=60))
                width, height = postprocess(mode, raw_path, cleaned_path, tile_size, tile_shape)
                output_record.update({
                    "status": "ok",
                    "raw_path": rel_path(project_root, raw_path),
                    "cleaned_path": rel_path(project_root, cleaned_path),
                    "width": width,
                    "height": height,
                })
            except Exception as exc:
                output_record.update({"status": "error", "error": str(exc)})
                failures.append(str(exc))
            manifest["outputs"].append(output_record)
    except Exception as exc:
        failures.append(str(exc))

    manifest["status"] = "complete" if not failures else "failed"
    if failures:
        manifest["errors"] = failures
    manifest["completed_utc"] = now_utc()
    write_json(manifest_path, manifest)
    print(manifest_path)
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
