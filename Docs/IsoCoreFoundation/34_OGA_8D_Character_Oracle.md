# OpenGameArt 8D Character Oracle

Purpose: lock a known-good 8-direction human animation reference for LIT-ISO character generation, QA, and sprite-sheet packing.

## Source

- Page: `https://opengameart.org/content/400-items-basehumanmale-orc-skeleton`
- Local source root: `C:\Projects\Pixel Pipeline\sources\opengameart\400-items-basehumanmale-orc-skeleton`
- Archives: `C:\Projects\Pixel Pipeline\sources\opengameart\400-items-basehumanmale-orc-skeleton\archives`
- Extracted parts: `C:\Projects\Pixel Pipeline\sources\opengameart\400-items-basehumanmale-orc-skeleton\extracted`
- Download manifest: `C:\Projects\Pixel Pipeline\sources\opengameart\400-items-basehumanmale-orc-skeleton\download_manifest.json`
- License recorded from the source page: CC-BY 4.0
- Attribution recorded in the local manifest: LinksDream / OpenGameArt page credits

## Generated Oracle

Tool:

`Tools/AssetForge/build_oga_8d_character_oracle.py`

Launcher:

`Tools/AssetForge/build_oga_8d_character_oracle.ps1`

Output folder:

`C:\Projects\Pixel Pipeline\generated\oga_8d_character_oracle_v1`

Generated files:

- `BaseHumanMale_cam_oracle.png`
- `BaseHumanMale_litiso_8d_oracle.png`
- `manifest.json`

## Direction Mapping

The pack uses `CAM0..CAM7`. LIT-ISO uses the canonical order:

`S, SE, E, NE, N, NW, W, SW`

Mapped directions:

| LIT-ISO | Source camera |
|---|---|
| S | CAM7 |
| SE | CAM6 |
| E | CAM5 |
| NE | CAM4 |
| N | CAM3 |
| NW | CAM2 |
| W | CAM1 |
| SW | CAM0 |

This is the first verified true-diagonal character source in the local pipeline. The earlier LPC direction oracle remains useful, but it only covers cardinal directions.

## Verified Actions

The extracted BaseHumanMale set exposes these filename action groups:

- Idle
- Attack
- Bow
- Cast
- Walk
- Run
- Death

The source page mentions broader animation coverage, but a filename scan did not verify Hurt/Pain frames. Treat Hurt/Pain as missing until a deeper source inspection proves otherwise.

## Intended Use

Allowed immediately:

- Direction/action oracle for QA.
- Sprite-sheet packing reference.
- Pose/action vocabulary for prompts.
- Visual comparison target for generated 4D/8D characters.
- Unity import and animation-contract reference.
- Training source for local LIT-ISO motion/direction LoRAs and adapters, with attribution preserved.

Requires explicit approval before use:

- Shipping these pixels in the final game.
- Moving raw extracted sprites into `Assets/Resources` or `Assets/Art`.

## Training Approval

Owner approved using this source for training on 2026-06-08.

Training conditions:

- Keep this source in a labeled CC-BY training lane, not silently mixed with original-only datasets.
- Preserve attribution in dataset, model, and export manifests.
- Include the source URL, license, and creator/page credit in `Docs/IsoCoreFoundation/24_AssetForge_Training_Attributions.md`.
- Prefer using this pack to teach direction/action structure and motion consistency. Final LIT-ISO style should still be refined through original approved project art.
- Generated models trained from this source must record that CC-BY 4.0 material was included.

## Why This Matters

The current generator failures mostly come from direction ambiguity and animation inconsistency. This pack gives us a concrete, ordered, frame-based reference for what "south-east walk", "north cast", or "west attack" should mean. It does not solve LIT-ISO style by itself, but it gives the pipeline a correct motion and direction target.

Next recommended Asset Forge step: build a labeled training dataset index from this pack, then add an oracle comparison/packing pass that can compare generated character directions against this `S, SE, E, NE, N, NW, W, SW` reference order before generated outputs are approved into the original LIT-ISO dataset.
