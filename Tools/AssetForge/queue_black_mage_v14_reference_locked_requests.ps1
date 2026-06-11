param(
    [string]$PythonExe = "C:\Projects\ComfyUI\.venv\Scripts\python.exe",
    [string[]]$Directions = @("S"),
    [int]$BatchCount = 4,
    [int]$Seed = 133400,
    [string]$VariantSuffix = "v14_identity",
    [double]$StyleWeight = 0.74,
    [double]$StyleEndAt = 0.84,
    [double]$ControlStrength = 0.58,
    [double]$TemplateDenoise = 0.24,
    [double]$LoraStrength = 0.24,
    [int]$Steps = 34,
    [double]$Cfg = 4.8,
    [switch]$Replace,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$queueScript = Join-Path $projectRoot "Tools\AssetForge\queue_black_mage_iso_requests.py"
$templateManifest = Join-Path $projectRoot "Assets\Generated\_Review\black_mage_direction_templates_v3\black_mage_direction_templates_v3_manifest.json"
$poseManifest = Join-Path $projectRoot "Assets\Generated\_Review\_PoseControls\litiso_openpose_8d_v1\idle_manifest.json"

if (-not (Test-Path -LiteralPath $PythonExe)) {
    throw "Python executable not found: $PythonExe"
}
if (-not (Test-Path -LiteralPath $templateManifest)) {
    throw "Missing v3 direction template manifest. Run Tools\AssetForge\build_black_mage_direction_templates_v3.py first."
}
if (-not (Test-Path -LiteralPath $poseManifest)) {
    throw "Missing 8D OpenPose manifest. Run Tools\AssetForge\build_litiso_openpose_direction_library.py with all 8 canonical directions first."
}

$args = @(
    $queueScript,
    "--project-root", $projectRoot,
    "--pose-manifest", $poseManifest,
    "--scaffold-manifest", $templateManifest,
    "--variant-suffix", $VariantSuffix,
    "--batch-count", $BatchCount,
    "--seed", $Seed,
    "--style-weight", $StyleWeight,
    "--style-end-at", $StyleEndAt,
    "--control-strength", $ControlStrength,
    "--template-denoise", $TemplateDenoise,
    "--lora-strength", $LoraStrength,
    "--steps", $Steps,
    "--cfg", $Cfg,
    "--use-scaffold-template",
    "--strict-sprite-contract",
    "--directions"
) + $Directions

if ($Replace.IsPresent) { $args += "--replace" }
if ($DryRun.IsPresent) { $args += "--dry-run" }

& $PythonExe @args
