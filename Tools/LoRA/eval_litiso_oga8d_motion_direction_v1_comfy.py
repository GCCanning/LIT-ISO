#!/usr/bin/env python3
"""
Generate a small 8-direction evaluation sheet for litiso_oga8d_motion_direction_v1.

The goal is not final art quality. This checks whether the motion/direction LoRA
responds to LIT-ISO direction tokens in the canonical order:

  S, SE, E, NE, N, NW, W, SW
"""

from __future__ import annotations

import json
import sys
import time
from datetime import datetime, timezone
from pathlib import Path
from urllib.error import URLError
from urllib.parse import urlencode
from urllib.request import Request, urlopen

from PIL import Image, ImageDraw

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")

COMFY_URL = "http://127.0.0.1:8188"
OUT_DIR = Path(r"C:\Projects\Pixel Pipeline\generated\litiso_oga8d_motion_direction_v1_eval")
CHECKPOINT = "DreamShaper_8_pruned.safetensors"
LORA = "litiso_oga8d_motion_direction_v1_final.safetensors"
LORA_STRENGTH = 0.65
STEPS = 28
CFG = 6.5
SAMPLER = "dpmpp_2m"
SCHEDULER = "karras"
WIDTH = 512
HEIGHT = 512
CANONICAL_DIRECTIONS = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"]

NEGATIVE_PROMPT = (
    "text, letters, logo, watermark, caption, frame, border, UI, scene, scenery, "
    "landscape, room, terrain, floor, ground circle, magic circle, aura floor, "
    "multiple characters, duplicate character, cropped body, portrait, close-up, "
    "blurry, painterly, realistic, photorealistic, smooth gradients, extra limbs, "
    "missing legs, malformed hands"
)

DIRECTION_PHRASE = {
    "S": "south",
    "SE": "south-east",
    "E": "east",
    "NE": "north-east",
    "N": "north",
    "NW": "north-west",
    "W": "west",
    "SW": "south-west",
}


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def prompt_for(direction: str) -> str:
    phrase = DIRECTION_PHRASE[direction]
    return (
        "litiso_oga8d_motion_reference, cc_by_oga_source, layer Base Human Male, "
        "layer kind base_body, walk animation, "
        f"facing {phrase}, direction {direction}, frame 6 of 11, "
        "8-direction RPG character motion, full body centered game sprite frame, "
        "single armored adventurer, simple leather armor, readable silhouette, "
        "crisp pixel art sprite, transparent background, no scene, no floor, no text"
    )


def request_json(path: str, payload: dict | None = None, timeout: int = 30) -> dict:
    data = None
    headers = {}
    if payload is not None:
        data = json.dumps(payload).encode("utf-8")
        headers["Content-Type"] = "application/json"
    request = Request(f"{COMFY_URL}{path}", data=data, headers=headers)
    with urlopen(request, timeout=timeout) as response:
        return json.loads(response.read().decode("utf-8"))


def request_bytes(path: str, params: dict, timeout: int = 60) -> bytes:
    with urlopen(f"{COMFY_URL}{path}?{urlencode(params)}", timeout=timeout) as response:
        return response.read()


def workflow(prompt: str, seed: int, prefix: str) -> dict:
    return {
        "1": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": CHECKPOINT}},
        "2": {
            "class_type": "LoraLoader",
            "inputs": {
                "lora_name": LORA,
                "strength_model": LORA_STRENGTH,
                "strength_clip": LORA_STRENGTH,
                "model": ["1", 0],
                "clip": ["1", 1],
            },
        },
        "3": {"class_type": "CLIPTextEncode", "inputs": {"text": prompt, "clip": ["2", 1]}},
        "4": {"class_type": "CLIPTextEncode", "inputs": {"text": NEGATIVE_PROMPT, "clip": ["2", 1]}},
        "5": {"class_type": "EmptyLatentImage", "inputs": {"width": WIDTH, "height": HEIGHT, "batch_size": 1}},
        "6": {
            "class_type": "KSampler",
            "inputs": {
                "seed": seed,
                "steps": STEPS,
                "cfg": CFG,
                "sampler_name": SAMPLER,
                "scheduler": SCHEDULER,
                "denoise": 1.0,
                "model": ["2", 0],
                "positive": ["3", 0],
                "negative": ["4", 0],
                "latent_image": ["5", 0],
            },
        },
        "7": {"class_type": "VAEDecode", "inputs": {"samples": ["6", 0], "vae": ["1", 2]}},
        "8": {"class_type": "SaveImage", "inputs": {"filename_prefix": prefix, "images": ["7", 0]}},
    }


def submit_and_wait(direction: str, seed: int) -> list[str]:
    prefix = f"oga8d_{direction.lower()}_{seed}"
    queued = request_json("/prompt", {"prompt": workflow(prompt_for(direction), seed, prefix)}, timeout=30)
    prompt_id = queued.get("prompt_id")
    if not prompt_id:
        raise RuntimeError(f"ComfyUI did not return a prompt_id: {queued}")

    start = time.time()
    while time.time() - start < 900:
        history = request_json(f"/history/{prompt_id}", timeout=30)
        if prompt_id in history:
            entry = history[prompt_id]
            status = entry.get("status", {})
            if status.get("status_str") == "error":
                raise RuntimeError(f"ComfyUI error for {direction}: {status}")
            if status.get("completed"):
                filenames: list[str] = []
                for node in entry.get("outputs", {}).values():
                    for image in node.get("images", []):
                        filenames.append(image["filename"])
                return filenames
        time.sleep(4)
    raise TimeoutError(f"Timed out waiting for {direction}")


def write_contact_sheet(records: list[dict]) -> str | None:
    if not records:
        return None
    cell = 256
    label_h = 34
    width = len(records) * cell
    height = cell + label_h
    sheet = Image.new("RGBA", (width, height), (16, 18, 24, 255))
    draw = ImageDraw.Draw(sheet)
    for index, record in enumerate(records):
        image = Image.open(record["output"]).convert("RGBA")
        image.thumbnail((cell, cell), Image.Resampling.NEAREST)
        x = index * cell
        bg = Image.new("RGBA", (cell, cell), (238, 238, 238, 255))
        bg.alpha_composite(image, ((cell - image.width) // 2, (cell - image.height) // 2))
        sheet.alpha_composite(bg, (x, 0))
        draw.rectangle((x, 0, x + cell - 1, cell - 1), outline=(66, 74, 90, 255))
        draw.text((x + 8, cell + 8), record["direction"], fill=(235, 240, 245, 255))
    path = OUT_DIR / "oga8d_direction_eval_contact_sheet.png"
    sheet.save(path)
    return str(path)


def main() -> int:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    try:
        request_json("/system_stats", timeout=5)
    except URLError as exc:
        manifest = {
            "status": "skipped",
            "reason": f"ComfyUI is not reachable at {COMFY_URL}: {exc}",
            "generated_utc": utc_now(),
            "checkpoint": CHECKPOINT,
            "lora": LORA,
        }
        (OUT_DIR / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        print(manifest["reason"], file=sys.stderr)
        return 0

    records = []
    for index, direction in enumerate(CANONICAL_DIRECTIONS):
        seed = 82000 + index
        print(f"Generating direction {direction}...")
        filenames = submit_and_wait(direction, seed)
        if not filenames:
            raise RuntimeError(f"No image produced for {direction}")
        content = request_bytes("/view", {"filename": filenames[0], "subfolder": "", "type": "output"})
        out_path = OUT_DIR / f"oga8d_eval_{direction.lower()}.png"
        out_path.write_bytes(content)
        records.append(
            {
                "direction": direction,
                "direction_phrase": DIRECTION_PHRASE[direction],
                "seed": seed,
                "prompt": prompt_for(direction),
                "output": str(out_path),
            }
        )

    contact = write_contact_sheet(records)
    manifest = {
        "status": "complete",
        "generated_utc": utc_now(),
        "checkpoint": CHECKPOINT,
        "lora": LORA,
        "lora_strength": LORA_STRENGTH,
        "steps": STEPS,
        "cfg": CFG,
        "sampler": SAMPLER,
        "scheduler": SCHEDULER,
        "directions": CANONICAL_DIRECTIONS,
        "records": records,
        "contact_sheet": contact,
        "qc_note": "Human-check whether silhouettes face S, SE, E, NE, N, NW, W, SW. Art quality is secondary for this pilot.",
    }
    (OUT_DIR / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps({"status": "complete", "out_dir": str(OUT_DIR), "contact_sheet": contact}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
