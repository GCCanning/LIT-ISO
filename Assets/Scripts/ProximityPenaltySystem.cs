using System;
using System.Collections.Generic;
using UnityEngine;

namespace EthraClone.TrialWeek
{
    /// <summary>
    /// Manages proximity-based scoring penalties in open world.
    /// When another player is nearby during a player's trial, their actions are penalized.
    /// Prevents carrying/power-leveling while allowing co-op in dungeons (which have squad boost instead).
    /// </summary>
    public class ProximityPenaltySystem : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private float proximityRadius = 50f;  // Units
        [SerializeField] private float checkInterval = 0.5f;   // Check every 0.5 sec
        [SerializeField] private float basePenaltyMultiplier = 0.2f;  // 80% reduction = 0.2x

        /// <summary> Action category to penalty multiplier mapping. </summary>
        [System.Serializable]
        public class ActionPenaltyConfig
        {
            public string actionCategory;  // "combat", "crafting", "exploration", etc.
            public float penaltyMultiplier = 0.2f;  // 0.2x = 80% reduction
        }

        [SerializeField] private ActionPenaltyConfig[] actionPenalties = new ActionPenaltyConfig[]
        {
            new ActionPenaltyConfig { actionCategory = "combat", penaltyMultiplier = 0.2f },
            new ActionPenaltyConfig { actionCategory = "exploration", penaltyMultiplier = 0.2f },
            new ActionPenaltyConfig { actionCategory = "magic", penaltyMultiplier = 0.2f },
            new ActionPenaltyConfig { actionCategory = "crafting", penaltyMultiplier = 0.4f },
            new ActionPenaltyConfig { actionCategory = "homesteading", penaltyMultiplier = 1.0f },
            new ActionPenaltyConfig { actionCategory = "social", penaltyMultiplier = 0.8f },
        };

        /// <summary> Cache of player positions. </summary>
        private Dictionary<string, Vector3> playerPositions = new Dictionary<string, Vector3>();
        private Dictionary<IsoPlayerController, Action<Vector3>> movementHandlers = new Dictionary<IsoPlayerController, Action<Vector3>>();
        private float lastCheckTime = 0f;

        // ============= SINGLETON =============
private static ProximityPenaltySystem instance;
        public static ProximityPenaltySystem Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<ProximityPenaltySystem>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("ProximityPenaltySystem");
                        instance = go.AddComponent<ProximityPenaltySystem>();
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

            SubscribeToEvents();
        }

        private void OnEnable()
        {
            if (instance == this)
            {
                SubscribeToEvents();
            }
        }

        private void OnDisable()
        {
            if (instance == this)
            {
                UnsubscribeFromEvents();
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                UnsubscribeFromEvents();
                instance = null;
            }
        }

        // ============= EVENT SUBSCRIPTION =============

        private void SubscribeToEvents()
        {
            // Migrating away from static bridge to instance-based events
        }

        private void UnsubscribeFromEvents()
        {
            // Clean up all handlers if system is destroyed
            foreach (var kvp in movementHandlers)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.OnMoved -= kvp.Value;
                }
            }
            movementHandlers.Clear();
        }

        public void RegisterPlayer(IsoPlayerController player, string playerId)
        {
            if (player == null || string.IsNullOrEmpty(playerId)) return;
            
            // Avoid duplicate registrations
            if (movementHandlers.ContainsKey(player)) return;

            Action<Vector3> handler = (pos) => OnPlayerMoved(playerId, pos);
            movementHandlers[player] = handler;
            player.OnMoved += handler;

            UpdatePlayerPosition(playerId, player.transform.position);
            Debug.Log($"[ProximityPenaltySystem] Registered player {playerId} for movement tracking.");
        }

        public void UnregisterPlayer(IsoPlayerController player)
        {
            if (player == null) return;
            
            if (movementHandlers.TryGetValue(player, out Action<Vector3> handler))
            {
                player.OnMoved -= handler;
                movementHandlers.Remove(player);
                Debug.Log("[ProximityPenaltySystem] Unregistered player from movement tracking.");
            }
        }

        private void OnPlayerMoved(string playerId, Vector3 newPosition)
{
            UpdatePlayerPosition(playerId, newPosition);
        }

        private void Update()
        {
            // Periodically update player position cache
            if (Time.time - lastCheckTime >= checkInterval)
            {
                UpdatePlayerPositionCache();
                lastCheckTime = Time.time;
            }
        }

        // ============= POSITION TRACKING =============

        /// <summary>
        /// Update a player's position (call this from player controller every frame or periodically).
        /// </summary>
        public void UpdatePlayerPosition(string playerId, Vector3 position)
        {
            playerPositions[playerId] = position;
        }

        private void UpdatePlayerPositionCache()
        {
            // Clean up positions for players no longer in trial
            List<string> toRemove = new List<string>();
            TrialWeekManager trialManager = TrialWeekManager.Instance;

            foreach (string playerId in playerPositions.Keys)
            {
                if (trialManager != null && !trialManager.IsPlayerInTrial(playerId))
                {
                    toRemove.Add(playerId);
                }
            }

            foreach (string playerId in toRemove)
            {
                playerPositions.Remove(playerId);
            }
        }

        // ============= PENALTY CALCULATION =============

        /// <summary>
        /// Get the proximity penalty multiplier for a player at the moment of action.
        /// </summary>
        public float GetProximityMultiplier(string actorPlayerId, string actionCategory)
        {
            TrialWeekManager trialManager = TrialWeekManager.Instance;
            if (trialManager == null || !trialManager.IsPlayerInTrial(actorPlayerId))
            {
                return 1.0f;  // No penalty if not in trial
            }

            // Get actor position
            if (!playerPositions.ContainsKey(actorPlayerId))
            {
                return 1.0f;  // No position data, assume no penalty
            }

            Vector3 actorPos = playerPositions[actorPlayerId];

            // Check if any OTHER player is nearby
            foreach (var kvp in playerPositions)
            {
                string otherPlayerId = kvp.Key;
                if (otherPlayerId == actorPlayerId)
                    continue;

                Vector3 otherPos = kvp.Value;
                float distance = Vector3.Distance(actorPos, otherPos);

                // If nearby player also in trial, apply penalty
                if (distance <= proximityRadius && trialManager.IsPlayerInTrial(otherPlayerId))
                {
                    return GetPenaltyForCategory(actionCategory);
                }
            }

            return 1.0f;  // No penalty, no nearby players in trial
        }

        /// <summary>
        /// Get penalty multiplier for a specific action category.
        /// </summary>
        private float GetPenaltyForCategory(string category)
        {
            foreach (ActionPenaltyConfig config in actionPenalties)
            {
                if (config.actionCategory.Equals(category, StringComparison.OrdinalIgnoreCase))
                {
                    return config.penaltyMultiplier;
                }
            }

            // Default to base penalty if category not found
            return basePenaltyMultiplier;
        }

        /// <summary>
        /// Check which players are nearby a given player.
        /// </summary>
        public string[] GetNearbyPlayersInTrial(string playerId)
        {
            TrialWeekManager trialManager = TrialWeekManager.Instance;
            if (trialManager == null || !playerPositions.ContainsKey(playerId))
            {
                return new string[0];
            }

            Vector3 playerPos = playerPositions[playerId];
            List<string> nearby = new List<string>();

            foreach (var kvp in playerPositions)
            {
                string otherPlayerId = kvp.Key;
                if (otherPlayerId == playerId)
                    continue;

                Vector3 otherPos = kvp.Value;
                float distance = Vector3.Distance(playerPos, otherPos);

                if (distance <= proximityRadius && trialManager.IsPlayerInTrial(otherPlayerId))
                {
                    nearby.Add(otherPlayerId);
                }
            }

            return nearby.ToArray();
        }

        /// <summary>
        /// Debug: Check if penalty would apply for actor.
        /// </summary>
        public bool WouldPenaltyApply(string actorPlayerId)
        {
            return GetProximityMultiplier(actorPlayerId, "combat") < 1.0f;
        }

        // ============= CONFIGURATION =============

        public void SetProximityRadius(float radius)
        {
            proximityRadius = radius;
            Debug.Log($"[ProximityPenaltySystem] Proximity radius set to {radius}");
        }

        public void SetBasePenalty(float multiplier)
        {
            basePenaltyMultiplier = multiplier;
            Debug.Log($"[ProximityPenaltySystem] Base penalty multiplier set to {multiplier}");
        }

        public float GetProximityRadius() => proximityRadius;
        public int GetPlayersTracked() => playerPositions.Count;
    }
}
