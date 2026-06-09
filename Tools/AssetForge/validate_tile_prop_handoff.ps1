param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$PackName = "CodexBiomeStarter",
    [string]$ReportPath,
    [switch]$IncludeUnapprovedGenerated
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

function Format-InvariantNumber {
    param([object]$Value)

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        return "0"
    }

    $number = [double]$Value
    return $number.ToString("0.###", [Globalization.CultureInfo]::InvariantCulture)
}

function Get-DecisionUnitySettings {
    param(
        [object]$Decision,
        [string]$Category
    )

    $expectedPpu = if ($Category -eq "decoration") { "128" } else { "32" }
    $expectedPivot = if ($Category -eq "decoration") { "{x: 0.5, y: 0}" } else { "{x: 0.5, y: 0.75}" }

    if ($null -ne $Decision -and $Decision.PSObject.Properties.Name.Contains("unity") -and $null -ne $Decision.unity) {
        if ($Decision.unity.PSObject.Properties.Name.Contains("ppu") -and -not [string]::IsNullOrWhiteSpace([string]$Decision.unity.ppu)) {
            $expectedPpu = [string]$Decision.unity.ppu
        }
        if ($Decision.unity.PSObject.Properties.Name.Contains("pivot") -and $null -ne $Decision.unity.pivot) {
            $pivot = $Decision.unity.pivot
            if ($pivot.PSObject.Properties.Name.Contains("x") -and $pivot.PSObject.Properties.Name.Contains("y")) {
                $x = Format-InvariantNumber -Value $pivot.x
                $y = Format-InvariantNumber -Value $pivot.y
                $expectedPivot = "{x: $x, y: $y}"
            }
        }
    }

    return [PSCustomObject]@{
        ppu = $expectedPpu
        pivot = $expectedPivot
    }
}

$items = @()

function New-HandoffValidationItem {
    param(
        [string]$Relative,
        [object]$Decision,
        [bool]$Approved
    )

    $absolute = Join-Path $ProjectRoot ($Relative -replace "/", "\")
    $metaPath = "$absolute.meta"
    $issues = New-Object System.Collections.Generic.List[string]
    $warnings = New-Object System.Collections.Generic.List[string]
    $category = if ($Relative -match "^Assets/Generated/Props/") { "decoration" } else { "terrain" }
    $unitySettings = Get-DecisionUnitySettings -Decision $Decision -Category $category
    $expectedPpu = $unitySettings.ppu
    $expectedPivot = $unitySettings.pivot

    if (-not $Approved) {
        $issues.Add("not_approved_by_review_decisions")
    }
    if (-not (Test-Path $absolute)) {
        $issues.Add("missing_generated_png")
        $meta = ""
    }
    elseif (-not (Test-Path $metaPath)) {
        $issues.Add("missing_meta")
        $meta = ""
    }
    else {
        $meta = Get-Content -Raw -Path $metaPath
    }

    if ($meta -and $meta -notmatch "spritePixelsToUnits: $expectedPpu") { $issues.Add("ppu_not_$expectedPpu") }
    if ($meta -and $meta -notmatch [Regex]::Escape("spritePivot: $expectedPivot")) { $issues.Add("pivot_not_expected") }
    if ($meta -and $meta -notmatch "filterMode: 0") { $issues.Add("filter_not_point") }
    if ($meta -and $meta -notmatch "enableMipMap: 0") { $issues.Add("mipmaps_enabled") }
    if ($meta -and $meta -notmatch "alphaIsTransparency: 1") { $warnings.Add("alpha_transparency_not_marked") }

    return [PSCustomObject]@{
        path = $Relative
        category = $category
        approved = $Approved
        expected_ppu = $expectedPpu
        expected_pivot = $expectedPivot
        status = if ($issues.Count -eq 0) { "pass" } else { "review" }
        issues = @($issues)
        warnings = @($warnings)
    }
}

foreach ($entry in $approved.GetEnumerator()) {
    $items += New-HandoffValidationItem -Relative $entry.Key -Decision $entry.Value -Approved $true
}

if ($IncludeUnapprovedGenerated.IsPresent) {
    $roots = @("Assets\Generated\Tiles", "Assets\Generated\Props")
    foreach ($root in $roots) {
        $absoluteRoot = Join-Path $ProjectRoot $root
        if (-not (Test-Path $absoluteRoot)) { continue }

        foreach ($file in Get-ChildItem -Path $absoluteRoot -Recurse -Filter *.png -File) {
            $relative = $file.FullName.Replace($ProjectRoot + "\", "").Replace("\", "/")
            if ($approved.ContainsKey($relative)) { continue }
            $items += New-HandoffValidationItem -Relative $relative -Decision $null -Approved $false
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
