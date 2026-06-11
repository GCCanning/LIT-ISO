param(
    [string]$ProjectRoot = "",
    [string]$TrainingRoot = "C:\Projects\LoRA-Training",
    [string]$OutputName = "litiso_reference32_clean_tile_geometry_v1",
    [switch]$AllowRunningCheckpoint,
    [switch]$ForceDuringTraining,
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
$evalScript = Join-Path $ProjectRoot "Tools\LoRA\evaluate_reference32_clean_tile_lora_matrix.ps1"
$summaryPath = Join-Path $ProjectRoot "Temp\LoRA\$OutputName.post_training_review_plan.json"
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $summaryPath) | Out-Null

$status = & powershell -NoProfile -ExecutionPolicy Bypass -File $statusScript -TrainingRoot $TrainingRoot -OutputName $OutputName -Json | ConvertFrom-Json
$isRunning = [string]$status.health -eq "active_process" -or [string]$status.state -eq "running" -or [string]$status.state -eq "loading"
$isComplete = [string]$status.health -eq "complete" -or [string]$status.state -eq "complete"
$canRunEval = $isComplete -or $AllowRunningCheckpoint.IsPresent -or $ForceDuringTraining.IsPresent

$evalArgs = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", $evalScript,
    "-ProjectRoot", $ProjectRoot,
    "-TrainingRoot", $TrainingRoot,
    "-OutputName", $OutputName
)
if ($AllowRunningCheckpoint.IsPresent) { $evalArgs += "-AllowRunningCheckpoint" }
if ($ForceDuringTraining.IsPresent) { $evalArgs += "-ForceDuringTraining" }
if ($DryRun.IsPresent) { $evalArgs += "-DryRun" }

$plan = [ordered]@{
    schema = "lit_iso.lora.reference32_post_training_review.v1"
    created_utc = (Get-Date).ToUniversalTime().ToString("o")
    output_name = $OutputName
    dry_run = $DryRun.IsPresent
    status = $status
    is_running = $isRunning
    is_complete = $isComplete
    can_run_eval = $canRunEval
    eval_command = @("powershell") + $evalArgs
    expected_eval_plan = Convert-ToRepoPath (Join-Path $ProjectRoot "Temp\LoRA\$OutputName.eval_matrix_plan.json")
    selected_review_root = "Assets/Generated/_Review/reference32_clean_tile_eval_selected_family_v1"
    next_step = if ($isRunning -and -not $canRunEval) {
        "Training is still active. Wait for completion before running live eval, or use -AllowRunningCheckpoint intentionally."
    } elseif ($canRunEval) {
        "Run evaluation now."
    } else {
        "No complete checkpoint is ready yet."
    }
}
$plan | ConvertTo-Json -Depth 12 | Set-Content -Path $summaryPath -Encoding UTF8

if ($DryRun.IsPresent) {
    [ordered]@{
        ok = $true
        mode = "dry_run"
        can_run_eval = $canRunEval
        is_running = $isRunning
        is_complete = $isComplete
        summary = Convert-ToRepoPath $summaryPath
    } | ConvertTo-Json -Depth 8
    return
}

if (-not $canRunEval) {
    [ordered]@{
        ok = $true
        mode = "waiting"
        can_run_eval = $false
        is_running = $isRunning
        is_complete = $isComplete
        summary = Convert-ToRepoPath $summaryPath
        message = $plan.next_step
    } | ConvertTo-Json -Depth 8
    return
}

& powershell @evalArgs
