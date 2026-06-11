"""
P2 — Biome tile families + iso TRANSITION mini-sets via PixelLab create-tiles-pro.

Concurrency-safe: submits at most MAX_IN_FLIGHT jobs, then waits — the trial/low
tiers cap concurrent background jobs at 8, so we never approach it.

Families run one at a time with a contact sheet each:
  py -3 generate_tilesets.py --family snow
  py -3 generate_tilesets.py --family dungeon_stone
  py -3 generate_tilesets.py --family mountain_stone
  py -3 generate_tilesets.py --family farming
  py -3 generate_tilesets.py --list

NOTE: directional transition tiles via PixelLab FAILED (2026-06-11) — model
ignores edge semantics and materials. Transitions are now generated locally:
Tools/BiomeSketch/make_blends.py
"""

import argparse, json, os, sys, time
from pixellab_common import token, call, wait_job, b64_file, save_b64, find_images, contact_sheet, download_urls

OUT_ROOT = r"C:\Users\garyc\OneDrive\Desktop\PixelArt\Tilesets"
REPO_TILES = r"C:\Projects\Unity-Projects\LIT-ISO\Assets\Generated\Tiles"
RES_TILES = r"C:\Projects\Unity-Projects\LIT-ISO\Assets\Resources\Tiles"
BIOMESKETCH_TILES = r"C:\Projects\Unity-Projects\LIT-ISO\Tools\BiomeSketch\assets\tile"
MAX_IN_FLIGHT = 2   # stay far below the 8-job account cap

STYLE = ("crisp isometric pixel art game tile, 2:1 diamond top surface, "
         "clean 1px edges, cohesive with the reference style tiles, "
         "no text, no border, transparent background outside the tile")


def _ref_entry(path):
    from PIL import Image
    w, h = Image.open(path).size
    return {"type": "base64", "base64": b64_file(path), "width": w, "height": h}


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
    # Farming kit (catalog section K). Crop growth stages come from local
    # recolor/derivation of the mature tile — only soil states are generated.
    "farming": {
        "refs": ["plains2_02", "dirt", "forest_rich_dirt"],
        "tiles": [
            ("farm_tilled_dry",     "flat tilled farmland, dry brown soil with neat furrow rows, full diamond coverage"),
            ("farm_tilled_wet",     "flat tilled farmland, dark moist soil with neat furrow rows, full diamond coverage"),
            ("farm_tilled_planted", "flat tilled dark soil with tiny green seedlings in rows, full diamond coverage"),
            ("farm_soil_rich",      "flat rich dark garden soil, loose crumbly texture, full diamond coverage"),
            ("farm_fallow",         "flat fallow field, pale dry cracked soil with sparse weeds, full diamond coverage"),
            ("farm_mulch",          "flat farm soil covered in straw mulch, full diamond coverage"),
        ],
    },
    # Beach kit: ocean-edge band tiles (water law: sand apron between grass
    # and ocean; ocean = dark water family).
    "beach": {
        "refs": ["sand_1", "plains2_02", "dirt"],
        "tiles": [
            ("beach_sand_1",   "flat pale beach sand, smooth, full diamond coverage"),
            ("beach_sand_2",   "flat beach sand with subtle wind ripple lines, full diamond coverage"),
            ("beach_sand_wet", "flat dark wet sand near the waterline, glossy sheen, full diamond coverage"),
            ("beach_shells",   "flat beach sand with tiny scattered shells and pebbles, full diamond coverage"),
            ("beach_seaweed",  "flat beach sand with a strand of dark washed-up seaweed, full diamond coverage"),
            ("beach_dunegrass","flat beach sand with sparse dry dune grass tufts, full diamond coverage"),
        ],
    },
    # Craftable plank flooring matched to the tree types (chop oak -> craft
    # oak planks you can PLACE as floor tiles).
    "planks": {
        "refs": ["plains2_02", "stone_block", "dirt"],
        "tiles": [
            ("planks_oak",    "flat warm oak wood plank floor tile, visible grain, full diamond coverage"),
            ("planks_pine",   "flat pale pine wood plank floor tile, knotty grain, full diamond coverage"),
            ("planks_willow", "flat greenish-gray willow wood plank floor tile, full diamond coverage"),
            ("planks_dark",   "flat dark stained wood plank floor tile, full diamond coverage"),
            ("path_stone",    "flat fitted stone paver path tile, full diamond coverage"),
            ("path_gravel",   "flat packed gravel path tile, small mixed stones, full diamond coverage"),
        ],
    },
    # Interior kit (catalog section H). Carpets are FLOOR OVERLAYS: walkable,
    # rendered above the floor tile, below props.
    "interior": {
        "refs": ["plains2_02", "stone_block", "dirt"],
        "tiles": [
            ("wood_floor_1",   "flat warm wooden plank floor, horizontal boards, full diamond coverage"),
            ("wood_floor_2",   "flat dark aged wooden plank floor, full diamond coverage"),
            ("stone_floor_in", "flat smooth dressed-stone interior floor, large pale slabs, full diamond coverage"),
            ("carpet_red",     "flat rich red carpet with thin gold trim border, full diamond coverage"),
            ("carpet_blue",    "flat deep blue carpet with silver trim border, full diamond coverage"),
            ("carpet_green",   "flat forest green carpet with brass trim border, full diamond coverage"),
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
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--family", default=None)
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--fetch", default=None,
                    help="tile_id of an already-generated batch to download")
    args = ap.parse_args()

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
            # completed records carry storage URLs, not inline base64.
            # Uploads can lag job completion — refetch until URL count stabilizes.
            tid2 = got.get("id") or got.get("tile_id") or resp.get("tile_id") or resp.get("background_job_id")
            saved = download_urls(tok, got, out_dir, prefix=names[0])
            for retry in range(5):
                if len(saved) >= len(names) or not tid2:
                    break
                print(f"  only {len(saved)}/{len(names)} so far — waiting 20s for uploads ...")
                time.sleep(20)
                got = call(tok, "GET", f"/tiles-pro/{tid2}", fatal=False)
                if isinstance(got, dict) and "_error" not in got:
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
