param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$PoseRoot = "Assets\Generated\_Review\_PoseControls\litiso_openpose_v1",
    [string]$Action = "Idle",
    [string[]]$Directions = @("S", "E", "N", "W"),
    [string]$JobPrefix = "litiso_control_refknight",
    [int]$Seed = 93400,
    [string]$StyleReference = "Assets\Generated\_Review\_StyleRefs\reference_knight_front_cell.png",
    [double]$StyleWeight = 0.55,
    [double]$ControlStrength = 0.82,
    [string]$OracleManifest = "",
    [double]$TemplateDenoise = 0.64,
    [switch]$Replace
)

$ErrorActionPreference = "Stop"
$python = "C:\Projects\ComfyUI\.venv\Scripts\python.exe"
if (-not (Test-Path $python)) { $python = "python" }

$argsList = @(
    (Join-Path $PSScriptRoot "queue_litiso_controlnet_direction_requests.py"),
    "--project-root", $ProjectRoot,
    "--pose-root", $PoseRoot,
    "--action", $Action,
    "--directions"
) + $Directions + @(
    "--job-prefix", $JobPrefix,
    "--seed", $Seed,
    "--style-reference", $StyleReference,
    "--style-weight", $StyleWeight,
    "--control-strength", $ControlStrength,
    "--template-denoise", $TemplateDenoise
)
if (-not [string]::IsNullOrWhiteSpace($OracleManifest)) {
    $argsList += @("--oracle-manifest", $OracleManifest)
}
if ($Replace.IsPresent) { $argsList += "--replace" }

& $python @argsList
