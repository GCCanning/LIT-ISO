"""
Animate the approved CLASS-SELECTION scene (class_scene_01) via PixelLab
animate-with-text-v3 — same composite trick as the menu: the endpoint caps
input at 256x256, so we animate only the LIVE REGION (the radiant being,
its halo, the moon and stars) and composite frames back onto the full still.

Usage:
  py -3 animate_class_scene.py
  py -3 animate_class_scene.py --source class_scene_01.png --frames 8
"""

import argparse
import base64
import io
import json
import os
import sys
import time

from pixellab_common import token, call, find_images

SCENE_DIR = r"C:\Users\garyc\OneDrive\Desktop\PixelArt\Scenes"

ANIM_PROMPT = (
    "subtle ambient loop: the radiant divine light at the mountain peak pulses "
    "slowly and breathes, its glowing halo rings shimmer and rotate gently, "
    "stars twinkle, faint light motes drift upward, moonlight glints; "
    "camera static, scenery static, the figure static, only light moves"
)

# 256x160 window centred on the being + halo + moon (source is 400x224)
CROP = (100, 0, 356, 160)


def find_job_id(node):
    if isinstance(node, dict):
        for key in ("job_id", "background_job_id", "id"):
            if isinstance(node.get(key), str) and len(node[key]) > 8:
                return node[key]
        for v in node.values():
            j = find_job_id(v)
            if j:
                return j
    elif isinstance(node, list):
        for v in node:
            j = find_job_id(v)
            if j:
                return j
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--source", default="class_scene_01.png")
    ap.add_argument("--frames", type=int, default=8)
    ap.add_argument("--prompt", default=ANIM_PROMPT)
    args = ap.parse_args()

    tok = token()
    src_path = os.path.join(SCENE_DIR, args.source)
    if not os.path.exists(src_path):
        sys.exit(f"Source still not found: {src_path}")

    try:
        from PIL import Image
    except ImportError:
        sys.exit("pip install pillow")
    im = Image.open(src_path).convert("RGBA")
    crop = im.crop(CROP)
    buf = io.BytesIO(); crop.save(buf, "PNG")
    src_b64 = base64.b64encode(buf.getvalue()).decode()
    out_dir = os.path.join(SCENE_DIR, "class_frames")
    os.makedirs(out_dir, exist_ok=True)
    with open(os.path.join(out_dir, "_composite.json"), "w") as f:
        json.dump({"source": args.source, "crop": CROP}, f)

    print("Balance:", json.dumps(call(tok, "GET", "/balance")))
    print(f"Animating {args.source} -> {args.frames} frames ... "
          "(this can take a few minutes)")

    payload = {
        "action": args.prompt,
        "first_frame": {"type": "base64", "base64": src_b64},
        "frame_count": args.frames,
    }
    resp = call(tok, "POST", "/animate-with-text-v3", payload)

    images = []
    find_images(resp, images)
    if not images:
        job = find_job_id(resp)
        if job:
            print("Queued as background job:", job)
            while True:
                time.sleep(10)
                status = call(tok, "GET", f"/background-jobs/{job}")
                state = str(status.get("status", status))[:120]
                print("  status:", state)
                find_images(status, images)
                s = str(status.get("status", "")).lower()
                if images:
                    break
                if s in ("failed", "error"):
                    print(json.dumps(status, indent=2)[:4000])
                    sys.exit(1)
        else:
            print("Unexpected response; full body follows:")
            print(json.dumps(resp, indent=2)[:4000])
            sys.exit(1)

    for i, b64 in enumerate(images):
        p = os.path.join(out_dir, f"frame_{i:02d}.png")
        with open(p, "wb") as f:
            f.write(base64.b64decode(b64))
        print("saved:", p)

    print(f"\nDone — {len(images)} frames in {out_dir}. "
          "Tell Claude and the composite + flipbook import happens automatically.")


if __name__ == "__main__":
    main()
