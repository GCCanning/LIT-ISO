param(
    [string]$AssetForgeUrl = "http://127.0.0.1:4180",
    [string]$OutputDir = "C:\Projects\Pixel Pipeline\generated\asset_forge_sprixen_eval",
    [string]$TrainingOutput = "C:\Projects\LoRA-Training\outputs\litiso_sprixen_frame_v1",
    [string]$Target = "latest",
    [string]$MinLastWriteTimeIso = "",
    [string]$RecoveryManifest = "Temp\LoRA\sprixen_frame_recovery_manifest.json",
    [switch]$AfterRecoveryStart,
    [int]$PollSeconds = 60,
    [int]$TimeoutMinutes = 180,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$targets = [ordered]@{
    final = "litiso_sprixen_frame_v1.safetensors"
    step02250 = "litiso_sprixen_frame_v1_step02250.safetensors"
    step01500 = "litiso_sprixen_frame_v1_step01500.safetensors"
    step00750 = "litiso_sprixen_frame_v1_step00750.safetensors"
}

function Write-WatchManifest {
    param(
        [string]$Status,
        [string]$SelectedLora,
        [string]$Message,
        [string]$Path
    )
    $manifest = [ordered]@{
        status = $Status
        selectedLora = $SelectedLora
        message = $Message
        assetForgeUrl = $AssetForgeUrl
        target = $Target
        minLastWriteTimeIso = $MinLastWriteTimeIso
        afterRecoveryStart = [bool]$AfterRecoveryStart
        recoveryManifest = $RecoveryManifest
        trainingOutput = $TrainingOutput
        writtenAt = (Get-Date).ToString("o")
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $Path) -Force | Out-Null
    $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Get-RequestedLora {
    $minLastWrite = $null
    if ($AfterRecoveryStart) {
        $manifestPath = if ([IO.Path]::IsPathRooted($RecoveryManifest)) { $RecoveryManifest } else { Join-Path (Get-Location) $RecoveryManifest }
        if (Test-Path -LiteralPath $manifestPath) {
            $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
            if ($manifest.writtenAt) {
                $minLastWrite = [DateTime]::Parse($manifest.writtenAt)
            }
        }
    }
    if (![string]::IsNullOrWhiteSpace($MinLastWriteTimeIso)) {
        $minLastWrite = [DateTime]::Parse($MinLastWriteTimeIso)
    }

    function Test-LoraFreshEnough {
        param([string]$Path)
        if (!(Test-Path -LiteralPath $Path)) {
            return $false
        }
        if ($null -eq $minLastWrite) {
            return $true
        }
        return (Get-Item -LiteralPath $Path).LastWriteTime -ge $minLastWrite
    }

    if ($targets.Contains($Target)) {
        $name = $targets[$Target]
        $path = Join-Path $TrainingOutput $name
        return if (Test-LoraFreshEnough -Path $path) { $name } else { $null }
    }

    foreach ($name in @($targets.final, $targets.step02250, $targets.step01500, $targets.step00750)) {
        $path = Join-Path $TrainingOutput $name
        if (Test-LoraFreshEnough -Path $path) {
            return $name
        }
    }
    return $null
}

$watchRoot = Join-Path $OutputDir "_watch"
$watchManifest = Join-Path $watchRoot "watch_manifest.json"
$deadline = (Get-Date).AddMinutes($TimeoutMinutes)
$selected = $null

Write-Host "Watching for Sprixen LoRA target '$Target' in $TrainingOutput..."
do {
    $selected = Get-RequestedLora
    if ($selected) {
        break
    }

    $remaining = [Math]::Max(0, [int]($deadline - (Get-Date)).TotalSeconds)
    Write-WatchManifest -Status "waiting" -SelectedLora "" -Message "Waiting for target. Seconds remaining: $remaining" -Path $watchManifest
    if ((Get-Date) -ge $deadline) {
        Write-WatchManifest -Status "timeout" -SelectedLora "" -Message "Timed out waiting for target '$Target'." -Path $watchManifest
        Write-Warning "Timed out waiting for Sprixen LoRA target '$Target'."
        exit 1
    }
    Start-Sleep -Seconds $PollSeconds
} while ($true)

Write-WatchManifest -Status "evaluating" -SelectedLora $selected -Message "Target found. Running Asset Forge evaluator." -Path $watchManifest

$args = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", "Tools\LoRA\evaluate_asset_forge_sprixen_checkpoint.ps1",
    "-AssetForgeUrl", $AssetForgeUrl,
    "-OutputDir", $OutputDir,
    "-LoraName", $selected
)
if ($DryRun) {
    $args += "-DryRun"
}

& powershell @args
$exitCode = $LASTEXITCODE
if ($exitCode -ne 0) {
    Write-WatchManifest -Status "failed" -SelectedLora $selected -Message "Evaluator exited with code $exitCode." -Path $watchManifest
    exit $exitCode
}

Write-WatchManifest -Status "complete" -SelectedLora $selected -Message "Evaluator completed." -Path $watchManifest
Write-Host "Sprixen checkpoint evaluation completed for $selected."
