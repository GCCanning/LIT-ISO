# LIT-ISO Farmer Character Builder

Static browser tool for reviewing layered Mana Seed / MSCA-style farmer sprites without importing licensed source art into the repo.

## Use

1. Open `index.html` in a browser.
2. Click **Load sprite-system folder** and select the folder that contains layer folders such as `00undr`, `01body`, `13hair`, and `14head`.
3. Pick layer variants, randomize layers/styles, preview animations, and export PNG review frames, strips, presets, or an approximate 8-direction LIT-ISO walk sheet.

## Source Contract

The tool follows the local Godot builder contract from `mana_seed_farmer_style_character_godot_builder-main`:

- layer folders are ordered `00undr` through `14head`;
- files are named like `fbas_01body_human_00.png`;
- spritesheets are treated as a `16 x 16` frame grid;
- the animation frame indices match the builder script for idle, walk, run, death, and forehand strike.

The referenced MSCA quickstart describes the same high-level workflow: point the tool at a sprite-system root and build either a layered player or a compact player node. This web tool keeps that local-folder workflow but exports browser review artifacts instead of Godot nodes.

## License Boundary

Do not commit paid Farmer Sprite System sheets into this repo unless the license explicitly permits it. This tool stores only code and review presets. Browser-loaded art stays local until you intentionally export a PNG.
