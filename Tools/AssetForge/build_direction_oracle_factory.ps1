param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [Parameter(Mandatory = $true)][string]$Sheet,
    [Parameter(Mandatory = $true)][string]$PackName,
    [string[]]$Frame = @("S=0,0", "E=1,0", "N=2,0", "W=3,0"),
    [int]$CellWidth = 128,
    [int]$CellHeight = 128,
    [int]$OutputWidth = 128,
    [int]$OutputHeight = 128,
    [double]$MaxFill = 0.92,
    [switch]$AllowUpscale,
    [string]$OutRoot = "Assets\Generated\_Review\_DirectionOracles",
    [string]$DatasetRoot = "C:\Projects\Pixel Pipeline\datasets\lit_iso",
    [string]$CharacterDescription = "armored knight with cyan energy trim, amber runes, dark hood, glowing sword",
    [string]$Action = "idle pose",
    [string]$License = "project_internal_or_explicitly_licensed",
    [string]$Author = "LIT-ISO",
    [string]$PythonExe = "C:\Projects\ComfyUI\.venv\Scripts\python.exe",
    [switch]$CaptureDataset
)

$ErrorActionPreference = "Stop"

if (!(Test-Path -LiteralPath $PythonExe)) {
    throw "Missing Python executable: $PythonExe"
}

$argsList = @(
    (Join-Path $PSScriptRoot "build_direction_oracle_factory.py"),
    "--project-root", $ProjectRoot,
    "--sheet", $Sheet,
    "--pack-name", $PackName,
    "--cell-width", $CellWidth,
    "--cell-height", $CellHeight,
    "--output-width", $OutputWidth,
    "--output-height", $OutputHeight,
    "--max-fill", $MaxFill,
    "--out-root", $OutRoot,
    "--dataset-root", $DatasetRoot,
    "--character-description", $CharacterDescription,
    "--action", $Action,
    "--license", $License,
    "--author", $Author
)

foreach ($frameSpec in $Frame) {
    $argsList += @("--frame", $frameSpec)
}

if ($CaptureDataset.IsPresent) {
    $argsList += "--capture-dataset"
}
if ($AllowUpscale.IsPresent) {
    $argsList += "--allow-upscale"
}

& $PythonExe @argsList
