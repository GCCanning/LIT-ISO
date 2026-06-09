param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$OutRoot = "Assets\Generated\_Review\_PoseControls\litiso_openpose_v1",
    [string]$Action = "Idle",
    [string[]]$Directions = @("S", "E", "N", "W"),
    [switch]$Replace
)

$ErrorActionPreference = "Stop"
$python = "C:\Projects\ComfyUI\.venv\Scripts\python.exe"
if (-not (Test-Path $python)) { $python = "python" }

$argsList = @(
    (Join-Path $PSScriptRoot "build_litiso_openpose_direction_library.py"),
    "--project-root", $ProjectRoot,
    "--out-root", $OutRoot,
    "--action", $Action,
    "--directions"
) + $Directions
if ($Replace.IsPresent) { $argsList += "--replace" }

& $python @argsList
