# Main-menu images — drop them in THIS folder

`WelcomeScreenManager` auto-loads these by filename at runtime (no inspector wiring).
Every slot is **optional** — any you leave out falls back to the procedural look, so
add them one at a time and they'll appear.

| Drop a PNG named… | Slot | What it is | Suggested size |
|---|---|---|---|
| `background.png` | splash | full-screen menu art (cover-fit, won't stretch) | 1920×1080 (16:9) |
| `logo.png` | title | game wordmark, shown top of main menu | ~800×360, transparent |
| `panel.png` | card frame | 9-sliced panel behind the buttons/inputs | ~512×640, set Border |
| `button.png` | button (rest) | 9-sliced button background | ~256×72, set Border |
| `button_hover.png` | button (hover) | 9-sliced hover/press state | ~256×72, set Border |

## Import settings (select the PNG in Unity → Inspector)
- **Texture Type:** `Sprite (2D and UI)`
- **Pixel art?** set **Filter Mode = Point**, **Compression = None** (crisp pixels)
- **`panel.png` / `button*.png`:** click **Sprite Editor** and set the **Border**
  (L/R/T/B) so the 9-slice corners don't stretch.
- `logo.png` / `background.png`: no border needed.

## Notes
- File **names matter** (lowercase, exactly as above). The loader looks for
  `Resources/UI/Menu/<name>`.
- You can also assign any slot directly on the `WelcomeScreenUI` object in MenuScene
  (fields under "Menu skin") — an inspector assignment wins over the file.
- The old `Resources/UI/CampfireMenu.png` still works as a background fallback if no
  `background.png` is present.
