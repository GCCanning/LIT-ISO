"""
Animate the approved cinematic campfire-party menu still via PixelLab REST v2.

The importer already reads PixelArt/MenuScene/frames/frame_*.png, so this
script writes the generated loop frames there and includes composite metadata
for Tools/PixelLab/import_menu_frames.py.

Usage:
  py -3 animate_campfire_party_menu_scene.py --source CampfireParty/campfire_party_cinematic_01.png
  py -3 animate_campfire_party_menu_scene.py --source CampfireParty/campfire_party_cinematic_02.png --frames 9

After this finishes:
  py -3 import_menu_frames.py
"""

import argparse
import base64
import json
import os
import sys
import time
import urllib.error
import urllib.request

API = "https://api.pixellab.ai/v2"
SCENE_DIR = r"C:\Users\garyc\OneDrive\Desktop\PixelArt\MenuScene"
FRAMES_DIR = os.path.join(SCENE_DIR, "frames")
TOKEN_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                          "pixellab_token.local.txt")
TOKEN_ENV = "PIXELLAB_TOKEN"

ANIM_PROMPT = (
    "subtle seamless cinematic main-menu pixel-art loop: campfire flames "
    "flicker naturally, sparks rise, smoke curls upward, fireflies drift, "
    "warm orange light gently pulses across the seated adventurers, tents, "
    "crates, mugs, and ground, mage staff glow breathes softly, tent cloth "
    "and pine branches sway slightly in night wind. Camera static, characters "
    "remain seated and resting, no walking, no new objects, no composition "
    "changes, no text."
)


def load_token():
    env_token = os.environ.get(TOKEN_ENV, "").strip()
    if env_token:
        return env_token[7:] if env_token.lower().startswith("bearer ") else env_token
    try:
        tok = open(TOKEN_FILE, encoding="utf-8").read().strip()
    except OSError:
        sys.exit(f"Token missing: set {TOKEN_ENV} or create {TOKEN_FILE}")
    return tok[7:] if tok.lower().startswith("bearer ") else tok


def call(token, method, path, payload=None, timeout=600):
    req = urllib.request.Request(
        API + path,
        data=json.dumps(payload).encode() if payload is not None else None,
        method=method,
        headers={
            "Authorization": f"Bearer {token}",
            "Content-Type": "application/json",
            "Accept": "application/json",
        },
    )
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        body = e.read().decode(errors="replace")
        print(f"\nHTTP {e.code} on {method} {path}:\n{body}\n")
        raise SystemExit(1)


def find_base64_images(node, found):
    if isinstance(node, dict):
        if node.get("type") == "base64" and isinstance(node.get("base64"), str):
            found.append(node["base64"])
        else:
            for value in node.values():
                find_base64_images(value, found)
    elif isinstance(node, list):
        for value in node:
            find_base64_images(value, found)


def find_job_id(node):
    if isinstance(node, dict):
        for key in ("job_id", "background_job_id", "id"):
            if isinstance(node.get(key), str) and len(node[key]) > 8:
                return node[key]
        for value in node.values():
            job_id = find_job_id(value)
            if job_id:
                return job_id
    elif isinstance(node, list):
        for value in node:
            job_id = find_job_id(value)
            if job_id:
                return job_id
    return None


def resolve_source(source):
    if os.path.isabs(source):
        return source
    return os.path.join(SCENE_DIR, source)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--source", default="CampfireParty/campfire_party_cinematic_01.png")
    ap.add_argument("--frames", type=int, default=9)
    ap.add_argument("--prompt", default=ANIM_PROMPT)
    ap.add_argument("--crop-left", type=int, default=120)
    ap.add_argument("--crop-top", type=int, default=0)
    ap.add_argument("--crop-width", type=int, default=256)
    ap.add_argument("--crop-height", type=int, default=224)
    args = ap.parse_args()

    token = load_token()
    source_path = resolve_source(args.source)
    if not os.path.exists(source_path):
        sys.exit(f"Source still not found: {source_path}")

    try:
        from PIL import Image
    except ImportError:
        sys.exit("pip install pillow")

    image = Image.open(source_path).convert("RGBA")
    crop = (
        max(0, args.crop_left),
        max(0, args.crop_top),
        min(image.width, args.crop_left + args.crop_width),
        min(image.height, args.crop_top + args.crop_height),
    )
    if crop[2] <= crop[0] or crop[3] <= crop[1]:
        sys.exit(f"Invalid crop: {crop}")
    if crop[2] - crop[0] > 256 or crop[3] - crop[1] > 256:
        sys.exit(f"Crop must fit PixelLab 256px cap: {crop}")

    import io
    crop_image = image.crop(crop)
    buf = io.BytesIO()
    crop_image.save(buf, "PNG")
    source_b64 = base64.b64encode(buf.getvalue()).decode()

    os.makedirs(FRAMES_DIR, exist_ok=True)
    with open(os.path.join(FRAMES_DIR, "_composite.json"), "w", encoding="utf-8") as f:
        json.dump({"source": args.source, "crop": crop}, f, indent=2)

    print("Balance:", json.dumps(call(token, "GET", "/balance")))
    print(f"Animating {args.source} crop {crop} -> {args.frames} frames ...")

    payload = {
        "action": args.prompt,
        "first_frame": {"type": "base64", "base64": source_b64},
        "frame_count": args.frames,
    }
    response = call(token, "POST", "/animate-with-text-v3", payload)

    images = []
    find_base64_images(response, images)

    if not images:
        job = find_job_id(response)
        if not job:
            print("Unexpected response; full body follows:")
            print(json.dumps(response, indent=2)[:4000])
            sys.exit(1)
        print("Queued as background job:", job)
        while True:
            time.sleep(10)
            status = call(token, "GET", f"/background-jobs/{job}")
            state = str(status.get("status", status))[:160]
            print("  status:", state)
            find_base64_images(status, images)
            if images:
                break
            if str(status.get("status", "")).lower() in ("failed", "error", "cancelled"):
                print(json.dumps(status, indent=2)[:4000])
                sys.exit(1)

    for index, image_b64 in enumerate(images):
        path = os.path.join(FRAMES_DIR, f"frame_{index:02d}.png")
        with open(path, "wb") as f:
            f.write(base64.b64decode(image_b64))
        print("saved:", path)

    print(f"\nDone - {len(images)} frames in {FRAMES_DIR}.")
    print("Next: run Tools/PixelLab/import_menu_frames.py to promote frames into Unity.")


if __name__ == "__main__":
    main()
