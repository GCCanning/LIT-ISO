# PHASE 2 INTEGRATION GUIDE
## Co-op, Dungeons, Proximity Penalty, Weighted Scoring

---

## 📦 PHASE 2 DELIVERABLES

```
Assets/Scripts/
├── SerializableData_Phase2.cs      [Enhanced data structures]
├── TrialWeekManager_Phase2.cs      [Per-player trial timers]
├── ProximityPenaltySystem.cs       [Open-world carry prevention]
├── DungeonInstanceSystem.cs        [Squad dungeons + boost]
├── DifficultyConfig.cs             [Difficulty multipliers]
├── ActionTracker.cs                [Action logging + multipliers]
├── ScoringWeightCalculator.cs      [Weighted score calculation]
└── SaveGameManager_Phase2.cs       [Co-op multi-player saves]
```

---

## 🎯 PHASE 2 SYSTEMS OVERVIEW

### **1. TrialWeekManager (Enhanced)**
- **Per-player trial timers** (each player's 7-day countdown from join time)
- **Shared world day/night cycle** (all players see dusk/dawn together)
- **Individual trial events** (OnPlayerTrialStart, OnPlayerDayChanged, OnPlayerTrialEnd)
- **Multi-player tracking** (RegisterPlayerTrial, ResumePlayerTrial per player)

**New API:**
```csharp
TrialWeekManager.Instance.RegisterPlayerTrial(playerId);
TrialWeekManager.Instance.ResumePlayerTrial(playerId, joinTimestamp, savedState);
TrialWeekManager.Instance.IsPlayerInTrial(playerId);
TrialWeekManager.Instance.AreAllPlayersInTrial();  // For squad boost
```

---

### **2. ProximityPenaltySystem (NEW)**
- **Open-world only** (no penalty in dungeons)
- **80% action point reduction** when other players nearby (0.2x multiplier)
- **Scaled by action category** (combat/exploration 80%, crafting 40%, homesteading 0%)
- **Point-of-action check** (penalty applied at moment action completes)

**Key Feature:** Prevents carrying/power-leveling while allowing mentorship after trial ends.

**API:**
```csharp
ProximityPenaltySystem.Instance.UpdatePlayerPosition(playerId, position);
float multiplier = ProximityPenaltySystem.Instance.GetProximityMultiplier(playerId, actionCategory);
bool wouldPenalize = ProximityPenaltySystem.Instance.WouldPenaltyApply(playerId);
```

---

### **3. DungeonInstanceSystem (NEW)**
- **Instance management** (create, track, query dungeons)
- **Squad boost eligibility** (1.5x points if ALL 4 players in trial together)
- **Treasure/mob tracking** (chests opened, mobs defeated)
- **Boss defeat tracking** (for story progression)

**Key Feature:** Incentivizes group content while preventing carry (boost only works if all 4 trialing).

**API:**
```csharp
DungeonInstanceSystem.Instance.CreateDungeon(id, name, location, difficulty);
float boostMultiplier = DungeonInstanceSystem.Instance.GetSquadBoostMultiplier(dungeonId);
DungeonInstanceSystem.Instance.OpenChest(dungeonId, chestId, playerId);
DungeonInstanceSystem.Instance.DefeatMob(dungeonId, mobId, playerId);
```

---

### **4. DifficultyConfig (NEW)**
- **Mob stat multipliers** (health, damage, effects)
- **Action weight multipliers** (harder = more points)
- **Squad boost scaling** (Easy 1.3x → Hardcore 2.0x)
- **Proximity radius scaling** (Easy 75 units → Hardcore 20 units)

**Values:**
```
Easy:      0.7x health, 0.7x damage, 0.8x action weight, 1.3x squad boost
Normal:    1.0x health, 1.0x damage, 1.0x action weight, 1.5x squad boost
Hard:      1.5x health, 1.5x damage, 1.4x action weight, 1.7x squad boost
Hardcore:  2.0x health, 2.0x damage, 2.0x action weight, 2.0x squad boost
```

**API:**
```csharp
DifficultyConfig.Instance.SetDifficulty(GameDifficulty.Hard);
float multiplier = DifficultyConfig.Instance.GetActionWeightMultiplier();
```

---

### **5. ActionTracker (Enhanced)**
- **Action logging** with multiplier recording
- **Per-player action history** (saved/loaded)
- **Points calculation** (baseWeight × difficulty × proximity/boost)
- **Statistics** (total points, by category, squad boosted, penalized)

**API:**
```csharp
ActionTracker.Instance.LogAction(playerId, "kill", "Goblin", baseWeight, location);
float totalPoints = ActionTracker.Instance.GetTotalPoints(playerId);
ActionRecord[] actions = ActionTracker.Instance.GetPlayerActions(playerId);
ActionTracker.Instance.GetPointsByCategory(playerId);  // Dictionary<category, points>
```

---

### **6. ScoringWeightCalculator (NEW)**
- **Action-to-score mapping** (kill → combat, craft → crafting, etc.)
- **Weighted score calculation** (sums final points per category)
- **Returns ScoringData** (8 categories with final scores)

**API:**
```csharp
ActionRecord[] history = ActionTracker.Instance.GetPlayerActions(playerId);
ScoringData scores = ScoringWeightCalculator.Instance.CalculateScores(history);
// scores.combatScore, scores.magicScore, scores.craftingScore, etc.
```

---

### **7. SaveGameManager (Enhanced for Co-op)**
- **1-4 players per world** (CreateNewMultiplayerWorld, AddPlayerToWorld)
- **Shared world data** (dungeons, biomes, POIs persist for all players)
- **Per-player progress** (individual trial states, actions, scores)
- **Co-op mode flag** (isCoopEnabled)

**API:**
```csharp
// Single-player (Phase 1 compat)
SaveGameManager.Instance.CreateNewTrial(slotId, "Player1");

// Co-op
SaveSlot world = SaveGameManager.Instance.CreateNewMultiplayerWorld(slotId, "Player1", enableCoop: true);
SaveGameManager.Instance.AddPlayerToWorld(slotId, "Player2");

SaveGameManager.Instance.SaveCurrentGame();
SaveGameManager.Instance.LoadTrialFromSlot(slotId);
```

---

### **8. SerializableData (Enhanced)**
- **New: WorldData** (shared biome layout, dungeons, POIs)
- **New: DungeonInstance** (instance state, treasures, mobs)
- **Enhanced: ActionRecord** (multiplier tracking)
- **Enhanced: PlayerProgress** (per-player data in co-op)
- **Enhanced: SaveSlot** (multi-player support)

---

## 🔌 INTEGRATION GUIDE

### **STEP 1: Scene Setup**

In your main menu scene, create manager GameObjects:

```
Managers (GameObject)
├── TrialWeekManager (component)
├── ProximityPenaltySystem (component)
├── DungeonInstanceSystem (component)
├── DifficultyConfig (component)
├── ActionTracker (component)
├── ScoringWeightCalculator (component)
├── SaveGameManager (component)
└── WorldSeedManager (component) [from Phase 1]
```

All have `DontDestroyOnLoad` in Awake, so they persist across scenes.

---

### **STEP 2: Create New World (Main Menu)**

```csharp
public class MainMenuController : MonoBehaviour
{
    public void OnCreateNewWorld(string playerName, bool enableCoop, GameDifficulty difficulty)
    {
        // 1. Set difficulty
        DifficultyConfig.Instance.SetDifficulty(difficulty);

        // 2. Create world
        SaveSlot world = SaveGameManager.Instance.CreateNewMultiplayerWorld(
            slotId: 1,
            playerName: playerName,
            enableCoopMode: enableCoop
        );

        // 3. Initialize trial week for first player
        TrialWeekManager.Instance.RegisterPlayerTrial(playerName);

        // 4. Initialize action tracking
        ActionTracker.Instance.InitializePlayer(playerName);

        // 5. Load game scene
        SceneManager.LoadScene("MainGame");
    }
}
```

---

### **STEP 3: Handle Player Actions (Gameplay)**

When player performs an action:

```csharp
public class PlayerController : MonoBehaviour
{
    private string playerId;
    private TrialWeekManager trialManager;
    private ActionTracker actionTracker;
    private ProximityPenaltySystem proximitySystem;

    private void OnEnemyDefeated(string enemyName)
    {
        // Get base weight for this action
        ScoringWeightCalculator calc = ScoringWeightCalculator.Instance;
        ActionWeightConfig config = calc.GetActionWeightConfig("kill");

        // Log action (applies multipliers automatically)
        actionTracker.LogAction(
            playerId,
            actionType: "kill",
            targetName: enemyName,
            baseWeight: config.baseWeight,
            location: "open_world"  // or "dungeon_123"
        );

        // Update proximity system (for next action's penalty check)
        proximitySystem.UpdatePlayerPosition(playerId, transform.position);
    }

    private void OnEnteredDungeon(int dungeonId)
    {
        // Track dungeon entry
        DungeonInstanceSystem.Instance.RegisterPlayerEntry(dungeonId, GetPlayerId());
    }

    private void OnOpenedChest(int dungeonId, int chestId)
    {
        DungeonInstanceSystem.Instance.OpenChest(dungeonId, chestId, GetPlayerId());

        // Log as treasure discovery
        actionTracker.LogAction(
            playerId,
            actionType: "find_treasure",
            targetName: $"Chest {chestId}",
            baseWeight: 20,
            location: $"dungeon_{dungeonId}"  // Squad boost applies here
        );
    }
}
```

---

### **STEP 4: Handle Co-op Entry (Lobby/Matchmaking)**

When second player joins:

```csharp
public class CoopLobby : MonoBehaviour
{
    public void OnPlayerJoinedWorld(string newPlayerName, int slotId)
    {
        // 1. Add player to world
        bool added = SaveGameManager.Instance.AddPlayerToWorld(slotId, newPlayerName);
        if (!added)
        {
            Debug.LogError("Failed to add player");
            return;
        }

        // 2. Register trial for new player
        SaveSlot world = SaveGameManager.Instance.GetSaveSlot(slotId);
        long joinTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        TrialWeekManager.Instance.RegisterPlayerTrial(newPlayerName);

        // 3. Initialize action tracking
        ActionTracker.Instance.InitializePlayer(newPlayerName);

        Debug.Log($"{newPlayerName} joined world. Total players: {world.GetPlayerCount()}");
    }
}
```

---

### **STEP 5: Save/Load (Pause Menu)**

```csharp
public class PauseMenuUI : MonoBehaviour
{
    public void OnSaveGame()
    {
        if (SaveGameManager.Instance.SaveCurrentGame())
        {
            ShowMessage("Game saved!");
        }
    }

    public void OnLoadGame(int slotId)
    {
        SaveSlot loaded = SaveGameManager.Instance.LoadTrialFromSlot(slotId);
        if (loaded != null)
        {
            // World restored, all systems re-initialized
            // Reload main game scene
            SceneManager.LoadScene("MainGame");
        }
    }
}
```

---

### **STEP 6: Trial End & Class Assignment**

Subscribe to trial completion event:

```csharp
public class ClassRevealUI : MonoBehaviour
{
    private void OnEnable()
    {
        TrialWeekManager.Instance.OnPlayerTrialEnd += OnPlayerTrialEnded;
    }

    private void OnPlayerTrialEnded(string playerId)
    {
        // Get player's actions
        ActionRecord[] actions = ActionTracker.Instance.GetPlayerActions(playerId);

        // Calculate scores
        ScoringData scores = ScoringWeightCalculator.Instance.CalculateScores(actions);

        // Determine class (dominant category)
        string assignedClass = GetClassFromScoring(scores);

        // Update player progress
        SaveSlot world = SaveGameManager.Instance.GetActiveSlot();
        PlayerProgress player = FindPlayerInSlot(world, playerId);
        player.trialState.assignedClass = assignedClass;

        // Show class reveal screen
        ShowClassRevealScreen(playerId, assignedClass, scores);
    }

    private string GetClassFromScoring(ScoringData scores)
    {
        string dominant = scores.GetDominantCategory();
        return dominant switch
        {
            "Combat" => "Warrior",
            "Magic" => "Mage",
            "Crafting" => "Artisan",
            "Exploration" => "Ranger",
            "Homesteading" => "Homesteader",
            "Social" => "Merchant",
            "Wealth" => "Merchant",
            _ => "Adventurer",
        };
    }
}
```

---

### **STEP 7: Update Proximity System Every Frame**

In player controller, update position:

```csharp
private void Update()
{
    ProximityPenaltySystem.Instance.UpdatePlayerPosition(playerId, transform.position);
}
```

---

## 💡 KEY MECHANICS SUMMARY

### **Trial Week Timing**
- Fixed 7 days per player (15 min day + 15 min night = 30 min per cycle)
- Each player's timer starts when they JOIN (not when world created)
- Shared world day/night cycle (all see dusk simultaneously)

### **Difficulty Multipliers**
- Applied at action point: `finalPoints = baseWeight × difficulty × (proximity OR boost)`
- Harder difficulty = higher rewards
- Example: Hard difficulty kill (weight 5) with squad boost = 5 × 1.4 × 1.7 = 11.9 points

### **Proximity Penalty**
- Open world only (not in dungeons)
- 80% reduction (0.2x) for combat/exploration
- 40% reduction (0.4x) for crafting
- 0% reduction (1.0x) for homesteading
- Applied at point-of-action (not continuous check)

### **Squad Dungeon Boost**
- 1.5x (Normal) to 2.0x (Hardcore) multiplier
- ONLY if ALL 4 players in trial together
- No penalty, just bonus
- Encourages group progression

### **Co-op Model**
```
World has 1-4 players
├─ Player 1 (Day 3) + Player 2 (Day 5) + Player 3 (Day 2)
└─ Each has individual class assignment at trial end
```

---

## 🧪 TESTING CHECKLIST

- [ ] Single-player trial: Works like Phase 1
- [ ] Create co-op world: Flags set correctly
- [ ] Add second player: Joins successfully, separate trial timer
- [ ] Action logging: Proximity penalty applies in open world
- [ ] Squad boost: 4 players in dungeon = 1.5x points
- [ ] Mixed trial states: 3 in trial + 1 done = no boost
- [ ] Save/load: All players restored with correct state
- [ ] Difficulty multipliers: Hard difficulty = more points
- [ ] Class assignment: Dominant category determines class
- [ ] Stats: ActionTracker shows correct points breakdown

---

## 📝 MIGRATION FROM PHASE 1

**Phase 1 code continues to work:**
- `CreateNewTrial()` → calls `CreateNewMultiplayerWorld(enableCoop: false)`
- `TrialWeekManager` → wraps per-player logic internally
- `SaveGameManager` → handles both single/multi-player

**No breaking changes** — Phase 1 features fully backward compatible.

---

## 🚀 NEXT PHASE (Phase 3)

- [ ] Camera zoom system with buttons
- [ ] Day/night UI (day counter, time progress bar)
- [ ] Music system integration (day/night crossfading)
- [ ] Character creation UI
- [ ] Save/load menu UI
- [ ] Class reveal screen design
- [ ] Guild system (post-trial)

---

**Phase 2 is ready for integration. Copy all 8 systems to Assets/Scripts/ and follow the integration steps above.** 🎯
