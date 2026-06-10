#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")

CANONICAL_DIRECTIONS = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"]
DEFAULT_DIRECTIONS = ["S", "E", "N", "W"]
DIRECTION_WORD = {
    "S": "south",
    "SE": "south-east",
    "E": "east",
    "NE": "north-east",
    "N": "north",
    "NW": "north-west",
    "W": "west",
    "SW": "south-west",
}


def now_utc() -> str:
    return datetime.now(timezone.utc).isoformat()


def zeros(count: int) -> list[int]:
    return [0 for _ in range(count * 3)]


def pose_points(direction: str, action: str) -> list[tuple[int, int, int]]:
    # OpenPose body-18 order: nose, neck, r/l shoulders, elbows, wrists,
    # r/l hips, knees, ankles, eyes, ears. These are deliberately simple,
    # squat, game-sprite proportions rather than realistic anatomy.
    cx = 256
    head_y = 150
    neck_y = 200
    hip_y = 312
    ankle_y = 424
    lean = {"S": 0, "SE": 12, "E": 18, "NE": 10, "N": 0, "NW": -10, "W": -18, "SW": -12}[direction]
    shoulder_span = {"S": 92, "SE": 76, "E": 52, "NE": 66, "N": 82, "NW": 66, "W": 52, "SW": 76}[direction]
    hip_span = {"S": 62, "SE": 52, "E": 38, "NE": 46, "N": 56, "NW": 46, "W": 38, "SW": 52}[direction]
    arm_drop = 88 if action.lower() == "idle" else 96
    stride = 0
    if action.lower() in {"walk", "run"}:
        stride = 34

    nose = (cx + lean, head_y, 1)
    neck = (cx, neck_y, 1)
    r_shoulder = (cx - shoulder_span // 2, neck_y + 8, 1)
    l_shoulder = (cx + shoulder_span // 2, neck_y + 8, 1)
    r_elbow = (r_shoulder[0] - (12 if direction in {"S", "N"} else 4), r_shoulder[1] + arm_drop // 2, 1)
    l_elbow = (l_shoulder[0] + (12 if direction in {"S", "N"} else 4), l_shoulder[1] + arm_drop // 2, 1)
    r_wrist = (r_elbow[0] - (8 if direction in {"S", "N"} else 2), r_elbow[1] + arm_drop // 2, 1)
    l_wrist = (l_elbow[0] + (8 if direction in {"S", "N"} else 2), l_elbow[1] + arm_drop // 2, 1)

    r_hip = (cx - hip_span // 2, hip_y, 1)
    l_hip = (cx + hip_span // 2, hip_y, 1)
    r_knee = (r_hip[0] - stride // 2, (hip_y + ankle_y) // 2, 1)
    l_knee = (l_hip[0] + stride // 2, (hip_y + ankle_y) // 2, 1)
    r_ankle = (r_knee[0] - stride // 3, ankle_y, 1)
    l_ankle = (l_knee[0] + stride // 3, ankle_y, 1)

    if direction == "E":
        r_shoulder, l_shoulder = (cx - 14, neck_y + 10, 1), (cx + 38, neck_y + 4, 1)
        r_hip, l_hip = (cx - 12, hip_y, 1), (cx + 24, hip_y - 4, 1)
        r_elbow, r_wrist = (cx - 18, 278, 1), (cx - 18, 350, 1)
        l_elbow, l_wrist = (cx + 56, 278, 1), (cx + 64, 350, 1)
        r_knee, r_ankle = (cx - 22 - stride // 2, 368, 1), (cx - 30 - stride, ankle_y, 1)
        l_knee, l_ankle = (cx + 30 + stride // 2, 362, 1), (cx + 42 + stride, ankle_y, 1)
    elif direction == "SE":
        r_shoulder, l_shoulder = (cx - 32, neck_y + 10, 1), (cx + 44, neck_y + 6, 1)
        r_hip, l_hip = (cx - 20, hip_y + 2, 1), (cx + 32, hip_y - 2, 1)
        r_elbow, r_wrist = (cx - 42, 280, 1), (cx - 46, 352, 1)
        l_elbow, l_wrist = (cx + 58, 278, 1), (cx + 66, 350, 1)
        r_knee, r_ankle = (cx - 28 - stride // 2, 368, 1), (cx - 34 - stride, ankle_y, 1)
        l_knee, l_ankle = (cx + 36 + stride // 2, 364, 1), (cx + 48 + stride, ankle_y, 1)
    elif direction == "NE":
        r_shoulder, l_shoulder = (cx - 26, neck_y + 4, 1), (cx + 40, neck_y + 10, 1)
        r_hip, l_hip = (cx - 18, hip_y - 2, 1), (cx + 28, hip_y + 2, 1)
        r_elbow, r_wrist = (cx - 38, 276, 1), (cx - 44, 346, 1)
        l_elbow, l_wrist = (cx + 52, 284, 1), (cx + 58, 354, 1)
        r_knee, r_ankle = (cx - 24 - stride // 2, 362, 1), (cx - 30 - stride, ankle_y, 1)
        l_knee, l_ankle = (cx + 34 + stride // 2, 370, 1), (cx + 44 + stride, ankle_y, 1)
    elif direction == "W":
        r_shoulder, l_shoulder = (cx - 38, neck_y + 4, 1), (cx + 14, neck_y + 10, 1)
        r_hip, l_hip = (cx - 24, hip_y - 4, 1), (cx + 12, hip_y, 1)
        r_elbow, r_wrist = (cx - 56, 278, 1), (cx - 64, 350, 1)
        l_elbow, l_wrist = (cx + 18, 278, 1), (cx + 18, 350, 1)
        r_knee, r_ankle = (cx - 30 - stride // 2, 362, 1), (cx - 42 - stride, ankle_y, 1)
        l_knee, l_ankle = (cx + 22 + stride // 2, 368, 1), (cx + 30 + stride, ankle_y, 1)
    elif direction == "SW":
        r_shoulder, l_shoulder = (cx - 44, neck_y + 6, 1), (cx + 32, neck_y + 10, 1)
        r_hip, l_hip = (cx - 32, hip_y - 2, 1), (cx + 20, hip_y + 2, 1)
        r_elbow, r_wrist = (cx - 58, 278, 1), (cx - 66, 350, 1)
        l_elbow, l_wrist = (cx + 42, 280, 1), (cx + 46, 352, 1)
        r_knee, r_ankle = (cx - 36 - stride // 2, 364, 1), (cx - 48 - stride, ankle_y, 1)
        l_knee, l_ankle = (cx + 28 + stride // 2, 368, 1), (cx + 34 + stride, ankle_y, 1)
    elif direction == "NW":
        r_shoulder, l_shoulder = (cx - 40, neck_y + 10, 1), (cx + 26, neck_y + 4, 1)
        r_hip, l_hip = (cx - 28, hip_y + 2, 1), (cx + 18, hip_y - 2, 1)
        r_elbow, r_wrist = (cx - 52, 284, 1), (cx - 58, 354, 1)
        l_elbow, l_wrist = (cx + 38, 276, 1), (cx + 44, 346, 1)
        r_knee, r_ankle = (cx - 34 - stride // 2, 370, 1), (cx - 44 - stride, ankle_y, 1)
        l_knee, l_ankle = (cx + 24 + stride // 2, 362, 1), (cx + 30 + stride, ankle_y, 1)

    eyes_y = head_y - 14
    right_eye = (cx + lean - 12, eyes_y, 1)
    left_eye = (cx + lean + 12, eyes_y, 1)
    right_ear = (cx + lean - 24, head_y, 1)
    left_ear = (cx + lean + 24, head_y, 1)
    if direction in {"N", "NE", "NW"}:
        # Keep head landmarks compact so ControlNet reads "back of head".
        right_eye = left_eye = right_ear = left_ear = (0, 0, 0)

    return [
        nose,
        neck,
        r_shoulder,
        r_elbow,
        r_wrist,
        l_shoulder,
        l_elbow,
        l_wrist,
        r_hip,
        r_knee,
        r_ankle,
        l_hip,
        l_knee,
        l_ankle,
        right_eye,
        left_eye,
        right_ear,
        left_ear,
    ]


def pose_json(direction: str, action: str) -> dict:
    points = [value for point in pose_points(direction, action) for value in point]
    return {
        "version": 1.3,
        "canvas_width": 512,
        "canvas_height": 512,
        "people": [
            {
                "pose_keypoints_2d": points,
                "face_keypoints_2d": zeros(70),
                "hand_left_keypoints_2d": zeros(21),
                "hand_right_keypoints_2d": zeros(21),
            }
        ],
    }


BODY_EDGES = [
    (0, 1),
    (1, 2),
    (2, 3),
    (3, 4),
    (1, 5),
    (5, 6),
    (6, 7),
    (1, 8),
    (8, 9),
    (9, 10),
    (1, 11),
    (11, 12),
    (12, 13),
    (0, 14),
    (14, 16),
    (0, 15),
    (15, 17),
]


def draw_preview(path: Path, direction: str, action: str) -> None:
    image = Image.new("RGBA", (512, 512), (16, 18, 24, 255))
    draw = ImageDraw.Draw(image)
    points = pose_points(direction, action)
    for a, b in BODY_EDGES:
        if points[a][2] and points[b][2]:
            draw.line((points[a][0], points[a][1], points[b][0], points[b][1]), fill=(88, 188, 255, 255), width=8)
    for x, y, c in points:
        if c:
            draw.ellipse((x - 8, y - 8, x + 8, y + 8), fill=(255, 192, 64, 255))
    try:
        font = ImageFont.truetype("arial.ttf", 22)
    except OSError:
        font = ImageFont.load_default()
    draw.text((18, 18), f"{action} {direction} / {DIRECTION_WORD[direction]}", fill=(238, 244, 255, 255), font=font)
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path)


def make_contact_sheet(entries: list[dict], output: Path) -> None:
    cell_w, cell_h = 220, 252
    sheet = Image.new("RGBA", (cell_w * len(entries), cell_h), (28, 31, 38, 255))
    draw = ImageDraw.Draw(sheet)
    for index, entry in enumerate(entries):
        with Image.open(entry["preview_path"]) as source:
            thumb = source.convert("RGBA")
            thumb.thumbnail((cell_w - 20, cell_h - 54), Image.Resampling.NEAREST)
        x = index * cell_w
        sheet.alpha_composite(thumb, (x + (cell_w - thumb.width) // 2, 8))
        draw.rectangle((x, 0, x + cell_w - 1, cell_h - 1), outline=(84, 92, 112, 255), width=1)
        draw.text((x + 10, cell_h - 38), f"{entry['direction']} {entry['action']}", fill=(235, 240, 248, 255))
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", type=Path, default=Path.cwd())
    parser.add_argument("--out-root", type=Path, default=Path(r"Assets\Generated\_Review\_PoseControls\litiso_openpose_v1"))
    parser.add_argument("--action", default="Idle")
    parser.add_argument("--directions", nargs="+", default=DEFAULT_DIRECTIONS)
    parser.add_argument("--replace", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    out_root = args.out_root if args.out_root.is_absolute() else project_root / args.out_root
    if out_root.exists() and args.replace:
        for child in out_root.iterdir():
            if child.is_file():
                child.unlink()
    out_root.mkdir(parents=True, exist_ok=True)

    entries = []
    for direction in args.directions:
        if direction not in CANONICAL_DIRECTIONS:
            raise ValueError(f"Unsupported direction for v1 pose library: {direction}")
        data = pose_json(direction, args.action)
        json_path = out_root / f"{args.action.lower()}_{direction.lower()}_openpose.json"
        preview_path = out_root / f"{args.action.lower()}_{direction.lower()}_preview.png"
        json_path.write_text(json.dumps(data, separators=(",", ":")), encoding="utf-8")
        draw_preview(preview_path, direction, args.action)
        entries.append(
            {
                "action": args.action,
                "direction": direction,
                "direction_word": DIRECTION_WORD[direction],
                "pose_json": str(json_path),
                "preview_path": str(preview_path),
                "source": "handauthored_litiso_openpose_v1",
                "license": "project-internal",
                "notes": "Minimal body-only OpenPose control for isometric direction smoke tests. Diagonals are starter controls and should be visually reviewed before training.",
            }
        )

    contact = out_root / f"{args.action.lower()}_{len(entries)}d_contact.png"
    make_contact_sheet(entries, contact)
    manifest = {
        "schema": "lit_iso.asset_forge.openpose_direction_library.v1",
        "created_utc": now_utc(),
        "action": args.action,
        "directions": args.directions,
        "entries": entries,
        "contact_sheet": str(contact),
    }
    manifest_path = out_root / f"{args.action.lower()}_manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps({"ok": True, "manifest": str(manifest_path), "contact_sheet": str(contact), "entries": len(entries)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
