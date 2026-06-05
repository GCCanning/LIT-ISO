using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// One-click build pipeline for LIT-ISO.
///
/// Menu items:
///   - Tools > LIT-ISO > Build > Build Standalone (Windows) - builds to ./Builds/LIT-ISO/
///   - Tools > LIT-ISO > Build > Build And Run                - builds then launches the .exe
///   - Tools > LIT-ISO > Build > Open Builds Folder           - opens the output directory
///
/// The build pipeline:
///   1. Validates the current scene has all required components
///   2. Saves the open scene
///   3. Configures Player Settings (product name, resolution, etc.)
///   4. Configures Quality Settings (anti-aliasing, vsync)
///   5. Ensures the scene is in EditorBuildSettings.scenes
///   6. Builds for Windows 64-bit Standalone
///   7. Reports build size + path + any errors
///   8. Optionally runs the .exe
/// </summary>
public static class GameBuilder
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    private const string ProductName = "LIT-ISO";
    private const string CompanyName = "LIT Games";
    private const string Version = "0.1.0";
    private const string BuildRootFolder = "Builds";
    private const string ScenePath = "Assets/Scenes/InfinitePlainsPrototype.unity";

    private static string BuildOutputFolder => Path.Combine(BuildRootFolder, ProductName);
    private static string BuildExePath => Path.Combine(BuildOutputFolder, ProductName + ".exe");

    // -------------------------------------------------------------------------
    // Menu items
    // -------------------------------------------------------------------------

    [MenuItem("Tools/LIT-ISO/Build/Build Standalone (Windows)", false, 400)]
    public static void BuildStandaloneWindows()
    {
        BuildGame(runAfterBuild: false);
    }

    [MenuItem("Tools/LIT-ISO/Build/Build And Run", false, 401)]
    public static void BuildAndRun()
    {
        BuildGame(runAfterBuild: true);
    }

    [MenuItem("Tools/LIT-ISO/Build/Open Builds Folder", false, 402)]
    public static void OpenBuildsFolder()
    {
        string folder = Path.GetFullPath(BuildOutputFolder);
        if (!Directory.Exists(folder))
        {
            EditorUtility.DisplayDialog(
                "Builds Folder",
                $"No build exists yet at:\n{folder}\n\nUse 'Build Standalone' first.",
                "OK");
            return;
        }
        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }

    [MenuItem("Tools/LIT-ISO/Build/Clean Builds Folder", false, 403)]
    public static void CleanBuildsFolder()
    {
        string folder = Path.GetFullPath(BuildOutputFolder);
        if (!Directory.Exists(folder))
        {
            Debug.Log("[GameBuilder] Builds folder is already empty.");
            return;
        }

        if (!EditorUtility.DisplayDialog(
            "Clean Builds Folder",
            $"Delete entire build folder?\n\n{folder}",
            "Delete",
            "Cancel"))
        {
            return;
        }

        try
        {
            Directory.Delete(folder, recursive: true);
            Debug.Log("[GameBuilder] Cleaned builds folder.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[GameBuilder] Could not delete build folder: {ex.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // Build pipeline
    // -------------------------------------------------------------------------

    private static void BuildGame(bool runAfterBuild)
    {
        Debug.Log("═══════════════════════════════════════════════════════════");
        Debug.Log("[GameBuilder] Starting build pipeline...");
        Debug.Log("═══════════════════════════════════════════════════════════");

        // Step 1: Configure Player Settings
        ConfigurePlayerSettings();

        // Step 2: Configure Quality Settings
        ConfigureQualitySettings();

        // Step 3: Ensure scene exists and is in Build Settings
        if (!EnsureSceneReady())
        {
            EditorUtility.DisplayDialog(
                "Build Failed",
                $"Scene not found at:\n{ScenePath}\n\n" +
                "Run 'Tools > LIT-ISO > Playtest > Rebuild Full Playtest Scene' first.",
                "OK");
            return;
        }

        // Step 4: Save the current scene if it's the build scene
        SaveOpenSceneIfBuildScene();

        // Step 5: Run the Unity build
        BuildReport report = ExecuteBuild();

        // Step 6: Report result
        if (report.summary.result == BuildResult.Succeeded)
        {
            ReportBuildSuccess(report);
            if (runAfterBuild)
            {
                RunBuiltExe();
            }
        }
        else
        {
            ReportBuildFailure(report);
        }
    }

    // -------------------------------------------------------------------------
    // Player Settings (defines what your .exe looks like)
    // -------------------------------------------------------------------------

    private static void ConfigurePlayerSettings()
    {
        Debug.Log("[GameBuilder] Configuring Player Settings...");

        PlayerSettings.productName = ProductName;
        PlayerSettings.companyName = CompanyName;
        PlayerSettings.bundleVersion = Version;

        // Default display
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.runInBackground = true;
        // Note: captureSingleScreen and resizableWindowEnabled were removed in newer Unity
        // versions. Resizable window is now the default for Standalone builds.

        // .NET API level for newer C# features
        PlayerSettings.SetApiCompatibilityLevel(
            UnityEditor.Build.NamedBuildTarget.Standalone,
            ApiCompatibilityLevel.NET_Standard);

        // Scripting backend: IL2CPP is more performant but Mono is faster to build
        // Keep Mono for now — quicker iteration on builds.
        PlayerSettings.SetScriptingBackend(
            UnityEditor.Build.NamedBuildTarget.Standalone,
            ScriptingImplementation.Mono2x);

        // Color space — Linear for better lighting
        PlayerSettings.colorSpace = ColorSpace.Linear;

        // Splash screen — disable for faster startup
#if UNITY_2022_3_OR_NEWER
        PlayerSettings.SplashScreen.showUnityLogo = false;
#endif

        Debug.Log($"   ✅ Product: {ProductName} v{Version}");
        Debug.Log($"   ✅ Company: {CompanyName}");
        Debug.Log($"   ✅ Default resolution: 1920x1080 (Fullscreen Window)");
    }

    // -------------------------------------------------------------------------
    // Quality Settings (renders + AA)
    // -------------------------------------------------------------------------

    private static void ConfigureQualitySettings()
    {
        Debug.Log("[GameBuilder] Configuring Quality Settings...");

        QualitySettings.vSyncCount = 1;             // Sync to monitor refresh
        // MSAA and anisotropic filtering both create seams/shimmer on pixel-art
        // tilemaps. For a 2D isometric game they must stay OFF for crisp tiles.
        QualitySettings.antiAliasing = 0;           // No MSAA — prevents tile-edge seams
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        QualitySettings.realtimeReflectionProbes = false;

        Debug.Log("   ✅ VSync enabled, MSAA OFF (crisp pixel tiles), Anisotropic OFF");
    }

    // -------------------------------------------------------------------------
    // Scene management
    // -------------------------------------------------------------------------

    private static bool EnsureSceneReady()
    {
        Debug.Log("[GameBuilder] Ensuring scene is in Build Settings...");

        if (!File.Exists(ScenePath))
        {
            Debug.LogError($"[GameBuilder] Scene not found at {ScenePath}");
            return false;
        }

        // Add scene to Build Settings if not already present
        List<EditorBuildSettingsScene> sceneList = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool sceneFound = false;
        foreach (EditorBuildSettingsScene s in sceneList)
        {
            if (s.path == ScenePath)
            {
                sceneFound = true;
                s.enabled = true;
                break;
            }
        }

        if (!sceneFound)
        {
            sceneList.Insert(0, new EditorBuildSettingsScene(ScenePath, enabled: true));
            EditorBuildSettings.scenes = sceneList.ToArray();
            Debug.Log($"   ✅ Added scene to Build Settings: {ScenePath}");
        }
        else
        {
            EditorBuildSettings.scenes = sceneList.ToArray();
            Debug.Log($"   ✅ Scene already in Build Settings: {ScenePath}");
        }

        return true;
    }

    private static void SaveOpenSceneIfBuildScene()
    {
        UnityEngine.SceneManagement.Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path == ScenePath && activeScene.isDirty)
        {
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("[GameBuilder] Saved open scene.");
        }
    }

    // -------------------------------------------------------------------------
    // Run the build
    // -------------------------------------------------------------------------

    private static BuildReport ExecuteBuild()
    {
        Debug.Log("[GameBuilder] Running build... (this may take a few minutes)");

        // Ensure output folder exists
        string fullOutput = Path.GetFullPath(BuildOutputFolder);
        if (!Directory.Exists(fullOutput))
        {
            Directory.CreateDirectory(fullOutput);
        }

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = BuildExePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        // Switch to standalone build target if not already there
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
        {
            Debug.Log("[GameBuilder] Switching active build target to StandaloneWindows64...");
            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Standalone,
                BuildTarget.StandaloneWindows64);
        }

        return BuildPipeline.BuildPlayer(options);
    }

    // -------------------------------------------------------------------------
    // Reporting
    // -------------------------------------------------------------------------

    private static void ReportBuildSuccess(BuildReport report)
    {
        ulong sizeBytes = report.summary.totalSize;
        double sizeMB = sizeBytes / (1024.0 * 1024.0);
        double timeSec = report.summary.totalTime.TotalSeconds;

        Debug.Log("═══════════════════════════════════════════════════════════");
        Debug.Log("[GameBuilder] ✅ BUILD SUCCEEDED");
        Debug.Log("═══════════════════════════════════════════════════════════");
        Debug.Log($"   📦 Output: {Path.GetFullPath(BuildExePath)}");
        Debug.Log($"   📏 Size:   {sizeMB:F1} MB");
        Debug.Log($"   ⏱  Time:   {timeSec:F1}s");
        Debug.Log($"   ⚠  Warnings: {report.summary.totalWarnings}");
        Debug.Log("═══════════════════════════════════════════════════════════");
        Debug.Log("To run: double-click " + Path.GetFileName(BuildExePath));
        Debug.Log("Or use: Tools > LIT-ISO > Build > Build And Run");

        bool runNow = EditorUtility.DisplayDialog(
            "Build Succeeded",
            $"Your game is built!\n\n" +
            $"Location: {Path.GetFullPath(BuildExePath)}\n" +
            $"Size:     {sizeMB:F1} MB\n" +
            $"Time:     {timeSec:F1}s\n\n" +
            $"Open the build folder?",
            "Open Folder",
            "Close");

        if (runNow)
        {
            OpenBuildsFolder();
        }
    }

    private static void ReportBuildFailure(BuildReport report)
    {
        Debug.LogError("═══════════════════════════════════════════════════════════");
        Debug.LogError("[GameBuilder] ❌ BUILD FAILED");
        Debug.LogError("═══════════════════════════════════════════════════════════");
        Debug.LogError($"   Result: {report.summary.result}");
        Debug.LogError($"   Errors: {report.summary.totalErrors}");
        Debug.LogError($"   Time:   {report.summary.totalTime.TotalSeconds:F1}s");

        EditorUtility.DisplayDialog(
            "Build Failed",
            $"Build failed with {report.summary.totalErrors} error(s).\n\n" +
            "Check the Console for details.",
            "OK");
    }

    // -------------------------------------------------------------------------
    // Run the built .exe
    // -------------------------------------------------------------------------

    private static void RunBuiltExe()
    {
        string exePath = Path.GetFullPath(BuildExePath);
        if (!File.Exists(exePath))
        {
            Debug.LogError($"[GameBuilder] Cannot run — exe not found at {exePath}");
            return;
        }

        Debug.Log($"[GameBuilder] ▶ Launching {Path.GetFileName(exePath)}...");
        Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath),
            UseShellExecute = true
        });
    }
}
