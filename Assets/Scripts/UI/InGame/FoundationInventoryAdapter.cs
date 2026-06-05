using System;
using IsoCore.Foundation;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Maps Codex's <see cref="Inventory"/> + <see cref="FoundationContent"/> onto the
    /// inventory panel's view model. Mirrors the slot-resolution rules used by the
    /// HUD adapter (display name from ItemDefinition.Display, icon via
    /// <see cref="ItemIconResolver"/>), but exposes the FULL inventory capacity rather
    /// than just the hotbar window.
    /// </summary>
    public sealed class FoundationInventoryAdapter : IInventoryViewModel, IDisposable
    {
        readonly Inventory _inv;
        readonly FoundationContent _content;

        public event Action Changed;

        public FoundationInventoryAdapter(Inventory inv, FoundationContent content)
        {
            _inv = inv;
            _content = content;
            if (_inv != null) _inv.OnChanged += OnChanged;
        }

        public void Dispose()
        {
            if (_inv != null) _inv.OnChanged -= OnChanged;
        }

        void OnChanged() => Changed?.Invoke();

        public int Capacity => _inv?.SlotCount ?? 0;

        public HudSlot GetSlot(int i)
        {
            if (_inv == null) return default;
            var st = _inv.GetSlot(i);
            bool empty = string.IsNullOrEmpty(st.itemId);
            string label = empty ? "" : st.itemId;
            if (!empty && _content != null)
            {
                var def = _content.Items.Get(st.itemId);
                if (def != null && !string.IsNullOrEmpty(def.Display)) label = def.Display;
            }
            return new HudSlot
            {
                label    = label,
                count    = empty ? 0 : st.count,
                icon     = empty ? null : ItemIconResolver.Resolve(st.itemId),
                selected = false,
            };
        }
    }
}
