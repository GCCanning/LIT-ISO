using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript
{
    public static void BuildWindows()
    {
        bool ok = GameBuilder.RunBuildPipeline(out BuildReport report);
        if (!ok)
        {
            string reason = GameBuilder.LastFailureReason ?? "Unknown build failure.";
            Debug.LogError($"[BuildScript] ❌ BUILD FAILED — {reason}");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("[BuildScript] ✅ BUILD SUCCEEDED");
        EditorApplication.Exit(0);
    }
}
