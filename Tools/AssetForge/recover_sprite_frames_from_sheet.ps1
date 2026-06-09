param(
    [Parameter(Mandatory = $true)]
    [string]$InputDir,

    [Parameter(Mandatory = $true)]
    [string]$OutputDir,

    [string]$SourceManifest = "",
    [int]$CellSize = 64,
    [double]$Threshold = 34,
    [int]$Limit = 0
)

$ErrorActionPreference = "Stop"

$python = "python"
$trainingPython = "C:\Projects\LoRA-Training\.venv\Scripts\python.exe"
if (Test-Path -LiteralPath $trainingPython) {
    $python = $trainingPython
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$script = Join-Path $projectRoot "Tools\AssetForge\recover_sprite_frames_from_sheet.py"

$args = @(
    $script,
    "--input-dir", $InputDir,
    "--out-dir", $OutputDir,
    "--cell-size", ([string]$CellSize),
    "--threshold", ([string]$Threshold)
)

if (-not [string]::IsNullOrWhiteSpace($SourceManifest)) {
    $args += @("--source-manifest", $SourceManifest)
}

& $python @args
if ($LASTEXITCODE -ne 0) {
    throw "Sprite frame recovery failed with exit code $LASTEXITCODE"
}
