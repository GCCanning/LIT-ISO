param(
    [string]$Dataset = "C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\lpc_motion_male_female_v1",
    [string]$OutputDir = "C:\Projects\Pixel Pipeline\generated\lpc_direction_oracle_v1",
    [int]$FrameIndex = 3
)

$ErrorActionPreference = "Stop"

$python = "python"
$trainingPython = "C:\Projects\LoRA-Training\.venv\Scripts\python.exe"
if (Test-Path -LiteralPath $trainingPython) {
    $python = $trainingPython
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$script = Join-Path $projectRoot "Tools\AssetForge\build_lpc_direction_oracle.py"

& $python $script --dataset $Dataset --out-dir $OutputDir --frame-index $FrameIndex
if ($LASTEXITCODE -ne 0) {
    throw "LPC direction oracle build failed with exit code $LASTEXITCODE"
}
