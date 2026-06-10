using System;
using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    /// <summary>Model the inventory panel renders from. Foundation-free; an adapter binds it later.</summary>
    public interface IInventoryViewModel
    {
        int Capacity { get; }
        HudSlot GetSlot(int i);  // reuses HudSlot from GameUIController (label/count/icon/selected)
        event Action Changed;
    }

    /// <summary>Demo data so the panel previews standalone before the Foundation adapter binds.</summary>
    public sealed class PlaceholderInventoryViewModel : IInventoryViewModel
    {
        readonly HudSlot[] _slots;
        public PlaceholderInventoryViewModel(int capacity = 36)
        {
            _slots = new HudSlot[capacity];
            string[] demo = { "wood", "stone", "iron_ore", "coal", "copper_ore", "ruby_common" };
            for (int i = 0; i < demo.Length && i < capacity; i++)
                _slots[i] = new HudSlot { label = demo[i], count = 5 + i * 3, icon = ItemIconResolver.Resolve(demo[i]) };
        }
        public int Capacity => _slots.Length;
        public HudSlot GetSlot(int i) => (i >= 0 && i < _slots.Length) ? _slots[i] : default;
        public event Action Changed;
        public void Raise() => Changed?.Invoke();
    }

    /// <summary>
    /// Inventory grid panel. Skinnable from Resources/UI/InGame/ (inv_panel, inv_slot,
    /// btn_close). Opens via key (handled by GamePanelsController), closes via Esc or X.
    /// Slots populated by an <see cref="IInventoryViewModel"/>; icons resolved by
    /// <see cref="ItemIconResolver"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryView : MonoBehaviour
    {
        const int COLUMNS = 6;
        const float SLOT = 80f, GAP = 6f, PAD = 32f;

        IInventoryViewModel _model;
        Canvas _canvas;
        GameObject _root;
        Image[] _icons;
        Text[] _counts;
        Image[] _highlights;

        public bool IsOpen => _root != null && _root.activeSelf;

        public event Action Closed;

        public void Init(IInventoryViewModel model)
        {
            Unsubscribe();
            _model = model;
            Build();
            Subscribe();
            Refresh();
            Hide();
        }

        void OnDestroy() => Unsubscribe();
        void OnEnable() => LitIsoFont.TextScaleChanged += HandleTextScaleChanged;
        void OnDisable() => LitIsoFont.TextScaleChanged -= HandleTextScaleChanged;
        void Subscribe()   { if (_model != null) _model.Changed += Refresh; }
        void Unsubscribe() { if (_model != null) _model.Changed -= Refresh; }

        public void Show() { if (_root != null) { _root.SetActive(true); Refresh(); } }
        public void Hide() { if (_root != null) _root.SetActive(false); Closed?.Invoke(); }
        public void Toggle() { if (IsOpen) Hide(); else Show(); }

        void Build()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = UiBuilder.NewCanvas(transform, "InventoryCanvas", 200);
            _root = new GameObject("Root", typeof(RectTransform));
            _root.transform.SetParent(_canvas.transform, false);
            UiBuilder.Stretch(_root.GetComponent<RectTransform>());

            // Scrim swallows clicks behind the panel and closes on background click.
            var scrim = UiBuilder.NewScrim(_root.transform);
            var scrimBtn = scrim.gameObject.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(Hide);

            int cap = _model?.Capacity ?? 36;
            int rows = (cap + COLUMNS - 1) / COLUMNS;
            float panelW = COLUMNS * SLOT + (COLUMNS - 1) * GAP + PAD * 2;
            float panelH = rows * SLOT + (rows - 1) * GAP + PAD * 2 + 56f;

            var panel = UiBuilder.NewPanel(_root.transform, "InvPanel", "inv_panel", UiBuilder.PanelBg);
            var pr = panel.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(panelW, panelH);

            // Title.
            var title = UiBuilder.NewText(panel.transform, "Title", "Inventory", 24, TextAnchor.UpperCenter);
            var tr = title.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = new Vector2(0f, -16f);
            tr.sizeDelta = new Vector2(0f, 36f);

            // Close (X) button.
            var close = UiBuilder.NewButton(panel.transform, "Close", "btn_close", "X", 18);
            close.onClick.AddListener(Hide);
            var cr = close.GetComponent<RectTransform>();
            cr.anchorMin = cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(1f, 1f);
            cr.anchoredPosition = new Vector2(-12f, -12f);
            cr.sizeDelta = new Vector2(40f, 40f);

            // Grid of slots.
            _icons = new Image[cap];
            _counts = new Text[cap];
            _highlights = new Image[cap];
            float gridX0 = -panelW * 0.5f + PAD + SLOT * 0.5f;
            float gridY0 =  panelH * 0.5f - PAD - 56f - SLOT * 0.5f;
            for (int i = 0; i < cap; i++)
            {
                int r = i / COLUMNS, c = i % COLUMNS;
                float x = gridX0 + c * (SLOT + GAP);
                float y = gridY0 - r * (SLOT + GAP);
                BuildSlot(panel.transform, i, x, y);
            }
        }

        void BuildSlot(Transform parent, int i, float x, float y)
        {
            var slot = UiBuilder.NewPanel(parent, "Slot" + i, "inv_slot", UiBuilder.SlotBg);
            var sr = slot.rectTransform;
            sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f);
            sr.pivot = new Vector2(0.5f, 0.5f);
            sr.anchoredPosition = new Vector2(x, y);
            sr.sizeDelta = new Vector2(SLOT, SLOT);

            var hi = UiBuilder.NewImage(sr, "Highlight", UiBuilder.Spr("slot_selected"), UiBuilder.Select);
            hi.raycastTarget = false;
            if (hi.sprite != null) hi.type = Image.Type.Sliced;
            else hi.color = new Color(UiBuilder.Select.r, UiBuilder.Select.g, UiBuilder.Select.b, 0.35f);
            UiBuilder.Stretch(hi.rectTransform, -3f);
            hi.gameObject.SetActive(false);
            _highlights[i] = hi;

            var icon = UiBuilder.NewImage(sr, "Icon", null, Color.white);
            icon.raycastTarget = false; icon.preserveAspect = true;
            UiBuilder.Stretch(icon.rectTransform, 8f);
            icon.enabled = false;
            _icons[i] = icon;

            var cnt = UiBuilder.NewText(sr, "Count", "", 14, TextAnchor.LowerRight);
            cnt.raycastTarget = false;
            UiBuilder.Stretch(cnt.rectTransform, 4f);
            _counts[i] = cnt;
        }

        void Refresh()
        {
            if (_model == null || _icons == null) return;
            for (int i = 0; i < _icons.Length; i++)
            {
                var s = _model.GetSlot(i);
                if (_icons[i] != null) { _icons[i].sprite = s.icon; _icons[i].enabled = s.icon != null; }
                if (_counts[i] != null) _counts[i].text = s.count > 1 ? s.count.ToString() : "";
                if (_highlights[i] != null) _highlights[i].gameObject.SetActive(s.selected);
            }
        }

        void HandleTextScaleChanged(float _)
        {
            if (_model == null)
                return;

            bool wasOpen = IsOpen;
            Unsubscribe();
            Build();
            Subscribe();
            Refresh();
            if (_root != null)
                _root.SetActive(wasOpen);
        }
    }
}
