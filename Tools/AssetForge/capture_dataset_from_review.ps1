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
    $DatasetRoot = "C:\Projects\Pixel Pipeline\datasets\lit_iso"
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
        throw "$Label must stay inside the configured root. Root: $rootFull Path: $pathFull"
    }
}

$decisions = Get-Content -Raw -Path $decisionsPath | ConvertFrom-Json
$report = Get-Content -Raw -Path $reportPath | ConvertFrom-Json
$styleProfileId = ""
if ($report.style_provenance) {
    $stylePath = Join-Path $ProjectRoot ([string]$report.style_provenance -replace "/", "\")
    if (Test-Path $stylePath) {
        try {
            $styleData = Get-Content -Raw -LiteralPath $stylePath | ConvertFrom-Json
            if ($styleData.profile_id) { $styleProfileId = [string]$styleData.profile_id }
        }
        catch { }
    }
}
$issueMap = @{}
$itemMap = @{}
foreach ($item in $report.items) {
    $key = $item.path.Replace("\", "/") -replace "^Assets/Generated/_Review/$PackName/", ""
    $issueMap[$key] = @($item.issues)
    $itemMap[$key] = $item
}

$projectRootResolved = (Resolve-Path -LiteralPath $ProjectRoot).Path
$datasetRootFull = [IO.Path]::GetFullPath($DatasetRoot)
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

    $reportItem = if ($itemMap.ContainsKey($decision.id)) { $itemMap[$decision.id] } else { $null }
    $subject = $decision.name -replace '\.png$', '' -replace '_', ' '
    $modeText = switch ($decision.category) {
        "terrain" { "isometric terrain tile" }
        "decoration" { "pixel prop" }
        "item" { "pixel item sprite" }
        "character" { "pixel character sprite" }
        "npc" { "pixel NPC sprite" }
        "mob" { "pixel creature sprite" }
        "vfx" { "pixel VFX sprite sheet" }
        default { "pixel art asset" }
    }
    $anchorText = switch ($decision.category) {
        "terrain" { "2:1 diamond grid, centered anchor" }
        "item" { "centered icon anchor" }
        "vfx" { "centered frame anchor" }
        default { "bottom-center anchor" }
    }
    $caption = "LIT-ISO $modeText, $($decision.biome) set, $subject, cyan-knight-compatible palette, isometric view, pixel art, transparent background, $anchorText"
    $provider = if ($reportItem -and $reportItem.generation -and $reportItem.generation.provider) { [string]$reportItem.generation.provider } else { [string]$report.provider }
    $prompt = if ($reportItem -and $reportItem.generation -and $reportItem.generation.prompt) { [string]$reportItem.generation.prompt } else { "" }
    $negativePrompt = if ($reportItem -and $reportItem.generation -and $reportItem.generation.negative_prompt) { [string]$reportItem.generation.negative_prompt } else { "" }
    $sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $imageDest).Hash.ToLowerInvariant()
    Set-Content -Path $captionDest -Value $caption -Encoding UTF8

    $records += [PSCustomObject]@{
        file_name = "images/$imageName"
        text = $caption
        source_path = $decision.source_path
        decision = $decision.decision
        category = $decision.category
        asset_mode = $decision.category
        subcategory = $subject
        biome = $decision.biome
        pack = $PackName
        provider = $provider
        prompt = $prompt
        negative_prompt = $negativePrompt
        qa_status = if ($reportItem) { [string]$reportItem.status } else { [string]$decision.review_status }
        style_profile_id = $styleProfileId
        palette_tags = @("cyan_knight", "teal_accent", "warm_muted")
        camera = "2:1_isometric"
        license = "project_internal_or_explicitly_licensed"
        author = "LIT-ISO"
        clean_room_notes = "Approved through Asset Forge review. Do not train on copied third-party pixels."
        sha256 = $sha256
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
