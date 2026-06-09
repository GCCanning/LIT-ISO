param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$JobName,
    [string]$RequestPath,
    [switch]$ReplaceExisting
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function Get-SafeName {
    param([string]$Value, [string]$Default = "asset_job")
    if ([string]::IsNullOrWhiteSpace($Value)) { return $Default }
    $safe = ($Value.Trim() -replace "[^A-Za-z0-9_.-]", "_")
    if ([string]::IsNullOrWhiteSpace($safe)) { return $Default }
    return $safe
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
        throw "$Label must stay inside ProjectRoot. Root: $rootFull Path: $pathFull"
    }
}

function New-HexId {
    return [Guid]::NewGuid().ToString("N")
}

function New-Dir {
    param([Parameter(Mandatory = $true)][string]$Path)
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

function Get-PropValue {
    param([object]$Object, [string]$Name, [object]$Default)
    if ($null -eq $Object) {
        return $Default
    }

    if ($Object -is [System.Collections.IDictionary] -and $Object.Contains($Name) -and $null -ne $Object[$Name]) {
        return $Object[$Name]
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -ne $property -and $null -ne $property.Value) {
        return $property.Value
    }
    return $Default
}

function New-Brush {
    param([int]$A, [int]$R, [int]$G, [int]$B)
    return [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb($A, $R, $G, $B))
}

function New-Bitmap {
    param([int]$Width, [int]$Height)
    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    return [PSCustomObject]@{ Bitmap = $bitmap; Graphics = $graphics }
}

function Save-Bitmap {
    param([object]$Canvas, [string]$Path)
    New-Dir -Path (Split-Path -Parent $Path)
    $Canvas.Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $Canvas.Graphics.Dispose()
    $Canvas.Bitmap.Dispose()
}

function Write-TextureMeta {
    param(
        [string]$PngPath,
        [int]$Ppu,
        [double]$PivotX,
        [double]$PivotY
    )
    $name = [IO.Path]::GetFileNameWithoutExtension($PngPath)
    $content = @"
fileFormatVersion: 2
guid: $(New-HexId)
TextureImporter:
  internalIDToNameTable:
  - first:
      213: 1
    second: ${name}_0
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
    sRGBTexture: 1
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 9
  spritePivot: {x: $PivotX, y: $PivotY}
  spritePixelsToUnits: $Ppu
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  maxTextureSize: 2048
  textureCompression: 0
"@
    Set-Content -LiteralPath "$PngPath.meta" -Value $content -Encoding UTF8
}

function Measure-Png {
    param([string]$Path)
    $img = [System.Drawing.Bitmap]::FromFile($Path)
    try {
        $opaque = 0
        $minX = $img.Width
        $minY = $img.Height
        $maxX = -1
        $maxY = -1
        $cornerOpaque = 0

        for ($y = 0; $y -lt $img.Height; $y++) {
            for ($x = 0; $x -lt $img.Width; $x++) {
                $alpha = $img.GetPixel($x, $y).A
                if ($alpha -le 0) { continue }
                $opaque++
                if ($x -lt $minX) { $minX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -gt $maxY) { $maxY = $y }
                $nearLeft = $x -lt [Math]::Max(2, [Math]::Floor($img.Width * 0.08))
                $nearRight = $x -ge $img.Width - [Math]::Max(2, [Math]::Floor($img.Width * 0.08))
                $nearTop = $y -lt [Math]::Max(2, [Math]::Floor($img.Height * 0.08))
                $nearBottom = $y -ge $img.Height - [Math]::Max(2, [Math]::Floor($img.Height * 0.08))
                if (($nearLeft -or $nearRight) -and ($nearTop -or $nearBottom)) { $cornerOpaque++ }
            }
        }

        return [ordered]@{
            width = $img.Width
            height = $img.Height
            opaque_pixels = $opaque
            bbox = @($minX, $minY, $maxX, $maxY)
            corner_opaque_pixels = $cornerOpaque
        }
    }
    finally {
        $img.Dispose()
    }
}

function Draw-Tile {
    param([string]$Path, [string]$Biome, [string]$Variant, [int]$Seed)
    $c = New-Bitmap -Width 32 -Height 32
    $g = $c.Graphics
    $edge = New-Brush 255 71 58 49
    $soil = New-Brush 255 101 73 58
    $grass = if ($Biome -match "plains") { New-Brush 255 147 161 75 } else { New-Brush 255 91 137 78 }
    $light = if ($Biome -match "plains") { New-Brush 255 183 185 100 } else { New-Brush 255 128 168 92 }
    $dark = New-Brush 255 58 84 58
    $top = @(
        [System.Drawing.Point]::new(16, 4),
        [System.Drawing.Point]::new(31, 15),
        [System.Drawing.Point]::new(16, 28),
        [System.Drawing.Point]::new(0, 15)
    )
    $g.FillPolygon($edge, $top)
    $inner = @(
        [System.Drawing.Point]::new(16, 6),
        [System.Drawing.Point]::new(29, 15),
        [System.Drawing.Point]::new(16, 26),
        [System.Drawing.Point]::new(2, 15)
    )
    $g.FillPolygon($(if ($Variant -match "dirt") { $soil } else { $grass }), $inner)
    for ($i = 0; $i -lt 26; $i++) {
        $x = (7 + (($Seed + $i * 11) % 19))
        $y = (9 + (($Seed + $i * 7) % 15))
        if ([Math]::Abs($x - 16) / 15 + [Math]::Abs($y - 16) / 11 -le 1) {
            $g.FillRectangle($(if ($i % 3 -eq 0) { $light } else { $dark }), $x, $y, 1, 1)
        }
    }
    Save-Bitmap -Canvas $c -Path $Path
    $edge.Dispose(); $soil.Dispose(); $grass.Dispose(); $light.Dispose(); $dark.Dispose()
    Write-TextureMeta -PngPath $Path -Ppu 32 -PivotX 0.5 -PivotY 0.75
}

function Draw-Prop {
    param([string]$Path, [string]$Subtype, [int]$Seed)
    $c = New-Bitmap -Width 128 -Height 128
    $g = $c.Graphics
    $outline = New-Brush 255 32 34 31
    $leaf = New-Brush 255 78 125 76
    $leaf2 = New-Brush 255 107 151 83
    $wood = New-Brush 255 111 75 54
    $rock = New-Brush 255 112 105 94
    $rockLight = New-Brush 255 157 142 122
    if ($Subtype -match "tree") {
        $g.FillRectangle($outline, 58, 72, 14, 40)
        $g.FillRectangle($wood, 61, 74, 8, 38)
        foreach ($p in @(@(40,26,34,30),@(59,18,32,34),@(72,31,32,28),@(47,45,46,34),@(67,51,36,28))) {
            $g.FillEllipse($outline, $p[0]-2, $p[1]-2, $p[2]+4, $p[3]+4)
            $g.FillEllipse($(if (($p[0]+$Seed)%2 -eq 0) { $leaf } else { $leaf2 }), $p[0], $p[1], $p[2], $p[3])
        }
    }
    elseif ($Subtype -match "rock|boulder") {
        $poly = @(
            [System.Drawing.Point]::new(36, 72),
            [System.Drawing.Point]::new(58, 48),
            [System.Drawing.Point]::new(88, 54),
            [System.Drawing.Point]::new(104, 82),
            [System.Drawing.Point]::new(78, 105),
            [System.Drawing.Point]::new(45, 100)
        )
        $g.FillPolygon($outline, $poly)
        $inner = @(
            [System.Drawing.Point]::new(40, 74),
            [System.Drawing.Point]::new(60, 53),
            [System.Drawing.Point]::new(84, 59),
            [System.Drawing.Point]::new(98, 82),
            [System.Drawing.Point]::new(76, 99),
            [System.Drawing.Point]::new(48, 95)
        )
        $g.FillPolygon($rock, $inner)
        $g.FillRectangle($rockLight, 60, 60, 14, 6)
        $g.FillRectangle($rockLight, 47, 78, 10, 5)
    }
    else {
        foreach ($p in @(@(34,63,32,24),@(52,50,36,30),@(72,61,32,26),@(45,75,48,24),@(62,70,34,24))) {
            $g.FillEllipse($outline, $p[0]-2, $p[1]-2, $p[2]+4, $p[3]+4)
            $g.FillEllipse($(if (($p[0]+$Seed)%2 -eq 0) { $leaf } else { $leaf2 }), $p[0], $p[1], $p[2], $p[3])
        }
    }
    Save-Bitmap -Canvas $c -Path $Path
    $outline.Dispose(); $leaf.Dispose(); $leaf2.Dispose(); $wood.Dispose(); $rock.Dispose(); $rockLight.Dispose()
    Write-TextureMeta -PngPath $Path -Ppu 128 -PivotX 0.5 -PivotY 0
}

function Draw-Item {
    param([string]$Path, [string]$Subtype)
    $c = New-Bitmap -Width 64 -Height 64
    $g = $c.Graphics
    $outline = New-Brush 255 28 31 34
    $metal = New-Brush 255 150 177 184
    $metalLight = New-Brush 255 218 242 240
    $wood = New-Brush 255 122 76 46
    if ($Subtype -match "potion") {
        $cyan = New-Brush 255 66 205 219
        $g.FillRectangle($outline, 26, 14, 12, 8)
        $g.FillRectangle($metalLight, 28, 15, 8, 5)
        $g.FillRectangle($outline, 19, 22, 26, 28)
        $g.FillRectangle($cyan, 22, 25, 20, 22)
        $cyan.Dispose()
    }
    else {
        $g.FillRectangle($outline, 29, 12, 7, 42)
        $g.FillRectangle($wood, 31, 15, 3, 38)
        $blade = @(
            [System.Drawing.Point]::new(20, 12),
            [System.Drawing.Point]::new(43, 18),
            [System.Drawing.Point]::new(36, 30),
            [System.Drawing.Point]::new(18, 25)
        )
        $g.FillPolygon($outline, $blade)
        $inner = @(
            [System.Drawing.Point]::new(23, 14),
            [System.Drawing.Point]::new(39, 18),
            [System.Drawing.Point]::new(34, 27),
            [System.Drawing.Point]::new(22, 24)
        )
        $g.FillPolygon($metal, $inner)
        $g.FillRectangle($metalLight, 28, 16, 8, 2)
    }
    Save-Bitmap -Canvas $c -Path $Path
    $outline.Dispose(); $metal.Dispose(); $metalLight.Dispose(); $wood.Dispose()
    Write-TextureMeta -PngPath $Path -Ppu 64 -PivotX 0.5 -PivotY 0.5
}

function Draw-CharacterSheet {
    param([string]$Path, [string]$Mode, [object[]]$Directions, [int]$Frames, [int]$CellSize, [string]$Subtype)
    $rows = [Math]::Max(1, $Directions.Count)
    $c = New-Bitmap -Width ($CellSize * $Frames) -Height ($CellSize * $rows)
    $g = $c.Graphics
    $outline = New-Brush 255 20 25 34
    $armor = if ($Mode -eq "mob") { New-Brush 255 76 145 88 } else { New-Brush 255 57 92 109 }
    $cyan = New-Brush 255 70 211 230
    $gold = New-Brush 255 225 154 55
    $shadow = New-Brush 65 24 28 30
    for ($r = 0; $r -lt $rows; $r++) {
        for ($f = 0; $f -lt $Frames; $f++) {
            $ox = $f * $CellSize
            $oy = $r * $CellSize
            $cx = $ox + [int]($CellSize / 2)
            $footY = $oy + $CellSize - 14
            $bob = if ($f % 2 -eq 0) { 0 } else { -2 }
            $scale = [Math]::Max(1, [int]($CellSize / 128))
            $g.FillEllipse($shadow, $cx - 18 * $scale, $footY - 4 * $scale, 36 * $scale, 7 * $scale)
            $g.FillRectangle($outline, $cx - 12 * $scale, $footY - 45 * $scale + $bob, 24 * $scale, 32 * $scale)
            $g.FillRectangle($armor, $cx - 9 * $scale, $footY - 42 * $scale + $bob, 18 * $scale, 27 * $scale)
            $g.FillRectangle($outline, $cx - 11 * $scale, $footY - 67 * $scale + $bob, 22 * $scale, 22 * $scale)
            $g.FillRectangle($armor, $cx - 8 * $scale, $footY - 64 * $scale + $bob, 16 * $scale, 16 * $scale)
            $g.FillRectangle($cyan, $cx - 1 * $scale, $footY - 64 * $scale + $bob, 2 * $scale, 18 * $scale)
            $g.FillRectangle($gold, $cx - 6 * $scale, $footY - 56 * $scale + $bob, 3 * $scale, 3 * $scale)
            $g.FillRectangle($gold, $cx + 4 * $scale, $footY - 56 * $scale + $bob, 3 * $scale, 3 * $scale)
            $armShift = ($f % 3) - 1
            $g.FillRectangle($outline, $cx - 21 * $scale, $footY - 39 * $scale + $bob + $armShift, 8 * $scale, 25 * $scale)
            $g.FillRectangle($outline, $cx + 13 * $scale, $footY - 39 * $scale + $bob - $armShift, 8 * $scale, 25 * $scale)
            $g.FillRectangle($outline, $cx - 10 * $scale, $footY - 14 * $scale, 7 * $scale, 14 * $scale)
            $g.FillRectangle($outline, $cx + 3 * $scale, $footY - 14 * $scale, 7 * $scale, 14 * $scale)
            if ($Subtype -match "knight|axe|sword") {
                $g.FillRectangle($cyan, $cx - 28 * $scale, $footY - 33 * $scale, 4 * $scale, 31 * $scale)
                $g.FillRectangle($outline, $cx - 30 * $scale, $footY - 35 * $scale, 8 * $scale, 4 * $scale)
            }
        }
    }
    Save-Bitmap -Canvas $c -Path $Path
    $outline.Dispose(); $armor.Dispose(); $cyan.Dispose(); $gold.Dispose(); $shadow.Dispose()
    Write-TextureMeta -PngPath $Path -Ppu 128 -PivotX 0.5 -PivotY 0
}

function Draw-VfxSheet {
    param([string]$Path, [int]$Frames, [int]$CellSize)
    $c = New-Bitmap -Width ($CellSize * $Frames) -Height $CellSize
    $g = $c.Graphics
    $cyan = New-Brush 255 90 229 238
    $blue = New-Brush 255 45 139 190
    for ($f = 0; $f -lt $Frames; $f++) {
        $ox = $f * $CellSize
        $cx = $ox + [int]($CellSize / 2)
        $cy = [int]($CellSize / 2)
        $radius = 4 + $f * 2
        for ($i = 0; $i -lt 16; $i++) {
            $a = ($i / 16.0) * [Math]::PI * 2
            $x = [int]($cx + [Math]::Cos($a) * $radius)
            $y = [int]($cy + [Math]::Sin($a) * $radius)
            $g.FillRectangle($(if ($i % 2 -eq 0) { $cyan } else { $blue }), $x, $y, 2, 2)
        }
    }
    Save-Bitmap -Canvas $c -Path $Path
    $cyan.Dispose(); $blue.Dispose()
    Write-TextureMeta -PngPath $Path -Ppu $CellSize -PivotX 0.5 -PivotY 0.5
}

function Add-ItemRecord {
    param(
        [System.Collections.Generic.List[object]]$Items,
        [string]$Path,
        [string]$Category,
        [string]$Biome,
        [string]$PackName,
        [string]$ProjectRoot
    )
    $measurement = Measure-Png -Path $Path
    $relative = $Path.Replace($ProjectRoot + "\", "").Replace("\", "/")
    $id = $relative -replace "^Assets/Generated/_Review/$PackName/", ""
    $issues = @()
    if ($measurement.opaque_pixels -le 0) { $issues += "blank_alpha" }
    if ($measurement.corner_opaque_pixels -gt 0) { $issues += "opaque_corners" }
    if ($Category -eq "terrain" -and ($measurement.width -ne 32 -or $measurement.height -ne 32)) { $issues += "terrain_size_not_32x32" }
    if ($Category -eq "decoration" -and ($measurement.width -ne 128 -or $measurement.height -ne 128)) { $issues += "prop_size_not_128x128" }
    if ($Category -eq "item" -and ($measurement.width -ne 64 -or $measurement.height -ne 64)) { $issues += "item_size_not_64x64" }
    if (($Category -eq "character" -or $Category -eq "npc" -or $Category -eq "mob") -and ($measurement.height -ne 128 -and $measurement.height -ne 256)) { $issues += "sprite_sheet_cell_height_unexpected" }
    if (($Category -eq "vfx") -and ($measurement.height -ne 32 -and $measurement.height -ne 64 -and $measurement.height -ne 96 -and $measurement.height -ne 128)) { $issues += "vfx_cell_height_unexpected" }
    $Items.Add([PSCustomObject]@{
        name = [IO.Path]::GetFileName($Path)
        path = $relative
        category = $Category
        biome = $Biome
        width = $measurement.width
        height = $measurement.height
        opaque_pixels = $measurement.opaque_pixels
        bbox = $measurement.bbox
        has_meta = Test-Path "$Path.meta"
        status = if ($issues.Count -eq 0) { "pass" } else { "review" }
        issues = $issues
    })
}

if ([string]::IsNullOrWhiteSpace($RequestPath)) {
    if ([string]::IsNullOrWhiteSpace($JobName)) { throw "Pass -JobName or -RequestPath." }
    $safeJobName = Get-SafeName -Value $JobName
    $RequestPath = Join-Path $ProjectRoot "Assets\Generated\_Review\_Requests\$safeJobName\generation_request.json"
}

if (-not (Test-Path $RequestPath)) { throw "Missing generation request: $RequestPath" }

$projectRootResolved = (Resolve-Path -LiteralPath $ProjectRoot).Path
$requestFull = (Resolve-Path -LiteralPath $RequestPath).Path
Assert-UnderRoot -Root $projectRootResolved -Path $requestFull -Label "RequestPath"
$request = Get-Content -Raw -LiteralPath $requestFull | ConvertFrom-Json

$job = Get-SafeName -Value ([string](Get-PropValue $request "job_name" (Get-PropValue $request "jobName" "asset_job")))
$mode = ([string](Get-PropValue $request "asset_mode" (Get-PropValue $request "assetMode" "tile"))).ToLowerInvariant()
$spec = Get-PropValue $request "asset_spec" (Get-PropValue $request "assetSpec" ([PSCustomObject]@{}))
$biome = Get-SafeName -Value ([string](Get-PropValue $spec "biome" "Shared")) -Default "Shared"
$biomeFolder = (Get-Culture).TextInfo.ToTitleCase($biome.ToLowerInvariant())
$variant = Get-SafeName -Value ([string](Get-PropValue $spec "variant" "base")) -Default "base"
$subtype = Get-SafeName -Value ([string](Get-PropValue $spec "subtype" $mode)) -Default $mode
$canvas = Get-PropValue $request "canvas" ([PSCustomObject]@{})
$cellSize = [int](Get-PropValue $canvas "cell_size" (Get-PropValue $canvas "width" 128))
if ($cellSize -lt 32) { $cellSize = 32 }
if ($cellSize -gt 256) { $cellSize = 256 }
$batchCount = [Math]::Max(1, [Math]::Min(16, [int](Get-PropValue $request "batch_count" (Get-PropValue $request "batchCount" 1))))
$animation = Get-PropValue $request "animation" ([PSCustomObject]@{ name = "none"; frame_count = 1 })
$frames = [Math]::Max(1, [Math]::Min(16, [int](Get-PropValue $animation "frame_count" (Get-PropValue $animation "frameCount" 1))))
$directions = @("S")
$directionMode = [string](Get-PropValue $request "directions" "none")
if ($directionMode -eq "4d") { $directions = @("S", "E", "N", "W") }
if ($directionMode -eq "8d") { $directions = @("S", "SE", "E", "NE", "N", "NW", "W", "SW") }

$reviewRoot = Join-Path $ProjectRoot "Assets\Generated\_Review\$job"
Assert-UnderRoot -Root $projectRootResolved -Path $reviewRoot -Label "ReviewRoot"
if ((Test-Path $reviewRoot) -and -not $ReplaceExisting.IsPresent) {
    throw "Review pack already exists. Pass -ReplaceExisting to overwrite: $reviewRoot"
}
if (Test-Path $reviewRoot) { Remove-Item -LiteralPath $reviewRoot -Recurse -Force }
New-Dir -Path $reviewRoot
$styleSnapshotSource = Join-Path (Split-Path -Parent $requestFull) "Inputs\style_profile.snapshot.json"
$styleProvenancePath = Join-Path $reviewRoot "style_provenance.json"
$styleProvenanceRepoPath = ""
if (Test-Path $styleSnapshotSource) {
    Copy-Item -LiteralPath $styleSnapshotSource -Destination $styleProvenancePath -Force
    $styleProvenanceRepoPath = $styleProvenancePath.Replace($ProjectRoot + "\", "").Replace("\", "/")
}

$items = [System.Collections.Generic.List[object]]::new()
$seedText = [string](Get-PropValue $request "seed" "0")
$seed = [Math]::Abs($seedText.GetHashCode())
$generatedFiles = @()

switch ($mode) {
    "tile" {
        for ($i = 0; $i -lt $batchCount; $i++) {
            $name = if ($batchCount -eq 1) { "${job}.png" } else { "${job}_v$($i + 1).png" }
            $path = Join-Path $reviewRoot "$biomeFolder\$name"
            Draw-Tile -Path $path -Biome $biome -Variant $variant -Seed ($seed + $i)
            Add-ItemRecord -Items $items -Path $path -Category "terrain" -Biome $biomeFolder -PackName $job -ProjectRoot $ProjectRoot
            $generatedFiles += $path
        }
    }
    "prop" {
        for ($i = 0; $i -lt $batchCount; $i++) {
            $name = if ($batchCount -eq 1) { "${job}.png" } else { "${job}_v$($i + 1).png" }
            $path = Join-Path $reviewRoot "Decorations\$biomeFolder\$name"
            Draw-Prop -Path $path -Subtype $subtype -Seed ($seed + $i)
            Add-ItemRecord -Items $items -Path $path -Category "decoration" -Biome $biomeFolder -PackName $job -ProjectRoot $ProjectRoot
            $generatedFiles += $path
        }
    }
    "item" {
        for ($i = 0; $i -lt $batchCount; $i++) {
            $name = if ($batchCount -eq 1) { "${job}.png" } else { "${job}_v$($i + 1).png" }
            $path = Join-Path $reviewRoot "Items\$biomeFolder\$name"
            Draw-Item -Path $path -Subtype $subtype
            Add-ItemRecord -Items $items -Path $path -Category "item" -Biome $biomeFolder -PackName $job -ProjectRoot $ProjectRoot
            $generatedFiles += $path
        }
    }
    "animation" {
        $path = Join-Path $reviewRoot "Effects\$biomeFolder\${job}_sheet.png"
        Draw-VfxSheet -Path $path -Frames $frames -CellSize ([Math]::Min($cellSize, 128))
        Add-ItemRecord -Items $items -Path $path -Category "vfx" -Biome $biomeFolder -PackName $job -ProjectRoot $ProjectRoot
        $generatedFiles += $path
    }
    default {
        $clips = @($request.clips)
        if ($clips.Count -eq 0) {
            $clips = @([PSCustomObject]@{ name = if ([string](Get-PropValue $animation "name" "none") -eq "none") { "idle" } else { [string](Get-PropValue $animation "name" "idle") }; frame_count = $frames })
        }
        foreach ($clip in $clips) {
            $clipName = Get-SafeName -Value ([string](Get-PropValue $clip "name" "idle")) -Default "idle"
            $clipFrames = [Math]::Max(1, [Math]::Min(16, [int](Get-PropValue $clip "frame_count" $frames)))
            $path = Join-Path $reviewRoot "Characters\$biomeFolder\${job}_${clipName}.png"
            Draw-CharacterSheet -Path $path -Mode $mode -Directions $directions -Frames $clipFrames -CellSize ([Math]::Min($cellSize, 256)) -Subtype $subtype
            Add-ItemRecord -Items $items -Path $path -Category $mode -Biome $biomeFolder -PackName $job -ProjectRoot $ProjectRoot
            $generatedFiles += $path
        }
    }
}

$passCount = @($items | Where-Object { $_.status -eq "pass" }).Count
$reviewCount = @($items | Where-Object { $_.status -ne "pass" }).Count
$generatedUtc = (Get-Date).ToUniversalTime().ToString("o")

$qualityContract = [ordered]@{
    local_worker = "Deterministic draft renderer for request/QA pipeline validation."
    generation = "Replace with ComfyUI provider output for production art quality."
    post_process = "Generated transparent PNGs with point-filtering metadata; ComfyUI outputs must still run cleanup/snap/QA."
}
$animationContract = [ordered]@{
    canonical_direction_order = @("S", "SE", "E", "NE", "N", "NW", "W", "SW")
    selected_direction_order = @($directions)
    direction_count = $directions.Count
    cell_size = $cellSize
    clips = @(@($request.clips) | ForEach-Object {
        [ordered]@{
            name = [string](Get-PropValue $_ "name" "idle")
            frame_count = [int](Get-PropValue $_ "frame_count" $frames)
            fps = [int](Get-PropValue $_ "fps" 8)
            loop = [bool](Get-PropValue $_ "loop" $true)
            sheet_rows = $directions.Count
            sheet_columns = [int](Get-PropValue $_ "frame_count" $frames)
            direction_order = @($directions)
        }
    })
    note = "Rows are directions, columns are frames. Production path is 4D first, then 8D once anchor QA is stable."
}

$report = [ordered]@{
    pack_name = $job
    generated_utc = $generatedUtc
    source_request = $requestFull.Replace($ProjectRoot + "\", "").Replace("\", "/")
    provider = "local_draft_worker"
    asset_mode = $mode
    total = $items.Count
    terrain_count = @($items | Where-Object { $_.category -eq "terrain" }).Count
    decoration_count = @($items | Where-Object { $_.category -eq "decoration" }).Count
    item_count = @($items | Where-Object { $_.category -eq "item" }).Count
    character_count = @($items | Where-Object { $_.category -eq "character" -or $_.category -eq "npc" -or $_.category -eq "mob" }).Count
    vfx_count = @($items | Where-Object { $_.category -eq "vfx" }).Count
    pass_count = $passCount
    review_count = $reviewCount
    quality_contract = $qualityContract
    animation_contract = $animationContract
    style_provenance = $styleProvenanceRepoPath
    items = @($items)
}
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $reviewRoot "review_report.json") -Encoding UTF8

$decisions = @()
foreach ($item in $items) {
    $id = $item.path.Replace("\", "/") -replace "^Assets/Generated/_Review/$job/", ""
    $categoryRoot = switch ($item.category) {
        "terrain" { "Tiles" }
        "decoration" { "Props" }
        "item" { "Items" }
        "vfx" { "Effects" }
        "npc" { "NPCs" }
        "mob" { "Mobs" }
        default { "Characters" }
    }
    $decisions += [PSCustomObject]@{
        id = $id
        name = $item.name
        category = $item.category
        biome = $item.biome
        source_path = $item.path.Replace("\", "/")
        destination_path = "Assets/Generated/$categoryRoot/$($item.biome)/$($item.name)"
        review_status = $item.status
        decision = if ($item.status -eq "pass") { "pending" } else { "needs_edit" }
        approval_blocked = $item.status -ne "pass"
        training_capture = $false
        notes = "Generated by local draft worker; review before approval or training."
        issues = @($item.issues)
    }
}
$decisionPayload = [ordered]@{
    pack_name = $job
    generated_utc = $generatedUtc
    decision_contract = [ordered]@{
        allowed_decisions = @("pending", "approved", "rejected", "needs_edit")
        approval_destination = "Assets/Generated category folders"
        local_worker_note = "Default decision is pending even when draft QA passes."
    }
    summary = [ordered]@{
        total = $decisions.Count
        approved = 0
        pending = @($decisions | Where-Object { $_.decision -eq "pending" }).Count
        rejected = 0
        needs_edit = @($decisions | Where-Object { $_.decision -eq "needs_edit" }).Count
    }
    decisions = $decisions
}
$decisionPayload | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $reviewRoot "review_decisions.json") -Encoding UTF8

$strictItems = foreach ($item in $items) {
    [PSCustomObject]@{
        path = (Join-Path $ProjectRoot ($item.path -replace "/", "\"))
        category = $item.category
        width = $item.width
        height = $item.height
        opaque_pixels = $item.opaque_pixels
        bbox = $item.bbox
        status = $item.status
        issues = @($item.issues)
        warnings = @()
    }
}
$strict = [ordered]@{
    generated_utc = $generatedUtc
    input_path = $reviewRoot
    total = $items.Count
    pass_count = $passCount
    review_count = $reviewCount
    warning_count = 0
    dataset_ready = $items.Count -gt 0 -and $reviewCount -eq 0
    fail_closed = $false
    readiness_summary = [ordered]@{
        ready_for_review_export = $items.Count -gt 0 -and $reviewCount -eq 0
        required_action = if ($items.Count -eq 0) { "add_pngs" } elseif ($reviewCount -gt 0) { "fix_or_reject_review_items" } else { "review_pending_decisions" }
        checks = @("blank alpha", "opaque corners/background", "mode size expectations", "Unity metadata presence")
    }
    items = @($strictItems)
}
$strict | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $reviewRoot "strict_asset_quality_report.json") -Encoding UTF8

$manifest = [ordered]@{
    schema = "lit_iso.asset_forge.local_worker_result.v1"
    job_name = $job
    asset_mode = $mode
    provider = "local_draft_worker"
    generated_utc = $generatedUtc
    request_path = $requestFull.Replace($ProjectRoot + "\", "").Replace("\", "/")
    review_root = $reviewRoot.Replace($ProjectRoot + "\", "").Replace("\", "/")
    style_provenance = $styleProvenanceRepoPath
    animation_contract = $animationContract
    generated_files = @($generatedFiles | ForEach-Object { $_.Replace($ProjectRoot + "\", "").Replace("\", "/") })
    next_steps = @(
        "Open review_report.json in the Asset Forge dashboard.",
        "Use generated draft output to validate workflow only.",
        "Replace local draft worker with ComfyUI output for production visual quality."
    )
}
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $reviewRoot "generation_manifest.json") -Encoding UTF8

$statusPath = Join-Path (Split-Path -Parent $requestFull) "request_status.json"
if (Test-Path (Split-Path -Parent $statusPath)) {
    [ordered]@{
        ok = $true
        status = "review_pack_ready"
        generated_utc = $generatedUtc
        job_name = $job
        review_root = $reviewRoot.Replace($ProjectRoot + "\", "").Replace("\", "/")
        review_report = "Assets/Generated/_Review/$job/review_report.json"
        review_decisions = "Assets/Generated/_Review/$job/review_decisions.json"
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $statusPath -Encoding UTF8
}

[PSCustomObject]@{
    ok = $true
    status = "review_pack_ready"
    job_name = $job
    asset_mode = $mode
    review_root = $reviewRoot.Replace($ProjectRoot + "\", "").Replace("\", "/")
    generated_files = $generatedFiles.Count
    pass = $passCount
    review = $reviewCount
    review_report = "Assets/Generated/_Review/$job/review_report.json"
    review_decisions = "Assets/Generated/_Review/$job/review_decisions.json"
} | ConvertTo-Json -Depth 8
