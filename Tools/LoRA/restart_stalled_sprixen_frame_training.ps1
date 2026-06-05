param(
    [int]$MinimumStaleMinutes = 10,
    [string]$RecoveryManifest = "",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$TrainingOutput = "C:\Projects\LoRA-Training\outputs\litiso_sprixen_frame_v1"
$LogPath = Join-Path $TrainingOutput "sprixen_frame_train.log"
$RunScript = Join-Path $ProjectRoot "Tools\LoRA\run_sprixen_frame_training.ps1"
$StatusScript = Join-Path $ProjectRoot "Tools\LoRA\watch_sprixen_frame_training.ps1"
if ([string]::IsNullOrWhiteSpace($RecoveryManifest)) {
    $RecoveryManifest = Join-Path $ProjectRoot "Temp\LoRA\sprixen_frame_recovery_manifest.json"
}

function Write-RecoveryManifest {
    param(
        [string]$Status,
        [string]$Message,
        [object]$TrainingStatus,
        [string]$StartedProcessId = ""
    )

    $manifest = [ordered]@{
        status = $Status
        message = $Message
        dryRun = [bool]$DryRun
        minimumStaleMinutes = $MinimumStaleMinutes
        startedProcessId = $StartedProcessId
        runScript = $RunScript
        trainingOutput = $TrainingOutput
        logPath = $LogPath
        trainingStatus = $TrainingStatus
        writtenAt = (Get-Date).ToString("o")
    }

    New-Item -ItemType Directory -Path (Split-Path -Parent $RecoveryManifest) -Force | Out-Null
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $RecoveryManifest -Encoding UTF8
}

if (!(Test-Path -LiteralPath $RunScript)) {
    throw "Missing Sprixen training runner: $RunScript"
}
if (!(Test-Path -LiteralPath $StatusScript)) {
    throw "Missing Sprixen status watcher: $StatusScript"
}

$status = & powershell -NoProfile -ExecutionPolicy Bypass -File $StatusScript -Json | ConvertFrom-Json

if ($status.FinalLora -and $status.FinalLora.Path) {
    Write-RecoveryManifest -Status "not_needed" -Message "Final LoRA already exists. Run evaluation instead of restarting training." -TrainingStatus $status
    Write-Host "Final LoRA already exists. No restart needed."
    exit 0
}

if (-not $status.IsStale -or [double]$status.LogAgeMinutes -lt $MinimumStaleMinutes) {
    Write-RecoveryManifest -Status "not_stale_enough" -Message "Training log is recent enough to avoid restarting." -TrainingStatus $status
    Write-Host "Training is not stale enough to restart. LogAgeMinutes=$($status.LogAgeMinutes)."
    exit 0
}

$message = "Restarting Sprixen full training without rebuilding dataset or smoke pass."
if ($DryRun) {
    Write-RecoveryManifest -Status "dry_run_ready" -Message $message -TrainingStatus $status
    Write-Host "Dry run: would launch $RunScript -SkipDataset -SkipSmoke"
    exit 0
}

$process = Start-Process -WindowStyle Hidden -FilePath "powershell" -ArgumentList @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", $RunScript,
    "-SkipDataset",
    "-SkipSmoke"
) -PassThru

Write-RecoveryManifest -Status "started" -Message $message -TrainingStatus $status -StartedProcessId $process.Id
Write-Host "Started Sprixen training restart. ProcessId=$($process.Id)"
