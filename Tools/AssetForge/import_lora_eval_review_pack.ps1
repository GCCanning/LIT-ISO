param(
    [Parameter(Mandatory = $true)][string]$InputPath,
    [string]$ProjectRoot,
    [string]$PackName = "LoRAEvalReview",
    [ValidateSet("auto", "terrain", "decoration")][string]$Category = "auto",
    [switch]$ReplaceExisting,
    [switch]$SkipStrictQA,
    [switch]$NoGallery
)

$ErrorActionPreference = "Stop"

if (-not $ProjectRoot) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

function Get-RelativePath([string]$BasePath, [string]$TargetPath) {
    $base = [Uri]((Resolve-Path $BasePath).Path.TrimEnd("\") + "\")
    $target = [Uri]([IO.Path]::GetFullPath($TargetPath))
    return [Uri]::UnescapeDataString($base.MakeRelativeUri($target).ToString()).Replace("/", "\")
}

function ConvertTo-RepoPath([string]$Path) {
    return (Get-RelativePath $ProjectRoot $Path).Replace("\", "/")
}

function Assert-ChildPath {
    param(
        [string]$ParentPath,
        [string]$ChildPath,
        [string]$Description
    )

    $parentFull = [IO.Path]::GetFullPath($ParentPath).TrimEnd("\", "/")
    $childFull = [IO.Path]::GetFullPath($ChildPath).TrimEnd("\", "/")
    $prefix = $parentFull + [IO.Path]::DirectorySeparatorChar
    if (-not ($childFull.Equals($parentFull, [StringComparison]::OrdinalIgnoreCase) -or $childFull.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase))) {
        throw "$Description must stay inside $parentFull. Resolved path: $childFull"
    }
}

function Get-SafeName([string]$Name) {
    $safe = $Name -replace "[^A-Za-z0-9_.-]", "_"
    $safe = $safe.Trim("._")
    if ([string]::IsNullOrWhiteSpace($safe)) { return "asset" }
    return $safe
}

function Get-InferredCategory([IO.FileInfo]$File) {
    if ($Category -ne "auto") { return $Category }
    $haystack = ($File.FullName + " " + $File.BaseName).ToLowerInvariant().Replace("\", "/")
    if ($haystack -match "terrain|terrains|tile|tiles|ground|floor|dirt|sand|snow|plains|forest|grass|path|water|coast|beach|swamp|mountain|cave") {
        return "terrain"
    }
    if ($haystack -match "prop|props|decor|decoration|decorations|tree|bush|rock|stump|flower|grassclump|log|crate|barrel|fence") {
        return "decoration"
    }
    return "terrain"
}

function Get-InferredBiome([IO.FileInfo]$File) {
    $haystack = ($File.FullName + " " + $File.BaseName).ToLowerInvariant().Replace("\", "/")
    foreach ($biome in @("plains", "forest", "desert", "snow", "swamp", "mountain", "coast", "beach", "cave", "farm")) {
        if ($haystack -match "(^|[^a-z])$biome([^a-z]|$)") { return $biome }
    }
    return "generic"
}

function Copy-VersionedPng([IO.FileInfo]$Source, [string]$DestinationFolder, [string]$Prefix) {
    $baseName = Get-SafeName $Source.BaseName
    $candidate = Join-Path $DestinationFolder ("{0}_{1}{2}" -f $Prefix, $baseName, $Source.Extension.ToLowerInvariant())
    if ($ReplaceExisting -or -not (Test-Path $candidate)) {
        Copy-Item -LiteralPath $Source.FullName -Destination $candidate -Force
        return $candidate
    }
    for ($i = 2; $i -lt 1000; $i++) {
        $versioned = Join-Path $DestinationFolder ("{0}_{1}_v{2}{3}" -f $Prefix, $baseName, $i, $Source.Extension.ToLowerInvariant())
        if (-not (Test-Path $versioned)) {
            Copy-Item -LiteralPath $Source.FullName -Destination $versioned -Force
            return $versioned
        }
    }
    throw "Could not find a free destination name for $($Source.FullName)"
}

function Read-PngFacts([string]$Path) {
    try {
        Add-Type -AssemblyName System.Drawing -ErrorAction Stop
        $img = [System.Drawing.Bitmap]::FromFile($Path)
        try {
            return [PSCustomObject]@{ width = $img.Width; height = $img.Height }
        } finally {
            $img.Dispose()
        }
    } catch {
        return [PSCustomObject]@{ width = $null; height = $null }
    }
}

function Write-HtmlGallery([string]$GalleryPath, [array]$Items) {
    $cards = foreach ($item in $Items) {
        $src = [Security.SecurityElement]::Escape($item.id)
        $name = [Security.SecurityElement]::Escape($item.name)
        $decision = [Security.SecurityElement]::Escape($item.default_decision)
        $categoryName = [Security.SecurityElement]::Escape($item.category)
        $issueText = [Security.SecurityElement]::Escape((@($item.issues + $item.warnings) -join ", "))
        @"
        <article class="card $($item.default_decision)">
            <img src="$src" alt="$name">
            <div class="meta">
                <strong>$name</strong>
                <span>$categoryName / $decision</span>
                <small>$issueText</small>
            </div>
        </article>
"@
    }
    $html = @"
<!doctype html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>$PackName Review Gallery</title>
    <style>
        body { margin: 0; font-family: Segoe UI, Arial, sans-serif; color: #e8edf0; background: #151819; }
        header { padding: 20px 24px; border-bottom: 1px solid #31383b; }
        h1 { margin: 0 0 6px; font-size: 24px; font-weight: 650; }
        p { margin: 0; color: #aeb9bd; }
        main { display: grid; grid-template-columns: repeat(auto-fill, minmax(180px, 1fr)); gap: 14px; padding: 18px; }
        .card { background: #202527; border: 1px solid #3a4245; border-radius: 6px; overflow: hidden; }
        .card.needs_edit { border-color: #a9664c; }
        .card img { display: block; width: 100%; aspect-ratio: 1; object-fit: contain; image-rendering: pixelated; background: #101314; }
        .meta { display: grid; gap: 5px; padding: 10px; }
        strong, span, small { overflow-wrap: anywhere; }
        span { color: #cad3d6; font-size: 13px; }
        small { color: #aeb9bd; min-height: 18px; }
    </style>
</head>
<body>
    <header>
        <h1>$PackName</h1>
        <p>Local Asset Forge review gallery. Edit review_decisions.json to approve final assets.</p>
    </header>
    <main>
$($cards -join "`n")
    </main>
</body>
</html>
"@
    Set-Content -Path $GalleryPath -Value $html -Encoding UTF8
}

function Convert-StrictQaPathToId([string]$QaPath, [string]$ReviewFullPath) {
    $normalized = $QaPath.Replace("\", "/")
    $reviewNormalized = $ReviewFullPath.Replace("\", "/").TrimEnd("/")
    $projectNormalized = $rootFull.Replace("\", "/").TrimEnd("/")
    $packPrefix = "Assets/Generated/_Review/$PackName/"

    if ($normalized.StartsWith($reviewNormalized + "/", [StringComparison]::OrdinalIgnoreCase)) {
        return $normalized.Substring($reviewNormalized.Length + 1)
    }
    if ($normalized.StartsWith($projectNormalized + "/", [StringComparison]::OrdinalIgnoreCase)) {
        $normalized = $normalized.Substring($projectNormalized.Length + 1)
    }
    if ($normalized.StartsWith($packPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        return $normalized.Substring($packPrefix.Length)
    }

    return $null
}

$rootFull = [IO.Path]::GetFullPath((Resolve-Path $ProjectRoot).Path)
$safePackName = Get-SafeName $PackName
if ($safePackName -ne $PackName) {
    throw "PackName must contain only letters, numbers, underscore, dot, or dash, and cannot start/end with dot or underscore. Got '$PackName'; safe form would be '$safePackName'."
}

$inputResolved = Resolve-Path -LiteralPath $InputPath -ErrorAction SilentlyContinue
if (-not $inputResolved) {
    throw "InputPath does not exist: $InputPath. Provide a folder containing LoRA eval PNG outputs."
}
$inputItem = Get-Item $inputResolved
if (-not $inputItem.PSIsContainer) { throw "InputPath must be a folder of LoRA eval PNG outputs, not a file: $($inputItem.FullName)" }

$reviewParent = Join-Path $ProjectRoot "Assets\Generated\_Review"
$reviewRoot = Join-Path $reviewParent $PackName
$reviewFull = [IO.Path]::GetFullPath($reviewRoot)
Assert-ChildPath -ParentPath $reviewParent -ChildPath $reviewFull -Description "Review pack root"
Assert-ChildPath -ParentPath (Join-Path $ProjectRoot "Assets\Generated\_Review\$PackName") -ChildPath $reviewFull -Description "Review pack delete target"
if ((Test-Path $reviewRoot) -and $ReplaceExisting) {
    Remove-Item -LiteralPath $reviewRoot -Recurse -Force
}
New-Item -ItemType Directory -Force $reviewRoot | Out-Null

if (-not $reviewFull.StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Review pack root must stay inside repo: $reviewFull"
}

$sourceFiles = @(Get-ChildItem -Path $inputItem.FullName -Recurse -Filter *.png -File | Where-Object {
    $_.FullName.Replace("\", "/") -notmatch "/_Preview/" -and $_.Name -notmatch "contact_sheet"
} | Sort-Object FullName)
if ($sourceFiles.Count -eq 0) { throw "No PNG files found under $($inputItem.FullName). Add PNG outputs or choose a different InputPath; _Preview folders and contact sheets are ignored." }

$items = New-Object System.Collections.Generic.List[object]
$index = 0
foreach ($file in $sourceFiles) {
    $index++
    $categoryName = Get-InferredCategory $file
    $biome = Get-InferredBiome $file
    $categoryFolder = if ($categoryName -eq "decoration") { "Decorations" } else { "Terrain" }
    $copyRoot = Join-Path $reviewRoot $categoryFolder
    New-Item -ItemType Directory -Force $copyRoot | Out-Null
    $dest = Copy-VersionedPng $file $copyRoot ("{0:D3}" -f $index)
    $facts = Read-PngFacts $dest
    $repoPath = ConvertTo-RepoPath $dest
    $id = $repoPath -replace "^Assets/Generated/_Review/$([Regex]::Escape($PackName))/", ""
    $destinationRoot = if ($categoryName -eq "decoration") { "Assets/Generated/Props" } else { "Assets/Generated/Tiles" }
    $destinationPath = "$destinationRoot/$biome/$([IO.Path]::GetFileName($dest))"
    $items.Add([PSCustomObject]@{
        id = $id
        name = [IO.Path]::GetFileName($dest)
        path = $repoPath
        source_input_path = $file.FullName
        category = $categoryName
        biome = $biome
        width = $facts.width
        height = $facts.height
        status = "pending_qa"
        issues = @()
        warnings = @()
        default_decision = "pending"
        destination_path = $destinationPath
    })
}

$strictReportPath = Join-Path $reviewRoot "strict_asset_quality_report.json"
$strictRan = $false
$strictError = $null
$strictScript = Join-Path $PSScriptRoot "test_strict_asset_quality.ps1"
if (-not $SkipStrictQA -and (Test-Path $strictScript)) {
    try {
        $strictCategory = if ($Category -eq "decoration") { "prop" } elseif ($Category -eq "terrain") { "terrain" } else { "auto" }
        $strictArgs = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $strictScript, "-InputPath", $reviewRoot, "-OutputPath", $strictReportPath, "-Category", $strictCategory)
        powershell.exe @strictArgs | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Strict QA exited with code $LASTEXITCODE" }
        $strictRan = $true
    } catch {
        $strictError = $_.Exception.Message
    }
}

if ($strictRan -and (Test-Path $strictReportPath)) {
    $strict = Get-Content -Raw $strictReportPath | ConvertFrom-Json
    $strictById = @{}
    foreach ($qa in @($strict.items)) {
        $qaId = Convert-StrictQaPathToId ([string]$qa.path) $reviewFull
        if ($qaId) {
            $strictById[$qaId] = $qa
        }
    }
    foreach ($item in $items) {
        if (-not $strictById.ContainsKey($item.id)) {
            $item.status = "qa_unmatched"
            $item.warnings = @(@($item.warnings) + "strict_qa_missing")
            $item.default_decision = "needs_edit"
            continue
        }
        $qa = $strictById[$item.id]
        $item.status = $qa.status
        $item.issues = @($qa.issues)
        $item.warnings = @($qa.warnings)
        $item.width = $qa.width
        $item.height = $qa.height
        if (@($qa.issues).Count -gt 0 -or @($qa.warnings).Count -gt 0 -or $qa.status -ne "pass") {
            $item.default_decision = "needs_edit"
        } else {
            $item.default_decision = "pending"
        }
    }
} elseif ($strictError) {
    foreach ($item in $items) {
        $item.status = "qa_unavailable"
        $item.warnings = @("strict_qa_unavailable")
        $item.default_decision = "needs_edit"
    }
}

$reportItems = @($items | ForEach-Object {
    [PSCustomObject]@{
        id = $_.id
        name = $_.name
        path = $_.path
        source_input_path = $_.source_input_path
        category = $_.category
        biome = $_.biome
        width = $_.width
        height = $_.height
        status = $_.status
        issues = @($_.issues)
        warnings = @($_.warnings)
        default_decision = $_.default_decision
        destination_path = $_.destination_path
    }
})

$report = [ordered]@{
    pack_name = $PackName
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    input_path = $inputItem.FullName
    strict_qa = [ordered]@{
        requested = (-not $SkipStrictQA.IsPresent)
        ran = $strictRan
        report_path = if (Test-Path $strictReportPath) { ConvertTo-RepoPath $strictReportPath } else { $null }
        error = $strictError
    }
    total = $reportItems.Count
    pending_count = @($reportItems | Where-Object default_decision -eq "pending").Count
    needs_edit_count = @($reportItems | Where-Object default_decision -eq "needs_edit").Count
    items = $reportItems
}
$reportPath = Join-Path $reviewRoot "review_report.json"
$report | ConvertTo-Json -Depth 10 | Set-Content -Path $reportPath -Encoding UTF8

$decisions = [ordered]@{
    pack_name = $PackName
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    decisions = @($reportItems | ForEach-Object {
        [PSCustomObject]@{
            id = $_.id
            name = $_.name
            decision = $_.default_decision
            source_path = $_.path
            destination_path = $_.destination_path
            category = $_.category
            biome = $_.biome
            notes = if ($_.default_decision -eq "needs_edit") { (@($_.issues + $_.warnings) -join "; ") } else { "" }
        }
    })
}
$decisionsPath = Join-Path $reviewRoot "review_decisions.json"
$decisions | ConvertTo-Json -Depth 10 | Set-Content -Path $decisionsPath -Encoding UTF8

$galleryPath = $null
if (-not $NoGallery) {
    $galleryPath = Join-Path $reviewRoot "gallery.html"
    Write-HtmlGallery $galleryPath $reportItems
}

[PSCustomObject]@{
    review_root = ConvertTo-RepoPath $reviewRoot
    report = ConvertTo-RepoPath $reportPath
    decisions = ConvertTo-RepoPath $decisionsPath
    strict_qa_report = if (Test-Path $strictReportPath) { ConvertTo-RepoPath $strictReportPath } else { $null }
    gallery = if ($galleryPath) { ConvertTo-RepoPath $galleryPath } else { $null }
    total = $report.total
    pending = $report.pending_count
    needs_edit = $report.needs_edit_count
    strict_qa_ran = $strictRan
} | ConvertTo-Json -Depth 4
