"""
P5 — Export everything generated (and approved repo art) into the local LoRA
training dataset, so each PixelLab purchase compounds toward replacing PixelLab.

Targets the Pixel Pipeline layout Codex already trains from:
  C:/Projects/Pixel Pipeline/datasets/lit_iso/<set>/img.png + img.txt (caption)

Sources:
  - PixelArt/Characters/<name>/  (incl. extracted character ZIPs)
  - PixelArt/Tilesets/<family>/
  - Repo approved tiles/props (Assets/Generated/Tiles|Props)

Captions are auto-built from folder/file names: character, state, direction,
frame; tiles get family + name. Dry-run by default — pass --write to copy.

  py -3 export_training_dataset.py            (dry run: report only)
  py -3 export_training_dataset.py --write
"""

import argparse, glob, os, re, shutil, sys, zipfile

PIPE = r"C:\Projects\Pixel Pipeline\datasets\lit_iso"
CHARS = r"C:\Users\garyc\OneDrive\Desktop\PixelArt\Characters"
TILES = r"C:\Users\garyc\OneDrive\Desktop\PixelArt\Tilesets"
REPO_T = r"C:\Projects\Unity-Projects\LIT-ISO\Assets\Generated\Tiles"
REPO_P = r"C:\Projects\Unity-Projects\LIT-ISO\Assets\Generated\Props"

DIRS = {"s": "south", "se": "south-east", "e": "east", "ne": "north-east",
        "n": "north", "nw": "north-west", "w": "west", "sw": "south-west"}


def caption_for_character(char, rel):
    """pixelab zips: e.g. animations/walk/south/frame_03.png or rotations/east.png"""
    parts = rel.lower().replace("\\", "/").split("/")
    state = next((p for p in parts if p in
                  ("idle", "walk", "run", "sprint", "jump", "attack_staff",
                   "cast_magic", "walking", "running")), None)
    direction = next((DIRS[p] for p in parts if p in DIRS), None)
    if direction is None:
        m = re.search(r"(south|north|east|west|[ns][ew])", rel.lower())
        direction = m.group(1) if m else None
    frame = None
    m = re.search(r"(\d+)\.png$", rel)
    if m: frame = int(m.group(1))
    bits = ["pixel art isometric game character", char.replace("_", " ")]
    if state: bits.append(state.replace("_", " "))
    if direction: bits.append("facing " + direction)
    if frame is not None: bits.append(f"frame {frame}")
    bits.append("transparent background")
    return ", ".join(bits)


def collect():
    items = []  # (src_path, set_name, out_name, caption)
    # characters (folders + zips)
    if os.path.isdir(CHARS):
        for char in os.listdir(CHARS):
            cdir = os.path.join(CHARS, char)
            if not os.path.isdir(cdir): continue
            for z in glob.glob(os.path.join(cdir, "*_full.zip")):
                xdir = os.path.join(cdir, "_zip")
                if not os.path.isdir(xdir):
                    with zipfile.ZipFile(z) as f: f.extractall(xdir)
            for root, _, files in os.walk(cdir):
                for f in files:
                    if not f.endswith(".png") or f.startswith("_"): continue
                    src = os.path.join(root, f)
                    rel = os.path.relpath(src, cdir)
                    out = f"{char}__{rel.replace(os.sep, '__')}"
                    items.append((src, f"characters_{char}", out,
                                  caption_for_character(char, rel)))
    # generated tilesets
    if os.path.isdir(TILES):
        for fam in os.listdir(TILES):
            fdir = os.path.join(TILES, fam)
            if not os.path.isdir(fdir): continue
            for f in os.listdir(fdir):
                if not f.endswith(".png") or f.startswith("_"): continue
                items.append((os.path.join(fdir, f), f"tiles_{fam}", f,
                              f"pixel art isometric game tile, {fam.replace('_',' ')}, "
                              f"{os.path.splitext(f)[0].replace('_',' ')}, "
                              "2:1 diamond, transparent background"))
    # approved repo art
    for root_dir, kind in ((REPO_T, "tile"), (REPO_P, "prop")):
        if not os.path.isdir(root_dir): continue
        for root, dirs, files in os.walk(root_dir):
            dirs[:] = [d for d in dirs if not d.startswith("_")]
            for f in files:
                if not f.endswith(".png"): continue
                biome = os.path.basename(root).lower()
                items.append((os.path.join(root, f), f"approved_{kind}s", f,
                              f"pixel art isometric game {kind}, {biome} biome, "
                              f"{os.path.splitext(f)[0].replace('_',' ')}, "
                              "transparent background"))
    return items


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--out", default=PIPE)
    args = ap.parse_args()

    items = collect()
    from collections import Counter
    print(Counter(s for _, s, _, _ in items))
    print(f"total: {len(items)} images")
    if not args.write:
        print("\nDRY RUN — pass --write to export to", args.out)
        for src, st, out, cap in items[:5]:
            print(" e.g.", st, "/", out, "->", cap)
        return
    for src, set_name, out, cap in items:
        d = os.path.join(args.out, set_name)
        os.makedirs(d, exist_ok=True)
        dst = os.path.join(d, out)
        shutil.copy2(src, dst)
        open(os.path.splitext(dst)[0] + ".txt", "w", encoding="utf-8").write(cap)
    print(f"\nexported {len(items)} image+caption pairs to {args.out}")
    print("Hand off to Codex: ready for the next LoRA run (see Pixel Pipeline).")


if __name__ == "__main__":
    main()
