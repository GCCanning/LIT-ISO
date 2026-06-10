using System;
using IsoCore.Foundation;
using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// One hotbar slot's display data. Foundation-free on purpose — the View never
    /// references IsoCore.Foundation types, so it can't break that assembly's build.
    /// </summary>
    public struct HudSlot
    {
        public string label;   // item display name (for tooltip/empty handling)
        public int count;      // stack count (only drawn when > 1)
        public Sprite icon;    // resolved item icon, or null → placeholder box
        public bool selected;  // is this the active hotbar slot
        public float durability01;
    }

    /// <summary>
    /// What the HUD renders from. Codex's lane provides a real adapter
    /// (FoundationHudAdapter) that maps Inventory / Hotbar / stats onto this; until
    /// then PlaceholderHudModel drives it so the bar previews standalone.
    /// </summary>
    public interface IGameHudModel
    {
        int SlotCount { get; }
        HudSlot GetSlot(int i);
        float Health01 { get; }   // 0..1
        float Mana01   { get; }   // 0..1
        float Hunger01 { get; }   // 0..1  (rendered only when showHungerBar=true)
        float Xp01     { get; }   // 0..1 toward next level
        int Level { get; }
        event Action Changed;     // raise when any of the above changes
    }

    /// <summary>Demo data so the HUD is visible/skinnable before the adapter binds.</summary>
    public sealed class PlaceholderHudModel : IGameHudModel
    {
        private readonly HudSlot[] _slots;
        public PlaceholderHudModel(int slots = 9)
        {
            _slots = new HudSlot[slots];
            for (int i = 0; i < slots; i++) _slots[i] = new HudSlot { label = "", count = 0, icon = null };
            if (slots > 0) _slots[0] = new HudSlot { label = "Axe", count = 1, selected = true };
            if (slots > 1) _slots[1] = new HudSlot { label = "Wood", count = 12 };
            if (slots > 2) _slots[2] = new HudSlot { label = "Stone", count = 8 };
        }
        public int SlotCount => _slots.Length;
        public HudSlot GetSlot(int i) => (i >= 0 && i < _slots.Length) ? _slots[i] : default;
        public float Health01 => 0.8f;
        public float Mana01   => 0.55f;
        public float Hunger01 => 0.6f;
        public float Xp01     => 0.35f;
        public int Level => 3;
        public event Action Changed; // never raised by the placeholder
        public void Raise() => Changed?.Invoke();
    }

    /// <summary>
    /// Skinnable in-game HUD bar (uGUI). Hotbar quick-bar centered along the bottom;
    /// vitals stacked at the bottom-left over an optional decorative plate.
    ///
    /// Default LitRPG vitals: HP / Mana / XP+level. Hunger bar is hidden (LitRPG
    /// scope); flip <see cref="showHungerBar"/> true to include it for survival.
    ///
    /// Skin art is auto-loaded from Resources/UI/InGame/ (see _DROP_INGAME_UI_HERE.md);
    /// every slot is optional and falls back to a flat colour so the bar always renders.
    ///
    /// Item icons resolve via <see cref="ItemIconResolver"/>:
    ///   content.Items.Get(itemId)?.Icon → Resources/Items/&lt;itemId&gt;.png → null.
    ///
    /// Spawn + bind happens automatically in <see cref="GameHudInitializer"/> as soon
    /// as <c>FoundationBootstrap.Ready</c> fires; no scene wiring required.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameUIController : MonoBehaviour
    {
        [Tooltip("Fallback slot count used by the placeholder model / before Init.")]
        public int defaultSlotCount = 9;

        [Tooltip("LitRPG default: false. Set true if survival scope expands to include hunger.")]
        public bool showHungerBar = false;

        // Palette fallbacks (used only when a skin sprite is absent).
        static readonly Color SlotBg      = new Color(0.10f, 0.12f, 0.16f, 0.85f);
        static readonly Color SlotSelect  = new Color(0.98f, 0.85f, 0.45f, 1f);
        static readonly Color BarTrack    = new Color(0.05f, 0.06f, 0.09f, 0.85f);
        static readonly Color HealthCol   = new Color(0.80f, 0.25f, 0.25f, 1f);
        static readonly Color ManaCol     = new Color(0.30f, 0.55f, 0.90f, 1f);
        static readonly Color HungerCol   = new Color(0.85f, 0.60f, 0.25f, 1f);
        static readonly Color XpCol       = new Color(0.85f, 0.70f, 0.30f, 1f);
        static readonly Color TextCol     = new Color(0.95f, 0.91f, 0.74f, 1f);

        IGameHudModel _model;
        Canvas _canvas;
        CanvasGroup _canvasGroup;
        RectTransform _vitalsRoot;
        RectTransform _hotbarRoot;
        FoundationHudViewMode _hudMode = FoundationHudViewMode.Adventure;

        // Slot widget refs (for cheap per-frame updates without rebuilding).
        Image[] _slotFrames;
        Image[] _slotIcons;
        Image[] _slotHighlights;
        Image[] _slotDurabilityFills;
        Text[]  _slotCounts;

        Image _healthFill, _manaFill, _hungerFill, _xpFill;
        Text  _levelText;

        /// <summary>Bind a real data model (e.g. <see cref="FoundationHudAdapter"/>) and rebuild against it.</summary>
        public void Init(IGameHudModel model)
        {
            Unsubscribe();
            _model = model;
            Build();
            Subscribe();
            Refresh();
            ApplyHudViewMode(FoundationUiCoordinator.CurrentHudViewMode);
        }

        void Awake()
        {
            if (_model == null) _model = new PlaceholderHudModel(defaultSlotCount);
            Build();
            Subscribe();
            Refresh();
            ApplyHudViewMode(FoundationUiCoordinator.CurrentHudViewMode);
        }

        void OnEnable()
        {
            FoundationUiCoordinator.HudViewModeChanged += ApplyHudViewMode;
            LitIsoFont.TextScaleChanged += HandleTextScaleChanged;
            ApplyHudViewMode(FoundationUiCoordinator.CurrentHudViewMode);
        }

        void OnDisable()
        {
            FoundationUiCoordinator.HudViewModeChanged -= ApplyHudViewMode;
            LitIsoFont.TextScaleChanged -= HandleTextScaleChanged;
        }

        void OnDestroy() => Unsubscribe();

        void Subscribe()   { if (_model != null) _model.Changed += Refresh; }
        void Unsubscribe() { if (_model != null) _model.Changed -= Refresh; }

        static Sprite Spr(string name) => Resources.Load<Sprite>("UI/InGame/" + name);

        // ---------------------------------------------------------------- build

        void Build()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);

            var canvasGo = new GameObject("InGameHUDCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.pixelPerfect = true;
            _canvas.sortingOrder = 100; // above the world
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            _canvasGroup = canvasGo.AddComponent<CanvasGroup>();

            BuildVitals(canvasGo.transform);
            BuildHotbar(canvasGo.transform);
            ApplyHudViewMode(_hudMode);
        }

        void BuildVitals(Transform parent)
        {
            // Bottom-left stack: HP / Mana / (Hunger, optional) / XP (top → bottom).
            int rowCount = showHungerBar ? 4 : 3;

            var col = NewRect("Vitals", parent);
            _vitalsRoot = col;
            col.anchorMin = col.anchorMax = new Vector2(0f, 1f);
            col.pivot = new Vector2(0f, 1f);
            col.anchoredPosition = new Vector2(24f, -24f);
            col.sizeDelta = new Vector2(430f, 46f * rowCount + 30f);
            PlayerResizableUi.Attach(col, "hud.vitals", new Vector2(260f, 120f), new Vector2(820f, 360f));

            // Optional decorative plate behind the bars (vitals_bg). Renders only if art exists.
            var plate = Spr("vitals_bg");
            if (plate != null)
            {
                var bg = NewImage(col, "Plate", plate, Color.white);
                bg.type = Image.Type.Sliced;
                bg.raycastTarget = false;
                var br = bg.rectTransform;
                br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
                br.offsetMin = new Vector2(-12f, -12f); br.offsetMax = new Vector2(12f, 12f);
            }

            int row = 0;
            _healthFill = Bar(col, "Health", row++, HealthCol, "bar_health_fill", 30f, "bar_track", "HP");
            _manaFill   = Bar(col, "Mana",   row++, ManaCol,   "bar_mana_fill",   30f, "bar_track", "MP");
            if (showHungerBar)
                _hungerFill = Bar(col, "Hunger", row++, HungerCol, "bar_hunger_fill", 30f, "bar_track", "FOOD");
            _xpFill     = Bar(col, "XP",     row++, XpCol,     "bar_xp_fill", 24f, "bar_xp_track", "XP");

            // Level label sits on the XP bar.
            _levelText = NewText(col, "Level", "Lv 1", 16, TextAnchor.MiddleLeft);
            var lt = _levelText.rectTransform;
            lt.anchorMin = lt.anchorMax = new Vector2(0f, 0f);
            lt.pivot = new Vector2(0f, 0f);
            lt.anchoredPosition = new Vector2(48f, -((rowCount - 1) * 44f));
            lt.sizeDelta = new Vector2(140f, 22f);
        }

        // Creates a track + fill bar; returns the fill Image. row 0=top.
        // trackSkin lets the XP bar use a thinner dedicated track (bar_xp_track).
        // label, if non-empty, draws a small ALL-CAPS tag inside the bar at the left
        // (e.g. "HP" / "MP" / "XP") so the bars are identifiable at a glance.
        Image Bar(RectTransform parent, string name, int row, Color fillCol, string fillSkin, float h = 24f, string trackSkin = "bar_track", string label = null)
        {
            // Wider vertical gap so the bars don't visually crowd each other.
            float gap = 14f;
            // Labels sit OUTSIDE the track to the left; the track + fill occupy the
            // remaining width. labelW=0 when no label so a no-label bar fills the row.
            float labelW = string.IsNullOrEmpty(label) ? 0f : 48f;
            float trackW = 382f - labelW;
            float y = -(row * (h + gap));

            // Label sits to the left of the track on its own rect — keeps the colored
            // fill visually unobstructed and the label is readable on the dark panel.
            if (!string.IsNullOrEmpty(label))
            {
                int sz = Mathf.Max(11, Mathf.RoundToInt(h * 0.6f));
                var lbl = NewText(parent, name + "Lbl", label, sz, TextAnchor.MiddleRight);
                lbl.raycastTarget = false;
                var lr = lbl.rectTransform;
                lr.anchorMin = lr.anchorMax = new Vector2(0f, 1f);
                lr.pivot = new Vector2(0f, 1f);
                lr.anchoredPosition = new Vector2(0f, y);
                lr.sizeDelta = new Vector2(labelW - 6f, h);
            }

            var track = NewImage(parent, name + "Track", Spr(trackSkin), BarTrack);
            track.type = Image.Type.Sliced;
            var tr = track.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0f, 1f);
            tr.pivot = new Vector2(0f, 1f);
            tr.anchoredPosition = new Vector2(labelW, y);
            tr.sizeDelta = new Vector2(trackW, h);

            var fillSprite = Spr(fillSkin);
            var fill = NewImage(tr, name + "Fill", fillSprite, fillCol);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            // Preserve the sprite's own aspect so the colored portion of the fill
            // sprite (which may have transparent padding above/below the colored bar)
            // doesn't get vertically stretched/squashed into the track.
            fill.preserveAspect = fillSprite != null;
            var fr = fill.rectTransform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
            fr.offsetMin = new Vector2(4f, 4f); fr.offsetMax = new Vector2(-4f, -4f);
            return fill;
        }

        void BuildHotbar(Transform parent)
        {
            int n = Mathf.Max(1, _model?.SlotCount ?? defaultSlotCount);
            const float slot = 68f, gap = 10f;
            float totalW = n * slot + (n - 1) * gap;

            var bar = NewRect("Hotbar", parent);
            _hotbarRoot = bar;
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(0f, 30f);
            bar.sizeDelta = new Vector2(totalW + 32f, slot + 22f);
            PlayerResizableUi.Attach(bar, "hud.hotbar", new Vector2(360f, 80f), new Vector2(1200f, 180f));

            // Optional bar background behind the slots (hotbar_bg.png if provided).
            var barBgSpr = Spr("hotbar_bg");
            if (barBgSpr != null)
            {
                var bg = NewImage(bar, "BarBg", barBgSpr, Color.white);
                bg.type = Image.Type.Sliced;
                var br = bg.rectTransform;
                br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
                br.offsetMin = Vector2.zero; br.offsetMax = Vector2.zero;
            }

            _slotFrames = new Image[n];
            _slotIcons = new Image[n];
            _slotHighlights = new Image[n];
            _slotDurabilityFills = new Image[n];
            _slotCounts = new Text[n];

            float x0 = -(totalW * 0.5f) + slot * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float x = x0 + i * (slot + gap);
                BuildSlot(bar, i, x, slot);
            }
        }

        void BuildSlot(RectTransform parent, int i, float x, float slot)
        {
            // Slot frame — uses slot.png if provided; falls back to a tinted-down
            // selected sprite (or flat color) if only slot_selected.png exists.
            var emptySpr = Spr("slot");
            var selSpr   = Spr("slot_selected");
            var frame = NewImage(parent, "Slot" + i, emptySpr != null ? emptySpr : selSpr, SlotBg);
            frame.type = Image.Type.Sliced;
            if (emptySpr == null && selSpr != null) frame.color = new Color(0.75f, 0.75f, 0.75f, 1f);
            var fr = frame.rectTransform;
            fr.anchorMin = fr.anchorMax = new Vector2(0.5f, 0.5f);
            fr.pivot = new Vector2(0.5f, 0.5f);
            fr.anchoredPosition = new Vector2(x, 0f);
            fr.sizeDelta = new Vector2(slot, slot);
            _slotFrames[i] = frame;

            // Selected highlight (sprite if provided, else tinted border overlay).
            var hi = NewImage(fr, "Highlight", selSpr, SlotSelect);
            hi.type = Image.Type.Sliced;
            hi.raycastTarget = false;
            if (selSpr == null) hi.color = new Color(SlotSelect.r, SlotSelect.g, SlotSelect.b, 0.35f);
            var hr = hi.rectTransform;
            hr.anchorMin = Vector2.zero; hr.anchorMax = Vector2.one;
            hr.offsetMin = new Vector2(-3f, -3f); hr.offsetMax = new Vector2(3f, 3f);
            hi.gameObject.SetActive(false);
            _slotHighlights[i] = hi;

            // Item icon.
            var icon = NewImage(fr, "Icon", null, Color.white);
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            var ir = icon.rectTransform;
            ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one;
            ir.offsetMin = new Vector2(10f, 10f); ir.offsetMax = new Vector2(-10f, -10f);
            icon.enabled = false;
            _slotIcons[i] = icon;

            var durTrack = NewImage(fr, "DurabilityTrack", null, new Color(0.04f, 0.05f, 0.06f, 0.9f));
            durTrack.raycastTarget = false;
            var dr = durTrack.rectTransform;
            dr.anchorMin = new Vector2(0f, 0f); dr.anchorMax = new Vector2(1f, 0f);
            dr.offsetMin = new Vector2(8f, 7f); dr.offsetMax = new Vector2(-8f, 12f);

            var durFill = NewImage(durTrack.transform, "DurabilityFill", null, Color.green);
            durFill.raycastTarget = false;
            durFill.type = Image.Type.Filled;
            durFill.fillMethod = Image.FillMethod.Horizontal;
            durFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            var dfr = durFill.rectTransform;
            dfr.anchorMin = Vector2.zero; dfr.anchorMax = Vector2.one;
            dfr.offsetMin = new Vector2(1f, 1f); dfr.offsetMax = new Vector2(-1f, -1f);
            durTrack.gameObject.SetActive(false);
            _slotDurabilityFills[i] = durFill;

            // Stack count (bottom-right).
            var count = NewText(fr, "Count", "", 14, TextAnchor.LowerRight);
            count.raycastTarget = false;
            var cr = count.rectTransform;
            cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one;
            cr.offsetMin = new Vector2(0f, 2f); cr.offsetMax = new Vector2(-4f, 0f);
            _slotCounts[i] = count;

            // Number key hint (top-left).
            var key = NewText(fr, "Key", (i + 1).ToString(), 11, TextAnchor.UpperLeft);
            key.raycastTarget = false;
            key.color = new Color(TextCol.r, TextCol.g, TextCol.b, 0.6f);
            var kr = key.rectTransform;
            kr.anchorMin = Vector2.zero; kr.anchorMax = Vector2.one;
            kr.offsetMin = new Vector2(4f, 0f); kr.offsetMax = new Vector2(0f, -2f);
        }

        // --------------------------------------------------------------- update

        void Refresh()
        {
            if (_model == null) return;

            if (_healthFill != null) _healthFill.fillAmount = Mathf.Clamp01(_model.Health01);
            if (_manaFill   != null) _manaFill.fillAmount   = Mathf.Clamp01(_model.Mana01);
            if (_hungerFill != null) _hungerFill.fillAmount = Mathf.Clamp01(_model.Hunger01);
            if (_xpFill     != null) _xpFill.fillAmount     = Mathf.Clamp01(_model.Xp01);
            if (_levelText  != null) _levelText.text = "Lv " + _model.Level;

            if (_slotFrames == null) return;
            for (int i = 0; i < _slotFrames.Length; i++)
            {
                var s = _model.GetSlot(i);
                bool hasItem = !string.IsNullOrEmpty(s.label) || s.count > 0 || s.icon != null;

                if (_slotIcons[i] != null)
                {
                    _slotIcons[i].sprite = s.icon;
                    _slotIcons[i].enabled = s.icon != null;
                    // No icon art yet but item present → faint placeholder fill.
                    if (s.icon == null && hasItem)
                    {
                        _slotIcons[i].enabled = true;
                        _slotIcons[i].sprite = null;
                        _slotIcons[i].color = new Color(0.6f, 0.65f, 0.7f, 0.5f);
                    }
                    else _slotIcons[i].color = Color.white;
                }

                if (_slotCounts[i] != null)
                    _slotCounts[i].text = s.count > 1 ? s.count.ToString() : "";

                if (_slotHighlights[i] != null)
                    _slotHighlights[i].gameObject.SetActive(s.selected);

                if (_slotDurabilityFills != null && i < _slotDurabilityFills.Length && _slotDurabilityFills[i] != null)
                {
                    float durability = Mathf.Clamp01(s.durability01);
                    var track = _slotDurabilityFills[i].transform.parent.gameObject;
                    track.SetActive(durability > 0f && hasItem);
                    _slotDurabilityFills[i].fillAmount = durability;
                    _slotDurabilityFills[i].color = Color.Lerp(
                        new Color(0.95f, 0.25f, 0.18f, 1f),
                        new Color(0.32f, 0.90f, 0.42f, 1f),
                        durability);
                }
            }
        }

        void HandleTextScaleChanged(float _)
        {
            Unsubscribe();
            Build();
            Subscribe();
            Refresh();
            ApplyHudViewMode(FoundationUiCoordinator.CurrentHudViewMode);
        }

        void ApplyHudViewMode(FoundationHudViewMode mode)
        {
            _hudMode = mode;
            bool show = mode != FoundationHudViewMode.Hidden;

            if (_canvas != null)
                _canvas.gameObject.SetActive(show);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = show ? 1f : 0f;
                _canvasGroup.blocksRaycasts = show;
                _canvasGroup.interactable = show;
            }

            SetVisible(_vitalsRoot, show);
            SetVisible(_hotbarRoot, show);
        }

        static void SetVisible(RectTransform root, bool visible)
        {
            if (root != null && root.gameObject.activeSelf != visible)
                root.gameObject.SetActive(visible);
        }

        // --------------------------------------------------------------- helpers

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static Image NewImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = sprite != null ? Color.white : color;
            return img;
        }

        static Text NewText(Transform parent, string name, string value, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = value;
            t.alignment = anchor;
            t.color = TextCol;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            LitIsoFont.Apply(t, size);
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.82f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            shadow.useGraphicAlpha = true;
            return t;
        }
    }
}
