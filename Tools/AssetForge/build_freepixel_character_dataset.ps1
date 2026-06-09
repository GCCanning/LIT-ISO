param(
    [string]$Source = "C:\Users\garyc\OneDrive\Desktop\PixelArt\FreePixel_Characters",
    [string]$OutDataset = "C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\freepixel_characters_v1",
    [string]$PromptPrefix = "LIT-ISO character style reference, cozy isometric pixel art",
    [string]$License = "freepixel_reference_pending_license_review",
    [string]$Author = "FreePixel",
    [int]$CellSize = 128,
    [int]$MaxSize = 512,
    [int]$ContentSize = 448,
    [int]$MinAlphaPixels = 24,
    [switch]$IncludeReference,
    [switch]$NoSplitSheets,
    [switch]$Replace
)

$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$python = "C:\Projects\ComfyUI\.venv\Scripts\python.exe"
if (-not (Test-Path -LiteralPath $python)) {
    $python = "py"
}

$script = Join-Path $projectRoot "Tools\AssetForge\build_freepixel_character_dataset.py"
$args = @(
    $script,
    "--source", $Source,
    "--out-dataset", $OutDataset,
    "--prompt-prefix", $PromptPrefix,
    "--license", $License,
    "--author", $Author,
    "--cell-size", ([string]$CellSize),
    "--max-size", ([string]$MaxSize),
    "--content-size", ([string]$ContentSize),
    "--min-alpha-pixels", ([string]$MinAlphaPixels)
)

if ($IncludeReference.IsPresent) {
    $args += "--include-reference"
}
if ($NoSplitSheets.IsPresent) {
    $args += "--no-split-sheets"
}
if ($Replace.IsPresent) {
    $args += "--replace"
}

& $python @args
if ($LASTEXITCODE -ne 0) {
    throw "FreePixel character dataset build failed with exit code $LASTEXITCODE"
}
