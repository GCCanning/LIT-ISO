param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$patterns = @(
    "train_litiso_lora_resumable.py",
    "comfy_generation_worker.py",
    "sprixen_generation_worker.py",
    "eval_litiso_direction_oracle_anchor_v1_comfy.py",
    "eval_litiso_",
    "process_generation_request_comfy.ps1",
    "process_generation_request_sprixen.ps1",
    "queue_litiso_controlnet_direction_requests.py",
    "queue_oga_template_guided_requests.py",
    "queue_oga_composite_template_guided_requests.py"
)

$currentPid = $PID
$matches = @(
    Get-CimInstance Win32_Process |
        Where-Object {
            $_.ProcessId -ne $currentPid -and
            @($patterns | Where-Object { [string]$_.CommandLine -like "*$_*" }).Count -gt 0
        } |
        Select-Object ProcessId, Name, CommandLine
)

foreach ($process in $matches) {
    if ($DryRun.IsPresent) {
        $process | Add-Member -NotePropertyName Stopped -NotePropertyValue $false -Force
        $process | Add-Member -NotePropertyName DryRun -NotePropertyValue $true -Force
        continue
    }

    try {
        Stop-Process -Id $process.ProcessId -Force -ErrorAction Stop
        $process | Add-Member -NotePropertyName Stopped -NotePropertyValue $true -Force
    } catch {
        $process | Add-Member -NotePropertyName Stopped -NotePropertyValue $false -Force
        $process | Add-Member -NotePropertyName StopError -NotePropertyValue $_.Exception.Message -Force
    }
}

[ordered]@{
    dry_run = $DryRun.IsPresent
    matched_count = @($matches).Count
    matches = @($matches)
} | ConvertTo-Json -Depth 6
