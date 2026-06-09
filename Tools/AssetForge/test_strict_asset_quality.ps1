param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,
    [string]$OutputPath,
    [ValidateSet("auto", "terrain", "prop", "item", "character", "npc", "mob", "vfx", "animation")]
    [string]$Category = "auto",
    [ValidateSet("auto", "flat", "raised_block")]
    [string]$TerrainProfile = "auto",
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
    if ($normalized -match "/Items/") {
        return "item"
    }
    if ($normalized -match "/Characters/") {
        return "character"
    }
    if ($normalized -match "/NPCs/") {
        return "npc"
    }
    if ($normalized -match "/Mobs/") {
        return "mob"
    }
    if ($normalized -match "/Effects/" -or $normalized -match "/VFX/") {
        return "vfx"
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
        $colors = @{}
        $minX = $img.Width
        $minY = $img.Height
        $maxX = -1
        $maxY = -1
        $cornerOpaque = 0
        $bottomBandOpaque = 0
        $bottomBandY = [Math]::Max(0, $img.Height - [Math]::Max(4, [Math]::Floor($img.Height * 0.12)))
        $edgeBandX = [Math]::Max(2, [Math]::Floor($img.Width * 0.04))
        $edgeBandY = [Math]::Max(2, [Math]::Floor($img.Height * 0.04))
        $leftEdgeOpaque = 0
        $rightEdgeOpaque = 0
        $topEdgeOpaque = 0
        $opaqueMap = [bool[,]]::new($img.Width, $img.Height)

        for ($y = 0; $y -lt $img.Height; $y++) {
            for ($x = 0; $x -lt $img.Width; $x++) {
                $alpha = $img.GetPixel($x, $y).A
                if ($alpha -le 0) {
                    continue
                }
                $color = $img.GetPixel($x, $y)
                $colors["$($color.R),$($color.G),$($color.B)"] = $true
                $opaqueMap[$x, $y] = $true

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
                if ($x -lt $edgeBandX) {
                    $leftEdgeOpaque++
                }
                if ($x -ge ($img.Width - $edgeBandX)) {
                    $rightEdgeOpaque++
                }
                if ($y -lt $edgeBandY) {
                    $topEdgeOpaque++
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
        $colorCount = $colors.Count
        $leftEdgeCoverage = if ($edgeBandX * $img.Height -gt 0) { $leftEdgeOpaque / ($edgeBandX * $img.Height) } else { 0 }
        $rightEdgeCoverage = if ($edgeBandX * $img.Height -gt 0) { $rightEdgeOpaque / ($edgeBandX * $img.Height) } else { 0 }
        $topEdgeCoverage = if ($edgeBandY * $img.Width -gt 0) { $topEdgeOpaque / ($edgeBandY * $img.Width) } else { 0 }
        $topHalfOpaque = 0
        $bottomHalfOpaque = 0
        $halfY = [Math]::Floor($img.Height / 2)
        for ($y = 0; $y -lt $img.Height; $y++) {
            for ($x = 0; $x -lt $img.Width; $x++) {
                if (-not $opaqueMap[$x, $y]) { continue }
                if ($y -lt $halfY) { $topHalfOpaque++ } else { $bottomHalfOpaque++ }
            }
        }
        $halfPixels = $img.Width * [Math]::Max(1, $halfY)
        $topHalfCoverage = if ($halfPixels -gt 0) { $topHalfOpaque / $halfPixels } else { 0 }
        $bottomHalfCoverage = if ($halfPixels -gt 0) { $bottomHalfOpaque / $halfPixels } else { 0 }
        $sideWallRatio = if ($opaque -gt 0) { $bottomHalfOpaque / $opaque } else { 0 }
        $visited = [bool[,]]::new($img.Width, $img.Height)
        $componentCount = 0
        $meaningfulComponentThreshold = [Math]::Max(8, [Math]::Floor($opaque * 0.015))
        for ($startY = 0; $startY -lt $img.Height; $startY++) {
            for ($startX = 0; $startX -lt $img.Width; $startX++) {
                if (-not $opaqueMap[$startX, $startY] -or $visited[$startX, $startY]) {
                    continue
                }

                $area = 0
                $queue = [System.Collections.Generic.Queue[int]]::new()
                $queue.Enqueue(($startY * $img.Width) + $startX)
                $visited[$startX, $startY] = $true
                while ($queue.Count -gt 0) {
                    $encoded = $queue.Dequeue()
                    $cy = [int][Math]::Floor($encoded / $img.Width)
                    $cx = [int]($encoded - ($cy * $img.Width))
                    $area++

                    $neighbors = @(
                        @{ x = $cx - 1; y = $cy },
                        @{ x = $cx + 1; y = $cy },
                        @{ x = $cx; y = $cy - 1 },
                        @{ x = $cx; y = $cy + 1 }
                    )
                    foreach ($neighbor in $neighbors) {
                        $nx = [int]$neighbor.x
                        $ny = [int]$neighbor.y
                        if ($nx -lt 0 -or $ny -lt 0 -or $nx -ge $img.Width -or $ny -ge $img.Height) {
                            continue
                        }
                        if ($opaqueMap[$nx, $ny] -and -not $visited[$nx, $ny]) {
                            $visited[$nx, $ny] = $true
                            $queue.Enqueue(($ny * $img.Width) + $nx)
                        }
                    }
                }
                if ($area -ge $meaningfulComponentThreshold) {
                    $componentCount++
                }
            }
        }

        if ($categoryName -eq "terrain") {
            $profile = $TerrainProfile
            if ($profile -eq "auto") {
                $normalizedPath = $Path.Replace("\", "/").ToLowerInvariant()
                $profile = if ($normalizedPath -match "height|raised|block|cliff|side") { "raised_block" } else { "flat" }
            }
            if (($img.Width -ne 32 -or $img.Height -ne 32) -and ($img.Width -ne 64 -or $img.Height -ne 64)) {
                $issues.Add("terrain_size_not_32x32_or_64x64")
            }
            if ($cornerOpaque -gt 0) {
                $issues.Add("terrain_opaque_corners")
            }
            if ($componentCount -gt 1) {
                $issues.Add("terrain_wrong_subject_or_detached_object")
            }
            if ($topEdgeCoverage -gt 0.03 -or $leftEdgeCoverage -gt 0.06 -or $rightEdgeCoverage -gt 0.06) {
                $issues.Add("terrain_touches_frame_edge_or_background")
            }
            if ($bboxWidth -gt 0 -and [Math]::Abs((($minX + $maxX) / 2) - (($img.Width - 1) / 2)) -gt ($img.Width * 0.12)) {
                $warnings.Add("terrain_may_be_off_anchor")
            }
            if ($profile -eq "raised_block") {
                if ($bboxWidth -gt 0 -and $bboxHeight -gt ($bboxWidth * 1.45)) {
                    $issues.Add("terrain_not_tile_footprint_tall_sprite")
                }
                if ($coverage -lt 0.16) {
                    $issues.Add("raised_block_too_empty_or_weak")
                }
                elseif ($coverage -lt 0.22) {
                    $warnings.Add("raised_block_low_coverage")
                }
                if ($coverage -gt 0.78) {
                    $issues.Add("raised_block_too_dense_or_background_heavy")
                }
                elseif ($coverage -gt 0.68) {
                    $warnings.Add("raised_block_high_coverage")
                }
                if ($topHalfCoverage -gt 0 -and ($bottomHalfCoverage / $topHalfCoverage) -lt 0.22) {
                    $issues.Add("raised_block_too_flat_or_missing_sides")
                }
                if ($sideWallRatio -lt 0.12) {
                    $issues.Add("raised_block_side_faces_too_weak")
                }
                elseif ($sideWallRatio -gt 0.45) {
                    $warnings.Add("raised_block_side_faces_dominate_top")
                }
            }
            else {
                if ($coverage -gt 0.72) {
                    $warnings.Add("terrain_may_be_square_or_block_instead_of_diamond")
                }
                if ($coverage -lt 0.18 -and $img.Width -eq 64) {
                    $issues.Add("flat_terrain_too_empty_or_weak")
                }
                if ($coverage -lt 0.16 -and $img.Width -eq 32) {
                    $issues.Add("flat_terrain_too_empty_or_weak")
                }
                if ($bboxWidth -gt 0 -and $bboxHeight -gt ($bboxWidth * 1.05)) {
                    $issues.Add("terrain_not_tile_footprint_tall_sprite")
                }
            }
            if ($colorCount -gt 32) {
                $issues.Add("terrain_palette_too_large_or_mixel_heavy")
            }
        }
        elseif ($categoryName -eq "prop") {
            if ($img.Width -ne 128 -or $img.Height -ne 128) {
                $warnings.Add("prop_size_not_128x128")
            }
            if ($cornerOpaque -gt 0) {
                $issues.Add("prop_opaque_corners_or_background")
            }
            if ($bottomCoverage -gt 0.45 -and $bboxWidth -gt ($img.Width * 0.65)) {
                $warnings.Add("prop_may_have_baked_ground_or_base_plate")
            }
            if ($bottomCoverage -gt 0.35 -and $bboxWidth -gt ($img.Width * 0.55)) {
                $issues.Add("prop_likely_base_plate_or_floor")
            }
            if ($colorCount -gt 48) {
                $issues.Add("prop_palette_too_large_or_mixel_heavy")
            }
            if ($coverage -gt 0.42 -and $bboxWidth -gt ($img.Width * 0.8) -and $bboxHeight -gt ($img.Height * 0.75)) {
                $issues.Add("prop_likely_diorama_room_or_scene")
            }
            if ($bboxWidth -gt ($img.Width * 0.72) -and $bboxHeight -gt ($img.Height * 0.62) -and $bottomCoverage -gt 0.28) {
                $issues.Add("prop_likely_diorama_room_or_scene")
            }
            if ($componentCount -gt 3) {
                $issues.Add("prop_too_many_foreground_components")
            }
            if ($leftEdgeCoverage -gt 0.03 -or $rightEdgeCoverage -gt 0.03 -or $topEdgeCoverage -gt 0.03) {
                $issues.Add("prop_touches_frame_edge_or_background")
            }
        }
        elseif ($categoryName -eq "item") {
            if ($img.Width -ne 64 -or $img.Height -ne 64) {
                $warnings.Add("item_size_not_64x64")
            }
            if ($cornerOpaque -gt 0) {
                $issues.Add("item_opaque_corners_or_background")
            }
            if ($coverage -gt 0.85) {
                $warnings.Add("item_may_include_background_or_ui_frame")
            }
            if ($coverage -lt 0.04) {
                $issues.Add("item_too_small_or_semantic_miss")
            }
            if ($coverage -gt 0.38) {
                $issues.Add("item_likely_badge_or_dense_background_shape")
            }
            if ($coverage -gt 0.45 -and $bboxWidth -gt ($img.Width * 0.72) -and $bboxHeight -gt ($img.Height * 0.72)) {
                $issues.Add("item_likely_badge_ui_frame_or_background_shape")
            }
            if ($coverage -gt 0.22 -and $bboxWidth -gt ($img.Width * 0.68) -and $bboxHeight -gt ($img.Height * 0.68) -and [Math]::Abs($bboxWidth - $bboxHeight) -le ($img.Width * 0.12)) {
                $issues.Add("item_likely_square_or_circular_backing")
            }
            if ($colorCount -gt 40) {
                $issues.Add("item_palette_too_large_or_mixel_heavy")
            }
            if ($componentCount -gt 2) {
                $issues.Add("item_too_many_foreground_components")
            }
            if ($leftEdgeCoverage -gt 0.03 -or $rightEdgeCoverage -gt 0.03 -or $topEdgeCoverage -gt 0.03) {
                $issues.Add("item_touches_frame_edge_or_background")
            }
            if ($bboxWidth -gt 0 -and $bboxHeight -gt 0) {
                $centerX = ($minX + $maxX) / 2
                $centerY = ($minY + $maxY) / 2
                if ([Math]::Abs($centerX - (($img.Width - 1) / 2)) -gt ($img.Width * 0.18) -or [Math]::Abs($centerY - (($img.Height - 1) / 2)) -gt ($img.Height * 0.18)) {
                    $warnings.Add("item_may_not_be_centered")
                }
            }
        }
        elseif ($categoryName -eq "character" -or $categoryName -eq "npc" -or $categoryName -eq "mob") {
            if ($cornerOpaque -gt 0) {
                $issues.Add("sprite_sheet_opaque_corners_or_background")
            }
            if ($img.Height -ne 128 -and $img.Height -ne 256) {
                $warnings.Add("sprite_sheet_cell_height_unexpected")
            }
            if ($img.Height -gt 0 -and $img.Width % $img.Height -ne 0) {
                $warnings.Add("sprite_sheet_width_not_multiple_of_cell_height")
            }
            if ($coverage -gt 0.75) {
                $warnings.Add("sprite_sheet_may_include_background_or_uncropped_cells")
            }
            if ($colorCount -gt 96) {
                $warnings.Add("sprite_sheet_palette_large")
            }
        }
        else {
            if ($cornerOpaque -gt 0) {
                $issues.Add("vfx_opaque_corners_or_background")
            }
            if ($img.Height -ne 32 -and $img.Height -ne 64 -and $img.Height -ne 96 -and $img.Height -ne 128 -and $img.Height -ne 256) {
                $warnings.Add("vfx_cell_height_unexpected")
            }
            if ($img.Height -gt 0 -and $img.Width % $img.Height -ne 0) {
                $warnings.Add("vfx_sheet_width_not_multiple_of_cell_height")
            }
            if ($colorCount -gt 64) {
                $warnings.Add("vfx_palette_large")
            }
        }

        [PSCustomObject]@{
            path = $Path
            category = $categoryName
            width = $img.Width
            height = $img.Height
            opaque_pixels = $opaque
            color_count = $colorCount
            coverage = [Math]::Round($coverage, 4)
            bbox = @($minX, $minY, $maxX, $maxY)
            corner_opaque_pixels = $cornerOpaque
            bottom_band_coverage = [Math]::Round($bottomCoverage, 4)
            left_edge_coverage = [Math]::Round($leftEdgeCoverage, 4)
            right_edge_coverage = [Math]::Round($rightEdgeCoverage, 4)
            top_edge_coverage = [Math]::Round($topEdgeCoverage, 4)
            terrain_profile = if ($categoryName -eq "terrain") { if ($TerrainProfile -eq "auto" -and $Path.Replace("\", "/").ToLowerInvariant() -match "height|raised|block|cliff|side") { "raised_block" } elseif ($TerrainProfile -eq "auto") { "flat" } else { $TerrainProfile } } else { "" }
            top_half_coverage = [Math]::Round($topHalfCoverage, 4)
            bottom_half_coverage = [Math]::Round($bottomHalfCoverage, 4)
            side_wall_ratio = [Math]::Round($sideWallRatio, 4)
            meaningful_components = $componentCount
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
            "terrain 32x32 or 64x64",
            "prop 128x128 warning",
            "item 64x64 warning",
            "character/NPC/mob transparent sheet",
            "VFX transparent sheet",
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
