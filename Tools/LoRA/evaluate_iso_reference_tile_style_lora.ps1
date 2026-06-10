param(
    [string]$ProjectRoot = "",
    [string]$TrainingRoot = "C:\Projects\LoRA-Training",
    [string]$ComfyRoot = "C:\Projects\ComfyUI",
    [string]$OutputName = "litiso_iso_reference_tile_style_v1",
    [string]$ComfyUrl = "http://127.0.0.1:8188",
    [string]$CheckpointName = "DreamShaper_8_pruned.safetensors",
    [double]$LoraStrength = 0.72,
    [string]$OutputDir = "",
    [switch]$AllowRunningCheckpoint,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

function Convert-ToRepoPath {
    param([string]$Path)
    $root = (Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd("\", "/")
    $full = [IO.Path]::GetFullPath($Path)
    if ($full.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        return $full.Substring($root.Length + 1).Replace("\", "/")
    }
    return $full.Replace("\", "/")
}

$statusScript = Join-Path $ProjectRoot "Tools\LoRA\status_litiso_training.ps1"
$syncScript = Join-Path $ProjectRoot "Tools\LoRA\sync_lora_to_comfyui.ps1"
$evalScript = Join-Path $ProjectRoot "Tools\LoRA\eval_litiso_tile_prop_v1_comfy.py"
$contactScript = Join-Path $ProjectRoot "Tools\LoRA\build_lora_eval_contact_sheet.py"
$summaryPath = Join-Path $ProjectRoot "Temp\LoRA\$OutputName.post_training_eval_plan.json"
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $summaryPath) | Out-Null

$status = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $statusScript -TrainingRoot $TrainingRoot -OutputName $OutputName -Json | ConvertFrom-Json
$latestCheckpoint = if ($status.latest_checkpoint -and $status.latest_checkpoint.path) { [string]$status.latest_checkpoint.path } else { "" }
if ([string]::IsNullOrWhiteSpace($latestCheckpoint) -or -not (Test-Path -LiteralPath $latestCheckpoint)) {
    throw "No checkpoint found for $OutputName. Status summary: $summaryPath"
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path "C:\Projects\Pixel Pipeline\generated" "$OutputName`_post_training_eval"
}

$trainingComplete = [string]$status.health -eq "complete" -or [string]$status.state -eq "complete"
$canEvaluate = $trainingComplete -or $AllowRunningCheckpoint.IsPresent
$python = "C:\Projects\LoRA-Training\.venv\Scripts\python.exe"
if (-not (Test-Path -LiteralPath $python)) {
    $python = "C:\Projects\ComfyUI\.venv\Scripts\python.exe"
}

$syncDryRun = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $syncScript -TrainingRoot $TrainingRoot -ComfyRoot $ComfyRoot -OutputName $OutputName -CheckpointPath $latestCheckpoint -DryRun | ConvertFrom-Json
$plannedLoraName = [IO.Path]::GetFileName([string]$syncDryRun.destination)
$evalCommand = @(
    $python,
    $evalScript,
    "--comfy-url", $ComfyUrl,
    "--out-dir", $OutputDir,
    "--checkpoint", $CheckpointName,
    "--lora", $plannedLoraName,
    "--lora-strength", ([string]$LoraStrength)
)
$contactCommand = @(
    $python,
    $contactScript,
    "--project-root", $ProjectRoot,
    "--eval-dir", $OutputDir,
    "--title", "$OutputName post-training tile eval"
)

$plan = [ordered]@{
    schema = "lit_iso.lora.iso_reference_tile_style_eval.v1"
    created_utc = (Get-Date).ToUniversalTime().ToString("o")
    output_name = $OutputName
    dry_run = [bool]$DryRun
    can_evaluate_now = $canEvaluate
    reason = if ($canEvaluate) { "checkpoint ready for evaluation" } else { "training is still running; wait for completion or pass -AllowRunningCheckpoint for checkpoint sampling" }
    status = $status
    selected_checkpoint = $latestCheckpoint
    planned_lora_name = $plannedLoraName
    lora_strength = $LoraStrength
    checkpoint_name = $CheckpointName
    comfy_url = $ComfyUrl
    output_dir = $OutputDir
    sync_plan = $syncDryRun
    eval_command = $evalCommand
    contact_sheet_command = $contactCommand
    next_step = if ($DryRun.IsPresent) { "When training is complete, rerun without -DryRun to sync and evaluate." } else { "Review generated eval images and manifest before keeping this LoRA." }
}
$plan | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

if ($DryRun.IsPresent) {
    [ordered]@{
        ok = $true
        mode = "dry_run"
        can_evaluate_now = $canEvaluate
        selected_checkpoint = $latestCheckpoint
        lora = $plannedLoraName
        summary = Convert-ToRepoPath $summaryPath
    } | ConvertTo-Json -Depth 6
    return
}

if (-not $canEvaluate) {
    throw "Training is still running for $OutputName. Rerun after completion, or pass -AllowRunningCheckpoint to evaluate the latest checkpoint. Plan: $summaryPath"
}

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $syncScript -TrainingRoot $TrainingRoot -ComfyRoot $ComfyRoot -OutputName $OutputName -CheckpointPath $latestCheckpoint | Out-Host
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
& $python @($evalCommand | Select-Object -Skip 1)
$exitCode = $LASTEXITCODE
if ($exitCode -eq 0) {
    & $python @($contactCommand | Select-Object -Skip 1) | Out-Host
}

$result = [ordered]@{
    ok = $exitCode -eq 0
    exit_code = $exitCode
    output_name = $OutputName
    lora = $plannedLoraName
    selected_checkpoint = $latestCheckpoint
    output_dir = $OutputDir
    manifest = Join-Path $OutputDir "manifest.json"
    contact_sheet = Join-Path $OutputDir "contact_sheet.png"
    contact_sheet_manifest = Join-Path $OutputDir "contact_sheet_manifest.json"
    summary = $summaryPath
}
$result | ConvertTo-Json -Depth 8
if ($exitCode -ne 0) {
    throw "Tile LoRA evaluator failed with exit code $exitCode. Summary: $summaryPath"
}
