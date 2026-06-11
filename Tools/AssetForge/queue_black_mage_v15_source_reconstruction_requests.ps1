param(
    [string]$PythonExe = "C:\Projects\ComfyUI\.venv\Scripts\python.exe",
    [int]$BatchCount = 4,
    [int]$Seed = 133800,
    [string]$VariantSuffix = "v15_source_recon",
    [double]$StyleWeight = 0.42,
    [double]$StyleEndAt = 0.62,
    [double]$TemplateDenoise = 0.16,
    [double]$LoraStrength = 0.16,
    [int]$Steps = 24,
    [double]$Cfg = 3.8,
    [switch]$Replace,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$queueScript = Join-Path $projectRoot "Tools\AssetForge\queue_black_mage_iso_requests.py"
$poseManifest = Join-Path $projectRoot "Assets\Generated\_Review\_PoseControls\litiso_openpose_8d_v1\idle_manifest.json"

if (-not (Test-Path -LiteralPath $PythonExe)) {
    throw "Python executable not found: $PythonExe"
}
if (-not (Test-Path -LiteralPath $poseManifest)) {
    throw "Missing 8D OpenPose manifest. It is still passed for metadata, but ControlNet is disabled in this v15 reconstruction path."
}

$args = @(
    $queueScript,
    "--project-root", $projectRoot,
    "--pose-manifest", $poseManifest,
    "--variant-suffix", $VariantSuffix,
    "--batch-count", $BatchCount,
    "--seed", $Seed,
    "--style-weight", $StyleWeight,
    "--style-end-at", $StyleEndAt,
    "--template-denoise", $TemplateDenoise,
    "--lora-strength", $LoraStrength,
    "--steps", $Steps,
    "--cfg", $Cfg,
    "--use-reference-template",
    "--disable-control",
    "--strict-sprite-contract",
    "--directions", "S"
)

if ($Replace.IsPresent) { $args += "--replace" }
if ($DryRun.IsPresent) { $args += "--dry-run" }

& $PythonExe @args
