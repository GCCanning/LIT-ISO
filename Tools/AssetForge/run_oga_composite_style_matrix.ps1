param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$Preset = "iron_knight",
    [string]$Action = "Walk",
    [string]$Direction = "S",
    [string]$JobPrefix = "oga_matrix_refknight_style",
    [double[]]$DenoiseValues = @(0.62, 0.70, 0.78),
    [double[]]$StyleWeights = @(0.45, 0.62, 0.78),
    [int]$Seed = 91700,
    [string]$StyleReference = "Assets\Generated\_Review\_StyleRefs\reference_knight_front_cell.png",
    [string]$PythonExe = "C:\Projects\ComfyUI\.venv\Scripts\python.exe",
    [switch]$ReplaceExisting
)

$ErrorActionPreference = "Stop"

if (!(Test-Path -LiteralPath $PythonExe)) {
    throw "Missing Python executable: $PythonExe"
}

$queueScript = Join-Path $PSScriptRoot "queue_oga_composite_template_guided_requests.ps1"
$processScript = Join-Path $PSScriptRoot "process_generation_request_comfy.ps1"
$contactScript = Join-Path $PSScriptRoot "build_review_contact_sheet.py"
$jobs = @()
$labels = @()
$index = 0

foreach ($styleWeight in $StyleWeights) {
    foreach ($denoise in $DenoiseValues) {
        $denoiseSuffix = ("d{0:000}" -f [int][Math]::Round($denoise * 100))
        $styleSuffix = ("sw{0:000}" -f [int][Math]::Round($styleWeight * 100))
        $prefix = "${JobPrefix}_${styleSuffix}_${denoiseSuffix}"
        $seedValue = $Seed + $index
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $queueScript `
            -ProjectRoot $ProjectRoot `
            -Preset $Preset `
            -Action $Action `
            -Directions @($Direction) `
            -JobPrefix $prefix `
            -Denoise $denoise `
            -Seed $seedValue `
            -StyleReference $StyleReference `
            -StyleWeight $styleWeight `
            -PythonExe $PythonExe `
            -Replace | Out-Host

        $job = "${prefix}_${Preset}_$($Action.ToLowerInvariant())_$($Direction.ToLowerInvariant())"
        $jobs += $job
        $labels += "style $styleWeight|denoise $denoise|seed $seedValue"

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
            throw "Matrix generation failed for $job with exit code $LASTEXITCODE."
        }
        $index += 1
    }
}

$firstRequest = Join-Path $ProjectRoot "Assets\Generated\_Review\_Requests\$($jobs[0])\generation_request.json"
$request = Get-Content -Raw -LiteralPath $firstRequest | ConvertFrom-Json
$template = [string]$request.reference_image
$output = Join-Path $ProjectRoot "Assets\Generated\_Review\${JobPrefix}_${Preset}_${Action}_${Direction}_matrix.png"
$contactArgs = @(
    $contactScript,
    "--project-root", $ProjectRoot,
    "--template", $template,
    "--template-label", "$Preset template|$Action $Direction",
    "--output", $output
)
for ($i = 0; $i -lt $jobs.Count; $i++) {
    $contactArgs += @("--job", "$($jobs[$i])=$($labels[$i])")
}
& $PythonExe @contactArgs

[PSCustomObject]@{
    ok = $true
    preset = $Preset
    action = $Action
    direction = $Direction
    jobs = $jobs
    contact_sheet = $output
} | ConvertTo-Json -Depth 6
