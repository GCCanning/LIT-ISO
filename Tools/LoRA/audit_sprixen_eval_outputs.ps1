param(
    [string]$EvalDir = "C:\Projects\Pixel Pipeline\generated\litiso_sprixen_frame_v1_eval",
    [string]$OutputPath = "Temp\LoRA\sprixen_eval_quality_report.json",
    [int]$MaxSpriteBounds = 180,
    [int]$MaxPaletteEstimate = 220,
    [double]$MaxForegroundCoverage = 0.35
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function Get-ImageAudit {
    param([string]$Path)

    $bitmap = [System.Drawing.Bitmap]::new($Path)
    try {
        $width = $bitmap.Width
        $height = $bitmap.Height
        $minX = $width
        $minY = $height
        $maxX = -1
        $maxY = -1
        $opaque = 0
        $palette = New-Object "System.Collections.Generic.HashSet[string]"
        $cornerAlpha = 0
        $cornerSamples = @(
            @(0, 0),
            @(($width - 1), 0),
            @(0, ($height - 1)),
            @(($width - 1), ($height - 1))
        )

        foreach ($point in $cornerSamples) {
            $color = $bitmap.GetPixel($point[0], $point[1])
            $cornerAlpha = [Math]::Max($cornerAlpha, $color.A)
        }

        $step = [Math]::Max(1, [int][Math]::Floor($width / 128))
        for ($y = 0; $y -lt $height; $y += $step) {
            for ($x = 0; $x -lt $width; $x += $step) {
                $color = $bitmap.GetPixel($x, $y)
                if ($color.A -gt 12) {
                    $opaque++
                    $null = $palette.Add("$($color.R -band 248),$($color.G -band 248),$($color.B -band 248)")
                    if ($x -lt $minX) { $minX = $x }
                    if ($y -lt $minY) { $minY = $y }
                    if ($x -gt $maxX) { $maxX = $x }
                    if ($y -gt $maxY) { $maxY = $y }
                }
            }
        }

        $boundsWidth = if ($maxX -ge $minX) { $maxX - $minX + 1 } else { 0 }
        $boundsHeight = if ($maxY -ge $minY) { $maxY - $minY + 1 } else { 0 }
        $pixelCount = $width * $height
        $coverage = if ($pixelCount -gt 0) { [Math]::Round($opaque / $pixelCount, 4) } else { 0 }

        $issues = @()
        if ($cornerAlpha -gt 8) { $issues += "opaque_or_flat_background_corners" }
        if ($boundsWidth -gt $MaxSpriteBounds -or $boundsHeight -gt $MaxSpriteBounds) { $issues += "foreground_too_large_for_sprite_frame" }
        if ($coverage -gt $MaxForegroundCoverage) { $issues += "foreground_or_scene_coverage_too_high" }
        if ($palette.Count -gt $MaxPaletteEstimate) { $issues += "palette_too_large_for_tiny_pixel_sprite" }

        [ordered]@{
            file = $Path
            width = $width
            height = $height
            maxCornerAlpha = $cornerAlpha
            foregroundBounds = [ordered]@{
                x = if ($boundsWidth) { $minX } else { 0 }
                y = if ($boundsHeight) { $minY } else { 0 }
                width = $boundsWidth
                height = $boundsHeight
            }
            foregroundCoverage = $coverage
            paletteEstimate = $palette.Count
            status = if ($issues.Count -eq 0) { "pass" } else { "fail" }
            issues = $issues
        }
    } finally {
        $bitmap.Dispose()
    }
}

if (!(Test-Path -LiteralPath $EvalDir)) {
    throw "Evaluation directory not found: $EvalDir"
}

$images = Get-ChildItem -LiteralPath $EvalDir -Filter "*.png" -File
$entries = @($images | ForEach-Object { Get-ImageAudit -Path $_.FullName })
$failed = @($entries | Where-Object { $_.status -ne "pass" })

$report = [ordered]@{
    generatedAt = (Get-Date).ToString("o")
    evalDir = $EvalDir
    total = $entries.Count
    passed = $entries.Count - $failed.Count
    failed = $failed.Count
    promotionReady = $failed.Count -eq 0 -and $entries.Count -gt 0
    thresholds = [ordered]@{
        maxSpriteBounds = $MaxSpriteBounds
        maxPaletteEstimate = $MaxPaletteEstimate
        maxForegroundCoverage = $MaxForegroundCoverage
    }
    entries = $entries
}

New-Item -ItemType Directory -Path (Split-Path -Parent $OutputPath) -Force | Out-Null
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Host "Sprixen eval quality: $($report.passed)/$($report.total) passed. Report: $OutputPath"
if (-not $report.promotionReady) {
    exit 2
}
