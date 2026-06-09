param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$Action = "Idle",
    [string]$Direction = "W",
    [string]$JobPrefix = "litiso_oracle_sweep_refknight",
    [double[]]$TemplateDenoiseValues = @(0.24, 0.32, 0.40),
    [double]$StyleWeight = 0.50,
    [double]$ControlStrength = 0.98,
    [int]$Seed = 94400,
    [string]$StyleReference = "Assets\Generated\_Review\_StyleRefs\reference_knight_front_cell.png",
    [string]$OracleManifest = "Assets\Generated\_Review\litiso_reference_knight_idle_4d_sheet_manifest.json",
    [string]$PythonExe = "C:\Projects\ComfyUI\.venv\Scripts\python.exe",
    [switch]$ReplaceExisting
)

$ErrorActionPreference = "Stop"

if (!(Test-Path -LiteralPath $PythonExe)) {
    throw "Missing Python executable: $PythonExe"
}

$queueScript = Join-Path $PSScriptRoot "queue_litiso_controlnet_direction_requests.ps1"
$processScript = Join-Path $PSScriptRoot "process_generation_request_comfy.ps1"
$contactScript = Join-Path $PSScriptRoot "build_review_contact_sheet.py"
$qaScript = Join-Path $PSScriptRoot "qa_against_direction_oracle.py"

$jobs = @()
$labels = @()
$index = 0

foreach ($denoise in $TemplateDenoiseValues) {
    $denoiseSuffix = ("d{0:000}" -f [int][Math]::Round($denoise * 100))
    $prefix = "${JobPrefix}_${denoiseSuffix}"
    $seedValue = $Seed + $index

    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $queueScript `
        -ProjectRoot $ProjectRoot `
        -Action $Action `
        -Directions @($Direction) `
        -JobPrefix $prefix `
        -Seed $seedValue `
        -StyleReference $StyleReference `
        -StyleWeight $StyleWeight `
        -ControlStrength $ControlStrength `
        -OracleManifest $OracleManifest `
        -TemplateDenoise $denoise `
        -Replace | Out-Host

    $job = "${prefix}_$($Action.ToLowerInvariant())_$($Direction.ToLowerInvariant())"
    $jobs += $job
    $labels += "oracle denoise $denoise|style $StyleWeight|seed $seedValue"

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
        throw "Oracle denoise sweep generation failed for $job with exit code $LASTEXITCODE."
    }
    $index += 1
}

$oracle = Get-Content -Raw -LiteralPath (Join-Path $ProjectRoot $OracleManifest) | ConvertFrom-Json
$frame = $oracle.frames | Where-Object { $_.direction -eq $Direction } | Select-Object -First 1
if ($null -eq $frame) {
    throw "Oracle manifest has no frame for direction $Direction."
}

$safeAction = $Action.ToLowerInvariant()
$safeDirection = $Direction.ToLowerInvariant()
$outputStem = "${JobPrefix}_${safeAction}_${safeDirection}_denoise_sweep"
$contactOutput = Join-Path $ProjectRoot "Assets\Generated\_Review\$outputStem.png"
$qaOutput = Join-Path $ProjectRoot "Assets\Generated\_Review\$($outputStem)_oracle_qa.json"

$contactArgs = @(
    $contactScript,
    "--project-root", $ProjectRoot,
    "--template", ([string]$frame.source_image),
    "--template-label", "oracle $Action $Direction",
    "--output", $contactOutput
)
for ($i = 0; $i -lt $jobs.Count; $i++) {
    $contactArgs += @("--job", "$($jobs[$i])=$($labels[$i])")
}
& $PythonExe @contactArgs

$qaArgs = @(
    $qaScript,
    "--project-root", $ProjectRoot,
    "--oracle-manifest", $OracleManifest,
    "--output", $qaOutput
)
foreach ($job in $jobs) {
    $qaArgs += @("--job", "$Direction=$job")
}
& $PythonExe @qaArgs

[PSCustomObject]@{
    ok = $true
    action = $Action
    direction = $Direction
    jobs = $jobs
    contact_sheet = $contactOutput
    oracle_qa = $qaOutput
} | ConvertTo-Json -Depth 6
