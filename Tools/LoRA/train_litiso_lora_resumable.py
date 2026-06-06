import argparse
import json
import time
from pathlib import Path

from PIL import Image
import torch
from torch.utils.data import Dataset, DataLoader
from torchvision import transforms
from diffusers import StableDiffusionPipeline, DDPMScheduler
from diffusers.utils import convert_state_dict_to_diffusers
from peft import LoraConfig, set_peft_model_state_dict
from peft.utils import get_peft_model_state_dict
from safetensors.torch import load_file, save_file
from tqdm import tqdm


class LocalImageCaptionDataset(Dataset):
    def __init__(self, root, resolution=512, limit=None, category=None):
        self.root = Path(root)
        records = []
        meta = self.root / "metadata.jsonl"
        if meta.exists():
            for line in meta.read_text(encoding="utf-8-sig").splitlines():
                if not line.strip():
                    continue
                item = json.loads(line)
                if category and item.get("category") != category:
                    continue
                img = self.root / item["file_name"]
                if img.exists():
                    records.append((img, item.get("text", "LIT-ISO pixel art asset")))
        else:
            for img in sorted((self.root / "images").glob("*.png")):
                cap = self.root / "captions" / f"{img.stem}.txt"
                text = cap.read_text(encoding="utf-8") if cap.exists() else "LIT-ISO pixel art asset"
                records.append((img, text))
        if limit:
            records = records[:limit]
        if not records:
            raise RuntimeError(f"No training images found in {self.root}")
        self.records = records
        self.tx = transforms.Compose([
            transforms.Resize((resolution, resolution), interpolation=transforms.InterpolationMode.NEAREST),
            transforms.ToTensor(),
            transforms.Normalize([0.5], [0.5]),
        ])

    def __len__(self):
        return len(self.records)

    def __getitem__(self, idx):
        img_path, text = self.records[idx]
        im = Image.open(img_path).convert("RGBA")
        bg = Image.new("RGBA", im.size, (255, 255, 255, 255))
        bg.alpha_composite(im)
        return {"pixel_values": self.tx(bg.convert("RGB")), "caption": text, "path": str(img_path)}


def write_status(path, **payload):
    path.parent.mkdir(parents=True, exist_ok=True)
    payload["updated_utc"] = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    path.write_text(json.dumps(payload, indent=2), encoding="utf-8")


def save_lora(pipe, out_dir, output_name, step, suffix=None):
    state = get_peft_model_state_dict(pipe.unet)
    state = convert_state_dict_to_diffusers(state)
    name = f"{output_name}_{suffix}.safetensors" if suffix else f"{output_name}_step{step:05d}.safetensors"
    path = out_dir / name
    save_file(state, path)
    return path


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--pretrained_model", required=True)
    ap.add_argument("--dataset", required=True)
    ap.add_argument("--output_dir", required=True)
    ap.add_argument("--output_name", default="litiso_resumable")
    ap.add_argument("--control_dir", required=True)
    ap.add_argument("--resume_lora")
    ap.add_argument("--resume_step", type=int, default=0)
    ap.add_argument("--category")
    ap.add_argument("--resolution", type=int, default=512)
    ap.add_argument("--train_limit", type=int, default=5000)
    ap.add_argument("--max_steps", type=int, default=3000)
    ap.add_argument("--batch_size", type=int, default=1)
    ap.add_argument("--learning_rate", type=float, default=4e-5)
    ap.add_argument("--rank", type=int, default=32)
    ap.add_argument("--seed", type=int, default=1234)
    ap.add_argument("--save_every", type=int, default=250)
    ap.add_argument("--force_float32", action="store_true")
    args = ap.parse_args()

    out = Path(args.output_dir)
    out.mkdir(parents=True, exist_ok=True)
    control = Path(args.control_dir)
    control.mkdir(parents=True, exist_ok=True)
    status_path = control / "status.json"
    pause_path = control / "pause.request"
    stop_path = control / "stop.request"
    for stale in (pause_path, stop_path):
        if stale.exists():
            stale.unlink()

    torch.manual_seed(args.seed)
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    dtype = torch.float32 if args.force_float32 else (torch.float16 if device.type == "cuda" else torch.float32)

    write_status(status_path, state="loading", step=args.resume_step, max_steps=args.max_steps, output_dir=str(out), device=str(device))
    pipe = StableDiffusionPipeline.from_single_file(
        args.pretrained_model,
        torch_dtype=dtype,
        safety_checker=None,
        local_files_only=True,
    )
    pipe.scheduler = DDPMScheduler.from_config(pipe.scheduler.config)
    pipe.vae.requires_grad_(False)
    pipe.text_encoder.requires_grad_(False)
    pipe.unet.requires_grad_(False)
    pipe.vae.to(device, dtype=dtype)
    pipe.text_encoder.to(device, dtype=dtype)
    pipe.unet.to(device, dtype=dtype)

    lora_config = LoraConfig(
        r=args.rank,
        lora_alpha=args.rank,
        init_lora_weights="gaussian",
        target_modules=["to_k", "to_q", "to_v", "to_out.0"],
    )
    pipe.unet.add_adapter(lora_config)
    if args.resume_lora:
        resume_state = load_file(args.resume_lora)
        set_peft_model_state_dict(pipe.unet, resume_state, adapter_name="default")

    trainable = [p for p in pipe.unet.parameters() if p.requires_grad]
    for p in trainable:
        p.data = p.data.float()
    opt = torch.optim.AdamW(trainable, lr=args.learning_rate, eps=1e-6)

    ds = LocalImageCaptionDataset(args.dataset, resolution=args.resolution, limit=args.train_limit, category=args.category)
    dl = DataLoader(ds, batch_size=args.batch_size, shuffle=True, num_workers=0)

    global_step = args.resume_step
    progress = tqdm(total=args.max_steps, initial=global_step, desc="LIT-ISO resumable LoRA")
    write_status(status_path, state="running", step=global_step, max_steps=args.max_steps, output_dir=str(out), records=len(ds), device=str(device))
    try:
        while global_step < args.max_steps:
            for batch in dl:
                if global_step >= args.max_steps:
                    break
                if pause_path.exists() or stop_path.exists():
                    suffix = "paused" if pause_path.exists() else "stopped"
                    saved = save_lora(pipe, out, args.output_name, global_step, suffix=f"{suffix}_step{global_step:05d}")
                    write_status(status_path, state=suffix, step=global_step, max_steps=args.max_steps, checkpoint=str(saved), output_dir=str(out))
                    progress.close()
                    print(saved)
                    return

                pixel_values = batch["pixel_values"].to(device=device, dtype=dtype)
                with torch.no_grad():
                    latents = pipe.vae.encode(pixel_values).latent_dist.sample() * pipe.vae.config.scaling_factor
                    noise = torch.randn_like(latents)
                    timesteps = torch.randint(0, pipe.scheduler.config.num_train_timesteps, (latents.shape[0],), device=device).long()
                    noisy_latents = pipe.scheduler.add_noise(latents, noise, timesteps)
                    tokens = pipe.tokenizer(batch["caption"], padding="max_length", truncation=True, max_length=pipe.tokenizer.model_max_length, return_tensors="pt")
                    encoder_hidden_states = pipe.text_encoder(tokens.input_ids.to(device))[0]
                pred = pipe.unet(noisy_latents, timesteps, encoder_hidden_states).sample
                loss = torch.nn.functional.mse_loss(pred.float(), noise.float(), reduction="mean")
                opt.zero_grad(set_to_none=True)
                if not torch.isfinite(loss):
                    raise RuntimeError(f"Non-finite loss at step {global_step}: {loss.item()}")
                loss.backward()
                torch.nn.utils.clip_grad_norm_(trainable, 1.0)
                opt.step()
                global_step += 1
                progress.update(1)
                progress.set_postfix(loss=f"{loss.item():.4f}")

                if args.save_every and global_step % args.save_every == 0:
                    saved = save_lora(pipe, out, args.output_name, global_step)
                    write_status(status_path, state="running", step=global_step, max_steps=args.max_steps, loss=float(loss.item()), checkpoint=str(saved), output_dir=str(out))
    finally:
        progress.close()

    final_path = save_lora(pipe, out, args.output_name, global_step, suffix="final")
    (out / f"{args.output_name}.json").write_text(json.dumps({
        "steps": global_step,
        "dataset": args.dataset,
        "train_limit": args.train_limit,
        "base": args.pretrained_model,
        "rank": args.rank,
        "category": args.category,
        "final": str(final_path),
    }, indent=2), encoding="utf-8")
    write_status(status_path, state="complete", step=global_step, max_steps=args.max_steps, checkpoint=str(final_path), output_dir=str(out))
    print(final_path)


if __name__ == "__main__":
    main()
