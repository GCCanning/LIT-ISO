using UnityEngine;

namespace EthraClone.TrialWeek
{
    /// <summary>
    /// Difficulty settings and multipliers.
    /// - Mob health/damage multipliers
    /// - Action weight multipliers (harder = more points)
    /// - Squad boost configuration
    /// - Loot quality rates
    /// </summary>
    public class DifficultyConfig : MonoBehaviour
    {
        [System.Serializable]
        public class DifficultySettings
        {
            [Header("Mob Stats")]
            public float mobHealthMultiplier = 1.0f;
            public float mobDamageMultiplier = 1.0f;
            public bool mobsApplyEffects = true;  // poison, stun, etc.

            [Header("Loot")]
            public float lootQualityMultiplier = 1.0f;  // Affects rarity drops
            public float goldDropMultiplier = 1.0f;

            [Header("Scoring")]
            public float actionWeightMultiplier = 1.0f;  // How much actions are worth
            public float squadBoostMultiplier = 1.5f;    // Boost in dungeons with all 4 in trial
            public float proximityPenaltyMultiplier = 0.2f;  // 80% reduction = 0.2x

            [Header("Proximity")]
            public float proximityRadius = 50f;  // Units
        }

        [SerializeField] private GameDifficulty currentDifficulty = GameDifficulty.Normal;

        [SerializeField] private DifficultySettings easySettings = new DifficultySettings
        {
            mobHealthMultiplier = 0.7f,
            mobDamageMultiplier = 0.7f,
            mobsApplyEffects = false,
            lootQualityMultiplier = 1.1f,
            goldDropMultiplier = 1.0f,
            actionWeightMultiplier = 0.8f,
            squadBoostMultiplier = 1.3f,
            proximityRadius = 75f,
        };

        [SerializeField] private DifficultySettings normalSettings = new DifficultySettings
        {
            mobHealthMultiplier = 1.0f,
            mobDamageMultiplier = 1.0f,
            mobsApplyEffects = true,
            lootQualityMultiplier = 1.0f,
            goldDropMultiplier = 1.0f,
            actionWeightMultiplier = 1.0f,
            squadBoostMultiplier = 1.5f,
            proximityRadius = 50f,
        };

        [SerializeField] private DifficultySettings hardSettings = new DifficultySettings
        {
            mobHealthMultiplier = 1.5f,
            mobDamageMultiplier = 1.5f,
            mobsApplyEffects = true,
            lootQualityMultiplier = 0.9f,
            goldDropMultiplier = 1.2f,
            actionWeightMultiplier = 1.4f,
            squadBoostMultiplier = 1.7f,
            proximityRadius = 30f,
        };

        [SerializeField] private DifficultySettings hardcoreSettings = new DifficultySettings
        {
            mobHealthMultiplier = 2.0f,
            mobDamageMultiplier = 2.0f,
            mobsApplyEffects = true,
            lootQualityMultiplier = 0.8f,
            goldDropMultiplier = 1.5f,
            actionWeightMultiplier = 2.0f,
            squadBoostMultiplier = 2.0f,
            proximityRadius = 20f,
        };

        // ============= SINGLETON =============
        private static DifficultyConfig instance;
        public static DifficultyConfig Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<DifficultyConfig>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("DifficultyConfig");
                        instance = go.AddComponent<DifficultyConfig>();
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

        // ============= PUBLIC API =============

        /// <summary>
        /// Set the game difficulty.
        /// </summary>
        public void SetDifficulty(GameDifficulty difficulty)
        {
            currentDifficulty = difficulty;
            Debug.Log($"[DifficultyConfig] Difficulty set to {difficulty}");
        }

        /// <summary>
        /// Get current difficulty.
        /// </summary>
        public GameDifficulty GetCurrentDifficulty() => currentDifficulty;

        /// <summary>
        /// Get settings for a specific difficulty.
        /// </summary>
        public DifficultySettings GetSettings(GameDifficulty difficulty)
        {
            return difficulty switch
            {
                GameDifficulty.Easy => easySettings,
                GameDifficulty.Normal => normalSettings,
                GameDifficulty.Hard => hardSettings,
                GameDifficulty.Hardcore => hardcoreSettings,
                _ => normalSettings,
            };
        }

        /// <summary>
        /// Get settings for current difficulty.
        /// </summary>
        public DifficultySettings GetCurrentSettings() => GetSettings(currentDifficulty);

        // ============= SPECIFIC MULTIPLIERS =============

        public float GetMobHealthMultiplier() => GetCurrentSettings().mobHealthMultiplier;
        public float GetMobDamageMultiplier() => GetCurrentSettings().mobDamageMultiplier;
        public bool GetMobsApplyEffects() => GetCurrentSettings().mobsApplyEffects;
        public float GetLootQualityMultiplier() => GetCurrentSettings().lootQualityMultiplier;
        public float GetGoldDropMultiplier() => GetCurrentSettings().goldDropMultiplier;
        public float GetActionWeightMultiplier() => GetCurrentSettings().actionWeightMultiplier;
        public float GetSquadBoostMultiplier() => GetCurrentSettings().squadBoostMultiplier;
        public float GetProximityPenaltyMultiplier() => GetCurrentSettings().proximityPenaltyMultiplier;
        public float GetProximityRadius() => GetCurrentSettings().proximityRadius;

        // ============= DEBUG =============

        public void LogCurrentSettings()
        {
            DifficultySettings settings = GetCurrentSettings();
            Debug.Log($"=== DIFFICULTY: {currentDifficulty} ===");
            Debug.Log($"Mob Health: {settings.mobHealthMultiplier}x");
            Debug.Log($"Mob Damage: {settings.mobDamageMultiplier}x");
            Debug.Log($"Mob Effects: {settings.mobsApplyEffects}");
            Debug.Log($"Loot Quality: {settings.lootQualityMultiplier}x");
            Debug.Log($"Gold Drop: {settings.goldDropMultiplier}x");
            Debug.Log($"Action Weight: {settings.actionWeightMultiplier}x");
            Debug.Log($"Squad Boost: {settings.squadBoostMultiplier}x");
            Debug.Log($"Proximity Penalty: {settings.proximityPenaltyMultiplier}x");
            Debug.Log($"Proximity Radius: {settings.proximityRadius} units");
        }
    }
}
