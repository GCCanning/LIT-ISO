#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
import math
import shutil
import sys
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Iterable

from PIL import Image, ImageDraw, ImageFont

if sys.platform == "win32":
    sys.stdout.reconfigure(encoding="utf-8")


DIRECTIONS = ["S", "SE", "E", "NE", "N", "NW", "W", "SW"]
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

EDGE_COLORS = [
    (255, 85, 85, 255),
    (255, 170, 70, 255),
    (255, 220, 80, 255),
    (200, 255, 95, 255),
    (80, 220, 150, 255),
    (65, 190, 255, 255),
    (105, 125, 255, 255),
    (190, 105, 255, 255),
    (255, 105, 190, 255),
]


@dataclass(frozen=True)
class Point:
    x: int
    y: int
    c: int = 1

    def shifted(self, dx: float = 0, dy: float = 0) -> "Point":
        if not self.c:
            return self
        return Point(round(self.x + dx), round(self.y + dy), self.c)


@dataclass(frozen=True)
class Phase:
    name: str
    bob: int = 0
    torso_sway: int = 0
    head_sway: int = 0
    near_leg: int = 0
    far_leg: int = 0
    near_arm: int = 0
    far_arm: int = 0
    lift_near: int = 0
    lift_far: int = 0


ACTION_PHASES = {
    "idle": [
        Phase("anchor"),
        Phase("breath_up", bob=-3, torso_sway=-1, head_sway=-1, near_arm=2, far_arm=-1),
        Phase("settle", bob=0, torso_sway=0, head_sway=0),
        Phase("breath_down", bob=2, torso_sway=1, head_sway=1, near_arm=-2, far_arm=1),
    ],
    "walk": [
        Phase("anchor"),
        Phase("near_forward", bob=-2, torso_sway=2, head_sway=1, near_leg=34, far_leg=-22, near_arm=-12, far_arm=10, lift_near=8),
        Phase("near_contact", bob=1, torso_sway=1, head_sway=0, near_leg=18, far_leg=-12, near_arm=-6, far_arm=5),
        Phase("far_forward", bob=-2, torso_sway=-2, head_sway=-1, near_leg=-22, far_leg=34, near_arm=10, far_arm=-12, lift_far=8),
        Phase("far_contact", bob=1, torso_sway=-1, head_sway=0, near_leg=-12, far_leg=18, near_arm=5, far_arm=-6),
        Phase("recover", bob=0, torso_sway=0, head_sway=0, near_leg=8, far_leg=-8, near_arm=-2, far_arm=2),
    ],
}

ACTION_DEFAULTS = {
    "idle": {"frames": 4, "fps": 4, "loop": True, "loop_start": 0, "loop_end": 3, "weapon_hand": "none"},
    "walk": {"frames": 6, "fps": 10, "loop": True, "loop_start": 1, "loop_end": 5, "weapon_hand": "none"},
}


def now_utc() -> str:
    return datetime.now(timezone.utc).isoformat()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            digest.update(chunk)
    return digest.hexdigest()


def zeros(count: int) -> list[int]:
    return [0 for _ in range(count * 3)]


def facing_sign(direction: str) -> int:
    if direction in {"E", "SE", "NE"}:
        return 1
    if direction in {"W", "SW", "NW"}:
        return -1
    return 0


def depth_sign(direction: str) -> int:
    if direction in {"S", "SE", "SW"}:
        return 1
    if direction in {"N", "NE", "NW"}:
        return -1
    return 0


def base_pose(direction: str) -> list[Point]:
    cx = 256
    head_y = 150
    neck_y = 200
    hip_y = 312
    ankle_y = 424
    lean = {"S": 0, "SE": 12, "E": 18, "NE": 10, "N": 0, "NW": -10, "W": -18, "SW": -12}[direction]
    shoulder_span = {"S": 92, "SE": 76, "E": 52, "NE": 66, "N": 82, "NW": 66, "W": 52, "SW": 76}[direction]
    hip_span = {"S": 62, "SE": 52, "E": 38, "NE": 46, "N": 56, "NW": 46, "W": 38, "SW": 52}[direction]

    nose = Point(cx + lean, head_y)
    neck = Point(cx, neck_y)
    r_shoulder = Point(cx - shoulder_span // 2, neck_y + 8)
    l_shoulder = Point(cx + shoulder_span // 2, neck_y + 8)
    r_elbow = Point(r_shoulder.x - (12 if direction in {"S", "N"} else 4), r_shoulder.y + 44)
    l_elbow = Point(l_shoulder.x + (12 if direction in {"S", "N"} else 4), l_shoulder.y + 44)
    r_wrist = Point(r_elbow.x - (8 if direction in {"S", "N"} else 2), r_elbow.y + 44)
    l_wrist = Point(l_elbow.x + (8 if direction in {"S", "N"} else 2), l_elbow.y + 44)
    r_hip = Point(cx - hip_span // 2, hip_y)
    l_hip = Point(cx + hip_span // 2, hip_y)
    r_knee = Point(r_hip.x, 368)
    l_knee = Point(l_hip.x, 368)
    r_ankle = Point(r_hip.x - 2, ankle_y)
    l_ankle = Point(l_hip.x + 2, ankle_y)

    if direction == "E":
        r_shoulder, l_shoulder = Point(cx - 14, neck_y + 10), Point(cx + 38, neck_y + 4)
        r_hip, l_hip = Point(cx - 12, hip_y), Point(cx + 24, hip_y - 4)
        r_elbow, r_wrist = Point(cx - 18, 278), Point(cx - 18, 350)
        l_elbow, l_wrist = Point(cx + 56, 278), Point(cx + 64, 350)
        r_knee, r_ankle = Point(cx - 22, 368), Point(cx - 30, ankle_y)
        l_knee, l_ankle = Point(cx + 30, 362), Point(cx + 42, ankle_y)
    elif direction == "SE":
        r_shoulder, l_shoulder = Point(cx - 32, neck_y + 10), Point(cx + 44, neck_y + 6)
        r_hip, l_hip = Point(cx - 20, hip_y + 2), Point(cx + 32, hip_y - 2)
        r_elbow, r_wrist = Point(cx - 42, 280), Point(cx - 46, 352)
        l_elbow, l_wrist = Point(cx + 58, 278), Point(cx + 66, 350)
        r_knee, r_ankle = Point(cx - 28, 368), Point(cx - 34, ankle_y)
        l_knee, l_ankle = Point(cx + 36, 364), Point(cx + 48, ankle_y)
    elif direction == "NE":
        r_shoulder, l_shoulder = Point(cx - 26, neck_y + 4), Point(cx + 40, neck_y + 10)
        r_hip, l_hip = Point(cx - 18, hip_y - 2), Point(cx + 28, hip_y + 2)
        r_elbow, r_wrist = Point(cx - 38, 276), Point(cx - 44, 346)
        l_elbow, l_wrist = Point(cx + 52, 284), Point(cx + 58, 354)
        r_knee, r_ankle = Point(cx - 24, 362), Point(cx - 30, ankle_y)
        l_knee, l_ankle = Point(cx + 34, 370), Point(cx + 44, ankle_y)
    elif direction == "W":
        r_shoulder, l_shoulder = Point(cx - 38, neck_y + 4), Point(cx + 14, neck_y + 10)
        r_hip, l_hip = Point(cx - 24, hip_y - 4), Point(cx + 12, hip_y)
        r_elbow, r_wrist = Point(cx - 56, 278), Point(cx - 64, 350)
        l_elbow, l_wrist = Point(cx + 18, 278), Point(cx + 18, 350)
        r_knee, r_ankle = Point(cx - 30, 362), Point(cx - 42, ankle_y)
        l_knee, l_ankle = Point(cx + 22, 368), Point(cx + 30, ankle_y)
    elif direction == "SW":
        r_shoulder, l_shoulder = Point(cx - 44, neck_y + 6), Point(cx + 32, neck_y + 10)
        r_hip, l_hip = Point(cx - 32, hip_y - 2), Point(cx + 20, hip_y + 2)
        r_elbow, r_wrist = Point(cx - 58, 278), Point(cx - 66, 350)
        l_elbow, l_wrist = Point(cx + 42, 280), Point(cx + 46, 352)
        r_knee, r_ankle = Point(cx - 36, 364), Point(cx - 48, ankle_y)
        l_knee, l_ankle = Point(cx + 28, 368), Point(cx + 34, ankle_y)
    elif direction == "NW":
        r_shoulder, l_shoulder = Point(cx - 40, neck_y + 10), Point(cx + 26, neck_y + 4)
        r_hip, l_hip = Point(cx - 28, hip_y + 2), Point(cx + 18, hip_y - 2)
        r_elbow, r_wrist = Point(cx - 52, 284), Point(cx - 58, 354)
        l_elbow, l_wrist = Point(cx + 38, 276), Point(cx + 44, 346)
        r_knee, r_ankle = Point(cx - 34, 370), Point(cx - 44, ankle_y)
        l_knee, l_ankle = Point(cx + 24, 362), Point(cx + 30, ankle_y)

    eyes_y = head_y - 14
    right_eye = Point(cx + lean - 12, eyes_y)
    left_eye = Point(cx + lean + 12, eyes_y)
    right_ear = Point(cx + lean - 24, head_y)
    left_ear = Point(cx + lean + 24, head_y)
    if direction in {"N", "NE", "NW"}:
        right_eye = left_eye = right_ear = left_ear = Point(0, 0, 0)

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


def apply_phase(points: list[Point], direction: str, phase: Phase) -> list[Point]:
    if phase.name == "anchor":
        return points

    sign = facing_sign(direction)
    depth = depth_sign(direction)
    x_axis = sign if sign else (1 if direction == "S" else -1 if direction == "N" else 0)
    y_axis = 0.35 * depth
    out = list(points)

    torso = {0, 1, 2, 5, 8, 11, 14, 15, 16, 17}
    for index in torso:
        out[index] = out[index].shifted(phase.torso_sway * 0.8, phase.bob)
    out[0] = out[0].shifted(phase.head_sway, phase.bob)

    near_side = "right" if direction in {"S", "SE", "E", "NE"} else "left"
    near_leg_indices = [8, 9, 10] if near_side == "right" else [11, 12, 13]
    far_leg_indices = [11, 12, 13] if near_side == "right" else [8, 9, 10]
    near_arm_indices = [2, 3, 4] if near_side == "right" else [5, 6, 7]
    far_arm_indices = [5, 6, 7] if near_side == "right" else [2, 3, 4]

    def move_chain(indices: Iterable[int], amount: int, lift: int, scale: float = 1.0) -> None:
        for order, index in enumerate(indices):
            if order == 0:
                continue
            factor = order / 2.0
            out[index] = out[index].shifted(amount * x_axis * factor * scale, amount * y_axis * factor * scale - lift * factor)

    move_chain(near_leg_indices, phase.near_leg, phase.lift_near)
    move_chain(far_leg_indices, phase.far_leg, phase.lift_far)
    move_chain(near_arm_indices, phase.near_arm, 0, 0.65)
    move_chain(far_arm_indices, phase.far_arm, 0, 0.65)

    return out


def pose_points(direction: str, action: str, frame_index: int) -> list[Point]:
    phases = ACTION_PHASES[action]
    phase = phases[frame_index]
    return apply_phase(base_pose(direction), direction, phase)


def pose_json(points: list[Point]) -> dict:
    flattened = []
    for point in points:
        flattened.extend([point.x, point.y, point.c])
    return {
        "version": 1.3,
        "canvas_width": 512,
        "canvas_height": 512,
        "people": [
            {
                "pose_keypoints_2d": flattened,
                "face_keypoints_2d": zeros(70),
                "hand_left_keypoints_2d": zeros(21),
                "hand_right_keypoints_2d": zeros(21),
            }
        ],
    }


def render_pose(path: Path, points: list[Point]) -> None:
    image = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    for edge_index, (a, b) in enumerate(BODY_EDGES):
        if points[a].c and points[b].c:
            color = EDGE_COLORS[edge_index % len(EDGE_COLORS)]
            draw.line((points[a].x, points[a].y, points[b].x, points[b].y), fill=color, width=8)
    for point in points:
        if point.c:
            draw.ellipse((point.x - 8, point.y - 8, point.x + 8, point.y + 8), fill=(255, 238, 130, 255), outline=(34, 28, 12, 255), width=2)
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path)


def make_contact_sheet(action_root: Path, action: str, frame_count: int, output: Path) -> None:
    thumb_w, thumb_h = 128, 128
    label_h = 28
    sheet = Image.new("RGBA", (thumb_w * frame_count, (thumb_h + label_h) * len(DIRECTIONS)), (19, 22, 29, 255))
    draw = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("arial.ttf", 13)
    except OSError:
        font = ImageFont.load_default()
    for row, direction in enumerate(DIRECTIONS):
        for frame_index in range(frame_count):
            frame_path = action_root / direction / f"frame_{frame_index:03d}.png"
            with Image.open(frame_path) as source:
                thumb = source.convert("RGBA")
                thumb.thumbnail((thumb_w - 8, thumb_h - 8), Image.Resampling.NEAREST)
            x = frame_index * thumb_w
            y = row * (thumb_h + label_h)
            draw.rectangle((x, y, x + thumb_w - 1, y + thumb_h + label_h - 1), outline=(63, 70, 88, 255), width=1)
            sheet.alpha_composite(thumb, (x + (thumb_w - thumb.width) // 2, y + (thumb_h - thumb.height) // 2))
            draw.text((x + 6, y + thumb_h + 6), f"{direction} f{frame_index}", fill=(236, 241, 248, 255), font=font)
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output)


def write_action_json(action_root: Path, action: str, frame_records: list[dict], version: str) -> None:
    defaults = ACTION_DEFAULTS[action]
    action_json = {
        "schema": "lit-iso.spriteforge.action.v1",
        "action": action,
        "frames": defaults["frames"],
        "fps": defaults["fps"],
        "loop": defaults["loop"],
        "loop_start": defaults["loop_start"],
        "loop_end": defaults["loop_end"],
        "loop_range": [defaults["loop_start"], defaults["loop_end"]],
        "loop_note": "Playback should loop loop_range; frame 0 remains available as the generation/idle anchor.",
        "anchor_frame": 0,
        "anchor_note": "frame 0 is byte-identical to the shared idle anchor pose for each direction.",
        "weapon_hand": defaults["weapon_hand"],
        "engine_default": "frames",
        "engine_by_target_size": {">=96": "video"},
        "directions": DIRECTIONS,
        "frame_size": [512, 512],
        "background": "transparent",
        "pose_library_version": version,
        "source": {
            "method": "deterministic_body18_openpose_skeletons",
            "derived_from": "Tools/AssetForge/build_litiso_openpose_direction_library.py geometry conventions",
            "license": "project-internal skeleton controls; no oracle art pixels included",
        },
        "mirrorable": {"W": "E", "NW": "NE", "SW": "SE"},
        "mirror_note": "W-side directions may be generated by mirroring E-side results only for symmetric character designs.",
        "frames_meta": frame_records,
    }
    action_root.joinpath("action.json").write_text(json.dumps(action_json, indent=2), encoding="utf-8")


def build_library(poses_root: Path, version: str, replace: bool) -> dict:
    if replace:
        for action in ACTION_PHASES:
            action_root = poses_root / action
            if action_root.exists():
                shutil.rmtree(action_root)

    poses_root.mkdir(parents=True, exist_ok=True)
    pose_readme = poses_root / "README.md"
    if not pose_readme.exists():
        pose_readme.write_text("# Action Pose Library\n", encoding="utf-8")

    manifest_actions = []
    idle_anchor_hashes: dict[str, str] = {}
    for action, phases in ACTION_PHASES.items():
        action_root = poses_root / action
        action_root.mkdir(parents=True, exist_ok=True)
        frame_records: list[dict] = []
        for direction in DIRECTIONS:
            direction_root = action_root / direction
            direction_root.mkdir(parents=True, exist_ok=True)
            for frame_index, phase in enumerate(phases):
                points = pose_points(direction, action, frame_index)
                png_path = direction_root / f"frame_{frame_index:03d}.png"
                json_path = direction_root / f"frame_{frame_index:03d}.openpose.json"
                render_pose(png_path, points)
                json_path.write_text(json.dumps(pose_json(points), separators=(",", ":")), encoding="utf-8")
                digest = sha256_file(png_path)
                if action == "idle" and frame_index == 0:
                    idle_anchor_hashes[direction] = digest
                frame_records.append(
                    {
                        "direction": direction,
                        "direction_word": DIRECTION_WORD[direction],
                        "frame_index": frame_index,
                        "phase": phase.name,
                        "pose_png": str(png_path.relative_to(poses_root).as_posix()),
                        "openpose_json": str(json_path.relative_to(poses_root).as_posix()),
                        "sha256": digest,
                        "shared_idle_anchor": frame_index == 0,
                    }
                )
        write_action_json(action_root, action, frame_records, version)
        contact_sheet = action_root / f"{action}_pose_contact_sheet.png"
        make_contact_sheet(action_root, action, len(phases), contact_sheet)
        manifest_actions.append(
            {
                "action": action,
                "frames": len(phases),
                "fps": ACTION_DEFAULTS[action]["fps"],
                "loop": ACTION_DEFAULTS[action]["loop"],
                "contact_sheet": str(contact_sheet.relative_to(poses_root).as_posix()),
            }
        )

    version_path = poses_root / "VERSION"
    version_path.write_text(version + "\n", encoding="utf-8")
    manifest = {
        "schema": "lit-iso.spriteforge.pose-library.v1",
        "generated_utc": now_utc(),
        "version": version,
        "directions": DIRECTIONS,
        "actions": manifest_actions,
        "requirements": {
            "canvas": [512, 512],
            "background": "transparent",
            "frame_0_contract": "Every action frame_000 matches idle frame_000 for the same direction.",
        },
        "source": {
            "method": "deterministic skeleton controls",
            "license": "project-internal skeleton controls; no source/oracle art pixels included",
        },
        "idle_anchor_hashes": idle_anchor_hashes,
    }
    manifest_path = poses_root / "pose_library_manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    return {"manifest": str(manifest_path), "actions": manifest_actions}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build SpriteForge P1 action pose library.")
    parser.add_argument("--poses-root", type=Path, default=Path(__file__).resolve().parent / "poses")
    parser.add_argument("--version", default="0.2.0-p1-idle-walk")
    parser.add_argument("--replace", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    result = build_library(args.poses_root.resolve(), args.version, args.replace)
    print(json.dumps({"ok": True, **result}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
