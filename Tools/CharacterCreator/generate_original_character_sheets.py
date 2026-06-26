#!/usr/bin/env python3
"""Generate original LIT-ISO character creator sheets.

This is a deterministic first-pass doll-sheet generator. It intentionally uses
simple authored pixel geometry rather than external game pixels.
"""

from __future__ import annotations

import hashlib
import json
import random
import re
from pathlib import Path

from PIL import Image, ImageDraw


REPO = Path(__file__).resolve().parents[2]
PLAYER_DIR = REPO / "Assets" / "Resources" / "Characters" / "Player"
OUT_DIR = PLAYER_DIR / "Generated"
REVIEW_DIR = REPO / "Assets" / "Generated" / "_Review" / "character_creator_original_suite_v1"
META_TEMPLATE = PLAYER_DIR / "BlackMage_Idle_512x1024.png.meta"
TEMPLATE_NAME = "BlackMage_Idle_512x1024"

CELL = 128
FRAMES = 4
ROWS = 8
SHEET_W = CELL * FRAMES
SHEET_H = CELL * ROWS
DIRS = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"]


PALETTES = {
    "adventurer": {
        "skin": (180, 120, 76, 255),
        "hair": (52, 36, 28, 255),
        "shirt": (42, 102, 122, 255),
        "trim": (228, 180, 82, 255),
        "pants": (52, 60, 78, 255),
        "boots": (42, 31, 25, 255),
        "accent": (106, 162, 92, 255),
    },
    "ranger": {
        "skin": (201, 148, 103, 255),
        "hair": (84, 55, 26, 255),
        "shirt": (56, 115, 72, 255),
        "trim": (159, 120, 58, 255),
        "pants": (74, 66, 46, 255),
        "boots": (44, 34, 27, 255),
        "accent": (92, 145, 105, 255),
    },
    "mystic": {
        "skin": (146, 100, 78, 255),
        "hair": (226, 220, 184, 255),
        "shirt": (70, 50, 112, 255),
        "trim": (126, 194, 210, 255),
        "pants": (38, 38, 62, 255),
        "boots": (31, 28, 42, 255),
        "accent": (160, 92, 205, 255),
    },
    "smith": {
        "skin": (186, 124, 86, 255),
        "hair": (45, 39, 36, 255),
        "shirt": (112, 70, 44, 255),
        "trim": (214, 132, 70, 255),
        "pants": (55, 63, 66, 255),
        "boots": (39, 33, 29, 255),
        "accent": (178, 180, 166, 255),
    },
}

CHARACTERS = [
    ("litiso_creator_adventurer", "LitIsoCreator_Adventurer_512x1024", "Trail Adventurer", "adventurer"),
    ("litiso_creator_ranger", "LitIsoCreator_Ranger_512x1024", "Greenwake Ranger", "ranger"),
    ("litiso_creator_mystic", "LitIsoCreator_Mystic_512x1024", "Moonwell Mystic", "mystic"),
    ("litiso_creator_smith", "LitIsoCreator_Smith_512x1024", "Hearth Smith", "smith"),
]


def shade(color: tuple[int, int, int, int], amount: int) -> tuple[int, int, int, int]:
    return (
        max(0, min(255, color[0] + amount)),
        max(0, min(255, color[1] + amount)),
        max(0, min(255, color[2] + amount)),
        color[3],
    )


def draw_poly(draw: ImageDraw.ImageDraw, points, fill, outline=None) -> None:
    draw.polygon([(int(x), int(y)) for x, y in points], fill=fill, outline=outline)


def direction_profile(row: int) -> tuple[int, int, bool, float]:
    # x compression makes side/back views read differently while keeping the same
    # bottom-center sprite footprint.
    if row in (2, 6):
        return (8 if row == 2 else -8, -1, row == 6, 0.72)
    if row in (1, 3, 5, 7):
        return (5 if row in (1, 3) else -5, -1, row in (5, 7), 0.86)
    if row == 4:
        return (0, -2, False, 0.94)
    return (0, 0, False, 1.0)


def draw_character(draw: ImageDraw.ImageDraw, palette: dict, row: int, frame: int) -> None:
    x_shift, y_shift, mirror, width_scale = direction_profile(row)
    foot_phase = [-3, -1, 3, 1][frame]
    bob = [0, -2, 0, 1][frame]
    cx = CELL // 2 + x_shift
    base = 104 + bob + y_shift
    outline = (22, 22, 26, 255)

    body_w = int(22 * width_scale)
    shoulder_w = int(30 * width_scale)
    head_w = int(19 * width_scale)
    arm_swing = [-4, 2, 4, -2][frame]
    if mirror:
        arm_swing = -arm_swing
        foot_phase = -foot_phase

    # Contact shadow.
    draw.ellipse((cx - 18, base - 5, cx + 18, base + 2), fill=(0, 0, 0, 58))

    # Legs and boots.
    draw.rectangle((cx - 11 + foot_phase, base - 28, cx - 3 + foot_phase, base - 8), fill=outline)
    draw.rectangle((cx + 3 - foot_phase, base - 28, cx + 11 - foot_phase, base - 8), fill=outline)
    draw.rectangle((cx - 10 + foot_phase, base - 28, cx - 4 + foot_phase, base - 10), fill=palette["pants"])
    draw.rectangle((cx + 4 - foot_phase, base - 28, cx + 10 - foot_phase, base - 10), fill=shade(palette["pants"], -10))
    draw.rectangle((cx - 13 + foot_phase, base - 10, cx - 2 + foot_phase, base - 4), fill=palette["boots"])
    draw.rectangle((cx + 2 - foot_phase, base - 10, cx + 13 - foot_phase, base - 4), fill=palette["boots"])

    # Torso and belt.
    draw_poly(draw, [
        (cx - shoulder_w // 2, base - 68),
        (cx + shoulder_w // 2, base - 68),
        (cx + body_w // 2, base - 30),
        (cx - body_w // 2, base - 30),
    ], palette["shirt"], outline)
    draw.rectangle((cx - body_w // 2 - 1, base - 43, cx + body_w // 2 + 1, base - 38), fill=palette["trim"])

    # Arms.
    draw.line((cx - shoulder_w // 2, base - 62, cx - 20 - arm_swing, base - 36), fill=outline, width=6)
    draw.line((cx + shoulder_w // 2, base - 62, cx + 20 + arm_swing, base - 36), fill=outline, width=6)
    draw.line((cx - shoulder_w // 2, base - 62, cx - 20 - arm_swing, base - 36), fill=shade(palette["shirt"], -8), width=4)
    draw.line((cx + shoulder_w // 2, base - 62, cx + 20 + arm_swing, base - 36), fill=shade(palette["shirt"], -12), width=4)
    draw.rectangle((cx - 23 - arm_swing, base - 37, cx - 18 - arm_swing, base - 31), fill=palette["skin"])
    draw.rectangle((cx + 18 + arm_swing, base - 37, cx + 23 + arm_swing, base - 31), fill=palette["skin"])

    # Neck/head.
    draw.rectangle((cx - 5, base - 75, cx + 5, base - 66), fill=palette["skin"])
    draw.ellipse((cx - head_w // 2 - 1, base - 93, cx + head_w // 2 + 1, base - 72), fill=outline)
    draw.ellipse((cx - head_w // 2, base - 92, cx + head_w // 2, base - 73), fill=palette["skin"])

    # Hair/front/back cues.
    if row == 4:
        draw.pieslice((cx - head_w // 2 - 3, base - 96, cx + head_w // 2 + 3, base - 70), 170, 370, fill=palette["hair"])
    else:
        draw.pieslice((cx - head_w // 2 - 3, base - 98, cx + head_w // 2 + 3, base - 76), 180, 360, fill=palette["hair"])
        if row not in (3, 4, 5):
            eye_y = base - 83
            draw.point((cx - 4, eye_y), fill=(18, 23, 28, 255))
            draw.point((cx + 4, eye_y), fill=(18, 23, 28, 255))

    # Character-specific accessory silhouette.
    if palette is PALETTES["ranger"]:
        draw_poly(draw, [(cx - 18, base - 94), (cx + 18, base - 94), (cx, base - 105)], palette["accent"], outline)
    elif palette is PALETTES["mystic"]:
        draw.ellipse((cx - 5, base - 105, cx + 5, base - 96), fill=palette["accent"], outline=outline)
        draw.line((cx, base - 96, cx, base - 88), fill=palette["accent"], width=2)
    elif palette is PALETTES["smith"]:
        draw.rectangle((cx - 17, base - 95, cx + 17, base - 90), fill=palette["accent"])
        draw.rectangle((cx - 13, base - 101, cx + 13, base - 94), fill=shade(palette["accent"], -30), outline=outline)
    else:
        draw.rectangle((cx - 11, base - 97, cx + 11, base - 92), fill=palette["accent"])


def make_sheet(palette_name: str) -> Image.Image:
    sheet = Image.new("RGBA", (SHEET_W, SHEET_H), (0, 0, 0, 0))
    palette = PALETTES[palette_name]
    for row in range(ROWS):
        for frame in range(FRAMES):
            cell = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
            draw = ImageDraw.Draw(cell)
            draw_character(draw, palette, row, frame)
            sheet.alpha_composite(cell, (frame * CELL, row * CELL))
    return sheet


def guid_for(name: str) -> str:
    return hashlib.md5(f"litiso-character-{name}".encode("utf-8")).hexdigest()


def write_meta(sheet_name: str) -> None:
    template = META_TEMPLATE.read_text(encoding="utf-8")
    meta = template.replace("guid: 0ac9f6f13553422ab553e1bac9afb96d", f"guid: {guid_for(sheet_name)}")
    meta = meta.replace(TEMPLATE_NAME, sheet_name)
    # Keep sprite IDs/internal IDs from the proven slicing template; they are local
    # to the imported asset, while the generated GUID above is unique.
    (OUT_DIR / f"{sheet_name}.png.meta").write_text(meta, encoding="utf-8")


def make_contact(generated: list[dict]) -> None:
    REVIEW_DIR.mkdir(parents=True, exist_ok=True)
    row_h = 150
    w = 900
    h = row_h * len(generated)
    contact = Image.new("RGBA", (w, h), (20, 23, 28, 255))
    draw = ImageDraw.Draw(contact)
    for idx, item in enumerate(generated):
        y = idx * row_h
        sheet = Image.open(item["path"]).convert("RGBA")
        preview = sheet.crop((0, 0, 512, 128)).resize((512, 128), Image.Resampling.NEAREST)
        draw.rectangle((0, y, w - 1, y + row_h - 1), outline=(58, 67, 82, 255))
        draw.text((16, y + 20), item["displayName"], fill=(240, 199, 109, 255))
        draw.text((16, y + 42), item["id"], fill=(180, 188, 200, 255))
        draw.text((16, y + 64), "original generated LIT-ISO sheet", fill=(160, 210, 180, 255))
        contact.alpha_composite(preview, (330, y + 10))
    contact.save(REVIEW_DIR / "original_character_suite_contact_sheet.png")


def main() -> int:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    generated = []
    for appearance_id, sheet_name, display_name, palette_name in CHARACTERS:
        sheet = make_sheet(palette_name)
        path = OUT_DIR / f"{sheet_name}.png"
        sheet.save(path)
        write_meta(sheet_name)
        generated.append({
            "id": appearance_id,
            "displayName": display_name,
            "spriteResource": f"Characters/Player/Generated/{sheet_name}",
            "path": str(path),
            "directionCount": ROWS,
            "framesPerRow": FRAMES,
            "cellSize": [CELL, CELL],
            "source": "deterministic_original_litiso_generator_v1",
        })

    REVIEW_DIR.mkdir(parents=True, exist_ok=True)
    (REVIEW_DIR / "original_character_suite_manifest.json").write_text(
        json.dumps({"version": 1, "appearances": generated}, indent=2),
        encoding="utf-8",
    )
    make_contact(generated)
    print(f"Generated {len(generated)} original sheets in {OUT_DIR}")
    print(f"Review sheet: {REVIEW_DIR / 'original_character_suite_contact_sheet.png'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
