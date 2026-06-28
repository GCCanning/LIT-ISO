#!/usr/bin/env python3
import argparse, json, os, struct, sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    sys.exit("Pillow required: pip install Pillow")

FRAME = 64

# canonical_anim_id : (v1_png_filename_stem, cols)   rows derived from PNG height
ANIMS = [
    ("walk",      "walk",        9),
    ("spellcast", "spellcast",   7),
    ("thrust",    "thrust",      8),
    ("slash",     "slash",       6),
    ("shoot",     "shoot",      13),
    ("hurt",      "hurt",        6),
    ("idle",      "idle",        2),
    ("run",       "run",         8),
    ("jump",      "jump",        5),
    ("sit",       "sit",         3),
    ("emote",     "emote",       3),
    ("climb",     "climb",       6),
    ("combat",    "combat_idle", 2),
    ("backslash", "backslash",  13),
    ("halfslash", "halfslash",   6),
]
ANIM_FPS = {"walk":10,"spellcast":8,"thrust":10,"slash":12,"shoot":14,"hurt":8,
            "idle":6,"run":12,"jump":8,"sit":4,"emote":6,"climb":8,"combat":6,
            "backslash":12,"halfslash":12}

# Oversize swing layers (great weapons / tools). LPC ships these "custom_animation"
# bands at a larger frame so the weapon arc extends past the 64px body box. The
# stored weapon PNG is authored at frameSize px; the body (64px) is centred inside
# it, so frameOffset = (frameSize - 64) / 2. Map each custom_animation name to the
# canonical game animation it overrides, its frame size, and column count.
# (rows are always 4 = N,W,S,E; cols/frameSize verified against custom-animations.ts
#  and the on-disk PNG dimensions.)
OVERSIZE = {
    # name                  : (canonical_anim, frameSize, cols)
    "slash_oversize":         ("slash",     192, 6),
    "slash_reverse_oversize": ("backslash", 192, 6),
    "thrust_oversize":        ("thrust",    192, 8),
    "whip_oversize":          ("thrust",    192, 8),
    "slash_128":              ("slash",     128, 6),
    "backslash_128":          ("backslash", 128, 13),
    "halfslash_128":          ("halfslash", 128, 6),
    "thrust_128":             ("thrust",    128, 8),
    "walk_128":               ("walk",      128, 9),
    "tool_rod":               ("thrust",    128, 13),
    "tool_whip":              ("thrust",    192, 8),
}

BODYTYPES = ["male", "female"]

# category (top sheet_definitions folder) -> (slot, kind, equipSlot)
# equipSlot is None for cosmetic items.
CAT_MAP = {
    "body":     ("body",   "cosmetic",  None),
    "head":     ("head",   "cosmetic",  None),       # heads/eyes/faces resolved finer below
    "hair":     ("hair",   "cosmetic",  None),       # incl. beards/mustaches (facial hair)
    "torso":    ("shirt",  "cosmetic",  None),       # basic clothes; armour overridden below
    "legs":     ("pants",  "cosmetic",  None),       # pants/skirts; greaves overridden below
    "feet":     ("shoes",  "cosmetic",  None),       # shoes; armoured boots overridden below
    "arms":     ("hands",  "equipment", "hands"),    # gauntlets/bracers/shoulders
    "headwear": ("head",   "equipment", "head"),     # helmets/hoods/hats/coverings
    "weapons":  ("weapon", "equipment", "weapon"),   # shields overridden below
    "tools":    ("weapon", "equipment", "weapon"),
}

# variant material -> palette file basename (under palette_definitions/<material>/)
MATERIAL_PALETTE = {
    "body": "body", "cloth": "cloth", "metal": "metal",
    "hair": "hair", "eye": "eye",
}

SAMPLE = [
    "body/body.json",
    "head/heads/human/heads_human_male.json",
    "head/eyes/eyes_cyclops.json",
    "hair/short/hair_plain.json",
    "torso/shirts/torso_clothes_tunic.json",
    "legs/pants/legs_pants.json",
    "feet/shoes/feet_shoes_basic.json",
    "torso/armour/torso_armour_plate.json",
    "headwear/helmets/helmets/hat_helmet_legion.json",
    "weapons/sword/weapon_sword_arming.json",
    "weapons/magic/weapon_magic_gnarled.json",
    "weapons/shields/shield_round.json",
]


def png_size(p):
    with open(p, "rb") as f:
        f.read(16)
        return struct.unpack(">II", f.read(8))


def load_palette(src_root, material):
    base = MATERIAL_PALETTE.get(material)
    if not base:
        return None
    p = src_root / "palette_definitions" / base / f"{base}_ulpc.json"
    if not p.exists():
        return None
    return json.loads(p.read_text())


def infer_material(src_root, variants):
    """When a def has no recolors, guess the palette material by matching the
    item's variant set against each palette's keys (best overlap wins)."""
    if not variants:
        return None
    vset = set(variants)
    best, best_key = None, (0, 0)  # (overlap, -palette_size): prefer most overlap, then tightest fit
    for mat in MATERIAL_PALETTE:
        pal = load_palette(src_root, mat)
        if not pal:
            continue
        keys = set(pal.keys())
        overlap = len(vset & keys)
        key = (overlap, -len(keys))
        if key > best_key:
            best, best_key = mat, key
    # require the variants to be substantially covered by the palette
    if best and best_key[0] >= max(2, len(vset) // 2):
        return best
    return None


def derive(category, sub_path, def_data):
    """Resolve (slot, kind, equipSlot) with sub-category overrides."""
    slot, kind, equip = CAT_MAP.get(category, ("accessory", "equipment", "accessory"))
    sp = sub_path.replace("\\", "/")
    tn = def_data.get("type_name", "")
    name = def_data.get("name", "").lower()
    if category == "head":
        if "/eyes/" in sp or tn == "eyes":
            return ("eyes", "cosmetic", None)
        if "/faces/" in sp or "/eyebrows/" in sp or "/nose/" in sp or "/ears/" in sp:
            return ("head", "cosmetic", None)
        return ("head", "cosmetic", None)
    if category == "hair":
        if "/beards/" in sp or "/mustaches/" in sp:
            return ("hair", "cosmetic", None)  # facial hair
        return ("hair", "cosmetic", None)
    if category == "torso":
        if "/armour/" in sp or "chainmail" in sp or "/jacket/" in sp:
            return ("shirt", "equipment", "chest")
        if "/cape/" in sp:
            return ("accessory", "equipment", "back")
        if "/backpack/" in sp:
            return ("accessory", "equipment", "back")
        if "/waist/" in sp:
            return ("accessory", "equipment", "waist")
        return ("shirt", "cosmetic", None)
    if category == "legs":
        if "armour" in sp:
            return ("pants", "equipment", "legs")
        return ("pants", "cosmetic", None)
    if category == "feet":
        if "armour" in sp:
            return ("shoes", "equipment", "feet")
        return ("shoes", "cosmetic", None)
    if category == "weapons":
        if "/shields/" in sp or tn == "shield":
            return ("offhand", "equipment", "offhand")
        return ("weapon", "equipment", "weapon")
    if category == "headwear":
        if "/neck/" in sp:
            return ("accessory", "equipment", "accessory")
        return ("head", "equipment", "head")
    return (slot, kind, equip)


def resolve_anim_png(src_root, rel_dir, fname, variants):
    """Find the base-palette source PNG for one animation.
    Pattern A: <rel_dir>/<fname>.png  (single base sheet)
    Pattern B: <rel_dir>/<fname>/<baseVariant>.png  (per-variant; pick first existing)
    Returns absolute path or None."""
    base = src_root / "spritesheets" / rel_dir
    a = base / f"{fname}.png"
    if a.exists():
        return a
    d = base / fname
    if d.is_dir():
        if variants:
            for v in variants:
                p = d / f"{v}.png"
                if p.exists():
                    return p
        pngs = sorted(d.glob("*.png"))
        if pngs:
            return pngs[0]
    return None


def resolve_oversize_png(src_root, rel_dir, variants):
    """Find the base-palette PNG for an oversize layer. The layer's rel path is
    the directory holding <baseVariant>.png (or a lone *.png like fg/bg)."""
    base = src_root / "spritesheets" / rel_dir
    if not base.is_dir():
        return None
    if variants:
        for v in variants:
            p = base / f"{v}.png"
            if p.exists():
                return p
    pngs = sorted(base.glob("*.png"))
    return pngs[0] if pngs else None


def build_oversize_bands(src_root, oversize_layers, bodytype, variants):
    """Stitch each oversize swing animation into its own band at native frame
    size. oversize_layers: list of (custom_animation_name, layer_dict). Returns
    list of (canonical_anim_id, frameSize, cols, rows, [(zPos, png_path)...])
    grouped + sorted by canonical id, or [] if none resolve for this bodytype."""
    groups = {}  # canonical_anim -> (frameSize, cols, rows, [(zPos, path)])
    for name, layer in oversize_layers:
        spec = OVERSIZE.get(name)
        if not spec:
            continue
        canon, fsize, cols = spec
        rel = layer.get(bodytype)
        if not rel:
            continue
        p = resolve_oversize_png(src_root, rel, variants)
        if p is None:
            continue
        w, h = png_size(p)
        rows = h // fsize
        g = groups.setdefault(canon, (fsize, cols, rows, []))
        g[3].append((layer["zPos"], p))
    out = []
    for canon, (fsize, cols, rows, paths) in groups.items():
        if paths:
            out.append((canon, fsize, cols, rows, sorted(paths)))
    return out


def build_sheet(src_root, layers, bodytype, variants, oversize_layers=None):
    """Stitch all available animations into ONE universal sheet for one bodytype.
    Composites every layer_N (by zPos asc) per animation. Returns (PIL.Image, anim_layout) or (None,None)."""
    # Resolve each animation: collect (anim_id, cols, rows, [layer source paths]).
    resolved = []
    for anim_id, fname, cols in ANIMS:
        paths = []
        rows = None
        ok = True
        for layer in layers:
            rel = layer.get(bodytype)
            if not rel:
                ok = False
                break
            p = resolve_anim_png(src_root, rel, fname, variants)
            if p is None:
                ok = False
                break
            w, h = png_size(p)
            r = h // FRAME
            if rows is None:
                rows = r
            paths.append((layer["zPos"], p))
        if ok and paths:
            resolved.append((anim_id, cols, rows, sorted(paths)))
    # Oversize swing bands (great weapons): each is authored at its own frameSize
    # (128 or 192). They are stitched below the normal 64px bands and carry pixel
    # offsets so the compositor can slice them with their native frame size.
    over = build_oversize_bands(src_root, oversize_layers or [], bodytype, variants)
    # Drop any normal-band anim that an oversize band overrides (great weapons ship
    # only the oversize attack, never a 64px one -- but guard against duplicates).
    over_ids = {canon for canon, *_ in over}
    resolved = [r for r in resolved if r[0] not in over_ids]

    if not resolved and not over:
        return None, None

    norm_h = sum(rows for _, _, rows, _ in resolved) * FRAME
    over_h = sum(rows * fsize for _, fsize, _, rows, _ in over)
    widths = [cols * FRAME for _, cols, _, _ in resolved] + \
             [cols * fsize for _, fsize, cols, _, _ in over]
    max_w = max(widths) if widths else FRAME
    total_h = norm_h + over_h
    sheet = Image.new("RGBA", (max_w, total_h), (0, 0, 0, 0))
    layout = []

    y_px = 0
    for anim_id, cols, rows, paths in resolved:
        band = Image.new("RGBA", (cols * FRAME, rows * FRAME), (0, 0, 0, 0))
        for _, p in paths:
            img = Image.open(p).convert("RGBA")
            band.alpha_composite(img.crop((0, 0, min(img.width, cols * FRAME),
                                           min(img.height, rows * FRAME))))
        sheet.paste(band, (0, y_px))
        layout.append({"id": anim_id, "rowOffset": y_px // FRAME, "rows": rows,
                       "cols": cols, "fps": ANIM_FPS.get(anim_id, 10)})
        y_px += rows * FRAME

    for canon, fsize, cols, rows, paths in over:
        band = Image.new("RGBA", (cols * fsize, rows * fsize), (0, 0, 0, 0))
        for _, p in paths:
            img = Image.open(p).convert("RGBA")
            band.alpha_composite(img.crop((0, 0, min(img.width, cols * fsize),
                                           min(img.height, rows * fsize))))
        sheet.paste(band, (0, y_px))
        layout.append({"id": canon, "rowOffset": y_px // FRAME, "rows": rows,
                       "cols": cols, "fps": ANIM_FPS.get(canon, 10),
                       "frameSize": fsize, "frameOffset": (fsize - FRAME) // 2,
                       "rowOffsetPx": y_px})
        y_px += rows * fsize
    return sheet, layout


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", default=r"C:\Projects\Universal-LPC-Spritesheet-Character-Generator-master")
    ap.add_argument("--dst", default=r"C:\Projects\Unity-Projects\LIT-ISO\Assets\Resources\Characters\Layers")
    ap.add_argument("--items", default="sample",
                    help='"sample", "all", or comma-separated sheet_definition relpaths')
    ap.add_argument("--merge", default=None,
                    help="path to an existing layer_catalog.json to merge into "
                         "(replace items by id, append new); omit to write a fresh catalog")
    ap.add_argument("--report", action="store_true", default=True)
    args = ap.parse_args()

    src = Path(args.src)
    dst = Path(args.dst)
    sd_root = src / "sheet_definitions"

    if args.items == "sample":
        rels = SAMPLE
    elif args.items == "all":
        rels = [str(p.relative_to(sd_root)).replace("\\", "/")
                for p in sd_root.rglob("*.json")
                if not p.name.startswith("meta_")]
    else:
        rels = [s.strip() for s in args.items.split(",") if s.strip()]

    out_dir = dst / "lpc"
    out_dir.mkdir(parents=True, exist_ok=True)

    items = []
    credits = {}
    report = []

    for rel in rels:
        defp = sd_root / rel
        if not defp.exists():
            report.append((rel, "DEF-MISSING", "", "", "", 0, 0, 0))
            continue
        d = json.loads(defp.read_text(encoding="utf-8"))
        category = rel.split("/")[0]
        sub = rel
        slot, kind, equip = derive(category, sub, d)

        layers = [d[k] for k in sorted(d) if k.startswith("layer_") and "custom_animation" not in d[k]]
        if not layers:
            layers = [d[k] for k in sorted(d) if k.startswith("layer_")]
        # Oversize swing layers (great weapons / tools): captured into separate
        # native-frame-size bands so big weapons don't clip mid-swing.
        oversize_layers = [(d[k]["custom_animation"], d[k]) for k in sorted(d)
                           if k.startswith("layer_") and d[k].get("custom_animation") in OVERSIZE]

        # variants + palette material
        recolors = d.get("recolors", {})
        material = recolors.get("material") if isinstance(recolors, dict) else None
        if not material and isinstance(recolors, dict):
            for v in recolors.values():
                if isinstance(v, dict) and v.get("material"):
                    material = v["material"]; break
        variants = list(d.get("variants", []))
        if not material:
            material = infer_material(src, variants)
        if not variants and material:
            pal = load_palette(src, material)
            if pal:
                variants = list(pal.keys())
        if not variants:
            variants = ["default"]

        item_id = "lpc/" + Path(rel).stem
        draws = []
        anim_layout = None
        sheet_count = 0
        for bt in BODYTYPES:
            if not any(bt in l for l in layers):
                # fall back: some items only ship one base body (e.g. unisex 'thin'); skip missing bt
                continue
            sheet, layout = build_sheet(src, layers, bt, variants, oversize_layers)
            if sheet is None:
                continue
            anim_layout = anim_layout or layout
            item_dir = out_dir / Path(rel).stem
            item_dir.mkdir(parents=True, exist_ok=True)
            out_png = item_dir / f"{bt}.png.bytes"
            sheet.save(out_png, "PNG")
            sheet_count += 1
            draws.append({
                "bodyType": bt,
                "sheet": f"Characters/Layers/lpc/{Path(rel).stem}/{bt}",
                "sortOrder": min(l["zPos"] for l in layers),
            })

        # credits aggregation
        for c in d.get("credits", []):
            key = c.get("file", rel)
            credits[key] = c

        item = {
            "id": item_id,
            "slot": slot,
            "kind": kind,
            "displayName": d.get("name", Path(rel).stem),
            "matchBodyColor": bool(d.get("match_body_color", False)),
            "paletteMaterial": material,
            "baseVariant": d.get("base_variant", variants[0]),
            "variants": variants,
            "animations": anim_layout or [],
            "draws": draws,
        }
        if equip:
            item["equipSlot"] = equip
        items.append(item)
        report.append((item_id, "OK", slot, kind, equip or "-",
                       draws[0]["sheet"] if draws else "-", len(variants), len(anim_layout or [])))

    if args.merge:
        # Re-import a subset: splice these items into an existing catalog (replace
        # by id, append new). Leaves untouched items + credits as they were.
        catp = Path(args.merge)
        catalog = json.loads(catp.read_text(encoding="utf-8"))
        by_id = {it["id"]: it for it in catalog.get("items", [])}
        for it in items:
            by_id[it["id"]] = it
        catalog["items"] = list(by_id.values())
        catp.write_text(json.dumps(catalog, indent=2), encoding="utf-8")
    else:
        catalog = {
            "version": 4,
            "frameSize": FRAME,
            "defaultAnimation": "walk",
            "rowOrder": ["N", "W", "S", "E"],
            "pixelsPerUnit": 64,
            "pivotX": 0.5,
            "pivotY": 0.5,
            "bodyTypes": BODYTYPES,
            "items": items,
        }
        (dst / "layer_catalog.json").write_text(json.dumps(catalog, indent=2), encoding="utf-8")

        # credits text
        lines = ["Universal LPC Spritesheet Generator - Attribution\n"]
        for key, c in sorted(credits.items()):
            lines.append(f"== {key} ==")
            if c.get("authors"):
                lines.append("Authors: " + ", ".join(c["authors"]))
            if c.get("licenses"):
                lines.append("Licenses: " + ", ".join(c["licenses"]))
            if c.get("urls"):
                lines.append("URLs:\n  " + "\n  ".join(c["urls"]))
            lines.append("")
        (dst / "lpc_credits.txt").write_text("\n".join(lines), encoding="utf-8")

    if args.report:
        print(f"\nImported {len(items)} item(s), {sum(1 for r in report if r[1]=='OK')} OK.")
        print(f"{'item':28} {'slot':8} {'kind':9} {'equip':9} {'var':>3} {'anim':>4}  sheet")
        for r in report:
            iid, st, slot, kind, eq, sheet, nv, na = r
            if st != "OK":
                print(f"{iid:28} {st}")
                continue
            print(f"{iid:28} {slot:8} {kind:9} {eq:9} {nv:>3} {na:>4}  {sheet}")
    return items, report


if __name__ == "__main__":
    main()
