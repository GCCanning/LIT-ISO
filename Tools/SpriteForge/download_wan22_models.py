#!/usr/bin/env python3
"""Download the preferred Wan 2.2 5B model set for SpriteForge lane B.

This intentionally installs only the low-vram-ish Wan 2.2 fallback stack used
by the Kijai WanVideoWrapper 5B example:

- UMT5 text encoder
- Wan 2.2 TI2V 5B FastWan diffusion model
- Wan 2.2 VAE

The files are large, so the script is resumable through huggingface_hub and
writes an install manifest under Tools/SpriteForge/out/lane_b.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from huggingface_hub import hf_hub_download


@dataclass(frozen=True)
class WanModelFile:
    role: str
    repo_id: str
    repo_path: str
    target_bucket: str
    target_name: str
    expected_size: int
    source_url: str


WAN22_5B_FASTWAN_FILES = [
    WanModelFile(
        role="text_encoder",
        repo_id="Kijai/WanVideo_comfy",
        repo_path="umt5-xxl-enc-bf16.safetensors",
        target_bucket="text_encoders",
        target_name="umt5-xxl-enc-bf16.safetensors",
        expected_size=11_361_845_464,
        source_url="https://huggingface.co/Kijai/WanVideo_comfy/blob/main/umt5-xxl-enc-bf16.safetensors",
    ),
    WanModelFile(
        role="diffusion_model",
        repo_id="Kijai/WanVideo_comfy",
        repo_path="FastWan/Wan2_2-TI2V-5B-FastWanFullAttn_bf16.safetensors",
        target_bucket="diffusion_models",
        target_name="Wan2_2-TI2V-5B-FastWanFullAttn_bf16.safetensors",
        expected_size=9_999_659_744,
        source_url="https://huggingface.co/Kijai/WanVideo_comfy/blob/main/FastWan/Wan2_2-TI2V-5B-FastWanFullAttn_bf16.safetensors",
    ),
    WanModelFile(
        role="vae",
        repo_id="Kijai/WanVideo_comfy",
        repo_path="Wan2_2_VAE_bf16.safetensors",
        target_bucket="vae",
        target_name="Wan2_2_VAE_bf16.safetensors",
        expected_size=1_409_401_152,
        source_url="https://huggingface.co/Kijai/WanVideo_comfy/blob/main/Wan2_2_VAE_bf16.safetensors",
    ),
]


def now_utc() -> str:
    return datetime.now(timezone.utc).isoformat()


def sha256_file(path: Path, block_size: int = 1024 * 1024) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        while True:
            block = handle.read(block_size)
            if not block:
                break
            digest.update(block)
    return digest.hexdigest()


def install_one(entry: WanModelFile, comfy_root: Path, dry_run: bool, hash_files: bool) -> dict[str, Any]:
    target_dir = comfy_root / "models" / entry.target_bucket
    target_path = target_dir / entry.target_name
    result: dict[str, Any] = {
        **asdict(entry),
        "target_path": str(target_path),
        "exists_before": target_path.exists(),
        "action": "skip",
        "size": target_path.stat().st_size if target_path.exists() else 0,
        "size_matches_expected": target_path.exists() and target_path.stat().st_size == entry.expected_size,
        "sha256": None,
    }

    if target_path.exists() and target_path.stat().st_size == entry.expected_size:
        if hash_files:
            result["sha256"] = sha256_file(target_path)
        return result

    result["action"] = "download"
    if dry_run:
        return result

    target_dir.mkdir(parents=True, exist_ok=True)
    downloaded = Path(
        hf_hub_download(
            repo_id=entry.repo_id,
            filename=entry.repo_path,
            local_dir=target_dir,
            local_dir_use_symlinks=False,
            resume_download=True,
        )
    )
    if downloaded.resolve() != target_path.resolve():
        if target_path.exists():
            target_path.unlink()
        shutil.move(str(downloaded), str(target_path))
        try:
            downloaded.parent.rmdir()
        except OSError:
            pass

    size = target_path.stat().st_size
    result["size"] = size
    result["size_matches_expected"] = size == entry.expected_size
    result["exists_after"] = target_path.exists()
    if hash_files:
        result["sha256"] = sha256_file(target_path)
    return result


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Install preferred Wan 2.2 5B model files for SpriteForge lane B.")
    parser.add_argument("--project-root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--comfy-root", type=Path, default=Path("C:/Projects/ComfyUI"))
    parser.add_argument("--manifest", type=Path, default=None)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--hash-files", action="store_true", help="Compute sha256 for installed files; slow for 20GB+.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    manifest_path = args.manifest
    if manifest_path is None:
        manifest_path = project_root / "Tools" / "SpriteForge" / "out" / "lane_b" / "wan22_model_install_manifest.json"
    elif not manifest_path.is_absolute():
        manifest_path = project_root / manifest_path

    comfy_root = args.comfy_root.resolve()
    results = [install_one(entry, comfy_root, args.dry_run, args.hash_files) for entry in WAN22_5B_FASTWAN_FILES]
    manifest = {
        "schema": "lit-iso.spriteforge.wan22-model-install.v1",
        "created_utc": now_utc(),
        "dry_run": args.dry_run,
        "comfy_root": str(comfy_root),
        "model_set": "wan22_5b_fastwan_bf16",
        "license_note": "Model provenance is Kijai/WanVideo_comfy on Hugging Face; verify upstream model-card terms before redistributing.",
        "total_expected_size_bytes": sum(entry.expected_size for entry in WAN22_5B_FASTWAN_FILES),
        "files": results,
    }
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps({"manifest": str(manifest_path), "dry_run": args.dry_run, "files": results}, indent=2))
    return 0 if all(item.get("size_matches_expected") or args.dry_run for item in results) else 2


if __name__ == "__main__":
    raise SystemExit(main())
