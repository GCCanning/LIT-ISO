"""
Generate the COMPLETE LIT-ISO UI skin set via PixelLab REST v2 in one run,
one consistent style — review a single contact sheet, not pieces.

Output: PixelArt/UISet/<piece>.png + _contact_sheet.png
Then Claude imports the approved set onto the existing skin names.

Usage:  py -3 generate_ui_set.py            (full set)
        py -3 generate_ui_set.py --only panel,button
"""

import argparse, base64, json, os, sys, urllib.request, urllib.error

API = "https://api.pixellab.ai/v2"
OUT = r"C:\Users\garyc\OneDrive\Desktop\PixelArt\UISet"
TOKEN_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                          "pixellab_token.local.txt")

STYLE = ("dark fantasy LitRPG game UI, pixel art, deep navy-charcoal panel "
         "(#10141f) with thin antique brass-gold border and subtle corner "
         "notches, clean flat surfaces, crisp 1px pixel edges, no text, "
         "no icons, no gradients banding, transparent background outside the shape")

PIECES = [
    ("panel",          256, 192, "large rectangular window panel, 9-slice friendly: uniform border, plain center"),
    ("system_row",     128, 48,  "small rectangular list-row plate, slightly lighter center, 9-slice friendly"),
    ("craft_row",      128, 48,  "small rectangular list-row plate, subtle inner bevel, 9-slice friendly"),
    ("inv_slot",       64,  64,  "square inventory slot, recessed center, brass corner accents"),
    ("slot",           64,  64,  "square hotbar slot, recessed dark center"),
    ("slot_selected",  64,  64,  "square hotbar slot, glowing brass-gold border, selected state"),
    ("button",         96,  40,  "rectangular button, raised, brass border, idle state"),
    ("button_hover",   96,  40,  "rectangular button, raised, brighter brass border, hover state"),
    ("button_pressed", 96,  40,  "rectangular button, pressed inward, darker, pressed state"),
    ("btn_close",      40,  40,  "small square close button, brass border"),
    ("bar_track",      128, 24,  "horizontal status bar track, empty, recessed dark channel"),
    ("bar_health_fill",128, 16,  "horizontal bar fill, rich crimson red with bright top edge highlight"),
    ("bar_mana_fill",  128, 16,  "horizontal bar fill, deep azure blue with bright top edge highlight"),
    ("bar_xp_fill",    128, 12,  "horizontal bar fill, warm brass gold with bright top edge highlight"),
    ("vitals_bg",      192, 96,  "wide decorative HUD plate, dark with brass trim, 9-slice friendly"),
    ("hotbar_bg",      256, 64,  "wide horizontal hotbar plate, dark with brass trim, 9-slice friendly"),
]


def token():
    t = open(TOKEN_FILE, encoding="utf-8").read().strip()
    return t[7:] if t.lower().startswith("bearer ") else t


def call(tok, method, path, payload=None, timeout=300):
    req = urllib.request.Request(API + path,
        data=json.dumps(payload).encode() if payload is not None else None,
        method=method,
        headers={"Authorization": f"Bearer {tok}",
                 "Content-Type": "application/json", "Accept": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        print(f"\nHTTP {e.code} on {method} {path}:\n{e.read().decode(errors='replace')}\n")
        raise SystemExit(1)


def find_images(node, out):
    if isinstance(node, dict):
        if node.get("type") == "base64" and isinstance(node.get("base64"), str):
            out.append(node["base64"])
        else:
            for v in node.values(): find_images(v, out)
    elif isinstance(node, list):
        for v in node: find_images(v, out)


# Jobs queued by the first (pre-polling) run on 2026-06-10 — recover, don't re-spend.
RECOVER_JOBS = {
    "panel":          "f594ae0e-f15e-42fc-a4d3-babba4f0aa7c",
    "system_row":     "ba8ae86a-3d72-4ca6-af38-652d914573f7",
    "craft_row":      "a9eb34d0-41dc-4f69-ab8e-4f07284b1064",
    "inv_slot":       "9def5bd8-8db3-4e42-b7f6-0f5283fb1846",
    "slot":           "4a6f336a-394b-476f-91d9-d0427cee8944",
    "slot_selected":  "bfdbe52c-d214-47e8-ab2a-873862a62beb",
    "button":         "7929519d-e3d7-45bc-b687-f8fe5db5f638",
    "button_hover":   "65425436-8d59-4491-b469-533e28ee6436",
}


def wait_for_job(tok, job_id, label):
    import time
    for _ in range(120):  # up to ~10 min
        st = call(tok, "GET", f"/background-jobs/{job_id}")
        status = str(st.get("status", "")).lower()
        imgs = []
        find_images(st, imgs)
        if imgs:
            return imgs[0]
        if status in ("failed", "error", "cancelled"):
            print(f"  [{label}] job {status}: {json.dumps(st)[:600]}")
            return None
        time.sleep(5)
    print(f"  [{label}] timed out waiting on job {job_id}")
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default="")
    ap.add_argument("--skip-recover", action="store_true")
    args = ap.parse_args()
    only = {s.strip() for s in args.only.split(",") if s.strip()}

    tok = token()
    os.makedirs(OUT, exist_ok=True)
    print("Balance:", json.dumps(call(tok, "GET", "/balance")))

    done = []

    # Phase 1: recover already-queued jobs (no new generations spent)
    if not args.skip_recover:
        for name, jid in RECOVER_JOBS.items():
            p = os.path.join(OUT, name + ".png")
            if os.path.exists(p):
                done.append(name); continue
            print(f"[recover {name}] job {jid} ...")
            b64 = wait_for_job(tok, jid, name)
            if b64:
                open(p, "wb").write(base64.b64decode(b64))
                print("  saved", p)
                done.append(name)

    # Phase 2: generate the rest, SEQUENTIALLY (submit -> poll -> next)
    # to respect the 8-concurrent-job trial cap.
    import time
    for name, w, h, desc in PIECES:
        if only and name not in only: continue
        p = os.path.join(OUT, name + ".png")
        if os.path.exists(p):
            if name not in done: done.append(name)
            continue
        print(f"\n[{name}] {w}x{h} ...")
        payload = {
            "description": f"{STYLE}. This piece: {desc}",
            "image_size": {"width": w, "height": h},
        }
        for attempt in range(6):
            try:
                resp = call(tok, "POST", "/generate-ui-v2", payload)
                break
            except SystemExit:
                print("  (rate/concurrency limited — waiting 30s)")
                time.sleep(30)
        else:
            continue
        imgs = []
        find_images(resp, imgs)
        if not imgs:
            jid = resp.get("background_job_id")
            if jid:
                b64 = wait_for_job(tok, jid, name)
                if b64: imgs = [b64]
        if not imgs:
            print("  no image; response:", json.dumps(resp)[:800]); continue
        open(p, "wb").write(base64.b64decode(imgs[0]))
        print("  saved", p)
        done.append(name)

    # contact sheet
    try:
        from PIL import Image, ImageDraw
        cells = [(n, Image.open(os.path.join(OUT, n + ".png"))) for n in done]
        if cells:
            cw = max(i.width for _, i in cells) + 16
            ch = max(i.height for _, i in cells) + 28
            cols = 4
            rows = (len(cells) + cols - 1) // cols
            sheet = Image.new("RGBA", (cols * cw, rows * ch), (60, 62, 70, 255))
            d = ImageDraw.Draw(sheet)
            for i, (n, im) in enumerate(cells):
                x, y = (i % cols) * cw, (i // cols) * ch
                sheet.paste(im, (x + 8, y + 22), im if im.mode == "RGBA" else None)
                d.text((x + 6, y + 4), n, fill=(255, 240, 200, 255))
            sp = os.path.join(OUT, "_contact_sheet.png")
            sheet.save(sp)
            print("\nContact sheet:", sp)
    except ImportError:
        pass
    print(f"\nDone: {len(done)} pieces. Review ONLY the contact sheet; "
          "then tell Claude approve/reject (or reject named pieces).")


if __name__ == "__main__":
    main()
