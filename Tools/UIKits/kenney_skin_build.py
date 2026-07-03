#!/usr/bin/env python3
"""Kenney -> LIT-ISO dark-wood adventurer HUD skin (playtest 2026-07-02 task #9a).

Recolours Kenney CC0 pieces (Tools/UIKits/Kenney/, greyscale-friendly) to the
owner-locked palette and exports them over the GameUIController auto-load slots
in Assets/Resources/UI/InGame/ (see _DROP_INGAME_UI_HERE.md). Pixel-native:
nearest-neighbour only, no anti-aliasing added.

Palette (owner, 2026-07-02):
  wood #3E2A1C  inset #2A1B10  brass #C9A24B  selected #E8C468
  text #E8DCC0  dim #A89878    ember #D96A2E
  HP #C94F44    MP #4F7FC9     stamina #6FA65A  XP #E8C468

Run: python3 Tools/UIKits/kenney_skin_build.py  (from repo root)
"""
import os, re, sys
from PIL import Image

ROOT = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else ".")
KEN = os.path.join(ROOT, "Tools", "UIKits", "Kenney")
OUT = os.path.join(ROOT, "Assets", "Resources", "UI", "InGame")
FB = os.path.join(KEN, "kenney_fantasy-ui-borders", "PNG", "Default")
UP = os.path.join(KEN, "kenney_ui-pack", "PNG", "Grey", "Default")

def hexc(s, a=255):
    s = s.lstrip("#")
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16), a)

WOOD    = hexc("3E2A1C")
WOOD_HI = hexc("4A3524")   # subtle top-light wood
INSET   = hexc("2A1B10")
INSET_D = hexc("1C110A")   # inset shadow line
BRASS   = hexc("C9A24B")
BRASS_D = hexc("8a6f33")   # aged brass shadow
SELECT  = hexc("E8C468")
EMBER   = hexc("D96A2E")
HP      = hexc("C94F44")
MP      = hexc("4F7FC9")
XP      = hexc("E8C468")
OUTLINE = hexc("120c07")

def tint_white_art(im, rgba):
    """Kenney fantasy borders are white line art: multiply to the target colour."""
    im = im.convert("RGBA")
    px = im.load()
    r, g, b, _ = rgba
    for y in range(im.height):
        for x in range(im.width):
            pr, pg, pb, pa = px[x, y]
            if pa == 0:
                continue
            px[x, y] = (pr * r // 255, pg * g // 255, pb * b // 255, pa)
    return im

def lut_recolour(im, stops):
    """Map greyscale luminance bands -> palette colours. stops: [(max_lum, rgba)...]"""
    im = im.convert("RGBA")
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            lum = (299 * r + 587 * g + 114 * b) // 1000
            for maxl, col in stops:
                if lum <= maxl:
                    px[x, y] = (col[0], col[1], col[2], a)
                    break
    return im

def solid(w, h, fill, border=None, bw=1):
    im = Image.new("RGBA", (w, h), fill)
    if border:
        px = im.load()
        for x in range(w):
            for i in range(bw):
                px[x, i] = border
                px[x, h - 1 - i] = border
        for y in range(h):
            for i in range(bw):
                px[i, y] = border
                px[w - 1 - i, y] = border
    return im

def bar_fill(w, h, col):
    """Solid fill with a 1px top highlight + 1px bottom shade (pixel-art depth)."""
    hi = tuple(min(255, int(c * 1.25)) for c in col[:3]) + (255,)
    lo = tuple(int(c * 0.68) for c in col[:3]) + (255,)
    im = Image.new("RGBA", (w, h), col)
    px = im.load()
    for x in range(w):
        px[x, 0] = hi       # PIL row 0 = image top -> highlight on top
        px[x, h - 1] = lo   # shade along the bottom
    return im

def panel(size, border_png, fill=WOOD, trim=BRASS, inset=3):
    """Walnut fill under a brass-tinted fantasy border."""
    base = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    body = solid(size - 2 * inset, size - 2 * inset, fill, border=WOOD_HI, bw=1)
    base.paste(body, (inset, inset))
    border = tint_white_art(Image.open(border_png), trim)
    if border.size != (size, size):
        border = border.resize((size, size), Image.NEAREST)
    base.alpha_composite(border)
    return base

made = {}
def save(name, im):
    im.save(os.path.join(OUT, name + ".png"))
    made[name] = im.size
    print(f"  {name}.png  {im.size[0]}x{im.size[1]}")

os.makedirs(OUT, exist_ok=True)
print("Building dark-wood Kenney skin ->", OUT)

# ---- panels (corner-braced adventurer frame) ----
save("vitals_bg",    panel(48, os.path.join(FB, "Border", "panel-border-000.png")))
save("system_panel", panel(48, os.path.join(FB, "Border", "panel-border-010.png")))
save("hotbar_bg",    panel(48, os.path.join(FB, "Border", "panel-border-015.png")))

# ---- slots: Kenney square button LUT'd to dark inset wells ----
sq = Image.open(os.path.join(UP, "button_square_border.png"))
save("slot",     lut_recolour(sq, [(60, OUTLINE), (140, INSET_D), (200, INSET), (255, BRASS_D)]))
save("inv_slot", lut_recolour(sq, [(60, OUTLINE), (140, INSET_D), (200, INSET), (255, BRASS_D)]))
sqd = Image.open(os.path.join(UP, "button_square_depth_border.png"))
save("slot_selected", lut_recolour(sqd, [(60, OUTLINE), (140, INSET), (200, WOOD), (255, SELECT)]))

# ---- buttons ----
rect = Image.open(os.path.join(UP, "button_rectangle_depth_border.png"))
save("button", lut_recolour(rect, [(60, OUTLINE), (140, INSET_D), (200, WOOD), (255, BRASS)]))
save("craft_row",  lut_recolour(rect, [(60, OUTLINE), (140, INSET_D), (200, WOOD_HI), (255, BRASS_D)]))
save("system_row", lut_recolour(rect, [(60, OUTLINE), (140, INSET_D), (200, WOOD_HI), (255, BRASS_D)]))
xicon = os.path.join(UP, "icon_cross.png")
btn = lut_recolour(sq.copy(), [(60, OUTLINE), (140, INSET_D), (200, WOOD), (255, BRASS)])
if os.path.exists(xicon):
    ic = tint_white_art(Image.open(xicon), EMBER)
    ic.thumbnail((btn.width // 2, btn.height // 2), Image.NEAREST)
    btn.alpha_composite(ic, ((btn.width - ic.width) // 2, (btn.height - ic.height) // 2))
save("btn_close", btn)

# ---- bars: inset tracks + palette fills ----
save("bar_track",    solid(24, 16, INSET, border=BRASS_D, bw=1))
save("bar_xp_track", solid(24, 12, INSET, border=BRASS_D, bw=1))
save("bar_health_fill", bar_fill(24, 12, HP))
save("bar_mana_fill",   bar_fill(24, 12, MP))
save("bar_xp_fill",     bar_fill(24, 8,  XP))

# ---- update 9-slice borders in the existing .metas (keeps GUIDs/wiring) ----
BORDERS = {
    "vitals_bg": 14, "system_panel": 14, "hotbar_bg": 14,
    "slot": 16, "inv_slot": 16, "slot_selected": 16,
    "button": 16, "craft_row": 16, "system_row": 16, "btn_close": 16,
    "bar_track": 5, "bar_xp_track": 4,
    "bar_health_fill": 3, "bar_mana_fill": 3, "bar_xp_fill": 3,
}
for name, b in BORDERS.items():
    meta = os.path.join(OUT, name + ".png.meta")
    if not os.path.exists(meta):
        print(f"  (no meta for {name} - Unity will generate one; set border {b} manually)")
        continue
    s = open(meta, encoding="utf-8").read()
    s2 = re.sub(r"spriteBorder: \{x: [\d.-]+, y: [\d.-]+, z: [\d.-]+, w: [\d.-]+\}",
                f"spriteBorder: {{x: {b}, y: {b}, z: {b}, w: {b}}}", s)
    if s2 != s:
        open(meta, "w", encoding="utf-8", newline="").write(s2)
print(f"done: {len(made)} slot images written; metas updated with 9-slice borders")
