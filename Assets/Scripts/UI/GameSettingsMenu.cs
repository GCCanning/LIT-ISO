// PHASE_COMPLETE Phase6
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// In-game Settings panel (press I). Redesigned to match the LIT-ISO front-end
/// spec: two-column layout (Audio/Display left, Controls right), gold toggle
/// indicators, minimal sliders with integer value labels, DEFAULTS + SAVE & CLOSE
/// footer. Settings apply immediately via PlayerPrefs; SAVE & CLOSE dismisses.
/// </summary>
public class GameSettingsMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    public static bool ShowDebugOverlay { get; private set; }

    // ── PlayerPrefs keys ────────────────────────────────────────────────────
    private const string KeyVsync           = "litiso.settings.vsync";
    private const string KeyFullscreen      = "litiso.settings.fullscreen";
    private const string KeyBrightness      = "litiso.settings.brightness";
    private const string KeyCameraSmooth    = "litiso.settings.cameraSmooth";
    private const string KeyCameraLead      = "litiso.settings.cameraLead";
    private const string KeyCameraReverse   = "litiso.settings.cameraReverse";
    private const string KeyZoom            = "litiso.settings.zoom";
    private const string KeyPixelSnap       = "litiso.settings.pixelSnap";
    private const string KeyScrollZoom      = "litiso.settings.scrollZoom";
    private const string KeyVignette        = "litiso.settings.vignette";
    private const string KeyVignetteStrength = "litiso.settings.vignetteStrength";
    private const string KeyDebugOverlay    = "litiso.settings.debugOverlay";
    private const string KeyMasterVolume    = "vol_master";
    private const string KeyMusicVolume     = "vol_music";
    private const string KeySfxVolume       = "vol_sfx";
    private const string KeyScreenShake     = "fx.screenshake";
    private const string KeyDamageNumbers   = "fx.damagenumbers";
    private const string KeyCrtScanlines    = "fx.crtscanlines";
    private const string KeyHudScale        = "hud.scale";

    public KeyCode toggleKey = KeyCode.I;

    [Header("Defaults")]
    [Range(0.6f, 1.4f)] public float brightness = 1f;

    private GameObject menuRoot;
    private Image brightnessOverlay;
    private Camera targetCamera;
    private CameraFollow cameraFollow;
    private ZoomController zoomController;
    private GraphicsEnhancer graphicsEnhancer;

    // ── Lifecycle ────────────────────────────────────────────────────────────

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
        if (IsOpen) Time.timeScale = 1f;
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
        cameraFollow      = targetCamera.GetComponent<CameraFollow>();
        zoomController    = targetCamera.GetComponent<ZoomController>();
        graphicsEnhancer  = targetCamera.GetComponent<GraphicsEnhancer>();
    }

    private void LoadSettings()
    {
        QualitySettings.vSyncCount = PlayerPrefs.GetInt(KeyVsync, 0);
        Screen.fullScreen = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;
        brightness = PlayerPrefs.GetFloat(KeyBrightness, brightness);
        ShowDebugOverlay = PlayerPrefs.GetInt(KeyDebugOverlay, 0) == 1;
        AudioListener.volume = PlayerPrefs.GetFloat(KeyMasterVolume, 1f);

        if (cameraFollow != null)
        {
            cameraFollow.smoothDampTime             = PlayerPrefs.GetFloat(KeyCameraSmooth, cameraFollow.smoothDampTime);
            cameraFollow.lookaheadDistance          = PlayerPrefs.GetFloat(KeyCameraLead, cameraFollow.lookaheadDistance);
            cameraFollow.lookaheadReverseSmoothTime = PlayerPrefs.GetFloat(KeyCameraReverse, cameraFollow.lookaheadReverseSmoothTime);
            cameraFollow.snapToPixelGrid            = PlayerPrefs.GetInt(KeyPixelSnap, cameraFollow.snapToPixelGrid ? 1 : 0) == 1;
        }
        if (zoomController != null)
        {
            zoomController.SetZoom(PlayerPrefs.GetFloat(KeyZoom,
                targetCamera != null ? targetCamera.orthographicSize : zoomController.defaultZoom));
            zoomController.enableScrollWheel = PlayerPrefs.GetInt(KeyScrollZoom, zoomController.enableScrollWheel ? 1 : 0) == 1;
        }
        else if (targetCamera != null)
        {
            targetCamera.orthographicSize = PlayerPrefs.GetFloat(KeyZoom, targetCamera.orthographicSize);
        }
        if (graphicsEnhancer != null)
        {
            graphicsEnhancer.enableVignette    = PlayerPrefs.GetInt(KeyVignette, graphicsEnhancer.enableVignette ? 1 : 0) == 1;
            graphicsEnhancer.vignetteStrength  = PlayerPrefs.GetFloat(KeyVignetteStrength, graphicsEnhancer.vignetteStrength);
        }
    }

    private void SaveFloat(string key, float value) { PlayerPrefs.SetFloat(key, value); PlayerPrefs.Save(); }
    private void SaveBool(string key, bool value)   { PlayerPrefs.SetInt(key, value ? 1 : 0); PlayerPrefs.Save(); }

    // ── Brightness overlay ───────────────────────────────────────────────────

    private void BuildBrightnessOverlay()
    {
        var go  = new GameObject("BrightnessOverlay", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        go.transform.SetAsFirstSibling();
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        brightnessOverlay = go.AddComponent<Image>();
        brightnessOverlay.raycastTarget = false;
    }

    private void ApplyBrightness()
    {
        if (brightnessOverlay == null) return;
        if (brightness < 1f)
            brightnessOverlay.color = new Color(0f, 0f, 0f, Mathf.InverseLerp(1f, 0.6f, brightness) * 0.45f);
        else
            brightnessOverlay.color = new Color(1f, 1f, 1f, Mathf.InverseLerp(1f, 1.4f, brightness) * 0.16f);
    }

    // ── Menu construction ────────────────────────────────────────────────────

    private void BuildMenu()
    {
        menuRoot = new GameObject("GameMenu", typeof(RectTransform));
        menuRoot.transform.SetParent(transform, false);
        var rootRt = menuRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;

        var dim = menuRoot.AddComponent<Image>();
        dim.color = LitIsoTheme.ShadowBlack;

        // Panel: 740×640, Stone frame + corner studs.
        const float pW = 740f, pH = 640f;
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(menuRoot.transform, false);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(pW, pH);

        var panelImg = panel.AddComponent<Image>();
        LitIsoTheme.StyleFrame(panelImg, LitIsoTheme.FrameStyle.Stone);
        AddStuds(panelRt);

        const float headerH = 64f, footerH = 70f, divH = 1f;
        const float padX = 28f;

        // Header.
        BuildSettingsHeader(panelRt, pW, headerH, padX);

        // Divider under header.
        CreateDiv(panelRt, pW, headerH);

        // Body: two columns.
        float bodyY = headerH + divH;
        float bodyH = pH - headerH - footerH - divH * 2f;
        BuildSettingsBody(panelRt, pW, bodyH, bodyY, padX);

        // Divider above footer.
        CreateDiv(panelRt, pW, headerH + divH + bodyH);

        // Footer.
        float footerY = pH - footerH;
        BuildSettingsFooter(panelRt, pW, footerH, footerY, padX);
    }

    private void BuildSettingsHeader(RectTransform panel, float pW, float headerH, float padX)
    {
        // Diamond bullet.
        var diam = NewImage(panel, "Diamond", LitIsoTheme.Gold);
        diam.raycastTarget = false;
        var dr = diam.rectTransform;
        dr.anchorMin = dr.anchorMax = new Vector2(0f, 1f); dr.pivot = new Vector2(0.5f, 0.5f);
        dr.anchoredPosition = new Vector2(padX + 8f, -headerH * 0.5f);
        dr.sizeDelta = new Vector2(11f, 11f);
        dr.localRotation = Quaternion.Euler(0f, 0f, 45f);

        // SETTINGS title.
        var title = NewText(panel, "SETTINGS", 22, LitIsoTheme.Gold, TextAnchor.MiddleLeft, true);
        var tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0f, 1f);
        tr.anchoredPosition = new Vector2(padX + 22f, 0f);
        tr.sizeDelta = new Vector2(pW - padX * 2f - 60f, headerH);

        // Close button.
        var closeBg = NewImage(panel, "Close", LitIsoTheme.Raised);
        LitIsoTheme.StyleButton(closeBg.gameObject.AddComponent<Button>(), closeBg, LitIsoTheme.ButtonStyle.Stone);
        closeBg.GetComponent<Button>().onClick.AddListener(() => SetMenuVisible(false));
        var cr = closeBg.rectTransform;
        cr.anchorMin = cr.anchorMax = new Vector2(1f, 1f); cr.pivot = new Vector2(1f, 1f);
        cr.anchoredPosition = new Vector2(-padX, -headerH * 0.5f + 20f);
        cr.sizeDelta = new Vector2(38f, 38f);
        var closeT = NewText(cr, "X", 14, LitIsoTheme.Parchment, TextAnchor.MiddleCenter, false);
        var ctr = closeT.rectTransform; ctr.anchorMin = Vector2.zero; ctr.anchorMax = Vector2.one;
        ctr.offsetMin = ctr.offsetMax = Vector2.zero;
    }

    private void BuildSettingsBody(RectTransform panel, float pW, float bodyH, float bodyY, float padX)
    {
        var body = new GameObject("Body", typeof(RectTransform));
        body.transform.SetParent(panel, false);
        var br = body.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0f, 1f); br.anchorMax = new Vector2(1f, 1f);
        br.pivot = new Vector2(0.5f, 1f);
        br.anchoredPosition = new Vector2(0f, -bodyY);
        br.sizeDelta = new Vector2(0f, bodyH);
        var bodyLayout = body.AddComponent<HorizontalLayoutGroup>();
        bodyLayout.padding = new RectOffset((int)padX, (int)padX, 16, 16);
        bodyLayout.spacing = 30f;
        bodyLayout.childControlWidth = true; bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = false; bodyLayout.childForceExpandHeight = true;

        BuildLeftColumn(body.transform);
        BuildRightColumn(body.transform);
    }

    private void BuildLeftColumn(Transform body)
    {
        var col = new GameObject("LeftCol", typeof(RectTransform));
        col.transform.SetParent(body, false);
        col.AddComponent<LayoutElement>().preferredWidth = 330f;
        var v = col.AddComponent<VerticalLayoutGroup>();
        v.spacing = 4f; v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;

        // AUDIO section.
        AddSectionHeader(col.transform, "AUDIO");
        AddDesignSlider(col.transform, "Master", 0, 100,
            Mathf.RoundToInt(PlayerPrefs.GetFloat(KeyMasterVolume, 1f) * 100f),
            v2 => { AudioListener.volume = v2 / 100f; SaveFloat(KeyMasterVolume, v2 / 100f); });
        AddDesignSlider(col.transform, "SFX", 0, 100,
            Mathf.RoundToInt(PlayerPrefs.GetFloat(KeySfxVolume, 1f) * 100f),
            v2 => SaveFloat(KeySfxVolume, v2 / 100f));
        AddDesignSlider(col.transform, "Music", 0, 100,
            Mathf.RoundToInt(PlayerPrefs.GetFloat(KeyMusicVolume, 0.6f) * 100f),
            v2 => SaveFloat(KeyMusicVolume, v2 / 100f));

        // Spacer.
        AddSpacer(col.transform, 10f);

        // DISPLAY section.
        AddSectionHeader(col.transform, "DISPLAY");
        AddIconToggle(col.transform, "Fullscreen",
            () => Screen.fullScreen,
            b => { Screen.fullScreen = b; SaveBool(KeyFullscreen, b); });
        AddIconToggle(col.transform, "V-Sync",
            () => QualitySettings.vSyncCount > 0,
            b => { QualitySettings.vSyncCount = b ? 1 : 0; SaveBool(KeyVsync, b); });
        AddIconToggle(col.transform, "Screen Shake",
            () => PlayerPrefs.GetInt(KeyScreenShake, 1) == 1,
            b => SaveBool(KeyScreenShake, b));
        AddIconToggle(col.transform, "Damage Numbers",
            () => PlayerPrefs.GetInt(KeyDamageNumbers, 1) == 1,
            b => SaveBool(KeyDamageNumbers, b));
        AddIconToggle(col.transform, "CRT Scanlines",
            () => PlayerPrefs.GetInt(KeyCrtScanlines, 0) == 1,
            b => SaveBool(KeyCrtScanlines, b));
    }

    private void BuildRightColumn(Transform body)
    {
        var col = new GameObject("RightCol", typeof(RectTransform));
        col.transform.SetParent(body, false);
        col.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var v = col.AddComponent<VerticalLayoutGroup>();
        v.spacing = 6f; v.childControlWidth = true; v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;

        AddSectionHeader(col.transform, "CONTROLS");
        AddControlRow(col.transform, "Move",      new[] { "W", "A", "S", "D" });
        AddControlRow(col.transform, "Jump",       new[] { "SPACE" });
        AddControlRow(col.transform, "Interact",   new[] { "E" });
        AddControlRow(col.transform, "Inventory",  new[] { "TAB" });
        AddControlRow(col.transform, "Map",        new[] { "M" });
        AddControlRow(col.transform, "Sprint",     new[] { "SHIFT" });
    }

    private void BuildSettingsFooter(RectTransform panel, float pW, float footerH,
        float footerY, float padX)
    {
        var footer = new GameObject("Footer", typeof(RectTransform));
        footer.transform.SetParent(panel, false);
        var fr = footer.GetComponent<RectTransform>();
        fr.anchorMin = new Vector2(0f, 1f); fr.anchorMax = new Vector2(1f, 1f);
        fr.pivot = new Vector2(0.5f, 1f);
        fr.anchoredPosition = new Vector2(0f, -footerY);
        fr.sizeDelta = new Vector2(0f, footerH);
        var layout = footer.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset((int)padX, (int)padX, (int)((footerH - 48f) * 0.5f), (int)((footerH - 48f) * 0.5f));
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childControlWidth = false; layout.childForceExpandWidth = false;
        layout.childControlHeight = true; layout.childForceExpandHeight = true;

        // Flexible left spacer.
        var spacer = new GameObject("Spacer", typeof(RectTransform));
        spacer.transform.SetParent(footer.transform, false);
        spacer.AddComponent<LayoutElement>().flexibleWidth = 1f;

        // DEFAULTS button.
        var defBtn = LitIsoTheme.NewButton(footer.transform, "Defaults", "DEFAULTS", 11,
            LitIsoTheme.ButtonStyle.Stone);
        defBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(180f, 48f);
        defBtn.onClick.AddListener(ResetToDefaults);

        // SAVE & CLOSE button.
        var saveBtn = LitIsoTheme.NewButton(footer.transform, "SaveClose", "SAVE & CLOSE", 11,
            LitIsoTheme.ButtonStyle.Gold);
        saveBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 48f);
        saveBtn.onClick.AddListener(() => SetMenuVisible(false));
    }

    // ── Row builders ─────────────────────────────────────────────────────────

    private void AddSectionHeader(Transform parent, string label)
    {
        var go = new GameObject(label + "Header", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 28f);
        go.AddComponent<LayoutElement>().minHeight = 28f;

        var t = NewText(go.transform, label, 11, LitIsoTheme.Gold, TextAnchor.MiddleLeft, true);
        LitIsoTheme.ApplyDisplay(t, 11, LitIsoTheme.Gold);
        var tr = t.rectTransform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(0f, 0f); tr.offsetMax = Vector2.zero;
    }

    /// <summary>Horizontal slider with label on the left and integer value top-right.</summary>
    private void AddDesignSlider(Transform parent, string label, int min, int max, int current,
        System.Action<float> setter)
    {
        var block = new GameObject(label + "Slider", typeof(RectTransform));
        block.transform.SetParent(parent, false);
        block.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 48f);
        block.AddComponent<LayoutElement>().minHeight = 48f;

        // Label.
        var lbl = NewText(block.transform, label, 16, LitIsoTheme.Parchment, TextAnchor.UpperLeft, false);
        LitIsoTheme.ApplyBody(lbl, 16, LitIsoTheme.Parchment);
        var lr = lbl.rectTransform;
        lr.anchorMin = new Vector2(0f, 1f); lr.anchorMax = new Vector2(1f, 1f);
        lr.pivot = new Vector2(0f, 1f);
        lr.anchoredPosition = new Vector2(0f, -2f); lr.sizeDelta = new Vector2(-40f, 22f);

        // Value label (top-right, gold).
        var val = NewText(block.transform, current.ToString(), 13, LitIsoTheme.Gold, TextAnchor.UpperRight, false);
        LitIsoTheme.ApplyBody(val, 13, LitIsoTheme.Gold);
        var vr = val.rectTransform;
        vr.anchorMin = new Vector2(1f, 1f); vr.anchorMax = new Vector2(1f, 1f);
        vr.pivot = new Vector2(1f, 1f);
        vr.anchoredPosition = new Vector2(0f, -2f); vr.sizeDelta = new Vector2(36f, 20f);

        // Slider track.
        var sliderGo = new GameObject("Track", typeof(RectTransform));
        sliderGo.transform.SetParent(block.transform, false);
        var sliderRt = sliderGo.GetComponent<RectTransform>();
        sliderRt.anchorMin = new Vector2(0f, 0f); sliderRt.anchorMax = new Vector2(1f, 0f);
        sliderRt.pivot = new Vector2(0.5f, 0f);
        sliderRt.anchoredPosition = new Vector2(0f, 6f);
        sliderRt.sizeDelta = new Vector2(0f, 6f);

        var slider = sliderGo.AddComponent<Slider>();
        slider.minValue = min; slider.maxValue = max;
        slider.value = Mathf.Clamp(current, min, max);
        slider.wholeNumbers = true;

        // Background.
        var bg = new GameObject("Bg", typeof(RectTransform)); bg.transform.SetParent(sliderGo.transform, false);
        var bgImg = bg.AddComponent<Image>(); bgImg.color = LitIsoTheme.Base;
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

        // Fill area.
        var fa = new GameObject("FillArea", typeof(RectTransform)); fa.transform.SetParent(sliderGo.transform, false);
        var far = fa.GetComponent<RectTransform>();
        far.anchorMin = Vector2.zero; far.anchorMax = Vector2.one;
        far.offsetMin = far.offsetMax = Vector2.zero;

        var fill = new GameObject("Fill", typeof(RectTransform)); fill.transform.SetParent(fa.transform, false);
        var fillImg = fill.AddComponent<Image>(); fillImg.color = LitIsoTheme.Gold;
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
        slider.fillRect = fillRt;

        // Handle.
        var handle = new GameObject("Handle", typeof(RectTransform)); handle.transform.SetParent(sliderGo.transform, false);
        var handleImg = handle.AddComponent<Image>(); handleImg.color = LitIsoTheme.GoldLit;
        var handleRt = handle.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(10f, 18f);
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImg;

        val.text = ((int)slider.value).ToString();
        slider.onValueChanged.AddListener(v =>
        {
            val.text = ((int)v).ToString();
            setter(v);
        });
    }

    /// <summary>Row with label on the left and a gold indicator rect on the right.</summary>
    private void AddIconToggle(Transform parent, string label,
        System.Func<bool> getter, System.Action<bool> setter)
    {
        var row = new GameObject(label + "Toggle", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        row.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 34f);
        row.AddComponent<LayoutElement>().minHeight = 34f;
        var bg = row.AddComponent<Image>(); bg.color = new Color(1f, 1f, 1f, 0.001f);

        // Label.
        var lbl = NewText(row.transform, label, 16, LitIsoTheme.Parchment, TextAnchor.MiddleLeft, false);
        LitIsoTheme.ApplyBody(lbl, 16, LitIsoTheme.Parchment);
        var lr = lbl.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = new Vector2(1f, 1f);
        lr.offsetMin = Vector2.zero; lr.offsetMax = new Vector2(-54f, 0f);

        // Indicator rect.
        var ind = new GameObject("Ind", typeof(RectTransform)); ind.transform.SetParent(row.transform, false);
        var indImg = ind.AddComponent<Image>();
        var ir = ind.GetComponent<RectTransform>();
        ir.anchorMin = new Vector2(1f, 0.5f); ir.anchorMax = new Vector2(1f, 0.5f);
        ir.pivot = new Vector2(1f, 0.5f);
        ir.anchoredPosition = Vector2.zero; ir.sizeDelta = new Vector2(44f, 22f);

        System.Action paint = () =>
        {
            bool on = getter();
            indImg.color = on ? LitIsoTheme.Gold : LitIsoTheme.Hex("#23262E");
        };
        paint();

        var btn = row.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(() => { setter(!getter()); paint(); });
    }

    /// <summary>Row showing a key binding with key chips on the right.</summary>
    private static void AddControlRow(Transform parent, string action, string[] keys)
    {
        var row = new GameObject(action + "Row", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        row.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 42f);
        row.AddComponent<LayoutElement>().minHeight = 42f;
        var rowImg = row.AddComponent<Image>();
        rowImg.color = LitIsoTheme.Panel2;

        // Action label.
        var lbl = NewText(row.transform, action, 16, LitIsoTheme.Parchment, TextAnchor.MiddleLeft, false);
        LitIsoTheme.ApplyBody(lbl, 16, LitIsoTheme.Parchment);
        var lr = lbl.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = new Vector2(0.5f, 1f);
        lr.offsetMin = new Vector2(14f, 0f); lr.offsetMax = Vector2.zero;

        // Key chips — laid out from the right edge inward.
        float chipX = -12f;
        for (int i = keys.Length - 1; i >= 0; i--)
        {
            string key = keys[i];
            float chipW = Mathf.Max(34f, key.Length * 10f + 18f);

            var chip = new GameObject("Key_" + key, typeof(RectTransform));
            chip.transform.SetParent(row.transform, false);
            var chipImg = chip.AddComponent<Image>();
            chipImg.color = LitIsoTheme.Hex("#23262E");
            var ol = chip.AddComponent<Outline>();
            ol.effectColor = LitIsoTheme.Stone; ol.effectDistance = new Vector2(1.5f, -1.5f); ol.useGraphicAlpha = false;
            var cr = chip.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(1f, 0.5f); cr.anchorMax = new Vector2(1f, 0.5f);
            cr.pivot = new Vector2(1f, 0.5f);
            cr.anchoredPosition = new Vector2(chipX, 0f); cr.sizeDelta = new Vector2(chipW, 26f);
            chipX -= chipW + 6f;

            var keyT = NewText(chip.transform, key, 11, LitIsoTheme.Parchment, TextAnchor.MiddleCenter, false);
            LitIsoTheme.ApplyBody(keyT, 11, LitIsoTheme.Parchment);
            keyT.rectTransform.anchorMin = Vector2.zero; keyT.rectTransform.anchorMax = Vector2.one;
            keyT.rectTransform.offsetMin = keyT.rectTransform.offsetMax = Vector2.zero;
        }
    }

    private static void AddSpacer(Transform parent, float h)
    {
        var go = new GameObject("Spacer", typeof(RectTransform)); go.transform.SetParent(parent, false);
        go.AddComponent<LayoutElement>().minHeight = h;
    }

    private void CreateDiv(RectTransform panel, float pW, float y)
    {
        var go = new GameObject("Div", typeof(RectTransform)); go.transform.SetParent(panel, false);
        var img = go.AddComponent<Image>(); img.color = LitIsoTheme.Stone; img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -y); rt.sizeDelta = new Vector2(0f, 1f);
    }

    // ── DEFAULTS ─────────────────────────────────────────────────────────────

    private void ResetToDefaults()
    {
        // Reset to factory defaults.
        PlayerPrefs.SetFloat(KeyMasterVolume, 1f);  AudioListener.volume = 1f;
        PlayerPrefs.SetFloat(KeySfxVolume, 1f);
        PlayerPrefs.SetFloat(KeyMusicVolume, 0.6f);
        PlayerPrefs.SetInt(KeyVsync, 0);            QualitySettings.vSyncCount = 0;
        PlayerPrefs.SetInt(KeyScreenShake, 1);
        PlayerPrefs.SetInt(KeyDamageNumbers, 1);
        PlayerPrefs.SetInt(KeyCrtScanlines, 0);
        PlayerPrefs.Save();

        // Rebuild the menu to reflect new values.
        if (menuRoot != null) Destroy(menuRoot);
        BuildMenu();
        menuRoot.SetActive(true);
    }

    // ── Visibility ────────────────────────────────────────────────────────────

    private void SetMenuVisible(bool visible)
    {
        if (menuRoot != null) menuRoot.SetActive(visible);
        IsOpen = visible;
        Time.timeScale = visible ? 0f : 1f;
    }

    // ── Corner studs (positioned on a non-layout layer) ──────────────────────

    private void AddStuds(RectTransform panelRt)
    {
        // Overlay stretches with panel but has ignoreLayout so it doesn't interfere.
        var overlay = new GameObject("StudOverlay", typeof(RectTransform));
        overlay.transform.SetParent(panelRt, false);
        var le = overlay.AddComponent<LayoutElement>(); le.ignoreLayout = true;
        var ort = overlay.GetComponent<RectTransform>();
        ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one;
        ort.offsetMin = ort.offsetMax = Vector2.zero;

        var anchors  = new[] { new Vector2(0f,1f), new Vector2(1f,1f), new Vector2(0f,0f), new Vector2(1f,0f) };
        var offsets  = new[] { new Vector2(10f,-10f), new Vector2(-10f,-10f), new Vector2(10f,10f), new Vector2(-10f,10f) };
        for (int i = 0; i < 4; i++)
        {
            var go  = new GameObject("Stud", typeof(RectTransform));
            go.transform.SetParent(overlay.transform, false);
            var rt  = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchors[i];
            rt.pivot = anchors[i];
            rt.sizeDelta = new Vector2(10f, 10f);
            rt.anchoredPosition = offsets[i];
            rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var img = go.AddComponent<Image>(); img.color = LitIsoTheme.Gold; img.raycastTarget = false;
        }
    }

    // ── Static helpers ───────────────────────────────────────────────────────

    private static Text NewText(Transform parent, string value, int size, Color color,
        TextAnchor anchor, bool display)
    {
        var go = new GameObject(value, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = value; t.color = color; t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow   = VerticalWrapMode.Truncate;
        if (display) LitIsoTheme.ApplyDisplay(t, size, color);
        else         LitIsoTheme.ApplyBody(t, size, color);
        return t;
    }

    private static Image NewImage(Transform parent, string name, Color color)
    {
        var go  = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>(); img.color = color;
        return img;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private void ApplyZoom(float value)
    {
        if (zoomController != null) zoomController.SetZoom(value);
        else if (targetCamera != null) targetCamera.orthographicSize = value;
    }
}
