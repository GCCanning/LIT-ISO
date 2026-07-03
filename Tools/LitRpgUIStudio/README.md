# LIT-RPG UI Studio

Standalone browser editor for approving LIT-RPG screen layouts before changing
the Unity runtime.

## Open

Run `open_ui_studio.bat` or open `index.html` directly from disk. The tool has
no server, package, build, or network dependency.

## Workflow

1. Select a screen from the left rail.
2. Drag or resize components in **Edit** mode.
3. Use **Preview** mode to test tabs, menu choices, hotbar selection, class
   selection, and other lightweight interactions.
4. Adjust component coordinates, anchors, runtime node IDs, resource keys,
   screen darkness, UI scale, and global theme tokens in the inspector.
5. Export the Unity handoff JSON from **Export** or the **Handoff** tab.
6. Keep the exported JSON with the implementation task after the layout is
   approved.

Changes autosave to guarded browser `localStorage`. Downloaded JSON remains the
portable source of truth because `file://` browser storage can vary by browser.

## Export Contract

The export schema is `litiso-ui-export/v1`.

It contains:

- the 1920x1080 design resolution and selected preview resolution;
- virtual-pixel and normalized component rectangles;
- Unity-style anchors and pivots;
- warm-cozy theme tokens;
- existing runtime surface and GameObject target names;
- `Resources` keys without file extensions;
- data-binding IDs for HUD, inventory, abilities, and map pins;
- one PixelLab prompt and required asset list per exported screen.

The editor does not modify Unity scenes or runtime code. Once a layout is
approved, implementation should consume the handoff deliberately and preserve
the current runtime defaults for missing nodes.

## Files

- `index.html`: application shell and inspector controls.
- `styles.css`: editor chrome and all interactive screen mockups.
- `studio-data.js`: screen catalog, default layouts, runtime targets, and asset
  IDs. It uses a local JavaScript global so the tool works under `file://`.
- `app.js`: rendering, edit interactions, persistence, history, import/export,
  and PixelLab prompt generation.
- `assets/campfire-party-concept.png`: approved party campsite concept used by
  menu, loading, gameplay, and pause previews.
- `assets/campfire-night.png`: retained fallback mood anchor.

## Franuka RPG UI Pack (v1.6)

The full purchased "RPG UI pack" by Franuka lives in `assets/franuka/`:

- `individual/{1x,2x,3x}/<category>/` — 631 PNGs per scale (boxes, buttons,
  slots, sliders, orbs, checkboxes, icons, spellbook, banners, dividers,
  cursors, font sheets).
- `fonts/` — FantasyRPGtext, FantasyRPGtitle, FantasyRPGtitleOutline (TTF).
- `sheets/` — full spritesheets and the reference sheet.
- `license.txt` — commercial use OK; credit franuka.itch.io if possible.

Studio integration:

- **Skin**: mockups render with Franuka frames, buttons, slots and fonts.
  Toggle under **Theme → "Franuka pack skin on mockups"**.
- **Assets tab**: browse/search all 631 assets (1x/2x/3x preview). With a
  component selected, click a tile to assign it as that component's skin;
  the Element tab shows the assignment plus a 9-slice border control
  (source px, 0 = stretch).
- **Handoff**: exports include `franukaSkin { file, slicePx }` per component
  and an `assetPack` credit block. Imports restore skin assignments.
- `franuka-manifest.js` is generated from `assets/franuka/individual/1x`;
  regenerate it if pack files change.

Unity font tips from the author: use the Font Asset Creator with custom
sampling size per font (text 8, title 11, outline 13); align
FantasyRPGtitleOutline 1px higher on Y than FantasyRPGtitle at the same X.
