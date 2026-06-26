[CmdletBinding()]
param(
    [string]$ToolRoot,
    [int]$Random = 1,
    [ValidateSet("male", "female", "both")]
    [string]$Gender = "both",
    [int]$Seed = 0,
    [string]$ContentDir,
    [string]$Actions,
    [ValidateSet("yes", "no")]
    [string]$Hats = "no",
    [ValidateSet("yes", "no")]
    [string]$Accessories = "no",
    [double]$HatProbability = -1,
    [double]$AccessoryProbability = -1,
    [int]$Shirt = -2147483648,
    [int]$Pants = -2147483648,
    [int]$Hair = -2147483648,
    [int]$Hat = -2147483648,
    [int]$Accessory = -2147483648,
    [string]$HairColor,
    [string]$PantsColor,
    [int]$ShoeIndex = -2147483648,
    [string]$ShoeColor,
    [switch]$NoEnforceGenderHair,
    [switch]$NoStrictMaleShirts,
    [switch]$VerboseLog,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ToolRoot)) {
    $ToolRoot = Join-Path $PSScriptRoot "_local\FarmSplitter"
}

function Add-Arg([System.Collections.Generic.List[string]]$Arguments, [string]$Name, $Value) {
    if ($null -eq $Value) {
        return
    }
    if ($Value -is [string] -and [string]::IsNullOrWhiteSpace($Value)) {
        return
    }
    $Arguments.Add($Name)
    $Arguments.Add([string]$Value)
}

$ToolRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ToolRoot)
if (!(Test-Path -LiteralPath $ToolRoot)) {
    throw "ToolRoot does not exist. Run setup first: $ToolRoot"
}

$exe = Get-ChildItem -LiteralPath $ToolRoot -Recurse -File -Filter "FarmSplitter.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if (!$exe) {
    $exe = Get-ChildItem -LiteralPath $ToolRoot -Recurse -File -Filter "FarmerSplitter.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
}
if (!$exe) {
    throw "FarmSplitter.exe/FarmerSplitter.exe not found under $ToolRoot. Download the Nexus file and rerun setup."
}

$arguments = New-Object System.Collections.Generic.List[string]
if ($Random -gt 0) {
    Add-Arg $arguments "--random" $Random
}
Add-Arg $arguments "--gender" $Gender
if ($Seed -ne 0) {
    Add-Arg $arguments "--seed" $Seed
}
Add-Arg $arguments "--content" $ContentDir
Add-Arg $arguments "--actions" $Actions
Add-Arg $arguments "--hats" $Hats
Add-Arg $arguments "--accessories" $Accessories
if ($HatProbability -ge 0) {
    Add-Arg $arguments "--p-hat" $HatProbability
}
if ($AccessoryProbability -ge 0) {
    Add-Arg $arguments "--p-acc" $AccessoryProbability
}
if ($Shirt -ne -2147483648) {
    Add-Arg $arguments "--shirt" $Shirt
}
if ($Pants -ne -2147483648) {
    Add-Arg $arguments "--pants" $Pants
}
if ($Hair -ne -2147483648) {
    Add-Arg $arguments "--hair" $Hair
}
if ($Hat -ne -2147483648) {
    Add-Arg $arguments "--hat" $Hat
}
if ($Accessory -ne -2147483648) {
    Add-Arg $arguments "--accessory" $Accessory
}
Add-Arg $arguments "--haircolor" $HairColor
Add-Arg $arguments "--pantscolor" $PantsColor
if ($ShoeIndex -ne -2147483648) {
    Add-Arg $arguments "--shoeindex" $ShoeIndex
}
Add-Arg $arguments "--shoecolor" $ShoeColor
if ($NoEnforceGenderHair) {
    $arguments.Add("--no-enforce-gender-hair")
}
if ($NoStrictMaleShirts) {
    $arguments.Add("--no-strict-male-shirts")
}
if ($VerboseLog) {
    $arguments.Add("--verbose")
}

$quoted = @($exe.FullName) + $arguments | ForEach-Object {
    if ($_ -match '\s') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ }
}
Write-Host ($quoted -join " ")

if ($DryRun) {
    return
}

Push-Location -LiteralPath $exe.DirectoryName
try {
    & $exe.FullName @arguments
} finally {
    Pop-Location
}
