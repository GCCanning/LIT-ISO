using System;
using System.Collections.Generic;
using UnityEngine;

namespace EthraClone.TrialWeek
{
    /// <summary>
    /// Phase 2 Enhanced: Per-player trial week management.
    /// - Each player has their own 7-day trial timer from when they JOIN
    /// - Shared world day/night cycle (all players see dusk/dawn together)
    /// - Individual trial progress per player
    /// - Events fire per-player (OnPlayerTrialStart, OnPlayerTrialEnd, etc.)
    /// </summary>
    public class TrialWeekManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private float dayLengthMinutes = 15f;
        [SerializeField] private float nightLengthMinutes = 15f;
        [SerializeField] private bool useRealTime = false;

        [Header("World Day/Night Cycle")]
        [SerializeField] private TimeOfDay worldTimeOfDay = TimeOfDay.Day;
        [SerializeField] private float worldElapsedMinutes = 0f;

        /// <summary> Per-player trial state, keyed by playerId. </summary>
        private Dictionary<string, PlayerTrialData> playerTrials = new Dictionary<string, PlayerTrialData>();

        /// <summary> Internal player trial tracking. </summary>
        private class PlayerTrialData
        {
            public string playerId;
            public long joinTimestamp;
            public TrialState trialState;
            public bool hasTrialEnded = false;
        }

        // ============= EVENTS =============
        // World-wide events (all players see these)
        public event Action<TimeOfDay> OnWorldTimeOfDayChanged;  // (newTimeOfDay)
        public event Action OnWorldDusk;                         // Fired for all players
        public event Action OnWorldDayStart;                     // Fired for all players

        // Per-player events
        public event Action<string> OnPlayerTrialStart;          // (playerId)
        public event Action<string, int> OnPlayerDayChanged;     // (playerId, newDay)
        public event Action<string> OnPlayerTrialEnd;            // (playerId) - trial completed
        public event Action<string, float> OnPlayerTrialProgress;// (playerId, progress 0-1)

        // ============= SINGLETON =============
        private static TrialWeekManager instance;
        public static TrialWeekManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<TrialWeekManager>();
                    if (instance == null)
                    {
                        Debug.LogError("TrialWeekManager not found in scene.");
                    }
                }
                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning("Multiple TrialWeekManagers detected. Destroying duplicate.");
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

        private void Update()
        {
            UpdateWorldCycle();
            UpdatePlayerTrials();
        }

        // ============= WORLD CYCLE (SHARED) =============

        private void UpdateWorldCycle()
        {
            float deltaTime = useRealTime ? Time.deltaTime : Time.deltaTime;
            float realMinutesPerSecond = (1f / 60f);
            worldElapsedMinutes += deltaTime * realMinutesPerSecond;

            // Check for world time of day change
            float cycleLength = dayLengthMinutes + nightLengthMinutes;
            TimeOfDay newTimeOfDay = (worldElapsedMinutes % cycleLength) < dayLengthMinutes
                ? TimeOfDay.Day
                : TimeOfDay.Night;

            if (newTimeOfDay != worldTimeOfDay)
            {
                worldTimeOfDay = newTimeOfDay;
                OnWorldTimeOfDayChanged?.Invoke(worldTimeOfDay);

                if (worldTimeOfDay == TimeOfDay.Day)
                {
                    OnWorldDayStart?.Invoke();
                }
                else if (worldTimeOfDay == TimeOfDay.Night)
                {
                    OnWorldDusk?.Invoke();
                }
            }
        }

        // ============= PER-PLAYER TRIALS =============

        private void UpdatePlayerTrials()
        {
            List<string> completedPlayers = new List<string>();

            foreach (var kvp in playerTrials)
            {
                string playerId = kvp.Key;
                PlayerTrialData trialData = kvp.Value;

                if (trialData.hasTrialEnded)
                    continue;

                // Calculate elapsed time since this player joined
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                long elapsedSeconds = now - trialData.joinTimestamp;
                float elapsedMinutes = elapsedSeconds / 60f;

                trialData.trialState.elapsedMinutes = elapsedMinutes;

                // Calculate current day (1-7)
                float cycleLength = dayLengthMinutes + nightLengthMinutes;
                int newDay = (int)(elapsedMinutes / cycleLength) + 1;

                if (newDay > trialData.trialState.currentDay && newDay <= 7)
                {
                    trialData.trialState.currentDay = newDay;
                    OnPlayerDayChanged?.Invoke(playerId, newDay);
                }

                // Check for trial completion (day > 7)
                if (newDay > 7 && !trialData.hasTrialEnded)
                {
                    CompletePlayerTrial(playerId);
                    completedPlayers.Add(playerId);
                }

                // Fire progress event
                float progress = Mathf.Clamp01((newDay - 1f) / 7f);
                OnPlayerTrialProgress?.Invoke(playerId, progress);
            }

            // Remove completed trials
            foreach (string playerId in completedPlayers)
            {
                playerTrials[playerId].hasTrialEnded = true;
            }
        }

        // ============= PUBLIC API =============

        /// <summary>
        /// Register a new player's trial. Called when player joins the world.
        /// </summary>
        public void RegisterPlayerTrial(string playerId, TrialState initialState = null)
        {
            if (playerTrials.ContainsKey(playerId))
            {
                Debug.LogWarning($"Player {playerId} trial already registered.");
                return;
            }

            PlayerTrialData data = new PlayerTrialData();
            data.playerId = playerId;
            data.joinTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            data.trialState = initialState ?? new TrialState();
            data.trialState.trialStartTimestamp = data.joinTimestamp;

            playerTrials[playerId] = data;

            Debug.Log($"[TrialWeekManager] Registered trial for {playerId}");
            OnPlayerTrialStart?.Invoke(playerId);
        }

        /// <summary>
        /// Resume an existing player's trial from save data.
        /// </summary>
        public void ResumePlayerTrial(string playerId, long joinTimestamp, TrialState savedState)
        {
            if (playerTrials.ContainsKey(playerId))
            {
                Debug.LogWarning($"Player {playerId} trial already exists.");
                return;
            }

            PlayerTrialData data = new PlayerTrialData();
            data.playerId = playerId;
            data.joinTimestamp = joinTimestamp;
            data.trialState = savedState;
            data.hasTrialEnded = savedState.isTrialComplete;

            playerTrials[playerId] = data;

            Debug.Log($"[TrialWeekManager] Resumed trial for {playerId}");
            if (!data.hasTrialEnded)
            {
                OnPlayerTrialStart?.Invoke(playerId);
            }
        }

        /// <summary>
        /// Complete a player's trial.
        /// </summary>
        private void CompletePlayerTrial(string playerId)
        {
            if (!playerTrials.ContainsKey(playerId))
                return;

            PlayerTrialData data = playerTrials[playerId];
            data.trialState.isTrialComplete = true;
            data.hasTrialEnded = true;

            Debug.Log($"[TrialWeekManager] Trial complete for {playerId}");
            OnPlayerTrialEnd?.Invoke(playerId);
        }

        /// <summary>
        /// Force-set a player's trial time (for testing).
        /// </summary>
        public void SetPlayerTime(string playerId, int day, TimeOfDay timeOfDay)
        {
            if (!playerTrials.ContainsKey(playerId))
            {
                Debug.LogWarning($"Player {playerId} trial not found.");
                return;
            }

            float cycleLength = dayLengthMinutes + nightLengthMinutes;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Calculate new join time to make the current time equal to desired day/time
            float elapsedMinutes = (day - 1) * cycleLength;
            if (timeOfDay == TimeOfDay.Night)
            {
                elapsedMinutes += dayLengthMinutes;
            }

            playerTrials[playerId].joinTimestamp = now - (long)(elapsedMinutes * 60f);
            playerTrials[playerId].trialState.currentDay = day;
            playerTrials[playerId].trialState.timeOfDay = timeOfDay;

            Debug.Log($"[TrialWeekManager] Set {playerId} time to Day {day}, {timeOfDay}");
        }

        /// <summary>
        /// Get a player's trial state.
        /// </summary>
        public TrialState GetPlayerTrialState(string playerId)
        {
            if (playerTrials.ContainsKey(playerId))
            {
                return playerTrials[playerId].trialState;
            }
            return null;
        }

        /// <summary>
        /// Get a player's trial progress (0-1).
        /// </summary>
        public float GetPlayerTrialProgress(string playerId)
        {
            if (!playerTrials.ContainsKey(playerId))
                return 0f;

            TrialState state = playerTrials[playerId].trialState;
            if (state.isTrialComplete)
                return 1f;

            return Mathf.Clamp01((state.currentDay - 1f) / 7f);
        }

        /// <summary>
        /// Check if a player is currently in their trial.
        /// </summary>
        public bool IsPlayerInTrial(string playerId)
        {
            if (!playerTrials.ContainsKey(playerId))
                return false;

            return !playerTrials[playerId].trialState.isTrialComplete;
        }

        /// <summary>
        /// Check if ALL registered players are in their trial (for squad dungeon boost).
        /// </summary>
        public bool AreAllPlayersInTrial()
        {
            if (playerTrials.Count == 0)
                return false;

            foreach (var kvp in playerTrials)
            {
                if (kvp.Value.trialState.isTrialComplete)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Get all registered players.
        /// </summary>
        public string[] GetAllPlayers()
        {
            string[] players = new string[playerTrials.Count];
            int i = 0;
            foreach (var kvp in playerTrials)
            {
                players[i++] = kvp.Key;
            }
            return players;
        }

        /// <summary>
        /// Get shared world day/night.
        /// </summary>
        public TimeOfDay GetWorldTimeOfDay() => worldTimeOfDay;
        public float GetWorldElapsedMinutes() => worldElapsedMinutes;

        /// <summary>
        /// Returns the current position in the day/night cycle as a 0–1 value.
        /// 0 = dawn (start of day), dayFraction = dusk (start of night), 1 = next dawn.
        /// Used by DayNightMusicManager to sync on startup.
        /// </summary>
        public float GetNormalizedCycleTime()
        {
            float cycleLength = dayLengthMinutes + nightLengthMinutes;
            if (cycleLength <= 0f) return 0f;
            return (worldElapsedMinutes % cycleLength) / cycleLength;
        }

        /// <summary>
        /// The fraction of the full cycle that is daytime (0–1).
        /// e.g. 0.5 for equal day and night lengths.
        /// </summary>
        public float GetDayFraction()
        {
            float cycleLength = dayLengthMinutes + nightLengthMinutes;
            return cycleLength > 0f ? dayLengthMinutes / cycleLength : 0.5f;
        }
    }
}
