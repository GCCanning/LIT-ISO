using System;
using System.Collections.Generic;
using UnityEngine;

namespace EthraClone.TrialWeek
{
    /// <summary>
    /// Phase 2 additions to SerializableData.
    /// Extends Phase 1 structures with co-op, dungeons, and scoring multipliers.
    /// </summary>

    // ============= DIFFICULTY ENUM =============
    [System.Serializable]
    public enum GameDifficulty
    {
        Easy,
        Normal,
        Hard,
        Hardcore
    }

    // ============= TIME OF DAY ENUM =============
    [System.Serializable]
    public enum TimeOfDay
    {
        Day,
        Night
    }

    // ============= SCORING DATA =============
    /// <summary>
    /// Container for all scoring categories (8 categories).
    /// </summary>
    [System.Serializable]
    public class ScoringData
    {
        public int combatScore = 0;
        public int magicScore = 0;
        public int craftingScore = 0;
        public int explorationScore = 0;
        public int homesteadingScore = 0;
        public int socialScore = 0;
        public int wealthScore = 0;
        public int survivalScore = 0;

        /// <summary>
        /// Get the category with the highest score.
        /// </summary>
        public string GetDominantCategory()
        {
            int max = Mathf.Max(combatScore, magicScore, craftingScore, explorationScore,
                               homesteadingScore, socialScore, wealthScore, survivalScore);

            if (max == 0) return "balanced";  // No actions taken
            if (combatScore == max) return "combat";
            if (magicScore == max) return "magic";
            if (craftingScore == max) return "crafting";
            if (explorationScore == max) return "exploration";
            if (homesteadingScore == max) return "homesteading";
            if (socialScore == max) return "social";
            if (wealthScore == max) return "wealth";
            if (survivalScore == max) return "survival";

            return "balanced";
        }

        /// <summary>
        /// Get total points across all categories.
        /// </summary>
        public int GetTotalScore()
        {
            return combatScore + magicScore + craftingScore + explorationScore +
                   homesteadingScore + socialScore + wealthScore + survivalScore;
        }
    }

    // ============= DUNGEON STRUCTURES =============

    /// <summary>
    /// Represents a single treasure chest or loot source in a dungeon.
    /// </summary>
    [System.Serializable]
    public class TreasureChestData
    {
        public int chestId;
        public Vector3Int gridPosition;
        public bool isOpened = false;
        public long openedByPlayerId = 0;  // Player ID who opened it
        public long openedAtTimestamp = 0;
        public string[] lootItemIds = new string[0];
    }

    /// <summary>
    /// Represents a mob spawn point or active mob in a dungeon.
    /// </summary>
    [System.Serializable]
    public class MobSpawnData
    {
        public int mobId;
        public string mobType;  // "Goblin", "Skeleton", "Boss", etc.
        public Vector3Int gridPosition;
        public bool isDefeated = false;
        public long defeatedByPlayerId = 0;
        public long defeatedAtTimestamp = 0;
        public int health = 100;
        public int maxHealth = 100;
    }

    /// <summary>
    /// A dungeon instance in the world. Shared across all players.
    /// </summary>
    [System.Serializable]
    public class DungeonInstance
    {
        public int dungeonId;
        public string dungeonName;
        public Vector3Int gridLocation;  // Location in world grid
        public GameDifficulty difficulty = GameDifficulty.Normal;
        public long createdAtTimestamp;
        public bool isFullyExplored = false;

        /// <summary> Treasure chests in this dungeon. </summary>
        public TreasureChestData[] treasureChests = new TreasureChestData[0];

        /// <summary> Mob spawns in this dungeon. </summary>
        public MobSpawnData[] mobs = new MobSpawnData[0];

        /// <summary> Player IDs who have entered this dungeon (for loot lockout, future). </summary>
        public long[] playersWhoEntered = new long[0];

        /// <summary> Boss defeated? </summary>
        public bool bossDefeated = false;

        public DungeonInstance() { }

        public DungeonInstance(int id, string name, Vector3Int location)
        {
            dungeonId = id;
            dungeonName = name;
            gridLocation = location;
            createdAtTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }

    // ============= WORLD DATA (SHARED) =============

    /// <summary>
    /// Shared world data (not per-player). Used in co-op.
    /// </summary>
    [System.Serializable]
    public class WorldData
    {
        /// <summary> The seed used to generate this world. </summary>
        public long worldSeed = 0;

        /// <summary> When the world was created (real-world timestamp). </summary>
        public long worldCreatedTimestamp = 0;

        /// <summary> When each player joined (parallel array with playerIds). </summary>
        public long[] playerJoinTimestamps = new long[0];

        /// <summary> Biome grid (64x64). </summary>
        public int[] biomeLayout = new int[4096];

        /// <summary> Town/settlement positions. </summary>
        public Vector2Int[] townPositions = new Vector2Int[0];

        /// <summary> Dungeon locations and states (SHARED across players). </summary>
        public DungeonInstance[] dungeonInstances = new DungeonInstance[0];

        /// <summary> Custom metadata for future expansion. </summary>
        public string customData = "";

        public WorldData()
        {
            worldCreatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }

    // ============= ENHANCED TRIAL STATE =============

    /// <summary>
    /// Enhanced TrialState with per-player trial timing.
    /// </summary>
    [System.Serializable]
    public class TrialState
    {
        /// <summary> Current day of the trial (1-7). </summary>
        public int currentDay = 1;

        /// <summary> Current time of day (DAY or NIGHT). </summary>
        public TimeOfDay timeOfDay = TimeOfDay.Day;

        /// <summary> Total elapsed game minutes since THIS PLAYER's trial start. </summary>
        public float elapsedMinutes = 0f;

        /// <summary> Real-world timestamp when THIS PLAYER's trial started (for individual timer). </summary>
        public long trialStartTimestamp = 0;

        /// <summary> Whether this player's trial week has completed. </summary>
        public bool isTrialComplete = false;

        /// <summary> Assigned class when trial completes. </summary>
        public string assignedClass = "";

        /// <summary> Final rank achieved (F-S). </summary>
        public string finalRank = "";

        /// <summary> Guild assignments after trial. </summary>
        public string[] guildAssignments = new string[0];

        /// <summary> Total playtime in minutes when trial completed. </summary>
        public float totalPlaytimeMinutes = 0f;

        public TrialState()
        {
            trialStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }

    // ============= ENHANCED ACTION RECORD =============

    /// <summary>
    /// Enhanced ActionRecord with multiplier tracking.
    /// </summary>
    [System.Serializable]
    public class ActionRecord
    {
        /// <summary> Type of action ("kill", "craft", "discover", etc.). </summary>
        public string actionType;

        /// <summary> Target/description (enemy name, item name, location, etc.). </summary>
        public string targetName;

        /// <summary> Base weight for this action (from config). </summary>
        public int baseWeight;

        /// <summary> Difficulty multiplier applied (0.7x, 1.0x, 1.4x). </summary>
        public float difficultyMultiplier = 1.0f;

        /// <summary> Proximity penalty or squad boost (0.2x or 1.5x). </summary>
        public float proximityOrBoostMultiplier = 1.0f;

        /// <summary> Final points awarded = baseWeight * difficulty * proximityOrBoost. </summary>
        public float finalPoints;

        /// <summary> Where action occurred: "open_world" or dungeon ID. </summary>
        public string actionLocation = "open_world";

        /// <summary> Was this action inside a dungeon with squad boost? </summary>
        public bool hadSquadBoost = false;

        /// <summary> When this action occurred (unix timestamp). </summary>
        public long actionTimestamp;

        public ActionRecord()
        {
            actionTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public ActionRecord(string type, string target, int weight)
        {
            actionType = type;
            targetName = target;
            baseWeight = weight;
            finalPoints = weight;
            actionTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary> Apply multipliers and calculate final points. </summary>
        public void CalculateFinalPoints()
        {
            finalPoints = baseWeight * difficultyMultiplier * proximityOrBoostMultiplier;
        }
    }

    // ============= ENHANCED PLAYER PROGRESS =============

    /// <summary>
    /// Enhanced PlayerProgress with action history and per-player trial timing.
    /// </summary>
    [System.Serializable]
    public class PlayerProgress
    {
        /// <summary> Unique player identifier. </summary>
        public string playerId;

        /// <summary> Player's display name. </summary>
        public string playerName;

        /// <summary> The world seed they're exploring. </summary>
        public long worldSeed;

        /// <summary> When THIS PLAYER joined the world (for individual trial timer). </summary>
        public long playerJoinTimestamp;

        /// <summary> This player's trial state (individual 7-day counter). </summary>
        public TrialState trialState = new TrialState();

        /// <summary> Hidden scoring tracker. </summary>
        public ScoringData scoringData = new ScoringData();

        /// <summary> All actions taken by this player (for scoring calculation). </summary>
        public ActionRecord[] actionHistory = new ActionRecord[0];

        /// <summary> Last known position in the world. </summary>
        [System.Serializable]
        public class PlayerPos
        {
            public float x, y, z;
        }

        public PlayerPos lastPosition = new PlayerPos();

        /// <summary> Total playtime across sessions in this world (minutes). </summary>
        public float totalPlaytimeMinutes = 0f;

        /// <summary> Last save time. </summary>
        public long lastSaveTimestamp = 0;

        public PlayerProgress()
        {
            playerJoinTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }

    // ============= CO-OP SAVE SLOT =============

    /// <summary>
    /// Enhanced SaveSlot with co-op support (1-4 players per world).
    /// </summary>
    [System.Serializable]
    public class SaveSlot
    {
        public int slotId;
        public string worldName = "My World";
        public bool isCoopEnabled = false;
        public long createdAtTimestamp = 0;

        /// <summary> SHARED: World data (biomes, dungeons, POIs). </summary>
        public WorldData worldData = new WorldData();

        /// <summary> PER-PLAYER: Each player's individual progress (1-4 players). </summary>
        public PlayerProgress[] playerProgresses = new PlayerProgress[4];

        /// <summary> Which player slots are occupied (indices of playerProgresses). </summary>
        public int[] occupiedPlayerSlots = new int[0];

        /// <summary> Total playtime in this world (in minutes). </summary>
        public float totalPlaytimeMinutes = 0f;

        public SaveSlot() { }

        public SaveSlot(int id)
        {
            slotId = id;
            createdAtTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Initialize all player slots as empty
            for (int i = 0; i < 4; i++)
            {
                playerProgresses[i] = null;
            }
        }

        /// <summary> Add a player to this save slot. </summary>
        public bool AddPlayer(PlayerProgress player, int playerSlot)
        {
            if (playerSlot < 0 || playerSlot > 3) return false;
            if (playerProgresses[playerSlot] != null) return false;

            playerProgresses[playerSlot] = player;

            // Add to occupied list
            System.Array.Resize(ref occupiedPlayerSlots, occupiedPlayerSlots.Length + 1);
            occupiedPlayerSlots[occupiedPlayerSlots.Length - 1] = playerSlot;

            if (occupiedPlayerSlots.Length > 1)
                isCoopEnabled = true;

            return true;
        }

        /// <summary> Get number of players in this world. </summary>
        public int GetPlayerCount()
        {
            return occupiedPlayerSlots.Length;
        }

        /// <summary> Get all active players. </summary>
        public PlayerProgress[] GetActivePlayers()
        {
            PlayerProgress[] active = new PlayerProgress[occupiedPlayerSlots.Length];
            for (int i = 0; i < occupiedPlayerSlots.Length; i++)
            {
                active[i] = playerProgresses[occupiedPlayerSlots[i]];
            }
            return active;
        }

        /// <summary> Check if all players are in their trial period. </summary>
        public bool AreAllPlayersInTrial()
        {
            foreach (int slot in occupiedPlayerSlots)
            {
                if (playerProgresses[slot].trialState.isTrialComplete)
                    return false;
            }
            return true;
        }

        /// <summary> Get formatted save time. </summary>
        public string GetCreatedTimeFormatted()
        {
            DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTime = dateTime.AddSeconds(createdAtTimestamp).ToLocalTime();
            return dateTime.ToString("yyyy-MM-dd HH:mm");
        }
    }

    // ============= ENHANCED SAVE GAME DATA =============

    /// <summary>
    /// Enhanced root save container with co-op support.
    /// </summary>
    [System.Serializable]
    public class SaveGameData
    {
        public int saveVersion = 2;  // Bumped to v2 for Phase 2
        public SaveSlot[] slots = new SaveSlot[5];
        public string activePlayerId = "";
        public GameSettings gameSettings = new GameSettings();

        public SaveGameData()
        {
            for (int i = 0; i < 5; i++)
            {
                slots[i] = new SaveSlot(i + 1);
            }
        }
    }

    // ============= GAME SETTINGS (ENHANCED) =============

    [System.Serializable]
    public class GameSettings
    {
        public float masterVolume = 1f;
        public float musicVolume = 0.8f;
        public float sfxVolume = 0.8f;
        public bool pixelPerfect = true;
        public int targetResolutionWidth = 1920;
        public int targetResolutionHeight = 1080;
        public GameDifficulty difficultyMode = GameDifficulty.Normal;
    }
}
