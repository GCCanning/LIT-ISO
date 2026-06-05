# Phase 2 Test Suite - Setup Instructions

## Quick Setup (5 minutes)

### Step 1: Create Test Scene
1. In Unity Editor, go to File > New Scene
2. Save scene as `TestPhase2Integration` in Assets/Scenes/
3. This scene can be completely empty (no prefabs needed)

### Step 2: Create Bootstrap GameObject
1. In scene hierarchy, right-click and select Create Empty
2. Rename to "Bootstrap"
3. Reset transform (0, 0, 0)

### Step 3: Add Phase2TestBootstrap Script
1. Select "Bootstrap" GameObject
2. In Inspector, click Add Component
3. Search for "Phase2TestBootstrap"
4. Click to add

### Step 4: Configure Bootstrap (Optional)
In Inspector, you'll see:
- `autoStartTests` (default: true) - Tests start automatically
- `showTestUI` (default: true) - On-screen results display enabled

Leave defaults as-is for typical use.

### Step 5: Run Tests
1. Click Play button
2. Tests run automatically
3. Watch Console for detailed output
4. (Optional) Press Spacebar to toggle on-screen results UI

## Expected Results

### Console Output (First 10 seconds):
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
...

=== PHASE 2 INTEGRATION TEST SUMMARY ===
Total Tests: 16
Passed: 16
Failed: 0
Warnings: 0
Status: ALL TESTS PASSED ✓

========== PHASE 2 TEST SUITE COMPLETE ==========
All tests completed. Check console for results.
```

### On-Screen Display (If showTestUI = true):
- Top-left corner: Test Results (pass/fail/warn counts)
- Top-right corner: Phase 2 Status (ENABLED/DISABLED)
- Press Spacebar to toggle visibility

## Alternative Setup (Manual, No Bootstrap)

If you don't want to use Bootstrap:

1. Create test scene (as above)
2. Create an empty GameObject named "TestRunner"
3. Add Phase2TestRunner.cs as component
4. (Optional) Create another empty GameObject "TestUI"
5. Add Phase2TestResultsUI.cs as component
6. Play scene

Tests will start automatically on Start().

## Headless/CI Setup

For automated testing without UI:

1. Create scene (as above)
2. Add Phase2TestBootstrap with:
   - `autoStartTests = true`
   - `showTestUI = false`
3. Run with: `Unity -batchmode -nographics -projectPath ... -executeMethod UnityEditor.SceneHierarchyHooks.OpenScene TestPhase2Integration`
4. Parse console for "PHASE 2 INTEGRATION TEST SUMMARY"
5. Check for "ALL TESTS PASSED" line

## Troubleshooting

### Tests Don't Run
- Verify Bootstrap GameObject is in scene
- Check that Play starts the scene with Bootstrap
- Look for errors in Console tab
- Ensure Phase2TestBootstrap.cs is correctly added to GameObject

### Only Some Tests Run
- Check console for specific test failures
- Verify Phase 2 managers exist in your project
- Look for "Exception" messages in console
- Some managers may be optional and will show as warnings

### UI Doesn't Show
- Press Spacebar (toggles visibility)
- Check that Phase2TestResultsUI is added to a GameObject
- Verify `showTestUI = true` in Bootstrap
- Look for OnGUI errors in Console

### Tests Fail Unexpectedly
- Check if Phase 2 managers are properly initialized
- Verify ActionTracker, DifficultyConfig exist
- Look for NullReferenceException messages
- Review manager initialization order

## Customizing Tests

To run tests without Bootstrap:

1. Attach Phase2TestRunner to any GameObject
2. Attach Phase2TestResultsUI to any GameObject (optional)
3. Play - tests start automatically

To add custom tests:

1. Edit Phase2OriginalGameTests.cs or Phase2IntegrationTests.cs
2. Add test method: `private static void TestYourFeature()`
3. Call Phase2TestAssertions methods to validate
4. Call test from RunTests() coroutine

Example:
```csharp
private static void TestMyFeature()
{
    string testName = "My Feature Test";
    bool result = CheckMyFeature();
    Phase2TestAssertions.AssertTrue(result, testName, "My feature should work");
}
```

## Scene Persistence

The test suite uses DontDestroyOnLoad, so:
- Tests persist when changing scenes
- Test results remain in console
- Results UI stays visible
- Can test scene transitions with Phase 2

To reset tests after running:
1. Stop Play mode
2. Play again - new test run starts

## Performance

- Full test suite: ~5-10 seconds
- No frame rate impact during tests
- Phase 2 toggling is instant
- No scene reloads needed

## Next Steps

After confirming tests pass:

1. Save test scene for recurring use
2. Add to version control if desired
3. Run before Phase 2 changes to verify compatibility
4. Add custom tests as needed
5. Integrate with CI/CD pipeline

## Documentation

For detailed information, see:
- PHASE2_TESTS_README.md - Full reference
- TEST_SUITE_SUMMARY.txt - Overview
- Individual .cs files - Source code comments

## Keyboard Controls

During test execution:
- **Spacebar** - Toggle on-screen UI visibility
- **ESC** - Stop Play (tests stop running)

## File Locations

All test files are in:
```
C:\Projects\Unity-Projects\LIT-ISO\Assets\Scripts\

Phase2TestRunner.cs
Phase2TestAssertions.cs
Phase2OriginalGameTests.cs
Phase2IntegrationTests.cs
Phase2TestResultsUI.cs
Phase2TestBootstrap.cs
```

Documentation files:
```
PHASE2_TESTS_README.md
TEST_SUITE_SUMMARY.txt
SETUP_TEST_SCENE.md (this file)
```

## Success Criteria

Tests are working correctly when:
- ✓ All 16 tests run without errors
- ✓ Console shows "ALL TESTS PASSED ✓"
- ✓ On-screen UI shows green checkmark
- ✓ Failed count = 0
- ✓ No exceptions in Console

You're ready to use Phase 2 for development!
