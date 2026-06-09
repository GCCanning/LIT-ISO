param(
    [string]$Dataset = "C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\recovered_motion_candidates_v1",
    [string]$Reason = "Human QC failed: recovered/generated sprites do not face requested north/east/south/west directions reliably."
)

$ErrorActionPreference = "Stop"

$manifestPath = Join-Path $Dataset "dataset_manifest.json"
$qaPath = Join-Path $Dataset "qa_report.json"
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "Missing dataset manifest: $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifestOut = [ordered]@{}
foreach ($property in $manifest.PSObject.Properties) {
    $manifestOut[$property.Name] = $property.Value
}
$manifestOut.status = "quarantined_direction_failed"
$manifestOut.direction_qc_status = "fail"
$manifestOut.direction_qc_reason = $Reason
$manifestOut.training_allowed = $false
$manifestOut.next_step = "Do not train on this dataset. Use only for extraction debugging unless individual frames are manually re-approved with correct direction labels."
$manifestOut | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

if (Test-Path -LiteralPath $qaPath) {
    $qa = Get-Content -LiteralPath $qaPath -Raw | ConvertFrom-Json
    $qaOut = [ordered]@{}
    foreach ($property in $qa.PSObject.Properties) {
        $qaOut[$property.Name] = $property.Value
    }
    $qaOut.status = "fail_direction_qc"
    $qaOut.direction_qc_reason = $Reason
    $qaOut.training_allowed = $false
    $qaOut | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $qaPath -Encoding UTF8
}

$quarantine = [ordered]@{
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    dataset = $Dataset
    status = "quarantined_direction_failed"
    reason = $Reason
    training_allowed = $false
}
$quarantine | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Dataset "QUARANTINED_DIRECTION_QC.json") -Encoding UTF8

$quarantine | ConvertTo-Json -Depth 4
