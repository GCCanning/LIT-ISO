param(
    [int]$Port = 4191,
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"

function Get-ContentType([string]$Path) {
    switch ([IO.Path]::GetExtension($Path).ToLowerInvariant()) {
        ".html" { "text/html; charset=utf-8" }
        ".js" { "application/javascript; charset=utf-8" }
        ".css" { "text/css; charset=utf-8" }
        ".json" { "application/json; charset=utf-8" }
        ".png" { "image/png" }
        ".jpg" { "image/jpeg" }
        ".jpeg" { "image/jpeg" }
        default { "application/octet-stream" }
    }
}

function New-Response([int]$Code, [string]$Text, [string]$ContentType, [byte[]]$Body) {
    $header = "HTTP/1.1 $Code $Text`r`nContent-Type: $ContentType`r`nContent-Length: $($Body.Length)`r`nAccess-Control-Allow-Origin: *`r`nAccess-Control-Allow-Methods: GET, POST, OPTIONS`r`nAccess-Control-Allow-Headers: Content-Type`r`nConnection: close`r`n`r`n"
    $headerBytes = [Text.Encoding]::ASCII.GetBytes($header)
    $response = New-Object byte[] ($headerBytes.Length + $Body.Length)
    [Buffer]::BlockCopy($headerBytes, 0, $response, 0, $headerBytes.Length)
    [Buffer]::BlockCopy($Body, 0, $response, $headerBytes.Length, $Body.Length)
    return $response
}

function New-JsonResponse([int]$Code, [object]$Payload) {
    $text = switch ($Code) {
        200 { "OK" }
        400 { "Bad Request" }
        404 { "Not Found" }
        405 { "Method Not Allowed" }
        default { "Internal Server Error" }
    }
    $json = $Payload | ConvertTo-Json -Depth 12
    New-Response $Code $text "application/json; charset=utf-8" ([Text.Encoding]::UTF8.GetBytes($json))
}

function Get-BodyText([string]$RequestText, [Net.Sockets.NetworkStream]$Stream) {
    $parts = $RequestText -split "`r`n`r`n", 2
    $headers = $parts[0]
    $body = if ($parts.Length -gt 1) { $parts[1] } else { "" }
    $contentLength = 0
    foreach ($line in ($headers -split "`r?`n")) {
        if ($line -match "^Content-Length:\s*(\d+)\s*$") { $contentLength = [int]$Matches[1] }
    }
    while ([Text.Encoding]::UTF8.GetByteCount($body) -lt $contentLength) {
        $buffer = New-Object byte[] 8192
        $read = $Stream.Read($buffer, 0, $buffer.Length)
        if ($read -le 0) { break }
        $body += [Text.Encoding]::UTF8.GetString($buffer, 0, $read)
    }
    return $body
}

function ConvertFrom-BodyJson([string]$Body) {
    if ([string]::IsNullOrWhiteSpace($Body)) { return [PSCustomObject]@{} }
    try { return $Body | ConvertFrom-Json } catch { throw "Invalid JSON body: $($_.Exception.Message)" }
}

function Get-SafeName([object]$Body, [string]$CamelName, [string]$Default) {
    $value = $null
    if ($Body.PSObject.Properties.Name.Contains($CamelName) -and -not [string]::IsNullOrWhiteSpace([string]$Body.$CamelName)) {
        $value = [string]$Body.$CamelName
    }
    else {
        $snake = [Regex]::Replace($CamelName, "([a-z0-9])([A-Z])", '$1_$2').ToLowerInvariant()
        if ($Body.PSObject.Properties.Name.Contains($snake) -and -not [string]::IsNullOrWhiteSpace([string]$Body.$snake)) {
            $value = [string]$Body.$snake
        }
    }
    if ([string]::IsNullOrWhiteSpace($value)) { return $Default }
    if ($value -notmatch "^[A-Za-z0-9_.-]+$") { throw "$CamelName may only contain letters, numbers, dot, underscore, and dash" }
    return $value
}

function Get-BodyValue([object]$Body, [string]$CamelName, [object]$Default) {
    if (-not $Body) { return $Default }
    if ($Body.PSObject.Properties.Name.Contains($CamelName) -and $null -ne $Body.$CamelName) {
        return $Body.$CamelName
    }

    $snake = [Regex]::Replace($CamelName, "([a-z0-9])([A-Z])", '$1_$2').ToLowerInvariant()
    if ($Body.PSObject.Properties.Name.Contains($snake) -and $null -ne $Body.$snake) {
        return $Body.$snake
    }

    return $Default
}

function Assert-UnderRoot {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $pathFull = [IO.Path]::GetFullPath($Path).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    if ($pathFull -ne $rootFull -and -not $pathFull.StartsWith($rootFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label must stay inside ProjectRoot. Root: $rootFull Path: $pathFull"
    }
}

function ConvertTo-ProjectRelativePath([string]$Path) {
    $root = [IO.Path]::GetFullPath($ProjectRoot).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $full = [IO.Path]::GetFullPath($Path)
    if ($full.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        return $full.Substring($root.Length + 1).Replace("\", "/")
    }
    return $full.Replace("\", "/")
}

function ConvertTo-PlainMap([object]$Body) {
    $map = [ordered]@{}
    if (-not $Body) { return $map }

    foreach ($property in $Body.PSObject.Properties) {
        $map[$property.Name] = $property.Value
    }

    return $map
}

function Quote-Arg([string]$Arg) {
    if ($Arg -match '^[A-Za-z0-9_./:\\-]+$') { return $Arg }
    return '"' + ($Arg -replace '"', '\"') + '"'
}

function Invoke-FixedScript([string]$Script, [string[]]$Arguments) {
    if (-not (Test-Path $Script)) { throw "Missing script: $Script" }
    $psi = [Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = "powershell.exe"
    $allArgs = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $Script) + $Arguments
    $psi.Arguments = ($allArgs | ForEach-Object { Quote-Arg $_ }) -join " "
    $psi.WorkingDirectory = $ProjectRoot
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($psi)
    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    return [ordered]@{ ok = ($process.ExitCode -eq 0); exitCode = $process.ExitCode; stdout = $stdout; stderr = $stderr }
}

function Convert-ScriptResultPayload([object]$ScriptResult) {
    $payload = [ordered]@{
        ok = $ScriptResult.ok
        exitCode = $ScriptResult.exitCode
        stdout = $ScriptResult.stdout
        stderr = $ScriptResult.stderr
    }

    if (-not [string]::IsNullOrWhiteSpace([string]$ScriptResult.stdout)) {
        try {
            $parsed = $ScriptResult.stdout | ConvertFrom-Json
            $payload["result"] = $parsed
            foreach ($property in $parsed.PSObject.Properties) {
                if (-not $payload.Contains($property.Name)) {
                    $payload[$property.Name] = $property.Value
                }
            }
        }
        catch {
            $payload["parseWarning"] = "Script stdout was not JSON: $($_.Exception.Message)"
        }
    }

    return $payload
}

function Get-AssetForgeConfig {
    $configPath = Join-Path $ProjectRoot "Tools\AssetForge\asset_forge.local.json"
    if (-not (Test-Path $configPath)) {
        $configPath = Join-Path $ProjectRoot "Tools\AssetForge\asset_forge.local.example.json"
    }
    if (-not (Test-Path $configPath)) { return $null }
    try { return Get-Content -Raw -LiteralPath $configPath | ConvertFrom-Json } catch { return $null }
}

function Get-StyleProfiles {
    $profileRoot = Join-Path $ProjectRoot "Assets\Generated\_StyleProfiles"
    $profiles = @()
    if (Test-Path $profileRoot) {
        $profiles = @(Get-ChildItem -LiteralPath $profileRoot -Directory | ForEach-Object {
            $profilePath = Join-Path $_.FullName "style_profile.json"
            if (-not (Test-Path $profilePath)) { return }

            try {
                $profile = Get-Content -Raw -LiteralPath $profilePath | ConvertFrom-Json
                $modeNames = @()
                if ($profile.prompt_defaults -and $profile.prompt_defaults.by_asset_mode) {
                    $modeNames = @($profile.prompt_defaults.by_asset_mode.PSObject.Properties.Name)
                }
                $referenceRoot = Join-Path $_.FullName "references"
                $referenceFiles = if (Test-Path $referenceRoot) { @(Get-ChildItem -LiteralPath $referenceRoot -Recurse -File | Where-Object { $_.Extension -match "^\.(png|jpg|jpeg)$" }) } else { @() }
                [ordered]@{
                    id = [string]$profile.profile_id
                    displayName = [string]$profile.display_name
                    version = [string]$profile.version
                    status = [string]$profile.status
                    path = ("Assets/Generated/_StyleProfiles/{0}/style_profile.json" -f $_.Name)
                    assetModes = $modeNames
                    referenceCount = $referenceFiles.Count
                    references = @($referenceFiles | Select-Object -First 40 | ForEach-Object {
                        [ordered]@{
                            path = $_.FullName.Substring($ProjectRoot.Length + 1).Replace("\", "/")
                            name = $_.Name
                            updatedUtc = $_.LastWriteTimeUtc.ToString("o")
                        }
                    })
                    profile = $profile
                }
            }
            catch {
                [ordered]@{
                    id = $_.Name
                    displayName = $_.Name
                    version = ""
                    status = "invalid"
                    path = ("Assets/Generated/_StyleProfiles/{0}/style_profile.json" -f $_.Name)
                    error = $_.Exception.Message
                }
            }
        })
    }

    return [ordered]@{
        ok = $true
        root = "Assets/Generated/_StyleProfiles"
        count = $profiles.Count
        profiles = $profiles
    }
}

function Get-ReviewPackSummary([IO.DirectoryInfo]$Directory) {
    $reportPath = Join-Path $Directory.FullName "review_report.json"
    if (-not (Test-Path $reportPath)) { return $null }

    $decisionsPath = Join-Path $Directory.FullName "review_decisions.json"
    $strictPath = Join-Path $Directory.FullName "strict_asset_quality_report.json"
    try {
        $report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
        $items = @($report.items)
        $total = if ($null -ne $report.total) { [int]$report.total } else { $items.Count }
        $pass = if ($null -ne $report.pass_count) { [int]$report.pass_count } else { @($items | Where-Object { $_.status -eq "pass" }).Count }
        $review = if ($null -ne $report.review_count) { [int]$report.review_count } else { @($items | Where-Object { $_.status -ne "pass" }).Count }
        return [ordered]@{
            name = $Directory.Name
            updatedUtc = $Directory.LastWriteTimeUtc.ToString("o")
            reportPath = ConvertTo-ProjectRelativePath $reportPath
            decisionsPath = if (Test-Path $decisionsPath) { ConvertTo-ProjectRelativePath $decisionsPath } else { "" }
            strictQaPath = if (Test-Path $strictPath) { ConvertTo-ProjectRelativePath $strictPath } else { "" }
            provider = [string]$report.provider
            assetMode = [string]$report.asset_mode
            total = $total
            pass = $pass
            review = $review
        }
    }
    catch {
        return [ordered]@{
            name = $Directory.Name
            updatedUtc = $Directory.LastWriteTimeUtc.ToString("o")
            reportPath = ConvertTo-ProjectRelativePath $reportPath
            status = "invalid"
            error = $_.Exception.Message
        }
    }
}

function Get-ReviewPacks {
    $reviewRoot = Join-Path $ProjectRoot "Assets\Generated\_Review"
    $packs = @()
    if (Test-Path $reviewRoot) {
        $packs = @(Get-ChildItem -LiteralPath $reviewRoot -Directory |
            Where-Object { $_.Name -ne "_Requests" } |
            Sort-Object LastWriteTimeUtc -Descending |
            ForEach-Object { Get-ReviewPackSummary $_ } |
            Where-Object { $null -ne $_ })
    }

    return [ordered]@{
        ok = $true
        root = "Assets/Generated/_Review"
        count = $packs.Count
        packs = $packs
    }
}

function Get-ReviewPack([object]$Body) {
    $pack = Get-SafeName $Body "packName" "CodexBiomeStarter"
    $reviewRoot = Join-Path $ProjectRoot "Assets\Generated\_Review\$pack"
    $projectRootResolved = (Resolve-Path -LiteralPath $ProjectRoot).Path
    Assert-UnderRoot -Root $projectRootResolved -Path $reviewRoot -Label "ReviewPackRoot"
    if (-not (Test-Path $reviewRoot)) { throw "Review pack does not exist: $pack" }

    $reportPath = Join-Path $reviewRoot "review_report.json"
    $decisionsPath = Join-Path $reviewRoot "review_decisions.json"
    $strictPath = Join-Path $reviewRoot "strict_asset_quality_report.json"
    if (-not (Test-Path $reportPath)) { throw "Review pack is missing review_report.json: $pack" }

    $report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
    $decisions = if (Test-Path $decisionsPath) { Get-Content -Raw -LiteralPath $decisionsPath | ConvertFrom-Json } else { $null }
    $strict = if (Test-Path $strictPath) { Get-Content -Raw -LiteralPath $strictPath | ConvertFrom-Json } else { $null }
    return [ordered]@{
        ok = $true
        packName = $pack
        root = ConvertTo-ProjectRelativePath $reviewRoot
        reportPath = ConvertTo-ProjectRelativePath $reportPath
        decisionsPath = if (Test-Path $decisionsPath) { ConvertTo-ProjectRelativePath $decisionsPath } else { "" }
        strictQaPath = if (Test-Path $strictPath) { ConvertTo-ProjectRelativePath $strictPath } else { "" }
        report = $report
        decisions = $decisions
        strict = $strict
    }
}

function Import-StyleReference {
    param([object]$Body)

    $profileId = Get-SafeName $Body "profileId" "lit_iso_foundation_v1"
    $referenceId = Get-SafeName $Body "referenceId" ("ref_" + (Get-Date -Format "yyyyMMddHHmmss"))
    $assetMode = Get-SafeName $Body "assetMode" "shared"
    $sourcePath = [string](Get-BodyValue $Body "sourcePath" (Get-BodyValue $Body "source_path" ""))
    if ([string]::IsNullOrWhiteSpace($sourcePath)) { throw "sourcePath is required." }

    $profileRoot = Join-Path $ProjectRoot "Assets\Generated\_StyleProfiles\$profileId"
    Assert-UnderRoot -Root $ProjectRoot -Path $profileRoot -Label "StyleProfileRoot"
    if (-not (Test-Path (Join-Path $profileRoot "style_profile.json"))) {
        throw "Missing style profile: $profileId"
    }

    $resolvedSource = (Resolve-Path -LiteralPath $sourcePath).Path
    $ext = [IO.Path]::GetExtension($resolvedSource).ToLowerInvariant()
    if (@(".png", ".jpg", ".jpeg") -notcontains $ext) { throw "Only png/jpg/jpeg references are supported." }

    $destinationRoot = Join-Path $profileRoot "references\$assetMode"
    Assert-UnderRoot -Root $ProjectRoot -Path $destinationRoot -Label "ReferenceDestinationRoot"
    New-Item -ItemType Directory -Force -Path $destinationRoot | Out-Null

    $destination = Join-Path $destinationRoot "$referenceId$ext"
    Copy-Item -LiteralPath $resolvedSource -Destination $destination -Force
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $destination).Hash.ToLowerInvariant()

    $imageFacts = [ordered]@{ width = $null; height = $null; alpha = $null }
    try {
        Add-Type -AssemblyName System.Drawing -ErrorAction SilentlyContinue
        $img = [System.Drawing.Bitmap]::FromFile($destination)
        try {
            $imageFacts.width = $img.Width
            $imageFacts.height = $img.Height
            $imageFacts.alpha = ($img.PixelFormat.ToString() -match "Alpha|Argb|PArgb")
        }
        finally {
            $img.Dispose()
        }
    }
    catch { }

    $meta = [ordered]@{
        schema = "lit_iso.asset_forge.reference_image.v1"
        reference_id = $referenceId
        file_name = [IO.Path]::GetFileName($destination)
        added_utc = (Get-Date).ToUniversalTime().ToString("o")
        source_path = $resolvedSource
        source_type = [string](Get-BodyValue $Body "sourceType" "original_or_licensed_reference")
        source_uri = [string](Get-BodyValue $Body "sourceUri" "")
        license = [string](Get-BodyValue $Body "license" "project_internal_or_explicitly_licensed")
        author = [string](Get-BodyValue $Body "author" "LIT-ISO")
        allowed_use = @("palette", "shape_language", "prompt_reference")
        blocked_use = @("pixel_copy", "direct_derivative", "high_strength_img2img_copy")
        asset_modes = @($assetMode)
        tags = @(([string](Get-BodyValue $Body "tags" $assetMode)) -split "," | ForEach-Object { $_.Trim() } | Where-Object { $_ })
        clean_room_review = [ordered]@{
            reviewed_by = [string](Get-BodyValue $Body "reviewedBy" "human")
            reviewed_utc = (Get-Date).ToUniversalTime().ToString("o")
            decision = "approved_for_style_reference"
            notes = [string](Get-BodyValue $Body "notes" "Approved for broad style guidance only.")
        }
        technical = [ordered]@{
            width = $imageFacts.width
            height = $imageFacts.height
            alpha = $imageFacts.alpha
            sha256 = $hash
        }
    }
    $metaPath = "$destination.meta.json"
    $meta | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $metaPath -Encoding UTF8

    return [ordered]@{
        ok = $true
        status = "reference_imported"
        profile_id = $profileId
        reference_id = $referenceId
        asset_mode = $assetMode
        path = $destination.Substring($ProjectRoot.Length + 1).Replace("\", "/")
        metadata_path = $metaPath.Substring($ProjectRoot.Length + 1).Replace("\", "/")
        sha256 = $hash
    }
}

function Invoke-QuickJson {
    param(
        [Parameter(Mandatory = $true)][string]$Uri,
        [int]$TimeoutMs = 900
    )
    $request = [System.Net.WebRequest]::Create($Uri)
    $request.Method = "GET"
    $request.Timeout = $TimeoutMs
    $request.ReadWriteTimeout = $TimeoutMs
    $response = $null
    $reader = $null
    try {
        $response = $request.GetResponse()
        $reader = [IO.StreamReader]::new($response.GetResponseStream())
        return ($reader.ReadToEnd() | ConvertFrom-Json)
    }
    finally {
        if ($reader) { $reader.Dispose() }
        if ($response) { $response.Dispose() }
    }
}

function Get-ComfyStatus {
    $config = Get-AssetForgeConfig
    $url = "http://127.0.0.1:8188"
    if ($config -and $config.comfyui -and -not [string]::IsNullOrWhiteSpace([string]$config.comfyui.url)) {
        $url = [string]$config.comfyui.url
    }
    $url = $url.TrimEnd("/")

    $payload = [ordered]@{
        ok = $true
        reachable = $false
        url = $url
        queue_pending = $null
        queue_running = $null
        system = $null
        error = $null
    }

    try {
        $queue = Invoke-QuickJson -Uri "$url/queue" -TimeoutMs 900
        $payload.reachable = $true
        if ($queue.PSObject.Properties.Name.Contains("queue_pending")) { $payload.queue_pending = @($queue.queue_pending).Count }
        if ($queue.PSObject.Properties.Name.Contains("queue_running")) { $payload.queue_running = @($queue.queue_running).Count }
    }
    catch {
        $payload.error = $_.Exception.Message
        return $payload
    }

    try {
        $system = Invoke-QuickJson -Uri "$url/system_stats" -TimeoutMs 900
        $payload.system = $system
    }
    catch {
        $payload.system = [ordered]@{ warning = $_.Exception.Message }
    }

    return $payload
}

function Invoke-SprixenJson {
    param(
        [Parameter(Mandatory = $true)][string]$Uri,
        [Parameter(Mandatory = $true)][string]$ApiKey,
        [int]$TimeoutMs = 1600
    )
    $request = [System.Net.WebRequest]::Create($Uri)
    $request.Method = "GET"
    $request.Timeout = $TimeoutMs
    $request.ReadWriteTimeout = $TimeoutMs
    $request.Headers.Add("Authorization", "Bearer $ApiKey")
    $request.Accept = "application/json"
    $response = $null
    $reader = $null
    try {
        $response = $request.GetResponse()
        $reader = [IO.StreamReader]::new($response.GetResponseStream())
        return ($reader.ReadToEnd() | ConvertFrom-Json)
    }
    finally {
        if ($reader) { $reader.Dispose() }
        if ($response) { $response.Dispose() }
    }
}

function Get-SprixenStatus {
    $config = Get-AssetForgeConfig
    $sprixen = if ($config -and $config.sprixen) { $config.sprixen } else { [PSCustomObject]@{} }
    $baseUrl = [string](Get-BodyValue $sprixen "base_url" "https://api.sprixen.com/v1")
    $apiKeyEnv = [string](Get-BodyValue $sprixen "api_key_env" "SPRIXEN_API_KEY")
    $apiKey = [string](Get-BodyValue $sprixen "api_key" "")
    if ([string]::IsNullOrWhiteSpace($apiKey) -and $apiKeyEnv -match "^spx_") {
        $apiKey = $apiKeyEnv
        $apiKeyEnv = "SPRIXEN_API_KEY"
    }
    $envKey = [Environment]::GetEnvironmentVariable($apiKeyEnv)
    if ([string]::IsNullOrWhiteSpace($apiKey) -and -not [string]::IsNullOrWhiteSpace($envKey)) {
        $apiKey = $envKey
    }
    $projects = if ($sprixen -and $sprixen.projects) { $sprixen.projects } else { [PSCustomObject]@{} }
    $payload = [ordered]@{
        ok = $true
        configured = -not [string]::IsNullOrWhiteSpace($apiKey)
        base_url = $baseUrl
        api_key_env = $apiKeyEnv
        projects = $projects
        billing = $null
        error = $null
        note = "Key is never returned by this endpoint. Sprixen is optional source material; local review/QA remains mandatory."
    }
    if ([string]::IsNullOrWhiteSpace($apiKey)) {
        $payload.error = "No Sprixen key configured. Set $apiKeyEnv or Tools/AssetForge/asset_forge.local.json:sprixen.api_key."
        return $payload
    }

    try {
        $payload.billing = Invoke-SprixenJson -Uri ($baseUrl.TrimEnd("/") + "/billing/status") -ApiKey $apiKey -TimeoutMs 1600
    }
    catch {
        $payload.error = $_.Exception.Message
    }
    return $payload
}

function Save-GenerationRequest([object]$Body) {
    $pack = Get-SafeName $Body "packName" "GeneratorLab"
    $job = Get-SafeName $Body "jobName" "asset_job"
    $assetMode = [string](Get-BodyValue $Body "assetMode" "character")
    $directions = [string](Get-BodyValue $Body "directionSet" (Get-BodyValue $Body "directions" "none"))
    $animation = Get-BodyValue $Body "animation" ([ordered]@{ name = "none"; frame_count = 1; fps = 0; loop = $false })
    $prompt = [string](Get-BodyValue $Body "prompt" "")
    $negativePrompt = [string](Get-BodyValue $Body "negativePrompt" (Get-BodyValue $Body "negative_prompt" ""))
    $seed = [string](Get-BodyValue $Body "seed" "random")
    $style = Get-BodyValue $Body "style" $null

    $projectRootResolved = (Resolve-Path -LiteralPath $ProjectRoot).Path
    $requestParent = Join-Path $ProjectRoot "Assets\Generated\_Review\_Requests"
    $requestRoot = Join-Path $requestParent $job
    Assert-UnderRoot -Root $projectRootResolved -Path $requestRoot -Label "RequestRoot"

    $inputsRoot = Join-Path $requestRoot "Inputs"
    $outputsRoot = Join-Path $requestRoot "Outputs"
    $reviewRoot = Join-Path $requestRoot "Review"
    New-Item -ItemType Directory -Force -Path $inputsRoot, $outputsRoot, $reviewRoot | Out-Null

    $relativeRequestRoot = "Assets/Generated/_Review/_Requests/$job"
    $relativeReviewOutput = "Assets/Generated/_Review/$job"
    $relativeStyleSnapshot = "$relativeRequestRoot/Inputs/style_profile.snapshot.json"
    $savedUtc = (Get-Date).ToUniversalTime().ToString("o")

    $request = [ordered]@{
        schema = "lit_iso.asset_forge.generation_request.v1"
        saved_utc = $savedUtc
        pack_name = $pack
        job_name = $job
        asset_mode = $assetMode
        provider = [string](Get-BodyValue $Body "provider" "comfyui")
        prompt = $prompt
        negative_prompt = $negativePrompt
        reference_image = [string](Get-BodyValue $Body "referenceImage" (Get-BodyValue $Body "reference_image" ""))
        seed = $seed
        directions = $directions
        canonical_direction_order = @("S", "SE", "E", "NE", "N", "NW", "W", "SW")
        animation = $animation
        batch_count = [int](Get-BodyValue $Body "batchCount" (Get-BodyValue $Body "batch_count" 1))
        asset_spec = Get-BodyValue $Body "assetSpec" (Get-BodyValue $Body "asset_spec" ([ordered]@{}))
        canvas = Get-BodyValue $Body "canvas" ([ordered]@{})
        unity_import = Get-BodyValue $Body "unityImport" (Get-BodyValue $Body "unity_import" ([ordered]@{}))
        control_guidance = Get-BodyValue $Body "controlGuidance" (Get-BodyValue $Body "control_guidance" ([ordered]@{}))
        clips = Get-BodyValue $Body "clips" @()
        post_process = Get-BodyValue $Body "postProcess" (Get-BodyValue $Body "post_process" @("background_remove", "sprite_fusion_snap", "palette_cap", "nearest_neighbor_resize", "fixed_canvas_normalize", "anchor_lock", "qa_report"))
        acceptance_checks = Get-BodyValue $Body "acceptanceChecks" (Get-BodyValue $Body "acceptance_checks" @("transparent_png", "manifest_ready_for_unity"))
        output_intent = Get-BodyValue $Body "outputIntent" (Get-BodyValue $Body "output_intent" ([ordered]@{}))
        clean_room_note = "Original LIT-ISO generation request. Do not copy protected game pixels, audio, names, or content."
        raw_request = ConvertTo-PlainMap $Body
    }
    if ($null -ne $style) {
        $request["style"] = $style
        $request["style_snapshot_path"] = $relativeStyleSnapshot
    }

    $worker = [ordered]@{
        schema = "lit_iso.asset_forge.worker_queue_item.v1"
        status = "queued"
        saved_utc = $savedUtc
        job_name = $job
        asset_mode = $assetMode
        provider = $request.provider
        request_path = "$relativeRequestRoot/generation_request.json"
        request_root = $relativeRequestRoot
        intended_review_pack_root = $relativeReviewOutput
        style_snapshot_path = if ($null -ne $style) { $relativeStyleSnapshot } else { "" }
        expected_outputs = [ordered]@{
            raw = "$relativeRequestRoot/Outputs/raw"
            cleaned = "$relativeRequestRoot/Outputs/cleaned"
            review_report = "$relativeReviewOutput/review_report.json"
            review_decisions = "$relativeReviewOutput/review_decisions.json"
            strict_qa = "$relativeReviewOutput/strict_asset_quality_report.json"
        }
        worker_steps = @(
            "load generation_request.json",
            "run ComfyUI/provider generation for the requested mode",
            "run deterministic cleanup and Sprite Fusion snap",
            "pack sheets/contact sheets/previews",
            "write review_report.json and review_decisions.json",
            "run strict_asset_quality.ps1 before approval"
        )
    }

    $status = [ordered]@{
        ok = $true
        status = "queued"
        saved_utc = $savedUtc
        job_name = $job
        asset_mode = $assetMode
        request_root = $relativeRequestRoot
        intended_review_pack_root = $relativeReviewOutput
        next_step = "Run the Asset Forge ComfyUI worker against worker_queue_item.json."
    }

    $placeholderReport = [ordered]@{
        schema = "lit_iso.asset_forge.review_report.v1"
        pack_name = $job
        generated_utc = $savedUtc
        status = "pending_generation"
        request_path = "$relativeRequestRoot/generation_request.json"
        total = 0
        pass_count = 0
        review_count = 0
        items = @()
    }

    $placeholderDecisions = [ordered]@{
        schema = "lit_iso.asset_forge.review_decisions.v1"
        pack_name = $job
        generated_utc = $savedUtc
        status = "pending_generation"
        decisions = @()
    }

    $readme = @"
# Asset Forge Generation Request

Job: $job
Mode: $assetMode
Status: queued

This folder is a local worker handoff. It does not contain generated assets yet.

Next step: run the Asset Forge ComfyUI worker against `worker_queue_item.json`.
The worker should create a review pack under `$relativeReviewOutput`.
"@

    $requestPath = Join-Path $requestRoot "generation_request.json"
    $styleSnapshotPath = Join-Path $inputsRoot "style_profile.snapshot.json"
    $workerPath = Join-Path $requestRoot "worker_queue_item.json"
    $statusPath = Join-Path $requestRoot "request_status.json"
    $reportPath = Join-Path $reviewRoot "review_report.placeholder.json"
    $decisionsPath = Join-Path $reviewRoot "review_decisions.placeholder.json"

    $request | ConvertTo-Json -Depth 24 | Set-Content -LiteralPath $requestPath -Encoding UTF8
    if ($null -ne $style) {
        $style | ConvertTo-Json -Depth 24 | Set-Content -LiteralPath $styleSnapshotPath -Encoding UTF8
    }
    $worker | ConvertTo-Json -Depth 24 | Set-Content -LiteralPath $workerPath -Encoding UTF8
    $status | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $statusPath -Encoding UTF8
    $placeholderReport | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8
    $placeholderDecisions | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $decisionsPath -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $requestRoot "README.md") -Value $readme -Encoding UTF8

    return [ordered]@{
        ok = $true
        status = "queued"
        jobName = $job
        packName = $pack
        assetMode = $assetMode
        requestRoot = $relativeRequestRoot
        requestPath = "$relativeRequestRoot/generation_request.json"
        workerQueuePath = "$relativeRequestRoot/worker_queue_item.json"
        statusPath = "$relativeRequestRoot/request_status.json"
        placeholderReviewReport = "$relativeRequestRoot/Review/review_report.placeholder.json"
        intendedReviewPackRoot = $relativeReviewOutput
        nextStep = "Run the Asset Forge ComfyUI worker against worker_queue_item.json."
    }
}

function Handle-Api([string]$Method, [string]$Path, [object]$Body) {
    $assetForge = Join-Path $ProjectRoot "Tools\AssetForge"
    $lora = Join-Path $ProjectRoot "Tools\LoRA"
    switch ("$Method $Path") {
        "OPTIONS $Path" { return New-JsonResponse 200 ([ordered]@{ ok = $true }) }
        "GET /api/assetforge/status" {
            $defaultPack = "CodexBiomeStarter"
            $requestQueueRoot = Join-Path $ProjectRoot "Assets\Generated\_Review\_Requests"
            $requestDirs = if (Test-Path $requestQueueRoot) { @(Get-ChildItem -Path $requestQueueRoot -Directory | Sort-Object LastWriteTime -Descending) } else { @() }
            $styleProfiles = Get-StyleProfiles
            $reviewPacks = Get-ReviewPacks
            $recentRequests = @($requestDirs | Select-Object -First 8 | ForEach-Object {
                [ordered]@{
                    name = $_.Name
                    updatedUtc = $_.LastWriteTimeUtc.ToString("o")
                    statusPath = "Assets/Generated/_Review/_Requests/$($_.Name)/request_status.json"
                }
            })
            return New-JsonResponse 200 ([ordered]@{
                ok = $true
                projectRoot = $ProjectRoot
                defaultPackName = $defaultPack
                reviewPackExists = Test-Path (Join-Path $ProjectRoot "Assets\Generated\_Review\$defaultPack")
                reviewPackCount = $reviewPacks.count
                recentReviewPacks = @($reviewPacks.packs | Select-Object -First 12)
                requestQueueExists = Test-Path $requestQueueRoot
                requestQueueCount = $requestDirs.Count
                recentRequests = $recentRequests
                styleProfileCount = $styleProfiles.count
                scripts = [ordered]@{
                    strictQa = Test-Path (Join-Path $assetForge "test_strict_asset_quality.ps1")
                    approve = Test-Path (Join-Path $assetForge "approve_review_pack.ps1")
                    captureDataset = Test-Path (Join-Path $assetForge "capture_dataset_from_review.ps1")
                    validateHandoff = Test-Path (Join-Path $assetForge "validate_tile_prop_handoff.ps1")
                    importEvalReview = Test-Path (Join-Path $assetForge "import_lora_eval_review_pack.ps1")
                    processGenerationRequest = Test-Path (Join-Path $assetForge "process_generation_request.ps1")
                    processGenerationRequestComfy = Test-Path (Join-Path $assetForge "process_generation_request_comfy.ps1")
                    processGenerationRequestSprixen = Test-Path (Join-Path $assetForge "process_generation_request_sprixen.ps1")
                }
            })
        }
        "GET /api/assetforge/style-profiles" {
            return New-JsonResponse 200 (Get-StyleProfiles)
        }
        "GET /api/assetforge/review-packs" {
            return New-JsonResponse 200 (Get-ReviewPacks)
        }
        "POST /api/assetforge/load-review-pack" {
            return New-JsonResponse 200 (Get-ReviewPack $Body)
        }
        "POST /api/assetforge/import-style-reference" {
            return New-JsonResponse 200 (Import-StyleReference $Body)
        }
        "POST /api/assetforge/save-generation-request" {
            return New-JsonResponse 200 (Save-GenerationRequest $Body)
        }
        "POST /api/assetforge/process-generation-request" {
            $job = Get-SafeName $Body "jobName" "asset_job"
            $replace = [bool](Get-BodyValue $Body "replaceExisting" $true)
            $args = @("-ProjectRoot", $ProjectRoot, "-JobName", $job)
            if ($replace) { $args += "-ReplaceExisting" }
            return New-JsonResponse 200 (Convert-ScriptResultPayload (Invoke-FixedScript (Join-Path $assetForge "process_generation_request.ps1") $args))
        }
        "POST /api/assetforge/process-generation-request-comfy" {
            $job = Get-SafeName $Body "jobName" "asset_job"
            $replace = [bool](Get-BodyValue $Body "replaceExisting" $true)
            $dryRun = [bool](Get-BodyValue $Body "dryRun" $false)
            $args = @("-ProjectRoot", $ProjectRoot, "-JobName", $job)
            if ($replace) { $args += "-ReplaceExisting" }
            if ($dryRun) { $args += "-DryRun" }
            return New-JsonResponse 200 (Convert-ScriptResultPayload (Invoke-FixedScript (Join-Path $assetForge "process_generation_request_comfy.ps1") $args))
        }
        "POST /api/assetforge/process-generation-request-sprixen" {
            $job = Get-SafeName $Body "jobName" "asset_job"
            $replace = [bool](Get-BodyValue $Body "replaceExisting" $true)
            $dryRun = [bool](Get-BodyValue $Body "dryRun" $false)
            $args = @("-ProjectRoot", $ProjectRoot, "-JobName", $job)
            if ($replace) { $args += "-ReplaceExisting" }
            if ($dryRun) { $args += "-DryRun" }
            return New-JsonResponse 200 (Convert-ScriptResultPayload (Invoke-FixedScript (Join-Path $assetForge "process_generation_request_sprixen.ps1") $args))
        }
        "GET /api/comfy/status" {
            return New-JsonResponse 200 (Get-ComfyStatus)
        }
        "GET /api/sprixen/status" {
            return New-JsonResponse 200 (Get-SprixenStatus)
        }
        "POST /api/assetforge/run-strict-qa" {
            $pack = Get-SafeName $Body "packName" "CodexBiomeStarter"
            $input = Join-Path $ProjectRoot "Assets\Generated\_Review\$pack"
            return New-JsonResponse 200 (Invoke-FixedScript (Join-Path $assetForge "test_strict_asset_quality.ps1") @("-InputPath", $input, "-OutputPath", (Join-Path $input "strict_asset_quality_report.json"), "-FailOnReview"))
        }
        "POST /api/assetforge/approve" {
            $pack = Get-SafeName $Body "packName" "CodexBiomeStarter"
            return New-JsonResponse 200 (Invoke-FixedScript (Join-Path $assetForge "approve_review_pack.ps1") @("-ProjectRoot", $ProjectRoot, "-PackName", $pack))
        }
        "POST /api/assetforge/capture-dataset" {
            $pack = Get-SafeName $Body "packName" "CodexBiomeStarter"
            return New-JsonResponse 200 (Invoke-FixedScript (Join-Path $assetForge "capture_dataset_from_review.ps1") @("-ProjectRoot", $ProjectRoot, "-PackName", $pack))
        }
        "POST /api/assetforge/validate-handoff" {
            $pack = Get-SafeName $Body "packName" "CodexBiomeStarter"
            return New-JsonResponse 200 (Invoke-FixedScript (Join-Path $assetForge "validate_tile_prop_handoff.ps1") @("-ProjectRoot", $ProjectRoot, "-PackName", $pack))
        }
        "POST /api/assetforge/import-eval-review" {
            $pack = Get-SafeName $Body "packName" "LoRAEvalReview"
            $output = Get-SafeName $Body "outputName" "litiso_tile_prop_v1"
            $input = Join-Path "C:\Projects\Pixel Pipeline\generated" "$output`_latest_synced_eval"
            return New-JsonResponse 200 (Invoke-FixedScript (Join-Path $assetForge "import_lora_eval_review_pack.ps1") @("-ProjectRoot", $ProjectRoot, "-InputPath", $input, "-PackName", $pack, "-Category", "auto", "-ReplaceExisting"))
        }
        "GET /api/lora/status" {
            return New-JsonResponse 200 (Invoke-FixedScript (Join-Path $lora "status_litiso_training.ps1") @("-Json"))
        }
        "POST /api/lora/sync" {
            $output = Get-SafeName $Body "outputName" "litiso_tile_prop_v1"
            return New-JsonResponse 200 (Invoke-FixedScript (Join-Path $lora "sync_lora_to_comfyui.ps1") @("-OutputName", $output, "-DryRun"))
        }
        "POST /api/lora/eval-dry-run" {
            $output = Get-SafeName $Body "outputName" "litiso_tile_prop_v1"
            return New-JsonResponse 200 (Invoke-FixedScript (Join-Path $lora "eval_latest_synced_lora.ps1") @("-ProjectRoot", $ProjectRoot, "-OutputName", $output, "-DryRun"))
        }
        default { return New-JsonResponse 404 ([ordered]@{ ok = $false; error = "Unknown API route"; route = "$Method $Path" }) }
    }
}

$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Parse("127.0.0.1"), $Port)
$listener.Start()
Write-Host "Asset Forge dashboard listening on http://127.0.0.1:$Port/Tools/AssetForge/Dashboard/index.html"

try {
    while ($true) {
        $client = $null
        $stream = $null
        try {
            $client = $listener.AcceptTcpClient()
            $stream = $client.GetStream()
            $buffer = New-Object byte[] 8192
            $read = $stream.Read($buffer, 0, $buffer.Length)
            if ($read -le 0) { continue }
            $requestText = [Text.Encoding]::UTF8.GetString($buffer, 0, $read)
            $requestLine = ($requestText -split "`r?`n")[0]
            $parts = $requestLine -split " "
            $method = if ($parts.Length -ge 1) { $parts[0].ToUpperInvariant() } else { "GET" }
            $path = if ($parts.Length -ge 2) { [Uri]::UnescapeDataString(($parts[1] -split "\?", 2)[0]) } else { "/" }
            if ($path.StartsWith("/api/")) {
                try {
                    $body = ConvertFrom-BodyJson (Get-BodyText $requestText $stream)
                    $response = Handle-Api $method $path $body
                }
                catch {
                    $response = New-JsonResponse 500 ([ordered]@{ ok = $false; error = $_.Exception.Message })
                }
            }
            else {
                if ($method -ne "GET" -and $method -ne "HEAD") {
                    $response = New-Response 405 "Method Not Allowed" "text/plain; charset=utf-8" ([Text.Encoding]::UTF8.GetBytes("Method not allowed"))
                }
                else {
                    $relative = $path.TrimStart("/")
                    if ([string]::IsNullOrWhiteSpace($relative)) { $relative = "Tools/AssetForge/Dashboard/index.html" }
                    $candidate = Join-Path $ProjectRoot ($relative -replace "/", "\")
                    $root = (Resolve-Path $ProjectRoot).Path
                    $resolved = if (Test-Path $candidate) { (Resolve-Path $candidate).Path } else { $null }
                    if (-not $resolved -or -not $resolved.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) -or (Get-Item $resolved).PSIsContainer) {
                        $response = New-Response 404 "Not Found" "text/plain; charset=utf-8" ([Text.Encoding]::UTF8.GetBytes("Not found"))
                    }
                    else {
                        $body = if ($method -eq "HEAD") { [byte[]]@() } else { [IO.File]::ReadAllBytes($resolved) }
                        $response = New-Response 200 "OK" (Get-ContentType $resolved) $body
                    }
                }
            }
            $stream.Write($response, 0, $response.Length)
        }
        catch {
            try {
                if ($stream) {
                    $response = New-JsonResponse 500 ([ordered]@{ ok = $false; error = $_.Exception.Message })
                    $stream.Write($response, 0, $response.Length)
                }
            }
            catch {
                # Keep the dashboard listener alive even if the client disconnects while we are reporting an error.
            }
        }
        finally {
            try {
                if ($client) { $client.Close() }
            }
            catch {
                # Client disconnects should never stop the local dashboard loop.
            }
        }
    }
}
finally { $listener.Stop() }
