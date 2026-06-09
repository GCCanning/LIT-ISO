param(
    [string]$TrainingRoot = "C:\Projects\LoRA-Training",
    [string]$Dataset = "C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\lpc_motion_male_female_v1",
    [string]$OutputName = "litiso_lpc_motion_template_v1",
    [string]$Checkpoint = "C:\Projects\ComfyUI\models\checkpoints\DreamShaper_8_pruned.safetensors",
    [int]$MaxSteps = 1200,
    [int]$TrainLimit = 2008,
    [int]$SaveEvery = 300,
    [switch]$ResumeLatest,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$launcher = Join-Path $projectRoot "Tools\LoRA\start_resumable_litiso_training.ps1"

$args = @{
    TrainingRoot = $TrainingRoot
    Dataset = $Dataset
    OutputName = $OutputName
    Checkpoint = $Checkpoint
    Category = "lpc_motion_template"
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
