using System;
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
    }

    /// <summary>Model the crafting panel renders from. Foundation-free; adapter binds later.</summary>
    public interface ICraftingViewModel
    {
        int RecipeCount { get; }
        CraftingRecipeRow GetRecipe(int i);
        CraftingRecipeDetails GetDetails(string recipeId);
        void Craft(string recipeId);
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
            };
        }
        public void Craft(string recipeId) { Debug.Log("[placeholder] craft " + recipeId); }
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
        Button _craftBtn;
        string _selectedId;

        public bool IsOpen => _root != null && _root.activeSelf;
        public event Action Closed;

        public void Init(ICraftingViewModel model)
        {
            Unsubscribe(); _model = model; Build(); Subscribe(); Refresh(); Hide();
        }
        void OnDestroy() => Unsubscribe();
        void Subscribe()   { if (_model != null) _model.Changed += Refresh; }
        void Unsubscribe() { if (_model != null) _model.Changed -= Refresh; }

        public void Show() { if (_root != null) { _root.SetActive(true); Refresh(); } }
        public void Hide() { if (_root != null) _root.SetActive(false); Closed?.Invoke(); }
        public void Toggle() { if (IsOpen) Hide(); else Show(); }

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

            // Left: scrollable recipe list (300 wide).
            var listRect = UiBuilder.NewRect("List", panel.transform);
            listRect.anchorMin = new Vector2(0f, 0f); listRect.anchorMax = new Vector2(0f, 1f);
            listRect.pivot = new Vector2(0f, 0.5f);
            listRect.anchoredPosition = new Vector2(24f, 0f);
            listRect.offsetMin = new Vector2(24f, 24f); listRect.offsetMax = new Vector2(324f, -60f);

            var scroll = listRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            var viewport = UiBuilder.NewRect("Viewport", listRect);
            UiBuilder.Stretch(viewport);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            viewport.gameObject.AddComponent<Image>().color = new Color(0,0,0,0.001f); // mask needs Image
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
        }

        void Refresh()
        {
            if (_model == null || _rowsContainer == null) return;
            foreach (Transform c in _rowsContainer) Destroy(c.gameObject);
            for (int i = 0; i < _model.RecipeCount; i++)
            {
                var r = _model.GetRecipe(i);
                var row = UiBuilder.NewPanel(_rowsContainer, "Row_" + r.id, "craft_row", UiBuilder.SlotBg);
                var rr = row.rectTransform;
                rr.sizeDelta = new Vector2(0f, 56f);
                var btn = row.gameObject.AddComponent<Button>(); btn.targetGraphic = row;
                string id = r.id;
                btn.onClick.AddListener(() => { _selectedId = id; RefreshDetails(); });
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
            }
            if (string.IsNullOrEmpty(_selectedId) && _model.RecipeCount > 0) _selectedId = _model.GetRecipe(0).id;
            RefreshDetails();
        }

        void RefreshDetails()
        {
            if (_detailsContainer == null) return;
            foreach (Transform c in _detailsContainer) Destroy(c.gameObject);
            if (string.IsNullOrEmpty(_selectedId) || _model == null) return;
            var d = _model.GetDetails(_selectedId);

            var head = UiBuilder.NewText(_detailsContainer, "Head", d.display, 22, TextAnchor.UpperLeft);
            var hr = head.rectTransform;
            hr.anchorMin = new Vector2(0f, 1f); hr.anchorMax = new Vector2(1f, 1f);
            hr.pivot = new Vector2(0f, 1f);
            hr.anchoredPosition = new Vector2(0f, 0f); hr.sizeDelta = new Vector2(0f, 32f);

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
                    var t = UiBuilder.NewText(_detailsContainer, "Ing_" + i,
                        $"{ing.display}   {ing.have}/{ing.needed}", 16, TextAnchor.UpperLeft,
                        ing.have >= ing.needed ? UiBuilder.TextCol : new Color(0.85f, 0.45f, 0.45f, 1f));
                    var tr = t.rectTransform;
                    tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f);
                    tr.pivot = new Vector2(0f, 1f);
                    tr.anchoredPosition = new Vector2(8f, y); tr.sizeDelta = new Vector2(0f, 22f);
                    y -= 26f;
                }

            if (_craftBtn != null) _craftBtn.interactable = d.canCraft;
        }
    }
}
