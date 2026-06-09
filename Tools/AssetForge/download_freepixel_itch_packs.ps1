param(
    [string]$OutputDir = "C:\Projects\Pixel Pipeline\sources\freepixel\zips",
    [string[]]$Pages = @(
        "https://freepixelart.itch.io/free-rpg-animations-pixel-art-270-sprites",
        "https://freepixelart.itch.io/free-rpg-items-loot-pixel-art-385-sprites",
        "https://freepixelart.itch.io/free-rpg-enemies-bosses-pixel-art-350-sprites"
    )
)

$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$records = @()

foreach ($pageUrl in $Pages) {
    Write-Host "Checking $pageUrl"
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $purchaseUrl = if ($pageUrl.EndsWith("/purchase")) { $pageUrl } else { "$pageUrl/purchase" }
    $page = Invoke-WebRequest -Uri $purchaseUrl -WebSession $session -UseBasicParsing
    $csrf = [regex]::Match($page.Content, '<meta name="csrf_token" value="([^"]+)"').Groups[1].Value
    if ([string]::IsNullOrWhiteSpace($csrf)) {
        throw "Could not find csrf token for $purchaseUrl"
    }

    $slug = [regex]::Match($page.Content, '"slug":"([^"]+)"').Groups[1].Value
    if ([string]::IsNullOrWhiteSpace($slug)) {
        $slug = (($pageUrl.TrimEnd("/") -split "/")[-1])
    }

    $fileName = [regex]::Match($page.Content, '<strong class="name">([^<]+)</strong>').Groups[1].Value
    if ([string]::IsNullOrWhiteSpace($fileName)) {
        $fileName = "$slug.zip"
    }
    $fileName = [System.Net.WebUtility]::HtmlDecode($fileName)

    $downloadUrlEndpoint = "$pageUrl/download_url"
    $body = @{ csrf_token = $csrf }
    $downloadResponse = Invoke-WebRequest -Uri $downloadUrlEndpoint -Method Post -WebSession $session -Body $body -UseBasicParsing
    $downloadPayload = $downloadResponse.Content | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace([string]$downloadPayload.url)) {
        throw "Download URL endpoint did not return a URL for $pageUrl. Response: $($downloadResponse.Content)"
    }

    $downloadPage = Invoke-WebRequest -Uri ([string]$downloadPayload.url) -WebSession $session -UseBasicParsing
    $downloadCsrf = [regex]::Match($downloadPage.Content, '<meta name="csrf_token" value="([^"]+)"').Groups[1].Value
    if ([string]::IsNullOrWhiteSpace($downloadCsrf)) {
        throw "Could not find download-page csrf token for $pageUrl"
    }
    $uploadId = [regex]::Match($downloadPage.Content, 'data-upload_id="([^"]+)"').Groups[1].Value
    if ([string]::IsNullOrWhiteSpace($uploadId)) {
        throw "Could not find upload id for $pageUrl"
    }
    $fileEndpoint = "$pageUrl/file/$uploadId`?source=game_download"
    $fileUrlResponse = Invoke-WebRequest -Uri $fileEndpoint -Method Post -WebSession $session -Body @{ csrf_token = $downloadCsrf } -UseBasicParsing
    $filePayload = $fileUrlResponse.Content | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace([string]$filePayload.url)) {
        throw "File endpoint did not return a URL for $pageUrl. Response: $($fileUrlResponse.Content)"
    }

    $destination = Join-Path $OutputDir $fileName
    Write-Host "Downloading $fileName"
    Invoke-WebRequest -Uri ([string]$filePayload.url) -OutFile $destination -WebSession $session

    $item = Get-Item -LiteralPath $destination
    $hash = Get-FileHash -LiteralPath $destination -Algorithm SHA256
    $records += [ordered]@{
        page_url = $pageUrl
        purchase_url = $purchaseUrl
        slug = $slug
        upload_id = $uploadId
        file_name = $fileName
        path = $destination
        size_bytes = $item.Length
        sha256 = $hash.Hash
        downloaded_utc = (Get-Date).ToUniversalTime().ToString("o")
    }
}

$manifestPath = Join-Path $OutputDir "freepixel_itch_download_manifest.json"
$manifest = [ordered]@{
    schemaVersion = 1
    generated_utc = (Get-Date).ToUniversalTime().ToString("o")
    method = "official itch.io free pack download_url endpoint"
    records = $records
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

[ordered]@{
    output_dir = $OutputDir
    downloaded = $records.Count
    manifest = $manifestPath
} | ConvertTo-Json -Depth 6
