param(
    [switch]$DryRun,
    [switch]$FromLaneAFrames
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$Python = "C:\Projects\ComfyUI\.venv\Scripts\python.exe"
if (!(Test-Path $Python)) {
    $Python = "python"
}

$argsList = @(
    "Tools\SpriteForge\run_lane_b_animation.py",
    "--project-root", $ProjectRoot,
    "--character", "witch",
    "--action", "walk",
    "--direction", "S",
    "--target-size", "64"
)

if ($DryRun) {
    $argsList += "--dry-run"
}

if ($FromLaneAFrames -or !$DryRun) {
    $argsList += @(
        "--input-frames",
        "Tools\SpriteForge\out\p2_fix_sweep\d038_c062_bob\witch\walk\S\frames"
    )
}

& $Python @argsList
& $Python "Tools\SpriteForge\build_lane_ab_comparison.py" --project-root $ProjectRoot
