param(
    [string]$TrainingRoot = "C:\Projects\LoRA-Training",
    [string]$ComfyRoot = "C:\Projects\ComfyUI",
    [string]$OutputName = "litiso_tile_prop_v1",
    [string]$CheckpointPath = "",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$outputDir = Join-Path $TrainingRoot "outputs\$OutputName"
if (-not $CheckpointPath) {
    $latest = Get-ChildItem -Path $outputDir -Filter "*.safetensors" -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if (-not $latest) {
        throw "No LoRA checkpoints found in $outputDir"
    }
    $CheckpointPath = $latest.FullName
}

if (-not (Test-Path $CheckpointPath)) {
    throw "Missing checkpoint: $CheckpointPath"
}

$checkpoint = Get-Item -LiteralPath $CheckpointPath
$loraDir = Join-Path $ComfyRoot "models\loras"
$dest = Join-Path $loraDir $checkpoint.Name
$manifestPath = Join-Path $loraDir "$OutputName.sync.json"
$existing = if (Test-Path -LiteralPath $dest) { Get-Item -LiteralPath $dest } else { $null }
$hash = Get-FileHash -LiteralPath $checkpoint.FullName -Algorithm SHA256
$existingHash = $null
if ($existing) {
    try {
        $existingHash = (Get-FileHash -LiteralPath $existing.FullName -Algorithm SHA256).Hash
    }
    catch {
        $existingHash = $null
    }
}
$alreadySynced = $existing -and $existingHash -eq $hash.Hash

$payload = [ordered]@{
    output_name = $OutputName
    source = $checkpoint.FullName
    destination = $dest
    source_size_bytes = $checkpoint.Length
    source_last_write_utc = $checkpoint.LastWriteTimeUtc.ToString("o")
    source_sha256 = $hash.Hash
    replacing_existing = $null -ne $existing
    existing_destination_size_bytes = if ($existing) { $existing.Length } else { $null }
    existing_destination_sha256 = $existingHash
    already_synced = $alreadySynced
    synced_utc = (Get-Date).ToUniversalTime().ToString("o")
}

if ($DryRun.IsPresent) {
    $payload | ConvertTo-Json -Depth 6
    return
}

New-Item -ItemType Directory -Force -Path $loraDir | Out-Null
if (-not $alreadySynced) {
    Copy-Item -LiteralPath $checkpoint.FullName -Destination $dest -Force
}
$payload | ConvertTo-Json -Depth 6 | Set-Content -Path $manifestPath -Encoding UTF8

[ordered]@{
    copied = $dest
    skipped_copy = [bool]$alreadySynced
    manifest = $manifestPath
    source_sha256 = $hash.Hash
    next = "Run eval_latest_synced_lora.ps1 -OutputName $OutputName when ComfyUI/Asset Forge is ready."
} | ConvertTo-Json
