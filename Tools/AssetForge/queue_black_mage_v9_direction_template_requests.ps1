param(
    [string]$PythonExe = "C:\Users\garyc\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe",
    [string[]]$Directions = @("NE", "NW", "SE", "SW"),
    [int]$BatchCount = 4,
    [int]$Seed = 130900,
    [string]$VariantSuffix = "v9",
    [double]$StyleWeight = 0.42,
    [double]$ControlStrength = 0.78,
    [double]$TemplateDenoise = 0.44,
    [switch]$Replace,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$script = Join-Path $projectRoot "Tools\AssetForge\queue_black_mage_iso_requests.py"
$templateManifest = Join-Path $projectRoot "Assets\Generated\_Review\black_mage_direction_templates_v2\black_mage_direction_templates_v2_manifest.json"
if (-not (Test-Path -LiteralPath $PythonExe)) {
    throw "Python executable not found: $PythonExe"
}
if (-not (Test-Path -LiteralPath $templateManifest)) {
    throw "Missing v2 direction template manifest. Run Tools\AssetForge\build_black_mage_direction_templates_v2.py first."
}

$args = @(
    $script,
    "--project-root", $projectRoot,
    "--scaffold-manifest", $templateManifest,
    "--variant-suffix", $VariantSuffix,
    "--batch-count", $BatchCount,
    "--seed", $Seed,
    "--style-weight", $StyleWeight,
    "--control-strength", $ControlStrength,
    "--template-denoise", $TemplateDenoise,
    "--use-scaffold-template",
    "--strict-sprite-contract",
    "--directions"
) + $Directions

if ($Replace.IsPresent) { $args += "--replace" }
if ($DryRun.IsPresent) { $args += "--dry-run" }

& $PythonExe @args
