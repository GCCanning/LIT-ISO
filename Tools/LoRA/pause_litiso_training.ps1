param(
    [string]$TrainingRoot = "C:\Projects\LoRA-Training",
    [string]$OutputName = "litiso_tile_prop_v1",
    [ValidateSet("pause", "stop")]
    [string]$Mode = "pause"
)

$ErrorActionPreference = "Stop"

$controlDir = Join-Path $TrainingRoot "control\$OutputName"
New-Item -ItemType Directory -Force -Path $controlDir | Out-Null
$request = Join-Path $controlDir "$Mode.request"
Set-Content -Path $request -Value ((Get-Date).ToUniversalTime().ToString("o")) -Encoding UTF8
$processes = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -like "*train_litiso_lora_resumable.py*" -and $_.CommandLine -like "*$OutputName*" } |
    Select-Object ProcessId, CommandLine)

[ordered]@{
    output_name = $OutputName
    requested = $Mode
    request_file = $request
    active_process_count = $processes.Count
    active_process_ids = @($processes | ForEach-Object { $_.ProcessId })
    requested_utc = (Get-Date).ToUniversalTime().ToString("o")
    note = "Trainer will save a checkpoint and exit at the next training step boundary."
    operator_hint = if ($processes.Count -gt 0) {
        "Watch status_litiso_training.ps1 until state changes to $Mode or the process exits."
    } else {
        "No matching trainer process was found. The request file is still present and will be consumed by the next matching run unless the trainer clears stale requests on launch."
    }
} | ConvertTo-Json
