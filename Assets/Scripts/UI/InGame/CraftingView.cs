// PHASE_COMPLETE Phase4
using System;
using System.Collections;
using System.Collections.Generic;
using IsoCore.Foundation;
using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    public struct CraftingIngredient { public string itemId; public string display; public Sprite icon; public int needed; public int have; }
    public struct CraftingRecipeRow
    {
        public string id;
        public string display;
        public Sprite icon;
        public bool canCraft;
        public string station;
        public string disabledReason;
    }
    public struct CraftingRecipeDetails
    {
        public string id; public string display; public Sprite icon;
        public CraftingIngredient[] inputs;
        public CraftingIngredient[] outputs;
        public bool canCraft;
        public string disabledReason;
        /// <summary>How many times this recipe can be crafted with the current inventory (0 if none).</summary>
        public int maxCraftable;
        /// <summary>Seconds for a timed craft (0 = instant).</summary>
        public float craftTimeSeconds;
        /// <summary>Display text for an optional fuel requirement, e.g. "Fuel: Wood 1/1". Empty if none.</summary>
        public string fuelInfo;
        /// <summary>True if this recipe's timed craft is currently in progress.</summary>
        public bool jobActive;
        /// <summary>0..1 progress of the active job, if jobActive.</summary>
        public float jobProgress01;
    }

    /// <summary>Model the crafting panel renders from. Foundation-free; adapter binds later.</summary>
    public interface ICraftingViewModel
    {
        int RecipeCount { get; }
        CraftingRecipeRow GetRecipe(int i);
        CraftingRecipeDetails GetDetails(string recipeId);
        void Craft(string recipeId);
        /// <summary>Craft up to <paramref name="count"/> times, stopping early if ingredients/space run out.</summary>
        void Craft(string recipeId, int count);
        /// <summary>
        /// Narrow the recipe list to the given station (plus Hand/anywhere recipes), or
        /// pass null to show every recipe. Used so opening a specific prop (e.g. a furnace)
        /// shows that station's recipes first.
        /// </summary>
        void SetStationFilter(StationType? station);
        event Action Changed;
    }

    public sealed class PlaceholderCraftingViewModel : ICraftingViewModel
    {
        readonly CraftingRecipeRow[] _rows;
        public PlaceholderCraftingViewModel()
        {
            _rows = new CraftingRecipeRow[]
            {
                new CraftingRecipeRow { id="copper_bar_refined", display="Copper Bar (Refined)", icon=ItemIconResolver.Resolve("copper_bar_refined"), canCraft=true, station="Forge", disabledReason="" },
                new CraftingRecipeRow { id="iron_helmet",        display="Iron Helmet",          icon=ItemIconResolver.Resolve("iron_helmet"),        canCraft=false, station="Anvil", disabledReason="Need iron bars" },
                new CraftingRecipeRow { id="fireball",           display="Fireball Scroll",      icon=ItemIconResolver.Resolve("fireball"),           canCraft=true, station="Scribe", disabledReason="" },
                new CraftingRecipeRow { id="ruby_common",        display="Cut Ruby (Common)",    icon=ItemIconResolver.Resolve("ruby_common"),        canCraft=false, station="Bench", disabledReason="Need ruby" },
            };
        }
        public int RecipeCount => _rows.Length;
        public CraftingRecipeRow GetRecipe(int i) => (i >= 0 && i < _rows.Length) ? _rows[i] : default;
        public CraftingRecipeDetails GetDetails(string recipeId)
        {
            return new CraftingRecipeDetails
            {
                id = recipeId, display = recipeId, icon = ItemIconResolver.Resolve(recipeId),
                inputs = new[]
                {
                    new CraftingIngredient { itemId="copper_ore", display="Copper Ore", icon=ItemIconResolver.Resolve("copper_ore"), needed=2, have=5 },
                    new CraftingIngredient { itemId="coal",       display="Coal",       icon=ItemIconResolver.Resolve("coal"),       needed=1, have=0 },
                },
                outputs = new[] { new CraftingIngredient { itemId=recipeId, display=recipeId, icon=ItemIconResolver.Resolve(recipeId), needed=1, have=0 } },
                canCraft = true,
                disabledReason = "",
                maxCraftable = 1,
            };
        }
        public void Craft(string recipeId) { Debug.Log("[placeholder] craft " + recipeId); }
        public void Craft(string recipeId, int count) { Debug.Log("[placeholder] craft " + recipeId + " x" + count); }
        public void SetStationFilter(StationType? station) { }
        public event Action Changed;
        public void Raise() => Changed?.Invoke();
    }

    /// <summary>
    /// Crafting panel: scrollable recipe list (left), selected-recipe details + Craft (right).
    /// Skinnable from Resources/UI/InGame/ (craft_panel, craft_row, craft_button, btn_close).
    /// Open/close handled by <see cref="GamePanelsController"/>; Esc and the X button close.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CraftingView : MonoBehaviour
    {
        ICraftingViewModel _model;
        Canvas _canvas;
        GameObject _root;
        Transform _rowsContainer;
        Transform _detailsContainer;
        ScrollRect _listScroll;
        Button _craftBtn;
        Button _craftAllBtn;
        Text _craftAllLabel;
        string _selectedId;
        int _selectedMax;
        bool _detailsJobActive;
        float _progressRefreshTimer;
        // Row backgrounds by recipe id, so selection can re-highlight without a rebuild.
        readonly Dictionary<string, Image> _rowImages = new Dictionary<string, Image>();
        static readonly Color SelectedBg = new Color(0.16f, 0.20f, 0.27f, 0.96f);

        // Queue panel — shows the active timed-craft job beneath the recipe list.
        RectTransform _queueRoot;
        Text _queueItemLabel;
        Image _queueProgressFill;
        Text _queueProgressLabel;

        // ---- Category filter dropdown (2026-06-13 owner request) ----
        // "All" plus every real StationType (None is folded into "Hand" — see
        // FoundationCraftingAdapter.StationOrder/StationLabel).
        static readonly string[] CategoryNames = { "All", "Hand", "Workbench", "Furnace", "CookingPot", "Tannery" };
        Button _categoryBtn;
        Text _categoryLabel;
        GameObject _categoryPopup;
        Transform _filterParent;
        RectTransform _filterRect;

        public bool IsOpen => _root != null && _root.activeSelf;
        public event Action Closed;

        public void Init(ICraftingViewModel model)
        {
            Unsubscribe(); _model = model; Build(); Subscribe(); Refresh(); Hide();
        }
        void OnDestroy() => Unsubscribe();

        void Update()
        {
            // Live-refresh the progress bar for an in-progress timed craft (e.g. furnace
            // smelting) without rebuilding the whole panel every frame.
            if (!IsOpen || !_detailsJobActive) return;
            _progressRefreshTimer -= Time.deltaTime;
            if (_progressRefreshTimer <= 0f)
            {
                _progressRefreshTimer = 0.2f;
                RefreshDetails();
            }
        }
        void Subscribe()   { if (_model != null) _model.Changed += Refresh; }
        void Unsubscribe() { if (_model != null) _model.Changed -= Refresh; }

        public void Show() { if (_root != null) { _root.SetActive(true); Refresh(); } }
        public void Hide() { if (_root != null) _root.SetActive(false); CloseCategoryPopup(); Closed?.Invoke(); }
        public void Toggle() { if (IsOpen) Hide(); else Show(); }

        // ---- Category filter dropdown -------------------------------------

        void ToggleCategoryPopup()
        {
            if (_categoryPopup != null) CloseCategoryPopup();
            else BuildCategoryPopup();
        }

        void CloseCategoryPopup()
        {
            if (_categoryPopup == null) return;
            Destroy(_categoryPopup);
            _categoryPopup = null;
        }

        void BuildCategoryPopup()
        {
            _categoryPopup = new GameObject("CategoryPopup", typeof(RectTransform));
            _categoryPopup.transform.SetParent(_filterParent, false);
            var rt = _categoryPopup.GetComponent<RectTransform>();
            rt.anchorMin = _filterRect.anchorMin; rt.anchorMax = _filterRect.anchorMax;
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = _filterRect.anchoredPosition - new Vector2(0f, _filterRect.sizeDelta.y);
            rt.sizeDelta = new Vector2(_filterRect.sizeDelta.x, CategoryNames.Length * 30f);
            _categoryPopup.transform.SetAsLastSibling();

            var bg = _categoryPopup.AddComponent<Image>();
            bg.color = UiBuilder.PanelBg;
            var outline = _categoryPopup.AddComponent<Outline>();
            outline.effectColor = UiBuilder.Border;
            outline.effectDistance = new Vector2(1f, -1f);

            var vlg = _categoryPopup.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            for (int i = 0; i < CategoryNames.Length; i++)
            {
                string name = CategoryNames[i];
                var b = UiBuilder.NewButton(_categoryPopup.transform, "Cat_" + name, "craft_row", name, 14);
                var le = b.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 30f; le.minHeight = 30f;
                b.onClick.AddListener(() => SelectCategory(name));
            }
        }

        /// <summary>Apply the chosen station filter (null for "All") and reset the
        /// selection so Refresh() picks the first recipe in the newly-filtered list.</summary>
        void SelectCategory(string name)
        {
            if (_categoryLabel != null) _categoryLabel.text = name;
            CloseCategoryPopup();
            _selectedId = null;
            StationType? station = name == "All" ? (StationType?)null : (StationType)Enum.Parse(typeof(StationType), name);
            _model?.SetStationFilter(station);
        }

        void Build()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = UiBuilder.NewCanvas(transform, "CraftingCanvas", 200);
            _root = new GameObject("Root", typeof(RectTransform));
            _root.transform.SetParent(_canvas.transform, false);
            UiBuilder.Stretch(_root.GetComponent<RectTransform>());

            var scrim = UiBuilder.NewScrim(_root.transform);
            var sb = scrim.gameObject.AddComponent<Button>(); sb.transition = Selectable.Transition.None;
            sb.onClick.AddListener(Hide);

            var panel = UiBuilder.NewPanel(_root.transform, "CraftPanel", "craft_panel", UiBuilder.PanelBg);
            var pr = panel.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(900f, 600f);

            var title = UiBuilder.NewText(panel.transform, "Title", "Crafting", 24, TextAnchor.UpperCenter);
            var tr = title.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = new Vector2(0f, -16f);
            tr.sizeDelta = new Vector2(0f, 36f);

            var close = UiBuilder.NewButton(panel.transform, "Close", "btn_close", "X", 18);
            close.onClick.AddListener(Hide);
            var ccr = close.GetComponent<RectTransform>();
            ccr.anchorMin = ccr.anchorMax = new Vector2(1f, 1f);
            ccr.pivot = new Vector2(1f, 1f);
            ccr.anchoredPosition = new Vector2(-12f, -12f);
            ccr.sizeDelta = new Vector2(40f, 40f);

            // Category filter dropdown, sat above the recipe list (2026-06-13 owner
            // request): lets the player narrow the list to one station so they don't
            // have to scroll past recipes they don't need right now.
            _filterParent = panel.transform;
            _filterRect = UiBuilder.NewRect("Filter", panel.transform);
            _filterRect.anchorMin = new Vector2(0f, 1f); _filterRect.anchorMax = new Vector2(0f, 1f);
            _filterRect.pivot = new Vector2(0f, 1f);
            _filterRect.anchoredPosition = new Vector2(24f, -56f);
            _filterRect.sizeDelta = new Vector2(300f, 32f);

            _categoryBtn = UiBuilder.NewButton(_filterRect, "CategoryBtn", "craft_row", "All", 16);
            UiBuilder.Stretch(_categoryBtn.GetComponent<RectTransform>());
            _categoryLabel = _categoryBtn.GetComponentInChildren<Text>();
            _categoryBtn.onClick.AddListener(ToggleCategoryPopup);

            // Left: scrollable recipe list (300 wide).
            var listRect = UiBuilder.NewRect("List", panel.transform);
            listRect.anchorMin = new Vector2(0f, 0f); listRect.anchorMax = new Vector2(0f, 1f);
            listRect.pivot = new Vector2(0f, 0.5f);
            listRect.anchoredPosition = new Vector2(24f, 0f);
            listRect.offsetMin = new Vector2(24f, 24f); listRect.offsetMax = new Vector2(324f, -98f);

            var scroll = listRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            var viewport = UiBuilder.NewRect("Viewport", listRect);
            UiBuilder.Stretch(viewport);
            // RectMask2D instead of stencil Mask (see CharacterPanelView.CreateScrollView).
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0,0,0,0.001f); // raycast target for scroll drag
            scroll.viewport = viewport;
            var content = UiBuilder.NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, 0f);
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f; vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            _rowsContainer = content;
            _listScroll = scroll;

            // Right: details + Craft button (rest of the panel).
            var detRect = UiBuilder.NewRect("Details", panel.transform);
            detRect.anchorMin = new Vector2(0f, 0f); detRect.anchorMax = new Vector2(1f, 1f);
            detRect.offsetMin = new Vector2(340f, 24f); detRect.offsetMax = new Vector2(-24f, -60f);
            _detailsContainer = detRect;

            _craftBtn = UiBuilder.NewButton(panel.transform, "CraftBtn", "craft_button", "Craft", 20);
            _craftBtn.onClick.AddListener(() => { if (!string.IsNullOrEmpty(_selectedId)) _model?.Craft(_selectedId); });
            var cbr = _craftBtn.GetComponent<RectTransform>();
            cbr.anchorMin = new Vector2(1f, 0f); cbr.anchorMax = new Vector2(1f, 0f);
            cbr.pivot = new Vector2(1f, 0f);
            cbr.anchoredPosition = new Vector2(-24f, 24f);
            cbr.sizeDelta = new Vector2(200f, 56f);

            // Queue panel — below the recipe list, shows the active timed-craft job.
            BuildQueuePanel(panel.transform);

            // Craft All: batch-crafts as many as ingredients/space allow (left of Craft).
            _craftAllBtn = UiBuilder.NewButton(panel.transform, "CraftAllBtn", "craft_button", "Craft All", 18);
            _craftAllBtn.onClick.AddListener(() =>
            {
                if (!string.IsNullOrEmpty(_selectedId) && _selectedMax > 0) _model?.Craft(_selectedId, _selectedMax);
            });
            _craftAllLabel = _craftAllBtn.GetComponentInChildren<Text>();
            // "Craft All (99)" can be wider than the static "Craft All" label;
            // shrink to fit rather than overflowing the button.
            UiBuilder.FitText(_craftAllLabel);
            var car = _craftAllBtn.GetComponent<RectTransform>();
            car.anchorMin = new Vector2(1f, 0f); car.anchorMax = new Vector2(1f, 0f);
            car.pivot = new Vector2(1f, 0f);
            car.anchoredPosition = new Vector2(-236f, 24f);
            car.sizeDelta = new Vector2(170f, 56f);
        }

        // Queue panel — 3px ink border, 1px steel inset, 1px gold hairline, hard bottom
        // shadow. Shows beneath the recipe list (left column, bottom area of the panel).
        void BuildQueuePanel(Transform panel)
        {
            // Position: below the recipe list, same left offset; 90px tall.
            var root = UiBuilder.NewRect("QueuePanel", panel);
            _queueRoot = root;
            root.anchorMin = new Vector2(0f, 0f); root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 0f);
            root.anchoredPosition = new Vector2(24f, 24f);
            root.sizeDelta = new Vector2(300f, 90f);

            // Hard ink border (3px)
            var bg = root.gameObject.AddComponent<Image>();
            bg.color = LitIsoTheme.Panel;
            var inkOutline = root.gameObject.AddComponent<Outline>();
            inkOutline.effectColor = LitIsoTheme.Base;
            inkOutline.effectDistance = new Vector2(3f, -3f);
            inkOutline.useGraphicAlpha = false;

            // Steel inset (1px)
            var steel = UiBuilder.NewImage(root, "Steel", null, new Color(LitIsoTheme.Stone.r, LitIsoTheme.Stone.g, LitIsoTheme.Stone.b, 0.6f));
            steel.raycastTarget = false;
            var sr = steel.rectTransform;
            sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one;
            sr.offsetMin = new Vector2(3f, 3f); sr.offsetMax = new Vector2(-3f, -3f);
            var steelFill = UiBuilder.NewImage(sr, "SteelFill", null, LitIsoTheme.Panel);
            steelFill.raycastTarget = false;
            var sfr = steelFill.rectTransform;
            sfr.anchorMin = Vector2.zero; sfr.anchorMax = Vector2.one;
            sfr.offsetMin = new Vector2(1f, 1f); sfr.offsetMax = new Vector2(-1f, -1f);
            steel.transform.SetAsFirstSibling();

            // Gold hairline (1px)
            var gold = UiBuilder.NewImage(root, "Gold", null, new Color(LitIsoTheme.GoldDeep.r, LitIsoTheme.GoldDeep.g, LitIsoTheme.GoldDeep.b, 0.5f));
            gold.raycastTarget = false;
            var gr = gold.rectTransform;
            gr.anchorMin = Vector2.zero; gr.anchorMax = Vector2.one;
            gr.offsetMin = new Vector2(4f, 4f); gr.offsetMax = new Vector2(-4f, -4f);
            var goldFill = UiBuilder.NewImage(gr, "GoldFill", null, LitIsoTheme.Panel);
            goldFill.raycastTarget = false;
            var gfr = goldFill.rectTransform;
            gfr.anchorMin = Vector2.zero; gfr.anchorMax = Vector2.one;
            gfr.offsetMin = new Vector2(1f, 1f); gfr.offsetMax = new Vector2(-1f, -1f);
            gold.transform.SetAsFirstSibling();

            // 6px hard bottom shadow
            var shadow = new GameObject("Shadow", typeof(RectTransform));
            shadow.transform.SetParent(panel, false);
            var shadowImg = shadow.AddComponent<Image>();
            shadowImg.color = LitIsoTheme.Base;
            shadowImg.raycastTarget = false;
            var shr = shadowImg.rectTransform;
            shr.anchorMin = new Vector2(0f, 0f); shr.anchorMax = new Vector2(0f, 0f);
            shr.pivot = new Vector2(0f, 0f);
            shr.anchoredPosition = new Vector2(24f, 18f); // 6px below root
            shr.sizeDelta = new Vector2(300f, 90f);
            shadow.transform.SetAsFirstSibling();

            // QUEUE header label
            var hdr = UiBuilder.NewText(root, "QueueHdr", "QUEUE", 11, TextAnchor.UpperLeft, LitIsoTheme.Gold);
            hdr.font = LitIsoTheme.DisplayFont;
            hdr.raycastTarget = false;
            var hdr_r = hdr.rectTransform;
            hdr_r.anchorMin = new Vector2(0f, 1f); hdr_r.anchorMax = new Vector2(1f, 1f);
            hdr_r.pivot = new Vector2(0f, 1f);
            hdr_r.anchoredPosition = new Vector2(10f, -8f);
            hdr_r.sizeDelta = new Vector2(-20f, 16f);

            // Item name label
            _queueItemLabel = UiBuilder.NewText(root, "QueueItem", "— idle —", 14, TextAnchor.UpperLeft, LitIsoTheme.WarmTan);
            LitIsoTheme.ApplyBody(_queueItemLabel, 14, LitIsoTheme.WarmTan);
            _queueItemLabel.raycastTarget = false;
            var il_r = _queueItemLabel.rectTransform;
            il_r.anchorMin = new Vector2(0f, 1f); il_r.anchorMax = new Vector2(1f, 1f);
            il_r.pivot = new Vector2(0f, 1f);
            il_r.anchoredPosition = new Vector2(10f, -26f);
            il_r.sizeDelta = new Vector2(-80f, 18f);

            // Progress bar track (dark)
            var track = UiBuilder.NewImage(root, "ProgTrack", null, LitIsoTheme.Base);
            var tk_outline = track.gameObject.AddComponent<Outline>();
            tk_outline.effectColor = LitIsoTheme.Base; tk_outline.effectDistance = new Vector2(2f, -2f); tk_outline.useGraphicAlpha = false;
            track.raycastTarget = false;
            var tk_r = track.rectTransform;
            tk_r.anchorMin = new Vector2(0f, 0f); tk_r.anchorMax = new Vector2(1f, 0f);
            tk_r.pivot = new Vector2(0f, 0f);
            tk_r.anchoredPosition = new Vector2(10f, 12f);
            tk_r.sizeDelta = new Vector2(-20f, 14f);

            // Progress fill (gold)
            _queueProgressFill = UiBuilder.NewImage(tk_r, "ProgFill", null, LitIsoTheme.Gold);
            _queueProgressFill.type = Image.Type.Filled;
            _queueProgressFill.fillMethod = Image.FillMethod.Horizontal;
            _queueProgressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _queueProgressFill.fillAmount = 0f;
            _queueProgressFill.raycastTarget = false;
            var pf_r = _queueProgressFill.rectTransform;
            pf_r.anchorMin = Vector2.zero; pf_r.anchorMax = Vector2.one;
            pf_r.offsetMin = new Vector2(2f, 2f); pf_r.offsetMax = new Vector2(-2f, -2f);

            // Progress percent label (right of header)
            _queueProgressLabel = UiBuilder.NewText(root, "QueuePct", "", 11, TextAnchor.UpperRight, LitIsoTheme.GoldLit);
            LitIsoTheme.ApplyBody(_queueProgressLabel, 11, LitIsoTheme.GoldLit);
            _queueProgressLabel.raycastTarget = false;
            var pl_r = _queueProgressLabel.rectTransform;
            pl_r.anchorMin = new Vector2(0f, 1f); pl_r.anchorMax = new Vector2(1f, 1f);
            pl_r.pivot = new Vector2(1f, 1f);
            pl_r.anchoredPosition = new Vector2(-10f, -26f);
            pl_r.sizeDelta = new Vector2(60f, 18f);

            // Cancel button — stone style, top-right of queue panel
            var cancelBtn = UiBuilder.NewButton(root, "CancelBtn", null, "✕", 14);
            var cb_bg = cancelBtn.GetComponent<Image>();
            if (cb_bg != null) LitIsoTheme.StyleButton(cancelBtn, cb_bg, LitIsoTheme.ButtonStyle.Stone);
            cancelBtn.onClick.AddListener(() => Debug.Log("[CraftingView] Cancel job requested (wire to adapter)."));
            var cb_r = cancelBtn.GetComponent<RectTransform>();
            cb_r.anchorMin = cb_r.anchorMax = new Vector2(1f, 1f); cb_r.pivot = new Vector2(1f, 1f);
            cb_r.anchoredPosition = new Vector2(-8f, -6f);
            cb_r.sizeDelta = new Vector2(28f, 22f);
        }

        // Sync the queue panel visuals from the currently-selected recipe's job state.
        void RefreshQueuePanel(CraftingRecipeDetails d)
        {
            if (_queueRoot == null) return;
            bool active = d.jobActive;
            if (_queueItemLabel != null)
                _queueItemLabel.text = active ? d.display : "— idle —";
            if (_queueProgressFill != null)
                _queueProgressFill.fillAmount = active ? Mathf.Clamp01(d.jobProgress01) : 0f;
            if (_queueProgressLabel != null)
                _queueProgressLabel.text = active ? $"{Mathf.RoundToInt(d.jobProgress01 * 100f)}%" : "";
        }

        void Refresh()
        {
            if (_model == null || _rowsContainer == null) return;
            // Remember where the player scrolled to: the rebuild below otherwise snaps
            // the list back to the top on every inventory change (e.g. mid-batch-craft).
            float scrollPos = _listScroll != null ? _listScroll.verticalNormalizedPosition : 1f;
            _rowImages.Clear();
            foreach (Transform c in _rowsContainer) Destroy(c.gameObject);
            string lastStation = null;
            for (int i = 0; i < _model.RecipeCount; i++)
            {
                var r = _model.GetRecipe(i);
                // Section header whenever the station group changes (model is station-sorted).
                if (!string.IsNullOrEmpty(r.station) && r.station != lastStation)
                {
                    lastStation = r.station;
                    var header = UiBuilder.NewText(_rowsContainer, "Hdr_" + r.station, r.station, 14,
                        TextAnchor.MiddleLeft, UiBuilder.MutedCol);
                    var le = header.gameObject.AddComponent<LayoutElement>();
                    le.minHeight = 24f; le.preferredHeight = 24f;
                }
                var row = UiBuilder.NewPanel(_rowsContainer, "Row_" + r.id, "craft_row", UiBuilder.SlotBg);
                var rr = row.rectTransform;
                rr.sizeDelta = new Vector2(0f, 56f);
                // Highlight the selected recipe's row so the list shows what the details pane is about.
                _rowImages[r.id] = row;
                if (r.id == _selectedId) row.color = SelectedBg;
                var btn = row.gameObject.AddComponent<Button>(); btn.targetGraphic = row;
                string id = r.id;
                btn.onClick.AddListener(() => SelectRow(id));
                // Icon + label inside the row.
                var icon = UiBuilder.NewImage(row.transform, "Icon", r.icon, Color.white);
                icon.preserveAspect = true; icon.enabled = r.icon != null;
                var ir = icon.rectTransform;
                ir.anchorMin = new Vector2(0f, 0.5f); ir.anchorMax = new Vector2(0f, 0.5f);
                ir.pivot = new Vector2(0f, 0.5f);
                ir.anchoredPosition = new Vector2(8f, 0f); ir.sizeDelta = new Vector2(40f, 40f);
                var lbl = UiBuilder.NewText(row.transform, "Lbl", r.display, 16, TextAnchor.MiddleLeft,
                    r.canCraft ? UiBuilder.TextCol : UiBuilder.MutedCol);
                var lr = lbl.rectTransform;
                lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(1f, 1f);
                lr.offsetMin = new Vector2(56f, 0f); lr.offsetMax = new Vector2(-8f, 0f);
                UiBuilder.FitText(lbl);
            }
            if (string.IsNullOrEmpty(_selectedId) && _model.RecipeCount > 0) SelectRow(_model.GetRecipe(0).id);
            else RefreshDetails();
            // Restore the scroll position once the deferred Destroy()/layout pass settles.
            if (_listScroll != null && isActiveAndEnabled && IsOpen)
                StartCoroutine(RestoreScroll(scrollPos));
        }

        /// <summary>Move selection (and its row highlight) without rebuilding the list.</summary>
        void SelectRow(string id)
        {
            if (!string.IsNullOrEmpty(_selectedId) && _selectedId != id &&
                _rowImages.TryGetValue(_selectedId, out var prev) && prev != null)
                prev.color = UiBuilder.SlotBg;
            _selectedId = id;
            if (!string.IsNullOrEmpty(id) && _rowImages.TryGetValue(id, out var cur) && cur != null)
                cur.color = SelectedBg;
            RefreshDetails();
        }

        IEnumerator RestoreScroll(float normalizedPos)
        {
            yield return null;
            if (_listScroll != null)
                _listScroll.verticalNormalizedPosition = Mathf.Clamp01(normalizedPos);
        }

        /// <summary>Rebuild the right-hand details pane for the current _selectedId.</summary>
        void RefreshDetails()
        {
            if (_detailsContainer == null) return;
            foreach (Transform c in _detailsContainer) Destroy(c.gameObject);

            if (string.IsNullOrEmpty(_selectedId) || _model == null) { _detailsJobActive = false; return; }

            var d = _model.GetDetails(_selectedId);
            _detailsJobActive = d.jobActive;
            _selectedMax = d.maxCraftable;

            // Recipe name heading
            var heading = UiBuilder.NewText(_detailsContainer, "RecipeName", d.display, 20, TextAnchor.UpperLeft, LitIsoTheme.Gold);
            LitIsoTheme.ApplyDisplay(heading, 20, LitIsoTheme.Gold);
            var hr = heading.rectTransform;
            hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f);
            hr.pivot = new Vector2(0f, 1f);
            hr.anchoredPosition = new Vector2(0f, -4f);
            hr.sizeDelta = new Vector2(0f, 28f);

            // Gold divider
            var div = UiBuilder.NewImage(_detailsContainer, "Divider", null,
                new Color(LitIsoTheme.Gold.r, LitIsoTheme.Gold.g, LitIsoTheme.Gold.b, 0.28f));
            div.raycastTarget = false;
            var dr = div.rectTransform;
            dr.anchorMin = new Vector2(0f, 1f); dr.anchorMax = new Vector2(1f, 1f);
            dr.pivot = new Vector2(0f, 1f);
            dr.anchoredPosition = new Vector2(0f, -36f);
            dr.sizeDelta = new Vector2(0f, 1f);

            // Ingredients section
            float y = -44f;
            if (d.inputs != null && d.inputs.Length > 0)
            {
                var ingHdr = UiBuilder.NewText(_detailsContainer, "IngHdr", "INGREDIENTS", 11, TextAnchor.UpperLeft, LitIsoTheme.WarmTan);
                LitIsoTheme.ApplyBody(ingHdr, 11, LitIsoTheme.WarmTan);
                ingHdr.raycastTarget = false;
                var wr = ingHdr.rectTransform;
                wr.anchorMin = new Vector2(0f, 1f); wr.anchorMax = new Vector2(1f, 1f);
                wr.pivot = new Vector2(0f, 1f);
                wr.anchoredPosition = new Vector2(0f, y); wr.sizeDelta = new Vector2(0f, 18f);
                y -= 22f;

                foreach (var ing in d.inputs)
                {
                    bool ok = ing.have >= ing.needed;
                    var row = UiBuilder.NewImage(_detailsContainer, "Ing_" + ing.itemId, null,
                        new Color(0.09f, 0.12f, 0.16f, 0.55f));
                    row.raycastTarget = false;
                    var ingRt = row.rectTransform;
                    ingRt.anchorMin = new Vector2(0f, 1f); ingRt.anchorMax = new Vector2(1f, 1f);
                    ingRt.pivot = new Vector2(0f, 1f);
                    ingRt.anchoredPosition = new Vector2(0f, y); ingRt.sizeDelta = new Vector2(0f, 32f);

                    // Tick / X indicator
                    var tick = UiBuilder.NewText(row.transform, "Tick", ok ? "✔" : "✘", 14, TextAnchor.MiddleLeft,
                        ok ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.3f, 0.3f));
                    LitIsoTheme.ApplyBody(tick, 14, ok ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.3f, 0.3f));
                    tick.raycastTarget = false;
                    var tkr = tick.rectTransform;
                    tkr.anchorMin = new Vector2(0f, 0f); tkr.anchorMax = new Vector2(0f, 1f);
                    tkr.pivot = new Vector2(0f, 0.5f);
                    tkr.anchoredPosition = new Vector2(6f, 0f); tkr.sizeDelta = new Vector2(18f, 0f);

                    // Ingredient name
                    var name = UiBuilder.NewText(row.transform, "Name", ing.display, 14, TextAnchor.MiddleLeft,
                        ok ? LitIsoTheme.Parchment : LitIsoTheme.WarmTan);
                    LitIsoTheme.ApplyBody(name, 14, ok ? LitIsoTheme.Parchment : LitIsoTheme.WarmTan);
                    name.raycastTarget = false;
                    var nr = name.rectTransform;
                    nr.anchorMin = new Vector2(0f, 0f); nr.anchorMax = new Vector2(1f, 1f);
                    nr.pivot = new Vector2(0f, 0.5f);
                    nr.offsetMin = new Vector2(28f, 0f); nr.offsetMax = new Vector2(-64f, 0f);

                    // Quantity fraction (right)
                    string qty = ing.have + "/" + ing.needed;
                    var count = UiBuilder.NewText(row.transform, "Qty", qty, 13, TextAnchor.MiddleRight,
                        ok ? LitIsoTheme.GoldLit : new Color(0.85f, 0.5f, 0.3f));
                    LitIsoTheme.ApplyBody(count, 13, ok ? LitIsoTheme.GoldLit : new Color(0.85f, 0.5f, 0.3f));
                    count.raycastTarget = false;
                    var cr = count.rectTransform;
                    cr.anchorMin = new Vector2(1f, 0f); cr.anchorMax = new Vector2(1f, 1f);
                    cr.pivot = new Vector2(1f, 0.5f);
                    cr.anchoredPosition = new Vector2(-6f, 0f); cr.sizeDelta = new Vector2(56f, 0f);

                    y -= 36f;
                }
            }

            // Fuel info (optional)
            if (!string.IsNullOrEmpty(d.fuelInfo))
            {
                var fuel = UiBuilder.NewText(_detailsContainer, "Fuel", d.fuelInfo, 13, TextAnchor.UpperLeft, LitIsoTheme.WarmTan);
                LitIsoTheme.ApplyBody(fuel, 13, LitIsoTheme.WarmTan);
                fuel.raycastTarget = false;
                var fr = fuel.rectTransform;
                fr.anchorMin = new Vector2(0f, 1f); fr.anchorMax = new Vector2(1f, 1f);
                fr.pivot = new Vector2(0f, 1f);
                fr.anchoredPosition = new Vector2(0f, y); fr.sizeDelta = new Vector2(0f, 18f);
                y -= 22f;
            }

            // Disabled reason
            if (!d.canCraft && !string.IsNullOrEmpty(d.disabledReason))
            {
                var reason = UiBuilder.NewText(_detailsContainer, "Reason", d.disabledReason, 12, TextAnchor.UpperLeft,
                    new Color(0.85f, 0.5f, 0.3f));
                LitIsoTheme.ApplyBody(reason, 12, new Color(0.85f, 0.5f, 0.3f));
                reason.horizontalOverflow = HorizontalWrapMode.Wrap;
                reason.raycastTarget = false;
                var rr = reason.rectTransform;
                rr.anchorMin = new Vector2(0f, 1f); rr.anchorMax = new Vector2(1f, 1f);
                rr.pivot = new Vector2(0f, 1f);
                rr.anchoredPosition = new Vector2(0f, y); rr.sizeDelta = new Vector2(0f, 36f);
            }

            // Craft button state
            if (_craftBtn != null)
            {
                _craftBtn.interactable = d.canCraft;
                var img = _craftBtn.GetComponent<Image>();
                if (img != null) LitIsoTheme.StyleButton(_craftBtn, img,
                    d.canCraft ? LitIsoTheme.ButtonStyle.Gold : LitIsoTheme.ButtonStyle.Stone);
            }

            // Craft All button state + label
            if (_craftAllBtn != null)
            {
                bool canAll = d.canCraft && d.maxCraftable > 0;
                _craftAllBtn.interactable = canAll;
                if (_craftAllLabel != null)
                    _craftAllLabel.text = canAll ? "Craft All (" + d.maxCraftable + ")" : "Craft All";
                var img2 = _craftAllBtn.GetComponent<Image>();
                if (img2 != null) LitIsoTheme.StyleButton(_craftAllBtn, img2,
                    canAll ? LitIsoTheme.ButtonStyle.Gold : LitIsoTheme.ButtonStyle.Stone);
            }

            // Sync queue panel (added Phase 4)
            RefreshQueuePanel(d);
        }
    }
}
