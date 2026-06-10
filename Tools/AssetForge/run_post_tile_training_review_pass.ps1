param(
    [string]$ProjectRoot = "",
    [string]$TileOutputName = "litiso_iso_reference_tile_style_v1",
    [string]$MageVariant = "v6",
    [string[]]$BlockOnTraining = @("litiso_iso_reference_critter_style_v1"),
    [switch]$AllowRunningCheckpoint,
    [switch]$ReplaceExisting,
    [switch]$SkipTileEval,
    [switch]$SkipMageRenders,
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
$tileEvalScript = Join-Path $ProjectRoot "Tools\LoRA\evaluate_iso_reference_tile_style_matrix.ps1"
$processScript = Join-Path $ProjectRoot "Tools\AssetForge\process_generation_request_comfy.ps1"
$candidateSheetScript = Join-Path $ProjectRoot "Tools\AssetForge\build_black_mage_candidate_review_sheet.py"
$python = "C:\Projects\ComfyUI\.venv\Scripts\python.exe"
$summaryPath = Join-Path $ProjectRoot "Temp\AssetForge\post_tile_training_review_pass_$MageVariant.json"
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $summaryPath) | Out-Null

function Get-TrainingBlocker {
    param([string]$Name)
    $statusPath = "C:\Projects\LoRA-Training\control\$Name\status.json"
    if (-not (Test-Path -LiteralPath $statusPath)) {
        return [ordered]@{ output_name = $Name; status_path = $statusPath; state = "not_started"; running = $false }
    }
    try {
        $payload = Get-Content -Raw -LiteralPath $statusPath | ConvertFrom-Json
        $state = if ($payload.state) { [string]$payload.state } else { "unknown" }
        return [ordered]@{
            output_name = $Name
            status_path = $statusPath
            state = $state
            running = $state -eq "running"
            step = $payload.step
            max_steps = $payload.max_steps
            updated_utc = $payload.updated_utc
        }
    }
    catch {
        return [ordered]@{ output_name = $Name; status_path = $statusPath; state = "status_unreadable"; running = $true; error = $_.Exception.Message }
    }
}

$status = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $statusScript -OutputName $TileOutputName -Json | ConvertFrom-Json
$trainingComplete = [string]$status.health -eq "complete" -or [string]$status.state -eq "complete"
$blockers = @($BlockOnTraining | ForEach-Object { Get-TrainingBlocker -Name $_ })
$activeBlockers = @($blockers | Where-Object { $_.running })
$canRunGpu = ($trainingComplete -or $AllowRunningCheckpoint.IsPresent) -and @($activeBlockers).Count -eq 0
$directions = @("ne", "nw", "se", "sw")
$mageRequests = @()
foreach ($direction in $directions) {
    $request = Join-Path $ProjectRoot "Temp\AssetForge\black_mage_requests\black_mage_iso_idle_$direction`_$MageVariant\generation_request.json"
    $mageRequests += [ordered]@{
        direction = $direction.ToUpperInvariant()
        request_path = Convert-ToRepoPath $request
        exists = Test-Path -LiteralPath $request
    }
}

$plan = [ordered]@{
    schema = "lit_iso.asset_forge.post_tile_training_review_pass.v1"
    created_utc = (Get-Date).ToUniversalTime().ToString("o")
    dry_run = [bool]$DryRun
    tile_output_name = $TileOutputName
    mage_variant = $MageVariant
    can_run_gpu_now = $canRunGpu
    reason = if ($canRunGpu) { "tile checkpoint is ready and no configured GPU blocker training is active" } elseif (@($activeBlockers).Count -gt 0) { "active training is using the GPU; hold tile eval and mage renders" } else { "tile training still running; hold GPU work" }
    training_status = $status
    blocking_training = $blockers
    mage_requests = $mageRequests
    planned_steps = @(
        if (-not $SkipTileEval.IsPresent) { "sync and evaluate tile LoRA strength matrix via Tools\LoRA\evaluate_iso_reference_tile_style_matrix.ps1" }
        if (-not $SkipMageRenders.IsPresent) { "process four staged black mage $MageVariant ComfyUI requests" }
        if (-not $SkipMageRenders.IsPresent) { "build black mage $MageVariant candidate review sheet" }
    )
    outputs = [ordered]@{
        tile_eval = Convert-ToRepoPath (Join-Path $ProjectRoot "Temp\LoRA\Evals")
        mage_review_root = "Assets/Generated/_Review/black_mage_iso_renders_$MageVariant"
        mage_candidate_sheet = "Assets/Generated/_Review/black_mage_iso_renders_$MageVariant/_$MageVariant`_candidate_review_sheet.png"
    }
}
$plan | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

if ($DryRun.IsPresent -or -not $canRunGpu) {
    [ordered]@{
        ok = $true
        mode = if ($DryRun.IsPresent) { "dry_run" } else { "pending_training" }
        can_run_gpu_now = $canRunGpu
        summary = Convert-ToRepoPath $summaryPath
        observed_progress = $status.observed_progress
        latest_checkpoint = $status.latest_checkpoint
    } | ConvertTo-Json -Depth 8
    return
}

if (-not $SkipTileEval.IsPresent) {
    $tileArgs = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $tileEvalScript, "-OutputName", $TileOutputName)
    if ($AllowRunningCheckpoint.IsPresent) { $tileArgs += "-AllowRunningCheckpoint" }
    foreach ($blocker in $BlockOnTraining) { $tileArgs += @("-BlockOnTraining", $blocker) }
    & powershell.exe @tileArgs | Out-Host
}

if (-not $SkipMageRenders.IsPresent) {
    foreach ($item in $mageRequests) {
        if (-not $item.exists) {
            throw "Missing mage request: $($item.request_path)"
        }
        $requestFull = Join-Path $ProjectRoot ([string]$item.request_path).Replace("/", "\")
        $processArgs = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $processScript, "-RequestPath", $requestFull)
        if ($ReplaceExisting.IsPresent) { $processArgs += "-ReplaceExisting" }
        & powershell.exe @processArgs | Out-Host
    }
    & $python -B $candidateSheetScript --variant $MageVariant | Out-Host
}

[ordered]@{
    ok = $true
    mode = "complete"
    summary = Convert-ToRepoPath $summaryPath
    tile_eval = $plan.outputs.tile_eval
    mage_candidate_sheet = $plan.outputs.mage_candidate_sheet
} | ConvertTo-Json -Depth 8
