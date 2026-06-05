using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the MenuScene with proper setup for the welcome screen.
/// Called automatically or via menu.
/// </summary>
public class MenuSceneBuilder
{
    private const string MenuScenePath = "Assets/Scenes/MenuScene.unity";

    [MenuItem("Tools/LIT-ISO/Setup/Create Menu Scene", false, 100)]
    public static void CreateMenuScene()
    {
        // Check if MenuScene already exists
        Scene existingScene = EditorSceneManager.GetSceneByPath(MenuScenePath);
        if (existingScene.isLoaded)
        {
            EditorSceneManager.CloseScene(existingScene, true);
        }

        // Create new scene WITH default objects (Main Camera + Directional Light).
        // EmptyScene has no camera, which causes "No cameras rendering" and a blank view.
        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // Configure the camera for a clean 2D UI background.
        Camera mainCam = Object.FindFirstObjectByType<Camera>();
        if (mainCam != null)
        {
            mainCam.orthographic = true;
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.06f, 0.07f, 0.10f, 1f);
        }

        // -- EventSystem (required for uGUI button clicks) --
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
        }

        // -- 1. GameStartupManager --
        GameObject startupGO = new GameObject("GameStartupManager");
        GameStartupManager startupMgr = startupGO.AddComponent<GameStartupManager>();
        startupMgr.skipMenuInDevelopment = false;  // Show menu for production

        // -- 2. WelcomeScreenUI --
        GameObject welcomeGO = new GameObject("WelcomeScreenUI");
        WelcomeScreenManager welcomeMgr = welcomeGO.AddComponent<WelcomeScreenManager>();

        // Try to auto-assign campfire background image if it exists
        AssignBackgroundImageIfExists(welcomeMgr);

        // Save scene
        EditorSceneManager.SaveScene(newScene, MenuScenePath, true);
        EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);

        Debug.Log($"✓ MenuScene created at {MenuScenePath}");
        Debug.Log($"✓ Added: GameStartupManager (skipMenuInDevelopment=false)");
        Debug.Log($"✓ Added: WelcomeScreenUI with WelcomeScreenManager");
        if (welcomeMgr.backgroundImage != null)
        {
            Debug.Log($"✓ Background image assigned: {welcomeMgr.backgroundImage.name}");
        }

        EditorUtility.DisplayDialog(
            "Menu Scene Ready",
            "MenuScene created successfully!\n\n" +
            (welcomeMgr.backgroundImage != null
                ? "✓ Background image auto-assigned!\n\n"
                : "Note: No background image found. You can assign one manually.\n\n") +
            "Next steps:\n" +
            "1. File > Build Settings\n" +
            "2. Drag MenuScene to Slot 0\n" +
            "3. SampleScene to Slot 1\n\n" +
            "Press Play to test!",
            "OK");
    }

    private static void AssignBackgroundImageIfExists(WelcomeScreenManager welcomeMgr)
    {
        // Method 1: Try direct path
        string directPath = "Assets/Art/UI/Splash/CampfireMenu.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(directPath);

        // Method 2: If that fails, search by name
        if (sprite == null)
        {
            string[] guids = AssetDatabase.FindAssets("CampfireMenu t:Sprite");
            if (guids.Length > 0)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            }
        }

        // Assign if found
        if (sprite != null)
        {
            welcomeMgr.backgroundImage = sprite;
            EditorUtility.SetDirty(welcomeMgr);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"✓ Auto-assigned background image: {sprite.name}");
        }
        else
        {
            Debug.LogWarning("⚠ CampfireMenu.png not found. Expected at: Assets/Art/UI/Splash/CampfireMenu.png");
        }
    }

    /// <summary>
    /// Also called from QuickPlayTestSetup to ensure MenuScene exists.
    /// </summary>
    public static bool EnsureMenuSceneExists()
    {
        Scene scene = EditorSceneManager.GetSceneByPath(MenuScenePath);
        if (scene.isLoaded)
        {
            return true;  // Already exists
        }

        // Check if file exists
        if (System.IO.File.Exists(MenuScenePath))
        {
            EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            return true;
        }

        // Need to create it
        CreateMenuScene();
        return true;
    }
}
