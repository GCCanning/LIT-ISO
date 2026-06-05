param(
    [string]$OutputRoot = "Assets\Generated\Characters\Player\AssetForge\SmokeTestAdventurer",
    [int]$FrameWidth = 256,
    [int]$FrameHeight = 256,
    [int]$FramesPerDirection = 4
)

$ErrorActionPreference = "Stop"

function New-Dir {
    param([Parameter(Mandatory = $true)][string]$Path)
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function New-Sheet {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Action,
        [int]$FrameWidth,
        [int]$FrameHeight,
        [int]$FramesPerDirection
    )

    Add-Type -AssemblyName System.Drawing
    $rows = 8
    $bitmap = [System.Drawing.Bitmap]::new($FrameWidth * $FramesPerDirection, $FrameHeight * $rows, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $directions = @("S", "SE", "E", "NE", "N", "NW", "W", "SW")
    $palette = @(
        [System.Drawing.Color]::FromArgb(255, 80, 114, 68),
        [System.Drawing.Color]::FromArgb(255, 126, 93, 55),
        [System.Drawing.Color]::FromArgb(255, 71, 87, 115),
        [System.Drawing.Color]::FromArgb(255, 132, 76, 72)
    )
    $outline = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 24, 28, 30))
    $skin = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 211, 156, 104))

    for ($row = 0; $row -lt $rows; $row++) {
        for ($col = 0; $col -lt $FramesPerDirection; $col++) {
            $x = $col * $FrameWidth
            $y = $row * $FrameHeight
            $bodyColor = $palette[($row + $col) % $palette.Count]
            $body = [System.Drawing.SolidBrush]::new($bodyColor)
            $bob = if ($Action -eq "walk") { ($col % 2) * 4 } else { 0 }
            $cx = $x + [int]($FrameWidth / 2)
            $footY = $y + 244

            $graphics.FillRectangle($outline, $cx - 18, $footY - 72 - $bob, 36, 54)
            $graphics.FillRectangle($body, $cx - 14, $footY - 68 - $bob, 28, 48)
            $graphics.FillEllipse($outline, $cx - 18, $footY - 104 - $bob, 36, 36)
            $graphics.FillEllipse($skin, $cx - 14, $footY - 100 - $bob, 28, 28)
            $graphics.FillRectangle($outline, $cx - 15, $footY - 18, 10, 18)
            $graphics.FillRectangle($outline, $cx + 5, $footY - 18, 10, 18)
            $graphics.FillRectangle($body, $cx - 28, $footY - 58 - $bob, 12, 34)
            $graphics.FillRectangle($body, $cx + 16, $footY - 58 - $bob, 12, 34)
            $font = [System.Drawing.Font]::new("Arial", 9)
            $graphics.DrawString($directions[$row], $font, $outline, $x + 8, $y + 8)
            $font.Dispose()
            $body.Dispose()
        }
    }

    New-Dir -Path (Split-Path -Parent $Path)
    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $outline.Dispose()
    $skin.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

$actionsRoot = Join-Path $OutputRoot "actions"
New-Dir -Path $actionsRoot

$idlePath = Join-Path $actionsRoot "idle.png"
$walkPath = Join-Path $actionsRoot "walk.png"
New-Sheet -Path $idlePath -Action "idle" -FrameWidth $FrameWidth -FrameHeight $FrameHeight -FramesPerDirection $FramesPerDirection
New-Sheet -Path $walkPath -Action "walk" -FrameWidth $FrameWidth -FrameHeight $FrameHeight -FramesPerDirection $FramesPerDirection
Copy-Item -LiteralPath $idlePath -Destination (Join-Path $OutputRoot "idle.png") -Force
Copy-Item -LiteralPath $walkPath -Destination (Join-Path $OutputRoot "walk.png") -Force
Copy-Item -LiteralPath $idlePath -Destination (Join-Path $OutputRoot "preview.png") -Force

$manifest = [ordered]@{
    version = 2
    source = "LIT-ISO Asset Forge"
    pipeline = "Asset Forge smoke export validation fixture"
    assetName = "SmokeTestAdventurer"
    assetMode = "character"
    category = "Characters/Player"
    exportedAt = (Get-Date).ToString("o")
    directionOrder = @("S", "SE", "E", "NE", "N", "NW", "W", "SW")
    frameWidth = $FrameWidth
    frameHeight = $FrameHeight
    columns = $FramesPerDirection
    rows = 8
    actions = [ordered]@{
        idle = [ordered]@{
            label = "Idle"
            fps = 4
            framesPerDirection = $FramesPerDirection
            directionMode = "8-direction"
            sheet = "actions/idle.png"
            contact = $null
        }
        walk = [ordered]@{
            label = "Walk"
            fps = 8
            framesPerDirection = $FramesPerDirection
            directionMode = "8-direction"
            sheet = "actions/walk.png"
            contact = $null
        }
    }
    pixelsPerUnit = 128
    anchor = [ordered]@{
        x = 128
        y = 244
        normalized = [ordered]@{ x = 0.5; y = 0.046875 }
    }
    spriteImport = [ordered]@{
        pixelsPerUnit = 128
        pivot = [ordered]@{ x = 0.5; y = 0.046875 }
        filterMode = "Point"
        compression = "Uncompressed"
    }
    productionPreset = [ordered]@{
        id = "smoke_test_adventurer"
        label = "Smoke Test Adventurer"
    }
    lora = [ordered]@{
        name = "smoke_fixture"
        checkpoint = "none"
        strength = 0
    }
    loraEvaluations = @(
        [ordered]@{
            id = "smoke_idle_s"
            action = "idle"
            direction = "S"
            qaStatus = "pass"
            status = "accepted"
        },
        [ordered]@{
            id = "smoke_walk_e"
            action = "walk"
            direction = "E"
            qaStatus = "pass"
            status = "accepted"
        }
    )
    acceptedFrames = @(
        [ordered]@{ id = "smoke_idle_s"; action = "idle"; direction = "S"; qaStatus = "pass" },
        [ordered]@{ id = "smoke_walk_e"; action = "walk"; direction = "E"; qaStatus = "pass" }
    )
    rejectedFrames = @()
    idle = "idle.png"
    walk = "walk.png"
    preview = "preview.png"
    notes = "Deterministic smoke fixture for Asset Forge Unity export validation."
}

$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputRoot "manifest.json") -Encoding UTF8
Write-Host "Created Asset Forge smoke export at $OutputRoot"
