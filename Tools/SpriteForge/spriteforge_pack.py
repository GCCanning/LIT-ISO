#!/usr/bin/env python3
"""SpriteForge sheet packer (SPEC section 5).

Takes a directory of aligned transparent frames (frame_000.png ...) and packs
a production-ready sprite sheet:

  sheet.png     single row, uniform cells sized to the max content bbox
                (+1px padding), frames bottom-center aligned on a shared
                baseline so the animation doesn't bob when played.
  sheet.json    cell size, fps/loop (from action.json when present),
                per-frame source bbox + pivot, pose library version.
  preview.png   horizontal contact strip with frame indices for review.

Usage:
  python3 spriteforge_pack.py --frames DIR [--out DIR] [--fps 10] [--loop]
                              [--action-json path/to/action.json]
                              [--character witch --action walk --direction S]
"""
from __future__ import annotations

import argparse
import json
import os
import re
import sys

from PIL import Image, ImageDraw

FRAME_RE = re.compile(r"frame_(\d+)\.png$")


def find_frames(frames_dir: str) -> list[str]:
    hits = []
    for name in os.listdir(frames_dir):
        m = FRAME_RE.match(name)
        if m:
            hits.append((int(m.group(1)), os.path.join(frames_dir, name)))
    hits.sort()
    if not hits:
        sys.exit(f"no frame_###.png files in {frames_dir}")
    nums = [n for n, _ in hits]
    if nums != list(range(len(nums))):
        sys.exit(f"frame numbering has gaps: {nums}")
    return [p for _, p in hits]


def content_bbox(img: Image.Image):
    """Bounding box of non-transparent pixels, or None for an empty frame."""
    return img.getbbox()  # respects alpha for RGBA


def clamp_loop_range(frame_count: int, loop_start: int, loop_end: int | None) -> tuple[int, int]:
    if frame_count <= 0:
        return 0, 0
    start = max(0, min(loop_start, frame_count - 1))
    end = frame_count - 1 if loop_end is None else loop_end
    end = max(start, min(end, frame_count - 1))
    return start, end


def pack(frames_dir: str, out_dir: str, fps: int, loop: bool,
         meta: dict | None = None, pad: int = 1,
         loop_start: int = 0, loop_end: int | None = None) -> dict:
    paths = find_frames(frames_dir)
    frames = [Image.open(p).convert("RGBA") for p in paths]

    # All frames must share one canvas size (the generator guarantees this;
    # fail loudly rather than guessing).
    sizes = {f.size for f in frames}
    if len(sizes) != 1:
        sys.exit(f"mixed frame canvas sizes: {sorted(sizes)}")

    boxes = []
    for i, f in enumerate(frames):
        b = content_bbox(f)
        if b is None:
            sys.exit(f"frame {i} is fully transparent")
        boxes.append(b)

    # Shared baseline: keep each frame's vertical offset RELATIVE to the
    # lowest content bottom so ground contact stays steady across frames
    # (bottom-aligning every frame individually makes jumps/bobs vanish).
    max_bottom = max(b[3] for b in boxes)

    # Uniform cell: width fits the widest frame; height fits every frame's
    # content PLUS its baseline offset (a frame riding high in the bob needs
    # headroom above the baseline, or it would clip at the cell top).
    cell_w = max(b[2] - b[0] for b in boxes) + 2 * pad
    cell_h = max(max_bottom - b[1] for b in boxes) + 2 * pad

    sheet = Image.new("RGBA", (cell_w * len(frames), cell_h), (0, 0, 0, 0))
    frame_meta = []
    for i, (f, b) in enumerate(zip(frames, boxes)):
        crop = f.crop(b)
        cx = i * cell_w + (cell_w - crop.width) // 2          # centered X
        cy = cell_h - pad - crop.height - (max_bottom - b[3])  # baseline Y
        assert cy >= 0, f"frame {i}: cell height math broke (cy={cy})"
        sheet.alpha_composite(crop, (cx, cy))
        frame_meta.append({
            "index": i,
            "source": os.path.basename(paths[i]),
            "source_bbox": list(b),
            "cell_rect": [i * cell_w, 0, cell_w, cell_h],
            # Pivot: bottom-center of the cell, the Unity slicing convention.
            "pivot": [0.5, 0.0],
        })

    os.makedirs(out_dir, exist_ok=True)
    sheet_path = os.path.join(out_dir, "sheet.png")
    sheet.save(sheet_path)

    # Review contact strip with indices.
    preview = Image.new("RGBA", (sheet.width, cell_h + 14), (12, 12, 14, 255))
    preview.alpha_composite(sheet, (0, 0))
    d = ImageDraw.Draw(preview)
    for i in range(len(frames)):
        d.text((i * cell_w + 2, cell_h + 1), str(i), fill=(220, 220, 220, 255))
        if i:
            d.line([(i * cell_w, 0), (i * cell_w, cell_h)],
                   fill=(60, 60, 70, 255), width=1)
    preview_path = os.path.join(out_dir, "preview.png")
    preview.save(preview_path)

    loop_start, loop_end = clamp_loop_range(len(frames), loop_start, loop_end)
    playback_frame_indices = list(range(loop_start, loop_end + 1)) if loop else list(range(len(frames)))

    sidecar = {
        "schema": "lit-iso.spriteforge.sheet.v1",
        "frames": len(frames),
        "cell": [cell_w, cell_h],
        "fps": fps,
        "loop": loop,
        "loop_start": loop_start,
        "loop_end": loop_end,
        "loop_range": [loop_start, loop_end],
        "playback_frame_indices": playback_frame_indices,
        "padding": pad,
        "frame_canvas": list(sizes.pop()),
        "frame_meta": frame_meta,
    }
    if meta:
        sidecar.update(meta)
    json_path = os.path.join(out_dir, "sheet.json")
    with open(json_path, "w") as fh:
        json.dump(sidecar, fh, indent=2)

    return {"sheet": sheet_path, "json": json_path, "preview": preview_path,
            "cell": [cell_w, cell_h], "frames": len(frames)}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--frames", required=True, help="dir with frame_###.png")
    ap.add_argument("--out", default=None, help="default: parent of --frames")
    ap.add_argument("--fps", type=int, default=None)
    ap.add_argument("--loop", action="store_true", default=None)
    ap.add_argument("--action-json", default=None,
                    help="action.json to take fps/loop defaults from")
    ap.add_argument("--character", default=None)
    ap.add_argument("--action", default=None)
    ap.add_argument("--direction", default=None)
    ap.add_argument("--pad", type=int, default=1)
    args = ap.parse_args()

    fps, loop = 10, True
    loop_start, loop_end = 0, None
    if args.action_json:
        with open(args.action_json) as fh:
            aj = json.load(fh)
        fps = aj.get("fps", fps)
        loop = aj.get("loop", loop)
        if "loop_range" in aj and isinstance(aj["loop_range"], list) and len(aj["loop_range"]) == 2:
            loop_start = int(aj["loop_range"][0])
            loop_end = int(aj["loop_range"][1])
        loop_start = int(aj.get("loop_start", loop_start))
        if "loop_end" in aj:
            loop_end = int(aj["loop_end"])
    if args.fps is not None:
        fps = args.fps
    if args.loop is not None:
        loop = args.loop

    meta = {k: v for k, v in (("character", args.character),
                              ("action", args.action),
                              ("direction", args.direction)) if v}
    ver_path = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                            "poses", "VERSION")
    if os.path.exists(ver_path):
        meta["pose_library_version"] = open(ver_path).read().strip()

    out_dir = args.out or os.path.dirname(os.path.abspath(args.frames))
    result = pack(args.frames, out_dir, fps, loop, meta, args.pad, loop_start, loop_end)
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
