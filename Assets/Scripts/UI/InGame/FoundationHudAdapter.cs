using System;
using IsoCore.Foundation;
using UnityEngine;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Maps the canonical Foundation runtime onto the uGUI HUD model.
    /// Placeholder vitals are returned only when a test scene supplies no stats handle.
    /// </summary>
    public sealed class FoundationHudAdapter : IGameHudModel, IDisposable
    {
        readonly Inventory _inv;
        readonly Hotbar _hotbar;
        readonly FoundationContent _content;
        readonly FoundationPlayerStats _stats;

        public event Action Changed;

        public FoundationHudAdapter(Inventory inv, Hotbar hotbar, FoundationContent content,
            FoundationPlayerStats stats = null)
        {
            _inv = inv;
            _hotbar = hotbar;
            _content = content;
            _stats = stats;

            ItemIconResolver.Bind(content);

            if (_inv != null) _inv.OnChanged += OnChanged;
            if (_hotbar != null) _hotbar.OnSelectionChanged += OnChanged;
            if (_stats != null) _stats.Changed += OnChanged;
        }

        public void Dispose()
        {
            if (_inv != null) _inv.OnChanged -= OnChanged;
            if (_hotbar != null) _hotbar.OnSelectionChanged -= OnChanged;
            if (_stats != null) _stats.Changed -= OnChanged;
        }

        void OnChanged() => Changed?.Invoke();

        public int SlotCount => _hotbar?.Size ?? 0;

        public HudSlot GetSlot(int i)
        {
            if (_inv == null) return default;
            var st = _inv.GetSlot(i);
            string itemId = st.itemId;
            bool empty = string.IsNullOrEmpty(itemId);

            string label = empty ? "" : itemId;
            if (!empty && _content != null)
            {
                var def = _content.Items.Get(itemId);
                if (def != null && !string.IsNullOrEmpty(def.Display))
                    label = def.Display;
            }

            return new HudSlot
            {
                label = label,
                count = empty ? 0 : st.count,
                icon = empty ? null : ItemIconResolver.Resolve(itemId),
                selected = _hotbar != null && i == _hotbar.Selected,
                durability01 = empty || _content == null ? 0f : Durability01(st),
            };
        }

        float Durability01(ItemStack stack)
        {
            var def = _content?.Items.Get(stack.itemId);
            if (def == null || !def.HasDurability)
                return 0f;
            return Mathf.Clamp01(stack.durability / (float)Mathf.Max(1, def.maxDurability));
        }

        public float Health01 => _stats != null ? _stats.Health01 : 1f;
        public float Mana01 => _stats != null ? _stats.Mana01 : 1f;
        public float Hunger01 => 0f;
        public float Xp01 => _stats != null ? _stats.Xp01 : 0f;
        public int Level => _stats != null ? _stats.Level : 1;
    }
}
