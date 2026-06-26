"""
P1 catalog props (ore resource nodes + dungeon chest base) via PixelLab
/create-1-direction-object. Same review workflow as generate_props.py:

  py -3 generate_catalog_props.py --family ores     (queue the ore ladder)
  py -3 generate_catalog_props.py --family chest    (queue the chest base)
  py -3 generate_catalog_props.py --status          (download candidates + sheets)
  py -3 generate_catalog_props.py --pick <object_id> 2,5   (promote chosen frames)

Output: PixelArt/Props/<family>/<prop>/candidate_NN.png + _contact_sheet.png
State:  PixelArt/Props/_catalog_objects.json maps object ids -> prop names.

Tier ladder (Docs/handoff/PROP_GENERATION_CATALOG.md section A):
  Copper+Iron=Common, Silver=Uncommon, Gold=Rare, Manacrystal=Epic,
  Starmetal=Mythical. Vein color carries the tier read.
Chest (section E): ONE wooden base; rank variants are derived LOCALLY by
  make_chest_ranks.py (no extra generations).
"""

import argparse, json, os, sys, time
from pixellab_common import token, call, find_images, save_b64, contact_sheet, b64_file, download_urls

OUT = r"C:\Users\garyc\OneDrive\Desktop\PixelArt\Props"
STATE = os.path.join(OUT, "_catalog_objects.json")

STYLE = ("low top-down isometric pixel art game prop, crisp 1px pixel edges, transparent "
         "background, no ground shadow baked in, cohesive with reference style")

FAMILIES = {
    "ores": [
        ("ore_copper",
         "weathered grey rock outcrop mining node, dull orange copper veins streaking "
         "the stone, plain muted palette, base fits within 1 tile"),
        ("ore_iron",
         "weathered grey rock outcrop mining node, dark gray iron bands layered through "
         "the stone, plain muted palette, base fits within 1 tile"),
        ("ore_silver",
         "weathered rock outcrop mining node, pale silver veins with a moonlit sheen, "
         "faint cool night glow as if the stone drinks moonlight, base fits within 1 tile"),
        ("ore_gold",
         "weathered rock outcrop mining node, warm glinting gold veins with tiny "
         "blue-cyan glowing flecks, rare and precious look, base fits within 1 tile"),
        ("ore_manacrystal",
         "dark rock outcrop mining node erupting with a violet-blue manacrystal cluster, "
         "pulsing arcane crystals, purple glow accents, ornate epic look, "
         "base fits within 1 tile"),
        ("ore_starmetal",
         "near-black meteor stone mining node, alien iridescent violet-white starmetal "
         "veins, radiant white-gold fractures, faint otherworldly glow and shimmer, "
         "base fits within 1 tile"),
    ],
    "chest": [
        ("dungeon_chest_wood",
         "closed wooden treasure chest with dark iron banding, plain sturdy dungeon "
         "chest, front-left isometric view, base fits within 1 tile"),
    ],
    "town": [
        ("market_stall_red",
         "wooden market stall with red and white striped canvas canopy, crates of goods "
         "on the counter, base fits within 2x2 tiles"),
        ("market_stall_blue",
         "wooden market stall with blue and white striped canvas canopy, hanging produce "
         "and sacks, base fits within 2x2 tiles"),
        ("boat_rowing",
         "small wooden rowing boat with two oars and a rope coil, seen from low isometric "
         "angle, base fits within 2x1 tiles"),
        ("scarecrow",
         "farm scarecrow on a wooden post, straw hat, patched tunic, arms out, "
         "base fits within 1 tile"),
        ("dock_post",
         "short wooden mooring post wrapped with rope, barnacle ring at the base, "
         "base fits within 1 tile"),
    ],
    "guild": [
        ("guild_banner_stand",
         "tall standing banner on a wooden pole, heraldic cloth with simple emblem shape, "
         "base fits within 1 tile"),
        ("guild_notice_board",
         "wooden quest notice board with pinned parchment notes, slight roof shingle top, "
         "base fits within 2x1 tiles"),
        ("weapon_rack",
         "wooden weapon rack holding swords and a spear, base fits within 2x1 tiles"),
        ("trophy_pedestal",
         "stone pedestal displaying a golden trophy cup, base fits within 1 tile"),
        ("guild_round_table",
         "large round wooden strategy table with a map spread on top, "
         "base fits within 2x2 tiles"),
    ],
    "library": [
        ("bookshelf_tall",
         "tall wooden bookshelf packed with colorful book spines, base fits within 2x1 tiles"),
        ("bookshelf_short",
         "waist-high wooden bookshelf with books and a potted plant on top, "
         "base fits within 1 tile"),
        ("reading_desk",
         "wooden reading desk with an open book and a lit candle, base fits within 2x1 tiles"),
        ("globe_stand",
         "antique world globe on a carved wooden stand, base fits within 1 tile"),
        ("candelabra_standing",
         "tall brass standing candelabra with three lit candles, warm glow, "
         "base fits within 1 tile"),
        ("book_stack",
         "small messy stack of leather-bound books on the floor, base fits within 1 tile"),
    ],
    "tavern": [
        ("bar_counter",
         "wooden tavern bar counter with mugs and a bottle on top, base fits within 2x1 tiles"),
        ("tavern_stool",
         "simple round wooden bar stool, base fits within 1 tile"),
        ("tavern_table",
         "square wooden tavern table with two tankards, base fits within 1 tile"),
        ("ale_barrel",
         "wooden ale barrel with iron hoops and a tap spigot, base fits within 1 tile"),
        ("keg_rack",
         "wooden rack holding three stacked kegs on their sides, base fits within 2x1 tiles"),
        ("tavern_sign",
         "hanging tavern sign on a wrought-iron bracket post, painted mug emblem, "
         "base fits within 1 tile"),
    ],
    # Catalog section J: craftable campsites, tier visual language Common->Mythical
    "camp": [
        ("tent_common",
         "small plain beige canvas camping tent, simple and worn, common rank, "
         "base fits within 2x2 tiles"),
        ("tent_uncommon",
         "canvas camping tent with green trim and a small pennant, sturdy stitching, "
         "uncommon rank, base fits within 2x2 tiles"),
        ("tent_rare",
         "quality camping tent with deep blue cloth, brass pole caps and a small banner, "
         "rare rank, base fits within 2x2 tiles"),
        ("tent_epic",
         "ornate camping tent with violet arcane cloth, faint purple runes glowing on the "
         "seams, epic rank, base fits within 2x2 tiles"),
        ("tent_mythical",
         "majestic camping tent of white and gold radiant cloth, soft golden shimmer and "
         "tiny floating light motes, mythical rank, base fits within 2x2 tiles"),
        ("bedroll",
         "simple rolled-out camping bedroll with a blanket, base fits within 2x1 tiles"),
    ],
    # Building exteriors (catalog section I, REVISED 2026-06-11): footprint
    # GROWS with rank (2x2 -> 3x3 -> 4x4) but the DOOR stays on the same cell —
    # buildings expand back/right away from the door anchor. Door: lower-left
    # face, near the left corner, identical across ranks.
    "buildings": [
        ("tavern_r1",
         "small cozy medieval tavern building, timber frame and plaster, hanging mug "
         "sign, single front door on the lower-left face near the left corner, "
         "base fits within 2x2 tiles"),
        ("tavern_r2",
         "medium prosperous medieval tavern building, two gables, timber frame, hanging "
         "mug sign, same single front door on the lower-left face near the left corner, "
         "base fits within 3x3 tiles"),
        ("tavern_r3",
         "grand medieval tavern inn, two storeys, balcony, warm window light, hanging mug "
         "sign, same single front door on the lower-left face near the left corner, "
         "base fits within 4x4 tiles"),
        ("guild_hall_r1",
         "small stone guild hall with a banner over the entrance, single front door on "
         "the lower-left face near the left corner, base fits within 2x2 tiles"),
        ("guild_hall_r2",
         "medium fortified stone guild hall, twin banners, crenellated trim, same single "
         "front door on the lower-left face near the left corner, base fits within 3x3 tiles"),
        ("guild_hall_r3",
         "grand stone guild hall with a tower, large heraldic banners, gilded trim, same "
         "single front door on the lower-left face near the left corner, "
         "base fits within 4x4 tiles"),
        ("library_r1",
         "small scholarly library building with a round window, single front door on the "
         "lower-left face near the left corner, base fits within 2x2 tiles"),
        ("library_r2",
         "medium library building with a domed skylight and tall arched windows, same "
         "single front door on the lower-left face near the left corner, "
         "base fits within 3x3 tiles"),
        ("library_r3",
         "grand arcane library with an observatory dome and glowing blue windows, same "
         "single front door on the lower-left face near the left corner, "
         "base fits within 4x4 tiles"),
        ("shop_r1",
         "small general store with an awning and crate display, single front door on the "
         "lower-left face near the left corner, base fits within 2x2 tiles"),
        ("shop_r2",
         "medium general store with striped awning, window display and chimney, same "
         "single front door on the lower-left face near the left corner, "
         "base fits within 3x3 tiles"),
        ("shop_r3",
         "grand emporium store with double striped awnings, ornate signage, lantern "
         "posts, same single front door on the lower-left face near the left corner, "
         "base fits within 4x4 tiles"),
    ],
    # Crafting stations — every profession's interactable workbench
    "stations": [
        ("crafting_table",
         "sturdy wooden crafting workbench with tools, hammer and pliers on top, "
         "base fits within 2x1 tiles"),
        ("furnace",
         "stone smelting furnace with a glowing orange ember mouth and small chimney, "
         "base fits within 1 tile"),
        ("anvil",
         "blacksmith iron anvil mounted on a heavy wooden stump, base fits within 1 tile"),
        ("tanning_rack",
         "wooden tanning rack frame with a stretched animal hide laced by cords, "
         "base fits within 2x1 tiles"),
        ("rune_station",
         "arcane runecrafting altar, stone lectern carved with glowing blue runes and a "
         "floating crystal, base fits within 1 tile"),
        ("alchemy_table",
         "alchemy workbench with glass alembics, bubbling potions and herb bundles, "
         "base fits within 2x1 tiles"),
        ("cooking_station",
         "cooking station with a hanging cauldron over coals and a spit rack, "
         "base fits within 2x1 tiles"),
        ("loom",
         "wooden weaving loom with stretched threads and a cloth in progress, "
         "base fits within 2x1 tiles"),
        ("sawmill_bench",
         "carpentry saw bench with a large saw blade and stacked cut planks, "
         "base fits within 2x1 tiles"),
        ("grindstone",
         "pedal-driven sharpening grindstone wheel on a wooden frame, "
         "base fits within 1 tile"),
    ],
    # Light sources. NOTE: an animated campfire already exists in-game
    # (Resources/FoundationCampfire/campfire-Sheet.png) — campfire_new here is
    # the style-matched upgrade; flame flicker is animated at runtime by
    # FoundationCampfireAnimator-style glow, or later via animate-with-text.
    "lights": [
        ("campfire_new",
         "small campfire with stacked stones ring, burning logs and bright orange "
         "flames, warm glow on the ground, base fits within 1 tile"),
        ("torch_standing",
         "standing wooden torch post with a burning flame head, warm glow, "
         "base fits within 1 tile"),
        ("torch_wall",
         "wall-mounted iron torch sconce with a burning flame, drawn as a standalone "
         "sprite, base fits within 1 tile"),
        ("brazier",
         "iron fire brazier bowl on three legs filled with glowing coals and flame, "
         "base fits within 1 tile"),
        ("lantern_post",
         "tall wrought-iron street lantern post with a warm glowing glass lamp, "
         "base fits within 1 tile"),
        ("candle_lantern",
         "small portable candle lantern sitting on the ground, soft warm glow, "
         "base fits within 1 tile"),
    ],
    # tiny night-ambient sprites; runtime animates them (drift + alpha pulse)
    "ambient": [
        ("glowbug",
         "tiny glowing firefly bug with a soft warm yellow-green light halo, very small "
         "sprite, dark body, base fits within 1 tile"),
        ("wisp",
         "small pale blue will-o-wisp, wispy glowing orb with a faint trail, very small "
         "sprite, base fits within 1 tile"),
    ],
}


def load_state():
    return json.load(open(STATE)) if os.path.exists(STATE) else {}


def save_state(s):
    os.makedirs(OUT, exist_ok=True)
    json.dump(s, open(STATE, "w"), indent=1)


def style_refs(family):
    """Approved plains v2 props as style anchors. Ore nodes are rock-like, so
    the rock ref leads; the tree ref is a secondary general-style anchor."""
    candidates = [os.path.join(OUT, "plains", "plains_rock_v2", "frame_0.png"),
                  os.path.join(OUT, "plains", "plains_tree_v2", "frame_0.png")]
    if family == "chest":
        candidates.reverse()  # chest is not rock-like; lead with the general ref
    if family == "buildings":
        # the in-game tavern is the building style anchor
        candidates = [r"C:\Projects\Unity-Projects\LIT-ISO\Assets\Resources\FoundationBuildings\tavern_building.png",
                      os.path.join(OUT, "plains", "plains_tree_v2", "frame_0.png")]
    refs = []
    for p in candidates:
        if os.path.exists(p):
            # PixelLab /create-1-direction-object rejects style_images
            # width/height metadata; send only the image payload.
            refs.append({"type": "base64", "base64": b64_file(p)})
    return refs[:2]


def payload_preview(payload):
    preview = dict(payload)
    preview["style_images"] = [
        {"type": img.get("type"), "base64_bytes": len(img.get("base64", ""))}
        for img in payload.get("style_images", [])
    ]
    return preview


def submit(tok, family, dry_run=False):
    fam = FAMILIES.get(family) or sys.exit(f"unknown family {family}; options: {list(FAMILIES)}")
    state = load_state()
    refs = style_refs(family)
    queued = 0
    rejected = 0
    for name, desc in fam:
        if not dry_run and any(v.get("name") == name for v in state.values()):
            print(f"[{name}] already queued/known - skipping"); continue
        print(f"[{name}] submitting ...")
        payload = {"description": f"{STYLE}. {desc}",
                   "view": "top-down",
                   "style_images": refs}
        if not refs:
            payload["size"] = 64
        if dry_run:
            print(json.dumps(payload_preview(payload), indent=2))
            continue
        resp = None
        for attempt in range(20):
            resp = call(tok, "POST", "/create-1-direction-object", payload, fatal=False)
            if "_error" not in resp:
                break
            if resp["_error"] == 402:
                sys.exit("Out of generations.")
            if resp["_error"] == 429:
                print("  all 8 job slots busy - waiting 30s ..."); time.sleep(30); continue
            rejected += 1
            print("  rejected - paste the error above to Claude."); resp = None; break
        if resp is None:
            continue
        oid = resp.get("object_id") or resp.get("id")
        if not oid:
            print("  no object id:", str(resp)[:400]); continue
        state[oid] = {"name": name, "family": family}
        save_state(state)
        queued += 1
        print("  queued:", oid)
        time.sleep(2)
    if queued:
        print(f"\nQueued {queued}. Run --status in a few minutes to fetch review candidates.")
    elif dry_run:
        print("\nDry run complete. No PixelLab objects were created.")
    else:
        print(f"\nQueued 0; rejected {rejected}. No PixelLab objects were created.")


def status(tok):
    state = load_state()
    if not state: sys.exit("nothing queued yet")
    for oid, meta in state.items():
        name, family = meta["name"], meta["family"]
        st = call(tok, "GET", f"/objects/{oid}?include_preview=true", fatal=False)
        if "_error" in st: continue
        s = str(st.get("status", "")).lower()
        print(f"[{name}] {s}  ({oid})")
        imgs = []
        find_images(st, imgs)
        d = os.path.join(OUT, family, name)
        if imgs:
            for i, b in enumerate(imgs[:40]):
                save_b64(b, os.path.join(d, f"candidate_{i:02d}.png"))
        saved = download_urls(tok, st, d, prefix="candidate_url")
        if imgs or saved:
            cs = contact_sheet(d)
            print(f"  {len(imgs) + len(saved)} candidates -> {cs or d}")


def pick(tok, oid, indices):
    resp = call(tok, "POST", f"/objects/{oid}/select-frames",
                {"indices": [int(i) for i in indices.split(",")]}, fatal=False)
    print(json.dumps(resp)[:600] if isinstance(resp, dict) else resp)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--family", default=None)
    ap.add_argument("--status", action="store_true")
    ap.add_argument("--pick", nargs=2, metavar=("OBJECT_ID", "INDICES"))
    ap.add_argument("--dry-run", action="store_true",
                    help="print request payloads without calling PixelLab")
    a = ap.parse_args()
    if a.dry_run:
        if not a.family:
            sys.exit("--dry-run requires --family <name>")
        submit(None, a.family, dry_run=True)
        return
    tok = token()
    print("Balance:", json.dumps(call(tok, "GET", "/balance")))
    if a.pick: pick(tok, a.pick[0], a.pick[1])
    elif a.status: status(tok)
    elif a.family: submit(tok, a.family)
    else: print("families:", {k: len(v) for k, v in FAMILIES.items()},
                "\nuse --family <name>, --status, or --pick <id> <i,j>")


if __name__ == "__main__":
    main()
