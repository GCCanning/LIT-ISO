# Direction-aware FreePixel LoRA training wrapper.
#
# First generate the structured dataset:
#   python Tools\LoRA\freepixel_structured_dataset.py --directional-only
#
# Then run this script from the project root or paste the command into PowerShell.

$root = 'C:\Projects\LoRA-Training'

& "$root\.venv\Scripts\python.exe" "$root\scripts\train_litiso_lora_smoke.py" `
  --pretrained_model 'C:\Projects\ComfyUI\models\checkpoints\DreamShaper_8_pruned.safetensors' `
  --dataset 'C:\Projects\Pixel Pipeline\style_examples\freepixel_web_download_structured' `
  --output_dir "$root\outputs\litiso_style_directional_v1" `
  --output_name 'litiso_style_directional_v1' `
  --resolution 512 `
  --train_limit 5000 `
  --max_steps 3000 `
  --batch_size 1 `
  --learning_rate 0.00004 `
  --rank 32 `
  --save_every 750 `
  --force_float32
