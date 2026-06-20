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
            yield return null; // old rows are Destroy()ed end-of-frame; wait them out
            if (_listScroll != null)
                _listScroll.verticalNormalizedPosition = Mathf.Clamp01(normalizedPos);
        }

        void RefreshDetails()
        {
            if (_detailsContainer == null) return;
            foreach (Transform c in _detailsContainer) Destroy(c.gameObject);
            if (string.IsNullOrEmpty(_selectedId) || _model == null) { _detailsJobActive = false; return; }
            var d = _model.GetDetails(_selectedId);

            var head = UiBuilder.NewText(_detailsContainer, "Head", d.display, 22, TextAnchor.UpperLeft);
            var hr = head.rectTransform;
            hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f);
            hr.pivot = new Vector2(0f, 1f);
            hr.anchoredPosition = new Vector2(0f, 0f); hr.sizeDelta = new Vector2(0f, 32f);
            UiBuilder.FitText(head);

            var sub = UiBuilder.NewText(_detailsContainer, "Sub", "Ingredients", 16, TextAnchor.UpperLeft, UiBuilder.MutedCol);
            var sr = sub.rectTransform;
            sr.anchorMin = new Vector2(0f, 1f); sr.anchorMax = new Vector2(1f, 1f);
            sr.pivot = new Vector2(0f, 1f);
            sr.anchoredPosition = new Vector2(0f, -40f); sr.sizeDelta = new Vector2(0f, 24f);

            float y = -72f;
            if (d.inputs != null)
                for (int i = 0; i < d.inputs.Length; i++)
                {
                    var ing = d.inputs[i];
                    bool enough = ing.have >= ing.needed;
                    string line = enough
                        ? $"{ing.display}   {ing.have}/{ing.needed}"
                        : $"{ing.display}   {ing.have}/{ing.needed}   (short {ing.needed - ing.have})";
                    var t = UiBuilder.NewText(_detailsContainer, "Ing_" + i, line, 16, TextAnchor.UpperLeft,
                        enough ? UiBuilder.TextCol : new Color(0.85f, 0.45f, 0.45f, 1f));
                    var tr = t.rectTransform;
                    tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f);
                    tr.pivot = new Vector2(0f, 1f);
                    tr.anchoredPosition = new Vector2(8f, y); tr.sizeDelta = new Vector2(0f, 22f);
                    UiBuilder.FitText(t);
                    y -= 26f;
                }

            if (d.outputs != null && d.outputs.Length > 0)
            {
                y -= 10f;
                var oh = UiBuilder.NewText(_detailsContainer, "OutHead", "Makes", 16, TextAnchor.UpperLeft, UiBuilder.MutedCol);
                var ohr = oh.rectTransform;
                ohr.anchorMin = new Vector2(0f, 1f); ohr.anchorMax = new Vector2(1f, 1f);
                ohr.pivot = new Vector2(0f, 1f);
                ohr.anchoredPosition = new Vector2(0f, y); ohr.sizeDelta = new Vector2(0f, 24f);
                y -= 28f;
                for (int i = 0; i < d.outputs.Length; i++)
                {
                    var o = d.outputs[i];
                    var t = UiBuilder.NewText(_detailsContainer, "Out_" + i,
                        $"{o.display} x{o.needed}", 16, TextAnchor.UpperLeft);
                    var tr = t.rectTransform;
                    tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f);
                    tr.pivot = new Vector2(0f, 1f);
                    tr.anchoredPosition = new Vector2(8f, y); tr.sizeDelta = new Vector2(0f, 22f);
                    UiBuilder.FitText(t);
                    y -= 26f;
                }
            }

            // Fuel requirement (e.g. furnace smelting needs wood).
            if (!string.IsNullOrEmpty(d.fuelInfo))
            {
                y -= 6f;
                var fuel = UiBuilder.NewText(_detailsContainer, "Fuel", d.fuelInfo, 16,
                    TextAnchor.UpperLeft, UiBuilder.MutedCol);
                var fr = fuel.rectTransform;
                fr.anchorMin = new Vector2(0f, 1f); fr.anchorMax = new Vector2(1f, 1f);
                fr.pivot = new Vector2(0f, 1f);
                fr.anchoredPosition = new Vector2(0f, y); fr.sizeDelta = new Vector2(0f, 22f);
                UiBuilder.FitText(fuel);
                y -= 26f;
            }

            // Craft-time / in-progress indicator (Minecraft-style smelting bar).
            _detailsJobActive = d.jobActive;
            if (d.jobActive)
            {
                y -= 6f;
                int pct = Mathf.RoundToInt(d.jobProgress01 * 100f);
                var prog = UiBuilder.NewText(_detailsContainer, "Progress", $"Working... {pct}%", 16,
                    TextAnchor.UpperLeft, new Color(0.55f, 0.85f, 0.55f, 1f));
                var pr = prog.rectTransform;
                pr.anchorMin = new Vector2(0f, 1f); pr.anchorMax = new Vector2(1f, 1f);
                pr.pivot = new Vector2(0f, 1f);
                pr.anchoredPosition = new Vector2(0f, y); pr.sizeDelta = new Vector2(0f, 22f);
                UiBuilder.FitText(prog);
                y -= 26f;

                // Simple bar: background track + filled portion sized by progress.
                var track = UiBuilder.NewImage(_detailsContainer, "ProgTrack", null, UiBuilder.SlotBg);
                var trr = track.rectTransform;
                trr.anchorMin = new Vector2(0f, 1f); trr.anchorMax = new Vector2(1f, 1f);
                trr.pivot = new Vector2(0f, 1f);
                trr.anchoredPosition = new Vector2(0f, y); trr.sizeDelta = new Vector2(0f, 16f);

                var fill = UiBuilder.NewImage(track.transform, "ProgFill", null, new Color(0.40f, 0.78f, 0.45f, 1f));
                var flr = fill.rectTransform;
                flr.anchorMin = new Vector2(0f, 0f); flr.anchorMax = new Vector2(Mathf.Clamp01(d.jobProgress01), 1f);
                flr.pivot = new Vector2(0f, 0.5f);
                flr.offsetMin = Vector2.zero; flr.offsetMax = Vector2.zero;
                y -= 24f;
            }
            else if (d.craftTimeSeconds > 0f)
            {
                y -= 6f;
                var time = UiBuilder.NewText(_detailsContainer, "CraftTime", $"Time: {d.craftTimeSeconds:0.#}s", 16,
                    TextAnchor.UpperLeft, UiBuilder.MutedCol);
                var trr2 = time.rectTransform;
                trr2.anchorMin = new Vector2(0f, 1f); trr2.anchorMax = new Vector2(1f, 1f);
                trr2.pivot = new Vector2(0f, 1f);
                trr2.anchoredPosition = new Vector2(0f, y); trr2.sizeDelta = new Vector2(0f, 22f);
                UiBuilder.FitText(time);
                y -= 26f;
            }

            // Tell the player exactly why the craft button is greyed out.
            if (!d.canCraft && !string.IsNullOrEmpty(d.disabledReason))
            {
                y -= 10f;
                var why = UiBuilder.NewText(_detailsContainer, "Reason", d.disabledReason, 16,
                    TextAnchor.UpperLeft, new Color(0.85f, 0.45f, 0.45f, 1f));
                var wr = why.rectTransform;
                wr.anchorMin = new Vector2(0f, 1f); wr.anchorMax = new Vector2(1f, 1f);
                wr.pivot = new Vector2(0f, 1f);
                wr.anchoredPosition = new Vector2(0f, y); wr.sizeDelta = new Vector2(0f, 22f);
                UiBuilder.FitText(why);
            }

            _selectedMax = d.maxCraftable;
            if (_craftBtn != null) _craftBtn.interactable = d.canCraft;
            if (_craftAllBtn != null)
            {
                _craftAllBtn.interactable = d.canCraft && d.maxCraftable > 1;
                if (_craftAllLabel != null)
                    _craftAllLabel.text = d.maxCraftable > 1 ? $"Craft All ({d.maxCraftable})" : "Craft All";
            }
        }
    }
}
