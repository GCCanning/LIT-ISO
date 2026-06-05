param(
    [switch]$Json
)

$FullOut = 'C:\Projects\LoRA-Training\outputs\litiso_sprixen_frame_v1'
$SmokeOut = 'C:\Projects\LoRA-Training\outputs\litiso_sprixen_frame_smoke'
$ComfyLora = 'C:\Projects\ComfyUI\models\loras\litiso_sprixen_frame_v1.safetensors'
$EvalManifest = 'C:\Projects\Pixel Pipeline\generated\litiso_sprixen_frame_v1_eval\manifest.json'
$LogPath = Join-Path $FullOut 'sprixen_frame_train.log'
$FinalName = 'litiso_sprixen_frame_v1'
$FinalLora = Join-Path $FullOut "$FinalName.safetensors"
$SmokeLora = Join-Path $SmokeOut 'litiso_sprixen_frame_smoke.safetensors'

function Test-File($Path) {
    if (!(Test-Path -LiteralPath $Path)) { return $null }
    $item = Get-Item -LiteralPath $Path
    [PSCustomObject]@{
        Path = $item.FullName
        SizeMB = [Math]::Round($item.Length / 1MB, 2)
        LastWriteTime = $item.LastWriteTime
    }
}

function Read-ProgressFromLog {
    if (!(Test-Path -LiteralPath $LogPath)) {
        return [PSCustomObject]@{ Step = 0; Total = 3000; Percent = 0; Eta = $null; Stage = 'not_started'; LogLastWriteTime = $null; LogAgeMinutes = $null; IsStale = $false }
    }
    $logItem = Get-Item -LiteralPath $LogPath
    $logAgeMinutes = [Math]::Round(((Get-Date) - $logItem.LastWriteTime).TotalMinutes, 1)
    $isStale = $logAgeMinutes -ge 10
    $tail = Get-Content -LiteralPath $LogPath -Tail 200
    $matches = $tail | Select-String -Pattern 'LoRA smoke train:.*?(\d+)/(\d+).*?(\d+:\d+|\d+:\d+:\d+)<([^,]+),' -AllMatches
    $last = $matches | Select-Object -Last 1
    if ($last) {
        $m = $last.Matches[0]
        $step = [int]$m.Groups[1].Value
        $total = [int]$m.Groups[2].Value
        return [PSCustomObject]@{
            Step = $step
            Total = $total
            Percent = [Math]::Round(($step / [Math]::Max(1, $total)) * 100, 1)
            Eta = $m.Groups[4].Value.Trim()
            Stage = if ($total -eq 250) { 'smoke_training' } else { 'full_training' }
            LogLastWriteTime = $logItem.LastWriteTime
            LogAgeMinutes = $logAgeMinutes
            IsStale = $isStale
        }
    }
    if (($tail -join "`n") -match 'Copied final LoRA to ComfyUI') {
        return [PSCustomObject]@{ Step = 3000; Total = 3000; Percent = 100; Eta = '0:00'; Stage = 'complete'; LogLastWriteTime = $logItem.LastWriteTime; LogAgeMinutes = $logAgeMinutes; IsStale = $false }
    }
    return [PSCustomObject]@{ Step = 0; Total = 3000; Percent = 0; Eta = $null; Stage = 'starting'; LogLastWriteTime = $logItem.LastWriteTime; LogAgeMinutes = $logAgeMinutes; IsStale = $isStale }
}

$progress = Read-ProgressFromLog
$checkpoints = @('00750','01500','02250','03000') | ForEach-Object {
    $path = Join-Path $FullOut "$FinalName`_step$_.safetensors"
    Test-File $path
} | Where-Object { $null -ne $_ }

$status = [PSCustomObject]@{
    Stage = if (Test-Path -LiteralPath $EvalManifest) { 'evaluated' } elseif (Test-Path -LiteralPath $ComfyLora) { 'copied_to_comfy' } elseif (Test-Path -LiteralPath $FinalLora) { 'trained' } elseif ($progress.Stage -ne 'not_started') { $progress.Stage } elseif (Test-Path -LiteralPath $SmokeLora) { 'smoke_complete' } else { 'not_started' }
    Percent = $progress.Percent
    Step = $progress.Step
    Total = $progress.Total
    Eta = $progress.Eta
    Health = if ($progress.IsStale -and !(Test-Path -LiteralPath $FinalLora)) { 'likely_stalled' } elseif ($progress.Percent -ge 100) { 'complete' } else { 'active_or_recent' }
    IsStale = $progress.IsStale
    LogLastWriteTime = $progress.LogLastWriteTime
    LogAgeMinutes = $progress.LogAgeMinutes
    RecommendedAction = if ($progress.IsStale -and !(Test-Path -LiteralPath $FinalLora)) {
        'Inspect the training terminal/process. If it is not actively progressing, restart Tools\LoRA\run_sprixen_frame_training.ps1 with -SkipDataset -SkipSmoke. The current trainer cannot truly resume optimizer state from step checkpoints.'
    } elseif (Test-Path -LiteralPath $FinalLora) {
        'Run Asset Forge checkpoint evaluation against the final LoRA and promote accepted sheets into Unity.'
    } else {
        'Keep monitoring. The log is recent enough to treat the run as active.'
    }
    SmokeLora = Test-File $SmokeLora
    FinalLora = Test-File $FinalLora
    ComfyCopy = Test-File $ComfyLora
    Checkpoints = $checkpoints
    EvalManifest = Test-File $EvalManifest
    LogPath = $LogPath
}

if ($Json) {
    $status | ConvertTo-Json -Depth 6
} else {
    Write-Host "Sprixen Frame LoRA Status"
    Write-Host "Stage: $($status.Stage)"
    Write-Host "Health: $($status.Health)"
    Write-Host "Progress: $($status.Percent)% ($($status.Step)/$($status.Total)) ETA=$($status.Eta)"
    Write-Host "Log age: $($status.LogAgeMinutes)m stale=$($status.IsStale)"
    Write-Host "Recommended: $($status.RecommendedAction)"
    Write-Host "Smoke: $($status.SmokeLora.Path)"
    Write-Host "Final: $($status.FinalLora.Path)"
    Write-Host "Comfy: $($status.ComfyCopy.Path)"
    Write-Host "Checkpoints: $($status.Checkpoints.Count)"
    Write-Host "Log: $($status.LogPath)"
}
