param(
    [string]$AssetForgeUrl = "http://127.0.0.1:4182",
    [string]$OutputDir = "C:\Projects\Unity-Projects\LIT-ISO\TempEvalFinal",
    [int]$TimeoutMinutes = 120,
    [int]$PollSeconds = 60,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$WatchScript = Join-Path $ProjectRoot "Tools\LoRA\watch_and_evaluate_sprixen_checkpoint.ps1"
$ManifestPath = Join-Path $ProjectRoot "Temp\LoRA\sprixen_final_eval_watcher_manifest.json"

if (!(Test-Path -LiteralPath $WatchScript)) {
    throw "Missing checkpoint watcher: $WatchScript"
}

$args = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", $WatchScript,
    "-AssetForgeUrl", $AssetForgeUrl,
    "-OutputDir", $OutputDir,
    "-Target", "final",
    "-AfterRecoveryStart",
    "-TimeoutMinutes", $TimeoutMinutes,
    "-PollSeconds", $PollSeconds
)
if ($DryRun) {
    $args += "-DryRun"
}

New-Item -ItemType Directory -Path (Split-Path -Parent $ManifestPath) -Force | Out-Null

if ($DryRun) {
    [ordered]@{
        status = "dry_run_ready"
        command = "powershell $($args -join ' ')"
        assetForgeUrl = $AssetForgeUrl
        outputDir = $OutputDir
        timeoutMinutes = $TimeoutMinutes
        pollSeconds = $PollSeconds
        writtenAt = (Get-Date).ToString("o")
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ManifestPath -Encoding UTF8
    Write-Host "Dry run: would start final Sprixen evaluator watcher."
    exit 0
}

$process = Start-Process -WindowStyle Hidden -FilePath "powershell" -ArgumentList $args -PassThru

[ordered]@{
    status = "started"
    processId = $process.Id
    assetForgeUrl = $AssetForgeUrl
    outputDir = $OutputDir
    timeoutMinutes = $TimeoutMinutes
    pollSeconds = $PollSeconds
    writtenAt = (Get-Date).ToString("o")
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ManifestPath -Encoding UTF8

Write-Host "Started final Sprixen evaluator watcher. ProcessId=$($process.Id)"
