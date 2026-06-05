using UnityEngine;

namespace EthraClone.TrialWeek
{
    /// <summary>
    /// Phase 2 Manager Initializer: Ensures all Phase 2 managers are initialized in the correct order.
    ///
    /// Initialization order (critical for dependency resolution):
    /// 1. DifficultyConfig - Base difficulty settings (no dependencies)
    /// 2. TrialWeekManager - World time/player trial tracking
    /// 3. ActionTracker - Per-player action logging
    /// 4. ProximityPenaltySystem - Proximity calculations (depends on player positions)
    /// 5. DungeonInstanceSystem - Squad dungeon management
    /// 6. SaveGameManager - Save/load system (depends on all above)
    /// 7. Phase 2 visual/player controllers handle themselves
    ///
    /// Attach this as a MonoBehaviour singleton to a Game Manager or Bootstrap GameObject
    /// that persists across scenes (or place it on the first active scene).
    /// </summary>
    public class Phase2ManagerInitializer : MonoBehaviour
    {
        private static bool isInitialized = false;
        private static Phase2ManagerInitializer instance = null;

        private void Awake()
        {
            // Prevent duplicate initialization instances
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;

            // Prevent re-initialization in same session
            if (isInitialized)
            {
                Debug.LogWarning("[Phase2ManagerInitializer] Already initialized, skipping.");
                return;
            }

            InitializePhase2Systems();
            isInitialized = true;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
                // Reset flag on destruction to allow re-initialization in new scenes
                isInitialized = false;
            }
        }

        /// <summary>
        /// Force initialization of all Phase 2 managers in dependency order.
        /// </summary>
        private void InitializePhase2Systems()
        {
            Debug.Log("[Phase2ManagerInitializer] Beginning Phase 2 system initialization...");

            // Step 1: Initialize DifficultyConfig
            DifficultyConfig difficultyConfig = DifficultyConfig.Instance;
            if (difficultyConfig != null)
            {
                Debug.Log("[Phase2ManagerInitializer] ✓ DifficultyConfig initialized");
            }
            else
            {
                Debug.LogWarning("[Phase2ManagerInitializer] ✗ DifficultyConfig failed to initialize");
            }

            // Step 2: Initialize TrialWeekManager
            TrialWeekManager trialWeekManager = TrialWeekManager.Instance;
            if (trialWeekManager != null)
            {
                Debug.Log("[Phase2ManagerInitializer] ✓ TrialWeekManager initialized");
            }
            else
            {
                Debug.LogWarning("[Phase2ManagerInitializer] ✗ TrialWeekManager failed to initialize");
            }

            // Step 3: Initialize ActionTracker
            ActionTracker actionTracker = ActionTracker.Instance;
            if (actionTracker != null)
            {
                Debug.Log("[Phase2ManagerInitializer] ✓ ActionTracker initialized");
            }
            else
            {
                Debug.LogWarning("[Phase2ManagerInitializer] ✗ ActionTracker failed to initialize");
            }

            // Step 4: Initialize ProximityPenaltySystem
            ProximityPenaltySystem proximityPenaltySystem = ProximityPenaltySystem.Instance;
            if (proximityPenaltySystem != null)
            {
                Debug.Log("[Phase2ManagerInitializer] ✓ ProximityPenaltySystem initialized");
            }
            else
            {
                Debug.LogWarning("[Phase2ManagerInitializer] ✗ ProximityPenaltySystem failed to initialize");
            }

            // Step 5: Initialize DungeonInstanceSystem
            DungeonInstanceSystem dungeonInstanceSystem = DungeonInstanceSystem.Instance;
            if (dungeonInstanceSystem != null)
            {
                Debug.Log("[Phase2ManagerInitializer] ✓ DungeonInstanceSystem initialized");
            }
            else
            {
                Debug.LogWarning("[Phase2ManagerInitializer] ✗ DungeonInstanceSystem failed to initialize");
            }

            // Step 6: Initialize SaveGameManager
            SaveGameManager saveGameManager = SaveGameManager.Instance;
            if (saveGameManager != null)
            {
                Debug.Log("[Phase2ManagerInitializer] ✓ SaveGameManager initialized");
            }
            else
            {
                Debug.LogWarning("[Phase2ManagerInitializer] ✗ SaveGameManager failed to initialize");
            }

            // All managers initialized
            Debug.Log("[Phase2ManagerInitializer] Phase 2 system initialization complete!");
            Phase2IntegrationBridge.NotifyPhase2Ready();
        }

        /// <summary>
        /// Reset initialization flag (for testing or scene reloads).
        /// </summary>
        public static void ResetInitialization()
        {
            isInitialized = false;
            Debug.Log("[Phase2ManagerInitializer] Initialization flag reset");
        }
    }
}
