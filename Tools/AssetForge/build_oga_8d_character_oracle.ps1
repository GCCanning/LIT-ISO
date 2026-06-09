param(
    [string]$Root = "C:\Projects\Pixel Pipeline\sources\opengameart\400-items-basehumanmale-orc-skeleton\extracted",
    [string]$Folder = "part-1/BaseHumanMale",
    [string]$OutputDir = "C:\Projects\Pixel Pipeline\generated\oga_8d_character_oracle_v1"
)

$ErrorActionPreference = "Stop"

$python = "python"
$trainingPython = "C:\Projects\LoRA-Training\.venv\Scripts\python.exe"
if (Test-Path -LiteralPath $trainingPython) {
    $python = $trainingPython
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$script = Join-Path $projectRoot "Tools\AssetForge\build_oga_8d_character_oracle.py"

& $python $script --root $Root --folder $Folder --out-dir $OutputDir
if ($LASTEXITCODE -ne 0) {
    throw "OGA 8D oracle build failed with exit code $LASTEXITCODE"
}
