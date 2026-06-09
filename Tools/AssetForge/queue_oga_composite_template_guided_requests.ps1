param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$Dataset = "C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\oga_8d_composite_motion_v1",
    [string]$Preset = "iron_knight",
    [string]$Action = "Walk",
    [string[]]$Directions = @("S", "E", "N", "W"),
    [string]$JobPrefix = "oga_composite_refknight_style",
    [double]$Denoise = 0.70,
    [int]$Seed = 91600,
    [string]$StyleReference = "Assets\Generated\_Review\_StyleRefs\reference_knight_front_cell.png",
    [double]$StyleWeight = 0.62,
    [string]$PythonExe = "C:\Projects\ComfyUI\.venv\Scripts\python.exe",
    [switch]$Replace
)

$ErrorActionPreference = "Stop"

$script = Join-Path $PSScriptRoot "queue_oga_composite_template_guided_requests.py"
if (!(Test-Path -LiteralPath $PythonExe)) {
    throw "Missing Python executable: $PythonExe"
}

$argsList = @(
    $script,
    "--project-root", $ProjectRoot,
    "--dataset", $Dataset,
    "--preset", $Preset,
    "--action", $Action,
    "--job-prefix", $JobPrefix,
    "--denoise", $Denoise,
    "--seed", $Seed,
    "--style-reference", $StyleReference,
    "--style-weight", $StyleWeight,
    "--directions"
) + $Directions

if ($Replace) {
    $argsList += "--replace"
}

& $PythonExe @argsList
