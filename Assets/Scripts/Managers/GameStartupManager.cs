using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Shows the welcome screen at game startup and guarantees the scene has the
/// infrastructure the UI needs (a Camera and an EventSystem). This is fully
/// self-sufficient: drop it on a single empty GameObject in any scene — even a
/// truly empty one — and the menu will render and respond to clicks.
///
/// On Awake it will:
///   1. Ensure a Camera exists (UI overlay + a clean background clear).
///   2. Ensure an EventSystem exists (so buttons receive clicks).
///   3. Ensure a WorldManager exists (persistent world config).
///   4. Create the WelcomeScreenManager UI.
/// </summary>
public class GameStartupManager : MonoBehaviour
{
    [Tooltip("If true AND running a development/editor build, skip the menu and jump " +
             "straight into SampleScene with a dev world. Off by default so the menu " +
             "actually shows when you press Play.")]
    public bool skipMenuInDevelopment = false;

    private void Awake()
    {
        // Optional fast-path for iteration. OFF by default.
        if (skipMenuInDevelopment && Debug.isDebugBuild)
        {
            EnsureWorldManagerExists();
            WorldManager.Instance.SetWorld("Dev World", "12345", 1);
            Debug.Log("GameStartupManager: skipMenuInDevelopment is ON — loading SampleScene directly.");
            SceneManager.LoadScene("SampleScene");
            return;
        }

        EnsureCamera();
        EnsureEventSystem();
        EnsureWorldManagerExists();
        ShowWelcomeScreen();
    }

    /// <summary>
    /// A ScreenSpaceOverlay canvas renders without a camera, but Unity still warns
    /// "No cameras rendering" and the game view is blank behind the UI. Guarantee one.
    /// </summary>
    private void EnsureCamera()
    {
        if (Camera.main != null)
        {
            return;
        }

        if (Object.FindFirstObjectByType<Camera>() != null)
        {
            return;
        }

        GameObject camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        Camera cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.07f, 0.10f, 1f);
        cam.orthographic = true;
        camGO.AddComponent<AudioListener>();
        Debug.Log("GameStartupManager: created Main Camera (none existed).");
    }

    /// <summary>
    /// Without an EventSystem, uGUI buttons never receive pointer events — the menu
    /// looks fine but nothing is clickable. This is the #1 cause of a "dead" menu.
    /// </summary>
    private void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>();
        Debug.Log("GameStartupManager: created EventSystem (none existed).");
    }

    private void EnsureWorldManagerExists()
    {
        if (WorldManager.Instance == null)
        {
            GameObject wmGO = new GameObject("WorldManager");
            wmGO.AddComponent<WorldManager>();
        }
    }

    private void ShowWelcomeScreen()
    {
        WelcomeScreenManager existing = FindFirstObjectByType<WelcomeScreenManager>();
        if (existing == null)
        {
            GameObject wsGO = new GameObject("WelcomeScreenManager");
            wsGO.AddComponent<WelcomeScreenManager>();
            Debug.Log("GameStartupManager: WelcomeScreenManager created.");
        }

        // WelcomeScreenManager now owns the flow.
        enabled = false;
    }
}
