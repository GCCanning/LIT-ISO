param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$PackName = "CodexBiomeStarter",
    [string]$OutputName = "litiso_tile_prop_v1",
    [string]$TrainingRoot = "C:\Projects\LoRA-Training",
    [string]$ComfyRoot = "C:\Projects\ComfyUI",
    [string]$DatasetRoot = "C:\Projects\Pixel Pipeline\datasets\lit_iso"
)

$ErrorActionPreference = "Stop"

function Test-File {
    param([string]$Path)
    [ordered]@{
        path = $Path
        exists = Test-Path -LiteralPath $Path
    }
}

function New-Check {
    param(
        [string]$Name,
        [bool]$Pass,
        [string]$Detail,
        [string]$Next = ""
    )
    [ordered]@{
        name = $Name
        pass = $Pass
        detail = $Detail
        next = $Next
    }
}

$packRoot = Join-Path $ProjectRoot "Assets\Generated\_Review\$PackName"
$reviewReport = Join-Path $packRoot "review_report.json"
$reviewDecisions = Join-Path $packRoot "review_decisions.json"
$strictReport = Join-Path $packRoot "strict_asset_quality_report.json"
$approvalManifest = Join-Path $packRoot "approval_manifest.json"
$datasetRoot = Join-Path $DatasetRoot "review_packs\$PackName"
$loraOutput = Join-Path $TrainingRoot "outputs\$OutputName"
$loraStatus = Join-Path $TrainingRoot "control\$OutputName\status.json"
$syncManifest = Join-Path $ComfyRoot "models\loras\$OutputName.sync.json"

$checks = @(
    New-Check "review_pack" (Test-Path $packRoot) $packRoot "Generate or restore a review pack under Assets/Generated/_Review/$PackName."
    New-Check "review_report" (Test-Path $reviewReport) $reviewReport "Run the pack generator or create review_report.json."
    New-Check "review_decisions" (Test-Path $reviewDecisions) $reviewDecisions "Load the pack in the dashboard and download/save decisions."
    New-Check "strict_report" (Test-Path $strictReport) $strictReport "Run test_strict_asset_quality.ps1."
    New-Check "approval_manifest" (Test-Path $approvalManifest) $approvalManifest "Run approve_review_pack.ps1 after review decisions are valid."
    New-Check "dataset_capture" (Test-Path (Join-Path $datasetRoot "metadata.jsonl")) $datasetRoot "Run capture_dataset_from_review.ps1."
    New-Check "lora_output_dir" (Test-Path $loraOutput) $loraOutput "Run or configure a local LoRA training job."
    New-Check "lora_status" (Test-Path $loraStatus) $loraStatus "Run status_litiso_training.ps1 after starting training."
    New-Check "comfy_sync_manifest" (Test-Path $syncManifest) $syncManifest "Run sync_lora_to_comfyui.ps1 after a checkpoint exists."
)

$payload = [ordered]@{
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    project_root = $ProjectRoot
    pack_name = $PackName
    output_name = $OutputName
    dataset_root = $DatasetRoot
    ready_count = @($checks | Where-Object { $_.pass }).Count
    blocked_count = @($checks | Where-Object { -not $_.pass }).Count
    checks = $checks
}

$payload | ConvertTo-Json -Depth 8
