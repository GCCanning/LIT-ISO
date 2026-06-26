"""
Props (trees/bushes/rocks) via PixelLab /create-1-direction-object.
These generate MANY candidates and enter a REVIEW state â€” workflow:

  py -3 generate_props.py --family plains          (queue the family)
  py -3 generate_props.py --status                 (download candidates + sheets)
  py -3 generate_props.py --pick <object_id> 2,5   (promote chosen frames)

Output: PixelArt/Props/<family>/<prop>/candidate_NN.png + _contact_sheet.png
State:  PixelArt/Props/_objects.json maps object ids -> prop names.
"""

import argparse, json, os, sys, time
from pixellab_common import token, call, find_images, save_b64, contact_sheet, b64_file, download_urls

OUT = r"C:\Users\garyc\OneDrive\Desktop\PixelArt\Props"
REPO_PROPS = r"C:\Projects\Unity-Projects\LIT-ISO\Assets\Generated\Props"
STATE = os.path.join(OUT, "_objects.json")

STYLE = ("low top-down isometric pixel art game prop, crisp 1px pixel edges, transparent "
         "background, no ground shadow baked in, cohesive with reference style")

FAMILIES = {
    "plains": [
        ("plains_tree_v2",  "leafy round-canopy tree, warm green, slim trunk"),
        ("plains_bush_v2",  "low rounded bush with tiny flowers"),
        ("plains_rock_v2",  "weathered grey boulder, mossy base"),
    ],
    "forest": [
        ("forest_pine",           "tall dark green pine tree, layered triangular boughs, slim trunk"),
        ("forest_pine_young",     "smaller young pine sapling"),
        ("forest_dead_tree",      "bare dead tree, twisted gray branches, no leaves"),
        ("forest_mushrooms",      "cluster of small red-cap mushrooms"),
        ("forest_mushrooms_glow", "cluster of pale blue faintly glowing mushrooms"),
        ("forest_log",            "fallen mossy log, horizontal"),
        ("forest_stump",          "old tree stump with growth rings"),
        ("forest_fern",           "low wide forest fern"),
    ],
}


def load_state():
    return json.load(open(STATE)) if os.path.exists(STATE) else {}


def save_state(s):
    os.makedirs(OUT, exist_ok=True)
    json.dump(s, open(STATE, "w"), indent=1)


def style_refs():
    refs = []
    # Preferred: approved v2 style candidates from the plains run.
    for p in (os.path.join(OUT, "plains", "plains_tree_v2", "frame_0.png"),
              os.path.join(OUT, "plains", "plains_rock_v2", "frame_0.png")):
        if os.path.exists(p):
            # PixelLab /create-1-direction-object currently rejects
            # style_images width/height metadata; send only the image payload.
            refs.append({"type": "base64", "base64": b64_file(p)})
    if refs:
        return refs[:2]
    # Fallback: old repo props (outdated style; used only if v2 files are missing).
    for n in ("forest_oak_tree", "plains_rock"):
        for root, _, files in os.walk(REPO_PROPS):
            if n + ".png" in files:
                p = os.path.join(root, n + ".png")
                refs.append({"type": "base64", "base64": b64_file(p)})
                break
    return refs[:2]


def payload_preview(payload):
    preview = dict(payload)
    preview["style_images"] = [
        {"type": img.get("type"), "base64_bytes": len(img.get("base64", ""))}
        for img in payload.get("style_images", [])
    ]
    return preview


def submit(tok, family, dry_run=False):
    fam = FAMILIES.get(family) or sys.exit(f"unknown family {family}; options: {list(FAMILIES)}")
    state = load_state()
    refs = style_refs()
    queued = 0
    rejected = 0
    for name, desc in fam:
        if not dry_run and any(v.get("name") == name for v in state.values()):
            print(f"[{name}] already queued/known â€” skipping"); continue
        print(f"[{name}] submitting ...")
        payload = {"description": f"{STYLE}. {desc}",
                   "view": "top-down",
                   "style_images": refs}
        if not refs:
            payload["size"] = 64
        if dry_run:
            print(json.dumps(payload_preview(payload), indent=2))
            continue
        resp = None
        for attempt in range(20):
            resp = call(tok, "POST", "/create-1-direction-object", payload, fatal=False)
            if "_error" not in resp:
                break
            if resp["_error"] == 402:
                sys.exit("Out of generations.")
            if resp["_error"] == 429:
                print("  all 8 job slots busy - waiting 30s ..."); time.sleep(30); continue
            rejected += 1
            print("  rejected â€” paste the error above to Claude."); resp = None; break
        if resp is None:
            continue
        oid = resp.get("object_id") or resp.get("id")
        if not oid:
            print("  no object id:", str(resp)[:400]); continue
        state[oid] = {"name": name, "family": family}
        save_state(state)
        queued += 1
        print("  queued:", oid)
        time.sleep(2)
    if queued:
        print(f"\nQueued {queued}. Run --status in a few minutes to fetch review candidates.")
    eli