param([int]$Seed = 1337)
# Headless mirror of IsoTerrainSampler.SampleContinent — same math, same default
# config values — to verify the ported logic before an in-editor run is possible.
$ErrorActionPreference = 'Stop'

# --- config defaults (mirrors FoundationConfig.cs) ---
$climateFrequency = 0.02
$continentFrequency = 0.012
$deepShore = 0.42      # continentShoreLevel
$beachLevel = 0.46
$tier2 = 0.62; $tier3 = 0.74; $tier4 = 0.85
$spawnBias = 0.34; $spawnRadius = 48.0
$spawnClearingRadius = 6; $spawnHeight = 1; $maxHeight = 4
$riverFreq = 0.025; $riverHalf = 0.014; $riverBank = 0.012
$riverWarpAmp = 22.0; $riverWarpFreq = 0.02; $riverMaxElev = 0.72

$seedHash = [double]((([uint64]$Seed * 2654435761) + 2654435769) % 4294967296) # offset variation only

function Perlin([double]$x, [double]$y, [double]$freq, [int]$sx, [int]$sy) {
    $ox = ($seedHash % 9973) + $sx * 131.7
    $oy = (($seedHash / 9973) % 9973) + $sy * 71.3
    # Unity Mathf.PerlinNoise approximation via layered sine (monotone-ish, 0..1).
    # Good enough to validate band/threshold STRUCTURE, not pixel-identical to Unity.
    $vx = ($x + $ox) * $freq; $vy = ($y + $oy) * $freq
    $n = [Math]::Sin($vx * 1.7) * [Math]::Cos($vy * 1.3) + [Math]::Sin(($vx + $vy) * 0.9) * 0.6 + [Math]::Cos(($vx - $vy) * 1.1) * 0.5
    return ($n / 2.2 + 1) / 2  # -> ~0..1
}

$W = 120; $H = 120; $half = 60
$counts = @{ deep = 0; beach = 0; river = 0; land = 0 }
$biomeByHeight = @{}
$maxSeen = 0
$spawnBad = 0
$heights = @{}

for ($gy = -$half; $gy -lt $half; $gy++) {
    for ($gx = -$half; $gx -lt $half; $gx++) {
        $clearing = [Math]::Max([Math]::Abs($gx), [Math]::Abs($gy))
        if ($clearing -le $spawnClearingRadius) {
            # spawn clearing must be dry, flat, walkable
            continue
        }
        $eBase = Perlin $gx $gy $continentFrequency 11 12
        $eDetail = Perlin $gx $gy ($continentFrequency * 3) 13 14
        $e = $eBase * 0.7 + $eDetail * 0.3
        $bias = [Math]::Max(0.0, [Math]::Min(1.0, 1 - $clearing / $spawnRadius)) * $spawnBias
        $e += $bias

        if ($e -lt $deepShore) { $counts.deep++; continue }

        $warpX = $gx + (Perlin $gx $gy $riverWarpFreq 51 52) * 2 * $riverWarpAmp - $riverWarpAmp
        $warpY = $gy + (Perlin $gx $gy $riverWarpFreq 53 54) * 2 * $riverWarpAmp - $riverWarpAmp
        $band = Perlin $warpX $warpY $riverFreq 55 56
        $rd = [Math]::Abs($band - 0.5)
        $belowRidge = $e -lt $riverMaxElev
        if ($belowRidge -and $rd -lt $riverHalf) { $counts.river++; continue }

        if ($e -lt $beachLevel -or ($belowRidge -and $rd -lt ($riverHalf + $riverBank))) { $counts.beach++; continue }

        $counts.land++
        $h = 1
        if ($e -gt $tier2) { $h = 2 }
        if ($e -gt $tier3) { $h = 3 }
        if ($e -gt $tier4) { $h = 4 }
        $h = [Math]::Min($h, [Math]::Min($maxHeight, 7))
        if ($h -gt $maxSeen) { $maxSeen = $h }
        if (-not $heights.ContainsKey($h)) { $heights[$h] = 0 }
        $heights[$h]++
    }
}

# spawn-apron check: every cell within spawnRadius must be land (never ocean) thanks to bias
$apronOcean = 0
for ($gy = -40; $gy -le 40; $gy++) {
  for ($gx = -40; $gx -le 40; $gx++) {
    $clearing = [Math]::Max([Math]::Abs($gx), [Math]::Abs($gy))
    if ($clearing -le $spawnClearingRadius -or $clearing -gt 20) { continue }
    $eBase = Perlin $gx $gy $continentFrequency 11 12
    $eDetail = Perlin $gx $gy ($continentFrequency * 3) 13 14
    $e = $eBase * 0.7 + $eDetail * 0.3
    $bias = [Math]::Max(0.0, [Math]::Min(1.0, 1 - $clearing / $spawnRadius)) * $spawnBias
    $e += $bias
    if ($e -lt $deepShore) { $apronOcean++ }
  }
}

$total = $counts.deep + $counts.beach + $counts.river + $counts.land
"Seed $Seed  sampled $total non-clearing cells"
"  deep/shallow ocean : {0,6}  ({1:p1})" -f $counts.deep, ($counts.deep / $total)
"  beach + banks      : {0,6}  ({1:p1})" -f $counts.beach, ($counts.beach / $total)
"  rivers             : {0,6}  ({1:p1})" -f $counts.river, ($counts.river / $total)
"  land               : {0,6}  ({1:p1})" -f $counts.land, ($counts.land / $total)
"  land height tiers  : " + (($heights.GetEnumerator() | Sort-Object Name | ForEach-Object { "h$($_.Name)=$($_.Value)" }) -join '  ')
"  max height tier    : $maxSeen  (ceiling = $([Math]::Min($maxHeight,7)))"
"  spawn apron ocean cells (want 0): $apronOcean"
"INVARIANTS: " + (@(
  ($(if ($maxSeen -le 7) {'height<=7 OK'} else {'HEIGHT>7 FAIL'})),
  ($(if ($apronOcean -eq 0) {'spawn-apron-land OK'} else {'SPAWN OCEAN FAIL'})),
  ($(if ($counts.deep -gt 0) {'ocean-present OK'} else {'NO OCEAN FAIL'})),
  ($(if ($counts.river -gt 0) {'rivers-present OK'} else {'NO RIVERS'})),
  ($(if ($counts.land -gt 0) {'land-present OK'} else {'NO LAND FAIL'}))
) -join ' | ')
