# Wizard LoRA Test Notes

Source handoff: `C:\Users\garyc\OneDrive\Desktop\Prompt for Codex.txt`

## Context

The previous LoRA sprite test used the local ComfyUI instance with the trained `litisochar_anim_v1.safetensors` LoRA. The useful generation set was written to:

- `C:\Projects\ComfyUI\output\wizard_A_quick_00001_.png`
- `C:\Projects\ComfyUI\output\wizard_B_standard_00001_.png`
- `C:\Projects\ComfyUI\output\wizard_C_detailed_00001_.png`
- `C:\Projects\ComfyUI\output\wizard_v1_00002_.png`

The separate background-removal web service expected by `C:\Projects\Pixel Pipeline\remove_bg.py` was not running at `http://127.0.0.1:7878`, so the cleaned candidates here were produced with a local edge flood-fill pass.

## Cleaned Candidates

- `wizard_v1_clean_candidate.png`
  - Source: `wizard_v1_00002_.png`
  - Size: 424x424
  - Transparency verified
  - Best silhouette of the tested outputs, but still includes a magic-circle/backdrop element.

- `wizard_detailed_clean_candidate.png`
  - Source: `wizard_C_detailed_00001_.png`
  - Size: 511x512
  - Transparency verified
  - Cleanest illustration, but not a true in-game sprite. It reads as splash/portrait art because of the large staff effect and ground aura.

## Finding

The LoRA trigger is producing consistent wizard-themed character art, but the current prompt/settings are not strict enough for production sprites. The model keeps adding scene elements, magic circles, text, aura bases, and illustration composition.

## Recommended Next Prompt Shape

Use a stricter asset prompt:

```text
litisochar, single isometric RPG wizard character sprite, full body, standing idle pose, 3/4 top-down isometric angle, centered, transparent background, no text, no letters, no logo, no frame, no magic circle, no ground, no shadow blob, no aura base, no environment, no scenery, no background, clean silhouette, game sprite asset, readable at 96 pixels tall, simple staff, compact spell glow only on staff tip
```

Negative prompt:

```text
text, letters, logo, watermark, caption, magic circle behind character, ground circle, aura floor, platform, scenery, background, portrait, close-up, cropped body, huge spell effect, extra limbs, duplicate character, painterly illustration, blurry, low contrast
```

## Recommended Settings

- Keep using the standard/detailed range rather than native 128/256 attempts.
- Prefer 512 generation, then crop/downscale after background removal.
- Use moderate LoRA strength first: `0.65-0.85`.
- Avoid prompts that mention "epic", "poster", "splash", "cinematic", or "detailed background".
- Generate at least 8 candidates per class, then choose based on silhouette at 96px height.

