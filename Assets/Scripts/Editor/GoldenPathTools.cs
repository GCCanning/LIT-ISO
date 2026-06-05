using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using EthraClone.TrialWeek;

/// <summary>
/// Canonical entry points for the active LIT-ISO golden path.
/// These wrappers intentionally delegate to the current setup/validation tools
/// so the menu stays synced instead of maintaining a second implementation.
/// </summary>
public static class GoldenPathTools
{
    private const string GoldenPathDocument = "GOLDEN_PATH.md";

    [MenuItem("Tools/LIT-ISO/Golden Path/Run Current Golden Path", false, -100)]
    public static void RunCurrentGoldenPath()
    {
        string setupLog = QuickPlayTestSetup.RunSetup(showDialog: false);

        // After the standard golden-path setup, wire the LPC layered character
        // onto the player. Safe and idempotent: if no LPC assets exist yet it
        // just adds the empty components, ready for sheet assignment.
        string lpcLog = LITISO.LPC.EditorTools.LPCGoldenPathSetup.WireLPCPlayer();
        Debug.Log("[Golden Path -> LPC]\n" + lpcLog);

        string validationLog = ValidateCurrentSceneInternal();

        var report = new StringBuilder();
        report.AppendLine("Golden Path completed.\n");
        report.AppendLine("Setup:");
        report.AppendLine(setupLog.Trim());
        report.AppendLine("\nLPC:");
        report.AppendLine(lpcLog.Trim());
        report.AppendLine("\nValidation:");
        report.AppendLine(validationLog.Trim());

        string reportText = report.ToString();
        Debug.Log("[Golden Path]\n" + reportText);
        EditorUtility.DisplayDialog("Golden Path Ready", reportText, "OK");
    }

    [MenuItem("Tools/LIT-ISO/Golden Path/Validate Current Scene", false, -99)]
    public static void ValidateCurrentScene()
    {
        string report = ValidateCurrentSceneInternal();
        Debug.Log("[Golden Path Validation]\n" + report);
        EditorUtility.DisplayDialog("Golden Path Validation", report, "OK");
    }

    [MenuItem("Tools/LIT-ISO/Golden Path/Rebuild Full Playtest Scene", false, -98)]
    public static void RebuildFullPlaytestScene()
    {
        IsoWorldSetup.BuildAndValidateFullPlaytestScene();
    }

    [MenuItem("Tools/LIT-ISO/Golden Path/Open Golden Path Doc", false, -97)]
    public static void OpenGoldenPathDoc()
    {
        string path = Path.GetFullPath(GoldenPathDocument);
        if (!File.Exists(path))
        {
            EditorUtility.DisplayDialog(
                "Golden Path",
                $"Could not find {GoldenPathDocument} at:\n{path}",
                "OK");
            return;
        }

        EditorUtility.OpenWithDefaultApp(path);
    }

    [MenuItem("Tools/LIT-ISO/Golden Path/About", false, -96)]
    public static void ShowAbout()
    {
        EditorUtility.DisplayDialog(
            "LIT-ISO Golden Path",
            "Run Current Golden Path is the synced one-click setup for the active prototype.\n\n" +
            "It delegates to QuickPlayTestSetup, wires the LPC player, then validates the active scene.\n\n" +
            "Use Rebuild Full Playtest Scene only when you want a clean generated scene.",
            "OK");
    }

    private static string ValidateCurrentSceneInternal()
    {
        IsoWorldSetup.ValidateFullPlaytestScene();

        var report = new StringBuilder();
        report.AppendLine("=== Golden Path Validation ===");

        Grid grid = Object.FindFirstObjectByType<Grid>();
        IsoWorldChunkManager world = Object.FindFirstObjectByType<IsoWorldChunkManager>();
        IsoPlayerController player = Object.FindFirstObjectByType<IsoPlayerController>();
        Camera camera = Camera.main;
        SceneValidator sceneValidator = Object.FindFirstObjectByType<SceneValidator>();
        DayNightMusicManager music = Object.FindFirstObjectByType<DayNightMusicManager>();
        Canvas hud = Object.FindFirstObjectByType<Canvas>();
        GameSettingsMenu settingsMenu = Object.FindFirstObjectByType<GameSettingsMenu>();
        MovementDebugOverlay debugOverlay = Object.FindFirstObjectByType<MovementDebugOverlay>();

        AppendStatus(report, grid != null, "Grid present");
        AppendStatus(report, world != null, "IsoWorldChunkManager present");
        AppendStatus(report, player != null, "IsoPlayerController present");
        AppendStatus(report, camera != null, "Main Camera present");
        AppendStatus(report, sceneValidator != null, "SceneValidator present");
        AppendStatus(report, music != null, "DayNightMusicManager present");
        AppendStatus(report, hud != null, "Gameplay HUD present");
        AppendStatus(report, settingsMenu != null, "GameSettingsMenu present");
        AppendStatus(report, debugOverlay != null, "MovementDebugOverlay present");

        if (world != null)
        {
            AppendStatus(report, world.grid != null, "World grid reference wired");
            AppendStatus(report, world.player != null, "World player reference wired");
            AppendStatus(report, world.biomes != null && world.biomes.Length > 0, "World biomes configured");
            report.AppendLine($"- Height range: 0..{world.MaxSupportedHeight}");
        }

        if (player != null)
        {
            AppendStatus(report, player.grid != null, "Player grid reference wired");
            AppendStatus(report, player.world != null, "Player world reference wired");
            AppendStatus(report, player.selectionMarker != null, "Selection marker wired");
            report.AppendLine($"- Player cached height: {player.CurrentGroundHeight}");

            Transform lpcRoot = player.transform.Find("LPCRoot");
            AppendStatus(report, lpcRoot != null, "LPC player root present");
        }

        if (camera != null)
        {
            AppendStatus(report, camera.transparencySortMode == TransparencySortMode.CustomAxis, "Camera uses custom transparency sort");
        }

        Vector3 expectedAxis = new Vector3(0f, 1f, -0.26f);
        bool axisMatches = GraphicsSettings.transparencySortMode == TransparencySortMode.CustomAxis &&
            (GraphicsSettings.transparencySortAxis - expectedAxis).sqrMagnitude <= 0.0001f;
        AppendStatus(report, axisMatches, "Project transparency sort axis matches isometric default");

        if (grid != null)
        {
            AppendStatus(report, grid.cellLayout == GridLayout.CellLayout.IsometricZAsY, "Grid uses Isometric Z as Y");
        }

        return report.ToString();
    }

    private static void AppendStatus(StringBuilder report, bool ok, string label)
    {
        report.AppendLine($"{(ok ? "OK" : "WARN")}  {label}");
    }
}
