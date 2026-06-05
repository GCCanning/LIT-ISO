using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EthraClone.TrialWeek
{
    /// <summary>
    /// Phase 2 Enhanced: Multi-player co-op save system.
    /// - Supports 1-4 players per world
    /// - Shared world data (dungeons, POIs, biomes)
    /// - Per-player progress (individual trial states, actions, scores)
    /// - Individual class assignments
    /// </summary>
    public class SaveGameManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private string saveDirectoryName = "EthraCloneSaves";
        [SerializeField] private int maxSaveSlots = 5;
#pragma warning disable CS0414  // Field is assigned but its value is never used (reserved for future use)
        [SerializeField] private bool useCompression = false;
#pragma warning restore CS0414

        private SaveGameData saveGameData;
        private string savePath;
        private SaveSlot currentActiveSlot;

        // ============= EVENTS =============
        public event Action<SaveSlot> OnGameSaved;
        public event Action<SaveSlot> OnGameLoaded;
        public event Action<int> OnSaveSlotDeleted;

        // ============= SINGLETON =============
        private static SaveGameManager instance;
        public static SaveGameManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<SaveGameManager>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("SaveGameManager");
                        instance = go.AddComponent<SaveGameManager>();
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

            savePath = Path.Combine(Application.persistentDataPath, saveDirectoryName);
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
                Debug.Log($"[SaveGameManager] Created save directory: {savePath}");
            }

            LoadSaveGameData();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        // ============= SINGLE-PLAYER (PHASE 1 COMPATIBILITY) =============

        /// <summary>
        /// Create a new single-player trial (backward compatible with Phase 1).
        /// </summary>
        public SaveSlot CreateNewTrial(int slotId, string playerName)
        {
            return CreateNewMultiplayerWorld(slotId, playerName, false);  // isCoopEnabled = false
        }

        // ============= MULTI-PLAYER WORLDS =============

        /// <summary>
        /// Create a new world (single or co-op).
        /// </summary>
        public SaveSlot CreateNewMultiplayerWorld(int slotId, string playerName, bool enableCoopMode)
        {
            if (slotId < 1 || slotId > maxSaveSlots)
            {
                Debug.LogError($"Invalid save slot: {slotId}");
                return null;
            }

            // Generate world seed
            long worldSeed = WorldSeedManager.Instance.GenerateNewSeed();

            // Create save slot
            SaveSlot slot = new SaveSlot(slotId);
            slot.worldName = $"{playerName}'s World";
            slot.isCoopEnabled = enableCoopMode;

            // Create shared world data
            WorldData worldData = new WorldData();
            worldData.worldSeed = worldSeed;
            slot.worldData = worldData;

            // Create player 1 (the creator/host)
            PlayerProgress player1 = new PlayerProgress();
            player1.playerId = playerName;
            player1.playerName = playerName;
            player1.worldSeed = worldSeed;
            player1.playerJoinTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            player1.trialState = new TrialState();

            slot.AddPlayer(player1, 0);  // Slot 0 = first player
            currentActiveSlot = slot;

            // Store in save data
            saveGameData.slots[slotId - 1] = slot;
            saveGameData.activePlayerId = playerName;

            SaveToDisk();

            Debug.Log($"[SaveGameManager] Created new {(enableCoopMode ? "co-op" : "single-player")} world in slot {slotId}");
            return slot;
        }

        /// <summary>
        /// Add a second (or third/fourth) player to an existing world.
        /// </summary>
        public bool AddPlayerToWorld(int slotId, string playerName)
        {
            if (slotId < 1 || slotId > maxSaveSlots)
            {
                Debug.LogError($"Invalid save slot: {slotId}");
                return false;
            }

            SaveSlot slot = saveGameData.slots[slotId - 1];
            if (slot == null || slot.worldData == null)
            {
                Debug.LogError($"Save slot {slotId} is empty");
                return false;
            }

            if (!slot.isCoopEnabled)
            {
                Debug.LogWarning($"Save slot {slotId} is not co-op enabled");
                return false;
            }

            int playerCount = slot.GetPlayerCount();
            if (playerCount >= 4)
            {
                Debug.LogError("World already has 4 players (max capacity)");
                return false;
            }

            // Create new player
            PlayerProgress newPlayer = new PlayerProgress();
            newPlayer.playerId = playerName;
            newPlayer.playerName = playerName;
            newPlayer.worldSeed = slot.worldData.worldSeed;
            newPlayer.playerJoinTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            newPlayer.trialState = new TrialState();

            // Add to next available slot
            bool added = slot.AddPlayer(newPlayer, playerCount);
            if (!added)
            {
                Debug.LogError("Failed to add player to world");
                return false;
            }

            // Update world data to track player join time
            System.Array.Resize(ref slot.worldData.playerJoinTimestamps, playerCount + 1);
            slot.worldData.playerJoinTimestamps[playerCount] = newPlayer.playerJoinTimestamp;

            SaveToDisk();

            Debug.Log($"[SaveGameManager] Added {playerName} to world {slotId}");
            return true;
        }

        // ============= LOAD/SAVE =============

        /// <summary>
        /// Load a world from save slot.
        /// </summary>
        public SaveSlot LoadTrialFromSlot(int slotId)
        {
            if (slotId < 1 || slotId > maxSaveSlots)
            {
                Debug.LogError($"Invalid save slot: {slotId}");
                return null;
            }

            SaveSlot slot = saveGameData.slots[slotId - 1];
            if (slot == null || slot.worldData == null)
            {
                Debug.LogWarning($"Save slot {slotId} is empty");
                return null;
            }

            currentActiveSlot = slot;
            saveGameData.activePlayerId = slot.worldName;

            // Restore world seed
            WorldSeedManager.Instance.SetSeed(slot.worldData.worldSeed);

            // Restore all players in this world
            foreach (PlayerProgress player in slot.GetActivePlayers())
            {
                TrialWeekManager.Instance.ResumePlayerTrial(
                    player.playerId,
                    player.playerJoinTimestamp,
                    player.trialState
                );

                // Restore action history
                ActionTracker actionTracker = ActionTracker.Instance;
                if (actionTracker != null)
                {
                    actionTracker.InitializePlayer(player.playerId);
                    actionTracker.LoadActionHistory(player.playerId, player.actionHistory);
                }
            }

            // Restore dungeons
            DungeonInstanceSystem dungeonSystem = DungeonInstanceSystem.Instance;
            if (dungeonSystem != null && slot.worldData.dungeonInstances != null)
            {
                dungeonSystem.InitializeForWorld(slot.worldData.worldSeed, slot.worldData.dungeonInstances);
            }

            Debug.Log($"[SaveGameManager] Loaded world '{slot.worldName}' from slot {slotId}");
            OnGameLoaded?.Invoke(slot);

            return slot;
        }

        /// <summary>
        /// Save current game state. Updates all players in active world.
        /// </summary>
        public bool SaveCurrentGame()
        {
            if (currentActiveSlot == null)
            {
                Debug.LogError("No active save slot. Create a new world first.");
                return false;
            }

            // Update all players in the world
            foreach (int playerSlot in currentActiveSlot.occupiedPlayerSlots)
            {
                PlayerProgress player = currentActiveSlot.playerProgresses[playerSlot];
                if (player == null) continue;

                // Update trial state
                TrialWeekManager trialManager = TrialWeekManager.Instance;
                if (trialManager != null)
                {
                    TrialState playerTrialState = trialManager.GetPlayerTrialState(player.playerId);
                    if (playerTrialState != null)
                    {
                        player.trialState = playerTrialState;
                    }
                }

                // Update action history
                ActionTracker actionTracker = ActionTracker.Instance;
                if (actionTracker != null)
                {
                    player.actionHistory = actionTracker.GetActionHistoryForSave(player.playerId);
                }

                player.lastSaveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }

            // Update shared world data (dungeons, etc.)
            DungeonInstanceSystem dungeonSystem = DungeonInstanceSystem.Instance;
            if (dungeonSystem != null)
            {
                currentActiveSlot.worldData.dungeonInstances = dungeonSystem.GetAllDungeons();
            }

            // Update metadata
            currentActiveSlot.totalPlaytimeMinutes += Time.deltaTime / 60f;

            SaveToDisk();

            Debug.Log($"[SaveGameManager] Saved world '{currentActiveSlot.worldName}'");
            OnGameSaved?.Invoke(currentActiveSlot);

            return true;
        }

        /// <summary>
        /// Delete a save slot.
        /// </summary>
        public bool DeleteSaveSlot(int slotId)
        {
            if (slotId < 1 || slotId > maxSaveSlots)
            {
                Debug.LogError($"Invalid save slot: {slotId}");
                return false;
            }

            if (currentActiveSlot != null && currentActiveSlot.slotId == slotId)
            {
                currentActiveSlot = null;
            }

            saveGameData.slots[slotId - 1] = new SaveSlot(slotId);
            SaveToDisk();

            Debug.Log($"[SaveGameManager] Deleted save slot {slotId}");
            OnSaveSlotDeleted?.Invoke(slotId);

            return true;
        }

        // ============= PERSISTENCE =============

        private void SaveToDisk()
        {
            try
            {
                string json = JsonUtility.ToJson(saveGameData, true);
                string filePath = Path.Combine(savePath, "savegame.json");
                File.WriteAllText(filePath, json);

                Debug.Log($"[SaveGameManager] Save file written: {filePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveGameManager] Failed to save: {ex.Message}");
            }
        }

        private void LoadSaveGameData()
        {
            try
            {
                string filePath = Path.Combine(savePath, "savegame.json");

                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    saveGameData = JsonUtility.FromJson<SaveGameData>(json);
                    Debug.Log($"[SaveGameManager] Loaded save data from: {filePath}");
                }
                else
                {
                    saveGameData = new SaveGameData();
                    Debug.Log("[SaveGameManager] No save file found. Created new save data.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveGameManager] Failed to load save data: {ex.Message}. Creating fresh.");
                saveGameData = new SaveGameData();
            }
        }

        // ============= QUERIES =============

        public SaveSlot[] GetAllSaveSlots() => saveGameData.slots;
        public SaveSlot GetSaveSlot(int slotId) => (slotId >= 1 && slotId <= maxSaveSlots) ? saveGameData.slots[slotId - 1] : null;
        public bool IsSaveSlotOccupied(int slotId) => GetSaveSlot(slotId) != null && !string.IsNullOrEmpty(GetSaveSlot(slotId).worldName);
        public SaveSlot GetActiveSlot() => currentActiveSlot;

        public List<SaveSlot> GetOccupiedSlots()
        {
            List<SaveSlot> occupied = new List<SaveSlot>();
            foreach (SaveSlot slot in saveGameData.slots)
            {
                if (slot != null && !string.IsNullOrEmpty(slot.worldName))
                {
                    occupied.Add(slot);
                }
            }
            return occupied;
        }

        public void LogSaveGameState()
        {
            Debug.Log("=== SAVE GAME STATE ===");
            for (int i = 0; i < maxSaveSlots; i++)
            {
                SaveSlot slot = saveGameData.slots[i];
                if (string.IsNullOrEmpty(slot.worldName))
                {
                    Debug.Log($"Slot {i + 1}: EMPTY");
                }
                else
                {
                    Debug.Log($"Slot {i + 1}: {slot.worldName} | Players: {slot.GetPlayerCount()}/4 | Seed: {slot.worldData.worldSeed}");
                }
            }
        }
    }
}
