using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    /// <summary>Model the inventory panel renders from. Foundation-free; an adapter binds it later.</summary>
    public interface IInventoryViewModel
    {
        int Capacity { get; }
        HudSlot GetSlot(int i);  // reuses HudSlot from GameUIController (label/count/icon/selected)

        // --- smart-inventory ops (2026-06-11). The placeholder implements all
        // of these in memory; the Foundation adapter maps whatever the
        // Foundation API exposes today and no-ops (returns false) for the rest
        // — see Docs/agent-comms/from-claude.md handoff.
        bool MoveSlot(int from, int to);      // move into an empty slot (merges same-item partials); false if blocked
        bool SwapSlots(int a, int b);         // exchange two slots (either side may be empty)
        bool SplitStack(int slot, int count); // peel `count` items off into the first empty slot
        bool DropItem(int slot, int count);   // drop to the world; false until the backend op exists
        void SortInventory();                 // merge partial stacks, then category → rarity → name

        event Action Changed;
    }

    /// <summary>Demo data so the panel previews standalone before the Foundation adapter
    /// binds. Implements the full smart-inventory op set in memory (move/swap/split/
    /// drop/sort) against a tiny demo catalog that carries category + rarity.</summary>
    public sealed class PlaceholderInventoryViewModel : IInventoryViewModel
    {
        struct DemoDef { public string id; public int category; public int rarity; public int maxStack; }

        // category mirrors ItemCategory ordering (Resource=0 … Misc=5); rarity is
        // placeholder-only until the Foundation ItemDefinition grows a rarity field.
        static readonly DemoDef[] Defs =
        {
            new DemoDef { id = "wood",        category = 0, rarity = 0, maxStack = 99 },
            new DemoDef { id = "stone",       category = 0, rarity = 0, maxStack = 99 },
            new DemoDef { id = "coal",        category = 0, rarity = 0, maxStack = 99 },
            new DemoDef { id = "copper_ore",  category = 0, rarity = 1, maxStack = 99 },
            new DemoDef { id = "iron_ore",    category = 0, rarity = 1, maxStack = 99 },
            new DemoDef { id = "ruby_common", category = 5, rarity = 2, maxStack = 20 },
        };

        struct Entry
        {
            public string id;
            public int count;
            public bool IsEmpty => string.IsNullOrEmpty(id) || count <= 0;
        }

        readonly Entry[] _slots;

        public PlaceholderInventoryViewModel(int capacity = 36)
        {
            _slots = new Entry[capacity];
            // deliberately unsorted, with duplicate partial stacks, so SORT has work to do
            Set(0, "iron_ore", 9); Set(1, "wood", 30); Set(2, "ruby_common", 3);
            Set(3, "stone", 12);   Set(4, "coal", 14); Set(5, "copper_ore", 17);
            Set(9, "wood", 8);     Set(12, "stone", 5); Set(15, "ruby_common", 2);
        }

        void Set(int i, string id, int count)
        {
            if (i >= 0 && i < _slots.Length)
                _slots[i] = new Entry { id = id, count = count };
        }

        static DemoDef DefOf(string id)
        {
            for (int i = 0; i < Defs.Length; i++)
                if (Defs[i].id == id) return Defs[i];
            return new DemoDef { id = id, category = 99, rarity = 0, maxStack = 99 };
        }

        bool Valid(int i) => i >= 0 && i < _slots.Length;

        int FirstEmpty()
        {
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].IsEmpty) return i;
            return -1;
        }

        public int Capacity => _slots.Length;
        public event Action Changed;
        public void Raise() => Changed?.Invoke();

        public HudSlot GetSlot(int i)
        {
            if (!Valid(i) || _slots[i].IsEmpty) return default;
            var e = _slots[i];
            return new HudSlot { label = e.id, count = e.count, icon = ItemIconResolver.Resolve(e.id) };
        }

        public bool MoveSlot(int from, int to)
        {
            if (!Valid(from) || !Valid(to) || from == to || _slots[from].IsEmpty) return false;
            if (_slots[to].IsEmpty)
            {
                _slots[to] = _slots[from];
                _slots[from] = default;
                Raise();
                return true;
            }
            if (_slots[to].id == _slots[from].id)
            {
                int max = Mathf.Max(1, DefOf(_slots[to].id).maxStack);
                int moved = Mathf.Min(max - _slots[to].count, _slots[from].count);
                if (moved <= 0) return false;
                _slots[to].count += moved;
                _slots[from].count -= moved;
                if (_slots[from].count <= 0) _slots[from] = default;
                Raise();
                return true;
            }
            return false;
        }

        public bool SwapSlots(int a, int b)
        {
            if (!Valid(a) || !Valid(b) || a == b) return false;
            if (_slots[a].IsEmpty && _slots[b].IsEmpty) return false;
            var tmp = _slots[a];
            _slots[a] = _slots[b];
            _slots[b] = tmp;
            Raise();
            return true;
        }

        public bool SplitStack(int slot, int count)
        {
            if (!Valid(slot) || _slots[slot].IsEmpty) return false;
            if (count <= 0 || count >= _slots[slot].count) return false;
            int empty = FirstEmpty();
            if (empty < 0) return false;
            _slots[empty] = new Entry { id = _slots[slot].id, count = count };
            _slots[slot].count -= count;
            Raise();
            return true;
        }

        public bool DropItem(int slot, int count)
        {
            if (!Valid(slot) || _slots[slot].IsEmpty || count <= 0) return false;
            int dropped = Mathf.Min(count, _slots[slot].count);
            string id = _slots[slot].id;
            _slots[slot].count -= dropped;
            if (_slots[slot].count <= 0) _slots[slot] = default;
            Debug.Log($"[Inventory] (placeholder) dropped {dropped} x {id} — world pickup pending the Foundation drop op");
            Raise();
            return true;
        }

        public void SortInventory()
        {
            // 1) merge partial stacks of the same item, 2) order category → rarity
            // (rarer first) → name, 3) re-deal into stacks respecting maxStack.
            var totals = new List<Entry>();
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsEmpty) { _slots[i] = default; continue; }
                bool merged = false;
                for (int t = 0; t < totals.Count; t++)
                {
                    if (totals[t].id != _slots[i].id) continue;
                    var e = totals[t];
                    e.count += _slots[i].count;
                    totals[t] = e;
                    merged = true;
                    break;
                }
                if (!merged) totals.Add(_slots[i]);
                _slots[i] = default;
            }

            totals.Sort(CompareEntries);

            int slotIdx = 0;
            for (int t = 0; t < totals.Count && slotIdx < _slots.Length; t++)
            {
                int max = Mathf.Max(1, DefOf(totals[t].id).maxStack);
                int remaining = totals[t].count;
                while (remaining > 0 && slotIdx < _slots.Length)
                {
                    int put = Mathf.Min(max, remaining);
                    _slots[slotIdx++] = new Entry { id = totals[t].id, count = put };
                    remaining -= put;
                }
            }
            Raise();
        }

        static int CompareEntries(Entry a, Entry b)
        {
            var da = DefOf(a.id);
            var db = DefOf(b.id);
            if (da.category != db.category) return da.category.CompareTo(db.category);
            if (da.rarity != db.rarity) return db.rarity.CompareTo(da.rarity); // rarer first within a category
            return string.Compare(a.id, b.id, StringComparison.OrdinalIgnoreCase);
        }
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
