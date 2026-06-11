param(
    [string]$ProjectRoot = "",
    [string]$PythonExe = "C:\Projects\ComfyUI\.venv\Scripts\python.exe",
    [string]$LoraName = "litiso_reference32_clean_tile_geometry_v1_final.safetensors",
    [double]$TemplateDenoise = 0.42,
    [double]$LoraStrength = 0.55,
    [double]$ControlStrength = 0.82,
    [ValidateSet("edge", "color")]
    [string]$ControlHint = "edge",
    [double]$ControlEnd = 0.9,
    [int]$Steps = 20,
    [double]$Cfg = 6.0,
    [int]$Variants = 2,
    [string]$ReviewName = "reference32_clean_tile_family_controlnet_v1",
    [switch]$ReplaceExisting,
    [switch]$QueueOnly,
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
$sheetScript = Join-Path $ProjectRoot "Tools\AssetForge\build_reference32_controlnet_family_sheet.py"
$safeReviewName = ($ReviewName -replace '[^A-Za-z0-9_.-]', '_').Trim("._")
if ([string]::IsNullOrWhiteSpace($safeReviewName)) { throw "-ReviewName must contain at least one safe character." }
$manifestPath = Join-Path $ProjectRoot "Temp\AssetForge\$safeReviewName.json"
$outSheet = Join-Path $ProjectRoot "Assets\Generated\_Review\$safeReviewName\reference32_clean_tile_family_contact_sheet.png"
$outReport = Join-Path $ProjectRoot "Assets\Generated\_Review\$safeReviewName\reference32_clean_tile_family_report.json"

foreach ($path in @($queueScript, $processScript, $sheetScript)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing required script: $path" }
}
if (-not (Test-Path -LiteralPath $PythonExe)) { throw "Missing Python executable: $PythonExe" }
if ($Variants -lt 1 -or $Variants -gt 4) { throw "-Variants must be between 1 and 4." }

$training = @(
    Get-TrainingState "litiso_reference32_clean_tile_geometry_v1"
    Get-TrainingState "litiso_iso_reference_critter_style_v1"
)
$active = @($training | Where-Object { $_.running })
if (@($active).Count -gt 0 -and -not $DryRun.IsPresent) {
    throw "Refusing tile family generation while training is active: $(@($active | ForEach-Object { $_.output_name }) -join ', ')"
}

$jobPrefix = "reference32_clean_family_d$($TemplateDenoise.ToString('0.00').Replace('.', ''))_l$($LoraStrength.ToString('0.00').Replace('.', ''))_c$($ControlStrength.ToString('0.00').Replace('.', ''))"
$queueArgs = @(
    "-B", $queueScript,
    "--project-root", $ProjectRoot,
    "--job-prefix", $jobPrefix,
    "--variants", ([string]$Variants),
    "--lora", $LoraName,
    "--template-denoise", ([string]$TemplateDenoise),
    "--lora-strength", ([string]$LoraStrength),
    "--control-strength", ([string]$ControlStrength),
    "--control-hint", $ControlHint,
    "--control-end", ([string]$ControlEnd),
    "--steps", ([string]$Steps),
    "--cfg", ([string]$Cfg)
)
if ($DryRun.IsPresent) { $queueArgs += "--dry-run" }

$queueText = & $PythonExe @queueArgs
if ($LASTEXITCODE -ne 0) { throw "Queue failed for Reference32 clean tile family." }
$queueText | Out-Host
$queuePlan = $queueText | ConvertFrom-Json

$jobs = @()
foreach ($created in $queuePlan.created) {
    $requestPath = Join-Path $ProjectRoot $created.request_path.Replace("/", "\")
    $reviewRoot = Join-Path $ProjectRoot "Assets\Generated\_Review\$($created.job_name)"
    $status = "queued"
    if ($DryRun.IsPresent) {
        $status = "dry_run_queued"
    }
    elseif ($QueueOnly.IsPresent) {
        $status = "queued_only"
    }
    elseif ((Test-Path -LiteralPath $reviewRoot) -and -not $ReplaceExisting.IsPresent) {
        $status = "existing_review_reused"
    }
    else {
        $processArgs = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $processScript, "-RequestPath", $requestPath)
        if ($ReplaceExisting.IsPresent) { $processArgs += "-ReplaceExisting" }
        & powershell.exe @processArgs | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Process failed for $($created.job_name)" }
        $status = "complete"
    }

    $jobs += [ordered]@{
        tile_id = $created.tile_id
        job_name = $created.job_name
        request_path = $created.request_path
        review_root = Convert-ToRepoPath $reviewRoot
        status = $status
        variants = $Variants
        template_denoise = $TemplateDenoise
        lora_name = $LoraName
        lora_strength = $LoraStrength
        control_strength = $ControlStrength
        control_hint = $ControlHint
        control_end = $ControlEnd
        steps = $Steps
        cfg = $Cfg
    }
}

$manifest = [ordered]@{
    schema = "lit_iso.asset_forge.reference32_clean_tile_family_controlnet.v1"
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    dry_run = [bool]$DryRun
    queue_only = [bool]$QueueOnly
    training = $training
    job_prefix = $jobPrefix
    lora_name = $LoraName
    template_denoise = $TemplateDenoise
    lora_strength = $LoraStrength
    control_strength = $ControlStrength
    control_hint = $ControlHint
    control_end = $ControlEnd
    steps = $Steps
    cfg = $Cfg
    variants = $Variants
    jobs = $jobs
    contact_sheet = Convert-ToRepoPath $outSheet
    report = Convert-ToRepoPath $outReport
    note = "Review-only Reference32 geometry-locked tile family. Do not import to Unity without explicit approval."
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $manifestPath) | Out-Null
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

if (-not $DryRun.IsPresent -and -not $QueueOnly.IsPresent) {
    & $PythonExe -B $sheetScript --project-root $ProjectRoot --run-manifest (Convert-ToRepoPath $manifestPath) --out-sheet (Convert-ToRepoPath $outSheet) --out-report (Convert-ToRepoPath $outReport) | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Failed to build Reference32 clean tile family sheet." }
}

[ordered]@{
    ok = $true
    dry_run = [bool]$DryRun
    queue_only = [bool]$QueueOnly
    manifest = Convert-ToRepoPath $manifestPath
    contact_sheet = Convert-ToRepoPath $outSheet
    report = Convert-ToRepoPath $outReport
    jobs = @($jobs).Count
} | ConvertTo-Json -Depth 8
