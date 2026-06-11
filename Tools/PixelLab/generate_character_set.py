"""
P1 — Full playable character via PixelLab: 8 directions + complete animation set.
Two-phase with an approval checkpoint so budget can't run away:

  py -3 generate_character_set.py --phase identity
      -> creates the character (reference: BlackMage1.png) + idle + walk.
         STOP. Review PixelArt/Characters/<name>/. Approve before phase 2.

  py -3 generate_character_set.py --phase rest
      -> run, sprint, jump, staff attack, magic cast; then downloads the
         full character ZIP (all rotations + animations).

New characters later (rogue, healer): --name rogue --ref path\to\ref.png
Character id is cached in the output folder so phases resume safely.

Animation strategy (owner-approved): TEMPLATE animations wherever they exist
(consistent skeleton/timing across all future characters = shared Unity animator
+ trainable dataset); action_description only flavors them; pure text only where
no template fits.
"""

import argparse, json, os, sys
from pixellab_common import token, call, wait_job, b64_file, save_b64, find_images, contact_sheet

OUT_ROOT = r"C:\Users\garyc\OneDrive\Desktop\PixelArt\Characters"
# Clean (watermark-free) reference extracted from the owner's walk-loop art.
# The original BlackMage1.png carries a "preview" watermark that the first
# generated character visibly learned — never use watermarked references.
DEFAULT_REF = r"C:\Users\garyc\OneDrive\Desktop\PixelArt\BlackMage_ref_clean.png"

# (key, template_animation_id, action_description, pure_text)
# Template ids from the API's own list (422 response 2026-06-10):
# breathing-idle, fight-stance-idle-8-frames, walking-8-frames, running-4/6/8-frames,
# jumping-1/2, two-footed-jump, running-jump, fireball, cross-punch, throw-object...
PHASE_IDENTITY_ANIMS = [
    ("idle", "breathing-idle", "calm idle, robe and hat sway gently", False),
    ("walk", "walking-8-frames", "steady walk, staff held at side", False),
]
PHASE_REST_ANIMS = [
    ("run",    "running-8-frames", "quick run, robe trailing", False),
    ("sprint", "running-4-frames", "full sprint, leaning forward, robe streaming behind", False),
    ("jump",   "jumping-1", "small hop with anticipation and landing", False),
    ("attack_staff", None, "two-handed downward staff smack, weighty swing with follow-through", True),
    ("cast_magic",   "fireball", "raises staff, gathers glowing energy, releases a spell forward", False),
]


def ensure_character(tok, name, ref_path, out_dir):
    cache = os.path.join(out_dir, "_character_id.txt")
    if os.path.exists(cache):
        cid = open(cache).read().strip()
        print(f"[{name}] using cached character id {cid}")
        return cid
    print(f"[{name}] creating character from {os.path.basename(ref_path)} ...")
    # v3 takes ONLY description + reference image — it handles directions,
    # sizing and proportions automatically (422 confirmed extras are forbidden).
    # Reference must be <=256x256: trim transparent padding, then fit if needed.
    import base64, io
    from PIL import Image
    im = Image.open(ref_path).convert("RGBA")
    bbox = im.getbbox()
    if bbox: im = im.crop(bbox)
    if im.width > 256 or im.height > 256:
        s = min(256 / im.width, 256 / im.height)
        im = im.resize((max(1, int(im.width * s)), max(1, int(im.height * s))),
                       Image.NEAREST)
        print(f"  (reference resized to {im.width}x{im.height})")
    buf = io.BytesIO(); im.save(buf, "PNG")
    ref_b64 = base64.b64encode(buf.getvalue()).decode()
    payload = {
        "description": ("young mage with dark pointed witch hat, dark robe with "
                        "gold trim, wooden staff with glowing orange orb. Distinct "
                        "flat palette regions for skin, hair, robe, and trim."),
        "reference_image": {"type": "base64", "base64": ref_b64},
    }
    resp = call(tok, "POST", "/create-character-v3", payload, fatal=False)
    if "_error" in resp:
        print("create-character-v3 rejected the payload — see the 422 detail above; "
              "send it to Claude for a one-pass fix.")
        sys.exit(1)
    cid = resp.get("character_id") or resp.get("id")
    if not cid:
        jid = resp.get("background_job_id")
        if jid:
            st = wait_job(tok, jid, name) or {}
            cid = st.get("character_id") or st.get("id")
    if not cid:
        print("No character id in response:", json.dumps(resp)[:1200]); sys.exit(1)
    os.makedirs(out_dir, exist_ok=True)
    open(cache, "w").write(cid)
    print(f"[{name}] character id: {cid}")
    return cid


def wait_character_ready(tok, cid, max_minutes=3):
    """Best-effort readiness check. We don't fully know the response shape, so:
    log it once for diagnosis, wait only while it clearly says 'processing',
    and otherwise PROCEED — animate() already retries on rotation-404s."""
    import time
    first = True
    for _ in range(int(max_minutes * 6)):
        st = call(tok, "GET", f"/characters/{cid}", fatal=False)
        if not isinstance(st, dict) or "_error" in st:
            time.sleep(10); continue
        status = str(st.get("status", "")).lower()
        if first:
            slim = {k: (v if not isinstance(v, (dict, list)) else f"<{type(v).__name__} len {len(v)}>")
                    for k, v in st.items()}
            print("  character record:", json.dumps(slim)[:500])
            first = False
        if status in ("processing", "queued", "pending", "in_progress"):
            print(f"  still {status} — waiting 10s ..."); time.sleep(10); continue
        print(f"  proceeding (status: {status or 'unknown'}).")
        return True
    print("  proceeding anyway — animations retry if rotations lag.")
    return True


def animate(tok, cid, key, template, desc, pure_text, out_dir):
    """POST /characters/animations. Template mode = 1 gen/direction.
    Queues 8 direction jobs at once, so animations run ONE AT A TIME and
    429s are retried after a wait (never converted into a mode fallback)."""
    import time
    print(f"[anim {key}] template={template or '(text/v3)'} ...")
    payload = {"character_id": cid, "animation_name": key,
               "action_description": desc, "frame_count": 8}
    if template and not pure_text:
        payload["template_animation_id"] = template
        payload["mode"] = "template"
        payload.pop("frame_count", None)   # template defines its own frames

    resp = None
    for attempt in range(20):
        resp = call(tok, "POST", "/characters/animations", payload, fatal=False)
        if "_error" not in resp:
            break
        body = resp.get("_body", "")
        if resp["_error"] == 429 or "concurrent" in body.lower():
            print("  job slots busy — waiting 30s ..."); time.sleep(30); continue
        if resp["_error"] == 422 and template and "template_animation_id" in body:
            print(f"  template '{template}' invalid; switching to v3 text mode ...")
            payload.pop("template_animation_id", None)
            payload.pop("mode", None)
            payload["frame_count"] = 8
            template = None
            continue
        if resp["_error"] == 404 and "rotation" in body.lower():
            print("  rotations not ready — waiting 60s ..."); time.sleep(60); continue
        print(f"  [anim {key}] FAILED — paste the error above to Claude.")
        return False
    if resp is None or "_error" in resp:
        print(f"  [anim {key}] gave up after retries."); return False

    jid = resp.get("background_job_id")
    jobs = resp.get("jobs") or resp.get("job_ids") or resp.get("background_job_ids")
    waited = False
    if isinstance(jobs, list):
        for j in jobs:
            wait_job(tok, j if isinstance(j, str) else (j.get("job_id") or j.get("id") or ""), key)
            waited = True
    elif jid:
        wait_job(tok, jid, key); waited = True
    if not waited:
        # response shape unknown: give the queue time to drain before next anim
        print("  (no job ids in response — pausing 90s before next animation)")
        time.sleep(90)
    print(f"  [anim {key}] complete.")
    return True


def snapshot(tok, cid, out_dir, label):
    import re, urllib.request
    st = call(tok, "GET", f"/characters/{cid}", fatal=False)
    imgs = []
    find_images(st, imgs)
    for i, b in enumerate(imgs[:16]):
        save_b64(b, os.path.join(out_dir, f"{label}_{i:02d}.png"))
    # their record uses image URLs, not inline base64 — download those too
    urls = re.findall(r'https?://[^"\\\s]+?\.(?:png|webp|gif)[^"\\\s]*',
                      json.dumps(st))
    seen = set()
    n = 0
    for u in urls:
        if u in seen: continue
        seen.add(u)
        try:
            req = urllib.request.Request(u, headers={"Authorization": f"Bearer {tok}"})
            data = urllib.request.urlopen(req, timeout=60).read()
            # keep a meaningful name: .../rotations/south.png -> rotations_south.png
            tail = "_".join(u.split("?")[0].split("/")[-2:])
            open(os.path.join(out_dir, f"{label}_{tail}"), "wb").write(data)
            n += 1
        except Exception as e:
            print("  url fetch failed:", str(e)[:80])
        if n >= 24: break
    print(f"  downloaded {n} preview images from URLs")
    if n == 0 and not imgs:
        print("  no previews found — raw record follows:")
        print(json.dumps(st)[:1500])
    cs = contact_sheet(out_dir)
    if cs: print("  contact sheet:", cs)


def download_zip(tok, cid, out_dir, name):
    data = call(tok, "GET", f"/characters/{cid}/zip", fatal=False)
    if isinstance(data, (bytes, bytearray)):
        p = os.path.join(out_dir, f"{name}_full.zip")
        open(p, "wb").write(data)
        print("FULL CHARACTER ZIP:", p)
    else:
        print("zip download returned:", str(data)[:400])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--phase", choices=["identity", "rest", "snapshot"], default="identity",
                    help="identity = create + idle/walk (checkpoint); rest = all other animations + ZIP; "
                         "snapshot = download previews/ZIP only, spends nothing")
    ap.add_argument("--name", default="black_mage")
    ap.add_argument("--ref", default=DEFAULT_REF)
    args = ap.parse_args()

    tok = token()
    out_dir = os.path.join(OUT_ROOT, args.name)
    os.makedirs(out_dir, exist_ok=True)
    print("Balance:", json.dumps(call(tok, "GET", "/balance")))

    cid = ensure_character(tok, args.name, args.ref, out_dir)
    wait_character_ready(tok, cid)

    if args.phase == "snapshot":
        snapshot(tok, cid, out_dir, "snapshot")
        download_zip(tok, cid, out_dir, args.name)
        return

    anims = PHASE_IDENTITY_ANIMS if args.phase == "identity" else PHASE_REST_ANIMS
    for key, template, desc, pure in anims:
        animate(tok, cid, key, template, desc, pure, out_dir)

    snapshot(tok, cid, out_dir, args.phase)
    if args.phase == "rest":
        download_zip(tok, cid, out_dir, args.name)
    else:
        print("\nCHECKPOINT: review the contact sheet / PixelLab web Character "
              "Creator, then run  --phase rest  to finish the set.")
    print("Balance after:", json.dumps(call(tok, "GET", "/balance")))


if __name__ == "__main__":
    main()
