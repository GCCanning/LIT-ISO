param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,
    [string]$OutputPath,
    [ValidateSet("auto", "terrain", "prop")]
    [string]$Category = "auto",
    [switch]$FailOnReview
)

Add-Type -AssemblyName System.Drawing

function Get-Category {
    param([string]$Path)

    if ($Category -ne "auto") {
        return $Category
    }

    $normalized = $Path.Replace("\", "/")
    if ($normalized -match "/Props/" -or $normalized -match "/Decorations/") {
        return "prop"
    }

    return "terrain"
}

function Test-Png {
    param([string]$Path)

    $categoryName = Get-Category -Path $Path
    $img = [System.Drawing.Bitmap]::FromFile($Path)
    try {
        $issues = New-Object System.Collections.Generic.List[string]
        $warnings = New-Object System.Collections.Generic.List[string]

        $opaque = 0
        $minX = $img.Width
        $minY = $img.Height
        $maxX = -1
        $maxY = -1
        $cornerOpaque = 0
        $bottomBandOpaque = 0
        $bottomBandY = [Math]::Max(0, $img.Height - [Math]::Max(4, [Math]::Floor($img.Height * 0.12)))

        for ($y = 0; $y -lt $img.Height; $y++) {
            for ($x = 0; $x -lt $img.Width; $x++) {
                $alpha = $img.GetPixel($x, $y).A
                if ($alpha -le 0) {
                    continue
                }

                $opaque++
                if ($x -lt $minX) { $minX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -gt $maxY) { $maxY = $y }

                $nearLeft = $x -lt [Math]::Max(2, [Math]::Floor($img.Width * 0.08))
                $nearRight = $x -ge $img.Width - [Math]::Max(2, [Math]::Floor($img.Width * 0.08))
                $nearTop = $y -lt [Math]::Max(2, [Math]::Floor($img.Height * 0.08))
                $nearBottom = $y -ge $img.Height - [Math]::Max(2, [Math]::Floor($img.Height * 0.08))
                if (($nearLeft -or $nearRight) -and ($nearTop -or $nearBottom)) {
                    $cornerOpaque++
                }

                if ($y -ge $bottomBandY) {
                    $bottomBandOpaque++
                }
            }
        }

        if ($opaque -eq 0) {
            $issues.Add("blank_alpha")
        }

        $bboxWidth = if ($maxX -ge $minX) { $maxX - $minX + 1 } else { 0 }
        $bboxHeight = if ($maxY -ge $minY) { $maxY - $minY + 1 } else { 0 }
        $coverage = if ($img.Width * $img.Height -gt 0) { $opaque / ($img.Width * $img.Height) } else { 0 }
        $bottomBandPixels = $img.Width * ($img.Height - $bottomBandY)
        $bottomCoverage = if ($bottomBandPixels -gt 0) { $bottomBandOpaque / $bottomBandPixels } else { 0 }

        if ($categoryName -eq "terrain") {
            if ($img.Width -ne 32 -or $img.Height -ne 32) {
                $issues.Add("terrain_size_not_32x32")
            }
            if ($cornerOpaque -gt 0) {
                $issues.Add("terrain_opaque_corners")
            }
            if ($coverage -gt 0.72) {
                $warnings.Add("terrain_may_be_square_or_block_instead_of_diamond")
            }
        }
        else {
            if ($img.Width -ne 128 -or $img.Height -ne 128) {
                $warnings.Add("prop_size_not_128x128")
            }
            if ($cornerOpaque -gt 0) {
                $issues.Add("prop_opaque_corners_or_background")
            }
            if ($bottomCoverage -gt 0.45 -and $bboxWidth -gt ($img.Width * 0.65)) {
                $warnings.Add("prop_may_have_baked_ground_or_base_plate")
            }
        }

        [PSCustomObject]@{
            path = $Path
            category = $categoryName
            width = $img.Width
            height = $img.Height
            opaque_pixels = $opaque
            coverage = [Math]::Round($coverage, 4)
            bbox = @($minX, $minY, $maxX, $maxY)
            corner_opaque_pixels = $cornerOpaque
            bottom_band_coverage = [Math]::Round($bottomCoverage, 4)
            status = if ($issues.Count -eq 0) { "pass" } else { "review" }
            issues = @($issues)
            warnings = @($warnings)
        }
    }
    finally {
        $img.Dispose()
    }
}

$resolvedInput = Resolve-Path $InputPath
$files = if ((Get-Item $resolvedInput).PSIsContainer) {
    Get-ChildItem -Path $resolvedInput -Recurse -Filter *.png -File |
        Where-Object {
            $normalized = $_.FullName.Replace("\", "/")
            $normalized -notmatch "/_Preview/" -and $normalized -notmatch "contact_sheet"
        }
}
else {
    Get-Item $resolvedInput
}

$items = @($files | ForEach-Object { Test-Png -Path $_.FullName })
$payload = [ordered]@{
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    input_path = $resolvedInput.Path
    total = $items.Count
    pass_count = @($items | Where-Object { $_.status -eq "pass" }).Count
    review_count = @($items | Where-Object { $_.status -ne "pass" }).Count
    warning_count = @($items | Where-Object { @($_.warnings).Count -gt 0 }).Count
    dataset_ready = $items.Count -gt 0 -and @($items | Where-Object { $_.status -ne "pass" }).Count -eq 0
    fail_closed = $FailOnReview.IsPresent
    readiness_summary = [ordered]@{
        ready_for_review_export = $items.Count -gt 0 -and @($items | Where-Object { $_.status -ne "pass" }).Count -eq 0
        required_action = if ($items.Count -eq 0) { "add_pngs" } elseif (@($items | Where-Object { $_.status -ne "pass" }).Count -gt 0) { "fix_or_reject_review_items" } else { "none" }
        checks = @(
            "blank alpha",
            "opaque corners/background",
            "terrain 32x32",
            "prop 128x128 warning",
            "likely prop ground/base plate warning"
        )
    }
    items = $items
}

if (-not $OutputPath) {
    $OutputPath = if ((Get-Item $resolvedInput).PSIsContainer) {
        Join-Path $resolvedInput "strict_asset_quality_report.json"
    }
    else {
        Join-Path (Split-Path -Parent $resolvedInput) "strict_asset_quality_report.json"
    }
}

$payload | ConvertTo-Json -Depth 8 | Set-Content -Path $OutputPath -Encoding UTF8

[PSCustomObject]@{
    report = $OutputPath
    total = $payload.total
    pass = $payload.pass_count
    review = $payload.review_count
    warnings = $payload.warning_count
    dataset_ready = $payload.dataset_ready
} | ConvertTo-Json

if ($FailOnReview.IsPresent -and (-not $payload.dataset_ready)) {
    throw "Strict asset quality failed closed: $($payload.review_count) review item(s), $($payload.total) total. Report: $OutputPath"
}
