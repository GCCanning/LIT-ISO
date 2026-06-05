using UnityEngine;

namespace EthraClone.TrialWeek
{
    /// <summary>
    /// Phase 2 Visual Manager: Handles visual updates based on world time of day changes.
    ///
    /// Responsibilities:
    /// - Subscribe to TrialWeekManager's OnWorldTimeOfDayChanged event
    /// - Update camera background color based on Day/Night cycle
    /// - Can be extended for other visual effects (lighting, fog, particle systems, etc.)
    ///
    /// Attach this component to the main camera or a Game Manager GameObject.
    /// This component is safe to attach even if Phase 2 is disabled.
    /// </summary>
    public class Phase2VisualManager : MonoBehaviour
    {
        [Header("Day/Night Colors")]
        [SerializeField] private Color dayColor = Color.white;
        [SerializeField] private Color nightColor = new Color(0.3f, 0.3f, 0.5f);

        private Camera mainCamera;
        private TrialWeekManager trialWeekManager;
        private bool hasSubscribed = false;

        private void Start()
        {
            // Skip initialization if Phase 2 is disabled
            if (!Phase2Enabler.IsActive)
            {
                Debug.Log("[Phase2VisualManager] Phase 2 disabled, visual updates skipped");
                return;
            }

            InitializeVisualManager();
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (hasSubscribed && trialWeekManager != null)
            {
                trialWeekManager.OnWorldTimeOfDayChanged -= HandleTimeOfDayChange;
                Debug.Log("[Phase2VisualManager] Unsubscribed from TrialWeekManager events");
            }
        }

        /// <summary>
        /// Initialize visual manager and subscribe to events.
        /// </summary>
        private void InitializeVisualManager()
        {
            try
            {
                // Get main camera
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    Debug.LogWarning("[Phase2VisualManager] Main camera not found, cannot update visuals");
                    return;
                }

                // Subscribe to TrialWeekManager events
                trialWeekManager = TrialWeekManager.Instance;
                if (trialWeekManager != null)
                {
                    trialWeekManager.OnWorldTimeOfDayChanged += HandleTimeOfDayChange;
                    hasSubscribed = true;
                    Debug.Log("[Phase2VisualManager] Subscribed to TrialWeekManager time changes");

                    // Set initial color based on current time of day
                    TimeOfDay currentTime = trialWeekManager.GetWorldTimeOfDay();
                    HandleTimeOfDayChange(currentTime);
                }
                else
                {
                    Debug.LogWarning("[Phase2VisualManager] TrialWeekManager not found, visual updates unavailable");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Phase2VisualManager] Error initializing visual manager: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle time of day changes and update visuals accordingly.
        /// </summary>
        private void HandleTimeOfDayChange(TimeOfDay newTimeOfDay)
        {
            if (mainCamera == null)
                return;

            Color targetColor = newTimeOfDay == TimeOfDay.Day ? dayColor : nightColor;
            mainCamera.backgroundColor = targetColor;

            Debug.Log($"[Phase2VisualManager] Time changed to {newTimeOfDay}, camera color: {targetColor}");
        }
    }
}
