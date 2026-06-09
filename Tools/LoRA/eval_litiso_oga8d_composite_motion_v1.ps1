param(
    [string]$TrainingRoot = "C:\Projects\LoRA-Training"
)

$ErrorActionPreference = "Stop"

$python = Join-Path $TrainingRoot ".venv\Scripts\python.exe"
$script = Join-Path $PSScriptRoot "eval_litiso_oga8d_composite_motion_v1_comfy.py"

if (!(Test-Path -LiteralPath $python)) {
    throw "Missing Python: $python"
}
if (!(Test-Path -LiteralPath $script)) {
    throw "Missing eval script: $script"
}

& $python $script
