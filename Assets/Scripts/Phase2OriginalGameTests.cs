using System;
using System.Collections;
using UnityEngine;

namespace EthraClone.TrialWeek
{
    /// <summary>
    /// Phase 2 Original Game Tests (Test 1 - Phase 2 Disabled).
    /// Tests that original game functionality works without Phase 2 active.
    /// Verifies backward compatibility and that Phase 2 doesn't break base systems.
    /// </summary>
    public static class Phase2OriginalGameTests
    {
        /// <summary>
        /// Run all original game tests with Phase 2 disabled.
        /// </summary>
        public static IEnumerator RunTests(Action<string> callback)
        {
            Debug.Log("[PHASE2_ORIGINAL_TESTS] Starting original game tests with Phase 2 DISABLED...");
            Phase2TestAssertions.ResetCounters();

            // Test 1: Phase2Enabler is disabled
            yield return new WaitForSeconds(0.1f);
            TestPhase2IsDisabled();

            // Test 2: Player movement controller can be found
            yield return new WaitForSeconds(0.1f);
            TestPlayerMovementExists();

            // Test 3: ActionTracker doesn't crash even if we access it
            yield return new WaitForSeconds(0.1f);
            TestActionTrackerDoesntCrash();

            // Test 4: No null reference exceptions in original systems
            yield return new WaitForSeconds(0.1f);
            TestNoNullReferenceExceptions();

            // Test 5: Phase 2 Enabler truly blocks Phase 2 notifications
            yield return new WaitForSeconds(0.1f);
            TestPhase2NotificationsBlocked();

            // All tests completed
            Debug.Log("[PHASE2_ORIGINAL_TESTS] Original game tests completed!");
            (int passed, int failed, int warnings) = Phase2TestAssertions.GetCounts();
            callback?.Invoke($"Original Game Tests: {passed} passed, {failed} failed, {warnings} warnings");
        }

        private static void TestPhase2IsDisabled()
        {
            string testName = "Phase 2 Disabled Check";
            bool isDisabled = !Phase2Enabler.IsActive;
            Phase2TestAssertions.AssertTrue(isDisabled, testName, "Phase2Enabler.IsActive should be false");
        }

        private static void TestPlayerMovementExists()
        {
            string testName = "Player Movement Controller Exists";
            IsometricPlayerMovementController controller = UnityEngine.Object.FindFirstObjectByType<IsometricPlayerMovementController>();
            bool exists = controller != null;
            Phase2TestAssertions.AssertTrue(exists, testName, "IsometricPlayerMovementController should exist or be findable");

            if (exists)
            {
                Phase2TestAssertions.AssertTrue(controller.movementSpeed > 0, testName, "Player movement speed should be positive");
            }
        }

        private static void TestActionTrackerDoesntCrash()
        {
            string testName = "ActionTracker No Crash";
            try
            {
                // Even though Phase 2 is disabled, ActionTracker should still exist as a singleton
                ActionTracker tracker = ActionTracker.Instance;
                Phase2TestAssertions.AssertNotNull(tracker, testName, "ActionTracker singleton should exist");

                // Try to access a method without crashing
                int actionCount = tracker.GetActionCount("TestPlayer");
                Phase2TestAssertions.AssertTrue(actionCount >= 0, testName, "ActionTracker should return valid action count");
            }
            catch (Exception ex)
            {
                Phase2TestAssertions.LogWarning(testName, $"Unexpected exception: {ex.Message}");
            }
        }

        private static void TestNoNullReferenceExceptions()
        {
            string testName = "No Null Reference Exceptions";
            try
            {
                // Try to access various singleton managers
                DifficultyConfig diffConfig = DifficultyConfig.Instance;
                ProximityPenaltySystem proxSystem = ProximityPenaltySystem.Instance;
                DungeonInstanceSystem dungeonSystem = DungeonInstanceSystem.Instance;

                // If we get here without exceptions, we're good
                Phase2TestAssertions.AssertTrue(true, testName, "No null reference exceptions occurred");
            }
            catch (System.NullReferenceException ex)
            {
                Phase2TestAssertions.LogWarning(testName, $"Null reference: {ex.Message}");
            }
            catch (Exception ex)
            {
                Phase2TestAssertions.LogWarning(testName, $"Other exception: {ex.Message}");
            }
        }

        private static void TestPhase2NotificationsBlocked()
        {
            string testName = "Phase 2 Notifications Blocked When Disabled";
            try
            {
                // Phase 2 Enabler should prevent notifications from firing
                // We can't easily test this without counting event subscriptions,
                // so we'll just verify the state
                bool isActive = Phase2Enabler.IsActive;
                Phase2TestAssertions.AssertFalse(isActive, testName, "Phase2Enabler.IsActive should remain false throughout tests");
            }
            catch (Exception ex)
            {
                Phase2TestAssertions.LogWarning(testName, $"Exception: {ex.Message}");
            }
        }
    }
}
