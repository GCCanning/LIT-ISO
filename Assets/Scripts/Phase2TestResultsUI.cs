using UnityEngine;

namespace EthraClone.TrialWeek
{
    /// <summary>
    /// Phase 2 Test Results UI: Displays test results on screen during/after tests.
    /// Shows real-time pass/fail counts and current test status.
    /// Uses OnGUI() for simple overlay display.
    /// Toggle visibility with Spacebar.
    /// </summary>
    public class Phase2TestResultsUI : MonoBehaviour
    {
        private bool showUI = true;
        private string lastResultSummary = "";
        private float updateInterval = 0.5f;
        private float nextUpdateTime = 0f;

        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private GUIStyle titleStyle;
        private GUIStyle passStyle;
        private GUIStyle failStyle;
        private GUIStyle warnStyle;

        private void Start()
        {
            InitializeStyles();
        }

        private void Update()
        {
            // Toggle UI with Spacebar
            if (Input.GetKeyDown(KeyCode.Space))
            {
                showUI = !showUI;
                Debug.Log($"[TEST_UI] Test results UI {(showUI ? "ENABLED" : "DISABLED")}");
            }

            // Update summary periodically
            if (Time.time >= nextUpdateTime)
            {
                lastResultSummary = Phase2TestAssertions.GetTestSummary();
                nextUpdateTime = Time.time + updateInterval;
            }
        }

        private void OnGUI()
        {
            if (!showUI)
                return;

            // Display test results UI panel
            GUILayout.BeginArea(new Rect(10, 10, 500, 400));
            {
                GUILayout.BeginVertical(boxStyle);
                {
                    GUILayout.Label("PHASE 2 TEST RESULTS", titleStyle);
                    GUILayout.Space(10);

                    // Display test counts
                    (int passed, int failed, int warnings) = Phase2TestAssertions.GetCounts();
                    int total = passed + failed + warnings;

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Total Tests:", labelStyle);
                        GUILayout.Label(total.ToString(), labelStyle);
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Passed:", passStyle);
                        GUILayout.Label(passed.ToString(), passStyle);
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Failed:", failStyle);
                        GUILayout.Label(failed.ToString(), failStyle);
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Warnings:", warnStyle);
                        GUILayout.Label(warnings.ToString(), warnStyle);
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.Space(10);

                    // Status indicator
                    string statusText = failed == 0 ? "✓ ALL TESTS PASSED" : "✗ FAILURES DETECTED";
                    GUIStyle statusStyle = failed == 0 ? passStyle : failStyle;
                    GUILayout.Label(statusText, statusStyle);

                    GUILayout.Space(10);
                    GUILayout.Label("(Press Spacebar to toggle display)", labelStyle);
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndArea();

            // Display Phase 2 status
            GUILayout.BeginArea(new Rect(520, 10, 300, 100));
            {
                GUILayout.BeginVertical(boxStyle);
                {
                    GUILayout.Label("PHASE 2 STATUS", titleStyle);
                    bool isActive = Phase2Enabler.IsActive;
                    string statusLabel = isActive ? "✓ ENABLED" : "✗ DISABLED";
                    GUIStyle statusStyle = isActive ? passStyle : failStyle;
                    GUILayout.Label($"Phase 2: {statusLabel}", statusStyle);
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndArea();
        }

        private void InitializeStyles()
        {
            // Box style with dark background
            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.normal.background = MakeTex(1, 1, new Color(0.1f, 0.1f, 0.1f, 0.8f));
            boxStyle.padding = new RectOffset(10, 10, 10, 10);

            // Label style with white text
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 12;
            labelStyle.normal.textColor = Color.white;

            // Title style
            titleStyle = new GUIStyle(labelStyle);
            titleStyle.fontSize = 14;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = Color.cyan;

            // Pass style - green
            passStyle = new GUIStyle(labelStyle);
            passStyle.normal.textColor = Color.green;
            passStyle.fontStyle = FontStyle.Bold;

            // Fail style - red
            failStyle = new GUIStyle(labelStyle);
            failStyle.normal.textColor = Color.red;
            failStyle.fontStyle = FontStyle.Bold;

            // Warning style - yellow
            warnStyle = new GUIStyle(labelStyle);
            warnStyle.normal.textColor = Color.yellow;
            warnStyle.fontStyle = FontStyle.Bold;
        }

        /// <summary>
        /// Helper to create a solid color texture for backgrounds.
        /// </summary>
        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];

            for (int i = 0; i < pix.Length; ++i)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();

            return result;
        }
    }
}
