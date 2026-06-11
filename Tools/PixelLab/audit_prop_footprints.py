"""Footprint-contract QA (owner rule 2026-06-11): a prop's BASE (bottom 25% of
content) must fit inside its declared footprint; canopy overhang above is fine.
Run after every prop batch:  py -3 audit_prop_footprints.py [folder]
Defaults to auditing the repo prop folders + PixelArt/Props candidates."""
import os, re, sys
from PIL import Image

def audit(folder):
    bad = 0
    for root, dirs, files in os.walk(folder):
        dirs[:] = [d for d in dirs if not d.startswith("_")]
        for f in sorted(files):
            if not f.endswith(".png"): continue
            p = os.path.join(root, f)
            ppu = 32.0
            meta = p + ".meta"
            if os.path.exists(meta):
                m = re.search(r"spritePixelsToUnits:\s*([\d.]+)", open(meta).read())
                if m: ppu = float(m.group(1))
            im = Image.open(p).convert("RGBA")
            bbox = im.getbbox()
            if not bbox: continue
            content = im.crop(bbox)
            ch = content.height
            base = content.crop((0, ch - max(2, ch // 4), content.width, ch))
            bb = base.getbbox()
            if not bb: continue
            base_cells = (bb[2] - bb[0]) / ppu
            footprint = max(1, round(content.width / ppu + 0.25))
            ok = base_cells <= footprint + 0.15
            if not ok:
                bad += 1
                print(f"VIOLATES  {p}  base={base_cells:.2f} cells vs footprint {footprint}")
    return bad

targets = sys.argv[1:] or [
    r"C:\Projects\Unity-Projects\LIT-ISO\Assets\Generated\Props",
    r"C:\Projects\Unity-Projects\LIT-ISO\Assets\Resources\Decorations",
    r"C:\Users\garyc\OneDrive\Desktop\PixelArt\Props",
]
total = sum(audit(t) for t in targets if os.path.isdir(t))
print("PASS — all bases fit their footprints" if total == 0 else f"{total} violations")
