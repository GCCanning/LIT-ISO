param(
    [string]$TrainingRoot = "C:\Projects\LoRA-Training",
    [string]$Dataset = "C:\Projects\Pixel Pipeline\datasets\lit_iso\style_lock\iso_reference_v1",
    [string]$OutputName = "litiso_iso_reference_tile_style_v1",
    [string]$Checkpoint = "C:\Projects\ComfyUI\models\checkpoints\DreamShaper_8_pruned.safetensors",
    [int]$MaxSteps = 1000,
    [int]$TrainLimit = 512,
    [int]$SaveEvery = 100,
    [switch]$ResumeLatest,
    [switch]$DryRun
)

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$launcher = Join-Path $projectRoot "Tools\LoRA\start_resumable_litiso_training.ps1"

$args = @{
    TrainingRoot = $TrainingRoot
    Dataset = $Dataset
    OutputName = $OutputName
    Checkpoint = $Checkpoint
    MaxSteps = $MaxSteps
    TrainLimit = $TrainLimit
    SaveEvery = $SaveEvery
    Category = "tile"
}
if ($ResumeLatest.IsPresent) { $args.ResumeLatest = $true }
if ($DryRun.IsPresent) { $args.DryRun = $true }

& $launcher @args
