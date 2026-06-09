param(
    [string]$SourceBatch = "Assets/Generated/_Review/LPC_MaleFemaleTrainingBatch_v2",
    [string]$OutputDataset = "C:\Projects\Pixel Pipeline\datasets\lit_iso\characters\training\lpc_motion_male_female_v1",
    [int]$ValidationEvery = 10
)

$ErrorActionPreference = "Stop"

$sourceRoot = (Resolve-Path $SourceBatch).Path
$frameIndexPath = Join-Path $sourceRoot "frame_index.json"
$manifestPath = Join-Path $sourceRoot "manifest.json"
if (!(Test-Path $frameIndexPath)) {
    throw "Missing frame index: $frameIndexPath"
}
if (!(Test-Path $manifestPath)) {
    throw "Missing manifest: $manifestPath"
}

if ((Test-Path $OutputDataset) -and ($OutputDataset -match "lpc_motion_male_female")) {
    Remove-Item -LiteralPath $OutputDataset -Recurse -Force
}

$imageDir = Join-Path $OutputDataset "images"
$captionDir = Join-Path $OutputDataset "captions"
$provenanceDir = Join-Path $OutputDataset "provenance"
New-Item -ItemType Directory -Force -Path $imageDir, $captionDir, $provenanceDir | Out-Null

$records = Get-Content -Raw -Path $frameIndexPath | ConvertFrom-Json
$metadataPath = Join-Path $OutputDataset "metadata.jsonl"
$trainPath = Join-Path $OutputDataset "train.txt"
$valPath = Join-Path $OutputDataset "val.txt"
$metadataLines = New-Object System.Collections.Generic.List[string]
$trainLines = New-Object System.Collections.Generic.List[string]
$valLines = New-Object System.Collections.Generic.List[string]
$summary = [ordered]@{
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    source_batch = $sourceRoot
    output_dataset = $OutputDataset
    status = "lora_ready_motion_template"
    warning = "Use as LPC motion/template data only. Do not treat as final LIT-ISO actor style. Confirm/retain LPC attribution and license obligations before production use."
    category = "lpc_motion_template"
    validation_every = $ValidationEvery
    total = 0
    train = 0
    validation = 0
    by_character = [ordered]@{}
    by_tool = [ordered]@{}
    by_action = [ordered]@{}
    by_direction = [ordered]@{}
}

function Inc-Count {
    param(
        [hashtable]$Map,
        [string]$Key
    )
    if (!$Map.Contains($Key)) {
        $Map[$Key] = 0
    }
    $Map[$Key] = [int]$Map[$Key] + 1
}

$index = 0
foreach ($record in $records) {
    $sourceImage = Resolve-Path $record.file
    $sourceCaption = Resolve-Path $record.caption_file
    $stem = [IO.Path]::GetFileNameWithoutExtension($sourceImage.Path)
    $destImageRel = "images/$stem.png"
    $destCaptionRel = "captions/$stem.txt"
    $destImage = Join-Path $OutputDataset $destImageRel
    $destCaption = Join-Path $OutputDataset $destCaptionRel
    Copy-Item -LiteralPath $sourceImage.Path -Destination $destImage -Force
    Copy-Item -LiteralPath $sourceCaption.Path -Destination $destCaption -Force

    $split = if ($ValidationEvery -gt 0 -and (($index + 1) % $ValidationEvery -eq 0)) { "validation" } else { "train" }
    if ($split -eq "validation") {
        $valLines.Add($destImageRel)
        $summary.validation = [int]$summary.validation + 1
    }
    else {
        $trainLines.Add($destImageRel)
        $summary.train = [int]$summary.train + 1
    }

    $payload = [ordered]@{
        file_name = $destImageRel
        text = [string]$record.caption
        category = "lpc_motion_template"
        split = $split
        character = [string]$record.character
        sex = [string]$record.sex
        tool = [string]$record.tool
        action = [string]$record.action
        direction = [string]$record.direction
        frame_index = [int]$record.frame_index
        frame_count = [int]$record.frame_count
        caption_file = $destCaptionRel
        source_sheet = [string]$record.source_sheet
    }
    $metadataLines.Add(($payload | ConvertTo-Json -Compress -Depth 6))
    Inc-Count -Map $summary.by_character -Key ([string]$record.character)
    Inc-Count -Map $summary.by_tool -Key ([string]$record.tool)
    Inc-Count -Map $summary.by_action -Key ([string]$record.action)
    Inc-Count -Map $summary.by_direction -Key ([string]$record.direction)
    $summary.total = [int]$summary.total + 1
    $index++
}

$metadataLines | Set-Content -Path $metadataPath -Encoding UTF8
$trainLines | Set-Content -Path $trainPath -Encoding UTF8
$valLines | Set-Content -Path $valPath -Encoding UTF8
Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $provenanceDir "source_batch_manifest.json") -Force
Copy-Item -LiteralPath $frameIndexPath -Destination (Join-Path $provenanceDir "source_frame_index.json") -Force
foreach ($name in @("SOURCE_CREDITS.csv", "SOURCE_LICENSE", "README.md")) {
    $path = Join-Path $sourceRoot $name
    if (Test-Path $path) {
        Copy-Item -LiteralPath $path -Destination (Join-Path $provenanceDir $name) -Force
    }
}

$summary | ConvertTo-Json -Depth 8 | Set-Content -Path (Join-Path $OutputDataset "dataset_manifest.json") -Encoding UTF8

[ordered]@{
    output_dataset = $OutputDataset
    images = (Get-ChildItem -Path $imageDir -Filter "*.png" -File).Count
    captions = (Get-ChildItem -Path $captionDir -Filter "*.txt" -File).Count
    metadata = $metadataLines.Count
    train = $trainLines.Count
    validation = $valLines.Count
} | ConvertTo-Json -Depth 4
