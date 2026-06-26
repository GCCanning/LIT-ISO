# How to Build and Run LIT-ISO as a Standalone Game

## TL;DR — Fastest Path

```
1. Tools > Iso World > Build And Validate Full Playtest Scene
2. Tools > Iso World > Build > Build And Run
```

That's it. The .exe will be built and launched automatically.

---

## 🛠 New Build Menu

A new `GameBuilder.cs` editor tool adds these menu items under **`Tools > Iso World > Build`**:

| Menu Item | What It Does |
|-----------|--------------|
| 🛠 **Build Standalone (Windows)** | Builds .exe to `Builds/LIT-ISO/` |
| ▶ **Build And Run** | Builds then immediately launches the .exe |
| 📂 **Open Builds Folder** | Opens the build output folder in Explorer |
| 🧹 **Clean Builds Folder** | Deletes previous builds (with confirmation) |

---

## 📋 Step-by-Step Walkthrough

### Step 1: Set up the scene (one-time)

```
Tools > Iso World > Build And Validate Full Playtest Scene
```

This creates the `InfinitePlainsPrototype.unity` scene with all features wired:
- World chunk manager
- Player with sprites, shadow, jump (3 blocks)
- Day/night music cycle
- Sun controller + dynamic lighting
- Drop shadows
- Vignette + atmospheric particles
- Camera with smooth follow + lookahead + shake

**Validation runs automatically** — check Console for:
```
✅ Full playtest scene ready!
```

### Step 2: Build the .exe

```
Tools > Iso World > Build > Build Standalone (Windows)
```

This will:
1. Configure Player Settings (name, version, resolution)
2. Configure Quality Settings (VSync, 4x MSAA, Anisotropic)
3. Add scene to Build Settings
4. Save the open scene
5. Build to `Builds/LIT-ISO/LIT-ISO.exe`
6. Show a dialog when complete

**Expected build time:** 1-3 minutes (first build), 30-60 seconds (subsequent builds)
**Expected size:** ~50-150 MB

### Step 3: Run the game

**Option A**: Use the menu — `Tools > Iso World > Build > Build And Run`

**Option B**: Manually launch — Open `Builds/LIT-ISO/LIT-ISO.exe` in File Explorer

**Option C**: From the build success dialog — click "Open Folder", then double-click the .exe

---

## ⚙️ Build Configuration

All these settings are applied automatically when you run the build:

### Player Settings
| Setting | Value |
|---------|-------|
| Product Name | `LIT-ISO` |
| Company | `LIT Games` |
| Version | `0.1.0` |
| Default Resolution | 1920×1080 |
| Window Mode | Fullscreen Window |
| Run In Background | ✅ Enabled |
| Resizable Window | ✅ Enabled |
| Splash Screen | ❌ Disabled (faster startup) |
| Color Space | Linear |
| Scripting Backend | Mono (faster builds) |

### Quality Settings
| Setting | Value |
|---------|-------|
| VSync | ✅ Enabled |
| Anti-Aliasing | 4× MSAA |
| Anisotropic Filtering | ✅ Enabled |
| Real-time Reflection Probes | ❌ Disabled (2D game) |

### Build Target
- **Platform**: Windows Standalone 64-bit
- **Architecture**: x64
- **Output**: `Builds/LIT-ISO/LIT-ISO.exe` + supporting files

---

## 🎮 Features That Will Be In Your Build

All features developed so far are **automatically included**:

### World & Movement
- ✅ Infinite procedural chunk world (Plains biome)
- ✅ 6 biomes available: Plains, Desert, Frozen Mountain, Frozen Cave, Temple, Basic
- ✅ Player movement (WASD)
- ✅ Jump (Space) — up to 3 blocks height
- ✅ Collision detection — cannot walk up cliffs
- ✅ Camera follow with smooth dampening and lookahead

### Day/Night Cycle
- ✅ 15-minute day + 15-minute night (30 min cycle)
- ✅ Day/night music with smooth crossfading
- ✅ Sun-driven directional lighting (rotation + intensity)
- ✅ Automatic lighting profile switching (Day → Dusk → Night)

### Visual Effects
- ✅ Dynamic drop shadow under player (follows sun)
- ✅ Vignette overlay (cinematic edges)
- ✅ Atmospheric particles (dust by day, fireflies at night)
- ✅ Camera shake on jump landings
- ✅ Zoomed-in cinematic view (orthographicSize=6)

### Gameplay Systems
- ✅ Inventory system (PlayerInventory)
- ✅ Hotbar UI
- ✅ Health bar UI
- ✅ Pickup notifications
- ✅ Interaction controller (right-click)
- ✅ Resource node framework
- ✅ Runtime data recording

### Mouse Controls
- ✅ Left click: Select tile
- ✅ Right click: Interact / Harvest
- ✅ Tile selection marker

### Keyboard Controls
- ✅ WASD / Arrow Keys: Move
- ✅ Space: Jump
- ✅ F6: Cycle lighting profiles manually
- ✅ 1-4: Set specific lighting profile

---

## 🐛 Troubleshooting

### "Scene not found" error
Run this first:
```
Tools > Iso World > Build And Validate Full Playtest Scene
```

### Build fails with compile errors
1. Open Console window (Window > General > Console)
2. Look for red errors — they prevent builds
3. Fix the errors and try again
4. Make sure scripts compiled successfully (check the spinner in bottom-right)

### Build succeeds but .exe doesn't launch
1. Check `Builds/LIT-ISO/` exists
2. Look for `LIT-ISO.exe` — should be ~5-10 MB
3. Check Windows Defender / antivirus isn't blocking
4. Try running `LIT-ISO.exe` from command line to see any errors:
   ```
   cd C:\Projects\Unity-Projects\LIT-ISO\Builds\LIT-ISO
   .\LIT-ISO.exe
   ```

### Game runs but no audio
- Check the AudioListener is on the Main Camera (SceneValidator handles this)
- Re-run `Tools > Iso World > Build And Validate Full Playtest Scene`

### Performance issues in the build
Build settings to try:
- In `GameBuilder.cs`, change `ScriptingImplementation.Mono2x` to `ScriptingImplementation.IL2CPP` — slower to build but ~30% faster runtime
- Reduce `GraphicsEnhancer.particleCount` to 30
- Disable `enableVignette` in Inspector

### .exe is too big (>500MB)
Inspect what's including:
- Open `Builds/LIT-ISO/LIT-ISO_Data/` — large files = likely uncompressed audio or textures
- In Unity, select audio files → Inspector → set Compression to Vorbis
- Build again

---

## 🚀 Distribution

Your `Builds/LIT-ISO/` folder contains everything needed to run the game:

```
Builds/LIT-ISO/
├── LIT-ISO.exe              ← The executable
├── UnityCrashHandler64.exe  ← Auto-included
├── UnityPlayer.dll          ← Auto-included
└── LIT-ISO_Data/            ← All game assets
    ├── Managed/
    ├── Resources/
    ├── StreamingAssets/
    └── ...
```

To distribute:
1. **Zip the entire `LIT-ISO/` folder** (not just the .exe)
2. Share the .zip with players
3. They extract and double-click `LIT-ISO.exe`

For Steam/itch.io, just upload this folder directly.

---

## 🎛 Want to Customize the Build?

Edit `Assets/Scripts/Editor/GameBuilder.cs`:

```csharp
private const string ProductName = "LIT-ISO";       // Game name
private const string CompanyName = "LIT Games";     // Studio name
private const string Version = "0.1.0";             // Version string
```

Or modify Player/Quality settings in `ConfigurePlayerSettings()` and `ConfigureQualitySettings()`.

---

## 📊 Build Pipeline Summary

```
┌──────────────────────────────────────────┐
│  Tools > Iso World > Build > Build And Run│
└──────────────────────────────────────────┘
                    ↓
        ┌───────────────────────┐
        │ ConfigurePlayerSettings│
        │ • Name, version       │
        │ • Resolution          │
        │ • Color space         │
        └───────────────────────┘
                    ↓
        ┌───────────────────────┐
        │ConfigureQualitySettings│
        │ • VSync               │
        │ • 4x MSAA             │
        └───────────────────────┘
                    ↓
        ┌───────────────────────┐
        │ EnsureSceneReady      │
        │ • Verify .unity exists│
        │ • Add to Build list   │
        └───────────────────────┘
                    ↓
        ┌───────────────────────┐
        │ ExecuteBuild          │
        │ • Compile scripts     │
        │ • Pack assets         │
        │ • Generate .exe       │
        └───────────────────────┘
                    ↓
        ┌───────────────────────┐
        │ ReportBuildSuccess    │
        │ • Show dialog         │
        │ • Optionally run      │
        └───────────────────────┘
```

---

## ✅ Quick Checklist Before Building

- [ ] Scripts compile without errors (no red in Console)
- [ ] `Tools > Iso World > Build And Validate Full Playtest Scene` succeeded
- [ ] Pressing Play in Editor works without errors
- [ ] All gameplay features tested in Editor
- [ ] Disk has at least 500MB free for the build output

If all checked → run **`Tools > Iso World > Build > Build And Run`**! 🚀

---

**Status:** Build pipeline ready ✅  
**Last Updated:** 2026-05-20
