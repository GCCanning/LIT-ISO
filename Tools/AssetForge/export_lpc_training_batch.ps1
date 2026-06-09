param(
    [string]$LpcRoot = "C:\Projects\Pixel Pipeline\training_data\Universal-LPC-Spritesheet-Character-Generator",
    [string]$OutputPath = "Assets/Generated/_Review/LPC_MaleFemaleTrainingBatch_v2",
    [switch]$SkipAdaptation
)

Add-Type -AssemblyName System.Drawing

$spriteRoot = Join-Path $LpcRoot "spritesheets"
$sheetOut = Join-Path $OutputPath "sheets"
$frameOut = Join-Path $OutputPath "frames"
$captionOut = Join-Path $OutputPath "captions"
$contactOut = Join-Path $OutputPath "contact"

if ((Test-Path $OutputPath) -and ($OutputPath -match "LPC_MaleFemaleTrainingBatch")) {
    Remove-Item -LiteralPath $OutputPath -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $sheetOut, $frameOut, $captionOut, $contactOut | Out-Null

function LpcPath {
    param([string]$RelativePath)
    return Join-Path $spriteRoot $RelativePath
}

function Existing-Layers {
    param([array]$Layers)
    $result = @()
    foreach ($layer in $Layers) {
        if (Test-Path $layer.path) {
            $result += $layer
        }
        else {
            Write-Warning "Missing LPC layer skipped: $($layer.path)"
        }
    }
    return $result
}

function Draw-Layer {
    param(
        [System.Drawing.Graphics]$Graphics,
        [string]$Path
    )

    $image = [System.Drawing.Image]::FromFile($Path)
    $Graphics.DrawImage($image, 0, 0, $image.Width, $image.Height)
    $image.Dispose()
}

function Compose-Sheet {
    param(
        [array]$Layers,
        [string]$OutputFile
    )

    $width = 0
    $height = 0
    foreach ($layer in $Layers) {
        $probe = [System.Drawing.Image]::FromFile($layer.path)
        if ($probe.Width -gt $width) { $width = $probe.Width }
        if ($probe.Height -gt $height) { $height = $probe.Height }
        $probe.Dispose()
    }

    if ($width -le 0 -or $height -le 0) {
        throw "Cannot compose sheet with no valid layer dimensions: $OutputFile"
    }

    $bitmap = New-Object System.Drawing.Bitmap $width, $height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor

    foreach ($layer in $Layers) {
        Draw-Layer -Graphics $graphics -Path $layer.path
    }

    $bitmap.Save($OutputFile, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

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

    if ($SkipAdaptation) {
        return $Cell.Clone()
    }

    $bounds = Get-Bounds -Bitmap $Cell
    $output = New-Object System.Drawing.Bitmap 64, 64, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($output)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

    $maxWidth = 40
    $maxHeight = 50
    $scale = [Math]::Min($maxWidth / $bounds.Width, $maxHeight / $bounds.Height)
    $drawWidth = [Math]::Max(1, [int][Math]::Round($bounds.Width * $scale))
    $drawHeight = [Math]::Max(1, [int][Math]::Round($bounds.Height * $scale * 0.9))
    $anchorX = 32
    $anchorY = 61
    $drawX = [int][Math]::Round($anchorX - ($drawWidth / 2))
    $drawY = [int][Math]::Round($anchorY - $drawHeight)
    $dest = New-Object System.Drawing.Rectangle $drawX, $drawY, $drawWidth, $drawHeight
    $graphics.DrawImage($Cell, $dest, $bounds, [System.Drawing.GraphicsUnit]::Pixel)
    $graphics.Dispose()
    return $output
}

$baseRecipes = @(
    [ordered]@{
        id = "male_leather_adventurer"
        sex = "male"
        description = "male leather adventurer"
        layers = @(
            @{ role = "body"; name = "Male body light"; path = LpcPath "body\bodies\male\light.png" },
            @{ role = "head"; name = "Human male head light"; path = LpcPath "head\heads\human\male\light.png" },
            @{ role = "legs"; name = "Pants male forest"; path = LpcPath "legs\pants\male\forest.png" },
            @{ role = "feet"; name = "Boots male leather"; path = LpcPath "feet\boots\male\leather.png" },
            @{ role = "torso"; name = "Leather armour male brown"; path = LpcPath "torso\armour\leather\male\brown.png" },
            @{ role = "hair"; name = "Bangs hair male dark brown"; path = LpcPath "hair\bangs\male\dark_brown.png" }
        )
    },
    [ordered]@{
        id = "female_forest_scout"
        sex = "female"
        description = "female forest scout"
        layers = @(
            @{ role = "body"; name = "Female body light"; path = LpcPath "body\bodies\female\light.png" },
            @{ role = "head"; name = "Human female head light"; path = LpcPath "head\heads\human\female\light.png" },
            @{ role = "legs"; name = "Pants female forest"; path = LpcPath "legs\pants\female\forest.png" },
            @{ role = "feet"; name = "Boots female leather"; path = LpcPath "feet\boots\female\leather.png" },
            @{ role = "torso"; name = "Leather armour female forest"; path = LpcPath "torso\armour\leather\female\forest.png" },
            @{ role = "hair"; name = "Bangs hair female carrot"; path = LpcPath "hair\bangs\female\carrot.png" }
        )
    }
)

function Tool-Layers {
    param(
        [string]$ToolId,
        [string]$Sex
    )

    switch ($ToolId) {
        "none" { return @() }
        "axe" {
            return @(
                @{ role = "tool"; name = "Axe universal $Sex"; path = LpcPath "tools\smash\universal\$Sex\axe.png" }
            )
        }
        "hammer" {
            return @(
                @{ role = "tool"; name = "Hammer universal $Sex"; path = LpcPath "tools\smash\universal\$Sex\hammer.png" }
            )
        }
        "pickaxe" {
            return @(
                @{ role = "tool"; name = "Pickaxe universal $Sex"; path = LpcPath "tools\smash\universal\$Sex\pickaxe.png" }
            )
        }
        "hoe" {
            return @(
                @{ role = "tool_back"; name = "Hoe background"; path = LpcPath "tools\thrust\background\hoe.png" },
                @{ role = "tool_front"; name = "Hoe foreground"; path = LpcPath "tools\thrust\foreground\hoe.png" }
            )
        }
        "shovel" {
            return @(
                @{ role = "tool_back"; name = "Shovel background"; path = LpcPath "tools\thrust\background\shovel.png" },
                @{ role = "tool_front"; name = "Shovel foreground"; path = LpcPath "tools\thrust\foreground\shovel.png" }
            )
        }
        "watering" {
            return @(
                @{ role = "tool_back"; name = "Watering can background"; path = LpcPath "tools\thrust\background\watering.png" },
                @{ role = "tool_front"; name = "Watering can foreground"; path = LpcPath "tools\thrust\foreground\watering.png" }
            )
        }
        "rod" {
            return @(
                @{ role = "tool_back"; name = "Rod background"; path = LpcPath "tools\rod\background\rod.png" },
                @{ role = "tool_front"; name = "Rod foreground"; path = LpcPath "tools\rod\foreground\rod.png" }
            )
        }
        "longsword_slash" {
            return @(
                @{ role = "weapon_back"; name = "Longsword slash behind"; path = LpcPath "weapon\sword\longsword\attack_slash\behind\longsword.png" },
                @{ role = "weapon_front"; name = "Longsword slash foreground"; path = LpcPath "weapon\sword\longsword\attack_slash\longsword.png" }
            )
        }
        default { return @() }
    }
}

$toolIds = @("none", "axe", "hammer", "pickaxe", "hoe", "shovel", "watering", "rod", "longsword_slash")
function Test-ToolActionCompatible {
    param(
        [string]$ToolId,
        [string]$ActionId
    )

    if ($ToolId -eq "longsword_slash") {
        return $ActionId -eq "slash"
    }

    if ($ToolId -in @("hoe", "shovel", "watering")) {
        return $ActionId -in @("thrust", "walk")
    }

    if ($ToolId -eq "rod") {
        return $ActionId -in @("spellcast", "walk")
    }

    return $true
}

$toolActionCompatibility = [ordered]@{
    none = @("spellcast", "thrust", "walk", "slash", "shoot", "hurt")
    axe = @("spellcast", "thrust", "walk", "slash", "shoot", "hurt")
    hammer = @("spellcast", "thrust", "walk", "slash", "shoot", "hurt")
    pickaxe = @("spellcast", "thrust", "walk", "slash", "shoot", "hurt")
    hoe = @("thrust", "walk")
    shovel = @("thrust", "walk")
    watering = @("thrust", "walk")
    rod = @("spellcast", "walk")
    longsword_slash = @("slash")
}

$actions = @(
    @{ id = "spellcast"; caption = "spellcast"; rows = @{ north = 0; west = 1; south = 2; east = 3 }; frames = 7 },
    @{ id = "thrust"; caption = "thrust attack"; rows = @{ north = 4; west = 5; south = 6; east = 7 }; frames = 8 },
    @{ id = "walk"; caption = "walk cycle"; rows = @{ north = 8; west = 9; south = 10; east = 11 }; frames = 9 },
    @{ id = "slash"; caption = "slash attack"; rows = @{ north = 12; west = 13; south = 14; east = 15 }; frames = 6 },
    @{ id = "shoot"; caption = "shoot attack"; rows = @{ north = 16; west = 17; south = 18; east = 19 }; frames = 13 },
    @{ id = "hurt"; caption = "hurt"; rows = @{ south = 20 }; frames = 6 }
)

$records = New-Object System.Collections.Generic.List[object]
$sheetRecords = New-Object System.Collections.Generic.List[object]
$contactSamples = New-Object System.Collections.Generic.List[object]

foreach ($recipe in $baseRecipes) {
    foreach ($toolId in $toolIds) {
        $toolLayersRaw = Tool-Layers -ToolId $toolId -Sex $recipe.sex
        $backToolLayers = @($toolLayersRaw | Where-Object { $_.role -match "_back|weapon_back" })
        $frontToolLayers = @($toolLayersRaw | Where-Object { $_.role -notmatch "_back|weapon_back" })
        $layers = Existing-Layers (@($backToolLayers) + @($recipe.layers) + @($frontToolLayers))
        $variantId = "$($recipe.id)__tool_$toolId"
        $sheetPath = Join-Path $sheetOut "$variantId.png"
        Compose-Sheet -Layers $layers -OutputFile $sheetPath
        $sheetRecords.Add([ordered]@{
            id = $variantId
            character = $recipe.id
            sex = $recipe.sex
            tool = $toolId
            sheet = $sheetPath
            layers = $layers
        })

        $sheet = [System.Drawing.Image]::FromFile($sheetPath)
        foreach ($action in $actions) {
            if (!(Test-ToolActionCompatible -ToolId $toolId -ActionId $action.id)) {
                continue
            }

            foreach ($direction in $action.rows.Keys) {
                $row = [int]$action.rows[$direction]
                for ($frame = 0; $frame -lt [int]$action.frames; $frame++) {
                    $cell = Get-Cell -Sheet $sheet -Column $frame -Row $row
                    $litIso = Convert-ToLitIsoCell -Cell $cell
                    $frameDir = Join-Path $frameOut (Join-Path $recipe.sex (Join-Path $toolId (Join-Path $action.id $direction)))
                    $captionDir = Join-Path $captionOut (Join-Path $recipe.sex (Join-Path $toolId (Join-Path $action.id $direction)))
                    New-Item -ItemType Directory -Force -Path $frameDir, $captionDir | Out-Null
                    $fileStem = "$($recipe.id)__tool_$($toolId)__$($action.id)_$direction`__f$($frame.ToString('00'))"
                    $frameFile = Join-Path $frameDir "$fileStem.png"
                    $captionFile = Join-Path $captionDir "$fileStem.txt"
                    $litIso.Save($frameFile, [System.Drawing.Imaging.ImageFormat]::Png)
                    $caption = "LPC motion template, $($recipe.description), tool $toolId, $($action.caption), $direction, frame $($frame + 1) of $($action.frames), 64x64 transparent pixel sprite, LIT-ISO normalized bottom-center anchor"
                    Set-Content -Path $captionFile -Value $caption -Encoding UTF8
                    $records.Add([ordered]@{
                        file = $frameFile
                        caption_file = $captionFile
                        caption = $caption
                        character = $recipe.id
                        sex = $recipe.sex
                        tool = $toolId
                        action = $action.id
                        direction = $direction
                        frame_index = $frame
                        frame_count = $action.frames
                        source_sheet = $sheetPath
                    })
                    if (($toolId -in @("none", "axe", "hoe", "rod", "longsword_slash")) -and ($action.id -in @("walk", "spellcast", "thrust", "slash")) -and ($direction -in @("south", "west")) -and ($frame -eq 2)) {
                        $contactSamples.Add([ordered]@{
                            image = $frameFile
                            label = "$($recipe.sex) $toolId $($action.id) $direction"
                        })
                    }
                    $cell.Dispose()
                    $litIso.Dispose()
                }
            }
        }
        $sheet.Dispose()
    }
}

$manifest = [ordered]@{
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    source = $LpcRoot
    output = $OutputPath
    status = "review_training_source"
    warning = "Not for runtime promotion until LPC license filtering and credit preservation are complete."
    cell_size = "64x64"
    adapted = (-not $SkipAdaptation)
    characters = $baseRecipes
    tools = $toolIds
    tool_action_compatibility = $toolActionCompatibility
    actions = $actions | ForEach-Object {
        [ordered]@{ id = $_.id; frames = $_.frames; directions = @($_.rows.Keys) }
    }
    sheet_count = $sheetRecords.Count
    frame_count = $records.Count
    sheets = $sheetRecords
}
$manifest | ConvertTo-Json -Depth 10 | Set-Content -Path (Join-Path $OutputPath "manifest.json") -Encoding UTF8
$records | ConvertTo-Json -Depth 7 | Set-Content -Path (Join-Path $OutputPath "frame_index.json") -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $LpcRoot "CREDITS.csv") -Destination (Join-Path $OutputPath "SOURCE_CREDITS.csv") -Force
Copy-Item -LiteralPath (Join-Path $LpcRoot "LICENSE") -Destination (Join-Path $OutputPath "SOURCE_LICENSE") -Force

$cols = 4
$thumb = 110
$pad = 22
$labelHeight = 42
$rows = [Math]::Ceiling($contactSamples.Count / $cols)
$contact = New-Object System.Drawing.Bitmap (($cols * ($thumb + $pad)) + $pad), (($rows * ($thumb + $labelHeight + $pad)) + $pad), ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($contact)
$graphics.Clear([System.Drawing.Color]::FromArgb(245, 246, 238))
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$font = New-Object System.Drawing.Font "Arial", 8
for ($i = 0; $i -lt $contactSamples.Count; $i++) {
    $sample = $contactSamples[$i]
    $col = $i % $cols
    $row = [Math]::Floor($i / $cols)
    $x = $pad + ($col * ($thumb + $pad))
    $y = $pad + ($row * ($thumb + $labelHeight + $pad))
    $img = [System.Drawing.Image]::FromFile($sample.image)
    $graphics.DrawImage($img, $x, $y, $thumb, $thumb)
    $label = $sample.label
    if ($label.Length -gt 22) {
        $label = $label.Substring(0, 22)
    }
    $graphics.DrawString($label, $font, [System.Drawing.Brushes]::Black, $x, ($y + $thumb + 4))
    $img.Dispose()
}
$contactPath = Join-Path (Resolve-Path $contactOut).Path "lpc_training_batch_contact.png"
$contact.Save($contactPath, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$contact.Dispose()

[ordered]@{
    output = $OutputPath
    sheets = $sheetRecords.Count
    frames = $records.Count
    contact = (Join-Path $contactOut "lpc_training_batch_contact.png")
} | ConvertTo-Json -Depth 4
