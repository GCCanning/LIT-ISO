param(
    [string]$ProjectRoot = "",
    [string]$TrainingRoot = "C:\Projects\LoRA-Training",
    [string]$ComfyRoot = "C:\Projects\ComfyUI",
    [string]$OutputName = "litiso_iso_reference_tile_style_v1",
    [string]$ComfyUrl = "http://127.0.0.1:8188",
    [string]$CheckpointName = "DreamShaper_8_pruned.safetensors",
    [double[]]$Strengths = @(0.35, 0.55, 0.72, 0.90),
    [string[]]$BlockOnTraining = @("litiso_iso_reference_critter_style_v1"),
    [string]$OutputRoot = "",
    [string]$ScoreRoot = "",
    [string]$SelectedFamilyRoot = "",
    [switch]$AllowRunningCheckpoint,
    [switch]$ForceDuringTraining,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $ProjectRoot "Temp\LoRA\Evals"
}
if ([string]::IsNullOrWhiteSpace($ScoreRoot)) {
    $ScoreRoot = Join-Path $ProjectRoot "Temp\LoRA\Evals"
}
if ([string]::IsNullOrWhiteSpace($SelectedFamilyRoot)) {
    $SelectedFamilyRoot = Join-Path $ProjectRoot "Assets\Generated\_Review\tile_style_eval_selected_family_v1"
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

function Get-TrainingStatus {
    param([string]$Name)
    $path = Join-Path $TrainingRoot "control\$Name\status.json"
    if (-not (Test-Path -LiteralPath $path)) {
        return [ordered]@{
            output_name = $Name
            status_path = $path
            state = "not_started"
            step = 0
            max_steps = 0
            running = $false
            updated_utc = $null
        }
    }

    try {
        $payload = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
        $state = if ($payload.state) { [string]$payload.state } else { "unknown" }
        [ordered]@{
            output_name = $Name
            status_path = $path
            state = $state
            step = if ($null -ne $payload.step) { [int]$payload.step } else { 0 }
            max_steps = if ($null -ne $payload.max_steps) { [int]$payload.max_steps } else { 0 }
            running = $state -eq "running"
            updated_utc = $payload.updated_utc
            checkpoint = $payload.checkpoint
        }
    }
    catch {
        [ordered]@{
            output_name = $Name
            status_path = $path
            state = "status_unreadable"
            step = 0
            max_steps = 0
            running = $true
            updated_utc = $null
            error = $_.Exception.Message
        }
    }
}

$evalScript = Join-Path $ProjectRoot "Tools\LoRA\evaluate_iso_reference_tile_style_lora.ps1"
if (-not (Test-Path -LiteralPath $evalScript)) {
    throw "Missing tile LoRA evaluator: $evalScript"
}
$scoreScript = Join-Path $ProjectRoot "Tools\LoRA\score_tile_style_eval_outputs.py"
if (-not (Test-Path -LiteralPath $scoreScript)) {
    throw "Missing tile style scorer: $scoreScript"
}
$selectScript = Join-Path $ProjectRoot "Tools\LoRA\select_tile_style_eval_family.py"
if (-not (Test-Path -LiteralPath $selectScript)) {
    throw "Missing tile style selector: $selectScript"
}
$referenceTargets = Join-Path $ProjectRoot "Temp\LoRA\Evals\stylelock_tile_reference_targets.png"
$referenceTargetsManifest = Join-Path $ProjectRoot "Temp\LoRA\Evals\stylelock_tile_reference_targets.json"
$scoreReport = Join-Path $ScoreRoot "tile_style_eval_scores.json"
$scoreCsv = Join-Path $ScoreRoot "tile_style_eval_scores.csv"
$scoreSheet = Join-Path $ScoreRoot "tile_style_eval_ranked_sheet.png"
$selectedFamilyRoot = $SelectedFamilyRoot

$summaryPath = Join-Path $ProjectRoot "Temp\LoRA\$OutputName.eval_matrix_plan.json"
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $summaryPath) | Out-Null

$blockers = @($BlockOnTraining | ForEach-Object { Get-TrainingStatus -Name $_ })
$activeBlockers = @($blockers | Where-Object { $_.running })
$canRun = @($activeBlockers).Count -eq 0 -or $ForceDuringTraining.IsPresent

$matrix = @()
foreach ($strength in $Strengths) {
    $label = ("s{0:0.00}" -f $strength).Replace(".", "p")
    $outDir = Join-Path $OutputRoot "$OutputName`_post_training_eval_$label"
    $command = @(
        "powershell",
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $evalScript,
        "-TrainingRoot",
        $TrainingRoot,
        "-ComfyRoot",
        $ComfyRoot,
        "-OutputName",
        $OutputName,
        "-ComfyUrl",
        $ComfyUrl,
        "-CheckpointName",
        $CheckpointName,
        "-LoraStrength",
        ([string]$strength),
        "-OutputDir",
        $outDir
    )
    if ($AllowRunningCheckpoint.IsPresent) { $command += "-AllowRunningCheckpoint" }
    if ($DryRun.IsPresent) { $command += "-DryRun" }

    $matrix += [ordered]@{
        strength = $strength
        label = $label
        output_dir = $outDir
        manifest = Join-Path $outDir "manifest.json"
        contact_sheet = Join-Path $outDir "contact_sheet.png"
        command = $command
    }
}

$plan = [ordered]@{
    schema = "lit_iso.lora.tile_style_eval_matrix.v1"
    created_utc = (Get-Date).ToUniversalTime().ToString("o")
    dry_run = [bool]$DryRun
    output_name = $OutputName
    checkpoint_name = $CheckpointName
    comfy_url = $ComfyUrl
    output_root = $OutputRoot
    style_reference_targets = [ordered]@{
        contact_sheet = Convert-ToRepoPath $referenceTargets
        manifest = Convert-ToRepoPath $referenceTargetsManifest
    }
    score_outputs = [ordered]@{
        report = Convert-ToRepoPath $scoreReport
        csv = Convert-ToRepoPath $scoreCsv
        ranked_sheet = Convert-ToRepoPath $scoreSheet
    }
    selected_family_review = [ordered]@{
        root = Convert-ToRepoPath $selectedFamilyRoot
        manifest = Convert-ToRepoPath (Join-Path $selectedFamilyRoot "selected_tile_family_manifest.json")
        contact_sheet = Convert-ToRepoPath (Join-Path $selectedFamilyRoot "selected_tile_family_contact_sheet.png")
    }
    strengths = @($Strengths)
    blocking_training = $blockers
    can_run_now = $canRun
    reason = if ($canRun) { "no active blocker training, or ForceDuringTraining was supplied" } else { "active training is using the GPU; wait before live evaluation" }
    matrix = $matrix
    next_step = if ($canRun) { "Run without -DryRun when GPU is free to generate per-strength eval folders and contact sheets." } else { "Wait for blocking training to finish, then rerun without -DryRun." }
}
$plan | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

if ($DryRun.IsPresent) {
    [ordered]@{
        ok = $true
        mode = "dry_run"
        can_run_now = $canRun
        active_blockers = @($activeBlockers | ForEach-Object { $_.output_name })
        summary = Convert-ToRepoPath $summaryPath
        matrix_count = @($matrix).Count
    } | ConvertTo-Json -Depth 8
    return
}

if (-not $canRun) {
    throw "GPU evaluation is blocked by active training. Plan: $summaryPath"
}

$results = @()
foreach ($entry in $matrix) {
    $liveCommand = @($entry.command | Where-Object { $_ -ne "-DryRun" })
    & $liveCommand[0] @($liveCommand | Select-Object -Skip 1) | Out-Host
    $exitCode = $LASTEXITCODE
    $results += [ordered]@{
        strength = $entry.strength
        output_dir = $entry.output_dir
        manifest = $entry.manifest
        contact_sheet = $entry.contact_sheet
        exit_code = $exitCode
        ok = $exitCode -eq 0
    }
    if ($exitCode -ne 0) {
        throw "Tile style eval failed for strength $($entry.strength). Plan: $summaryPath"
    }
}

$python = "C:\Projects\LoRA-Training\.venv\Scripts\python.exe"
if (-not (Test-Path -LiteralPath $python)) {
    $python = "C:\Projects\ComfyUI\.venv\Scripts\python.exe"
}
& $python -B $scoreScript --project-root $ProjectRoot --matrix-plan $summaryPath --reference-manifest $referenceTargetsManifest --output $scoreReport --csv $scoreCsv --sheet $scoreSheet | Out-Host
$scoreExitCode = $LASTEXITCODE
if ($scoreExitCode -ne 0) {
    throw "Tile style scoring failed with exit code $scoreExitCode. Plan: $summaryPath"
}
& $python -B $selectScript --project-root $ProjectRoot --scores $scoreReport --out-root $selectedFamilyRoot | Out-Host
$selectExitCode = $LASTEXITCODE
if ($selectExitCode -ne 0) {
    throw "Tile style selected-family review pack failed with exit code $selectExitCode. Plan: $summaryPath"
}

$plan["results"] = $results
$plan["score_report"] = Convert-ToRepoPath $scoreReport
$plan["score_csv"] = Convert-ToRepoPath $scoreCsv
$plan["score_ranked_sheet"] = Convert-ToRepoPath $scoreSheet
$plan["selected_family_manifest"] = Convert-ToRepoPath (Join-Path $selectedFamilyRoot "selected_tile_family_manifest.json")
$plan["selected_family_contact_sheet"] = Convert-ToRepoPath (Join-Path $selectedFamilyRoot "selected_tile_family_contact_sheet.png")
$plan["completed_utc"] = (Get-Date).ToUniversalTime().ToString("o")
$plan | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

[ordered]@{
    ok = $true
    mode = "complete"
    summary = Convert-ToRepoPath $summaryPath
    score_report = Convert-ToRepoPath $scoreReport
    score_ranked_sheet = Convert-ToRepoPath $scoreSheet
    selected_family_manifest = Convert-ToRepoPath (Join-Path $selectedFamilyRoot "selected_tile_family_manifest.json")
    selected_family_contact_sheet = Convert-ToRepoPath (Join-Path $selectedFamilyRoot "selected_tile_family_contact_sheet.png")
    results = $results
} | ConvertTo-Json -Depth 8
