#!/usr/bin/env python3
from __future__ import annotations

import json
import argparse
import sys
import time
from datetime import datetime, timezone
from pathlib import Path
from urllib.error import URLError
from urllib.parse import urlencode
from urllib.request import Request, urlopen

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")

COMFY_URL = "http://127.0.0.1:8188"
OUT_DIR = Path(r"C:\Projects\Pixel Pipeline\generated\litiso_tile_prop_v1_eval")
CHECKPOINT = "DreamShaper_8_pruned.safetensors"
LORA = "litiso_tile_prop_v1_final.safetensors"
LORA_STRENGTH = 0.75
STEPS = 22
CFG = 6.0
SAMPLER = "dpmpp_2m"
SCHEDULER = "karras"

NEGATIVE = (
    "photorealistic, 3d render, soft blur, antialiasing haze, text, watermark, logo, "
    "large scene, background, character, person, animal, extra object, duplicate, floor base, "
    "isometric diorama, building, tree on terrain tile, prop baked into tile, cropped, cut off, "
    "large diamond border, smooth vector art, procedural repeated marks, floating object, full scene"
)

PRODUCTION_GUIDANCE = (
    "Live ComfyUI evaluation requires the next implementation step: add a verified ComfyUI "
    "runner contract for this LoRA, including checkpoint/LoRA presence checks, workflow-node "
    "compatibility checks, output image validation, and artifact import handoff. Until that is "
    "implemented, run this evaluator with --dry-run for manifest/prompt validation or start "
    "ComfyUI locally with the configured checkpoint and LoRA installed."
)

STYLE_LOCK_PROFILE = "Tools/AssetForge/style_profiles/litiso_iso_reference_v1.json"
STYLE_LOCK_REFERENCE_SHEET = "Assets/Generated/_Review/style_lock_sources/style_lock_analysis/tileset_contact_sheet.png"
STYLE_LOCK_TARGETS = [
    {"family": "earth_block", "reference_tiles": ["tile_000", "tile_001", "tile_004", "tile_008", "tile_017", "tile_018"]},
    {"family": "grass_top", "reference_tiles": ["tile_022", "tile_023", "tile_024", "tile_037", "tile_038", "tile_039", "tile_040"]},
    {"family": "stone_water", "reference_tiles": ["tile_061", "tile_066", "tile_070", "tile_071", "tile_077", "tile_078"]},
    {"family": "dark_water", "reference_tiles": ["tile_086", "tile_087", "tile_088", "tile_089", "tile_090", "tile_095"]},
    {"family": "ice", "reference_tiles": ["tile_104", "tile_105", "tile_106", "tile_107", "tile_108", "tile_109", "tile_114"]},
]

STYLE_LOCK_PHRASE = (
    "match the supplied LIT-ISO isometric tileset style: compact 32x32 pixel art cell, "
    "dark warm outline, hand-authored cluster dithering, limited palette, transparent corners, "
    "no baked decoration on top"
)

PROMPT_PRESETS = [
    {
        "name": "stylelock_earth_flat_top",
        "seed": 60101,
        "category": "terrain",
        "subcategory": "earth",
        "asset_kind": "tile_top",
        "style_target_family": "earth_block",
        "review_focus": ["transparent_background", "32x32_scale", "earth_palette", "no_baked_props"],
        "prompt": (
            "LIT-ISO isometric terrain tile, single 32x32 earth top block tile only, "
            "brown dirt top with dark underside lip, compact walkable tile, "
            f"{STYLE_LOCK_PHRASE}"
        ),
    },
    {
        "name": "stylelock_grass_flat_top",
        "seed": 60102,
        "category": "terrain",
        "subcategory": "grass",
        "asset_kind": "tile_top",
        "style_target_family": "grass_top",
        "review_focus": ["transparent_background", "32x32_scale", "grass_palette", "no_baked_props"],
        "prompt": (
            "LIT-ISO isometric terrain tile, single 32x32 bright grass top tile only, "
            "chunky organic grass edge, small hand-dithered grass surface, "
            f"{STYLE_LOCK_PHRASE}"
        ),
    },
    {
        "name": "stylelock_grass_to_earth_edge",
        "seed": 60103,
        "category": "terrain",
        "subcategory": "transition",
        "asset_kind": "edge_transition",
        "style_target_family": "grass_top",
        "review_focus": ["transparent_background", "32x32_scale", "organic_edge", "no_baked_props"],
        "prompt": (
            "LIT-ISO isometric terrain tile, single 32x32 grass over brown earth edge tile only, "
            "green top surface with dark brown underside and organic pixel edge, "
            f"{STYLE_LOCK_PHRASE}"
        ),
    },
    {
        "name": "stylelock_stone_water_edge",
        "seed": 60104,
        "category": "terrain",
        "subcategory": "stone_water",
        "asset_kind": "edge_transition",
        "style_target_family": "stone_water",
        "review_focus": ["transparent_background", "32x32_scale", "stone_palette", "water_rim"],
        "prompt": (
            "LIT-ISO isometric terrain tile, single 32x32 gray stone tile with blue water rim only, "
            "compact stone cluster, dark outline, small blue water highlights, "
            f"{STYLE_LOCK_PHRASE}"
        ),
    },
    {
        "name": "stylelock_dark_water_tile",
        "seed": 60105,
        "category": "terrain",
        "subcategory": "water",
        "asset_kind": "water_tile",
        "style_target_family": "dark_water",
        "review_focus": ["transparent_background", "32x32_scale", "dark_blue_palette", "subtle_ripple"],
        "prompt": (
            "LIT-ISO isometric terrain tile, single 32x32 dark blue water diamond tile only, "
            "subtle hand-placed ripple pixels, dark navy depth, no shore props, "
            f"{STYLE_LOCK_PHRASE}"
        ),
    },
    {
        "name": "stylelock_ice_tile",
        "seed": 60106,
        "category": "terrain",
        "subcategory": "ice",
        "asset_kind": "ice_tile",
        "style_target_family": "ice",
        "review_focus": ["transparent_background", "32x32_scale", "ice_palette", "clean_edge"],
        "prompt": (
            "LIT-ISO isometric terrain tile, single 32x32 pale blue ice tile only, "
            "crisp cold palette, small hand-authored highlight clusters, clean edge, "
            f"{STYLE_LOCK_PHRASE}"
        ),
    },
]

PROMPTS = PROMPT_PRESETS
PROMPT_CATEGORIES = {
    "terrain": {
        "description": "Walkable or transition isometric terrain cells. Review as 32x32 tilemap pieces, not decorative sprites.",
        "presets": [item["name"] for item in PROMPT_PRESETS if item["category"] == "terrain"],
    }
}


def request_json(path: str, payload: dict | None = None, timeout: int = 30) -> dict:
    data = None
    headers = {}
    if payload is not None:
        data = json.dumps(payload).encode("utf-8")
        headers["Content-Type"] = "application/json"
    req = Request(f"{COMFY_URL}{path}", data=data, headers=headers)
    with urlopen(req, timeout=timeout) as response:
        return json.loads(response.read().decode("utf-8"))


def request_bytes(path: str, params: dict, timeout: int = 60) -> bytes:
    with urlopen(f"{COMFY_URL}{path}?{urlencode(params)}", timeout=timeout) as response:
        return response.read()


def workflow(item: dict) -> dict:
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
        "3": {"class_type": "CLIPTextEncode", "inputs": {"text": item["prompt"], "clip": ["2", 1]}},
        "4": {"class_type": "CLIPTextEncode", "inputs": {"text": NEGATIVE, "clip": ["2", 1]}},
        "5": {"class_type": "EmptyLatentImage", "inputs": {"width": 512, "height": 512, "batch_size": 1}},
        "6": {
            "class_type": "KSampler",
            "inputs": {
                "seed": item["seed"],
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
        "8": {"class_type": "SaveImage", "inputs": {"filename_prefix": f"litiso_tile_prop_v1/{item['name']}", "images": ["7", 0]}},
    }


def now_utc() -> str:
    return datetime.now(timezone.utc).isoformat()


def manifest_base(status: str) -> dict:
    return {
        "schema_version": 2,
        "status": status,
        "created_at": now_utc(),
        "evaluator": "eval_litiso_tile_prop_v1_comfy.py",
        "requires_network": False if status == "dry-run" else "local ComfyUI only",
        "production_guidance": PRODUCTION_GUIDANCE,
        "settings": {
            "lora": LORA,
            "checkpoint": CHECKPOINT,
            "lora_strength": LORA_STRENGTH,
            "steps": STEPS,
            "cfg": CFG,
            "sampler": SAMPLER,
            "scheduler": SCHEDULER,
            "comfy_url": COMFY_URL,
            "out_dir": str(OUT_DIR),
        },
        "style_lock": {
            "profile": STYLE_LOCK_PROFILE,
            "reference_sheet": STYLE_LOCK_REFERENCE_SHEET,
            "target_families": STYLE_LOCK_TARGETS,
            "acceptance_gate": [
                "32x32 compact tile scale",
                "transparent corners/background",
                "no baked props or trees on terrain tiles",
                "matches dark outline and clustered hand-authored dithering",
                "matches the selected target family palette before any Unity promotion",
            ],
        },
        "prompt_categories": PROMPT_CATEGORIES,
    }


def failure_details(exc: BaseException) -> dict:
    return {
        "error": str(exc),
        "guidance": PRODUCTION_GUIDANCE,
        "next_steps": [
            "Use --dry-run to validate manifest and prompt preset output without ComfyUI.",
            "Start ComfyUI locally at --comfy-url and confirm /system_stats responds.",
            "Install the configured checkpoint under ComfyUI/models/checkpoints.",
            "Sync the LoRA into ComfyUI/models/loras before running live evaluation.",
            "Implement the production runner checks before treating live output as shippable.",
        ],
    }


def configure_from_args() -> bool:
    global COMFY_URL, OUT_DIR, CHECKPOINT, LORA, LORA_STRENGTH, STEPS, CFG, SAMPLER, SCHEDULER
    parser = argparse.ArgumentParser()
    parser.add_argument("--comfy-url", default=COMFY_URL)
    parser.add_argument("--out-dir", default=str(OUT_DIR))
    parser.add_argument("--checkpoint", default=CHECKPOINT)
    parser.add_argument("--lora", default=LORA)
    parser.add_argument("--lora-strength", type=float, default=LORA_STRENGTH)
    parser.add_argument("--steps", type=int, default=STEPS)
    parser.add_argument("--cfg", type=float, default=CFG)
    parser.add_argument("--sampler", default=SAMPLER)
    parser.add_argument("--scheduler", default=SCHEDULER)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    COMFY_URL = args.comfy_url.rstrip("/")
    OUT_DIR = Path(args.out_dir)
    CHECKPOINT = args.checkpoint
    LORA = args.lora
    LORA_STRENGTH = args.lora_strength
    STEPS = args.steps
    CFG = args.cfg
    SAMPLER = args.sampler
    SCHEDULER = args.scheduler
    return args.dry_run


def submit_and_fetch(item: dict) -> list[Path]:
    queued = request_json("/prompt", {"prompt": workflow(item)}, timeout=30)
    prompt_id = queued.get("prompt_id")
    if not prompt_id:
        raise RuntimeError(f"ComfyUI did not return prompt_id: {queued}")
    start = time.time()
    while time.time() - start < 600:
        history = request_json(f"/history/{prompt_id}", timeout=30)
        if prompt_id in history:
            outputs = history[prompt_id].get("outputs", {})
            saved = []
            for output in outputs.values():
                for image in output.get("images", []):
                    data = request_bytes("/view", image, timeout=60)
                    dest = OUT_DIR / f"{item['name']}_{image.get('filename', 'output.png')}"
                    dest.parent.mkdir(parents=True, exist_ok=True)
                    dest.write_bytes(data)
                    saved.append(dest)
            return saved
        time.sleep(1.5)
    raise TimeoutError(f"Timed out waiting for {item['name']}")


def main() -> int:
    dry_run = configure_from_args()
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    if dry_run:
        manifest = manifest_base("dry-run")
        manifest["results"] = [{**item, "status": "dry-run"} for item in PROMPTS]
        (OUT_DIR / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        print(OUT_DIR / "manifest.json")
        return 0

    results = []
    for item in PROMPTS:
        print(f"Generating {item['name']}...")
        try:
            paths = submit_and_fetch(item)
            results.append({**item, "status": "ok", "outputs": [str(p) for p in paths]})
        except (URLError, TimeoutError, RuntimeError) as exc:
            results.append({**item, "status": "error", **failure_details(exc)})
            print(f"ERROR {item['name']}: {exc}")
            print(f"GUIDANCE: {PRODUCTION_GUIDANCE}")
    manifest = manifest_base("complete" if all(r["status"] == "ok" for r in results) else "failed")
    manifest["results"] = results
    (OUT_DIR / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(OUT_DIR / "manifest.json")
    return 0 if all(r["status"] == "ok" for r in results) else 1


if __name__ == "__main__":
    raise SystemExit(main())
