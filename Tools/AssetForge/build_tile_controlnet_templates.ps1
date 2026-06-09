param(
    [string]$Out = "C:\Projects\Pixel Pipeline\datasets\lit_iso\controlnet_templates\tile_geometry_v1",
    [switch]$Replace
)

$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$python = "python"
$comfyPython = "C:\Projects\ComfyUI\.venv\Scripts\python.exe"
if (Test-Path -LiteralPath $comfyPython) {
    $python = $comfyPython
}

$script = Join-Path $projectRoot "Tools\AssetForge\build_tile_controlnet_templates.py"
$args = @($script, "--out", $Out)
if ($Replace.IsPresent) {
    $args += "--replace"
}

& $python @args
if ($LASTEXITCODE -ne 0) {
    throw "Tile ControlNet template build failed with exit code $LASTEXITCODE"
}
