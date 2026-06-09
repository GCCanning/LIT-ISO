param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$Action = "Walk",
    [string[]]$Directions = @("S", "E", "N", "W"),
    [string]$JobPrefix = "oga_template_cyan_knight",
    [string]$TemplatePath = "",
    [double]$Denoise = 0.42,
    [int]$Seed = 91000,
    [string]$StyleReference = "",
    [double]$StyleWeight = 0.58,
    [string]$PythonExe = "C:\Projects\ComfyUI\.venv\Scripts\python.exe",
    [switch]$Replace
)

$ErrorActionPreference = "Stop"

$script = Join-Path $PSScriptRoot "queue_oga_template_guided_requests.py"
if (!(Test-Path -LiteralPath $PythonExe)) {
    throw "Missing Python executable: $PythonExe"
}
$argsList = @(
    $script,
    "--project-root", $ProjectRoot,
    "--action", $Action,
    "--job-prefix", $JobPrefix,
    "--template-path", $TemplatePath,
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
