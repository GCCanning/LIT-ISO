param(
    [string]$PythonExe = "C:\Users\garyc\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe",
    [int]$BatchCount = 2,
    [switch]$Process,
    [switch]$Replace
)

$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$queueScript = Join-Path $projectRoot "Tools\AssetForge\queue_black_mage_iso_requests.py"
$processScript = Join-Path $projectRoot "Tools\AssetForge\process_generation_request_comfy.ps1"
$templateManifest = Join-Path $projectRoot "Assets\Generated\_Review\black_mage_direction_templates_v2\black_mage_direction_templates_v2_manifest.json"

$matrix = @(
    @{ suffix = "v10_ne_a_s050_c072_d046"; seed = 131000; style = 0.50; control = 0.72; denoise = 0.46 },
    @{ suffix = "v10_ne_b_s056_c072_d042"; seed = 131100; style = 0.56; control = 0.72; denoise = 0.42 },
    @{ suffix = "v10_ne_c_s046_c070_d052"; seed = 131200; style = 0.46; control = 0.70; denoise = 0.52 },
    @{ suffix = "v10_ne_d_s052_c082_d048"; seed = 131300; style = 0.52; control = 0.82; denoise = 0.48 }
)

$created = @()
foreach ($entry in $matrix) {
    $args = @(
        $queueScript,
        "--project-root", $projectRoot,
        "--scaffold-manifest", $templateManifest,
        "--variant-suffix", $entry.suffix,
        "--directions", "NE",
        "--batch-count", $BatchCount,
        "--seed", $entry.seed,
        "--style-weight", $entry.style,
        "--control-strength", $entry.control,
        "--template-denoise", $entry.denoise,
        "--use-scaffold-template",
        "--strict-sprite-contract"
    )
    if ($Replace.IsPresent) { $args += "--replace" }
    $queueResult = & $PythonExe @args | ConvertFrom-Json
    $requestPath = $queueResult.created[0].request_path
    $created += [ordered]@{
        suffix = $entry.suffix
        request_path = $requestPath
        style_weight = $entry.style
        control_strength = $entry.control
        template_denoise = $entry.denoise
        seed = $entry.seed
    }
    if ($Process.IsPresent) {
        $processArgs = @(
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-File", $processScript,
            "-RequestPath", $requestPath,
            "-PythonExe", "C:\Projects\ComfyUI\.venv\Scripts\python.exe"
        )
        if ($Replace.IsPresent) { $processArgs += "-ReplaceExisting" }
        & powershell @processArgs | Out-Host
    }
}

$manifestPath = Join-Path $projectRoot "Assets\Generated\_Review\black_mage_v10_ne_matrix\black_mage_v10_ne_matrix_run_manifest.json"
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $manifestPath) | Out-Null
[ordered]@{
    schema = "lit_iso.asset_forge.black_mage_v10_ne_matrix_run.v1"
    created_utc = (Get-Date).ToUniversalTime().ToString("o")
    processed = $Process.IsPresent
    batch_count = $BatchCount
    matrix = $created
} | ConvertTo-Json -Depth 8 | Set-Content -Path $manifestPath -Encoding UTF8

Get-Content $manifestPath
