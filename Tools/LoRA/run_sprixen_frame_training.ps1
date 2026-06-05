param(
    [switch]$SkipDataset,
    [switch]$SkipSmoke,
    [switch]$SkipFull,
    [switch]$SkipSamples
)

$ErrorActionPreference = 'Stop'
$env:PYTHONWARNINGS = 'ignore::DeprecationWarning'

$ProjectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$LoRaRoot = 'C:\Projects\LoRA-Training'
$PixelPipelineRoot = 'C:\Projects\Pixel Pipeline'
$DatasetDir = Join-Path $PixelPipelineRoot 'style_examples\freepixel_sprixen_frame_only'
$SmokeOut = Join-Path $LoRaRoot 'outputs\litiso_sprixen_frame_smoke'
$FullOut = Join-Path $LoRaRoot 'outputs\litiso_sprixen_frame_v1'
$ComfyLoraDir = 'C:\Projects\ComfyUI\models\loras'
$FinalName = 'litiso_sprixen_frame_v1'
$FinalLora = Join-Path $FullOut "$FinalName.safetensors"
$ComfyFinalLora = Join-Path $ComfyLoraDir "$FinalName.safetensors"
$TrainScript = Join-Path $LoRaRoot 'scripts\train_litiso_lora_smoke.py'
$TrainPython = Join-Path $LoRaRoot '.venv\Scripts\python.exe'
$PrepScript = Join-Path $ProjectRoot 'Tools\LoRA\freepixel_sprixen_frame_dataset.py'
$EvalScript = Join-Path $ProjectRoot 'Tools\LoRA\eval_litiso_sprixen_frame_v1_comfy.py'
$BaseModel = 'C:\Projects\ComfyUI\models\checkpoints\DreamShaper_8_pruned.safetensors'
$LogPath = Join-Path $FullOut 'sprixen_frame_train.log'

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

function Validate-SprixenDataset {
    if (!(Test-Path -LiteralPath (Join-Path $DatasetDir 'manifest.json'))) {
        throw "Dataset manifest missing: $DatasetDir"
    }
    if (!(Test-Path -LiteralPath (Join-Path $DatasetDir 'metadata.jsonl'))) {
        throw "Dataset metadata missing: $DatasetDir"
    }

    $metadata = Join-Path $DatasetDir 'metadata.jsonl'
    foreach ($needle in 'litiso_sprixen','FreePixel character sprite frame','small 64x64 RPG walk-cycle sprite proportions','no environment') {
        $match = Select-String -LiteralPath $metadata -Pattern $needle -SimpleMatch | Select-Object -First 1
        if ($null -eq $match) {
            throw "Dataset metadata does not contain required caption token: $needle"
        }
    }

    $probe = @"
from pathlib import Path
from PIL import Image
import json, sys
root = Path(r'$DatasetDir')
images = sorted((root / 'images').glob('*.png'))
if not images:
    print('image_count=0')
    raise SystemExit(1)
corner_clear = 0
too_large = 0
for path in images[: min(512, len(images))]:
    im = Image.open(path).convert('RGBA')
    a = im.getchannel('A')
    if all(im.getpixel(pt)[3] == 0 for pt in [(0,0),(im.width-1,0),(0,im.height-1),(im.width-1,im.height-1)]):
        corner_clear += 1
    box = a.getbbox()
    if not box:
        continue
    w = box[2] - box[0]
    h = box[3] - box[1]
    if w > 112 or h > 112:
        too_large += 1
print(f'image_count={len(images)} sampled={min(512,len(images))} corner_clear={corner_clear} too_large={too_large}')
raise SystemExit(0 if corner_clear > 0 and too_large == 0 else 1)
"@
    $probe | & $TrainPython -
    if ($LASTEXITCODE -ne 0) {
        throw "Dataset transparency/size probe failed."
    }

    $manifest = Get-Content -LiteralPath (Join-Path $DatasetDir 'manifest.json') -Raw | ConvertFrom-Json
    if ([int]$manifest.total_frames -le 0) {
        throw "Dataset manifest reports no frames."
    }
    Write-Log "Dataset OK: $($manifest.total_frames) Sprixen-style frames."
}

Ensure-Directory -Path $FullOut
if (Test-Path -LiteralPath $LogPath) {
    $archive = Join-Path $FullOut ("sprixen_frame_train_" + (Get-Date -Format 'yyyyMMdd_HHmmss') + ".previous.log")
    Move-Item -LiteralPath $LogPath -Destination $archive -Force
}
New-Item -ItemType File -Force -Path $LogPath | Out-Null

Write-Log "Sprixen frame LoRA training started."
Write-Log "ProjectRoot=$ProjectRoot"
Write-Log "DatasetDir=$DatasetDir"

if (!(Test-Path -LiteralPath $TrainPython)) { throw "Missing Python: $TrainPython" }
if (!(Test-Path -LiteralPath $TrainScript)) { throw "Missing train script: $TrainScript" }
if (!(Test-Path -LiteralPath $BaseModel)) { throw "Missing base model: $BaseModel" }

if (!$SkipDataset) {
    Invoke-Logged -Label 'Sprixen-style frame dataset build' -Command {
        & $TrainPython $PrepScript --output-dir $DatasetDir
    }
} else {
    Write-Log "Skipping dataset build by request."
}

Validate-SprixenDataset

if (!$SkipSmoke) {
    Ensure-Directory -Path $SmokeOut
    Invoke-Logged -Label 'smoke training' -Command {
        & $TrainPython $TrainScript `
            --pretrained_model $BaseModel `
            --dataset $DatasetDir `
            --output_dir $SmokeOut `
            --output_name 'litiso_sprixen_frame_smoke' `
            --resolution 512 `
            --train_limit 512 `
            --max_steps 250 `
            --batch_size 1 `
            --learning_rate 0.00004 `
            --rank 16 `
            --save_every 250 `
            --force_float32
    }

    $SmokeLora = Join-Path $SmokeOut 'litiso_sprixen_frame_smoke.safetensors'
    if (!(Test-Path -LiteralPath $SmokeLora)) {
        throw "Smoke training did not produce $SmokeLora"
    }
    Write-Log "Smoke LoRA OK: $SmokeLora"
} else {
    Write-Log "Skipping smoke training by request."
}

if (!$SkipFull) {
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
            --learning_rate 0.000035 `
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
} else {
    Write-Log "Skipping full training by request."
}

Write-Log "Sprixen frame LoRA training complete."
