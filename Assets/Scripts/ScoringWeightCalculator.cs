using System;
using System.Collections.Generic;
using UnityEngine;

namespace EthraClone.TrialWeek
{
    /// <summary>
    /// Calculates final scores from action history.
    /// Transforms ActionRecords into ScoringData with weighted scoring.
    /// Called at end of trial to determine class/rank/guilds.
    /// </summary>
    public class ScoringWeightCalculator : MonoBehaviour
    {
        /// <summary>
        /// Scoring configuration: defines weights for each action type and its category.
        /// </summary>
        [System.Serializable]
        public class ActionWeightConfig
        {
            public string actionType;           // "kill", "craft", "discover", etc.
            public string category;             // "combat", "crafting", "exploration", etc.
            public int baseWeight = 5;          // Base points
            public bool countTowardsDominant = true;  // Contribute to dominant category calculation
        }

        [SerializeField] private ActionWeightConfig[] actionWeights = new ActionWeightConfig[]
        {
            // COMBAT
            new ActionWeightConfig { actionType = "kill", category = "combat", baseWeight = 5, countTowardsDominant = true },
            new ActionWeightConfig { actionType = "defeat_boss", category = "combat", baseWeight = 50, countTowardsDominant = true },
            new ActionWeightConfig { actionType = "land_crit", category = "combat", baseWeight = 10, countTowardsDominant = true },

            // MAGIC
            new ActionWeightConfig { actionType = "cast_spell", category = "magic", baseWeight = 8, countTowardsDominant = true },
            new ActionWeightConfig { actionType = "potion_effect", category = "magic", baseWeight = 3, countTowardsDominant = true },

            // CRAFTING
            new ActionWeightConfig { actionType = "craft", category = "crafting", baseWeight = 6, countTowardsDominant = true },
            new ActionWeightConfig { actionType = "smith", category = "crafting", baseWeight = 10, countTowardsDominant = true },
            new ActionWeightConfig { actionType = "discover_recipe", category = "crafting", baseWeight = 15, countTowardsDominant = true },

            // EXPLORATION
            new ActionWeightConfig { actionType = "discover", category = "exploration", baseWeight = 15, countTowardsDominant = true },
            new ActionWeightConfig { actionType = "explore_cave", category = "exploration", baseWeight = 25, countTowardsDominant = true },
            new ActionWeightConfig { actionType = "find_secret", category = "exploration", baseWeight = 30, countTowardsDominant = true },

            // HOMESTEADING
            new ActionWeightConfig { actionType = "farm_harvest", category = "homesteading", baseWeight = 8, countTowardsDominant = true },
            new ActionWeightConfig { actionType = "build_structure", category = "homesteading", baseWeight = 20, countTowardsDominant = true },
            new ActionWeightConfig { actionType = "tend_animal", category = "homesteading", baseWeight = 5, countTowardsDominant = true },

            // SOCIAL
            new ActionWeightConfig { actionType = "talk_npc", category = "social", baseWeight = 2, countTowardsDominant = true },
            new ActionWeightConfig { actionType = "complete_quest", category = "social", baseWeight = 25, countTowardsDominant = true },
            new ActionWeightConfig { actionType = "faction_favor", category = "social", baseWeight = 10, countTowardsDominant = true },

            // WEALTH
            new ActionWeightConfig { actionType = "find_treasure", category = "wealth", baseWeight = 20, countTowardsDominant = true },
            new ActionWeightConfig { actionType = "sell_item", category = "wealth", baseWeight = 1, countTowardsDominant = false },  // Doesn't count toward class
            new ActionWeightConfig { actionType = "loot_gold", category = "wealth", baseWeight = 2, countTowardsDominant = false },

            // SURVIVAL
            new ActionWeightConfig { actionType = "avoid_death", category = "survival", baseWeight = 50, countTowardsDominant = true },
            new ActionWeightConfig { actionType = "heal_self", category = "survival", baseWeight = 5, countTowardsDominant = true },
            new ActionWeightConfig { actionType = "resist_effect", category = "survival", baseWeight = 10, countTowardsDominant = true },
        };

        private Dictionary<string, ActionWeightConfig> actionWeightMap = new Dictionary<string, ActionWeightConfig>();

        // ============= SINGLETON =============
        private static ScoringWeightCalculator instance;
        public static ScoringWeightCalculator Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<ScoringWeightCalculator>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("ScoringWeightCalculator");
                        instance = go.AddComponent<ScoringWeightCalculator>();
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

            // Build lookup map
            foreach (ActionWeightConfig config in actionWeights)
            {
                actionWeightMap[config.actionType.ToLower()] = config;
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        // ============= SCORING CALCULATION =============

        /// <summary>
        /// Calculate final scores from action history.
        /// Returns ScoringData with points in each category.
        /// </summary>
        public ScoringData CalculateScores(ActionRecord[] actionHistory)
        {
            ScoringData scores = new ScoringData();

            if (actionHistory == null || actionHistory.Length == 0)
            {
                Debug.LogWarning("[ScoringWeightCalculator] Empty action history");
                return scores;
            }

            // Aggregate points by category
            Dictionary<string, int> categoryScores = new Dictionary<string, int>();

            foreach (ActionRecord action in actionHistory)
            {
                int points = (int)action.finalPoints;

                // Categorize and add to appropriate score
                switch (action.actionType.ToLower())
                {
                    case "kill":
                    case "defeat_boss":
                    case "land_crit":
                        scores.combatScore += points;
                        break;

                    case "cast_spell":
                    case "potion_effect":
                        scores.magicScore += points;
                        break;

                    case "craft":
                    case "smith":
                    case "discover_recipe":
                        scores.craftingScore += points;
                        break;

                    case "discover":
                    case "explore_cave":
                    case "find_secret":
                        scores.explorationScore += points;
                        break;

                    case "farm_harvest":
                    case "build_structure":
                    case "tend_animal":
                        scores.homesteadingScore += points;
                        break;

                    case "talk_npc":
                    case "complete_quest":
                    case "faction_favor":
                        scores.socialScore += points;
                        break;

                    case "find_treasure":
                    case "sell_item":
                    case "loot_gold":
                        scores.wealthScore += points;
                        break;

                    case "avoid_death":
                    case "heal_self":
                    case "resist_effect":
                        scores.survivalScore += points;
                        break;

                    default:
                        scores.socialScore += points;  // Default category
                        break;
                }
            }

            return scores;
        }

        /// <summary>
        /// Get the weight config for an action type.
        /// </summary>
        public ActionWeightConfig GetActionWeightConfig(string actionType)
        {
            string key = actionType.ToLower();
            if (actionWeightMap.ContainsKey(key))
            {
                return actionWeightMap[key];
            }

            // Return default config if not found
            return new ActionWeightConfig { actionType = actionType, category = "social", baseWeight = 5 };
        }

        /// <summary>
        /// Debug: log the scoring weights.
        /// </summary>
        public void LogWeightConfiguration()
        {
            Debug.Log("=== ACTION WEIGHT CONFIGURATION ===");
            foreach (ActionWeightConfig config in actionWeights)
            {
                Debug.Log($"{config.actionType} ({config.category}): {config.baseWeight} pts");
            }
        }
    }
}
