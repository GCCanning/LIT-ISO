param(
    [string]$SourceRoot = "C:\Projects\Pixel Pipeline\sources\opengameart\400-items-basehumanmale-orc-skeleton\extracted",
    [string]$OutDataset = "C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\oga_8d_motion_direction_v1",
    [string[]]$ReadyFolder = @("BaseHumanMale"),
    [int]$ValidationEvery = 10,
    [switch]$Replace,
    [switch]$IndexOnly
)

$ErrorActionPreference = "Stop"

$script = Join-Path $PSScriptRoot "build_oga_8d_training_index.py"
$argsList = @(
    $script,
    "--source-root", $SourceRoot,
    "--out-dataset", $OutDataset,
    "--validation-every", $ValidationEvery
)

foreach ($folder in $ReadyFolder) {
    $argsList += @("--ready-folder", $folder)
}

if ($Replace) {
    $argsList += "--replace"
}

if ($IndexOnly) {
    $argsList += "--index-only"
}

python @argsList
