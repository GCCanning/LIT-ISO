param(
    [string]$Source = "C:\Users\garyc\OneDrive\Desktop\PixelArt",
    [string]$OutDataset = "C:\Projects\Pixel Pipeline\datasets\lit_iso\style_lock\pixelart_local_v1",
    [string]$PromptPrefix = "LIT-ISO style lock, cozy isometric pixel art",
    [string]$License = "user_supplied_pending_license_review",
    [string]$Author = "user_supplied_reference_pack",
    [int]$MaxSize = 512,
    [int]$ContentSize = 448,
    [string[]]$Category = @(),
    [switch]$IncludeGif,
    [switch]$NoTrim,
    [switch]$NoSplitStrips,
    [switch]$NoUpscaleSmall,
    [switch]$Replace
)

$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$python = "python"
$comfyPython = "C:\Projects\ComfyUI\.venv\Scripts\python.exe"
if (Test-Path -LiteralPath $comfyPython) {
    $python = $comfyPython
}

$script = Join-Path $projectRoot "Tools\AssetForge\build_pixelart_style_dataset.py"
$args = @(
    $script,
    "--source", $Source,
    "--out-dataset", $OutDataset,
    "--prompt-prefix", $PromptPrefix,
    "--license", $License,
    "--author", $Author,
    "--max-size", ([string]$MaxSize),
    "--content-size", ([string]$ContentSize)
)

foreach ($item in $Category) {
    $args += @("--category", $item)
}
if ($IncludeGif.IsPresent) {
    $args += "--include-gif"
}
if ($NoTrim.IsPresent) {
    $args += "--no-trim"
}
if ($NoSplitStrips.IsPresent) {
    $args += "--no-split-strips"
}
if ($NoUpscaleSmall.IsPresent) {
    $args += "--no-upscale-small"
}
if ($Replace.IsPresent) {
    $args += "--replace"
}

& $python @args
if ($LASTEXITCODE -ne 0) {
    throw "PixelArt style dataset build failed with exit code $LASTEXITCODE"
}
