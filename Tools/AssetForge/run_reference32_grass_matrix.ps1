param(
    [string]$ProjectRoot = "",
    [string]$PythonExe = "C:\Projects\ComfyUI\.venv\Scripts\python.exe",
    [string]$LoraName = "litiso_reference32_clean_tile_geometry_v1_final.safetensors",
    [switch]$ReplaceExisting,
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

function Get-TrainingState {
    param([string]$OutputName)
    $path = "C:\Projects\LoRA-Training\control\$OutputName\status.json"
    if (-not (Test-Path -LiteralPath $path)) {
        return [ordered]@{ output_name = $OutputName; state = "missing"; running = $false; status_path = $path }
    }
    try {
        $status = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
        $state = if ($status.state) { [string]$status.state } else { "unknown" }
        return [ordered]@{
            output_name = $OutputName
            state = $state
            running = $state -eq "running"
            step = $status.step
            max_steps = $status.max_steps
            checkpoint = $status.checkpoint
            status_path = $path
        }
    }
    catch {
        return [ordered]@{ output_name = $OutputName; state = "unreadable"; running = $true; status_path = $path; error = $_.Exception.Message }
    }
}

$queueScript = Join-Path $ProjectRoot "Tools\AssetForge\queue_reference32_controlnet_tile_requests.py"
$processScript = Join-Path $ProjectRoot "Tools\AssetForge\process_generation_request_comfy.ps1"
$sheetScript = Join-Path $ProjectRoot "Tools\AssetForge\build_reference32_grass_matrix_sheet.py"
$manifestPath = Join-Path $ProjectRoot "Temp\AssetForge\reference32_clean_tile_grass_matrix_v1.json"
$outSheet = Join-Path $ProjectRoot "Assets\Generated\_Review\reference32_clean_tile_grass_matrix_v1\reference32_clean_tile_grass_matrix_contact_sheet.png"
$outReport = Join-Path $ProjectRoot "Assets\Generated\_Review\reference32_clean_tile_grass_matrix_v1\reference32_clean_tile_grass_matrix_report.json"

foreach ($path in @($queueScript, $processScript, $sheetScript)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing required script: $path" }
}
if (-not (Test-Path -LiteralPath $PythonExe)) { throw "Missing Python executable: $PythonExe" }

$training = @(
    Get-TrainingState "litiso_reference32_clean_tile_geometry_v1"
    Get-TrainingState "litiso_iso_reference_critter_style_v1"
)
$active = @($training | Where-Object { $_.running })
if (@($active).Count -gt 0 -and -not $DryRun.IsPresent) {
    throw "Refusing grass matrix while training is active: $(@($active | ForEach-Object { $_.output_name }) -join ', ')"
}

$matrix = @(
    [ordered]@{ label = "d038_l045_c090"; prefix = "reference32_matrix_d038_l045_c090"; denoise = 0.38; lora_strength = 0.45; control_strength = 0.90 }
    [ordered]@{ label = "d042_l055_c082"; prefix = "reference32_matrix_d042_l055_c082"; denoise = 0.42; lora_strength = 0.55; control_strength = 0.82 }
    [ordered]@{ label = "d046_l060_c078"; prefix = "reference32_matrix_d046_l060_c078"; denoise = 0.46; lora_strength = 0.60; control_strength = 0.78 }
    [ordered]@{ label = "d048_l065_c075"; prefix = "reference32_matrix_d048_l065_c075"; denoise = 0.48; lora_strength = 0.65; control_strength = 0.75 }
)

$records = @()
foreach ($combo in $matrix) {
    $queueArgs = @(
        "-B", $queueScript,
        "--project-root", $ProjectRoot,
        "--only", "grass_flat",
        "--job-prefix", $combo.prefix,
        "--lora", $LoraName,
        "--template-denoise", ([string]$combo.denoise),
        "--lora-strength", ([string]$combo.lora_strength),
        "--control-strength", ([string]$combo.control_strength)
    )
    if ($DryRun.IsPresent) { $queueArgs += "--dry-run" }
    & $PythonExe @queueArgs | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Queue failed for $($combo.label)" }

    $jobName = "$($combo.prefix)_grass_flat_v1"
    $requestPath = Join-Path $ProjectRoot "Temp\AssetForge\reference32_controlnet_tile_requests\$jobName\generation_request.json"
    $reviewRoot = Join-Path $ProjectRoot "Assets\Generated\_Review\$jobName"
    $status = "queued"
    if ($DryRun.IsPresent) {
        $status = "dry_run_queued"
    }
    elseif ((Test-Path -LiteralPath $reviewRoot) -and -not $ReplaceExisting.IsPresent) {
        $status = "existing_review_reused"
    }
    else {
        $processArgs = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $processScript, "-RequestPath", $requestPath)
        if ($ReplaceExisting.IsPresent) { $processArgs += "-ReplaceExisting" }
        & powershell.exe @processArgs | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Process failed for $jobName" }
        $status = "complete"
    }

    $records += [ordered]@{
        label = $combo.label
        job_name = $jobName
        request_path = Convert-ToRepoPath $requestPath
        review_root = Convert-ToRepoPath $reviewRoot
        denoise = $combo.denoise
        lora_strength = $combo.lora_strength
        control_strength = $combo.control_strength
        status = $status
    }
}

$manifest = [ordered]@{
    schema = "lit_iso.asset_forge.reference32_grass_matrix.v1"
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    dry_run = [bool]$DryRun
    training = $training
    matrix = $records
    contact_sheet = Convert-ToRepoPath $outSheet
    report = Convert-ToRepoPath $outReport
    note = "Review-only grass setting matrix. Do not import to Unity without explicit approval."
    lora_name = $LoraName
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $manifestPath) | Out-Null
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

if (-not $DryRun.IsPresent) {
    & $PythonExe -B $sheetScript --project-root $ProjectRoot --matrix-manifest (Convert-ToRepoPath $manifestPath) --out-sheet (Convert-ToRepoPath $outSheet) --out-report (Convert-ToRepoPath $outReport) | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Failed to build matrix sheet." }
}

[ordered]@{
    ok = $true
    dry_run = [bool]$DryRun
    manifest = Convert-ToRepoPath $manifestPath
    contact_sheet = Convert-ToRepoPath $outSheet
    report = Convert-ToRepoPath $outReport
    rows = @($records).Count
} | ConvertTo-Json -Depth 8
