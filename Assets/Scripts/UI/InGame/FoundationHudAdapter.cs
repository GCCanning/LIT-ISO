using System;
using IsoCore.Foundation;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Maps Codex's runtime Foundation systems (Inventory + Hotbar + Content) onto the
    /// View's <see cref="IGameHudModel"/> so the uGUI HUD renders live data.
    ///
    /// Lives in Assembly-CSharp: the Foundation assembly is forbidden from referencing
    /// us, so the binding flows our way — we adapt Foundation, never the other way.
    ///
    /// HP/MP/XP/Level/Class/Title are placeholder values until Codex's character/stats
    /// system lands (LitRPG scope: STR/DEX/INT/VIT/DEF/LUCK + Class + Title + HP/MP/XP).
    /// When that exposes its source, the only change here is reading from it.
    /// Hunger01 stays in the contract for the (deferred) survival scope; the View hides
    /// the hunger bar by default — see <c>GameUIController.showHungerBar</c>.
    /// </summary>
    public sealed class FoundationHudAdapter : IGameHudModel, IDisposable
    {
        readonly Inventory _inv;
        readonly Hotbar _hotbar;
        readonly FoundationContent _content;

        public event Action Changed;

        public FoundationHudAdapter(Inventory inv, Hotbar hotbar, FoundationContent content)
        {
            _inv = inv;
            _hotbar = hotbar;
            _content = content;

            ItemIconResolver.Bind(content);

            if (_inv != null)    _inv.OnChanged += OnChanged;
            if (_hotbar != null) _hotbar.OnSelectionChanged += OnChanged;
        }

        public void Dispose()
        {
            if (_inv != null)    _inv.OnChanged -= OnChanged;
            if (_hotbar != null) _hotbar.OnSelectionChanged -= OnChanged;
        }

        void OnChanged() => Changed?.Invoke();

        // ---- IGameHudModel: slot data ---------------------------------------

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
                if (def != null && !string.IsNullOrEmpty(def.Display)) label = def.Display;
            }

            return new HudSlot
            {
                label    = label,
                count    = empty ? 0 : st.count,
                icon     = empty ? null : ItemIconResolver.Resolve(itemId),
                selected = _hotbar != null && i == _hotbar.Selected,
            };
        }

        // ---- IGameHudModel: vitals (placeholder until LitRPG stats land) ----

        // These are static placeholders so the bars render and look right. When Codex
        // exposes a stats source, swap the four lines below for live values. The View
        // re-Refreshes whenever Inventory/Hotbar Changed fires, and the future stats
        // source can raise the same Changed event when needed.
        public float Health01 => 0.85f;
        public float Mana01   => 0.65f;
        public float Hunger01 => 0.0f;  // hidden by default (LitRPG scope)
        public float Xp01     => 0.30f;
        public int   Level    => 1;
    }
}
