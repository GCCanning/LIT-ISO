param(
    [switch]$SkipDataset,
    [switch]$SkipSamples
)

$ErrorActionPreference = 'Stop'
$env:PYTHONWARNINGS = 'ignore::DeprecationWarning'

$ProjectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$LoRaRoot = 'C:\Projects\LoRA-Training'
$PixelPipelineRoot = 'C:\Projects\Pixel Pipeline'
$DatasetDir = Join-Path $PixelPipelineRoot 'style_examples\freepixel_web_download_structured'
$SmokeOut = Join-Path $LoRaRoot 'outputs\litiso_style_directional_smoke'
$FullOut = Join-Path $LoRaRoot 'outputs\litiso_style_directional_v1'
$ComfyLoraDir = 'C:\Projects\ComfyUI\models\loras'
$FinalName = 'litiso_style_directional_v1'
$FinalLora = Join-Path $FullOut "$FinalName.safetensors"
$ComfyFinalLora = Join-Path $ComfyLoraDir "$FinalName.safetensors"
$TrainScript = Join-Path $LoRaRoot 'scripts\train_litiso_lora_smoke.py'
$TrainPython = Join-Path $LoRaRoot '.venv\Scripts\python.exe'
$PrepScript = Join-Path $ProjectRoot 'Tools\LoRA\freepixel_structured_dataset.py'
$EvalScript = Join-Path $ProjectRoot 'Tools\LoRA\eval_litiso_style_directional_v1_comfy.py'
$BaseModel = 'C:\Projects\ComfyUI\models\checkpoints\DreamShaper_8_pruned.safetensors'
$LogPath = Join-Path $FullOut 'overnight_train.log'

function Ensure-Directory {
    param([string]$Path)
    if (!(Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Force -Path $Path | Out-Null
    }
}

function Write-Log {
    param([string]$Message)
    $stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $line = "[$stamp] $Message"
    Write-Host $line
    Add-Content -LiteralPath $LogPath -Value $line
}

function Invoke-Logged {
    param(
        [string]$Label,
        [scriptblock]$Command
    )
    Write-Log "START $Label"
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $Command 2>&1 | ForEach-Object {
            $line = $_.ToString()
            Write-Host $line
            Add-Content -LiteralPath $LogPath -Value $line
        }
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousPreference
    }
    if ($exitCode -ne 0) {
        throw "$Label failed with exit code $exitCode"
    }
    Write-Log "DONE $Label"
}

function Test-StructuredDatasetImages {
    param([string]$DatasetPath)
    $probe = @"
from PIL import Image
from pathlib import Path
import sys

root = Path(r'$DatasetPath')
images = sorted((root / 'images').glob('*.png'))
if not images:
    print('transparent_png_count=0 total_png_count=0')
    raise SystemExit(1)

transparent_count = 0
corner_clear_count = 0
for path in images:
    im = Image.open(path).convert('RGBA')
    alpha = im.getchannel('A')
    if alpha.getextrema()[0] == 0:
        transparent_count += 1
    corners = [
        im.getpixel((0, 0))[3],
        im.getpixel((im.width - 1, 0))[3],
        im.getpixel((0, im.height - 1))[3],
        im.getpixel((im.width - 1, im.height - 1))[3],
    ]
    if min(corners) == 0:
        corner_clear_count += 1

print(f'transparent_png_count={transparent_count} total_png_count={len(images)} corner_clear_count={corner_clear_count}')
raise SystemExit(0 if transparent_count > 0 and corner_clear_count > 0 else 1)
"@
    $probe | & $TrainPython -
    return $LASTEXITCODE -eq 0
}

function Validate-Dataset {
    if (!(Test-Path -LiteralPath (Join-Path $DatasetDir 'manifest.json'))) {
        throw "Dataset manifest missing: $DatasetDir"
    }
    if (!(Test-Path -LiteralPath (Join-Path $DatasetDir 'metadata.jsonl'))) {
        throw "Dataset metadata missing: $DatasetDir"
    }

    $metadata = Join-Path $DatasetDir 'metadata.jsonl'
    $firstDirection = Select-String -LiteralPath $metadata -Pattern 'fp_direction' -SimpleMatch | Select-Object -First 1
    if ($null -eq $firstDirection) {
        throw "Dataset metadata does not contain fp_direction captions."
    }

    $firstImage = Get-ChildItem -LiteralPath (Join-Path $DatasetDir 'images') -Filter '*.png' -File | Select-Object -First 1
    if ($null -eq $firstImage) {
        throw "Dataset has no PNG images."
    }
    if (!(Test-StructuredDatasetImages -DatasetPath $DatasetDir)) {
        throw "Dataset transparency probe failed for $DatasetDir."
    }

    $manifest = Get-Content -LiteralPath (Join-Path $DatasetDir 'manifest.json') -Raw | ConvertFrom-Json
    if ($manifest.directional_only -ne $true) {
        throw "Dataset manifest does not indicate directional_only=true."
    }
    if ([int]$manifest.total_frames -le 0) {
        throw "Dataset manifest reports no frames."
    }
    if ([int]$manifest.stats.partial_sheets_skipped -le 0) {
        throw "Dataset manifest did not record skipped partial directional sheets."
    }

    Write-Log "Dataset OK: $($manifest.total_frames) directional frames, partial_sheets_skipped=$($manifest.stats.partial_sheets_skipped)."
}

Ensure-Directory -Path $FullOut
if (Test-Path -LiteralPath $LogPath) {
    $archive = Join-Path $FullOut ("overnight_train_" + (Get-Date -Format 'yyyyMMdd_HHmmss') + ".previous.log")
    Move-Item -LiteralPath $LogPath -Destination $archive -Force
}
New-Item -ItemType File -Force -Path $LogPath | Out-Null

Write-Log "Overnight directional LoRA training started."
Write-Log "ProjectRoot=$ProjectRoot"
Write-Log "DatasetDir=$DatasetDir"

if (!(Test-Path -LiteralPath $TrainPython)) { throw "Missing Python: $TrainPython" }
if (!(Test-Path -LiteralPath $TrainScript)) { throw "Missing train script: $TrainScript" }
if (!(Test-Path -LiteralPath $BaseModel)) { throw "Missing base model: $BaseModel" }

if (!$SkipDataset) {
    Invoke-Logged -Label 'structured directional dataset build' -Command {
        & $TrainPython $PrepScript --directional-only
    }
} else {
    Write-Log "Skipping dataset build by request."
}

Validate-Dataset

Ensure-Directory -Path $SmokeOut
Invoke-Logged -Label 'smoke training' -Command {
    & $TrainPython $TrainScript `
        --pretrained_model $BaseModel `
        --dataset $DatasetDir `
        --output_dir $SmokeOut `
        --output_name 'litiso_style_directional_smoke' `
        --resolution 512 `
        --train_limit 512 `
        --max_steps 250 `
        --batch_size 1 `
        --learning_rate 0.00004 `
        --rank 16 `
        --save_every 250 `
        --force_float32
}

$SmokeLora = Join-Path $SmokeOut 'litiso_style_directional_smoke.safetensors'
if (!(Test-Path -LiteralPath $SmokeLora)) {
    throw "Smoke training did not produce $SmokeLora"
}
Write-Log "Smoke LoRA OK: $SmokeLora"

Invoke-Logged -Label 'full training' -Command {
    & $TrainPython $TrainScript `
        --pretrained_model $BaseModel `
        --dataset $DatasetDir `
        --output_dir $FullOut `
        --output_name $FinalName `
        --resolution 512 `
        --train_limit 5000 `
        --max_steps 3000 `
        --batch_size 1 `
        --learning_rate 0.00004 `
        --rank 32 `
        --save_every 750 `
        --force_float32
}

if (!(Test-Path -LiteralPath $FinalLora)) {
    throw "Full training did not produce $FinalLora"
}

foreach ($step in '00750','01500','02250','03000') {
    $checkpoint = Join-Path $FullOut "$FinalName`_step$step.safetensors"
    if (!(Test-Path -LiteralPath $checkpoint)) {
        Write-Log "WARNING missing expected checkpoint: $checkpoint"
    } else {
        Write-Log "Checkpoint OK: $checkpoint"
    }
}

Ensure-Directory -Path $ComfyLoraDir
Copy-Item -LiteralPath $FinalLora -Destination $ComfyFinalLora -Force
Write-Log "Copied final LoRA to ComfyUI: $ComfyFinalLora"

if (!$SkipSamples) {
    try {
        Invoke-Logged -Label 'ComfyUI evaluation samples' -Command {
            & $TrainPython $EvalScript
        }
    } catch {
        Write-Log "Sample generation skipped/failed without failing training: $($_.Exception.Message)"
    }
} else {
    Write-Log "Skipping sample generation by request."
}

Write-Log "Overnight directional LoRA training complete."
