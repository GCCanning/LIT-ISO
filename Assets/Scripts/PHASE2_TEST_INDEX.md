# Phase 2 Test Suite - Complete Index

## Overview
Complete automated test suite for Phase 2 integration testing in LIT-ISO. Validates Phase 2 functionality across original game compatibility, integration, and runtime toggle scenarios.

**Location:** `C:\Projects\Unity-Projects\LIT-ISO\Assets\Scripts\`
**Namespace:** `EthraClone.TrialWeek`
**Framework:** Unity IEnumerator (Coroutines)
**Runtime:** ~5-10 seconds
**Test Count:** 16 total assertions

---

## Core Files (Required)

### 1. Phase2TestRunner.cs
**Purpose:** Main orchestrator that runs all tests in sequence
**Type:** MonoBehaviour singleton
**Key Methods:**
- `Start()` - Triggers test suite
- `RunAllTests()` - IEnumerator orchestrating test flow
- `EnsureManagersInitialized()` - Verifies manager setup
- `PrintFinalSummary()` - Outputs summary results
- `TestsCompleted` - Property showing completion status

**Startup:** Automatic on Start()
**Persistence:** DontDestroyOnLoad
**Output:** Console logs + summary report

**Test Flow:**
1. Disable Phase 2 + Run Original Game Tests
2. Enable Phase 2 + Wait for init
3. Run Phase 2 Integration Tests
4. Print final summary

---

### 2. Phase2TestAssertions.cs
**Purpose:** Static assertion library and test tracking
**Type:** Static utility class
**Key Methods:**
```
AssertTrue(condition, testName, message)
AssertFalse(condition, testName, message)
AssertNull(obj, testName, message)
AssertNotNull(obj, testName, message)
AssertEqual(expected, actual, testName, message)
AssertNotEqual(expected, actual, testName, message)
LogWarning(testName, message)
GetTestSummary() → string
GetCounts() → (int passed, int failed, int warnings)
ResetCounters() → void
```

**Logging Format:**
```
[TEST] Test Name
  ✓ PASS: Message
  ✗ FAIL: Message
  ⚠ WARN: Message
```

**Output Colors:**
- Green: PASS
- Red: FAIL
- Yellow: WARN

**Global Tracking:**
- passCount, failCount, warnCount
- Updated per assertion
- Reset per test phase

---

### 3. Phase2OriginalGameTests.cs
**Purpose:** Validate original game works with Phase 2 disabled
**Type:** Static test class
**Key Method:** `RunTests(Action<string> callback) → IEnumerator`

**Tests (5):**
1. **Phase2Enabler.IsActive = false** - Verify disabled state
2. **Player Movement Exists** - IsometricPlayerMovementController findable
3. **ActionTracker Stability** - No crash on access
4. **No Null References** - Manager access safe
5. **Phase 2 Notifications Blocked** - Events don't fire

**Setup:** Phase 2 disabled before tests
**Assertions:** 5+ total
**Output:** Results logged per test

**Key Verifications:**
- Phase2Enabler.IsActive == false
- IsometricPlayerMovementController accessible
- ActionTracker.Instance not null
- DifficultyConfig.Instance accessible
- ProximityPenaltySystem.Instance accessible
- DungeonInstanceSystem.Instance accessible
- No NullReferenceException thrown

---

### 4. Phase2IntegrationTests.cs
**Purpose:** Validate Phase 2 works when enabled + runtime toggle
**Type:** Static test class
**Key Method:** `RunTests(Action<string> callback) → IEnumerator`

**Tests (11):**
1. **Phase2Enabler.IsActive = true** - Verify enabled state
2. **All Managers Initialize** - Core managers accessible
3. **DifficultyConfig Works** - Returns valid multiplier
4. **TrialWeekManager Works** - Events subscribable
5. **ActionTracker Works** - Can initialize player
6. **ProximityPenaltySystem Works** - Returns valid multiplier
7. **DungeonInstanceSystem Works** - Accessible
8. **Disable Phase 2 Runtime** - IsActive = false
9. **Systems Work After Disable** - Original systems still functional
10. **Re-enable Phase 2 Runtime** - IsActive = true
11. **Phase 2 Works After Re-enable** - Managers still functional

**Setup:** Phase 2 enabled, waits for init
**Assertions:** 11+ total
**Output:** Results logged per test

**Key Verifications:**
- Phase2Enabler.IsActive == true
- All singleton managers exist
- GetProximityMultiplier() returns > 0
- GetActionWeightMultiplier() returns > 0
- Event subscriptions work
- Runtime disable/enable functions
- Original systems persist through toggles
- No dangling subscriptions

---

## Optional UI & Bootstrap Files

### 5. Phase2TestResultsUI.cs
**Purpose:** On-screen visual display of test results
**Type:** MonoBehaviour
**Key Methods:**
- `OnGUI()` - Renders test results overlay
- `InitializeStyles()` - Sets up GUIStyles
- `Update()` - Handles Spacebar toggle

**Display Panels:**
1. **Results Summary (Left)**
   - Total Tests
   - Passed (green)
   - Failed (red)
   - Warnings (yellow)
   - Status indicator

2. **Phase 2 Status (Right)**
   - Current Phase 2 state (ENABLED/DISABLED)

**Controls:**
- Press Spacebar to toggle visibility
- Colors: Green=pass, Red=fail, Yellow=warn

**Styling:**
- Dark background (0.1, 0.1, 0.1, 0.8)
- Cyan title text
- White labels
- Colored status indicators
- 12-14pt font sizes

---

### 6. Phase2TestBootstrap.cs
**Purpose:** Minimal test environment setup
**Type:** MonoBehaviour
**Key Method:**
- `Awake()` - Creates test infrastructure

**Setup:**
- Auto-creates Phase2TestRunner if missing
- Auto-creates Phase2TestResultsUI if showTestUI=true
- No scene prefabs required

**Options:**
- `autoStartTests` (default: true) - Tests start automatically
- `showTestUI` (default: true) - Display results UI

**Usage:**
1. Create empty GameObject "Bootstrap"
2. Attach Phase2TestBootstrap.cs
3. Play - tests run automatically

---

## Documentation Files

### 7. PHASE2_TESTS_README.md
**Complete usage guide with:**
- Overview of test framework
- Quick start instructions (2 options)
- Test execution flow diagram
- Logging format examples
- Assertion method reference
- Custom test examples
- Test state management
- Troubleshooting guide
- CI/CD integration notes
- Performance notes
- Code quality info

### 8. TEST_SUITE_SUMMARY.txt
**High-level overview containing:**
- File metrics and line counts
- Test execution flow
- Key features list
- Usage instructions
- Logging format specification
- Assertion methods reference
- Managers tested list
- Code quality metrics
- Integration checklist
- Next steps guide

### 9. SETUP_TEST_SCENE.md
**Step-by-step setup guide with:**
- 5-minute quick start
- Scene creation instructions
- Bootstrap configuration
- Expected results examples
- Alternative setup methods
- Headless/CI setup
- Troubleshooting per scenario
- Performance info
- File locations
- Success criteria

### 10. PHASE2_TEST_INDEX.md
**This file - Complete reference with:**
- File descriptions
- Method documentation
- Test breakdowns
- Usage examples
- Quick lookup

---

## Quick Reference

### Test Execution Times
| Phase | Duration | Tests |
|-------|----------|-------|
| Phase 1 (Original Game) | ~2-3s | 5 tests |
| Phase 2 (Integration) | ~3-5s | 11 tests |
| Setup/Cleanup | ~2-3s | - |
| **Total** | **~7-10s** | **16 tests** |

### Managers Tested
- Phase2Enabler (IsActive, Enable, Disable)
- DifficultyConfig (GetActionWeightMultiplier)
- TrialWeekManager (OnWorldTimeOfDayChanged)
- ActionTracker (InitializePlayer, GetActionCount)
- ProximityPenaltySystem (GetProximityMultiplier)
- DungeonInstanceSystem (initialization)
- SaveGameManager (initialization)
- IsometricPlayerMovementController (existence, speed)
- Phase2IntegrationBridge (event firing)

### Key Test Scenarios
1. **Backward Compatibility** - Original game works without Phase 2
2. **Phase 2 Functionality** - Phase 2 systems work when enabled
3. **Runtime Toggle** - Can disable/enable Phase 2 mid-game
4. **Stability** - No crashes or null references
5. **Event Subscription** - Events properly connected
6. **Manager Initialization** - All systems initialize correctly

---

## Setup Instructions

### Quick Setup (5 minutes)
```
1. Create scene
2. Create empty GameObject "Bootstrap"
3. Attach Phase2TestBootstrap.cs
4. Play
5. Watch Console for results
6. (Optional) Press Spacebar for UI toggle
```

### Manual Setup
```
1. Create scene
2. Create empty GameObject
3. Attach Phase2TestRunner.cs
4. (Optional) Create another object, attach Phase2TestResultsUI.cs
5. Play
```

### CI/CD Setup
```
1. Create scene with Bootstrap
2. Set showTestUI = false in Bootstrap
3. Run with -batchmode -nographics flags
4. Parse console for "PHASE 2 INTEGRATION TEST SUMMARY"
5. Check for "ALL TESTS PASSED ✓"
```

---

## Usage Examples

### Running Tests
```csharp
// Tests run automatically on scene start
// No manual triggering needed
// Results appear in Console immediately
```

### Adding Custom Tests
```csharp
// In Phase2OriginalGameTests.cs or Phase2IntegrationTests.cs:

private static void TestMyFeature()
{
    string testName = "My Feature Test";
    bool result = CheckMyFeature();
    Phase2TestAssertions.AssertTrue(
        result, 
        testName, 
        "My feature should work"
    );
}

// Then call from RunTests():
yield return new WaitForSeconds(0.1f);
TestMyFeature();
```

### Checking Results
```csharp
// In your code:
(int passed, int failed, int warnings) = Phase2TestAssertions.GetCounts();

if (failed == 0)
{
    Debug.Log("All tests passed!");
}
else
{
    Debug.LogError($"Failed: {failed} tests");
}
```

### UI Toggle
```
Press Spacebar during gameplay to toggle results display
Visibility persists through test phases
Shows: Total, Passed, Failed, Warnings, Status
```

---

## Assertion Reference

| Method | Passes When | Example |
|--------|------------|---------|
| AssertTrue | condition == true | AssertTrue(myBool, "test", "message") |
| AssertFalse | condition == false | AssertFalse(!myBool, "test", "message") |
| AssertNull | obj == null | AssertNull(nullObj, "test", "message") |
| AssertNotNull | obj != null | AssertNotNull(manager, "test", "message") |
| AssertEqual | expected == actual | AssertEqual(5, count, "test", "message") |
| AssertNotEqual | expected != actual | AssertNotEqual(0, count, "test", "message") |
| LogWarning | always (non-blocking) | LogWarning("test", "message") |

---

## Troubleshooting

### Issue: Tests Don't Run
**Solutions:**
- Verify Bootstrap GameObject in scene
- Check Phase2TestRunner added as component
- Look for errors in Console
- Ensure Play starts correct scene

### Issue: Only Some Tests Run
**Solutions:**
- Check console for specific failures
- Verify managers are initialized
- Look for Exception messages
- Some managers optional (show warnings)

### Issue: UI Doesn't Show
**Solutions:**
- Press Spacebar (toggles visibility)
- Verify Phase2TestResultsUI in scene
- Check showTestUI = true in Bootstrap
- Look for OnGUI errors

### Issue: Tests Fail
**Solutions:**
- Check manager initialization order
- Verify Phase2Enabler state
- Look for NullReferenceException
- Review test output carefully

---

## File Structure

```
Assets/Scripts/
├── Phase2TestRunner.cs             (Main orchestrator)
├── Phase2TestAssertions.cs         (Assertion library)
├── Phase2OriginalGameTests.cs      (Original game tests)
├── Phase2IntegrationTests.cs       (Integration tests)
├── Phase2TestResultsUI.cs          (Optional UI)
├── Phase2TestBootstrap.cs          (Optional bootstrap)
├── PHASE2_TESTS_README.md          (Complete guide)
├── TEST_SUITE_SUMMARY.txt          (Summary)
├── SETUP_TEST_SCENE.md             (Setup guide)
└── PHASE2_TEST_INDEX.md            (This file)
```

---

## Success Criteria

Tests are working correctly when:
- ✓ All 16 tests complete without errors
- ✓ Console shows "ALL TESTS PASSED ✓"
- ✓ On-screen shows green checkmark
- ✓ Failed count = 0
- ✓ No exceptions in output
- ✓ Runtime ~5-10 seconds

---

## Next Steps

1. **Run tests** - Execute test scene to verify setup
2. **Review output** - Check console for details
3. **Use as baseline** - Run before any Phase 2 changes
4. **Add custom tests** - Extend as needed
5. **CI/CD integration** - Automate for releases

---

## Support

For issues or questions:
1. Check relevant documentation file above
2. Review console output for specific errors
3. Verify all Phase 2 managers exist
4. Check Phase2Enabler state
5. Look for NullReferenceException messages

---

## License & Attribution

Part of the LIT-ISO project (EthraClone.TrialWeek namespace)
Created for Phase 2 integration testing

---

**Last Updated:** 2026-05-19
**Test Suite Version:** 1.0
**Status:** Production Ready
