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
from pixellab_common import token, call, wait_job, b64_file, save_b64, find_images, contact_sheet

OUT_ROOT = r"C:\Users\garyc\OneDrive\Desktop\PixelArt\Tilesets"
REPO_TILES = r"C:\Projects\Unity-Projects\LIT-ISO\Assets\Generated\Tiles"
RES_TILES = r"C:\Projects\Unity-Projects\LIT-ISO\Assets\Resources\Tiles"
MAX_IN_FLIGHT = 2   # stay far below the 8-job account cap

STYLE = ("crisp isometric pixel art game tile, 2:1 diamond top surface, "
         "clean 1px edges, cohesive with the reference style tiles, "
         "no text, no border, transparent background outside the tile")


def style_refs(*names):
    refs = []
    for n in names:
        for root, _, files in os.walk(REPO_TILES):
            if n + ".png" in files:
                refs.append({"type": "base64", "base64": b64_file(os.path.join(root, n + ".png"))})
                break
        else:
            p = os.path.join(RES_TILES, n + ".png")
            if os.path.exists(p):
                refs.append({"type": "base64", "base64": b64_file(p)})
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
    "dungeon_stone": {
        "refs": ["dungeon_floor_1", "dungeon_floor_2"],
        "tiles": [
            ("dungeon_moss_1", "ancient stone floor with moss in the cracks"),
            ("dungeon_moss_2", "cracked mossy stone floor, worn smooth"),
            ("dungeon_rune",   "stone floor tile with a faint glowing rune"),
            ("dungeon_broken", "broken stone floor, missing corner chunks"),
            ("dungeon_dark_1", "dark obsidian-like dungeon floor"),
            ("dungeon_dark_2", "dark dungeon floor with ember-orange cracks"),
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
    args = ap.parse_args()

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

    in_flight = []   # (name, job_id)

    def drain(target):
        while len(in_flight) > target:
            name, jid = in_flight.pop(0)
            st = wait_job(tok, jid, name)
            if st:
                imgs = []
                find_images(st, imgs)
                if not imgs:
                    got = call(tok, "GET", f"/tiles-pro/{st.get('tile_id', jid)}", fatal=False)
                    find_images(got, imgs)
                if imgs:
                    save_b64(imgs[0], os.path.join(out_dir, name + ".png"))
                    print("  saved", name)

    for name, desc in fam["tiles"]:
        p = os.path.join(out_dir, name + ".png")
        if os.path.exists(p):
            continue
        print(f"[{name}] submitting ...")
        payload = {
            "description": f"{STYLE}. {desc}",
            "tile_type": "isometric",
            "tile_size": 32,
            "tile_view": "low top-down",
            "style_images": refs,
            "outline_mode": "lineless",
        }
        for attempt in range(5):
            resp = call(tok, "POST", "/create-tiles-pro", payload, fatal=False)
            if "_error" not in resp:
                break
            if resp["_error"] == 429:
                print("  concurrency cap — draining ..."); drain(0); time.sleep(10)
            elif resp["_error"] == 402:
                sys.exit("Out of generations.")
            else:
                print("  rejected — paste the error above to Claude."); resp = None; break
        if not resp:
            continue
        jid = resp.get("background_job_id") or resp.get("tile_id")
        imgs = []
        find_images(resp, imgs)
        if imgs:
            save_b64(imgs[0], os.path.join(out_dir, name + ".png")); print("  saved (inline)")
        elif jid:
            in_flight.append((name, jid))
            drain(MAX_IN_FLIGHT - 1)

    drain(0)
    cs = contact_sheet(out_dir)
    print("\nContact sheet:", cs)
    print("Balance after:", json.dumps(call(tok, "GET", "/balance")))


if __name__ == "__main__":
    main()
