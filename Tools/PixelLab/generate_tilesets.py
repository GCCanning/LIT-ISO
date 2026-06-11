"""
P2 — Biome tile families + iso TRANSITION mini-sets via PixelLab create-tiles-pro.

Concurrency-safe: submits at most MAX_IN_FLIGHT jobs, then waits — the trial/low
tiers cap concurrent background jobs at 8, so we never approach it.

Families run one at a time with a contact sheet each:
  py -3 generate_tilesets.py --family snow
  py -3 generate_tilesets.py --family dungeon_stone
  py -3 generate_tilesets.py --family mountain_stone
  py -3 generate_tilesets.py --family transitions     (the Wang-idea edge tiles)
  py -3 generate_tilesets.py --list
"""

import argparse, json, os, sys, time
from pixellab_common import token, call, wait_job, b64_file, save_b64, find_images, contact_sheet, download_urls

OUT_ROOT = r"C:\Users\garyc\OneDrive\Desktop\PixelArt\Tilesets"
REPO_TILES = r"C:\Projects\Unity-Projects\LIT-ISO\Assets\Generated\Tiles"
RES_TILES = r"C:\Projects\Unity-Projects\LIT-ISO\Assets\Resources\Tiles"
MAX_IN_FLIGHT = 2   # stay far below the 8-job account cap

STYLE = ("crisp isometric pixel art game tile, 2:1 diamond top surface, "
         "clean 1px edges, cohesive with the reference style tiles, "
         "no text, no border, transparent background outside the tile")


def _ref_entry(path):
    from PIL import Image
    w, h = Image.open(path).size
    return {"type": "base64", "base64": b64_file(path), "width": w, "height": h}


BIOMESKETCH_TILES = r"C:\Projects\Unity-Projects\LIT-ISO\Tools\BiomeSketch\assets\tile"

def style_refs(*names):
    refs = []
    for n in names:
        found = None
        for root, _, files in os.walk(REPO_TILES):
            if n + ".png" in files:
                found = os.path.join(root, n + ".png"); break
        if not found:
            for base in (RES_TILES, BIOMESKETCH_TILES):
                p = os.path.join(base, n + ".png")
                if os.path.exists(p):
                    found = p; break
        if found:
            refs.append(_ref_entry(found))
    return refs[:3]


FAMILIES = {
    # small review batch first (owner QC in BiomeSketch before bigger spends)
    "plains": {
        "refs": ["plains_grass_base", "plains_flower_grass", "grass_1"],
        "tiles": [
            ("plains_lush_v2",   "lush green meadow grass, soft blade texture"),
            ("plains_clover",    "green grass with small clover patches"),
            ("plains_wildflower","green grass with tiny scattered white and yellow wildflowers"),
        ],
    },
    "snow": {
        "refs": ["plains_grass_base", "forest_grass_base"],
        "tiles": [
            ("snow_base_1", "pristine snow surface, soft drifts"),
            ("snow_base_2", "snow surface with subtle wind ripples"),
            ("snow_sparse", "thin snow with grass blades poking through"),
            ("snow_ice",    "patch of pale blue ice in snow"),
            ("snow_rocky",  "snow with small embedded stones"),
            ("snow_drift",  "deeper snow drift, soft shadowed edge"),
        ],
    },
    # NOTE: refs must be 32px FLAT tiles — the old dungeon_floor_1/2 are 256x512
    # tall blocks and steered the model into generating wall geometry (bad batch
    # 2026-06-11). Style now anchored by the approved plains2/snow2 look instead.
    "dungeon_stone": {
        "refs": ["plains2_02", "stone_block", "grass_3"],
        "tiles": [
            ("dungeon_moss_1", "flat ancient stone dungeon floor, moss in the cracks, full diamond coverage"),
            ("dungeon_moss_2", "flat cracked mossy stone floor, worn smooth, full diamond coverage"),
            ("dungeon_rune",   "flat stone floor with a faint glowing blue rune, full diamond coverage"),
            ("dungeon_broken", "flat broken stone floor, chipped edges, full diamond coverage"),
            ("dungeon_dark_1", "flat dark obsidian dungeon floor, full diamond coverage"),
            ("dungeon_dark_2", "flat dark stone floor with ember-orange cracks, full diamond coverage"),
        ],
    },
    "mountain_stone": {
        "refs": ["plains_grass_base"],
        "tiles": [
            ("stone_scree",   "gray scree and gravel mountain surface"),
            ("stone_mossy",   "weathered gray stone with moss patches"),
            ("stone_cracked", "cracked granite surface"),
            ("stone_snowcap", "gray stone dusted with snow at the edges"),
        ],
    },
    # The Wang-tileset IDEA in iso form: directional edge tiles per boundary pair.
    # N/E/S/W edges + 2 diagonals each for the boundaries the generator hits most.
    "transitions": {
        "refs": ["plains_grass_base", "forest_grass_base"],
        "tiles": [(f"{a}_to_{b}_{d}",
                   f"transition tile: {a.replace('_',' ')} blending into {b.replace('_',' ')} "
                   f"along the {dn} edge of the diamond, drawn edge (not dithered)")
                  for a, b in (("grass", "sand"), ("sand", "water"), ("grass", "dirt"),
                               ("grass", "forest_floor"), ("grass", "stone"))
                  for d, dn in (("n", "north"), ("e", "east"), ("s", "south"), ("w", "west"),
                                ("ne", "north-east corner"), ("sw", "south-west corner"))],
    },
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--family", default=None)
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--fetch", default=None,
                    help="tile_id of an already-generated batch to download")
    ap.add_argument("--list-remote", action="store_true",
                    help="list recent tiles-pro batches on the account")
    args = ap.parse_args()

    if args.list_remote:
        tok3 = token()
        got = call(tok3, "GET", "/tiles-pro?limit=10", fatal=False)
        blob = json.dumps(got) if isinstance(got, (dict, list)) else str(got)
        import re as _re
        ids = _re.findall(r'"(?:tile_)?id"\s*:\s*"([0-9a-f-]{36})"', blob)
        stats = _re.findall(r'"status"\s*:\s*"(\w+)"', blob)
        for i, tid in enumerate(ids):
            print(tid, stats[i] if i < len(stats) else "?")
        if not ids:
            print(blob[:800])
        return

    if args.fetch:
        tok2 = token()
        out_dir2 = os.path.join(OUT_ROOT, args.family or "plains")
        got = call(tok2, "GET", f"/tiles-pro/{args.fetch}", fatal=False)
        if isinstance(got, dict) and "_error" not in got:
            print("status:", got.get("status"))
            saved = download_urls(tok2, got, out_dir2, prefix="tile")
            if saved:
                cs = contact_sheet(out_dir2)
                print("Contact sheet:", cs)
            else:
                slim = {k: (v if not isinstance(v, (dict, list)) else f"<{type(v).__name__}>")
                        for k, v in got.items()}
                print("record:", json.dumps(slim)[:600])
        return

    if args.list or not args.family:
        for k, v in FAMILIES.items():
            print(f"{k}: {len(v['tiles'])} tiles")
        return
    fam = FAMILIES.get(args.family)
    if not fam:
        sys.exit(f"unknown family {args.family}")

    tok = token()
    out_dir = os.path.join(OUT_ROOT, args.family)
    os.makedirs(out_dir, exist_ok=True)
    print("Balance:", json.dumps(call(tok, "GET", "/balance")))
    refs = style_refs(*fam["refs"])
    print(f"{len(refs)} style refs loaded")

    # PixelLab's own recommended pattern: number multiple tiles in ONE request
    # ("1). grass tile 2). dirt tile ...") — cohesive style, fewer jobs.
    pending = [(n, d) for n, d in fam["tiles"]
               if not os.path.exists(os.path.join(out_dir, n + ".png"))]
    CHUNK = 6
    for c0 in range(0, len(pending), CHUNK):
        chunk = pending[c0:c0 + CHUNK]
        names = [n for n, _ in chunk]
        numbered = " ".join(f"{k+1}). {d}" for k, (_, d) in enumerate(chunk))
        print(f"[batch {c0//CHUNK + 1}] {', '.join(names)}")
        payload = {
            "description": f"{STYLE}. {numbered}",
            "tile_type": "isometric",
            "tile_size": 32,
            "tile_view": "low top-down",
            "style_images": refs,
        }
        resp = None
        for attempt in range(6):
            resp = call(tok, "POST", "/create-tiles-pro", payload, fatal=False)
            if "_error" not in resp:
                break
            if resp["_error"] == 429:
                print("  job slots busy — waiting 30s ..."); time.sleep(30)
            elif resp["_error"] == 402:
                sys.exit("Out of generations.")
            else:
                print("  rejected — paste the error above to Claude."); resp = None; break
        if resp is None:
            continue

        imgs = []
        find_images(resp, imgs)
        got = None
        if not imgs:
            jid = resp.get("background_job_id")
            tid = resp.get("tile_id") or jid
            if jid:
                st = wait_job(tok, jid, names[0])
                if st: find_images(st, imgs)
            if not imgs and tid:
                got = call(tok, "GET", f"/tiles-pro/{tid}", fatal=False)
                find_images(got, imgs)
        if imgs:
            for k, n in enumerate(names):
                if k < len(imgs):
                    save_b64(imgs[k], os.path.join(out_dir, n + ".png"))
                    print("  saved", n)
            for k in range(len(names), len(imgs)):
                save_b64(imgs[k], os.path.join(out_dir, f"_extra_{c0+k:02d}.png"))
        elif got is not None:
            # completed records carry storage URLs, not inline base64
            saved = download_urls(tok, got, out_dir, prefix=names[0])
            if not saved:
                print("  record had no images/urls:", str(got)[:500])
        else:
            print("  no images; response:", str(resp)[:500])
    cs = contact_sheet(out_dir)
    print("\nContact sheet:", cs)
    print("Balance after:", json.dumps(call(tok, "GET", "/balance")))


if __name__ == "__main__":
    main()
