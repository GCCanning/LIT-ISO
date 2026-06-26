using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using EthraClone.TrialWeek;

/// <summary>
/// Editor tool: Tools > LIT-ISO > Setup > Setup Gameplay Layer
///
/// Adds the resource/inventory/UI gameplay systems to the currently-open scene.
///
/// What it does:
///   1. Finds the Player (IsoPlayerController) and adds PlayerInventory,
///      PlayerHealth, and IsoInteractionController if not already present.
///   2. Creates (or updates) a ScreenSpaceOverlay Canvas with:
///        - Bottom-center hotbar  (HotbarUI)
///        - Bottom-left health bar (HealthBarUI)
///        - Top-right notification stack (PickupNotificationUI)
///
/// Run this once after opening the prototype scene.
/// Safe to re-run — existing components are not replaced or reset.
/// </summary>
public static class GameplayLayerSetup
{
    private const string CanvasName         = "GameplayHUD";
    private const string HotbarPanelName    = "HotbarPanel";
    private const string HealthPanelName    = "HealthPanel";
    private const string NotifPanelName     = "NotificationStack";

    // SetupGameplayLayer() retired 2026-06. The HUD is now bootstrapped at
    // runtime by GameHudInitializer (RuntimeInitializeOnLoadMethod) — no scene
    // setup needed. The legacy HealthBarUI / HotbarUI / PickupNotificationUI /
    // MovementDebugOverlay components have been removed from the project.
    // Use Tools > LIT-ISO > Assets > Create Starter Gameplay Assets for items.

    // -------------------------------------------------------------------------
    // Canvas
    // -------------------------------------------------------------------------

    private static Canvas GetOrCreateCanvas()
    {
        // Look for an existing GameplayHUD canvas
        foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.name == CanvasName) return c;
        }

        var canvasGO = new GameObject(CanvasName);
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution  = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight   = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        return canvas;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        var eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static GameObject GetOrCreatePanel(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        T existing = go.GetComponent<T>();
        if (existing != null) return existing;
        return go.AddComponent<T>();
    }

    // =========================================================================
    // Starter item and node assets
    // =========================================================================

    [MenuItem("Tools/LIT-ISO/Assets/Create Starter Gameplay Assets", false, 202)]
    public static void CreateStarterAssets()
    {
        EnsureAssetFolder("Assets/World");
        EnsureAssetFolder("Assets/World/Items");
        EnsureAssetFolder("Assets/World/ResourceNodes");

        // --- Item definitions ---
        CreateItemDef("Assets/World/Items/Item_Wood.asset",       "wood",        "Wood");
        CreateItemDef("Assets/World/Items/Item_Pinecone.asset",   "pinecone",    "Pinecone");
        CreateItemDef("Assets/World/Items/Item_Treesap.asset",    "treesap",     "Treesap");
        CreateItemDef("Assets/World/Items/Item_Stone.asset",      "stone",       "Stone");
        CreateItemDef("Assets/World/Items/Item_CopperOre.asset",  "copper_ore",  "Copper Ore");
        CreateItemDef("Assets/World/Items/Item_Coin.asset",       "coin",        "Coin",
                      ItemCategory.Currency);

        // --- Resource node definitions ---
        CreateOakTreeNodeDef();
        CreateRockNodeDef();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[GameplayLayerSetup] Starter assets created at Assets/World/. " +
                  "Assign Sprite icons to each ItemDefinition in the Inspector.");
    }

    private static ItemDefinition CreateItemDef(string path, string id, string display,
                                                ItemCategory cat = ItemCategory.Resource)
    {
        var existing = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        if (existing != null) return existing;

        var def = ScriptableObject.CreateInstance<ItemDefinition>();
        def.itemId      = id;
        def.displayName = display;
        def.category    = cat;
        def.maxStack    = cat == ItemCategory.Currency ? 9999 : 999;
        AssetDatabase.CreateAsset(def, path);
        return def;
    }

    private static void CreateOakTreeNodeDef()
    {
        const string path = "Assets/World/ResourceNodes/Node_OakTree.asset";
        if (AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(path) != null) return;

        var def = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
        def.nodeName       = "Oak Tree";
        def.spawnChance    = 0.06f;
        def.harvestCooldown = 45f;
        def.harvestRadius  = 1.3f;
        def.minimumSpacing = 2.5f;

        def.drops = new ItemDrop[]
        {
            MakeDrop("Assets/World/Items/Item_Wood.asset",     1, 3, 1.00f),
            MakeDrop("Assets/World/Items/Item_Pinecone.asset", 0, 2, 0.40f),
            MakeDrop("Assets/World/Items/Item_Treesap.asset",  0, 1, 0.20f),
        };

        AssetDatabase.CreateAsset(def, path);
    }

    private static void CreateRockNodeDef()
    {
        const string path = "Assets/World/ResourceNodes/Node_Rock.asset";
        if (AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(path) != null) return;

        var def = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
        def.nodeName        = "Rock";
        def.spawnChance     = 0.04f;
        def.harvestCooldown = 60f;
        def.harvestRadius   = 1.2f;
        def.minimumSpacing  = 3.0f;

        def.drops = new ItemDrop[]
        {
            MakeDrop("Assets/World/Items/Item_Stone.asset",     1, 3, 1.00f),
            MakeDrop("Assets/World/Items/Item_CopperOre.asset", 0, 1, 0.30f),
        };

        AssetDatabase.CreateAsset(def, path);
    }

    private static ItemDrop MakeDrop(string itemPath, int min, int max, float chance)
    {
        return new ItemDrop
        {
            item      = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath),
            minAmount = min,
            maxAmount = max,
            chance    = chance,
        };
    }

    private static void EnsureAssetFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            string folder = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
