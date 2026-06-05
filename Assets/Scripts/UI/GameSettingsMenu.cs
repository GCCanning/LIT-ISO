using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Prototype in-game menu. Press I to open/close.
/// Settings are saved with PlayerPrefs so tuning survives play sessions.
/// </summary>
public class GameSettingsMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    public static bool ShowDebugOverlay { get; private set; }

    private const string KeyVsync = "litiso.settings.vsync";
    private const string KeyFullscreen = "litiso.settings.fullscreen";
    private const string KeyBrightness = "litiso.settings.brightness";
    private const string KeyCameraSmooth = "litiso.settings.cameraSmooth";
    private const string KeyCameraLead = "litiso.settings.cameraLead";
    private const string KeyCameraReverse = "litiso.settings.cameraReverse";
    private const string KeyZoom = "litiso.settings.zoom";
    private const string KeyPixelSnap = "litiso.settings.pixelSnap";
    private const string KeyScrollZoom = "litiso.settings.scrollZoom";
    private const string KeyVignette = "litiso.settings.vignette";
    private const string KeyVignetteStrength = "litiso.settings.vignetteStrength";
    private const string KeyDebugOverlay = "litiso.settings.debugOverlay";

    public KeyCode toggleKey = KeyCode.I;

    [Header("Defaults")]
    [Range(0.6f, 1.4f)] public float brightness = 1f;

    private GameObject menuRoot;
    private Transform contentRoot;
    private Image brightnessOverlay;
    private Camera targetCamera;
    private CameraFollow cameraFollow;
    private ZoomController zoomController;
    private GraphicsEnhancer graphicsEnhancer;
    private Tab currentTab = Tab.Settings;

    private enum Tab
    {
        Settings,
        Camera,
        Controls,
        Debug
    }

    private void Awake()
    {
        EnsureEventSystem();
        AutoWireReferences();
        LoadSettings();
        BuildBrightnessOverlay();
        BuildMenu();
        ApplyBrightness();
        SetMenuVisible(false);
    }

    private void OnDestroy()
    {
        if (IsOpen)
            Time.timeScale = 1f;
        IsOpen = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            SetMenuVisible(menuRoot != null && !menuRoot.activeSelf);
    }

    private void AutoWireReferences()
    {
        targetCamera = Camera.main;
        if (targetCamera == null) return;

        cameraFollow = targetCamera.GetComponent<CameraFollow>();
        zoomController = targetCamera.GetComponent<ZoomController>();
        graphicsEnhancer = targetCamera.GetComponent<GraphicsEnhancer>();
    }

    private void LoadSettings()
    {
        QualitySettings.vSyncCount = PlayerPrefs.GetInt(KeyVsync, QualitySettings.vSyncCount > 0 ? 1 : 0);
        Screen.fullScreen = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;
        brightness = PlayerPrefs.GetFloat(KeyBrightness, brightness);
        ShowDebugOverlay = PlayerPrefs.GetInt(KeyDebugOverlay, ShowDebugOverlay ? 1 : 0) == 1;

        if (cameraFollow != null)
        {
            cameraFollow.smoothDampTime = PlayerPrefs.GetFloat(KeyCameraSmooth, cameraFollow.smoothDampTime);
            cameraFollow.lookaheadDistance = PlayerPrefs.GetFloat(KeyCameraLead, cameraFollow.lookaheadDistance);
            cameraFollow.lookaheadReverseSmoothTime = PlayerPrefs.GetFloat(KeyCameraReverse, cameraFollow.lookaheadReverseSmoothTime);
            cameraFollow.snapToPixelGrid = PlayerPrefs.GetInt(KeyPixelSnap, cameraFollow.snapToPixelGrid ? 1 : 0) == 1;
        }

        if (zoomController != null)
        {
            zoomController.SetZoom(PlayerPrefs.GetFloat(KeyZoom, targetCamera != null ? targetCamera.orthographicSize : zoomController.defaultZoom));
            zoomController.enableScrollWheel = PlayerPrefs.GetInt(KeyScrollZoom, zoomController.enableScrollWheel ? 1 : 0) == 1;
        }
        else if (targetCamera != null)
        {
            targetCamera.orthographicSize = PlayerPrefs.GetFloat(KeyZoom, targetCamera.orthographicSize);
        }

        if (graphicsEnhancer != null)
        {
            graphicsEnhancer.enableVignette = PlayerPrefs.GetInt(KeyVignette, graphicsEnhancer.enableVignette ? 1 : 0) == 1;
            graphicsEnhancer.vignetteStrength = PlayerPrefs.GetFloat(KeyVignetteStrength, graphicsEnhancer.vignetteStrength);
        }
    }

    private void SaveFloat(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();
    }

    private void SaveBool(string key, bool value)
    {
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void BuildBrightnessOverlay()
    {
        GameObject go = new GameObject("BrightnessOverlay", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        go.transform.SetAsFirstSibling();

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        brightnessOverlay = go.AddComponent<Image>();
        brightnessOverlay.raycastTarget = false;
    }

    private void BuildMenu()
    {
        menuRoot = new GameObject("GameMenu", typeof(RectTransform));
        menuRoot.transform.SetParent(transform, false);

        RectTransform rootRect = menuRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image dim = menuRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.50f);

        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(menuRoot.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(660f, 560f);

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.055f, 0.07f, 0.085f, 0.97f);

        VerticalLayoutGroup panelLayout = panel.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(24, 24, 20, 18);
        panelLayout.spacing = 10f;
        panelLayout.childControlWidth = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        AddLabel(panel.transform, "LIT-ISO", 39, new Color(1f, 0.9f, 0.58f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);

        GameObject tabs = new GameObject("Tabs", typeof(RectTransform));
        tabs.transform.SetParent(panel.transform, false);
        tabs.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 38f);
        HorizontalLayoutGroup tabsLayout = tabs.AddComponent<HorizontalLayoutGroup>();
        tabsLayout.spacing = 8f;
        tabsLayout.childAlignment = TextAnchor.MiddleCenter;
        tabsLayout.childControlWidth = false;
        tabsLayout.childForceExpandWidth = false;

        AddTabButton(tabs.transform, "Settings", Tab.Settings);
        AddTabButton(tabs.transform, "Camera", Tab.Camera);
        AddTabButton(tabs.transform, "Controls", Tab.Controls);
        AddTabButton(tabs.transform, "Debug", Tab.Debug);

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(panel.transform, false);
        content.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 385f);
        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 8f;
        contentLayout.childControlWidth = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentRoot = content.transform;

        GameObject footer = new GameObject("Footer", typeof(RectTransform));
        footer.transform.SetParent(panel.transform, false);
        footer.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 42f);
        HorizontalLayoutGroup footerLayout = footer.AddComponent<HorizontalLayoutGroup>();
        footerLayout.spacing = 10f;
        footerLayout.childAlignment = TextAnchor.MiddleCenter;
        footerLayout.childControlWidth = false;
        footerLayout.childForceExpandWidth = false;
        AddCommandButton(footer.transform, "Reset Camera", ResetCamera);
        AddCommandButton(footer.transform, "Close", () => SetMenuVisible(false));

        AddLabel(panel.transform, "Press I to close", 13, new Color(0.72f, 0.78f, 0.84f, 1f), TextAnchor.MiddleCenter, FontStyle.Normal);
        RebuildTab();
    }

    private void RebuildTab()
    {
        if (contentRoot == null) return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        switch (currentTab)
        {
            case Tab.Settings:
                BuildSettingsTab();
                break;
            case Tab.Camera:
                BuildCameraTab();
                break;
            case Tab.Controls:
                BuildControlsTab();
                break;
            case Tab.Debug:
                BuildDebugTab();
                break;
        }
    }

    private void BuildSettingsTab()
    {
        AddToggleRow(contentRoot, "VSync", () => QualitySettings.vSyncCount > 0, value =>
        {
            QualitySettings.vSyncCount = value ? 1 : 0;
            SaveBool(KeyVsync, value);
        });

        AddToggleRow(contentRoot, "Fullscreen", () => Screen.fullScreen, value =>
        {
            Screen.fullScreen = value;
            SaveBool(KeyFullscreen, value);
        });

        AddSliderRow(contentRoot, "Brightness", 0.6f, 1.4f, brightness, value =>
        {
            brightness = value;
            ApplyBrightness();
            SaveFloat(KeyBrightness, value);
        }, value => $"{value:0.00}");

        AddToggleRow(contentRoot, "Vignette", () => graphicsEnhancer != null && graphicsEnhancer.enableVignette, value =>
        {
            if (graphicsEnhancer != null) graphicsEnhancer.enableVignette = value;
            SaveBool(KeyVignette, value);
        });

        AddSliderRow(contentRoot, "Vignette Strength", 0f, 1f, graphicsEnhancer != null ? graphicsEnhancer.vignetteStrength : 0.45f, value =>
        {
            if (graphicsEnhancer != null) graphicsEnhancer.vignetteStrength = value;
            SaveFloat(KeyVignetteStrength, value);
        }, value => $"{value:0.00}");
    }

    private void BuildCameraTab()
    {
        AddSliderRow(contentRoot, "Camera Smooth", 0.05f, 0.55f, cameraFollow != null ? cameraFollow.smoothDampTime : 0.22f, value =>
        {
            if (cameraFollow != null) cameraFollow.smoothDampTime = value;
            SaveFloat(KeyCameraSmooth, value);
        }, value => $"{value:0.00}s");

        AddSliderRow(contentRoot, "Camera Lead", 0f, 1.5f, cameraFollow != null ? cameraFollow.lookaheadDistance : 0.45f, value =>
        {
            if (cameraFollow != null) cameraFollow.lookaheadDistance = value;
            SaveFloat(KeyCameraLead, value);
        }, value => $"{value:0.00}");

        AddSliderRow(contentRoot, "Reverse Damping", 0.05f, 0.6f, cameraFollow != null ? cameraFollow.lookaheadReverseSmoothTime : 0.35f, value =>
        {
            if (cameraFollow != null) cameraFollow.lookaheadReverseSmoothTime = value;
            SaveFloat(KeyCameraReverse, value);
        }, value => $"{value:0.00}s");

        float currentZoom = targetCamera != null ? targetCamera.orthographicSize : 6f;
        AddSliderRow(contentRoot, "Zoom", 3f, 14f, currentZoom, value =>
        {
            ApplyZoom(value);
            SaveFloat(KeyZoom, value);
        }, value => $"{value:0.0}");

        AddToggleRow(contentRoot, "Pixel Snap Camera", () => cameraFollow != null && cameraFollow.snapToPixelGrid, value =>
        {
            if (cameraFollow != null) cameraFollow.snapToPixelGrid = value;
            SaveBool(KeyPixelSnap, value);
        });

        AddToggleRow(contentRoot, "Mouse Wheel Zoom", () => zoomController == null || zoomController.enableScrollWheel, value =>
        {
            if (zoomController != null) zoomController.enableScrollWheel = value;
            SaveBool(KeyScrollZoom, value);
        });

        GameObject presetRow = new GameObject("CameraPresets", typeof(RectTransform));
        presetRow.transform.SetParent(contentRoot, false);
        presetRow.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 38f);
        HorizontalLayoutGroup layout = presetRow.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childForceExpandWidth = false;
        AddCommandButton(presetRow.transform, "Tight", () => ApplyCameraPreset(0.14f, 0.25f, 5f));
        AddCommandButton(presetRow.transform, "Balanced", () => ApplyCameraPreset(0.22f, 0.45f, 6f));
        AddCommandButton(presetRow.transform, "Cinematic", () => ApplyCameraPreset(0.34f, 0.85f, 7.5f));
        AddCommandButton(presetRow.transform, "Debug Wide", () => ApplyCameraPreset(0.18f, 0.15f, 11f));
    }

    private void BuildControlsTab()
    {
        AddInfoRow("Move", "WASD");
        AddInfoRow("Jump", "Space");
        AddInfoRow("Interact", "E / Right Click");
        AddInfoRow("Open Menu", "I");
        AddInfoRow("Zoom", "Ctrl +/- or wheel");
    }

    private void BuildDebugTab()
    {
        AddInfoRow("Camera", targetCamera != null ? $"{targetCamera.orthographicSize:0.0} zoom" : "None");
        AddInfoRow("VSync", QualitySettings.vSyncCount > 0 ? "On" : "Off");
        AddInfoRow("Fullscreen", Screen.fullScreen ? "On" : "Off");
        AddInfoRow("Menu Pauses", "Gameplay input gated");
        AddToggleRow(contentRoot, "Movement Overlay", () => ShowDebugOverlay, value =>
        {
            ShowDebugOverlay = value;
            SaveBool(KeyDebugOverlay, value);
        });
        AddCommandButton(contentRoot, "Clear Saved Settings", () =>
        {
            PlayerPrefs.DeleteKey(KeyVsync);
            PlayerPrefs.DeleteKey(KeyFullscreen);
            PlayerPrefs.DeleteKey(KeyBrightness);
            PlayerPrefs.DeleteKey(KeyCameraSmooth);
            PlayerPrefs.DeleteKey(KeyCameraLead);
            PlayerPrefs.DeleteKey(KeyCameraReverse);
            PlayerPrefs.DeleteKey(KeyZoom);
            PlayerPrefs.DeleteKey(KeyPixelSnap);
            PlayerPrefs.DeleteKey(KeyScrollZoom);
            PlayerPrefs.DeleteKey(KeyVignette);
            PlayerPrefs.DeleteKey(KeyVignetteStrength);
            PlayerPrefs.DeleteKey(KeyDebugOverlay);
            PlayerPrefs.Save();
            LoadSettings();
            ApplyBrightness();
            RebuildTab();
        });
    }

    private void AddTabButton(Transform parent, string label, Tab tab)
    {
        AddCommandButton(parent, label, () =>
        {
            currentTab = tab;
            RebuildTab();
        });
    }

    private Text AddLabel(Transform parent, string value, int fontSize, Color color, TextAnchor anchor, FontStyle style)
    {
        GameObject go = new GameObject(value, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, fontSize + 10f);
        Text text = go.AddComponent<Text>();
        text.text = value;
        text.color = color;
        text.alignment = anchor;
        text.raycastTarget = false;
        LitIsoFont.Apply(text, fontSize, style);
        return text;
    }

    private void AddToggleRow(Transform parent, string label, System.Func<bool> getter, System.Action<bool> setter)
    {
        Text valueText;
        GameObject row = CreateRow(parent, label, out valueText);
        SetValueText(valueText, getter() ? "On" : "Off");
        Button button = row.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.onClick.AddListener(() =>
        {
            bool newValue = !getter();
            setter(newValue);
            SetValueText(valueText, newValue ? "On" : "Off");
        });
    }

    private void AddInfoRow(string label, string value)
    {
        Text valueText;
        CreateRow(contentRoot, label, out valueText);
        SetValueText(valueText, value);
    }

    private void AddSliderRow(
        Transform parent,
        string label,
        float min,
        float max,
        float current,
        System.Action<float> setter,
        System.Func<float, string> formatter)
    {
        Text valueText;
        GameObject row = CreateRow(parent, label, out valueText);

        GameObject sliderObject = new GameObject("Slider", typeof(RectTransform));
        sliderObject.transform.SetParent(row.transform, false);
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(260f, 24f);

        Slider slider = sliderObject.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = Mathf.Clamp(current, min, max);

        Image background = AddSliderImage(sliderObject.transform, "Background", new Color(0.12f, 0.15f, 0.18f, 1f), out RectTransform bgRect);
        slider.targetGraphic = background;

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(4f, 4f);
        fillAreaRect.offsetMax = new Vector2(-4f, -4f);

        AddSliderImage(fillArea.transform, "Fill", new Color(0.85f, 0.65f, 0.30f, 1f), out RectTransform fillRect);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        slider.fillRect = fillRect;

        GameObject handle = new GameObject("Handle", typeof(RectTransform));
        handle.transform.SetParent(sliderObject.transform, false);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(14f, 24f);
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = new Color(0.96f, 0.90f, 0.70f, 1f);
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImage;

        SetValueText(valueText, formatter(slider.value));
        slider.onValueChanged.AddListener(value =>
        {
            setter(value);
            SetValueText(valueText, formatter(value));
        });
    }

    private Image AddSliderImage(Transform parent, string name, Color color, out RectTransform rect)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private GameObject CreateRow(Transform parent, string label, out Text valueText)
    {
        GameObject row = new GameObject(label + "Row", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        row.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 36f);
        Image rowImage = row.AddComponent<Image>();
        rowImage.color = new Color(1f, 1f, 1f, 0.001f);

        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childForceExpandWidth = false;

        Text labelText = AddLabel(row.transform, label, 13, new Color(0.88f, 0.90f, 0.86f, 1f), TextAnchor.MiddleLeft, FontStyle.Normal);
        labelText.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 32f);

        valueText = AddLabel(row.transform, "", 13, new Color(1f, 0.86f, 0.50f, 1f), TextAnchor.MiddleRight, FontStyle.Bold);
        valueText.GetComponent<RectTransform>().sizeDelta = new Vector2(110f, 32f);

        return row;
    }

    private void AddCommandButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 34f);
        Image image = go.AddComponent<Image>();
        image.color = new Color(0.16f, 0.19f, 0.22f, 1f);
        Button button = go.AddComponent<Button>();
        button.onClick.AddListener(action);

        Text text = AddLabel(go.transform, label, 13, new Color(1f, 0.92f, 0.72f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private void ApplyCameraPreset(float smooth, float lead, float zoom)
    {
        if (cameraFollow != null)
        {
            cameraFollow.smoothDampTime = smooth;
            cameraFollow.lookaheadDistance = lead;
            SaveFloat(KeyCameraSmooth, smooth);
            SaveFloat(KeyCameraLead, lead);
            cameraFollow.SnapToTarget();
        }

        ApplyZoom(zoom);
        SaveFloat(KeyZoom, zoom);
        RebuildTab();
    }

    private void ResetCamera()
    {
        ApplyCameraPreset(0.22f, 0.45f, 6f);
    }

    private void ApplyZoom(float value)
    {
        if (zoomController != null) zoomController.SetZoom(value);
        else if (targetCamera != null) targetCamera.orthographicSize = value;
    }

    private void SetMenuVisible(bool visible)
    {
        if (menuRoot != null)
            menuRoot.SetActive(visible);

        IsOpen = visible;
        Time.timeScale = visible ? 0f : 1f;
    }

    private void ApplyBrightness()
    {
        if (brightnessOverlay == null) return;

        if (brightness < 1f)
            brightnessOverlay.color = new Color(0f, 0f, 0f, Mathf.InverseLerp(1f, 0.6f, brightness) * 0.45f);
        else
            brightnessOverlay.color = new Color(1f, 1f, 1f, Mathf.InverseLerp(1f, 1.4f, brightness) * 0.16f);
    }

    private void SetValueText(Text text, string value)
    {
        if (text != null) text.text = value;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }
}
