param(
    [string]$ProjectRoot = "",
    [Parameter(Mandatory = $true)]
    [string]$InputPath,
    [string]$OutputRoot = "",
    [ValidateSet("auto", "tile", "prop", "item", "character", "npc", "mob")]
    [string]$Mode = "auto",
    [int]$Colors = -1,
    [int]$ScaleResult = 1,
    [int]$InitialUpscale = 2,
    [int]$PixelWidth = -1,
    [int]$MaxFiles = 0,
    [string]$TargetSize = "auto",
    [string]$ProperPixelArtRoot = "",
    [switch]$Transparent,
    [switch]$NoFitTarget,
    [switch]$SaveIntermediates,
    [switch]$FailMissing,
    [switch]$DryRun,
    [string]$PythonExe = "C:\Projects\ComfyUI\.venv\Scripts\python.exe"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}
if (-not (Test-Path -LiteralPath $PythonExe)) {
    throw "Python executable not found: $PythonExe"
}

$script = Join-Path $ProjectRoot "Tools\AssetForge\proper_pixel_art_cleanup.py"
if (-not (Test-Path -LiteralPath $script)) {
    throw "Missing Proper Pixel Art adapter: $script"
}

$args = @(
    "-B",
    $script,
    "--project-root", $ProjectRoot,
    "--input-path", $InputPath,
    "--mode", $Mode,
    "--scale-result", ([string]$ScaleResult),
    "--initial-upscale", ([string]$InitialUpscale),
    "--max-files", ([string]$MaxFiles),
    "--target-size", $TargetSize
)

if ($Colors -ge 0) {
    $args += @("--colors", ([string]$Colors))
}
if ($PixelWidth -ge 0) {
    $args += @("--pixel-width", ([string]$PixelWidth))
}
if (-not [string]::IsNullOrWhiteSpace($OutputRoot)) {
    $args += @("--output-root", $OutputRoot)
}
if (-not [string]::IsNullOrWhiteSpace($ProperPixelArtRoot)) {
    $args += @("--proper-pixel-art-root", $ProperPixelArtRoot)
}
if ($Transparent.IsPresent) { $args += "--transparent" }
if ($NoFitTarget.IsPresent) { $args += "--no-fit-target" }
if ($SaveIntermediates.IsPresent) { $args += "--save-intermediates" }
if ($FailMissing.IsPresent) { $args += "--fail-missing" }
if ($DryRun.IsPresent) { $args += "--dry-run" }

& $PythonExe @args
exit $LASTEXITCODE
