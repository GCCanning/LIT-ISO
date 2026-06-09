#!/usr/bin/env python3
"""
Evaluate the litiso_direction_oracle_anchor_v1 LoRA with fixed direction prompts.

This LoRA is an experimental direction/body-anchor LoRA, not the final
production art-style LoRA. The evaluator intentionally asks simple S/E/N/W
questions so review can focus on whether direction tokens steer the sprite
without creating scenes, floors, portraits, or duplicated bodies.
"""

from __future__ import annotations

import argparse
import json
import sys
import time
from datetime import datetime, timezone
from pathlib import Path
from urllib.error import URLError
from urllib.parse import urlencode
from urllib.request import Request, urlopen

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")

DEFAULT_COMFY_URL = "http://127.0.0.1:8188"
DEFAULT_OUT_DIR = Path(r"C:\Projects\Pixel Pipeline\generated\litiso_direction_oracle_anchor_v1_eval")
DEFAULT_CHECKPOINT = "DreamShaper_8_pruned.safetensors"
DEFAULT_LORA = "litiso_direction_oracle_anchor_v1_final.safetensors"
DEFAULT_STRENGTH = 0.68
DEFAULT_STYLE_LORA = ""
DEFAULT_STYLE_STRENGTH = 0.0
DEFAULT_STEPS = 24
DEFAULT_CFG = 6.2
DEFAULT_WIDTH = 512
DEFAULT_HEIGHT = 512
DEFAULT_PROMPT_PREFIX = ""
DEFAULT_PROMPT_SUFFIX = ""
SAMPLER = "dpmpp_2m"
SCHEDULER = "karras"

NEGATIVE_PROMPT = (
    "text, letters, logo, watermark, caption, frame border, UI, scene, scenery, "
    "background, terrain, tile, floor, platform, ground circle, shadow blob, "
    "multiple characters, duplicate character, sprite sheet, grid, contact sheet, "
    "cropped body, cropped feet, portrait, close-up, realistic, 3d render, "
    "smooth painterly render, blurry, antialiased haze, extra limbs, malformed hands"
)

EVAL_PROMPTS = [
    {
        "name": "male_leather_walk_south",
        "seed": 71001,
        "direction": "S",
        "caption": (
            "LIT-ISO direction oracle, male leather adventurer, walk reference pose, "
            "south, facing toward camera, full body pixel sprite, transparent background, "
            "bottom-center anchor, clean RPG game asset"
        ),
    },
    {
        "name": "male_leather_walk_east",
        "seed": 71002,
        "direction": "E",
        "caption": (
            "LIT-ISO direction oracle, male leather adventurer, walk reference pose, "
            "east, side view facing right, full body pixel sprite, transparent background, "
            "bottom-center anchor, clean RPG game asset"
        ),
    },
    {
        "name": "male_leather_walk_north",
        "seed": 71003,
        "direction": "N",
        "caption": (
            "LIT-ISO direction oracle, male leather adventurer, walk reference pose, "
            "north, back view facing away from camera, full body pixel sprite, "
            "transparent background, bottom-center anchor, clean RPG game asset"
        ),
    },
    {
        "name": "male_leather_walk_west",
        "seed": 71004,
        "direction": "W",
        "caption": (
            "LIT-ISO direction oracle, male leather adventurer, walk reference pose, "
            "west, side view facing left, full body pixel sprite, transparent background, "
            "bottom-center anchor, clean RPG game asset"
        ),
    },
    {
        "name": "female_forest_walk_south",
        "seed": 71005,
        "direction": "S",
        "caption": (
            "LIT-ISO direction oracle, female forest scout, walk reference pose, "
            "south, facing toward camera, full body pixel sprite, transparent background, "
            "bottom-center anchor, clean RPG game asset"
        ),
    },
    {
        "name": "female_forest_walk_east",
        "seed": 71006,
        "direction": "E",
        "caption": (
            "LIT-ISO direction oracle, female forest scout, walk reference pose, "
            "east, side view facing right, full body pixel sprite, transparent background, "
            "bottom-center anchor, clean RPG game asset"
        ),
    },
    {
        "name": "female_forest_walk_north",
        "seed": 71007,
        "direction": "N",
        "caption": (
            "LIT-ISO direction oracle, female forest scout, walk reference pose, "
            "north, back view facing away from camera, full body pixel sprite, "
            "transparent background, bottom-center anchor, clean RPG game asset"
        ),
    },
    {
        "name": "female_forest_walk_west",
        "seed": 71008,
        "direction": "W",
        "caption": (
            "LIT-ISO direction oracle, female forest scout, walk reference pose, "
            "west, side view facing left, full body pixel sprite, transparent background, "
            "bottom-center anchor, clean RPG game asset"
        ),
    },
    {
        "name": "cyan_knight_idle_south",
        "seed": 71009,
        "direction": "S",
        "caption": (
            "LIT-ISO direction oracle, armored knight with cyan energy trim and amber runes, "
            "idle reference pose, south, facing toward camera, full body pixel sprite, "
            "transparent background, bottom-center anchor, clean RPG game asset"
        ),
    },
    {
        "name": "cyan_knight_idle_north",
        "seed": 71010,
        "direction": "N",
        "caption": (
            "LIT-ISO direction oracle, armored knight with cyan energy trim and amber runes, "
            "idle reference pose, north, back view facing away from camera, full body pixel sprite, "
            "transparent background, bottom-center anchor, clean RPG game asset"
        ),
    },
]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def request_json(base_url: str, path: str, payload: dict | None = None, timeout: int = 30) -> dict:
    data = None
    headers = {}
    if payload is not None:
        data = json.dumps(payload).encode("utf-8")
        headers["Content-Type"] = "application/json"
    req = Request(f"{base_url}{path}", data=data, headers=headers)
    with urlopen(req, timeout=timeout) as response:
        return json.loads(response.read().decode("utf-8"))


def request_bytes(base_url: str, path: str, params: dict, timeout: int = 60) -> bytes:
    with urlopen(f"{base_url}{path}?{urlencode(params)}", timeout=timeout) as response:
        return response.read()


def build_workflow(args: argparse.Namespace, prompt: str, seed: int, prefix: str) -> dict:
    nodes = {
        "1": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": args.checkpoint}},
    }

    model_ref = ["1", 0]
    clip_ref = ["1", 1]
    next_id = 2
    if args.style_lora:
        style_id = str(next_id)
        nodes[style_id] = {
            "class_type": "LoraLoader",
            "inputs": {
                "lora_name": args.style_lora,
                "strength_model": args.style_strength,
                "strength_clip": args.style_strength,
                "model": model_ref,
                "clip": clip_ref,
            },
        }
        model_ref = [style_id, 0]
        clip_ref = [style_id, 1]
        next_id += 1

    direction_id = str(next_id)
    nodes[direction_id] = {
            "class_type": "LoraLoader",
            "inputs": {
                "lora_name": args.lora,
                "strength_model": args.lora_strength,
                "strength_clip": args.lora_strength,
                "model": model_ref,
                "clip": clip_ref,
            },
        }
    model_ref = [direction_id, 0]
    clip_ref = [direction_id, 1]
    next_id += 1

    positive_id = str(next_id)
    negative_id = str(next_id + 1)
    latent_id = str(next_id + 2)
    sampler_id = str(next_id + 3)
    decode_id = str(next_id + 4)
    save_id = str(next_id + 5)

    nodes[positive_id] = {"class_type": "CLIPTextEncode", "inputs": {"text": prompt, "clip": clip_ref}}
    nodes[negative_id] = {"class_type": "CLIPTextEncode", "inputs": {"text": NEGATIVE_PROMPT, "clip": clip_ref}}
    nodes[latent_id] = {"class_type": "EmptyLatentImage", "inputs": {"width": args.width, "height": args.height, "batch_size": 1}}
    nodes[sampler_id] = {
            "class_type": "KSampler",
            "inputs": {
                "seed": seed,
                "steps": args.steps,
                "cfg": args.cfg,
                "sampler_name": SAMPLER,
                "scheduler": SCHEDULER,
                "denoise": 1.0,
                "model": model_ref,
                "positive": [positive_id, 0],
                "negative": [negative_id, 0],
                "latent_image": [latent_id, 0],
            },
    }
    nodes[decode_id] = {"class_type": "VAEDecode", "inputs": {"samples": [sampler_id, 0], "vae": ["1", 2]}}
    nodes[save_id] = {"class_type": "SaveImage", "inputs": {"filename_prefix": prefix, "images": [decode_id, 0]}}
    return nodes


def submit_and_wait(args: argparse.Namespace, item: dict) -> list[str]:
    prefix = f"direction_oracle_anchor_eval/{item['name']}"
    prompt = " ".join(part.strip() for part in (args.prompt_prefix, item["caption"], args.prompt_suffix) if part.strip())
    queued = request_json(args.comfy_url, "/prompt", {"prompt": build_workflow(args, prompt, item["seed"], prefix)}, timeout=30)
    prompt_id = queued.get("prompt_id")
    if not prompt_id:
        raise RuntimeError(f"ComfyUI did not return a prompt_id: {queued}")

    start = time.time()
    while time.time() - start < args.timeout_seconds:
        history = request_json(args.comfy_url, f"/history/{prompt_id}", timeout=30)
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
        time.sleep(3)
    raise TimeoutError(f"Timed out waiting for {item['name']}")


def write_contact_sheet(out_dir: Path, records: list[dict]) -> str | None:
    try:
        from PIL import Image, ImageDraw
    except Exception:
        return None

    if not records:
        return None

    cell_w = 220
    cell_h = 252
    columns = 4
    rows = (len(records) + columns - 1) // columns
    sheet = Image.new("RGBA", (columns * cell_w, rows * cell_h), (18, 21, 28, 255))
    draw = ImageDraw.Draw(sheet)

    for index, record in enumerate(records):
        path = Path(record["outputs"][0])
        image = Image.open(path).convert("RGBA")
        image.thumbnail((192, 192), Image.Resampling.NEAREST)
        x = (index % columns) * cell_w
        y = (index // columns) * cell_h
        bg = Image.new("RGBA", (192, 192), (245, 246, 248, 255))
        bg.alpha_composite(image, ((192 - image.width) // 2, (192 - image.height) // 2))
        sheet.alpha_composite(bg, (x + 14, y + 12))
        draw.rectangle((x + 14, y + 12, x + 205, y + 203), outline=(65, 72, 84, 255))
        draw.text((x + 14, y + 212), f"{record['direction']}  {path.stem[:22]}", fill=(230, 235, 245, 255))

    contact = out_dir / "direction_oracle_anchor_eval_contact.png"
    sheet.save(contact)
    return str(contact)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Evaluate the direction-oracle anchor LoRA in ComfyUI.")
    parser.add_argument("--comfy-url", default=DEFAULT_COMFY_URL)
    parser.add_argument("--out-dir", type=Path, default=DEFAULT_OUT_DIR)
    parser.add_argument("--checkpoint", default=DEFAULT_CHECKPOINT)
    parser.add_argument("--lora", default=DEFAULT_LORA)
    parser.add_argument("--lora-strength", type=float, default=DEFAULT_STRENGTH)
    parser.add_argument("--style-lora", default=DEFAULT_STYLE_LORA, help="Optional style LoRA to apply before the direction LoRA.")
    parser.add_argument("--style-strength", type=float, default=DEFAULT_STYLE_STRENGTH)
    parser.add_argument("--steps", type=int, default=DEFAULT_STEPS)
    parser.add_argument("--cfg", type=float, default=DEFAULT_CFG)
    parser.add_argument("--width", type=int, default=DEFAULT_WIDTH)
    parser.add_argument("--height", type=int, default=DEFAULT_HEIGHT)
    parser.add_argument("--prompt-prefix", default=DEFAULT_PROMPT_PREFIX)
    parser.add_argument("--prompt-suffix", default=DEFAULT_PROMPT_SUFFIX)
    parser.add_argument("--timeout-seconds", type=int, default=600)
    parser.add_argument("--limit", type=int, default=0, help="Only run the first N prompts. 0 runs the full matrix.")
    parser.add_argument("--dry-run", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    args.out_dir.mkdir(parents=True, exist_ok=True)
    prompts = EVAL_PROMPTS[: args.limit] if args.limit > 0 else EVAL_PROMPTS

    manifest = {
        "schemaVersion": 1,
        "status": "planned" if args.dry_run else "running",
        "started_utc": utc_now(),
        "purpose": "Evaluate whether the direction-oracle LoRA learned cardinal facing and bottom-anchor cues.",
        "checkpoint": args.checkpoint,
        "lora": args.lora,
        "lora_strength": args.lora_strength,
        "style_lora": args.style_lora,
        "style_strength": args.style_strength,
        "steps": args.steps,
        "cfg": args.cfg,
        "sampler": SAMPLER,
        "scheduler": SCHEDULER,
        "width": args.width,
        "height": args.height,
        "negative_prompt": NEGATIVE_PROMPT,
        "prompt_prefix": args.prompt_prefix,
        "prompt_suffix": args.prompt_suffix,
        "prompts": prompts,
        "review_questions": [
            "Does south face toward the camera?",
            "Does north show a back view, not a front view?",
            "Do east and west become true side views instead of near-front variants?",
            "Do all outputs stay as isolated full-body sprites with no floor or scene?",
            "Is the LoRA useful as a direction anchor even if final art style still needs a separate style LoRA?",
        ],
    }

    if args.dry_run:
        manifest["completed_utc"] = utc_now()
        (args.out_dir / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        print(json.dumps({"planned": True, "manifest": str(args.out_dir / "manifest.json"), "prompt_count": len(prompts)}, indent=2))
        return 0

    try:
        request_json(args.comfy_url, "/system_stats", timeout=5)
    except URLError as exc:
        manifest["status"] = "skipped"
        manifest["reason"] = f"ComfyUI is not reachable at {args.comfy_url}: {exc}"
        manifest["completed_utc"] = utc_now()
        (args.out_dir / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        print(manifest["reason"], file=sys.stderr)
        return 0

    results = []
    for item in prompts:
        print(f"Generating {item['name']}...")
        filenames = submit_and_wait(args, item)
        saved_files = []
        for index, filename in enumerate(filenames):
            content = request_bytes(
                args.comfy_url,
                "/view",
                {"filename": filename, "subfolder": "direction_oracle_anchor_eval", "type": "output"},
            )
            suffix = "" if len(filenames) == 1 else f"_{index + 1}"
            out_path = args.out_dir / f"{item['name']}{suffix}.png"
            out_path.write_bytes(content)
            saved_files.append(str(out_path))
        results.append(
            {
                "name": item["name"],
                "direction": item["direction"],
                "seed": item["seed"],
                "caption": item["caption"],
                "prompt": " ".join(part.strip() for part in (args.prompt_prefix, item["caption"], args.prompt_suffix) if part.strip()),
                "outputs": saved_files,
            }
        )

    manifest["status"] = "complete"
    manifest["completed_utc"] = utc_now()
    manifest["results"] = results
    manifest["contact_sheet"] = write_contact_sheet(args.out_dir, results)
    (args.out_dir / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"Saved direction-oracle anchor evaluation to {args.out_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
