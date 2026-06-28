"""
Research-only metadata inventory of ISO-CORE sprites/textures.

Reads the installed ISO-CORE Playtest's Unity serialized files and records ONLY
metadata (name, dimensions, format, sprite rect, PPU, pivot, atlas membership).
It NEVER exports pixels. Output is for learning canonical sizes / sheet layouts so
we can REAUTHOR our own placeholder art. Do not wire any ISO-CORE asset into the game.

Usage: python parse_iso_core_sprites.py
"""
import os, csv, json, collections

import UnityPy

DATA_DIR = r"C:/Program Files (x86)/Steam/steamapps/common/ISO-CORE Playtest/ISO CORE_Data"
OUT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))  # Docs/IsoCoreFoundation


def g(obj, *names, default=None):
    for n in names:
        if hasattr(obj, n):
            return getattr(obj, n)
    return default


def categorize(name: str) -> str:
    n = (name or "").lower()
    if "normal" in n or n.endswith("_n"):
        return "normalmap"
    pairs = [
        ("biome", "biome"), ("block", "block"), ("tool", "tool"),
        ("axe", "tool"), ("pickaxe", "tool"), ("shovel", "tool"), ("hoe", "tool"),
        ("sword", "tool"), ("furnace", "building"), ("table", "building"),
        ("station", "building"), ("kiln", "building"), ("anvil", "building"),
        ("house", "building"), ("chest", "building"), ("well", "building"),
        ("windmill", "building"), ("tree", "tree"), ("plant", "plant"),
        ("flower", "plant"), ("mushroom", "plant"), ("crop", "crop"),
        ("wheat", "crop"), ("carrot", "crop"), ("carot", "crop"), ("potato", "crop"),
        ("pumpkin", "crop"), ("seed", "crop"), ("path", "path"), ("fence", "build"),
        ("gate", "build"), ("lantern", "build"), ("bridge", "build"),
        ("slime", "mob"), ("deer", "mob"), ("fox", "mob"), ("frog", "mob"),
        ("fish", "mob"), ("armadillo", "mob"), ("bee", "mob"), ("butterfly", "mob"),
        ("firefly", "mob"), ("mob", "mob"), ("ore", "resource"), ("stone", "resource"),
        ("wood", "resource"), ("fiber", "resource"), ("leather", "resource"),
        ("glass", "resource"), ("copper", "resource"), ("iron", "resource"),
        ("steel", "resource"), ("ui", "ui"), ("button", "ui"), ("bar", "ui"),
        ("icon", "ui"), ("slot", "ui"), ("heart", "ui"), ("effect", "effect"),
        ("water", "nature"), ("rock", "nature"), ("ruin", "nature"),
        ("item", "item"), ("food", "food"), ("potion", "item"), ("elixir", "item"),
    ]
    for key, cat in pairs:
        if key in n:
            return cat
    return "other"


def main():
    print(f"Loading {DATA_DIR} ...")
    env = UnityPy.load(DATA_DIR)

    textures = {}   # path_id -> meta
    tex_rows = []
    spr_rows = []
    errors = 0

    objs = list(env.objects)
    print(f"{len(objs)} objects total. Scanning Texture2D / Sprite ...")

    # Pass 1: textures
    for obj in objs:
        if obj.type.name != "Texture2D":
            continue
        try:
            d = obj.read()
            name = g(d, "m_Name", "name", default="")
            w = int(g(d, "m_Width", "width", default=0) or 0)
            h = int(g(d, "m_Height", "height", default=0) or 0)
            fmt = str(g(d, "m_TextureFormat", "format", default="")).replace("TextureFormat.", "")
            meta = {"name": name, "width": w, "height": h, "format": fmt,
                    "category": categorize(name)}
            textures[obj.path_id] = meta
            tex_rows.append(meta)
        except Exception:
            errors += 1

    # Pass 2: sprites
    for obj in objs:
        if obj.type.name != "Sprite":
            continue
        try:
            d = obj.read()
            name = g(d, "m_Name", "name", default="")
            rect = g(d, "m_Rect", "rect")
            rx = g(rect, "x", default=0); ry = g(rect, "y", default=0)
            rw = g(rect, "width", default=0); rh = g(rect, "height", default=0)
            ppu = g(d, "m_PixelsToUnits", "pixelsToUnits", default=0)
            piv = g(d, "m_Pivot", "pivot")
            px = g(piv, "x", default=0.5); py = g(piv, "y", default=0.5)
            src = ""
            rd = g(d, "m_RD")
            if rd is not None:
                texptr = g(rd, "texture")
                pid = g(texptr, "m_PathID", "path_id")
                if pid in textures:
                    src = textures[pid]["name"]
            spr_rows.append({
                "name": name,
                "rectW": round(float(rw), 1), "rectH": round(float(rh), 1),
                "rectX": round(float(rx), 1), "rectY": round(float(ry), 1),
                "ppu": round(float(ppu), 1),
                "pivotX": round(float(px), 3), "pivotY": round(float(py), 3),
                "sourceTexture": src,
                "category": categorize(name),
            })
        except Exception:
            errors += 1

    # CSV
    csv_path = os.path.join(OUT_DIR, "iso_core_sprite_inventory.csv")
    with open(csv_path, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["kind", "name", "width", "height", "format", "ppu",
                    "pivotX", "pivotY", "rectX", "rectY", "rectW", "rectH",
                    "sourceTexture", "category"])
        for t in tex_rows:
            w.writerow(["texture", t["name"], t["width"], t["height"], t["format"],
                        "", "", "", "", "", "", "", "", t["category"]])
        for s in spr_rows:
            w.writerow(["sprite", s["name"], "", "", "", s["ppu"],
                        s["pivotX"], s["pivotY"], s["rectX"], s["rectY"],
                        s["rectW"], s["rectH"], s["sourceTexture"], s["category"]])

    # Summaries
    spr_cat = collections.Counter(s["category"] for s in spr_rows)
    tex_cat = collections.Counter(t["category"] for t in tex_rows)
    ppu_hist = collections.Counter(s["ppu"] for s in spr_rows)
    size_hist = collections.Counter(
        f'{int(s["rectW"])}x{int(s["rectH"])}' for s in spr_rows if s["rectW"] and s["rectH"])

    summary = {
        "textureCount": len(tex_rows),
        "spriteCount": len(spr_rows),
        "errors": errors,
        "spriteByCategory": dict(spr_cat.most_common()),
        "textureByCategory": dict(tex_cat.most_common()),
        "ppuHistogram": dict(ppu_hist.most_common(12)),
        "topSpriteSizes": dict(size_hist.most_common(25)),
    }
    json_path = os.path.join(OUT_DIR, "iso_core_sprite_inventory.json")
    with open(json_path, "w", encoding="utf-8") as f:
        json.dump({"summary": summary, "textures": tex_rows, "sprites": spr_rows},
                  f, indent=1)

    print(json.dumps(summary, indent=1))
    print(f"\nWrote:\n  {csv_path}\n  {json_path}")


if __name__ == "__main__":
    main()
