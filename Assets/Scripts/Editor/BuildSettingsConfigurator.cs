using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Configures the build settings with the proper scene order for the golden path:
/// Slot 0: MenuScene (lightweight, displays immediately)
/// Slot 1: SampleScene (full gameplay with biome generation)
/// </summary>
public class BuildSettingsConfigurator
{
    private const string MenuScenePath = "Assets/Scenes/MenuScene.unity";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Tools/LIT-ISO/Setup/Configure Build Settings", false, 101)]
    public static void ConfigureBuildSettings()
    {
        // First, ensure MenuScene exists
        MenuSceneBuilder.EnsureMenuSceneExists();

        // Get current scenes in build
        EditorBuildSettingsScene[] currentScenes = EditorBuildSettings.scenes;

        // Create new scenes array with MenuScene and SampleScene in the right order
        EditorBuildSettingsScene[] newScenes = new EditorBuildSettingsScene[2];

        // Slot 0: MenuScene
        newScenes[0] = new EditorBuildSettingsScene(MenuScenePath, true);

        // Slot 1: SampleScene
        newScenes[1] = new EditorBuildSettingsScene(SampleScenePath, true);

        // Apply to build settings
        EditorBuildSettings.scenes = newScenes;

        Debug.Log("✓ Build Settings configured:");
        Debug.Log("  Slot 0: MenuScene (menu + startup)");
        Debug.Log("  Slot 1: SampleScene (gameplay)");

        EditorUtility.DisplayDialog(
            "Build Settings Ready",
            "Build settings configured successfully!\n\n" +
            "Slot 0: MenuScene\n" +
            "Slot 1: SampleScene\n\n" +
            "The game will boot to the menu. Players create/load a world, then it loads SampleScene for gameplay.",
            "OK");
    }
}
