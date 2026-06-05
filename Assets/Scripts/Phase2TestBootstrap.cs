using UnityEngine;

namespace EthraClone.TrialWeek
{
    /// <summary>
    /// Phase 2 Test Bootstrap: Sets up the minimal test environment.
    /// Attach this to a "Bootstrap" GameObject in your test scene.
    /// Creates Phase2TestRunner and Phase2TestResultsUI automatically in Awake().
    /// Allows running tests without a full game scene.
    /// </summary>
    public class Phase2TestBootstrap : MonoBehaviour
    {
        [Header("Test Setup")]
        [SerializeField] private bool autoStartTests = true;
        [SerializeField] private bool showTestUI = true;

        private void Awake()
        {
            Debug.Log("[TEST_BOOTSTRAP] Initializing Phase 2 test environment...");

            // Instantiate Phase2TestRunner if it doesn't exist
            Phase2TestRunner testRunner = FindFirstObjectByType<Phase2TestRunner>();
            if (testRunner == null)
            {
                GameObject runnerGO = new GameObject("Phase2TestRunner");
                runnerGO.AddComponent<Phase2TestRunner>();
                Debug.Log("[TEST_BOOTSTRAP] Created Phase2TestRunner");
            }

            // Instantiate Phase2TestResultsUI if requested
            if (showTestUI)
            {
                Phase2TestResultsUI testUI = FindFirstObjectByType<Phase2TestResultsUI>();
                if (testUI == null)
                {
                    GameObject uiGO = new GameObject("Phase2TestResultsUI");
                    uiGO.AddComponent<Phase2TestResultsUI>();
                    Debug.Log("[TEST_BOOTSTRAP] Created Phase2TestResultsUI");
                }
            }

            Debug.Log("[TEST_BOOTSTRAP] Phase 2 test environment ready!");

            if (autoStartTests)
            {
                Debug.Log("[TEST_BOOTSTRAP] Auto-starting tests...");
            }
        }
    }
}
