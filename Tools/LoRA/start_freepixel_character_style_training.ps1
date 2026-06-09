param(
    [string]$TrainingRoot = "C:\Projects\LoRA-Training",
    [string]$Dataset = "C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\freepixel_characters_v1",
    [string]$OutputName = "litiso_freepixel_character_style_v1",
    [string]$Checkpoint = "C:\Projects\ComfyUI\models\checkpoints\DreamShaper_8_pruned.safetensors",
    [int]$MaxSteps = 1200,
    [int]$TrainLimit = 512,
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

if ($ResumeLatest.IsPresent) {
    $args.ResumeLatest = $true
}
if ($DryRun.IsPresent) {
    $args.DryRun = $true
}

& $launcher @args
