param(
    [string]$TrainingRoot = "C:\Projects\LoRA-Training",
    [string]$Dataset = "",
    [string]$OutputName = "litiso_reference32_clean_tile_geometry_v1",
    [string]$Checkpoint = "C:\Projects\ComfyUI\models\checkpoints\DreamShaper_8_pruned.safetensors",
    [int]$MaxSteps = 1000,
    [int]$TrainLimit = 256,
    [int]$SaveEvery = 100,
    [string]$PythonExe = "C:\Users\garyc\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe",
    [switch]$ResumeLatest,
    [switch]$RebuildSeedPack,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$seedPackScript = Join-Path $projectRoot "Tools\AssetForge\build_reference32_training_seed_pack.py"
$defaultDataset = Join-Path $projectRoot "Assets\Generated\_Review\reference32_training_seed_pack_v1\training_dataset"
$datasetPath = if ($Dataset) { $Dataset } else { $defaultDataset }
$launcher = Join-Path $projectRoot "Tools\LoRA\start_resumable_litiso_training.ps1"

if ($RebuildSeedPack.IsPresent) {
    if (-not (Test-Path -LiteralPath $PythonExe)) {
        throw "Python executable not found: $PythonExe"
    }
    & $PythonExe $seedPackScript --project-root $projectRoot
}

$metadataPath = Join-Path $datasetPath "metadata.jsonl"
$readinessPath = Join-Path $datasetPath "dataset_readiness_summary.json"
if (-not (Test-Path -LiteralPath $metadataPath)) {
    throw "Reference32 training dataset is not ready. Run: powershell -NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -RebuildSeedPack -DryRun"
}

$recordCount = @(Get-Content -LiteralPath $metadataPath -ErrorAction Stop).Count
if ($recordCount -lt 20) {
    throw "Reference32 dataset has only $recordCount records. Expected at least 20 clean terrain records."
}

$args = @{
    TrainingRoot = $TrainingRoot
    Dataset = $datasetPath
    OutputName = $OutputName
    Checkpoint = $Checkpoint
    MaxSteps = $MaxSteps
    TrainLimit = $TrainLimit
    SaveEvery = $SaveEvery
    Category = "tile"
}
if ($ResumeLatest.IsPresent) { $args.ResumeLatest = $true }
if ($DryRun.IsPresent) { $args.DryRun = $true }

$result = & $launcher @args

$localManifestPath = Join-Path $projectRoot "Temp\LoRA\$OutputName.reference32_dataset_manifest.json"
$payload = [ordered]@{
    schema = "lit_iso.lora.reference32_clean_tile_training_launcher.v1"
    output_name = $OutputName
    dry_run = $DryRun.IsPresent
    dataset = $datasetPath
    metadata_jsonl = $metadataPath
    readiness_summary = if (Test-Path -LiteralPath $readinessPath) { $readinessPath } else { $null }
    record_count = $recordCount
    checkpoint = $Checkpoint
    max_steps = $MaxSteps
    train_limit = $TrainLimit
    save_every = $SaveEvery
    category = "tile"
    warning = "Review/training-prep dataset only. Do not ship or import generated outputs without explicit approval and license review."
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $localManifestPath) | Out-Null
$payload | ConvertTo-Json -Depth 8 | Set-Content -Path $localManifestPath -Encoding UTF8

$result
Write-Output "Reference32 launcher manifest: $localManifestPath"
