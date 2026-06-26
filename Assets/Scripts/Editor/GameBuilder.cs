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
    // Must match Tools/BuildGame.bat, which looks under "Build\" for the most
    // recently created timestamped build folder and points the desktop shortcut
    // there.
    // (NOTE: there is also a stale "Builds/" (plural) folder from an earlier
    // drift between this script and the .bat — safe to delete once confirmed.)
    private const string BuildRootFolder = "Build";

    // 2026-06-13: the game now boots into MenuScene (WelcomeScreenManager), which
    // loads "IsoCoreFoundation" (the canonical Track B game scene) via
    // LoadingScreen.Go(). The old InfinitePlainsPrototype/SampleScene flow is
    // retired — build both scenes, MenuScene first so it's scene 0 (the one that
    // launches on startup).
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/MenuScene.unity",
        "Assets/Scenes/IsoCoreFoundation.unity",
    };

    // 2026-06-14 (owner request): every build gets its own timestamped folder
    // (Build/<ProductName>_yyyy-MM-dd_HHmmss/) instead of overwriting
    // Build/LIT-ISO/ in place. This makes it impossible to mistake a stale exe
    // (e.g. from before a content/asset change) for the latest one — the .bat
    // always launches/shortcuts the newest timestamped folder, and old builds
    // are kept side-by-side until cleaned up.
    private static string _buildStamp;

    /// <summary>The timestamped folder name for the build currently in progress.
    /// Set once at the start of <see cref="RunBuildPipeline"/>. Falls back to
    /// "latest" if accessed outside of a build (shouldn't normally happen).</summary>
    private static string BuildFolderName => string.IsNullOrEmpty(_buildStamp) ? "latest" : $"{ProductName}_{_buildStamp}";

    private static string BuildOutputFolder => Path.Combine(BuildRootFolder, BuildFolderName, ProductName);
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

    /// <summary>Returns the newest "Build/&lt;ProductName&gt;_&lt;timestamp&gt;" folder
    /// (each build gets its own timestamped folder — see <see cref="_buildStamp"/>),
    /// or null if no build has ever been made.</summary>
    private static string FindLatestBuildFolder()
    {
        string root = Path.GetFullPath(BuildRootFolder);
        if (!Directory.Exists(root)) return null;

        string latest = null;
        System.DateTime latestTime = System.DateTime.MinValue;
        foreach (string dir in Directory.GetDirectories(root, $"{ProductName}_*"))
        {
            System.DateTime t = Directory.GetCreationTime(dir);
            if (t > latestTime)
            {
                latestTime = t;
                latest = dir;
            }
        }
        return latest;
    }

    [MenuItem("Tools/LIT-ISO/Build/Open Builds Folder", false, 402)]
    public static void OpenBuildsFolder()
    {
        string folder = FindLatestBuildFolder() ?? Path.GetFullPath(BuildRootFolder);
        if (!Directory.Exists(folder))
        {
            EditorUtility.DisplayDialog(
                "Builds Folder",
                $"No build exists yet at:\n{Path.GetFullPath(BuildRootFolder)}\n\nUse 'Build Standalone' first.",
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
        string folder = Path.GetFullPath(BuildRootFolder);
        if (!Directory.Exists(folder))
        {
            Debug.Log("[GameBuilder] Builds folder is already empty.");
            return;
        }

        if (!EditorUtility.DisplayDialog(
            "Clean Builds Folder",
            $"Delete ALL builds (every timestamped folder) under?\n\n{folder}",
            "Delete All",
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
        bool ok = RunBuildPipeline(out BuildReport report);

        if (ok)
        {
            ReportBuildSuccess(report);
            if (runAfterBuild)
            {
                RunBuiltExe();
            }
        }
        else
        {
            if (report != null)
            {
                ReportBuildFailure(report);
            }
        }
    }

    /// <summary>
    /// Shared pipeline used by both the interactive menu items and the
    /// batchmode entry point (<see cref="BuildScript.BuildWindows"/>).
    ///
    /// Returns true on success. <paramref name="report"/> is null only if the
    /// build never reached <see cref="ExecuteBuild"/> (e.g. missing scenes) —
    /// in that case <see cref="LastFailureReason"/> explains why.
    /// </summary>
    internal static bool RunBuildPipeline(out BuildReport report)
    {
        report = null;
        LastFailureReason = null;
        _buildStamp = System.DateTime.Now.ToString("yyyy-MM-dd_HHmmss");

        Debug.Log("═══════════════════════════════════════════════════════════");
        Debug.Log("[GameBuilder] Starting build pipeline...");
        Debug.Log($"[GameBuilder] Output folder: {Path.Combine(BuildRootFolder, BuildFolderName)}");
        Debug.Log("═══════════════════════════════════════════════════════════");

        // Step 1: Configure Player Settings
        ConfigurePlayerSettings();

        // Step 2: Configure Quality Settings
        ConfigureQualitySettings();

        // Step 3: Ensure scenes exist and are in Build Settings
        if (!EnsureScenesReady())
        {
            LastFailureReason = $"One or more required scenes were not found:\n{string.Join("\n", ScenePaths)}";
            Debug.LogError($"[GameBuilder] ❌ BUILD FAILED — {LastFailureReason}");

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Build Failed", LastFailureReason, "OK");
            }
            return false;
        }

        // Step 4: Save the current scene if it's one of the build scenes
        SaveOpenSceneIfBuildScene();

        // Step 5: Run the Unity build
        report = ExecuteBuild();

        // Step 6: Always write build_info.txt so the owner can see what happened,
        // whether the build succeeded or failed.
        WriteBuildInfo(report);

        if (report.summary.result != BuildResult.Succeeded)
        {
            LastFailureReason = $"Build result: {report.summary.result} " +
                $"({report.summary.totalErrors} error(s)). See Editor log / build_info.txt.";
            return false;
        }

        return true;
    }

    /// <summary>Human-readable reason the last <see cref="RunBuildPipeline"/> call failed
    /// before producing a <see cref="BuildReport"/> (null otherwise).</summary>
    internal static string LastFailureReason { get; private set; }

    // -------------------------------------------------------------------------
    // build_info.txt
    // -------------------------------------------------------------------------

    /// <summary>
    /// Writes a small text file alongside the .exe summarizing the build, so the
    /// owner can confirm (without opening Unity) what was built, when, from which
    /// scenes/commit, and whether it succeeded.
    /// </summary>
    private static void WriteBuildInfo(BuildReport report)
    {
        try
        {
            string fullOutput = Path.GetFullPath(BuildOutputFolder);
            if (!Directory.Exists(fullOutput))
            {
                Directory.CreateDirectory(fullOutput);
            }

            string infoPath = Path.Combine(fullOutput, "build_info.txt");

            BuildSummary summary = report.summary;
            double sizeMB = summary.totalSize / (1024.0 * 1024.0);
            string gitCommit = TryGetGitCommit();

            using (StreamWriter writer = new StreamWriter(infoPath, append: false))
            {
                writer.WriteLine($"LIT-ISO build info");
                writer.WriteLine($"==================");
                writer.WriteLine($"Product:     {ProductName} v{Version}");
                writer.WriteLine($"Built:       {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($"Result:      {summary.result}");
                writer.WriteLine($"Errors:      {summary.totalErrors}");
                writer.WriteLine($"Warnings:    {summary.totalWarnings}");
                writer.WriteLine($"Size:        {sizeMB:F1} MB");
                writer.WriteLine($"Build time:  {summary.totalTime.TotalSeconds:F1}s");
                writer.WriteLine($"Output:      {Path.GetFullPath(BuildExePath)}");
                writer.WriteLine($"Unity:       {Application.unityVersion}");
                writer.WriteLine($"Git commit:  {gitCommit}");
                writer.WriteLine($"Scenes:");
                foreach (string scenePath in ScenePaths)
                {
                    writer.WriteLine($"  - {scenePath}");
                }
            }

            Debug.Log($"[GameBuilder] Wrote build info to {infoPath}");
        }
        catch (System.Exception ex)
        {
            // Never let build_info.txt writing fail the build itself.
            Debug.LogWarning($"[GameBuilder] Could not write build_info.txt: {ex.Message}");
        }
    }

    private static string TryGetGitCommit()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --short HEAD",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Directory.GetCurrentDirectory()
            };
            using (Process p = Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(2000);
                return string.IsNullOrEmpty(output) ? "unknown" : output;
            }
        }
        catch
        {
            return "unknown";
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

    private static bool EnsureScenesReady()
    {
        Debug.Log("[GameBuilder] Ensuring scenes are in Build Settings...");

        foreach (string scenePath in ScenePaths)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogError($"[GameBuilder] Scene not found at {scenePath}");
                return false;
            }
        }

        // Rebuild the scene list so ScenePaths are present, enabled, and in order
        // (MenuScene first = scene 0 = the one that launches on startup), while
        // preserving any other scenes already configured in Build Settings.
        List<EditorBuildSettingsScene> existing = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        List<EditorBuildSettingsScene> ordered = new List<EditorBuildSettingsScene>();

        foreach (string scenePath in ScenePaths)
        {
            ordered.Add(new EditorBuildSettingsScene(scenePath, enabled: true));
            Debug.Log($"   ✅ Scene in Build Settings: {scenePath}");
        }

        foreach (EditorBuildSettingsScene s in existing)
        {
            bool alreadyOrdered = false;
            foreach (string scenePath in ScenePaths)
            {
                if (s.path == scenePath) { alreadyOrdered = true; break; }
            }
            if (!alreadyOrdered)
                ordered.Add(s);
        }

        EditorBuildSettings.scenes = ordered.ToArray();
        return true;
    }

    private static void SaveOpenSceneIfBuildScene()
    {
        UnityEngine.SceneManagement.Scene activeScene = EditorSceneManager.GetActiveScene();
        foreach (string scenePath in ScenePaths)
        {
            if (activeScene.path == scenePath && activeScene.isDirty)
            {
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log($"[GameBuilder] Saved open scene: {scenePath}");
                return;
            }
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
            scenes = ScenePaths,
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
