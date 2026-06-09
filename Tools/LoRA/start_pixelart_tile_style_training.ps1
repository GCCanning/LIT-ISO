param(
    [string]$TrainingRoot = "C:\Projects\LoRA-Training",
    [string]$Dataset = "C:\Projects\Pixel Pipeline\datasets\lit_iso\style_lock\pixelart_local_tile_geometry_v1",
    [string]$OutputName = "litiso_pixelart_tile_geometry_style_v1",
    [string]$Checkpoint = "C:\Projects\ComfyUI\models\checkpoints\DreamShaper_8_pruned.safetensors",
    [string]$Category = "",
    [int]$MaxSteps = 800,
    [int]$TrainLimit = 64,
    [int]$SaveEvery = 100,
    [switch]$ResumeLatest,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$launcher = Join-Path $projectRoot "Tools\LoRA\start_resumable_litiso_training.ps1"

if (-not [IO.Path]::IsPathRooted($Dataset)) {
    $Dataset = Join-Path $projectRoot $Dataset
}

$args = @{
    TrainingRoot = $TrainingRoot
    Dataset = $Dataset
    OutputName = $OutputName
    Checkpoint = $Checkpoint
    MaxSteps = $MaxSteps
    TrainLimit = $TrainLimit
    SaveEvery = $SaveEvery
}
if ($Category) {
    $args.Category = $Category
}

if ($ResumeLatest.IsPresent) {
    $args.ResumeLatest = $true
}
if ($DryRun.IsPresent) {
    $args.DryRun = $true
}

& $launcher @args
