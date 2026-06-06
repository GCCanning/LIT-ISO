param(
    [string]$TrainingRoot = "C:\Projects\LoRA-Training",
    [string]$OutputName = "litiso_tile_prop_v1",
    [switch]$Json
)

$ErrorActionPreference = "Stop"

$controlDir = Join-Path $TrainingRoot "control\$OutputName"
$status = Join-Path $controlDir "status.json"
$outputDir = Join-Path $TrainingRoot "outputs\$OutputName"
$logPath = Join-Path (Join-Path $TrainingRoot "logs") "$OutputName.log"
$errPath = Join-Path (Join-Path $TrainingRoot "logs") "$OutputName.err.log"

$latestCheckpoint = Get-ChildItem -Path $outputDir -Filter "*.safetensors" -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
$checkpoints = @(Get-ChildItem -Path $outputDir -Filter "*.safetensors" -File -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTimeUtc)
$processes = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -like "*train_litiso_lora_resumable.py*" -and $_.CommandLine -like "*$OutputName*" } |
    Select-Object ProcessId, CommandLine
$statusPayload = $null
$statusReadError = $null
if (Test-Path $status) {
    try {
        $statusPayload = Get-Content -Raw -Path $status | ConvertFrom-Json
    } catch {
        $statusReadError = $_.Exception.Message
    }
}

function Get-FileSummary {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }
    $item = Get-Item -LiteralPath $Path
    [ordered]@{
        path = $item.FullName
        size_mb = [Math]::Round($item.Length / 1MB, 2)
        last_write_local = $item.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
        age_minutes = [Math]::Round(((Get-Date) - $item.LastWriteTime).TotalMinutes, 1)
    }
}

$step = if ($statusPayload -and $null -ne $statusPayload.step) { [int]$statusPayload.step } else { 0 }
$maxSteps = if ($statusPayload -and $null -ne $statusPayload.max_steps) { [int]$statusPayload.max_steps } else { 0 }
$percent = if ($maxSteps -gt 0) { [Math]::Round(($step / $maxSteps) * 100, 1) } else { 0 }
$latestSummary = if ($latestCheckpoint) { Get-FileSummary -Path $latestCheckpoint.FullName } else { $null }
$logSummary = Get-FileSummary -Path $logPath
$errSummary = Get-FileSummary -Path $errPath
$state = if ($statusPayload -and $statusPayload.state) { [string]$statusPayload.state } elseif ($statusReadError) { "status_unreadable" } else { "unknown" }
$isRunning = @($processes).Count -gt 0
$health = if ($state -eq "complete") {
    "complete"
} elseif ($isRunning) {
    "active_process"
} elseif ($state -in @("paused", "stopped")) {
    $state
} elseif ($statusPayload -or $latestCheckpoint) {
    "no_active_process"
} else {
    "not_started"
}
$recommended = if ($statusReadError) {
    "Fix or replace the unreadable status file before resuming."
} elseif (Test-Path (Join-Path $controlDir "pause.request")) {
    "Pause has been requested; wait for the next checkpoint boundary."
} elseif (Test-Path (Join-Path $controlDir "stop.request")) {
    "Stop has been requested; wait for the next checkpoint boundary."
} elseif ($health -eq "no_active_process" -and $latestCheckpoint) {
    "Resume with start_resumable_litiso_training.ps1 -OutputName $OutputName -ResumeLatest, or sync/evaluate the latest checkpoint."
} elseif ($health -eq "complete" -and $latestCheckpoint) {
    "Sync the final/latest LoRA to ComfyUI, then run evaluation."
} elseif ($health -eq "not_started") {
    "Launch training when ready; no active process or checkpoint was found."
} else {
    "Keep monitoring. Use pause_litiso_training.ps1 for a checkpointed pause/stop."
}

$payload = [ordered]@{
    output_name = $OutputName
    training_root = $TrainingRoot
    output_dir = $outputDir
    control_dir = $controlDir
    status_file = $status
    status = $statusPayload
    status_read_error = $statusReadError
    state = $state
    health = $health
    percent = $percent
    latest_checkpoint = $latestSummary
    checkpoint_count = @($checkpoints).Count
    active_processes = @($processes)
    pause_requested = Test-Path (Join-Path $controlDir "pause.request")
    stop_requested = Test-Path (Join-Path $controlDir "stop.request")
    log = $logSummary
    errors = $errSummary
    recommended_action = $recommended
}

if ($Json.IsPresent) {
    $payload | ConvertTo-Json -Depth 8
    return
}

Write-Host "LIT-ISO LoRA Training Status"
Write-Host "Output:      $OutputName"
Write-Host "State:       $state"
Write-Host "Health:      $health"
Write-Host "Progress:    $percent% ($step/$maxSteps)"
Write-Host "Processes:   $(@($processes).Count)"
Write-Host "Pause/stop:  pause=$($payload.pause_requested) stop=$($payload.stop_requested)"
if ($latestSummary) {
    Write-Host "Latest:      $($latestSummary.path)"
    Write-Host "Latest info: $($latestSummary.size_mb) MB, age $($latestSummary.age_minutes)m"
} else {
    Write-Host "Latest:      none found in $outputDir"
}
Write-Host "Checkpoints: $($payload.checkpoint_count)"
if ($logSummary) {
    Write-Host "Log:         $($logSummary.path) (age $($logSummary.age_minutes)m)"
}
if ($errSummary) {
    Write-Host "Errors:      $($errSummary.path) (age $($errSummary.age_minutes)m)"
}
if ($statusReadError) {
    Write-Host "Status read: $statusReadError"
}
Write-Host "Next:        $recommended"
