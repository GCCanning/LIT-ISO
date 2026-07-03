"""Generate and download LIT-RPG UI candidates from a UI Studio handoff.

The default run is offline and writes a normalized asset manifest, prompt list,
and approval template. Pass --generate to spend PixelLab credits.

Candidate files are kept in an ignored persistent review directory under
Tools/PixelLab. Approved assets are promoted separately with
promote_ui_handoff_assets.py.
"""

from __future__ import annotations

import argparse
import base64
import io
import json
import os
import re
import sys
import time
import urllib.request
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from pixellab_common import call, contact_sheet, find_images, wait_job


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_HANDOFF = (
    ROOT
    / "Tools"
    / "LitRpgUIStudio"
    / "handoffs"
    / "lit-rpg-ui-handoff-2026-06-30.json"
)
TOKEN_FILE = Path(__file__).with_name("pixellab_token.local.txt")

STYLE = (
    "original warm cozy indie survival-crafting game UI, handcrafted pixel art, "
    "medium walnut-brown wood frame, stitched deep forest-green canvas inset, "
    "warm cream highlights, restrained ember-orange focus accents, occasional "
    "small brass fasteners and subtle leaf motifs, readable chunky pixels, "
    "friendly and practical rather than ornate, transparent background outside "
    "the component, no text, no letters, no numbers, no logos, no watermark, "
    "no purple gradient, no glossy mobile-game finish, no black-and-gold MMO "
    "ornament, no photorealism, no copied game assets"
)


@dataclass(frozen=True)
class Spec:
    width: int
    height: int
    description: str
    mode: str = "pixellab"
    border: int = 0
    source_key: str = ""
    transform: str = ""
    notes: str = ""
    color: str = ""


BASE_SPECS: dict[str, Spec] = {
    "UI/Crafting/recipe_slot": Spec(
        64, 64, "square crafting recipe slot, recessed center, tiny wood corner joints"
    ),
    "UI/Creator/category_button": Spec(
        160, 48, "compact horizontal appearance-category button, blank center, 9-slice friendly", border=10
    ),
    "UI/Creator/class_slot": Spec(
        96, 96, "square class selection token frame, broad empty center for a runtime class icon"
    ),
    "UI/Creator/option_slot": Spec(
        64, 64, "square appearance thumbnail slot, quiet inset center"
    ),
    "UI/HUD/ability_slot": Spec(
        64, 64, "square active-ability slot, strong readable edge, empty center for spell icon"
    ),
    "UI/HUD/day_band": Spec(
        320, 48, "thin horizontal day, time and weather band, blank center, 9-slice friendly", border=10
    ),
    "UI/HUD/hotbar_slot": Spec(
        64, 64, "chunky square inventory hotbar slot, recessed center, small lower corner for key label"
    ),
    "UI/HUD/minimap_frame": Spec(
        192, 192, "circular minimap frame with four restrained compass ticks and a hollow transparent center"
    ),
    "UI/HUD/quest_frame": Spec(
        256, 160, "compact pinned-quest panel with a subtle wood header edge and plain readable center", border=14
    ),
    "UI/HUD/vitals_frame": Spec(
        384, 176, "compact player vitals plate with room for portrait and four horizontal meters", border=14
    ),
    "UI/Loading/flame": Spec(
        32, 48, "single small campfire flame progress icon, centered, transparent background"
    ),
    "UI/Loading/tip_frame": Spec(
        512, 96, "wide low-profile loading tip frame, mostly transparent center, 9-slice friendly", border=14
    ),
    "UI/Map/frame": Spec(
        512, 384, "thin unobtrusive world-map edge frame with open transparent center", border=16
    ),
    "UI/Menu/button": Spec(
        192, 56, "horizontal wooden menu plaque, blank center, clear silhouette, 9-slice friendly", border=18
    ),
    "UI/Menu/logo": Spec(
        512,
        128,
        "LIT-RPG wordmark",
        mode="manual",
        notes="Use the approved pixel font and runtime text. Do not ask an image model to spell the title.",
    ),
    "UI/Menu/panel": Spec(
        384,
        512,
        "one single large rectangular menu panel, warm wood outer frame and a fully filled opaque dark forest-green stitched canvas center; the center is solid canvas, not transparent, not hollow, and not a cutout; no separate parts, no atlas, no guide lines, no swatches, no example icons",
        border=60,
    ),
    "UI/Quest/log_frame": Spec(
        384, 512, "tall quest-log frame with compact header rail and plain list area", border=16
    ),
    "UI/Settings/category": Spec(
        160, 48, "horizontal settings category tab, blank center, 9-slice friendly", border=10
    ),
    "UI/Settings/keycap": Spec(
        48, 32, "small blank keyboard keycap frame with empty center for runtime text", border=7
    ),
    "UI/Settings/panel": Spec(
        512, 512, "large settings canvas panel with quiet wood edge and plain control area", border=16
    ),
    "UI/Skills/skill_slot": Spec(
        64, 64, "square learned-skill slot with a small lower edge for runtime rank pips"
    ),
    "UI/Skills/wheel_segment": Spec(
        128,
        128,
        "one clean ninety-degree radial ability-wheel quadrant segment, exact quarter-ring geometry, transparent elsewhere",
        notes="Geometry-sensitive. Reject candidates whose inner and outer arcs are not rotationally symmetric.",
    ),
    "UI/System/detail_frame": Spec(
        320, 512, "tall selected-item or selected-skill detail panel with plain center", border=16
    ),
    "UI/System/equipment_slot": Spec(
        72, 72, "square equipment slot, slightly heavier edge than an inventory slot"
    ),
    "UI/System/item_slot": Spec(
        64, 64, "square backpack and storage item slot with a quiet recessed center"
    ),
    "UI/System/tab": Spec(
        144, 48, "compact horizontal system-book tab, blank center, 9-slice friendly", border=10
    ),
    "UI/World/preview_frame": Spec(
        512, 384, "thin world-preview frame with a mostly open transparent center", border=16
    ),
}


SUPPORT_SPECS: dict[str, Spec] = {
    "UI/Common/close_button": Spec(
        48, 48, "small square close-button frame with empty center for a runtime X icon"
    ),
    "UI/Common/context_menu": Spec(
        192, 240, "compact vertical context-menu plate with a plain center", border=12
    ),
    "UI/Common/dropdown": Spec(
        192, 48, "horizontal blank dropdown selector plate with a small arrow socket at right", border=10
    ),
    "UI/Common/scroll_handle": Spec(
        20, 56, "narrow vertical scrollbar handle with two tiny grip notches"
    ),
    "UI/Common/scroll_track": Spec(
        20, 160, "narrow vertical recessed scrollbar track"
    ),
    "UI/Common/toggle_off": Spec(
        56, 28, "small toggle switch in off state, knob on the left, muted forest canvas"
    ),
    "UI/Common/toggle_on": Spec(
        56, 28, "small toggle switch in on state, knob on the right, restrained leaf-green highlight"
    ),
    "UI/Common/tooltip": Spec(
        256, 128, "small tooltip panel with a plain readable center and tiny pointer notch", border=12
    ),
    "UI/HUD/bar_track": Spec(
        192, 20, "flat dark recessed meter track", mode="procedural", color="#1b241d"
    ),
    "UI/HUD/health_fill": Spec(
        192, 16, "flat health meter fill", mode="procedural", color="#c94f48"
    ),
    "UI/HUD/mana_fill": Spec(
        192, 16, "flat mana meter fill", mode="procedural", color="#4d8fba"
    ),
    "UI/HUD/stamina_fill": Spec(
        192, 16, "flat stamina meter fill", mode="procedural", color="#79a85a"
    ),
    "UI/HUD/xp_fill": Spec(
        192, 12, "flat experience meter fill", mode="procedural", color="#d8ae45"
    ),
    "UI/HUD/hotbar_slot_selected": Spec(
        64,
        64,
        "selected hotbar state",
        mode="derived",
        source_key="UI/HUD/hotbar_slot",
        transform="selected",
    ),
    "UI/Menu/button_disabled": Spec(
        192,
        56,
        "disabled menu button state",
        mode="derived",
        source_key="UI/Menu/button",
        transform="disabled",
        border=12,
    ),
    "UI/Menu/button_hover": Spec(
        192,
        56,
        "hover menu button state",
        mode="derived",
        source_key="UI/Menu/button",
        transform="hover",
        border=12,
    ),
    "UI/Menu/button_pressed": Spec(
        192,
        56,
        "pressed menu button state",
        mode="derived",
        source_key="UI/Menu/button",
        transform="pressed",
        border=12,
    ),
    "UI/System/tab_active": Spec(
        144,
        48,
        "active system tab state",
        mode="derived",
        source_key="UI/System/tab",
        transform="selected",
        border=10,
    ),
}


def load_token() -> str:
    value = os.environ.get("PIXELLAB_TOKEN", "").strip()
    if not value:
        try:
            value = TOKEN_FILE.read_text(encoding="utf-8").strip()
        except OSError:
            raise SystemExit(
                f"PixelLab token missing. Set PIXELLAB_TOKEN or create {TOKEN_FILE}"
            )
    return value[7:] if value.lower().startswith("bearer ") else value


def slug(resource_key: str) -> str:
    return resource_key.replace("/", "__")


def resolve_output(handoff: Path, value: str) -> Path:
    if value:
        return Path(value).expanduser().resolve()
    date_match = re.search(r"(\d{4}-\d{2}-\d{2})", handoff.stem)
    label = date_match.group(1) if date_match else handoff.stem
    return ROOT / "Tools" / "PixelLab" / "review" / "UIHandoff" / label


def load_handoff(path: Path) -> dict[str, Any]:
    try:
        doc = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise SystemExit(f"Could not read handoff {path}: {exc}")
    if doc.get("$schema") != "litiso-ui-export/v1":
        raise SystemExit(
            f"Unsupported handoff schema {doc.get('$schema')!r}; expected litiso-ui-export/v1"
        )
    if not isinstance(doc.get("screens"), list):
        raise SystemExit("Handoff has no screens array.")
    return doc


def component_usage(doc: dict[str, Any]) -> dict[str, dict[str, list[str]]]:
    usage: dict[str, dict[str, list[str]]] = {}
    for screen in doc["screens"]:
        for component in screen.get("components", []):
            key = component.get("resourcesKey")
            if not key:
                continue
            record = usage.setdefault(key, {"screens": [], "components": [], "types": []})
            for field, value in (
                ("screens", screen.get("id", "")),
                ("components", component.get("id", "")),
                ("types", component.get("type", "")),
            ):
                if value and value not in record[field]:
                    record[field].append(value)
    return usage


def prompt_for(resource_key: str, spec: Spec) -> str:
    geometry = (
        f"Canvas exactly {spec.width} by {spec.height} pixels. "
        "The component must be centered with at least two transparent pixels around its outer silhouette. "
    )
    slicing = (
        f"Keep all corners and edge ornament inside a uniform {spec.border}-pixel border; "
        "the center must remain visually plain so Unity can 9-slice it. "
        if spec.border
        else ""
    )
    return (
        f"{STYLE}. {geometry}{slicing}Asset: {spec.description}. "
        f"Production resource ID: {resource_key}."
    )


def build_manifest(
    doc: dict[str, Any], handoff: Path, include_support: bool
) -> dict[str, Any]:
    usage = component_usage(doc)
    missing_specs = sorted(set(usage) - set(BASE_SPECS))
    if missing_specs:
        raise SystemExit("Missing asset specifications for: " + ", ".join(missing_specs))

    records = []
    specs = dict(BASE_SPECS)
    if include_support:
        specs.update(SUPPORT_SPECS)

    for key in sorted(specs):
        spec = specs[key]
        target = ROOT / "Assets" / "Resources" / f"{key}.png"
        reference = usage.get(key, {"screens": [], "components": [], "types": []})
        records.append(
            {
                "resourceKey": key,
                "slug": slug(key),
                "width": spec.width,
                "height": spec.height,
                "mode": spec.mode,
                "kind": "support" if key in SUPPORT_SPECS else "handoff",
                "sliceBorder": spec.border,
                "sourceKey": spec.source_key or None,
                "transform": spec.transform or None,
                "color": spec.color or None,
                "notes": spec.notes or None,
                "screens": reference["screens"],
                "components": reference["components"],
                "componentTypes": reference["types"],
                "prompt": prompt_for(key, spec) if spec.mode == "pixellab" else None,
                "candidatePattern": f"{slug(key)}__c{{candidate:02d}}.png",
                "promotionTarget": str(target.relative_to(ROOT)).replace("\\", "/"),
                "currentlyExists": target.exists(),
            }
        )

    return {
        "$schema": "lit-rpg-ui-assets/v1",
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "sourceHandoff": str(handoff.relative_to(ROOT)).replace("\\", "/")
        if handoff.is_relative_to(ROOT)
        else str(handoff),
        "sourceSchema": doc["$schema"],
        "designResolution": doc.get("designResolution"),
        "style": STYLE,
        "counts": {
            "screens": len(doc["screens"]),
            "handoffResourceKeys": len(usage),
            "records": len(records),
            "pixelLab": sum(r["mode"] == "pixellab" for r in records),
            "derived": sum(r["mode"] == "derived" for r in records),
            "procedural": sum(r["mode"] == "procedural" for r in records),
            "manual": sum(r["mode"] == "manual" for r in records),
        },
        "assets": records,
    }


def write_offline_handoff(out_dir: Path, manifest: dict[str, Any]) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / "candidates").mkdir(exist_ok=True)
    (out_dir / "asset_manifest.json").write_text(
        json.dumps(manifest, indent=2), encoding="utf-8"
    )
    prompts = {
        item["resourceKey"]: item["prompt"]
        for item in manifest["assets"]
        if item["mode"] == "pixellab"
    }
    (out_dir / "prompts.json").write_text(
        json.dumps(prompts, indent=2), encoding="utf-8"
    )
    lines = [
        "# Approve one candidate per line.",
        "# Format: Resources/Key=candidate_filename.png",
        "# Lines beginning with # are ignored.",
        "",
    ]
    for item in manifest["assets"]:
        if item["mode"] == "pixellab":
            lines.append(
                f"# {item['resourceKey']}={item['candidatePattern'].format(candidate=1)}"
            )
    approvals_path = out_dir / "approvals.txt"
    if not approvals_path.exists():
        approvals_path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def approved_candidates(path: Path) -> dict[str, str]:
    if not path.exists():
        return {}
    approved: dict[str, str] = {}
    for raw in path.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        resource_key, filename = (part.strip() for part in line.split("=", 1))
        if resource_key and filename:
            approved[resource_key] = filename
    return approved


def approved_resource_keys(path: Path) -> set[str]:
    return set(approved_candidates(path))


def matches_filters(
    record: dict[str, Any], only: set[str], screens: set[str]
) -> bool:
    if only and record["resourceKey"] not in only and record["slug"] not in only:
        return False
    if screens and not screens.intersection(record["screens"]):
        return False
    return True


def extract_image_bytes(token: str, record: Any) -> bytes | None:
    images: list[str] = []
    find_images(record, images)
    if images:
        return base64.b64decode(images[0])

    blob = json.dumps(record)
    urls = re.findall(r'https?://[^"\\\s]+?\.(?:png|webp)[^"\\\s]*', blob)
    for url in urls:
        for headers in (
            {},
            {"Authorization": f"Bearer {token}"},
            {"User-Agent": "Mozilla/5.0"},
            {"Authorization": f"Bearer {token}", "User-Agent": "Mozilla/5.0"},
        ):
            try:
                return urllib.request.urlopen(
                    urllib.request.Request(url, headers=headers), timeout=90
                ).read()
            except Exception:
                continue
    return None


def normalize_png(data: bytes, expected: tuple[int, int]) -> bytes:
    try:
        from PIL import Image
    except ImportError:
        if data.startswith(b"\x89PNG\r\n\x1a\n"):
            return data
        raise SystemExit("Pillow is required when PixelLab returns a non-PNG image.")

    with Image.open(io.BytesIO(data)) as image:
        image = image.convert("RGBA")
        if image.size != expected:
            print(
                f"  warning: PixelLab returned {image.width}x{image.height}; "
                f"expected {expected[0]}x{expected[1]}"
            )
        output = io.BytesIO()
        image.save(output, format="PNG")
        return output.getvalue()


def load_jobs(path: Path) -> dict[str, Any]:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {"jobs": {}}


def save_jobs(path: Path, jobs: dict[str, Any]) -> None:
    path.write_text(json.dumps(jobs, indent=2), encoding="utf-8")


def submit_candidate(
    token: str,
    record: dict[str, Any],
    candidate: int,
    jobs: dict[str, Any],
    jobs_path: Path,
    force: bool,
) -> bytes | None:
    job_key = f"{record['resourceKey']}#c{candidate:02d}"
    if force:
        jobs["jobs"].pop(job_key, None)
    prior = jobs["jobs"].get(job_key, {})
    job_id = prior.get("backgroundJobId")
    response: Any = None

    if job_id and prior.get("status") != "failed":
        print(f"  resuming background job {job_id}")
        response = wait_job(token, job_id, job_key)
    else:
        payload = {
            "description": record["prompt"],
            "image_size": {"width": record["width"], "height": record["height"]},
        }
        for attempt in range(6):
            response = call(
                token, "POST", "/generate-ui-v2", payload, fatal=False
            )
            error = response.get("_error") if isinstance(response, dict) else None
            if not error:
                break
            if error == 429:
                print("  PixelLab queue is busy; waiting 30 seconds.")
                time.sleep(30)
                continue
            if error == 402:
                raise SystemExit("PixelLab reports insufficient generations.")
            jobs["jobs"][job_key] = {"status": "failed", "error": response}
            save_jobs(jobs_path, jobs)
            return None

        job_id = response.get("background_job_id") if isinstance(response, dict) else None
        jobs["jobs"][job_key] = {
            "status": "submitted",
            "backgroundJobId": job_id,
            "submittedAt": datetime.now(timezone.utc).isoformat(),
        }
        save_jobs(jobs_path, jobs)
        if job_id:
            response = wait_job(token, job_id, job_key)

    if not response:
        jobs["jobs"][job_key] = {
            **jobs["jobs"].get(job_key, {}),
            "status": "failed",
        }
        save_jobs(jobs_path, jobs)
        return None

    data = extract_image_bytes(token, response)
    jobs["jobs"][job_key] = {
        **jobs["jobs"].get(job_key, {}),
        "status": "completed" if data else "failed",
        "completedAt": datetime.now(timezone.utc).isoformat(),
    }
    save_jobs(jobs_path, jobs)
    return data


def generate_candidates(
    out_dir: Path,
    manifest: dict[str, Any],
    candidates: int,
    only: set[str],
    screens: set[str],
    force: bool,
) -> None:
    token = load_token()
    candidates_dir = out_dir / "candidates"
    jobs_path = out_dir / "jobs.json"
    jobs = load_jobs(jobs_path)
    print("PixelLab balance:", json.dumps(call(token, "GET", "/balance")))

    approved = approved_resource_keys(out_dir / "approvals.txt")
    records = [
        record
        for record in manifest["assets"]
        if record["mode"] == "pixellab"
        and matches_filters(record, only, screens)
        and record["resourceKey"] not in approved
    ]
    locked = [
        record["resourceKey"]
        for record in manifest["assets"]
        if record["mode"] == "pixellab"
        and matches_filters(record, only, screens)
        and record["resourceKey"] in approved
    ]
    for resource_key in locked:
        print(f"[approved lock] skipping {resource_key}")
    for index, record in enumerate(records, 1):
        print(
            f"\n[{index}/{len(records)}] {record['resourceKey']} "
            f"{record['width']}x{record['height']}"
        )
        for candidate in range(1, candidates + 1):
            filename = record["candidatePattern"].format(candidate=candidate)
            destination = candidates_dir / filename
            if destination.exists() and not force:
                print(f"  exists: {filename}")
                continue
            print(f"  candidate {candidate}/{candidates}")
            data = submit_candidate(
                token, record, candidate, jobs, jobs_path, force
            )
            if not data:
                print("  no image returned")
                continue
            destination.write_bytes(
                normalize_png(data, (record["width"], record["height"]))
            )
            print(f"  saved {destination}")

    sheet = contact_sheet(str(candidates_dir))
    if sheet:
        print("\nContact sheet:", sheet)
    print("PixelLab balance after:", json.dumps(call(token, "GET", "/balance")))


def recover_approved(out_dir: Path, manifest: dict[str, Any]) -> None:
    approvals = approved_candidates(out_dir / "approvals.txt")
    if not approvals:
        print("No approved resources are recorded.")
        return

    records = {record["resourceKey"]: record for record in manifest["assets"]}
    jobs_path = out_dir / "jobs.json"
    jobs = load_jobs(jobs_path).get("jobs", {})
    candidates_dir = out_dir / "candidates"
    candidates_dir.mkdir(parents=True, exist_ok=True)
    token = load_token()
    recovered = 0

    for resource_key, filename in approvals.items():
        destination = candidates_dir / filename
        if destination.exists():
            print(f"[approved present] {resource_key}: {filename}")
            continue
        record = records.get(resource_key)
        if not record:
            print(f"[approved missing manifest] {resource_key}")
            continue
        match = re.search(r"__c(\d+)\.png$", filename)
        if not match:
            print(f"[approved invalid filename] {resource_key}: {filename}")
            continue
        candidate = int(match.group(1))
        job_key = f"{resource_key}#c{candidate:02d}"
        job_id = jobs.get(job_key, {}).get("backgroundJobId")
        if not job_id:
            print(f"[approved missing job] {resource_key}: {job_key}")
            continue
        print(f"[recover approved] {resource_key} from job {job_id}")
        response = call(token, "GET", f"/background-jobs/{job_id}", fatal=False)
        if isinstance(response, dict) and response.get("_error"):
            print(f"  could not retrieve job {job_id}")
            continue
        data = extract_image_bytes(token, response)
        if not data:
            print(f"  job {job_id} returned no image")
            continue
        destination.write_bytes(
            normalize_png(data, (record["width"], record["height"]))
        )
        print(f"  restored {destination}")
        recovered += 1

    print(f"Recovered {recovered} approved candidate(s) without new generations.")
    sheet = contact_sheet(str(candidates_dir))
    if sheet:
        print("Contact sheet:", sheet)


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Compile or generate PixelLab assets from the LIT-RPG UI handoff."
    )
    parser.add_argument("--handoff", default=str(DEFAULT_HANDOFF))
    parser.add_argument("--out-dir", default="")
    parser.add_argument("--generate", action="store_true")
    parser.add_argument(
        "--recover-approved",
        action="store_true",
        help="Download missing approved candidates from recorded completed jobs without submitting new generations.",
    )
    parser.add_argument("--candidates", type=int, default=2)
    parser.add_argument("--only", default="")
    parser.add_argument("--screen", default="")
    parser.add_argument("--base-only", action="store_true")
    parser.add_argument("--force", action="store_true")
    args = parser.parse_args()

    handoff = Path(args.handoff).expanduser().resolve()
    out_dir = resolve_output(handoff, args.out_dir)
    doc = load_handoff(handoff)
    manifest = build_manifest(doc, handoff, include_support=not args.base_only)
    write_offline_handoff(out_dir, manifest)

    print("UI handoff:", handoff)
    print("Review output:", out_dir)
    print("Asset counts:", json.dumps(manifest["counts"]))

    if args.recover_approved:
        recover_approved(out_dir, manifest)
        return

    if not args.generate:
        print(
            "\nDry run complete. No PixelLab credits were used. "
            "Re-run with --generate when the manifest is approved."
        )
        return

    only = {item.strip() for item in args.only.split(",") if item.strip()}
    screens = {item.strip() for item in args.screen.split(",") if item.strip()}
    generate_candidates(
        out_dir,
        manifest,
        candidates=max(1, args.candidates),
        only=only,
        screens=screens,
        force=args.force,
    )


if __name__ == "__main__":
    main()
