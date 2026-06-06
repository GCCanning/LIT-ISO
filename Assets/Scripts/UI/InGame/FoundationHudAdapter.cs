using System;
using IsoCore.Foundation;
using UnityEngine;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Maps Codex's runtime Foundation systems (Inventory + Hotbar + Content) onto the
    /// View's <see cref="IGameHudModel"/> so the uGUI HUD renders live data.
    ///
    /// Lives in Assembly-CSharp: the Foundation assembly is forbidden from referencing
    /// us, so the binding flows our way — we adapt Foundation, never the other way.
    ///
    /// Vitals (HP/MP/XP/Level) are now wired to PlayerHealth, PlayerMana, and XPSystem
    /// singletons (all in Assembly-CSharp). The adapter subscribes to their events and
    /// raises Changed so the HUD refreshes in real time.
    ///
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

            // Subscribe to live stat sources so the HUD refreshes on every change.
            if (PlayerHealth.Instance != null) PlayerHealth.Instance.OnHealthChanged += OnVitalsChanged;
            if (PlayerMana.Instance   != null) PlayerMana.Instance.OnManaChanged     += OnVitalsChanged;
            PlayerStats.OnStatsChanged += OnChanged;
            XPSystem.OnXPGained       += OnXpGained;
            XPSystem.OnLevelUp        += OnLevelUp;
        }

        public void Dispose()
        {
            if (_inv != null)    _inv.OnChanged -= OnChanged;
            if (_hotbar != null) _hotbar.OnSelectionChanged -= OnChanged;

            if (PlayerHealth.Instance != null) PlayerHealth.Instance.OnHealthChanged -= OnVitalsChanged;
            if (PlayerMana.Instance   != null) PlayerMana.Instance.OnManaChanged     -= OnVitalsChanged;
            PlayerStats.OnStatsChanged -= OnChanged;
            XPSystem.OnXPGained       -= OnXpGained;
            XPSystem.OnLevelUp        -= OnLevelUp;
        }

        void OnChanged()                        => Changed?.Invoke();
        void OnVitalsChanged(int _a, int _b)    => Changed?.Invoke();
        void OnXpGained(int _gained, int _total) => Changed?.Invoke();
        void OnLevelUp(int _newLevel)            => Changed?.Invoke();

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

        // ---- IGameHudModel: vitals ------------------------------------------

        public float Health01
        {
            get
            {
                var ph = PlayerHealth.Instance;
                if (ph == null || ph.maxHealth <= 0) return 1f;
                return Mathf.Clamp01((float)ph.CurrentHealth / ph.maxHealth);
            }
        }

        public float Mana01
        {
            get
            {
                var pm = PlayerMana.Instance;
                if (pm == null || pm.MaxMana <= 0) return 1f;
                return Mathf.Clamp01((float)pm.CurrentMana / pm.MaxMana);
            }
        }

        public float Hunger01 => 0f;   // hidden by default — survival scope not yet active

        public float Xp01
        {
            get
            {
                var xp = XPSystem.Instance;
                if (xp == null) return 0f;
                int needed = xp.XPNeededForNextLevel;
                if (needed <= 0) return 1f;
                return Mathf.Clamp01((float)xp.XPInCurrentLevel / needed);
            }
        }

        public int Level => XPSystem.Instance != null ? XPSystem.Instance.CurrentLevel : 1;
    }
}
