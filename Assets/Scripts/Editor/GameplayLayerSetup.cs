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

    [MenuItem("Tools/LIT-ISO/Setup/Setup Gameplay Layer", false, 120)]
    public static void SetupGameplayLayer()
    {
        // ------------------------------------------------------------------
        // 1. Player components
        // ------------------------------------------------------------------
        IsoPlayerController playerCtrl = Object.FindFirstObjectByType<IsoPlayerController>();
        if (playerCtrl == null)
        {
            Debug.LogWarning("[GameplayLayerSetup] No IsoPlayerController found in scene. " +
                             "Open the prototype scene first, then run this tool.");
            return;
        }

        GameObject player = playerCtrl.gameObject;

        PlayerInventory inventory = EnsureComponent<PlayerInventory>(player);
        PlayerHealth    health    = EnsureComponent<PlayerHealth>(player);
                                    EnsureComponent<IsoInteractionController>(player);
                                    EnsureComponent<IsoHoverController>(player);

        Debug.Log($"[GameplayLayerSetup] Player components ensured on '{player.name}'.");

        // ------------------------------------------------------------------
        // 2. Canvas
        // ------------------------------------------------------------------
        Canvas canvas = GetOrCreateCanvas();
        EnsureEventSystem();

        // ------------------------------------------------------------------
        // 3. Hotbar — bottom-center
        // ------------------------------------------------------------------
        GameObject hotbarPanel = GetOrCreatePanel(canvas.transform, HotbarPanelName);
        {
            var rt = hotbarPanel.GetComponent<RectTransform>();
            // Anchor: bottom-center
            rt.anchorMin        = new Vector2(0.5f, 0f);
            rt.anchorMax        = new Vector2(0.5f, 0f);
            rt.pivot            = new Vector2(0.5f, 0f);
            rt.sizeDelta        = new Vector2(420f, 60f);
            rt.anchoredPosition = new Vector2(0f, 14f);

            EnsureComponent<HotbarUI>(hotbarPanel);

            // Transparent background for the whole bar container
            if (hotbarPanel.GetComponent<Image>() == null)
            {
                var img = hotbarPanel.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0f);
                img.raycastTarget = false;
            }
        }

        // ------------------------------------------------------------------
        // 4. Health bar — bottom-left
        // ------------------------------------------------------------------
        GameObject healthPanel = GetOrCreatePanel(canvas.transform, HealthPanelName);
        {
            var rt = healthPanel.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 0f);
            rt.anchorMax        = new Vector2(0f, 0f);
            rt.pivot            = new Vector2(0f, 0f);
            rt.sizeDelta        = new Vector2(180f, 36f);
            rt.anchoredPosition = new Vector2(14f, 14f);

            HealthBarUI healthUI = EnsureComponent<HealthBarUI>(healthPanel);
            BuildHealthBarContents(healthPanel, healthUI);
        }

        // ------------------------------------------------------------------
        // 5. Notification stack — top-right
        // ------------------------------------------------------------------
        GameObject notifPanel = GetOrCreatePanel(canvas.transform, NotifPanelName);
        {
            var rt = notifPanel.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(1f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(1f, 1f);
            rt.sizeDelta        = new Vector2(180f, 200f);
            rt.anchoredPosition = new Vector2(-14f, -14f);

            EnsureComponent<PickupNotificationUI>(notifPanel);

            // No background on the stack container itself
            var existingImg = notifPanel.GetComponent<Image>();
            if (existingImg != null) Object.DestroyImmediate(existingImg);
        }

        // ------------------------------------------------------------------
        // 6. Settings menu — toggled in-game with I
        // ------------------------------------------------------------------
        EnsureComponent<GameSettingsMenu>(canvas.gameObject);
        EnsureComponent<MovementDebugOverlay>(canvas.gameObject);

        // ------------------------------------------------------------------
        // 7. Mark scene dirty and save
        // ------------------------------------------------------------------
        EditorUtility.SetDirty(player);
        EditorUtility.SetDirty(canvas.gameObject);
        EditorSceneManager.MarkSceneDirty(player.scene);

        Debug.Log("[GameplayLayerSetup] Gameplay layer set up successfully. " +
                  "Press Play to test. Assign item art to ItemDefinition assets " +
                  "at Assets/World/Items/ once created.");
    }

    // -------------------------------------------------------------------------
    // Health bar sub-hierarchy
    // -------------------------------------------------------------------------

    private static void BuildHealthBarContents(GameObject parent, HealthBarUI healthUI)
    {
        // Only build once
        if (parent.transform.Find("BarBg") != null) return;

        // Dark background strip
        var bgGO = new GameObject("BarBg", typeof(RectTransform));
        bgGO.transform.SetParent(parent.transform, false);
        var bgRt = bgGO.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0.22f, 0.1f);
        bgRt.anchorMax = new Vector2(1f, 0.9f);
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.10f, 0.06f, 0.06f, 0.90f);

        // Fill image (Filled type, Horizontal)
        var fillGO = new GameObject("Fill", typeof(RectTransform));
        fillGO.transform.SetParent(bgGO.transform, false);
        var fillRt = fillGO.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(2f, 2f);
        fillRt.offsetMax = new Vector2(-2f, -2f);
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color    = new Color(0.78f, 0.14f, 0.14f, 1f);
        fillImg.type     = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 1f;

        // Text overlay "100 / 100"
        var txtGO = new GameObject("HealthText", typeof(RectTransform));
        txtGO.transform.SetParent(bgGO.transform, false);
        var txtRt = txtGO.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;
        var txt = txtGO.AddComponent<Text>();
        txt.text      = "100 / 100";
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = new Color(0.95f, 0.90f, 0.80f, 0.85f);
        LitIsoFont.Apply(txt, 13);

        // Portrait placeholder (left side)
        var portraitGO = new GameObject("Portrait", typeof(RectTransform));
        portraitGO.transform.SetParent(parent.transform, false);
        var portRt = portraitGO.GetComponent<RectTransform>();
        portRt.anchorMin = new Vector2(0f, 0f);
        portRt.anchorMax = new Vector2(0.20f, 1f);
        portRt.offsetMin = Vector2.zero;
        portRt.offsetMax = Vector2.zero;
        var portImg = portraitGO.AddComponent<Image>();
        portImg.color = new Color(0.22f, 0.30f, 0.40f, 0.85f);

        // Wire references
        healthUI.fillImage   = fillImg;
        healthUI.healthText  = txt;
        healthUI.portraitImage = portImg;
    }

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
