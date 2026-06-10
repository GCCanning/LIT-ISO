param(
    [string]$Python = "C:\Projects\ComfyUI\.venv\Scripts\python.exe"
)

$ErrorActionPreference = "Stop"
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
if (!(Test-Path $Python)) {
    $Python = "python"
}

$denoiseValues = @("0.55", "0.65", "0.75")
$controlValues = @("0.80", "1.00", "1.20")
$sweepRoot = Join-Path $projectRoot.Path "Tools\SpriteForge\out\p2_fix_sweep"

foreach ($denoise in $denoiseValues) {
    foreach ($control in $controlValues) {
        $safeD = $denoise.Replace(".", "")
        $safeC = $control.Replace(".", "")
        $outRoot = Join-Path $sweepRoot "d${safeD}_c${safeC}"
        & $Python (Join-Path $PSScriptRoot "run_lane_a_animation.py") `
            --project-root $projectRoot.Path `
            --character witch `
            --character-ref "Assets\Characters\Witch\AnimationSprites\Static\witch static00.png" `
            --action walk `
            --direction S `
            --target-size 64 `
            --seed 1207 `
            --out-root $outRoot `
            --anchor-template-denoise 0.38 `
            --anchor-style-weight 0.72 `
            --anchor-control-strength 0.62 `
            --template-denoise $denoise `
            --style-weight 0.68 `
            --control-strength $control `
            --palette-lock
        if ($LASTEXITCODE -ne 0) {
            throw "Lane A candidate failed: denoise=$denoise control=$control"
        }
        & $Python (Join-Path $PSScriptRoot "validate_lane_a_output.py") `
            --root (Join-Path $outRoot "witch\walk\S")
        if ($LASTEXITCODE -ne 0) {
            throw "Lane A validation failed: denoise=$denoise control=$control"
        }
    }
}

& $Python (Join-Path $PSScriptRoot "build_lane_a_sweep_sheet.py") `
    --root $sweepRoot `
    --out (Join-Path $sweepRoot "lane_a_sweep_contact_sheet.png")
