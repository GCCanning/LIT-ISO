param(
    [string]$ProjectRoot = "",
    [string]$PythonExe = "C:\Projects\ComfyUI\.venv\Scripts\python.exe",
    [string]$TrainingRoot = "C:\Projects\LoRA-Training",
    [string]$ComfyRoot = "C:\Projects\ComfyUI",
    [switch]$SkipRebuild
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

function Read-JsonFile {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    try {
        return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
    }
    catch {
        return [ordered]@{ unreadable = $true; path = $Path; error = $_.Exception.Message }
    }
}

function Get-TrainingSnapshot {
    param([string]$OutputName)
    $statusScript = Join-Path $ProjectRoot "Tools\LoRA\status_litiso_training.ps1"
    if (-not (Test-Path -LiteralPath $statusScript)) {
        return [ordered]@{ output_name = $OutputName; state = "missing_status_script" }
    }
    try {
        $json = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $statusScript -TrainingRoot $TrainingRoot -OutputName $OutputName -Json
        return $json | ConvertFrom-Json
    }
    catch {
        return [ordered]@{ output_name = $OutputName; state = "status_error"; error = $_.Exception.Message }
    }
}

function Get-LoraSnapshot {
    param([string]$OutputName)
    $syncManifestPath = Join-Path $ComfyRoot "models\loras\$OutputName.sync.json"
    $syncManifest = Read-JsonFile -Path $syncManifestPath
    $destination = if ($syncManifest -and $syncManifest.copied) { [string]$syncManifest.copied } else { Join-Path $ComfyRoot "models\loras\$OutputName`_final.safetensors" }
    $exists = Test-Path -LiteralPath $destination
    $hash = $null
    if ($exists) {
        try { $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $destination).Hash } catch { $hash = $null }
    }
    return [ordered]@{
        output_name = $OutputName
        synced_lora = $destination
        exists = $exists
        sha256 = $hash
        sync_manifest = if (Test-Path -LiteralPath $syncManifestPath) { $syncManifestPath } else { $null }
        source_sha256 = if ($syncManifest -and $syncManifest.source_sha256) { $syncManifest.source_sha256 } else { $null }
    }
}

function Run-PythonTool {
    param([string[]]$Arguments)
    & $PythonExe @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Python tool failed: $PythonExe $($Arguments -join ' ')"
    }
}

$tileSelector = Join-Path $ProjectRoot "Tools\AssetForge\select_reference32_style_locked_family.py"
$tileScore = Join-Path $ProjectRoot "Tools\AssetForge\score_reference32_style_lock.py"
$tileAiGate = Join-Path $ProjectRoot "Tools\AssetForge\gate_reference32_ai_tile_candidates.py"
$tileMaskLocked = Join-Path $ProjectRoot "Tools\AssetForge\build_reference32_mask_locked_texture_variants.py"
$screenshotPalette = Join-Path $ProjectRoot "Tools\AssetForge\analyze_litiso_screenshot_palette.py"
$mageGate = Join-Path $ProjectRoot "Tools\AssetForge\build_black_mage_selected_review_gate.py"
$mageDirectionCoverage = Join-Path $ProjectRoot "Tools\AssetForge\build_black_mage_direction_coverage_report.py"
$readinessAudit = Join-Path $ProjectRoot "Tools\AssetForge\build_litiso_asset_pipeline_readiness_report.py"
$tileCapturePlan = Join-Path $ProjectRoot "Tools\AssetForge\build_tile_selected_training_capture_plan.py"
$datasetCapture = Join-Path $ProjectRoot "Tools\AssetForge\capture_approved_review_pack.py"
$visualDeltaReport = Join-Path $ProjectRoot "Tools\AssetForge\build_litiso_pipeline_visual_delta_report.py"
$mageIdentityLock = Join-Path $ProjectRoot "Tools\AssetForge\build_black_mage_identity_lock_report.py"
$mageReferenceAnchor = Join-Path $ProjectRoot "Tools\AssetForge\build_black_mage_reference_anchor_pack.py"
$mageV14PartialReview = Join-Path $ProjectRoot "Tools\AssetForge\build_black_mage_request_identity_review.py"
foreach ($path in @($PythonExe, $tileSelector, $tileScore, $tileAiGate, $tileMaskLocked, $screenshotPalette, $mageGate, $mageDirectionCoverage, $readinessAudit, $tileCapturePlan, $datasetCapture, $visualDeltaReport, $mageIdentityLock, $mageReferenceAnchor, $mageV14PartialReview)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing required path: $path" }
}

$artifacts = [ordered]@{
    tile_source_family = "Assets/Generated/_Review/reference32_selected_tile_family_source_v1"
    tile_litiso_green_family = "Assets/Generated/_Review/reference32_selected_tile_family_litiso_green_v1"
    screenshot_palette = "Assets/Generated/_Review/litiso_screenshot_palette_v1"
    tile_mask_locked_variants = "Assets/Generated/_Review/reference32_mask_locked_texture_variants_v5"
    tile_mask_locked_forest = "Assets/Generated/_Review/reference32_mask_locked_texture_family_forest_moss_v1"
    tile_mask_locked_plains = "Assets/Generated/_Review/reference32_mask_locked_texture_family_plains_sun_v1"
    tile_mask_locked_screenshot_balanced = "Assets/Generated/_Review/reference32_mask_locked_texture_family_screenshot_balanced_v1"
    black_mage_v11_gate = "Assets/Generated/_Review/black_mage_iso_selected_v11"
    black_mage_v12_cardinals = "Assets/Generated/_Review/black_mage_iso_renders_v12_cardinals"
    black_mage_v12_cardinal_selection = "Assets/Generated/_Review/black_mage_iso_selected_v12_cardinals"
    black_mage_v12_mixed_8d = "Assets/Generated/_Review/black_mage_iso_selected_v12_mixed_8d"
    black_mage_v13_side = "Assets/Generated/_Review/black_mage_iso_renders_v13_side"
    black_mage_v13_side_selection = "Assets/Generated/_Review/black_mage_iso_selected_v13_side"
    black_mage_v13_mixed_8d = "Assets/Generated/_Review/black_mage_iso_selected_v13_mixed_8d"
    black_mage_identity_lock = "Assets/Generated/_Review/black_mage_identity_lock_v1"
    black_mage_reference_anchor = "Assets/Generated/_Review/black_mage_reference_anchor_v1"
    black_mage_v14_identity_partial = "Assets/Generated/_Review/black_mage_v14_identity_partial"
}

$tileBalancedCaptureDryRun = "Temp/AssetForge/dataset_capture_plans/reference32_mask_locked_texture_family_screenshot_balanced_v1_capture_dry_run.json"
$mageV13CaptureDryRun = "Temp/AssetForge/dataset_capture_plans/black_mage_iso_selected_v13_mixed_8d_capture_dry_run.json"
$visualDeltaRoot = "Assets/Generated/_Review/litiso_pipeline_visual_delta_v1"

if (-not $SkipRebuild.IsPresent) {
    Run-PythonTool -Arguments @(
        "-B", $tileSelector,
        "--project-root", $ProjectRoot,
        "--default-variant", "source",
        "--output-root", $artifacts.tile_source_family
    )
    Run-PythonTool -Arguments @(
        "-B", $tileSelector,
        "--project-root", $ProjectRoot,
        "--default-variant", "litiso_green",
        "--output-root", $artifacts.tile_litiso_green_family
    )
    Run-PythonTool -Arguments @(
        "-B", $tileScore,
        "--project-root", $ProjectRoot,
        "--candidate-manifest", "$($artifacts.tile_source_family)/selected_tile_family_manifest.json"
    )
    Run-PythonTool -Arguments @(
        "-B", $tileScore,
        "--project-root", $ProjectRoot,
        "--candidate-manifest", "$($artifacts.tile_litiso_green_family)/selected_tile_family_manifest.json"
    )
    Run-PythonTool -Arguments @(
        "-B", $screenshotPalette,
        "--project-root", $ProjectRoot,
        "--output-root", $artifacts.screenshot_palette
    )
    Run-PythonTool -Arguments @(
        "-B", $tileMaskLocked,
        "--project-root", $ProjectRoot,
        "--output-root", $artifacts.tile_mask_locked_variants
    )
    Run-PythonTool -Arguments @(
        "-B", $tileSelector,
        "--project-root", $ProjectRoot,
        "--source-manifest", "$($artifacts.tile_mask_locked_variants)/reference32_mask_locked_texture_variants_manifest.json",
        "--default-variant", "forest_moss",
        "--output-root", $artifacts.tile_mask_locked_forest
    )
    Run-PythonTool -Arguments @(
        "-B", $tileSelector,
        "--project-root", $ProjectRoot,
        "--source-manifest", "$($artifacts.tile_mask_locked_variants)/reference32_mask_locked_texture_variants_manifest.json",
        "--default-variant", "plains_sun",
        "--output-root", $artifacts.tile_mask_locked_plains
    )
    Run-PythonTool -Arguments @(
        "-B", $tileSelector,
        "--project-root", $ProjectRoot,
        "--source-manifest", "$($artifacts.tile_mask_locked_variants)/reference32_mask_locked_texture_variants_manifest.json",
        "--default-variant", "screenshot_balanced",
        "--output-root", $artifacts.tile_mask_locked_screenshot_balanced
    )
    Run-PythonTool -Arguments @(
        "-B", $tileScore,
        "--project-root", $ProjectRoot,
        "--candidate-manifest", "$($artifacts.tile_mask_locked_forest)/selected_tile_family_manifest.json"
    )
    Run-PythonTool -Arguments @(
        "-B", $tileScore,
        "--project-root", $ProjectRoot,
        "--candidate-manifest", "$($artifacts.tile_mask_locked_plains)/selected_tile_family_manifest.json"
    )
    Run-PythonTool -Arguments @(
        "-B", $tileScore,
        "--project-root", $ProjectRoot,
        "--candidate-manifest", "$($artifacts.tile_mask_locked_screenshot_balanced)/selected_tile_family_manifest.json"
    )
    $aiReports = @(
        "Assets/Generated/_Review/reference32_clean_tile_family_controlnet_v1/reference32_clean_tile_family_report.json",
        "Assets/Generated/_Review/reference32_clean_tile_family_lowdenoise_v1/reference32_clean_tile_family_report.json"
    )
    $missingAiReports = @($aiReports | Where-Object { -not (Test-Path -LiteralPath (Join-Path $ProjectRoot $_)) })
    if ($missingAiReports.Count -eq 0) {
        $gateArgs = @(
            "-B", $tileAiGate,
            "--project-root", $ProjectRoot,
            "--reports"
        ) + $aiReports + @(
            "--output-root", "Assets/Generated/_Review/reference32_ai_candidate_gate_v1"
        )
        Run-PythonTool -Arguments $gateArgs
    }
    Run-PythonTool -Arguments @(
        "-B", $mageGate,
        "--project-root", $ProjectRoot,
        "--selected-manifest", "Assets/Generated/_Review/black_mage_iso_selected_v11/black_mage_selected_v11_manifest.json"
    )
    Run-PythonTool -Arguments @(
        "-B", $mageDirectionCoverage,
        "--project-root", $ProjectRoot,
        "--selected-manifest", "Assets/Generated/_Review/black_mage_iso_selected_v11/black_mage_selected_v11_manifest.json"
    )
}

Run-PythonTool -Arguments @(
    "-B", $tileCapturePlan,
    "--project-root", $ProjectRoot,
    "--selected-manifest", "$($artifacts.tile_mask_locked_screenshot_balanced)/selected_tile_family_manifest.json"
)
Run-PythonTool -Arguments @(
    "-B", $datasetCapture,
    "--project-root", $ProjectRoot,
    "--pack-name", "reference32_mask_locked_texture_family_screenshot_balanced_v1",
    "--output-report", $tileBalancedCaptureDryRun
)
Run-PythonTool -Arguments @(
    "-B", $datasetCapture,
    "--project-root", $ProjectRoot,
    "--pack-name", "black_mage_iso_selected_v13_mixed_8d",
    "--output-report", $mageV13CaptureDryRun
)
Run-PythonTool -Arguments @(
    "-B", $visualDeltaReport,
    "--project-root", $ProjectRoot,
    "--tile-manifest", "$($artifacts.tile_mask_locked_screenshot_balanced)/selected_tile_family_manifest.json",
    "--mage-manifest", "$($artifacts.black_mage_v13_mixed_8d)/black_mage_selected_v13_mixed_8d_manifest.json",
    "--output-root", $visualDeltaRoot
)
Run-PythonTool -Arguments @(
    "-B", $mageIdentityLock,
    "--project-root", $ProjectRoot,
    "--selected-manifest", "$($artifacts.black_mage_v13_mixed_8d)/black_mage_selected_v13_mixed_8d_manifest.json",
    "--output-root", $artifacts.black_mage_identity_lock
)
Run-PythonTool -Arguments @(
    "-B", $mageReferenceAnchor,
    "--project-root", $ProjectRoot,
    "--selected-manifest", "$($artifacts.black_mage_v13_mixed_8d)/black_mage_selected_v13_mixed_8d_manifest.json",
    "--identity-report", "$($artifacts.black_mage_identity_lock)/black_mage_identity_lock_report.json",
    "--output-root", $artifacts.black_mage_reference_anchor
)
$v14CleanRoot = Join-Path $ProjectRoot "Temp\AssetForge\black_mage_requests\black_mage_iso_idle_s_v14_identity\Outputs\cleaned"
if ((Test-Path -LiteralPath $v14CleanRoot) -and @((Get-ChildItem -LiteralPath $v14CleanRoot -Filter "*.png" -File -ErrorAction SilentlyContinue)).Count -gt 0) {
    Run-PythonTool -Arguments @(
        "-B", $mageV14PartialReview,
        "--project-root", $ProjectRoot,
        "--output-root", $artifacts.black_mage_v14_identity_partial
    )
}

$tileSourceDecisions = Read-JsonFile -Path (Join-Path $ProjectRoot "$($artifacts.tile_source_family)\review_decisions.json")
$tileGreenDecisions = Read-JsonFile -Path (Join-Path $ProjectRoot "$($artifacts.tile_litiso_green_family)\review_decisions.json")
$mageDecisions = Read-JsonFile -Path (Join-Path $ProjectRoot "$($artifacts.black_mage_v11_gate)\review_decisions.json")
$tileSourceScore = Read-JsonFile -Path (Join-Path $ProjectRoot "$($artifacts.tile_source_family)\style_lock_scorecard.json")
$tileGreenScore = Read-JsonFile -Path (Join-Path $ProjectRoot "$($artifacts.tile_litiso_green_family)\style_lock_scorecard.json")
$tileMaskForestScore = Read-JsonFile -Path (Join-Path $ProjectRoot "$($artifacts.tile_mask_locked_forest)\style_lock_scorecard.json")
$tileMaskPlainsScore = Read-JsonFile -Path (Join-Path $ProjectRoot "$($artifacts.tile_mask_locked_plains)\style_lock_scorecard.json")
$tileMaskBalancedScore = Read-JsonFile -Path (Join-Path $ProjectRoot "$($artifacts.tile_mask_locked_screenshot_balanced)\style_lock_scorecard.json")
$tileMaskForestDecisions = Read-JsonFile -Path (Join-Path $ProjectRoot "$($artifacts.tile_mask_locked_forest)\review_decisions.json")
$tileMaskPlainsDecisions = Read-JsonFile -Path (Join-Path $ProjectRoot "$($artifacts.tile_mask_locked_plains)\review_decisions.json")
$tileMaskBalancedDecisions = Read-JsonFile -Path (Join-Path $ProjectRoot "$($artifacts.tile_mask_locked_screenshot_balanced)\review_decisions.json")
$tileMaskBalancedCapturePlan = Read-JsonFile -Path (Join-Path $ProjectRoot "$($artifacts.tile_mask_locked_screenshot_balanced)\training_capture_plan.json")
$tileMaskBalancedCaptureDryRun = Read-JsonFile -Path (Join-Path $ProjectRoot $tileBalancedCaptureDryRun)
$visualDelta = Read-JsonFile -Path (Join-Path $ProjectRoot "$visualDeltaRoot\litiso_pipeline_visual_delta_report.json")
$mageIdentityLockReport = Read-JsonFile -Path (Join-Path $ProjectRoot "$($artifacts.black_mage_identity_lock)\black_mage_identity_lock_report.json")
$mageReferenceAnchorManifest = Read-JsonFile -Path (Join-Path $ProjectRoot "$($artifacts.black_mage_reference_anchor)\black_mage_reference_anchor_manifest.json")
$mageV14PartialReport = Read-JsonFile -Path (Join-Path $ProjectRoot "$($artifacts.black_mage_v14_identity_partial)\black_mage_v14_identity_partial_report.json")
$tileAiGateReport = Read-JsonFile -Path (Join-Path $ProjectRoot "Assets\Generated\_Review\reference32_ai_candidate_gate_v1\candidate_gate_report.json")
$tileCopyLockGateReport = Read-JsonFile -Path (Join-Path $ProjectRoot "Assets\Generated\_Review\reference32_copylock_ai_gate_d012_l018_c100_color_v1\candidate_gate_report.json")
$tileCopyLockManifest = Read-JsonFile -Path (Join-Path $ProjectRoot "Temp\AssetForge\reference32_copylock_tile_family_d012_l018_c100_color_v1.json")
$mageDirectionCoverageReport = Read-JsonFile -Path (Join-Path $ProjectRoot "$($artifacts.black_mage_v11_gate)\direction_coverage_report.json")
$mageV12CardinalQc = Read-JsonFile -Path (Join-Path $ProjectRoot "$($artifacts.black_mage_v12_cardinals)\_v12_cardinals_strict_qc_report.json")
$mageV12MixedCoverageReport = Read-JsonFile -Path (Join-Path $ProjectRoot "$($artifacts.black_mage_v12_mixed_8d)\direction_coverage_report.json")
$mageV13SideQc = Read-JsonFile -Path (Join-Path $ProjectRoot "$($artifacts.black_mage_v13_side)\_v13_side_strict_qc_report.json")
$mageV13MixedCoverageReport = Read-JsonFile -Path (Join-Path $ProjectRoot "$($artifacts.black_mage_v13_mixed_8d)\direction_coverage_report.json")
$mageV13MixedDecisions = Read-JsonFile -Path (Join-Path $ProjectRoot "$($artifacts.black_mage_v13_mixed_8d)\review_decisions.json")
$mageV13CaptureDryRunReport = Read-JsonFile -Path (Join-Path $ProjectRoot $mageV13CaptureDryRun)
$blackMageV12RequestRoot = Join-Path $ProjectRoot "Temp\AssetForge\black_mage_requests"
$blackMageV12Requests = @()
if (Test-Path -LiteralPath $blackMageV12RequestRoot) {
    $blackMageV12Requests = @(Get-ChildItem -LiteralPath $blackMageV12RequestRoot -Directory -Filter "black_mage_iso_idle_*_v12" -ErrorAction SilentlyContinue)
}

$summary = [ordered]@{
    schema = "lit_iso.asset_forge.review_golden_path_status.v1"
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    mode = if ($SkipRebuild.IsPresent) { "status_only" } else { "rebuilt_review_artifacts" }
    no_credit = $true
    unity_imported = $false
    training = [ordered]@{
        tile = Get-TrainingSnapshot -OutputName "litiso_reference32_clean_tile_geometry_v1"
        critter = Get-TrainingSnapshot -OutputName "litiso_iso_reference_critter_style_v1"
    }
    comfy_loras = [ordered]@{
        tile = Get-LoraSnapshot -OutputName "litiso_reference32_clean_tile_geometry_v1"
        critter = Get-LoraSnapshot -OutputName "litiso_iso_reference_critter_style_v1"
    }
    review_artifacts = [ordered]@{
        tile_source_family = [ordered]@{
            root = $artifacts.tile_source_family
            contact_sheet = "$($artifacts.tile_source_family)/selected_tile_family_contact_sheet.png"
            map_preview = "$($artifacts.tile_source_family)/selected_tile_family_map_preview.png"
            manifest = "$($artifacts.tile_source_family)/selected_tile_family_manifest.json"
            decisions = "$($artifacts.tile_source_family)/review_decisions.json"
            style_lock_scorecard = "$($artifacts.tile_source_family)/style_lock_scorecard.json"
            style_lock_score_sheet = "$($artifacts.tile_source_family)/style_lock_score_sheet.png"
            style_lock_score_mean = if ($tileSourceScore) { $tileSourceScore.score_mean } else { $null }
            style_lock_pass_count = if ($tileSourceScore) { $tileSourceScore.pass_count } else { $null }
            total = if ($tileSourceDecisions) { $tileSourceDecisions.total } else { $null }
            pending = if ($tileSourceDecisions) { $tileSourceDecisions.pending_count } else { $null }
        }
        tile_litiso_green_family = [ordered]@{
            root = $artifacts.tile_litiso_green_family
            contact_sheet = "$($artifacts.tile_litiso_green_family)/selected_tile_family_contact_sheet.png"
            map_preview = "$($artifacts.tile_litiso_green_family)/selected_tile_family_map_preview.png"
            manifest = "$($artifacts.tile_litiso_green_family)/selected_tile_family_manifest.json"
            decisions = "$($artifacts.tile_litiso_green_family)/review_decisions.json"
            style_lock_scorecard = "$($artifacts.tile_litiso_green_family)/style_lock_scorecard.json"
            style_lock_score_sheet = "$($artifacts.tile_litiso_green_family)/style_lock_score_sheet.png"
            style_lock_score_mean = if ($tileGreenScore) { $tileGreenScore.score_mean } else { $null }
            style_lock_pass_count = if ($tileGreenScore) { $tileGreenScore.pass_count } else { $null }
            total = if ($tileGreenDecisions) { $tileGreenDecisions.total } else { $null }
            pending = if ($tileGreenDecisions) { $tileGreenDecisions.pending_count } else { $null }
        }
        tile_mask_locked_variants = [ordered]@{
            root = $artifacts.tile_mask_locked_variants
            contact_sheet = "$($artifacts.tile_mask_locked_variants)/reference32_mask_locked_texture_variants_contact_sheet.png"
            map_preview = "$($artifacts.tile_mask_locked_variants)/reference32_mask_locked_texture_variants_map_preview.png"
            manifest = "$($artifacts.tile_mask_locked_variants)/reference32_mask_locked_texture_variants_manifest.json"
        }
        screenshot_palette = [ordered]@{
            root = $artifacts.screenshot_palette
            manifest = "$($artifacts.screenshot_palette)/litiso_screenshot_material_palette.json"
            swatch = "$($artifacts.screenshot_palette)/litiso_screenshot_material_palette.png"
        }
        tile_mask_locked_forest = [ordered]@{
            root = $artifacts.tile_mask_locked_forest
            contact_sheet = "$($artifacts.tile_mask_locked_forest)/selected_tile_family_contact_sheet.png"
            map_preview = "$($artifacts.tile_mask_locked_forest)/selected_tile_family_map_preview.png"
            manifest = "$($artifacts.tile_mask_locked_forest)/selected_tile_family_manifest.json"
            style_lock_scorecard = "$($artifacts.tile_mask_locked_forest)/style_lock_scorecard.json"
            style_lock_score_sheet = "$($artifacts.tile_mask_locked_forest)/style_lock_score_sheet.png"
            style_lock_score_mean = if ($tileMaskForestScore) { $tileMaskForestScore.score_mean } else { $null }
            style_lock_pass_count = if ($tileMaskForestScore) { $tileMaskForestScore.pass_count } else { $null }
            total = if ($tileMaskForestDecisions) { $tileMaskForestDecisions.total } else { $null }
            pending = if ($tileMaskForestDecisions) { $tileMaskForestDecisions.pending_count } else { $null }
        }
        tile_mask_locked_plains = [ordered]@{
            root = $artifacts.tile_mask_locked_plains
            contact_sheet = "$($artifacts.tile_mask_locked_plains)/selected_tile_family_contact_sheet.png"
            map_preview = "$($artifacts.tile_mask_locked_plains)/selected_tile_family_map_preview.png"
            manifest = "$($artifacts.tile_mask_locked_plains)/selected_tile_family_manifest.json"
            style_lock_scorecard = "$($artifacts.tile_mask_locked_plains)/style_lock_scorecard.json"
            style_lock_score_sheet = "$($artifacts.tile_mask_locked_plains)/style_lock_score_sheet.png"
            style_lock_score_mean = if ($tileMaskPlainsScore) { $tileMaskPlainsScore.score_mean } else { $null }
            style_lock_pass_count = if ($tileMaskPlainsScore) { $tileMaskPlainsScore.pass_count } else { $null }
            total = if ($tileMaskPlainsDecisions) { $tileMaskPlainsDecisions.total } else { $null }
            pending = if ($tileMaskPlainsDecisions) { $tileMaskPlainsDecisions.pending_count } else { $null }
        }
        tile_mask_locked_screenshot_balanced = [ordered]@{
            root = $artifacts.tile_mask_locked_screenshot_balanced
            contact_sheet = "$($artifacts.tile_mask_locked_screenshot_balanced)/selected_tile_family_contact_sheet.png"
            map_preview = "$($artifacts.tile_mask_locked_screenshot_balanced)/selected_tile_family_map_preview.png"
            manifest = "$($artifacts.tile_mask_locked_screenshot_balanced)/selected_tile_family_manifest.json"
            style_lock_scorecard = "$($artifacts.tile_mask_locked_screenshot_balanced)/style_lock_scorecard.json"
            style_lock_score_sheet = "$($artifacts.tile_mask_locked_screenshot_balanced)/style_lock_score_sheet.png"
            style_lock_score_mean = if ($tileMaskBalancedScore) { $tileMaskBalancedScore.score_mean } else { $null }
            style_lock_pass_count = if ($tileMaskBalancedScore) { $tileMaskBalancedScore.pass_count } else { $null }
            total = if ($tileMaskBalancedDecisions) { $tileMaskBalancedDecisions.total } else { $null }
            pending = if ($tileMaskBalancedDecisions) { $tileMaskBalancedDecisions.pending_count } else { $null }
            training_capture_plan = "$($artifacts.tile_mask_locked_screenshot_balanced)/training_capture_plan.json"
            training_capture_plan_records = if ($tileMaskBalancedCapturePlan) { @($tileMaskBalancedCapturePlan.records).Count } else { $null }
            dataset_capture_dry_run = $tileBalancedCaptureDryRun
            dataset_capture_planned_records = if ($tileMaskBalancedCaptureDryRun) { $tileMaskBalancedCaptureDryRun.planned_record_count } else { $null }
            dataset_capture_ready_for_apply = if ($tileMaskBalancedCaptureDryRun) { $tileMaskBalancedCaptureDryRun.ready_for_apply } else { $null }
            visual_delta_report = "$visualDeltaRoot/litiso_pipeline_visual_delta_report.json"
            visual_delta_board = "$visualDeltaRoot/litiso_pipeline_visual_delta_board.png"
        }
        black_mage_v11 = [ordered]@{
            root = $artifacts.black_mage_v11_gate
            contact_sheet = "$($artifacts.black_mage_v11_gate)/black_mage_selected_v11_contact_sheet.png"
            manifest = "$($artifacts.black_mage_v11_gate)/black_mage_selected_v11_manifest.json"
            decisions = "$($artifacts.black_mage_v11_gate)/review_decisions.json"
            training_capture_plan = "$($artifacts.black_mage_v11_gate)/training_capture_plan.json"
            direction_coverage_report = "$($artifacts.black_mage_v11_gate)/direction_coverage_report.json"
            direction_coverage_sheet = "$($artifacts.black_mage_v11_gate)/black_mage_direction_coverage_sheet.png"
            present_directions = if ($mageDirectionCoverageReport) { $mageDirectionCoverageReport.present_directions } else { $null }
            missing_directions = if ($mageDirectionCoverageReport) { $mageDirectionCoverageReport.missing_directions } else { $null }
            complete_4d_cardinal_set = if ($mageDirectionCoverageReport) { $mageDirectionCoverageReport.complete_4d_cardinal_set } else { $null }
            complete_8d_set = if ($mageDirectionCoverageReport) { $mageDirectionCoverageReport.complete_8d_set } else { $null }
            animation_ready = if ($mageDirectionCoverageReport) { $mageDirectionCoverageReport.animation_ready } else { $null }
            total = if ($mageDecisions) { $mageDecisions.total } else { $null }
            pending = if ($mageDecisions) { $mageDecisions.pending_count } else { $null }
        }
        black_mage_v12_preflight = [ordered]@{
            template_manifest = "Assets/Generated/_Review/black_mage_direction_templates_v3/black_mage_direction_templates_v3_manifest.json"
            template_sheet = "Assets/Generated/_Review/black_mage_direction_templates_v3/black_mage_direction_templates_v3_sheet.png"
            pose_manifest = "Assets/Generated/_Review/_PoseControls/litiso_openpose_8d_v1/idle_manifest.json"
            pose_sheet = "Assets/Generated/_Review/_PoseControls/litiso_openpose_8d_v1/idle_8d_contact.png"
            request_root = "Temp/AssetForge/black_mage_requests"
            queued_request_count = $blackMageV12Requests.Count
            queued_request_dirs = @($blackMageV12Requests | ForEach-Object { $_.Name -replace "^black_mage_iso_idle_", "" -replace "_v12$", "" } | ForEach-Object { $_.ToUpperInvariant() })
            status = "queued_request_json_only_no_comfy_render_started"
        }
        black_mage_v12_cardinals = [ordered]@{
            root = $artifacts.black_mage_v12_cardinals
            candidate_sheet = "$($artifacts.black_mage_v12_cardinals)/_v12_cardinals_candidate_review_sheet.png"
            candidate_manifest = "$($artifacts.black_mage_v12_cardinals)/_v12_cardinals_candidate_manifest.json"
            strict_qc_report = "$($artifacts.black_mage_v12_cardinals)/_v12_cardinals_strict_qc_report.json"
            strict_qc_sheet = "$($artifacts.black_mage_v12_cardinals)/_v12_cardinals_strict_qc_sheet.png"
            candidate_count = if ($mageV12CardinalQc) { $mageV12CardinalQc.candidate_count } else { $null }
            reject_count = if ($mageV12CardinalQc) { $mageV12CardinalQc.reject_count } else { $null }
            review_candidate_count = if ($mageV12CardinalQc) { $mageV12CardinalQc.review_candidate_count } else { $null }
            selected_root = $artifacts.black_mage_v12_cardinal_selection
            selected_sheet = "$($artifacts.black_mage_v12_cardinal_selection)/black_mage_selected_v12_cardinals_contact_sheet.png"
            selected_manifest = "$($artifacts.black_mage_v12_cardinal_selection)/black_mage_selected_v12_cardinals_manifest.json"
            status = "review_only_not_unity_imported"
        }
        black_mage_v12_mixed_8d = [ordered]@{
            root = $artifacts.black_mage_v12_mixed_8d
            contact_sheet = "$($artifacts.black_mage_v12_mixed_8d)/black_mage_selected_v12_mixed_8d_contact_sheet.png"
            manifest = "$($artifacts.black_mage_v12_mixed_8d)/black_mage_selected_v12_mixed_8d_manifest.json"
            direction_coverage_report = "$($artifacts.black_mage_v12_mixed_8d)/direction_coverage_report.json"
            direction_coverage_sheet = "$($artifacts.black_mage_v12_mixed_8d)/black_mage_direction_coverage_sheet.png"
            present_directions = if ($mageV12MixedCoverageReport) { $mageV12MixedCoverageReport.present_directions } else { $null }
            missing_directions = if ($mageV12MixedCoverageReport) { $mageV12MixedCoverageReport.missing_directions } else { $null }
            complete_8d_set = if ($mageV12MixedCoverageReport) { $mageV12MixedCoverageReport.complete_8d_set } else { $null }
            animation_ready = if ($mageV12MixedCoverageReport) { $mageV12MixedCoverageReport.animation_ready } else { $null }
            art_direction_note = "Coverage is complete, but E/W still need manual review because the current best side views read partly front-facing."
            status = "review_only_not_unity_imported"
        }
        black_mage_v13_side = [ordered]@{
            root = $artifacts.black_mage_v13_side
            candidate_sheet = "$($artifacts.black_mage_v13_side)/_v13_side_candidate_review_sheet.png"
            candidate_manifest = "$($artifacts.black_mage_v13_side)/_v13_side_candidate_manifest.json"
            strict_qc_report = "$($artifacts.black_mage_v13_side)/_v13_side_strict_qc_report.json"
            strict_qc_sheet = "$($artifacts.black_mage_v13_side)/_v13_side_strict_qc_sheet.png"
            candidate_count = if ($mageV13SideQc) { $mageV13SideQc.candidate_count } else { $null }
            reject_count = if ($mageV13SideQc) { $mageV13SideQc.reject_count } else { $null }
            review_candidate_count = if ($mageV13SideQc) { $mageV13SideQc.review_candidate_count } else { $null }
            selected_root = $artifacts.black_mage_v13_side_selection
            selected_sheet = "$($artifacts.black_mage_v13_side_selection)/black_mage_selected_v13_side_contact_sheet.png"
            selected_manifest = "$($artifacts.black_mage_v13_side_selection)/black_mage_selected_v13_side_manifest.json"
            settings = [ordered]@{
                style_weight = 0.42
                control_strength = 0.88
                template_denoise = 0.34
                batch_count = 4
            }
            status = "review_only_not_unity_imported"
        }
        black_mage_v13_mixed_8d = [ordered]@{
            root = $artifacts.black_mage_v13_mixed_8d
            contact_sheet = "$($artifacts.black_mage_v13_mixed_8d)/black_mage_selected_v13_mixed_8d_contact_sheet.png"
            manifest = "$($artifacts.black_mage_v13_mixed_8d)/black_mage_selected_v13_mixed_8d_manifest.json"
            direction_coverage_report = "$($artifacts.black_mage_v13_mixed_8d)/direction_coverage_report.json"
            direction_coverage_sheet = "$($artifacts.black_mage_v13_mixed_8d)/black_mage_direction_coverage_sheet.png"
            review_report = "$($artifacts.black_mage_v13_mixed_8d)/review_report.json"
            review_decisions = "$($artifacts.black_mage_v13_mixed_8d)/review_decisions.json"
            training_capture_plan = "$($artifacts.black_mage_v13_mixed_8d)/training_capture_plan.json"
            present_directions = if ($mageV13MixedCoverageReport) { $mageV13MixedCoverageReport.present_directions } else { $null }
            missing_directions = if ($mageV13MixedCoverageReport) { $mageV13MixedCoverageReport.missing_directions } else { $null }
            complete_8d_set = if ($mageV13MixedCoverageReport) { $mageV13MixedCoverageReport.complete_8d_set } else { $null }
            animation_ready = if ($mageV13MixedCoverageReport) { $mageV13MixedCoverageReport.animation_ready } else { $null }
            total = if ($mageV13MixedDecisions) { $mageV13MixedDecisions.total } else { $null }
            pending = if ($mageV13MixedDecisions) { $mageV13MixedDecisions.pending_count } else { $null }
            approved = if ($mageV13MixedDecisions) { $mageV13MixedDecisions.approved_count } else { $null }
            dataset_capture_dry_run = $mageV13CaptureDryRun
            dataset_capture_planned_records = if ($mageV13CaptureDryRunReport) { $mageV13CaptureDryRunReport.planned_record_count } else { $null }
            dataset_capture_ready_for_apply = if ($mageV13CaptureDryRunReport) { $mageV13CaptureDryRunReport.ready_for_apply } else { $null }
            art_direction_note = "Current best 8D evidence. E/W are improved over v12 but still need user approval before training."
            status = "review_only_not_unity_imported"
        }
        black_mage_identity_lock = [ordered]@{
            root = $artifacts.black_mage_identity_lock
            report = "$($artifacts.black_mage_identity_lock)/black_mage_identity_lock_report.json"
            board = "$($artifacts.black_mage_identity_lock)/black_mage_identity_lock_board.png"
            status = if ($mageIdentityLockReport) { $mageIdentityLockReport.status } else { $null }
            direction_count = if ($mageIdentityLockReport) { $mageIdentityLockReport.direction_count } else { $null }
            identity_fail_count = if ($mageIdentityLockReport) { $mageIdentityLockReport.identity_fail_count } else { $null }
            identity_review_count = if ($mageIdentityLockReport) { $mageIdentityLockReport.identity_review_count } else { $null }
            conclusion = if ($mageIdentityLockReport) { $mageIdentityLockReport.conclusion } else { $null }
        }
        black_mage_reference_anchor = [ordered]@{
            root = $artifacts.black_mage_reference_anchor
            manifest = "$($artifacts.black_mage_reference_anchor)/black_mage_reference_anchor_manifest.json"
            sheet = "$($artifacts.black_mage_reference_anchor)/black_mage_reference_anchor_sheet.png"
            source_anchor = "$($artifacts.black_mage_reference_anchor)/black_mage_s_source_anchor.png"
            next_recommendation = if ($mageReferenceAnchorManifest) { $mageReferenceAnchorManifest.next_recommendation } else { $null }
            status = if ($mageReferenceAnchorManifest) { $mageReferenceAnchorManifest.status } else { $null }
        }
        black_mage_v14_identity_partial = [ordered]@{
            root = $artifacts.black_mage_v14_identity_partial
            report = "$($artifacts.black_mage_v14_identity_partial)/black_mage_v14_identity_partial_report.json"
            sheet = "$($artifacts.black_mage_v14_identity_partial)/black_mage_v14_identity_partial_sheet.png"
            status = if ($mageV14PartialReport) { $mageV14PartialReport.status } else { $null }
            candidate_count = if ($mageV14PartialReport) { $mageV14PartialReport.candidate_count } else { $null }
            identity_fail_count = if ($mageV14PartialReport) { $mageV14PartialReport.identity_fail_count } else { $null }
            best_score = if ($mageV14PartialReport -and $mageV14PartialReport.best_candidate) { $mageV14PartialReport.best_candidate.identity_score } else { $null }
            conclusion = if ($mageV14PartialReport) { $mageV14PartialReport.conclusion } else { $null }
        }
        ai_tile_candidate_gate = [ordered]@{
            root = "Assets/Generated/_Review/reference32_ai_candidate_gate_v1"
            report = "Assets/Generated/_Review/reference32_ai_candidate_gate_v1/candidate_gate_report.json"
            sheet = "Assets/Generated/_Review/reference32_ai_candidate_gate_v1/reference32_ai_candidate_gate_sheet.png"
            decisions = "Assets/Generated/_Review/reference32_ai_candidate_gate_v1/review_decisions.json"
            candidate_count = if ($tileAiGateReport) { $tileAiGateReport.candidate_count } else { $null }
            tile_count = if ($tileAiGateReport) { $tileAiGateReport.tile_count } else { $null }
            accepted_count = if ($tileAiGateReport) { $tileAiGateReport.accepted_count } else { $null }
            rejected_best_count = if ($tileAiGateReport) { $tileAiGateReport.rejected_best_count } else { $null }
        }
        copylock_tile_smoke = [ordered]@{
            manifest = "Temp/AssetForge/reference32_copylock_tile_family_d012_l018_c100_color_v1.json"
            report = "Assets/Generated/_Review/reference32_copylock_tile_family_d012_l018_c100_color_v1/reference32_clean_tile_family_report.json"
            sheet = "Assets/Generated/_Review/reference32_copylock_tile_family_d012_l018_c100_color_v1/reference32_clean_tile_family_contact_sheet.png"
            gate_report = "Assets/Generated/_Review/reference32_copylock_ai_gate_d012_l018_c100_color_v1/candidate_gate_report.json"
            gate_sheet = "Assets/Generated/_Review/reference32_copylock_ai_gate_d012_l018_c100_color_v1/reference32_ai_candidate_gate_sheet.png"
            queue_only = if ($tileCopyLockManifest) { $tileCopyLockManifest.queue_only } else { $null }
            jobs = if ($tileCopyLockManifest -and $tileCopyLockManifest.jobs) { @($tileCopyLockManifest.jobs).Count } else { $null }
            accepted_count = if ($tileCopyLockGateReport) { $tileCopyLockGateReport.accepted_count } else { $null }
            rejected_best_count = if ($tileCopyLockGateReport) { $tileCopyLockGateReport.rejected_best_count } else { $null }
            tested_tile_count = if ($tileCopyLockGateReport) { $tileCopyLockGateReport.tile_count } else { $null }
        }
        visual_delta = [ordered]@{
            root = $visualDeltaRoot
            report = "$visualDeltaRoot/litiso_pipeline_visual_delta_report.json"
            board = "$visualDeltaRoot/litiso_pipeline_visual_delta_board.png"
            tile_count = if ($visualDelta) { $visualDelta.summary.tile_count } else { $null }
            tile_min_alpha_iou = if ($visualDelta) { $visualDelta.summary.tile_min_alpha_iou } else { $null }
            tile_max_mean_rgb_delta = if ($visualDelta) { $visualDelta.summary.tile_max_mean_rgb_delta } else { $null }
            mage_direction_count = if ($visualDelta) { $visualDelta.summary.mage_direction_count } else { $null }
            mage_directions = if ($visualDelta) { $visualDelta.summary.mage_directions } else { $null }
        }
    }
    repeat_commands = @(
        "powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\run_litiso_asset_pipeline_review_golden_path.ps1",
        "powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AssetForge\run_litiso_asset_pipeline_review_golden_path.ps1 -SkipRebuild"
    )
    next_recommendations = @(
        "Review tile_source_family first as the exact supplied-style baseline.",
        "Review tile_litiso_green_family as the first no-credit LIT-ISO palette shift.",
        "Review tile_mask_locked_forest and tile_mask_locked_plains as the current practical local generator output.",
        "Prefer tile_mask_locked_screenshot_balanced as the current best screenshot-targeted tile review pack.",
        "Keep prompt-only and ControlNet tile outputs out of Unity until they beat the deterministic baseline.",
        "Current AI tile candidate gate rejects all best ControlNet attempts because geometry/alpha coverage drifts from source.",
        "Copy-lock grass smoke also rejects; pivot tile generation toward deterministic geometry masks with AI/detail texture fill or recolor.",
        "Approve or reject black mage v11 directions manually before dataset capture.",
        "Black mage v11 is direction-incomplete: generate true S/E/N/W before 4D sheets or animation loops.",
        "Black mage v12 preflight has 8D v3 scaffolds, 8D OpenPose controls, and request JSON staged; render cardinals first if GPU time is limited.",
        "Black mage v12 cardinal render produced 12 structural review candidates and a mixed 8D coverage-complete sheet; do not train until E/W side-view quality is manually accepted or rerendered.",
        "Black mage v13 side pass improves E/W with lower denoise/style pull; v13 mixed 8D is the current best review sheet, still not training-approved.",
        "Tile and black mage dataset capture are now dry-run gated; no external dataset writes happen until review_decisions.json contains explicit approvals.",
        "Use visual_delta.board to review source/current tile deltas and mage reference/current direction evidence in one place.",
        "Black mage v13 mixed 8D now fails the stricter identity lock; treat it as direction evidence only, not training or production art.",
        "Use black_mage_reference_anchor.sheet as the visual contract: S/front must use the supplied anchor or pass strict reconstruction before 8D expansion.",
        "v14 S/front partial run also fails identity lock; stop repeating the current IP-Adapter + template/OpenPose setting.",
        "Next mage attempt should be image-to-image reconstruction from the original S anchor without conflicting template/OpenPose, then solve rotations separately.",
        "Next technical step for AI tiles is tighter image-to-image/reference-copy workflow, not more freehand prompt-only generation."
    )
}

$summaryPath = Join-Path $ProjectRoot "Temp\AssetForge\litiso_asset_pipeline_review_golden_path_status.json"
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $summaryPath) | Out-Null
$summary | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

Run-PythonTool -Arguments @(
    "-B", $readinessAudit,
    "--project-root", $ProjectRoot,
    "--status", "Temp/AssetForge/litiso_asset_pipeline_review_golden_path_status.json",
    "--out", "Temp/AssetForge/litiso_asset_pipeline_readiness_audit.json"
)

[ordered]@{
    ok = $true
    status = Convert-ToRepoPath $summaryPath
    mode = $summary.mode
    no_credit = $true
    unity_imported = $false
    tile_source_contact_sheet = $summary.review_artifacts.tile_source_family.contact_sheet
    tile_litiso_green_contact_sheet = $summary.review_artifacts.tile_litiso_green_family.contact_sheet
    tile_screenshot_balanced_contact_sheet = $summary.review_artifacts.tile_mask_locked_screenshot_balanced.contact_sheet
    black_mage_contact_sheet = $summary.review_artifacts.black_mage_v11.contact_sheet
    black_mage_direction_coverage_sheet = $summary.review_artifacts.black_mage_v11.direction_coverage_sheet
    black_mage_v12_cardinals_sheet = $summary.review_artifacts.black_mage_v12_cardinals.candidate_sheet
    black_mage_v12_mixed_8d_sheet = $summary.review_artifacts.black_mage_v12_mixed_8d.contact_sheet
    black_mage_v13_side_sheet = $summary.review_artifacts.black_mage_v13_side.candidate_sheet
    black_mage_v13_mixed_8d_sheet = $summary.review_artifacts.black_mage_v13_mixed_8d.contact_sheet
    black_mage_identity_lock_board = "$($artifacts.black_mage_identity_lock)/black_mage_identity_lock_board.png"
    black_mage_reference_anchor_sheet = "$($artifacts.black_mage_reference_anchor)/black_mage_reference_anchor_sheet.png"
    black_mage_v14_identity_partial_sheet = "$($artifacts.black_mage_v14_identity_partial)/black_mage_v14_identity_partial_sheet.png"
    tile_dataset_capture_dry_run = $tileBalancedCaptureDryRun
    black_mage_dataset_capture_dry_run = $mageV13CaptureDryRun
    visual_delta_board = "$visualDeltaRoot/litiso_pipeline_visual_delta_board.png"
    visual_delta_report = "$visualDeltaRoot/litiso_pipeline_visual_delta_report.json"
    readiness_audit = "Temp/AssetForge/litiso_asset_pipeline_readiness_audit.json"
} | ConvertTo-Json -Depth 8
