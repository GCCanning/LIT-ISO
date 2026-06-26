[CmdletBinding()]
param(
    [string]$ToolZip,
    [string]$ToolRoot,
    [string]$ExtractedContentRoot,
    [string]$StardewRoot,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ToolRoot)) {
    $ToolRoot = Join-Path $PSScriptRoot "_local\FarmSplitter"
}

$requiredInput = @(
    "accessories.png",
    "farmer_base.png",
    "farmer_base_bald.png",
    "farmer_girl_base.png",
    "farmer_girl_base_bald.png",
    "hairstyles.png",
    "hairstyles2.png",
    "hats.png",
    "pants.png",
    "shirts.png",
    "shoeColors.png",
    "skinColors.png"
)

$requiredGameDlls = @(
    "SDL2.dll",
    "MonoGame.Framework.dll",
    "FAudio-CS.dll",
    "FAudio.dll",
    "Stardew Valley.dll",
    "StardewValley.GameData.dll",
    "Netcode.dll",
    "Lidgren.Network.dll",
    "xTile.dll",
    "SkiaSharp.dll",
    "libSkiaSharp.dll",
    "TextCopy.dll"
)

function Resolve-ExistingPath([string]$PathValue, [string]$Name) {
    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        return $null
    }
    if (!(Test-Path -LiteralPath $PathValue)) {
        throw "$Name does not exist: $PathValue"
    }
    return (Resolve-Path -LiteralPath $PathValue).Path
}

function Find-FirstFile([string]$Root, [string]$FileName) {
    if ([string]::IsNullOrWhiteSpace($Root) -or !(Test-Path -LiteralPath $Root)) {
        return $null
    }
    $direct = Join-Path $Root $FileName
    if (Test-Path -LiteralPath $direct) {
        return $direct
    }
    $match = Get-ChildItem -LiteralPath $Root -Recurse -File -Filter $FileName -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($match) {
        return $match.FullName
    }
    return $null
}

$ToolRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ToolRoot)
New-Item -ItemType Directory -Force -Path $ToolRoot | Out-Null

if ($ToolZip) {
    $zipPath = Resolve-ExistingPath $ToolZip "ToolZip"
    if ($Force) {
        $localRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath((Join-Path $PSScriptRoot "_local"))
        if (!$ToolRoot.StartsWith($localRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "-Force cleanup is only allowed inside $localRoot. Refusing to clear $ToolRoot."
        }
        Get-ChildItem -LiteralPath $ToolRoot -Force | Remove-Item -Recurse -Force
    }
    Expand-Archive -LiteralPath $zipPath -DestinationPath $ToolRoot -Force
}

$exe = Get-ChildItem -LiteralPath $ToolRoot -Recurse -File -Filter "FarmSplitter.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if (!$exe) {
    $exe = Get-ChildItem -LiteralPath $ToolRoot -Recurse -File -Filter "FarmerSplitter.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
}
if (!$exe) {
    Write-Warning "FarmSplitter.exe was not found under $ToolRoot. Download the Nexus file, then rerun with -ToolZip or extract it into this folder."
}

$inputDir = Join-Path $ToolRoot "input"
New-Item -ItemType Directory -Force -Path $inputDir | Out-Null

$contentRoot = Resolve-ExistingPath $ExtractedContentRoot "ExtractedContentRoot"
$missing = New-Object System.Collections.Generic.List[string]
if ($contentRoot) {
    foreach ($fileName in $requiredInput) {
        $source = Find-FirstFile $contentRoot $fileName
        if ($source) {
            Copy-Item -LiteralPath $source -Destination (Join-Path $inputDir $fileName) -Force
        } else {
            $missing.Add($fileName)
        }
    }
} else {
    foreach ($fileName in $requiredInput) {
        $missing.Add($fileName)
    }
}

$resolvedStardewRoot = Resolve-ExistingPath $StardewRoot "StardewRoot"
if (!$resolvedStardewRoot) {
    $candidateRoots = @(
        "C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley",
        "C:\Program Files\Steam\steamapps\common\Stardew Valley",
        "C:\GOG Games\Stardew Valley"
    )
    foreach ($candidate in $candidateRoots) {
        if (Test-Path -LiteralPath $candidate) {
            $resolvedStardewRoot = (Resolve-Path -LiteralPath $candidate).Path
            break
        }
    }
}

$copiedDlls = New-Object System.Collections.Generic.List[string]
$missingDlls = New-Object System.Collections.Generic.List[string]
if ($resolvedStardewRoot -and $exe) {
    foreach ($dllName in $requiredGameDlls) {
        $dllPath = Join-Path $resolvedStardewRoot $dllName
        if (Test-Path -LiteralPath $dllPath) {
            Copy-Item -LiteralPath $dllPath -Destination (Join-Path $exe.DirectoryName $dllName) -Force
            $copiedDlls.Add($dllName)
        } else {
            $missingDlls.Add($dllName)
        }
    }
}

$manifest = [ordered]@{
    generatedAt = (Get-Date).ToString("o")
    toolRoot = $ToolRoot
    executable = if ($exe) { $exe.FullName } else { $null }
    inputDir = $inputDir
    stardewRoot = $resolvedStardewRoot
    contentDir = if ($resolvedStardewRoot) { Join-Path $resolvedStardewRoot "Content" } else { $null }
    missingInput = @($missing)
    copiedDlls = @($copiedDlls)
    missingDlls = @($missingDlls)
    stardewIds = "https://mateusaquino.github.io/stardewids/"
    nexusPage = "https://www.nexusmods.com/stardewvalley/mods/37108"
}

$manifestPath = Join-Path $ToolRoot "litiso_sdv_npc_generator_setup.json"
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Host "Setup manifest: $manifestPath"
if ($manifest.missingInput.Count -gt 0) {
    Write-Warning ("Missing input PNGs: " + ($manifest.missingInput -join ", "))
}
if (!$manifest.executable) {
    Write-Warning "Executable missing. Download/login through Nexus and rerun setup with -ToolZip."
}
if (!$manifest.stardewRoot) {
    Write-Warning "Stardew root was not found. Pass -StardewRoot or run commands with -ContentDir."
}
if ($manifest.missingDlls.Count -gt 0) {
    Write-Warning ("Missing game DLLs: " + ($manifest.missingDlls -join ", "))
}
