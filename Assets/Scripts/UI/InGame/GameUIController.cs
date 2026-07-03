// PHASE_COMPLETE Phase2
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
        // Live absolute readouts rendered inside the bars ("82 / 110").
        // LitRPG rule: the player always sees the numbers.
        string HealthText { get; }
        string ManaText   { get; }
        string HotbarRowText { get; }
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
        public string HealthText => "82 / 110";
        public string ManaText   => "35 / 60";
        public string HotbarRowText => "";
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

        // Palette — aligned to the LIT-ISO front-end design tokens.
        static readonly Color SlotBg      = LitIsoTheme.Panel2;          // #1D2027 raised slot
        static readonly Color SlotSelect  = LitIsoTheme.Gold;            // #E8C468 gold accent
        static readonly Color BarTrack    = LitIsoTheme.Base;            // #0a0b0e hard track
        static readonly Color HealthCol   = LitIsoTheme.Hex("#d9425a");  // HP red
        static readonly Color ManaCol     = LitIsoTheme.Hex("#5f9ae8");  // MP blue
        static readonly Color HungerCol   = LitIsoTheme.Hex("#e8a03c");  // food amber
        static readonly Color XpCol       = LitIsoTheme.Gold;            // #E8C468 xp gold
        static readonly Color TextCol     = LitIsoTheme.Parchment;       // #d8d4c8 parchment

        // One shared translucent panel fill for EVERY HUD box (vitals, clock band,
        // coords, quest tracker, minimap caption…) so every cluster reads as the
        // same material. Design Panel #15171C over the scene at ~0.9 alpha. Inner
        // chips/cells use the darker inset. Do not hardcode per-widget greys.
        static readonly Color HudPanelBg    = new Color(0.082f, 0.090f, 0.110f, 0.92f); // #15171C
        static readonly Color HudPanelInset = new Color(0.055f, 0.063f, 0.078f, 0.95f); // #0e1014

        IGameHudModel _model;
        Canvas _canvas;
        CanvasGroup _canvasGroup;
        RectTransform _vitalsRoot;
        RectTransform _hotbarRoot;
        RectTransform _statusFxRoot;
        RectTransform _dayBandRoot;
        RectTransform _topRightRoot;
        RectTransform _abilityRoot;
        RectTransform _attackBtnRoot;
        Text _selItemName;
        Text _hotbarRowText;
        FoundationHudViewMode _hudMode = FoundationHudViewMode.Adventure;

        // Slot widget refs (for cheap per-frame updates without rebuilding).
        Image[] _slotFrames;
        Image[] _slotIcons;
        Image[] _slotHighlights;
        Image[] _slotDurabilityFills;
        Text[]  _slotCounts;

        Image _healthFill, _manaFill, _hungerFill, _xpFill;
        Text  _levelText;
        Text  _healthValue, _manaValue;

        // ---- live cluster sources (null-safe; fall back to design sample text) ----
        // UI may reference IsoCore.Foundation (never the reverse) so the player +
        // progression handles are taken directly; the clock/quest read models are the
        // same VMs the retired DayClockView / QuestTrackerView consumed.
        IDayClockViewModel _clockVm;
        IQuestTrackerViewModel _questVm;
        IsoFoundationPlayer _player;
        FoundationProgression _progression;

        // Day/time band widgets captured during build.
        Text _timeText;          // "18:24"
        Text _phaseText;         // "Night · Emberfall Woods"
        Text _dayChipText;       // "Day 4 of 7 · Forecast C"

        // Minimap player marker.
        RectTransform _playerMarker;

        // Coords box cells (X / Y / Z value texts).
        Text _coordX, _coordY, _coordZ;

        // Quest tracker widgets.
        Text _questTitleText;
        Text[] _questObjMarks;
        Text[] _questObjTexts;
        Text[] _questObjProgs;
        Text _questRewardText;

        // Interaction prompt (bottom-centre: key chip + action label).
        RectTransform _interactRoot;
        Text _interactKeyText;
        Text _interactActionText;

        // Tutorial overlay (semi-transparent stone card + dismiss).
        RectTransform _tutorialRoot;
        Text _tutorialBodyText;

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

        /// <summary>
        /// Bind the live HUD-cluster feeds the retired DayClockView / QuestTrackerView
        /// used (day clock + quest read models) plus the Foundation player and
        /// progression handles for coords / minimap heading / trial day. Every feed is
        /// optional — a null feed leaves the design sample text in place. Safe to call
        /// after <see cref="Init"/> (the widgets are built by then) or before (the
        /// references are simply cached and picked up on the next Refresh tick).
        /// </summary>
        public void BindLiveData(IDayClockViewModel clock, IQuestTrackerViewModel quest,
            IsoFoundationPlayer player, FoundationProgression progression)
        {
            if (_questVm != null) _questVm.Changed -= Refresh;
            _clockVm = clock;
            _questVm = quest;
            _player = player;
            _progression = progression;
            if (_questVm != null) _questVm.Changed += Refresh;
            Refresh();
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

        void Subscribe()
        {
            if (_model != null) _model.Changed += Refresh;
            if (_questVm != null) _questVm.Changed += Refresh;
        }
        void Unsubscribe()
        {
            if (_model != null) _model.Changed -= Refresh;
            if (_questVm != null) _questVm.Changed -= Refresh;
        }

        static Sprite Spr(string name) => Resources.Load<Sprite>("UI/InGame/" + name);

        // ---------------------------------------------------------------- build

        void Build()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);

            // Fresh widgets are created below, so drop the live-cluster change caches
            // and force an immediate re-poll — otherwise the cached "last" strings
            // would suppress the first write and leave design sample text showing.
            _lastClock = _lastPhase = null;
            _lastCoordX = _lastCoordY = _lastCoordZ = null;
            _lastHeading = float.NaN;
            _nextLivePoll = 0f;

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
            BuildStatusFx(canvasGo.transform);
            BuildDayTimeBand(canvasGo.transform);
            BuildTopRight(canvasGo.transform);
            BuildAbilityBar(canvasGo.transform);
            BuildAttackButton(canvasGo.transform);
            BuildHotbar(canvasGo.transform);
            BuildInteractionPrompt(canvasGo.transform);
            BuildTutorialOverlay(canvasGo.transform);
            ApplyHudViewMode(_hudMode);
        }

        void BuildVitals(Transform parent)
        {
            // TOP-LEFT vitals cluster — exact translation of the design HUD: a
            // rgba(15,17,22,.86) box (top:26 left:28 width:362), a gold-bordered
            // level diamond, WANDERER + Adventurer Rank, HP / MP / STA bars and an
            // XP bar. Live-bound: Refresh() drives the fill Images + value texts.
            var col = NewRect("Vitals", parent);
            _vitalsRoot = col;
            col.anchorMin = col.anchorMax = new Vector2(0f, 1f);
            col.pivot = new Vector2(0f, 1f);
            col.anchoredPosition = new Vector2(28f, -26f);
            col.sizeDelta = new Vector2(362f, 214f);
            var box = col.gameObject.AddComponent<Image>();
            box.color = HudPanelBg;
            HardBorder(col.gameObject, LitIsoTheme.Base, 2f);
            AddBevel(col);   // inner stone bevel — same frame grammar as every box

            const float pad = 14f;
            float innerW = 362f - pad * 2f;

            // gold level diamond (rotated square) with the level numeral upright inside
            var diamond = NewImage(col, "LvlDiamond", null, LitIsoTheme.Hex("#1a1d23"));
            HardBorder(diamond.gameObject, LitIsoTheme.Gold, 2f);
            var dr = diamond.rectTransform;
            dr.anchorMin = dr.anchorMax = new Vector2(0f, 1f); dr.pivot = new Vector2(0.5f, 0.5f);
            dr.anchoredPosition = new Vector2(pad + 26f, -pad - 26f);
            dr.sizeDelta = new Vector2(40f, 40f);
            diamond.transform.localEulerAngles = new Vector3(0f, 0f, 45f);
            _levelText = NewText(diamond.rectTransform, "Level", "Lv 1", 13, TextAnchor.MiddleCenter);
            LitIsoTheme.ApplyDisplay(_levelText, 13, LitIsoTheme.Gold);
            var ltr = _levelText.rectTransform;
            ltr.anchorMin = Vector2.zero; ltr.anchorMax = Vector2.one; ltr.offsetMin = Vector2.zero; ltr.offsetMax = Vector2.zero;
            _levelText.transform.localEulerAngles = new Vector3(0f, 0f, -45f);

            var name = NewText(col, "Name", "WANDERER", 13, TextAnchor.UpperLeft);
            LitIsoTheme.ApplyDisplay(name, 13, LitIsoTheme.Hex("#e8e4d8"));
            var nr = name.rectTransform; nr.anchorMin = nr.anchorMax = new Vector2(0f, 1f); nr.pivot = new Vector2(0f, 1f);
            nr.anchoredPosition = new Vector2(pad + 64f, -pad - 2f); nr.sizeDelta = new Vector2(innerW - 64f, 20f);

            var rank = NewText(col, "Rank", "Adventurer Rank · F", 14, TextAnchor.UpperLeft);
            LitIsoTheme.ApplyBody(rank, 14, LitIsoTheme.WarmTan);
            var rr = rank.rectTransform; rr.anchorMin = rr.anchorMax = new Vector2(0f, 1f); rr.pivot = new Vector2(0f, 1f);
            rr.anchoredPosition = new Vector2(pad + 64f, -pad - 26f); rr.sizeDelta = new Vector2(innerW - 64f, 20f);

            float by = -(pad + 52f + 12f);
            _healthFill = VitalBar(col, "HP",  pad, innerW, by, 24f, LitIsoTheme.Hex("#d9425a"), "HP",  true,  out _healthValue); by -= 32f;
            _manaFill   = VitalBar(col, "MP",  pad, innerW, by, 24f, LitIsoTheme.Hex("#5f9ae8"), "MP",  true,  out _manaValue);   by -= 32f;
            _hungerFill = VitalBar(col, "SP",  pad, innerW, by, 24f, LitIsoTheme.Hex("#6fae6f"), "SP",  false, out _);            by -= 30f;
            _xpFill     = VitalBar(col, "XP",  pad, innerW, by, 12f, LitIsoTheme.Gold,           "",    false, out _);
        }

        // One vital bar: dark track + left-filled colour fill + ALL-CAPS label and an
        // optional right-aligned live value. Fill is Image.Type.Filled so Refresh()
        // drives fillAmount. Returns the fill; value via out param.
        Image VitalBar(RectTransform parent, string id, float pad, float innerW, float y, float h,
            Color fill, string label, bool withValue, out Text value)
        {
            var track = NewImage(parent, id + "Track", null, LitIsoTheme.Hex("#0a0c10"));
            HardBorder(track.gameObject, LitIsoTheme.Base, 2f);
            var tr = track.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0f, 1f); tr.pivot = new Vector2(0f, 1f);
            tr.anchoredPosition = new Vector2(pad, y); tr.sizeDelta = new Vector2(innerW, h);

            var f = NewImage(track.rectTransform, "Fill", null, fill);
            f.type = Image.Type.Filled; f.fillMethod = Image.FillMethod.Horizontal;
            f.fillOrigin = (int)Image.OriginHorizontal.Left; f.raycastTarget = false;
            var fr = f.rectTransform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;

            if (!string.IsNullOrEmpty(label))
            {
                var lbl = NewText(track.rectTransform, "Lbl", label, 10, TextAnchor.MiddleLeft);
                LitIsoTheme.ApplyDisplay(lbl, 10, LitIsoTheme.Hex("#e8e4d8"));
                lbl.raycastTarget = false;
                var lr = lbl.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
                lr.offsetMin = new Vector2(8f, 0f); lr.offsetMax = new Vector2(-8f, 0f);
            }

            if (withValue)
            {
                value = NewText(track.rectTransform, "Val", "", 16, TextAnchor.MiddleRight);
                LitIsoTheme.ApplyBody(value, 16, Color.white);
                value.raycastTarget = false;
                var vr = value.rectTransform; vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
                vr.offsetMin = new Vector2(8f, 0f); vr.offsetMax = new Vector2(-8f, 0f);
            }
            else value = null;
            return f;
        }

        // Centered live readout inside a bar's track ("82 / 110").
        Text BarValueText(Image fill, string name)
        {
            if (fill == null) return null;
            var track = fill.rectTransform.parent;
            var t = NewText(track, name, "", 14, TextAnchor.MiddleCenter);
            t.raycastTarget = false;
            // Pixelify Sans body face, bright parchment — legible over the colored fill
            // thanks to the hard shadow NewText already attaches.
            LitIsoTheme.ApplyBody(t, 14, LitIsoTheme.ParchmentLit);
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return t;
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
                // Tiny ALL-CAPS gold tag so the bars are identifiable at a glance.
                LitIsoTheme.ApplyBody(lbl, sz, LitIsoTheme.GoldLit);
                var lr = lbl.rectTransform;
                lr.anchorMin = lr.anchorMax = new Vector2(0f, 1f);
                lr.pivot = new Vector2(0f, 1f);
                lr.anchoredPosition = new Vector2(0f, y);
                lr.sizeDelta = new Vector2(labelW - 6f, h);
            }

            bool hasTrackSkin = Spr(trackSkin) != null;
            var track = NewImage(parent, name + "Track", Spr(trackSkin), BarTrack);
            track.type = Image.Type.Sliced;
            var tr = track.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0f, 1f);
            tr.pivot = new Vector2(0f, 1f);
            tr.anchoredPosition = new Vector2(labelW, y);
            tr.sizeDelta = new Vector2(trackW, h);

            // Procedural fallback (no skin sprite): build the design's hard-bordered
            // inset track — 2px #0a0b0e border around a dark #0a0b0e inset, with the
            // colored fill recessed inside. When a skin sprite is present we trust it
            // and leave the border off so the art reads cleanly.
            if (!hasTrackSkin)
            {
                track.color = LitIsoTheme.Base;            // #0a0b0e hard track
                HardBorder(track.gameObject, LitIsoTheme.Base, 2f);

                // Inset shim: a touch lighter so the recessed track is readable on the
                // panel, with the fill sitting inside it.
                var inset = NewImage(tr, name + "Inset", null, new Color(0.06f, 0.07f, 0.09f, 1f));
                inset.raycastTarget = false;
                var insr = inset.rectTransform;
                insr.anchorMin = Vector2.zero; insr.anchorMax = Vector2.one;
                insr.offsetMin = new Vector2(2f, 2f); insr.offsetMax = new Vector2(-2f, -2f);
            }

            var fillSprite = Spr(fillSkin);
            var fill = NewImage(tr, name + "Fill", fillSprite, fillCol);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            // New skin fills are exact edge-to-edge bars (no transparent padding),
            // so they must stretch with the track. preserveAspect previously made
            // fills render tiny/misaligned inside the track.
            fill.preserveAspect = false;
            var fr = fill.rectTransform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
            fr.offsetMin = new Vector2(4f, 4f); fr.offsetMax = new Vector2(-4f, -4f);
            return fill;
        }

        // BOTTOM-CENTER hotbar — exact translation of the design: a centered column
        // with a gold selected-item-name label on top, then a row of 76×76 slots with
        // 7px gaps. Each slot: 2px frame border + inset, slot number (top-left, dim),
        // item icon (inset 16/16/18 with a 3px colored edge), stack count, and a 5px
        // durability sliver across the bottom. Live-bound via _slotFrames/_slotIcons/
        // _slotHighlights/_slotDurabilityFills/_slotCounts so Refresh() drives values.
        void BuildHotbar(Transform parent)
        {
            int n = Mathf.Max(1, _model?.SlotCount ?? defaultSlotCount);
            const float slot = 76f, gap = 7f;
            float totalW = n * slot + (n - 1) * gap;

            var col = NewRect("Hotbar", parent);
            _hotbarRoot = col;
            col.anchorMin = col.anchorMax = new Vector2(0.5f, 0f);
            col.pivot = new Vector2(0.5f, 0f);
            col.anchoredPosition = new Vector2(0f, 34f);
            col.sizeDelta = new Vector2(totalW, slot + 30f);
            PlayerResizableUi.Attach(col, "hud.hotbar", new Vector2(360f, 90f), new Vector2(1200f, 200f));

            // Selected item name — gold, centered, sits above the slot row.
            _selItemName = NewText(col, "SelItemName", "", 18, TextAnchor.MiddleCenter);
            LitIsoTheme.ApplyBody(_selItemName, 18, LitIsoTheme.Gold);
            _selItemName.raycastTarget = false;
            var sr = _selItemName.rectTransform;
            sr.anchorMin = new Vector2(0f, 1f); sr.anchorMax = new Vector2(1f, 1f);
            sr.pivot = new Vector2(0.5f, 1f);
            sr.anchoredPosition = new Vector2(0f, 0f);
            sr.sizeDelta = new Vector2(0f, 22f);

            _hotbarRowText = NewText(col, "HotbarRow", "", 13, TextAnchor.MiddleRight);
            LitIsoTheme.ApplyBody(_hotbarRowText, 13, LitIsoTheme.WarmTan);
            _hotbarRowText.raycastTarget = false;
            var rr = _hotbarRowText.rectTransform;
            rr.anchorMin = new Vector2(0f, 1f); rr.anchorMax = new Vector2(1f, 1f);
            rr.pivot = new Vector2(1f, 1f);
            rr.anchoredPosition = new Vector2(0f, -22f);
            rr.sizeDelta = new Vector2(0f, 18f);

            // Row container holding the slots.
            var row = NewRect("Row", col);
            row.anchorMin = new Vector2(0.5f, 0f); row.anchorMax = new Vector2(0.5f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.anchoredPosition = new Vector2(0f, 0f);
            row.sizeDelta = new Vector2(totalW, slot);

            _slotFrames = new Image[n];
            _slotIcons = new Image[n];
            _slotHighlights = new Image[n];
            _slotDurabilityFills = new Image[n];
            _slotCounts = new Text[n];

            float x0 = -(totalW * 0.5f) + slot * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float x = x0 + i * (slot + gap);
                BuildSlot(row, i, x, slot);
            }
        }

        void BuildSlot(RectTransform parent, int i, float x, float slot)
        {
            // Slot frame — design slot is #0e1014 with a 2px #0a0b0e border + 2px
            // inset and a 5px hard bottom shadow. A skin sprite (slot.png) wins if
            // present; otherwise build the procedural frame to match the mockup.
            var emptySpr = Spr("slot");
            var selSpr   = Spr("slot_selected");

            // 5px hard bottom shadow (#060708) behind the frame.
            var shadow = NewImage(parent, "Slot" + i + "Shadow", null, LitIsoTheme.Hex("#060708"));
            shadow.raycastTarget = false;
            var shr = shadow.rectTransform;
            shr.anchorMin = shr.anchorMax = new Vector2(0.5f, 0.5f); shr.pivot = new Vector2(0.5f, 0.5f);
            shr.anchoredPosition = new Vector2(x, -5f);
            shr.sizeDelta = new Vector2(slot, slot);

            var frame = NewImage(parent, "Slot" + i, emptySpr, HudPanelInset);
            frame.type = Image.Type.Sliced;
            if (emptySpr == null) HardBorder(frame.gameObject, LitIsoTheme.Base, 2f);
            var fr = frame.rectTransform;
            fr.anchorMin = fr.anchorMax = new Vector2(0.5f, 0.5f);
            fr.pivot = new Vector2(0.5f, 0.5f);
            fr.anchoredPosition = new Vector2(x, 0f);
            fr.sizeDelta = new Vector2(slot, slot);
            _slotFrames[i] = frame;

            // Inner 2px inset (the design's "inset 0 0 0 2px #0a0b0e") on procedural.
            if (emptySpr == null)
            {
                var inset = NewImage(fr, "Inset", null, HudPanelInset);
                inset.raycastTarget = false;
                var insr = inset.rectTransform;
                insr.anchorMin = Vector2.zero; insr.anchorMax = Vector2.one;
                insr.offsetMin = new Vector2(2f, 2f); insr.offsetMax = new Vector2(-2f, -2f);
            }

            // Hover name (owner request): shows the slot's item name + stack count.
            int slotIndex = i;
            var hover = frame.gameObject.AddComponent<UiHoverTooltip>();
            hover.textProvider = () =>
            {
                var s = _model?.GetSlot(slotIndex) ?? default;
                if (string.IsNullOrEmpty(s.label)) return null;
                return s.count > 1 ? $"{s.label} x{s.count}" : s.label;
            };

            // Selected highlight — gold frame border. Sprite if provided, else a gold
            // 2px outline overlay matching the design's selected gold frame border.
            var hi = NewImage(fr, "Highlight", selSpr, SlotSelect);
            hi.raycastTarget = false;
            if (selSpr != null) { hi.type = Image.Type.Sliced; }
            else { hi.color = Color.clear; HardBorder(hi.gameObject, SlotSelect, 2f); }
            var hr = hi.rectTransform;
            hr.anchorMin = Vector2.zero; hr.anchorMax = Vector2.one;
            hr.offsetMin = new Vector2(-2f, -2f); hr.offsetMax = new Vector2(2f, 2f);
            hi.gameObject.SetActive(false);
            _slotHighlights[i] = hi;

            // Item icon — design insets the icon 16px sides/top, 18px bottom, with a
            // 3px colored edge. The edge is drawn as a backing image behind the icon.
            var edge = NewImage(fr, "IconEdge", null, LitIsoTheme.Gold);
            edge.raycastTarget = false;
            var er = edge.rectTransform;
            er.anchorMin = Vector2.zero; er.anchorMax = Vector2.one;
            er.offsetMin = new Vector2(16f, 18f); er.offsetMax = new Vector2(-16f, -16f);
            edge.gameObject.SetActive(false);

            var icon = NewImage(edge.rectTransform, "Icon", null, Color.white);
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            var ir = icon.rectTransform;
            ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one;
            ir.offsetMin = new Vector2(3f, 3f); ir.offsetMax = new Vector2(-3f, -3f);
            icon.enabled = false;
            _slotIcons[i] = icon;

            // Durability sliver — 5px tall, 6px inset L/R, sitting 5px off the bottom,
            // dark track #0a0c10 with a colored fill.
            var durTrack = NewImage(fr, "DurabilityTrack", null, LitIsoTheme.Hex("#0a0c10"));
            durTrack.raycastTarget = false;
            var dr = durTrack.rectTransform;
            dr.anchorMin = new Vector2(0f, 0f); dr.anchorMax = new Vector2(1f, 0f);
            dr.pivot = new Vector2(0.5f, 0f);
            dr.offsetMin = new Vector2(6f, 5f); dr.offsetMax = new Vector2(-6f, 10f);

            var durFill = NewImage(durTrack.transform, "DurabilityFill", null, Color.green);
            durFill.raycastTarget = false;
            durFill.type = Image.Type.Filled;
            durFill.fillMethod = Image.FillMethod.Horizontal;
            durFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            var dfr = durFill.rectTransform;
            dfr.anchorMin = Vector2.zero; dfr.anchorMax = Vector2.one;
            dfr.offsetMin = Vector2.zero; dfr.offsetMax = Vector2.zero;
            durTrack.gameObject.SetActive(false);
            _slotDurabilityFills[i] = durFill;

            // Stack count (bottom-right) — Press Start 2P, white, hard shadow.
            var count = NewText(fr, "Count", "", 12, TextAnchor.LowerRight);
            LitIsoTheme.ApplyDisplay(count, 12, Color.white);
            count.raycastTarget = false;
            var cr = count.rectTransform;
            cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one;
            cr.offsetMin = new Vector2(0f, 9f); cr.offsetMax = new Vector2(-5f, 0f);
            _slotCounts[i] = count;

            // Slot number (top-left) — design uses #5a564c Press Start 2P.
            var key = NewText(fr, "Key", (i + 1).ToString(), 10, TextAnchor.UpperLeft);
            LitIsoTheme.ApplyDisplay(key, 10, LitIsoTheme.Hex("#5a564c"));
            key.raycastTarget = false;
            var kr = key.rectTransform;
            kr.anchorMin = Vector2.zero; kr.anchorMax = Vector2.one;
            kr.offsetMin = new Vector2(4f, 0f); kr.offsetMax = new Vector2(0f, -2f);
        }

        // ------------------------------------------------------ status FX row

        // Under the vitals box (top:248 left:28 in the design): a row of 40×40 icon
        // chips, each a #0e1014 box with a colored ring border and an inner colored
        // icon, plus a small timer label beneath. Static design data (no live FX feed
        // bound into this controller).
        void BuildStatusFx(Transform parent)
        {
            var root = NewRect("StatusFx", parent);
            _statusFxRoot = root;
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(28f, -248f);
            root.sizeDelta = new Vector2(200f, 56f);

            (Color ring, Color icon, string time)[] fx =
            {
                (LitIsoTheme.Hex("#d9425a"), LitIsoTheme.Hex("#e0563c"), "0:12"),
                (LitIsoTheme.Hex("#5f9ae8"), LitIsoTheme.Hex("#6fd0f0"), "0:30"),
                (LitIsoTheme.Hex("#6fae6f"), LitIsoTheme.Hex("#7ad07a"), "1:05"),
            };

            const float chip = 40f, gap = 8f;
            for (int i = 0; i < fx.Length; i++)
            {
                float x = i * (chip + gap);
                var box = NewImage(root, "Fx" + i, null, HudPanelInset);
                box.raycastTarget = false;
                HardBorder(box.gameObject, fx[i].ring, 2f);
                var br = box.rectTransform;
                br.anchorMin = br.anchorMax = new Vector2(0f, 1f); br.pivot = new Vector2(0f, 1f);
                br.anchoredPosition = new Vector2(x, 0f);
                br.sizeDelta = new Vector2(chip, chip);

                var ic = NewImage(br, "Ic", null, fx[i].icon);
                ic.raycastTarget = false;
                var icr = ic.rectTransform;
                icr.anchorMin = Vector2.zero; icr.anchorMax = Vector2.one;
                icr.offsetMin = new Vector2(7f, 7f); icr.offsetMax = new Vector2(-7f, -7f);

                var t = NewText(root, "T" + i, fx[i].time, 9, TextAnchor.UpperCenter);
                LitIsoTheme.ApplyDisplay(t, 9, LitIsoTheme.Hex("#a39e90"));
                t.raycastTarget = false;
                var tr = t.rectTransform;
                tr.anchorMin = tr.anchorMax = new Vector2(0f, 1f); tr.pivot = new Vector2(0.5f, 1f);
                tr.anchoredPosition = new Vector2(x + chip * 0.5f, -(chip + 3f));
                tr.sizeDelta = new Vector2(chip + 12f, 14f);
            }
        }

        // ------------------------------------------------ day/time + trial band

        // TOP-CENTER: a clock band (time + moon + phase/location) over a TRIAL banner
        // ("TRIAL" gold chip + "Day 4 of 7 · Forecast C" dark chip). Static design
        // text — the live day/forecast feed is owned by TrialStatusBanner elsewhere.
        void BuildDayTimeBand(Transform parent)
        {
            var col = NewRect("DayBand", parent);
            _dayBandRoot = col;
            col.anchorMin = col.anchorMax = new Vector2(0.5f, 1f);
            col.pivot = new Vector2(0.5f, 1f);
            col.anchoredPosition = new Vector2(0f, -26f);
            col.sizeDelta = new Vector2(420f, 86f);

            // --- clock band ---
            var band = NewImage(col, "Band", null, HudPanelBg);
            HardBorder(band.gameObject, LitIsoTheme.Base, 2f);
            var bandr = band.rectTransform;
            bandr.anchorMin = new Vector2(0.5f, 1f); bandr.anchorMax = new Vector2(0.5f, 1f);
            bandr.pivot = new Vector2(0.5f, 1f);
            bandr.anchoredPosition = new Vector2(0f, 0f);
            bandr.sizeDelta = new Vector2(360f, 38f);
            // inner stone bevel line
            var bevel = NewImage(bandr, "Bevel", null, Color.clear);
            bevel.raycastTarget = false;
            HardBorder(bevel.gameObject, LitIsoTheme.Stone, 2f);
            var bvr = bevel.rectTransform; bvr.anchorMin = Vector2.zero; bvr.anchorMax = Vector2.one;
            bvr.offsetMin = new Vector2(2f, 2f); bvr.offsetMax = new Vector2(-2f, -2f);

            var time = NewText(bandr, "Time", "18:24", 13, TextAnchor.MiddleLeft);
            _timeText = time;
            LitIsoTheme.ApplyDisplay(time, 13, LitIsoTheme.Gold);
            var tmr = time.rectTransform; tmr.anchorMin = tmr.anchorMax = new Vector2(0f, 0.5f); tmr.pivot = new Vector2(0f, 0.5f);
            tmr.anchoredPosition = new Vector2(20f, 0f); tmr.sizeDelta = new Vector2(70f, 20f);

            // moon dot
            var moon = NewImage(bandr, "Moon", null, LitIsoTheme.Parchment);
            moon.raycastTarget = false;
            var mr = moon.rectTransform; mr.anchorMin = mr.anchorMax = new Vector2(0f, 0.5f); mr.pivot = new Vector2(0f, 0.5f);
            mr.anchoredPosition = new Vector2(96f, 0f); mr.sizeDelta = new Vector2(16f, 16f);

            var phase = NewText(bandr, "Phase", "Night · Emberfall Woods", 18, TextAnchor.MiddleLeft);
            _phaseText = phase;
            LitIsoTheme.ApplyBody(phase, 18, LitIsoTheme.WarmTan);
            var phr = phase.rectTransform; phr.anchorMin = new Vector2(0f, 0.5f); phr.anchorMax = new Vector2(1f, 0.5f); phr.pivot = new Vector2(0f, 0.5f);
            phr.offsetMin = new Vector2(120f, -10f); phr.offsetMax = new Vector2(-12f, 10f);

            // --- trial banner ---
            var banner = NewRect("TrialBanner", col);
            banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 1f); banner.pivot = new Vector2(0.5f, 1f);
            banner.anchoredPosition = new Vector2(0f, -46f);
            banner.sizeDelta = new Vector2(360f, 34f);
            HardBorder(banner.gameObject, LitIsoTheme.GoldDeep, 2f);

            var trialChip = NewImage(banner, "TrialChip", null, LitIsoTheme.Gold);
            var tcr = trialChip.rectTransform; tcr.anchorMin = new Vector2(0f, 0f); tcr.anchorMax = new Vector2(0f, 1f); tcr.pivot = new Vector2(0f, 0.5f);
            tcr.anchoredPosition = new Vector2(0f, 0f); tcr.sizeDelta = new Vector2(78f, 0f);
            var trialTxt = NewText(tcr, "TrialTxt", "TRIAL", 11, TextAnchor.MiddleCenter);
            LitIsoTheme.ApplyDisplay(trialTxt, 11, LitIsoTheme.GoldText);
            var ttr = trialTxt.rectTransform; ttr.anchorMin = Vector2.zero; ttr.anchorMax = Vector2.one; ttr.offsetMin = Vector2.zero; ttr.offsetMax = Vector2.zero;

            var dayChip = NewImage(banner, "DayChip", null, HudPanelInset);
            var dcr = dayChip.rectTransform; dcr.anchorMin = new Vector2(0f, 0f); dcr.anchorMax = new Vector2(1f, 1f); dcr.pivot = new Vector2(0f, 0.5f);
            dcr.offsetMin = new Vector2(78f, 0f); dcr.offsetMax = new Vector2(0f, 0f);
            var dayTxt = NewText(dcr, "DayTxt", "Day 4 of 7 · Forecast C", 17, TextAnchor.MiddleCenter);
            _dayChipText = dayTxt;
            LitIsoTheme.ApplyBody(dayTxt, 17, LitIsoTheme.ParchmentLit);
            var dtr = dayTxt.rectTransform; dtr.anchorMin = Vector2.zero; dtr.anchorMax = Vector2.one; dtr.offsetMin = new Vector2(8f, 0f); dtr.offsetMax = new Vector2(-8f, 0f);
        }

        // ----------------------------------------- top-right minimap/coords/quest

        // TOP-RIGHT column (width 300, right:28): a round minimap with layered
        // border rings + biomes + river + markers + N compass, a coords box (X/Y/Z
        // cells), and a collapsible quest tracker. Static design values.
        void BuildTopRight(Transform parent)
        {
            var col = NewRect("TopRight", parent);
            _topRightRoot = col;
            col.anchorMin = col.anchorMax = new Vector2(1f, 1f);
            col.pivot = new Vector2(1f, 1f);
            col.anchoredPosition = new Vector2(-28f, -26f);
            col.sizeDelta = new Vector2(300f, 520f);

            BuildMinimap(col, out float yCursor);
            yCursor -= 14f;
            BuildCoordsBox(col, ref yCursor);
            yCursor -= 14f;
            BuildQuestTracker(col, ref yCursor);
        }

        void BuildMinimap(RectTransform col, out float yBottom)
        {
            const float size = 220f;
            // Layered ring border (design: 2px #0a0b0e, 2px #2c313c, 3px #5a4d2a, 2px
            // #0a0b0e) built as nested rings, centered horizontally in the 300px col.
            var rings = new (Color c, float thick)[]
            {
                (LitIsoTheme.Base, 2f), (LitIsoTheme.Stone, 2f), (LitIsoTheme.GoldDeep, 3f), (LitIsoTheme.Base, 2f),
            };

            var outer = NewRect("Minimap", col);
            outer.anchorMin = outer.anchorMax = new Vector2(0.5f, 1f); outer.pivot = new Vector2(0.5f, 1f);
            outer.anchoredPosition = new Vector2(0f, 0f);
            float total = size + 2f * (2f + 2f + 3f + 2f);
            outer.sizeDelta = new Vector2(total, total);

            // Concentric ring images (squares with rounded look approximated by full
            // squares — uGUI Image has no per-corner radius; the design intent is a
            // ringed disc, approximated here as nested colored frames).
            float pad = 0f;
            foreach (var r in rings)
            {
                var ring = NewImage(outer, "Ring", null, r.c);
                ring.raycastTarget = false;
                var rr = ring.rectTransform;
                rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
                rr.offsetMin = new Vector2(pad, pad); rr.offsetMax = new Vector2(-pad, -pad);
                pad += r.thick;
            }

            // Map disc (terrain gradient base).
            var disc = NewImage(outer, "Disc", null, LitIsoTheme.Hex("#172012"));
            disc.raycastTarget = false;
            var discr = disc.rectTransform;
            discr.anchorMin = Vector2.zero; discr.anchorMax = Vector2.one;
            discr.offsetMin = new Vector2(pad, pad); discr.offsetMax = new Vector2(-pad, -pad);

            // forest blobs
            var f1 = NewImage(discr, "Forest1", null, LitIsoTheme.Hex("#243018"));
            f1.raycastTarget = false;
            var f1r = f1.rectTransform; f1r.anchorMin = new Vector2(-0.12f, 0.30f); f1r.anchorMax = new Vector2(0.63f, 0.88f);
            f1r.offsetMin = Vector2.zero; f1r.offsetMax = Vector2.zero;
            var f2 = NewImage(discr, "Forest2", null, LitIsoTheme.Hex("#1d2716"));
            f2.raycastTarget = false;
            var f2r = f2.rectTransform; f2r.anchorMin = new Vector2(0.42f, 0.46f); f2r.anchorMax = new Vector2(1.08f, 0.94f);
            f2r.offsetMin = Vector2.zero; f2r.offsetMax = Vector2.zero;

            // river (rotated bar)
            var river = NewImage(discr, "River", null, LitIsoTheme.Hex("#27506e"));
            river.raycastTarget = false; river.color = new Color(river.color.r, river.color.g, river.color.b, 0.8f);
            var rivr = river.rectTransform; rivr.anchorMin = rivr.anchorMax = new Vector2(0.08f, 0.5f); rivr.pivot = new Vector2(0.5f, 0.5f);
            rivr.sizeDelta = new Vector2(16f, size * 1.3f); rivr.anchoredPosition = new Vector2(0f, 0f);
            river.transform.localEulerAngles = new Vector3(0f, 0f, -22f);

            // settlement marker (gold diamond)
            var sett = NewImage(discr, "Settlement", null, LitIsoTheme.Gold);
            sett.raycastTarget = false;
            var settr = sett.rectTransform; settr.anchorMin = settr.anchorMax = new Vector2(0.64f, 0.66f); settr.pivot = new Vector2(0.5f, 0.5f);
            settr.sizeDelta = new Vector2(11f, 11f); sett.transform.localEulerAngles = new Vector3(0f, 0f, 45f);

            // objective ring (red circle outline)
            var obj = NewImage(discr, "Objective", null, Color.clear);
            obj.raycastTarget = false; HardBorder(obj.gameObject, LitIsoTheme.Hex("#d96a55"), 2f);
            var objr = obj.rectTransform; objr.anchorMin = objr.anchorMax = new Vector2(0.30f, 0.38f); objr.pivot = new Vector2(0.5f, 0.5f);
            objr.sizeDelta = new Vector2(10f, 10f);

            // player arrow (cyan triangle approximated by a small rotated square)
            var arrow = NewImage(discr, "Player", null, LitIsoTheme.Hex("#6fd0f0"));
            arrow.raycastTarget = false;
            var ar = arrow.rectTransform; ar.anchorMin = ar.anchorMax = new Vector2(0.5f, 0.5f); ar.pivot = new Vector2(0.5f, 0.5f);
            ar.sizeDelta = new Vector2(12f, 12f); arrow.transform.localEulerAngles = new Vector3(0f, 0f, 45f);
            _playerMarker = ar;

            // compass N
            var nLbl = NewText(discr, "N", "N", 11, TextAnchor.UpperCenter);
            LitIsoTheme.ApplyDisplay(nLbl, 11, LitIsoTheme.Gold);
            nLbl.raycastTarget = false;
            var nr = nLbl.rectTransform; nr.anchorMin = nr.anchorMax = new Vector2(0.5f, 1f); nr.pivot = new Vector2(0.5f, 1f);
            nr.anchoredPosition = new Vector2(0f, -7f); nr.sizeDelta = new Vector2(20f, 16f);

            yBottom = -total;
        }

        void BuildCoordsBox(RectTransform col, ref float y)
        {
            const float h = 78f;
            var box = NewImage(col, "Coords", null, HudPanelBg);
            HardBorder(box.gameObject, LitIsoTheme.Base, 2f);
            var br = box.rectTransform;
            br.anchorMin = new Vector2(0f, 1f); br.anchorMax = new Vector2(1f, 1f); br.pivot = new Vector2(0.5f, 1f);
            br.offsetMin = new Vector2(0f, 0f); br.offsetMax = new Vector2(0f, 0f);
            br.anchoredPosition = new Vector2(0f, y);
            br.sizeDelta = new Vector2(0f, h);

            var bevel = NewImage(br, "Bevel", null, Color.clear);
            bevel.raycastTarget = false; HardBorder(bevel.gameObject, LitIsoTheme.Stone, 2f);
            var bvr = bevel.rectTransform; bvr.anchorMin = Vector2.zero; bvr.anchorMax = Vector2.one;
            bvr.offsetMin = new Vector2(2f, 2f); bvr.offsetMax = new Vector2(-2f, -2f);

            var region = NewText(br, "Region", "EMBERFALL · NW", 10, TextAnchor.UpperLeft);
            LitIsoTheme.ApplyDisplay(region, 10, LitIsoTheme.Gold);
            var rr = region.rectTransform; rr.anchorMin = new Vector2(0f, 1f); rr.anchorMax = new Vector2(1f, 1f); rr.pivot = new Vector2(0f, 1f);
            rr.offsetMin = new Vector2(12f, -22f); rr.offsetMax = new Vector2(-110f, -10f);

            var disc = NewText(br, "Disc", "Discovered", 14, TextAnchor.UpperRight);
            LitIsoTheme.ApplyBody(disc, 14, LitIsoTheme.WarmTan);
            var dr = disc.rectTransform; dr.anchorMin = new Vector2(1f, 1f); dr.anchorMax = new Vector2(1f, 1f); dr.pivot = new Vector2(1f, 1f);
            dr.anchoredPosition = new Vector2(-12f, -10f); dr.sizeDelta = new Vector2(110f, 18f);

            // X / Y / Z cells
            (string k, string v)[] cells = { ("X", "1284"), ("Y", "64"), ("Z", "-892") };
            const float gap = 7f;
            float cellW = (300f - 24f - 2f * gap) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var cell = NewImage(br, "Cell" + i, null, HudPanelInset);
                HardBorder(cell.gameObject, LitIsoTheme.Base, 2f);
                cell.raycastTarget = false;
                var cr = cell.rectTransform;
                cr.anchorMin = new Vector2(0f, 0f); cr.anchorMax = new Vector2(0f, 0f); cr.pivot = new Vector2(0f, 0f);
                cr.anchoredPosition = new Vector2(12f + i * (cellW + gap), 10f);
                cr.sizeDelta = new Vector2(cellW, 38f);

                var k = NewText(cr, "K", cells[i].k, 9, TextAnchor.UpperCenter);
                LitIsoTheme.ApplyDisplay(k, 9, LitIsoTheme.WarmTan);
                k.raycastTarget = false;
                var kr = k.rectTransform; kr.anchorMin = new Vector2(0f, 1f); kr.anchorMax = new Vector2(1f, 1f); kr.pivot = new Vector2(0.5f, 1f);
                kr.offsetMin = new Vector2(0f, -16f); kr.offsetMax = new Vector2(0f, -4f);

                var v = NewText(cr, "V", cells[i].v, 18, TextAnchor.LowerCenter);
                LitIsoTheme.ApplyBody(v, 18, LitIsoTheme.Parchment);
                v.raycastTarget = false;
                var vr = v.rectTransform; vr.anchorMin = new Vector2(0f, 0f); vr.anchorMax = new Vector2(1f, 0f); vr.pivot = new Vector2(0.5f, 0f);
                vr.offsetMin = new Vector2(0f, 4f); vr.offsetMax = new Vector2(0f, 20f);

                if (i == 0) _coordX = v;
                else if (i == 1) _coordY = v;
                else _coordZ = v;
            }

            y -= h;
        }

        void BuildQuestTracker(RectTransform col, ref float y)
        {
            const float headerH = 42f, bodyH = 130f;
            float total = headerH + bodyH;
            var box = NewImage(col, "QuestTracker", null, HudPanelBg);
            HardBorder(box.gameObject, LitIsoTheme.Base, 2f);
            AddBevel(box.rectTransform);   // same inner stone bevel as every box
            var br = box.rectTransform;
            br.anchorMin = new Vector2(0f, 1f); br.anchorMax = new Vector2(1f, 1f); br.pivot = new Vector2(0.5f, 1f);
            br.offsetMin = new Vector2(0f, 0f); br.offsetMax = new Vector2(0f, 0f);
            br.anchoredPosition = new Vector2(0f, y);
            br.sizeDelta = new Vector2(0f, total);

            // header row: gold diamond + title + collapse caret
            var header = NewImage(br, "Header", null, Color.clear);
            header.raycastTarget = false;
            var hr = header.rectTransform; hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f); hr.pivot = new Vector2(0.5f, 1f);
            hr.offsetMin = new Vector2(0f, -headerH); hr.offsetMax = new Vector2(0f, 0f);
            // header bottom border
            var hb = NewImage(hr, "HBorder", null, LitIsoTheme.Base);
            hb.raycastTarget = false;
            var hbr = hb.rectTransform; hbr.anchorMin = new Vector2(0f, 0f); hbr.anchorMax = new Vector2(1f, 0f); hbr.pivot = new Vector2(0.5f, 0f);
            hbr.offsetMin = Vector2.zero; hbr.offsetMax = new Vector2(0f, 2f);

            var diamond = NewImage(hr, "Diamond", null, LitIsoTheme.Gold);
            diamond.raycastTarget = false;
            var dr = diamond.rectTransform; dr.anchorMin = dr.anchorMax = new Vector2(0f, 0.5f); dr.pivot = new Vector2(0.5f, 0.5f);
            dr.anchoredPosition = new Vector2(20f, 0f); dr.sizeDelta = new Vector2(11f, 11f);
            diamond.transform.localEulerAngles = new Vector3(0f, 0f, 45f);

            var title = NewText(hr, "Title", "Light the Beacons", 12, TextAnchor.MiddleLeft);
            _questTitleText = title;
            LitIsoTheme.ApplyDisplay(title, 12, LitIsoTheme.Gold);
            var ttr = title.rectTransform; ttr.anchorMin = new Vector2(0f, 0f); ttr.anchorMax = new Vector2(1f, 1f); ttr.pivot = new Vector2(0f, 0.5f);
            ttr.offsetMin = new Vector2(38f, 0f); ttr.offsetMax = new Vector2(-34f, 0f);

            var caret = NewText(hr, "Caret", "▾", 20, TextAnchor.MiddleCenter);
            LitIsoTheme.ApplyBody(caret, 20, LitIsoTheme.Hex("#a39e90"));
            var car = caret.rectTransform; car.anchorMin = new Vector2(1f, 0f); car.anchorMax = new Vector2(1f, 1f); car.pivot = new Vector2(1f, 0.5f);
            car.offsetMin = new Vector2(-30f, 0f); car.offsetMax = new Vector2(-12f, 0f);

            // body: objectives + reward
            var body = NewRect("Body", br);
            body.anchorMin = new Vector2(0f, 1f); body.anchorMax = new Vector2(1f, 1f); body.pivot = new Vector2(0.5f, 1f);
            body.offsetMin = new Vector2(16f, -total); body.offsetMax = new Vector2(-16f, -headerH);

            (string mark, Color markCol, string text, string prog, Color progCol)[] objs =
            {
                ("✓", LitIsoTheme.Green, "Reach the ridge", "DONE", LitIsoTheme.Green),
                ("◆", LitIsoTheme.Gold, "Light brazier", "1/3", LitIsoTheme.Gold),
                ("○", LitIsoTheme.WarmTan, "Return to Aldric", "0/1", LitIsoTheme.WarmTan),
            };
            _questObjMarks = new Text[objs.Length];
            _questObjTexts = new Text[objs.Length];
            _questObjProgs = new Text[objs.Length];
            float oy = -2f;
            for (int i = 0; i < objs.Length; i++)
            {
                var mark = NewText(body, "Mark" + i, objs[i].mark, 17, TextAnchor.MiddleLeft);
                LitIsoTheme.ApplyBody(mark, 17, objs[i].markCol);
                var mr = mark.rectTransform; mr.anchorMin = new Vector2(0f, 1f); mr.anchorMax = new Vector2(0f, 1f); mr.pivot = new Vector2(0f, 1f);
                mr.anchoredPosition = new Vector2(0f, oy); mr.sizeDelta = new Vector2(18f, 22f);
                _questObjMarks[i] = mark;

                var txt = NewText(body, "Obj" + i, objs[i].text, 17, TextAnchor.MiddleLeft);
                LitIsoTheme.ApplyBody(txt, 17, LitIsoTheme.Parchment);
                var txr = txt.rectTransform; txr.anchorMin = new Vector2(0f, 1f); txr.anchorMax = new Vector2(1f, 1f); txr.pivot = new Vector2(0f, 1f);
                txr.offsetMin = new Vector2(22f, oy - 22f); txr.offsetMax = new Vector2(-44f, oy);
                _questObjTexts[i] = txt;

                var prog = NewText(body, "Prog" + i, objs[i].prog, 11, TextAnchor.MiddleRight);
                LitIsoTheme.ApplyDisplay(prog, 11, objs[i].progCol);
                var pr = prog.rectTransform; pr.anchorMin = new Vector2(1f, 1f); pr.anchorMax = new Vector2(1f, 1f); pr.pivot = new Vector2(1f, 1f);
                pr.anchoredPosition = new Vector2(0f, oy); pr.sizeDelta = new Vector2(44f, 22f);
                _questObjProgs[i] = prog;

                oy -= 30f;
            }

            var reward = NewText(body, "Reward", "Reward: 120 XP · Ember Charm", 15, TextAnchor.UpperLeft);
            _questRewardText = reward;
            LitIsoTheme.ApplyBody(reward, 15, LitIsoTheme.Hex("#a39e90"));
            var rwr = reward.rectTransform; rwr.anchorMin = new Vector2(0f, 1f); rwr.anchorMax = new Vector2(1f, 1f); rwr.pivot = new Vector2(0f, 1f);
            rwr.offsetMin = new Vector2(0f, oy - 22f); rwr.offsetMax = new Vector2(0f, oy);

            y -= total;
        }

        // -------------------------------------------------------- ability bar

        // BOTTOM-LEFT: an "ABILITIES" caption over a row of 70×70 ability buttons.
        // Each: #0e1014 frame with colored border, glowing icon, radial cooldown
        // (approximated with a darkening overlay), centered cd text, hotkey chip
        // (top-left), and a cost label (bottom-right). Static design data — live
        // casting/cooldown is owned by AbilityWheelView's input layer.
        void BuildAbilityBar(Transform parent)
        {
            var col = NewRect("AbilityBar", parent);
            _abilityRoot = col;
            col.anchorMin = col.anchorMax = new Vector2(0f, 0f);
            col.pivot = new Vector2(0f, 0f);
            col.anchoredPosition = new Vector2(28f, 34f);
            col.sizeDelta = new Vector2(330f, 96f);

            var caption = NewText(col, "Caption", "ABILITIES", 9, TextAnchor.UpperLeft);
            LitIsoTheme.ApplyDisplay(caption, 9, LitIsoTheme.WarmTan);
            caption.raycastTarget = false;
            var cr = caption.rectTransform; cr.anchorMin = new Vector2(0f, 1f); cr.anchorMax = new Vector2(1f, 1f); cr.pivot = new Vector2(0f, 1f);
            cr.offsetMin = new Vector2(0f, -16f); cr.offsetMax = new Vector2(0f, 0f);

            // Honest labels for the default Q/E/R/F loadout (flash_step / mana_bolt /
            // ember_spark / steady_strike) with their real costs. No fake cooldown/disabled
            // states (the old hardcoded values showed F greyed-out as if on cooldown).
            // NB: still static — does not yet track live reassignments from the X wheel.
            (string key, Color col, float iconA, int cdPct, string cd, string cost, Color costFg, Color border)[] abilities =
            {
                ("Q", LitIsoTheme.Hex("#7fd0e8"), 1f, 0, "", "ST 8",  LitIsoTheme.Green, LitIsoTheme.GoldDeep),
                ("E", LitIsoTheme.Hex("#5f9ae8"), 1f, 0, "", "MP 9",  LitIsoTheme.Hex("#5f9ae8"), LitIsoTheme.Base),
                ("R", LitIsoTheme.Hex("#e8a03c"), 1f, 0, "", "MP 12", LitIsoTheme.Hex("#e8a03c"), LitIsoTheme.Base),
                ("F", LitIsoTheme.Hex("#d9425a"), 1f, 0, "", "ST 10", LitIsoTheme.Green, LitIsoTheme.GoldDeep),
            };

            const float cell = 70f, gap = 10f;
            for (int i = 0; i < abilities.Length; i++)
            {
                float x = i * (cell + gap);
                var a = abilities[i];

                // hard bottom shadow
                var sh = NewImage(col, "AbShadow" + i, null, LitIsoTheme.Hex("#060708"));
                sh.raycastTarget = false;
                var shr = sh.rectTransform; shr.anchorMin = shr.anchorMax = new Vector2(0f, 0f); shr.pivot = new Vector2(0f, 0f);
                shr.anchoredPosition = new Vector2(x, -5f); shr.sizeDelta = new Vector2(cell, cell);

                var frame = NewImage(col, "Ability" + i, null, HudPanelInset);
                HardBorder(frame.gameObject, a.border, 2f);
                var fr = frame.rectTransform; fr.anchorMin = fr.anchorMax = new Vector2(0f, 0f); fr.pivot = new Vector2(0f, 0f);
                fr.anchoredPosition = new Vector2(x, 0f); fr.sizeDelta = new Vector2(cell, cell);

                // glowing icon
                var icon = NewImage(fr, "Icon", null, new Color(a.col.r, a.col.g, a.col.b, a.iconA));
                icon.raycastTarget = false;
                var ir = icon.rectTransform; ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one;
                ir.offsetMin = new Vector2(12f, 18f); ir.offsetMax = new Vector2(-12f, -12f);

                // radial cooldown approximation: a dark overlay whose height tracks the
                // remaining-cooldown fraction (uGUI conic gradients aren't available;
                // a vertical Filled overlay reads as a sweeping cooldown shroud).
                if (a.cdPct > 0)
                {
                    var cd = NewImage(fr, "Cooldown", null, new Color(0.024f, 0.027f, 0.031f, 0.82f));
                    cd.raycastTarget = false;
                    cd.type = Image.Type.Filled; cd.fillMethod = Image.FillMethod.Radial360;
                    cd.fillOrigin = (int)Image.Origin360.Top; cd.fillClockwise = true;
                    cd.fillAmount = a.cdPct / 100f;
                    var cdr = cd.rectTransform; cdr.anchorMin = Vector2.zero; cdr.anchorMax = Vector2.one;
                    cdr.offsetMin = Vector2.zero; cdr.offsetMax = Vector2.zero;

                    var cdT = NewText(fr, "CdText", a.cd, 18, TextAnchor.MiddleCenter);
                    LitIsoTheme.ApplyDisplay(cdT, 18, Color.white);
                    cdT.raycastTarget = false;
                    var cdtr = cdT.rectTransform; cdtr.anchorMin = Vector2.zero; cdtr.anchorMax = Vector2.one;
                    cdtr.offsetMin = Vector2.zero; cdtr.offsetMax = Vector2.zero;
                }

                // hotkey chip top-left
                var keyChip = NewImage(fr, "KeyChip", null, LitIsoTheme.Hex("#1a1d23"));
                HardBorder(keyChip.gameObject, LitIsoTheme.Base, 2f);
                keyChip.raycastTarget = false;
                var kcr = keyChip.rectTransform; kcr.anchorMin = kcr.anchorMax = new Vector2(0f, 1f); kcr.pivot = new Vector2(0f, 1f);
                kcr.anchoredPosition = new Vector2(-2f, 2f); kcr.sizeDelta = new Vector2(20f, 20f);
                var keyT = NewText(kcr, "K", a.key, 10, TextAnchor.MiddleCenter);
                LitIsoTheme.ApplyDisplay(keyT, 10, LitIsoTheme.ParchmentLit);
                keyT.raycastTarget = false;
                var ktr = keyT.rectTransform; ktr.anchorMin = Vector2.zero; ktr.anchorMax = Vector2.one; ktr.offsetMin = Vector2.zero; ktr.offsetMax = Vector2.zero;

                // cost bottom-right
                var cost = NewText(fr, "Cost", a.cost, 10, TextAnchor.LowerRight);
                LitIsoTheme.ApplyDisplay(cost, 10, a.costFg);
                cost.raycastTarget = false;
                var cor = cost.rectTransform; cor.anchorMin = Vector2.zero; cor.anchorMax = Vector2.one;
                cor.offsetMin = new Vector2(0f, 2f); cor.offsetMax = new Vector2(-4f, 0f);
            }
        }

        // ------------------------------------------------------- attack button

        // BOTTOM-CENTRE-RIGHT: a large 96×96 primary ATTACK button (SPACE / LMB).
        // Sits to the right of the ability bar at the same bottom-left anchor band.
        // Red-tinted frame with a sword icon placeholder and "SPACE" key chip.
        // Pressing it calls PlayerAttack() on any IsoFoundationPlayer in the scene.
        void BuildAttackButton(Transform parent)
        {
            const float size = 96f;
            var root = NewRect("AttackButton", parent);
            _attackBtnRoot = root;
            // Anchored bottom-left, offset so it sits to the right of the 330px ability bar
            root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 0f);
            root.anchoredPosition = new Vector2(28f + 330f + 16f, 34f);
            root.sizeDelta = new Vector2(size, size);

            // Hard shadow
            var shadow = NewImage(root, "AtkShadow", null, LitIsoTheme.Hex("#060708"));
            shadow.raycastTarget = false;
            var shr = shadow.rectTransform;
            shr.anchorMin = shr.anchorMax = new Vector2(0.5f, 0.5f); shr.pivot = new Vector2(0.5f, 0.5f);
            shr.anchoredPosition = new Vector2(0f, -5f); shr.sizeDelta = new Vector2(size, size);

            // Frame — red-bordered for primary attack clarity
            var frame = NewImage(root, "AtkFrame", null, HudPanelInset);
            HardBorder(frame.gameObject, LitIsoTheme.Hex("#d9425a"), 3f);
            frame.raycastTarget = true;
            var fr = frame.rectTransform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
            fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;

            // Sword icon placeholder (⚔ unicode, gold)
            var icon = NewText(fr, "AtkIcon", "⚔", 38, TextAnchor.MiddleCenter);
            LitIsoTheme.ApplyDisplay(icon, 38, LitIsoTheme.Gold);
            icon.raycastTarget = false;
            var ir = icon.rectTransform;
            ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one;
            ir.offsetMin = new Vector2(0f, 12f); ir.offsetMax = new Vector2(0f, 0f);

            // "ATTACK" label
            var lbl = NewText(fr, "AtkLabel", "ATTACK", 9, TextAnchor.LowerCenter);
            LitIsoTheme.ApplyDisplay(lbl, 9, LitIsoTheme.Hex("#d9425a"));
            lbl.raycastTarget = false;
            var lr = lbl.rectTransform;
            lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(1f, 0f);
            lr.pivot = new Vector2(0.5f, 0f);
            lr.anchoredPosition = new Vector2(0f, 4f); lr.sizeDelta = new Vector2(0f, 16f);

            // SPACE key chip (top-left)
            var keyChip = NewImage(fr, "AtkKeyChip", null, LitIsoTheme.Hex("#1a1d23"));
            HardBorder(keyChip.gameObject, LitIsoTheme.Base, 2f);
            keyChip.raycastTarget = false;
            var kcr = keyChip.rectTransform;
            kcr.anchorMin = kcr.anchorMax = new Vector2(0f, 1f); kcr.pivot = new Vector2(0f, 1f);
            kcr.anchoredPosition = new Vector2(-2f, 2f); kcr.sizeDelta = new Vector2(20f, 20f);
            var keyT = NewText(kcr, "K", "Z", 8, TextAnchor.MiddleCenter);
            LitIsoTheme.ApplyDisplay(keyT, 8, LitIsoTheme.ParchmentLit);
            keyT.raycastTarget = false;
            var ktr = keyT.rectTransform;
            ktr.anchorMin = Vector2.zero; ktr.anchorMax = Vector2.one;
            ktr.offsetMin = Vector2.zero; ktr.offsetMax = Vector2.zero;

            // Button component — triggers PlayerAttack on the first IsoFoundationPlayer found
            var btn = frame.gameObject.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor    = new Color(1f, 1f, 1f, 1f);
            cb.highlightedColor = new Color(1f, 0.7f, 0.7f, 1f);
            cb.pressedColor   = new Color(0.7f, 0.3f, 0.3f, 1f);
            btn.colors = cb;
            btn.onClick.AddListener(() =>
            {
                var player = UnityEngine.Object.FindFirstObjectByType<IsoFoundationPlayer>();
                if (player != null) player.TriggerBasicAttack();
            });
        }

        // Floating combat text is world-space (IsoCore.Foundation.FloatingText, pooled).
        // The old static screen-space mock ("-12" / "+40 XP" / "FIRE TRAP!" pinned at fixed
        // screen percentages) was removed 2026-07-02 — it never despawned in builds
        // (playtest rec_20260702_195606, task #1).

        // BOTTOM-CENTRE contextual popup: 32×32 key chip (gold border) + action label.
        // Hidden by default; caller drives ShowInteractionPrompt / HideInteractionPrompt.
        void BuildInteractionPrompt(Transform parent)
        {
            var root = NewRect("InteractPrompt", parent);
            _interactRoot = root;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, 120f); // above the hotbar
            root.sizeDelta = new Vector2(320f, 52f);

            // Stone panel backing
            var bg = root.gameObject.AddComponent<Image>();
            bg.color = new Color(LitIsoTheme.Panel.r, LitIsoTheme.Panel.g, LitIsoTheme.Panel.b, 0.92f);
            HardBorder(root.gameObject, LitIsoTheme.Base, 3f);

            // Gold hairline inner line
            var hairline = NewImage(root, "Hairline", null, new Color(LitIsoTheme.GoldDeep.r, LitIsoTheme.GoldDeep.g, LitIsoTheme.GoldDeep.b, 0.55f));
            hairline.raycastTarget = false;
            var hr = hairline.rectTransform;
            hr.anchorMin = Vector2.zero; hr.anchorMax = Vector2.one;
            hr.offsetMin = new Vector2(3f, 3f); hr.offsetMax = new Vector2(-3f, -3f);
            var hairFill = NewImage(hr, "HairFill", null, LitIsoTheme.Panel);
            hairFill.raycastTarget = false;
            var hfr = hairFill.rectTransform;
            hfr.anchorMin = Vector2.zero; hfr.anchorMax = Vector2.one;
            hfr.offsetMin = new Vector2(1f, 1f); hfr.offsetMax = new Vector2(-1f, -1f);
            hairline.transform.SetAsFirstSibling();

            // Key chip — gold-bordered square on the left
            var chip = NewImage(root, "KeyChip", null, LitIsoTheme.Hex("#1a1d23"));
            HardBorder(chip.gameObject, LitIsoTheme.Gold, 2f);
            var cr = chip.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(0f, 0.5f); cr.pivot = new Vector2(0f, 0.5f);
            cr.anchoredPosition = new Vector2(10f, 0f);
            cr.sizeDelta = new Vector2(32f, 32f);

            _interactKeyText = NewText(cr, "Key", "E", 16, TextAnchor.MiddleCenter);
            LitIsoTheme.ApplyDisplay(_interactKeyText, 16, LitIsoTheme.GoldLit);
            _interactKeyText.raycastTarget = false;
            var ktr = _interactKeyText.rectTransform;
            ktr.anchorMin = Vector2.zero; ktr.anchorMax = Vector2.one;
            ktr.offsetMin = Vector2.zero; ktr.offsetMax = Vector2.zero;

            // Action label — parchment text to the right of the chip
            _interactActionText = NewText(root, "Action", "Interact", 18, TextAnchor.MiddleLeft);
            LitIsoTheme.ApplyBody(_interactActionText, 18, LitIsoTheme.Parchment);
            _interactActionText.raycastTarget = false;
            var atr = _interactActionText.rectTransform;
            atr.anchorMin = new Vector2(0f, 0f); atr.anchorMax = new Vector2(1f, 1f);
            atr.offsetMin = new Vector2(52f, 0f); atr.offsetMax = new Vector2(-10f, 0f);

            root.gameObject.SetActive(false); // hidden until needed
        }

        // Full-width semi-transparent stone card centred on screen for tutorial hints.
        // Hidden by default; caller drives ShowTutorial / HideTutorial.
        void BuildTutorialOverlay(Transform parent)
        {
            // Scrim behind the card
            var root = NewRect("TutorialOverlay", parent);
            _tutorialRoot = root;
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;

            // Stone card — centred, fixed width
            var card = NewImage(root, "Card", null, new Color(LitIsoTheme.Panel.r, LitIsoTheme.Panel.g, LitIsoTheme.Panel.b, 0.96f));
            var cardRt = card.rectTransform;
            cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(600f, 260f);
            HardBorder(card.gameObject, LitIsoTheme.Base, 3f);

            // Steel inset
            var steel = NewImage(cardRt, "Steel", null, new Color(LitIsoTheme.Stone.r, LitIsoTheme.Stone.g, LitIsoTheme.Stone.b, 0.60f));
            steel.raycastTarget = false;
            var sr = steel.rectTransform;
            sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one;
            sr.offsetMin = new Vector2(3f, 3f); sr.offsetMax = new Vector2(-3f, -3f);
            var steelFill = NewImage(sr, "SteelFill", null, LitIsoTheme.Panel);
            steelFill.raycastTarget = false;
            var sfr = steelFill.rectTransform;
            sfr.anchorMin = Vector2.zero; sfr.anchorMax = Vector2.one;
            sfr.offsetMin = new Vector2(1f, 1f); sfr.offsetMax = new Vector2(-1f, -1f);
            steel.transform.SetAsFirstSibling();

            // Gold hairline
            var gold = NewImage(cardRt, "GoldLine", null, new Color(LitIsoTheme.GoldDeep.r, LitIsoTheme.GoldDeep.g, LitIsoTheme.GoldDeep.b, 0.55f));
            gold.raycastTarget = false;
            var gr = gold.rectTransform;
            gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one;
            gr.offsetMin = new Vector2(4f, 4f); gr.offsetMax = new Vector2(-4f, -4f);
            var goldFill = NewImage(gr, "GoldFill", null, LitIsoTheme.Panel);
            goldFill.raycastTarget = false;
            var gfr = goldFill.rectTransform;
            gfr.anchorMin = Vector2.zero; gfr.anchorMax = Vector2.one;
            gfr.offsetMin = new Vector2(1f, 1f); gfr.offsetMax = new Vector2(-1f, -1f);
            gold.transform.SetAsFirstSibling();

            // Corner studs (gold diamond at each corner)
            AddCornerStudToPanel(cardRt, new Vector2(0f, 1f), new Vector2(6f, -6f));
            AddCornerStudToPanel(cardRt, new Vector2(1f, 1f), new Vector2(-6f, -6f));
            AddCornerStudToPanel(cardRt, new Vector2(0f, 0f), new Vector2(6f, 6f));
            AddCornerStudToPanel(cardRt, new Vector2(1f, 0f), new Vector2(-6f, 6f));

            // Heading — gold "TIP" wordmark
            var heading = NewText(cardRt, "Heading", "TIP", 14, TextAnchor.UpperCenter);
            LitIsoTheme.ApplyDisplay(heading, 14, LitIsoTheme.Gold);
            heading.raycastTarget = false;
            var hdr = heading.rectTransform;
            hdr.anchorMin = new Vector2(0f, 1f); hdr.anchorMax = new Vector2(1f, 1f);
            hdr.pivot = new Vector2(0.5f, 1f);
            hdr.anchoredPosition = new Vector2(0f, -18f);
            hdr.sizeDelta = new Vector2(0f, 28f);

            // Gold divider line under heading
            var divider = NewImage(cardRt, "Divider", null, new Color(LitIsoTheme.Gold.r, LitIsoTheme.Gold.g, LitIsoTheme.Gold.b, 0.35f));
            divider.raycastTarget = false;
            var divRt = divider.rectTransform;
            divRt.anchorMin = new Vector2(0.05f, 1f); divRt.anchorMax = new Vector2(0.95f, 1f);
            divRt.pivot = new Vector2(0.5f, 1f);
            divRt.anchoredPosition = new Vector2(0f, -50f);
            divRt.sizeDelta = new Vector2(0f, 1f);

            // Body text — scrollable area
            _tutorialBodyText = NewText(cardRt, "Body", "Tutorial text will appear here.", 16, TextAnchor.UpperLeft);
            LitIsoTheme.ApplyBody(_tutorialBodyText, 16, LitIsoTheme.Parchment);
            _tutorialBodyText.raycastTarget = false;
            _tutorialBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _tutorialBodyText.verticalOverflow = VerticalWrapMode.Overflow;
            var btr = _tutorialBodyText.rectTransform;
            btr.anchorMin = new Vector2(0f, 0f); btr.anchorMax = new Vector2(1f, 1f);
            btr.offsetMin = new Vector2(20f, 56f); btr.offsetMax = new Vector2(-20f, -56f);

            // Dismiss button — bottom-right of card, gold style
            var dismissGo = new GameObject("Dismiss", typeof(RectTransform));
            dismissGo.transform.SetParent(cardRt, false);
            var dismissBg = dismissGo.AddComponent<Image>();
            var dismissBtn = dismissGo.AddComponent<Button>();
            LitIsoTheme.StyleButton(dismissBtn, dismissBg, LitIsoTheme.ButtonStyle.Gold);
            dismissBtn.onClick.AddListener(HideTutorial);
            var dbr = dismissBg.rectTransform;
            dbr.anchorMin = dbr.anchorMax = new Vector2(1f, 0f); dbr.pivot = new Vector2(1f, 0f);
            dbr.anchoredPosition = new Vector2(-16f, 16f);
            dbr.sizeDelta = new Vector2(120f, 36f);
            var dismissLbl = NewText(dbr, "Label", "GOT IT", 13, TextAnchor.MiddleCenter);
            LitIsoTheme.ApplyDisplay(dismissLbl, 13, LitIsoTheme.GoldText);
            dismissLbl.raycastTarget = false;
            var dlr = dismissLbl.rectTransform;
            dlr.anchorMin = Vector2.zero; dlr.anchorMax = Vector2.one;
            dlr.offsetMin = new Vector2(4f, 2f); dlr.offsetMax = new Vector2(-4f, -2f);

            root.gameObject.SetActive(false); // hidden until needed
        }

        // Corner gold diamond stud helper (mirrors WelcomeScreenManager pattern).
        static void AddCornerStudToPanel(RectTransform parent, Vector2 anchor, Vector2 offset)
        {
            const float half = 5f; // half-size of the stud
            var go = new GameObject("Stud", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = LitIsoTheme.GoldLit;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(half * 2f, half * 2f);
            rt.localEulerAngles = new Vector3(0f, 0f, 45f);
        }

        // ---------------------------------------------------------------- public API

        /// <summary>
        /// Show the bottom-centre interaction prompt with a key chip + action label.
        /// Call HideInteractionPrompt() when the interactable is no longer in range.
        /// </summary>
        public void ShowInteractionPrompt(string keyLabel, string actionLabel)
        {
            if (_interactRoot == null) return;
            if (_interactKeyText != null)    _interactKeyText.text    = keyLabel ?? "E";
            if (_interactActionText != null) _interactActionText.text = actionLabel ?? "Interact";
            _interactRoot.gameObject.SetActive(true);
        }

        /// <summary>Hide the interaction prompt.</summary>
        public void HideInteractionPrompt()
        {
            if (_interactRoot != null) _interactRoot.gameObject.SetActive(false);
        }

        /// <summary>Show the tutorial overlay with the given body text.</summary>
        public void ShowTutorial(string bodyText)
        {
            if (_tutorialRoot == null) return;
            if (_tutorialBodyText != null) _tutorialBodyText.text = bodyText ?? "";
            _tutorialRoot.gameObject.SetActive(true);
        }

        /// <summary>Dismiss the tutorial overlay.</summary>
        public void HideTutorial()
        {
            if (_tutorialRoot != null) _tutorialRoot.gameObject.SetActive(false);
        }

        // --------------------------------------------------------------- update

        // change-detection caches (perf audit 2026-06-11: this method was the #1
        // UI hotspot — per-refresh string allocations + redundant SetActive/text
        // writes dirtied the canvas every event and churned the GC)
        int _lastLevel = int.MinValue;
        string _lastHealthText, _lastManaText;
        int[] _slotCountCache;
        bool[] _slotSelCache, _slotDuraActiveCache;

        void Refresh()
        {
            // Quest tracker + trial-day are event-driven (Progression.Changed →
            // _questVm.Changed → Refresh); they don't depend on the hotbar _model.
            RefreshQuestCluster();
            RefreshDayChip();

            if (_model == null) return;

            if (_healthFill != null) _healthFill.fillAmount = Mathf.Clamp01(_model.Health01);
            if (_manaFill   != null) _manaFill.fillAmount   = Mathf.Clamp01(_model.Mana01);
            if (_hungerFill != null) _hungerFill.fillAmount = Mathf.Clamp01(_model.Hunger01);
            if (_xpFill     != null) _xpFill.fillAmount     = Mathf.Clamp01(_model.Xp01);

            if (_levelText != null && _model.Level != _lastLevel)
            {
                _lastLevel = _model.Level;
                _levelText.text = _lastLevel.ToString();
            }
            string ht = _model.HealthText ?? "";
            if (_healthValue != null && ht != _lastHealthText)
            {
                _lastHealthText = ht;
                _healthValue.text = ht;
            }
            string mt = _model.ManaText ?? "";
            if (_manaValue != null && mt != _lastManaText)
            {
                _lastManaText = mt;
                _manaValue.text = mt;
            }

            if (_slotFrames == null) return;
            int n = _slotFrames.Length;
            if (_slotCountCache == null || _slotCountCache.Length != n)
            {
                _slotCountCache = new int[n];
                _slotSelCache = new bool[n];
                _slotDuraActiveCache = new bool[n];
                for (int i = 0; i < n; i++) _slotCountCache[i] = int.MinValue;
            }

            string selName = "";
            for (int i = 0; i < n; i++)
            {
                var s = _model.GetSlot(i);
                bool hasItem = !string.IsNullOrEmpty(s.label) || s.count > 0 || s.icon != null;
                if (s.selected && !string.IsNullOrEmpty(s.label)) selName = s.label;

                if (_slotIcons[i] != null)
                {
                    // The icon lives inside the colored "IconEdge" backing image; the
                    // edge only shows when the slot actually holds an item.
                    var edge = _slotIcons[i].transform.parent != null
                        ? _slotIcons[i].transform.parent.gameObject : null;

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

                    if (edge != null && edge.name == "IconEdge")
                        edge.SetActive(hasItem);
                }

                if (_slotCounts[i] != null && s.count != _slotCountCache[i])
                {
                    _slotCountCache[i] = s.count;
                    _slotCounts[i].text = s.count > 1 ? s.count.ToString() : "";
                }

                if (_slotHighlights[i] != null && s.selected != _slotSelCache[i])
                {
                    _slotSelCache[i] = s.selected;
                    _slotHighlights[i].gameObject.SetActive(s.selected);
                }

                if (_slotDurabilityFills != null && i < _slotDurabilityFills.Length && _slotDurabilityFills[i] != null)
                {
                    float durability = Mathf.Clamp01(s.durability01);
                    bool active = durability > 0f && hasItem;
                    if (active != _slotDuraActiveCache[i])
                    {
                        _slotDuraActiveCache[i] = active;
                        _slotDurabilityFills[i].transform.parent.gameObject.SetActive(active);
                    }
                    if (active)
                    {
                        _slotDurabilityFills[i].fillAmount = durability;
                        _slotDurabilityFills[i].color = Color.Lerp(
                            new Color(0.95f, 0.25f, 0.18f, 1f),
                            new Color(0.32f, 0.90f, 0.42f, 1f),
                            durability);
                    }
                }
            }

            if (_selItemName != null && _selItemName.text != selName)
                _selItemName.text = selName;

            if (_hotbarRowText != null)
            {
                string rowText = _model.HotbarRowText ?? "";
                if (_hotbarRowText.text != rowText)
                    _hotbarRowText.text = rowText;
            }
        }

        // ----------------------------------------------- live cluster polling

        float _nextLivePoll;
        string _lastClock, _lastPhase;
        string _lastCoordX, _lastCoordY, _lastCoordZ;
        float _lastHeading = float.NaN;

        void Update()
        {
            if (Time.unscaledTime < _nextLivePoll) return;
            _nextLivePoll = Time.unscaledTime + 0.25f;
            RefreshClockCluster();
            RefreshCoordsCluster();
            RefreshMinimapCluster();
        }

        void RefreshClockCluster()
        {
            if (_clockVm == null) return;
            if (_timeText != null)
            {
                string clock = _clockVm.TimeText ?? "";
                if (clock != _lastClock) { _lastClock = clock; _timeText.text = clock; }
            }
            if (_phaseText != null)
            {
                string phase = _clockVm.PhaseLabel ?? "";
                // Live biome from the Foundation discovery journal; the design-sample
                // name only remains as the pre-bind placeholder.
                string biome = FoundationBiomeDiscovery.ActiveBiomeDisplay;
                if (string.IsNullOrEmpty(biome)) biome = "Emberfall Woods";
                string shown = string.IsNullOrEmpty(phase) ? biome : phase + " · " + biome;
                if (shown != _lastPhase)
                {
                    _lastPhase = shown;
                    _phaseText.text = shown;
                    // Phase-coloured band: day parchment, dawn gold, dusk amber and a
                    // danger red at night — the day/night rhythm is readable from the
                    // HUD alone (Romestead-informed hierarchy; original styling).
                    _phaseText.color =
                        phase == "Night" ? NightDangerCol :
                        phase == "Dusk" ? HungerCol :
                        phase == "Dawn" ? XpCol :
                        TextCol;
                }
            }
        }

        static readonly Color NightDangerCol = LitIsoTheme.Hex("#e06767");

        void RefreshDayChip()
        {
            if (_dayChipText == null || _progression == null) return;
            string shown = "Day " + _progression.TrialDay + " of " + _progression.TrialDurationDays + " · Forecast " + _progression.GradeForecast;
            if (shown != _dayChipText.text) _dayChipText.text = shown;
        }

        void RefreshCoordsCluster()
        {
            if (_player == null) return;
            var cell = _player.CurrentCell;
            string x = cell.x.ToString();
            string y = cell.y.ToString();
            string z = _player.Height.ToString();
            if (_coordX != null && x != _lastCoordX) { _lastCoordX = x; _coordX.text = x; }
            if (_coordY != null && y != _lastCoordY) { _lastCoordY = y; _coordY.text = y; }
            if (_coordZ != null && z != _lastCoordZ) { _lastCoordZ = z; _coordZ.text = z; }
        }

        void RefreshMinimapCluster()
        {
            if (_playerMarker == null || _player == null) return;
            Vector2 dir = _player.MoveDir;
            if (dir.sqrMagnitude < 1e-4f) return;
            float heading = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f + 45f;
            if (float.IsNaN(_lastHeading) || Mathf.Abs(Mathf.DeltaAngle(heading, _lastHeading)) > 1f)
            {
                _lastHeading = heading;
                _playerMarker.localEulerAngles = new Vector3(0f, 0f, heading);
            }
        }

        void RefreshQuestCluster()
        {
            if (_questVm == null) return;
            var pinned = _questVm.PinnedQuest;
            if (!pinned.HasValue)
            {
                if (_questTitleText != null) _questTitleText.text = "No Active Quest";
                if (_questObjMarks != null)
                    for (int i = 0; i < _questObjMarks.Length; i++)
                    {
                        if (_questObjMarks[i] != null) _questObjMarks[i].text = "";
                        if (_questObjTexts[i] != null) _questObjTexts[i].text = "";
                        if (_questObjProgs[i] != null) _questObjProgs[i].text = "";
                    }
                if (_questRewardText != null) _questRewardText.text = "";
                return;
            }
            var q = pinned.Value;
            if (_questTitleText != null) _questTitleText.text = string.IsNullOrEmpty(q.title) ? "Quest" : q.title;
            if (_questObjMarks != null && _questObjMarks.Length > 0)
            {
                if (_questObjMarks[0] != null) _questObjMarks[0].text = "◆";
                if (_questObjTexts[0] != null) _questObjTexts[0].text = q.objectiveText ?? "";
                if (_questObjProgs[0] != null)
                    _questObjProgs[0].text = q.objectiveRequired > 1
                        ? q.objectiveCurrent + "/" + q.objectiveRequired : "";
                for (int i = 1; i < _questObjMarks.Length; i++)
                {
                    if (_questObjMarks[i] != null) _questObjMarks[i].text = "";
                    if (_questObjTexts[i] != null) _questObjTexts[i].text = "";
                    if (_questObjProgs[i] != null) _questObjProgs[i].text = "";
                }
            }
            if (_questRewardText != null)
                _questRewardText.text = string.IsNullOrEmpty(q.rewardText) ? "" : "Reward: " + q.rewardText;
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
            if (_canvas != null) _canvas.gameObject.SetActive(show);
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = show ? 1f : 0f;
                _canvasGroup.blocksRaycasts = show;
                _canvasGroup.interactable = show;
            }
            SetVisible(_vitalsRoot, show);
            SetVisible(_hotbarRoot, show);
            SetVisible(_statusFxRoot, show);
            SetVisible(_dayBandRoot, show);
            SetVisible(_topRightRoot, show);
            SetVisible(_abilityRoot, show);
            if (!show)
            {
                if (_interactRoot != null) _interactRoot.gameObject.SetActive(false);
                if (_tutorialRoot != null) _tutorialRoot.gameObject.SetActive(false);
            }
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

        static void HardBorder(GameObject go, Color color, float dist = 2f)
        {
            var o = go.GetComponent<Outline>() ?? go.AddComponent<Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(dist, -dist);
            o.useGraphicAlpha = false;
        }

        // Inner stone bevel inset — the shared frame grammar from the design system
        // (a #2c313c hairline 2px inside the ink border). Every HUD box uses this so
        // their borders read identically. Non-interactive; draws only a thin frame.
        static void AddBevel(RectTransform box)
        {
            var bevel = NewImage(box, "Bevel", null, Color.clear);
            bevel.raycastTarget = false;
            HardBorder(bevel.gameObject, LitIsoTheme.Stone, 2f);
            var bvr = bevel.rectTransform;
            bvr.anchorMin = Vector2.zero; bvr.anchorMax = Vector2.one;
            bvr.offsetMin = new Vector2(2f, 2f); bvr.offsetMax = new Vector2(-2f, -2f);
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
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            LitIsoFont.Apply(t, size);
            t.resizeTextForBestFit = true;
            t.resizeTextMaxSize = t.fontSize;
            // Floor low enough that long live strings (e.g. a full weather forecast)
            // shrink to fit their box instead of spilling over the border. The user
            // wants autofit-inside over overflow, so favour shrink over clip.
            t.resizeTextMinSize = Mathf.Min(8, t.fontSize);
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.82f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            shadow.useGraphicAlpha = true;
            return t;
        }
    }
}
                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                         