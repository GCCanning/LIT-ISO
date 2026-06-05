#!/usr/bin/env python3
"""
Generate a small fixed-seed evaluation set for litiso_style_directional_v1.

This is intended to run after overnight LoRA training. It uses only the Python
standard library so it can run in lightweight environments. If ComfyUI is not
reachable, it writes a skipped manifest and exits cleanly so the training run
does not get marked as failed after the LoRA has already been produced.
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
OUT_DIR = Path(r"C:\Projects\Pixel Pipeline\generated\litiso_style_directional_v1_eval")
CHECKPOINT = "DreamShaper_8_pruned.safetensors"
LORA = "litiso_style_directional_v1.safetensors"
LORA_STRENGTH = 0.85
STEPS = 28
CFG = 6.5
SAMPLER = "dpmpp_2m"
SCHEDULER = "karras"

NEGATIVE_PROMPT = (
    "text, letters, logo, watermark, caption, frame, border, UI, scene, scenery, "
    "background, ground circle, magic circle behind character, aura floor, platform, "
    "multiple characters, duplicate character, cropped body, portrait, close-up, "
    "blurry, painterly, realistic, 3d render, smooth gradients, extra limbs"
)

PROMPTS = [
    {
        "name": "wizard_idle_south",
        "seed": 11001,
        "prompt": (
            "litiso_style, fp_category mages, fp_character wizard, fp_action idle, "
            "fp_direction south, pixel art RPG character sprite, mages wizard, idle "
            "animation frame, facing south, 8-direction game sprite, full body, "
            "crisp pixel art, clean outline, transparent background, no text, no logo, "
            "no scene, no floor aura, game asset"
        ),
    },
    {
        "name": "wizard_walk_east",
        "seed": 11002,
        "prompt": (
            "litiso_style, fp_category mages, fp_character wizard, fp_action walk, "
            "fp_direction east, pixel art RPG character sprite, mages wizard, walk "
            "animation frame, facing east, 8-direction game sprite, full body, crisp "
            "pixel art, clean outline, transparent background, no text, no logo, no scene, "
            "no floor aura, game asset"
        ),
    },
    {
        "name": "knight_idle_north",
        "seed": 11003,
        "prompt": (
            "litiso_style, fp_category warriors, fp_character knight, fp_action idle, "
            "fp_direction north, pixel art RPG character sprite, warriors knight, idle "
            "animation frame, facing north, 8-direction game sprite, full body, crisp "
            "pixel art, clean outline, transparent background, no text, no logo, no scene, "
            "no floor aura, game asset"
        ),
    },
    {
        "name": "rogue_walk_south_east",
        "seed": 11004,
        "prompt": (
            "litiso_style, fp_category rogues, fp_character rogue, fp_action walk, "
            "fp_direction south-east, pixel art RPG character sprite, rogues rogue, walk "
            "animation frame, facing south-east, 8-direction game sprite, full body, crisp "
            "pixel art, clean outline, transparent background, no text, no logo, no scene, "
            "no floor aura, game asset"
        ),
    },
    {
        "name": "skeleton_archer_run_west",
        "seed": 11005,
        "prompt": (
            "litiso_style, fp_category enemies, fp_character skeleton archer, fp_action run, "
            "fp_direction west, pixel art RPG character sprite, enemies skeleton archer, run "
            "animation frame, facing west, 8-direction game sprite, full body, crisp pixel art, "
            "clean outline, transparent background, no text, no logo, no scene, no floor aura, "
            "game asset"
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
                filenames: list[str] = []
                for node in entry.get("outputs", {}).values():
                    for image in node.get("images", []):
                        filenames.append(image["filename"])
                return filenames
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
        print(f"Wrote skipped evaluation manifest to {OUT_DIR / 'manifest.json'}")
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
