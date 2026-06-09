#!/usr/bin/env python3
from __future__ import annotations

import importlib.util
import sys
from pathlib import Path

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")

base_path = Path(__file__).with_name("eval_litiso_oga8d_motion_direction_v1_comfy.py")
spec = importlib.util.spec_from_file_location("oga8d_base_eval", base_path)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Could not load base eval script: {base_path}")

module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

module.OUT_DIR = Path(r"C:\Projects\Pixel Pipeline\generated\litiso_oga8d_composite_motion_v1_eval")
module.LORA = "litiso_oga8d_composite_motion_v1_final.safetensors"
module.LORA_STRENGTH = 0.7


def composite_prompt_for(direction: str) -> str:
    phrase = module.DIRECTION_PHRASE[direction]
    return (
        "litiso_oga8d_composite_motion_reference, cc_by_oga_source, "
        "character preset forest_guard, human forest guard with vest light armor sword and buckler, "
        "walk animation, "
        f"facing {phrase}, direction {direction}, frame 6 of 11, "
        "8-direction RPG character motion, full body centered game sprite frame, "
        "single fantasy game character, readable silhouette, crisp pixel art sprite, "
        "transparent background, no scene, no floor, no text"
    )


module.prompt_for = composite_prompt_for

if __name__ == "__main__":
    raise SystemExit(module.main())
