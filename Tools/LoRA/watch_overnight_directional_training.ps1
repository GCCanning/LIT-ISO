param(
    [switch]$Watch,
    [int]$RefreshSeconds = 10,
    [int]$Tail = 8
)

$ErrorActionPreference = 'SilentlyContinue'

$DatasetDir = 'C:\Projects\Pixel Pipeline\style_examples\freepixel_web_download_structured'
$SmokeOut = 'C:\Projects\LoRA-Training\outputs\litiso_style_directional_smoke'
$FullOut = 'C:\Projects\LoRA-Training\outputs\litiso_style_directional_v1'
$ComfyLora = 'C:\Projects\ComfyUI\models\loras\litiso_style_directional_v1.safetensors'
$EvalManifest = 'C:\Projects\Pixel Pipeline\generated\litiso_style_directional_v1_eval\manifest.json'
$LogPath = Join-Path $FullOut 'overnight_train.log'
$FinalName = 'litiso_style_directional_v1'
$SmokeLora = Join-Path $SmokeOut 'litiso_style_directional_smoke.safetensors'
$FinalLora = Join-Path $FullOut "$FinalName.safetensors"

function Get-FileSizeText {
    param([string]$Path)
    if (!(Test-Path -LiteralPath $Path)) { return '-' }
    $length = (Get-Item -LiteralPath $Path).Length
    if ($length -ge 1GB) { return "{0:n2} GB" -f ($length / 1GB) }
    if ($length -ge 1MB) { return "{0:n1} MB" -f ($length / 1MB) }
    if ($length -ge 1KB) { return "{0:n1} KB" -f ($length / 1KB) }
    return "$length B"
}

function Get-RunningTrainingProcesses {
    Get-CimInstance Win32_Process |
        Where-Object {
            $_.CommandLine -match 'run_overnight_directional_training|freepixel_structured_dataset|train_litiso_lora_smoke|eval_litiso_style_directional_v1_comfy'
        } |
        Select-Object ProcessId, Name, CommandLine
}

function Get-TrainingStatus {
    $logText = ''
    if (Test-Path -LiteralPath $LogPath) {
        $logText = Get-Content -LiteralPath $LogPath -Raw
    }

    $imageCount = 0
    $metadataCount = 0
    $manifest = $null
    $manifestPath = Join-Path $DatasetDir 'manifest.json'
    $metadataPath = Join-Path $DatasetDir 'metadata.jsonl'

    if (Test-Path -LiteralPath (Join-Path $DatasetDir 'images')) {
        $imageCount = (Get-ChildItem -LiteralPath (Join-Path $DatasetDir 'images') -Filter '*.png' -File).Count
    }
    if (Test-Path -LiteralPath $metadataPath) {
        $metadataCount = (Get-Content -LiteralPath $metadataPath).Count
    }
    if (Test-Path -LiteralPath $manifestPath) {
        try { $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json } catch { $manifest = $null }
    }

    $stepFiles = @()
    foreach ($step in '00750','01500','02250','03000') {
        $path = Join-Path $FullOut "$FinalName`_step$step.safetensors"
        if (Test-Path -LiteralPath $path) { $stepFiles += $path }
    }

    $percent = 0
    $stage = 'Not started'
    $detail = 'No overnight log found yet.'

    if ($logText -match 'START structured directional dataset build') {
        $stage = 'Dataset build'
        $percent = [Math]::Min(18, 5 + [Math]::Floor($imageCount / 400))
        $detail = "Building directional dataset. Images written: $imageCount."
    }
    if ($manifest -ne $null) {
        $stage = 'Dataset ready'
        $percent = 20
        $detail = "Dataset ready: $($manifest.total_frames) frames; partial sheets skipped: $($manifest.stats.partial_sheets_skipped)."
    }
    if ($logText -match 'START smoke training') {
        $stage = 'Smoke training'
        $percent = 25
        $detail = 'Running the 250-step smoke LoRA.'
    }
    if (Test-Path -LiteralPath $SmokeLora) {
        $stage = 'Smoke complete'
        $percent = 35
        $detail = "Smoke LoRA exists: $(Get-FileSizeText $SmokeLora)."
    }
    if ($logText -match 'START full training') {
        $stage = 'Full training'
        $percent = 40 + ($stepFiles.Count * 12)
        $detail = "Full 3000-step run. Checkpoints found: $($stepFiles.Count)/4."
    }
    if (Test-Path -LiteralPath $FinalLora) {
        $stage = 'Final LoRA written'
        $percent = 90
        $detail = "Final LoRA exists: $(Get-FileSizeText $FinalLora)."
    }
    if (Test-Path -LiteralPath $ComfyLora) {
        $stage = 'Copied to ComfyUI'
        $percent = 95
        $detail = "ComfyUI copy exists: $(Get-FileSizeText $ComfyLora)."
    }
    if (Test-Path -LiteralPath $EvalManifest) {
        $stage = 'Evaluation complete'
        $percent = 100
        $detail = "Evaluation manifest exists: $EvalManifest."
    }
    if ($logText -match 'Overnight directional LoRA training complete') {
        $stage = 'Complete'
        $percent = 100
    }

    [PSCustomObject]@{
        Percent = $percent
        Stage = $stage
        Detail = $detail
        DatasetImages = $imageCount
        MetadataRows = $metadataCount
        SmokeLora = Test-Path -LiteralPath $SmokeLora
        Checkpoints = $stepFiles.Count
        FinalLora = Test-Path -LiteralPath $FinalLora
        ComfyCopy = Test-Path -LiteralPath $ComfyLora
        EvalManifest = Test-Path -LiteralPath $EvalManifest
        Processes = @(Get-RunningTrainingProcesses)
    }
}

function Show-TrainingStatus {
    $status = Get-TrainingStatus
    $barWidth = 32
    $filled = [Math]::Floor($status.Percent / 100 * $barWidth)
    $bar = ('#' * $filled).PadRight($barWidth, '-')

    Clear-Host
    Write-Host "LIT-ISO Directional LoRA Overnight Run"
    Write-Host "[$bar] $($status.Percent)%"
    Write-Host "Stage:  $($status.Stage)"
    Write-Host "Status: $($status.Detail)"
    Write-Host ""
    Write-Host "Artifacts"
    Write-Host "  Dataset images: $($status.DatasetImages)"
    Write-Host "  Metadata rows:  $($status.MetadataRows)"
    Write-Host "  Smoke LoRA:     $($status.SmokeLora)"
    Write-Host "  Checkpoints:    $($status.Checkpoints)/4"
    Write-Host "  Final LoRA:     $($status.FinalLora)"
    Write-Host "  ComfyUI copy:   $($status.ComfyCopy)"
    Write-Host "  Eval manifest:  $($status.EvalManifest)"
    Write-Host ""
    Write-Host "Processes"
    if ($status.Processes.Count -eq 0) {
        Write-Host "  No matching training process found."
    } else {
        foreach ($proc in $status.Processes) {
            Write-Host "  $($proc.Name) pid=$($proc.ProcessId)"
        }
    }
    Write-Host ""
    Write-Host "Recent Log"
    if (Test-Path -LiteralPath $LogPath) {
        Get-Content -LiteralPath $LogPath -Tail $Tail | ForEach-Object { Write-Host "  $_" }
    } else {
        Write-Host "  $LogPath not found."
    }
}

do {
    Show-TrainingStatus
    if ($Watch) {
        Start-Sleep -Seconds $RefreshSeconds
    }
} while ($Watch)
