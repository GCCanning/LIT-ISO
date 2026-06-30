"""
Generate LIT-ISO campfire-party main-menu background candidates via the
PixelLab REST API v2.

This script asks PixelLab for a cinematic title-screen key-art scene: four
detailed adventurers from distinct classes resting around a campfire at night.

Usage:
  py -3 generate_campfire_party_menu_scene.py --dry-run
  py -3 generate_campfire_party_menu_scene.py --count 3
  py -3 generate_campfire_party_menu_scene.py --count 5 --prefix campfire_party_cinematic

Outputs land in:
  C:/Users/garyc/OneDrive/Desktop/PixelArt/MenuScene/CampfireParty/
"""

import argparse
import base64
import json
import os
import sys
import urllib.error
import urllib.request

API = "https://api.pixellab.ai/v2"
OUT_DIR = r"C:\Users\garyc\OneDrive\Desktop\PixelArt\MenuScene\CampfireParty"
TOKEN_FILE = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                          "pixellab_token.local.txt")
TOKEN_ENV = "PIXELLAB_TOKEN"

PARTY_BRIEF = {
    "concept": (
        "warm cozy cinematic dungeon party resting around a campfire at night "
        "with tents, cooking food, drinks, warm firelight, and menu-safe "
        "negative space"
    ),
    "moodAnchor": (
        "https://easy-peasy.ai/ai-image-generator/images/"
        "dark-forest-camping-pixel-art"
    ),
    "style": (
        "LIT-ISO PixelLab isometric pixel art, original assets, no copied "
        "external art"
    ),
    "party": [
        {
            "id": "frontliner",
            "label": "Frontliner",
            "class": "tank or guardian",
            "look": (
                "sturdy armored adventurer with shield, travel-worn cloak, "
                "distinct protective silhouette, relaxed beside the campfire"
            )
        },
        {
            "id": "healer",
            "label": "Healer",
            "class": "mage or healer",
            "look": (
                "warm robed caster with staff or lantern, gentle healing glow, "
                "soft readable silhouette, cozy and kind rather than battle-posed"
            )
        },
        {
            "id": "damage_dealer",
            "label": "Damage Dealer",
            "class": "ranger, spear fighter, or spellblade",
            "look": (
                "confident adventurer with a bow, spear, or blade nearby, "
                "clearly different silhouette from the tank and rogue"
            )
        },
        {
            "id": "rogue",
            "label": "Rogue",
            "class": "rogue or scout",
            "look": (
                "hooded scout with dagger, shortbow, satchel, or map, seated "
                "near the edge of the firelight with a distinct nimble silhouette"
            )
        }
    ]
}

BASE_PROMPT = (
    "premium cinematic 2D pixel-art title screen key art for an original "
    "isometric survival crafting LitRPG, wide 16:9 main menu background, not "
    "a gameplay screenshot and not a tile-editor diorama. A four-person "
    "dungeon party rests together around a glowing campfire at night in an "
    "organic deep forest clearing. The area around the campsite should feel "
    "natural and lived-in, not tiled, not gridded, not like a map editor: uneven "
    "mossy ground, soft dirt patches, pine needles, roots, mushrooms, ferns, "
    "wild grass, small flowers, fallen branches, scattered stones, tree stumps, "
    "dark shrubs, and layered pine trees fading into moonlit fog. The mood is "
    "warm, cozy, safe, and inviting after a long adventure: shared food, mugs, "
    "cooking pot over the flames, bedrolls, packs, open crates, tents, small "
    "lanterns, soft smiles, relaxed poses. The four adventurers should be more "
    "detailed than gameplay sprites, with expressive class silhouettes and "
    "individual outfits chosen by the artist. Do not copy the exact in-game "
    "character sheets. Design them as original title-screen heroes who belong "
    "in the LIT-ISO world. The characters and campfire sit center-right; the "
    "entire left third is darker quiet negative space for menu buttons, made "
    "from shadowed forest, not empty flat color. Make the scene aspirational "
    "title-screen art in the same universe: cinematic, polished, and richly lit, "
    "but still crisp hand-authored pixel art with hard pixel edges, no painterly "
    "blur, no smooth gradients. Use a 3/4 isometric camera angle with layered "
    "depth: dark foreground pine silhouettes, a warm firelit campsite in the "
    "midground, cool moonlit forest and faint ruined dungeon stones in the "
    "background. Strong warm orange campfire light, cool navy moon shadows, "
    "subtle smoke, fireflies, rim light on armor and tents, limited cozy dark "
    "fantasy palette, no text, no logo, no UI, no watermark. The party must read "
    "as four distinct adventurer classes, not generic identical campers."
)

NEGATIVE = (
    "text, watermark, logo, ui, frame, border, blurry, painterly, realistic, "
    "3d render, smooth gradients, anime splash art, oversized characters, "
    "copied game style, terraria clone, stardew clone, flat side-view scene, "
    "small board game diorama, tile editor scene, gameplay screenshot, empty "
    "campfire, only one character, only two characters, only three characters, "
    "identical campers, generic campers, simple stick-figure characters, low "
    "detail characters, faceless characters, visible square grid, obvious tile "
    "grid, repeated ground tiles, boring flat campsite, empty blue floor, plain "
    "geometric platform, centered symmetrical composition, bright left-side UI "
    "area, baked menu text"
)

ANIMATION_BRIEF = (
    "subtle seamless main-menu loop: campfire flickers, sparks rise, smoke "
    "curls upward, fireflies drift, warm light gently pulses on characters, "
    "tents, crates, and nearby ground, mage staff glow breathes softly, tent "
    "cloth and pine branches sway slightly in night wind; camera remains static, "
    "characters remain seated/resting, no walking, no composition changes"
)


def load_token():
    env_token = os.environ.get(TOKEN_ENV, "").strip()
    if env_token:
        return env_token[7:] if env_token.lower().startswith("bearer ") else env_token
    try:
        tok = open(TOKEN_FILE, encoding="utf-8").read().strip()
    except OSError:
        sys.exit(f"Token missing: set {TOKEN_ENV} or create {TOKEN_FILE}")
    if tok.lower().startswith("bearer "):
        tok = tok[7:]
    return tok


def call(token, method, path, payload=None, timeout=300):
    req = urllib.request.Request(
        API + path,
        data=json.dumps(payload).encode() if payload is not None else None,
        method=method,
        headers={
            "Authorization": f"Bearer {token}",
            "Content-Type": "application/json",
            "Accept": "application/json",
        },
    )
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        body = e.read().decode(errors="replace")
        print(f"\nHTTP {e.code} on {method} {path}:\n{body}\n")
        raise SystemExit(1)


def find_base64_images(node, found):
    if isinstance(node, dict):
        if node.get("type") == "base64" and isinstance(node.get("base64"), str):
            found.append(node["base64"])
        else:
            for value in node.values():
                find_base64_images(value, found)
    elif isinstance(node, list):
        for value in node:
            find_base64_images(value, found)


def upscale_nearest(path, factor=4):
    try:
        from PIL import Image
    except ImportError:
        return None
    im = Image.open(path)
    big = im.resize((im.width * factor, im.height * factor), Image.NEAREST)
    out = path.replace(".png", f"_x{factor}.png")
    big.save(out)
    return out


def build_prompt():
    party_text = " Party class direction, let PixelLab design the details: " + " ".join(
        f"{member['label']} ({member['class']}): {member['look']}."
        for member in PARTY_BRIEF["party"]
    )
    return BASE_PROMPT + party_text


def write_handoff(prompt, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, "campfire_party_prompt_handoff.json")
    payload = {
        "version": 2,
        "target": "Assets/Resources/UI/Menu/background.png",
        "outputSize": "1920x1080 after nearest-neighbor upscale/crop",
        "pixelLabRequestSize": "400x224",
        "recipe": PARTY_BRIEF,
        "prompt": prompt,
        "negativePrompt": NEGATIVE,
        "animationBrief": ANIMATION_BRIEF,
        "animationTarget": "Assets/Resources/UI/Menu/background_frames/frame_00.png..frame_08.png",
        "acceptance": [
            "four party roles are readable around the campfire",
            "left third stays dark and quiet enough for menu buttons",
            "scene reads as premium cinematic pixel-art key art, not a small tilemap diorama",
            "no text, logo, watermark, copied external art, or baked UI",
            "tents, cooking pot, food/drinks, packs, and bedrolls are visible",
            "animation can be isolated to fire, smoke, fireflies, glow, tent cloth, and tree sway"
        ]
    }
    with open(path, "w", encoding="utf-8") as f:
        json.dump(payload, f, indent=2)
    return path


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--count", type=int, default=3)
    ap.add_argument("--width", type=int, default=400)
    ap.add_argument("--height", type=int, default=224)
    ap.add_argument("--out-dir", default=OUT_DIR)
    ap.add_argument("--prefix", default="campfire_party_cinematic")
    ap.add_argument("--dry-run", action="store_true",
                    help="write handoff JSON and print prompt without using API")
    args = ap.parse_args()

    prompt = build_prompt()
    out_dir = args.out_dir
    handoff = write_handoff(prompt, out_dir)
    print("handoff:", handoff)
    print("\nPROMPT:\n" + prompt)
    print("\nNEGATIVE:\n" + NEGATIVE)

    if args.dry_run:
        return

    token = load_token()
    os.makedirs(out_dir, exist_ok=True)

    bal = call(token, "GET", "/balance")
    print("\nBalance:", json.dumps(bal))

    for i in range(1, args.count + 1):
        print(f"\nGenerating campfire party candidate {i}/{args.count} "
              f"({args.width}x{args.height}) ...")
        payload = {
            "description": prompt,
            "negative_description": NEGATIVE,
            "image_size": {"width": args.width, "height": args.height},
            "no_background": False,
        }
        resp = call(token, "POST", "/create-image-pixflux", payload)

        images = []
        find_base64_images(resp, images)
        if not images:
            print("No image in response; full response follows:")
            print(json.dumps(resp, indent=2)[:4000])
            continue

        path = os.path.join(out_dir, f"{args.prefix}_{i:02d}.png")
        with open(path, "wb") as f:
            f.write(base64.b64decode(images[0]))
        print("saved:", path)
        preview = upscale_nearest(path)
        if preview:
            print("preview:", preview)

    print("\nDone. Review the *_x4 previews, then promote the winner to")
    print("Assets/Resources/UI/Menu/background.png after 1920x1080 finishing.")


if __name__ == "__main__":
    main()
