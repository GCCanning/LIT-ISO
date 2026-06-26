# In-Game Time Display & Zoom Controls

## What Was Added

| Feature | Description | Location |
|---------|-------------|----------|
| 🕐 **Clock display** | Shows in-game time + phase | Top-right corner |
| ➕➖ **Zoom buttons** | Click to zoom in/out/reset | Bottom-right corner |
| ⌨️ **Ctrl + =** | Zoom in (hold for smooth) | Keyboard |
| ⌨️ **Ctrl + -** | Zoom out (hold for smooth) | Keyboard |
| 🖱️ **Mouse wheel** | Quick zoom up/down | Optional, on by default |

---

## 🕐 Clock Display (Top-Right)

Shows the current in-game time with phase indicator:

```
┌──────────────┐
│      18:30   │   ← Time (24h format)
│  ☀ Afternoon │   ← Phase
└──────────────┘
```

### Cycle Layout

| Normalized Time | Display | Phase |
|----------------|---------|-------|
| 0.00 | 06:00 | ✦ Dawn |
| 0.08 | 08:00 | ☀ Morning |
| 0.20 | 11:00 | ☀ Morning |
| 0.25 | 12:00 | ☀ Noon |
| 0.30 | 13:00 | ☀ Afternoon |
| 0.42 | 16:00 | ☀ Afternoon |
| 0.50 | 18:00 | ✦ Dusk |
| 0.58 | 20:00 | ✦ Dusk |
| 0.70 | 22:30 | ☾ Evening |
| 0.75 | 00:00 | ☾ Midnight |
| 0.80 | 01:00 | ☾ Late Night |
| 0.92 | 04:30 | ✦ Pre-Dawn |

### Configurable in Inspector

On the `ClockDisplay` GameObject:
- **`showSeconds`** — Display HH:MM:SS instead of HH:MM
- **`use24HourFormat`** — `true` for "16:30", `false` for "4:30 PM"
- **`timeColor`** — Color of the time text
- **`phaseColor`** — Color of the phase text
- **`timeFontSize`** — Font size for the clock
- **`phaseFontSize`** — Font size for the phase indicator

---

## 🔍 Zoom Controls

### Keyboard Shortcuts

| Key | Action | Behavior |
|-----|--------|----------|
| `Ctrl + =` | Zoom **in** | Tap = small step, **Hold = smooth continuous** |
| `Ctrl + +` | Zoom **in** | (same — `+` is `Shift+=`) |
| `Ctrl + -` | Zoom **out** | Tap = small step, **Hold = smooth continuous** |
| `Mouse wheel up` | Zoom **in** | Quick step |
| `Mouse wheel down` | Zoom **out** | Quick step |

### UI Buttons (Bottom-Right)

```
   ┌───┐
   │ + │  ← Zoom in
   ├───┤
   │ − │  ← Zoom out
   ├───┤
   │ ⌂ │  ← Reset to default zoom
   └───┘
```

### Zoom Range

| Setting | Value | Effect |
|---------|-------|--------|
| `minZoom` | 3.0 | Closest possible zoom-in (extreme close-up) |
| `defaultZoom` | 6.0 | Starting / reset zoom |
| `maxZoom` | 14.0 | Farthest zoom-out |

### Behavior Tuning

| Setting | Default | What It Does |
|---------|---------|--------------|
| `holdZoomSpeed` | 6.0 | Speed of continuous zoom when holding keys |
| `tapZoomStep` | 1.0 | Instant change on first key press (snappy feel) |
| `scrollZoomSpeed` | 2.5 | Mouse-wheel sensitivity |
| `smoothing` | 10.0 | How quickly zoom catches up to target (higher = snappier) |
| `requireCtrl` | true | Set false to allow `=` / `-` zoom without Ctrl |
| `enableScrollWheel` | true | Disable to prevent mouse-wheel zoom |

---

## 🚀 How to Apply

### Automatic (happens during scene rebuild)
```
Tools > Iso World > Build And Validate Full Playtest Scene
```
The Quick Play Test setup now includes the time + zoom HUD automatically.

### Manual (add to current scene)
```
Tools > Iso World > Setup Time And Zoom HUD
```
Adds the clock + zoom buttons to whatever scene you have open.

---

## 🎮 Testing

After running setup, press Play. You should see:

1. ✅ **Top-right**: Clock showing time like "06:00" + "✦ Dawn"
2. ✅ **Bottom-right**: 3 buttons stacked vertically (`+`, `−`, `⌂`)
3. ✅ **Click + button** → Camera zooms in (smooth)
4. ✅ **Click − button** → Camera zooms out (smooth)
5. ✅ **Click ⌂ button** → Camera resets to default zoom
6. ✅ **Tap Ctrl + =** → Single zoom step in
7. ✅ **Hold Ctrl + =** → Continuous smooth zoom in
8. ✅ **Tap Ctrl + -** → Single zoom step out
9. ✅ **Hold Ctrl + -** → Continuous smooth zoom out
10. ✅ **Scroll mouse wheel** → Quick zoom up/down
11. ✅ **Watch the clock advance** as day/night cycle progresses

---

## 🛠 Files Created/Modified

### NEW
- `Assets/Scripts/UI/GameTimeUI.cs` — Clock display component
- `Assets/Scripts/ZoomController.cs` — Zoom input + smoothing
- `Assets/Scripts/Editor/TimeAndZoomHUDSetup.cs` — Editor tool to wire it up

### MODIFIED
- `Assets/Scripts/Editor/QuickPlayTestSetup.cs` — Auto-runs HUD setup
- `Assembly-CSharp.csproj` — Registered new runtime scripts
- `Assembly-CSharp-Editor.csproj` — Registered editor script

---

## 🐛 Troubleshooting

### "I don't see the clock"
1. Run `Tools > Iso World > Setup Time And Zoom HUD`
2. Press Play
3. Check the top-right corner of the Game view

### "Clock shows '—' / placeholder"
- `DayNightMusicManager` isn't in the scene
- Run `Tools > Iso World > Build And Validate Full Playtest Scene`

### "Ctrl+= doesn't zoom"
1. Make sure Game view has focus (click on it)
2. Check that the Main Camera has a `ZoomController` component
3. Verify `requireCtrl` is true in Inspector (or set to false to test without Ctrl)

### "Zoom snaps too quickly / too slowly"
- Adjust `smoothing` in Inspector
  - Lower = floatier (e.g. 5)
  - Higher = snappier (e.g. 15)
- Adjust `holdZoomSpeed` for hold-key zoom speed (default 6.0)

### "Buttons don't work"
- Confirm Main Camera has `ZoomController` component
- Try re-running `Tools > Iso World > Setup Time And Zoom HUD` to re-wire button callbacks

### "Zoom + camera follow are fighting"
- They don't share state — `CameraFollow` controls position, `ZoomController` controls orthographicSize
- If they appear to fight, check that nothing else (like a script) is overriding orthographicSize

---

## 🎛 Inspector Customization

After setup, you can fine-tune everything in the Inspector:

### Main Camera → ZoomController
- Change min/max/default zoom values
- Adjust speeds
- Toggle scroll wheel or Ctrl requirement

### GameplayHUD → ClockDisplay → GameTimeUI
- Toggle 12h/24h format
- Toggle seconds display
- Customize colors and font sizes

---

## 💡 Bonus: Use the API

If you want to trigger zoom from your own scripts:

```csharp
// Get the camera's zoom controller
ZoomController zoom = Camera.main.GetComponent<ZoomController>();

// Zoom in/out programmatically
zoom.ZoomIn();
zoom.ZoomOut();
zoom.ResetZoom();

// Set a specific zoom level
zoom.SetZoom(8f);

// Get current zoom as 0-1 for UI sliders
float zoomPercent = zoom.GetZoomPercent();
```

---

**Status:** Time display + Zoom controls fully wired ✅  
**Last Updated:** 2026-05-20
