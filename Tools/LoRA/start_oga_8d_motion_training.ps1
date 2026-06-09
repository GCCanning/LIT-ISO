param(
    [string]$TrainingRoot = "C:\Projects\LoRA-Training",
    [string]$Dataset = "C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\oga_8d_motion_direction_v1\ready_training_subset",
    [string]$OutputName = "litiso_oga8d_motion_direction_v1",
    [string]$Checkpoint = "C:\Projects\ComfyUI\models\checkpoints\DreamShaper_8_pruned.safetensors",
    [int]$MaxSteps = 1000,
    [int]$TrainLimit = 488,
    [int]$SaveEvery = 200,
    [switch]$ResumeLatest,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$launcher = Join-Path $projectRoot "Tools\LoRA\start_resumable_litiso_training.ps1"

if (!(Test-Path -LiteralPath $launcher)) {
    throw "Missing shared resumable training launcher: $launcher"
}
if (!(Test-Path -LiteralPath (Join-Path $Dataset "metadata.jsonl"))) {
    throw "Missing OGA 8D metadata.jsonl. Build it first with Tools\AssetForge\build_oga_8d_training_index.ps1."
}

$argsList = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", $launcher,
    "-TrainingRoot", $TrainingRoot,
    "-Dataset", $Dataset,
    "-OutputName", $OutputName,
    "-Checkpoint", $Checkpoint,
    "-Category", "oga_8d_motion_direction",
    "-MaxSteps", $MaxSteps,
    "-TrainLimit", $TrainLimit,
    "-SaveEvery", $SaveEvery
)

if ($ResumeLatest) {
    $argsList += "-ResumeLatest"
}

if ($DryRun) {
    $argsList += "-DryRun"
}

& powershell @argsList
