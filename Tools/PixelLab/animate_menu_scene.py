"""
Animate the approved menu scene via PixelLab REST v2 (animate-with-text-v3).

Takes the approved still (menu_scene_01.png by default), asks for a subtle
ambient loop (fire flicker, fireflies, twinkling stars), and saves the
resulting frames to PixelArt/MenuScene/frames/.

Usage:
  py -3 animate_menu_scene.py
  py -3 animate_menu_scene.py --source menu_scene_02.png --frames 8
"""

import argparse
import base64
import json
import os
import sys
import time
import urllib.request
import urllib.error

API = "https://api.pixellab.ai/v2"
SCENE_DIR = r"C:\Users\garyc\OneDrive\Desktop\PixelArt\MenuScene"
TOKEN_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                          "pixellab_token.local.txt")

ANIM_PROMPT = (
    "subtle ambient loop: campfire flames flicker and dance, small sparks "
    "rise, warm light pulses gently on the ground, fireflies drift slowly, "
    "stars twinkle, cabin window light glows steadily, portal glow pulses "
    "softly; camera static, scenery static, only light and particles move"
)


def load_token():
    try:
        tok = open(TOKEN_FILE, encoding="utf-8").read().strip()
    except OSError:
        sys.exit(f"Token file missing: {TOKEN_FILE}")
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
            for v in node.values():
                find_base64_images(v, found)
    elif isinstance(node, list):
        for v in node:
            find_base64_images(v, found)


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
    ap.add_argument("--source", default="menu_scene_01.png")
    ap.add_argument("--frames", type=int, default=8)
    ap.add_argument("--prompt", default=ANIM_PROMPT)
    args = ap.parse_args()

    token = load_token()
    src_path = os.path.join(SCENE_DIR, args.source)
    if not os.path.exists(src_path):
        sys.exit(f"Source still not found: {src_path}")
    with open(src_path, "rb") as f:
        src_b64 = base64.b64encode(f.read()).decode()

    print("Balance:", json.dumps(call(token, "GET", "/balance")))
    print(f"Animating {args.source} -> {args.frames} frames ... "
          "(this can take a few minutes)")

    payload = {
        "description": args.prompt,
        "image_size": {"width": 400, "height": 224},
        "frame_count": args.frames,
        "start_image": {"type": "base64", "base64": src_b64},
    }
    resp = call(token, "POST", "/animate-with-text-v3", payload)

    images = []
    find_base64_images(resp, images)

    if not images:
        job = find_job_id(resp)
        if job:
            print("Queued as background job:", job)
            while True:
                time.sleep(10)
                status = call(token, "GET", f"/background-jobs/{job}")
                state = str(status.get("status", status))[:120]
                print("  status:", state)
                find_base64_images(status, images)
                if images or str(status.get("status", "")).lower() in (
                        "completed", "failed", "error"):
                    if not images:
                        find_base64_images(status, images)
                    if str(status.get("status", "")).lower() in ("failed", "error"):
                        print(json.dumps(status, indent=2)[:4000])
                        sys.exit(1)
                    if images:
                        break
        else:
            print("Unexpected response; full body follows:")
            print(json.dumps(resp, indent=2)[:4000])
            sys.exit(1)

    out_dir = os.path.join(SCENE_DIR, "frames")
    os.makedirs(out_dir, exist_ok=True)
    for i, b64 in enumerate(images):
        p = os.path.join(out_dir, f"frame_{i:02d}.png")
        with open(p, "wb") as f:
            f.write(base64.b64decode(b64))
        print("saved:", p)

    print(f"\nDone — {len(images)} frames in {out_dir}. "
          "Tell Claude and the flipbook import happens automatically.")


if __name__ == "__main__":
    main()
