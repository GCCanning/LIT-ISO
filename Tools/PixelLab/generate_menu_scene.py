"""
Generate LIT-ISO main-menu background candidates via the PixelLab REST API v2.

Runs entirely on this PC (no MCP/connector needed). Token is read from
pixellab_token.local.txt next to this script (gitignored).

Usage:
  py -3 generate_menu_scene.py             -> 3 candidates, default prompt
  py -3 generate_menu_scene.py --count 5
  py -3 generate_menu_scene.py --prompt "your own scene description"

Outputs land in:  C:/Users/garyc/OneDrive/Desktop/PixelArt/MenuScene/
(raw 400x224 pixel-art + a 4x nearest-neighbor preview of each)
"""

import argparse
import base64
import json
import os
import sys
import urllib.request
import urllib.error

API = "https://api.pixellab.ai/v2"
OUT_DIR = r"C:\Users\garyc\OneDrive\Desktop\PixelArt\MenuScene"
TOKEN_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                          "pixellab_token.local.txt")

DEFAULT_PROMPT = (
    "isometric pixel art game scene at golden dusk, a lit campfire with a "
    "cooking pot in the lower right casting warm light on mossy grass tiles, "
    "a cozy tavern with glowing windows in the middle distance, a faint "
    "glowing blue portal at the treeline, rolling plains fading into misty "
    "forest, first stars in a deep blue sky, mossy greens, warm ochre paths, "
    "deep night blues, fireflies, calm dark empty area on the left side, "
    "no characters, no text, no UI"
)

NEGATIVE = "text, watermark, characters, people, ui, frame, border, blurry"


def load_token():
    try:
        tok = open(TOKEN_FILE, encoding="utf-8").read().strip()
    except OSError:
        sys.exit(f"Token file missing: {TOKEN_FILE}")
    if tok.lower().startswith("bearer "):
        tok = tok[7:]
    return tok


def call(token, method, path, payload=None, timeout=300):
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
    """Recursively collect base64 image payloads from any response shape."""
    if isinstance(node, dict):
        if node.get("type") == "base64" and isinstance(node.get("base64"), str):
            found.append(node["base64"])
        else:
            for v in node.values():
                find_base64_images(v, found)
    elif isinstance(node, list):
        for v in node:
            find_base64_images(v, found)


def upscale_nearest(path, factor=4):
    try:
        from PIL import Image
    except ImportError:
        return None
    im = Image.open(path)
    big = im.resize((im.width * factor, im.height * factor), Image.NEAREST)
    out = path.replace(".png", f"_x{factor}.png")
    big.save(out)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--count", type=int, default=3)
    ap.add_argument("--prompt", default=DEFAULT_PROMPT)
    ap.add_argument("--width", type=int, default=400)
    ap.add_argument("--height", type=int, default=224)
    args = ap.parse_args()

    token = load_token()
    os.makedirs(OUT_DIR, exist_ok=True)

    bal = call(token, "GET", "/balance")
    print("Balance:", json.dumps(bal))

    for i in range(1, args.count + 1):
        print(f"\nGenerating candidate {i}/{args.count} "
              f"({args.width}x{args.height}) ... (can take a minute)")
        payload = {
            "description": args.prompt,
            "negative_description": NEGATIVE,
            "image_size": {"width": args.width, "height": args.height},
            "no_background": False,
        }
        resp = call(token, "POST", "/create-image-pixflux", payload)

        images = []
        find_base64_images(resp, images)
        if not images:
            print("No image in response; full response follows:")
            print(json.dumps(resp, indent=2)[:4000])
            continue

        path = os.path.join(OUT_DIR, f"menu_scene_{i:02d}.png")
        with open(path, "wb") as f:
            f.write(base64.b64decode(images[0]))
        print("saved:", path)
        preview = upscale_nearest(path)
        if preview:
            print("preview:", preview)

    print("\nDone. Review the *_x4 previews, tell Claude which number wins.")


if __name__ == "__main__":
    main()
