"""
UI POLISH set — everything the comprehensive UI still needs, one consistent
System Brass style, via /generate-ui-v2 (sequential, respects the 8-job cap).

These complete the skin so ALL old UI layers can be retired:
tooltip, scrollbar, toggles, dropdown, right-click context menu, drag ghost,
ability wheel backplate, skill web node frames, rank badge, class card,
minimap frame, quest tracker, tab states, and small inventory action icons
(sort / split / drop) for the smart inventory.

Usage:  py -3 generate_ui_polish.py            (full set)
        py -3 generate_ui_polish.py --only tooltip,tab_active

Output: PixelArt/UIPolish/<piece>.png + _contact_sheet.png
Rank badge: ONE blank medallion is generated; the F/E/D/C/B/A/S letters and
tier tints are applied at runtime (text + color), zero extra generations.
"""

import argparse, base64, json, os, time
from pixellab_common import token, call, find_images

OUT = r"C:\Users\garyc\OneDrive\Desktop\PixelArt\UIPolish"

STYLE = ("dark fantasy LitRPG game UI, pixel art, deep navy-charcoal panel "
         "(#10141f) with thin antique brass-gold border and subtle corner "
         "notches, clean flat surfaces, crisp 1px pixel edges, no text, "
         "no gradients banding, transparent background outside the shape")

PIECES = [
    ("tooltip",        160, 96,  "small tooltip panel, 9-slice friendly: uniform thin border, plain dark center"),
    ("context_menu",   112, 136, "vertical right-click context menu plate, thin border, plain center, 9-slice friendly"),
    ("scroll_track",   16,  128, "thin vertical scrollbar track, recessed dark channel"),
    ("scroll_handle",  16,  48,  "small vertical scrollbar handle, raised brass-edged grip"),
    ("toggle_on",      48,  24,  "small toggle switch in ON state, brass knob right, lit channel"),
    ("toggle_off",     48,  24,  "small toggle switch in OFF state, dark knob left, unlit channel"),
    ("dropdown",       128, 40,  "dropdown selector plate with a small down-arrow notch on the right side"),
    ("drag_ghost",     64,  64,  "square inventory slot outline only, dashed brass border, hollow transparent center"),
    ("wheel_ring",     192, 192, "circular ability wheel backplate, ring divided into four quadrant sockets, dark with brass rim"),
    ("skillnode",      48,  48,  "small circular skill node frame, recessed dark center, thin brass ring"),
    ("skillnode_lit",  48,  48,  "small circular skill node frame, glowing warm brass ring, filled lit center"),
    ("badge_blank",    56,  64,  "ornate blank rank medallion shield, brass frame, empty dark center for a letter"),
    ("class_card",     160, 224, "tall ornate class-selection card frame, decorated brass corners, plain dark center, 9-slice friendly"),
    ("minimap_frame",  160, 160, "square minimap frame, thin brass border with compass notch at top, hollow transparent center"),
    ("quest_tracker",  192, 96,  "subtle quest tracker side panel, left brass accent strip, mostly dark, 9-slice friendly"),
    ("tab_active",     96,  36,  "rectangular tab button, active state, lit brass underline edge"),
    ("tab_idle",       96,  36,  "rectangular tab button, idle state, dark and flat"),
    ("icon_sort",      24,  24,  "tiny icon: two opposing vertical arrows, brass on transparent"),
    ("icon_split",     24,  24,  "tiny icon: a square splitting into two halves with a gap, brass on transparent"),
    ("icon_drop",      24,  24,  "tiny icon: downward arrow over a ground line, brass on transparent"),
]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default="")
    args = ap.parse_args()
    only = {s.strip() for s in args.only.split(",") if s.strip()}

    tok = token()
    os.makedirs(OUT, exist_ok=True)
    print("Balance:", json.dumps(call(tok, "GET", "/balance")))

    done = []
    for name, w, h, desc in PIECES:
        if only and name not in only:
            continue
        p = os.path.join(OUT, name + ".png")
        if os.path.exists(p):
            done.append(name); continue
        print(f"\n[{name}] {w}x{h} ...")
        payload = {"description": f"{STYLE}. This piece: {desc}",
                   "image_size": {"width": w, "height": h}}
        resp = None
        for attempt in range(6):
            resp = call(tok, "POST", "/generate-ui-v2", payload, fatal=False)
            if "_error" not in resp:
                break
            if resp["_error"] == 429:
                print("  job slots busy — waiting 30s ..."); time.sleep(30)
            elif resp["_error"] == 402:
                raise SystemExit("Out of generations.")
            else:
                print("  rejected — paste the error above to Claude."); resp = None; break
        if resp is None:
            continue
        imgs = []
        find_images(resp, imgs)
        if not imgs:
            jid = resp.get("background_job_id")
            if jid:
                from pixellab_common import wait_job
                st = wait_job(tok, jid, name)
                if st: find_images(st, imgs)
        if not imgs:
            print("  no image; response:", json.dumps(resp)[:600]); continue
        open(p, "wb").write(base64.b64decode(imgs[0]))
        print("  saved", p)
        done.append(name)

    try:
        from pixellab_common import contact_sheet
        cs = contact_sheet(OUT)
        print("\nContact sheet:", cs)
    except Exception:
        pass
    print(f"Done: {len(done)}/{len(PIECES)} pieces.")
    print("Balance after:", json.dumps(call(tok, "GET", "/balance")))


if __name__ == "__main__":
    main()
