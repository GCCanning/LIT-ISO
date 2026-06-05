# In-Game UI Spec (HUD, settings, navigation)

> **Owner: Codex** (this is Foundation lane — `Assets/Scripts/IsoCoreFoundation/UI/FoundationHUD.cs`,
> an IMGUI/`OnGUI` HUD). Authored by Claude as a UX hand-off from owner feedback.
> Claude does not edit FoundationHUD; Claude owns the *menu* settings counterpart.

## Source of these requirements (owner, verbatim intent)
1. HUD should **scale with camera zoom** (in/out), like the ISO-Tile reference.
2. HUD should be a **vertical stack down the LEFT** of the screen, not spread across top-right.
3. **In-game settings** must be reachable (e.g. HUD scale, audio) while playing.
4. **Back/close** must exist on every in-game panel (settings, crafting, inventory…).

## 1. HUD layout — left column
Move the HUD widgets from top-right into a single left-anchored vertical stack,
top-to-bottom, with consistent gutter (e.g. 12px) from the screen's left edge:
1. Health bar
2. Energy + Food bars (when survival scope lands — see `menu-style-spec` sibling note)
3. Day / time-of-day indicator
4. (optional) trial-week category pips
- **Hotbar:** keep bottom-center (standard) OR bottom-left to match the column — owner's
  call; default to **bottom-center** unless they say otherwise.
- Each widget left-anchored: `x = gutter`, `y` accumulates downward. In IMGUI that's
  computing `Rect`s from a running `y` cursor at fixed left `x`.

## 2. HUD scale with camera zoom
Two scale inputs, combined:
- **User HUD-scale setting** `hudScale` (0.75–1.5, default 1.0) from in-game settings (§3).
- **Camera-zoom factor**: derive from the camera's `orthographicSize` vs a reference
  size, so zooming out shrinks the HUD and zooming in grows it (ISO-Tile feel). Clamp
  the zoom contribution (e.g. 0.85–1.25) so the HUD never becomes unreadable or huge.
- **Apply once** at the top of `OnGUI` via the IMGUI matrix:
  ```
  float s = hudScale * Mathf.Clamp(refOrthoSize / cam.orthographicSize, 0.85f, 1.25f);
  GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
  ```
  Then lay out all widgets in unscaled coordinates. Reset `GUI.matrix` at the end.
- Recompute hit-test rects (`_hotbarRect`, etc.) in the scaled space so clicks still land.

## 3. In-game settings overlay
- Open with **Esc** (and/or a small gear button in the HUD). Pauses input to the world
  while open (block placement/move, like the existing `_hotbarRect.Contains` input-guard
  pattern already in FoundationHUD).
- Controls (minimum): **HUD scale** slider (writes `hudScale`), master **volume**,
  and a **Resume** + **Quit to Menu** pair.
- Persist `hudScale`/volume via `PlayerPrefs` (simple, no save-file coupling).
- **Quit to Menu** → `SceneManager.LoadScene("MenuScene")`. (Claude's menu Options screen
  will mirror the same settings for parity; share the `PlayerPrefs` keys — propose
  `hud.scale`, `audio.master` — so both surfaces read/write the same values.)

## 4. Back / close on every panel
- Every overlay (settings, crafting, inventory, any future page) gets an explicit
  **Back/Close** affordance **and** responds to **Esc** to step back one level:
  panel open → Esc closes panel; nothing open → Esc opens settings.
- Crafting/inventory: a Close (✕) in the panel corner + Esc. Never trap the player in a
  panel with no exit.

## Coordination notes
- **Shared `PlayerPrefs` keys** (`hud.scale`, `audio.master`) are the contract between
  Codex's in-game settings and Claude's menu Options. Agree the key names before either
  side writes them.
- The **menu** counterpart (Options screen with the same sliders) is **Claude's lane**;
  Claude will build it to match once Codex confirms the keys.
- No gameplay-contract changes implied here — this is pure presentation/navigation.
