using System;
using System.Collections.Generic;
using UnityEngine;

namespace EthraClone.TrialWeek
{
    /// <summary>
    /// Tracks all player actions during their trial week.
    /// Records: action type, location, multipliers (difficulty, proximity/boost), final points.
    /// Per-player instance (each player has their own tracker).
    /// </summary>
    public class ActionTracker : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private int maxActionsStored = 1000;  // Memory limit

        /// <summary> Actions recorded for each player, keyed by playerId. </summary>
        private Dictionary<string, List<ActionRecord>> playerActions = new Dictionary<string, List<ActionRecord>>();

        // ============= SINGLETON =============
        private static ActionTracker instance;
        public static ActionTracker Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<ActionTracker>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("ActionTracker");
                        instance = go.AddComponent<ActionTracker>();
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

        // ============= ACTION LOGGING =============

        /// <summary>
        /// Log an action for a player. Call this when player performs an action.
        /// </summary>
        public void LogAction(string playerId, string actionType, string targetName, int baseWeight, string location = "open_world")
        {
            // Validate playerId
            if (string.IsNullOrEmpty(playerId))
            {
                Debug.LogWarning("[ActionTracker] Cannot log action: playerId is null or empty");
                return;
            }

            if (!playerActions.ContainsKey(playerId))
            {
                playerActions[playerId] = new List<ActionRecord>();
            }

            // Create action record
            ActionRecord action = new ActionRecord(actionType, targetName, baseWeight);
            action.actionLocation = location;

            // Apply difficulty multiplier
            DifficultyConfig diffConfig = DifficultyConfig.Instance;
            if (diffConfig != null)
            {
                action.difficultyMultiplier = diffConfig.GetActionWeightMultiplier();
            }

            // Apply proximity penalty or squad boost
            ApplyMultipliers(playerId, action, location);

            // Calculate final points
            action.CalculateFinalPoints();

            playerActions[playerId].Add(action);

            // Memory management - trim oldest actions if exceeding limit
            if (playerActions[playerId].Count > maxActionsStored)
            {
                playerActions[playerId].RemoveAt(0);
            }

            Debug.Log($"[ActionTracker] {playerId}: {actionType} ({targetName}) = {action.finalPoints:F1} points");
        }

        /// <summary>
        /// Apply proximity penalty or squad boost multiplier to action.
        /// </summary>
        private void ApplyMultipliers(string playerId, ActionRecord action, string location)
        {
            if (location == "open_world")
            {
                // Open world: apply proximity penalty
                ProximityPenaltySystem proximitySystem = ProximityPenaltySystem.Instance;
                if (proximitySystem != null)
                {
                    action.proximityOrBoostMultiplier = proximitySystem.GetProximityMultiplier(playerId, action.actionType);
                }
            }
            else if (location.StartsWith("dungeon_"))
            {
                // Dungeon: check for squad boost
                int dungeonId = int.Parse(location.Substring(8));  // Extract ID from "dungeon_123"
                DungeonInstanceSystem dungeonSystem = DungeonInstanceSystem.Instance;
                if (dungeonSystem != null)
                {
                    float boostMultiplier = dungeonSystem.GetSquadBoostMultiplier(dungeonId);
                    action.proximityOrBoostMultiplier = boostMultiplier;
                    action.hadSquadBoost = (boostMultiplier > 1.0f);
                }
            }
        }

        // ============= ACTION QUERIES =============

        /// <summary>
        /// Get all actions for a player.
        /// </summary>
        public ActionRecord[] GetPlayerActions(string playerId)
        {
            if (!playerActions.ContainsKey(playerId))
                return new ActionRecord[0];

            return playerActions[playerId].ToArray();
        }

        /// <summary>
        /// Get actions of a specific type for a player.
        /// </summary>
        public ActionRecord[] GetActionsByType(string playerId, string actionType)
        {
            if (!playerActions.ContainsKey(playerId))
                return new ActionRecord[0];

            List<ActionRecord> filtered = new List<ActionRecord>();
            foreach (ActionRecord action in playerActions[playerId])
            {
                if (action.actionType.Equals(actionType, StringComparison.OrdinalIgnoreCase))
                {
                    filtered.Add(action);
                }
            }

            return filtered.ToArray();
        }

        /// <summary>
        /// Get total points earned by a player across all actions.
        /// </summary>
        public float GetTotalPoints(string playerId)
        {
            if (!playerActions.ContainsKey(playerId))
                return 0f;

            float total = 0f;
            foreach (ActionRecord action in playerActions[playerId])
            {
                total += action.finalPoints;
            }

            return total;
        }

        /// <summary>
        /// Get points by action category.
        /// </summary>
        public Dictionary<string, float> GetPointsByCategory(string playerId)
        {
            Dictionary<string, float> categoryPoints = new Dictionary<string, float>();

            if (!playerActions.ContainsKey(playerId))
                return categoryPoints;

            // Map action type to category (this is simplified, extend as needed)
            foreach (ActionRecord action in playerActions[playerId])
            {
                string category = GetCategoryForActionType(action.actionType);
                if (!categoryPoints.ContainsKey(category))
                {
                    categoryPoints[category] = 0f;
                }
                categoryPoints[category] += action.finalPoints;
            }

            return categoryPoints;
        }

        /// <summary>
        /// Map action type to scoring category (for ScoringData).
        /// </summary>
        private string GetCategoryForActionType(string actionType)
        {
            return actionType.ToLower() switch
            {
                "kill" => "combat",
                "defeat_boss" => "combat",
                "land_crit" => "combat",
                "cast_spell" => "magic",
                "potion_effect" => "magic",
                "craft" => "crafting",
                "smith" => "crafting",
                "discover" => "exploration",
                "explore_cave" => "exploration",
                "farm_harvest" => "homesteading",
                "build_structure" => "homesteading",
                "talk_npc" => "social",
                "complete_quest" => "social",
                "find_treasure" => "wealth",
                "sell_item" => "wealth",
                _ => "social",  // Default category
            };
        }

        /// <summary>
        /// Get actions that had squad boost applied.
        /// </summary>
        public ActionRecord[] GetSquadBoostedActions(string playerId)
        {
            if (!playerActions.ContainsKey(playerId))
                return new ActionRecord[0];

            List<ActionRecord> boosted = new List<ActionRecord>();
            foreach (ActionRecord action in playerActions[playerId])
            {
                if (action.hadSquadBoost)
                {
                    boosted.Add(action);
                }
            }

            return boosted.ToArray();
        }

        /// <summary>
        /// Get actions that had proximity penalty applied.
        /// </summary>
        public ActionRecord[] GetPenalizedActions(string playerId)
        {
            if (!playerActions.ContainsKey(playerId))
                return new ActionRecord[0];

            List<ActionRecord> penalized = new List<ActionRecord>();
            foreach (ActionRecord action in playerActions[playerId])
            {
                if (action.proximityOrBoostMultiplier < 1.0f)
                {
                    penalized.Add(action);
                }
            }

            return penalized.ToArray();
        }

        // ============= PLAYER MANAGEMENT =============

        /// <summary>
        /// Initialize action tracking for a player.
        /// </summary>
        public void InitializePlayer(string playerId)
        {
            if (!playerActions.ContainsKey(playerId))
            {
                playerActions[playerId] = new List<ActionRecord>();
                Debug.Log($"[ActionTracker] Initialized tracking for {playerId}");
            }
        }

        /// <summary>
        /// Load action history for a player (from save).
        /// </summary>
        public void LoadActionHistory(string playerId, ActionRecord[] actions)
        {
            if (actions == null)
                return;

            playerActions[playerId] = new List<ActionRecord>(actions);
            Debug.Log($"[ActionTracker] Loaded {actions.Length} actions for {playerId}");
        }

        /// <summary>
        /// Get action history for saving.
        /// </summary>
        public ActionRecord[] GetActionHistoryForSave(string playerId)
        {
            if (!playerActions.ContainsKey(playerId))
                return new ActionRecord[0];

            return playerActions[playerId].ToArray();
        }

        /// <summary>
        /// Clear all actions for a player (start fresh trial).
        /// </summary>
        public void ClearPlayerActions(string playerId)
        {
            if (playerActions.ContainsKey(playerId))
            {
                playerActions[playerId].Clear();
                Debug.Log($"[ActionTracker] Cleared actions for {playerId}");
            }
        }

        // ============= STATISTICS =============

        /// <summary>
        /// Get action count for a player.
        /// </summary>
        public int GetActionCount(string playerId)
        {
            if (!playerActions.ContainsKey(playerId))
                return 0;

            return playerActions[playerId].Count;
        }

        /// <summary>
        /// Get statistics for a player.
        /// </summary>
        public void LogPlayerStatistics(string playerId)
        {
            if (!playerActions.ContainsKey(playerId))
            {
                Debug.Log($"[ActionTracker] No actions for {playerId}");
                return;
            }

            ActionRecord[] actions = playerActions[playerId].ToArray();
            float totalPoints = GetTotalPoints(playerId);
            Dictionary<string, float> byCategory = GetPointsByCategory(playerId);
            ActionRecord[] boosted = GetSquadBoostedActions(playerId);
            ActionRecord[] penalized = GetPenalizedActions(playerId);

            Debug.Log($"=== ACTION STATISTICS FOR {playerId} ===");
            Debug.Log($"Total Actions: {actions.Length}");
            Debug.Log($"Total Points: {totalPoints:F1}");
            Debug.Log($"Squad Boosted: {boosted.Length}");
            Debug.Log($"Penalized: {penalized.Length}");

            foreach (var kvp in byCategory)
            {
                Debug.Log($"  {kvp.Key}: {kvp.Value:F1}");
            }
        }
    }
}
