param(
    [string]$PythonExe = "C:\Users\garyc\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe",
    [string[]]$Directions = @("NE", "NW", "SE", "SW"),
    [int]$BatchCount = 4,
    [int]$Seed = 129800,
    [double]$StyleWeight = 0.36,
    [double]$ControlStrength = 0.84,
    [double]$TemplateDenoise = 0.34,
    [switch]$Replace,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$script = Join-Path $projectRoot "Tools\AssetForge\queue_black_mage_iso_requests.py"
if (-not (Test-Path -LiteralPath $PythonExe)) {
    throw "Python executable not found: $PythonExe"
}

$args = @(
    $script,
    "--project-root", $projectRoot,
    "--variant-suffix", "v8",
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
