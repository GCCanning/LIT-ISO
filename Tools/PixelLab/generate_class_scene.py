"""
Generate LIT-ISO class-selection cinematic backdrop candidates via the
PixelLab REST API v2.

Runs entirely on this PC (no MCP/connector needed). Token is read from
pixellab_token.local.txt next to this script (gitignored).

Usage:
  py -3 generate_class_scene.py             -> 3 candidates, default prompt
  py -3 generate_class_scene.py --count 5
  py -3 generate_class_scene.py --prompt "your own scene description"

Outputs land in:  C:/Users/garyc/OneDrive/Desktop/PixelArt/Scenes/
(raw 400x224 pixel-art + a 4x nearest-neighbor preview of each)

After picking a winner, upscale ~5x nearest-neighbor to 1920x1080 + crop
(same finishing step the menu background got) and drop it in as
Assets/Resources/UI/ClassSelection/background.png — ClassAssignmentView
picks it up automatically.
"""

import argparse
import base64
import json
import os
import sys
import urllib.request
import urllib.error

API = "https://api.pixellab.ai/v2"
OUT_DIR = r"C:\Users\garyc\OneDrive\Desktop\PixelArt\Scenes"
TOKEN_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                          "pixellab_token.local.txt")

DEFAULT_PROMPT = (
    "pixel art cinematic scene in a dark cosmic void, a tiny lone robed "
    "figure seen from behind standing on a small cluster of floating stone "
    "tiles, looking up in awe at a vast incomprehensible luminous being of "
    "pure light that looms far overhead, the colossal being hovering above "
    "a floating isometric island world with grass tiles and tiny buildings "
    "high in the sky, immense scale contrast between the tiny figure and "
    "the godlike entity, deep purples and dark blues, drifting glowing "
    "motes of light, faint distant stars, dark empty space at the bottom, "
    "no text, no UI"
)

NEGATIVE = "text, watermark, ui, frame, border, blurry"


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

        path = os.path.join(OUT_DIR, f"class_scene_{i:02d}.png")
        with open(path, "wb") as f:
            f.write(base64.b64decode(images[0]))
        print("saved:", path)
        preview = upscale_nearest(path)
        if preview:
            print("preview:", preview)

    print("\nDone. Review the *_x4 previews, tell Claude which number wins.")
    print("Finishing step for the winner: ~5x nearest-neighbor to 1920x1080")
    print("+ crop, then save as Assets/Resources/UI/ClassSelection/background.png")


if __name__ == "__main__":
    main()
