param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$JobName,
    [string]$RequestPath,
    [switch]$ReplaceExisting,
    [switch]$DryRun,
    [string]$PythonExe = "C:\Projects\ComfyUI\.venv\Scripts\python.exe"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function Get-SafeName {
    param([string]$Value, [string]$Default = "asset_job")
    if ([string]::IsNullOrWhiteSpace($Value)) { return $Default }
    $safe = ($Value.Trim() -replace "[^A-Za-z0-9_.-]", "_")
    $safe = $safe.Trim("._")
    if ([string]::IsNullOrWhiteSpace($safe)) { return $Default }
    return $safe
}

function Assert-UnderRoot {
    param([string]$Root, [string]$Path, [string]$Label)
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd("\", "/")
    $pathFull = [IO.Path]::GetFullPath($Path).TrimEnd("\", "/")
    if ($pathFull -ne $rootFull -and -not $pathFull.StartsWith($rootFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label must stay inside ProjectRoot. Root: $rootFull Path: $pathFull"
    }
}

function Get-PropValue {
    param([object]$Object, [string]$Name, [object]$Default)
    if ($null -eq $Object) { return $Default }
    if ($Object -is [System.Collections.IDictionary] -and $Object.Contains($Name) -and $null -ne $Object[$Name]) { return $Object[$Name] }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -ne $property -and $null -ne $property.Value) { return $property.Value }
    return $Default
}

function Get-Config {
    $local = Join-Path $ProjectRoot "Tools\AssetForge\asset_forge.local.json"
    $example = Join-Path $ProjectRoot "Tools\AssetForge\asset_forge.local.example.json"
    $path = if (Test-Path $local) { $local } else { $example }
    if (-not (Test-Path $path)) { return [PSCustomObject]@{} }
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

function Convert-ToRepoPath {
    param([string]$Path)
    $root = (Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd("\", "/")
    $full = [IO.Path]::GetFullPath($Path)
    if ($full.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        return $full.Substring($root.Length + 1).Replace("\", "/")
    }
    return $full.Replace("\", "/")
}

function Convert-ToTitleName {
    param([string]$Value, [string]$Default = "Shared")
    $safe = Get-SafeName -Value $Value -Default $Default
    return (Get-Culture).TextInfo.ToTitleCase($safe.ToLowerInvariant())
}

function Update-RequestStatus {
    param([string]$StatusPath, [hashtable]$Fields)
    if (-not $StatusPath) { return }
    $payload = [ordered]@{}
    if (Test-Path $StatusPath) {
        try {
            $existing = Get-Content -Raw -LiteralPath $StatusPath | ConvertFrom-Json
            foreach ($property in $existing.PSObject.Properties) { $payload[$property.Name] = $property.Value }
        }
        catch { }
    }
    foreach ($key in $Fields.Keys) { $payload[$key] = $Fields[$key] }
    $payload["updated_utc"] = (Get-Date).ToUniversalTime().ToString("o")
    $payload | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $StatusPath -Encoding UTF8
}

function Read-PngFacts {
    param([string]$Path)
    $img = [System.Drawing.Bitmap]::FromFile($Path)
    try { return [PSCustomObject]@{ width = $img.Width; height = $img.Height } }
    finally { $img.Dispose() }
}

function Get-StrictByPath {
    param([string]$StrictReportPath)
    $map = @{}
    if (-not (Test-Path $StrictReportPath)) { return $map }
    $strict = Get-Content -Raw -LiteralPath $StrictReportPath | ConvertFrom-Json
    foreach ($item in @($strict.items)) {
        $key = [IO.Path]::GetFullPath([string]$item.path).Replace("\", "/").ToLowerInvariant()
        $map[$key] = $item
    }
    return $map
}

if ([string]::IsNullOrWhiteSpace($RequestPath)) {
    if ([string]::IsNullOrWhiteSpace($JobName)) { throw "Pass -JobName or -RequestPath." }
    $safeJobName = Get-SafeName -Value $JobName
    $RequestPath = Join-Path $ProjectRoot "Assets\Generated\_Review\_Requests\$safeJobName\generation_request.json"
}

if (-not (Test-Path $RequestPath)) { throw "Missing generation request: $RequestPath" }
if (-not (Test-Path $PythonExe)) { throw "Missing Python executable for Sprixen worker: $PythonExe" }

$projectRootResolved = (Resolve-Path -LiteralPath $ProjectRoot).Path
$requestFull = (Resolve-Path -LiteralPath $RequestPath).Path
Assert-UnderRoot -Root $projectRootResolved -Path $requestFull -Label "RequestPath"
$requestRoot = Split-Path -Parent $requestFull
$statusPath = Join-Path $requestRoot "request_status.json"
$request = Get-Content -Raw -LiteralPath $requestFull | ConvertFrom-Json

$job = Get-SafeName -Value ([string](Get-PropValue $request "job_name" (Get-PropValue $request "jobName" "asset_job")))
$mode = ([string](Get-PropValue $request "asset_mode" (Get-PropValue $request "assetMode" "tile"))).ToLowerInvariant()
if (@("tile", "prop", "item", "character", "npc", "mob") -notcontains $mode) {
    throw "Sprixen request worker currently supports tile, prop, item, character, npc, and mob. Job '$job' requested '$mode'."
}

$spec = Get-PropValue $request "asset_spec" (Get-PropValue $request "assetSpec" ([PSCustomObject]@{}))
$biomeFolder = Convert-ToTitleName -Value ([string](Get-PropValue $spec "biome" "Shared")) -Default "Shared"
$config = Get-Config
$sprixen = if ($config -and $config.sprixen) { $config.sprixen } else { [PSCustomObject]@{} }
$defaults = if ($sprixen -and $sprixen.defaults) { $sprixen.defaults } else { [PSCustomObject]@{} }
$modeDefaults = [PSCustomObject]@{}
if ($defaults -and $defaults.mode_defaults -and $defaults.mode_defaults.PSObject.Properties[$mode]) {
    $modeDefaults = $defaults.mode_defaults.$mode
}

$baseUrl = [string](Get-PropValue $sprixen "base_url" "https://api.sprixen.com/v1")
$apiKey = [string](Get-PropValue $sprixen "api_key" "")
$apiKeyEnv = [string](Get-PropValue $sprixen "api_key_env" "SPRIXEN_API_KEY")
if ([string]::IsNullOrWhiteSpace($apiKey) -and $apiKeyEnv -match "^spx_") {
    $apiKey = $apiKeyEnv
    $apiKeyEnv = "SPRIXEN_API_KEY"
}
if ([string]::IsNullOrWhiteSpace($apiKey) -and -not [string]::IsNullOrWhiteSpace($env:SPRIXEN_API_KEY)) {
    $apiKey = $env:SPRIXEN_API_KEY
}
$projectId = [string](Get-PropValue $modeDefaults "project_id" (Get-PropValue $defaults "project_id" ""))
$resolution = [string](Get-PropValue $modeDefaults "resolution" (Get-PropValue $defaults "resolution" "64x64"))
$pixelPerfect = [string](Get-PropValue $modeDefaults "pixel_perfect" (Get-PropValue $defaults "pixel_perfect" "on"))
$timeoutSeconds = [int](Get-PropValue $modeDefaults "timeout_seconds" (Get-PropValue $defaults "timeout_seconds" 180))
$chainAnimation = [bool](Get-PropValue $modeDefaults "chain_animation" (Get-PropValue $defaults "chain_animation" $false))

$reviewRoot = Join-Path $ProjectRoot "Assets\Generated\_Review\$job"
$rawRoot = Join-Path $requestRoot "Outputs\sprixen_raw"
$cleanRoot = Join-Path $requestRoot "Outputs\sprixen_cleaned"
$manifestPath = Join-Path $requestRoot "sprixen_generation_manifest.json"
Assert-UnderRoot -Root $projectRootResolved -Path $reviewRoot -Label "ReviewRoot"
Assert-UnderRoot -Root $projectRootResolved -Path $rawRoot -Label "RawOutputRoot"
Assert-UnderRoot -Root $projectRootResolved -Path $cleanRoot -Label "CleanOutputRoot"

Update-RequestStatus -StatusPath $statusPath -Fields @{
    ok = $true
    status = if ($DryRun.IsPresent) { "sprixen_dry_run" } else { "sprixen_running" }
    job_name = $job
    provider = "sprixen"
}

$pythonScript = Join-Path $PSScriptRoot "sprixen_generation_worker.py"
$pythonArgs = @(
    $pythonScript,
    "--project-root", $ProjectRoot,
    "--request-path", $requestFull,
    "--raw-output-root", $rawRoot,
    "--clean-output-root", $cleanRoot,
    "--manifest-path", $manifestPath,
    "--base-url", $baseUrl,
    "--resolution", $resolution,
    "--pixel-perfect", $pixelPerfect,
    "--timeout-seconds", $timeoutSeconds
)
if (-not [string]::IsNullOrWhiteSpace($projectId)) {
    $pythonArgs += @("--project-id", $projectId)
}
if ($chainAnimation) { $pythonArgs += "--chain-animation" }
if ($DryRun.IsPresent) { $pythonArgs += "--dry-run" }

$oldSprixenKey = $env:SPRIXEN_API_KEY
if (-not [string]::IsNullOrWhiteSpace($apiKey)) { $env:SPRIXEN_API_KEY = $apiKey }
try {
    $workerOutput = & $PythonExe @pythonArgs 2>&1
    $exitCode = $LASTEXITCODE
}
finally {
    $env:SPRIXEN_API_KEY = $oldSprixenKey
}

if (-not (Test-Path $manifestPath)) {
    Update-RequestStatus -StatusPath $statusPath -Fields @{ ok = $false; status = "failed"; error = "Sprixen worker did not write manifest."; stdout = ($workerOutput -join "`n") }
    throw "Sprixen worker did not write manifest. Output: $($workerOutput -join "`n")"
}
$sprixenManifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
if ($DryRun.IsPresent) {
    Update-RequestStatus -StatusPath $statusPath -Fields @{ ok = $true; status = "sprixen_dry_run_ready"; manifest = (Convert-ToRepoPath $manifestPath) }
    [PSCustomObject]@{
        ok = $true
        status = "sprixen_dry_run_ready"
        job_name = $job
        asset_mode = $mode
        provider = "sprixen"
        manifest = Convert-ToRepoPath $manifestPath
        outputs_planned = @($sprixenManifest.outputs).Count
        key_configured = -not [string]::IsNullOrWhiteSpace($apiKey)
    } | ConvertTo-Json -Depth 8
    exit 0
}
if ($exitCode -ne 0 -or $sprixenManifest.status -ne "complete") {
    Update-RequestStatus -StatusPath $statusPath -Fields @{ ok = $false; status = "failed"; error = "Sprixen worker failed."; manifest = (Convert-ToRepoPath $manifestPath); stdout = ($workerOutput -join "`n") }
    throw "Sprixen worker failed with exit code $exitCode. Manifest: $manifestPath Output: $($workerOutput -join "`n")"
}

if ((Test-Path $reviewRoot) -and -not $ReplaceExisting.IsPresent) {
    throw "Review pack already exists. Pass -ReplaceExisting to overwrite: $reviewRoot"
}
if (Test-Path $reviewRoot) { Remove-Item -LiteralPath $reviewRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $reviewRoot | Out-Null
$styleSnapshotSource = Join-Path $requestRoot "Inputs\style_profile.snapshot.json"
$styleProvenancePath = Join-Path $reviewRoot "style_provenance.json"
$styleProvenanceRepoPath = ""
if (Test-Path $styleSnapshotSource) {
    Copy-Item -LiteralPath $styleSnapshotSource -Destination $styleProvenancePath -Force
    $styleProvenanceRepoPath = Convert-ToRepoPath $styleProvenancePath
}

$copied = @()
foreach ($output in @($sprixenManifest.outputs | Where-Object { $_.status -eq "ok" })) {
    $source = Join-Path $ProjectRoot ([string]$output.cleaned_path).Replace("/", "\")
    if (-not (Test-Path $source)) { throw "Missing cleaned output: $source" }
    $category = [string]$output.category
    $folder = switch ($category) {
        "terrain" { Join-Path $reviewRoot $biomeFolder }
        "decoration" { Join-Path $reviewRoot "Decorations\$biomeFolder" }
        "item" { Join-Path $reviewRoot "Items\$biomeFolder" }
        "character" { Join-Path $reviewRoot "Characters\$biomeFolder" }
        "npc" { Join-Path $reviewRoot "NPCs\$biomeFolder" }
        "mob" { Join-Path $reviewRoot "Mobs\$biomeFolder" }
        default { Join-Path $reviewRoot "Misc\$biomeFolder" }
    }
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    $dest = Join-Path $folder ([IO.Path]::GetFileName([string]$output.cleaned_path))
    Copy-Item -LiteralPath $source -Destination $dest -Force
    $copied += [PSCustomObject]@{ output = $output; path = $dest; category = $category }
}
if ($copied.Count -eq 0) { throw "Sprixen worker produced no successful cleaned PNG outputs." }

$strictReport = Join-Path $reviewRoot "strict_asset_quality_report.json"
$strictScript = Join-Path $PSScriptRoot "test_strict_asset_quality.ps1"
$tileShape = ([string](Get-PropValue $spec "tile_shape" (Get-PropValue $spec "tileShape" ""))).ToLowerInvariant()
$terrainProfile = if ($mode -eq "tile" -and @("raised_height_block", "height_block", "raised", "height") -contains $tileShape) { "raised_block" } elseif ($mode -eq "tile") { "flat" } else { "auto" }
$strictOutput = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $strictScript -InputPath $reviewRoot -OutputPath $strictReport -Category auto -TerrainProfile $terrainProfile 2>&1
$strictExitCode = $LASTEXITCODE
if ($strictExitCode -ne 0) { throw "Strict QA failed to run: $strictReport Output: $($strictOutput -join "`n")" }
$strictByPath = Get-StrictByPath -StrictReportPath $strictReport

$items = @()
foreach ($copy in $copied) {
    $facts = Read-PngFacts -Path $copy.path
    $key = [IO.Path]::GetFullPath($copy.path).Replace("\", "/").ToLowerInvariant()
    $qa = if ($strictByPath.ContainsKey($key)) { $strictByPath[$key] } else { $null }
    $issues = if ($qa) { @($qa.issues) } else { @() }
    $warnings = if ($qa) { @($qa.warnings) } else { @() }
    $status = if ($qa) { [string]$qa.status } elseif ($issues.Count -eq 0) { "pass" } else { "review" }
    $relativePath = Convert-ToRepoPath $copy.path
    $items += [PSCustomObject]@{
        name = [IO.Path]::GetFileName($copy.path)
        path = $relativePath
        category = $copy.category
        biome = $biomeFolder
        width = $facts.width
        height = $facts.height
        status = $status
        issues = $issues
        warnings = $warnings
        generation = [ordered]@{
            provider = "sprixen"
            source_kind = "sprixen_api"
            sprixen_generation_id = $copy.output.sprixen_generation_id
            result_url = $copy.output.result_url
            raw_path = $copy.output.raw_path
            cleaned_path = $copy.output.cleaned_path
            resolution = $resolution
            pixel_perfect = $pixelPerfect
            project_id = $projectId
        }
    }
}

$passCount = @($items | Where-Object { $_.status -eq "pass" }).Count
$reviewCount = @($items | Where-Object { $_.status -ne "pass" }).Count
$generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
$qualityContract = [ordered]@{
    provider = "sprixen"
    supported_modes = @("tile", "prop", "item", "character", "npc", "mob")
    post_process = @("edge_background_removal", "nearest_neighbor_normalize", "mode_canvas_fit", "palette_snap", "strict_asset_quality_scan")
    clean_room_note = "Sprixen output is optional source material and must pass local review before training or Unity promotion."
}

$report = [ordered]@{
    pack_name = $job
    generated_utc = $generatedUtc
    source_request = Convert-ToRepoPath $requestFull
    provider = "sprixen"
    source_kind = "sprixen_api"
    asset_mode = $mode
    total = $items.Count
    terrain_count = @($items | Where-Object { $_.category -eq "terrain" }).Count
    decoration_count = @($items | Where-Object { $_.category -eq "decoration" }).Count
    item_count = @($items | Where-Object { $_.category -eq "item" }).Count
    character_count = @($items | Where-Object { $_.category -eq "character" }).Count
    npc_count = @($items | Where-Object { $_.category -eq "npc" }).Count
    mob_count = @($items | Where-Object { $_.category -eq "mob" }).Count
    pass_count = $passCount
    review_count = $reviewCount
    quality_contract = $qualityContract
    sprixen_manifest = Convert-ToRepoPath $manifestPath
    style_provenance = $styleProvenanceRepoPath
    items = @($items)
}
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $reviewRoot "review_report.json") -Encoding UTF8

$decisions = @()
foreach ($item in $items) {
    $id = $item.path.Replace("\", "/") -replace "^Assets/Generated/_Review/$job/", ""
    $categoryRoot = switch ($item.category) {
        "terrain" { "Tiles" }
        "decoration" { "Props" }
        "item" { "Items" }
        "character" { "Characters" }
        "npc" { "NPCs" }
        "mob" { "Mobs" }
        default { "Misc" }
    }
    $decisions += [PSCustomObject]@{
        id = $id
        name = $item.name
        category = $item.category
        biome = $item.biome
        source_path = $item.path
        destination_path = "Assets/Generated/$categoryRoot/$($item.biome)/$($item.name)"
        review_status = $item.status
        decision = if ($item.status -eq "pass") { "pending" } else { "needs_edit" }
        approval_blocked = $item.status -ne "pass"
        training_capture = $false
        notes = "Generated by Sprixen API; review before approval or training."
        issues = @($item.issues)
        warnings = @($item.warnings)
    }
}
$decisionPayload = [ordered]@{
    pack_name = $job
    generated_utc = $generatedUtc
    provider = "sprixen"
    summary = [ordered]@{
        total = $decisions.Count
        approved = 0
        pending = @($decisions | Where-Object { $_.decision -eq "pending" }).Count
        rejected = 0
        needs_edit = @($decisions | Where-Object { $_.decision -eq "needs_edit" }).Count
    }
    decisions = $decisions
}
$decisionPayload | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $reviewRoot "review_decisions.json") -Encoding UTF8

$manifest = [ordered]@{
    schema = "lit_iso.asset_forge.sprixen_worker_result.v1"
    status = "review_pack_ready"
    job_name = $job
    asset_mode = $mode
    provider = "sprixen"
    generated_utc = $generatedUtc
    request_path = Convert-ToRepoPath $requestFull
    review_root = Convert-ToRepoPath $reviewRoot
    raw_output_root = Convert-ToRepoPath $rawRoot
    cleaned_output_root = Convert-ToRepoPath $cleanRoot
    sprixen_generation_manifest = Convert-ToRepoPath $manifestPath
    style_provenance = $styleProvenanceRepoPath
    generated_files = @($items | ForEach-Object { $_.path })
    summary = [ordered]@{ pass = $passCount; review = $reviewCount; total = $items.Count }
}
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $reviewRoot "generation_manifest.json") -Encoding UTF8

Update-RequestStatus -StatusPath $statusPath -Fields @{
    ok = $true
    status = "review_pack_ready"
    provider = "sprixen"
    job_name = $job
    review_root = Convert-ToRepoPath $reviewRoot
    review_report = "Assets/Generated/_Review/$job/review_report.json"
    review_decisions = "Assets/Generated/_Review/$job/review_decisions.json"
    sprixen_generation_manifest = Convert-ToRepoPath $manifestPath
    style_provenance = $styleProvenanceRepoPath
}

[PSCustomObject]@{
    ok = $true
    status = "review_pack_ready"
    job_name = $job
    asset_mode = $mode
    provider = "sprixen"
    review_root = Convert-ToRepoPath $reviewRoot
    generated_files = $items.Count
    pass = $passCount
    review = $reviewCount
    review_report = "Assets/Generated/_Review/$job/review_report.json"
    review_decisions = "Assets/Generated/_Review/$job/review_decisions.json"
    sprixen_generation_manifest = Convert-ToRepoPath $manifestPath
    style_provenance = $styleProvenanceRepoPath
} | ConvertTo-Json -Depth 10
