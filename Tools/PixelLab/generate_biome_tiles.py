"""
Generate 2 tile variants per biome in ONE PixelLab call (16 tiles total).

8 biomes x 2 = 16 tiles, matching PixelLab's batch size exactly:
  grass, stone, sand (beach/desert), forest, dungeon, meadow, ice, badlands

Style target: ISO-Core block — clean flat side faces matching the top material,
no props or vegetation baked in.

Usage:
  cd Tools/PixelLab
  py -3 generate_biome_tiles.py
  py -3 generate_biome_tiles.py --dry-run    # print payload, no API call

tile_thickness is not exposed in the v2 API — depth is driven through description.
"""

import argparse, json, os, sys, time
from pixellab_common import token, call, wait_job, b64_file, save_b64, find_images, contact_sheet, download_urls

OUT_DIR   = r"C:\Users\garyc\OneDrive\Desktop\PixelArt\BiomeTiles"
RES_TILES = r"C:\Projects\Unity-Projects\LIT-ISO\Assets\Resources\Tiles"

STYLE = (
    "crisp isometric pixel art game tile, tall thick cube block, 2:1 diamond top surface, "
    "deep tall side faces equal in height to the tile width, "
    "side faces are a flat darker shade of the top surface material, "
    "side face colour matches the material of the top face, "
    "side face depth is maximum, cube looks thick and solid not flat, "
    "no vegetation, rocks, or props baked onto the tile, terrain surface only, "
    "clean 1px pixel edges, transparent background, no text, no border"
)

# 16 tiles in order — PixelLab maps numbered descriptions to output slots 1-16
TILES = [
    # Grass biome
    ("grass_base",      "1). lush green grass top, flat brown earthy dirt side faces"),
    ("grass_accent",    "2). green grass with small clover patches, flat brown earthy dirt side faces"),
    # Stone biome
    ("stone_base",      "3). flat grey granite stone surface, flat darker grey granite side faces"),
    ("stone_accent",    "4). grey granite with natural crack lines, flat darker grey granite side faces"),
    # Sand biome — beach variant and desert variant
    ("sand_beach",      "5). flat pale cool beach sand, flat tan sandy side faces"),
    ("sand_desert",     "6). flat warm golden-orange desert sand, flat golden sand side faces"),
    # Forest biome
    ("forest_base",     "7). flat dark rich forest floor with leaf litter, flat dark brown earth side faces"),
    ("forest_accent",   "8). dark forest floor with moss patches, flat dark brown earth side faces"),
    # Dungeon biome
    ("dungeon_base",    "9). flat dark grey ancient stone dungeon floor, flat darker grey stone side faces"),
    ("dungeon_accent",  "10). dark grey dungeon stone with worn crack lines, flat darker grey stone side faces"),
    # Meadow biome
    ("meadow_base",     "11). bright green meadow grass, flat brown earth side faces"),
    ("meadow_accent",   "12). bright green meadow grass with tiny wildflowers on top, flat brown earth side faces"),
    # Ice biome
    ("ice_base",        "13). flat pale blue-white smooth ice surface, flat grey-blue stone side faces"),
    ("ice_accent",      "14). pale ice with faint frost crack lines on top, flat grey-blue stone side faces"),
    # Badlands biome
    ("badlands_base",   "15). flat cracked dry red-brown clay earth, flat darker red-brown clay side faces"),
    ("badlands_accent", "16). cracked dry red-brown clay with deep fissure lines, flat darker red-brown clay side faces"),
]


def load_refs():
    refs = []
    for name in ("plains2_02", "dungeon2_06", "snow2_07"):
        p = os.path.join(RES_TILES, name + ".png")
        if os.path.exists(p):
            from PIL import Image
            w, h = Image.open(p).size
            refs.append({"type": "base64", "base64": b64_file(p), "width": w, "height": h})
    return refs[:3]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()

    os.makedirs(OUT_DIR, exist_ok=True)

    names = [name for name, _ in TILES]
    description = STYLE + ". " + " ".join(desc for _, desc in TILES)

    payload = {
        "description": description,
        "tile_type":   "isometric",
        "tile_size":   32,
        "tile_view":   "low top-down",
        "style_images": load_refs(),
    }

    if args.dry_run:
        preview = dict(payload)
        preview["style_images"] = f"[{len(payload['style_images'])} refs]"
        print(json.dumps(preview, indent=2))
        print(f"\n{len(TILES)} tiles described, {len(names)} output files expected.")
        return

    tok = token()
    print("Balance:", json.dumps(call(tok, "GET", "/balance")))
    print(f"Submitting 1 batch of {len(TILES)} tiles ...")

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
            st = wait_job(tok, jid, "biome_tiles")
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
                    if os.path.exists(extra):
                        os.remove(extra)
                contact_sheet(OUT_DIR)
                print(f"\nBalance after:", json.dumps(call(tok, "GET", "/balance")))
                return

    # Save exactly the 16 named tiles
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
    print("Review contact sheet, then copy approved tiles to Resources/Tiles/")


if __name__ == "__main__":
    main()
