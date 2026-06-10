param([int]$WorldSeed = 1207)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Join-Path $env:TEMP 'litiso-archive-review'
$tileRoot = Join-Path $root 'tileset\isometric tileset\separated images'

$tileCache = @{}
function Get-TileImage([int]$n) {
    if (-not $tileCache.ContainsKey($n)) {
        $tileCache[$n] = [System.Drawing.Image]::FromFile((Join-Path $tileRoot ('tile_{0:d3}.png' -f $n)))
    }
    return $tileCache[$n]
}

# deterministic per-cell hash in [0,1)
function Hash01([int]$x, [int]$y, [int]$salt) {
    $h = ([long]$x * 374761393 + [long]$y * 668265263 + [long]($WorldSeed + $salt) * 1274126177) % 2147483647
    $h = ($h -bxor ($h -shr 13))
    $h = ($h * 1103515245) % 2147483647
    $h = ($h -bxor ($h -shr 7))
    return [double]([Math]::Abs($h % 100000)) / 100000.0
}

# smooth value-noise field as a 2D array (lattice + bilinear, no per-sample calls)
function New-NoiseField([int]$W, [int]$H, [double]$period, [int]$seed) {
    $lw = [int][Math]::Ceiling($W / $period) + 2
    $lh = [int][Math]::Ceiling($H / $period) + 2
    $rnd = New-Object System.Random ($WorldSeed * 7919 + $seed)
    $lat = New-Object 'double[,]' $lw, $lh
    for ($j = 0; $j -lt $lh; $j++) { for ($i = 0; $i -lt $lw; $i++) { $lat[$i, $j] = $rnd.NextDouble() } }
    $f = New-Object 'double[,]' $W, $H
    for ($y = 0; $y -lt $H; $y++) {
        $gy = $y / $period; $iy = [int][Math]::Floor($gy); $fy = $gy - $iy; $fy = $fy * $fy * (3 - 2 * $fy)
        for ($x = 0; $x -lt $W; $x++) {
            $gx = $x / $period; $ix = [int][Math]::Floor($gx); $fx = $gx - $ix; $fx = $fx * $fx * (3 - 2 * $fx)
            $a = $lat[$ix, $iy]; $b = $lat[($ix + 1), $iy]; $c = $lat[$ix, ($iy + 1)]; $d = $lat[($ix + 1), ($iy + 1)]
            $f[$x, $y] = ($a * (1 - $fx) + $b * $fx) * (1 - $fy) + ($c * (1 - $fx) + $d * $fx) * $fy
        }
    }
    return , $f
}

# ---------------------------------------------------------------- world fields
$W = 56; $H = 42

$n1 = New-NoiseField $W $H 14.0 101
$n2 = New-NoiseField $W $H 6.0  202
$m1 = New-NoiseField $W $H 18.0 303
$m2 = New-NoiseField $W $H 8.0  404
$flowerN = New-NoiseField $W $H 4.0 505
$bushN   = New-NoiseField $W $H 3.5 606
$rockN   = New-NoiseField $W $H 4.5 707
$lushN   = New-NoiseField $W $H 5.0 808

$elev = New-Object 'double[,]' $W, $H
$moist = New-Object 'double[,]' $W, $H
for ($y = 0; $y -lt $H; $y++) {
    for ($x = 0; $x -lt $W; $x++) {
        $nx = 2.0 * $x / ($W - 1) - 1.0
        $ny = 2.0 * $y / ($H - 1) - 1.0
        $d = [Math]::Sqrt($nx * $nx + $ny * $ny) / 1.4142
        $edge = [Math]::Max([Math]::Abs($nx), [Math]::Abs($ny))
        $e = ($n1[$x, $y] * 0.62 + $n2[$x, $y] * 0.38) * 0.72 + (1.0 - $d) * 0.52 - 0.20
        if ($edge -gt 0.72) { $e -= ($edge - 0.72) * 1.6 }   # guarantee ocean at the world border
        $elev[$x, $y] = $e
        $moist[$x, $y] = $m1[$x, $y] * 0.75 + $m2[$x, $y] * 0.25
    }
}

# classify: deep / shallow / beach / meadow / forest / badlands (+ lvl, canopy)
$kind = New-Object 'string[,]' $W, $H
$lvl = New-Object 'int[,]' $W, $H
for ($y = 0; $y -lt $H; $y++) {
    for ($x = 0; $x -lt $W; $x++) {
        $e = $elev[$x, $y]; $m = $moist[$x, $y]
        $e = $e + ((Hash01 $x $y 9) - 0.5) * 0.02   # break up straight band edges
        if ($e -lt 0.30) { $kind[$x, $y] = 'deep' }
        elseif ($e -lt 0.40) { $kind[$x, $y] = 'shallow' }
        elseif ($e -lt 0.44) { $kind[$x, $y] = 'beach' }
        else {
            if ($m -lt 0.30) { $kind[$x, $y] = 'badlands' }
            elseif ($m -lt 0.62) { $kind[$x, $y] = 'meadow' }
            else { $kind[$x, $y] = 'forest' }
            if ($e -gt 0.72) { $lvl[$x, $y] = 1 }
            if ($e -gt 0.84) { $lvl[$x, $y] = 2 }
        }
    }
}

function KindAt([int]$x, [int]$y) {
    if ($x -lt 0 -or $x -ge $W -or $y -lt 0 -or $y -ge $H) { return 'deep' }
    return $kind[$x, $y]
}

# cleanup: drown beach spits that have no land biome in their 8-neighbourhood
for ($pass = 0; $pass -lt 2; $pass++) {
    for ($y = 0; $y -lt $H; $y++) {
        for ($x = 0; $x -lt $W; $x++) {
            if ($kind[$x, $y] -ne 'beach') { continue }
            $hasLand = $false
            for ($dy = -1; $dy -le 1; $dy++) {
                for ($dx = -1; $dx -le 1; $dx++) {
                    if ((KindAt ($x + $dx) ($y + $dy)) -in @('meadow', 'forest', 'badlands')) { $hasLand = $true }
                }
            }
            if (-not $hasLand) { $kind[$x, $y] = 'shallow' }
        }
    }
}

# cleanup: erode terrace fragments per level (fewer than 2 equal-or-higher
# 4-neighbours -> demote one step); top level first
foreach ($level in @(2, 1)) {
    for ($y = 0; $y -lt $H; $y++) {
        for ($x = 0; $x -lt $W; $x++) {
            if ($lvl[$x, $y] -ne $level) { continue }
            $n = 0
            foreach ($nb in @(@(1, 0), @(-1, 0), @(0, 1), @(0, -1))) {
                $tx = $x + $nb[0]; $ty = $y + $nb[1]
                if ($tx -ge 0 -and $tx -lt $W -and $ty -ge 0 -and $ty -lt $H -and $lvl[$tx, $ty] -ge $level) { $n++ }
            }
            if ($n -lt 2) { $lvl[$x, $y] = $level - 1 }
        }
    }
}

# ---------------------------------------------------------------------- rivers
# two sources on high ground, descend to the sea, carve shallow water + dirt banks
$sources = @()
$best = @{}
for ($y = 2; $y -lt ($H - 2); $y++) {
    for ($x = 2; $x -lt ($W - 2); $x++) {
        if ($lvl[$x, $y] -ge 1) { $best["$x,$y"] = $elev[$x, $y] }
    }
}
if ($best.Count -eq 0) {
    # flat world fallback: source rivers from the highest land cells
    for ($y = 2; $y -lt ($H - 2); $y++) {
        for ($x = 2; $x -lt ($W - 2); $x++) {
            if ($kind[$x, $y] -in @('meadow', 'forest', 'badlands')) { $best["$x,$y"] = $elev[$x, $y] }
        }
    }
}
$sorted = $best.GetEnumerator() | Sort-Object Value -Descending
foreach ($entry in $sorted) {
    $p = $entry.Key -split ','
    $px = [int]$p[0]; $py = [int]$p[1]
    $far = $true
    foreach ($s in $sources) {
        if (([Math]::Abs($s[0] - $px) + [Math]::Abs($s[1] - $py)) -lt 16) { $far = $false; break }
    }
    if ($far) { $sources += , @($px, $py) }
    if ($sources.Count -ge 2) { break }
}

$riverCells = @{}
foreach ($s in $sources) {
    $cx = $s[0]; $cy = $s[1]
    for ($step = 0; $step -lt 300; $step++) {
        $k = $kind[$cx, $cy]
        if ($k -eq 'deep' -or $k -eq 'shallow') { break }
        $riverCells["$cx,$cy"] = $true
        $bestE = [double]::MaxValue; $bx = $cx; $by = $cy
        foreach ($nb in @(@(1, 0), @(-1, 0), @(0, 1), @(0, -1))) {
            $tx = $cx + $nb[0]; $ty = $cy + $nb[1]
            if ($tx -lt 0 -or $tx -ge $W -or $ty -lt 0 -or $ty -ge $H) { continue }
            if ($riverCells.ContainsKey("$tx,$ty")) { continue }
            $te = $elev[$tx, $ty] + (Hash01 $tx $ty 31) * 0.035   # meander
            if ($te -lt $bestE) { $bestE = $te; $bx = $tx; $by = $ty }
        }
        if ($bx -eq $cx -and $by -eq $cy) {
            # stuck in a basin: end the river in a small carved pond
            for ($dy = -1; $dy -le 1; $dy++) {
                for ($dx = -1; $dx -le 1; $dx++) {
                    $tx = $cx + $dx; $ty = $cy + $dy
                    if ($tx -lt 1 -or $tx -ge ($W - 1) -or $ty -lt 1 -or $ty -ge ($H - 1)) { continue }
                    if ($kind[$tx, $ty] -in @('meadow', 'forest', 'badlands', 'beach')) {
                        $riverCells["$tx,$ty"] = $true
                    }
                }
            }
            break
        }
        $cx = $bx; $cy = $by
    }
}
foreach ($rc in $riverCells.Keys) {
    $p = $rc -split ','
    $kind[[int]$p[0], [int]$p[1]] = 'river'
    $lvl[[int]$p[0], [int]$p[1]] = 0
}
# river banks: grass meets the stream directly (like the pond reference); only
# drop raised cells beside the river so the stream is not walled in by cliffs
for ($y = 0; $y -lt $H; $y++) {
    for ($x = 0; $x -lt $W; $x++) {
        if ($lvl[$x, $y] -eq 1) {
            foreach ($nb in @(@(1, 0), @(-1, 0), @(0, 1), @(0, -1))) {
                if ((KindAt ($x + $nb[0]) ($y + $nb[1])) -eq 'river') { $lvl[$x, $y] = 0; break }
            }
        }
    }
}

# canopy: interior forest cells, clumped by moisture, never on the waterline
$canopy = New-Object 'bool[,]' $W, $H
for ($y = 0; $y -lt $H; $y++) {
    for ($x = 0; $x -lt $W; $x++) {
        if ($kind[$x, $y] -ne 'forest' -or $lvl[$x, $y] -ne 0) { continue }
        if ($moist[$x, $y] -lt 0.68 -and $bushN[$x, $y] -lt 0.62) { continue }
        $ok = $true
        foreach ($nb in @(@(1, 0), @(-1, 0), @(0, 1), @(0, -1))) {
            $nk = KindAt ($x + $nb[0]) ($y + $nb[1])
            if ($nk -in @('deep', 'shallow', 'river', 'beach')) { $ok = $false; break }
        }
        if ($ok) { $canopy[$x, $y] = $true }
    }
}

# ---------------------------------------------------------------------- render
$S = 2
$TILE = 32 * $S
$STEPX = 16 * $S
$STEPY = 8 * $S
$RAISE = 16          # one block step (8 native px) at S=2
$PROP = -5           # prop lift onto the tile top

$canvasW = ($W + $H - 2) * $STEPX + $TILE + 64
$canvasH = ($W + $H - 2) * $STEPY + $TILE + 96
$bmp = New-Object System.Drawing.Bitmap $canvasW, $canvasH
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(255, 0, 0, 0))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$originX = [int]($canvasW / 2)
$originY = [int](($canvasH - ($W + $H - 2) * $STEPY) / 2)

function Pick([int[]]$arr, [int]$x, [int]$y, [int]$salt) {
    return $arr[[int][Math]::Floor((Hash01 $x $y $salt) * $arr.Length) % $arr.Length]
}

$sparkles = @()

# ---- decision pass: choose tiles for every cell, store, then paint z-aware
$dTerrain = New-Object 'int[,]' $W, $H
$dUnder = New-Object 'int[,]' $W, $H
$dProp = New-Object 'int[,]' $W, $H

for ($y = 0; $y -lt $H; $y++) {
    for ($x = 0; $x -lt $W; $x++) {
        $k = $kind[$x, $y]
        $L = $lvl[$x, $y]
        $terrain = -1
        $under = -1
        $prop = -1

        $nearLand = $false; $nearShallow = $false; $nearBadlands = $false; $nearMeadow = $false; $nearLvl1 = $false; $nearCanopy = $false
        foreach ($nb in @(@(1, 0), @(-1, 0), @(0, 1), @(0, -1))) {
            $tx = $x + $nb[0]; $ty = $y + $nb[1]
            $nk = KindAt $tx $ty
            if ($nk -in @('beach', 'meadow', 'forest', 'badlands')) { $nearLand = $true }
            if ($nk -eq 'shallow' -or $nk -eq 'river') { $nearShallow = $true }
            if ($nk -eq 'badlands') { $nearBadlands = $true }
            if ($nk -eq 'meadow') { $nearMeadow = $true }
            if ($tx -ge 0 -and $tx -lt $W -and $ty -ge 0 -and $ty -lt $H) {
                if ($lvl[$tx, $ty] -gt $L) { $nearLvl1 = $true }
                if ($canopy[$tx, $ty]) { $nearCanopy = $true }
            }
        }

        $r = Hash01 $x $y 11
        switch ($k) {
            'deep' {
                if ($nearShallow -and (Hash01 $x $y 12) -lt 0.15) {
                    $terrain = Pick @(87, 88, 90, 95, 96, 98) $x $y 13   # surf swell on the deep side
                } elseif ($r -lt 0.75) { $terrain = 92 }
                elseif ($r -lt 0.85) { $terrain = 101 }
                else { $terrain = Pick @(93, 94, 102, 103) $x $y 14 }
                if (-not $nearShallow -and (Hash01 $x $y 15) -lt 0.025) {
                    $sparkles += , @($x, $y, (Pick @(82, 83, 84, 85) $x $y 16))
                }
            }
            { $_ -in 'shallow', 'river' } {
                if ($nearLand -and $r -lt 0.25) { $terrain = Pick @(106, 107, 108) $x $y 17 }  # shore wash
                elseif ($r -lt 0.85) { $terrain = 104 }
                else { $terrain = Pick @(105, 109, 110) $x $y 18 }
                if ($nearLand -and (Hash01 $x $y 19) -lt 0.05) {
                    $prop = Pick @(70, 71, 73, 78, 80, 81) $x $y 20      # foam-footed stones at the shoreline
                } elseif ((Hash01 $x $y 21) -lt 0.015) { $terrain = 114 }
            }
            'beach' {
                if ($r -lt 0.60) { $terrain = 0 }
                elseif ($r -lt 0.85) { $terrain = 10 }
                else { $terrain = 21 }
            }
            'meadow' {
                if ($L -ge 1) {
                    $under = 3
                    $terrain = Pick @(22, 23, 24) $x $y 22
                    if ($L -eq 2 -and $rockN[$x, $y] -gt 0.45) {
                        $prop = Pick @(61, 62, 65, 67, 68) $x $y 23      # rocky crown
                    }
                } else {
                    if ($nearBadlands) { $terrain = Pick @(19, 20) $x $y 24 }   # sprout transition
                    elseif ($lushN[$x, $y] -gt 0.74) { $terrain = 40 }          # lush patch
                    elseif ($r -lt 0.90) { $terrain = 37 }
                    else { $terrain = Pick @(38, 39) $x $y 25 }
                    if ($terrain -eq 37) {
                        if ($flowerN[$x, $y] -gt 0.68 -and (Hash01 $x $y 26) -lt 0.6) {
                            $prop = Pick @(41, 42, 44, 46, 47, 43, 45) $x $y 27
                        } elseif ($bushN[$x, $y] -gt 0.80 -and (Hash01 $x $y 28) -lt 0.5) {
                            $prop = Pick @(30, 31, 35) $x $y 29
                        } elseif ($nearLvl1 -and (Hash01 $x $y 30) -lt 0.04) {
                            $prop = Pick @(65, 67) $x $y 31              # cliff-base boulder
                        }
                    }
                }
            }
            'forest' {
                if ($L -ge 1) {
                    $under = 3
                    $terrain = Pick @(22, 23, 24) $x $y 32
                    if ($L -eq 2 -and $rockN[$x, $y] -gt 0.45) {
                        $prop = Pick @(61, 62, 65, 67, 68) $x $y 33
                    }
                } elseif ($canopy[$x, $y]) {
                    $terrain = Pick @(29, 29, 29, 27, 28) $x $y 34       # canopy mass, leafy-dominant
                } else {
                    if ($nearBadlands -or ($nearLand -eq $false)) { $terrain = Pick @(25, 26) $x $y 35 }
                    else { $terrain = 40 }
                    if ($terrain -eq 40) {
                        if ($bushN[$x, $y] -gt 0.50 -and (Hash01 $x $y 36) -lt 0.55) {
                            $prop = Pick @(30, 31, 32, 33, 34, 35, 36) $x $y 37
                        } elseif ($nearMeadow -and (Hash01 $x $y 38) -lt 0.08) {
                            $prop = Pick @(48, 49, 50, 51, 52) $x $y 39  # logs at the forest edge
                        }
                    }
                }
            }
            'badlands' {
                if ($L -ge 1) {
                    $under = 3
                    $terrain = Pick @(14, 15, 16) $x $y 40               # strata mesa top
                    if ($rockN[$x, $y] -gt 0.55) { $prop = Pick @(53, 56, 57) $x $y 41 }
                } else {
                    if ($r -lt 0.60) { $terrain = 17 }
                    elseif ($r -lt 0.85) { $terrain = 18 }
                    else { $terrain = 3 }
                    if ($rockN[$x, $y] -gt 0.68 -and (Hash01 $x $y 42) -lt 0.55) {
                        $prop = Pick @(53, 54, 55, 56, 57, 58, 59, 60) $x $y 43
                    } elseif ((Hash01 $x $y 44) -lt 0.03) {
                        $terrain = Pick @(11, 12, 13) $x $y 45           # rubble block accent
                    }
                }
            }
        }

        $dTerrain[$x, $y] = $terrain
        $dUnder[$x, $y] = $under
        $dProp[$x, $y] = $prop
    }
}

# ---- paint pass: painter order with z-awareness. A raised top (lvl 1, lifted
# by exactly one stepY) occupies the screen band of the next diagonal, so it is
# drawn in group (x+y+1) AFTER that diagonal's flat tiles — southern lowland
# tiles can no longer cover the cliff skirt.
function Draw-TileAt([int]$x, [int]$y, [int]$tileNo, [int]$off) {
    $sx = $originX + (($x - $y) * $STEPX)
    $sy = $originY + (($x + $y) * $STEPY)
    $img = Get-TileImage $tileNo
    $g.DrawImage($img, (New-Object System.Drawing.Rectangle ([int]($sx - $TILE / 2)), ([int]($sy - $TILE / 2 + $off)), $TILE, $TILE))
}

for ($sum = 0; $sum -le ($W + $H); $sum++) {
    for ($z = 0; $z -le 2; $z++) {
        $diag = $sum - $z
        if ($diag -lt 0) { continue }
        for ($y = 0; $y -lt $H; $y++) {
            $x = $diag - $y
            if ($x -lt 0 -or $x -ge $W) { continue }
            $L = $lvl[$x, $y]
            if ($z -lt $L) {
                if ($dUnder[$x, $y] -ge 0) { Draw-TileAt $x $y $dUnder[$x, $y] (-$z * $RAISE) }
            } elseif ($z -eq $L) {
                if ($dTerrain[$x, $y] -ge 0) { Draw-TileAt $x $y $dTerrain[$x, $y] (-$z * $RAISE) }
                if ($dProp[$x, $y] -ge 0) { Draw-TileAt $x $y $dProp[$x, $y] (-$z * $RAISE + $PROP) }
            }
        }
    }
}

foreach ($sp in $sparkles) {
    Draw-TileAt $sp[0] $sp[1] $sp[2] 0
}

$outPath = Join-Path $root 'world-preview.png'
$bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
foreach ($kv in $tileCache.GetEnumerator()) { if ($kv.Value) { $kv.Value.Dispose() } }
Write-Output $outPath
