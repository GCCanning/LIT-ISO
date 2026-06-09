param(
    [string]$AssetForgeUrl = "http://127.0.0.1:4182",
    [string]$LoraName = "litiso_sprixen_frame_v1.safetensors",
    [string]$CheckpointName = "DreamShaper_8_pruned.safetensors",
    [double]$LoraStrength = 0.65,
    [int]$Steps = 28,
    [double]$Cfg = 6.5,
    [string]$SamplerName = "euler",
    [string]$Scheduler = "normal",
    [string]$OutputDir = "C:\Projects\Unity-Projects\LIT-ISO\TempEvalStrict",
    [int]$FrameSize = 64,
    [int]$MaxRawForegroundBounds = 180,
    [int]$MaxCleanPaletteEstimate = 128,
    [int]$PerRequestTimeoutSec = 600,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function Save-DataUrl {
    param(
        [Parameter(Mandatory = $true)][string]$DataUrl,
        [Parameter(Mandatory = $true)][string]$Path
    )
    if ($DataUrl -notmatch '^data:image/[^;]+;base64,') {
        throw "Response did not contain an image data URL."
    }
    $base64 = $DataUrl -replace '^data:image/[^;]+;base64,', ''
    $bytes = [Convert]::FromBase64String($base64)
    if ($bytes.Length -lt 1024) {
        throw "Generated image payload is too small: $($bytes.Length) bytes."
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $Path) -Force | Out-Null
    [IO.File]::WriteAllBytes($Path, $bytes)
}

function Get-ColorDistance {
    param($A, $B)
    $dr = [int]$A.R - [int]$B.R
    $dg = [int]$A.G - [int]$B.G
    $db = [int]$A.B - [int]$B.B
    return [Math]::Sqrt(($dr * $dr) + ($dg * $dg) + ($db * $db))
}

function Get-BackgroundColor {
    param([System.Drawing.Bitmap]$Bitmap)
    $samples = @(
        $Bitmap.GetPixel(0, 0),
        $Bitmap.GetPixel(($Bitmap.Width - 1), 0),
        $Bitmap.GetPixel(0, ($Bitmap.Height - 1)),
        $Bitmap.GetPixel(($Bitmap.Width - 1), ($Bitmap.Height - 1))
    )
    $r = [int](($samples | ForEach-Object { $_.R } | Measure-Object -Average).Average)
    $g = [int](($samples | ForEach-Object { $_.G } | Measure-Object -Average).Average)
    $b = [int](($samples | ForEach-Object { $_.B } | Measure-Object -Average).Average)
    return [System.Drawing.Color]::FromArgb(255, $r, $g, $b)
}

function Test-ForegroundPixel {
    param($Color, $BackgroundColor, [double]$Tolerance = 24)
    if ($Color.A -le 12) { return $false }
    if ($Color.G -gt 180 -and $Color.R -lt 90 -and $Color.B -lt 120) { return $false }
    return (Get-ColorDistance $Color $BackgroundColor) -gt $Tolerance
}

function Get-ForegroundBounds {
    param([System.Drawing.Bitmap]$Bitmap, $BackgroundColor)
    $minX = $Bitmap.Width
    $minY = $Bitmap.Height
    $maxX = -1
    $maxY = -1
    for ($y = 0; $y -lt $Bitmap.Height; $y++) {
        for ($x = 0; $x -lt $Bitmap.Width; $x++) {
            if (Test-ForegroundPixel $Bitmap.GetPixel($x, $y) $BackgroundColor) {
                if ($x -lt $minX) { $minX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    $width = if ($maxX -ge $minX) { $maxX - $minX + 1 } else { 0 }
    $height = if ($maxY -ge $minY) { $maxY - $minY + 1 } else { 0 }
    return [ordered]@{ x = if ($width) { $minX } else { 0 }; y = if ($height) { $minY } else { 0 }; width = $width; height = $height }
}

function Get-PaletteEstimate {
    param([System.Drawing.Bitmap]$Bitmap)
    $palette = New-Object "System.Collections.Generic.HashSet[string]"
    for ($y = 0; $y -lt $Bitmap.Height; $y++) {
        for ($x = 0; $x -lt $Bitmap.Width; $x++) {
            $c = $Bitmap.GetPixel($x, $y)
            if ($c.A -gt 12) {
                $null = $palette.Add("$($c.R),$($c.G),$($c.B),$($c.A)")
            }
        }
    }
    return $palette.Count
}

function Get-CornerAlpha {
    param([System.Drawing.Bitmap]$Bitmap)
    $points = @(
        @(0, 0),
        @(($Bitmap.Width - 1), 0),
        @(0, ($Bitmap.Height - 1)),
        @(($Bitmap.Width - 1), ($Bitmap.Height - 1))
    )
    $alpha = 0
    foreach ($point in $points) {
        $alpha = [Math]::Max($alpha, $Bitmap.GetPixel($point[0], $point[1]).A)
    }
    return $alpha
}

function Snap-Channel {
    param([int]$Value)
    return [Math]::Max(0, [Math]::Min(255, [int]([Math]::Round($Value / 32) * 32)))
}

function Convert-ToStrictSpriteFrame {
    param(
        [string]$RawPath,
        [string]$CleanPath,
        [int]$Size
    )
    $raw = [System.Drawing.Bitmap]::new($RawPath)
    try {
        $bg = Get-BackgroundColor $raw
        $bounds = Get-ForegroundBounds $raw $bg
        if ($bounds.width -le 0 -or $bounds.height -le 0) {
            throw "No foreground found in raw output."
        }

        $canvas = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($canvas)
        try {
            $graphics.Clear([System.Drawing.Color]::FromArgb(0, 0, 0, 0))
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

            $maxDraw = $Size - 8
            $scale = [Math]::Min($maxDraw / [double]$bounds.width, $maxDraw / [double]$bounds.height)
            $drawW = [Math]::Max(1, [int][Math]::Round($bounds.width * $scale))
            $drawH = [Math]::Max(1, [int][Math]::Round($bounds.height * $scale))
            $drawX = [int][Math]::Round(($Size - $drawW) / 2)
            $drawY = $Size - $drawH - 4

            $crop = [System.Drawing.Rectangle]::new($bounds.x, $bounds.y, $bounds.width, $bounds.height)
            $dest = [System.Drawing.Rectangle]::new($drawX, $drawY, $drawW, $drawH)
            $graphics.DrawImage($raw, $dest, $crop, [System.Drawing.GraphicsUnit]::Pixel)
        } finally {
            $graphics.Dispose()
        }

        for ($y = 0; $y -lt $canvas.Height; $y++) {
            for ($x = 0; $x -lt $canvas.Width; $x++) {
                $c = $canvas.GetPixel($x, $y)
                if ($c.A -le 12 -or (Get-ColorDistance $c $bg) -le 24) {
                    $canvas.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
                } else {
                    $canvas.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, (Snap-Channel $c.R), (Snap-Channel $c.G), (Snap-Channel $c.B)))
                }
            }
        }

        New-Item -ItemType Directory -Path (Split-Path -Parent $CleanPath) -Force | Out-Null
        $canvas.Save($CleanPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $canvas.Dispose()

        return [ordered]@{ background = "$($bg.R),$($bg.G),$($bg.B)"; rawBounds = $bounds }
    } finally {
        $raw.Dispose()
    }
}

function Get-StrictQa {
    param(
        [string]$RawPath,
        [string]$CleanPath,
        [object]$Cleanup
    )
    $clean = [System.Drawing.Bitmap]::new($CleanPath)
    try {
        $transparentBg = [System.Drawing.Color]::FromArgb(0, 0, 0, 0)
        $cleanBounds = Get-ForegroundBounds $clean $transparentBg
        $cleanPalette = Get-PaletteEstimate $clean
        $cleanCornerAlpha = Get-CornerAlpha $clean
        $issues = @()

        if ($Cleanup.rawBounds.width -gt $MaxRawForegroundBounds -or $Cleanup.rawBounds.height -gt $MaxRawForegroundBounds) {
            $issues += "raw_foreground_too_large_for_sprite_source"
        }
        if ($cleanCornerAlpha -gt 8) {
            $issues += "clean_frame_has_opaque_corners"
        }
        if ($cleanBounds.width -le 0 -or $cleanBounds.height -le 0) {
            $issues += "clean_frame_has_no_foreground"
        }
        if ($cleanBounds.width -gt ($FrameSize - 2) -or $cleanBounds.height -gt ($FrameSize - 2)) {
            $issues += "clean_foreground_too_large"
        }
        if ($cleanPalette -gt $MaxCleanPaletteEstimate) {
            $issues += "clean_palette_too_large"
        }

        return [ordered]@{
            qaStatus = if ($issues.Count -eq 0) { "pass" } else { "fail" }
            issues = $issues
            rawForegroundBounds = $Cleanup.rawBounds
            cleanForegroundBounds = $cleanBounds
            cleanPaletteEstimate = $cleanPalette
            cleanMaxCornerAlpha = $cleanCornerAlpha
        }
    } finally {
        $clean.Dispose()
    }
}

$negativePrompt = @(
    "scene", "background", "floor", "platform", "floor aura", "screenshot",
    "UI", "interface", "text", "logo", "watermark", "second character",
    "multiple characters", "duplicate body", "cropped feet", "cut off",
    "portrait", "closeup", "photograph", "photorealistic", "realistic",
    "cinematic", "street", "buildings", "room", "landscape", "human photo",
    "smooth skin", "fabric photo", "anti-aliasing", "blur", "muddy pixels",
    "soft painterly render", "3D render", "checkerboard background",
    "large character illustration", "concept art", "full page character art"
) -join ", "

$cases = @(
    @{ name = "male_adventurer_idle_south"; seed = 41001; action = "idle"; direction = "S"; prompt = "litiso_sprixen, FreePixel character sprite frame, fp_category adventurers, fp_character male adventurer, fp_action idle, fp_direction south, one tiny 64x64 RPG pixel sprite only, isolated full body, facing south, bottom-center feet anchor, transparent or flat chroma background, no floor, no shadow, no illustration, no concept art, no scene, hard pixel edges, limited palette" },
    @{ name = "male_adventurer_walk_east"; seed = 41002; action = "walk"; direction = "E"; prompt = "litiso_sprixen, FreePixel character sprite frame, fp_category adventurers, fp_character male adventurer, fp_action walk, fp_direction east, one tiny 64x64 RPG pixel sprite only, isolated full body walk keyframe, facing east, bottom-center feet anchor, transparent or flat chroma background, no floor, no shadow, no illustration, no concept art, no scene, hard pixel edges, limited palette" },
    @{ name = "female_adventurer_idle_north"; seed = 41003; action = "idle"; direction = "N"; prompt = "litiso_sprixen, FreePixel character sprite frame, fp_category adventurers, fp_character female adventurer, fp_action idle, fp_direction north, one tiny 64x64 RPG pixel sprite only, isolated full body, back view facing north, bottom-center feet anchor, transparent or flat chroma background, no floor, no shadow, no illustration, no concept art, no scene, hard pixel edges, limited palette" },
    @{ name = "common_slime_idle_south"; seed = 41004; action = "idle"; direction = "S"; prompt = "litiso_sprixen, FreePixel creature sprite frame, fp_category enemies, fp_character common slime, fp_action idle, fp_direction south, one tiny 64x64 RPG pixel slime only, isolated full body, facing south, bottom-center anchor, transparent or flat chroma background, no floor, no shadow, no illustration, no concept art, no scene, hard pixel edges, limited palette" }
)

$runId = Get-Date -Format "yyyyMMdd_HHmmss"
$target = Join-Path $OutputDir "$runId`_$($LoraName -replace '\.safetensors$','')_strict"
$rawDir = Join-Path $target "raw"
$cleanDir = Join-Path $target "clean"
New-Item -ItemType Directory -Path $rawDir, $cleanDir -Force | Out-Null

$manifest = [ordered]@{
    status = "running"
    mode = "strict_asset_forge_sprite_frame_eval"
    assetForgeUrl = $AssetForgeUrl
    lora = [ordered]@{ name = $LoraName; checkpoint = $CheckpointName; strength = $LoraStrength; steps = $Steps; cfg = $Cfg; sampler = $SamplerName; scheduler = $Scheduler }
    frameSize = $FrameSize
    negativePrompt = $negativePrompt
    startedAt = (Get-Date).ToString("o")
    acceptedFrames = @()
    rejectedFrames = @()
    evaluations = @()
}

foreach ($case in $cases) {
    Write-Host "Strict eval $($case.name) with $LoraName..."
    $rawPath = Join-Path $rawDir "$($case.name).png"
    $cleanPath = Join-Path $cleanDir "$($case.name)_64.png"

    if ($DryRun) {
        $manifest.evaluations += [ordered]@{ id = $case.name; status = "dry-run"; prompt = $case.prompt; seed = $case.seed }
        continue
    }

    try {
        $body = @{
            prompt = $case.prompt
            negativePrompt = $negativePrompt
            loraName = $LoraName
            loraStrength = $LoraStrength
            checkpointName = $CheckpointName
            steps = $Steps
            cfg = $Cfg
            samplerName = $SamplerName
            scheduler = $Scheduler
            seed = $case.seed
        } | ConvertTo-Json -Depth 5

        $response = Invoke-RestMethod -Uri "$AssetForgeUrl/api/providers/comfy/lora-frame" -Method Post -ContentType "application/json" -Body $body -TimeoutSec $PerRequestTimeoutSec
        Save-DataUrl -DataUrl $response.image -Path $rawPath
        $cleanup = Convert-ToStrictSpriteFrame -RawPath $rawPath -CleanPath $cleanPath -Size $FrameSize
        $qa = Get-StrictQa -RawPath $rawPath -CleanPath $cleanPath -Cleanup $cleanup
        $record = [ordered]@{
            id = $case.name
            action = $case.action
            direction = $case.direction
            seed = $case.seed
            prompt = $case.prompt
            raw = $rawPath
            clean = $cleanPath
            qaStatus = $qa.qaStatus
            issues = $qa.issues
            qa = $qa
            provider = $response.provider
            lora = $response.lora
            checkpoint = $response.checkpoint
        }
        $manifest.evaluations += $record
        if ($qa.qaStatus -eq "pass") {
            $manifest.acceptedFrames += $record
        } else {
            $manifest.rejectedFrames += $record
        }
    } catch {
        $record = [ordered]@{ id = $case.name; status = "failed"; seed = $case.seed; prompt = $case.prompt; error = $_.Exception.Message }
        $manifest.evaluations += $record
        $manifest.rejectedFrames += $record
        Write-Warning "$($case.name) failed: $($_.Exception.Message)"
    }

    $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $target "manifest.json") -Encoding UTF8
}

$manifest.status = if ($DryRun) { "dry_run" } elseif ($manifest.acceptedFrames.Count -gt 0 -and $manifest.rejectedFrames.Count -eq 0) { "promotion_ready" } else { "blocked" }
$manifest.completedAt = (Get-Date).ToString("o")
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $target "manifest.json") -Encoding UTF8
Write-Host "Strict Sprixen eval complete: accepted=$($manifest.acceptedFrames.Count) rejected=$($manifest.rejectedFrames.Count) manifest=$(Join-Path $target 'manifest.json')"
if (!$DryRun -and $manifest.status -ne "promotion_ready") {
    exit 2
}
