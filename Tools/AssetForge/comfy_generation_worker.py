#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import math
import mimetypes
import sys
import time
from datetime import datetime, timezone
from pathlib import Path
from urllib.error import URLError
from urllib.parse import urlencode
from urllib.request import Request, urlopen

try:
    from PIL import Image
except Exception as exc:  # pragma: no cover - surfaced in manifest/stdout.
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
    keep = []
    for char in text:
        keep.append(char if char.isalnum() or char in "_.-" else "_")
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
    digest = hashlib.sha256(f"{job_name}:{text}:{index}:{time.time_ns()}".encode("utf-8")).hexdigest()
    return int(digest[:12], 16) % 2147483647


def request_json(comfy_url: str, path: str, payload: dict | None = None, timeout: int = 30) -> dict:
    data = None
    headers = {}
    if payload is not None:
        data = json.dumps(payload).encode("utf-8")
        headers["Content-Type"] = "application/json"
    req = Request(f"{comfy_url}{path}", data=data, headers=headers)
    with urlopen(req, timeout=timeout) as response:
        return json.loads(response.read().decode("utf-8"))


def request_bytes(comfy_url: str, path: str, params: dict, timeout: int = 60) -> bytes:
    with urlopen(f"{comfy_url}{path}?{urlencode(params)}", timeout=timeout) as response:
        return response.read()


def upload_image(comfy_url: str, image_path: Path, subfolder: str = "AssetForgeTemplates", timeout: int = 60) -> str:
    boundary = f"----AssetForgeBoundary{time.time_ns()}"
    filename = image_path.name
    content_type = mimetypes.guess_type(filename)[0] or "image/png"
    image_bytes = image_path.read_bytes()
    fields = [
        (
            f"--{boundary}\r\n"
            f'Content-Disposition: form-data; name="image"; filename="{filename}"\r\n'
            f"Content-Type: {content_type}\r\n\r\n"
        ).encode("utf-8")
        + image_bytes
        + b"\r\n",
        (
            f"--{boundary}\r\n"
            'Content-Disposition: form-data; name="subfolder"\r\n\r\n'
            f"{subfolder}\r\n"
        ).encode("utf-8"),
        (
            f"--{boundary}\r\n"
            'Content-Disposition: form-data; name="overwrite"\r\n\r\n'
            "true\r\n"
        ).encode("utf-8"),
        f"--{boundary}--\r\n".encode("utf-8"),
    ]
    data = b"".join(fields)
    request = Request(
        f"{comfy_url}/upload/image",
        data=data,
        headers={"Content-Type": f"multipart/form-data; boundary={boundary}"},
    )
    with urlopen(request, timeout=timeout) as response:
        payload = json.loads(response.read().decode("utf-8"))
    uploaded_name = payload.get("name") or filename
    uploaded_subfolder = payload.get("subfolder") or subfolder
    return f"{uploaded_subfolder}/{uploaded_name}" if uploaded_subfolder else uploaded_name


def resolve_local_path(project_root: Path, value: object) -> Path | None:
    text = str(value or "").strip()
    if not text or text.lower().startswith(("http://", "https://")):
        return None
    path = Path(text)
    if not path.is_absolute():
        path = project_root / text.replace("/", "\\")
    return path.resolve()


def prepare_template_canvas(source_path: Path, output_path: Path, width: int, height: int) -> Path:
    if Image is None:
        raise RuntimeError(f"Pillow is not available: {PIL_IMPORT_ERROR}")
    image = Image.open(source_path).convert("RGBA")
    bbox = image.getchannel("A").getbbox()
    cropped = image.crop(bbox) if bbox else image
    max_w = max(1, int(width * 0.5))
    max_h = max(1, int(height * 0.72))
    scale = min(max_w / max(1, cropped.width), max_h / max(1, cropped.height))
    size = (max(1, int(cropped.width * scale)), max(1, int(cropped.height * scale)))
    resized = cropped.resize(size, Image.Resampling.NEAREST)
    canvas = Image.new("RGBA", (width, height), (0, 255, 0, 255))
    x = (width - resized.width) // 2
    y = height - resized.height - max(16, height // 12)
    canvas.alpha_composite(resized, (x, max(0, y)))
    output_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.convert("RGB").save(output_path)
    return output_path


def prepare_style_reference(source_path: Path, output_path: Path, width: int, height: int) -> Path:
    if Image is None:
        raise RuntimeError(f"Pillow is not available: {PIL_IMPORT_ERROR}")
    image = Image.open(source_path).convert("RGBA")
    bbox = image.getchannel("A").getbbox()
    cropped = image.crop(bbox) if bbox else image
    max_w = max(1, int(width * 0.52))
    max_h = max(1, int(height * 0.78))
    scale = min(max_w / max(1, cropped.width), max_h / max(1, cropped.height))
    size = (max(1, int(cropped.width * scale)), max(1, int(cropped.height * scale)))
    resized = cropped.resize(size, Image.Resampling.NEAREST)
    canvas = Image.new("RGBA", (width, height), (224, 224, 224, 255))
    x = (width - resized.width) // 2
    y = (height - resized.height) // 2
    canvas.alpha_composite(resized, (x, y))
    output_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.convert("RGB").save(output_path)
    return output_path


def mode_prompt(mode: str, prompt: str, template_guided: bool = False) -> str:
    requested = prompt.strip().replace('"', "'")
    semantic_lock = f'The image must clearly depict exactly "{requested}". No substitutes.'
    template_lock = ""
    if template_guided:
        template_lock = " Preserve the reference template pose, body orientation, silhouette placement, and full-body framing."
    contracts = {
        "tile": "single isolated 2:1 isometric terrain diamond tile sprite only, uniform terrain surface only, transparent background, no trees, no rocks, no props, no characters, no border frame, no raised cube, no object on top",
        "prop": "one standalone object, object fills frame, orthographic 3/4 isometric game sprite, transparent empty background, bottom centered, full object visible, not cropped, no floor contact shadow except tiny object shadow, no other objects, no room, no diorama, no walls, no ground tile, no floor, no base plate, no pedestal, no collection, no set",
        "item": "one standalone item sprite only, centered inventory item sprite, transparent empty background, object silhouette only, full object visible, not cropped, no hand, no character, no badge, no emblem, no label, no circular backing, no square backing, no pedestal, no collection, no set",
        "character": "one standalone full-body isometric pixel character sprite only, solid flat chroma green background for extraction, bottom-center foot anchor, full body visible, not cropped, readable face and silhouette, no floor, no base, no scene, no duplicate character",
        "npc": "one standalone full-body isometric NPC pixel sprite only, solid flat chroma green background for extraction, bottom-center foot anchor, full body visible, not cropped, readable face and silhouette, no floor, no base, no scene, no duplicate character",
        "mob": "one standalone full-body isometric creature pixel sprite only, solid flat chroma green background for extraction, bottom-center foot anchor, full body visible, not cropped, readable silhouette, no floor, no base, no scene, no duplicate creature",
    }
    return f"{prompt}, {semantic_lock}, {template_lock}, {contracts.get(mode, '')}".strip(", ")


def mode_negative(mode: str, negative: str) -> str:
    contracts = {
        "tile": "tree, bush, rock, flower, log, prop, object on top, character, building, border, frame, square floor, cube, wall, scene, background, text, watermark, blurry, antialias haze, realistic render",
        "prop": "room, house, village, landscape, tile, terrain, floor plan, miniature scene, terrarium, dollhouse, showcase, shelf, display case, vignette, circular badge, emblem, icon border, pedestal, plinth, multiple objects, object set, clutter, diorama, walls, floor, corner wall, furniture set, ground, grass patch, terrain tile, base plate, platform, scene, background, text, watermark, blurry, antialias haze, realistic render",
        "item": "character, face, building, room, landscape, terrain, tile, weapon rack, sign, hand, person, badge, emblem, medallion, coin, sticker, app icon, logo, circular frame, square frame, rounded rectangle, inventory slot, UI backing, label, text, pedestal, display stand, background, scene, floor, blurry, antialias haze, realistic render",
        "character": "floor, base, ground patch, scene, scenery, cave, portal, mountains, rays, vignette, duplicate character, cropped body, portrait, bust, face close-up, realistic render, 3d render, blurry, antialias haze, text, watermark",
        "npc": "floor, base, ground patch, scene, scenery, cave, portal, mountains, rays, vignette, duplicate character, cropped body, portrait, bust, face close-up, realistic render, 3d render, blurry, antialias haze, text, watermark",
        "mob": "floor, base, ground patch, scene, scenery, cave, portal, mountains, rays, vignette, duplicate creature, cropped body, portrait, bust, face close-up, realistic render, 3d render, blurry, antialias haze, text, watermark",
    }
    base = negative.strip()
    extra = contracts.get(mode, "")
    return f"{base}, {extra}" if base else extra


def workflow(
    args,
    prompt: str,
    negative: str,
    seed: int,
    prefix: str,
    template_image: str = "",
    denoise: float | None = None,
    style_image: str = "",
    style_settings: dict | None = None,
    control_settings: dict | None = None,
) -> dict:
    has_lora = bool(args.lora and args.lora.lower() not in {"none", "off", "disabled"})
    style_settings = style_settings or {}
    control_settings = control_settings or {}
    has_style = bool(style_image)
    has_control = bool(control_settings.get("enabled") and (control_settings.get("pose_json") or control_settings.get("control_image")))
    model_ref = ["12", 0] if has_style else (["2", 0] if has_lora else ["1", 0])
    ip_base_model_ref = ["2", 0] if has_lora else ["1", 0]
    clip_ref = ["2", 1] if has_lora else ["1", 1]
    latent_ref = ["10", 0] if template_image else ["5", 0]
    denoise_value = args.denoise if denoise is None else denoise
    positive_ref = ["22", 0] if has_control else ["3", 0]
    negative_ref = ["22", 1] if has_control else ["4", 0]
    graph = {
        "1": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": args.checkpoint}},
        "3": {"class_type": "CLIPTextEncode", "inputs": {"text": prompt, "clip": clip_ref}},
        "4": {"class_type": "CLIPTextEncode", "inputs": {"text": negative, "clip": clip_ref}},
        "5": {"class_type": "EmptyLatentImage", "inputs": {"width": args.width, "height": args.height, "batch_size": 1}},
        "6": {
            "class_type": "KSampler",
            "inputs": {
                "seed": seed,
                "steps": args.steps,
                "cfg": args.cfg,
                "sampler_name": args.sampler,
                "scheduler": args.scheduler,
                "denoise": denoise_value,
                "model": model_ref,
                "positive": positive_ref,
                "negative": negative_ref,
                "latent_image": latent_ref,
            },
        },
        "7": {"class_type": "VAEDecode", "inputs": {"samples": ["6", 0], "vae": ["1", 2]}},
        "8": {"class_type": "SaveImage", "inputs": {"filename_prefix": prefix, "images": ["7", 0]}},
    }
    if template_image:
        graph["9"] = {"class_type": "LoadImage", "inputs": {"image": template_image}}
        graph["10"] = {"class_type": "VAEEncode", "inputs": {"pixels": ["9", 0], "vae": ["1", 2]}}
    if has_style:
        graph["11"] = {
            "class_type": "IPAdapterUnifiedLoader",
            "inputs": {
                "model": ip_base_model_ref,
                "preset": style_settings.get("preset", "STANDARD (medium strength)"),
            },
        }
        graph["12"] = {
            "class_type": "IPAdapter",
            "inputs": {
                "model": ["11", 0],
                "ipadapter": ["11", 1],
                "image": ["13", 0],
                "weight": float(style_settings.get("weight", 0.58)),
                "start_at": float(style_settings.get("start_at", 0.0)),
                "end_at": float(style_settings.get("end_at", 0.72)),
                "weight_type": style_settings.get("weight_type", "style transfer"),
            },
        }
        graph["13"] = {"class_type": "LoadImage", "inputs": {"image": style_image}}
    if has_control:
        graph["20"] = {
            "class_type": "ControlNetLoader",
            "inputs": {"control_net_name": control_settings.get("control_net", "control_v11p_sd15_openpose.pth")},
        }
        if control_settings.get("control_image"):
            graph["21"] = {"class_type": "LoadImage", "inputs": {"image": control_settings["control_image"]}}
        else:
            graph["21"] = {
                "class_type": "OpenPoseStudio",
                "inputs": {
                    "pose_json": control_settings["pose_json"],
                    "render_body": True,
                    "render_hand": False,
                    "render_face": False,
                },
            }
        graph["22"] = {
            "class_type": "ControlNetApplyAdvanced",
            "inputs": {
                "positive": ["3", 0],
                "negative": ["4", 0],
                "control_net": ["20", 0],
                "image": ["21", 0],
                "strength": float(control_settings.get("strength", 0.82)),
                "start_percent": float(control_settings.get("start_percent", 0.0)),
                "end_percent": float(control_settings.get("end_percent", 0.85)),
            },
        }
    if has_lora:
        graph["2"] = {
            "class_type": "LoraLoader",
            "inputs": {
                "lora_name": args.lora,
                "strength_model": args.lora_strength,
                "strength_clip": args.lora_strength,
                "model": ["1", 0],
                "clip": ["1", 1],
            },
        }
    return graph


def wait_for_outputs(comfy_url: str, prompt_id: str, timeout_seconds: int) -> list[dict]:
    start = time.time()
    while time.time() - start < timeout_seconds:
        history = request_json(comfy_url, f"/history/{prompt_id}", timeout=30)
        if prompt_id in history:
            entry = history[prompt_id]
            status = entry.get("status", {})
            if status.get("status_str") == "error":
                raise RuntimeError(f"ComfyUI returned an error: {status}")
            if status.get("completed") or entry.get("outputs"):
                return [
                    image
                    for output in entry.get("outputs", {}).values()
                    for image in output.get("images", [])
                ]
        time.sleep(1.5)
    raise TimeoutError(f"Timed out waiting for ComfyUI prompt {prompt_id}")


def color_distance(a: tuple[int, int, int], b: tuple[int, int, int]) -> float:
    return math.sqrt(sum((int(a[i]) - int(b[i])) ** 2 for i in range(3)))


def is_edge_neutral_background(rgb: tuple[int, int, int]) -> bool:
    low = min(rgb)
    high = max(rgb)
    return low > 92 and (high - low) <= 30


def remove_edge_background(image, tolerance: int = 46):
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
    seen = set()
    stack = []
    for x in range(width):
        stack.append((x, 0))
        stack.append((x, height - 1))
    for y in range(height):
        stack.append((0, y))
        stack.append((width - 1, y))
    while stack:
        x, y = stack.pop()
        if (x, y) in seen or x < 0 or y < 0 or x >= width or y >= height:
            continue
        seen.add((x, y))
        r, g, b, a = pixels[x, y]
        rgb = (r, g, b)
        if a < 12 or color_distance(rgb, bg) <= tolerance or is_edge_neutral_background(rgb):
            pixels[x, y] = (r, g, b, 0)
            stack.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    return rgba


def remove_chroma_green(image):
    rgba = image.convert("RGBA")
    pixels = rgba.load()
    for y in range(rgba.height):
        for x in range(rgba.width):
            r, g, b, a = pixels[x, y]
            if a == 0:
                continue
            if g > 110 and g > r * 1.28 and g > b * 1.18:
                pixels[x, y] = (r, g, b, 0)
    return rgba


def alpha_coverage(image) -> float:
    alpha = image.getchannel("A")
    visible = 0
    data = alpha.get_flattened_data() if hasattr(alpha, "get_flattened_data") else alpha.getdata()
    for value in data:
        if value > 0:
            visible += 1
    return visible / max(1, image.width * image.height)


def alpha_bbox(image) -> tuple[int, int, int, int] | None:
    alpha = image.getchannel("A")
    return alpha.getbbox()


def keep_primary_foreground(image, alpha_threshold: int = 16):
    rgba = image.convert("RGBA")
    width, height = rgba.size
    pixels = rgba.load()
    seen = set()
    components = []
    for y in range(height):
        for x in range(width):
            if (x, y) in seen or pixels[x, y][3] <= alpha_threshold:
                continue
            stack = [(x, y)]
            seen.add((x, y))
            coords = []
            min_x = max_x = x
            min_y = max_y = y
            while stack:
                px, py = stack.pop()
                coords.append((px, py))
                min_x = min(min_x, px)
                max_x = max(max_x, px)
                min_y = min(min_y, py)
                max_y = max(max_y, py)
                for nx, ny in ((px + 1, py), (px - 1, py), (px, py + 1), (px, py - 1)):
                    if nx < 0 or ny < 0 or nx >= width or ny >= height or (nx, ny) in seen:
                        continue
                    if pixels[nx, ny][3] <= alpha_threshold:
                        continue
                    seen.add((nx, ny))
                    stack.append((nx, ny))
            box_w = max_x - min_x + 1
            box_h = max_y - min_y + 1
            if len(coords) < 24:
                continue
            aspect = box_w / max(1, box_h)
            center_x = (min_x + max_x) * 0.5
            center_bias = 1.0 - min(1.0, abs(center_x - width * 0.5) / max(1.0, width * 0.5))
            height_bias = box_h / max(1.0, height)
            strip_penalty = 0.25 if aspect > 2.4 and box_h < height * 0.28 else 1.0
            score = len(coords) * (0.65 + center_bias * 0.35) * (0.7 + height_bias) * strip_penalty
            components.append(
                {
                    "coords": coords,
                    "score": score,
                    "box": (min_x, min_y, max_x + 1, max_y + 1),
                    "area": len(coords),
                }
            )
    if not components:
        return rgba
    components.sort(key=lambda item: item["score"], reverse=True)
    selected = components[0]
    selected_box = selected["box"]
    pad_x = max(8, int(width * 0.08))
    pad_y = max(8, int(height * 0.08))
    expanded = (
        max(0, selected_box[0] - pad_x),
        max(0, selected_box[1] - pad_y),
        min(width, selected_box[2] + pad_x),
        min(height, selected_box[3] + pad_y),
    )
    keep = set()
    minimum_score = selected["score"] * 0.035
    for component in components:
        box = component["box"]
        overlaps_main = not (
            box[2] < expanded[0]
            or box[0] > expanded[2]
            or box[3] < expanded[1]
            or box[1] > expanded[3]
        )
        if component is selected or (component["score"] >= minimum_score and overlaps_main):
            keep.update(component["coords"])
    output = Image.new("RGBA", rgba.size, (0, 0, 0, 0))
    out_pixels = output.load()
    for x, y in keep:
        out_pixels[x, y] = pixels[x, y]
    return output


def fit_foreground(image, size: tuple[int, int], max_fill: float, anchor: str, retry_tolerance: int | None = None):
    target_w, target_h = size
    cleaned = remove_chroma_green(remove_edge_background(image))
    if retry_tolerance is not None and alpha_coverage(cleaned) > 0.52:
        retried = remove_chroma_green(remove_edge_background(image, tolerance=retry_tolerance))
        if alpha_coverage(retried) < alpha_coverage(cleaned):
            cleaned = retried
    if retry_tolerance is not None:
        pruned = keep_primary_foreground(cleaned)
        if alpha_coverage(pruned) > 0:
            cleaned = pruned
    box = alpha_bbox(cleaned)
    if not box:
        return Image.new("RGBA", size, (0, 0, 0, 0))
    cropped = cleaned.crop(box)
    max_w = max(1, int(target_w * max_fill))
    max_h = max(1, int(target_h * max_fill))
    scale = min(max_w / cropped.width, max_h / cropped.height)
    new_size = (max(1, int(cropped.width * scale)), max(1, int(cropped.height * scale)))
    resized = cropped.resize(new_size, Image.Resampling.NEAREST)
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    x = (target_w - resized.width) // 2
    if anchor == "bottom":
        y = target_h - resized.height - max(1, target_h // 16)
    else:
        y = (target_h - resized.height) // 2
    canvas.alpha_composite(resized, (x, max(0, y)))
    return canvas


def remove_floor_artifacts(image, alpha_threshold: int = 16):
    rgba = image.convert("RGBA")
    width, height = rgba.size
    pixels = rgba.load()
    seen = set()
    remove = set()
    for y in range(height):
        for x in range(width):
            if (x, y) in seen or pixels[x, y][3] <= alpha_threshold:
                continue
            stack = [(x, y)]
            seen.add((x, y))
            coords = []
            min_x = max_x = x
            min_y = max_y = y
            while stack:
                px, py = stack.pop()
                coords.append((px, py))
                min_x = min(min_x, px)
                max_x = max(max_x, px)
                min_y = min(min_y, py)
                max_y = max(max_y, py)
                for nx, ny in ((px + 1, py), (px - 1, py), (px, py + 1), (px, py - 1)):
                    if nx < 0 or ny < 0 or nx >= width or ny >= height or (nx, ny) in seen:
                        continue
                    if pixels[nx, ny][3] <= alpha_threshold:
                        continue
                    seen.add((nx, ny))
                    stack.append((nx, ny))
            box_w = max_x - min_x + 1
            box_h = max_y - min_y + 1
            center_y = (min_y + max_y) * 0.5
            aspect = box_w / max(1, box_h)
            if center_y > height * 0.62 and aspect > 2.2 and box_h < height * 0.24:
                remove.update(coords)
    if not remove:
        return rgba
    for x, y in remove:
        r, g, b, _ = pixels[x, y]
        pixels[x, y] = (r, g, b, 0)
    return rgba


def make_tile(image):
    fitted = fit_foreground(image, (64, 64), 1.0, "center")
    fitted = fitted.resize((64, 64), Image.Resampling.NEAREST)
    pixels = fitted.load()
    cx, cy = 31.5, 31.5
    for y in range(64):
        for x in range(64):
            if abs(x - cx) / 31.0 + abs(y - cy) / 15.5 > 1.0:
                r, g, b, _ = pixels[x, y]
                pixels[x, y] = (r, g, b, 0)
    return fitted


def snap_pixels(image, max_colors: int):
    rgba = image.convert("RGBA")
    alpha = rgba.getchannel("A")
    hard_alpha = alpha.point(lambda value: 255 if value >= 80 else 0)
    rgba.putalpha(hard_alpha)
    if max_colors > 0:
        quantized = rgba.quantize(colors=max_colors, method=Image.Quantize.FASTOCTREE).convert("RGBA")
        quantized.putalpha(hard_alpha)
        rgba = quantized
    pixels = rgba.load()
    width, height = rgba.size
    for y in range(height):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            if a < 255:
                pixels[x, y] = (0, 0, 0, 0)
            else:
                pixels[x, y] = (r, g, b, 255)
    return rgba


def category_for_mode(mode: str) -> str:
    if mode == "tile":
        return "terrain"
    if mode == "prop":
        return "decoration"
    if mode == "item":
        return "item"
    return mode


def postprocess(mode: str, raw_path: Path, cleaned_path: Path) -> tuple[int, int]:
    if Image is None:
        raise RuntimeError(f"Pillow is not available: {PIL_IMPORT_ERROR}")
    image = Image.open(raw_path).convert("RGBA")
    if mode == "tile":
        result = make_tile(image)
        result = snap_pixels(result, 16)
    elif mode == "prop":
        result = fit_foreground(image, (128, 128), 0.88, "bottom")
        result = snap_pixels(result, 28)
    elif mode == "item":
        result = fit_foreground(image, (64, 64), 0.82, "center")
        result = snap_pixels(result, 24)
    elif mode in {"character", "npc", "mob"}:
        result = fit_foreground(image, (128, 128), 0.88, "bottom", retry_tolerance=78)
        result = remove_floor_artifacts(result)
        result = snap_pixels(result, 48)
    else:
        raise ValueError(f"Unsupported mode for Comfy worker: {mode}")
    cleaned_path.parent.mkdir(parents=True, exist_ok=True)
    result.save(cleaned_path)
    return result.size


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", required=True)
    parser.add_argument("--request-path", required=True)
    parser.add_argument("--raw-output-root", required=True)
    parser.add_argument("--clean-output-root", required=True)
    parser.add_argument("--manifest-path", required=True)
    parser.add_argument("--comfy-url", default="http://127.0.0.1:8188")
    parser.add_argument("--checkpoint", default="DreamShaper_8_pruned.safetensors")
    parser.add_argument("--lora", default="")
    parser.add_argument("--lora-strength", type=float, default=0.0)
    parser.add_argument("--steps", type=int, default=22)
    parser.add_argument("--cfg", type=float, default=6.0)
    parser.add_argument("--sampler", default="dpmpp_2m")
    parser.add_argument("--scheduler", default="karras")
    parser.add_argument("--width", type=int, default=512)
    parser.add_argument("--height", type=int, default=512)
    parser.add_argument("--denoise", type=float, default=1.0)
    parser.add_argument("--timeout-seconds", type=int, default=600)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

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
    batch_count = max(1, min(8, int(get_value(request, "batch_count", "batchCount", default=1))))
    style = get_value(request, "style", default={}) or {}
    reference_path = resolve_local_path(project_root, get_value(request, "reference_image", "referenceImage", default=""))
    style_reference_path = resolve_local_path(project_root, get_value(request, "style_reference_image", "styleReferenceImage", default=""))
    template_guidance = get_value(request, "template_guidance", "templateGuidance", default={}) or {}
    style_guidance = get_value(request, "style_guidance", "styleGuidance", default={}) or {}
    control_guidance = get_value(request, "control_guidance", "controlGuidance", default={}) or {}
    template_enabled = bool(reference_path and mode in {"character", "npc", "mob"})
    if template_guidance and "enabled" in template_guidance:
        template_enabled = template_enabled and bool(template_guidance.get("enabled"))
    template_denoise = float(get_value(template_guidance, "denoise", default=args.denoise))
    if template_enabled and (not reference_path or not reference_path.exists()):
        raise FileNotFoundError(f"Template/reference image was requested but not found: {reference_path}")
    style_enabled = bool(style_reference_path and mode in {"character", "npc", "mob"})
    if style_guidance and "enabled" in style_guidance:
        style_enabled = style_enabled and bool(style_guidance.get("enabled"))
    if style_enabled and (not style_reference_path or not style_reference_path.exists()):
        raise FileNotFoundError(f"Style reference image was requested but not found: {style_reference_path}")
    style_settings = {
        "ipadapter_model": str(get_value(style_guidance, "ipadapter_model", "ipadapterModel", default="ip-adapter_sd15.safetensors")),
        "preset": str(get_value(style_guidance, "preset", default="STANDARD (medium strength)")),
        "weight": float(get_value(style_guidance, "weight", default=0.58)),
        "start_at": float(get_value(style_guidance, "start_at", "startAt", default=0.0)),
        "end_at": float(get_value(style_guidance, "end_at", "endAt", default=0.72)),
        "weight_type": str(get_value(style_guidance, "weight_type", "weightType", default="style transfer")),
    }
    control_enabled = bool(control_guidance.get("enabled")) and mode in {"tile", "character", "npc", "mob"}
    control_pose_json_path = resolve_local_path(project_root, get_value(control_guidance, "pose_json_path", "poseJsonPath", default=""))
    control_image_path = resolve_local_path(
        project_root,
        get_value(
            control_guidance,
            "control_image_path",
            "controlImagePath",
            "control_image",
            "controlImage",
            "pose_image",
            "poseImage",
            "pose_image_path",
            "poseImagePath",
            default="",
        ),
    )
    control_pose_json = str(get_value(control_guidance, "pose_json", "poseJson", default="")).strip()
    if control_enabled and control_pose_json_path:
        if not control_pose_json_path.exists():
            raise FileNotFoundError(f"Control pose JSON was requested but not found: {control_pose_json_path}")
        control_pose_json = control_pose_json_path.read_text(encoding="utf-8-sig")
    if control_enabled and control_image_path and not control_image_path.exists():
        raise FileNotFoundError(f"Control image was requested but not found: {control_image_path}")
    control_enabled = control_enabled and bool(control_pose_json or control_image_path)
    control_settings = {
        "enabled": control_enabled,
        "type": str(get_value(control_guidance, "type", default="openpose")),
        "control_net": str(get_value(control_guidance, "control_net", "controlNet", default="control_v11p_sd15_openpose.pth")),
        "strength": float(get_value(control_guidance, "strength", default=0.82)),
        "start_percent": float(get_value(control_guidance, "start_percent", "startPercent", default=0.0)),
        "end_percent": float(get_value(control_guidance, "end_percent", "endPercent", default=0.85)),
        "pose_json_path": str(control_pose_json_path) if control_pose_json_path else "",
        "control_image_path": str(control_image_path) if control_image_path else "",
        "pose_json": control_pose_json,
        "pose_json_present": bool(control_pose_json),
    }

    manifest = {
        "schema": "lit_iso.asset_forge.comfy_generation_manifest.v1",
        "status": "planned" if args.dry_run else "running",
        "created_utc": now_utc(),
        "job_name": job_name,
        "asset_mode": mode,
        "request_path": rel_path(project_root, request_path),
        "style": style,
        "style_snapshot_path": get_value(request, "style_snapshot_path", "styleSnapshotPath", default=""),
        "settings": {
            "comfy_url": args.comfy_url.rstrip("/"),
            "checkpoint": args.checkpoint,
            "lora": args.lora,
            "lora_strength": args.lora_strength,
            "steps": args.steps,
            "cfg": args.cfg,
            "sampler": args.sampler,
            "scheduler": args.scheduler,
            "width": args.width,
            "height": args.height,
            "denoise": args.denoise,
            "template_guidance": {
                "enabled": template_enabled,
                "reference_image": str(reference_path) if reference_path else "",
                "denoise": template_denoise,
            },
            "style_guidance": {
                "enabled": style_enabled,
                "style_reference_image": str(style_reference_path) if style_reference_path else "",
                **style_settings,
            },
            "control_guidance": {
                key: value for key, value in control_settings.items() if key != "pose_json"
            },
        },
        "outputs": [],
    }

    if mode not in SUPPORTED_MODES:
        manifest["status"] = "unsupported_mode"
        manifest["error"] = f"Comfy worker currently supports only: {', '.join(sorted(SUPPORTED_MODES))}"
        write_json(manifest_path, manifest)
        print(manifest_path)
        return 2

    if args.dry_run:
        for index in range(batch_count):
            manifest["outputs"].append({
                "index": index + 1,
                "name": f"{job_name}_v{index + 1}.png",
                "category": category_for_mode(mode),
                "biome": biome,
                "seed": seed_from_text(get_value(request, "seed", default="random"), job_name, index),
                "prompt": mode_prompt(mode, prompt, template_guided=template_enabled),
                "negative_prompt": mode_negative(mode, negative),
                "template_guidance": {
                    "enabled": template_enabled,
                    "reference_image": str(reference_path) if reference_path else "",
                    "denoise": template_denoise,
                },
                "style_guidance": {
                    "enabled": style_enabled,
                    "style_reference_image": str(style_reference_path) if style_reference_path else "",
                    **style_settings,
                },
                "control_guidance": {
                    key: value for key, value in control_settings.items() if key != "pose_json"
                },
                "status": "planned",
            })
        write_json(manifest_path, manifest)
        print(manifest_path)
        return 0

    if Image is None:
        manifest["status"] = "failed"
        manifest["error"] = f"Pillow is not available: {PIL_IMPORT_ERROR}"
        write_json(manifest_path, manifest)
        print(manifest_path)
        return 1

    comfy_url = args.comfy_url.rstrip("/")
    request_json(comfy_url, "/queue", timeout=5)
    raw_root.mkdir(parents=True, exist_ok=True)
    clean_root.mkdir(parents=True, exist_ok=True)
    uploaded_template = ""
    prepared_template_path = ""
    uploaded_style = ""
    prepared_style_path = ""
    uploaded_control = ""
    if template_enabled and reference_path:
        prepared = prepare_template_canvas(reference_path, raw_root / "_template_uploads" / f"{job_name}_template.png", args.width, args.height)
        prepared_template_path = rel_path(project_root, prepared)
        uploaded_template = upload_image(comfy_url, prepared)
    if style_enabled and style_reference_path:
        prepared_style = prepare_style_reference(style_reference_path, raw_root / "_style_uploads" / f"{job_name}_style.png", args.width, args.height)
        prepared_style_path = rel_path(project_root, prepared_style)
        uploaded_style = upload_image(comfy_url, prepared_style, subfolder="AssetForgeStyleRefs")
    if control_enabled and control_image_path:
        uploaded_control = upload_image(comfy_url, control_image_path, subfolder="AssetForgeControls")

    category = category_for_mode(mode)
    failures = []
    for index in range(batch_count):
        candidate = f"{job_name}_v{index + 1}"
        seed = seed_from_text(get_value(request, "seed", default="random"), job_name, index)
        positive = mode_prompt(mode, prompt, template_guided=template_enabled)
        negative_text = mode_negative(mode, negative)
        prefix = f"AssetForge/{job_name}/{candidate}"
        output_record = {
            "index": index + 1,
            "name": f"{candidate}.png",
            "category": category,
            "biome": biome,
            "seed": seed,
            "prompt": positive,
            "negative_prompt": negative_text,
            "template_guidance": {
                "enabled": template_enabled,
                "reference_image": str(reference_path) if reference_path else "",
                "prepared_template_path": prepared_template_path,
                "uploaded_template": uploaded_template,
                "denoise": template_denoise,
            },
            "style_guidance": {
                "enabled": style_enabled,
                "style_reference_image": str(style_reference_path) if style_reference_path else "",
                "prepared_style_path": prepared_style_path,
                "uploaded_style": uploaded_style,
                **style_settings,
            },
            "control_guidance": {
                **{key: value for key, value in control_settings.items() if key != "pose_json"},
                "uploaded_pose_image": uploaded_control,
            },
        }
        try:
            graph_control_settings = dict(control_settings)
            if uploaded_control:
                graph_control_settings["control_image"] = uploaded_control
            queued = request_json(
                comfy_url,
                "/prompt",
                {
                    "prompt": workflow(
                        args,
                        positive,
                        negative_text,
                        seed,
                        prefix,
                        template_image=uploaded_template,
                        denoise=template_denoise,
                        style_image=uploaded_style,
                        style_settings=style_settings,
                        control_settings=graph_control_settings,
                    )
                },
                timeout=30,
            )
            prompt_id = queued.get("prompt_id")
            if not prompt_id:
                raise RuntimeError(f"ComfyUI did not return prompt_id: {queued}")
            images = wait_for_outputs(comfy_url, prompt_id, args.timeout_seconds)
            if not images:
                raise RuntimeError("ComfyUI completed but returned no images")
            image_info = images[0]
            raw_bytes = request_bytes(comfy_url, "/view", image_info, timeout=60)
            raw_path = raw_root / f"{candidate}_raw.png"
            cleaned_path = clean_root / f"{candidate}.png"
            raw_path.write_bytes(raw_bytes)
            width, height = postprocess(mode, raw_path, cleaned_path)
            output_record.update({
                "status": "ok",
                "prompt_id": prompt_id,
                "comfy_image": image_info,
                "raw_path": rel_path(project_root, raw_path),
                "cleaned_path": rel_path(project_root, cleaned_path),
                "width": width,
                "height": height,
            })
        except Exception as exc:
            output_record.update({"status": "error", "error": str(exc)})
            failures.append(str(exc))
        manifest["outputs"].append(output_record)

    manifest["status"] = "complete" if not failures else "failed"
    if failures:
        manifest["errors"] = failures
    manifest["completed_utc"] = now_utc()
    write_json(manifest_path, manifest)
    print(manifest_path)
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
