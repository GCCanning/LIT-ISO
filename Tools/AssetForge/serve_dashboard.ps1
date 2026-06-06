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

function Handle-Api([string]$Method, [string]$Path, [object]$Body) {
    $assetForge = Join-Path $ProjectRoot "Tools\AssetForge"
    $lora = Join-Path $ProjectRoot "Tools\LoRA"
    switch ("$Method $Path") {
        "OPTIONS $Path" { return New-JsonResponse 200 ([ordered]@{ ok = $true }) }
        "GET /api/assetforge/status" {
            $defaultPack = "CodexBiomeStarter"
            return New-JsonResponse 200 ([ordered]@{
                ok = $true
                projectRoot = $ProjectRoot
                defaultPackName = $defaultPack
                reviewPackExists = Test-Path (Join-Path $ProjectRoot "Assets\Generated\_Review\$defaultPack")
                scripts = [ordered]@{
                    strictQa = Test-Path (Join-Path $assetForge "test_strict_asset_quality.ps1")
                    approve = Test-Path (Join-Path $assetForge "approve_review_pack.ps1")
                    captureDataset = Test-Path (Join-Path $assetForge "capture_dataset_from_review.ps1")
                    validateHandoff = Test-Path (Join-Path $assetForge "validate_tile_prop_handoff.ps1")
                    importEvalReview = Test-Path (Join-Path $assetForge "import_lora_eval_review_pack.ps1")
                }
            })
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
        $client = $listener.AcceptTcpClient()
        try {
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
        finally { $client.Close() }
    }
}
finally { $listener.Stop() }
