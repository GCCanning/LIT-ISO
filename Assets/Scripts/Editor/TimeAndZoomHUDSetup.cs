using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor tool: Tools > LIT-ISO > Setup > Setup Time And Zoom HUD
///
/// Adds these UI elements to the GameplayHUD canvas (creating it if missing):
///   1. Top-right clock display (GameTimeUI)
///   2. Bottom-right zoom +/- buttons that call ZoomController on Main Camera
///
/// Also adds a ZoomController to the Main Camera if not present.
///
/// Safe to re-run — existing elements are reused, not duplicated.
/// </summary>
public static class TimeAndZoomHUDSetup
{
    private const string CanvasName    = "GameplayHUD";
    private const string ClockName     = "ClockDisplay";
    private const string ZoomPanelName = "ZoomControls";

    [MenuItem("Tools/LIT-ISO/Setup/Setup Time And Zoom HUD", false, 121)]
    public static void SetupTimeAndZoomHUD()
    {
        // ------------------------------------------------------------------
        // 1. Find or create the GameplayHUD canvas
        // ------------------------------------------------------------------
        Canvas canvas = GetOrCreateCanvas();

        // ------------------------------------------------------------------
        // 2. Top-right clock
        // ------------------------------------------------------------------
        CreateClockDisplay(canvas);

        // ------------------------------------------------------------------
        // 3. ZoomController on Main Camera
        // ------------------------------------------------------------------
        Camera mainCam = Camera.main;
        ZoomController zoomCtrl = null;
        if (mainCam != null)
        {
            zoomCtrl = mainCam.GetComponent<ZoomController>();
            if (zoomCtrl == null)
            {
                zoomCtrl = mainCam.gameObject.AddComponent<ZoomController>();
                zoomCtrl.minZoom = 3f;
                zoomCtrl.maxZoom = 14f;
                zoomCtrl.defaultZoom = mainCam.orthographicSize;
                Debug.Log("[TimeAndZoomHUD] Added ZoomController to Main Camera.");
            }
        }
        else
        {
            Debug.LogWarning("[TimeAndZoomHUD] No Main Camera found — zoom buttons will not be wired.");
        }

        // ------------------------------------------------------------------
        // 4. Bottom-right zoom +/- buttons
        // ------------------------------------------------------------------
        CreateZoomButtons(canvas, zoomCtrl);

        // ------------------------------------------------------------------
        // 5. Save
        // ------------------------------------------------------------------
        EditorUtility.SetDirty(canvas.gameObject);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);

        Debug.Log("[TimeAndZoomHUD] Set up Time display + Zoom controls. " +
                  "Press Play and try: Ctrl+= to zoom in, Ctrl+- to zoom out (hold for smooth).");
    }

    // -------------------------------------------------------------------------
    // Canvas
    // -------------------------------------------------------------------------

    private static Canvas GetOrCreateCanvas()
    {
        GameObject existing = GameObject.Find(CanvasName);
        if (existing != null)
        {
            Canvas c = existing.GetComponent<Canvas>();
            if (c != null) return c;
        }

        GameObject canvasObj = new GameObject(CanvasName);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        Debug.Log("[TimeAndZoomHUD] Created GameplayHUD canvas.");
        return canvas;
    }

    // -------------------------------------------------------------------------
    // Clock display (top-right)
    // -------------------------------------------------------------------------

    private static void CreateClockDisplay(Canvas canvas)
    {
        Transform existing = canvas.transform.Find(ClockName);
        GameObject clockObj;

        if (existing != null)
        {
            clockObj = existing.gameObject;
            Debug.Log("[TimeAndZoomHUD] Clock display already exists — refreshing setup.");
        }
        else
        {
            clockObj = new GameObject(ClockName);
            clockObj.transform.SetParent(canvas.transform, false);
        }

        RectTransform rect = clockObj.GetComponent<RectTransform>();
        if (rect == null) rect = clockObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-20f, -20f);
        rect.sizeDelta = new Vector2(180f, 60f);

        // Add semi-transparent background
        Image bg = clockObj.GetComponent<Image>();
        if (bg == null) bg = clockObj.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.35f);
        bg.raycastTarget = false;

        // GameTimeUI handles the text children
        GameTimeUI timeUI = clockObj.GetComponent<GameTimeUI>();
        if (timeUI == null) timeUI = clockObj.AddComponent<GameTimeUI>();

        Debug.Log("[TimeAndZoomHUD] Clock display ready in top-right.");
    }

    // -------------------------------------------------------------------------
    // Zoom +/- buttons (bottom-right)
    // -------------------------------------------------------------------------

    private static void CreateZoomButtons(Canvas canvas, ZoomController zoomCtrl)
    {
        Transform existing = canvas.transform.Find(ZoomPanelName);
        GameObject panelObj;

        if (existing != null)
        {
            panelObj = existing.gameObject;
            // Clear old children for a clean rebuild
            for (int i = panelObj.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(panelObj.transform.GetChild(i).gameObject);
            }
        }
        else
        {
            panelObj = new GameObject(ZoomPanelName);
            panelObj.transform.SetParent(canvas.transform, false);
        }

        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        if (panelRect == null) panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-20f, 20f);
        panelRect.sizeDelta = new Vector2(56f, 120f);

        // Vertical layout for stacked zoom buttons
        VerticalLayoutGroup vlg = panelObj.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = panelObj.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(4, 4, 4, 4);
        vlg.spacing = 6f;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;

        // Zoom IN button (+)
        GameObject zoomInBtn = CreateButton(panelObj.transform, "ZoomInButton", "+");
        if (zoomCtrl != null)
        {
            zoomInBtn.GetComponent<Button>().onClick.AddListener(zoomCtrl.ZoomIn);
        }

        // Zoom OUT button (-)
        GameObject zoomOutBtn = CreateButton(panelObj.transform, "ZoomOutButton", "−");
        if (zoomCtrl != null)
        {
            zoomOutBtn.GetComponent<Button>().onClick.AddListener(zoomCtrl.ZoomOut);
        }

        // Reset button (⌂)
        GameObject resetBtn = CreateButton(panelObj.transform, "ZoomResetButton", "⌂");
        if (zoomCtrl != null)
        {
            resetBtn.GetComponent<Button>().onClick.AddListener(zoomCtrl.ResetZoom);
        }

        Debug.Log("[TimeAndZoomHUD] Zoom buttons created (bottom-right).");
    }

    private static GameObject CreateButton(Transform parent, string name, string label)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform rect = btnObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(48f, 32f);

        Image bg = btnObj.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.18f, 0.22f, 0.85f);
        bg.raycastTarget = true;

        Button button = btnObj.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 1f);
        colors.highlightedColor = new Color(0.8f, 0.85f, 1f, 1f);
        colors.pressedColor = new Color(0.5f, 0.6f, 0.8f, 1f);
        button.colors = colors;

        // Text child for label
        GameObject textObj = new GameObject("Label");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textObj.AddComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.96f, 0.86f, 1f);
        text.raycastTarget = false;
        LitIsoFont.Apply(text, 26, FontStyle.Bold);

        return btnObj;
    }
}
