"""Promote approved UI candidates into Assets/Resources using exact IDs.

This script is intentionally separate from generation. It defaults to a dry
run; pass --apply to copy files. Existing Resources assets are protected unless
--replace is also supplied.
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_REVIEW = (
    ROOT / "Tools" / "PixelLab" / "review" / "UIHandoff" / "2026-06-30"
)
RESOURCES_ROOT = (ROOT / "Assets" / "Resources").resolve()


def load_json(path: Path) -> dict[str, Any]:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise SystemExit(f"Could not read {path}: {exc}")


def parse_approvals(path: Path) -> dict[str, str]:
    try:
        lines = path.read_text(encoding="utf-8").splitlines()
    except OSError as exc:
        raise SystemExit(f"Could not read approvals {path}: {exc}")
    approvals: dict[str, str] = {}
    for number, raw in enumerate(lines, 1):
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        if "=" not in line:
            raise SystemExit(f"{path}:{number}: expected Resource/Key=filename.png")
        resource_key, filename = (part.strip() for part in line.split("=", 1))
        if not resource_key or not filename:
            raise SystemExit(f"{path}:{number}: resource key and filename are required")
        if resource_key in approvals:
            raise SystemExit(f"{path}:{number}: duplicate approval for {resource_key}")
        approvals[resource_key] = filename
    return approvals


def safe_target(relative: str) -> Path:
    target = (ROOT / relative).resolve()
    if os.path.commonpath([str(target), str(RESOURCES_ROOT)]) != str(RESOURCES_ROOT):
        raise SystemExit(f"Refusing target outside Assets/Resources: {target}")
    return target


def validate_png(path: Path, expected: tuple[int, int]) -> None:
    try:
        from PIL import Image
    except ImportError:
        if not path.read_bytes().startswith(b"\x89PNG\r\n\x1a\n"):
            raise SystemExit(f"Candidate is not a PNG: {path}")
        return
    with Image.open(path) as image:
        if image.format != "PNG":
            raise SystemExit(f"Candidate is not PNG-formatted: {path}")
        if image.size != expected:
            raise SystemExit(
                f"Candidate {path.name} is {image.width}x{image.height}; "
                f"expected {expected[0]}x{expected[1]}"
            )
        if image.mode != "RGBA":
            print(f"warning: {path.name} has mode {image.mode}, not RGBA")


def procedural_image(record: dict[str, Any]):
    try:
        from PIL import Image, ImageColor, ImageDraw
    except ImportError:
        raise SystemExit("Pillow is required to create procedural UI meters.")
    width, height = record["width"], record["height"]
    color = ImageColor.getrgb(record["color"] or "#ffffff")
    image = Image.new("RGBA", (width, height), (*color, 255))
    draw = ImageDraw.Draw(image)
    if height >= 8:
        draw.line((1, 1, width - 2, 1), fill=(255, 255, 255, 55), width=1)
        draw.line(
            (1, height - 2, width - 2, height - 2),
            fill=(0, 0, 0, 90),
            width=1,
        )
    return image


def derived_image(source: Path, transform: str):
    try:
        from PIL import Image, ImageDraw, ImageEnhance, ImageOps
    except ImportError:
        raise SystemExit("Pillow is required to derive deterministic UI states.")

    image = Image.open(source).convert("RGBA")
    alpha = image.getchannel("A")
    rgb = image.convert("RGB")

    if transform == "hover":
        rgb = ImageEnhance.Brightness(rgb).enhance(1.14)
        overlay = Image.new("RGB", image.size, (233, 155, 69))
        rgb = Image.blend(rgb, overlay, 0.08)
    elif transform == "pressed":
        rgb = ImageEnhance.Brightness(rgb).enhance(0.76)
    elif transform == "disabled":
        rgb = ImageOps.grayscale(rgb).convert("RGB")
        alpha = alpha.point(lambda value: int(value * 0.62))
    elif transform == "selected":
        rgb = ImageEnhance.Brightness(rgb).enhance(1.08)
    else:
        raise SystemExit(f"Unsupported derived transform: {transform}")

    output = Image.merge("RGBA", (*rgb.split(), alpha))
    if transform == "pressed":
        shifted = Image.new("RGBA", output.size, (0, 0, 0, 0))
        shifted.alpha_composite(output, (0, 2))
        output = shifted
    elif transform == "selected":
        draw = ImageDraw.Draw(output)
        for inset in (0, 1):
            draw.rectangle(
                (inset, inset, output.width - 1 - inset, output.height - 1 - inset),
                outline=(233, 155, 69, 255),
                width=1,
            )
    return output


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Promote approved PixelLab UI candidates into Unity Resources."
    )
    parser.add_argument(
        "--manifest", default=str(DEFAULT_REVIEW / "asset_manifest.json")
    )
    parser.add_argument("--approvals", default="")
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--replace", action="store_true")
    parser.add_argument(
        "--derive-existing",
        action="store_true",
        help="Allow derived states to use an existing Resources source that was not approved in this run.",
    )
    parser.add_argument("--skip-derived", action="store_true")
    parser.add_argument("--skip-procedural", action="store_true")
    args = parser.parse_args()

    manifest_path = Path(args.manifest).expanduser().resolve()
    review_dir = manifest_path.parent
    approvals_path = (
        Path(args.approvals).expanduser().resolve()
        if args.approvals
        else review_dir / "approvals.txt"
    )
    candidates_dir = review_dir / "candidates"
    manifest = load_json(manifest_path)
    if manifest.get("$schema") != "lit-rpg-ui-assets/v1":
        raise SystemExit("Unsupported asset manifest schema.")
    approvals = parse_approvals(approvals_path)
    records = {item["resourceKey"]: item for item in manifest["assets"]}

    unknown = sorted(set(approvals) - set(records))
    if unknown:
        raise SystemExit("Approvals contain unknown resource IDs: " + ", ".join(unknown))

    actions: list[dict[str, Any]] = []
    for resource_key, filename in approvals.items():
        record = records[resource_key]
        if record["mode"] != "pixellab":
            raise SystemExit(f"{resource_key} is {record['mode']}, not a PixelLab candidate")
        source = (candidates_dir / filename).resolve()
        if os.path.commonpath([str(source), str(candidates_dir.resolve())]) != str(
            candidates_dir.resolve()
        ):
            raise SystemExit(f"Approval escapes the candidates folder: {filename}")
        if not source.is_file():
            raise SystemExit(f"Approved candidate does not exist: {source}")
        validate_png(source, (record["width"], record["height"]))
        actions.append(
            {
                "resourceKey": resource_key,
                "mode": "copy",
                "source": source,
                "target": safe_target(record["promotionTarget"]),
            }
        )

    approved_keys = {action["resourceKey"] for action in actions}
    available_keys = set(approved_keys)
    if args.derive_existing:
        available_keys.update(
            key
            for key, record in records.items()
            if safe_target(record["promotionTarget"]).exists()
        )

    if not args.skip_procedural:
        for record in records.values():
            if record["mode"] == "procedural":
                actions.append(
                    {
                        "resourceKey": record["resourceKey"],
                        "mode": "procedural",
                        "record": record,
                        "target": safe_target(record["promotionTarget"]),
                    }
                )

    if not args.skip_derived:
        for record in records.values():
            if record["mode"] != "derived" or record["sourceKey"] not in available_keys:
                continue
            source_record = records[record["sourceKey"]]
            actions.append(
                {
                    "resourceKey": record["resourceKey"],
                    "mode": "derived",
                    "sourceKey": record["sourceKey"],
                    "source": safe_target(source_record["promotionTarget"]),
                    "record": record,
                    "target": safe_target(record["promotionTarget"]),
                }
            )

    if not actions:
        print("No active approvals or deterministic support assets to promote.")
        return

    blocked = [
        action for action in actions if action["target"].exists() and not args.replace
    ]
    print(f"Planned actions: {len(actions)}")
    for action in actions:
        marker = "BLOCKED existing" if action in blocked else action["mode"]
        print(
            f"  [{marker}] {action['resourceKey']} -> "
            f"{action['target'].relative_to(ROOT)}"
        )

    if blocked:
        print(
            "\nExisting targets are protected. Re-run with --replace after reviewing "
            "the list, or remove those approvals."
        )
        if args.apply:
            raise SystemExit(2)

    if not args.apply:
        print("\nDry run only. Pass --apply to write these assets.")
        return

    receipt = {
        "$schema": "lit-rpg-ui-promotion/v1",
        "promotedAt": datetime.now(timezone.utc).isoformat(),
        "manifest": str(manifest_path),
        "actions": [],
    }
    for action in actions:
        target = action["target"]
        target.parent.mkdir(parents=True, exist_ok=True)
        if action["mode"] == "copy":
            shutil.copy2(action["source"], target)
        elif action["mode"] == "procedural":
            procedural_image(action["record"]).save(target, format="PNG")
        elif action["mode"] == "derived":
            if not action["source"].exists():
                raise SystemExit(
                    f"Derived source was not promoted: {action['sourceKey']}"
                )
            derived_image(action["source"], action["record"]["transform"]).save(
                target, format="PNG"
            )
        receipt["actions"].append(
            {
                "resourceKey": action["resourceKey"],
                "mode": action["mode"],
                "target": str(target.relative_to(ROOT)).replace("\\", "/"),
            }
        )
        print("wrote", target.relative_to(ROOT))

    receipt_path = review_dir / "promotion_receipt.json"
    receipt_path.write_text(json.dumps(receipt, indent=2), encoding="utf-8")
    print("\nPromotion receipt:", receipt_path)
    print("Open Unity once to import the PNGs and generate/update their .meta files.")


if __name__ == "__main__":
    main()
