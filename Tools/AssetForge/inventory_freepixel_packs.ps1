param(
    [string]$SourceRoot = "C:\Projects\Pixel Pipeline\sources\freepixel\extracted",
    [string]$OutRoot = "C:\Projects\Pixel Pipeline\generated\freepixel_inventory"
)

$ErrorActionPreference = "Stop"

$python = "python"
$trainingPython = "C:\Projects\LoRA-Training\.venv\Scripts\python.exe"
if (Test-Path -LiteralPath $trainingPython) {
    $python = $trainingPython
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$script = Join-Path $projectRoot "Tools\AssetForge\inventory_freepixel_packs.py"

& $python $script --source-root $SourceRoot --out-root $OutRoot
if ($LASTEXITCODE -ne 0) {
    throw "FreePixel inventory failed with exit code $LASTEXITCODE"
}
