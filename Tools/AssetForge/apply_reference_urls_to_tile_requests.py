#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path
from urllib.parse import urlparse


def read_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, payload: dict) -> None:
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def absolute_sprixen_url(base_url: str, result_url: str) -> str:
    if result_url.startswith("http://") or result_url.startswith("https://"):
        return result_url
    parsed = urlparse(base_url)
    if result_url.startswith("/") and parsed.scheme and parsed.netloc:
        return f"{parsed.scheme}://{parsed.netloc}{result_url}"
    return result_url


def find_selected_references(project_root: Path, selected_report: Path, base_url: str) -> dict[str, dict]:
    report = read_json(selected_report)
    references: dict[str, dict] = {}
    for item in report.get("items", []):
        material = item.get("material")
        source_job = item.get("source_job")
        source_candidate = item.get("source_candidate")
        if not material or not source_job or not source_candidate:
            continue
        source_report_path = project_root / "Assets/Generated/_Review" / source_job / "review_report.json"
        if not source_report_path.exists():
            continue
        source_report = read_json(source_report_path)
        for source_item in source_report.get("items", []):
            generation = source_item.get("generation") or {}
            if source_item.get("name") == source_candidate and generation.get("result_url"):
                references[material] = {
                    "source_job": source_job,
                    "source_candidate": source_candidate,
                    "reference_image_url": absolute_sprixen_url(base_url, generation["result_url"]),
                    "sprixen_generation_id": generation.get("sprixen_generation_id", ""),
                }
                break
    return references


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-root", default=".")
    parser.add_argument("--selected-report", default="Assets/Generated/_Review/greenwake_height_material_masters_selected_v1/review_report.json")
    parser.add_argument("--request-root", default="Assets/Generated/_Review/_Requests")
    parser.add_argument("--base-url", default="https://api.sprixen.com/v1")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    project_root = Path(args.project_root).resolve()
    selected_report = (project_root / args.selected_report).resolve()
    request_root = (project_root / args.request_root).resolve()
    references = find_selected_references(project_root, selected_report, args.base_url)
    updated = []

    for request_path in request_root.glob("greenwake_*/generation_request.json"):
        request = read_json(request_path)
        spec = request.get("asset_spec") or {}
        material = spec.get("material")
        if isinstance(material, list):
            material = material[0] if material else None
        if material not in references:
            continue
        reference = references[material]
        request["reference_image_url"] = reference["reference_image_url"]
        request["style_reference"] = {
            "mode": "sprixen_reference_image_url",
            "material": material,
            **reference,
            "note": "Uses the original selected Sprixen result URL as a style guide. Local cleaned PNG remains the review source of truth.",
        }
        if not args.dry_run:
            write_json(request_path, request)
        updated.append({
            "job_name": request.get("job_name"),
            "material": material,
            "request_path": str(request_path.relative_to(project_root)).replace("\\", "/"),
            "reference_image_url": reference["reference_image_url"],
        })

    print(json.dumps({
        "schema": "lit_iso.asset_forge.reference_url_application.v1",
        "selected_report": str(selected_report.relative_to(project_root)).replace("\\", "/"),
        "references": references,
        "updated_count": len(updated),
        "updated": updated,
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
