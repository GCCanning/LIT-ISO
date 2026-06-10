param(
    [string]$TrainingRoot = "C:\Projects\LoRA-Training",
    [string]$Dataset = "C:\Projects\Pixel Pipeline\style_examples\freepixel_web_download",
    [string]$OutputName = "litiso_tile_prop_v1",
    [string]$Checkpoint = "C:\Projects\ComfyUI\models\checkpoints\DreamShaper_8_pruned.safetensors",
    [string]$Category = "",
    [int]$MaxSteps = 3000,
    [int]$TrainLimit = 5000,
    [int]$SaveEvery = 250,
    [switch]$ResumeLatest,
    [switch]$DryRun
)

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$python = Join-Path $TrainingRoot ".venv\Scripts\python.exe"
$trainer = Join-Path $projectRoot "Tools\LoRA\train_litiso_lora_resumable.py"
$dryRunRoot = Join-Path $projectRoot "Temp\LoRA"
$outputDir = if ($DryRun.IsPresent) { Join-Path $dryRunRoot "outputs\$OutputName" } else { Join-Path $TrainingRoot "outputs\$OutputName" }
$controlDir = if ($DryRun.IsPresent) { Join-Path $dryRunRoot "control\$OutputName" } else { Join-Path $TrainingRoot "control\$OutputName" }
$logDir = if ($DryRun.IsPresent) { Join-Path $dryRunRoot "logs" } else { Join-Path $TrainingRoot "logs" }
New-Item -ItemType Directory -Force -Path $outputDir, $controlDir, $logDir | Out-Null

$args = @(
    $trainer,
    "--pretrained_model", $Checkpoint,
    "--dataset", $Dataset,
    "--output_dir", $outputDir,
    "--output_name", $OutputName,
    "--control_dir", $controlDir,
    "--max_steps", $MaxSteps,
    "--train_limit", $TrainLimit,
    "--save_every", $SaveEvery,
    "--rank", 32,
    "--learning_rate", "0.00004",
    "--batch_size", 1,
    "--resolution", 512,
    "--force_float32"
)

if ($Category) {
    $args += @("--category", $Category)
}

if ($ResumeLatest.IsPresent) {
    $latest = Get-ChildItem -Path $outputDir -Filter "*.safetensors" -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($latest) {
        $step = 0
        if ($latest.BaseName -match "step(\d+)") {
            $step = [int]$Matches[1]
        }
    $args += @("--resume_lora", $latest.FullName, "--resume_step", $step)
    }
}

$dryRunManifestPath = Join-Path $projectRoot "Temp\LoRA\$OutputName.launch_manifest.json"
$manifestPath = if ($DryRun.IsPresent) { $dryRunManifestPath } else { Join-Path $controlDir "launch_manifest.json" }
$manifest = [ordered]@{
    output_name = $OutputName
    output_dir = $outputDir
    control_dir = $controlDir
    dataset = $Dataset
    checkpoint = $Checkpoint
    category = $Category
    max_steps = $MaxSteps
    train_limit = $TrainLimit
    save_every = $SaveEvery
    command = @($python) + $args
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -Path $manifestPath -Encoding UTF8

if ($DryRun.IsPresent) {
    Get-Content $manifestPath
    return
}

function Quote-Arg {
    param([object]$Value)
    $text = [string]$Value
    if ($text -match '[\s"]') {
        return '"' + ($text -replace '"', '\"') + '"'
    }
    return $text
}

$logPath = Join-Path $logDir "$OutputName.log"
$errPath = Join-Path $logDir "$OutputName.err.log"
$argumentLine = ($args | ForEach-Object { Quote-Arg $_ }) -join " "
$proc = Start-Process -WindowStyle Hidden -FilePath $python -ArgumentList $argumentLine -RedirectStandardOutput $logPath -RedirectStandardError $errPath -PassThru

[ordered]@{
    pid = $proc.Id
    output_name = $OutputName
    status = Join-Path $controlDir "status.json"
    pause_command = "powershell -NoProfile -ExecutionPolicy Bypass -File `"$projectRoot\Tools\LoRA\pause_litiso_training.ps1`" -OutputName $OutputName"
    resume_command = "powershell -NoProfile -ExecutionPolicy Bypass -File `"$projectRoot\Tools\LoRA\start_resumable_litiso_training.ps1`" -OutputName $OutputName -ResumeLatest"
    log = $logPath
    errors = $errPath
} | ConvertTo-Json
