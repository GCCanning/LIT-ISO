param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$PackName = "CodexBiomeStarter",
    [switch]$ApprovePassing,
    [switch]$ReplaceExisting
)

$ErrorActionPreference = "Stop"

$reviewRoot = Join-Path $ProjectRoot "Assets\Generated\_Review\$PackName"
$reportPath = Join-Path $reviewRoot "review_report.json"
$decisionsPath = Join-Path $reviewRoot "review_decisions.json"
$strictReportPath = Join-Path $reviewRoot "strict_asset_quality_report.json"
$legacyGeneratorRoot = Join-Path $ProjectRoot "Temp\GeneratedTiles"

function Run-Step {
    param(
        [string]$Name,
        [scriptblock]$Script
    )
    Write-Host "== $Name ==" -ForegroundColor Cyan
    & $Script
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE"
    }
}

function Invoke-LegacyGeneratorIfAvailable {
    $required = @(
        "make_biome_starter_tiles.ps1",
        "make_biome_starter_decorations.ps1",
        "normalize_biome_starter_imports.ps1",
        "make_biome_pack_review.ps1",
        "initialize_biome_review_decisions.ps1"
    )

    $missing = @($required | Where-Object { -not (Test-Path (Join-Path $legacyGeneratorRoot $_)) })
    if ($missing.Count -gt 0) {
        return $false
    }

    Run-Step "Generate terrain tiles" {
        powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $legacyGeneratorRoot "make_biome_starter_tiles.ps1")
    }

    Run-Step "Generate decoration props" {
        powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $legacyGeneratorRoot "make_biome_starter_decorations.ps1")
    }

    Run-Step "Normalize generated import metadata" {
        powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $legacyGeneratorRoot "normalize_biome_starter_imports.ps1") -PackRoot (Join-Path $ProjectRoot "Assets\Generated")
    }

    Run-Step "Build review report/gallery" {
        powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $legacyGeneratorRoot "make_biome_pack_review.ps1")
    }

    $decisionArgs = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", (Join-Path $legacyGeneratorRoot "initialize_biome_review_decisions.ps1"),
        "-ProjectRoot", $ProjectRoot,
        "-PackName", $PackName
    )
    if (-not $ApprovePassing.IsPresent) {
        $decisionArgs += @("-DefaultPassDecision", "pending")
    }

    Run-Step "Initialize review decisions" {
        powershell.exe @decisionArgs
    }

    return $true
}

$generated = Invoke-LegacyGeneratorIfAvailable
if (-not $generated) {
    Write-Warning "Legacy Temp\GeneratedTiles generator scripts are not present. Reusing existing review pack files."
    if (-not (Test-Path $reportPath)) {
        throw "Cannot build review pack because no generator scripts were found and no review_report.json exists at $reportPath"
    }
    if (-not (Test-Path $decisionsPath)) {
        throw "Cannot build review pack because no generator scripts were found and no review_decisions.json exists at $decisionsPath"
    }
}

$strictArgs = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", (Join-Path $PSScriptRoot "test_strict_asset_quality.ps1"),
    "-InputPath", $reviewRoot,
    "-OutputPath", $strictReportPath
)
if ($ApprovePassing.IsPresent) {
    $strictArgs += "-FailOnReview"
}

Run-Step "Run strict asset quality scan" {
    powershell.exe @strictArgs
}

if ($ApprovePassing.IsPresent) {
    $approveArgs = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", (Join-Path $PSScriptRoot "approve_review_pack.ps1"),
        "-ProjectRoot", $ProjectRoot,
        "-PackName", $PackName
    )
    if ($ReplaceExisting.IsPresent) {
        $approveArgs += "-ReplaceExisting"
    }

    Run-Step "Copy approved assets into generated handoff folders" {
        powershell.exe @approveArgs
    }
}

Write-Host "Review pack ready: $reviewRoot" -ForegroundColor Green
Write-Host "Open review_gallery.html or serve Tools\AssetForge\Dashboard, then run approve_review_pack.ps1 when ready."
