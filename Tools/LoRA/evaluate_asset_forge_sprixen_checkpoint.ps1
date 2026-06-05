param(
    [string]$AssetForgeUrl = "http://127.0.0.1:4180",
    [string]$LoraName = "litiso_sprixen_frame_v1_step01500.safetensors",
    [string]$CheckpointName = "PixelartSpritesheet_V.1.ckpt",
    [double]$LoraStrength = 0.95,
    [int]$Steps = 22,
    [double]$Cfg = 5.5,
    [string]$SamplerName = "euler",
    [string]$Scheduler = "normal",
    [string]$OutputDir = "C:\Projects\Pixel Pipeline\generated\asset_forge_sprixen_eval",
    [int]$PerRequestTimeoutSec = 600,
    [switch]$UseLatestAvailable,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Save-DataUrl {
    param(
        [Parameter(Mandatory = $true)][string]$DataUrl,
        [Parameter(Mandatory = $true)][string]$Path
    )
    if ($DataUrl -notmatch '^data:image/[^;]+;base64,') {
        throw "Response did not contain a PNG data URL."
    }
    $base64 = $DataUrl -replace '^data:image/[^;]+;base64,', ''
    $bytes = [Convert]::FromBase64String($base64)
    if ($bytes.Length -lt 1024) {
        throw "Generated image payload is too small: $($bytes.Length) bytes."
    }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path)) | Out-Null
    $tmp = "$Path.tmp"
    [IO.File]::WriteAllBytes($tmp, $bytes)
    if ((Get-Item $tmp).Length -lt 1024) {
        Remove-Item -LiteralPath $tmp -Force
        throw "Generated image file is unexpectedly small."
    }
    Move-Item -LiteralPath $tmp -Destination $Path -Force
}

function Get-ImageQa {
    param([Parameter(Mandatory = $true)][string]$Path)

    Add-Type -AssemblyName System.Drawing
    $bitmap = [System.Drawing.Bitmap]::new($Path)
    try {
        $width = $bitmap.Width
        $height = $bitmap.Height
        $cornerPoints = @(
            @(0, 0),
            @($width - 1, 0),
            @(0, $height - 1),
            @($width - 1, $height - 1)
        )
        $cornerAlpha = 0
        $cornerColors = @()
        foreach ($point in $cornerPoints) {
            $color = $bitmap.GetPixel($point[0], $point[1])
            $cornerAlpha = [Math]::Max($cornerAlpha, $color.A)
            $cornerColors += "$($color.R),$($color.G),$($color.B),$($color.A)"
        }

        $minX = $width
        $minY = $height
        $maxX = -1
        $maxY = -1
        $opaquePixels = 0
        $palette = New-Object 'System.Collections.Generic.HashSet[string]'
        $step = [Math]::Max(1, [Math]::Floor($width / 128))
        for ($y = 0; $y -lt $height; $y += $step) {
            for ($x = 0; $x -lt $width; $x += $step) {
                $color = $bitmap.GetPixel($x, $y)
                if ($color.A -gt 16) {
                    $opaquePixels++
                    $null = $palette.Add("$($color.R -band 248),$($color.G -band 248),$($color.B -band 248),$($color.A)")
                    $isChroma = ($color.G -gt 180 -and $color.R -lt 90 -and $color.B -lt 120)
                    if (-not $isChroma) {
                        if ($x -lt $minX) { $minX = $x }
                        if ($y -lt $minY) { $minY = $y }
                        if ($x -gt $maxX) { $maxX = $x }
                        if ($y -gt $maxY) { $maxY = $y }
                    }
                }
            }
        }

        $boundsWidth = if ($maxX -ge $minX) { $maxX - $minX + 1 } else { 0 }
        $boundsHeight = if ($maxY -ge $minY) { $maxY - $minY + 1 } else { 0 }
        $transparentCorners = $cornerAlpha -le 8
        $opaqueSceneRisk = (-not $transparentCorners) -and ($boundsWidth -gt ($width * 0.75) -or $boundsHeight -gt ($height * 0.75))
        $smallSpriteRisk = $boundsWidth -eq 0 -or $boundsHeight -eq 0 -or $boundsHeight -gt 180
        $paletteRisk = $palette.Count -gt 180
        $status = if ($opaqueSceneRisk -or $smallSpriteRisk) { "fail" } elseif ($paletteRisk -or -not $transparentCorners) { "warn" } else { "pass" }

        return [ordered]@{
            status = $status
            width = $width
            height = $height
            transparentCorners = $transparentCorners
            maxCornerAlpha = $cornerAlpha
            cornerColors = $cornerColors
            foregroundBounds = [ordered]@{
                x = if ($boundsWidth) { $minX } else { 0 }
                y = if ($boundsHeight) { $minY } else { 0 }
                width = $boundsWidth
                height = $boundsHeight
            }
            paletteEstimate = $palette.Count
            opaqueSceneRisk = $opaqueSceneRisk
            smallSpriteRisk = $smallSpriteRisk
            notes = "Raw provider QA. Asset Forge browser cleanup performs stricter alpha/crop/snap checks after generation."
        }
    } finally {
        $bitmap.Dispose()
    }
}

function Get-LatestAvailableLora {
    param([string]$AssetForgeUrl)
    try {
        $response = Invoke-RestMethod -Uri "$AssetForgeUrl/api/providers/comfy/loras" -TimeoutSec 10
        $available = @($response.loras | Where-Object { $_.available -eq $true })
        $preferredOrder = @(
            "litiso_sprixen_frame_v1.safetensors",
            "litiso_sprixen_frame_v1_step02250.safetensors",
            "litiso_sprixen_frame_v1_step01500.safetensors",
            "litiso_sprixen_frame_v1_step00750.safetensors",
            "litiso_style_directional_v1.safetensors"
        )
        foreach ($name in $preferredOrder) {
            if ($available | Where-Object { $_.name -eq $name }) {
                return $name
            }
        }
    } catch {
        Write-Warning "Could not query Asset Forge LoRA list: $($_.Exception.Message)"
    }
    return $LoraName
}

$negativePrompt = @(
    "scene", "background", "floor", "platform", "floor aura", "screenshot",
    "UI", "interface", "text", "logo", "watermark", "second character",
    "multiple characters", "duplicate body", "cropped feet", "cut off",
    "portrait", "closeup", "photograph", "photorealistic", "realistic",
    "cinematic", "street", "buildings", "room", "landscape", "human photo",
    "smooth skin", "fabric photo", "anti-aliasing", "blur", "muddy pixels",
    "soft painterly render", "3D render", "checkerboard background"
) -join ", "

$cases = @(
    @{
        name = "male_adventurer_idle_south"
        seed = 31001
        prompt = "litiso_sprixen, litiso_style, FreePixel character sprite frame, fp_category adventurers, fp_character male adventurer, fp_action idle, fp_direction south, single tiny player character only, male adventurer with travel cloak leather tunic boots small belt pouch, idle animation frame, facing south, small 64x64 RPG walk-cycle sprite proportions, Unity-ready isolated sprite frame, centered full body including feet, transparent background or flat solid chroma green background, no environment, no scene, no floor, no platform, no props, hard pixel edges, clean dark outline, limited palette, readable silhouette"
    },
    @{
        name = "male_adventurer_walk_east"
        seed = 31002
        prompt = "litiso_sprixen, litiso_style, FreePixel character sprite frame, fp_category adventurers, fp_character male adventurer, fp_action walk, fp_direction east, single tiny player character only, male adventurer with travel cloak leather tunic boots small belt pouch, walk animation frame, facing east, small 64x64 RPG walk-cycle sprite proportions, Unity-ready isolated sprite frame, centered full body including feet, transparent background or flat solid chroma green background, no environment, no scene, no floor, no platform, no props, hard pixel edges, clean dark outline, limited palette, readable silhouette"
    },
    @{
        name = "female_adventurer_idle_north"
        seed = 31003
        prompt = "litiso_sprixen, litiso_style, FreePixel character sprite frame, fp_category adventurers, fp_character female adventurer, fp_action idle, fp_direction north, single tiny player character only, female adventurer with travel cloak leather armor boots satchel, idle animation frame, facing north, small 64x64 RPG walk-cycle sprite proportions, Unity-ready isolated sprite frame, centered full body including feet, transparent background or flat solid chroma green background, no environment, no scene, no floor, no platform, no props, hard pixel edges, clean dark outline, limited palette, readable silhouette"
    },
    @{
        name = "common_slime_idle_south"
        seed = 31004
        prompt = "litiso_sprixen, litiso_style, FreePixel creature sprite frame, fp_category enemies, fp_character common slime, fp_action idle, fp_direction south, single tiny mob creature only, small round green slime, idle animation frame, facing south, small 64x64 RPG sprite proportions, Unity-ready isolated sprite frame, centered full body, transparent background or flat solid chroma green background, no environment, no scene, no floor, no platform, no props, hard pixel edges, clean dark outline, limited palette, readable silhouette"
    }
)

if ($UseLatestAvailable) {
    $LoraName = Get-LatestAvailableLora -AssetForgeUrl $AssetForgeUrl
    Write-Host "Using latest available LoRA: $LoraName"
}

$runId = Get-Date -Format "yyyyMMdd_HHmmss"
$target = Join-Path $OutputDir "$runId`_$($LoraName -replace '\.safetensors$','')"
[IO.Directory]::CreateDirectory($target) | Out-Null

$manifest = [ordered]@{
    status = "running"
    assetForgeUrl = $AssetForgeUrl
    loraName = $LoraName
    checkpointName = $CheckpointName
    loraStrength = $LoraStrength
    steps = $Steps
    cfg = $Cfg
    samplerName = $SamplerName
    scheduler = $Scheduler
    negativePrompt = $negativePrompt
    startedAt = (Get-Date).ToString("o")
    results = @()
}

foreach ($case in $cases) {
    Write-Host "Generating $($case.name) with $LoraName..."
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
    } | ConvertTo-Json -Depth 4

    if ($DryRun) {
        $manifest.results += [ordered]@{
            name = $case.name
            seed = $case.seed
            prompt = $case.prompt
            status = "dry-run"
        }
        continue
    }

    try {
        $response = Invoke-RestMethod -Uri "$AssetForgeUrl/api/providers/comfy/lora-frame" -Method Post -ContentType "application/json" -Body $body -TimeoutSec $PerRequestTimeoutSec
        $outputPath = Join-Path $target "$($case.name).png"
        Save-DataUrl -DataUrl $response.image -Path $outputPath
        $qa = Get-ImageQa -Path $outputPath
        $manifest.results += [ordered]@{
            name = $case.name
            seed = $case.seed
            prompt = $case.prompt
            output = $outputPath
            provider = $response.provider
            lora = $response.lora
            checkpoint = $response.checkpoint
            status = "generated"
            qa = $qa
        }
    } catch {
        $manifest.results += [ordered]@{
            name = $case.name
            seed = $case.seed
            prompt = $case.prompt
            status = "failed"
            error = $_.Exception.Message
        }
        Write-Warning "$($case.name) failed: $($_.Exception.Message)"
    }

    $manifest | ConvertTo-Json -Depth 8 | Set-Content -Path (Join-Path $target "manifest.json") -Encoding UTF8
}

$manifest.status = "complete"
$manifest.completedAt = (Get-Date).ToString("o")
$manifest | ConvertTo-Json -Depth 8 | Set-Content -Path (Join-Path $target "manifest.json") -Encoding UTF8
Write-Host "Asset Forge Sprixen checkpoint eval written to $target"
