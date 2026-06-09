#!/usr/bin/env python3
"""
Evaluate the LPC motion-template LoRA with a fixed prompt matrix.

This LoRA is not expected to solve final LIT-ISO style by itself. Its job is to
teach action, direction, tool, frame, and bottom-anchor labels from LPC motion
data. The evaluator therefore uses training-caption phrasing on purpose.
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
DEFAULT_OUT_DIR = Path(r"C:\Projects\Pixel Pipeline\generated\litiso_lpc_motion_template_v1_eval")
DEFAULT_CHECKPOINT = "DreamShaper_8_pruned.safetensors"
DEFAULT_LORA = "litiso_lpc_motion_template_v1_final.safetensors"
DEFAULT_STRENGTH = 0.72
DEFAULT_STEPS = 24
DEFAULT_CFG = 6.2
DEFAULT_WIDTH = 512
DEFAULT_HEIGHT = 512
SAMPLER = "dpmpp_2m"
SCHEDULER = "karras"

NEGATIVE_PROMPT = (
    "text, letters, logo, watermark, caption, frame border, UI, scene, scenery, "
    "background, ground, floor, platform, tile, tree, rock, building, multiple characters, "
    "duplicate character, cropped body, cropped feet, portrait, close-up, realistic, 3d render, "
    "smooth painterly render, blurry, antialiased haze, extra limbs, malformed hands"
)

EVAL_PROMPTS = [
    {
        "name": "male_walk_south_f03",
        "seed": 62001,
        "caption": (
            "LPC motion template, male leather adventurer, tool none, walk, south, "
            "frame 4 of 9, 64x64 transparent pixel sprite, LIT-ISO normalized bottom-center anchor"
        ),
    },
    {
        "name": "male_walk_east_f03",
        "seed": 62002,
        "caption": (
            "LPC motion template, male leather adventurer, tool none, walk, east, "
            "frame 4 of 9, 64x64 transparent pixel sprite, LIT-ISO normalized bottom-center anchor"
        ),
    },
    {
        "name": "female_walk_north_f03",
        "seed": 62003,
        "caption": (
            "LPC motion template, female forest scout, tool none, walk, north, "
            "frame 4 of 9, 64x64 transparent pixel sprite, LIT-ISO normalized bottom-center anchor"
        ),
    },
    {
        "name": "female_walk_west_f03",
        "seed": 62004,
        "caption": (
            "LPC motion template, female forest scout, tool none, walk, west, "
            "frame 4 of 9, 64x64 transparent pixel sprite, LIT-ISO normalized bottom-center anchor"
        ),
    },
    {
        "name": "male_longsword_slash_south_f04",
        "seed": 62005,
        "caption": (
            "LPC motion template, male leather adventurer, tool longsword slash, slash attack, south, "
            "frame 5 of 6, 64x64 transparent pixel sprite, LIT-ISO normalized bottom-center anchor"
        ),
    },
    {
        "name": "female_axe_thrust_east_f03",
        "seed": 62006,
        "caption": (
            "LPC motion template, female forest scout, tool axe, thrust attack, east, "
            "frame 4 of 8, 64x64 transparent pixel sprite, LIT-ISO normalized bottom-center anchor"
        ),
    },
    {
        "name": "male_hoe_thrust_west_f03",
        "seed": 62007,
        "caption": (
            "LPC motion template, male leather adventurer, tool hoe, thrust attack, west, "
            "frame 4 of 8, 64x64 transparent pixel sprite, LIT-ISO normalized bottom-center anchor"
        ),
    },
    {
        "name": "female_spellcast_south_f04",
        "seed": 62008,
        "caption": (
            "LPC motion template, female forest scout, tool none, spellcast, south, "
            "frame 5 of 7, 64x64 transparent pixel sprite, LIT-ISO normalized bottom-center anchor"
        ),
    },
    {
        "name": "male_shoot_north_f06",
        "seed": 62009,
        "caption": (
            "LPC motion template, male leather adventurer, tool none, shoot attack, north, "
            "frame 7 of 13, 64x64 transparent pixel sprite, LIT-ISO normalized bottom-center anchor"
        ),
    },
    {
        "name": "female_hurt_south_f03",
        "seed": 62010,
        "caption": (
            "LPC motion template, female forest scout, tool none, hurt, south, "
            "frame 4 of 6, 64x64 transparent pixel sprite, LIT-ISO normalized bottom-center anchor"
        ),
    },
]


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def request_json(base_url: str, path: str, payload: dict | None = None, timeout: int = 30) -> dict:
    url = f"{base_url}{path}"
    data = None
    headers = {}
    if payload is not None:
        data = json.dumps(payload).encode("utf-8")
        headers["Content-Type"] = "application/json"
    req = Request(url, data=data, headers=headers)
    with urlopen(req, timeout=timeout) as response:
        return json.loads(response.read().decode("utf-8"))


def request_bytes(base_url: str, path: str, params: dict, timeout: int = 60) -> bytes:
    url = f"{base_url}{path}?{urlencode(params)}"
    with urlopen(url, timeout=timeout) as response:
        return response.read()


def build_workflow(args: argparse.Namespace, prompt: str, seed: int, prefix: str) -> dict:
    return {
        "1": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": args.checkpoint}},
        "2": {
            "class_type": "LoraLoader",
            "inputs": {
                "lora_name": args.lora,
                "strength_model": args.lora_strength,
                "strength_clip": args.lora_strength,
                "model": ["1", 0],
                "clip": ["1", 1],
            },
        },
        "3": {"class_type": "CLIPTextEncode", "inputs": {"text": prompt, "clip": ["2", 1]}},
        "4": {"class_type": "CLIPTextEncode", "inputs": {"text": NEGATIVE_PROMPT, "clip": ["2", 1]}},
        "5": {"class_type": "EmptyLatentImage", "inputs": {"width": args.width, "height": args.height, "batch_size": 1}},
        "6": {
            "class_type": "KSampler",
            "inputs": {
                "seed": seed,
                "steps": args.steps,
                "cfg": args.cfg,
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


def submit_and_wait(args: argparse.Namespace, item: dict) -> list[str]:
    workflow = build_workflow(args, item["caption"], item["seed"], f"lpc_motion_eval/{item['name']}")
    queued = request_json(args.comfy_url, "/prompt", {"prompt": workflow}, timeout=30)
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


def write_contact_sheet(out_dir: Path, image_paths: list[Path]) -> str | None:
    try:
        from PIL import Image, ImageDraw
    except Exception:
        return None

    if not image_paths:
        return None

    thumbs = []
    for path in image_paths:
        image = Image.open(path).convert("RGBA")
        image.thumbnail((192, 192), Image.Resampling.NEAREST)
        thumbs.append((path, image.copy()))

    columns = 5
    cell_w = 224
    cell_h = 240
    rows = (len(thumbs) + columns - 1) // columns
    sheet = Image.new("RGBA", (columns * cell_w, rows * cell_h), (18, 21, 28, 255))
    draw = ImageDraw.Draw(sheet)

    for index, (path, image) in enumerate(thumbs):
        x = (index % columns) * cell_w
        y = (index // columns) * cell_h
        bg = Image.new("RGBA", image.size, (255, 255, 255, 255))
        bg.alpha_composite(image)
        sheet.alpha_composite(bg, (x + (cell_w - image.width) // 2, y + 12))
        label = path.stem[:28]
        draw.text((x + 10, y + cell_h - 32), label, fill=(230, 235, 245, 255))

    contact = out_dir / "lpc_motion_template_eval_contact.png"
    sheet.save(contact)
    return str(contact)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Evaluate the LPC motion-template LoRA in ComfyUI.")
    parser.add_argument("--comfy-url", default=DEFAULT_COMFY_URL)
    parser.add_argument("--out-dir", type=Path, default=DEFAULT_OUT_DIR)
    parser.add_argument("--checkpoint", default=DEFAULT_CHECKPOINT)
    parser.add_argument("--lora", default=DEFAULT_LORA)
    parser.add_argument("--lora-strength", type=float, default=DEFAULT_STRENGTH)
    parser.add_argument("--steps", type=int, default=DEFAULT_STEPS)
    parser.add_argument("--cfg", type=float, default=DEFAULT_CFG)
    parser.add_argument("--width", type=int, default=DEFAULT_WIDTH)
    parser.add_argument("--height", type=int, default=DEFAULT_HEIGHT)
    parser.add_argument("--timeout-seconds", type=int, default=600)
    parser.add_argument("--limit", type=int, default=0, help="Only run the first N prompts. 0 runs the full matrix.")
    parser.add_argument("--dry-run", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    args.out_dir.mkdir(parents=True, exist_ok=True)

    manifest = {
        "schemaVersion": 1,
        "status": "planned" if args.dry_run else "running",
        "started_utc": utc_now(),
        "purpose": "Evaluate whether the LPC LoRA learned action, direction, tool, frame, and anchor labels.",
        "checkpoint": args.checkpoint,
        "lora": args.lora,
        "lora_strength": args.lora_strength,
        "steps": args.steps,
        "cfg": args.cfg,
        "sampler": SAMPLER,
        "scheduler": SCHEDULER,
        "width": args.width,
        "height": args.height,
        "negative_prompt": NEGATIVE_PROMPT,
        "prompts": EVAL_PROMPTS[: args.limit] if args.limit > 0 else EVAL_PROMPTS,
        "review_questions": [
            "Do north/east/south/west prompts visibly change facing direction?",
            "Do walk/slash/thrust/spellcast/shoot/hurt prompts visibly change pose?",
            "Do tool prompts introduce the intended tool without ruining the body?",
            "Do outputs keep bottom-center sprite framing instead of becoming portraits or scenes?",
            "Is the result useful as a motion/template LoRA, even if final style still needs a separate style lock?",
        ],
    }

    if args.dry_run:
        manifest["completed_utc"] = utc_now()
        manifest["status"] = "planned"
        (args.out_dir / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        print(json.dumps({"planned": True, "manifest": str(args.out_dir / "manifest.json")}, indent=2))
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

    image_paths: list[Path] = []
    results = []
    prompts = EVAL_PROMPTS[: args.limit] if args.limit > 0 else EVAL_PROMPTS
    for item in prompts:
        print(f"Generating {item['name']}...")
        filenames = submit_and_wait(args, item)
        saved_files = []
        for index, filename in enumerate(filenames):
            content = request_bytes(args.comfy_url, "/view", {"filename": filename, "subfolder": "lpc_motion_eval", "type": "output"})
            suffix = "" if len(filenames) == 1 else f"_{index + 1}"
            out_path = args.out_dir / f"{item['name']}{suffix}.png"
            out_path.write_bytes(content)
            image_paths.append(out_path)
            saved_files.append(str(out_path))
        results.append({"name": item["name"], "seed": item["seed"], "caption": item["caption"], "outputs": saved_files})

    manifest["status"] = "complete"
    manifest["completed_utc"] = utc_now()
    manifest["results"] = results
    manifest["contact_sheet"] = write_contact_sheet(args.out_dir, image_paths)
    (args.out_dir / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"Saved LPC motion-template evaluation to {args.out_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
