// PHASE_COMPLETE Phase6
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Esc-toggled pause overlay built entirely in code (uGUI). Pauses the game (timeScale=0),
    /// offers Resume / Save / Quit-to-Menu, exposes Master/Music/SFX/UI-scale sliders.
    ///
    /// Visual grammar: 3px ink border + 1px steel inset + 1px gold hairline + gold corner studs.
    /// Buttons use LitIsoTheme.StyleButton (6px hard bottom shadow, press offset).
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        CanvasGroup _group;
        RectTransform _panelRoot;
        bool _open;
        FoundationBootstrap _bootstrap;
        FoundationInteractionOverlay _overlay;

        const string TextScalePrefKey = "ui.textScale";

        void Awake()
        {
            _bootstrap = GetComponent<FoundationBootstrap>();
            _overlay   = GetComponent<FoundationInteractionOverlay>();
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("vol_master", 1f));
            EnsureEventSystem();
            Build();
            SetOpen(false);
        }

        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            var ui = FoundationUiCoordinator.Active;
            if (!_open && ui != null && !ui.CanOpenPause()) return;
            ui?.ConsumeInputThisFrame();
            SetOpen(!_open);
        }

        void SetOpen(bool open)
        {
            _open = open;
            if (_group != null)
            {
                _group.alpha          = open ? 1f : 0f;
                _group.interactable   = open;
                _group.blocksRaycasts = open;
            }
            FoundationUiCoordinator.Active?.SetModalOpen("pause", open);
            Time.timeScale = open ? 0f : 1f;
        }

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        static float CurrentTextScale() =>
            Mathf.Clamp(PlayerPrefs.GetFloat(TextScalePrefKey, 1.08f), 0.8f, 1.45f);

        // ---------------------------------------------------------------- build

        void Build()
        {
            var canvasGo = new GameObject("PauseCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            _group = canvasGo.AddComponent<CanvasGroup>();

            // ---- Full-screen modal scrim ----
            var scrimGo = new GameObject("Scrim", typeof(RectTransform));
            scrimGo.transform.SetParent(canvasGo.transform, false);
            var scrimImg = scrimGo.AddComponent<Image>();
            scrimImg.color = LitIsoTheme.ModalScrim;
            Stretch(scrimImg.rectTransform);

            // ---- Stone card ----
            var cardGo = new GameObject("PauseCard", typeof(RectTransform));
            cardGo.transform.SetParent(canvasGo.transform, false);
            var cardImg = cardGo.AddComponent<Image>();
            cardImg.color = LitIsoTheme.Panel;
            _panelRoot = cardImg.rectTransform;
            _panelRoot.anchorMin = _panelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRoot.pivot     = new Vector2(0.5f, 0.5f);
            _panelRoot.sizeDelta = new Vector2(480f, 660f);
            ApplyUiScale(PlayerPrefs.GetFloat("ui.scale", 1f));

            // 3px ink border
            var inkOutline = cardGo.AddComponent<Outline>();
            inkOutline.effectColor    = LitIsoTheme.Base;
            inkOutline.effectDistance = new Vector2(3f, -3f);
            inkOutline.useGraphicAlpha = false;

            // 1px steel inset
            AddInsetLayer(_panelRoot, LitIsoTheme.Stone, 0.6f, 3f, 1f);

            // 1px gold hairline
            AddInsetLayer(_panelRoot, LitIsoTheme.GoldDeep, 0.5f, 5f, 1f);

            // Gold corner studs
            AddCornerStud(_panelRoot, new Vector2(0f, 1f), new Vector2(8f, -8f));
            AddCornerStud(_panelRoot, new Vector2(1f, 1f), new Vector2(-8f, -8f));
            AddCornerStud(_panelRoot, new Vector2(0f, 0f), new Vector2(8f, 8f));
            AddCornerStud(_panelRoot, new Vector2(1f, 0f), new Vector2(-8f, 8f));

            // 6px hard bottom shadow behind the card
            var shadowGo = new GameObject("CardShadow", typeof(RectTransform));
            shadowGo.transform.SetParent(canvasGo.transform, false);
            var shadowImg = shadowGo.AddComponent<Image>();
            shadowImg.color = LitIsoTheme.Base;
            shadowImg.raycastTarget = false;
            var shadowRt = shadowImg.rectTransform;
            shadowRt.anchorMin = _panelRoot.anchorMin; shadowRt.anchorMax = _panelRoot.anchorMax;
            shadowRt.pivot     = _panelRoot.pivot;
            shadowRt.anchoredPosition = _panelRoot.anchoredPosition + new Vector2(0f, -6f);
            shadowRt.sizeDelta = _panelRoot.sizeDelta;
            shadowGo.transform.SetSiblingIndex(cardGo.transform.GetSiblingIndex());

            // ---- "PAUSED" heading ----
            float y = 280f;
            var heading = NewText(_panelRoot, "Heading", "PAUSED", 26, TextAnchor.MiddleCenter);
            LitIsoTheme.ApplyDisplay(heading, 26, LitIsoTheme.Gold);
            SetCentred(heading.rectTransform, new Vector2(0f, y), new Vector2(420f, 40f)); y -= 50f;

            // Gold divider below heading
            var div = new GameObject("Divider", typeof(RectTransform));
            div.transform.SetParent(_panelRoot, false);
            var divImg = div.AddComponent<Image>();
            divImg.color = new Color(LitIsoTheme.Gold.r, LitIsoTheme.Gold.g, LitIsoTheme.Gold.b, 0.30f);
            divImg.raycastTarget = false;
            var divRt = divImg.rectTransform;
            divRt.anchorMin = new Vector2(0.05f, 0.5f); divRt.anchorMax = new Vector2(0.95f, 0.5f);
            divRt.pivot     = new Vector2(0.5f, 0.5f);
            divRt.anchoredPosition = new Vector2(0f, y); divRt.sizeDelta = new Vector2(0f, 1f); y -= 26f;

            // ---- Action buttons ----
            MakeThemedButton("RESUME",            _panelRoot, y, LitIsoTheme.ButtonStyle.Gold, () => SetOpen(false)); y -= 62f;
            MakeThemedButton("SAVE GAME",         _panelRoot, y, LitIsoTheme.ButtonStyle.Stone, SaveGame);            y -= 62f;
            MakeThemedButton("SAVE & QUIT",       _panelRoot, y, LitIsoTheme.ButtonStyle.Stone, SaveAndQuitToMenu);   y -= 80f;

            // ---- Sliders (stone track + gold fill) ----
            MakeThemedSlider("Master Vol",  "vol_master",             1f,    _panelRoot, y, v => AudioListener.volume = v); y -= 56f;
            MakeThemedSlider("Music Vol",   "vol_music",              0.7f,  _panelRoot, y, null);                          y -= 56f;
            MakeThemedSlider("SFX Vol",     "vol_sfx",                1f,    _panelRoot, y, null);                          y -= 56f;
            MakeThemedSlider("UI Scale",    "ui.scale",               1f,    _panelRoot, y, ApplyUiScale, 0.75f, 1.75f);   y -= 56f;
            MakeThemedSlider("Text Scale",  TextScalePrefKey,         1.08f, _panelRoot, y, ApplyTextScale, 0.8f, 1.45f);  y -= 56f;
            MakeThemedSlider("Zoom Sens",   FoundationBootstrap.CameraZoomSensitivityPrefKey, 1f, _panelRoot, y, null, 0.35f, 2.5f);
        }

        // ---- Frame grammar helpers ----

        static void AddInsetLayer(RectTransform parent, Color color, float alpha, float offset, float thickness)
        {
            var go = new GameObject("Inset", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(color.r, color.g, color.b, alpha);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(offset, offset);
            rt.offsetMax = new Vector2(-offset, -offset);
            // Hollow interior — only the border line shows.
            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(rt, false);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = LitIsoTheme.Panel;
            fillImg.raycastTarget = false;
            var fr = fillImg.rectTransform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
            fr.offsetMin = new Vector2(thickness, thickness);
            fr.offsetMax = new Vector2(-thickness, -thickness);
            go.transform.SetAsFirstSibling();
        }

        static void AddCornerStud(RectTransform parent, Vector2 anchor, Vector2 offset)
        {
            const float half = 5f;
            var go = new GameObject("Stud", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = LitIsoTheme.GoldLit;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(half * 2f, half * 2f);
            rt.localEulerAngles = new Vector3(0f, 0f, 45f);
        }

        // ---- Button builder using LitIsoTheme.StyleButton ----

        void MakeThemedButton(string label, RectTransform parent, float y,
            LitIsoTheme.ButtonStyle style, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var bg  = go.AddComponent<Image>();
            var btn = go.AddComponent<Button>();
            LitIsoTheme.StyleButton(btn, bg, style);
            btn.onClick.AddListener(onClick);

            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(360f, 48f);

            var lbl = NewText(rt, "Label", label, 14, TextAnchor.MiddleCenter);
            LitIsoTheme.ApplyDisplay(lbl, 14, style == LitIsoTheme.ButtonStyle.Gold ? LitIsoTheme.GoldText : LitIsoTheme.Parchment);
            lbl.raycastTarget = false;
            Stretch(lbl.rectTransform);
        }

        // ---- Slider builder (dark stone track + gold fill, parchment label) ----

        void MakeThemedSlider(string label, string prefKey, float def, RectTransform parent,
            float y, UnityEngine.Events.UnityAction<float> extra, float min = 0f, float max = 1f)
        {
            // Label (left)
            var lbl = NewText(parent, "Lbl_" + label, label, 14, TextAnchor.MiddleLeft);
            LitIsoTheme.ApplyBody(lbl, 14, LitIsoTheme.WarmTan);
            lbl.raycastTarget = false;
            var lr = lbl.rectTransform;
            lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f); lr.pivot = new Vector2(0f, 0.5f);
            lr.anchoredPosition = new Vector2(-190f, y); lr.sizeDelta = new Vector2(140f, 22f);

            // Slider track (dark stone)
            var trackGo = new GameObject("Track_" + label, typeof(RectTransform));
            trackGo.transform.SetParent(parent, false);
            var trackImg = trackGo.AddComponent<Image>();
            trackImg.color = LitIsoTheme.Base;
            var trackOl = trackGo.AddComponent<Outline>();
            trackOl.effectColor = LitIsoTheme.Base;
            trackOl.effectDistance = new Vector2(2f, -2f);
            trackOl.useGraphicAlpha = false;
            var trackRt = trackImg.rectTransform;
            trackRt.anchorMin = trackRt.anchorMax = new Vector2(0.5f, 0.5f); trackRt.pivot = new Vector2(0f, 0.5f);
            trackRt.anchoredPosition = new Vector2(-40f, y); trackRt.sizeDelta = new Vector2(220f, 16f);

            // Fill area (gold)
            var fillAreaGo = new GameObject("FillArea", typeof(RectTransform));
            fillAreaGo.transform.SetParent(trackGo.transform, false);
            // GameObject(..., typeof(RectTransform)) already created the RectTransform; a second
            // AddComponent<RectTransform>() returns null (a GameObject can only hold one Transform),
            // which caused a NullReferenceException on the next line. Use the existing one.
            var fillAreaRt = (RectTransform)fillAreaGo.transform;
            fillAreaRt.anchorMin = Vector2.zero; fillAreaRt.anchorMax = Vector2.one;
            fillAreaRt.offsetMin = new Vector2(2f, 2f); fillAreaRt.offsetMax = new Vector2(-2f, -2f);

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = LitIsoTheme.Gold;
            var fillRt = fillImg.rectTransform;
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;

            // Handle (gold diamond)
            var handleGo = new GameObject("Handle", typeof(RectTransform));
            handleGo.transform.SetParent(trackGo.transform, false);
            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = LitIsoTheme.GoldLit;
            var handleRt = handleImg.rectTransform;
            handleRt.anchorMin = handleRt.anchorMax = new Vector2(0f, 0.5f);
            handleRt.pivot = new Vector2(0.5f, 0.5f);
            handleRt.sizeDelta = new Vector2(14f, 20f);
            handleRt.localEulerAngles = new Vector3(0f, 0f, 45f);
            var handleOl = handleGo.AddComponent<Outline>();
            handleOl.effectColor = LitIsoTheme.GoldShadow;
            handleOl.effectDistance = new Vector2(1f, -1f);
            handleOl.useGraphicAlpha = false;

            var slider = trackGo.AddComponent<Slider>();
            slider.targetGraphic = handleImg;
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = Mathf.Clamp(PlayerPrefs.GetFloat(prefKey, def), min, max);
            slider.onValueChanged.AddListener(v =>
            {
                PlayerPrefs.SetFloat(prefKey, v);
                extra?.Invoke(v);
                PlayerPrefs.Save();
            });
        }

        // ---- save / quit ----

        void ApplyUiScale(float scale)
        {
            scale = Mathf.Clamp(scale, 0.75f, 1.75f);
            PlayerPrefs.SetFloat("ui.scale", scale);
            PlayerPrefs.Save();
            if (_panelRoot != null) _panelRoot.localScale = Vector3.one * scale;
        }

        void ApplyTextScale(float scale)
        {
            scale = Mathf.Clamp(scale, 0.8f, 1.45f);
            PlayerPrefs.SetFloat(TextScalePrefKey, scale);
            PlayerPrefs.Save();
        }

        void QuitToMenu()
        {
            Time.timeScale = 1f;
            FoundationUiCoordinator.Active?.SetModalOpen("pause", false);
            FoundationBootstrap.RequestHudShutdown();
            SceneManager.LoadScene("MenuScene");
        }

        void SaveGame()
        {
            if (_bootstrap == null)
                _bootstrap = GetComponent<FoundationBootstrap>() ?? Object.FindFirstObjectByType<FoundationBootstrap>();
            if (_overlay == null)
                _overlay = GetComponent<FoundationInteractionOverlay>() ?? Object.FindFirstObjectByType<FoundationInteractionOverlay>();
            if (_bootstrap != null && _bootstrap.Save(_bootstrap.DefaultSavePath))
            {
                Debug.Log("[PauseMenu] Saved to " + _bootstrap.DefaultSavePath);
                _overlay?.Flash("Game saved.");
            }
            else
            {
                Debug.LogWarning("[PauseMenu] Save failed.");
                _overlay?.Flash("Save failed.");
            }
        }

        void SaveAndQuitToMenu() { SaveGame(); QuitToMenu(); }

        void OnDestroy()
        {
            FoundationUiCoordinator.Active?.SetModalOpen("pause", false);
            if (_open) Time.timeScale = 1f;
        }

        // ---- uGUI helpers ----

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void SetCentred(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static Text NewText(Transform parent, string name, string value, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = value;
            t.alignment = anchor;
            t.color = LitIsoTheme.Parchment;
            LitIsoFont.Apply(t, size);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.82f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            shadow.useGraphicAlpha = true;
            return t;
        }
    }
}
