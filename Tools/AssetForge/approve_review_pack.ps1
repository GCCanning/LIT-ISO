param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$PackName = "CodexBiomeStarter",
    [switch]$ReplaceExisting,
    [switch]$PruneUnapproved
)

$ErrorActionPreference = "Stop"

$packRoot = Join-Path $ProjectRoot "Assets\Generated\_Review\$PackName"
$reportPath = Join-Path $packRoot "review_report.json"
$strictReportPath = Join-Path $packRoot "strict_asset_quality_report.json"
$decisionsPath = Join-Path $packRoot "review_decisions.json"
$approvalManifestPath = Join-Path $packRoot "approval_manifest.json"

if (-not (Test-Path $reportPath)) { throw "Missing review report: $reportPath" }
if (-not (Test-Path $decisionsPath)) { throw "Missing review decisions: $decisionsPath" }
if (-not (Test-Path $strictReportPath)) {
    throw "Missing strict QA report: $strictReportPath. Run Tools\AssetForge\test_strict_asset_quality.ps1 first."
}

function New-HexId {
    return [Guid]::NewGuid().ToString("N")
}

function Convert-ToAbsoluteProjectPath {
    param([string]$RelativePath)
    return Join-Path $ProjectRoot ($RelativePath -replace "/", "\")
}

function Ensure-FolderMeta {
    param([string]$FolderPath)

    $metaPath = "$FolderPath.meta"
    if (Test-Path $metaPath) { return }

    $content = @"
fileFormatVersion: 2
guid: $(New-HexId)
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@
    Set-Content -Path $metaPath -Value $content -Encoding UTF8
}

function Ensure-GeneratedFolderMetas {
    param([string]$DestinationDir)

    $generatedRoot = Join-Path $ProjectRoot "Assets\Generated"
    $current = $DestinationDir
    $folders = @()
    while ($current.StartsWith($generatedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        $folders += $current
        if ($current -ieq $generatedRoot) { break }
        $current = Split-Path -Parent $current
    }

    foreach ($folder in ($folders | Sort-Object { $_.Length })) {
        Ensure-FolderMeta -FolderPath $folder
    }
}

function Get-VersionedDestination {
    param(
        [string]$DestinationPath,
        [bool]$Replace
    )

    if ($Replace -or -not (Test-Path $DestinationPath)) {
        return $DestinationPath
    }

    $dir = Split-Path -Parent $DestinationPath
    $name = [IO.Path]::GetFileNameWithoutExtension($DestinationPath)
    $ext = [IO.Path]::GetExtension($DestinationPath)
    for ($i = 2; $i -lt 1000; $i++) {
        $candidate = Join-Path $dir ("{0}_v{1}{2}" -f $name, $i, $ext)
        if (-not (Test-Path $candidate)) { return $candidate }
    }

    throw "Unable to find versioned destination for $DestinationPath"
}

function Copy-ImporterMetaFresh {
    param(
        [string]$SourceMetaPath,
        [string]$DestinationMetaPath,
        [string]$DestinationPngPath,
        [string]$Category
    )

    if (-not (Test-Path $SourceMetaPath)) { return $false }

    $content = Get-Content -Raw -Path $SourceMetaPath
    $spriteName = [IO.Path]::GetFileNameWithoutExtension($DestinationPngPath)
    $expectedPpu = if ($Category -eq "decoration") { "128" } else { "32" }
    $expectedPivot = if ($Category -eq "decoration") { "{x: 0.5, y: 0}" } else { "{x: 0.5, y: 0.75}" }

    $content = [Regex]::Replace($content, "(?m)^guid:\s*[0-9a-f]+$", "guid: $(New-HexId)")
    $content = [Regex]::Replace($content, "spriteID:\s*[0-9a-f]+", "spriteID: $(New-HexId)")
    $content = [Regex]::Replace($content, "second:\s*[^\r\n]+", "second: ${spriteName}_0")
    $content = [Regex]::Replace($content, "(?m)^(\s*)name:\s*[^\r\n]+$", "`${1}name: ${spriteName}_0")
    $content = [Regex]::Replace($content, "(?m)^(\s*)spritePixelsToUnits:\s*.+$", "`${1}spritePixelsToUnits: $expectedPpu")
    $content = [Regex]::Replace($content, "(?m)^(\s*)spritePivot:\s*\{x:\s*[^,]+,\s*y:\s*[^}]+\}", "`${1}spritePivot: $expectedPivot")
    $content = [Regex]::Replace($content, "(?m)^(\s*)filterMode:\s*.+$", "`${1}filterMode: 0")
    $content = [Regex]::Replace($content, "(?m)^(\s*)enableMipMap:\s*.+$", "`${1}enableMipMap: 0")
    $content = [Regex]::Replace($content, "(?m)^(\s*)alphaIsTransparency:\s*.+$", "`${1}alphaIsTransparency: 1")
    $content = [Regex]::Replace($content, "(?m)^(\s*)textureCompression:\s*.+$", "`${1}textureCompression: 0")

    Set-Content -Path $DestinationMetaPath -Value $content -Encoding UTF8
    return $true
}

$report = Get-Content -Raw -Path $reportPath | ConvertFrom-Json
$strictReport = Get-Content -Raw -Path $strictReportPath | ConvertFrom-Json
$decisionsPayload = Get-Content -Raw -Path $decisionsPath | ConvertFrom-Json

$reportIds = New-Object System.Collections.Generic.HashSet[string]
$issueMap = @{}
foreach ($item in $report.items) {
    $key = $item.path.Replace("\", "/") -replace "^Assets/Generated/_Review/$PackName/", ""
    [void]$reportIds.Add($key)
    $issueMap[$key] = @($item.issues)
}

$strictIssueMap = @{}
foreach ($item in $strictReport.items) {
    $relative = $item.path.Replace("\", "/") -replace "^$([Regex]::Escape($ProjectRoot.Replace('\','/')))/", ""
    $key = $relative -replace "^Assets/Generated/_Review/$PackName/", ""
    $strictIssueMap[$key] = @($item.issues)
}

$allowedDecisions = @("approved", "pending", "rejected", "needs_edit")
$decisionIds = New-Object System.Collections.Generic.HashSet[string]
$decisionIssues = New-Object System.Collections.Generic.List[object]
$badApprovals = @()

foreach ($decision in $decisionsPayload.decisions) {
    $id = [string]$decision.id
    $decisionValue = [string]$decision.decision

    if ([string]::IsNullOrWhiteSpace($id)) {
        $decisionIssues.Add([PSCustomObject]@{ id = ""; issue = "decision entry is missing id" })
        continue
    }
    if (-not $decisionIds.Add($id)) {
        $decisionIssues.Add([PSCustomObject]@{ id = $id; issue = "duplicate decision id" })
    }
    if ($allowedDecisions -notcontains $decisionValue) {
        $decisionIssues.Add([PSCustomObject]@{ id = $id; issue = "decision must be approved, pending, rejected, or needs_edit; got '$decisionValue'" })
    }
    if (-not $reportIds.Contains($id)) {
        $decisionIssues.Add([PSCustomObject]@{ id = $id; issue = "decision id is not present in review_report.json" })
    }
    foreach ($field in @("source_path", "destination_path", "category", "biome")) {
        if (-not $decision.PSObject.Properties.Name.Contains($field) -or [string]::IsNullOrWhiteSpace([string]$decision.$field)) {
            $decisionIssues.Add([PSCustomObject]@{ id = $id; issue = "missing required field '$field'" })
        }
    }

    $combinedIssues = @()
    if ($issueMap.ContainsKey($id)) { $combinedIssues += @($issueMap[$id]) }
    if ($strictIssueMap.ContainsKey($id)) { $combinedIssues += @($strictIssueMap[$id]) }
    if ($decisionValue -eq "approved" -and $combinedIssues.Count -gt 0) {
        $badApprovals += [PSCustomObject]@{ id = $id; issues = $combinedIssues }
    }
}

$missingDecisions = @()
foreach ($reportId in $reportIds) {
    if (-not $decisionIds.Contains($reportId)) { $missingDecisions += $reportId }
}

if ($decisionIssues.Count -gt 0) {
    throw "Refusing approval because review_decisions.json is not export-ready: $($decisionIssues | ConvertTo-Json -Depth 6)"
}
if ($missingDecisions.Count -gt 0) {
    throw "Refusing approval because $($missingDecisions.Count) report item(s) have no decision: $($missingDecisions -join ', ')"
}
if ($badApprovals.Count -gt 0) {
    throw "Refusing approval because $($badApprovals.Count) approved item(s) still have QA issues: $($badApprovals | ConvertTo-Json -Depth 6)"
}

$copied = @()
$skipped = @()
$failed = @()
$approvedDestinationSet = New-Object System.Collections.Generic.HashSet[string]

foreach ($decision in $decisionsPayload.decisions) {
    if ($decision.decision -ne "approved") {
        $skipped += [PSCustomObject]@{ id = $decision.id; decision = $decision.decision; reason = "not_approved" }
        continue
    }

    $source = Convert-ToAbsoluteProjectPath -RelativePath $decision.source_path
    $destination = Convert-ToAbsoluteProjectPath -RelativePath $decision.destination_path

    if (-not (Test-Path $source)) {
        $failed += [PSCustomObject]@{ id = $decision.id; source_path = $decision.source_path; reason = "missing_source" }
        continue
    }

    $destination = Get-VersionedDestination -DestinationPath $destination -Replace $ReplaceExisting.IsPresent
    [void]$approvedDestinationSet.Add($destination.ToLowerInvariant())
    $destinationDir = Split-Path -Parent $destination
    New-Item -ItemType Directory -Force -Path $destinationDir | Out-Null
    Ensure-GeneratedFolderMetas -DestinationDir $destinationDir

    Copy-Item -LiteralPath $source -Destination $destination -Force
    $metaCopied = Copy-ImporterMetaFresh -SourceMetaPath "$source.meta" -DestinationMetaPath "$destination.meta" -DestinationPngPath $destination -Category $decision.category

    $copied += [PSCustomObject]@{
        id = $decision.id
        name = $decision.name
        category = $decision.category
        biome = $decision.biome
        source_path = $decision.source_path
        destination_path = $destination.Replace($ProjectRoot + "\", "").Replace("\", "/")
        meta_created = $metaCopied
    }
}

$pruned = @()
if ($PruneUnapproved.IsPresent) {
    foreach ($root in @("Assets\Generated\Tiles", "Assets\Generated\Props")) {
        $absoluteRoot = Join-Path $ProjectRoot $root
        if (-not (Test-Path $absoluteRoot)) { continue }
        foreach ($file in Get-ChildItem -Path $absoluteRoot -Recurse -Filter *.png -File) {
            if (-not $approvedDestinationSet.Contains($file.FullName.ToLowerInvariant())) {
                Remove-Item -LiteralPath $file.FullName -Force
                if (Test-Path "$($file.FullName).meta") {
                    Remove-Item -LiteralPath "$($file.FullName).meta" -Force
                }
                $pruned += $file.FullName.Replace($ProjectRoot + "\", "").Replace("\", "/")
            }
        }
    }
}

$payload = [ordered]@{
    pack_name = $PackName
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    source_decisions = $decisionsPath.Replace($ProjectRoot + "\", "").Replace("\", "/")
    review_report = $reportPath.Replace($ProjectRoot + "\", "").Replace("\", "/")
    strict_report = $strictReportPath.Replace($ProjectRoot + "\", "").Replace("\", "/")
    approved_root = "Assets/Generated"
    copied_count = $copied.Count
    skipped_count = $skipped.Count
    failed_count = $failed.Count
    pruned_count = $pruned.Count
    copied = $copied
    skipped = $skipped
    failed = $failed
    pruned = $pruned
}

$payload | ConvertTo-Json -Depth 8 | Set-Content -Path $approvalManifestPath -Encoding UTF8

if ($failed.Count -gt 0) {
    throw "Approval completed with $($failed.Count) failed copy operation(s). See $approvalManifestPath"
}

[PSCustomObject]@{
    approval_manifest = $approvalManifestPath
    copied = $copied.Count
    skipped = $skipped.Count
    failed = $failed.Count
    pruned = $pruned.Count
} | ConvertTo-Json
