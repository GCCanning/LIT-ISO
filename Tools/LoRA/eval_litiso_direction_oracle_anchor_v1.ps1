param(
    [string]$ProjectRoot = "",
    [string]$ComfyRoot = "C:\Projects\ComfyUI",
    [string]$ComfyUrl = "http://127.0.0.1:8188",
    [string]$OutputName = "litiso_direction_oracle_anchor_v1",
    [string]$OutputDir = "C:\Projects\Pixel Pipeline\generated\litiso_direction_oracle_anchor_v1_eval",
    [string]$Checkpoint = "DreamShaper_8_pruned.safetensors",
    [double]$LoraStrength = 0.68,
    [string]$StyleLora = "",
    [double]$StyleStrength = 0.0,
    [string]$PromptPrefix = "",
    [string]$PromptSuffix = "",
    [int]$Limit = 0,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

$loraName = "$OutputName`_final.safetensors"
$manifestPath = Join-Path $ComfyRoot "models\loras\$OutputName.sync.json"
if (Test-Path -LiteralPath $manifestPath) {
    $sync = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $loraName = [IO.Path]::GetFileName([string]$sync.destination)
    $loraPath = Join-Path $ComfyRoot "models\loras\$loraName"
    if (-not (Test-Path -LiteralPath $loraPath)) {
        throw "Synced LoRA is missing: $loraPath"
    }
} elseif (-not $DryRun.IsPresent) {
    throw "Missing sync manifest: $manifestPath. Run Tools\LoRA\sync_lora_to_comfyui.ps1 -OutputName $OutputName first."
}

$python = "python"
$trainingPython = "C:\Projects\LoRA-Training\.venv\Scripts\python.exe"
if (Test-Path -LiteralPath $trainingPython) {
    $python = $trainingPython
}

$script = Join-Path $ProjectRoot "Tools\LoRA\eval_litiso_direction_oracle_anchor_v1_comfy.py"
$args = @(
    $script,
    "--comfy-url", $ComfyUrl,
    "--out-dir", $OutputDir,
    "--checkpoint", $Checkpoint,
    "--lora", $loraName,
    "--lora-strength", ([string]$LoraStrength),
    "--style-lora", $StyleLora,
    "--style-strength", ([string]$StyleStrength),
    "--prompt-prefix", $PromptPrefix,
    "--prompt-suffix", $PromptSuffix,
    "--limit", ([string]$Limit)
)

if ($DryRun.IsPresent) {
    $args += "--dry-run"
}

& $python @args
if ($LASTEXITCODE -ne 0) {
    throw "Direction-oracle anchor evaluation failed with exit code $LASTEXITCODE"
}
