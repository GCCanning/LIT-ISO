param(
    [string]$ProjectRoot = "",
    [string]$ComfyRoot = "C:\Projects\ComfyUI",
    [string]$ComfyUrl = "http://127.0.0.1:8188",
    [string]$OutputName = "litiso_lpc_motion_template_v1",
    [string]$OutputDir = "C:\Projects\Pixel Pipeline\generated\litiso_lpc_motion_template_v1_eval",
    [string]$Checkpoint = "DreamShaper_8_pruned.safetensors",
    [double]$LoraStrength = 0.72,
    [int]$Limit = 0,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

$manifestPath = Join-Path $ComfyRoot "models\loras\$OutputName.sync.json"
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Missing sync manifest: $manifestPath. Run Tools\LoRA\sync_lora_to_comfyui.ps1 -OutputName $OutputName first."
}

$sync = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$loraName = [IO.Path]::GetFileName([string]$sync.destination)
$loraPath = Join-Path $ComfyRoot "models\loras\$loraName"
if (-not (Test-Path -LiteralPath $loraPath)) {
    throw "Synced LoRA is missing: $loraPath"
}

$python = "python"
$trainingPython = "C:\Projects\LoRA-Training\.venv\Scripts\python.exe"
if (Test-Path -LiteralPath $trainingPython) {
    $python = $trainingPython
}

$script = Join-Path $ProjectRoot "Tools\LoRA\eval_lpc_motion_template_v1_comfy.py"
$args = @(
    $script,
    "--comfy-url", $ComfyUrl,
    "--out-dir", $OutputDir,
    "--checkpoint", $Checkpoint,
    "--lora", $loraName,
    "--lora-strength", ([string]$LoraStrength),
    "--limit", ([string]$Limit)
)

if ($DryRun.IsPresent) {
    $args += "--dry-run"
}

& $python @args
if ($LASTEXITCODE -ne 0) {
    throw "LPC motion-template evaluation failed with exit code $LASTEXITCODE"
}
