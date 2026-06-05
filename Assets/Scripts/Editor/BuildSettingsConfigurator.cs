using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Configures the build settings with the proper scene order for the golden path:
/// Slot 0: MenuScene (lightweight, displays immediately)
/// Slot 1: IsoCoreFoundation (canonical game — survival/crafting/building loop)
/// </summary>
public class BuildSettingsConfigurator
{
    private const string MenuScenePath = "Assets/Scenes/MenuScene.unity";
    private const string GameScenePath = "Assets/Scenes/IsoCoreFoundation.unity";

    [MenuItem("Tools/LIT-ISO/Setup/Configure Build Settings", false, 101)]
    public static void ConfigureBuildSettings()
    {
        // First, ensure MenuScene exists
        MenuSceneBuilder.EnsureMenuSceneExists();

        // Slot 0: MenuScene, Slot 1: IsoCoreFoundation (canonical game scene).
        EditorBuildSettingsScene[] newScenes = new EditorBuildSettingsScene[2];
        newScenes[0] = new EditorBuildSettingsScene(MenuScenePath, true);
        newScenes[1] = new EditorBuildSettingsScene(GameScenePath, true);

        EditorBuildSettings.scenes = newScenes;

        Debug.Log("✓ Build Settings configured:");
        Debug.Log("  Slot 0: MenuScene (menu + startup)");
        Debug.Log("  Slot 1: IsoCoreFoundation (gameplay)");

        EditorUtility.DisplayDialog(
            "Build Settings Ready",
            "Build settings configured successfully!\n\n" +
            "Slot 0: MenuScene\n" +
            "Slot 1: IsoCoreFoundation\n\n" +
            "The game boots to the menu. Players create/load a world, then it loads " +
            "IsoCoreFoundation for gameplay.",
            "OK");
    }
}
