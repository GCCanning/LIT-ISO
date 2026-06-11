param(
    [string]$ProjectRoot = "",
    [string]$TrainingRoot = "C:\Projects\LoRA-Training",
    [string]$ComfyRoot = "C:\Projects\ComfyUI",
    [string]$OutputName = "litiso_reference32_clean_tile_geometry_v1",
    [string]$ComfyUrl = "http://127.0.0.1:8188",
    [string]$CheckpointName = "DreamShaper_8_pruned.safetensors",
    [double[]]$Strengths = @(0.35, 0.50, 0.65, 0.80),
    [switch]$AllowRunningCheckpoint,
    [switch]$ForceDuringTraining,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

$matrixScript = Join-Path $ProjectRoot "Tools\LoRA\evaluate_iso_reference_tile_style_matrix.ps1"
$outputRoot = Join-Path $ProjectRoot "Temp\LoRA\Evals\$OutputName"
$scoreRoot = Join-Path $outputRoot "_Scores"
$selectedRoot = Join-Path $ProjectRoot "Assets\Generated\_Review\reference32_clean_tile_eval_selected_family_v1"

$args = @{
    ProjectRoot = $ProjectRoot
    TrainingRoot = $TrainingRoot
    ComfyRoot = $ComfyRoot
    OutputName = $OutputName
    ComfyUrl = $ComfyUrl
    CheckpointName = $CheckpointName
    Strengths = $Strengths
    BlockOnTraining = @($OutputName, "litiso_iso_reference_critter_style_v1")
    OutputRoot = $outputRoot
    ScoreRoot = $scoreRoot
    SelectedFamilyRoot = $selectedRoot
}
if ($AllowRunningCheckpoint.IsPresent) { $args.AllowRunningCheckpoint = $true }
if ($ForceDuringTraining.IsPresent) { $args.ForceDuringTraining = $true }
if ($DryRun.IsPresent) { $args.DryRun = $true }

& $matrixScript @args
