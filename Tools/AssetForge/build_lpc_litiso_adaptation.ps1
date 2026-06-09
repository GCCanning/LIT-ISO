param(
    [string]$InputPath = "Assets/Generated/_Review/LPC_CharacterRecipeReview_v1",
    [string]$OutputPath = "Assets/Generated/_Review/LPC_LitIsoViewAdaptation_v1"
)

Add-Type -AssemblyName System.Drawing

New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null

$recipes = @(
    @{ id = "lpc_male_leather_adventurer"; title = "Male Leather Adventurer" },
    @{ id = "lpc_female_forest_scout"; title = "Female Forest Scout" },
    @{ id = "lpc_male_plate_guard"; title = "Male Plate Guard" }
)

function Get-Cell {
    param(
        [System.Drawing.Image]$Sheet,
        [int]$Column,
        [int]$Row
    )

    $bitmap = New-Object System.Drawing.Bitmap 64, 64, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $dest = New-Object System.Drawing.Rectangle 0, 0, 64, 64
    $src = New-Object System.Drawing.Rectangle ($Column * 64), ($Row * 64), 64, 64
    $graphics.DrawImage($Sheet, $dest, $src, [System.Drawing.GraphicsUnit]::Pixel)
    $graphics.Dispose()
    return $bitmap
}

function Get-Bounds {
    param([System.Drawing.Bitmap]$Bitmap)

    $minX = 64
    $minY = 64
    $maxX = -1
    $maxY = -1

    for ($y = 0; $y -lt $Bitmap.Height; $y++) {
        for ($x = 0; $x -lt $Bitmap.Width; $x++) {
            $pixel = $Bitmap.GetPixel($x, $y)
            if ($pixel.A -gt 0) {
                if ($x -lt $minX) { $minX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }

    if ($maxX -lt 0) {
        return New-Object System.Drawing.Rectangle 0, 0, 1, 1
    }

    return New-Object System.Drawing.Rectangle $minX, $minY, (($maxX - $minX) + 1), (($maxY - $minY) + 1)
}

function Convert-ToLitIsoCell {
    param([System.Drawing.Bitmap]$Cell)

    $bounds = Get-Bounds -Bitmap $Cell
    $output = New-Object System.Drawing.Bitmap 64, 64, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($output)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

    # LPC frames have generous headroom and read slightly tall for the LIT-ISO camera.
    # Normalize to a lower bottom-center anchor and a compact readable body box.
    $maxWidth = 36
    $maxHeight = 49
    $scale = [Math]::Min($maxWidth / $bounds.Width, $maxHeight / $bounds.Height)
    $drawWidth = [Math]::Max(1, [int][Math]::Round($bounds.Width * $scale))
    $drawHeight = [Math]::Max(1, [int][Math]::Round($bounds.Height * $scale * 0.90))
    $anchorX = 32
    $anchorY = 61
    $drawX = [int][Math]::Round($anchorX - ($drawWidth / 2))
    $drawY = [int][Math]::Round($anchorY - $drawHeight)

    $dest = New-Object System.Drawing.Rectangle $drawX, $drawY, $drawWidth, $drawHeight
    $graphics.DrawImage($Cell, $dest, $bounds, [System.Drawing.GraphicsUnit]::Pixel)
    $graphics.Dispose()
    return $output
}

function Save-AdaptedSheet {
    param(
        [string]$RecipeId
    )

    $sheetPath = Join-Path $InputPath "$($RecipeId)_sheet.png"
    if (!(Test-Path $sheetPath)) {
        throw "Missing sheet: $sheetPath"
    }

    $source = [System.Drawing.Image]::FromFile($sheetPath)
    $adapted = New-Object System.Drawing.Bitmap $source.Width, $source.Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($adapted)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor

    $columns = [int]($source.Width / 64)
    $rows = [int]($source.Height / 64)
    for ($row = 0; $row -lt $rows; $row++) {
        for ($col = 0; $col -lt $columns; $col++) {
            $cell = Get-Cell -Sheet $source -Column $col -Row $row
            $litIso = Convert-ToLitIsoCell -Cell $cell
            $graphics.DrawImage($litIso, ($col * 64), ($row * 64), 64, 64)
            $cell.Dispose()
            $litIso.Dispose()
        }
    }

    $outFile = Join-Path $OutputPath "$($RecipeId)_litiso_adapted_sheet.png"
    $adapted.Save($outFile, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $adapted.Dispose()
    $source.Dispose()
}

foreach ($recipe in $recipes) {
    Save-AdaptedSheet -RecipeId $recipe.id
}

$cardWidth = 1100
$cardHeight = 250
$preview = New-Object System.Drawing.Bitmap $cardWidth, ($cardHeight * $recipes.Count), ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($preview)
$g.Clear([System.Drawing.Color]::FromArgb(245, 246, 238))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor

$titleFont = New-Object System.Drawing.Font "Arial", 12, ([System.Drawing.FontStyle]::Bold)
$font = New-Object System.Drawing.Font "Arial", 9
$smallFont = New-Object System.Drawing.Font "Arial", 8
$brush = [System.Drawing.Brushes]::Black
$muted = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(70, 70, 70))
$frames = @(
    @{ label = "back"; col = 4; row = 8 },
    @{ label = "west"; col = 4; row = 9 },
    @{ label = "front"; col = 4; row = 10 },
    @{ label = "east"; col = 4; row = 11 }
)

for ($i = 0; $i -lt $recipes.Count; $i++) {
    $recipe = $recipes[$i]
    $y = ($i * $cardHeight) + 10
    $g.DrawString($recipe.title, $titleFont, $brush, 12, $y)
    $g.DrawString("Original LPC", $font, $muted, 22, ($y + 32))
    $g.DrawString("LIT-ISO normalized", $font, $muted, 410, ($y + 32))

    $original = [System.Drawing.Image]::FromFile((Join-Path $InputPath "$($recipe.id)_sheet.png"))
    $adapted = [System.Drawing.Image]::FromFile((Join-Path $OutputPath "$($recipe.id)_litiso_adapted_sheet.png"))

    for ($f = 0; $f -lt $frames.Count; $f++) {
        $frame = $frames[$f]
        $origCell = Get-Cell -Sheet $original -Column $frame.col -Row $frame.row
        $adaptCell = Get-Cell -Sheet $adapted -Column $frame.col -Row $frame.row
        $ox = 20 + ($f * 88)
        $ax = 410 + ($f * 88)
        $fy = $y + 60
        $g.DrawImage($origCell, $ox, $fy, 128, 128)
        $g.DrawImage($adaptCell, $ax, $fy, 128, 128)
        $g.DrawString($frame.label, $smallFont, $muted, $ox, ($fy + 130))
        $g.DrawString($frame.label, $smallFont, $muted, $ax, ($fy + 130))
        $origCell.Dispose()
        $adaptCell.Dispose()
    }

    $g.DrawString("Change:", $font, $brush, 790, ($y + 60))
    $g.DrawString("bottom-center anchor", $smallFont, $brush, 790, ($y + 82))
    $g.DrawString("reduced headroom", $smallFont, $brush, 790, ($y + 100))
    $g.DrawString("slightly shorter body read", $smallFont, $brush, 790, ($y + 118))
    $g.DrawString("Training use:", $font, $brush, 790, ($y + 148))
    $g.DrawString("animation/body consistency source", $smallFont, $brush, 790, ($y + 170))
    $g.DrawString("not final shipped actor art", $smallFont, $brush, 790, ($y + 188))
    $original.Dispose()
    $adapted.Dispose()
}

$preview.Save((Join-Path $OutputPath "lpc_litiso_adaptation_contact.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$preview.Dispose()

$manifest = [ordered]@{
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    input = $InputPath
    output = $OutputPath
    excluded = @("lpc_child_crusader_url_sample")
    method = "deterministic per-cell bbox normalization; bottom-center anchor; reduced headroom; compact vertical fit"
    note = "This does not change LPC camera into true LIT-ISO isometric art. It prepares cleaner actor sheets for review/training before AI style transfer."
    recipes = $recipes
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -Path (Join-Path $OutputPath "adaptation_manifest.json") -Encoding UTF8

Get-ChildItem -Path $OutputPath | Select-Object Name, Length
