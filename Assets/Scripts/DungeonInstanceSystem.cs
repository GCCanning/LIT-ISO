using System;
using System.Collections.Generic;
using UnityEngine;

namespace EthraClone.TrialWeek
{
    /// <summary>
    /// Manages dungeon instances in the world.
    /// - Creates/tracks dungeon instances
    /// - Manages player entry/participation
    /// - Calculates squad boost eligibility (all 4 players in trial?)
    /// - Tracks treasure and mob state
    /// </summary>
    public class DungeonInstanceSystem : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private float squadBoostMultiplier = 1.5f;  // 1.5x for squad
        [SerializeField] private bool requireAllPlayersForBoost = true;  // Must have 4/4 in trial

        /// <summary> Dungeons in the current world, keyed by dungeonId. </summary>
        private Dictionary<int, DungeonInstance> dungeonInstances = new Dictionary<int, DungeonInstance>();

        /// <summary> Current world seed (to track which world we're in). </summary>
        private long currentWorldSeed = 0;

        // ============= EVENTS =============
        public event Action<int> OnDungeonDiscovered;      // (dungeonId)
#pragma warning disable CS0067  // Event is declared but never used (reserved for future use)
        public event Action<int> OnDungeonExplored;        // (dungeonId)
#pragma warning restore CS0067
        public event Action<int, int> OnTreasureOpened;    // (dungeonId, chestId)
        public event Action<int, int> OnMobDefeated;       // (dungeonId, mobId)

        // ============= SINGLETON =============
        private static DungeonInstanceSystem instance;
        public static DungeonInstanceSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<DungeonInstanceSystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("DungeonInstanceSystem");
                        instance = go.AddComponent<DungeonInstanceSystem>();
                    }
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        // ============= INITIALIZATION =============

        /// <summary>
        /// Initialize dungeons for a world (called when loading a world).
        /// </summary>
        public void InitializeForWorld(long worldSeed, DungeonInstance[] dungeons)
        {
            currentWorldSeed = worldSeed;
            dungeonInstances.Clear();

            if (dungeons != null)
            {
                foreach (DungeonInstance dungeon in dungeons)
                {
                    dungeonInstances[dungeon.dungeonId] = dungeon;
                }
            }

            Debug.Log($"[DungeonInstanceSystem] Initialized {dungeonInstances.Count} dungeons for world {worldSeed}");
        }

        /// <summary>
        /// Create a new dungeon in the world.
        /// </summary>
        public DungeonInstance CreateDungeon(int dungeonId, string name, Vector3Int gridLocation, GameDifficulty difficulty)
        {
            if (dungeonInstances.ContainsKey(dungeonId))
            {
                Debug.LogWarning($"Dungeon {dungeonId} already exists.");
                return null;
            }

            DungeonInstance dungeon = new DungeonInstance(dungeonId, name, gridLocation);
            dungeon.difficulty = difficulty;
            dungeonInstances[dungeonId] = dungeon;

            Debug.Log($"[DungeonInstanceSystem] Created dungeon {name} (ID: {dungeonId})");
            OnDungeonDiscovered?.Invoke(dungeonId);

            return dungeon;
        }

        // ============= SQUAD BOOST =============

        /// <summary>
        /// Check if all players in the trial are in a specific dungeon together.
        /// Returns boost multiplier if true, 1.0 if false.
        /// </summary>
        public float GetSquadBoostMultiplier(int dungeonId)
        {
            TrialWeekManager trialManager = TrialWeekManager.Instance;
            if (trialManager == null)
                return 1.0f;

            if (!requireAllPlayersForBoost)
            {
                // Alternative: just check if multiple players in dungeon
                // For now, implement strict version
            }

            // Check if all players are in trial
            if (!trialManager.AreAllPlayersInTrial())
            {
                return 1.0f;  // At least one player finished trial
            }

            // Check if all players have entered this dungeon (simple tracking)
            if (!dungeonInstances.ContainsKey(dungeonId))
            {
                return 1.0f;
            }

            DungeonInstance dungeon = dungeonInstances[dungeonId];
            string[] trialPlayers = trialManager.GetAllPlayers();

            if (trialPlayers.Length < 2)
            {
                return 1.0f;  // Need at least 2 players for squad
            }

            // For now, just return boost if all in trial
            // In future, could track actual player positions in dungeon
            return squadBoostMultiplier;
        }

        /// <summary>
        /// Check if a player has entered a dungeon (for loot lockout tracking).
        /// </summary>
        public bool HasPlayerEnteredDungeon(int dungeonId, long playerId)
        {
            if (!dungeonInstances.ContainsKey(dungeonId))
                return false;

            DungeonInstance dungeon = dungeonInstances[dungeonId];
            return System.Array.IndexOf(dungeon.playersWhoEntered, playerId) >= 0;
        }

        /// <summary>
        /// Mark a player as having entered a dungeon.
        /// </summary>
        public void RegisterPlayerEntry(int dungeonId, long playerId)
        {
            if (!dungeonInstances.ContainsKey(dungeonId))
                return;

            DungeonInstance dungeon = dungeonInstances[dungeonId];
            if (System.Array.IndexOf(dungeon.playersWhoEntered, playerId) >= 0)
                return;  // Already registered

            System.Array.Resize(ref dungeon.playersWhoEntered, dungeon.playersWhoEntered.Length + 1);
            dungeon.playersWhoEntered[dungeon.playersWhoEntered.Length - 1] = playerId;
        }

        // ============= TREASURE MANAGEMENT =============

        /// <summary>
        /// Open a treasure chest in a dungeon.
        /// </summary>
        public TreasureChestData OpenChest(int dungeonId, int chestId, long playerId)
        {
            if (!dungeonInstances.ContainsKey(dungeonId))
            {
                Debug.LogWarning($"Dungeon {dungeonId} not found.");
                return null;
            }

            DungeonInstance dungeon = dungeonInstances[dungeonId];

            for (int i = 0; i < dungeon.treasureChests.Length; i++)
            {
                if (dungeon.treasureChests[i].chestId == chestId)
                {
                    TreasureChestData chest = dungeon.treasureChests[i];
                    if (chest.isOpened)
                    {
                        Debug.LogWarning($"Chest {chestId} already opened.");
                        return chest;
                    }

                    chest.isOpened = true;
                    chest.openedByPlayerId = playerId;
                    chest.openedAtTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    Debug.Log($"[DungeonInstanceSystem] Opened chest {chestId} in dungeon {dungeonId}");
                    OnTreasureOpened?.Invoke(dungeonId, chestId);

                    return chest;
                }
            }

            Debug.LogWarning($"Chest {chestId} not found in dungeon {dungeonId}.");
            return null;
        }

        /// <summary>
        /// Get all treasure chests in a dungeon.
        /// </summary>
        public TreasureChestData[] GetTreasureChests(int dungeonId)
        {
            if (!dungeonInstances.ContainsKey(dungeonId))
                return new TreasureChestData[0];

            return dungeonInstances[dungeonId].treasureChests;
        }

        // ============= MOB MANAGEMENT =============

        /// <summary>
        /// Mark a mob as defeated in a dungeon.
        /// </summary>
        public MobSpawnData DefeatMob(int dungeonId, int mobId, long playerId)
        {
            if (!dungeonInstances.ContainsKey(dungeonId))
                return null;

            DungeonInstance dungeon = dungeonInstances[dungeonId];

            for (int i = 0; i < dungeon.mobs.Length; i++)
            {
                if (dungeon.mobs[i].mobId == mobId)
                {
                    MobSpawnData mob = dungeon.mobs[i];
                    if (mob.isDefeated)
                        return mob;  // Already defeated

                    mob.isDefeated = true;
                    mob.defeatedByPlayerId = playerId;
                    mob.defeatedAtTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    Debug.Log($"[DungeonInstanceSystem] Defeated mob {mobId} in dungeon {dungeonId}");
                    OnMobDefeated?.Invoke(dungeonId, mobId);

                    return mob;
                }
            }

            return null;
        }

        /// <summary>
        /// Get all mobs in a dungeon.
        /// </summary>
        public MobSpawnData[] GetMobs(int dungeonId)
        {
            if (!dungeonInstances.ContainsKey(dungeonId))
                return new MobSpawnData[0];

            return dungeonInstances[dungeonId].mobs;
        }

        /// <summary>
        /// Check if dungeon boss is defeated.
        /// </summary>
        public bool IsBossDefeated(int dungeonId)
        {
            if (!dungeonInstances.ContainsKey(dungeonId))
                return false;

            return dungeonInstances[dungeonId].bossDefeated;
        }

        /// <summary>
        /// Mark boss as defeated.
        /// </summary>
        public void DefeatBoss(int dungeonId)
        {
            if (dungeonInstances.ContainsKey(dungeonId))
            {
                dungeonInstances[dungeonId].bossDefeated = true;
                Debug.Log($"[DungeonInstanceSystem] Boss defeated in dungeon {dungeonId}");
            }
        }

        // ============= QUERIES =============

        /// <summary>
        /// Get a dungeon instance by ID.
        /// </summary>
        public DungeonInstance GetDungeon(int dungeonId)
        {
            if (dungeonInstances.ContainsKey(dungeonId))
                return dungeonInstances[dungeonId];

            return null;
        }

        /// <summary>
        /// Get all dungeons in the world.
        /// </summary>
        public DungeonInstance[] GetAllDungeons()
        {
            DungeonInstance[] dungeons = new DungeonInstance[dungeonInstances.Count];
            int i = 0;
            foreach (var kvp in dungeonInstances)
            {
                dungeons[i++] = kvp.Value;
            }
            return dungeons;
        }

        /// <summary>
        /// Check if world is fully explored (all dungeons explored).
        /// </summary>
        public bool IsWorldFullyExplored()
        {
            foreach (var dungeon in dungeonInstances.Values)
            {
                if (!dungeon.isFullyExplored)
                    return false;
            }
            return true;
        }

        public long GetCurrentWorldSeed() => currentWorldSeed;

        // ============= CONFIGURATION =============

        public void SetSquadBoostMultiplier(float multiplier)
        {
            squadBoostMultiplier = multiplier;
            Debug.Log($"[DungeonInstanceSystem] Squad boost multiplier set to {multiplier}");
        }

        public float GetSquadBoostMultiplier() => squadBoostMultiplier;
    }
}
