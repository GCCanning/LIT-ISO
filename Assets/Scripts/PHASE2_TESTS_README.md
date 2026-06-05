# Phase 2 Integration Test Suite

Complete automated test suite for Phase 2 integration testing in the LIT-ISO project.

## Overview

This test framework validates Phase 2 functionality through three sequential test phases:

1. **Original Game Tests** (Phase 2 Disabled) - Verify backward compatibility
2. **Phase 2 Integration Tests** (Phase 2 Enabled) - Verify Phase 2 functionality
3. **Runtime Toggle Tests** - Verify enable/disable at runtime

## Files Included

### Core Test Files

1. **Phase2TestRunner.cs** - Main orchestrator
   - MonoBehaviour singleton that runs all tests in sequence
   - Starts automatically on game Start()
   - Handles test sequencing and summary reporting
   - Persists across scenes with DontDestroyOnLoad

2. **Phase2TestAssertions.cs** - Assertion helper library
   - Static utility class with assertion methods
   - Tracks pass/fail/warn counts globally
   - Provides GetTestSummary() for final results
   - Methods: AssertTrue, AssertFalse, AssertNull, AssertNotNull, AssertEqual, AssertNotEqual, LogWarning

3. **Phase2OriginalGameTests.cs** - Original game tests (Phase 2 disabled)
   - Tests 5+ behaviors with Phase 2 integration disabled:
     - Phase 2Enabler is disabled
     - Player movement controller exists
     - ActionTracker doesn't crash
     - No null reference exceptions
     - Phase 2 notifications are blocked
   - Returns IEnumerator for async testing

4. **Phase2IntegrationTests.cs** - Phase 2 integration tests (Phase 2 enabled + toggle)
   - Tests 11+ behaviors with Phase 2 enabled:
     - Phase 2Enabler is enabled
     - All managers initialize successfully
     - DifficultyConfig, TrialWeekManager, ActionTracker accessible
     - ProximityPenaltySystem, DungeonInstanceSystem work
     - Runtime disable/enable functionality
     - Original systems still work after disable
     - Phase 2 systems work again after re-enable
   - Returns IEnumerator for async testing

### Optional UI & Bootstrap Files

5. **Phase2TestResultsUI.cs** - Visual test results display
   - Displays test results on-screen during/after tests
   - Shows real-time pass/fail/warn counts
   - Color-coded results (green=pass, red=fail, yellow=warn)
   - Toggle display with Spacebar
   - Uses OnGUI() for simple overlay

6. **Phase2TestBootstrap.cs** - Test scene setup helper
   - Attach to "Bootstrap" GameObject in test scene
   - Automatically creates Phase2TestRunner and UI
   - Allows running tests without full game scene
   - Auto-starts tests on Awake()

## Quick Start

### Setup Option 1: Using Bootstrap (Recommended)

1. Create a new scene or use existing test scene
2. Create an empty GameObject named "Bootstrap"
3. Attach `Phase2TestBootstrap.cs` to it
4. Play the scene - tests run automatically
5. Press Spacebar to toggle test results UI
6. Check Console for detailed logs

### Setup Option 2: Manual Setup

1. Add `Phase2TestRunner.cs` to any GameObject that persists
2. (Optional) Add `Phase2TestResultsUI.cs` to a GameObject for on-screen display
3. Play the scene - tests run automatically on Start()
4. Check Console for detailed logs

## Test Execution Flow

```
Start() 
  ↓
Disable Phase 2 (Phase2Enabler.Disable())
  ↓
Wait 1 frame
  ↓
Run Phase2OriginalGameTests
  ├─ Test: Phase 2 Disabled Check
  ├─ Test: Player Movement Controller Exists
  ├─ Test: ActionTracker No Crash
  ├─ Test: No Null Reference Exceptions
  └─ Test: Phase 2 Notifications Blocked
  ↓
Log results
  ↓
Enable Phase 2 (Phase2Enabler.Enable())
  ↓
Wait 2 seconds for manager initialization
  ↓
Run Phase2IntegrationTests
  ├─ Test: Phase 2 Enabled Check
  ├─ Test: All Managers Initialize
  ├─ Test: DifficultyConfig Works
  ├─ Test: TrialWeekManager Works
  ├─ Test: ActionTracker Works
  ├─ Test: ProximityPenaltySystem Works
  ├─ Test: DungeonInstanceSystem Works
  ├─ Test: Disable Phase 2 at Runtime
  ├─ Test: Original Systems Work After Disable
  ├─ Test: Re-enable Phase 2 at Runtime
  └─ Test: Phase 2 Works After Re-enable
  ↓
Log results
  ↓
Print final summary
```

## Logging Format

Each test logs like:
```
[TEST] Test Name
  ✓ PASS: Condition met (detail)
  ✗ FAIL: Condition failed (detail)
  ⚠ WARN: Non-blocking issue (detail)
```

Final summary:
```
=== PHASE 2 INTEGRATION TEST SUMMARY ===
Total Tests: 16
Passed: 16
Failed: 0
Warnings: 0
Status: ALL TESTS PASSED ✓
```

## Console Output Example

```
========== PHASE 2 TEST SUITE STARTING ==========

[TEST_RUNNER] PHASE 1: Testing original game with Phase 2 disabled...
[Phase2Enabler] Phase 2 integration DISABLED
[TEST] Phase 2 Disabled Check
  ✓ PASS: Phase2Enabler.IsActive should be false
[TEST] Player Movement Controller Exists
  ✓ PASS: IsometricPlayerMovementController should exist or be findable
...
[TEST_RUNNER] PHASE 2: Testing Phase 2 integration with Phase 2 enabled...
[Phase2Enabler] Phase 2 integration ENABLED
[TEST_RUNNER] ✓ DifficultyConfig initialized
[TEST_RUNNER] ✓ TrialWeekManager initialized
...
=== PHASE 2 INTEGRATION TEST SUMMARY ===
Total Tests: 16
Passed: 16
Failed: 0
Warnings: 0
Status: ALL TESTS PASSED ✓

========== PHASE 2 TEST SUITE COMPLETE ==========
```

## Test Results UI (On-Screen Display)

When Phase2TestResultsUI is active, you'll see two panels:

**Left Panel:**
- Total Tests count
- Passed count (green)
- Failed count (red)
- Warnings count (yellow)
- Status indicator (✓ or ✗)

**Right Panel:**
- Phase 2 status (ENABLED/DISABLED)

**Keyboard Controls:**
- Press Spacebar to toggle UI visibility

## Adding Custom Tests

To add more tests, create a new test method in either test file:

```csharp
private static void TestYourFeature()
{
    string testName = "Your Feature Test";
    
    // Your test logic here
    bool condition = CheckSomething();
    
    Phase2TestAssertions.AssertTrue(condition, testName, "Your condition should be true");
}
```

Then call it from the appropriate `RunTests()` coroutine:

```csharp
yield return new WaitForSeconds(0.1f);
TestYourFeature();
```

## Assertion Methods

### AssertTrue(bool condition, string testName, string message)
Passes if condition is true.

### AssertFalse(bool condition, string testName, string message)
Passes if condition is false.

### AssertNull(object obj, string testName, string message)
Passes if object is null.

### AssertNotNull(object obj, string testName, string message)
Passes if object is not null.

### AssertEqual(object expected, object actual, string testName, string message)
Passes if expected equals actual.

### AssertNotEqual(object expected, object actual, string testName, string message)
Passes if expected does not equal actual.

### LogWarning(string testName, string message)
Logs a non-blocking warning without failing the test.

## Test State Management

### GetCounts()
Returns tuple of (passed, failed, warnings) counts.

### GetTestSummary()
Returns formatted string with final summary.

### ResetCounters()
Resets pass/fail/warn counts (called automatically before each test phase).

## Troubleshooting

### Tests Not Running
- Ensure Phase2TestRunner.cs is attached to a GameObject in the scene
- Check that Awake() and Start() can run (not disabled or missing)
- Look for errors in Console

### Tests Always Fail
- Check that Phase 2 managers are in the scene or can auto-initialize
- Verify ActionTracker, DifficultyConfig, etc. have their Awake/Instance patterns
- Enable AutoInitialize on managers if available

### UI Not Showing
- Ensure Phase2TestResultsUI.cs is attached to a GameObject
- Press Spacebar to toggle visibility (should default to on)
- Check Canvas scale if using GUI

### Managers Not Found
- Ensure Phase2ManagerInitializer is in the scene
- Or let tests create dummy managers in Awake
- Check that DontDestroyOnLoad is set correctly

## Integration with CI/CD

To run tests in CI/CD without UI:

1. Remove Phase2TestResultsUI from bootstrap (or set showTestUI = false)
2. Run with headless mode: `Unity -batchmode -nographics -executeMethod ...`
3. Parse console output for "PHASE 2 INTEGRATION TEST SUMMARY"
4. Exit with success/failure based on test counts

## Performance Notes

- Full test suite completes in ~5-10 seconds
- Tests are designed to not impact game state
- Phase2Enabler toggles don't require scene reload
- All singleton managers persist for efficiency

## Code Quality

All test files include:
- XML documentation on public methods
- Consistent EthraClone.TrialWeek namespace
- Safe null handling (managers may not exist)
- No external dependencies
- Error handling with try/catch where needed

## License

Part of the LIT-ISO project.
