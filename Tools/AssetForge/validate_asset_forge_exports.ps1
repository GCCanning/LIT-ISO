param(
    [string]$GeneratedRoot = "Assets\Generated",
    [string]$ReportPath = "Assets\Generated\asset_forge_export_validation.json"
)

$ErrorActionPreference = "Stop"

function Get-JsonFile {
    param([Parameter(Mandatory = $true)][string]$Path)
    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    } catch {
        throw "Could not parse JSON: $Path :: $($_.Exception.Message)"
    }
}

function Test-RelativeFile {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [AllowNull()][string]$Relative
    )
    if ([string]::IsNullOrWhiteSpace($Relative)) { return $false }
    return Test-Path -LiteralPath (Join-Path $Root $Relative)
}

function Get-RelativeFileLength {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [AllowNull()][string]$Relative
    )
    if ([string]::IsNullOrWhiteSpace($Relative)) { return 0 }
    $path = Join-Path $Root $Relative
    if (!(Test-Path -LiteralPath $path)) { return 0 }
    return (Get-Item -LiteralPath $path).Length
}

function Count-Array {
    param($Value)
    if ($null -eq $Value) { return 0 }
    if ($Value -is [array]) { return $Value.Count }
    return 1
}

if (!(Test-Path -LiteralPath $GeneratedRoot)) {
    $empty = [ordered]@{
        generatedAt = (Get-Date).ToString("o")
        generatedRoot = (Resolve-Path -LiteralPath ".").Path
        total = 0
        ready = 0
        blocked = 0
        entries = @()
        warning = "Generated root does not exist: $GeneratedRoot"
    }
    $dir = Split-Path -Parent $ReportPath
    if ($dir) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $empty | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
    Write-Host "No generated root found. Wrote $ReportPath"
    exit 0
}

$manifestPaths = Get-ChildItem -Path $GeneratedRoot -Recurse -Filter manifest.json -File
$entries = @()
$skipped = @()

foreach ($manifestFile in $manifestPaths) {
    $issues = New-Object System.Collections.Generic.List[string]
    $warnings = New-Object System.Collections.Generic.List[string]
    $manifest = $null
    try {
        $manifest = Get-JsonFile -Path $manifestFile.FullName
    } catch {
        $issues.Add($_.Exception.Message)
    }

    $root = Split-Path -Parent $manifestFile.FullName
    $relativeManifest = Resolve-Path -LiteralPath $manifestFile.FullName -Relative
    $assetName = if ($manifest.assetName) { [string]$manifest.assetName } else { Split-Path -Leaf $root }
    $assetMode = if ($manifest.assetMode) { [string]$manifest.assetMode } else { "unknown" }
    $loraName = if ($manifest.lora.name) { [string]$manifest.lora.name } else { "" }
    $loraCheckpoint = if ($manifest.lora.checkpoint) { [string]$manifest.lora.checkpoint } else { "" }

    if ($manifest) {
        $isAssetForgeExport = $manifest.source -eq "LIT-ISO Asset Forge" -or $manifest.pipeline -like "*Asset Forge*" -or $null -ne $manifest.actions -or $null -ne $manifest.spriteImport
        if (!$isAssetForgeExport) {
            $skipped += [ordered]@{
                manifestPath = $relativeManifest
                reason = "not an Asset Forge Unity export manifest"
                source = if ($manifest.source) { [string]$manifest.source } else { "" }
                trigger = if ($manifest.trigger_word) { [string]$manifest.trigger_word } else { "" }
            }
            continue
        }

        if (!$manifest.actions) {
            $issues.Add("manifest has no actions")
        } else {
            $actionProperties = $manifest.actions.PSObject.Properties
            if ($actionProperties.Count -eq 0) {
                $issues.Add("manifest actions object is empty")
            }
            foreach ($property in $actionProperties) {
                $action = $property.Value
                if (!(Test-RelativeFile -Root $root -Relative $action.sheet)) {
                    $issues.Add("missing action sheet: $($action.sheet)")
                } elseif ((Get-RelativeFileLength -Root $root -Relative $action.sheet) -lt 1024) {
                    $issues.Add("action sheet is too small or empty: $($action.sheet)")
                }
                if ($action.contact -and !(Test-RelativeFile -Root $root -Relative $action.contact)) {
                    $warnings.Add("missing contact sheet: $($action.contact)")
                } elseif ($action.contact -and (Get-RelativeFileLength -Root $root -Relative $action.contact) -lt 1024) {
                    $warnings.Add("contact sheet is too small or empty: $($action.contact)")
                }
                $framesPerDirection = 0
                if ($null -ne $action.framesPerDirection) {
                    $framesPerDirection = [int]$action.framesPerDirection
                }
                if ($framesPerDirection -le 0) {
                    $issues.Add("invalid framesPerDirection for action: $($property.Name)")
                }
            }
        }

        if (!$manifest.spriteImport) {
            $warnings.Add("missing spriteImport block")
        } else {
            $pixelsPerUnit = 0
            if ($null -ne $manifest.spriteImport.pixelsPerUnit) {
                $pixelsPerUnit = [int]$manifest.spriteImport.pixelsPerUnit
            }
            if ($pixelsPerUnit -le 0) {
                $issues.Add("invalid spriteImport pixelsPerUnit")
            }
            if ($null -eq $manifest.spriteImport.pivot) {
                $warnings.Add("missing spriteImport pivot")
            }
        }

        if (!$manifest.lora) {
            $warnings.Add("missing lora provenance")
        }

        $accepted = Count-Array $manifest.acceptedFrames
        $rejected = Count-Array $manifest.rejectedFrames
        $qaFails = 0
        $qaWarnings = 0
        foreach ($eval in @($manifest.loraEvaluations)) {
            if ($eval.qaStatus -eq "fail") { $qaFails++ }
            if ($eval.qaStatus -eq "warn") { $qaWarnings++ }
        }
        if ($rejected -gt 0) {
            $issues.Add("$rejected rejected frame(s)")
        }
        if ($qaFails -gt 0) {
            $issues.Add("$qaFails QA failure(s)")
        }

        $automationReport = Join-Path $root "automation_report.json"
        if (!(Test-Path -LiteralPath $automationReport)) {
            $warnings.Add("Unity automation_report.json has not been generated")
        }

        $entries += [ordered]@{
            assetName = $assetName
            assetMode = $assetMode
            manifestPath = $relativeManifest
            loraName = $loraName
            loraCheckpoint = $loraCheckpoint
            acceptedFrameCount = $accepted
            rejectedFrameCount = $rejected
            qaWarnCount = $qaWarnings
            qaFailCount = $qaFails
            promotionReady = $issues.Count -eq 0
            issues = @($issues)
            warnings = @($warnings)
        }
    } else {
        $entries += [ordered]@{
            assetName = $assetName
            assetMode = $assetMode
            manifestPath = $relativeManifest
            loraName = $loraName
            loraCheckpoint = $loraCheckpoint
            acceptedFrameCount = 0
            rejectedFrameCount = 0
            qaWarnCount = 0
            qaFailCount = 0
            promotionReady = $false
            issues = @($issues)
            warnings = @($warnings)
        }
    }
}

$ready = @($entries | Where-Object { $_.promotionReady }).Count
$report = [ordered]@{
    generatedAt = (Get-Date).ToString("o")
    generatedRoot = (Resolve-Path -LiteralPath $GeneratedRoot).Path
    scannedManifests = $manifestPaths.Count
    skipped = $skipped.Count
    total = $entries.Count
    ready = $ready
    blocked = $entries.Count - $ready
    skippedEntries = $skipped
    entries = $entries
}

$reportDir = Split-Path -Parent $ReportPath
if ($reportDir) {
    New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
}
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
Write-Host "Asset Forge export validation: $ready/$($entries.Count) ready, $($skipped.Count) skipped. Report: $ReportPath"
