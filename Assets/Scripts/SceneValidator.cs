using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using EthraClone.TrialWeek;

/// <summary>
/// Comprehensive scene validation and setup on startup.
/// Ensures all required components exist (AudioListener, MainCamera, etc.)
/// Logs validation results for debugging.
/// </summary>
public class SceneValidator : MonoBehaviour
{
    private static bool hasRun = false;

    private void Awake()
    {
        // Only run once per scene load
        if (hasRun)
            return;

        hasRun = true;

        Debug.Log("═══════════════════════════════════════════════════════════");
        Debug.Log("[SceneValidator] Starting comprehensive scene validation...");
        Debug.Log("═══════════════════════════════════════════════════════════");

        ValidateAudioListener();
        ValidateMainCamera();
        ValidateGameplayComponents();
        ValidateIsometricRenderingContract();
        ValidateWorldHeightContract();

        Debug.Log("═══════════════════════════════════════════════════════════");
        Debug.Log("[SceneValidator] Scene validation complete!");
        Debug.Log("═══════════════════════════════════════════════════════════");
    }

    private void ValidateAudioListener()
    {
        Debug.Log("\n[AudioListener Validation]");

        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);

        if (listeners.Length == 0)
        {
            Debug.LogWarning("❌ NO AudioListener found in scene!");
            Debug.Log("   Attempting to add AudioListener...");

            // Try to add to Main Camera
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                AudioListener listener = mainCam.gameObject.AddComponent<AudioListener>();
                Debug.Log("   ✅ Added AudioListener to Main Camera: " + mainCam.gameObject.name);
            }
            else
            {
                // Try to find any camera
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                if (cameras.Length > 0)
                {
                    AudioListener listener = cameras[0].gameObject.AddComponent<AudioListener>();
                    Debug.Log("   ✅ Added AudioListener to first camera: " + cameras[0].gameObject.name);
                }
                else
                {
                    Debug.LogError("   ❌ CRITICAL: No Camera found in scene! Cannot add AudioListener.");
                    Debug.LogError("   ❌ Audio will NOT work. Please add a Camera to the scene.");
                }
            }
        }
        else if (listeners.Length == 1)
        {
            Debug.Log("   ✅ Found AudioListener on: " + listeners[0].gameObject.name);
        }
        else
        {
            Debug.LogWarning("   ⚠️  Multiple AudioListeners found (" + listeners.Length + "). Only one should exist.");
            Debug.Log("   ℹ️  First listener will be used (on " + listeners[0].gameObject.name + ")");

            // Disable extra listeners
            for (int i = 1; i < listeners.Length; i++)
            {
                Debug.Log("   Disabling extra AudioListener on: " + listeners[i].gameObject.name);
                listeners[i].enabled = false;
            }
        }
    }

    private void ValidateMainCamera()
    {
        Debug.Log("\n[Main Camera Validation]");

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("❌ No Main Camera found in scene!");

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cameras.Length > 0)
            {
                Debug.Log("   Found " + cameras.Length + " camera(s). Setting first as MainCamera...");
                cameras[0].gameObject.tag = "MainCamera";
                Debug.Log("   ✅ Tagged camera: " + cameras[0].gameObject.name);
            }
            else
            {
                Debug.LogError("   ❌ CRITICAL: No cameras in scene!");
            }
        }
        else
        {
            Debug.Log("   ✅ Main Camera found: " + mainCam.gameObject.name);

            // Verify it has a Camera component
            if (mainCam.GetComponent<Camera>() != null)
            {
                Debug.Log("      ✅ Camera component present");
            }
        }
    }

    private void ValidateGameplayComponents()
    {
        Debug.Log("\n[Gameplay Components Validation]");

        // Check for IsoPlayerController (Player)
        IsoPlayerController playerCtrl = FindFirstObjectByType<IsoPlayerController>();
        if (playerCtrl != null)
        {
            Debug.Log("   ✅ Player (IsoPlayerController) found: " + playerCtrl.gameObject.name);
        }
        else
        {
            Debug.LogWarning("   ⚠️  No IsoPlayerController in scene (Player not spawned)");
        }

        // Check for IsoWorldChunkManager
        IsoWorldChunkManager chunkMgr = FindFirstObjectByType<IsoWorldChunkManager>();
        if (chunkMgr != null)
        {
            Debug.Log("   ✅ World (IsoWorldChunkManager) found: " + chunkMgr.gameObject.name);
        }
        else
        {
            Debug.LogWarning("   ⚠️  No IsoWorldChunkManager in scene (World not spawned)");
        }

        // Check for GameplayHUD Canvas
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Canvas gameplayHUD = null;
        foreach (Canvas c in canvases)
        {
            if (c.gameObject.name == "GameplayHUD" || c.gameObject.name.Contains("HUD"))
            {
                gameplayHUD = c;
                break;
            }
        }

        if (gameplayHUD != null)
        {
            Debug.Log("   ✅ GameplayHUD Canvas found: " + gameplayHUD.gameObject.name);
        }
        else
        {
            Debug.LogWarning("   ⚠️  No GameplayHUD Canvas found (UI not spawned)");
        }

        // Check for DayNightMusicManager
        DayNightMusicManager musicMgr = FindFirstObjectByType<DayNightMusicManager>();
        if (musicMgr != null)
        {
            Debug.Log("   ✅ Music Manager found: " + musicMgr.gameObject.name);
        }
        else
        {
            Debug.LogWarning("   ⚠️  No DayNightMusicManager in scene (Music disabled)");
        }

        // Check for SunController (dynamic lighting)
        SunController sunCtrl = FindFirstObjectByType<SunController>();
        if (sunCtrl != null)
        {
            Debug.Log("   ✅ Sun Controller found: " + sunCtrl.gameObject.name);
            if (sunCtrl.cycleManager == null)
            {
                Debug.LogWarning("      ⚠️  SunController.cycleManager is not wired!");
            }
            if (sunCtrl.directionalLight == null)
            {
                Debug.LogWarning("      ⚠️  SunController.directionalLight is not wired!");
            }
        }
        else
        {
            Debug.LogWarning("   ⚠️  No SunController in scene (Dynamic lighting disabled)");
        }

        // Check for IsoRuntimeRecorder
        IsoRuntimeRecorder recorder = FindFirstObjectByType<IsoRuntimeRecorder>();
        if (recorder != null)
        {
            Debug.Log("   ✅ Runtime Recorder found: " + recorder.gameObject.name);
        }
        else
        {
            Debug.LogWarning("   ⚠️  No IsoRuntimeRecorder in scene (Data not recording)");
        }

        // Check for DropShadowCaster on player
        IsoPlayerController playerForShadow = FindFirstObjectByType<IsoPlayerController>();
        if (playerForShadow != null)
        {
            DropShadowCaster shadow = playerForShadow.GetComponent<DropShadowCaster>();
            if (shadow != null)
            {
                Debug.Log("   ✅ Player has DropShadowCaster (dynamic shadows enabled)");
            }
            else
            {
                Debug.LogWarning("   ℹ️  Player has no DropShadowCaster (no drop shadow)");
            }
        }
    }

    private void ValidateIsometricRenderingContract()
    {
        Debug.Log("\n[Isometric Rendering Contract]");

        if (GraphicsSettings.transparencySortMode != TransparencySortMode.CustomAxis)
        {
            Debug.LogWarning("   ⚠ GraphicsSettings.transparencySortMode is not CustomAxis.");
        }
        else
        {
            Debug.Log("   ✅ GraphicsSettings uses CustomAxis transparency sorting");
        }

        Vector3 expectedAxis = new Vector3(0f, 1f, -0.26f);
        if ((GraphicsSettings.transparencySortAxis - expectedAxis).sqrMagnitude > 0.0001f)
        {
            Debug.LogWarning($"   ⚠ Transparency sort axis differs from expected isometric axis. Current={GraphicsSettings.transparencySortAxis}");
        }
        else
        {
            Debug.Log("   ✅ Transparency sort axis matches isometric default");
        }

        TilemapRenderer[] renderers = FindObjectsByType<TilemapRenderer>(FindObjectsSortMode.None);
        if (renderers.Length == 0)
        {
            Debug.LogWarning("   ⚠ No TilemapRenderers found to validate.");
            return;
        }

        int nonIndividualCount = 0;
        int nonTopRightCount = 0;
        foreach (TilemapRenderer renderer in renderers)
        {
            if (!renderer.enabled)
            {
                continue;
            }

            if (renderer.mode != TilemapRenderer.Mode.Individual)
            {
                nonIndividualCount++;
            }

            if (renderer.sortOrder != TilemapRenderer.SortOrder.TopRight)
            {
                nonTopRightCount++;
            }
        }

        if (nonIndividualCount > 0)
        {
            Debug.LogWarning($"   ⚠ {nonIndividualCount} enabled TilemapRenderer(s) are not using Individual mode.");
        }
        else
        {
            Debug.Log("   ✅ Enabled TilemapRenderers use Individual mode");
        }

        if (nonTopRightCount > 0)
        {
            Debug.LogWarning($"   ⚠ {nonTopRightCount} enabled TilemapRenderer(s) are not using TopRight sort order.");
        }
        else
        {
            Debug.Log("   ✅ Enabled TilemapRenderers use TopRight sort order");
        }
    }

    private void ValidateWorldHeightContract()
    {
        Debug.Log("\n[World Height Contract]");

        IsoWorldChunkManager world = FindFirstObjectByType<IsoWorldChunkManager>();
        IsoPlayerController player = FindFirstObjectByType<IsoPlayerController>();
        if (world == null || player == null)
        {
            Debug.LogWarning("   ⚠ Cannot validate world height contract without both world and player.");
            return;
        }

        if (world.grid == null)
        {
            Debug.LogWarning("   ⚠ IsoWorldChunkManager.grid is missing.");
            return;
        }

        Grid grid = world.grid;
        if (grid.cellLayout != GridLayout.CellLayout.IsometricZAsY)
        {
            Debug.LogWarning($"   ⚠ Grid layout is {grid.cellLayout}, expected IsometricZAsY.");
        }
        else
        {
            Debug.Log("   ✅ Grid layout is IsometricZAsY");
        }

        if ((grid.cellSize - new Vector3(1f, 0.5f, 1f)).sqrMagnitude > 0.0001f)
        {
            Debug.LogWarning($"   ⚠ Grid cell size is {grid.cellSize}, expected approximately (1, 0.5, 1).");
        }
        else
        {
            Debug.Log("   ✅ Grid cell size matches expected isometric footprint");
        }

        IsoWorldChunkManager.GroundCellSample playerSample = world.SampleWorldPosition(player.transform.position, player.CurrentGroundHeight);
        if (playerSample.Height != player.CurrentGroundHeight)
        {
            Debug.LogWarning($"   ⚠ Player current ground height ({player.CurrentGroundHeight}) does not match sampled world height ({playerSample.Height}).");
        }
        else
        {
            Debug.Log("   ✅ Player ground height matches sampled world height");
        }

        int clampedLayerHeight = Mathf.Clamp(player.CurrentGroundHeight, 0, world.MaxSupportedHeight);
        int expectedLayer = 10 + clampedLayerHeight;
        if (player.gameObject.layer != expectedLayer)
        {
            Debug.LogWarning($"   ⚠ Player layer is {player.gameObject.layer}, expected {expectedLayer} for height {clampedLayerHeight}.");
        }
        else
        {
            Debug.Log("   ✅ Player physics layer matches current ground height");
        }
    }

    public static void ResetValidation()
    {
        hasRun = false;
    }
}
