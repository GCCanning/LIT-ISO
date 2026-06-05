using UnityEngine;

namespace EthraClone.TrialWeek
{
    /// <summary>
    /// Phase 2 Test Assertions: Static utility class for test validation.
    /// Provides assertion methods and tracks pass/fail/warn counts globally.
    /// Each assertion logs test result with ✓ PASS or ✗ FAIL or ⚠ WARN.
    /// </summary>
    public static class Phase2TestAssertions
    {
        private static int passCount = 0;
        private static int failCount = 0;
        private static int warnCount = 0;

        /// <summary>
        /// Reset test counters for a new test run.
        /// </summary>
        public static void ResetCounters()
        {
            passCount = 0;
            failCount = 0;
            warnCount = 0;
        }

        /// <summary>
        /// Assert that a condition is true.
        /// </summary>
        public static void AssertTrue(bool condition, string testName, string message)
        {
            if (condition)
            {
                LogPass(testName, message);
            }
            else
            {
                LogFail(testName, message);
            }
        }

        /// <summary>
        /// Assert that a condition is false.
        /// </summary>
        public static void AssertFalse(bool condition, string testName, string message)
        {
            if (!condition)
            {
                LogPass(testName, message);
            }
            else
            {
                LogFail(testName, message);
            }
        }

        /// <summary>
        /// Assert that an object is null.
        /// </summary>
        public static void AssertNull(object obj, string testName, string message)
        {
            if (obj == null)
            {
                LogPass(testName, message);
            }
            else
            {
                LogFail(testName, $"{message} (object was not null)");
            }
        }

        /// <summary>
        /// Assert that an object is not null.
        /// </summary>
        public static void AssertNotNull(object obj, string testName, string message)
        {
            if (obj != null)
            {
                LogPass(testName, message);
            }
            else
            {
                LogFail(testName, $"{message} (object was null)");
            }
        }

        /// <summary>
        /// Assert that two objects are equal.
        /// </summary>
        public static void AssertEqual(object expected, object actual, string testName, string message)
        {
            if ((expected == null && actual == null) || (expected != null && expected.Equals(actual)))
            {
                LogPass(testName, message);
            }
            else
            {
                LogFail(testName, $"{message} (expected: {expected}, actual: {actual})");
            }
        }

        /// <summary>
        /// Assert that two objects are not equal.
        /// </summary>
        public static void AssertNotEqual(object expected, object actual, string testName, string message)
        {
            if (!((expected == null && actual == null) || (expected != null && expected.Equals(actual))))
            {
                LogPass(testName, message);
            }
            else
            {
                LogFail(testName, $"{message} (expected and actual were equal)");
            }
        }

        /// <summary>
        /// Log a warning (non-blocking issue).
        /// </summary>
        public static void LogWarning(string testName, string message)
        {
            warnCount++;
            Debug.LogWarning($"[TEST] {testName}\n  ⚠ WARN: {message}");
        }

        /// <summary>
        /// Get current test summary as a formatted string.
        /// </summary>
        public static string GetTestSummary()
        {
            int totalTests = passCount + failCount + warnCount;
            string statusIcon = failCount == 0 ? "✓" : "✗";
            string statusText = failCount == 0 ? "ALL TESTS PASSED" : $"FAILURES DETECTED";

            return $@"
=== PHASE 2 INTEGRATION TEST SUMMARY ===
Total Tests: {totalTests}
Passed: {passCount}
Failed: {failCount}
Warnings: {warnCount}
Status: {statusText} {statusIcon}";
        }

        /// <summary>
        /// Get current counts for display purposes.
        /// </summary>
        public static (int passed, int failed, int warnings) GetCounts()
        {
            return (passCount, failCount, warnCount);
        }

        // ============= PRIVATE HELPERS =============

        private static void LogPass(string testName, string message)
        {
            passCount++;
            Debug.Log($"[TEST] {testName}\n  ✓ PASS: {message}");
        }

        private static void LogFail(string testName, string message)
        {
            failCount++;
            Debug.LogError($"[TEST] {testName}\n  ✗ FAIL: {message}");
        }
    }
}
