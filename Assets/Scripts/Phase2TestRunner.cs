using System.Collections;
using UnityEngine;

namespace EthraClone.TrialWeek
{
    /// <summary>
    /// Phase 2 Test Runner: Main orchestrator that runs all Phase 2 integration tests in sequence.
    /// - MonoBehaviour singleton that starts tests on Start()
    /// - Runs all tests using IEnumerator (coroutines)
    /// - Logs results with pass/fail status
    /// - Persists across scenes with DontDestroyOnLoad
    ///
    /// Test execution flow:
    /// 1. Disable Phase 2 (Phase2Enabler.Disable())
    /// 2. Run Phase2OriginalGameTests
    /// 3. Enable Phase 2 (Phase2Enabler.Enable())
    /// 4. Wait for manager initialization
    /// 5. Run Phase2IntegrationTests
    /// 6. Print final summary
    /// </summary>
    public class Phase2TestRunner : MonoBehaviour
    {
        private static Phase2TestRunner instance;
        private bool testsCompleted = false;

        private void Awake()
        {
            // Singleton pattern
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

        private void Start()
        {
            if (!testsCompleted)
            {
                testsCompleted = true;
                StartCoroutine(RunAllTests());
            }
        }

        /// <summary>
        /// Main test orchestration coroutine. Runs all tests in sequence.
        /// </summary>
        private IEnumerator RunAllTests()
        {
            Debug.Log("\n========== PHASE 2 TEST SUITE STARTING ==========\n");

            // ========= PHASE 1: TEST ORIGINAL GAME (PHASE 2 DISABLED) =========
            Debug.Log("[TEST_RUNNER] PHASE 1: Testing original game with Phase 2 disabled...");
            Phase2Enabler.Disable();
            yield return new WaitForSeconds(0.5f);

            yield return StartCoroutine(Phase2OriginalGameTests.RunTests((result) =>
            {
                Debug.Log($"[TEST_RUNNER] {result}");
            }));

            yield return new WaitForSeconds(1f);

            // ========= PHASE 2: TEST PHASE 2 INTEGRATION (PHASE 2 ENABLED) =========
            Debug.Log("[TEST_RUNNER] PHASE 2: Testing Phase 2 integration with Phase 2 enabled...");
            Phase2Enabler.Enable();
            yield return new WaitForSeconds(2f);  // Wait for managers to initialize

            // Ensure initialization
            EnsureManagersInitialized();
            yield return new WaitForSeconds(0.5f);

            yield return StartCoroutine(Phase2IntegrationTests.RunTests((result) =>
            {
                Debug.Log($"[TEST_RUNNER] {result}");
            }));

            yield return new WaitForSeconds(1f);

            // ========= PHASE 3: TEST REGRESSION (BUG FIXES) =========
            Debug.Log("[TEST_RUNNER] PHASE 3: Running regression tests for bug fixes...");
            yield return new WaitForSeconds(0.5f);

            yield return StartCoroutine(Phase2IntegrationTests.RunRegressionTests((result) =>
            {
                Debug.Log($"[TEST_RUNNER] {result}");
            }));

            yield return new WaitForSeconds(1f);

            // ========= FINAL SUMMARY =========
            PrintFinalSummary();
            Debug.Log("\n========== PHASE 2 TEST SUITE COMPLETE ==========\n");
            Debug.Log("All tests completed. Check console for results.");
        }

        /// <summary>
        /// Ensure all Phase 2 managers are initialized before testing.
        /// </summary>
        private void EnsureManagersInitialized()
        {
            Debug.Log("[TEST_RUNNER] Ensuring Phase 2 managers are initialized...");

            // Access each singleton to ensure initialization
            DifficultyConfig diffConfig = DifficultyConfig.Instance;
            TrialWeekManager trialWeekMgr = TrialWeekManager.Instance;
            ActionTracker actionTracker = ActionTracker.Instance;
            ProximityPenaltySystem proxSystem = ProximityPenaltySystem.Instance;
            DungeonInstanceSystem dungeonSystem = DungeonInstanceSystem.Instance;
            SaveGameManager saveGameMgr = SaveGameManager.Instance;

            if (diffConfig != null) Debug.Log("[TEST_RUNNER] ✓ DifficultyConfig initialized");
            if (trialWeekMgr != null) Debug.Log("[TEST_RUNNER] ✓ TrialWeekManager initialized");
            if (actionTracker != null) Debug.Log("[TEST_RUNNER] ✓ ActionTracker initialized");
            if (proxSystem != null) Debug.Log("[TEST_RUNNER] ✓ ProximityPenaltySystem initialized");
            if (dungeonSystem != null) Debug.Log("[TEST_RUNNER] ✓ DungeonInstanceSystem initialized");
            if (saveGameMgr != null) Debug.Log("[TEST_RUNNER] ✓ SaveGameManager initialized");
        }

        /// <summary>
        /// Print the final test summary with all counts and status.
        /// </summary>
        private void PrintFinalSummary()
        {
            string summary = Phase2TestAssertions.GetTestSummary();
            Debug.Log(summary);

            // Additional status check
            (int passed, int failed, int warnings) = Phase2TestAssertions.GetCounts();
            if (failed == 0)
            {
                Debug.Log("\n[TEST_RUNNER] SUCCESS: All tests passed! ✓");
            }
            else
            {
                Debug.LogError($"\n[TEST_RUNNER] FAILURE: {failed} test(s) failed! ✗");
            }
        }

        /// <summary>
        /// Check if tests have completed.
        /// </summary>
        public bool TestsCompleted => testsCompleted;

        /// <summary>
        /// Get the singleton instance.
        /// </summary>
        public static Phase2TestRunner Instance => instance;
    }
}
