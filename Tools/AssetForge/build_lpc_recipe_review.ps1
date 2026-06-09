param(
    [string]$LpcRoot = "C:\Projects\Pixel Pipeline\training_data\Universal-LPC-Spritesheet-Character-Generator",
    [string]$OutputPath = "Assets/Generated/_Review/LPC_CharacterRecipeReview_v1"
)

Add-Type -AssemblyName System.Drawing

$spriteRoot = Join-Path $LpcRoot "spritesheets"
New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null

function Resolve-LpcPath {
    param([string]$RelativePath)
    return Join-Path $spriteRoot $RelativePath
}

function Add-Layer {
    param(
        [System.Drawing.Graphics]$Graphics,
        [string]$Path
    )

    if (!(Test-Path $Path)) {
        throw "Missing layer: $Path"
    }

    $image = [System.Drawing.Image]::FromFile($Path)
    $Graphics.DrawImage($image, 0, 0, $image.Width, $image.Height)
    $image.Dispose()
}

function Compose-Sheet {
    param(
        [object]$Recipe,
        [string]$OutputFile
    )

    $base = [System.Drawing.Image]::FromFile($Recipe.layers[0].path)
    $bitmap = New-Object System.Drawing.Bitmap $base.Width, $base.Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor

    foreach ($layer in $Recipe.layers) {
        Add-Layer -Graphics $graphics -Path $layer.path
    }

    $bitmap.Save((Join-Path $OutputPath $OutputFile), [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
    $base.Dispose()
}

function Crop-Cell {
    param(
        [string]$SheetPath,
        [int]$Column,
        [int]$Row
    )

    $source = [System.Drawing.Image]::FromFile($SheetPath)
    $bitmap = New-Object System.Drawing.Bitmap 64, 64, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $dest = New-Object System.Drawing.Rectangle 0, 0, 64, 64
    $src = New-Object System.Drawing.Rectangle ($Column * 64), ($Row * 64), 64, 64
    $graphics.DrawImage($source, $dest, $src, [System.Drawing.GraphicsUnit]::Pixel)
    $graphics.Dispose()
    $source.Dispose()
    return $bitmap
}

$recipes = @(
    [ordered]@{
        id = "lpc_child_crusader_url_sample"
        title = "URL Sample: Child Crusader"
        layers = @(
            @{ part = "shield back"; name = "Crusader shield bg"; path = Resolve-LpcPath "shield\crusader\bg\crusader.png" },
            @{ part = "body"; name = "Child body, light"; path = Resolve-LpcPath "body\bodies\child\light.png" },
            @{ part = "head"; name = "Human male head, light"; path = Resolve-LpcPath "head\heads\human\male\light.png" },
            @{ part = "shield front"; name = "Crusader shield fg"; path = Resolve-LpcPath "shield\crusader\fg\crusader.png" }
        )
    },
    [ordered]@{
        id = "lpc_male_leather_adventurer"
        title = "Male Leather Adventurer"
        layers = @(
            @{ part = "body"; name = "Male body, light"; path = Resolve-LpcPath "body\bodies\male\light.png" },
            @{ part = "head"; name = "Human male head, light"; path = Resolve-LpcPath "head\heads\human\male\light.png" },
            @{ part = "legs"; name = "Pants, male forest"; path = Resolve-LpcPath "legs\pants\male\forest.png" },
            @{ part = "torso"; name = "Leather armour, male brown"; path = Resolve-LpcPath "torso\armour\leather\male\brown.png" },
            @{ part = "hair"; name = "Bangs hair, male dark brown"; path = Resolve-LpcPath "hair\bangs\male\dark_brown.png" }
        )
    },
    [ordered]@{
        id = "lpc_female_forest_scout"
        title = "Female Forest Scout"
        layers = @(
            @{ part = "body"; name = "Female body, light"; path = Resolve-LpcPath "body\bodies\female\light.png" },
            @{ part = "head"; name = "Human female head, light"; path = Resolve-LpcPath "head\heads\human\female\light.png" },
            @{ part = "legs"; name = "Pants, female forest"; path = Resolve-LpcPath "legs\pants\female\forest.png" },
            @{ part = "torso"; name = "Leather armour, female forest"; path = Resolve-LpcPath "torso\armour\leather\female\forest.png" },
            @{ part = "hair"; name = "Bangs hair, female carrot"; path = Resolve-LpcPath "hair\bangs\female\carrot.png" }
        )
    },
    [ordered]@{
        id = "lpc_male_plate_guard"
        title = "Male Plate Guard"
        layers = @(
            @{ part = "shield back"; name = "Crusader shield bg"; path = Resolve-LpcPath "shield\crusader\bg\crusader.png" },
            @{ part = "body"; name = "Male body, light"; path = Resolve-LpcPath "body\bodies\male\light.png" },
            @{ part = "head"; name = "Human male head, light"; path = Resolve-LpcPath "head\heads\human\male\light.png" },
            @{ part = "legs"; name = "Plate legs, male steel"; path = Resolve-LpcPath "legs\armour\plate\male\steel.png" },
            @{ part = "torso"; name = "Plate torso, male steel"; path = Resolve-LpcPath "torso\armour\plate\male\steel.png" },
            @{ part = "shield front"; name = "Crusader shield fg male"; path = Resolve-LpcPath "shield\crusader\fg\male\crusader.png" }
        )
    }
)

foreach ($recipe in $recipes) {
    Compose-Sheet -Recipe $recipe -OutputFile "$($recipe.id)_sheet.png"
}

$cardWidth = 760
$cardHeight = 190
$scale = 2
$contact = New-Object System.Drawing.Bitmap $cardWidth, ($cardHeight * $recipes.Count), ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($contact)
$graphics.Clear([System.Drawing.Color]::FromArgb(245, 246, 238))
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor

$titleFont = New-Object System.Drawing.Font "Arial", 12, ([System.Drawing.FontStyle]::Bold)
$font = New-Object System.Drawing.Font "Arial", 9
$smallFont = New-Object System.Drawing.Font "Arial", 8
$brush = [System.Drawing.Brushes]::Black
$muted = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(70, 70, 70))
$frames = @(
    @{ label = "cast N"; col = 1; row = 0 },
    @{ label = "walk W"; col = 4; row = 9 },
    @{ label = "walk S"; col = 4; row = 10 },
    @{ label = "walk E"; col = 4; row = 11 }
)

for ($i = 0; $i -lt $recipes.Count; $i++) {
    $recipe = $recipes[$i]
    $y = ($i * $cardHeight) + 10
    $graphics.DrawString($recipe.title, $titleFont, $brush, 12, $y)
    $sheetPath = Join-Path $OutputPath "$($recipe.id)_sheet.png"

    for ($frameIndex = 0; $frameIndex -lt $frames.Count; $frameIndex++) {
        $frame = $frames[$frameIndex]
        $cell = Crop-Cell -SheetPath $sheetPath -Column $frame.col -Row $frame.row
        $x = 20 + ($frameIndex * 80)
        $fy = $y + 34
        $graphics.DrawImage($cell, $x, $fy, (64 * $scale), (64 * $scale))
        $graphics.DrawString($frame.label, $smallFont, $muted, $x, ($fy + 132))
        $cell.Dispose()
    }

    $textX = 370
    $textY = $y + 28
    foreach ($layer in $recipe.layers) {
        $graphics.DrawString("$($layer.part): $($layer.name)", $font, $brush, $textX, $textY)
        $textY += 18
    }
    $graphics.DrawString("Full sheet: $($recipe.id)_sheet.png", $smallFont, $muted, $textX, ($y + 150))
}

$contact.Save((Join-Path $OutputPath "lpc_character_recipe_contact.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$contact.Dispose()

$manifest = [ordered]@{
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    source = "Universal-LPC-Spritesheet-Character-Generator local repo"
    warning = "Review/training only until licenses are filtered and credits are preserved."
    cell_size = "64x64"
    sheet_size = "832x1344"
    recipes = $recipes
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -Path (Join-Path $OutputPath "recipe_manifest.json") -Encoding UTF8

Get-ChildItem -Path $OutputPath | Select-Object Name, Length
