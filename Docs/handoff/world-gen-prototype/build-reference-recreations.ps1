$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Join-Path $env:TEMP 'litiso-archive-review'
$tileRoot = Join-Path $root 'tileset\isometric tileset\separated images'
$outDir = Join-Path $root 'reference-recreations'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$tileCache = @{}
function Get-TileImage([int]$n) {
    if (-not $tileCache.ContainsKey($n)) {
        $p = Join-Path $tileRoot ('tile_{0:d3}.png' -f $n)
        $tileCache[$n] = [System.Drawing.Image]::FromFile($p)
    }
    return $tileCache[$n]
}

function Pick-From([int[]]$arr, [int]$x, [int]$y, [int]$seed) {
    $idx = [Math]::Abs((($x + 11) * 73856093) -bxor (($y + 17) * 19349663) -bxor ($seed * 83492791)) % $arr.Length
    return $arr[$idx]
}

$S = 3           # 32px tile -> 96px
$TILE = 32 * $S
$STEPX = 16 * $S # 48
$STEPY = 8 * $S  # 24

# terrainMap entry: @{ Tiles = int[]; Off = px offset (negative = raised); Under = optional tile drawn first at Off 0 }
# propMap entries ride on the terrain offset of their cell.
# overlays: list of @(x, y, tileNo, off) drawn last.
function Render-Board(
    [string]$name,
    [string[]]$terrain,
    [hashtable]$terrainMap,
    [string[]]$props,
    [hashtable]$propMap,
    [object[]]$overlays,
    [int]$seed,
    [int]$canvasW = 1280,
    [int]$canvasH = 1024
) {
    $rows = $terrain.Length
    $cols = $terrain[0].Length

    $bmp = New-Object System.Drawing.Bitmap $canvasW, $canvasH
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromArgb(255, 0, 0, 0))
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

    $originX = [int]($canvasW / 2 - (($cols - $rows) / 2.0) * $STEPX)
    $originY = [int](($canvasH - ($rows + $cols - 2) * $STEPY) / 2)

    $drawCell = {
        param([int]$x, [int]$y, [int]$tileNo, [int]$off)
        $sx = $originX + (($x - $y) * $STEPX)
        $sy = $originY + (($x + $y) * $STEPY) + $off
        $img = Get-TileImage $tileNo
        $dest = New-Object System.Drawing.Rectangle ([int]($sx - $TILE / 2)), ([int]($sy - $TILE / 2)), $TILE, $TILE
        $g.DrawImage($img, $dest)
    }

    for ($sum = 0; $sum -le ($rows + $cols - 2); $sum++) {
        for ($y = 0; $y -lt $rows; $y++) {
            $x = $sum - $y
            if ($x -lt 0 -or $x -ge $cols) { continue }

            $tSym = $terrain[$y].Substring($x, 1)
            $tOff = 0
            if ($tSym -ne '.' -and $terrainMap.ContainsKey($tSym)) {
                $e = $terrainMap[$tSym]
                $tOff = [int]$e.Off
                if ($e.ContainsKey('Under')) {
                    & $drawCell $x $y ([int]$e.Under) 0
                }
                $tileNo = Pick-From $e.Tiles $x $y $seed
                & $drawCell $x $y $tileNo $tOff
            }

            if ($props -and $y -lt $props.Length -and $x -lt $props[$y].Length) {
                $pSym = $props[$y].Substring($x, 1)
                if ($pSym -ne '.' -and $propMap.ContainsKey($pSym)) {
                    $pe = $propMap[$pSym]
                    $pOff = $tOff + [int]$pe.Off
                    $tileNo = Pick-From $pe.Tiles $x $y ($seed + 7)
                    & $drawCell $x $y $tileNo $pOff
                }
            }
        }
    }

    if ($overlays) {
        foreach ($ov in $overlays) {
            $off = 0
            if ($ov.Length -ge 4) { $off = [int]$ov[3] }
            & $drawCell ([int]$ov[0]) ([int]$ov[1]) ([int]$ov[2]) $off
        }
    }

    $path = Join-Path $outDir ($name + '.png')
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    return $path
}

# ============================================================================
# REFERENCE 1 - meadow bowl: thick ragged dirt rim (flush, side art does the
# depth), meadow, big pond left-of-center with pale top-left edge, logs top,
# grey boulders left, brown rocks lining the NE inner wall, bushes + flowers
# on the right.
# ============================================================================
$ref1Terrain = @(
    '....DD.DD.....',
    '..DDDDDDDDDD..',
    '.DDDDDDDDDDDD.',
    '.DDGGGGGGGDDD.',
    'DDGGWWWWGGGDD.',
    'DDGEWWWWWGGDD.',
    '.DDGEWWWWGGDD.',
    '.DDGEWWWWGGDD.',
    '..DDGGGGGGDDD.',
    '..DDDGGGGDDD..',
    '...DDDDDDDD...',
    '.....DDDDD....'
)
$ref1Props = @(
    '..............',
    '..............',
    '.........RRR..',
    '.....tl...RR..',
    '..........bR..',
    '...q.....bbR..',
    '..k......fbb..',
    '..k......bbb..',
    '........fbp...',
    '.......f.b....',
    '..............',
    '..............'
)
$ref1TerrainMap = @{
    'D' = @{ Tiles = @(0, 0, 0, 1, 2, 6); Off = 0 }
    'G' = @{ Tiles = @(37);               Off = 0 }
    'W' = @{ Tiles = @(104);              Off = 0 }
    'E' = @{ Tiles = @(106, 107);         Off = 0 }   # pale pond edge (left)
}
$ref1PropMap = @{
    't' = @{ Tiles = @(49);          Off = -8 }   # log pile
    'l' = @{ Tiles = @(51);          Off = -8 }   # log w/ grass
    'k' = @{ Tiles = @(65);          Off = -8 }   # grey boulder
    'q' = @{ Tiles = @(67);          Off = -8 }   # grey boulder pair
    'R' = @{ Tiles = @(54, 56, 53);  Off = -8 }   # brown rocks on rim
    'b' = @{ Tiles = @(30, 31, 35);  Off = -8 }   # bushes
    'f' = @{ Tiles = @(41, 46);      Off = -8 }   # warm flowers
    'p' = @{ Tiles = @(44);          Off = -8 }   # pink tulips
}

# ============================================================================
# REFERENCE 2 - dark-water island: near-uniform deep navy field with sparkle
# overlays, tight island, foam stone ring left/bottom/right, dense bushes,
# flowers peeking over the top edge (drawn last).
# ============================================================================
$ref2Terrain = @(
    '.....WWWWWW.....',
    '...WWWWWWWWWW...',
    '..WWWWGGGGWWWW..',
    '.WWWWSGGGGSWWWW.',
    'WWWWSSGGGSWWWWWW',
    '.WWWWWSSSWWWWWW.',
    '..WWWWWWWWWWWW..',
    '...WWWWWWWWWW...',
    '.....WWWWWW.....'
)
$ref2Props = @(
    '................',
    '................',
    '................',
    '......bbbb......',
    '......bbb.......',
    '................',
    '................',
    '................',
    '................'
)
$ref2TerrainMap = @{
    'W' = @{ Tiles = @(92); Off = 0 }
    'G' = @{ Tiles = @(40);                               Off = 0 }
    'S' = @{ Tiles = @(66, 70, 71, 78, 81);               Off = 0 }
}
$ref2PropMap = @{
    'b' = @{ Tiles = @(30, 31, 35); Off = -8 }
}
$ref2Overlays = @()
# flowers along the island top edge, drawn last so they peek over the bushes
$ref2Overlays += , @(6, 2, 41, -8)
$ref2Overlays += , @(8, 2, 46, -8)
# water sparkles
$rand = New-Object System.Random 42
for ($i = 0; $i -lt 18; $i++) {
    $ox = $rand.Next(1, 15); $oy = $rand.Next(0, 9)
    if ($ox -ge 4 -and $ox -le 10 -and $oy -ge 2 -and $oy -le 5) { continue }
    $ref2Overlays += , @($ox, $oy, (82 + ($i % 4)), 0)
}

# ============================================================================
# REFERENCE 3 - light-water island: near-uniform light blue field, small
# two-tier island (bright raised grass over dirt underlay), dark grass with
# flowers, dirt terrace lower-left, cohesive grey rock mass right with foam
# feet at the waterline, sparse shore-foam accents.
# ============================================================================
$ref3Terrain = @(
    '....IIIIII....',
    '..IIIIIIIIII..',
    '.IIIIBBBIIIII.',
    'IIIIBBGGRRIII.',
    '.IIDDGGGRRIII.',
    '.IIDDDGRRFIII.',
    '..IIDDDFIIII..',
    '..IIIIIIIIII..',
    '...IIIIIIII...',
    '....IIIIII....'
)
$ref3Props = @(
    '..............',
    '..............',
    '..............',
    '........hh....',
    '.....ff.mm....',
    '......fmm.....',
    '..............',
    '..............',
    '..............',
    '..............'
)
$ref3TerrainMap = @{
    'I' = @{ Tiles = @(104); Off = 0 }
    'B' = @{ Tiles = @(22, 23); Off = -16; Under = 3 }
    'G' = @{ Tiles = @(40);     Off = -4 }
    'D' = @{ Tiles = @(0, 2, 10); Off = 0 }
    'R' = @{ Tiles = @(61);         Off = -6 }   # cobble base of the rock wall
    'F' = @{ Tiles = @(73, 80);     Off = 0 }    # foam rocks at waterline
}
$ref3PropMap = @{
    'f' = @{ Tiles = @(41, 46, 47); Off = -8 }
    'h' = @{ Tiles = @(64);         Off = -22 }  # tall pinnacles at the back
    'm' = @{ Tiles = @(65, 68);     Off = -10 }  # boulders stacked on the wall
}
$ref3Overlays = @()

$paths = @()
$paths += Render-Board -name 'reference-1-meadow-pond' -terrain $ref1Terrain -terrainMap $ref1TerrainMap -props $ref1Props -propMap $ref1PropMap -overlays @() -seed 11
$paths += Render-Board -name 'reference-2-island-ring' -terrain $ref2Terrain -terrainMap $ref2TerrainMap -props $ref2Props -propMap $ref2PropMap -overlays $ref2Overlays -seed 23
$paths += Render-Board -name 'reference-3-ice-rock-island' -terrain $ref3Terrain -terrainMap $ref3TerrainMap -props $ref3Props -propMap $ref3PropMap -overlays $ref3Overlays -seed 37

# composite sheet
$panelW = 1280; $panelH = 1024
$sheet = New-Object System.Drawing.Bitmap (3 * $panelW), $panelH
$gs = [System.Drawing.Graphics]::FromImage($sheet)
$gs.Clear([System.Drawing.Color]::FromArgb(255, 0, 0, 0))
for ($i = 0; $i -lt $paths.Count; $i++) {
    $img = [System.Drawing.Image]::FromFile($paths[$i])
    $gs.DrawImage($img, (New-Object System.Drawing.Rectangle ($i * $panelW), 0, $panelW, $panelH))
    $img.Dispose()
}
$sheetPath = Join-Path $root 'reference-recreations-sheet-v2.png'
$sheet.Save($sheetPath, [System.Drawing.Imaging.ImageFormat]::Png)
$gs.Dispose(); $sheet.Dispose()

foreach ($kv in $tileCache.GetEnumerator()) { if ($kv.Value) { $kv.Value.Dispose() } }
Write-Output $sheetPath
