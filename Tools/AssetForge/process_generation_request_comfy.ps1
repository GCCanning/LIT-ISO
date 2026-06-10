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
    if ($Object -is [System.Collections.IDictionary] -and $Object.Contains($Name) -and $null -ne $Object[$Name]) {
        return $Object[$Name]
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -ne $property -and $null -ne $property.Value) {
        return $property.Value
    }
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
    try {
        return [PSCustomObject]@{ width = $img.Width; height = $img.Height }
    }
    finally {
        $img.Dispose()
    }
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
if (-not (Test-Path $PythonExe)) { throw "Missing Python executable for Comfy worker: $PythonExe" }

$projectRootResolved = (Resolve-Path -LiteralPath $ProjectRoot).Path
$requestFull = (Resolve-Path -LiteralPath $RequestPath).Path
Assert-UnderRoot -Root $projectRootResolved -Path $requestFull -Label "RequestPath"
$requestRoot = Split-Path -Parent $requestFull
$statusPath = Join-Path $requestRoot "request_status.json"
$request = Get-Content -Raw -LiteralPath $requestFull | ConvertFrom-Json

$job = Get-SafeName -Value ([string](Get-PropValue $request "job_name" (Get-PropValue $request "jobName" "asset_job")))
$mode = ([string](Get-PropValue $request "asset_mode" (Get-PropValue $request "assetMode" "tile"))).ToLowerInvariant()
if (@("tile", "prop", "item", "character", "npc", "mob") -notcontains $mode) {
    throw "Comfy request worker currently supports tile, prop, item, character, npc, and mob. Job '$job' requested '$mode'."
}

$spec = Get-PropValue $request "asset_spec" (Get-PropValue $request "assetSpec" ([PSCustomObject]@{}))
$requestComfySettings = Get-PropValue $request "comfy_settings" (Get-PropValue $request "comfySettings" ([PSCustomObject]@{}))
$biomeFolder = Convert-ToTitleName -Value ([string](Get-PropValue $spec "biome" "Shared")) -Default "Shared"
$config = Get-Config
$workerDefaults = if ($config -and $config.comfyui -and $config.comfyui.worker_defaults) { $config.comfyui.worker_defaults } else { [PSCustomObject]@{} }
$modeDefaults = [PSCustomObject]@{}
if ($workerDefaults -and $workerDefaults.mode_defaults -and $workerDefaults.mode_defaults.PSObject.Properties[$mode]) {
    $modeDefaults = $workerDefaults.mode_defaults.$mode
}
$comfyUrl = [string](Get-PropValue (Get-PropValue $config "comfyui" ([PSCustomObject]@{})) "url" "http://127.0.0.1:8188")
$checkpoint = [string](Get-PropValue $modeDefaults "checkpoint" (Get-PropValue $workerDefaults "checkpoint" "DreamShaper_8_pruned.safetensors"))
$lora = [string](Get-PropValue $modeDefaults "lora" (Get-PropValue $workerDefaults "lora" ""))
$loraStrength = [double](Get-PropValue $modeDefaults "lora_strength" (Get-PropValue $workerDefaults "lora_strength" 0.0))
$steps = [int](Get-PropValue $modeDefaults "steps" (Get-PropValue $workerDefaults "steps" 22))
$cfg = [double](Get-PropValue $modeDefaults "cfg" (Get-PropValue $workerDefaults "cfg" 6.0))
$sampler = [string](Get-PropValue $modeDefaults "sampler" (Get-PropValue $workerDefaults "sampler" "dpmpp_2m"))
$scheduler = [string](Get-PropValue $modeDefaults "scheduler" (Get-PropValue $workerDefaults "scheduler" "karras"))
$width = [int](Get-PropValue $modeDefaults "width" (Get-PropValue $workerDefaults "width" 512))
$height = [int](Get-PropValue $modeDefaults "height" (Get-PropValue $workerDefaults "height" 512))
$denoise = [double](Get-PropValue $modeDefaults "denoise" (Get-PropValue $workerDefaults "denoise" 1.0))
$timeoutSeconds = [int](Get-PropValue $modeDefaults "timeout_seconds" (Get-PropValue $workerDefaults "timeout_seconds" 600))

if ($requestComfySettings) {
    $checkpoint = [string](Get-PropValue $requestComfySettings "checkpoint" $checkpoint)
    $lora = [string](Get-PropValue $requestComfySettings "lora" $lora)
    $loraStrength = [double](Get-PropValue $requestComfySettings "lora_strength" (Get-PropValue $requestComfySettings "loraStrength" $loraStrength))
    $steps = [int](Get-PropValue $requestComfySettings "steps" $steps)
    $cfg = [double](Get-PropValue $requestComfySettings "cfg" $cfg)
    $sampler = [string](Get-PropValue $requestComfySettings "sampler" $sampler)
    $scheduler = [string](Get-PropValue $requestComfySettings "scheduler" $scheduler)
    $width = [int](Get-PropValue $requestComfySettings "width" $width)
    $height = [int](Get-PropValue $requestComfySettings "height" $height)
    $denoise = [double](Get-PropValue $requestComfySettings "denoise" $denoise)
    $timeoutSeconds = [int](Get-PropValue $requestComfySettings "timeout_seconds" (Get-PropValue $requestComfySettings "timeoutSeconds" $timeoutSeconds))
}

$reviewRoot = Join-Path $ProjectRoot "Assets\Generated\_Review\$job"
$rawRoot = Join-Path $requestRoot "Outputs\raw"
$cleanRoot = Join-Path $requestRoot "Outputs\cleaned"
$manifestPath = Join-Path $requestRoot "comfy_generation_manifest.json"
Assert-UnderRoot -Root $projectRootResolved -Path $reviewRoot -Label "ReviewRoot"
Assert-UnderRoot -Root $projectRootResolved -Path $rawRoot -Label "RawOutputRoot"
Assert-UnderRoot -Root $projectRootResolved -Path $cleanRoot -Label "CleanOutputRoot"

Update-RequestStatus -StatusPath $statusPath -Fields @{
    ok = $true
    status = if ($DryRun.IsPresent) { "comfy_dry_run" } else { "comfy_running" }
    job_name = $job
    provider = "comfyui"
}

$pythonScript = Join-Path $PSScriptRoot "comfy_generation_worker.py"
$pythonArgs = @(
    $pythonScript,
    "--project-root", $ProjectRoot,
    "--request-path", $requestFull,
    "--raw-output-root", $rawRoot,
    "--clean-output-root", $cleanRoot,
    "--manifest-path", $manifestPath,
    "--comfy-url", $comfyUrl,
    "--checkpoint", $checkpoint,
    "--steps", $steps,
    "--cfg", $cfg,
    "--sampler", $sampler,
    "--scheduler", $scheduler,
    "--width", $width,
    "--height", $height,
    "--denoise", $denoise,
    "--timeout-seconds", $timeoutSeconds
)
if (-not [string]::IsNullOrWhiteSpace($lora)) {
    $pythonArgs += @("--lora", $lora, "--lora-strength", $loraStrength)
}
if ($DryRun.IsPresent) { $pythonArgs += "--dry-run" }

$workerOutput = & $PythonExe @pythonArgs 2>&1
$exitCode = $LASTEXITCODE
if (-not (Test-Path $manifestPath)) {
    Update-RequestStatus -StatusPath $statusPath -Fields @{ ok = $false; status = "failed"; error = "Comfy worker did not write manifest."; stdout = ($workerOutput -join "`n") }
    throw "Comfy worker did not write manifest. Output: $($workerOutput -join "`n")"
}
$comfyManifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
if ($DryRun.IsPresent) {
    Update-RequestStatus -StatusPath $statusPath -Fields @{ ok = $true; status = "comfy_dry_run_ready"; manifest = (Convert-ToRepoPath $manifestPath) }
    [PSCustomObject]@{
        ok = $true
        status = "comfy_dry_run_ready"
        job_name = $job
        asset_mode = $mode
        manifest = Convert-ToRepoPath $manifestPath
        outputs_planned = @($comfyManifest.outputs).Count
    } | ConvertTo-Json -Depth 8
    exit 0
}
if ($exitCode -ne 0 -or $comfyManifest.status -ne "complete") {
    Update-RequestStatus -StatusPath $statusPath -Fields @{ ok = $false; status = "failed"; error = "Comfy worker failed."; manifest = (Convert-ToRepoPath $manifestPath); stdout = ($workerOutput -join "`n") }
    throw "Comfy worker failed with exit code $exitCode. Manifest: $manifestPath Output: $($workerOutput -join "`n")"
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
foreach ($output in @($comfyManifest.outputs | Where-Object { $_.status -eq "ok" })) {
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
    $copied += [PSCustomObject]@{
        output = $output
        path = $dest
        category = $category
    }
}
if ($copied.Count -eq 0) { throw "Comfy worker produced no successful cleaned PNG outputs." }

$strictReport = Join-Path $reviewRoot "strict_asset_quality_report.json"
$strictScript = Join-Path $PSScriptRoot "test_strict_asset_quality.ps1"
$strictOutput = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $strictScript -InputPath $reviewRoot -OutputPath $strictReport -Category auto 2>&1
$strictExitCode = $LASTEXITCODE
if ($strictExitCode -ne 0) { throw "Strict QA failed to run: $strictReport Output: $($strictOutput -join "`n")" }
$strictByPath = Get-StrictByPath -StrictReportPath $strictReport

$postProcessList = @((Get-PropValue $request "post_process" (Get-PropValue $request "postProcess" @())) | ForEach-Object { ([string]$_).ToLowerInvariant() })
$properPixelArtRequested = $postProcessList -contains "proper_pixel_art" -or $postProcessList -contains "proper-pixel-art" -or $postProcessList -contains "proper_pixel_art_cleanup"
$properPixelArtReportPath = ""
$properPixelArtContactSheetPath = ""
if ($properPixelArtRequested) {
    $properPixelArtRoot = Join-Path $reviewRoot "_ProperPixelArt"
    $properScript = Join-Path $PSScriptRoot "run_proper_pixel_art_cleanup.ps1"
    $properOutput = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $properScript -ProjectRoot $ProjectRoot -InputPath $reviewRoot -OutputRoot $properPixelArtRoot -Mode $mode -Transparent -PythonExe $PythonExe 2>&1
    $properExitCode = $LASTEXITCODE
    if ($properExitCode -ne 0) {
        throw "Proper Pixel Art cleanup failed: $properPixelArtRoot Output: $($properOutput -join "`n")"
    }
    $candidateReport = Join-Path $properPixelArtRoot "proper_pixel_art_report.json"
    $candidateSheet = Join-Path $properPixelArtRoot "proper_pixel_art_contact_sheet.png"
    if (Test-Path -LiteralPath $candidateReport) { $properPixelArtReportPath = Convert-ToRepoPath $candidateReport }
    if (Test-Path -LiteralPath $candidateSheet) { $properPixelArtContactSheetPath = Convert-ToRepoPath $candidateSheet }
}

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
            provider = "comfyui"
            seed = $copy.output.seed
            prompt_id = $copy.output.prompt_id
            raw_path = $copy.output.raw_path
            cleaned_path = $copy.output.cleaned_path
            checkpoint = $checkpoint
            lora = $lora
            lora_strength = $loraStrength
        }
    }
}

$passCount = @($items | Where-Object { $_.status -eq "pass" }).Count
$reviewCount = @($items | Where-Object { $_.status -ne "pass" }).Count
$generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
$qualityContract = [ordered]@{
    provider = "comfyui"
    supported_modes = @("tile", "prop", "item", "character", "npc", "mob")
    post_process = @("edge_background_removal", "nearest_neighbor_normalize", "tile_diamond_mask_or_asset_fit", "strict_asset_quality_scan") + $(if ($properPixelArtReportPath) { @("proper_pixel_art_sidecar_review") } else { @() })
    note = "First production worker path. Character directions and animation sheets are intentionally deferred."
}

$report = [ordered]@{
    pack_name = $job
    generated_utc = $generatedUtc
    source_request = Convert-ToRepoPath $requestFull
    provider = "comfyui"
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
    comfy_manifest = Convert-ToRepoPath $manifestPath
    style_provenance = $styleProvenanceRepoPath
    proper_pixel_art_report = $properPixelArtReportPath
    proper_pixel_art_contact_sheet = $properPixelArtContactSheetPath
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
        notes = "Generated by ComfyUI worker; review before approval or training."
        issues = @($item.issues)
        warnings = @($item.warnings)
    }
}
$decisionPayload = [ordered]@{
    pack_name = $job
    generated_utc = $generatedUtc
    provider = "comfyui"
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
    schema = "lit_iso.asset_forge.comfy_worker_result.v1"
    status = "review_pack_ready"
    job_name = $job
    asset_mode = $mode
    provider = "comfyui"
    generated_utc = $generatedUtc
    request_path = Convert-ToRepoPath $requestFull
    review_root = Convert-ToRepoPath $reviewRoot
    raw_output_root = Convert-ToRepoPath $rawRoot
    cleaned_output_root = Convert-ToRepoPath $cleanRoot
    comfy_generation_manifest = Convert-ToRepoPath $manifestPath
    style_provenance = $styleProvenanceRepoPath
    proper_pixel_art_report = $properPixelArtReportPath
    proper_pixel_art_contact_sheet = $properPixelArtContactSheetPath
    generated_files = @($items | ForEach-Object { $_.path })
    summary = [ordered]@{ pass = $passCount; review = $reviewCount; total = $items.Count }
}
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $reviewRoot "generation_manifest.json") -Encoding UTF8

Update-RequestStatus -StatusPath $statusPath -Fields @{
    ok = $true
    status = "review_pack_ready"
    provider = "comfyui"
    job_name = $job
    review_root = Convert-ToRepoPath $reviewRoot
    review_report = "Assets/Generated/_Review/$job/review_report.json"
    review_decisions = "Assets/Generated/_Review/$job/review_decisions.json"
    comfy_generation_manifest = Convert-ToRepoPath $manifestPath
    style_provenance = $styleProvenanceRepoPath
    proper_pixel_art_report = $properPixelArtReportPath
    proper_pixel_art_contact_sheet = $properPixelArtContactSheetPath
}

[PSCustomObject]@{
    ok = $true
    status = "review_pack_ready"
    job_name = $job
    asset_mode = $mode
    provider = "comfyui"
    review_root = Convert-ToRepoPath $reviewRoot
    generated_files = $items.Count
    pass = $passCount
    review = $reviewCount
    review_report = "Assets/Generated/_Review/$job/review_report.json"
    review_decisions = "Assets/Generated/_Review/$job/review_decisions.json"
    comfy_generation_manifest = Convert-ToRepoPath $manifestPath
    style_provenance = $styleProvenanceRepoPath
    proper_pixel_art_report = $properPixelArtReportPath
    proper_pixel_art_contact_sheet = $properPixelArtContactSheetPath
} | ConvertTo-Json -Depth 10
