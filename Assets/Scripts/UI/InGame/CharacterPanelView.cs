// PHASE_COMPLETE Phase3
using System;
using System.Collections.Generic;
using IsoCore.Foundation;
using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    // Owner-approved UI overhaul (2026-06-10): 9-tab System Window.
    // Skills = the skill web (the old XP-bucket list is retired);
    // Journal = quests; System = save/quit + System log; Settings is new.
    public enum CharacterPanelTab
    {
        Inventory,
        Crafting,
        Skills,
        Spells,
        Character,
        Journal,
        Map,
        Settings,
        System,
        Admin,
    }

    /// <summary>
    /// Canonical in-game uGUI panel for the LitRPG shell. It replaces the separate
    /// inventory/crafting/status windows with one tabbed surface.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterPanelView : MonoBehaviour
    {
        IInventoryViewModel _inventory;
        ICraftingViewModel _crafting;
        ICharacterSheetViewModel _character;
        ISkillWebViewModel _skillWeb;
        IAdminViewModel _admin;
        FoundationProgression _progression;
        FoundationQoLService _qol;

        Canvas _canvas;
        GameObject _root;
        RectTransform _body;
        Text _title;
        Button[] _tabButtons;
        CharacterPanelTab _activeTab = CharacterPanelTab.Inventory;
        string _selectedRecipeId;

        // --- smart-inventory interaction state (sort / context menu / drag).
        // Input is polled in Update (AbilityWheelView convention) and slots are
        // hit-tested by rect, so no EventSystems per-slot components are needed.
        const float DragHoldSeconds = 0.15f;
        RectTransform _overlay;        // canvas-level layer for menu + ghost; survives tab Refresh
        RectTransform[] _invSlotRects;
        Image[] _invSlotIcons;
        int _invSlotCount;
        int _pressSlot = -1;
        float _pressTime;
        bool _dragging;
        int _dragFrom = -1;
        RectTransform _dragGhost;
        RectTransform _ctxMenu;
        int _ctxSlot = -1;

        struct SkillUiBucket
        {
            public string title;
            public string subtitle;
            public Color color;
            public FoundationProgressionActivity[] activities;

            public SkillUiBucket(string title, string subtitle, Color color, params FoundationProgressionActivity[] activities)
            {
                this.title = title;
                this.subtitle = subtitle;
                this.color = color;
                this.activities = activities;
            }
        }

        static readonly SkillUiBucket[] SkillBuckets =
        {
            new SkillUiBucket("Combat & Warding",
                "Weapon timing, dungeon threat, patrol defense, and creature pressure.",
                new Color(0.92f, 0.38f, 0.24f, 1f),
                FoundationProgressionActivity.Combat, FoundationProgressionActivity.Creature),
            new SkillUiBucket("Gathering & Exploration",
                "Resource reading, routes, landmarks, harvest technique, and hidden finds.",
                new Color(0.50f, 0.78f, 0.48f, 1f),
                FoundationProgressionActivity.Harvest, FoundationProgressionActivity.Explore),
            new SkillUiBucket("Crafting & Building",
                "Stations, quality, repairs, structures, rooms, paths, and town utility.",
                new Color(0.88f, 0.68f, 0.36f, 1f),
                FoundationProgressionActivity.Craft, FoundationProgressionActivity.Build),
            new SkillUiBucket("Hearth & Settlement",
                "Farming, food, trade, comfort, requests, vendors, and visitor support.",
                new Color(0.72f, 0.56f, 0.92f, 1f),
                FoundationProgressionActivity.Farm, FoundationProgressionActivity.Trade),
            new SkillUiBucket("Lore & Magic",
                "Relics, memory pages, shrines, ward clues, affinities, and class evidence.",
                new Color(0.48f, 0.70f, 0.95f, 1f),
                FoundationProgressionActivity.Lore),
        };

        public bool IsOpen => _root != null && _root.activeSelf;
        public event Action Closed;

        public void Init(IInventoryViewModel inventory, ICraftingViewModel crafting,
            ICharacterSheetViewModel character, FoundationProgression progression, FoundationQoLService qol,
            ISkillWebViewModel skillWeb = null, IAdminViewModel admin = null)
        {
            Unsubscribe();
            _inventory = inventory;
            _crafting = crafting;
            _character = character;
            _skillWeb = skillWeb;
            _admin = admin;
            _progression = progression;
            _qol = qol;
            Build();
            Subscribe();
            Hide();
        }

        void OnDestroy()
        {
            Unsubscribe();
            CancelInventoryOps();
        }
        void OnEnable() => LitIsoFont.TextScaleChanged += HandleTextScaleChanged;
        void OnDisable() => LitIsoFont.TextScaleChanged -= HandleTextScaleChanged;

        void Subscribe()
        {
            if (_inventory != null) _inventory.Changed += Refresh;
            if (_crafting != null) _crafting.Changed += Refresh;
            if (_character != null) _character.Changed += Refresh;
            if (_skillWeb != null) _skillWeb.Changed += Refresh;
            if (_admin != null) _admin.Changed += Refresh;
            if (_progression != null) _progression.Changed += Refresh;
        }

        void Unsubscribe()
        {
            if (_inventory != null) _inventory.Changed -= Refresh;
            if (_crafting != null) _crafting.Changed -= Refresh;
            if (_character != null) _character.Changed -= Refresh;
            if (_skillWeb != null) _skillWeb.Changed -= Refresh;
            if (_admin != null) _admin.Changed -= Refresh;
            if (_progression != null) _progression.Changed -= Refresh;
        }

        public void Show(CharacterPanelTab tab)
        {
            if (_root != null && _root.activeSelf && _activeTab == tab)
                return;
            _activeTab = tab;
            if (_root != null) _root.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            CancelInventoryOps();
            if (_root != null) _root.SetActive(false);
            Closed?.Invoke();
        }

        void Build()
        {
            CancelInventoryOps();
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = UiBuilder.NewCanvas(transform, "CharacterPanelCanvas", 220);
            _root = new GameObject("Root", typeof(RectTransform));
            _root.transform.SetParent(_canvas.transform, false);
            UiBuilder.Stretch(_root.GetComponent<RectTransform>());

            var scrim = UiBuilder.NewScrim(_root.transform);
            var scrimButton = scrim.gameObject.AddComponent<Button>();
            scrimButton.transition = Selectable.Transition.None;
            scrimButton.onClick.AddListener(Hide);

            // ===== Book panel: 1640x900 stone frame with a gold inner line and
            //       four gold corner squares (litiso_frontend_deescaped.html isBook). =====
            var panel = UiBuilder.NewPanel(_root.transform, "Panel", "system_panel", LitIsoTheme.Panel);
            var pr = panel.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(1640f, 900f);
            PlayerResizableUi.Attach(pr, "panel.character", new Vector2(900f, 540f), new Vector2(1820f, 1010f));

            // gold corner accents (14px squares poking past the 3px border)
            BuildCornerAccent(panel.transform, new Vector2(0f, 1f), new Vector2(-3f, 3f));
            BuildCornerAccent(panel.transform, new Vector2(1f, 1f), new Vector2(3f, 3f));
            BuildCornerAccent(panel.transform, new Vector2(0f, 0f), new Vector2(-3f, -3f));
            BuildCornerAccent(panel.transform, new Vector2(1f, 0f), new Vector2(3f, -3f));

            // ===== Header rail (wordmark + tab strip + close), 3px bottom divider =====
            var header = UiBuilder.NewRect("Header", panel.transform);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.anchoredPosition = Vector2.zero;
            header.sizeDelta = new Vector2(0f, 62f);
            var headerDiv = UiBuilder.NewImage(header, "HeaderDiv", null, InvHardEdge);
            headerDiv.raycastTarget = false;
            var hdr = headerDiv.rectTransform;
            hdr.anchorMin = new Vector2(0f, 0f); hdr.anchorMax = new Vector2(1f, 0f);
            hdr.pivot = new Vector2(0f, 0f);
            hdr.anchoredPosition = Vector2.zero;
            hdr.sizeDelta = new Vector2(0f, 3f);

            // wordmark: gold diamond + "WANDERER" (Press Start 2P) + dim subtitle
            var diamond = UiBuilder.NewImage(header, "Diamond", null, LitIsoTheme.Gold);
            diamond.raycastTarget = false;
            var dmr = diamond.rectTransform;
            dmr.anchorMin = dmr.anchorMax = new Vector2(0f, 0.5f);
            dmr.pivot = new Vector2(0f, 0.5f);
            dmr.anchoredPosition = new Vector2(20f, -2f);
            dmr.sizeDelta = new Vector2(13f, 13f);
            dmr.localRotation = Quaternion.Euler(0f, 0f, 45f);

            _title = UiBuilder.NewText(header, "Title", "WANDERER", 16, TextAnchor.MiddleLeft, LitIsoTheme.Gold);
            _title.font = LitIsoTheme.DisplayFont;
            var tr = _title.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0f, 0.5f);
            tr.pivot = new Vector2(0f, 0.5f);
            tr.anchoredPosition = new Vector2(44f, -2f);
            tr.sizeDelta = new Vector2(150f, 24f);

            var subtitle = UiBuilder.NewText(header, "Subtitle", "· Lv 6 · Knight", 17, TextAnchor.MiddleLeft, InvMuted);
            var subr = subtitle.rectTransform;
            subr.anchorMin = subr.anchorMax = new Vector2(0f, 0.5f);
            subr.pivot = new Vector2(0f, 0.5f);
            subr.anchoredPosition = new Vector2(196f, -2f);
            subr.sizeDelta = new Vector2(160f, 22f);

            var close = UiBuilder.NewButton(header, "Close", "btn_close", "✕", 16, LitIsoTheme.ButtonStyle.Stone);
            close.onClick.AddListener(Hide);
            var cr = close.GetComponent<RectTransform>();
            cr.anchorMin = cr.anchorMax = new Vector2(1f, 0.5f);
            cr.pivot = new Vector2(1f, 0.5f);
            cr.anchoredPosition = new Vector2(-16f, -2f);
            cr.sizeDelta = new Vector2(48f, 44f);

            BuildTabs(header);

            _body = UiBuilder.NewRect("Body", panel.transform);
            _body.anchorMin = Vector2.zero;
            _body.anchorMax = Vector2.one;
            _body.offsetMin = new Vector2(6f, 6f);
            _body.offsetMax = new Vector2(-6f, -65f);

            // Overlay for the inventory context menu / drag ghost. Parented to
            // the canvas (not _body) and created last so it renders above the
            // panel and survives the per-tab Refresh teardown.
            _overlay = UiBuilder.NewRect("InvOpsOverlay", _canvas.transform);
            UiBuilder.Stretch(_overlay);
        }

        // gold 14px corner square poking past the panel's 3px border
        static void BuildCornerAccent(Transform parent, Vector2 anchor, Vector2 offset)
        {
            var sq = UiBuilder.NewImage(parent, "Corner", null, LitIsoTheme.Gold);
            sq.raycastTarget = false;
            var rt = sq.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(14f, 14f);
        }

        // Book header tab strip: Press Start 2P 12px, gold fill + gold bottom-border
        // when active, transparent with #a39e90 text when not (isBook/bookTabs).
        void BuildTabs(Transform parent)
        {
            var tabs = (CharacterPanelTab[])Enum.GetValues(typeof(CharacterPanelTab));
            _tabButtons = new Button[tabs.Length];
            // strip starts right of the wordmark and runs to just before the close button
            var strip = UiBuilder.NewRect("TabStrip", parent);
            strip.anchorMin = new Vector2(0f, 0f);
            strip.anchorMax = new Vector2(1f, 1f);
            strip.pivot = new Vector2(0f, 0.5f);
            strip.offsetMin = new Vector2(376f, 0f);
            strip.offsetMax = new Vector2(-74f, 0f);

            float x = 0f;
            for (int i = 0; i < tabs.Length; i++)
            {
                var tab = tabs[i];
                string label = LabelFor(tab).ToUpperInvariant();
                float w = 22f + label.Length * 8.2f;

                var img = UiBuilder.NewImage(strip, "Tab_" + tab, null, Color.clear);
                var btn = img.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.transition = Selectable.Transition.None;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(x, -2f);
                rt.sizeDelta = new Vector2(w, 40f);
                // 2px hard border + a thick bottom underline (gold when active)
                AddBorder(rt, InvHardEdge, 2f);
                var underline = UiBuilder.NewImage(img.transform, "Underline", null, Color.clear);
                underline.raycastTarget = false;
                var ur = underline.rectTransform;
                ur.anchorMin = new Vector2(0f, 0f); ur.anchorMax = new Vector2(1f, 0f);
                ur.pivot = new Vector2(0f, 0f);
                ur.anchoredPosition = Vector2.zero;
                ur.sizeDelta = new Vector2(0f, 4f);

                var t = UiBuilder.NewText(img.transform, "L", label, 12, TextAnchor.MiddleCenter, InvChipMuted);
                t.font = LitIsoTheme.DisplayFont;
                t.raycastTarget = false;
                UiBuilder.Stretch(t.rectTransform, 2f);
                UiBuilder.FitText(t);

                x += w + 4f;
                btn.onClick.AddListener(() => Show(tab));
                _tabButtons[i] = btn;
            }
        }

        bool _refreshing;

        void Refresh()
        {
            if (_body == null || !IsOpen) return;
            // Re-entrancy guard: a draw method that raises a Changed event (e.g. DrawCrafting
            // -> SetStationFilter) must not recursively re-enter Refresh. Unbounded recursion
            // throws an UNCATCHABLE StackOverflowException that wedges Unity's player loop, so
            // input, abilities and attacks stop responding.
            if (_refreshing) return;
            _refreshing = true;
            try
            {
            ClearBodyChildren();
            // slot widgets are about to be destroyed; the context menu would
            // point at stale data, so dismiss it (a drag survives — DrawInventory
            // re-applies the source-slot dim from _dragFrom).
            if (_ctxMenu != null) CloseContextMenu();
            _invSlotRects = null;
            _invSlotIcons = null;
            _invSlotCount = 0;
            UpdateTabButtons();
            // Header wordmark is fixed ("WANDERER") per the book design; the active
            // tab is shown by the gold tab in the strip, not by retitling the header.

            try
            {
                switch (_activeTab)
                {
                    case CharacterPanelTab.Inventory: DrawInventory(); break;
                    case CharacterPanelTab.Crafting: DrawCrafting(); break;
                    case CharacterPanelTab.Skills: SkillWebDrawer.Draw(_body, _skillWeb, Refresh); break;
                    case CharacterPanelTab.Spells: DrawSpells(); break;
                    case CharacterPanelTab.Character: DrawStatus(); break;
                    case CharacterPanelTab.Journal: DrawJournal(); break;
                    case CharacterPanelTab.Map: DrawMap(); break;
                    case CharacterPanelTab.Settings: DrawSettings(); break;
                    case CharacterPanelTab.System: DrawSystem(); break;
                    case CharacterPanelTab.Admin: DrawAdmin(); break;
                }
            }
            catch (Exception e)
            {
                // A failing tab must never blank the whole panel; surface the
                // error in place so it can be reported and fixed.
                Debug.LogException(e);
                var err = UiBuilder.NewText(_body, "TabError",
                    $"This tab hit an error:\n{e.GetType().Name}: {e.Message}\n(see Console for stack)",
                    16, TextAnchor.UpperLeft, new Color(0.95f, 0.55f, 0.45f, 1f));
                err.horizontalOverflow = HorizontalWrapMode.Wrap;
                err.verticalOverflow = VerticalWrapMode.Truncate;
                UiBuilder.FitText(err);
                UiBuilder.Stretch(err.rectTransform, 8f);
            }
            }
            finally { _refreshing = false; }
        }

        void ClearBodyChildren()
        {
            while (_body.childCount > 0)
            {
                var child = _body.GetChild(0);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }

        void UpdateTabButtons()
        {
            if (_tabButtons == null) return;
            var tabs = (CharacterPanelTab[])Enum.GetValues(typeof(CharacterPanelTab));
            for (int i = 0; i < _tabButtons.Length && i < tabs.Length; i++)
            {
                var img = _tabButtons[i].targetGraphic as Image;
                if (img == null) continue;
                bool active = tabs[i] == _activeTab;
                // Active: gold fill, dark text, gold underline. Inactive: transparent
                // fill, dim text, transparent underline (matches bookTabs styling).
                img.color = active ? LitIsoTheme.Gold : Color.clear;
                var lbl = _tabButtons[i].GetComponentInChildren<Text>();
                if (lbl != null) lbl.color = active ? LitIsoTheme.Hex("#1a1408") : InvChipMuted;
                var underline = img.transform.Find("Underline")?.GetComponent<Image>();
                if (underline != null) underline.color = active ? LitIsoTheme.Gold : Color.clear;
            }
        }

        // ---- Inventory book design tokens (from litiso_frontend_deescaped.html, isInv block) ----
        static readonly Color InvSlotFill   = LitIsoTheme.Hex("#0e1014"); // occupied-slot well
        static readonly Color InvPadFill    = LitIsoTheme.Hex("#101216"); // empty pad fill
        static readonly Color InvPadBorder  = LitIsoTheme.Hex("#1a1d23"); // empty pad border
        static readonly Color InvHardEdge   = LitIsoTheme.Hex("#0a0b0e"); // 2px inset / hard border
        static readonly Color InvDetailBg   = LitIsoTheme.Hex("#101216"); // right detail column
        static readonly Color InvBevel      = LitIsoTheme.Hex("#2c313c"); // inset stone bevel
        static readonly Color RarityCommon  = LitIsoTheme.Hex("#5a6068");
        static readonly Color RarityUncommon= LitIsoTheme.Hex("#5aa05a");
        static readonly Color RarityRare    = LitIsoTheme.Hex("#4f8ad9");
        static readonly Color RarityEpic    = LitIsoTheme.Hex("#a060d9");
        static readonly Color RarityLegend  = LitIsoTheme.Gold;           // #E8C468
        static readonly Color DurGood       = LitIsoTheme.Hex("#5aa05a");
        static readonly Color DurMid        = LitIsoTheme.Hex("#e8a03c");
        static readonly Color DurLow        = LitIsoTheme.Hex("#d9425a");
        static readonly Color InvMuted      = LitIsoTheme.Hex("#8a8578"); // footer / labels
        static readonly Color InvChipMuted  = LitIsoTheme.Hex("#a39e90"); // inactive chip text
        static readonly Color InvChipBg     = LitIsoTheme.Hex("#1a1d23"); // inactive chip fill

        const int InvCols = 7;
        const float InvSlot = 84f;
        const float InvGap = 9f;
        const float InvTotalSlots = 35f; // design pads the grid to 35 cells
        int _invSelected = -1;           // selected slot for the detail column (visual)

        // Heuristic rarity from the live slot's stack/durability (no rarity field
        // on the Foundation item model yet — see FoundationInventoryAdapter notes).
        static Color RarityForSlot(HudSlot s)
        {
            if (s.durability01 > 0f) return RarityRare;   // gear-like (durability) reads as rare
            if (s.count <= 1) return RarityUncommon;      // singletons read as uncommon
            return RarityCommon;
        }

        void DrawInventory()
        {
            int cap = Mathf.Max(0, _inventory?.Capacity ?? 0);
            if (cap == 0)
            {
                TextLine("Inventory unavailable", 0, 18, UiBuilder.MutedCol);
                return;
            }

            // ===== Two-column layout: left grid area + 380px right detail =====
            const float detailW = 380f;
            const float padL = 26f, padTop = 22f;

            var left = UiBuilder.NewRect("InvLeft", _body);
            left.anchorMin = Vector2.zero;
            left.anchorMax = Vector2.one;
            left.offsetMin = new Vector2(padL, 0f);
            left.offsetMax = new Vector2(-detailW - 3f, -padTop);

            // ---- filter chips + sort selector ----
            string[] chips = { "ALL", "WEAPONS", "ARMOR", "MATERIALS" };
            float chipX = 0f;
            for (int c = 0; c < chips.Length; c++)
                chipX += DrawFilterChip(left, chips[c], c == 0, chipX) + 8f;

            var sortLabel = UiBuilder.NewText(left, "SortLabel", "Sort:", 17, TextAnchor.MiddleRight, InvMuted);
            var slr = sortLabel.rectTransform;
            slr.anchorMin = new Vector2(1f, 1f); slr.anchorMax = new Vector2(1f, 1f);
            slr.pivot = new Vector2(1f, 1f);
            slr.anchoredPosition = new Vector2(-88f, -4f);
            slr.sizeDelta = new Vector2(70f, 26f);
            var sortBtn = UiBuilder.NewButton(left, "SortBtn", "button", "Rarity ▾", 14, LitIsoTheme.ButtonStyle.Stone);
            var sbr = sortBtn.GetComponent<RectTransform>();
            sbr.anchorMin = new Vector2(1f, 1f); sbr.anchorMax = new Vector2(1f, 1f);
            sbr.pivot = new Vector2(1f, 1f);
            sbr.anchoredPosition = new Vector2(0f, -2f);
            sbr.sizeDelta = new Vector2(86f, 30f);
            var sbLbl = sortBtn.GetComponentInChildren<Text>();
            if (sbLbl != null) sbLbl.color = LitIsoTheme.Gold;
            sortBtn.onClick.AddListener(() => { CancelInventoryOps(); _inventory?.SortInventory(); });

            // ---- item grid (7 cols x 84px, padded to 35 cells) ----
            var grid = UiBuilder.NewRect("InvGrid", left);
            grid.anchorMin = new Vector2(0f, 1f);
            grid.anchorMax = new Vector2(1f, 1f);
            grid.pivot = new Vector2(0f, 1f);
            grid.anchoredPosition = new Vector2(0f, -44f);
            // height covers 5 rows of the 35-cell pad
            grid.sizeDelta = new Vector2(0f, 5f * InvSlot + 4f * InvGap);

            int cells = Mathf.Max(cap, Mathf.RoundToInt(InvTotalSlots));
            _invSlotRects = new RectTransform[cap];
            _invSlotIcons = new Image[cap];
            _invSlotCount = cap;
            HudSlot selSlot = default; Color selRarity = RarityCommon; int selIndex = -1;

            for (int i = 0; i < cells; i++)
            {
                int row = i / InvCols;
                int col = i % InvCols;
                float x = col * (InvSlot + InvGap);
                float y = -row * (InvSlot + InvGap);

                if (i >= cap)
                {
                    // empty pad cell
                    var pad = UiBuilder.NewImage(grid, "InvPad_" + i, null, InvPadFill);
                    pad.raycastTarget = false;
                    var padRt = pad.rectTransform;
                    padRt.anchorMin = padRt.anchorMax = new Vector2(0f, 1f);
                    padRt.pivot = new Vector2(0f, 1f);
                    padRt.anchoredPosition = new Vector2(x, y);
                    padRt.sizeDelta = new Vector2(InvSlot, InvSlot);
                    AddBorder(padRt, InvPadBorder, 2f);
                    AddInset(padRt, InvHardEdge, 2f);
                    continue;
                }

                var s = _inventory.GetSlot(i);
                bool occupied = !string.IsNullOrWhiteSpace(s.label) && s.count > 0;
                bool selected = _invSelected == i;
                Color rarity = occupied ? RarityForSlot(s) : InvPadBorder;

                if (!occupied)
                {
                    // an in-capacity but empty slot draws like a pad too
                    var emptyImg = UiBuilder.NewImage(grid, "InvSlot_" + i, null, InvPadFill);
                    var er = emptyImg.rectTransform;
                    er.anchorMin = er.anchorMax = new Vector2(0f, 1f);
                    er.pivot = new Vector2(0f, 1f);
                    er.anchoredPosition = new Vector2(x, y);
                    er.sizeDelta = new Vector2(InvSlot, InvSlot);
                    AddBorder(er, InvPadBorder, 2f);
                    AddInset(er, InvHardEdge, 2f);
                    _invSlotRects[i] = er;
                    _invSlotIcons[i] = null;
                    continue;
                }

                // occupied slot: dark well + rarity (or gold-if-selected) border + inset
                var cell = UiBuilder.NewImage(grid, "InvSlot_" + i, null, InvSlotFill);
                var rt = cell.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(x, y);
                rt.sizeDelta = new Vector2(InvSlot, InvSlot);
                AddBorder(rt, selected ? RarityLegend : rarity, 2f);
                AddInset(rt, InvHardEdge, 2f);
                _invSlotRects[i] = rt;

                int slotIndex = i;
                var btn = cell.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => { _invSelected = slotIndex; Refresh(); });

                // icon: inset 15px (17px bottom) with a 3px rarity "edge" frame
                var icon = UiBuilder.NewImage(cell.transform, "Icon", s.icon, Color.white);
                icon.preserveAspect = true;
                icon.enabled = s.icon != null;
                icon.raycastTarget = false;
                var ir = icon.rectTransform;
                ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one;
                ir.offsetMin = new Vector2(15f, 17f);
                ir.offsetMax = new Vector2(-15f, -15f);
                if (s.icon == null)
                {
                    // no sprite: paint the icon area as a rarity-tinted block (matches the mockup's coloured icons)
                    icon.enabled = true;
                    icon.sprite = null;
                    icon.color = new Color(rarity.r, rarity.g, rarity.b, 0.85f);
                }
                AddBorder(ir, new Color(rarity.r * 0.6f, rarity.g * 0.6f, rarity.b * 0.6f, 1f), 3f);
                _invSlotIcons[i] = icon;
                if (_dragging && i == _dragFrom)
                    icon.color = new Color(icon.color.r, icon.color.g, icon.color.b, 0.35f);

                if (s.count > 1)
                {
                    var count = UiBuilder.NewText(cell.transform, "Count", s.count.ToString(), 11, TextAnchor.LowerRight, Color.white);
                    count.font = LitIsoTheme.DisplayFont;
                    count.raycastTarget = false;
                    var cr = count.rectTransform;
                    cr.anchorMin = new Vector2(0f, 0f); cr.anchorMax = new Vector2(1f, 0f);
                    cr.pivot = new Vector2(1f, 0f);
                    cr.anchoredPosition = new Vector2(-5f, 7f);
                    cr.sizeDelta = new Vector2(InvSlot - 8f, 16f);
                }

                if (s.durability01 > 0f)
                {
                    var durTrack = UiBuilder.NewImage(cell.transform, "DurTrack", null, LitIsoTheme.Hex("#0a0c10"));
                    durTrack.raycastTarget = false;
                    var dtr = durTrack.rectTransform;
                    dtr.anchorMin = new Vector2(0f, 0f); dtr.anchorMax = new Vector2(1f, 0f);
                    dtr.pivot = new Vector2(0f, 0f);
                    dtr.anchoredPosition = new Vector2(0f, 5f);
                    dtr.offsetMin = new Vector2(6f, 5f); dtr.offsetMax = new Vector2(-6f, 5f);
                    dtr.sizeDelta = new Vector2(dtr.sizeDelta.x, 5f);
                    var durFill = UiBuilder.NewImage(durTrack.transform, "DurFill", null,
                        s.durability01 > 0.5f ? DurGood : s.durability01 > 0.25f ? DurMid : DurLow);
                    durFill.raycastTarget = false;
                    durFill.type = Image.Type.Filled;
                    durFill.fillMethod = Image.FillMethod.Horizontal;
                    durFill.fillAmount = Mathf.Clamp01(s.durability01);
                    UiBuilder.Stretch(durFill.rectTransform);
                }

                if (selected)
                {
                    selSlot = s; selRarity = rarity; selIndex = i;
                }
                else if (selIndex < 0 && _invSelected < 0)
                {
                    // no explicit selection yet -> default the detail to the first item
                    selSlot = s; selRarity = rarity; selIndex = i;
                }
            }

            // ---- footer: Slots + Weight ----
            int used = 0;
            for (int i = 0; i < cap; i++)
            {
                var s = _inventory.GetSlot(i);
                if (!string.IsNullOrWhiteSpace(s.label) && s.count > 0) used++;
            }
            DrawInvFooter(left, used, cap);

            // ---- right detail column ----
            DrawInvDetail(detailW, selIndex >= 0, selSlot, selRarity);
        }

        float DrawFilterChip(RectTransform parent, string label, bool active, float x)
        {
            float w = 14f + label.Length * 8.5f; // approx Press Start 2P metrics
            var chip = UiBuilder.NewImage(parent, "Chip_" + label, null, active ? LitIsoTheme.Gold : InvChipBg);
            chip.raycastTarget = false;
            var rt = chip.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -2f);
            rt.sizeDelta = new Vector2(w, 30f);
            if (!active) AddBorder(rt, InvBevel, 2f);
            var t = UiBuilder.NewText(chip.transform, "L", label, 10, TextAnchor.MiddleCenter,
                active ? LitIsoTheme.Hex("#1a1408") : InvChipMuted);
            t.font = LitIsoTheme.DisplayFont;
            t.raycastTarget = false;
            UiBuilder.Stretch(t.rectTransform, 2f);
            UiBuilder.FitText(t);
            return w;
        }

        void DrawInvFooter(RectTransform parent, int used, int cap)
        {
            var footer = UiBuilder.NewRect("InvFooter", parent);
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0f, 0f);
            footer.anchoredPosition = new Vector2(0f, 0f);
            footer.sizeDelta = new Vector2(0f, 40f);
            // 2px top divider
            var div = UiBuilder.NewImage(footer, "Div", null, InvHardEdge);
            div.raycastTarget = false;
            var dr = div.rectTransform;
            dr.anchorMin = new Vector2(0f, 1f); dr.anchorMax = new Vector2(1f, 1f);
            dr.pivot = new Vector2(0f, 1f);
            dr.anchoredPosition = Vector2.zero;
            dr.sizeDelta = new Vector2(0f, 2f);

            var slots = UiBuilder.NewText(footer, "Slots", $"Slots: {used} / {cap}", 17, TextAnchor.MiddleLeft, InvMuted);
            var slr = slots.rectTransform;
            slr.anchorMin = new Vector2(0f, 0f); slr.anchorMax = new Vector2(0.5f, 1f);
            slr.offsetMin = new Vector2(0f, 0f); slr.offsetMax = new Vector2(0f, -8f);

            // approx the design's 80kg cap (no live weight model yet — see report)
            var weight = UiBuilder.NewText(footer, "Weight", $"Weight: {used * 3:0.0} / {cap * 3:0.0}", 17, TextAnchor.MiddleRight, InvMuted);
            var wr = weight.rectTransform;
            wr.anchorMin = new Vector2(0.5f, 0f); wr.anchorMax = new Vector2(1f, 1f);
            wr.offsetMin = new Vector2(0f, 0f); wr.offsetMax = new Vector2(0f, -8f);
        }

        void DrawInvDetail(float width, bool hasSel, HudSlot s, Color rarity)
        {
            var panel = UiBuilder.NewImage(_body, "InvDetail", null, InvDetailBg);
            var pr = panel.rectTransform;
            pr.anchorMin = new Vector2(1f, 0f); pr.anchorMax = new Vector2(1f, 1f);
            pr.pivot = new Vector2(1f, 0.5f);
            pr.anchoredPosition = Vector2.zero;
            pr.sizeDelta = new Vector2(width, 0f);
            // left edge: 3px hard border + 2px stone inset
            var edge = UiBuilder.NewImage(panel.transform, "Edge", null, InvHardEdge);
            edge.raycastTarget = false;
            var er = edge.rectTransform;
            er.anchorMin = new Vector2(0f, 0f); er.anchorMax = new Vector2(0f, 1f);
            er.pivot = new Vector2(0f, 0.5f);
            er.anchoredPosition = Vector2.zero;
            er.sizeDelta = new Vector2(3f, 0f);

            if (!hasSel)
            {
                var empty = UiBuilder.NewText(panel.transform, "Empty", "Select an item to inspect it.", 16, TextAnchor.UpperLeft, InvMuted);
                empty.horizontalOverflow = HorizontalWrapMode.Wrap;
                var emr = empty.rectTransform;
                emr.anchorMin = new Vector2(0f, 1f); emr.anchorMax = new Vector2(1f, 1f);
                emr.pivot = new Vector2(0f, 1f);
                emr.anchoredPosition = new Vector2(26f, -26f);
                emr.sizeDelta = new Vector2(-52f, 60f);
                UiBuilder.FitText(empty);
                return;
            }

            const float ipad = 26f;
            // header: 88px icon well + name/type
            var iconWell = UiBuilder.NewImage(panel.transform, "DetailIcon", null, InvSlotFill);
            iconWell.raycastTarget = false;
            var iwr = iconWell.rectTransform;
            iwr.anchorMin = new Vector2(0f, 1f); iwr.anchorMax = new Vector2(0f, 1f);
            iwr.pivot = new Vector2(0f, 1f);
            iwr.anchoredPosition = new Vector2(ipad, -ipad);
            iwr.sizeDelta = new Vector2(88f, 88f);
            AddBorder(iwr, rarity, 2f);
            AddInset(iwr, InvHardEdge, 2f);
            var dIcon = UiBuilder.NewImage(iconWell.transform, "Icon", s.icon, Color.white);
            dIcon.preserveAspect = true;
            dIcon.raycastTarget = false;
            var dir = dIcon.rectTransform;
            dir.anchorMin = Vector2.zero; dir.anchorMax = Vector2.one;
            dir.offsetMin = new Vector2(15f, 15f); dir.offsetMax = new Vector2(-15f, -15f);
            if (s.icon == null) dIcon.color = new Color(rarity.r, rarity.g, rarity.b, 0.85f);
            AddBorder(dir, new Color(rarity.r * 0.6f, rarity.g * 0.6f, rarity.b * 0.6f, 1f), 3f);

            var name = UiBuilder.NewText(panel.transform, "Name", string.IsNullOrWhiteSpace(s.label) ? "Item" : s.label, 24, TextAnchor.UpperLeft, rarity);
            name.font = LitIsoTheme.BodyFont;
            name.fontStyle = FontStyle.Bold;
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            var nmr = name.rectTransform;
            nmr.anchorMin = new Vector2(0f, 1f); nmr.anchorMax = new Vector2(1f, 1f);
            nmr.pivot = new Vector2(0f, 1f);
            nmr.anchoredPosition = new Vector2(ipad + 88f + 18f, -ipad - 6f);
            nmr.sizeDelta = new Vector2(-(ipad + 88f + 18f + ipad), 32f);
            UiBuilder.FitText(name);

            string typeStr = RarityLabel(rarity) + (s.durability01 > 0f ? " · Equipment" : s.count > 1 ? " · Material" : " · Item");
            var type = UiBuilder.NewText(panel.transform, "Type", typeStr, 17, TextAnchor.UpperLeft, InvMuted);
            var tyr = type.rectTransform;
            tyr.anchorMin = new Vector2(0f, 1f); tyr.anchorMax = new Vector2(1f, 1f);
            tyr.pivot = new Vector2(0f, 1f);
            tyr.anchoredPosition = new Vector2(ipad + 88f + 18f, -ipad - 44f);
            tyr.sizeDelta = new Vector2(-(ipad + 88f + 18f + ipad), 22f);
            UiBuilder.FitText(type);

            // stat rows
            float ry = ipad + 88f + 20f;
            ry += DetailStatRow(panel.transform, "Durability", s.durability01 > 0f ? $"{Mathf.RoundToInt(s.durability01 * 100f)} / 100" : "—", LitIsoTheme.Parchment, ipad, ry);
            ry += DetailStatRow(panel.transform, "Quantity", (s.count > 0 ? s.count : 1).ToString(), LitIsoTheme.Parchment, ipad, ry);
            ry += DetailStatRow(panel.transform, "Sell value", $"{Mathf.Max(1, s.count) * 6} G", LitIsoTheme.Gold, ipad, ry);

            // flavour text
            var flavour = UiBuilder.NewText(panel.transform, "Flavour",
                "“Kept close through the long roads — worn, but it still serves.”", 18, TextAnchor.UpperLeft, InvMuted);
            flavour.fontStyle = FontStyle.Italic;
            flavour.horizontalOverflow = HorizontalWrapMode.Wrap;
            var flr = flavour.rectTransform;
            flr.anchorMin = new Vector2(0f, 1f); flr.anchorMax = new Vector2(1f, 1f);
            flr.pivot = new Vector2(0f, 1f);
            flr.anchoredPosition = new Vector2(ipad, -(ry + 18f));
            flr.sizeDelta = new Vector2(-ipad * 2f, 72f);

            // EQUIP / DROP action buttons
            var equip = UiBuilder.NewButton(panel.transform, "EquipBtn", "button", "EQUIP", 13, LitIsoTheme.ButtonStyle.Gold);
            equip.interactable = false; // equipment system not wired yet — render disabled instead of a silent no-op
            var eqr = equip.GetComponent<RectTransform>();
            eqr.anchorMin = new Vector2(0f, 0f); eqr.anchorMax = new Vector2(1f, 0f);
            eqr.pivot = new Vector2(0f, 0f);
            eqr.anchoredPosition = new Vector2(ipad, ipad);
            eqr.offsetMin = new Vector2(ipad, ipad);
            eqr.offsetMax = new Vector2(-ipad - 96f, ipad + 50f);

            int dropIdx = _invSelected;
            var drop = UiBuilder.NewButton(panel.transform, "DropBtn", "button", "DROP", 13, LitIsoTheme.ButtonStyle.Stone);
            var dpr = drop.GetComponent<RectTransform>();
            dpr.anchorMin = new Vector2(1f, 0f); dpr.anchorMax = new Vector2(1f, 0f);
            dpr.pivot = new Vector2(1f, 0f);
            dpr.anchoredPosition = new Vector2(-ipad, ipad);
            dpr.sizeDelta = new Vector2(86f, 50f);
            drop.onClick.AddListener(() =>
            {
                if (dropIdx < 0) return;
                int count = _inventory.GetSlot(dropIdx).count;
                if (count > 0 && !_inventory.DropItem(dropIdx, count))
                    Debug.Log("[Inventory] Drop unavailable — pending Foundation world-drop op.");
            });
        }

        float DetailStatRow(Transform parent, string key, string value, Color valueColor, float ipad, float y)
        {
            var row = UiBuilder.NewRect("Stat_" + key, (RectTransform)parent);
            row.anchorMin = new Vector2(0f, 1f); row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0f, 1f);
            row.anchoredPosition = new Vector2(ipad, -y);
            row.sizeDelta = new Vector2(-ipad * 2f, 30f);
            var k = UiBuilder.NewText(row, "K", key, 18, TextAnchor.MiddleLeft, InvChipMuted);
            UiBuilder.Stretch(k.rectTransform);
            var v = UiBuilder.NewText(row, "V", value, 18, TextAnchor.MiddleRight, valueColor);
            UiBuilder.Stretch(v.rectTransform);
            // bottom hairline divider
            var div = UiBuilder.NewImage(row, "Div", null, LitIsoTheme.Hex("#23262E"));
            div.raycastTarget = false;
            var dr = div.rectTransform;
            dr.anchorMin = new Vector2(0f, 0f); dr.anchorMax = new Vector2(1f, 0f);
            dr.pivot = new Vector2(0f, 0f);
            dr.anchoredPosition = Vector2.zero;
            dr.sizeDelta = new Vector2(0f, 1f);
            return 38f;
        }

        static string RarityLabel(Color rarity)
        {
            if (rarity == RarityLegend)   return "Legendary";
            if (rarity == RarityEpic)     return "Epic";
            if (rarity == RarityRare)     return "Rare";
            if (rarity == RarityUncommon) return "Uncommon";
            return "Common";
        }

        // 2px (default) hard border drawn as an Outline on the given rect's Image.
        static void AddBorder(RectTransform rt, Color color, float dist)
        {
            var img = rt.GetComponent<Image>();
            if (img == null) return;
            var o = rt.gameObject.GetComponent<Outline>() ?? rt.gameObject.AddComponent<Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(dist, -dist);
            o.useGraphicAlpha = false;
        }

        // inset hard line (the mockup's "box-shadow:inset 0 0 0 Npx" frame) drawn
        // as four thin edge strips hugging the inside of the parent's rect, so the
        // parent's fill stays visible in the centre.
        static void AddInset(RectTransform parent, Color color, float thickness)
        {
            void Strip(string n, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
            {
                var go = new GameObject(n, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
                var img = go.GetComponent<Image>();
                img.color = color;
                img.raycastTarget = false;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = aMin; rt.anchorMax = aMax;
                rt.offsetMin = oMin; rt.offsetMax = oMax;
            }
            Strip("InsetT", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), new Vector2(0f, 0f));
            Strip("InsetB", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, thickness));
            Strip("InsetL", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(thickness, 0f));
            Strip("InsetR", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thickness, 0f), new Vector2(0f, 0f));
        }

        // ====================================================================
        // Smart inventory: SORT button, right-click context menu, hold-to-drag.
        // Input is polled in Update (same convention as AbilityWheelView's
        // hold-X detection); slots are hit-tested against their RectTransforms
        // so the rebuilt-every-Refresh slot widgets need no extra components.
        // ====================================================================

        void DrawSortButton(float rightEdgeX)
        {
            var btn = UiBuilder.NewButton(_body, "SortBtn", "button", "", 13);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(rightEdgeX, 0f);
            rt.sizeDelta = new Vector2(88f, 26f);
            btn.onClick.AddListener(() =>
            {
                CancelInventoryOps();
                _inventory?.SortInventory();
            });

            // optional generated icon (Resources/UI/InGame/icon_sort); text-only fallback
            float textX = 6f;
            var sortSpr = UiBuilder.Spr("icon_sort");
            if (sortSpr != null)
            {
                var icon = UiBuilder.NewImage(btn.transform, "Icon", sortSpr, Color.white);
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                var ir = icon.rectTransform;
                ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f);
                ir.pivot = new Vector2(0f, 0.5f);
                ir.anchoredPosition = new Vector2(6f, 0f);
                ir.sizeDelta = new Vector2(18f, 18f);
                textX = 28f;
            }
            var label = UiBuilder.NewText(btn.transform, "Label", "Sort", 13, TextAnchor.MiddleCenter, UiBuilder.TextCol);
            label.raycastTarget = false;
            var lr = label.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(textX, 1f);
            lr.offsetMax = new Vector2(-6f, -1f);
        }

        void Update()
        {
            if (!IsOpen || _activeTab != CharacterPanelTab.Inventory)
            {
                CancelInventoryOps();
                return;
            }
            UpdateInventoryOps();
        }

        void UpdateInventoryOps()
        {
            if (_inventory == null) return;
            Vector2 mouse = Input.mousePosition;

            // ghost tracks the cursor every frame while dragging
            if (_dragging && _dragGhost != null)
                _dragGhost.anchoredPosition = OverlayPoint(mouse);

            if (_ctxMenu != null)
            {
                // pointer-down anywhere outside the menu dismisses it; clicks
                // inside resolve via the row Buttons (which fire on pointer-up)
                if ((Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) &&
                    !RectTransformUtility.RectangleContainsScreenPoint(_ctxMenu, mouse))
                    CloseContextMenu();
                return; // no drags while the menu is up
            }

            if (_dragging)
            {
                if (Input.GetMouseButtonUp(0)) EndDrag(mouse);
                return;
            }

            // right-click on an occupied slot → context menu at the cursor
            if (Input.GetMouseButtonDown(1))
            {
                int slot = SlotUnderPointer(mouse);
                if (slot >= 0 && _inventory.GetSlot(slot).count > 0)
                    OpenContextMenu(slot, mouse);
                _pressSlot = -1;
                return;
            }

            // press on an occupied slot arms a potential drag…
            if (Input.GetMouseButtonDown(0))
            {
                int slot = SlotUnderPointer(mouse);
                if (slot >= 0 && _inventory.GetSlot(slot).count > 0)
                {
                    _pressSlot = slot;
                    _pressTime = Time.unscaledTime;
                }
            }

            // …which starts once the hold passes the threshold. A quick click
            // (released earlier) keeps the panel's default slot behavior.
            if (_pressSlot >= 0)
            {
                if (!Input.GetMouseButton(0))
                    _pressSlot = -1;
                else if (Time.unscaledTime - _pressTime >= DragHoldSeconds)
                    BeginDrag(_pressSlot);
            }
        }

        int SlotUnderPointer(Vector2 screenPos)
        {
            if (_invSlotRects == null) return -1;
            for (int i = 0; i < _invSlotCount; i++)
            {
                var rt = _invSlotRects[i];
                if (rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos))
                    return i;
            }
            return -1;
        }

        // screen point → local point on the (full-canvas, center-pivot) overlay
        Vector2 OverlayPoint(Vector2 screenPos)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_overlay, screenPos, null, out var local);
            return local;
        }

        void BeginDrag(int slot)
        {
            _pressSlot = -1;
            var s = _inventory.GetSlot(slot);
            if (s.count <= 0 || _overlay == null) return;

            _dragging = true;
            _dragFrom = slot;

            if (_invSlotIcons != null && slot < _invSlotIcons.Length && _invSlotIcons[slot] != null)
                _invSlotIcons[slot].color = new Color(1f, 1f, 1f, 0.35f);

            // ghost: optional skin frame (Resources/UI/InGame/drag_ghost) + the
            // item icon; semi-transparent and raycast-invisible throughout.
            _dragGhost = UiBuilder.NewRect("DragGhost", _overlay);
            _dragGhost.sizeDelta = new Vector2(58f, 58f);
            var frame = UiBuilder.NewImage(_dragGhost, "Frame", UiBuilder.Spr("drag_ghost"), Color.clear);
            frame.raycastTarget = false;
            UiBuilder.Stretch(frame.rectTransform);
            if (frame.sprite != null)
            {
                frame.type = Image.Type.Sliced;
                frame.color = new Color(1f, 1f, 1f, 0.85f);
            }
            else if (s.icon == null)
            {
                // no skin and no item icon: flat translucent square fallback
                frame.color = new Color(UiBuilder.SlotBg.r, UiBuilder.SlotBg.g, UiBuilder.SlotBg.b, 0.6f);
            }
            else
            {
                frame.enabled = false;
            }
            var icon = UiBuilder.NewImage(_dragGhost, "Icon", s.icon, Color.clear);
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.enabled = s.icon != null;
            icon.color = new Color(1f, 1f, 1f, 0.7f);
            UiBuilder.Stretch(icon.rectTransform, 6f);
            _dragGhost.anchoredPosition = OverlayPoint(Input.mousePosition);

            FoundationUiCoordinator.Active?.SetModalOpen("inventoryOps", true);
        }

        void EndDrag(Vector2 screenPos)
        {
            int from = _dragFrom;
            int target = SlotUnderPointer(screenPos);
            CancelDrag(); // clear ghost/dim/modal before the VM op triggers Refresh
            if (target < 0 || target == from)
                return;   // released outside the grid = cancel (dropping is context-menu only)

            var t = _inventory.GetSlot(target);
            if (t.count > 0) _inventory.SwapSlots(from, target);
            else _inventory.MoveSlot(from, target);
        }

        void CancelDrag()
        {
            if (_dragGhost != null) Destroy(_dragGhost.gameObject);
            _dragGhost = null;
            if (_dragging && _invSlotIcons != null && _dragFrom >= 0 &&
                _dragFrom < _invSlotIcons.Length && _invSlotIcons[_dragFrom] != null)
                _invSlotIcons[_dragFrom].color = Color.white;
            _dragging = false;
            _dragFrom = -1;
            _pressSlot = -1;
            FoundationUiCoordinator.Active?.SetModalOpen("inventoryOps", _ctxMenu != null);
        }

        void OpenContextMenu(int slot, Vector2 screenPos)
        {
            _ctxSlot = slot;
            BuildContextMenu(false, screenPos);
            FoundationUiCoordinator.Active?.SetModalOpen("inventoryOps", _ctxMenu != null);
        }

        void BuildContextMenu(bool splitMode, Vector2 screenPos)
        {
            if (_ctxMenu != null) Destroy(_ctxMenu.gameObject);
            _ctxMenu = null;
            if (_overlay == null) return;

            var s = _inventory.GetSlot(_ctxSlot);
            if (s.count <= 0) return;

            const float w = 200f, rowH = 36f, rowGap = 4f, pad = 8f, headH = 24f;
            int rows = splitMode ? 3 : (s.count > 1 ? 3 : 2);
            float h = pad * 2f + headH + 2f + rows * (rowH + rowGap);

            // skinnable via Resources/UI/InGame/context_menu; flat panel fallback
            var panel = UiBuilder.NewPanel(_overlay, "InvContextMenu", "context_menu",
                new Color(0.07f, 0.08f, 0.11f, 0.97f));
            _ctxMenu = panel.rectTransform;
            _ctxMenu.anchorMin = _ctxMenu.anchorMax = new Vector2(0.5f, 0.5f);
            _ctxMenu.pivot = new Vector2(0f, 1f);
            _ctxMenu.sizeDelta = new Vector2(w, h);
            var lp = OverlayPoint(screenPos);
            var bounds = _overlay.rect.size * 0.5f; // keep the menu on-canvas
            lp.x = Mathf.Min(lp.x, bounds.x - w);
            lp.y = Mathf.Max(lp.y, -bounds.y + h);
            _ctxMenu.anchoredPosition = lp;

            string head = splitMode ? $"Split {s.label}" : $"{s.label} x{s.count}";
            var title = UiBuilder.NewText(panel.transform, "Head", head, 13, TextAnchor.MiddleLeft, UiBuilder.MutedCol);
            Place(title.rectTransform, pad + 2f, pad, w - pad * 2f, headH);
            UiBuilder.FitText(title);

            float y = pad + headH + 2f;
            if (!splitMode)
            {
                if (s.count > 1)
                {
                    ContextRow(panel.transform, "Split stack", "icon_split", y,
                        () => BuildContextMenu(true, screenPos));
                    y += rowH + rowGap;
                }
                ContextRow(panel.transform, "Drop", "icon_drop", y, () =>
                {
                    int slot = _ctxSlot;
                    int count = _inventory.GetSlot(slot).count;
                    CloseContextMenu();
                    if (count > 0 && !_inventory.DropItem(slot, count))
                        Debug.Log("[Inventory] Drop unavailable — pending Foundation world-drop op.");
                });
                y += rowH + rowGap;
                ContextRow(panel.transform, "Cancel", null, y, CloseContextMenu);
            }
            else
            {
                int halfCount = s.count / 2;
                ContextRow(panel.transform, $"Half ({halfCount})", "icon_split", y, () =>
                {
                    int slot = _ctxSlot;
                    CloseContextMenu();
                    _inventory.SplitStack(slot, halfCount);
                });
                y += rowH + rowGap;
                ContextRow(panel.transform, "One", "icon_split", y, () =>
                {
                    int slot = _ctxSlot;
                    CloseContextMenu();
                    _inventory.SplitStack(slot, 1);
                });
                y += rowH + rowGap;
                ContextRow(panel.transform, "Cancel", null, y, CloseContextMenu);
            }
        }

        void ContextRow(Transform parent, string label, string iconSkin, float y, System.Action onClick)
        {
            var btn = UiBuilder.NewButton(parent, "Ctx_" + label, "craft_row", "", 13);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(8f, -y);
            rt.sizeDelta = new Vector2(-16f, 36f);
            btn.onClick.AddListener(() => onClick());

            float textX = 10f;
            var iconSpr = iconSkin != null ? UiBuilder.Spr(iconSkin) : null;
            if (iconSpr != null)
            {
                var icon = UiBuilder.NewImage(btn.transform, "Icon", iconSpr, Color.white);
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                var ir = icon.rectTransform;
                ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f);
                ir.pivot = new Vector2(0f, 0.5f);
                ir.anchoredPosition = new Vector2(8f, 0f);
                ir.sizeDelta = new Vector2(22f, 22f);
                textX = 38f;
            }
            var t = UiBuilder.NewText(btn.transform, "Label", label, 13, TextAnchor.MiddleLeft, UiBuilder.TextCol);
            t.raycastTarget = false;
            var tr = t.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(textX, 2f);
            tr.offsetMax = new Vector2(-8f, -2f);
            UiBuilder.FitText(t);
        }

        void CloseContextMenu()
        {
            if (_ctxMenu != null) Destroy(_ctxMenu.gameObject);
            _ctxMenu = null;
            _ctxSlot = -1;
            FoundationUiCoordinator.Active?.SetModalOpen("inventoryOps", _dragging);
        }

        void CancelInventoryOps()
        {
            if (_ctxMenu != null) CloseContextMenu();
            if (_dragging) CancelDrag();
            _pressSlot = -1;
        }

        /// <summary>Escape first dismisses inventory overlays (context menu or an
        /// active drag) before the panel itself closes; GamePanelsController calls
        /// this ahead of Hide(). Returns true if Escape was consumed here.</summary>
        public bool ConsumeEscape()
        {
            if (_ctxMenu != null) { CloseContextMenu(); return true; }
            if (_dragging) { CancelDrag(); return true; }
            return false;
        }

        /// <summary>
        /// Minecraft-style (but roomier) equipment paper-doll: live player sprite
        /// flanked by armor/accessory slots. Slots are visual placeholders until
        /// the Foundation equipment system lands — tools stay on the hotbar by design.
        /// </summary>
        void DrawPaperDoll()
        {
            var frame = UiBuilder.NewPanel(_body, "Doll", "system_row", new Color(0.07f, 0.08f, 0.11f, 0.94f));
            var fr = frame.rectTransform;
            fr.anchorMin = new Vector2(0f, 1f);
            fr.anchorMax = new Vector2(0f, 1f);
            fr.pivot = new Vector2(0f, 1f);
            fr.anchoredPosition = Vector2.zero;
            fr.sizeDelta = new Vector2(380f, 520f);

            // live player sprite (south idle frame of the current sheet)
            Sprite playerSprite = null;
            var sheet = Resources.LoadAll<Sprite>("Characters/Player/BlackMage_Idle_512x1024");
            if (sheet != null)
                for (int i = 0; i < sheet.Length; i++)
                    if (sheet[i].name.EndsWith("_0")) { playerSprite = sheet[i]; break; }
            var pv = UiBuilder.NewImage(frame.transform, "Player", playerSprite, Color.white);
            pv.preserveAspect = true;
            pv.enabled = playerSprite != null;
            var pvr = pv.rectTransform;
            pvr.anchorMin = pvr.anchorMax = new Vector2(0.5f, 1f);
            pvr.pivot = new Vector2(0.5f, 1f);
            pvr.anchoredPosition = new Vector2(0f, -40f);
            pvr.sizeDelta = new Vector2(150f, 260f);

            string[] leftSlots = { "Head", "Chest", "Legs", "Feet" };
            for (int i = 0; i < leftSlots.Length; i++)
                DollSlot(frame.transform, leftSlots[i], 16f, 36f + i * 78f);
            string[] rightSlots = { "Back", "Acc 1", "Acc 2" };
            for (int i = 0; i < rightSlots.Length; i++)
                DollSlot(frame.transform, rightSlots[i], 290f, 36f + i * 78f);

            var hint = UiBuilder.NewText(frame.transform, "Hint",
                "Equipment slots — armor & accessories.\nTools and weapons live on the hotbar.\n(Equip system arriving with the class update.)",
                13, TextAnchor.UpperLeft, UiBuilder.MutedCol);
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            hint.verticalOverflow = VerticalWrapMode.Truncate;
            UiBuilder.FitText(hint);
            var hr = hint.rectTransform;
            hr.anchorMin = new Vector2(0f, 0f);
            hr.anchorMax = new Vector2(1f, 0f);
            hr.pivot = new Vector2(0f, 0f);
            hr.anchoredPosition = new Vector2(16f, 14f);
            hr.sizeDelta = new Vector2(-32f, 110f);
        }

        void DollSlot(Transform parent, string label, float x, float y)
        {
            var s = UiBuilder.NewPanel(parent, "Equip_" + label, "inv_slot", UiBuilder.SlotBg);
            var rt = s.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(72f, 60f);
            var t = UiBuilder.NewText(s.transform, "L", label, 11, TextAnchor.MiddleCenter, UiBuilder.MutedCol);
            UiBuilder.Stretch(t.rectTransform, 2f);
            UiBuilder.FitText(t);
        }

        /// <summary>Ability loadout tab. Visual contract for the confirmed Q/E/R/F +
        /// hold-X wheel scheme; binds to the Foundation ability API when it lands.</summary>
        void DrawSpells()
        {
            TextLine("Abilities", 0, 22, UiBuilder.TextCol);
            TextLine("Tap Q / E / R / F to cast. Hold X for the ability wheel: drag outward to assign, release on an ability to cast it once.",
                34, 14, UiBuilder.MutedCol);

            string[] keys = { "Q", "E", "R", "F" };
            for (int i = 0; i < 4; i++)
            {
                var slot = UiBuilder.NewPanel(_body, "Ability_" + keys[i], "inv_slot", UiBuilder.SlotBg);
                var rt = slot.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(i * 130f, -80f);
                rt.sizeDelta = new Vector2(116f, 96f);
                var k = UiBuilder.NewText(slot.transform, "K", keys[i], 26, TextAnchor.UpperCenter, UiBuilder.TextCol);
                UiBuilder.Stretch(k.rectTransform, 8f);
                var e = UiBuilder.NewText(slot.transform, "E", "(empty)", 12, TextAnchor.LowerCenter, UiBuilder.MutedCol);
                UiBuilder.Stretch(e.rectTransform, 8f);
            }

            TextLine("Known abilities", 200, 18, UiBuilder.TextCol);
            string[] known =
            {
                "Steady Strike — stamina 10 · cooldown 3s · melee",
                "Guard Step — stamina 8 · cooldown 5s · defense",
                "Mana Bolt — mana 12 · cooldown 2s · neutral",
                "Ember Spark — mana 15 · requires Ember affinity rank 1",
            };
            for (int i = 0; i < known.Length; i++)
                TextLine(known[i], 236 + i * 30, 15, i == 0 ? UiBuilder.TextCol : UiBuilder.MutedCol);
            TextLine("Assignment activates with the class/trial update (runtime API pending).",
                236 + known.Length * 30 + 12, 13, new Color(0.98f, 0.84f, 0.52f, 1f));
        }

        /// <summary>Settings tab: volumes (same PlayerPrefs keys as the pause menu),
        /// UI scale and text scale.</summary>
        void DrawSettings()
        {
            TextLine("Settings", 0, 22, UiBuilder.TextCol);
            DrawPrefSlider("Master volume", "vol_master", 1f, 60f, v => AudioListener.volume = Mathf.Clamp01(v));
            DrawPrefSlider("SFX volume", "vol_sfx", 1f, 124f, null);
            DrawPrefSlider("Music volume", "vol_music", 1f, 188f, null);
            DrawPrefSlider("UI scale", "ui.scale", 1f, 252f, v => UiBuilder.ApplyUiScale(v), 0.75f, 1.75f);
            float textScale = LitIsoFont.TextScale;
            TextLine($"Text size: {Mathf.RoundToInt(textScale * 100f)}%", 316, 16, UiBuilder.TextCol);
            DrawStepButtons(344f, () => LitIsoFont.SetTextScale(LitIsoFont.TextScale - 0.1f),
                                   () => LitIsoFont.SetTextScale(LitIsoFont.TextScale + 0.1f));

            // display options (right column)
            const float dx = 560f;
            TextLine("Display", 0, 18, UiBuilder.TextCol, dx);
            DrawToggleButton($"Fullscreen: {(Screen.fullScreen ? "ON" : "OFF")}", dx, 36f,
                () => Screen.fullScreen = !Screen.fullScreen);
            DrawToggleButton($"VSync: {(QualitySettings.vSyncCount > 0 ? "ON" : "OFF")}", dx, 96f,
                () =>
                {
                    QualitySettings.vSyncCount = QualitySettings.vSyncCount > 0 ? 0 : 1;
                    PlayerPrefs.SetInt("display.vsync", QualitySettings.vSyncCount);
                    PlayerPrefs.Save();
                });
            TextLine($"Resolution: {Screen.width} x {Screen.height}", 160, 14, UiBuilder.MutedCol, dx);

            TextLine("More options (bindings, HUD layout, accessibility) arrive with the overhaul. F1 cycles HUD modes; Alt-drag moves HUD panels.",
                414, 13, UiBuilder.MutedCol);
        }

        void DrawPrefSlider(string label, string prefKey, float def, float y,
            System.Action<float> onApply, float min = 0f, float max = 1f)
        {
            float val = PlayerPrefs.GetFloat(prefKey, def);
            TextLine($"{label}: {Mathf.RoundToInt(Mathf.InverseLerp(min, max, val) * 100f)}%", y, 16, UiBuilder.TextCol);
            DrawStepButtons(y + 28f,
                () => { float v = Mathf.Clamp(PlayerPrefs.GetFloat(prefKey, def) - (max - min) * 0.1f, min, max);
                        PlayerPrefs.SetFloat(prefKey, v); PlayerPrefs.Save(); onApply?.Invoke(v); Refresh(); },
                () => { float v = Mathf.Clamp(PlayerPrefs.GetFloat(prefKey, def) + (max - min) * 0.1f, min, max);
                        PlayerPrefs.SetFloat(prefKey, v); PlayerPrefs.Save(); onApply?.Invoke(v); Refresh(); });
        }

        void DrawToggleButton(string label, float x, float y, System.Action onClick)
        {
            var btn = UiBuilder.NewButton(_body, "Toggle_" + label, "button", label, 14);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(230f, 44f);
            btn.onClick.AddListener(() => { onClick(); Refresh(); });
        }

        void DrawStepButtons(float y, System.Action minus, System.Action plus) => DrawStepButtons(0f, y, minus, plus);

        void DrawStepButtons(float x, float y, System.Action minus, System.Action plus)
        {
            var btnDown = UiBuilder.NewButton(_body, "Minus" + x + "_" + y, "button", "-", 18);
            var dr = btnDown.GetComponent<RectTransform>();
            dr.anchorMin = dr.anchorMax = new Vector2(0f, 1f);
            dr.pivot = new Vector2(0f, 1f);
            dr.anchoredPosition = new Vector2(x, -y);
            dr.sizeDelta = new Vector2(48f, 34f);
            btnDown.onClick.AddListener(() => minus());
            var btnUp = UiBuilder.NewButton(_body, "Plus" + x + "_" + y, "button", "+", 18);
            var ur = btnUp.GetComponent<RectTransform>();
            ur.anchorMin = ur.anchorMax = new Vector2(0f, 1f);
            ur.pivot = new Vector2(0f, 1f);
            ur.anchoredPosition = new Vector2(x + 56f, -y);
            ur.sizeDelta = new Vector2(48f, 34f);
            btnUp.onClick.AddListener(() => plus());
        }

        /// <summary>
        /// Admin/debug tab: scrollable "give item" grid (every ItemDefinition in
        /// FoundationContent — click a row to add 1 to the player's inventory for
        /// hotbar/equip testing) plus +/- steppers for Level and core stats
        /// (STR/DEX/INT/VIT/DEF/LUCK), mirroring the Inventory tab's slot styling
        /// and the Settings tab's step-button pattern.
        /// </summary>
        void DrawAdmin()
        {
            if (_admin == null)
            {
                TextLine("Admin tools unavailable", 0, 18, UiBuilder.MutedCol);
                return;
            }

            TextLine("Give Item (click a row to add 1 to your inventory)", 0, 18, UiBuilder.TextCol);

            var listRoot = CreateScrollView(_body, "AdminItemList", out var content);
            listRoot.anchorMin = new Vector2(0f, 0f);
            listRoot.anchorMax = new Vector2(0f, 1f);
            listRoot.pivot = new Vector2(0f, 1f);
            listRoot.offsetMin = Vector2.zero;
            listRoot.offsetMax = new Vector2(0f, -34f);
            listRoot.sizeDelta = new Vector2(500f, 0f);

            int count = _admin.ItemCount;
            for (int i = 0; i < count; i++)
            {
                var entry = _admin.GetItem(i);
                if (string.IsNullOrEmpty(entry.id)) continue;

                var row = UiBuilder.NewPanel(content, "AdminItem_" + entry.id, "system_row", UiBuilder.SlotBg);
                var rowLayout = row.gameObject.AddComponent<LayoutElement>();
                rowLayout.preferredHeight = 50f;
                rowLayout.minHeight = 50f;

                var icon = UiBuilder.NewImage(row.transform, "Icon", entry.icon, entry.icon != null ? Color.white : entry.color);
                icon.preserveAspect = true;
                var iconRt = icon.rectTransform;
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0f, 0.5f);
                iconRt.anchoredPosition = new Vector2(8f, 0f);
                iconRt.sizeDelta = new Vector2(34f, 34f);

                var label = UiBuilder.NewText(row.transform, "Label", entry.label, 15, TextAnchor.MiddleLeft, UiBuilder.TextCol);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                UiBuilder.FitText(label);
                var labelRt = label.rectTransform;
                labelRt.anchorMin = new Vector2(0f, 0f);
                labelRt.anchorMax = new Vector2(1f, 1f);
                labelRt.offsetMin = new Vector2(50f, 4f);
                labelRt.offsetMax = new Vector2(-4f, -4f);

                var btn = row.gameObject.AddComponent<Button>();
                string itemId = entry.id;
                btn.onClick.AddListener(() => _admin.GiveItem(itemId, 1));
            }

            // ---- Right column: live player stats/level controls ----
            const float dx = 560f;
            TextLine("Player Stats (debug)", 0, 18, UiBuilder.TextCol, dx);

            TextLine($"Level: {_admin.Level}", 40f, 16, UiBuilder.TextCol, dx);
            DrawStepButtons(dx, 68f,
                () => { _admin.AdjustLevel(-1); Refresh(); },
                () => { _admin.AdjustLevel(1); Refresh(); });

            var stats = _admin.Stats;
            string[] labels = { "STR", "DEX", "INT", "VIT", "DEF", "LUCK" };
            int[] vals = { stats.str, stats.dex, stats.intel, stats.vit, stats.def, stats.luck };
            var statTypes = new[]
            {
                FoundationStatType.STR, FoundationStatType.DEX, FoundationStatType.INT,
                FoundationStatType.VIT, FoundationStatType.DEF, FoundationStatType.LUCK,
            };
            for (int i = 0; i < labels.Length; i++)
            {
                float y = 132f + i * 60f;
                TextLine($"{labels[i]}: {vals[i]}", y, 16, UiBuilder.TextCol, dx);
                var stat = statTypes[i];
                DrawStepButtons(dx, y + 28f,
                    () => { _admin.AdjustStat(stat, -1); Refresh(); },
                    () => { _admin.AdjustStat(stat, 1); Refresh(); });
            }
        }

        void DrawCrafting()
        {
            // Station filtering hint: PlayerInteraction.RequestCrafting sets
            // CraftingStationContext when the player opens this tab from a world
            // station (workbench / furnace / tannery / campfire-cookpot). It is
            // already populated and safe to read here.
            //
            // TODO(station-filter): when CraftingStationContext.HasActiveStation is
            // true, restrict the recipe list below to recipes whose station matches
            // CraftingStationContext.ActiveStation (Hand/None recipes stay always
            // visible), and change the list title from "all stations" to the active
            // station name. The recipe rows expose `row.station` (a string) already,
            // so the filter is row-local and does NOT require changing the view model.
            //
            // TODO(tiers): there is currently NO tier concept to gate on.
            //   - PlaceableDefinition has no `stationTier`.
            //   - RecipeDefinition has no `minStationTier` / output-tier field.
            //   - StationType (None/Hand/Workbench/Furnace/CookingPot/Tannery) has no
            //     Anvil or EnchantingTable members.
            // Owner intent: higher-tier station -> exposes higher-tier recipes AND
            // better outputs. To support it cleanly, add `stationTier` to
            // PlaceableDefinition + `minStationTier` to RecipeDefinition, plumb the
            // placed station's tier into CraftingStationContext.Set(...) from
            // PlayerInteraction.RequestCrafting, then here hide recipes where
            // row.minStationTier > CraftingStationContext.ActiveStationTier. Do NOT
            // fake this with id string-matching.
            //
            // 2026-06 station/board wiring: world-station props now Set
            // CraftingStationContext before opening this tab. Apply that station as the
            // adapter filter so the recipe list narrows to the station (and the adapter's
            // BuildVisible also honours minStationTier vs ActiveStationTier). When no
            // station drove the open (hotkey / Stations Hub Clear()), show everything.
            // RecipeDefinition.minStationTier now exists, so the tier gate lives in
            // FoundationCraftingAdapter.BuildVisible (the proper, non-string-matching point).
            _crafting?.SetStationFilter(
                CraftingStationContext.HasActiveStation
                    ? CraftingStationContext.ActiveStation
                    : (StationType?)null);
            int count = Mathf.Max(0, _crafting?.RecipeCount ?? 0);
            if (count == 0)
            {
                TextLine("No crafting recipes available", 0, 18, UiBuilder.MutedCol);
                return;
            }

            if (!RecipeExists(_selectedRecipeId))
                _selectedRecipeId = _crafting.GetRecipe(0).id;

            var listFrame = UiBuilder.NewPanel(_body, "RecipeListFrame", "system_row", UiBuilder.SlotBg);
            var listFrameRt = listFrame.rectTransform;
            listFrameRt.anchorMin = new Vector2(0f, 0f);
            listFrameRt.anchorMax = new Vector2(0f, 1f);
            listFrameRt.offsetMin = Vector2.zero;
            listFrameRt.offsetMax = new Vector2(380f, 0f);

            var listTitle = UiBuilder.NewText(listFrame.transform, "ListTitle", $"Recipes ({count}) - all stations", 17, TextAnchor.MiddleLeft, LitIsoTheme.Gold);
            var listTitleRt = listTitle.rectTransform;
            listTitleRt.anchorMin = new Vector2(0f, 1f);
            listTitleRt.anchorMax = new Vector2(1f, 1f);
            listTitleRt.pivot = new Vector2(0f, 1f);
            listTitleRt.anchoredPosition = new Vector2(16f, -10f);
            listTitleRt.sizeDelta = new Vector2(-32f, 28f);
            UiBuilder.FitText(listTitle);

            var listScrollRoot = CreateScrollView(listFrame.transform, "RecipeScroll", out var listContent);
            listScrollRoot.offsetMin = new Vector2(10f, 10f);
            listScrollRoot.offsetMax = new Vector2(-10f, -48f);

            for (int i = 0; i < count; i++)
            {
                var row = _crafting.GetRecipe(i);
                var recipeRow = UiBuilder.NewPanel(listContent, "Recipe_" + SafeName(row.id), "craft_row",
                    row.id == _selectedRecipeId ? LitIsoTheme.GoldDeep : UiBuilder.SlotBg);
                recipeRow.color = row.id == _selectedRecipeId
                    ? LitIsoTheme.GoldDeep
                    : row.canCraft ? UiBuilder.SlotBg : new Color(0.08f, 0.09f, 0.12f, 0.84f);
                var rowRt = recipeRow.rectTransform;
                rowRt.sizeDelta = new Vector2(0f, 68f);
                var rowLayout = recipeRow.gameObject.AddComponent<LayoutElement>();
                rowLayout.preferredHeight = 68f;
                rowLayout.minHeight = 68f;

                var btn = recipeRow.gameObject.AddComponent<Button>();
                btn.targetGraphic = recipeRow;
                var id = row.id;
                btn.onClick.AddListener(() => { _selectedRecipeId = id; Refresh(); });

                var icon = UiBuilder.NewImage(recipeRow.transform, "Icon", row.icon, row.canCraft ? UiBuilder.TextCol : UiBuilder.MutedCol);
                icon.preserveAspect = true;
                icon.enabled = row.icon != null;
                var iconRt = icon.rectTransform;
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0f, 0.5f);
                iconRt.anchoredPosition = new Vector2(10f, 0f);
                iconRt.sizeDelta = new Vector2(44f, 44f);

                string station = string.IsNullOrWhiteSpace(row.station) ? "Hand" : row.station;
                var label = UiBuilder.NewText(recipeRow.transform, "Label", $"{row.display}\n{station}", 15, TextAnchor.MiddleLeft,
                    row.canCraft ? UiBuilder.TextCol : UiBuilder.MutedCol);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                UiBuilder.FitText(label);
                var labelRt = label.rectTransform;
                labelRt.anchorMin = new Vector2(0f, 0f);
                labelRt.anchorMax = new Vector2(1f, 1f);
                labelRt.offsetMin = new Vector2(64f, 16f);
                labelRt.offsetMax = new Vector2(-142f, -8f);

                string rowStatus = row.canCraft
                    ? "Ready"
                    : string.IsNullOrWhiteSpace(row.disabledReason) ? "Locked" : row.disabledReason;
                var status = UiBuilder.NewText(recipeRow.transform, "Status", rowStatus, 12,
                    TextAnchor.MiddleRight, row.canCraft ? new Color(0.55f, 0.95f, 0.62f, 1f) : new Color(0.95f, 0.52f, 0.45f, 1f));
                status.horizontalOverflow = HorizontalWrapMode.Wrap;
                status.verticalOverflow = VerticalWrapMode.Truncate;
                UiBuilder.FitText(status);
                var statusRt = status.rectTransform;
                statusRt.anchorMin = new Vector2(1f, 0f);
                statusRt.anchorMax = new Vector2(1f, 1f);
                statusRt.pivot = new Vector2(1f, 0.5f);
                statusRt.anchoredPosition = new Vector2(-10f, 0f);
                statusRt.sizeDelta = new Vector2(126f, -10f);
            }

            var details = _crafting.GetDetails(_selectedRecipeId);
            var selectedRow = SelectedRecipeRow();
            var detailsFrame = UiBuilder.NewPanel(_body, "RecipeDetailsFrame", "system_panel", new Color(0.08f, 0.10f, 0.14f, 0.92f));
            var detailsRt = detailsFrame.rectTransform;
            detailsRt.anchorMin = Vector2.zero;
            detailsRt.anchorMax = Vector2.one;
            detailsRt.offsetMin = new Vector2(404f, 0f);
            detailsRt.offsetMax = Vector2.zero;

            var title = UiBuilder.NewText(detailsFrame.transform, "RecipeTitle", details.display ?? "Recipe", 22, TextAnchor.UpperLeft, LitIsoTheme.Gold);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            UiBuilder.FitText(title);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.anchoredPosition = new Vector2(18f, -14f);
            titleRt.sizeDelta = new Vector2(-36f, 56f);

            string detailStation = string.IsNullOrWhiteSpace(selectedRow.station) ? "Hand" : selectedRow.station;
            string reason = details.canCraft
                ? "Ready to craft"
                : string.IsNullOrWhiteSpace(details.disabledReason) ? "Cannot craft" : details.disabledReason;
            var statusText = UiBuilder.NewText(detailsFrame.transform, "RecipeStatus", $"Station: {detailStation}\n{reason}", 16, TextAnchor.UpperLeft,
                details.canCraft ? new Color(0.55f, 0.95f, 0.62f, 1f) : new Color(0.95f, 0.52f, 0.45f, 1f));
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Truncate;
            UiBuilder.FitText(statusText);
            var statusTextRt = statusText.rectTransform;
            statusTextRt.anchorMin = new Vector2(0f, 1f);
            statusTextRt.anchorMax = new Vector2(1f, 1f);
            statusTextRt.pivot = new Vector2(0f, 1f);
            statusTextRt.anchoredPosition = new Vector2(18f, -72f);
            statusTextRt.sizeDelta = new Vector2(-36f, 44f);

            var detailsScrollRoot = CreateScrollView(detailsFrame.transform, "DetailsScroll", out var detailsContent);
            detailsScrollRoot.offsetMin = new Vector2(12f, 84f);
            detailsScrollRoot.offsetMax = new Vector2(-12f, -124f);

            AddIngredientSection(detailsContent, "Ingredients", details.inputs, true);
            AddIngredientSection(detailsContent, "Creates", details.outputs, false);

            var craft = UiBuilder.NewButton(detailsFrame.transform, "Craft", "craft_button", details.canCraft ? "Craft" : "Cannot Craft", 20);
            craft.interactable = details.canCraft;
            craft.onClick.AddListener(() => { _crafting?.Craft(_selectedRecipeId); Refresh(); });
            var cr = craft.GetComponent<RectTransform>();
            cr.anchorMin = cr.anchorMax = new Vector2(1f, 0f);
            cr.pivot = new Vector2(1f, 0f);
            cr.anchoredPosition = new Vector2(-18f, 18f);
            cr.sizeDelta = new Vector2(240f, 58f);
        }

        bool RecipeExists(string recipeId)
        {
            if (string.IsNullOrWhiteSpace(recipeId) || _crafting == null)
                return false;

            for (int i = 0; i < _crafting.RecipeCount; i++)
            {
                if (_crafting.GetRecipe(i).id == recipeId)
                    return true;
            }

            return false;
        }

        CraftingRecipeRow SelectedRecipeRow()
        {
            if (string.IsNullOrWhiteSpace(_selectedRecipeId) || _crafting == null)
                return default;

            for (int i = 0; i < _crafting.RecipeCount; i++)
            {
                var row = _crafting.GetRecipe(i);
                if (row.id == _selectedRecipeId)
                    return row;
            }

            return default;
        }

        RectTransform CreateScrollView(Transform parent, string name, out RectTransform content)
        {
            var root = UiBuilder.NewRect(name, parent);
            UiBuilder.Stretch(root);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 34f;

            var viewport = UiBuilder.NewRect("Viewport", root);
            UiBuilder.Stretch(viewport);
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            // RectMask2D instead of stencil Mask: a Mask over a (near-)transparent Image
            // can cull every masked child depending on canvas/material setup, which made
            // scroll-list rows render invisible (recipes/callings). RectMask2D clips by
            // rect with no stencil/graphic dependency.
            viewport.gameObject.AddComponent<RectMask2D>();

            content = UiBuilder.NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            return root;
        }

        void AddIngredientSection(RectTransform parent, string title, CraftingIngredient[] rows, bool compareHave)
        {
            var header = UiBuilder.NewText(parent, title + "Header", title, 18, TextAnchor.MiddleLeft, UiBuilder.MutedCol);
            var headerLayout = header.gameObject.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 30f;
            headerLayout.minHeight = 30f;

            if (rows == null || rows.Length == 0)
            {
                var empty = UiBuilder.NewText(parent, title + "Empty", "None", 15, TextAnchor.MiddleLeft, UiBuilder.MutedCol);
                var emptyLayout = empty.gameObject.AddComponent<LayoutElement>();
                emptyLayout.preferredHeight = 28f;
                emptyLayout.minHeight = 28f;
                return;
            }

            for (int i = 0; i < rows.Length; i++)
            {
                var input = rows[i];
                bool haveEnough = !compareHave || input.have >= input.needed;
                var row = UiBuilder.NewPanel(parent, title + "_" + SafeName(input.itemId) + "_" + i, "system_row",
                    haveEnough ? UiBuilder.SlotBg : new Color(0.18f, 0.08f, 0.08f, 0.82f));
                var layout = row.gameObject.AddComponent<LayoutElement>();
                layout.preferredHeight = 54f;
                layout.minHeight = 54f;

                var icon = UiBuilder.NewImage(row.transform, "Icon", input.icon, UiBuilder.MutedCol);
                icon.preserveAspect = true;
                icon.enabled = input.icon != null;
                var iconRt = icon.rectTransform;
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0f, 0.5f);
                iconRt.anchoredPosition = new Vector2(10f, 0f);
                iconRt.sizeDelta = new Vector2(34f, 34f);

                var label = UiBuilder.NewText(row.transform, "Label", input.display, 15, TextAnchor.MiddleLeft,
                    haveEnough ? UiBuilder.TextCol : new Color(0.95f, 0.52f, 0.45f, 1f));
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                UiBuilder.FitText(label);
                var labelRt = label.rectTransform;
                labelRt.anchorMin = new Vector2(0f, 0f);
                labelRt.anchorMax = new Vector2(1f, 1f);
                labelRt.offsetMin = new Vector2(54f, 8f);
                labelRt.offsetMax = new Vector2(-120f, -8f);

                string count = compareHave ? $"{input.have}/{input.needed}" : $"x{input.needed}";
                var amount = UiBuilder.NewText(row.transform, "Amount", count, 16, TextAnchor.MiddleRight,
                    haveEnough ? UiBuilder.TextCol : new Color(0.95f, 0.52f, 0.45f, 1f));
                var amountRt = amount.rectTransform;
                amountRt.anchorMin = new Vector2(1f, 0f);
                amountRt.anchorMax = new Vector2(1f, 1f);
                amountRt.pivot = new Vector2(1f, 0.5f);
                amountRt.anchoredPosition = new Vector2(-12f, 0f);
                amountRt.sizeDelta = new Vector2(104f, 0f);
                UiBuilder.FitText(amount);
            }
        }

        static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "none";

            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-')
                    chars[i] = '_';
            }

            return new string(chars);
        }

        void DrawStatus()
        {
            if (_character == null)
            {
                TextLine("Status unavailable", 0, 18, UiBuilder.MutedCol);
                return;
            }

            TextLine(_character.CharacterName, 0, 24, UiBuilder.TextCol);
            TextLine($"Class: {_character.ClassName}", 36, 18, UiBuilder.MutedCol);
            TextLine($"Title: {_character.TitleName}", 64, 18, UiBuilder.MutedCol);
            TextLine($"Level {_character.Level}", 92, 18, UiBuilder.TextCol);
            DrawBar("HP", _character.Health01, 136, new Color(0.80f, 0.25f, 0.25f, 1f));
            DrawBar("MP", _character.Mana01, 174, new Color(0.30f, 0.55f, 0.90f, 1f));
            DrawBar("XP", _character.Xp01, 212, new Color(0.85f, 0.70f, 0.30f, 1f));

            var s = _character.Stats;
            string[] labels = { "STR", "DEX", "INT", "VIT", "DEF", "LUCK" };
            int[] vals = { s.str, s.dex, s.intel, s.vit, s.def, s.luck };
            for (int i = 0; i < labels.Length; i++)
            {
                float x = 520f + (i % 2) * 170f;
                float y = 20f + (i / 2) * 62f;
                TextLine(labels[i], y, 16, UiBuilder.MutedCol, x);
                TextLine(vals[i].ToString(), y + 22f, 24, UiBuilder.TextCol, x);
            }
        }

        void DrawSkills()
        {
            var state = _progression?.CaptureReadState();
            if (state?.skills == null || state.skills.Length == 0)
            {
                TextLine("No skills tracked yet", 0, 18, UiBuilder.MutedCol);
                return;
            }

            var scrollRoot = CreateScrollView(_body, "SkillsScroll", out var content);
            scrollRoot.offsetMin = Vector2.zero;
            scrollRoot.offsetMax = Vector2.zero;

            AddSkillsIntro(content, state);

            foreach (var bucket in SkillBuckets)
            {
                var matches = CollectSkills(state.skills, bucket);
                if (matches.Count == 0) continue;

                AddSkillBucketHeader(content, bucket, matches);
                for (int i = 0; i < matches.Count; i++)
                    AddSkillCard(content, matches[i], bucket);
            }
        }

        void AddSkillsIntro(RectTransform parent, FoundationProgressionReadState state)
        {
            var frame = UiBuilder.NewPanel(parent, "SkillsIntro", "system_panel", new Color(0.075f, 0.085f, 0.12f, 0.94f));
            AddLayoutHeight(frame.rectTransform, 112f);

            string calling = state.calling.hasCalling ? state.calling.displayName : "Unwritten";
            string title = state.calling.hasCalling ? state.calling.title : "No title yet";
            AddText(frame.transform, "Title", "LitRPG Skill Paths", 24, UiBuilder.TextCol, 18f, 12f, 420f, 30f);
            AddText(frame.transform, "Sub", "Combat, gathering, crafting, survival, settlement, lore, and future dungeon mastery all live here.", 15,
                UiBuilder.MutedCol, 18f, 48f, 660f, 44f);
            AddText(frame.transform, "Calling", $"Calling: {calling}\nTitle: {title}", 16, UiBuilder.TextCol, 720f, 18f, 260f, 58f, TextAnchor.UpperRight);
            AddText(frame.transform, "Hint", "Skills gain XP from real actions; unlock previews show what each path will feed later.", 13,
                new Color(0.98f, 0.84f, 0.52f, 1f), 18f, 86f, 790f, 24f);
        }

        void AddSkillBucketHeader(RectTransform parent, SkillUiBucket bucket, List<FoundationSkillReadState> skills)
        {
            float average = 0f;
            int highLevel = 0;
            for (int i = 0; i < skills.Count; i++)
            {
                average += Mathf.Clamp01(skills[i].progress01);
                highLevel = Mathf.Max(highLevel, skills[i].level);
            }
            average = skills.Count > 0 ? average / skills.Count : 0f;

            var header = UiBuilder.NewPanel(parent, "SkillBucket_" + SafeName(bucket.title), "system_row", new Color(0.08f, 0.095f, 0.13f, 0.94f));
            AddLayoutHeight(header.rectTransform, 82f);

            var strip = UiBuilder.NewImage(header.transform, "Strip", null, bucket.color);
            Place(strip.rectTransform, 0f, 0f, 8f, 82f);
            AddText(header.transform, "Title", bucket.title, 20, UiBuilder.TextCol, 18f, 10f, 360f, 28f);
            AddText(header.transform, "Sub", bucket.subtitle, 14, UiBuilder.MutedCol, 18f, 42f, 600f, 32f);
            AddText(header.transform, "Count", $"{skills.Count} skills\nHighest Lv {highLevel}", 14, UiBuilder.TextCol, 760f, 12f, 190f, 46f, TextAnchor.UpperRight);
            AddLocalBar(header.transform, 635f, 58f, 310f, 10f, average, bucket.color);
        }

        void AddSkillCard(RectTransform parent, FoundationSkillReadState skill, SkillUiBucket bucket)
        {
            var card = UiBuilder.NewPanel(parent, "Skill_" + SafeName(skill.id), "system_panel", new Color(0.055f, 0.065f, 0.09f, 0.94f));
            AddLayoutHeight(card.rectTransform, 138f);

            var sigil = UiBuilder.NewPanel(card.transform, "Sigil", "slot_selected", new Color(bucket.color.r * 0.45f, bucket.color.g * 0.45f, bucket.color.b * 0.45f, 0.96f));
            Place(sigil.rectTransform, 16f, 18f, 58f, 58f);
            AddText(sigil.transform, "Letter", SkillInitial(skill), 26, UiBuilder.TextCol, 0f, 10f, 58f, 38f, TextAnchor.MiddleCenter);

            string tracked = skill.isTracked ? "Tracked" : "Dormant";
            AddText(card.transform, "Name", $"{skill.displayName}  Lv {skill.level}", 21, UiBuilder.TextCol, 92f, 12f, 420f, 30f);
            AddText(card.transform, "Meta", $"{ReadableActivity(skill.activity)} / {ReadableNodeKind(skill.primaryNodeKind)} / {tracked}", 13,
                new Color(0.98f, 0.84f, 0.52f, 1f), 92f, 42f, 480f, 22f);
            AddText(card.transform, "Desc", skill.description, 14, UiBuilder.MutedCol, 92f, 66f, 590f, 38f);
            AddLocalBar(card.transform, 92f, 112f, 440f, 12f, skill.progress01, bucket.color);
            AddText(card.transform, "Xp", $"{skill.xpIntoLevel}/{skill.xpToNextLevel} XP to next level", 13, UiBuilder.MutedCol,
                548f, 104f, 180f, 28f, TextAnchor.MiddleLeft);

            AddText(card.transform, "UnlockTitle", "Unlock path", 13, UiBuilder.TextCol, 742f, 14f, 160f, 20f, TextAnchor.UpperRight);
            AddText(card.transform, "Unlocks", FormatUnlocks(skill.unlocks), 13, UiBuilder.MutedCol, 640f, 40f, 310f, 76f, TextAnchor.UpperRight);
        }

        static List<FoundationSkillReadState> CollectSkills(FoundationSkillReadState[] skills, SkillUiBucket bucket)
        {
            var result = new List<FoundationSkillReadState>();
            if (skills == null) return result;

            for (int i = 0; i < skills.Length; i++)
            {
                if (MatchesBucket(skills[i], bucket))
                    result.Add(skills[i]);
            }

            return result;
        }

        static bool MatchesBucket(FoundationSkillReadState skill, SkillUiBucket bucket)
        {
            if (bucket.activities == null) return false;
            for (int i = 0; i < bucket.activities.Length; i++)
                if (skill.activity == bucket.activities[i])
                    return true;
            return false;
        }

        static string SkillInitial(FoundationSkillReadState skill)
        {
            string source = string.IsNullOrWhiteSpace(skill.displayName) ? skill.id : skill.displayName;
            return string.IsNullOrEmpty(source) ? "?" : source.Substring(0, 1).ToUpperInvariant();
        }

        static string FormatUnlocks(string[] unlocks)
        {
            if (unlocks == null || unlocks.Length == 0)
                return "Future perks, passives, titles, and class evidence.";

            string value = "";
            for (int i = 0; i < unlocks.Length && i < 3; i++)
            {
                if (string.IsNullOrWhiteSpace(unlocks[i])) continue;
                if (value.Length > 0) value += "\n";
                value += "* " + unlocks[i];
            }
            return string.IsNullOrWhiteSpace(value) ? "Future perks, passives, titles, and class evidence." : value;
        }

        static string ReadableActivity(FoundationProgressionActivity activity)
        {
            switch (activity)
            {
                case FoundationProgressionActivity.Harvest: return "Gathering";
                case FoundationProgressionActivity.Craft: return "Crafting";
                case FoundationProgressionActivity.Build: return "Building";
                case FoundationProgressionActivity.Farm: return "Farming";
                case FoundationProgressionActivity.Explore: return "Exploration";
                case FoundationProgressionActivity.Creature: return "Creaturecraft";
                case FoundationProgressionActivity.Combat: return "Combat";
                case FoundationProgressionActivity.Magic: return "Magic";
                case FoundationProgressionActivity.Trade: return "Trade";
                case FoundationProgressionActivity.Lore: return "Lore";
                default: return activity.ToString();
            }
        }

        static string ReadableNodeKind(FoundationSkillNodeKind kind)
        {
            switch (kind)
            {
                case FoundationSkillNodeKind.Ease: return "Ease";
                case FoundationSkillNodeKind.Yield: return "Yield";
                case FoundationSkillNodeKind.Insight: return "Insight";
                case FoundationSkillNodeKind.Expression: return "Expression";
                case FoundationSkillNodeKind.Utility: return "Utility";
                case FoundationSkillNodeKind.Harmony: return "Harmony";
                default: return kind.ToString();
            }
        }

        /// <summary>Journal = quests (left) + live trial status and the System
        /// log (right), per the approved 9-tab design. All real data.</summary>
        void DrawJournal()
        {
            DrawQuests();   // left column (self-limits to ~520px wide)

            const float x = 560f;
            if (_progression != null)
            {
                string trialTitle = _progression.TrialCompleted
                    ? "Trial complete"
                    : $"Trial — Day {_progression.TrialDay} of {_progression.TrialDurationDays}";
                TextLine(trialTitle, 0, 20, new Color(0.55f, 0.85f, 1f, 1f), x);
                TextLine($"Grade forecast: {_progression.GradeForecast}    ·    total score {_progression.TotalTrialScore}",
                    32, 15, UiBuilder.TextCol, x);

                float ty = 64f;
                foreach (var kv in _progression.TrialScores)
                {
                    TextLine($"{kv.Key}: {kv.Value}", ty, 13, UiBuilder.MutedCol, x);
                    ty += 22f;
                    if (ty > 220f) break;
                }
            }

            var read = _qol?.CaptureReadState();
            TextLine("System log", 250, 18, UiBuilder.TextCol, x);
            float ly = 284f;
            if (read?.visibleMessages != null && read.visibleMessages.Length > 0)
            {
                for (int i = read.visibleMessages.Length - 1; i >= 0 && ly < 560f; i--)
                {
                    var msg = read.visibleMessages[i];
                    TextLine($"[{msg.channel}] {msg.text}", ly, 13, UiBuilder.MutedCol, x);
                    ly += 24f;
                }
            }
            else
            {
                TextLine("No System messages yet", ly, 14, UiBuilder.MutedCol, x);
            }
        }

        void DrawQuests()
        {
            var state = _progression?.CaptureReadState();
            if (state?.quests == null || state.quests.Length == 0)
            {
                TextLine("No active quests", 0, 18, UiBuilder.MutedCol);
                return;
            }

            float y = 0f;
            foreach (var quest in state.quests)
            {
                TextLine($"{quest.displayName}  {(quest.completed ? "Complete" : "Active")}", y, 18, UiBuilder.TextCol);
                DrawBar("", quest.progress01, y + 28f, new Color(0.95f, 0.78f, 0.34f, 1f), 360f, 14f);
                y += 54f;
                if (quest.objectives != null)
                {
                    foreach (var objective in quest.objectives)
                    {
                        string mark = objective.completed ? "[x]" : "[ ]";
                        TextLine($"{mark} {objective.text}  {objective.current}/{objective.required}", y, 14, UiBuilder.MutedCol, 20f);
                        y += 24f;
                        if (y > 470f) return;
                    }
                }
                y += 14f;
                if (y > 470f) break;
            }
        }

        void DrawSystem()
        {
            // save / quit block (mirrors PauseMenu behaviour + same save path)
            DrawSystemButton("Save Game", 0f, () =>
            {
                var bootstrap = UnityEngine.Object.FindFirstObjectByType<IsoCore.Foundation.FoundationBootstrap>();
                if (bootstrap != null && bootstrap.Save(bootstrap.DefaultSavePath))
                    Debug.Log("[SystemTab] Game saved.");
                else
                    Debug.LogWarning("[SystemTab] Save failed.");
                Refresh();
            });
            DrawSystemButton("Save & Main Menu", 230f, () =>
            {
                var bootstrap = UnityEngine.Object.FindFirstObjectByType<IsoCore.Foundation.FoundationBootstrap>();
                if (bootstrap != null) bootstrap.Save(bootstrap.DefaultSavePath);
                LoadingScreen.Go("MenuScene", "Returning to the fire…");
            });
            DrawSystemButton("Quit to Desktop", 460f, () =>
            {
                var bootstrap = UnityEngine.Object.FindFirstObjectByType<IsoCore.Foundation.FoundationBootstrap>();
                if (bootstrap != null) bootstrap.Save(bootstrap.DefaultSavePath);
                Application.Quit();
            });

            var read = _qol?.CaptureReadState();
            TextLine("System Log", 76, 22, UiBuilder.TextCol);
            TextLine("Filtered, persistent Ledger messages from trial evidence, quests, titles, affinities, and warnings.", 110, 15, UiBuilder.MutedCol);
            float y = 152f;
            if (read?.visibleMessages != null && read.visibleMessages.Length > 0)
            {
                for (int i = read.visibleMessages.Length - 1; i >= 0 && y < 480f; i--)
                {
                    var msg = read.visibleMessages[i];
                    TextLine($"[{msg.channel}] {msg.text}", y, 15, UiBuilder.TextCol);
                    y += 28f;
                }
            }
            else
            {
                TextLine("No System messages yet", y, 16, UiBuilder.MutedCol);
            }

            if (read?.pinnedGoals != null && read.pinnedGoals.Length > 0)
            {
                TextLine("Pinned Goals", 0, 18, UiBuilder.TextCol, 640f);
                float gy = 34f;
                foreach (var goal in read.pinnedGoals)
                {
                    TextLine(goal.available ? goal.displayName : goal.detail, gy, 15, goal.available ? UiBuilder.TextCol : UiBuilder.MutedCol, 640f);
                    DrawBar("", goal.progress01, gy + 24f, new Color(0.95f, 0.78f, 0.34f, 1f), 260f, 12f, 640f);
                    gy += 60f;
                    if (gy > 420f) break;
                }
            }
        }

        void DrawSystemButton(string label, float x, System.Action onClick)
        {
            var btn = UiBuilder.NewButton(_body, "Sys_" + label, "button", label, 16);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(214f, 52f);
            btn.onClick.AddListener(() => onClick());
        }

        void DrawMap()
        {
            TextLine("Map", 0, 24, UiBuilder.TextCol);
            TextLine("Press M for the explored map. Drag the fullscreen map to pan and use mouse wheel to zoom.", 40, 17, UiBuilder.MutedCol);
            TextLine("Hold Alt and drag/resize HUD panels to author your layout. Alt+Shift+R resets HUD/map layout.", 78, 17, UiBuilder.MutedCol);
            TextLine("Map markers now show player, spawn/home, portals, resources, and buildings discovered in explored cells.", 116, 17, UiBuilder.MutedCol);
        }

        static void AddLayoutHeight(RectTransform rt, float height)
        {
            var layout = rt.gameObject.GetComponent<LayoutElement>();
            if (layout == null) layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
        }

        static Text AddText(Transform parent, string name, string value, int size, Color color,
            float x, float y, float width, float height, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var text = UiBuilder.NewText(parent, name, value ?? "", size, anchor, color);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            UiBuilder.FitText(text);
            Place(text.rectTransform, x, y, width, height);
            return text;
        }

        static void AddLocalBar(Transform parent, float x, float y, float width, float height, float value, Color fillColor)
        {
            var track = UiBuilder.NewPanel(parent, "ProgressTrack", "bar_track", UiBuilder.SlotBg);
            Place(track.rectTransform, x, y, width, height);

            var fill = UiBuilder.NewImage(track.transform, "ProgressFill", null, fillColor);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = Mathf.Clamp01(value);
            UiBuilder.Stretch(fill.rectTransform, 2f);
        }

        static void Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(width, height);
        }

        Text TextLine(string value, float y, int size, Color color, float x = 0f)
        {
            var t = UiBuilder.NewText(_body, "Line", value ?? "", size, TextAnchor.UpperLeft, color);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(-x, size + 10f);
            UiBuilder.FitText(t);
            return t;
        }

        void DrawBar(string label, float value, float y, Color color, float width = 380f, float height = 20f, float x = 0f)
        {
            if (!string.IsNullOrEmpty(label))
                TextLine(label, y - 2f, 14, UiBuilder.MutedCol, x);
            var track = UiBuilder.NewPanel(_body, "Bar", "bar_track", UiBuilder.SlotBg);
            var tr = track.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0f, 1f);
            tr.pivot = new Vector2(0f, 1f);
            tr.anchoredPosition = new Vector2(x + (string.IsNullOrEmpty(label) ? 0f : 46f), -y);
            tr.sizeDelta = new Vector2(width, height);

            var fill = UiBuilder.NewImage(track.transform, "Fill", null, color);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = Mathf.Clamp01(value);
            UiBuilder.Stretch(fill.rectTransform, 2f);
        }

        static string LabelFor(CharacterPanelTab tab)
        {
            switch (tab)
            {
                case CharacterPanelTab.Inventory: return "Inventory";
                case CharacterPanelTab.Crafting: return "Crafting";
                case CharacterPanelTab.Skills: return "Skills";
                case CharacterPanelTab.Spells: return "Spells";
                case CharacterPanelTab.Character: return "Character";
                case CharacterPanelTab.Journal: return "Journal";
                case CharacterPanelTab.Settings: return "Settings";
                case CharacterPanelTab.System: return "System";
                case CharacterPanelTab.Map: return "Map";
                case CharacterPanelTab.Admin: return "Admin";
                default: return tab.ToString();
            }
        }

        void HandleTextScaleChanged(float _)
        {
            bool wasOpen = IsOpen;
            var activeTab = _activeTab;
            Unsubscribe();
            Build();
            Subscribe();
            if (wasOpen)
                Show(activeTab);
            else if (_root != null)
                _root.SetActive(false);
        }
    }
}
