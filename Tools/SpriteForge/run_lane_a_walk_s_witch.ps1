param(
    [switch]$DryRun,
    [string]$Python = "C:\Projects\ComfyUI\.venv\Scripts\python.exe"
)

$ErrorActionPreference = "Stop"
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
if (!(Test-Path $Python)) {
    $Python = "python"
}

$argsList = @(
    (Join-Path $PSScriptRoot "run_lane_a_animation.py"),
    "--project-root", $projectRoot.Path,
    "--character", "witch",
    "--character-ref", "Assets\Characters\Witch\AnimationSprites\Static\witch static00.png",
    "--action", "walk",
    "--direction", "S",
    "--target-size", "64",
    "--seed", "1207"
)

if ($DryRun) {
    $argsList += "--dry-run"
}

& $Python @argsList
