param(
    [int]$Port = 4191,
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$dashboardPath = Join-Path $ProjectRoot "Tools\AssetForge\Dashboard\index.html"
if (-not (Test-Path $dashboardPath)) {
    throw "Missing dashboard: $dashboardPath"
}

function Get-ContentType {
    param([string]$Path)
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

function New-Response {
    param(
        [int]$StatusCode,
        [string]$StatusText,
        [string]$ContentType,
        [byte[]]$Body
    )

    $header = "HTTP/1.1 $StatusCode $StatusText`r`nContent-Type: $ContentType`r`nContent-Length: $($Body.Length)`r`nConnection: close`r`n`r`n"
    $headerBytes = [Text.Encoding]::ASCII.GetBytes($header)
    $response = New-Object byte[] ($headerBytes.Length + $Body.Length)
    [Buffer]::BlockCopy($headerBytes, 0, $response, 0, $headerBytes.Length)
    [Buffer]::BlockCopy($Body, 0, $response, $headerBytes.Length, $Body.Length)
    return $response
}

$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Parse("127.0.0.1"), $Port)
$listener.Start()

Write-Host "Asset Forge dashboard listening on http://127.0.0.1:$Port/"
Write-Host "Open http://127.0.0.1:$Port/Tools/AssetForge/Dashboard/index.html"
Write-Host "Press Ctrl+C to stop."

try {
    while ($true) {
        $client = $listener.AcceptTcpClient()
        try {
            $stream = $client.GetStream()
            $buffer = New-Object byte[] 4096
            $read = $stream.Read($buffer, 0, $buffer.Length)
            if ($read -le 0) {
                continue
            }

            $requestText = [Text.Encoding]::ASCII.GetString($buffer, 0, $read)
            $requestLine = ($requestText -split "`r?`n")[0]
            $parts = $requestLine -split " "
            $requestPath = if ($parts.Length -ge 2) { [Uri]::UnescapeDataString($parts[1].TrimStart("/")) } else { "" }
            if ([string]::IsNullOrWhiteSpace($requestPath)) {
                $requestPath = "Tools/AssetForge/Dashboard/index.html"
            }

            $candidate = Join-Path $ProjectRoot ($requestPath -replace "/", "\")
            $resolvedRoot = (Resolve-Path $ProjectRoot).Path
            $resolvedCandidate = $null
            if (Test-Path $candidate) {
                $resolvedCandidate = (Resolve-Path $candidate).Path
            }

            if ($null -eq $resolvedCandidate -or -not $resolvedCandidate.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase) -or (Get-Item $resolvedCandidate).PSIsContainer) {
                $body = [Text.Encoding]::UTF8.GetBytes("Not found")
                $response = New-Response -StatusCode 404 -StatusText "Not Found" -ContentType "text/plain; charset=utf-8" -Body $body
            }
            else {
                $body = [IO.File]::ReadAllBytes($resolvedCandidate)
                $response = New-Response -StatusCode 200 -StatusText "OK" -ContentType (Get-ContentType -Path $resolvedCandidate) -Body $body
            }

            $stream.Write($response, 0, $response.Length)
        }
        finally {
            $client.Close()
        }
    }
}
finally {
    $listener.Stop()
}
