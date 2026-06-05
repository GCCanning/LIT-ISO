using UnityEngine;

namespace EthraClone.TrialWeek
{
    /// <summary>
    /// Phase 2 Player Controller: Bridges individual player GameObjects to Phase 2 systems.
    ///
    /// Attach this component to each player GameObject in the scene.
    /// Responsibilities:
    /// - Register player with ActionTracker and TrialWeekManager on spawn
    /// - Fire OnPlayerSpawned event to initialize Phase 2 tracking
    /// - Track player position updates for proximity calculations
    ///
    /// This component is safe to attach even if Phase 2 is disabled (Phase2Enabler.IsActive).
    /// </summary>
    public class Phase2PlayerController : MonoBehaviour
    {
        [SerializeField] private string playerId = "Player1";

        private Vector3 lastReportedPosition = Vector3.zero;
        private bool hasInitialized = false;
        private IsoPlayerController isoController;

        private void Start()
        {
            isoController = GetComponent<IsoPlayerController>();

            // Skip initialization if Phase 2 is disabled
            if (!Phase2Enabler.IsActive)
{
                Debug.Log($"[Phase2PlayerController] Phase 2 disabled, skipping initialization for {playerId}");
                return;
            }

            InitializePlayer();
        }

        private void Update()
        {
            // Skip updates if Phase 2 is disabled or not initialized
            if (!Phase2Enabler.IsActive || !hasInitialized)
                return;

            // Movement is now tracked via IsoPlayerController.OnMoved event subscribed by ProximityPenaltySystem
        }

        /// <summary>
        /// Initialize the player with Phase 2 systems.
        /// </summary>
        private void InitializePlayer()
        {
            try
            {
                // Initialize player in ActionTracker
                ActionTracker actionTracker = ActionTracker.Instance;
                if (actionTracker != null)
                {
                    actionTracker.InitializePlayer(playerId);
                    Debug.Log($"[Phase2PlayerController] {playerId} registered with ActionTracker");
                }

                // Register player trial with TrialWeekManager
                TrialWeekManager trialWeekManager = TrialWeekManager.Instance;
                if (trialWeekManager != null)
                {
                    trialWeekManager.RegisterPlayerTrial(playerId);
                    Debug.Log($"[Phase2PlayerController] {playerId} registered trial with TrialWeekManager");
                }

                // Register player with ProximityPenaltySystem via events
                ProximityPenaltySystem proximitySystem = ProximityPenaltySystem.Instance;
                if (proximitySystem != null && isoController != null)
                {
                    proximitySystem.RegisterPlayer(isoController, playerId);
                }

                // Fire OnPlayerSpawned event (deprecated bridge)
                Vector3 spawnPosition = transform.position;
                Phase2IntegrationBridge.NotifyPlayerSpawned(playerId, spawnPosition);
                lastReportedPosition = spawnPosition;

                hasInitialized = true;
                Debug.Log($"[Phase2PlayerController] {playerId} fully initialized for Phase 2");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Phase2PlayerController] Error initializing {playerId}: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            if (isoController != null && ProximityPenaltySystem.Instance != null)
            {
                ProximityPenaltySystem.Instance.UnregisterPlayer(isoController);
            }
        }

        /// <summary>
        /// Get the playerId assigned to this controller.
/// </summary>
        public string GetPlayerId() => playerId;

        /// <summary>
        /// Set the playerId (can be used for multiplayer assignment).
        /// </summary>
        public void SetPlayerId(string newPlayerId)
        {
            playerId = newPlayerId;
            Debug.Log($"[Phase2PlayerController] PlayerId set to {playerId}");
        }
    }
}
