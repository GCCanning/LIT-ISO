param(
    [string]$SourceRoot = "C:\Projects\Pixel Pipeline\sources\opengameart\400-items-basehumanmale-orc-skeleton\extracted\part-1",
    [string]$OutDataset = "C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\oga_8d_composite_motion_v1",
    [int]$ValidationEvery = 10,
    [switch]$Replace
)

$ErrorActionPreference = "Stop"

$script = Join-Path $PSScriptRoot "build_oga_8d_composite_dataset.py"
$argsList = @(
    $script,
    "--source-root", $SourceRoot,
    "--out-dataset", $OutDataset,
    "--validation-every", $ValidationEvery
)

if ($Replace) {
    $argsList += "--replace"
}

python @argsList
