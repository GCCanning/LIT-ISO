"""
Generate cliff / rock-face tiles at 64x32 px (tile_size=64, isometric 2:1).

Three tiers matching the reference art:
  Tier 1 (mesa tops)     — flat diamond tops, slight surface texture, no/minimal height
  Tier 2 (cliff faces)   — tall thick block, maximum side-face depth, 1-2 cell footprint
  Tier 3 (small outcrops)— compact single-cell rocks for edge scatter

Usage:
  cd Tools/PixelLab
  py -3 generate_cliff_tiles.py
  py -3 generate_cliff_tiles.py --dry-run    # print payload, no API call

After generation, review contact sheet in OUT_DIR, then copy approved PNGs to
  Assets/Resources/Tiles/
and add entries to FoundationContent.BuildDefault() under the 'mountain' block group.
"""

import argparse, json, os, sys, time

# Ensure pixellab_common is always importable regardless of launch directory
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

try:
    from pixellab_common import token, call, wait_job, b64_file, save_b64, find_images, contact_sheet, download_urls
except ImportError as e:
    sys.exit(f"Cannot find pixellab_common.py — run this script from Tools/PixelLab/\n  {e}")

OUT_DIR   = r"C:\Users\garyc\OneDrive\Desktop\PixelArt\CliffTiles"
RES_TILES = r"C:\Projects\Unity-Projects\LIT-ISO\Assets\Resources\Tiles"
REFS_DIR  = r"C:\Projects\Unity-Projects\LIT-ISO\Tools\PixelLab\cliff_refs"
# Drop reference images into cliff_refs/ before running:
#   cliff_ref_tileset.png  — the isometric cliff block tileset reference
#   cliff_ref_ingame.png   — the in-game screenshot showing cliff formations
# Up to 3 total style images (PixelLab limit); stone tiles fill any empty slots.

STYLE = (
    "isometric pixel art cliff rock tile, natural organic rock formation NOT a geometric cube, "
    "irregular uneven top face with natural stone texture, "
    "side faces show clear horizontal geological strata bands and natural crack lines, "
    "warm mid-grey stone palette with light grey highlights on upper faces and dark charcoal in deep crevices, "
    "same natural rock art style as ISO-Core pixel art tileset, "
    "clean crisp pixel edges, transparent background, no shadow baked in, no text, no border"
)

# 13 tiles across 3 tiers. PixelLab maps numbered descriptions -> output slots.
TILES = [
    # --- Tier 1: Mesa / Plateau Tops (flat diamond surface, thin height) ---
    ("cliff_mesa_01",
     "1). flat natural stone plateau top tile, dry sparse grass growing from cracks, "
     "weathered irregular stone surface texture, thin cliff height visible on sides"),
    ("cliff_mesa_02",
     "2). flat mossy natural stone plateau top tile, thin green moss spreading across "
     "the top face, uneven weathered stone surface, thin cliff height on sides"),
    ("cliff_mesa_03",
     "3). flat bare cracked natural stone plateau top tile, deep fissure lines on top face, "
     "no vegetation, pure weathered grey stone, thin cliff height on sides"),

    # --- Tier 2: Large Cliff Formations (tall natural rock mass, organic shape) ---
    ("cliff_face_01",
     "4). tall natural grey rock cliff formation, organic irregular shape NOT a cube, "
     "bold geological strata bands layered horizontally on side faces, "
     "uneven top with single dry grass clump, deep charcoal crevice shadows"),
    ("cliff_face_02",
     "5). large natural grey stone cliff mass, stepped irregular silhouette on top edge, "
     "prominent horizontal strata fracture lines across the full side face height, "
     "small green grass patches on upper top corners"),
    ("cliff_face_03",
     "6). dark grey rough natural rock cliff face, organic irregular top edge, "
     "mossy green patches clinging to lower side face crevices, "
     "deep shadow cuts between the strata bands"),
    ("cliff_face_04",
     "7). warm mid-grey worn natural stone cliff, smooth water-worn side faces, "
     "subtle layered strata bands, slightly rounded organic top edge, no vegetation"),
    ("cliff_face_05",
     "8). natural grey stone cliff formation, orange-brown lichen patches on upper side faces, "
     "irregular organic top silhouette, clear horizontal strata lines"),

    # --- Tier 3: Small Rock Outcrops (compact single-cell, organic shape) ---
    ("cliff_rock_sm_01",
     "9). small natural grey rock outcrop, organic irregular shape, rough angular surface, "
     "slightly taller than wide, strata lines on side face"),
    ("cliff_rock_sm_02",
     "10). compact natural grey stone boulder, cracked uneven top surface, "
     "organic rounded silhouette, short squat form"),
    ("cliff_rock_sm_03",
     "11). small dark grey natural rocky chunk, jagged irregular top edge, "
     "deep crevice shadows, compact footprint"),
    ("cliff_rock_sm_04",
     "12). small natural grey stone outcrop, single grass tuft growing from a top crack, "
     "organic irregular shape, weathered surface"),
    ("cliff_rock_sm_05",
     "13). small natural warm-grey stone cluster, two joined rock forms, "
     "organic irregular silhouette, worn weathered faces"),
]


def load_refs():
    """Load style refs: reference images first, then pl_stone tiles as fallback.
    PixelLab accepts up to 3 style images. cliff_refs/ takes priority because
    the tileset + in-game screenshots anchor the cliff block visual style directly.
    """
    refs = []

    from PIL import Image

    def ref(path):
        w, h = Image.open(path).size
        return {"type": "base64", "base64": b64_file(path), "width": w, "height": h}

    # 1. Preferred: cliff reference images placed in cliff_refs/
    for name in ("cliff_ref_tileset.png", "cliff_ref_ingame.png"):
        p = os.path.join(REFS_DIR, name)
        if os.path.exists(p):
            refs.append(ref(p))

    # 2. Fill remaining slots with existing stone tiles for material match
    for name in ("pl_stone_01", "pl_stone_02"):
        if len(refs) >= 3:
            break
        p = os.path.join(RES_TILES, name + ".png")
        if os.path.exists(p):
            refs.append(ref(p))

    if not refs:
        print("  [warn] no style refs found — generation will use description only")
    else:
        print(f"  {len(refs)} style ref(s) loaded")
    return refs[:3]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true",
                    help="Print payload without calling PixelLab")
    args = ap.parse_args()

    os.makedirs(OUT_DIR, exist_ok=True)

    names = [name for name, _ in TILES]
    description = STYLE + ". " + " ".join(desc for _, desc in TILES)

    payload = {
        "description": description,
        "tile_type":   "isometric",
        "tile_size":   64,          # → 64x32 isometric output
        "tile_view":   "low top-down",
        "style_images": load_refs(),
    }

    if args.dry_run:
        preview = dict(payload)
        preview["style_images"] = f"[{len(payload['style_images'])} refs loaded]"
        print(json.dumps(preview, indent=2))
        print(f"\n{len(TILES)} tiles described, {len(names)} output files expected.")
        return

    tok = token()
    print("Balance:", json.dumps(call(tok, "GET", "/balance")))
    print(f"Submitting 1 batch of {len(TILES)} cliff tiles ...")

    resp = None
    for attempt in range(6):
        resp = call(tok, "POST", "/create-tiles-pro", payload, fatal=False)
        if "_error" not in resp:
            break
        if resp["_error"] == 429:
            print("  rate limited — waiting 30s"); time.sleep(30)
        elif resp["_error"] == 402:
            sys.exit("Out of credits.")
        else:
            print("  error:", str(resp)[:300]); sys.exit(1)

    if resp is None:
        sys.exit("No response from API.")

    # Collect images
    imgs = []
    find_images(resp, imgs)
    if not imgs:
        jid = resp.get("background_job_id")
        tid = resp.get("tile_id") or jid
        if jid:
            print(f"  background job {jid} — polling ...")
            st = wait_job(tok, jid, "cliff_tiles")
            if st:
                find_images(st, imgs)
        if not imgs and tid:
            got = call(tok, "GET", f"/tiles-pro/{tid}", fatal=False)
            find_images(got, imgs)
            if not imgs:
                all_saved = download_urls(tok, got, OUT_DIR, prefix="tmp")
                imgs_paths = all_saved[:len(names)]
                for k, name in enumerate(names):
                    if k < len(imgs_paths):
                        dst = os.path.join(OUT_DIR, name + ".png")
                        os.replace(imgs_paths[k], dst)
                        print(f"  saved {name}.png")
                for extra in all_saved[len(names):]:
                    if os.path.exists(extra): os.remove(extra)
                contact_sheet(OUT_DIR)
                print(f"\nBalance after:", json.dumps(call(tok, "GET", "/balance")))
                return

    # Save named tiles
    for k, name in enumerate(names):
        if k < len(imgs):
            path = os.path.join(OUT_DIR, name + ".png")
            save_b64(imgs[k], path)
            print(f"  saved {name}.png")
        else:
            print(f"  [warn] no image for slot {k+1} ({name})")

    contact_sheet(OUT_DIR)
    print(f"\nGot {len(imgs)} images for {len(names)} requested.")
    print("Balance after:", json.dumps(call(tok, "GET", "/balance")))
    print(f"Output: {OUT_DIR}")
    print("Review contact sheet, then copy approved tiles to:")
    print(f"  {RES_TILES}")
    print("Then register in FoundationContent.BuildDefault() under the cliff block group.")


if __name__ == "__main__":
    import traceback
    LOG = os.path.join(os.path.dirname(os.path.abspath(__file__)), "cliff_tiles_crash.log")
    try:
        main()
    except SystemExit:
        raise
    except Exception:
        msg = traceback.format_exc()
        with open(LOG, "w") as f:
            f.write(msg)
        print("\n--- CRASH ---\n" + msg)
        print(f"Log written to: {LOG}")
        input("\nPress Enter to close...")
        import sys; sys.exit(1)
