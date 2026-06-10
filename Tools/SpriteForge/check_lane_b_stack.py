#!/usr/bin/env python3
"""SpriteForge P3 Lane B stack checker.

Lane B is the pose-driven video path (One-to-All / WanVideoWrapper). This
script deliberately separates three states that are easy to confuse:

1. node repo/dependencies installed;
2. ComfyUI restarted and exposing the node classes;
3. large Wan model files installed.

The report is written under Tools/SpriteForge/out/lane_b by default so the P3
gate has an auditable artifact without importing anything into Unity.
"""
from __future__ import annotations

import argparse
import importlib.util
import json
import os
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


REQUIRED_MODULES = {
    "ftfy": "ftfy",
    "accelerate": "accelerate",
    "einops": "einops",
    "diffusers": "diffusers",
    "peft": "peft",
    "sentencepiece": "sentencepiece",
    "protobuf": "google.protobuf",
    "pyloudnorm": "pyloudnorm",
    "gguf": "gguf",
    "opencv-python": "cv2",
    "scipy": "scipy",
}

REQUIRED_NODES = [
    "WanVideoModelLoader",
    "WanVideoVAELoader",
    "LoadWanVideoT5TextEncoder",
    "WanVideoTextEncode",
    "WanVideoSampler",
    "WanVideoDecode",
    "WanVideoAddOneToAllPoseEmbeds",
    "WanVideoAddOneToAllReferenceEmbeds",
]

MODEL_BUCKETS = {
    "text_encoders": "T5/text encoder for Wan prompts",
    "diffusion_models": "Wan transformer / diffusion model",
    "vae": "Wan VAE",
    "clip_vision": "CLIP vision/reference encoder",
}

PREFERRED_WAN22_5B_FASTWAN_FILES = {
    "text_encoder": {
        "bucket": "text_encoders",
        "name": "umt5-xxl-enc-bf16.safetensors",
        "expected_size": 11_361_845_464,
        "source_url": "https://huggingface.co/Kijai/WanVideo_comfy/blob/main/umt5-xxl-enc-bf16.safetensors",
    },
    "diffusion_model": {
        "bucket": "diffusion_models",
        "name": "Wan2_2-TI2V-5B-FastWanFullAttn_bf16.safetensors",
        "expected_size": 9_999_659_744,
        "source_url": "https://huggingface.co/Kijai/WanVideo_comfy/blob/main/FastWan/Wan2_2-TI2V-5B-FastWanFullAttn_bf16.safetensors",
    },
    "vae": {
        "bucket": "vae",
        "name": "Wan2_2_VAE_bf16.safetensors",
        "expected_size": 1_409_401_152,
        "source_url": "https://huggingface.co/Kijai/WanVideo_comfy/blob/main/Wan2_2_VAE_bf16.safetensors",
    },
}

PLACEHOLDER_PREFIXES = ("put_", "README", ".gitkeep")
MODEL_EXTENSIONS = {".safetensors", ".ckpt", ".pt", ".pth", ".bin", ".gguf"}


def now_utc() -> str:
    return datetime.now(timezone.utc).isoformat()


def module_status() -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}
    for package, module_name in REQUIRED_MODULES.items():
        result[package] = {
            "module": module_name,
            "available": importlib.util.find_spec(module_name) is not None,
        }
    return result


def api_json(url: str, timeout: float = 4.0) -> tuple[dict[str, Any] | None, str]:
    try:
        with urllib.request.urlopen(url, timeout=timeout) as response:
            return json.loads(response.read().decode("utf-8")), ""
    except (urllib.error.URLError, TimeoutError, json.JSONDecodeError, OSError) as exc:
        return None, str(exc)


def model_files(root: Path, bucket: str) -> list[dict[str, Any]]:
    folder = root / "models" / bucket
    if not folder.exists():
        return []
    hits: list[dict[str, Any]] = []
    for path in folder.rglob("*"):
        if not path.is_file():
            continue
        if path.name.startswith(PLACEHOLDER_PREFIXES):
            continue
        if path.suffix.lower() not in MODEL_EXTENSIONS:
            continue
        hits.append({
            "path": str(path),
            "name": path.name,
            "size_mb": round(path.stat().st_size / (1024 * 1024), 2),
        })
    hits.sort(key=lambda item: item["path"])
    return hits


def preferred_wan22_status(root: Path) -> dict[str, Any]:
    files: dict[str, Any] = {}
    missing: list[str] = []
    wrong_size: list[str] = []
    for role, data in PREFERRED_WAN22_5B_FASTWAN_FILES.items():
        path = root / "models" / str(data["bucket"]) / str(data["name"])
        exists = path.exists()
        size = path.stat().st_size if exists else 0
        expected_size = int(data["expected_size"])
        status = "ok" if exists and size == expected_size else "missing"
        if exists and size != expected_size:
            status = "wrong_size"
            wrong_size.append(role)
        elif not exists:
            missing.append(role)
        files[role] = {
            **data,
            "path": str(path),
            "exists": exists,
            "size": size,
            "size_mb": round(size / (1024 * 1024), 2) if exists else 0,
            "status": status,
        }
    return {
        "model_set": "wan22_5b_fastwan_bf16",
        "files": files,
        "missing": missing,
        "wrong_size": wrong_size,
        "ready": not missing and not wrong_size,
    }


def build_report(
    project_root: Path,
    comfy_root: Path,
    comfy_url: str,
    report_path: Path | None = None,
) -> dict[str, Any]:
    wrapper_dir = comfy_root / "custom_nodes" / "ComfyUI-WanVideoWrapper"
    upstream_one_to_all = wrapper_dir / "example_workflows" / "wanvideo_2_1_14B_OneToAllAnimation_pose_control_example_01.json"
    local_one_to_all = project_root / "Tools" / "SpriteForge" / "workflows" / "one_to_all_pose_i2v.json"
    local_wan22 = project_root / "Tools" / "SpriteForge" / "workflows" / "wan22_i2v_pose.json"

    deps = module_status()
    missing_deps = [name for name, data in deps.items() if not data["available"]]
    system_stats, system_error = api_json(f"{comfy_url.rstrip('/')}/system_stats")
    object_info, object_error = api_json(f"{comfy_url.rstrip('/')}/object_info")
    object_classes = set(object_info.keys()) if isinstance(object_info, dict) else set()
    missing_nodes = [node for node in REQUIRED_NODES if node not in object_classes]
    models = {
        bucket: {
            "purpose": purpose,
            "folder": str(comfy_root / "models" / bucket),
            "files": model_files(comfy_root, bucket),
        }
        for bucket, purpose in MODEL_BUCKETS.items()
    }
    missing_model_buckets = [
        bucket for bucket in ("text_encoders", "diffusion_models", "vae")
        if not models[bucket]["files"]
    ]
    preferred_models = preferred_wan22_status(comfy_root)
    missing_preferred_models = preferred_models["missing"] + preferred_models["wrong_size"]

    vram_total_gb = None
    vram_free_gb = None
    if system_stats and system_stats.get("devices"):
        device = system_stats["devices"][0]
        vram_total_gb = round(float(device.get("vram_total", 0)) / (1024 ** 3), 2)
        vram_free_gb = round(float(device.get("vram_free", 0)) / (1024 ** 3), 2)

    if not wrapper_dir.exists():
        status = "blocked_missing_node_repo"
    elif missing_deps:
        status = "blocked_missing_python_dependencies"
    elif object_info is None:
        status = "installed_comfy_not_reachable"
    elif missing_nodes and missing_preferred_models:
        status = "installed_restart_required_missing_preferred_models"
    elif missing_nodes:
        status = "installed_restart_required"
    elif missing_preferred_models:
        status = "installed_missing_preferred_models"
    else:
        status = "ready_low_vram_risk" if (vram_total_gb or 0) < 12 else "ready"

    report: dict[str, Any] = {
        "schema": "lit-iso.spriteforge.lane-b-stack-report.v1",
        "created_utc": now_utc(),
        "status": status,
        "project_root": str(project_root),
        "comfy_root": str(comfy_root),
        "comfy_url": comfy_url,
        "node_repo": {
            "path": str(wrapper_dir),
            "exists": wrapper_dir.exists(),
            "requirements": str(wrapper_dir / "requirements.txt"),
            "upstream_one_to_all_example": str(upstream_one_to_all),
            "upstream_one_to_all_example_exists": upstream_one_to_all.exists(),
        },
        "python_dependencies": deps,
        "missing_python_dependencies": missing_deps,
        "comfy": {
            "reachable": system_stats is not None,
            "system_error": system_error,
            "object_info_reachable": object_info is not None,
            "object_error": object_error,
            "vram_total_gb": vram_total_gb,
            "vram_free_gb": vram_free_gb,
            "argv": (system_stats or {}).get("system", {}).get("argv", []),
            "missing_required_nodes": missing_nodes,
            "required_nodes": REQUIRED_NODES,
        },
        "models": models,
        "missing_model_buckets": missing_model_buckets,
        "preferred_wan22_5b_fastwan": preferred_models,
        "local_workflows": {
            "one_to_all_pose_i2v": {
                "path": str(local_one_to_all),
                "exists": local_one_to_all.exists(),
            },
            "wan22_i2v_pose": {
                "path": str(local_wan22),
                "exists": local_wan22.exists(),
            },
        },
        "gate_notes": [
            "P3 is installed only when node repo and Python dependencies are present.",
            "Live generation additionally requires ComfyUI restart, Wan model files, and enough VRAM.",
            "On this 8GB laptop, One-to-All 14B is expected to be a high-risk or blocked live render path; use lane A below 96px unless a smaller/quantized Wan stack is installed.",
        ],
    }

    if report_path is not None:
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    return report


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Check SpriteForge Lane B / WanVideoWrapper stack.")
    parser.add_argument("--project-root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--comfy-root", type=Path, default=Path("C:/Projects/ComfyUI"))
    parser.add_argument("--comfy-url", default=os.environ.get("COMFYUI_URL", "http://127.0.0.1:8188"))
    parser.add_argument("--report", type=Path, default=None)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    project_root = args.project_root.resolve()
    report_path = args.report
    if report_path is None:
        report_path = project_root / "Tools" / "SpriteForge" / "out" / "lane_b" / "p3_stack_report.json"
    elif not report_path.is_absolute():
        report_path = project_root / report_path
    report = build_report(project_root, args.comfy_root.resolve(), args.comfy_url, report_path)
    print(json.dumps({"status": report["status"], "report": str(report_path)}, indent=2))
    return 0 if not report["status"].startswith("blocked_missing") else 1


if __name__ == "__main__":
    raise SystemExit(main())
