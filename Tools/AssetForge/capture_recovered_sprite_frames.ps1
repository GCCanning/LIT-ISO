param(
    [Parameter(Mandatory = $true)]
    [string]$RecoveredPack,

    [string]$OutputDataset = "C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\recovered_motion_candidates_v1",
    [string]$Decisions = "",
    [int]$ValidationEvery = 8,
    [switch]$IncludeRejected,
    [switch]$Replace
)

$ErrorActionPreference = "Stop"

$python = "python"
$trainingPython = "C:\Projects\LoRA-Training\.venv\Scripts\python.exe"
if (Test-Path -LiteralPath $trainingPython) {
    $python = $trainingPython
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$script = Join-Path $projectRoot "Tools\AssetForge\capture_recovered_sprite_frames.py"
$args = @(
    $script,
    "--recovered-pack", $RecoveredPack,
    "--out-dataset", $OutputDataset,
    "--validation-every", ([string]$ValidationEvery)
)

if (-not [string]::IsNullOrWhiteSpace($Decisions)) {
    $args += @("--decisions", $Decisions)
}
if ($IncludeRejected.IsPresent) {
    $args += "--include-rejected"
}
if ($Replace.IsPresent) {
    $args += "--replace"
}

& $python @args
if ($LASTEXITCODE -ne 0) {
    throw "Recovered sprite frame capture failed with exit code $LASTEXITCODE"
}
