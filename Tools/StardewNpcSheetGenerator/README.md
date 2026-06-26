# Stardew NPC Sheet Generator Wrapper

Local wrapper for the Nexus **SDV NPC Sheet Generator** tool.

The Nexus page describes a C# executable that renders pixel-perfect Stardew farmer/NPC frames using vanilla `FarmerRenderer`, then exports composed NPC folders and a `64x128` movement sheet. Nexus login is required for the download, so this repository only stores helper scripts and a small command-builder page.

## What You Need

- The Nexus file from `https://www.nexusmods.com/stardewvalley/mods/37108`.
- Extracted Stardew Valley PNG files. The tool expects these names in `input/`:
  - `accessories.png`
  - `farmer_base.png`
  - `farmer_base_bald.png`
  - `farmer_girl_base.png`
  - `farmer_girl_base_bald.png`
  - `hairstyles.png`
  - `hairstyles2.png`
  - `hats.png`
  - `pants.png`
  - `shirts.png`
  - `shoeColors.png`
  - `skinColors.png`
- A Stardew Valley game folder. The setup script copies `SDL2.dll`, `MonoGame.Framework.dll`, and related game assemblies when the tool is not running next to `Stardew Valley.exe`.

Use the Stardew IDs reference at `https://mateusaquino.github.io/stardewids/` for shirt, pants, hat, and accessory IDs.

## Setup

From the repo root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\StardewNpcSheetGenerator\setup_sdv_npc_generator.ps1 `
  -ToolZip "C:\Users\garyc\Downloads\SDV NPC Sheet Generator-37108-2-0.zip" `
  -ExtractedContentRoot "C:\Path\To\Extracted\Stardew\Content" `
  -StardewRoot "C:\Path\To\Stardew Valley"
```

The script stages files into `Tools\StardewNpcSheetGenerator\_local`, which is ignored by git.

## Run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\StardewNpcSheetGenerator\run_sdv_npc_generator.ps1 -Random 12 -Gender both -Seed 240611 -Hats yes -Accessories yes
```

Explicit outfit example:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\StardewNpcSheetGenerator\run_sdv_npc_generator.ps1 `
  -Gender male `
  -Shirt 1021 `
  -Pants 0 `
  -Hair 11 `
  -HairColor 191919 `
  -PantsColor 1e90ff `
  -Hat 4 `
  -Accessory 6 `
  -ShoeColor a0522d `
  -VerboseLog
```

Open `index.html` for a browser-based command builder.
