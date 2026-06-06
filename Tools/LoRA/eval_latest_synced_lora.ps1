param(
    [string]$ProjectRoot = "",
    [string]$ComfyRoot = "C:\Projects\ComfyUI",
    [string]$OutputName = "litiso_tile_prop_v1",
    [string]$AssetForgeUrl = "http://127.0.0.1:4180",
    [string]$ComfyUrl = "http://127.0.0.1:8188",
    [string]$OutputDir = "",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

$loraDir = Join-Path $ComfyRoot "models\loras"
$manifestPath = Join-Path $loraDir "$OutputName.sync.json"
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Missing sync manifest: $manifestPath. Run sync_lora_to_comfyui.ps1 first."
}

$sync = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$destination = [string]$sync.destination
if ([string]::IsNullOrWhiteSpace($destination)) {
    throw "Sync manifest does not include destination: $manifestPath"
}

$loraName = [IO.Path]::GetFileName($destination)
if (-not (Test-Path -LiteralPath $destination)) {
    throw "Synced LoRA is missing from ComfyUI: $destination"
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path "C:\Projects\Pixel Pipeline\generated" "$OutputName`_latest_synced_eval"
}

$summary = [ordered]@{
    schemaVersion = 2
    outputName = $OutputName
    loraName = $loraName
    syncManifest = $manifestPath
    sync = $sync
    outputDir = $OutputDir
    dryRun = [bool]$DryRun
    startedAt = (Get-Date).ToString("o")
    productionGuidance = "Live ComfyUI evaluation requires the next implementation step: add a verified runner contract with checkpoint/LoRA presence checks, workflow-node compatibility checks, output image validation, and artifact import handoff. Use -DryRun for manifest/prompt validation without ComfyUI/network."
    promptCategories = [ordered]@{
        terrain = [ordered]@{
            description = "Walkable isometric top tiles for tilemap surfaces."
            presets = @("forest_grass_tile", "plains_dirt_tile")
        }
        props = [ordered]@{
            description = "Separate bottom-anchored decoration or blocker sprites."
            presets = @("forest_oak_prop", "plains_bush_prop", "shared_rock_prop")
        }
    }
}

if ($OutputName -like "*sprixen_frame*") {
    $script = Join-Path $ProjectRoot "Tools\LoRA\evaluate_asset_forge_sprixen_checkpoint.ps1"
    $args = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $script,
        "-AssetForgeUrl", $AssetForgeUrl,
        "-OutputDir", $OutputDir,
        "-LoraName", $loraName
    )
    $summary.evaluator = $script
    $summary.mode = "asset_forge_sprixen"
    $summary.command = @("powershell") + $args
    if ($DryRun) {
        $summary.completedAt = (Get-Date).ToString("o")
        $summary.exitCode = 0
        $summaryPath = Join-Path $ProjectRoot "Tools\LoRA\$OutputName.latest_synced_eval_summary.json"
        $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
        [ordered]@{ planned = $true; lora = $loraName; output_dir = $OutputDir; summary = $summaryPath; mode = $summary.mode } | ConvertTo-Json -Depth 6
        return
    }
    Write-Host "Evaluating latest synced Sprixen LoRA: $loraName"
    & powershell @args
    $exitCode = $LASTEXITCODE
} else {
    $script = Join-Path $ProjectRoot "Tools\LoRA\eval_litiso_tile_prop_v1_comfy.py"
    $python = "python"
    $trainingPython = "C:\Projects\LoRA-Training\.venv\Scripts\python.exe"
    if (Test-Path -LiteralPath $trainingPython) {
        $python = $trainingPython
    }
    $args = @(
        $script,
        "--comfy-url", $ComfyUrl,
        "--out-dir", $OutputDir,
        "--lora", $loraName
    )
    $summary.evaluator = $script
    $summary.mode = "comfy_tile_prop"
    $summary.command = @($python) + $args
    if ($DryRun) {
        $summary.completedAt = (Get-Date).ToString("o")
        $summary.exitCode = 0
        $summaryPath = Join-Path $ProjectRoot "Tools\LoRA\$OutputName.latest_synced_eval_summary.json"
        $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
        [ordered]@{ planned = $true; lora = $loraName; output_dir = $OutputDir; summary = $summaryPath; mode = $summary.mode } | ConvertTo-Json -Depth 6
        return
    }
    Write-Host "Evaluating latest synced tile/prop LoRA: $loraName"
    & $python @args
    $exitCode = $LASTEXITCODE
}

$summary.completedAt = (Get-Date).ToString("o")
$summary.exitCode = $exitCode
$summaryPath = Join-Path $ProjectRoot "Tools\LoRA\$OutputName.latest_synced_eval_summary.json"
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding UTF8

if ($exitCode -ne 0) {
    throw "Evaluator failed with exit code $exitCode. Summary: $summaryPath. Next implementation required: add production runner checks for ComfyUI availability, checkpoint/LoRA installation, workflow compatibility, output validation, and Asset Forge/artifact handoff before treating live output as shippable. Use -DryRun to validate planning without ComfyUI/network."
}

[ordered]@{
    lora = $loraName
    output_dir = $OutputDir
    summary = $summaryPath
    mode = $summary.mode
} | ConvertTo-Json -Depth 6
