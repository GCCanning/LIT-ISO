param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$PackName = "CodexBiomeStarter",
    [string]$DatasetRoot,
    [switch]$IncludeRejected
)

$ErrorActionPreference = "Stop"

$packRoot = Join-Path $ProjectRoot "Assets\Generated\_Review\$PackName"
$decisionsPath = Join-Path $packRoot "review_decisions.json"
$reportPath = Join-Path $packRoot "review_report.json"
$approvalManifestPath = Join-Path $packRoot "approval_manifest.json"

if (-not (Test-Path $decisionsPath)) {
    throw "Missing review decisions: $decisionsPath"
}
if (-not (Test-Path $reportPath)) {
    throw "Missing review report: $reportPath"
}

if ([string]::IsNullOrWhiteSpace($DatasetRoot)) {
    $DatasetRoot = Join-Path $ProjectRoot "Assets\Generated\_Datasets\lit_iso"
}

function Assert-UnderRoot {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $pathFull = [IO.Path]::GetFullPath($Path).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    if ($pathFull -ne $rootFull -and -not $pathFull.StartsWith($rootFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label must stay inside the repo. Root: $rootFull Path: $pathFull"
    }
}

$decisions = Get-Content -Raw -Path $decisionsPath | ConvertFrom-Json
$report = Get-Content -Raw -Path $reportPath | ConvertFrom-Json
$issueMap = @{}
foreach ($item in $report.items) {
    $key = $item.path.Replace("\", "/") -replace "^Assets/Generated/_Review/$PackName/", ""
    $issueMap[$key] = @($item.issues)
}

$projectRootResolved = (Resolve-Path -LiteralPath $ProjectRoot).Path
$datasetRootFull = [IO.Path]::GetFullPath($DatasetRoot)
Assert-UnderRoot -Root $projectRootResolved -Path $datasetRootFull -Label "DatasetRoot"
New-Item -ItemType Directory -Force -Path $datasetRootFull | Out-Null

$datasetPackRoot = Join-Path $datasetRootFull "review_packs\$PackName"
$imageRoot = Join-Path $datasetPackRoot "images"
$captionRoot = Join-Path $datasetPackRoot "captions"
$metaRoot = Join-Path $datasetPackRoot "metadata"
New-Item -ItemType Directory -Force -Path $imageRoot, $captionRoot, $metaRoot | Out-Null

$records = @()
$blocked = New-Object System.Collections.Generic.List[object]
$decisionSummary = [ordered]@{
    approved = 0
    rejected = 0
    needs_edit = 0
    other = 0
}
foreach ($decision in $decisions.decisions) {
    $decisionValue = [string]$decision.decision
    if ($decisionSummary.Contains($decisionValue)) { $decisionSummary[$decisionValue]++ } else { $decisionSummary.other++ }

    if ($decisionValue -eq "approved" -and $issueMap.ContainsKey($decision.id) -and $issueMap[$decision.id].Count -gt 0) {
        $blocked.Add([PSCustomObject]@{ id = $decision.id; issue = "approved item has unresolved review_report issues"; issues = $issueMap[$decision.id] })
        continue
    }

    $include = $decision.decision -eq "approved" -or ($IncludeRejected.IsPresent -and $decision.decision -eq "rejected")
    if (-not $include) {
        continue
    }

    $sourceAbs = Join-Path $ProjectRoot ($decision.source_path -replace "/", "\")
    if (-not (Test-Path $sourceAbs)) {
        $blocked.Add([PSCustomObject]@{ id = $decision.id; issue = "source image is missing"; source_path = $decision.source_path })
        continue
    }
    Assert-UnderRoot -Root $projectRootResolved -Path (Resolve-Path -LiteralPath $sourceAbs).Path -Label "Source image"

    $safeId = ($decision.id -replace "[\\/]+", "_") -replace "[^A-Za-z0-9_.-]", "_"
    $imageName = $safeId
    $captionName = [IO.Path]::ChangeExtension($safeId, ".txt")
    $imageDest = Join-Path $imageRoot $imageName
    $captionDest = Join-Path $captionRoot $captionName

    Copy-Item -LiteralPath $sourceAbs -Destination $imageDest -Force

    $modeText = if ($decision.category -eq "decoration") { "pixel prop" } else { "isometric terrain tile" }
    $caption = "LIT-ISO $modeText, $($decision.biome) biome, $($decision.name -replace '\.png$', '' -replace '_', ' '), pixel art, transparent background"
    Set-Content -Path $captionDest -Value $caption -Encoding UTF8

    $records += [PSCustomObject]@{
        file_name = "images/$imageName"
        text = $caption
        source_path = $decision.source_path
        decision = $decision.decision
        category = $decision.category
        biome = $decision.biome
        pack = $PackName
    }
}

if ($blocked.Count -gt 0) {
    throw "Dataset capture is blocked: $($blocked | ConvertTo-Json -Depth 8)"
}
if ($records.Count -eq 0) {
    throw "Dataset capture produced 0 records. Approve at least one QA-clean item, or pass -IncludeRejected to capture rejected examples too."
}

$metadataJsonl = Join-Path $datasetPackRoot "metadata.jsonl"
$records | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 6 } | Set-Content -Path $metadataJsonl -Encoding UTF8

$manifest = [ordered]@{
    pack_name = $PackName
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    dataset_root = $datasetPackRoot
    decision_source = $decisionsPath
    approval_manifest = if (Test-Path $approvalManifestPath) { $approvalManifestPath } else { $null }
    record_count = $records.Count
    decision_counts = $decisionSummary
    include_rejected = $IncludeRejected.IsPresent
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -Path (Join-Path $metaRoot "capture_manifest.json") -Encoding UTF8

$readiness = [ordered]@{
    pack_name = $PackName
    ready_for_training = $true
    record_count = $records.Count
    approved_count = $decisionSummary.approved
    rejected_count = $decisionSummary.rejected
    needs_edit_count = $decisionSummary.needs_edit
    includes_rejected_examples = $IncludeRejected.IsPresent
    dataset_root = $datasetPackRoot
    metadata_jsonl = $metadataJsonl
    notes = @(
        "DatasetRoot is repo-local by default and is refused when outside ProjectRoot.",
        "Approved records are blocked if review_report.json still lists QA issues."
    )
}
$readiness | ConvertTo-Json -Depth 6 | Set-Content -Path (Join-Path $metaRoot "dataset_readiness_summary.json") -Encoding UTF8

[PSCustomObject]@{
    dataset_root = $datasetPackRoot
    records = $records.Count
    metadata = $metadataJsonl
} | ConvertTo-Json
