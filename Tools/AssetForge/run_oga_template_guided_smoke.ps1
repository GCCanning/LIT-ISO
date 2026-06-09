param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$Action = "Walk",
    [string]$Direction = "S",
    [string]$JobPrefix = "oga_template_cyan_knight_smoke",
    [string]$TemplatePath = "",
    [double[]]$DenoiseValues = @(0.42, 0.54, 0.62, 0.72),
    [int]$Seed = 91200,
    [string]$StyleReference = "",
    [double]$StyleWeight = 0.58,
    [string]$PythonExe = "C:\Projects\ComfyUI\.venv\Scripts\python.exe",
    [switch]$ReplaceExisting
)

$ErrorActionPreference = "Stop"

if (!(Test-Path -LiteralPath $PythonExe)) {
    throw "Missing Python executable: $PythonExe"
}

$queueScript = Join-Path $PSScriptRoot "queue_oga_template_guided_requests.ps1"
$processScript = Join-Path $PSScriptRoot "process_generation_request_comfy.ps1"
$contactScript = Join-Path $PSScriptRoot "build_review_contact_sheet.py"
$jobs = @()
$labels = @()

for ($i = 0; $i -lt $DenoiseValues.Count; $i++) {
    $denoise = [double]$DenoiseValues[$i]
    $suffix = ("d{0:000}" -f [int][Math]::Round($denoise * 100))
    $prefix = "${JobPrefix}_${suffix}"
    $seedValue = $Seed + $i
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $queueScript `
        -ProjectRoot $ProjectRoot `
        -Action $Action `
        -Directions @($Direction) `
        -JobPrefix $prefix `
        -TemplatePath $TemplatePath `
        -Denoise $denoise `
        -Seed $seedValue `
        -StyleReference $StyleReference `
        -StyleWeight $StyleWeight `
        -PythonExe $PythonExe `
        -Replace | Out-Host
    $job = "${prefix}_$($Action.ToLowerInvariant())_$($Direction.ToLowerInvariant())"
    $jobs += $job
    $labels += "denoise $denoise|seed $seedValue"
    $processArgs = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $processScript,
        "-ProjectRoot", $ProjectRoot,
        "-JobName", $job,
        "-PythonExe", $PythonExe
    )
    if ($ReplaceExisting.IsPresent) {
        $processArgs += "-ReplaceExisting"
    }
    & powershell.exe @processArgs | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Template-guided smoke generation failed for $job with exit code $LASTEXITCODE."
    }
}

$firstRequest = Join-Path $ProjectRoot "Assets\Generated\_Review\_Requests\$($jobs[0])\generation_request.json"
$request = Get-Content -Raw -LiteralPath $firstRequest | ConvertFrom-Json
$template = [string]$request.reference_image
$output = Join-Path $ProjectRoot "Assets\Generated\_Review\${JobPrefix}_comparison.png"
$contactArgs = @(
    $contactScript,
    "--project-root", $ProjectRoot,
    "--template", $template,
    "--template-label", "OGA template|$Action $Direction",
    "--output", $output
)
for ($i = 0; $i -lt $jobs.Count; $i++) {
    $contactArgs += @("--job", "$($jobs[$i])=$($labels[$i])")
}
& $PythonExe @contactArgs

[PSCustomObject]@{
    ok = $true
    action = $Action
    direction = $Direction
    jobs = $jobs
    contact_sheet = $output
} | ConvertTo-Json -Depth 6
