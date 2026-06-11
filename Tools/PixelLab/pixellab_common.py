"""Shared helpers for the LIT-ISO PixelLab REST v2 scripts."""

import base64
import json
import os
import sys
import time
import urllib.error
import urllib.request

API = "https://api.pixellab.ai/v2"
TOKEN_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                          "pixellab_token.local.txt")


def token():
    try:
        t = open(TOKEN_FILE, encoding="utf-8").read().strip()
    except OSError:
        sys.exit(f"Token file missing: {TOKEN_FILE}")
    return t[7:] if t.lower().startswith("bearer ") else t


def call(tok, method, path, payload=None, timeout=300, fatal=True):
    req = urllib.request.Request(
        API + path,
        data=json.dumps(payload).encode() if payload is not None else None,
        method=method,
        headers={"Authorization": f"Bearer {tok}",
                 "Content-Type": "application/json",
                 "Accept": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            ct = r.headers.get("Content-Type", "")
            data = r.read()
            if "json" in ct:
                return json.loads(data.decode())
            return data  # binary (e.g. character zip)
    except urllib.error.HTTPError as e:
        body = e.read().decode(errors="replace")
        print(f"\nHTTP {e.code} on {method} {path}:\n{body[:2500]}\n")
        if fatal:
            raise SystemExit(1)
        return {"_error": e.code, "_body": body}


def find_images(node, out):
    if isinstance(node, dict):
        if node.get("type") == "base64" and isinstance(node.get("base64"), str):
            out.append(node["base64"])
        else:
            for v in node.values():
                find_images(v, out)
    elif isinstance(node, list):
        for v in node:
            find_images(v, out)


def wait_job(tok, job_id, label, max_minutes=12):
    for _ in range(int(max_minutes * 12)):
        st = call(tok, "GET", f"/background-jobs/{job_id}")
        status = str(st.get("status", "")).lower()
        imgs = []
        find_images(st, imgs)
        if imgs or status == "completed":
            return st
        if status in ("failed", "error", "cancelled"):
            print(f"  [{label}] job {status}: {json.dumps(st)[:600]}")
            return None
        time.sleep(5)
    print(f"  [{label}] timed out on job {job_id}")
    return None


def b64_file(path):
    return base64.b64encode(open(path, "rb").read()).decode()


def save_b64(b64, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    open(path, "wb").write(base64.b64decode(b64))
    return path


def download_urls(tok, record, out_dir, prefix="img"):
    """Download every image URL found in an API record. Tries unauthenticated
    first (storage links are often public), then with the bearer token."""
    import re, urllib.request
    blob = json.dumps(record) if not isinstance(record, str) else record
    urls = []
    for u in re.findall(r'https?://[^"\\\s]+?\.(?:png|webp|gif)[^"\\\s]*', blob):
        if u not in urls:
            urls.append(u)
    os.makedirs(out_dir, exist_ok=True)
    saved = []
    first_fail_logged = False
    for i, u in enumerate(urls):
        data = None
        errs = []
        for headers in ({}, {"Authorization": f"Bearer {tok}"},
                        {"User-Agent": "Mozilla/5.0"},
                        {"Authorization": f"Bearer {tok}", "User-Agent": "Mozilla/5.0"}):
            try:
                req = urllib.request.Request(u, headers=headers)
                data = urllib.request.urlopen(req, timeout=60).read()
                break
            except Exception as e:
                errs.append(str(e)[:60])
        if data is None:
            if not first_fail_logged:
                first_fail_logged = True
                print("  FULL URL:", u)
                print("  attempts:", " | ".join(errs))
            else:
                print("  could not fetch:", u[:90])
            continue
        tail = u.split("?")[0].rstrip("/").split("/")[-1]
        name = tail if tail.endswith(".png") else f"{prefix}_{i:02d}.png"
        p = os.path.join(out_dir, name)
        open(p, "wb").write(data)
        saved.append(p)
        print("  downloaded", name)
    return saved


def contact_sheet(folder, out_name="_contact_sheet.png"):
    try:
        from PIL import Image, ImageDraw
    except ImportError:
        return None
    pngs = [f for f in sorted(os.listdir(folder))
            if f.endswith(".png") and not f.startswith("_")]
    if not pngs:
        return None
    cells = [(f, Image.open(os.path.join(folder, f))) for f in pngs]
    cw = max(i.width for _, i in cells) + 14
    ch = max(i.height for _, i in cells) + 26
    cols = min(6, len(cells))
    rows = (len(cells) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cw, rows * ch), (58, 60, 68, 255))
    d = ImageDraw.Draw(sheet)
    for i, (f, im) in enumerate(cells):
        x, y = (i % cols) * cw, (i // cols) * ch
        sheet.paste(im, (x + 7, y + 20), im if im.mode == "RGBA" else None)
        d.text((x + 5, y + 3), f[:30], fill=(255, 238, 196, 255))
    p = os.path.join(folder, out_name)
    sheet.save(p)
    return p
