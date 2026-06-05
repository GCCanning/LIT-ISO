#!/usr/bin/env python3
"""
Generate a stricter evaluation set for litiso_sprixen_frame_v1.
"""

from __future__ import annotations

import json
import sys
import time
from pathlib import Path
from urllib.error import URLError
from urllib.parse import urlencode
from urllib.request import Request, urlopen

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")

COMFY_URL = "http://127.0.0.1:8188"
OUT_DIR = Path(r"C:\Projects\Pixel Pipeline\generated\litiso_sprixen_frame_v1_eval")
CHECKPOINT = "DreamShaper_8_pruned.safetensors"
LORA = "litiso_sprixen_frame_v1.safetensors"
LORA_STRENGTH = 0.65
STEPS = 28
CFG = 6.5
SAMPLER = "dpmpp_2m"
SCHEDULER = "karras"

NEGATIVE_PROMPT = (
    "isometric scene, diorama, room, house, shop, building, tile platform, ground, floor, "
    "prop, lamp, snow, tree, background, landscape, screenshot, UI, interface, text, logo, "
    "watermark, second character, multiple characters, duplicate body, cropped feet, cut off, "
    "portrait, closeup, large illustration, anime render, smooth painting, anti-aliasing, blur, "
    "muddy pixels, soft painterly render, 3D render, checkerboard background"
)

PROMPTS = [
    {
        "name": "male_adventurer_idle_south",
        "seed": 21001,
        "prompt": (
            "litiso_sprixen, litiso_style, fp_category adventurers, fp_character male hero, "
            "fp_action idle, fp_direction south, FreePixel character sprite frame, single tiny "
            "male adventurer character only, idle frame, facing south, small 64x64 RPG walk-cycle "
            "sprite proportions, full body including feet, centered transparent background, clean "
            "outline, hard pixel edges, limited palette, no environment, no scene, no floor, no UI"
        ),
    },
    {
        "name": "female_adventurer_walk_east",
        "seed": 21002,
        "prompt": (
            "litiso_sprixen, litiso_style, fp_category adventurers, fp_character female rogue, "
            "fp_action walk, fp_direction east, FreePixel character sprite frame, single tiny "
            "female adventurer character only, walk frame, facing east, small 64x64 RPG walk-cycle "
            "sprite proportions, full body including feet, centered transparent background, clean "
            "outline, hard pixel edges, limited palette, no environment, no scene, no floor, no UI"
        ),
    },
    {
        "name": "knight_idle_north",
        "seed": 21003,
        "prompt": (
            "litiso_sprixen, litiso_style, fp_category warriors, fp_character knight, fp_action idle, "
            "fp_direction north, FreePixel character sprite frame, single tiny knight character only, "
            "idle frame, facing north, small 64x64 RPG walk-cycle sprite proportions, full body "
            "including feet, centered transparent background, clean outline, hard pixel edges, "
            "limited palette, no environment, no scene, no floor, no UI"
        ),
    },
    {
        "name": "skeleton_archer_walk_west",
        "seed": 21004,
        "prompt": (
            "litiso_sprixen, litiso_style, fp_category enemies, fp_character skeleton archer, "
            "fp_action walk, fp_direction west, FreePixel character sprite frame, single tiny "
            "skeleton archer character only, walk frame, facing west, small 64x64 RPG walk-cycle "
            "sprite proportions, full body including feet, centered transparent background, clean "
            "outline, hard pixel edges, limited palette, no environment, no scene, no floor, no UI"
        ),
    },
    {
        "name": "wizard_cast_south_east",
        "seed": 21005,
        "prompt": (
            "litiso_sprixen, litiso_style, fp_category mages, fp_character wizard, fp_action cast, "
            "fp_direction south-east, FreePixel character sprite frame, single tiny wizard character "
            "only, cast frame, facing south-east, small 64x64 RPG walk-cycle sprite proportions, full "
            "body including feet, centered transparent background, clean outline, hard pixel edges, "
            "limited palette, no environment, no scene, no floor, no UI"
        ),
    },
]


def request_json(path: str, payload: dict | None = None, timeout: int = 30) -> dict:
    url = f"{COMFY_URL}{path}"
    data = None
    headers = {}
    if payload is not None:
        data = json.dumps(payload).encode("utf-8")
        headers["Content-Type"] = "application/json"
    req = Request(url, data=data, headers=headers)
    with urlopen(req, timeout=timeout) as response:
        return json.loads(response.read().decode("utf-8"))


def request_bytes(path: str, params: dict, timeout: int = 60) -> bytes:
    url = f"{COMFY_URL}{path}?{urlencode(params)}"
    with urlopen(url, timeout=timeout) as response:
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
        "5": {"class_type": "EmptyLatentImage", "inputs": {"width": 512, "height": 512, "batch_size": 1}},
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


def submit_and_wait(item: dict) -> list[str]:
    queued = request_json("/prompt", {"prompt": workflow(item["prompt"], item["seed"], item["name"])}, timeout=30)
    prompt_id = queued.get("prompt_id")
    if not prompt_id:
        raise RuntimeError(f"ComfyUI did not return a prompt_id: {queued}")

    start = time.time()
    while time.time() - start < 600:
        history = request_json(f"/history/{prompt_id}", timeout=30)
        if prompt_id in history:
            entry = history[prompt_id]
            status = entry.get("status", {})
            if status.get("status_str") == "error":
                raise RuntimeError(f"ComfyUI error for {item['name']}: {status}")
            if status.get("completed"):
                return [
                    image["filename"]
                    for node in entry.get("outputs", {}).values()
                    for image in node.get("images", [])
                ]
        time.sleep(4)
    raise TimeoutError(f"Timed out waiting for {item['name']}")


def main() -> int:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    try:
        request_json("/system_stats", timeout=5)
    except URLError as exc:
        skipped = {
            "status": "skipped",
            "reason": f"ComfyUI is not reachable at {COMFY_URL}: {exc}",
            "checkpoint": CHECKPOINT,
            "lora": LORA,
            "lora_strength": LORA_STRENGTH,
            "planned_prompts": PROMPTS,
        }
        (OUT_DIR / "manifest.json").write_text(json.dumps(skipped, indent=2), encoding="utf-8")
        print(skipped["reason"], file=sys.stderr)
        return 0

    results = []
    for item in PROMPTS:
        print(f"Generating {item['name']}...")
        filenames = submit_and_wait(item)
        saved_files = []
        for index, filename in enumerate(filenames):
            content = request_bytes("/view", {"filename": filename, "subfolder": "", "type": "output"})
            suffix = "" if len(filenames) == 1 else f"_{index + 1}"
            out_path = OUT_DIR / f"{item['name']}{suffix}.png"
            out_path.write_bytes(content)
            saved_files.append(str(out_path))
        results.append({
            "name": item["name"],
            "seed": item["seed"],
            "prompt": item["prompt"],
            "checkpoint": CHECKPOINT,
            "lora": LORA,
            "lora_strength": LORA_STRENGTH,
            "steps": STEPS,
            "cfg": CFG,
            "outputs": saved_files,
        })

    manifest = {
        "status": "complete",
        "checkpoint": CHECKPOINT,
        "lora": LORA,
        "lora_strength": LORA_STRENGTH,
        "results": results,
    }
    (OUT_DIR / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"Saved evaluation outputs to {OUT_DIR}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
