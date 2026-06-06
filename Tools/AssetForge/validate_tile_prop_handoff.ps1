param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$PackName = "CodexBiomeStarter",
    [string]$ReportPath
)

$ErrorActionPreference = "Stop"

$packRoot = Join-Path $ProjectRoot "Assets\Generated\_Review\$PackName"
$decisionsPath = Join-Path $packRoot "review_decisions.json"
if (-not (Test-Path $decisionsPath)) {
    throw "Missing review decisions: $decisionsPath"
}
if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $packRoot "tile_prop_handoff_validation.json"
}

$decisions = Get-Content -Raw -Path $decisionsPath | ConvertFrom-Json
$approved = @{}
foreach ($decision in $decisions.decisions) {
    if ($decision.decision -eq "approved") {
        $approved[$decision.destination_path.Replace("\", "/")] = $decision
    }
}

$items = @()
$roots = @("Assets\Generated\Tiles", "Assets\Generated\Props")
foreach ($root in $roots) {
    $absoluteRoot = Join-Path $ProjectRoot $root
    if (-not (Test-Path $absoluteRoot)) { continue }

    foreach ($file in Get-ChildItem -Path $absoluteRoot -Recurse -Filter *.png -File) {
        $relative = $file.FullName.Replace($ProjectRoot + "\", "").Replace("\", "/")
        $metaPath = "$($file.FullName).meta"
        $issues = New-Object System.Collections.Generic.List[string]
        $warnings = New-Object System.Collections.Generic.List[string]
        $category = if ($relative -match "^Assets/Generated/Props/") { "decoration" } else { "terrain" }
        $expectedPpu = if ($category -eq "decoration") { "128" } else { "32" }

        if (-not $approved.ContainsKey($relative)) {
            $issues.Add("not_approved_by_review_decisions")
        }
        if (-not (Test-Path $metaPath)) {
            $issues.Add("missing_meta")
            $meta = ""
        }
        else {
            $meta = Get-Content -Raw -Path $metaPath
        }
        if ($meta -and $meta -notmatch "spritePixelsToUnits: $expectedPpu") { $issues.Add("ppu_not_$expectedPpu") }
        if ($meta -and $meta -notmatch "filterMode: 0") { $issues.Add("filter_not_point") }
        if ($meta -and $meta -notmatch "enableMipMap: 0") { $issues.Add("mipmaps_enabled") }
        if ($meta -and $meta -notmatch "alphaIsTransparency: 1") { $warnings.Add("alpha_transparency_not_marked") }

        $items += [PSCustomObject]@{
            path = $relative
            category = $category
            approved = $approved.ContainsKey($relative)
            status = if ($issues.Count -eq 0) { "pass" } else { "review" }
            issues = @($issues)
            warnings = @($warnings)
        }
    }
}

$payload = [ordered]@{
    pack_name = $PackName
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    total = $items.Count
    pass_count = @($items | Where-Object { $_.status -eq "pass" }).Count
    review_count = @($items | Where-Object { $_.status -ne "pass" }).Count
    approved_expected_count = $approved.Count
    items = $items
}

$payload | ConvertTo-Json -Depth 8 | Set-Content -Path $ReportPath -Encoding UTF8

[PSCustomObject]@{
    report = $ReportPath
    total = $payload.total
    pass = $payload.pass_count
    review = $payload.review_count
    approved_expected = $payload.approved_expected_count
} | ConvertTo-Json
