# Welcome Screen System — Integration Guide

## Overview

The Welcome Screen system provides a **Minecraft-style main menu** with world creation and save/load functionality. It's fully procedural (no sprites required) and integrates seamlessly into the golden path.

### Components

| Component | Role | Location |
|-----------|------|----------|
| `WelcomeScreenManager` | Main menu UI (New Game / Load Game / Options / Quit) | Scripts/UI/ |
| `WorldManager` | Persistent world config (seed, difficulty, name) | Scripts/World/ |
| `GameStartupManager` | Entry point that shows welcome screen | Scripts/Managers/ |
| `QuickPlayTestSetup` | Editor tool that wires the full scene + WorldManager | Scripts/Editor/ |

---

## Player Flow (Production)

### 1. Game Launches
```
SampleScene (or dedicated MenuScene) loads
  → GameStartupManager.Awake() runs
    → Creates WorldManager (DontDestroyOnLoad)
    → Shows WelcomeScreenManager canvas
```

### 2. Main Menu
Player sees four buttons:
- **New Game** → Create World screen
- **Load Game** → Load World screen (list of saved worlds)
- **Options** → Settings screen (placeholder)
- **Quit** → Exit game

### 3. Create World
Player enters:
- World name (defaults to "Untitled World" if blank)
- Seed (defaults to random if blank)
- Difficulty slider (Easy / Normal / Hard)

On **Play**, the world is:
1. Saved to `Application.persistentDataPath/LitIsoWorlds/{name}_{timestamp}.world.json`
2. Passed to `WorldManager.SetWorld(name, seed, difficulty)`
3. Scene loads to SampleScene
4. `IsoWorldChunkManager.Awake()` reads seed from WorldManager and uses it for procedural generation

### 4. Load Game
Player sees a scrollable list of saved worlds (sorted by most recent):
- Each entry shows: World Name + Seed
- **Play** button → launches that world
- **Delete** button → removes the save file

### 5. Gameplay
The world loads with the configured seed + difficulty, and trial week begins.

---

## Developer Flow (Development Build)

### Quick Play Test Setup

**Menu:** Tools > LIT-ISO > Playtest > Quick Play Test

This one-click tool:
1. Creates/finds IsoWorldGrid with full biome setup
2. Creates/finds Player with all gameplay scripts
3. Creates all required managers (lighting, sound, UI, etc.)
4. **Creates WorldManager** with dev defaults (seed=12345, difficulty=Normal)
5. Saves the scene and reports readiness

### Testing Without Welcome Screen

For rapid iteration, `GameStartupManager.skipMenuInDevelopment` is set to `true` by default:
- In Debug builds, the welcome screen is skipped
- World loads directly with dev config
- Useful for testing gameplay, not the menu itself

### Testing the Welcome Screen

To test the menu in-editor:
1. Create a dedicated scene with just `GameStartupManager`
2. Set `skipMenuInDevelopment = false` on the GameStartupManager component
3. Press Play
4. Test Create World, Load Game, world persistence

---

## Save/Load Persistence

### Save Format

Worlds are saved as JSON in `Application.persistentDataPath/LitIsoWorlds/`:

```json
{
  "worldName": "My Adventure",
  "seed": "45678",
  "difficulty": 1,
  "createdTicks": 637123456789012345
}
```

File name: `{worldName}_{createdTicks}.world.json`

### Load Behavior

When WelcomeScreenManager initializes:
1. Scans the worlds folder for all `.world.json` files
2. Deserializes each into a `WorldSaveData` object
3. Sorts by creation time (newest first)
4. Displays in a scrollable list
5. Supports delete (removes the JSON file)

---

## Data Flow Diagram

```
┌──────────────────────────────────────────────────────────┐
│ Game Startup (SampleScene)                               │
│ ┌─ GameStartupManager.Awake()                            │
│ │  └─ Creates WorldManager (DontDestroyOnLoad)           │
│ │  └─ Shows WelcomeScreenManager                         │
│ └─────────────────────────────────────────────────────────┤
│                                                           │
│ User Input → WelcomeScreenManager                        │
│  ├─ New Game                                             │
│  │  └─ Saves world → calls LaunchWorld()                 │
│  │     └─ WorldManager.SetWorld(name, seed, diff)        │
│  │     └─ SceneManager.LoadScene("SampleScene")          │
│  └─ Load Game                                            │
│     └─ Lists saved worlds                                │
│        └─ User selects → LaunchWorld()                   │
│           └─ WorldManager.SetWorld(...)                  │
│           └─ SceneManager.LoadScene("SampleScene")       │
│                                                           │
│ SampleScene (Gameplay)                                   │
│ ┌─ IsoWorldChunkManager.Awake()                          │
│ │  └─ Reads WorldManager.Seed                            │
│ │  └─ Uses seed for Perlin noise → procedural world      │
│ └─────────────────────────────────────────────────────────┤
│                                                           │
│ TrialWeekManager.Awake()                                 │
│  └─ Reads WorldManager.Difficulty                        │
│  └─ Adjusts spawn rates / enemy health / loot            │
│                                                           │
└──────────────────────────────────────────────────────────┘
```

---

## Integration Checklist

- [ ] **Scene Setup**
  - [ ] Create or update SampleScene with all gameplay systems (use QuickPlayTestSetup)
  - [ ] Ensure IsoWorldChunkManager is present
  - [ ] Ensure TrialWeekManager is present

- [ ] **Add WelcomeScreenManager to startup flow**
  - [ ] Option A: Add GameStartupManager to SampleScene (empty GameObject)
  - [ ] Option B: Create dedicated MenuScene, add GameStartupManager, load it first
  - [ ] Option C: Manually instantiate WelcomeScreenManager in your startup code

- [ ] **Verify WorldManager integration**
  - [ ] IsoWorldChunkManager reads seed from WorldManager.Instance
  - [ ] TrialWeekManager (if present) reads difficulty from WorldManager.Instance
  - [ ] WelcomeScreenManager calls WorldManager.SetWorld() before scene load

- [ ] **Test Save/Load**
  - [ ] Create a world via welcome screen
  - [ ] Verify JSON saved to `Application.persistentDataPath/LitIsoWorlds/`
  - [ ] Load the world from the list
  - [ ] Verify seed/difficulty are applied correctly

- [ ] **Test Development Flow**
  - [ ] Run `Tools > LIT-ISO > Playtest > Quick Play Test`
  - [ ] Press Play — should load SampleScene with dev world (skip menu)
  - [ ] Verify world generates and gameplay works

---

## Customization

### Change Default Difficulty
Edit `WorldManager.cs` line 14:
```csharp
public int Difficulty { get; private set; } = 1;  // 0=easy, 1=normal, 2=hard
```

### Change Colors/Layout
All UI is procedural. Edit color fields in `WelcomeScreenManager.cs`:
```csharp
public Color panelBg = new Color(0.07f, 0.09f, 0.13f, 0.95f);
public Color buttonBg = new Color(0.15f, 0.18f, 0.24f, 0.9f);
// etc.
```

Or adjust layout fields:
```csharp
public float panelWidth = 500f;
public float panelHeight = 600f;
public float buttonHeight = 50f;
```

### Sprite Assignment (Future)
Every button/panel uses `Image` components. Once art assets are ready:
1. Drag sprites onto the Image components (they're sprite-ready, no code needed)
2. Update colors/alpha if needed
3. All procedurally-created elements will display the assigned sprites

---

## Troubleshooting

**"NullReferenceException in WelcomeScreenManager.LaunchWorld()"**
- Ensure WorldManager.cs exists in the project
- Verify WorldManager is added to an active GameObject before WelcomeScreenManager tries to access it

**Worlds not saving**
- Check `Application.persistentDataPath/LitIsoWorlds/` exists and is writable
- Verify no I/O exceptions in console
- Check file system permissions on the save folder

**Seed not being used in world generation**
- Verify IsoWorldChunkManager.Awake() runs after WorldManager is created
- Check that WorldManager.Instance is not null in IsoWorldChunkManager
- Verify the seed parsing logic (int vs string hash) handles your seed format

**Welcome screen not showing**
- Ensure GameStartupManager is in the scene and enabled
- Check `skipMenuInDevelopment` is false (or build a Release build, not Debug)
- Verify WelcomeScreenManager.BuildCanvas() is executing (check console logs)

---

## Next Steps

1. **Integrate into your startup scene** (see Integration Checklist above)
2. **Test the golden path**: Create world → Play → Gameplay
3. **Tune UI colors/layout** to match your game's art style
4. **Generate sprites for UI elements** once design is locked
5. **Add more Options** (volume, graphics settings, etc.) as needed
